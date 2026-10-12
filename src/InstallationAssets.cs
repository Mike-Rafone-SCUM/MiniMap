using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ScumMiniMap {
    internal static class InstallationAssets {
        internal static readonly string[] Names={"map-tiles.bin","roads.bin"};
        internal static string Digest(Stream stream) {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        internal static string Digest(string path) { using(var stream=File.OpenRead(path))return Digest(stream); }
        internal static Stream Open(string name) {
            if(Array.IndexOf(Names,name)<0)throw new ArgumentException("Unknown installation asset.");
            var assembly=typeof(InstallationAssets).Assembly;
            using(var manifest=assembly.GetManifestResourceStream("installation-assets.txt")) {
                // Regression hosts and older development builds may still embed assets.
                if(manifest==null)return assembly.GetManifestResourceStream(name);
                using(var reader=new StreamReader(manifest,Encoding.UTF8)) {
                    string expected=null;
                    foreach(string line in reader.ReadToEnd().Split('\n')) {
                        string[] fields=line.Trim().Split('=');
                        if(fields.Length==2 && fields[0]==name)expected=fields[1];
                    }
                    if(expected==null)throw new InvalidDataException("Installation asset identity is missing: "+name);
                    return OpenVerified(Path.Combine(Path.GetDirectoryName(assembly.Location),"assets",name),expected);
                }
            }
        }
        internal static Stream OpenVerified(string path,string expected) {
            FileStream stream=null;
            try {
                stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(!string.Equals(Digest(stream),expected,StringComparison.Ordinal))throw new InvalidDataException("Installation asset checksum mismatch: "+Path.GetFileName(path));
                stream.Position=0;return stream;
            }catch { if(stream!=null)stream.Dispose();throw; }
        }
        internal static void Validate() { foreach(string name in Names)using(var stream=Open(name)) { if(stream==null)throw new FileNotFoundException("Extract the complete MiniMap ZIP, including its assets folder.",name); } }
    }
}
