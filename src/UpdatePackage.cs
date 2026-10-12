using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace ScumMiniMap {
    internal static class UpdatePackage {
        internal static string AssetDirectory(string staged) { return staged+".assets"; }
        internal static string AssetIdentity(string staged) {
            string directory=AssetDirectory(staged);
            if(!Directory.Exists(directory))return "";
            return DirectoryIdentity(directory);
        }
        static string DirectoryIdentity(string directory) {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Installation assets must not be a directory link.");
            return string.Join(":",InstallationAssets.Names.Select(name=>InstallationAssets.Digest(Path.Combine(directory,name))).ToArray());
        }
        internal static void Cleanup(string staged) {
            string directory=AssetDirectory(staged);
            if(File.Exists(staged))File.Delete(staged);
            if(Directory.Exists(directory)) {
                foreach(string name in InstallationAssets.Names)File.Delete(Path.Combine(directory,name));
                Directory.Delete(directory,false);
            }
        }
        internal static void Extract(byte[] bytes,string staged,UpdateRelease release) {
            if(File.Exists(staged) || Directory.Exists(AssetDirectory(staged)))throw new IOException("Update staging already exists.");
            bool complete=false;
            try {
                using(var memory=new MemoryStream(bytes,false))using(var zip=new ZipArchive(memory,ZipArchiveMode.Read)) {
                    var allowed=new HashSet<string>(new[]{"SkynettMiniMap.exe","package.txt","README.md","CHANGELOG.md","ATTRIBUTION.md","detect_zones.py","requirements.txt","Start-MiniMap.cmd","Update-MiniMap.cmd","keyboard-setup.png","assets/","assets/map-tiles.bin","assets/roads.bin"},StringComparer.Ordinal);
                    var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
                    foreach(var entry in zip.Entries) {
                        if(!allowed.Contains(entry.FullName) || !seen.Add(entry.FullName) || entry.Length>128*1024*1024)throw new InvalidDataException("Unexpected update ZIP entry.");
                        total+=entry.Length;if(total>160*1024*1024)throw new InvalidDataException("Update ZIP exceeds the unpacked size limit.");
                    }
                    foreach(string name in new[]{"SkynettMiniMap.exe","package.txt","assets/map-tiles.bin","assets/roads.bin"})if(!seen.Contains(name))throw new InvalidDataException("Incomplete update package.");
                    string manifest;
                    var manifestEntry=zip.GetEntry("package.txt");if(manifestEntry.Length>4096)throw new InvalidDataException("Oversized package manifest.");
                    using(var input=manifestEntry.Open())using(var memoryManifest=new MemoryStream()) {
                        byte[] buffer=new byte[1024];int count;
                        while((count=input.Read(buffer,0,buffer.Length))!=0) {
                            if(memoryManifest.Length+count>4096)throw new InvalidDataException("Oversized package manifest.");
                            memoryManifest.Write(buffer,0,count);
                        }
                        if(memoryManifest.Length!=manifestEntry.Length)throw new InvalidDataException("Invalid package manifest length.");
                        manifest=System.Text.Encoding.UTF8.GetString(memoryManifest.ToArray());
                    }
                    var expected=new Dictionary<string,string>(StringComparer.Ordinal);
                    foreach(string line in manifest.Trim().Split('\n')) {
                        string[] fields=line.Trim().Split('=');
                        if(fields.Length!=2 || expected.ContainsKey(fields[0]))throw new InvalidDataException("Invalid package manifest.");
                        expected.Add(fields[0],fields[1]);
                    }
                    if(expected.Count!=4 || !expected.ContainsKey("version") || expected["version"]!=release.VersionText || !expected.ContainsKey("SkynettMiniMap.exe") || expected["SkynettMiniMap.exe"]!=release.Sha256)throw new InvalidDataException("Package release identity mismatch.");
                    Directory.CreateDirectory(AssetDirectory(staged));
                    foreach(string name in new[]{"SkynettMiniMap.exe","assets/map-tiles.bin","assets/roads.bin"}) {
                        string hash;if(!expected.TryGetValue(name,out hash) || !System.Text.RegularExpressions.Regex.IsMatch(hash,@"\A[0-9a-f]{64}\z"))throw new InvalidDataException("Missing package checksum.");
                        string target=name=="SkynettMiniMap.exe"?staged:Path.Combine(AssetDirectory(staged),Path.GetFileName(name));
                        CopyEntry(zip.GetEntry(name),target);
                        if(InstallationAssets.Digest(target)!=hash)throw new InvalidDataException("Package file checksum mismatch.");
                    }
                    release.VerifyFile(staged);complete=true;
                }
            }finally { if(!complete)Cleanup(staged); }
        }
        static void CopyEntry(ZipArchiveEntry entry,string target) {
            using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                byte[] buffer=new byte[32768];long copied=0;int count;
                while((count=input.Read(buffer,0,buffer.Length))!=0) {
                    copied+=count;
                    if(copied>entry.Length || copied>128*1024*1024)throw new InvalidDataException("Update entry exceeds its declared size.");
                    output.Write(buffer,0,count);
                }
                if(copied!=entry.Length)throw new InvalidDataException("Truncated update entry.");
            }
        }
        internal static void ReplaceAndLaunch(string staged,string installed,string hash,string assetIdentity,Action<string> launch) {
            if(assetIdentity=="") { UpdateInstaller.ReplaceAndLaunch(staged,installed,hash,launch);return; }
            if(AssetIdentity(staged)!=assetIdentity)throw new InvalidDataException("Staged assets changed.");
            string target=Path.Combine(Path.GetDirectoryName(installed),"assets");
            Directory.CreateDirectory(target);
            if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Installation assets must not be a directory link.");
            var changed=new List<string>();var backups=new Dictionary<string,string>();
            try {
                foreach(string name in InstallationAssets.Names) {
                    string destination=Path.Combine(target,name),source=Path.Combine(AssetDirectory(staged),name);
                    if(File.Exists(destination)) { string backup=destination+"."+Guid.NewGuid().ToString("N")+".previous";File.Replace(source,destination,backup);backups.Add(destination,backup); }
                    else File.Move(source,destination);
                    changed.Add(destination);
                }
                if(DirectoryIdentity(target)!=assetIdentity)throw new InvalidDataException("Installed assets changed during replacement.");
                UpdateInstaller.ReplaceAndLaunch(staged,installed,hash,launch);
            }catch {
                foreach(string destination in Enumerable.Reverse(changed)) {
                    string backup;if(backups.TryGetValue(destination,out backup))File.Replace(backup,destination,null);else File.Delete(destination);
                }
                throw;
            }
            foreach(string backup in backups.Values)try { File.Delete(backup); }catch(IOException){}catch(UnauthorizedAccessException){}
            try { Directory.Delete(AssetDirectory(staged),false); }catch(IOException){}
        }
    }
}
