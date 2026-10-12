using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ScumMiniMap;
namespace ScumMiniMap { public static class MapWindow { public const string VersionString="1.2.4"; } }
static class UpdateTests {
    static int count;
    static void CheckSecurityPolicy() {
        foreach(string url in new[]{"https://github.com/o/r", "https://release-assets.githubusercontent.com/a", "https://objects.githubusercontent.com/a"})
            Check(UpdateService.AllowedDownloadUri(new Uri(url),false),"Official HTTPS release endpoint accepted");
        foreach(string url in new[]{"http://github.com/a","https://github.com.evil.example/a","https://github.com:444/a","https://user@github.com/a","https://steamcommunity.com/a","https://127.0.0.1/a"})
            Check(!UpdateService.AllowedDownloadUri(new Uri(url),false),"Unapproved release redirect rejected before connection");
        Check(UpdateService.AllowedDownloadUri(new Uri("https://steamcommunity.com/profiles/1"),true),"Steam HTTPS endpoint accepted");
        Check(!UpdateService.AllowedDownloadUri(new Uri("https://github.com/a"),true),"Steam redirects cannot cross to another service");
        var user=new System.Security.Principal.SecurityIdentifier("S-1-5-21-1-2-3-1001");
        var admin=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid,null);
        var elevated=UpdateInstaller.HelperDirectorySecurity(true,user);
        Check(elevated.AreAccessRulesProtected && elevated.GetOwner(typeof(System.Security.Principal.SecurityIdentifier)).Equals(admin),"Elevated helper permissions protected and owned by Administrators");
        var rules=elevated.GetAccessRules(true,true,typeof(System.Security.Principal.SecurityIdentifier));
        Check(rules.Count==2,"Elevated helper grants exactly Administrators and SYSTEM");
        foreach(System.Security.AccessControl.FileSystemAccessRule rule in rules)
            Check(!rule.IdentityReference.Equals(user) && rule.InheritanceFlags==(System.Security.AccessControl.InheritanceFlags.ContainerInherit|System.Security.AccessControl.InheritanceFlags.ObjectInherit),"Unelevated user has no helper write grant; files inherit protection");
    }
    static byte[] PackageBytes(byte[] exe,byte[] tiles,byte[] roads,string extra,bool corruptManifest) {
        Func<byte[],string> hex=bytes=> { using(var stream=new MemoryStream(bytes))return InstallationAssets.Digest(stream); };
        using(var memory=new MemoryStream()) {
            using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
                var entries=new Dictionary<string,byte[]> {
                    {"SkynettMiniMap.exe",exe},{"assets/map-tiles.bin",tiles},{"assets/roads.bin",roads},
                    {"package.txt",Encoding.UTF8.GetBytes("version=1.2.10\nSkynettMiniMap.exe="+hex(exe)+"\nassets/map-tiles.bin="+(corruptManifest?new string('0',64):hex(tiles))+"\nassets/roads.bin="+hex(roads)+"\n")}
                };
                if(extra!=null)entries.Add(extra,new byte[]{1});
                foreach(var entry in entries)using(var stream=zip.CreateEntry(entry.Key).Open())stream.Write(entry.Value,0,entry.Value.Length);
            }
            return memory.ToArray();
        }
    }
    static void CheckPackageUpdates(string root) {
        byte[] exe=Encoding.UTF8.GetBytes("MZ package fixture"),tiles=Encoding.UTF8.GetBytes("MTL2 tiles fixture"),roads=Encoding.UTF8.GetBytes("roads fixture");
        string exeHash;using(var stream=new MemoryStream(exe))exeHash=InstallationAssets.Digest(stream);
        byte[] package=PackageBytes(exe,tiles,roads,null,false);
        string packageHash;using(var stream=new MemoryStream(package))packageHash=InstallationAssets.Digest(stream);
        var release=UpdateRelease.Parse("schema=2\nversion=1.2.10\nsha256="+exeHash+"\npackage-sha256="+packageHash+"\n");
        string releaseManifest="schema=2\nversion=1.2.10\nsha256="+exeHash+"\npackage-sha256="+packageHash+"\n";
        var service=new UpdateService("owner/releases",root,(uri,limit)=>uri.AbsolutePath.EndsWith("update.txt")?Encoding.UTF8.GetBytes(releaseManifest):package);
        Check(release.IsPackage && service.DownloadUri(release).AbsolutePath.EndsWith("/SkynettMiniMap.zip"),"Package updates use ZIP endpoint");
        string staged=Path.Combine(root,"package.update.exe");service.Download(release,staged);
        CheckBytes(staged,exe,"Package executable verified");
        CheckBytes(Path.Combine(UpdatePackage.AssetDirectory(staged),"map-tiles.bin"),tiles,"Package map asset verified");
        foreach(string entry in new[]{"../outside.txt","assets/../outside.txt","assets\\roads.bin","assets/MAP-TILES.BIN","unapproved.exe"}) {
            string rejected=Path.Combine(root,"bad-entry.update.exe");
            Reject(()=>UpdatePackage.Extract(PackageBytes(exe,tiles,roads,entry,false),rejected,release),"Unexpected/traversal ZIP path rejected");
            Check(!File.Exists(rejected) && !Directory.Exists(UpdatePackage.AssetDirectory(rejected)),"Rejected ZIP leaves no staged installation");
        }
        string corrupt=Path.Combine(root,"bad-hash.update.exe");
        byte[] dishonest=(byte[])package.Clone();
        for(int i=0;i<dishonest.Length-46;i++)if(dishonest[i]==0x50 && dishonest[i+1]==0x4b && dishonest[i+2]==1 && dishonest[i+3]==2) {
            Array.Copy(BitConverter.GetBytes(1),0,dishonest,i+24,4);break;
        }
        Reject(()=>UpdatePackage.Extract(dishonest,corrupt,release),"Decompression cannot exceed a forged declared entry length");
        Check(!File.Exists(corrupt) && !Directory.Exists(UpdatePackage.AssetDirectory(corrupt)),"Forged-length ZIP cleans partial extraction");
        byte[] oversized;
        using(var memory=new MemoryStream()) {
            using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
                foreach(string name in new[]{"SkynettMiniMap.exe","assets/map-tiles.bin","assets/roads.bin","package.txt"})
                    using(var output=zip.CreateEntry(name).Open()) {
                        byte[] content=name=="package.txt"?new byte[100000]:exe;output.Write(content,0,content.Length);
                    }
            }
            oversized=memory.ToArray();
        }
        for(int i=0;i<oversized.Length-46;i++)if(oversized[i]==0x50 && oversized[i+1]==0x4b && oversized[i+2]==1 && oversized[i+3]==2 &&
            Encoding.UTF8.GetString(oversized,i+46,BitConverter.ToUInt16(oversized,i+28))=="package.txt") {
            Array.Copy(BitConverter.GetBytes(1),0,oversized,i+24,4);break;
        }
        Reject(()=>UpdatePackage.Extract(oversized,corrupt,release),"Manifest decompression bounded despite forged central-directory length");
        Check(!File.Exists(corrupt) && !Directory.Exists(UpdatePackage.AssetDirectory(corrupt)),"Oversized manifest leaves no staged files");
        Reject(()=>UpdatePackage.Extract(PackageBytes(exe,tiles,roads,null,true),corrupt,release),"Asset checksum mismatch rejects package");
        Check(!File.Exists(corrupt) && !Directory.Exists(UpdatePackage.AssetDirectory(corrupt)),"Corrupt package is cleaned");
        var wrongTransport=new UpdateService("owner/releases",root,(uri,limit)=>uri.AbsolutePath.EndsWith("update.txt")?Encoding.UTF8.GetBytes(releaseManifest):new byte[]{1,2,3});
        Reject(()=>wrongTransport.Download(release,corrupt),"ZIP digest must match before extraction");
        using(var verified=InstallationAssets.OpenVerified(Path.Combine(UpdatePackage.AssetDirectory(staged),"roads.bin"),InstallationAssets.Digest(Path.Combine(UpdatePackage.AssetDirectory(staged),"roads.bin"))))Check(verified.Position==0,"Verified asset stream rewound");
        Reject(()=>InstallationAssets.OpenVerified(staged,new string('0',64)),"Modified installation asset rejected");
        Reject(()=>InstallationAssets.OpenVerified(Path.Combine(root,"missing.bin"),exeHash),"Missing installation asset rejected");
        string installDir=Path.Combine(root,"package-install");Directory.CreateDirectory(Path.Combine(installDir,"assets"));
        string installed=Path.Combine(installDir,"MiniMap.exe"),oldRoads=Path.Combine(installDir,"assets","roads.bin");
        byte[] oldExe=Encoding.UTF8.GetBytes("MZ previous fixture"),oldRoadBytes=Encoding.UTF8.GetBytes("old roads");
        File.WriteAllBytes(installed,oldExe);File.WriteAllBytes(oldRoads,oldRoadBytes);File.WriteAllText(Path.Combine(installDir,"assets","unrelated.txt"),"keep");
        string identity=UpdatePackage.AssetIdentity(staged);
        Reject(()=>UpdatePackage.ReplaceAndLaunch(staged,installed,Digest(exe),identity,path=> { throw new IOException("restart failure"); }),"Restart failure rolls back complete package");
        CheckBytes(installed,oldExe,"Rollback restores old EXE");CheckBytes(oldRoads,oldRoadBytes,"Rollback restores old asset");
        Check(!File.Exists(Path.Combine(installDir,"assets","map-tiles.bin")),"Rollback removes only newly introduced asset");
        Check(File.ReadAllText(Path.Combine(installDir,"assets","unrelated.txt"))=="keep","Rollback preserves unrelated files");
        UpdatePackage.Cleanup(staged);service.Download(release,staged);identity=UpdatePackage.AssetIdentity(staged);int launches=0;
        using(var locked=new FileStream(oldRoads,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            Reject(()=>UpdatePackage.ReplaceAndLaunch(staged,installed,Digest(exe),identity,path=>launches++),"Locked second asset rolls back partial replacement");
        }
        CheckBytes(installed,oldExe,"Partial asset failure leaves old EXE intact");CheckBytes(oldRoads,oldRoadBytes,"Partial asset failure retains locked old asset");
        Check(launches==0 && !File.Exists(Path.Combine(installDir,"assets","map-tiles.bin")),"Partial asset failure removes first introduced asset without launch");
        UpdatePackage.Cleanup(staged);service.Download(release,staged);identity=UpdatePackage.AssetIdentity(staged);
        UpdatePackage.ReplaceAndLaunch(staged,installed,Digest(exe),identity,path=> { launches++;CheckBytes(oldRoads,roads,"Assets replaced before restart"); });
        Check(launches==1 && !Directory.Exists(UpdatePackage.AssetDirectory(staged)),"Successful package install cleans staging and launches once");
        CheckBytes(installed,exe,"Successful package install replaces EXE");
        Check(Directory.GetFiles(Path.Combine(installDir,"assets"),"*.previous").Length==0,"Successful package install cleans owned backups");
    }
    static void CheckSchedule() {
        DateTime start=new DateTime(2026,10,9,12,0,0,DateTimeKind.Utc);
        var schedule=new UpdateCheckSchedule(start);
        Check(!schedule.TryBegin(start.AddMinutes(59),true),"Periodic update does not run before its hourly interval");
        Check(!schedule.TryBegin(start.AddHours(1),false),"Busy UI defers a due update check");
        Check(schedule.TryBegin(start.AddHours(1).AddMinutes(1),true),"A deferred update runs when the UI becomes available");
        Check(!schedule.TryBegin(start.AddHours(1).AddMinutes(1),true),"Repeated timer ticks cannot duplicate an update check");
        Check(schedule.TryBegin(start.AddHours(5),true),"Resuming after a long pause performs one overdue check");
        Check(!schedule.TryBegin(start.AddHours(5),true),"Resume does not replay missed update intervals");
        schedule.RecordCheck(start.AddHours(5).AddMinutes(20));
        Check(!schedule.TryBegin(start.AddHours(6),true),"A manual or startup check postpones the next periodic check");
        Check(schedule.TryBegin(start.AddHours(6).AddMinutes(20),true),"Periodic checks continue after a manual check");
    }
    static void Check(bool pass,string name) { if(!pass)throw new Exception(name); count++; }
    static void Reject(Action action,string name) { try { action(); } catch { count++; return; } throw new Exception(name); }
    static string Manifest(string version,string hash) { return "schema=1\nversion="+version+"\nsha256="+hash+"\n"; }
    static string Digest(byte[] bytes) { using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes)); }
    static void CheckBytes(string path,byte[] expected,string name) { Check(File.Exists(path) && Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(expected),name); }
    static void CheckReplacement(string root) {
        byte[] oldBytes=Encoding.UTF8.GetBytes("MZ original MiniMap fixture"),newBytes=Encoding.UTF8.GetBytes("MZ updated MiniMap fixture");
        string success=Path.Combine(root,"replace-success");Directory.CreateDirectory(success);
        string installed=Path.Combine(success,"MiniMap user & test.exe"),staged=Path.Combine(success,"new MiniMap.exe");
        File.WriteAllBytes(installed,oldBytes);File.WriteAllBytes(staged,newBytes);
        int launches=0;
        UpdateInstaller.ReplaceAndLaunch(staged,installed,Digest(newBytes),path=> {
            launches++;
            Check(path==installed,"Successful install launches exactly the approved destination path");
            CheckBytes(installed,newBytes,"Launch callback sees the verified replacement bytes");
            Check(Directory.GetFiles(success,"*.previous").Length==1,"Original executable remains recoverable until launch succeeds");
        });
        Check(launches==1 && !File.Exists(staged) && Directory.GetFiles(success,"*.previous").Length==0,
            "Successful replacement consumes staging and removes only its backup after launch");
        string rejected=Path.Combine(root,"replace-hash-rejected");Directory.CreateDirectory(rejected);
        installed=Path.Combine(rejected,"MiniMap.exe");staged=Path.Combine(rejected,"new.exe");
        File.WriteAllBytes(installed,oldBytes);File.WriteAllBytes(staged,newBytes);launches=0;
        Reject(()=>UpdateInstaller.ReplaceAndLaunch(staged,installed,Digest(oldBytes),path=>launches++),"Changed staging hash must reject installation");
        CheckBytes(installed,oldBytes,"Changed staging leaves the installed executable intact");
        Check(launches==0 && File.Exists(staged) && Directory.GetFiles(rejected,"*.previous").Length==0,
            "Hash rejection cannot launch, consume staging or create a backup");
        string rollback=Path.Combine(root,"replace-launch-failed");Directory.CreateDirectory(rollback);
        installed=Path.Combine(rollback,"MiniMap.exe");staged=Path.Combine(rollback,"new.exe");
        File.WriteAllBytes(installed,oldBytes);File.WriteAllBytes(staged,newBytes);launches=0;
        Reject(()=>UpdateInstaller.ReplaceAndLaunch(staged,installed,Digest(newBytes),path=> { launches++;throw new IOException("Fixture restart failed."); }),
            "Launch failure must stop the update transaction");
        CheckBytes(installed,oldBytes,"Failed launch restores the original executable bytes");
        Check(launches==1 && Directory.GetFiles(rollback,"*.previous").Length==0,"Rollback restores its own backup without leaving a stale previous executable");
        string locked=Path.Combine(root,"replace-locked");Directory.CreateDirectory(locked);
        installed=Path.Combine(locked,"MiniMap.exe");staged=Path.Combine(locked,"new.exe");
        File.WriteAllBytes(installed,oldBytes);File.WriteAllBytes(staged,newBytes);launches=0;
        using(var ownLock=new FileStream(installed,FileMode.Open,FileAccess.Read,FileShare.None))
            Reject(()=>UpdateInstaller.ReplaceAndLaunch(staged,installed,Digest(newBytes),path=>launches++),"Locked installation must fail closed");
        CheckBytes(installed,oldBytes,"Failed replacement leaves the locked original intact");
        Check(launches==0 && File.Exists(staged) && Directory.GetFiles(locked,"*.previous").Length==0,
            "Failed replacement retains verified staging and never attempts launch");
    }
    static void CheckHelperCleanup(string root) {
        string status=Path.Combine(root,"cleanup-status.txt"),outside=Path.Combine(root,"unrelated-helper.exe");
        File.WriteAllText(outside,"owned unrelated fixture");File.WriteAllText(status+".helper",outside);
        Check(!UpdateInstaller.CleanupHelper(status) && File.Exists(outside) && File.Exists(status+".helper"),
            "Cleanup refuses an updater marker pointing outside its private temporary helper directory");
        string helperDirectory=Path.Combine(Path.GetTempPath(),"ScumMiniMap-update-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(helperDirectory);
        try {
            string helper=Path.Combine(helperDirectory,"Updater.exe");
            File.WriteAllText(helper,"owned updater fixture");File.WriteAllText(Path.Combine(helperDirectory,"request.bin"),"request");
            File.WriteAllText(Path.Combine(helperDirectory,"request.bin.ready"),"ready");
            File.WriteAllText(status+".helper",helper);
            Check(UpdateInstaller.CleanupHelper(status) && !Directory.Exists(helperDirectory) && !File.Exists(status+".helper") && File.Exists(outside),
                "Successful cleanup removes its known private helper files and leaves unrelated files intact");
            Directory.CreateDirectory(helperDirectory);
            File.WriteAllText(helper,"owned updater fixture");File.WriteAllText(Path.Combine(helperDirectory,"unrelated.txt"),"keep");
            File.WriteAllText(status+".helper",helper);
            Check(!UpdateInstaller.CleanupHelper(status) && File.Exists(Path.Combine(helperDirectory,"unrelated.txt")) && File.Exists(status+".helper"),
                "Helper cleanup never recursively deletes unexpected files in its temporary directory");
            File.Delete(Path.Combine(helperDirectory,"unrelated.txt"));
            Check(UpdateInstaller.CleanupHelper(status) && !Directory.Exists(helperDirectory),"Helper cleanup can finish safely after an unexpected file is removed");
        } finally {
            string expectedTemp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if(Directory.Exists(helperDirectory) && string.Equals(Path.GetDirectoryName(Path.GetFullPath(helperDirectory)),expectedTemp,StringComparison.OrdinalIgnoreCase))Directory.Delete(helperDirectory,true);
        }
    }
    sealed class FixtureProcess:IUpdateProcess {
        internal int ProcessId=17;
        internal long Started=1234567;
        internal string Path;
        internal bool Exited,Disposed,ExitOnClose,ExitOnKill=true;
        internal int CloseCalls,KillCalls;
        internal readonly List<int> Waits=new List<int>();
        internal Func<FixtureProcess,int,bool> DuringWait;
        internal Exception KillFailure;
        public int Id { get { return ProcessId; } }
        public long StartTimeUtcTicks { get { return Started; } }
        public string ExecutablePath { get { return Path; } }
        public bool HasExited { get { return Exited; } }
        public bool CloseMainWindow() { CloseCalls++;if(ExitOnClose)Exited=true;return true; }
        public bool WaitForExit(int milliseconds) {
            Waits.Add(milliseconds);
            if(DuringWait!=null && DuringWait(this,milliseconds))Exited=true;
            return Exited;
        }
        public void Kill() { KillCalls++;if(KillFailure!=null)throw KillFailure;if(ExitOnKill)Exited=true; }
        public void Dispose() { Disposed=true; }
    }
    static void CheckProcessPolicy(string root) {
        string approvedPath=Path.Combine(root,"approved MiniMap.exe");
        var approved=new UpdateProcessIdentity(17,1234567,approvedPath);
        int acquisitions=0;
        UpdateInstaller.CloseApprovedProcess(approved,pid=> { acquisitions++;throw new ArgumentException("Owned fixture already exited."); },20,10);
        Check(acquisitions==1,"Already-exited process lookup permits install without targeting another PID");
        var exited=new FixtureProcess { Path=approvedPath,Exited=true };
        UpdateInstaller.CloseApprovedProcess(approved,pid=>exited,20,10);
        Check(exited.CloseCalls==0 && exited.KillCalls==0 && exited.Disposed,"Exited process receives no close or kill and releases its inspection handle");
        foreach(var wrong in new[]{
            new FixtureProcess { Path=approvedPath,ProcessId=18 },
            new FixtureProcess { Path=approvedPath,Started=1234568 },
            new FixtureProcess { Path=approvedPath+".unrelated.exe" },
            new FixtureProcess { Path=Path.Combine(root,"other","approved MiniMap.exe") }
        }) {
            Reject(()=>UpdateInstaller.CloseApprovedProcess(approved,pid=>wrong,20,10),"PID, start-time or exact-path mismatch must reject shutdown");
            Check(wrong.CloseCalls==0 && wrong.KillCalls==0 && wrong.Disposed,"Mismatched process cannot receive close or kill and is disposed");
        }
        var graceful=new FixtureProcess { Path=approvedPath,ExitOnClose=true };
        UpdateInstaller.CloseApprovedProcess(approved,pid=> { Check(pid==17,"Only the approved PID is acquired for graceful close");return graceful; },1000,10);
        Check(graceful.CloseCalls==1 && graceful.KillCalls==0 && graceful.Exited && graceful.Disposed,
            "Graceful process exit completes without force-closing the installation");
        var stubborn=new FixtureProcess { Path=approvedPath };
        UpdateInstaller.CloseApprovedProcess(approved,pid=>stubborn,1000,10);
        Check(stubborn.CloseCalls>0 && stubborn.KillCalls==1 && stubborn.Exited && stubborn.Disposed,
            "Only the still-matching approved process is force-closed after graceful attempts expire");
        Check(stubborn.Waits.TrueForAll(wait=>wait>=0 && wait<=500) && stubborn.Waits.Count<12,
            "Graceful and forced process wait budgets stay bounded with deterministic callback fixtures");
        foreach(bool changePath in new[]{false,true}) {
            var reused=new FixtureProcess { Path=approvedPath };
            reused.DuringWait=(process,milliseconds)=> {
                if(process.CloseCalls>0) { if(changePath)process.Path=approvedPath+".changed";else process.Started++; }
                return false;
            };
            Reject(()=>UpdateInstaller.CloseApprovedProcess(approved,pid=>reused,1000,10),"Changed process identity must stop force fallback");
            Check(reused.CloseCalls>0 && reused.KillCalls==0 && reused.Disposed,
                "Identity changed during graceful waiting cannot be force-killed");
        }
        var noExit=new FixtureProcess { Path=approvedPath,ExitOnKill=false };
        Reject(()=>UpdateInstaller.CloseApprovedProcess(approved,pid=>noExit,1000,10),"Force-close timeout must abort installation");
        Check(noExit.KillCalls==1 && !noExit.Exited && noExit.Disposed,"Force-close timeout retains a live process and disposes its inspection handle");
        var denied=new FixtureProcess { Path=approvedPath,KillFailure=new System.ComponentModel.Win32Exception(5,"Owned fixture access denied.") };
        Reject(()=>UpdateInstaller.CloseApprovedProcess(approved,pid=>denied,1000,10),"Denied force-close must abort installation");
        Check(denied.KillCalls==1 && denied.Disposed,"Denied force-close releases its inspection handle");
    }
    static void CheckHelperLaunchConfiguration(string root) {
        string helper=Path.Combine(root,"private helper","Updater.exe"),config=Path.Combine(root,"private helper","request ü & %.bin");
        foreach(bool elevated in new[]{false,true}) {
            ProcessStartInfo start=UpdateInstaller.CreateHelperStartInfo(helper,config,elevated);
            Check(start.FileName==helper && start.WorkingDirectory==Path.GetDirectoryName(helper),"Helper launch keeps exact filesystem paths outside shell command text");
            Check(start.UseShellExecute==!elevated && start.Verb==(elevated?"":"runas"),"Unelevated updater requests UAC through shell RunAs; elevated updater inherits its token");
            string prefix="-ApplyVerifiedUpdate ";
            Check(start.Arguments.StartsWith(prefix,StringComparison.Ordinal)
                && Encoding.UTF8.GetString(Convert.FromBase64String(start.Arguments.Substring(prefix.Length)))==config,
                "Helper request path with spaces and special characters round-trips through binary argument encoding");
        }
    }
    static void CheckBundleAndInstallationSelection(string root) {
        string source=Path.Combine(root,"downloaded release.exe"),oldPath=Path.Combine(root,"previous installation.exe");
        byte[] candidateBytes=Encoding.UTF8.GetBytes("MZ trusted standalone or ZIP fixture"),oldBytes=Encoding.UTF8.GetBytes("MZ old installation fixture");
        File.WriteAllBytes(source,candidateBytes);File.WriteAllBytes(oldPath,oldBytes);
        string hex;
        using(var sha=SHA256.Create())hex=BitConverter.ToString(sha.ComputeHash(candidateBytes)).Replace("-","").ToLowerInvariant();
        string manifest=Manifest("1.2.10",hex);
        Version candidateVersion=new Version(1,2,10,0);
        int metadataReads=0;
        UpdateRelease bundled=UpdateBootstrap.VerifyBundle(source,manifest,path=> { metadataReads++;return candidateVersion; });
        Check(bundled.Version==candidateVersion && bundled.Sha256==hex && metadataReads==1,
            "Bundled manifest verifies exact executable bytes and the stamped stable version together");
        metadataReads=0;
        Reject(()=>UpdateBootstrap.VerifyBundle(source,Manifest("1.2.10",new string('0',64)),path=> { metadataReads++;return candidateVersion; }),
            "Untrusted or modified bootstrap bytes must reject verification");
        Check(metadataReads==0,"Bootstrap hash rejection happens before reading executable metadata or selecting processes");
        Reject(()=>UpdateBootstrap.VerifyBundle(source,manifest,path=>new Version(1,2,11,0)),"Manifest and stamped executable version mismatch must reject bootstrap");
        Reject(()=>UpdateBootstrap.VerifyBundle(source,manifest,path=>null),"Missing stable executable metadata must reject bootstrap");
        Reject(()=>UpdateBootstrap.ReadStableVersion(source),"Arbitrary MZ files without authentic MiniMap product metadata must reject selection");
        CheckBytes(oldPath,oldBytes,"Bootstrap trust failures leave the previous installation untouched");
        int pinnedRequests=0;
        var pinned=new UpdateService("owner/releases",root,(uri,limit)=> {
            pinnedRequests++;
            Check(uri.AbsoluteUri=="https://github.com/owner/releases/releases/download/v1.2.10/update.txt" && limit==4096,
                "Standalone EXE checks its exact official release manifest with a bounded response");
            return Encoding.UTF8.GetBytes(manifest);
        });
        UpdateRelease official=pinned.CheckVersion("1.2.10");
        Check(UpdateBootstrap.VerifyBundle(source,Manifest(official.VersionText,official.Sha256),path=>candidateVersion).Version==candidateVersion
            && pinnedRequests==1,"Standalone EXE uses the same verified-byte and version contract as the ZIP bootstrap");
        foreach(string invalid in new[]{"1.2.10-beta","1.2","../latest","1.2.10/../../evil","01.2.10"})
            Reject(()=>pinned.CheckVersion(invalid),"Invalid pinned release identifiers cannot initiate manifest requests");
        Check(pinnedRequests==1,"Rejected pinned release versions never invoke transport");
        var wrongManifest=new UpdateService("owner/releases",root,(uri,limit)=>Encoding.UTF8.GetBytes(Manifest("1.2.11",hex)));
        Reject(()=>wrongManifest.CheckVersion("1.2.10"),"Pinned manifest describing another release must fail closed");
        var offline=new UpdateService("owner/releases",root,(uri,limit)=> { throw new WebException("Owned fixture offline."); });
        Reject(()=>offline.CheckVersion("1.2.10"),"Unavailable official standalone manifest cannot approve an update");
        CheckBytes(oldPath,oldBytes,"Official verification failures preserve the old installation");
        var process=new UpdateProcessIdentity(17,1234567,oldPath);
        var one=new UpdateInstallation(oldPath,new Version(1,2,4,0),process);
        Check(Object.ReferenceEquals(UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one},null,candidateVersion,source),one),
            "A single eligible running installation resolves to its captured process identity");
        Reject(()=>UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>(),null,candidateVersion,source),
            "No eligible running installation cannot become an automatic update target");
        var another=new UpdateInstallation(Path.Combine(root,"another installation.exe"),new Version(1,2,3,0),new UpdateProcessIdentity(18,7654321,Path.Combine(root,"another installation.exe")));
        Reject(()=>UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one,another},null,candidateVersion,source),
            "Ambiguous running installations require an explicit selection before shutdown");
        Check(Object.ReferenceEquals(UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one,another},oldPath.ToUpperInvariant(),candidateVersion,source),one),
            "Explicit installation selection uses an exact Windows path and preserves the chosen process identity");
        var duplicate=new UpdateInstallation(oldPath,new Version(1,2,4,0),new UpdateProcessIdentity(19,7654322,oldPath));
        Reject(()=>UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one,duplicate},oldPath,candidateVersion,source),
            "Multiple running copies of one installation cannot silently choose a process to terminate");
        Reject(()=>UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one},source,candidateVersion,source),
            "Bootstrap cannot select the newly downloaded executable as the old installation");
        Reject(()=>UpdateBootstrap.SelectInstallation(new List<UpdateInstallation>{one},oldPath+".unrelated",candidateVersion,source),
            "An unrecognized executable path cannot match an approved installation by prefix");
    }
    static void CheckCancelledHelperStartup(string root) {
        string directory=Path.Combine(root,"cancelled-helper");Directory.CreateDirectory(directory);
        string installed=Path.Combine(directory,"MiniMap.exe"),staged=Path.Combine(directory,"new.exe"),status=Path.Combine(directory,"status.txt");
        byte[] oldBytes=Encoding.UTF8.GetBytes("MZ old MiniMap fixture"),newBytes=Encoding.UTF8.GetBytes("MZ new MiniMap fixture");
        File.WriteAllBytes(installed,oldBytes);File.WriteAllBytes(staged,newBytes);
        var approved=new UpdateProcessIdentity(17,1234567,installed);
        string helperSource=System.Reflection.Assembly.GetExecutingAssembly().Location;
        Func<string,Version> fixtureVersion=path=>string.Equals(Path.GetFullPath(path),staged,StringComparison.OrdinalIgnoreCase)?new Version(1,2,10,0):new Version(1,2,4,0);
        ProcessStartInfo requested=null;string config=null,requestMagic=null,requestStaged=null,requestInstalled=null;int requestPid=0;long requestTicks=0;
        bool sawCommit=false;Exception cancelled=null;
        try {
            UpdateInstaller.StartForTarget(staged,installed,status,approved,false,start=> {
                requested=start;config=Encoding.UTF8.GetString(Convert.FromBase64String(start.Arguments.Substring("-ApplyVerifiedUpdate ".Length)));
                using(var reader=new BinaryReader(File.OpenRead(config))) {
                    requestMagic=reader.ReadString();requestPid=reader.ReadInt32();requestTicks=reader.ReadInt64();
                    requestStaged=reader.ReadString();requestInstalled=reader.ReadString();
                }
                sawCommit=File.Exists(config+".commit");
                throw new System.ComponentModel.Win32Exception(1223,"Owned fixture UAC cancelled.");
            },fixtureVersion,helperSource);
        }catch(IOException ex) { cancelled=ex; }
        Check(cancelled!=null && cancelled.InnerException is System.ComponentModel.Win32Exception
            && ((System.ComponentModel.Win32Exception)cancelled.InnerException).NativeErrorCode==1223,
            "Cancelled UAC reports an update cancellation before any process-close approval");
        Check(requested!=null && requested.UseShellExecute && requested.Verb=="runas" && !sawCommit,
            "Unelevated bootstrap requests explicit RunAs and cannot approve shutdown before helper readiness");
        Check(requestMagic=="SCUM-MiniMap-update-v3" && requestPid==17 && requestTicks==1234567
            && requestStaged==staged && requestInstalled==installed,
            "Helper request preserves the approved process snapshot and exact staging and installation paths");
        CheckBytes(installed,oldBytes,"Cancelled elevation leaves the original executable bytes intact");
        CheckBytes(staged,newBytes,"Cancelled elevation retains the staged update for a deliberate retry");
        Check(!Directory.Exists(requested.WorkingDirectory) && !File.Exists(status+".helper") && !File.Exists(config),
            "Cancelled helper launch removes its owned helper, request and status marker without recursive deletion");
        int launches=0;
        Reject(()=>UpdateInstaller.StartForTarget(staged,installed,status,approved,true,start=> { launches++;requested=start;return null; },fixtureVersion,helperSource),
            "Missing helper process must prevent approval and abort startup");
        Check(launches==1 && !Directory.Exists(requested.WorkingDirectory) && !File.Exists(status+".helper"),
            "Failed helper startup cleans only its private files and never commits shutdown");
        CheckBytes(installed,oldBytes,"Failed helper startup preserves the previous installation");
        var wrongPath=new UpdateProcessIdentity(17,1234567,installed+".unrelated");launches=0;
        Reject(()=>UpdateInstaller.StartForTarget(staged,installed,status,wrongPath,false,start=> { launches++;return null; },fixtureVersion,helperSource),
            "Helper startup must reject an approved process path different from its installation");
        Check(launches==0 && !File.Exists(status+".helper"),"Invalid process-path approval cannot launch an elevated helper or create a live request marker");
    }
    static int Main() {
        CheckSchedule();
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-UpdateTests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            byte[] binary=Encoding.UTF8.GetBytes("MZtest executable fixture");
            string hash;
            using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(binary)).Replace("-","").ToLowerInvariant();
            string manifest=Manifest("1.2.10",hash);
            UpdateRelease release=UpdateRelease.Parse(manifest);
            Check(release.Version>new Version(1,2,9,0),"Numeric comparison");
            Check(release.Version==new Version(1,2,10,0),"Equal version");
            Check(release.Version<new Version(1,3,0,0),"No downgrade");
            Check(UpdateRelease.Parse("\uFEFF"+manifest.Replace("\n","\r\n")).Version==release.Version,"BOM and Windows newlines");
            foreach(string bad in new [] { "", "<html>version 9.0.0</html>", manifest.Replace("schema=1","schema=2"), manifest+"version=9.0.0\n", manifest+"url=https://evil.example/file.exe", Manifest("1.2.3-beta",hash), Manifest("1.2",hash), Manifest("01.2.3",hash), Manifest("1.2.3.4",hash), Manifest("999999999999.2.3",hash), Manifest("1.2.3","wrong") })
                Reject(()=>UpdateRelease.Parse(bad),"Invalid manifest accepted");
            Reject(()=>new UpdateService("",root,null),"Missing repository accepted");
            Reject(()=>new UpdateService("owner/repo/../../evil",root,null),"Invalid repository accepted");
            int requests=0;
            Func<Uri,int,byte[]> transport=(url,limit)=>{ requests++; return url.AbsolutePath.EndsWith("update.txt")?Encoding.UTF8.GetBytes(manifest):binary; };
            var service=new UpdateService("owner/releases",root,transport);
            Check(service.ManifestUri.AbsoluteUri=="https://github.com/owner/releases/releases/latest/download/update.txt","Manifest endpoint");
            Check(service.DownloadUri(release).AbsoluteUri=="https://github.com/owner/releases/releases/download/v1.2.10/SkynettMiniMap.exe","Pinned download endpoint");
            service.Check(false); service.Check(false); service.Check(true);
            Check(requests==1,"Fresh cache avoids repeated requests");
            string cache=Path.Combine(root,"owner_releases-release.txt");
            File.SetLastWriteTimeUtc(cache,DateTime.UtcNow.AddMinutes(-2));
            service.Check(false); Check(requests==1,"Startup cache lasts an hour");
            service.Check(true); Check(requests==2,"Manual check refreshes after a minute");
            File.WriteAllText(cache,"broken"); service.Check(true); Check(requests==3,"Corrupt cache is refreshed");
            File.SetLastWriteTimeUtc(cache,DateTime.UtcNow.AddHours(-2));
            var offline=new UpdateService("owner/releases",root,(url,limit)=>{throw new WebException("offline");});
            Reject(()=>offline.Check(false),"Stale cache reported as current when offline");
            File.WriteAllText(Path.Combine(root,"owner_releases-retry-after.txt"),DateTime.UtcNow.AddMinutes(5).ToString("o"));
            Reject(()=>service.Check(true),"Persisted cooldown ignored");
            Check(requests==3,"Cooldown made a request");
            var now=DateTime.UtcNow;
            Check(UpdateService.RetryAfter("120",now)==now.AddMinutes(2),"Retry seconds");
            Check(UpdateService.RetryAfter("0",now)==now.AddMinutes(1),"Minimum cooldown");
            Check(UpdateService.RetryAfter("bad",now)==now.AddMinutes(5),"Default cooldown");
            Check(Math.Abs((UpdateService.RetryAfter(now.AddMinutes(10).ToString("r"),now)-now.AddMinutes(10)).TotalSeconds)<1,"HTTP date cooldown");
            string destination=Path.Combine(root,"download.exe");
            service.Download(release,destination);
            var poisoned=UpdateRelease.Parse(Manifest("1.2.10",new string('0',64)));
            Reject(()=>service.Download(poisoned,Path.Combine(root,"poisoned.exe")),"Locally forged release hash rejected by official pinned manifest");
            Check(!File.Exists(Path.Combine(root,"poisoned.exe")),"Forged cached release writes no executable");
            Check(File.Exists(destination),"Verified download missing");
            Reject(()=>service.Download(release,destination),"Existing executable overwritten");
            Check(File.ReadAllBytes(destination).Length==binary.Length,"Existing download changed");
            var corrupt=new UpdateService("owner/releases",root,(url,limit)=>Encoding.UTF8.GetBytes(url.AbsolutePath.EndsWith("update.txt")?manifest:"MZcorrupt"));
            Reject(()=>corrupt.Download(release,Path.Combine(root,"corrupt.exe")),"Bad checksum accepted");
            Check(!File.Exists(Path.Combine(root,"corrupt.exe")),"Corrupt download retained");
            Check(Directory.GetFiles(root,"*.partial").Length==0,"Partial download retained");
            CheckReplacement(root);
            CheckPackageUpdates(root);
            CheckSecurityPolicy();
            CheckHelperCleanup(root);
            CheckProcessPolicy(root);
            CheckHelperLaunchConfiguration(root);
            CheckBundleAndInstallationSelection(root);
            CheckCancelledHelperStartup(root);
            Console.WriteLine("Passed "+count+" GitHub updater regression checks.");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally {
            string expectedTemp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if(Directory.Exists(root) && string.Equals(Path.GetDirectoryName(Path.GetFullPath(root)),expectedTemp,StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("MiniMap-UpdateTests-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
    }
}
