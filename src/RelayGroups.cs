using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace ScumMiniMap {
    internal sealed class RelayMembership {
        internal string Id,Key,Owner,Pending,Name;
        internal DirectPositions Session;
        internal string Code { get { return "MM1-"+Id+"-"+Key.TrimEnd('=').Replace('+','-').Replace('/','_'); } }
        internal bool IsOwner { get { return !string.IsNullOrEmpty(Owner); } }
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
        readonly object gate=new object();readonly string file,address,certificate;readonly int port;
        readonly List<RelayMembership> groups=new List<RelayMembership>();bool disposed;
        internal string DisplayName;
        internal RelayGroupStore(string directory,string address=null,int port=0,string certificate=null) {
            file=Path.Combine(directory,"private-groups.dat");this.address=address??DirectPositions.RelayAddress;this.port=port==0?DirectPositions.RelayPort:port;this.certificate=certificate??DirectPositions.RelayCertificate;
            DisplayName=Localization.Get("PrivateGroupSurvivor")+" "+Guid.NewGuid().ToString("N").Substring(0,4);
            if(!File.Exists(file))return;
            if(new FileInfo(file).Length>65536)throw new InvalidDataException();
            var lines=Encoding.UTF8.GetString(Protect(File.ReadAllBytes(file),false)).Split('\n');
            if(lines.Length<2||lines[0]!="1")throw new InvalidDataException();DisplayName=Text(lines[1]);ValidateName(DisplayName);
            foreach(string line in lines.Skip(2).Where(value=>value.Length>0)) {
                var fields=line.Split('\t');if(fields.Length!=5)throw new InvalidDataException();var group=RelayMembership.Parse(fields[0]);
                group.Owner=fields[1];group.Pending=fields[2];group.Name=Text(fields[3]);
                if(fields[4]!="1"||group.Owner.Length!=0&&(group.Owner.Length!=64||!group.Owner.All(Uri.IsHexDigit))||group.Pending!=""&&group.Pending!="CREATE"&&group.Pending!="ROTATE"||groups.Any(g=>g.Id==group.Id)||groups.Count>=16)throw new InvalidDataException();groups.Add(group);
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
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string text="1\n"+Base(DisplayName)+"\n"+string.Join("\n",groups.Select(g=>g.Code+"\t"+g.Owner+"\t"+g.Pending+"\t"+Base(g.Name)+"\t1"));
            byte[] plain=Encoding.UTF8.GetBytes(text);byte[] encrypted;try { encrypted=Protect(plain,true); }finally { Array.Clear(plain,0,plain.Length); }
            string temp=file+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(encrypted,0,encrypted.Length);stream.Flush(true); }
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
        internal RelayMembership[] Groups { get { lock(gate)return groups.ToArray(); } }
        internal DirectPositions[] Sessions { get { lock(gate)return groups.Where(g=>g.Session!=null).Select(g=>g.Session).ToArray(); } }
        internal void SetName(string name) { ValidateName(name);lock(gate) { if(disposed)throw new ObjectDisposedException("RelayGroupStore");DisplayName=name.Trim();Save(); } }
        internal RelayMembership Create() {
            var group=new RelayMembership { Id=Guid.NewGuid().ToString("N"),Key=PrivatePositions.NewKey(),Owner=BitConverter.ToString(Convert.FromBase64String(PrivatePositions.NewKey())).Replace("-","").ToLowerInvariant(),Pending="CREATE",Name="" };
            lock(gate) { if(disposed||groups.Count>=16)throw new InvalidOperationException();groups.Add(group);Save(); }
            Complete(group);return group;
        }
        internal RelayMembership Join(string code) {
            var parsed=RelayMembership.Parse(code);RelayMembership group;
            lock(gate) {
                if(disposed)throw new ObjectDisposedException("RelayGroupStore");group=groups.FirstOrDefault(g=>g.Id==parsed.Id);
                if(group==null) { if(groups.Count>=16)throw new InvalidOperationException();groups.Add(parsed);group=parsed; }
                else if(group.Key!=parsed.Key) { if(group.Pending!="")throw new InvalidOperationException();if(group.Session!=null)group.Session.Dispose();group.Session=null;group.Key=parsed.Key; }
                Save();
            }
            Connect(group);return group;
        }
        internal void Connect(RelayMembership group) {
            if(group.Pending!="") { Complete(group);return; }
            lock(gate) {
                if(disposed||!groups.Contains(group))throw new InvalidOperationException();if(group.Session!=null)group.Session.Dispose();
                group.Session=new DirectPositions(false,address,port,group.Key,DisplayName,false,null,true,certificate,group.Id);
            }
        }
        internal void Leave(RelayMembership group) { lock(gate) { if(group.Session!=null)group.Session.Dispose();group.Session=null; } }
        internal void Rotate(RelayMembership group) {
            lock(gate) {
                if(disposed||!groups.Contains(group)||!group.IsOwner)throw new InvalidOperationException();
                if(group.Pending=="") { if(group.Session!=null)group.Session.Dispose();group.Session=null;group.Key=PrivatePositions.NewKey();group.Pending="ROTATE";Save(); }
            }
            Complete(group);
        }
        void Complete(RelayMembership group) {
            // A retry must durably journal its key and owner capability as well,
            // even if an earlier local save failed before any network request.
            lock(gate) { if(disposed||!groups.Contains(group)||!group.IsOwner||group.Pending=="")throw new InvalidOperationException();Save(); }
            Manage(group.Pending,group.Id,DirectPositions.RelayRoom(Convert.FromBase64String(group.Key)),group.Owner);
            lock(gate) { if(disposed)return;group.Pending="";Save(); }
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
            lock(gate)return groups.Where(g=>g.Session!=null).SelectMany(g=>(positionsOnly?g.Session.Snapshot():g.Session.Roster()).Select(p=>new PrivatePositions.Peer { Id=g.Id+":"+p.Id,Name=p.Name+" · "+g.Id.Substring(0,6),Position=p.Position,Updated=p.Updated })).ToArray();
        }
        internal SharedMapMarker[] Markers() {
            lock(gate)return groups.Where(g=>g.Session!=null).SelectMany(g=>g.Session.SnapshotMarkers().Select(m=>new SharedMapMarker { Id=m.Id,Name=m.Name,Owner=m.Owner+" · "+g.Id.Substring(0,6),OwnerId=g.Id+":"+m.OwnerId,X=m.X,Y=m.Y,Death=m.Death })).ToArray();
        }
        public void Dispose() { lock(gate) { disposed=true;foreach(var group in groups)if(group.Session!=null) { group.Session.Dispose();group.Session=null; } } }
    }
}
