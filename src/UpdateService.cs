using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ScumMiniMap {
    public sealed class UpdateRelease {
        public Version Version { get; private set; }
        public string Sha256 { get; private set; }
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
            string schema, version, hash;
            Version parsed;
            if(values.Count != 3 || !values.TryGetValue("schema",out schema) || schema != "1" ||
               !values.TryGetValue("version",out version) || !Regex.IsMatch(version,@"\A(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\z") ||
               !System.Version.TryParse(version + ".0",out parsed) ||
               !values.TryGetValue("sha256",out hash) || !Regex.IsMatch(hash,@"\A[0-9a-fA-F]{64}\z"))
                throw new FormatException("The update manifest does not describe a valid stable release.");
            return new UpdateRelease { Version=parsed, Sha256=hash.ToLowerInvariant() };
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
            return new Uri("https://github.com/"+repository+"/releases/download/v"+release.VersionText+"/SkynettMiniMap.exe");
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
        static byte[] Fetch(Uri uri,int limit) {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent="SCUM-MiniMap/"+MapWindow.VersionString;
            request.Timeout=15000;
            request.ReadWriteTimeout=30000;
            request.MaximumAutomaticRedirections=5;
            using(var response=(HttpWebResponse)request.GetResponse()) {
                if(response.ResponseUri.Scheme!=Uri.UriSchemeHttps) throw new InvalidDataException("Update downloads require HTTPS.");
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
    }
    public static class UpdateInstaller {
        // The helper is a private copy of this executable. Paths are binary data, never shell code.
        public static void Start(string staged,string installed,string statusPath) {
            string directory=Path.Combine(Path.GetTempPath(),"ScumMiniMap-update-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string helper=Path.Combine(directory,"Updater.exe"), config=Path.Combine(directory,"request.bin");
            File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,helper);
            File.WriteAllText(statusPath+".helper",helper);
            using(var writer=new BinaryWriter(File.Create(config))) {
                writer.Write(System.Diagnostics.Process.GetCurrentProcess().Id);
                writer.Write(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks);
                writer.Write(Path.GetFullPath(staged)); writer.Write(Path.GetFullPath(installed));
                writer.Write(Path.GetFullPath(statusPath)); writer.Write(Hash(staged));
            }
            var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName=helper, Arguments="-ApplyVerifiedUpdate "+Convert.ToBase64String(Encoding.UTF8.GetBytes(config)),
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory
            });
            if(process==null) throw new IOException("Unable to start the update helper.");
            using(process) {
                DateTime deadline=DateTime.UtcNow.AddSeconds(10);
                while(!File.Exists(config+".ready")) {
                    if(process.WaitForExit(25) || DateTime.UtcNow>=deadline)
                        throw new IOException("The update helper did not become ready; the current application will stay open.");
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
            string status=null, installed=null, config=null; bool parentExited=false;
            try {
                config=Encoding.UTF8.GetString(Convert.FromBase64String(encodedConfig));
                int pid; long ticks; string staged,hash;
                using(var reader=new BinaryReader(File.OpenRead(config))) {
                    pid=reader.ReadInt32(); ticks=reader.ReadInt64(); staged=reader.ReadString();
                    installed=reader.ReadString(); status=reader.ReadString(); hash=reader.ReadString();
                }
                File.WriteAllText(status,"Update pending: waiting for the application to close.");
                File.WriteAllText(config+".ready","ready");
                try {
                    using(var parent=System.Diagnostics.Process.GetProcessById(pid)) {
                        if(parent.StartTime.ToUniversalTime().Ticks==ticks && !parent.WaitForExit(120000)) throw new IOException("The application did not close; update cancelled.");
                    }
                } catch(ArgumentException) { /* Parent already exited. */ }
                parentExited=true;
                ReplaceAndLaunch(staged,installed,hash,path=> {
                    File.WriteAllText(status,"Update installed successfully.");
                    Launch(path);
                });
            } catch(Exception ex) {
                if(status!=null) try { File.WriteAllText(status,"Update failed. Check the installation and any .previous backup before retrying. "+ex.Message); } catch {}
                if(parentExited && installed!=null && File.Exists(installed)) try { Launch(installed); } catch {}
            } finally {
                if(config!=null) try { File.Delete(config); File.Delete(config+".ready"); } catch {}
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
