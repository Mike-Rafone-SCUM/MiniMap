using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace ScumMiniMap {
    internal sealed class RelayMembership {
        internal string Id,Key,Owner,Pending,Name,NameStamp="";
        internal DirectPositions Session;
        internal string Code { get { return "MM1-"+Id+"-"+Key.TrimEnd('=').Replace('+','-').Replace('/','_'); } }
        internal bool IsOwner { get { return !string.IsNullOrEmpty(Owner); } }
        internal string Label { get { return string.IsNullOrWhiteSpace(Name)?Localization.Get("PrivateGroupLabel")+" "+Id.Substring(0,8):Name; } }
        internal static bool IsNameMarker(SharedMapMarker marker) { return marker!=null&&marker.Id!=null&&marker.Id.Length==64&&marker.Id.StartsWith("4752504E",StringComparison.Ordinal); }
        internal static void ValidateGroupName(string value) { if(string.IsNullOrWhiteSpace(value)||value.Length>48||value.Any(char.IsControl))throw new ArgumentException(); }
        internal void Rename(string value) {
            ValidateGroupName(value);ulong revision=0;if(NameStamp.Length==64&&!ulong.TryParse(NameStamp.Substring(8,16),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out revision))throw new InvalidDataException();
            if(revision==ulong.MaxValue)throw new InvalidOperationException();Name=value.Trim();NameStamp="4752504E"+(revision+1).ToString("X16")+Guid.NewGuid().ToString("N").ToUpperInvariant()+"00000000";
        }
        internal static RelayMembership Parse(string code) {
            code=(code??"").Trim();
            if(code.Length!=80||!code.StartsWith("MM1-",StringComparison.Ordinal)||code[36]!='-')throw new ArgumentException();
            string id=code.Substring(4,32),key=code.Substring(37).Replace('-','+').Replace('_','/')+"=";
            if(!id.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||Convert.FromBase64String(key).Length!=32)throw new ArgumentException();
            var result=new RelayMembership { Id=id,Key=key,Owner="",Pending="",Name="" };
            if(result.Code!=code)throw new ArgumentException();return result;
        }
    }
    internal sealed class RelayGroupStore:IDisposable {
        readonly object gate=new object(),operations=new object();readonly string file,address,certificate;readonly int port;
        readonly Action<string,byte[]> persist;
        int nameRevision,savedNameRevision;bool nameSaveWorker;DateTime nextNameSave;volatile string persistenceError;
        internal string PersistenceError { get { return persistenceError; } }
        readonly List<RelayMembership> groups=new List<RelayMembership>();bool disposed;
        internal string DisplayName;
        internal readonly string MemberColorSeed;
        internal RelayGroupStore(string directory,string address=null,int port=0,string certificate=null,Action<string,byte[]> persist=null) {
            this.persist=persist??WriteProtectedFile;
            file=Path.Combine(directory,"private-groups.dat");this.address=address??DirectPositions.RelayAddress;this.port=port==0?DirectPositions.RelayPort:port;this.certificate=certificate??DirectPositions.RelayCertificate;
            string colorFile=Path.Combine(directory,"private-member-color.dat");
            if(File.Exists(colorFile)) {
                if(new FileInfo(colorFile).Length>4096)throw new InvalidDataException();
                MemberColorSeed=Encoding.UTF8.GetString(Protect(File.ReadAllBytes(colorFile),false));
                if(MemberColorSeed.Length!=8||!MemberColorSeed.All(Uri.IsHexDigit))throw new InvalidDataException();
            }else {
                MemberColorSeed=Guid.NewGuid().ToString("N").Substring(0,8);
                WriteProtectedFile(colorFile,Protect(Encoding.UTF8.GetBytes(MemberColorSeed),true));
            }
            DisplayName=Localization.Get("PrivateGroupSurvivor")+" "+Guid.NewGuid().ToString("N").Substring(0,4);
            if(!File.Exists(file))return;
            if(new FileInfo(file).Length>65536)throw new InvalidDataException();
            var lines=Encoding.UTF8.GetString(Protect(File.ReadAllBytes(file),false)).Split('\n');
            if(lines.Length<2||lines[0]!="1"&&lines[0]!="2")throw new InvalidDataException();bool version2=lines[0]=="2";DisplayName=Text(lines[1]);ValidateName(DisplayName);
            foreach(string line in lines.Skip(2).Where(value=>value.Length>0)) {
                var fields=line.Split('\t');if(fields.Length!=(version2?6:5))throw new InvalidDataException();var group=RelayMembership.Parse(fields[0]);
                group.Owner=fields[1];group.Pending=fields[2];group.Name=Text(fields[3]);
                group.NameStamp=version2?fields[4]:"";
                if(group.Name.Length>48||group.Name.Any(char.IsControl)||group.NameStamp!=""&&(group.NameStamp.Length!=64||!group.NameStamp.All(Uri.IsHexDigit)||!group.NameStamp.StartsWith("4752504E",StringComparison.Ordinal)))throw new InvalidDataException();
                if(fields[version2?5:4]!="1"||group.Owner.Length!=0&&(group.Owner.Length!=64||!group.Owner.All(Uri.IsHexDigit))||group.Pending!=""&&group.Pending!="CREATE"&&group.Pending!="ROTATE"&&group.Pending!="DELETE"||groups.Any(g=>g.Id==group.Id)||groups.Count>=16)throw new InvalidDataException();groups.Add(group);
            }
        }
        static string Text(string value) { return new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value)); }
        static string Base(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value??"")); }
        static byte[] Protect(byte[] bytes,bool encrypt) {
            var type=Type.GetType("System.Security.Cryptography.ProtectedData, System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",true);
            var scope=type.Assembly.GetType("System.Security.Cryptography.DataProtectionScope",true);
            return (byte[])type.GetMethod(encrypt?"Protect":"Unprotect").Invoke(null,new object[]{bytes,Encoding.UTF8.GetBytes("SCUM MiniMap private groups v1"),Enum.ToObject(scope,0)});
        }
        static void ValidateName(string name) { if(string.IsNullOrWhiteSpace(name)||name.Length>32||name.Any(char.IsControl))throw new ArgumentException(); }
        void Save() {
            string text;int revision;lock(gate) { text="2\n"+Base(DisplayName)+"\n"+string.Join("\n",groups.Select(g=>g.Code+"\t"+g.Owner+"\t"+g.Pending+"\t"+Base(g.Name)+"\t"+g.NameStamp+"\t1"));revision=nameRevision; }
            byte[] plain=Encoding.UTF8.GetBytes(text);byte[] encrypted;try { encrypted=Protect(plain,true); }finally { Array.Clear(plain,0,plain.Length); }
            persist(file,encrypted);lock(gate)savedNameRevision=Math.Max(savedNameRevision,revision);persistenceError=null;
        }
        static void WriteProtectedFile(string file,byte[] encrypted) {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp=file+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(encrypted,0,encrypted.Length);stream.Flush(true); }
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
        internal RelayMembership[] Groups { get { lock(gate)return groups.ToArray(); } }
        internal sealed class View {
            internal readonly RelayMembership Member;internal readonly string Id,Name,Pending,Label,Code;internal readonly bool IsOwner;internal readonly DirectPositions Session;
            internal View(RelayMembership group) { Member=group;Id=group.Id;Name=group.Name;Pending=group.Pending;Label=group.Label;Code=group.Code;IsOwner=group.IsOwner;Session=group.Session; }
        }
        internal View[] GroupViews { get { lock(gate)return groups.Select(g=>new View(g)).ToArray(); } }
        internal bool Connected(RelayMembership group) { lock(gate)return group.Session!=null&&group.Session.Error==null; }
        internal DirectPositions[] Sessions { get { lock(gate)return groups.Where(g=>g.Session!=null).Select(g=>g.Session).ToArray(); } }
        internal void SetName(string name) { ValidateName(name);lock(operations) { lock(gate) { if(disposed)throw new ObjectDisposedException("RelayGroupStore");DisplayName=name.Trim(); }Save(); } }
        internal RelayMembership Create(string name="") {
            var group=new RelayMembership { Id=Guid.NewGuid().ToString("N"),Key=PrivatePositions.NewKey(),Owner=BitConverter.ToString(Convert.FromBase64String(PrivatePositions.NewKey())).Replace("-","").ToLowerInvariant(),Pending="CREATE",Name="" };
            if(!string.IsNullOrWhiteSpace(name))group.Rename(name);
            lock(operations) { lock(gate) { if(disposed||groups.Count>=16)throw new InvalidOperationException();groups.Add(group); }Save();Complete(group);return group; }
        }
        internal RelayMembership Join(string code) {
            var parsed=RelayMembership.Parse(code);RelayMembership group;
            lock(operations) { lock(gate) {
                if(disposed)throw new ObjectDisposedException("RelayGroupStore");group=groups.FirstOrDefault(g=>g.Id==parsed.Id);
                if(group==null) { if(groups.Count>=16)throw new InvalidOperationException();groups.Add(parsed);group=parsed; }
                else if(group.Key!=parsed.Key) { if(group.Pending!="")throw new InvalidOperationException();if(group.Session!=null)group.Session.Dispose();group.Session=null;group.Key=parsed.Key; }
            }Save();Connect(group);return group; }
        }
        internal void Connect(RelayMembership group) {
            lock(operations) {
            if(group.Pending!="") { Complete(group);return; }
            lock(gate) {
                if(disposed||!groups.Contains(group))throw new InvalidOperationException();if(group.Session!=null)group.Session.Dispose();
                group.Session=new DirectPositions(false,address,port,group.Key,DisplayName,false,null,true,certificate,group.Id,MemberColorSeed);
            }
            }
        }
        internal void Leave(RelayMembership group) { lock(gate) { if(group.Session!=null)group.Session.Dispose();group.Session=null; } }
        internal void Rename(RelayMembership group,string name) { lock(operations) { lock(gate) { if(disposed||!groups.Contains(group))throw new InvalidOperationException();group.Rename(name); }Save(); } }
        internal void SynchronizeNames() {
            lock(gate) {
                bool changed=false;foreach(var group in groups.Where(g=>g.Session!=null)) {
                    var latest=group.Session.SnapshotMarkers().Where(RelayMembership.IsNameMarker).Where(m=>m.Name.Length<=48).OrderByDescending(m=>m.Id,StringComparer.Ordinal).FirstOrDefault();
                    if(latest!=null&&string.CompareOrdinal(latest.Id,group.NameStamp)>0) { group.Name=latest.Name;group.NameStamp=latest.Id;changed=true; }
                }
                if(changed)nameRevision++;
                if(nameRevision!=savedNameRevision&&!nameSaveWorker&&DateTime.UtcNow>=nextNameSave) { nameSaveWorker=true;System.Threading.Tasks.Task.Run((Action)SaveSynchronizedNames); }
            }
        }
        void SaveSynchronizedNames() {
            try {
                while(true) {
                    int revision;lock(operations) { lock(gate) { if(disposed) { nameSaveWorker=false;return; }revision=nameRevision; }Save(); }
                    lock(gate)if(revision==nameRevision) { nameSaveWorker=false;return; }
                }
            }catch(Exception error) { lock(gate) { persistenceError=error.Message;nextNameSave=DateTime.UtcNow.AddSeconds(2);nameSaveWorker=false; } }
        }
        internal SharedMapMarker[] MarkersFor(DirectPositions session,SharedMapMarker[] markers) {
            lock(gate) {
                var group=groups.FirstOrDefault(g=>g.Session==session);if(group==null||group.NameStamp=="")return markers;
                return new[]{new SharedMapMarker { Id=group.NameStamp,Name=group.Name,X=0,Y=0 }}.Concat(markers).Take(128).ToArray();
            }
        }
        internal void Rotate(RelayMembership group) {
            lock(operations) { lock(gate) {
                if(disposed||!groups.Contains(group)||!group.IsOwner||group.Pending=="DELETE")throw new InvalidOperationException();
                if(group.Pending=="") { if(group.Session!=null)group.Session.Dispose();group.Session=null;group.Key=PrivatePositions.NewKey();group.Pending="ROTATE"; }
            }Save();Complete(group); }
        }
        internal void Delete(RelayMembership group) {
            lock(operations) { lock(gate) {
                if(disposed||!groups.Contains(group)||!group.IsOwner||group.Pending!=""&&group.Pending!="DELETE")throw new InvalidOperationException();
                if(group.Session!=null)group.Session.Dispose();group.Session=null;group.Pending="DELETE";
            }Save();Complete(group); }
        }
        void Complete(RelayMembership group) {
            // A retry must durably journal its key and owner capability as well,
            // even if an earlier local save failed before any network request.
            lock(gate) { if(disposed||!groups.Contains(group)||!group.IsOwner||group.Pending=="")throw new InvalidOperationException(); }Save();
            string action=group.Pending;
            Manage(action,group.Id,DirectPositions.RelayRoom(Convert.FromBase64String(group.Key)),group.Owner);
            if(action=="DELETE") { int index;lock(gate) { index=groups.IndexOf(group);groups.Remove(group); }try { Save(); }catch { lock(gate)if(index>=0)groups.Insert(index,group);throw; }return; }
            lock(gate) { if(disposed)return;group.Pending=""; }Save();
            Connect(group);
        }
        void Manage(string action,string id,string room,string owner) {
            using(var client=new System.Net.Sockets.TcpClient()) {
                var task=client.ConnectAsync(address,port);if(!task.Wait(5000))throw new TimeoutException();client.ReceiveTimeout=client.SendTimeout=5000;
                using(var stream=new System.Net.Security.SslStream(client.GetStream(),false,(sender,cert,chain,errors)=> {
                    if(cert==null)return false;using(var sha=SHA256.Create())return string.Equals(BitConverter.ToString(sha.ComputeHash(cert.GetRawCertData())).Replace("-",""),certificate,StringComparison.OrdinalIgnoreCase);
                })) {
                    stream.AuthenticateAsClient("minimap-relay",null,System.Security.Authentication.SslProtocols.Tls12,false);
                    byte[] request=Encoding.ASCII.GetBytes("SCUM"+action+"1 "+id+" "+room+" "+owner+"\n");stream.Write(request,0,request.Length);Array.Clear(request,0,request.Length);
                    if(stream.ReadByte()!='O'||stream.ReadByte()!='K'||stream.ReadByte()!='\n')throw new IOException("Group operation rejected");
                }
            }
        }
        internal PrivatePositions.Peer[] Roster(bool positionsOnly=false) {
            lock(gate)return groups.Where(g=>g.Session!=null).SelectMany(g=>(positionsOnly?g.Session.Snapshot():g.Session.Roster()).Select(p=>new PrivatePositions.Peer { Id=g.Id+":"+p.Id,Name=p.Name+" · "+g.Label,Position=p.Position,Updated=p.Updated })).ToArray();
        }
        internal SharedMapMarker[] Markers() {
            lock(gate)return groups.Where(g=>g.Session!=null).SelectMany(g=>g.Session.SnapshotMarkers().Where(m=>!RelayMembership.IsNameMarker(m)).Select(m=>new SharedMapMarker { Id=m.Id,Name=m.Name,Owner=m.Owner+" · "+g.Label,OwnerId=g.Id+":"+m.OwnerId,X=m.X,Y=m.Y,Death=m.Death })).ToArray();
        }
        public void Dispose() { lock(gate) { disposed=true;foreach(var group in groups)if(group.Session!=null) { group.Session.Dispose();group.Session=null; } } }
    }
}
