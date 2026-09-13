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
    public sealed class UpdateRetryException:Exception {
        public DateTime RetryAt { get; private set; }
        public UpdateRetryException(DateTime retry):base("Update service is temporarily unavailable.") { RetryAt=retry; }
    }
}
