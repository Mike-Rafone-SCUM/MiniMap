using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ScumMiniMap;
namespace ScumMiniMap { public static class MapWindow { public const string VersionString="1.2.4"; } }
static class UpdateTests {
    static int count;
    static void Check(bool pass,string name) { if(!pass)throw new Exception(name); count++; }
    static void Reject(Action action,string name) { try { action(); } catch { count++; return; } throw new Exception(name); }
    static string Manifest(string version,string hash) { return "schema=1\nversion="+version+"\nsha256="+hash+"\n"; }
    static int Main() {
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
            Check(File.Exists(destination),"Verified download missing");
            Reject(()=>service.Download(release,destination),"Existing executable overwritten");
            Check(File.ReadAllBytes(destination).Length==binary.Length,"Existing download changed");
            var corrupt=new UpdateService("owner/releases",root,(url,limit)=>Encoding.UTF8.GetBytes("MZcorrupt"));
            Reject(()=>corrupt.Download(release,Path.Combine(root,"corrupt.exe")),"Bad checksum accepted");
            Check(!File.Exists(Path.Combine(root,"corrupt.exe")),"Corrupt download retained");
            Check(Directory.GetFiles(root,"*.partial").Length==0,"Partial download retained");
            Console.WriteLine("Passed "+count+" GitHub updater regression checks.");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { foreach(string file in Directory.GetFiles(root))File.Delete(file); Directory.Delete(root); }
    }
}
