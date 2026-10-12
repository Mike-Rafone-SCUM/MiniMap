using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace ScumMiniMap {
    internal sealed class UpdateInstallation {
        internal readonly string InstalledPath;
        internal readonly Version Version;
        internal readonly UpdateProcessIdentity Process;
        internal UpdateInstallation(string path,Version version,UpdateProcessIdentity process) {
            InstalledPath=Path.GetFullPath(path);Version=version;Process=process;
        }
    }
    internal static class UpdateBootstrap {
        internal static Version ReadStableVersion(string path) {
            var metadata=FileVersionInfo.GetVersionInfo(path);Version version;
            if(metadata.ProductName!="SkynettMiniMap" || metadata.CompanyName!="MikeRafone" ||
                !System.Text.RegularExpressions.Regex.IsMatch(metadata.ProductVersion??"",@"\A(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\z") ||
                !System.Version.TryParse(metadata.ProductVersion+".0",out version))
                throw new InvalidDataException("Select a stable SCUM MiniMap executable.");
            // Read managed identity without loading or executing the old application.
            if(AssemblyName.GetAssemblyName(path).Name.IndexOf("MiniMap",StringComparison.OrdinalIgnoreCase)<0)
                throw new InvalidDataException("The selected file is not a SCUM MiniMap application.");
            return version;
        }
        internal static UpdateRelease VerifyBundle(string source,string manifest,Func<string,Version> readVersion) {
            if(readVersion==null)throw new ArgumentNullException("readVersion");
            var release=UpdateRelease.Parse(manifest);release.VerifyFile(source);
            if(readVersion(source)!=release.Version)throw new InvalidDataException("The update manifest does not match this executable version.");
            return release;
        }
        internal static IList<UpdateInstallation> RunningInstallations(string source,Version candidateVersion) {
            string fullSource=Path.GetFullPath(source);var installations=new List<UpdateInstallation>();
            int current;
            using(var own=Process.GetCurrentProcess())current=own.Id;
            foreach(var process in Process.GetProcesses())using(process) {
                try {
                    if(process.Id==current || process.HasExited)continue;
                    // Distribution names retain MiniMap; all destructive actions still require
                    // product identity, exact executable path, PID and start-time verification.
                    if(process.ProcessName.IndexOf("MiniMap",StringComparison.OrdinalIgnoreCase)<0)continue;
                    string path=Path.GetFullPath(process.MainModule.FileName);
                    if(string.Equals(path,fullSource,StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName(path),"Updater.exe",StringComparison.OrdinalIgnoreCase))continue;
                    var version=ReadStableVersion(path);if(version>=candidateVersion)continue;
                    var identity=new UpdateProcessIdentity(process.Id,process.StartTime.ToUniversalTime().Ticks,path);
                    installations.Add(new UpdateInstallation(path,version,identity));
                }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(ArgumentException){}
                catch(IOException){}catch(UnauthorizedAccessException){}catch(BadImageFormatException){}
            }
            return installations.AsReadOnly();
        }
        internal static UpdateInstallation SelectInstallation(IList<UpdateInstallation> running,string explicitPath,Version candidateVersion,string source) {
            if(running==null)throw new ArgumentNullException("running");
            if(string.IsNullOrWhiteSpace(explicitPath)) {
                if(running.Count!=1)throw new InvalidOperationException("Select one installed MiniMap executable to update.");
                if(running[0].Version>=candidateVersion || string.Equals(running[0].InstalledPath,Path.GetFullPath(source),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Choose an older installed MiniMap, not this downloaded copy.");
                return running[0];
            }
            string selected=Path.GetFullPath(explicitPath);
            if(string.Equals(selected,Path.GetFullPath(source),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose the previous installed MiniMap, not this downloaded copy.");
            var matching=new List<UpdateInstallation>();
            foreach(var target in running)if(string.Equals(target.InstalledPath,selected,StringComparison.OrdinalIgnoreCase))matching.Add(target);
            if(matching.Count>1)throw new InvalidOperationException("More than one MiniMap process is using that installation; close the extra instance first.");
            if(matching.Count==1) {
                if(matching[0].Version>=candidateVersion)throw new InvalidDataException("The chosen MiniMap is already this version or newer.");
                return matching[0];
            }
            Version version=ReadStableVersion(selected);
            if(version>=candidateVersion)throw new InvalidDataException("The chosen MiniMap is already this version or newer.");
            return new UpdateInstallation(selected,version,null);
        }
        internal static void Install(string source,string manifest,UpdateInstallation target,string statusPath) {
            if(target==null)throw new ArgumentNullException("target");
            var release=VerifyBundle(source,manifest,ReadStableVersion);
            if(release.Version<=target.Version || ReadStableVersion(target.InstalledPath)!=target.Version)
                throw new InvalidDataException("The installed MiniMap changed or does not need this update.");
            string staged=target.InstalledPath+"."+Guid.NewGuid().ToString("N")+".update.exe";
            bool committed=false;
            try {
                File.Copy(source,staged);release.VerifyFile(staged);
                // A ZIP-based bootstrap copies only the validated installation assets.
                using(var assetManifest=typeof(InstallationAssets).Assembly.GetManifestResourceStream("installation-assets.txt"))if(assetManifest!=null) {
                    Directory.CreateDirectory(UpdatePackage.AssetDirectory(staged));
                    foreach(string name in InstallationAssets.Names)using(var input=InstallationAssets.Open(name))
                    using(var output=new FileStream(Path.Combine(UpdatePackage.AssetDirectory(staged),name),FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
                }
                UpdateInstaller.StartForTarget(staged,target.InstalledPath,statusPath,target.Process);committed=true;
            }finally { if(!committed)UpdatePackage.Cleanup(staged); }
        }
    }
}
