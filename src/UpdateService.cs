using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Principal;
using System.Security.AccessControl;

namespace ScumMiniMap {
    internal sealed class UpdateCheckSchedule {
        DateTime nextCheckUtc;
        internal UpdateCheckSchedule(DateTime now) { RecordCheck(now); }
        internal void RecordCheck(DateTime now) { nextCheckUtc=now.AddHours(1); }
        internal bool TryBegin(DateTime now,bool allowed) {
            if(!allowed || now<nextCheckUtc)return false;
            RecordCheck(now);return true;
        }
    }
    public sealed class UpdateRelease {
        public Version Version { get; private set; }
        public string Sha256 { get; private set; }
        public string PackageSha256 { get; private set; }
        public bool IsPackage { get { return PackageSha256!=null; } }
        public string VersionText { get { return Version.ToString(3); } }
        public string Filename { get { return "v" + VersionText + " - SkynettMiniMap.exe"; } }

        public static UpdateRelease Parse(string manifest) {
            if(string.IsNullOrWhiteSpace(manifest) || manifest.Length > 4096) throw new FormatException("Invalid update manifest.");
            var values = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string raw in manifest.TrimStart('\uFEFF').Split('\n')) {
                string line = raw.Trim();
                if(line.Length == 0) continue;
                int split = line.IndexOf('=');
                if(split < 1 || values.ContainsKey(line.Substring(0,split))) throw new FormatException("Invalid update manifest field.");
                values.Add(line.Substring(0,split), line.Substring(split+1));
            }
            string schema, version, hash, packageHash=null;
            Version parsed;
            if(!values.TryGetValue("schema",out schema) || (schema!="1" && schema!="2") ||
               values.Count!=(schema=="2"?4:3) ||
               (schema=="2" && (!values.TryGetValue("package-sha256",out packageHash) || !Regex.IsMatch(packageHash,@"\A[0-9a-fA-F]{64}\z"))) ||
               !values.TryGetValue("version",out version) || !Regex.IsMatch(version,@"\A(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\z") ||
               !System.Version.TryParse(version + ".0",out parsed) ||
               !values.TryGetValue("sha256",out hash) || !Regex.IsMatch(hash,@"\A[0-9a-fA-F]{64}\z"))
                throw new FormatException("The update manifest does not describe a valid stable release.");
            return new UpdateRelease { Version=parsed, Sha256=hash.ToLowerInvariant(),PackageSha256=packageHash==null?null:packageHash.ToLowerInvariant() };
        }
        public void VerifyFile(string path) {
            string actual;
            using(var sha=SHA256.Create()) using(var stream=File.OpenRead(path))
                actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(!string.Equals(actual,Sha256,StringComparison.Ordinal)) throw new InvalidDataException("Update checksum mismatch. The download was discarded.");
        }
    }

    public sealed class UpdateService {
        // Set to the release repository before distributing a build. No credentials are shipped.
        public const string Repository = "Mike-Rafone-SCUM/MiniMap";
        readonly string repository;
        readonly string cacheDirectory;
        readonly Func<Uri,int,byte[]> fetch;
        public UpdateService(string repo, string cache, Func<Uri,int,byte[]> transport) {
            if(!Regex.IsMatch(repo ?? "",@"\A[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?/[A-Za-z0-9][A-Za-z0-9._-]*\z"))
                throw new InvalidOperationException("The GitHub update repository has not been configured.");
            repository=repo;
            cacheDirectory=cache;
            fetch=transport ?? Fetch;
        }
        public Uri ManifestUri { get { return new Uri("https://github.com/"+repository+"/releases/latest/download/update.txt"); } }
        public Uri DownloadUri(UpdateRelease release) {
            return new Uri("https://github.com/"+repository+"/releases/download/v"+release.VersionText+"/"+(release.IsPackage?"SkynettMiniMap.zip":"SkynettMiniMap.exe"));
        }
        public UpdateRelease Check(bool manual) {
            // Cache is scoped to this repository; never use another repository's manifest.
            string cachePath=Path.Combine(cacheDirectory,repository.Replace('/','_')+"-release.txt");
            string retryPath=Path.Combine(cacheDirectory,repository.Replace('/','_')+"-retry-after.txt");
            try {
                DateTime retry;
                if(File.Exists(retryPath) && DateTime.TryParse(File.ReadAllText(retryPath),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out retry) && retry.ToUniversalTime()>DateTime.UtcNow)
                    throw new UpdateRetryException(retry.ToUniversalTime());
                TimeSpan age=DateTime.UtcNow-File.GetLastWriteTimeUtc(cachePath);
                if(File.Exists(cachePath) && age>=TimeSpan.Zero && age<TimeSpan.FromMinutes(manual?1:60))
                    return UpdateRelease.Parse(File.ReadAllText(cachePath));
            } catch(UpdateRetryException) { throw; }
              catch(IOException) {} catch(UnauthorizedAccessException) {} catch(FormatException) {}
            try {
                string manifest=Encoding.UTF8.GetString(fetch(ManifestUri,4096));
                UpdateRelease release=UpdateRelease.Parse(manifest);
                TrySave(cachePath,manifest);
                return release;
            } catch(WebException ex) {
                var response=ex.Response as HttpWebResponse;
                if(response!=null) using(response) {
                    if((int)response.StatusCode==429 || (int)response.StatusCode==503 ||
                       ((int)response.StatusCode==403 && response.Headers["Retry-After"]!=null)) {
                        DateTime retry=RetryAfter(response.Headers["Retry-After"],DateTime.UtcNow);
                        TrySave(retryPath,retry.ToString("o",CultureInfo.InvariantCulture));
                        throw new UpdateRetryException(retry);
                    }
                }
                throw;
            }
        }
        public UpdateRelease CheckVersion(string version) {
            // A downloaded older release remains verifiable after a newer one becomes latest.
            Version parsed;
            if(!Regex.IsMatch(version??"",@"\A(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\z") ||
                !Version.TryParse(version+".0",out parsed))throw new FormatException("Invalid pinned update version.");
            var uri=new Uri("https://github.com/"+repository+"/releases/download/v"+version+"/update.txt");
            var release=UpdateRelease.Parse(Encoding.UTF8.GetString(fetch(uri,4096)));
            if(release.Version!=parsed)throw new InvalidDataException("The pinned update manifest describes a different version.");
            return release;
        }
        public static DateTime RetryAfter(string value, DateTime now) {
            double seconds;
            DateTime date;
            if(double.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out seconds) && seconds>=0 && seconds<=86400)
                return now.AddSeconds(Math.Max(60,seconds));
            if(DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out date) && date>now)
                return date;
            return now.AddMinutes(5);
        }
        public void Download(UpdateRelease release, string destination) {
            // Local cache is a notification hint, never authority for executable bytes.
            var authoritative=CheckVersion(release.VersionText);
            if(authoritative.Sha256!=release.Sha256 || authoritative.PackageSha256!=release.PackageSha256)
                throw new InvalidDataException("The release identity changed. Check for updates again.");
            if(release.IsPackage) {
                byte[] package=fetch(DownloadUri(release),128*1024*1024);
                using(var stream=new MemoryStream(package,false))if(InstallationAssets.Digest(stream)!=release.PackageSha256)throw new InvalidDataException("Update package checksum mismatch.");
                UpdatePackage.Extract(package,destination,release);return;
            }
            // The caller chooses a new file. Never overwrite a running installation or existing file.
            string staging=destination+"."+Guid.NewGuid().ToString("N")+".partial";
            try {
                byte[] bytes=fetch(DownloadUri(release),128*1024*1024);
                using(var stream=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None)) stream.Write(bytes,0,bytes.Length);
                release.VerifyFile(staging);
                if(bytes.Length<2 || bytes[0]!='M' || bytes[1]!='Z') throw new InvalidDataException("The update is not a Windows executable.");
                File.Move(staging,destination);
            } finally { if(File.Exists(staging)) File.Delete(staging); }
        }
        static void TrySave(string path,string text) {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,text); }
            catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        internal static bool AllowedDownloadUri(Uri uri,bool steam) {
            if(uri==null || !uri.IsAbsoluteUri || uri.Scheme!=Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo!="")return false;
            string host=uri.DnsSafeHost.ToLowerInvariant();
            return steam?host=="steamcommunity.com":host=="github.com" || host=="release-assets.githubusercontent.com" || host=="objects.githubusercontent.com" || host=="github-releases.githubusercontent.com";
        }
        static byte[] Fetch(Uri uri,int limit) { return FetchBounded(uri,limit,false); }
        internal static byte[] FetchBounded(Uri uri,int limit,bool steam) {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            for(int redirect=0;redirect<=5;redirect++) {
            if(!AllowedDownloadUri(uri,steam))throw new InvalidDataException("Unapproved download endpoint.");
            var request=(HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent="SCUM-MiniMap/"+MapWindow.VersionString;
            request.Timeout=15000;
            request.ReadWriteTimeout=30000;
            request.AllowAutoRedirect=false;
            using(var response=(HttpWebResponse)request.GetResponse()) {
                int status=(int)response.StatusCode;
                if(status==301 || status==302 || status==303 || status==307 || status==308) {
                    string location=response.Headers["Location"];
                    if(redirect==5 || string.IsNullOrWhiteSpace(location))throw new InvalidDataException("Invalid download redirect.");
                    uri=new Uri(uri,location);continue;
                }
                if(status!=200)throw new InvalidDataException("Unexpected download response.");
                if(response.ContentLength>limit) throw new InvalidDataException("Update response exceeds the size limit.");
                using(var input=response.GetResponseStream()) using(var output=new MemoryStream()) {
                    byte[] buffer=new byte[32768];
                    int count;
                    while((count=input.Read(buffer,0,buffer.Length))>0) {
                        if(output.Length+count>limit) throw new InvalidDataException("Update response exceeds the size limit.");
                        output.Write(buffer,0,count);
                    }
                    return output.ToArray();
                }
            }
            }
            throw new InvalidDataException("Too many download redirects.");
        }
    }
    internal sealed class UpdateProcessIdentity {
        internal readonly int ProcessId;
        internal readonly long StartTimeUtcTicks;
        internal readonly string ExecutablePath;
        internal UpdateProcessIdentity(int processId,long startTimeUtcTicks,string executablePath) {
            if(processId<=0 || startTimeUtcTicks<=0)throw new ArgumentException("Invalid update process identity.");
            ProcessId=processId;StartTimeUtcTicks=startTimeUtcTicks;ExecutablePath=Path.GetFullPath(executablePath);
        }
    }
    internal interface IUpdateProcess:IDisposable {
        int Id { get; }
        long StartTimeUtcTicks { get; }
        string ExecutablePath { get; }
        bool HasExited { get; }
        bool CloseMainWindow();
        bool WaitForExit(int milliseconds);
        void Kill();
    }
    public static class UpdateInstaller {
        internal static DirectorySecurity HelperDirectorySecurity(bool elevated,SecurityIdentifier user) {
            var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
            var administrators=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
            security.SetOwner(elevated?administrators:user);
            foreach(var sid in elevated?new[]{administrators,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)}:
                new[]{user,administrators,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
                security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            return security;
        }
        sealed class OwnedProcess:IUpdateProcess {
            readonly Process process;
            internal OwnedProcess(int id) {
                process=Process.GetProcessById(id);
                try {
                    // Pin the OS process handle. PID reuse cannot redirect a later close/kill
                    // after the identity check while this exact handle remains owned.
                    if(process.Handle==IntPtr.Zero)throw new IOException("Unable to acquire the approved MiniMap process handle.");
                }catch { process.Dispose();throw; }
            }
            public int Id { get { return process.Id; } }
            public long StartTimeUtcTicks { get { return process.StartTime.ToUniversalTime().Ticks; } }
            public string ExecutablePath { get { return process.MainModule.FileName; } }
            public bool HasExited { get { return process.HasExited; } }
            public bool CloseMainWindow() { return process.CloseMainWindow(); }
            public bool WaitForExit(int milliseconds) { return process.WaitForExit(milliseconds); }
            public void Kill() { process.Kill(); }
            public void Dispose() { process.Dispose(); }
        }
        internal static void CloseApprovedProcess(UpdateProcessIdentity approved,Func<int,IUpdateProcess> acquire,int graceMilliseconds,int forceWaitMilliseconds) {
            if(approved==null)throw new ArgumentNullException("approved");
            if(acquire==null)throw new ArgumentNullException("acquire");
            if(graceMilliseconds<0 || forceWaitMilliseconds<1)throw new ArgumentOutOfRangeException("graceMilliseconds");
            IUpdateProcess process;
            try { process=acquire(approved.ProcessId); }catch(ArgumentException) { return; }
            using(process) {
                if(process.HasExited)return;
                ValidateProcess(approved,process);
                // Leave time for at least one graceful close request even with a short budget.
                int first=Math.Min(500,Math.Max(0,graceMilliseconds-1));
                if(first>0 && process.WaitForExit(first))return;
                int remaining=graceMilliseconds-first;
                while(remaining>0) {
                    if(process.HasExited)return;
                    ValidateProcess(approved,process);process.CloseMainWindow();
                    int wait=Math.Min(250,remaining);remaining-=wait;
                    if(process.WaitForExit(wait))return;
                }
                if(process.HasExited)return;
                ValidateProcess(approved,process);
                process.Kill();
                if(!process.WaitForExit(forceWaitMilliseconds))throw new IOException("The approved MiniMap process did not exit; update cancelled.");
            }
        }
        static void ValidateProcess(UpdateProcessIdentity approved,IUpdateProcess actual) {
            if(actual.Id!=approved.ProcessId || actual.StartTimeUtcTicks!=approved.StartTimeUtcTicks ||
                !string.Equals(Path.GetFullPath(actual.ExecutablePath),approved.ExecutablePath,StringComparison.OrdinalIgnoreCase))
                throw new IOException("The running MiniMap process identity changed; update cancelled.");
        }
        internal static bool IsElevated() {
            using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        internal static ProcessStartInfo CreateHelperStartInfo(string helper,string config,bool elevated) {
            return new ProcessStartInfo {
                FileName=helper,Arguments="-ApplyVerifiedUpdate "+Convert.ToBase64String(Encoding.UTF8.GetBytes(config)),
                UseShellExecute=!elevated,Verb=elevated?"":"runas",CreateNoWindow=elevated,WindowStyle=ProcessWindowStyle.Hidden,
                WorkingDirectory=Path.GetDirectoryName(helper)
            };
        }
        // The helper is a private copy of this executable. Paths are binary data, never shell code.
        public static void Start(string staged,string installed,string statusPath) {
            UpdateProcessIdentity parent;
            using(var current=Process.GetCurrentProcess())parent=new UpdateProcessIdentity(current.Id,current.StartTime.ToUniversalTime().Ticks,current.MainModule.FileName);
            StartForTarget(staged,installed,statusPath,parent);
        }
        internal static void StartForTarget(string staged,string installed,string statusPath,UpdateProcessIdentity parent) {
            StartForTarget(staged,installed,statusPath,parent,IsElevated(),Process.Start);
        }
        internal static void StartForTarget(string staged,string installed,string statusPath,UpdateProcessIdentity parent,bool elevated,Func<ProcessStartInfo,Process> launch) {
            StartForTarget(staged,installed,statusPath,parent,elevated,launch,UpdateBootstrap.ReadStableVersion,
                System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        internal static void StartForTarget(string staged,string installed,string statusPath,UpdateProcessIdentity parent,bool elevated,Func<ProcessStartInfo,Process> launch,Func<string,Version> readVersion,string helperSource) {
            staged=Path.GetFullPath(staged);installed=Path.GetFullPath(installed);statusPath=Path.GetFullPath(statusPath);
            if(string.Equals(staged,installed,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The staged update must be a separate file.");
            if(launch==null || readVersion==null)throw new ArgumentNullException("launch");
            if(readVersion(staged)<=readVersion(installed))throw new InvalidDataException("The replacement must be a newer stable MiniMap release.");
            if(parent!=null && !string.Equals(parent.ExecutablePath,installed,StringComparison.OrdinalIgnoreCase))
                throw new IOException("The approved MiniMap path does not match the installation.");
            using(var requiredAssets=typeof(InstallationAssets).Assembly.GetManifestResourceStream("installation-assets.txt"))
                if(requiredAssets!=null && UpdatePackage.AssetIdentity(staged)=="")throw new InvalidDataException("The update is missing its installation assets. Use the complete ZIP package.");
            string stagedHash=Hash(staged),installedHash=Hash(installed);
            string directory=Path.Combine(Path.GetTempPath(),"ScumMiniMap-update-"+Guid.NewGuid().ToString("N"));
            using(var identity=WindowsIdentity.GetCurrent())
                Directory.CreateDirectory(directory,HelperDirectorySecurity(IsElevated(),identity.User));
            string helper=Path.Combine(directory,"Updater.exe"), config=Path.Combine(directory,"request.bin");
            bool committed=false;
            try {
                File.Copy(helperSource,helper);
                Directory.CreateDirectory(Path.GetDirectoryName(statusPath));File.WriteAllText(statusPath+".helper",helper);
                using(var writer=new BinaryWriter(File.Create(config))) {
                    writer.Write("SCUM-MiniMap-update-v3");
                    writer.Write(parent==null?0:parent.ProcessId);writer.Write(parent==null?0:parent.StartTimeUtcTicks);
                    writer.Write(staged);writer.Write(installed);writer.Write(statusPath);writer.Write(stagedHash);writer.Write(installedHash);
                    writer.Write(Hash(helper));
                    writer.Write(UpdatePackage.AssetIdentity(staged));
                }
                var process=launch(CreateHelperStartInfo(helper,config,elevated));
                if(process==null)throw new IOException("Unable to start the update helper.");
                using(process) {
                    DateTime deadline=DateTime.UtcNow.AddSeconds(10);
                    while(!File.Exists(config+".ready")) {
                        if(process.WaitForExit(25) || DateTime.UtcNow>=deadline)
                            throw new IOException("The update helper did not become ready; the current application will stay open.");
                    }
                    // A helper must not close the app unless the caller has observed readiness.
                    File.WriteAllText(config+".commit","approved");committed=true;
                }
            }catch(Win32Exception ex) {
                if(ex.NativeErrorCode==1223)throw new IOException("Administrator permission was cancelled; the current MiniMap stays open.",ex);
                throw;
            }finally {
                if(!committed) {
                    try { File.WriteAllText(config+".cancel","cancelled"); }catch(IOException){}
                    CleanupHelper(statusPath);
                }
            }
        }
        static string Hash(string path) {
            using(var sha=SHA256.Create()) using(var file=File.OpenRead(path)) return Convert.ToBase64String(sha.ComputeHash(file));
        }
        public static bool CleanupHelper(string statusPath) {
            string marker=statusPath+".helper";
            if(!File.Exists(marker)) return true;
            try {
                string helper=Path.GetFullPath(File.ReadAllText(marker));
                string directory=Path.GetDirectoryName(helper);
                string temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
                if(Path.GetFileName(helper)!="Updater.exe" ||
                   !string.Equals(Path.GetDirectoryName(directory),temp,StringComparison.OrdinalIgnoreCase) ||
                   !Regex.IsMatch(Path.GetFileName(directory),@"\AScumMiniMap-update-[0-9a-f]{32}\z")) return false;
                // Delete only our known files, never recursively remove a supplied path.
                File.Delete(helper);
                File.Delete(Path.Combine(directory,"request.bin"));
                File.Delete(Path.Combine(directory,"request.bin.ready"));
                File.Delete(Path.Combine(directory,"request.bin.commit"));
                File.Delete(Path.Combine(directory,"request.bin.cancel"));
                if(Directory.Exists(directory)) Directory.Delete(directory,false);
                File.Delete(marker);
                return true;
            } catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
              catch(ArgumentException) { return false; }
        }
        public static void ReplaceAndLaunch(string staged,string installed,string expectedHash,Action<string> launch) {
            if(!string.Equals(Hash(staged),expectedHash,StringComparison.Ordinal)) throw new InvalidDataException("Staged update checksum changed.");
            string backup=installed+"."+Guid.NewGuid().ToString("N")+".previous";
            bool replaced=false;
            try {
                File.Replace(staged,installed,backup); replaced=true;
                launch(installed);
            } catch {
                if(replaced) File.Replace(backup,installed,null);
                throw;
            }
            // A backup cleanup failure must not roll back a successfully launched update.
            try { if(File.Exists(backup)) File.Delete(backup); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        public static void Run(string encodedConfig) {
            string status=null, installed=null, config=null,oldHash=null;bool parentExited=false,installedLaunched=false;
            try {
                config=Encoding.UTF8.GetString(Convert.FromBase64String(encodedConfig));
                string executing=Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string directory=Path.GetDirectoryName(executing),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
                if(Path.GetFileName(executing)!="Updater.exe" ||
                    !string.Equals(Path.GetDirectoryName(directory),temp,StringComparison.OrdinalIgnoreCase) ||
                    !Regex.IsMatch(Path.GetFileName(directory),@"\AScumMiniMap-update-[0-9a-f]{32}\z") ||
                    !string.Equals(Path.GetFullPath(config),Path.Combine(directory,"request.bin"),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Invalid update-helper location.");
                int pid;long ticks;string staged,hash,helperHash,assetIdentity;
                if(new FileInfo(config).Length>16384)throw new InvalidDataException("The update-helper request is too large.");
                using(var reader=new BinaryReader(File.OpenRead(config))) {
                    if(reader.ReadString()!="SCUM-MiniMap-update-v3")throw new InvalidDataException("Invalid update-helper request.");
                    pid=reader.ReadInt32(); ticks=reader.ReadInt64(); staged=reader.ReadString();
                    installed=reader.ReadString();status=reader.ReadString();hash=reader.ReadString();oldHash=reader.ReadString();helperHash=reader.ReadString();assetIdentity=reader.ReadString();
                    if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Unexpected update-helper request data.");
                }
                if(pid<0 || (pid==0? ticks!=0:ticks<=0))throw new InvalidDataException("Invalid update parent identity.");
                if(Hash(executing)!=helperHash || Hash(staged)!=hash || Hash(installed)!=oldHash)
                    throw new InvalidDataException("Update files changed before installation.");
                if(UpdatePackage.AssetIdentity(staged)!=assetIdentity)throw new InvalidDataException("Update assets changed before installation.");
                if(UpdateBootstrap.ReadStableVersion(staged)<=UpdateBootstrap.ReadStableVersion(installed))
                    throw new InvalidDataException("The replacement must be a newer stable MiniMap release.");
                File.WriteAllText(status,"Update pending: preparing to close MiniMap.");
                File.WriteAllText(config+".ready","ready");
                DateTime deadline=DateTime.UtcNow.AddSeconds(15);
                while(!File.Exists(config+".commit")) {
                    if(File.Exists(config+".cancel") || DateTime.UtcNow>=deadline)throw new IOException("The update was not approved; MiniMap stays open.");
                    System.Threading.Thread.Sleep(25);
                }
                if(pid!=0)CloseApprovedProcess(new UpdateProcessIdentity(pid,ticks,installed),id=>new OwnedProcess(id),10000,5000);
                parentExited=true;
                if(Hash(installed)!=oldHash)throw new InvalidDataException("The installed MiniMap changed while closing.");
                UpdatePackage.ReplaceAndLaunch(staged,installed,hash,assetIdentity,path=> {
                    Launch(path);installedLaunched=true;
                });
                // Reporting cannot roll back or launch again after a successful restart.
                try { File.WriteAllText(status,"Update installed successfully."); }catch(IOException){}catch(UnauthorizedAccessException){}
            } catch(Exception ex) {
                if(status!=null) try { File.WriteAllText(status,"Update failed. Check the installation and any .previous backup before retrying. "+ex.Message); } catch {}
                if(parentExited && !installedLaunched && installed!=null && oldHash!=null && File.Exists(installed))try {
                    if(Hash(installed)==oldHash) { UpdateBootstrap.ReadStableVersion(installed);Launch(installed); }
                }catch{}
            } finally {
                if(config!=null) try { File.Delete(config);File.Delete(config+".ready");File.Delete(config+".commit");File.Delete(config+".cancel"); }catch{}
            }
        }
        static void Launch(string path) {
            var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName=path,UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)
            });
            if(process==null) throw new IOException("Unable to restart the application.");
            process.Dispose();
        }
    }
    public sealed class UpdateRetryException:Exception {
        public DateTime RetryAt { get; private set; }
        public UpdateRetryException(DateTime retry):base("Update service is temporarily unavailable.") { RetryAt=retry; }
    }
}
