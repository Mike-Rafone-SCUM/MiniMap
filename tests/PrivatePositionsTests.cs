using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ScumMiniMap;
class PrivatePositionsTests {
    sealed class FakeFirewall:IPeerFirewallPolicy {
        internal PeerFirewallRule Rule;internal int Adds,Removes;internal bool Deny,Corrupt;
        public PeerFirewallRule Get(string name) { return Rule!=null&&Rule.Name==name?Rule:null; }
        public void Add(PeerFirewallRule rule) { Adds++;if(Deny)throw new UnauthorizedAccessException();Rule=rule;if(Corrupt)Rule.Direction=2; }
        public void Remove(string name) { Removes++;if(Rule!=null&&Rule.Name==name)Rule=null; }
        public void Dispose() {}
    }
    static void Check(bool value,string message) { if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message); }
    static bool Rejected(byte[] key,byte[] packet) { try { PrivatePositions.Open(key,packet);return false; }catch(CryptographicException) { return true; }catch(InvalidDataException) { return true; } }
    static void Await(Func<bool> condition) { for(int i=0;i<100;i++) { if(condition())return;Thread.Sleep(100); }throw new Exception("Peer exchange timed out"); }
    static void Main() {
        string application=Path.Combine(Path.GetTempPath(),"Synthetic MiniMap folder","MiniMap.exe");var firewall=new FakeFirewall();
        Check(PeerFirewall.Ensure(application,47831,firewall)&&firewall.Rule.Application==Path.GetFullPath(application)&&firewall.Rule.Ports=="47831"&&firewall.Rule.Protocol==6&&firewall.Rule.Direction==1&&firewall.Rule.Profiles==7,"Automatic firewall exception permits only this executable and selected inbound TCP port on all network profiles");
        Check(PeerFirewall.Ensure(application,47831,firewall)&&firewall.Adds==1,"Repeated hosting reuses the verified managed firewall rule without duplicates");
        Check(PeerFirewall.Expected(application,47832).Name!=firewall.Rule.Name&&PeerFirewall.Expected(application.ToUpperInvariant(),47831).Name==firewall.Rule.Name,"Firewall identity distinguishes ports while preserving Windows path case equivalence");
        firewall.Rule.Enabled=false;Check(!PeerFirewall.Ensure(application,47831,firewall)&&firewall.Adds==1&&firewall.Removes==0,"A deliberately disabled existing rule is not silently overridden");
        var conflict=new FakeFirewall { Rule=PeerFirewall.Expected(application,47831) };conflict.Rule.Description="User-managed rule";Check(!PeerFirewall.Ensure(application,47831,conflict)&&conflict.Adds==0&&conflict.Removes==0,"Unrelated firewall rules are never overwritten or removed");
        var corruptFirewall=new FakeFirewall { Corrupt=true };Check(!PeerFirewall.Ensure(application,47831,corruptFirewall)&&corruptFirewall.Removes==1&&corruptFirewall.Rule==null,"A newly created rule that fails scope verification is rolled back");
        var deniedFirewall=new FakeFirewall { Deny=true };bool denied=false;try { PeerFirewall.Ensure(application,47831,deniedFirewall); }catch(UnauthorizedAccessException) { denied=true; }Check(denied&&deniedFirewall.Rule==null,"Firewall access denial is reported without claiming success");
        var localGateway=System.Net.IPAddress.Parse("192.168.1.1");Uri descriptor=RouterAddressDiscovery.Location("HTTP/1.1 200 OK\r\nLOCATION: http://192.168.1.1:1234/root.xml\r\n",localGateway);
        Check(descriptor!=null,"UPnP discovery accepts a descriptor hosted by the local default gateway");
        foreach(string remote in new[]{"http://8.8.8.8/root.xml","http://router.example/root.xml","http://user:pass@192.168.1.1/root.xml","https://192.168.1.1/root.xml"})Check(RouterAddressDiscovery.Location("HTTP/1.1 200 OK\r\nLOCATION: "+remote+"\r\n",localGateway)==null,"Router discovery rejects outside, credentialed or unsupported endpoints: "+remote);
        string device="<root xmlns='urn:schemas-upnp-org:device-1-0'><device><serviceList><service><serviceType>urn:schemas-upnp-org:service:WANIPConnection:1</serviceType><controlURL>/control/wan</controlURL></service></serviceList></device></root>";
        var services=RouterAddressDiscovery.Services(System.Text.Encoding.UTF8.GetBytes(device),descriptor,localGateway);Check(services.Length==1&&services[0].Control.AbsoluteUri=="http://192.168.1.1:1234/control/wan","UPnP resolves the local WAN control URL from the router descriptor");
        Check(RouterAddressDiscovery.Services(System.Text.Encoding.UTF8.GetBytes(device.Replace("/control/wan","http://8.8.8.8/control")),descriptor,localGateway).Length==0,"UPnP never follows a control endpoint outside the gateway");
        string soap="<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><u:GetExternalIPAddressResponse xmlns:u='urn:schemas-upnp-org:service:WANIPConnection:1'><NewExternalIPAddress>8.8.8.8</NewExternalIPAddress></u:GetExternalIPAddressResponse></s:Body></s:Envelope>";
        Check(RouterAddressDiscovery.SoapAddress(System.Text.Encoding.UTF8.GetBytes(soap))=="8.8.8.8"&&RouterAddressDiscovery.SoapAddress(System.Text.Encoding.UTF8.GetBytes(soap.Replace("8.8.8.8","100.64.0.1")))==null,"UPnP WAN address parsing accepts public IPv4 and rejects CGNAT addresses");
        bool entityRejected=false;try { RouterAddressDiscovery.Services(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE root [<!ENTITY test SYSTEM 'file:///C:/Windows/win.ini'>]><root>&test;</root>"),descriptor,localGateway); }catch(System.Xml.XmlException) { entityRejected=true; }Check(entityRejected,"Router XML cannot load external entities or files");
        int upnpCalls=0;Check(RouterAddressDiscovery.Find(new[]{localGateway},g=> { throw new IOException(); },g=> { upnpCalls++;return "8.8.8.8"; })=="8.8.8.8"&&upnpCalls==1,"UPnP fills the public address when NAT-PMP is unavailable, without an outside lookup service");
        Check(RouterAddressDiscovery.Find(new[]{localGateway},g=>"172.24.0.1",g=>"192.168.1.1")==null,"Failed local discovery never substitutes a private adapter address as the public IP");
        int routerPort=47831,deletions=0;bool failRenewal=false;
        byte[] publicReply={0,128,0,0,0,0,0,1,8,8,8,8};
        byte[] mapReply={0,130,0,0,0,0,0,1,(byte)(routerPort>>8),(byte)routerPort,(byte)(routerPort>>8),(byte)routerPort,0,0,2,88};
        var fakeGateway=System.Net.IPAddress.Parse("192.168.1.1");
        Func<System.Net.IPAddress,byte[],byte[]> fakeRouter=(gateway,request)=> {
            if(!gateway.Equals(fakeGateway))throw new Exception("Unexpected gateway");
            if(request.Length==2)return (byte[])publicReply.Clone();
            if(request.Skip(8).All(value=>value==0)) { Interlocked.Increment(ref deletions);return (byte[])mapReply.Clone(); }
            if(failRenewal)throw new IOException();return (byte[])mapReply.Clone();
        };
        byte[] remove=PeerRouter.Request(routerPort,true);
        Check(remove[4]==(byte)(routerPort>>8)&&remove[5]==(byte)routerPort&&remove.Skip(6).All(value=>value==0),"Router cleanup deletes only the selected internal TCP port");
        var mapping=PeerRouter.Open(routerPort,new[]{fakeGateway},fakeRouter);
        Check(mapping!=null&&mapping.Ready&&mapping.Address=="8.8.8.8","Local router discovery and TCP lease work without external services");
        failRenewal=true;typeof(PeerRouter).GetMethod("Renew",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(mapping,new object[]{null});
        Check(!mapping.Ready,"Failed lease renewal stops advertising router readiness");mapping.Dispose();Await(()=>deletions==1);mapping.Dispose();Thread.Sleep(50);Check(deletions==1,"Stopping host withdraws only its lease once");
        failRenewal=false;mapReply[11]++;Check(PeerRouter.Open(routerPort,new[]{fakeGateway},fakeRouter)==null&&deletions==2,"Reassigned external port is withdrawn rather than advertised incorrectly");mapReply[11]--;
        publicReply[8]=100;publicReply[9]=64;Check(PeerRouter.Open(routerPort,new[]{fakeGateway},fakeRouter)==null,"CGNAT gateway address cannot be advertised as internet-ready");publicReply[8]=8;publicReply[9]=8;
        foreach(int index in new[]{0,1,2,3,8,9,10,11,12,13}) { var invalid=(byte[])mapReply.Clone();invalid[index]^=128;Check(!PeerRouter.MappingAccepted(invalid,routerPort),"Invalid mapping response rejected at byte "+index); }
        var expired=(byte[])mapReply.Clone();expired[14]=expired[15]=0;Check(!PeerRouter.MappingAccepted(expired,routerPort),"Expired router lease rejected");
        var oldMapping=PeerRouter.Open(routerPort,new[]{fakeGateway},fakeRouter);var replacement=PeerRouter.Open(routerPort,new[]{fakeGateway},fakeRouter);int beforeRestart=deletions;
        oldMapping.Dispose();replacement.Dispose();Await(()=>deletions==beforeRestart+1);
        Check(deletions==beforeRestart+1,"Late cleanup from an older host cannot delete the replacement host's mapping");
        Check(PublicPositionAddress.Parse("8.8.8.8\n")=="8.8.8.8","Public IPv4 detection accepts a canonical external address");
        foreach(string invalid in new[]{"172.24.0.1","192.168.0.14","10.0.0.1","100.64.0.1","127.0.0.1","169.254.1.1","0.0.0.0","224.0.0.1","255.255.255.255","192.0.2.1","198.51.100.1","203.0.113.1","198.18.0.1","::1","8.8.8.8:47831","0x08080808",new string('x',65)})Check(PublicPositionAddress.Parse(invalid)==null,"Public address lookup rejects private, reserved or malformed output: "+invalid);
        byte[] key=Convert.FromBase64String(PrivatePositions.NewKey());var packet=PrivatePositions.Seal(key,"synthetic private position");
        Check(PrivatePositions.Open(key,packet)=="synthetic private position","Authenticated encryption round trip");
        Check(!packet.SequenceEqual(PrivatePositions.Seal(key,"synthetic private position")),"Each packet has a fresh random IV");
        Check(Rejected(Convert.FromBase64String(PrivatePositions.NewKey()),packet),"Wrong group key fails closed");
        for(int i=0;i<packet.Length;i++) { byte[] damaged=(byte[])packet.Clone();damaged[i]^=1;Check(Rejected(key,damaged),"Packet tampering rejected at byte "+i); }
        Check(Rejected(key,new byte[PrivatePositions.MaximumPacketSize+1])&&Rejected(key,new byte[16]),"Oversized and truncated packets rejected");
        var now=DateTime.UtcNow;Check(PrivatePositions.Fresh(now.AddSeconds(-29),now)&&!PrivatePositions.Fresh(now.AddSeconds(-31),now)&&!PrivatePositions.Fresh(now.AddSeconds(6),now),"Stale and future positions rejected");
        string folder=Path.Combine(Path.GetTempPath(),"MiniMap-private-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        PrivatePositions a=null,b=null,outsider=null;
        try {
            File.WriteAllBytes(Path.Combine(folder,"stale.scumpos"),PrivatePositions.Seal(key,"1\tExpired fixture\t"+now.AddSeconds(-31).Ticks+"\t123\t456\t0\t90"));
            File.WriteAllBytes(Path.Combine(folder,"invalid.scumpos"),PrivatePositions.Seal(key,"1\tInvalid fixture\t"+now.Ticks+"\tNaN\t456\t0\t90"));
            File.WriteAllBytes(Path.Combine(folder,"future.scumpos"),PrivatePositions.Seal(key,"1\tFuture fixture\t"+now.AddMinutes(5).Ticks+"\t123\t456\t0\t90"));
            a=new PrivatePositions(folder,Convert.ToBase64String(key),"Synthetic A");b=new PrivatePositions(folder,Convert.ToBase64String(key),"Synthetic B");outsider=new PrivatePositions(folder,PrivatePositions.NewKey(),"Other group");
            a.Update(new Position { X=123,Y=456,Yaw=90 },now);b.Update(new Position { X=789,Y=321,Yaw=-90 },now);
            Await(()=>a.Snapshot().Length==1&&b.Snapshot().Length==1);
            Check(a.Snapshot()[0].Name=="Synthetic B"&&b.Snapshot()[0].Position.X==123,"Two clients exchange only their own opt-in positions");
            Check(a.Snapshot().All(p=>!p.Name.EndsWith("fixture")),"Authenticated stale, future and invalid-coordinate fixtures stay hidden");
            Check(outsider.Snapshot().Length==0,"Another group cannot read positions in the same folder");
            Check(Directory.GetFiles(folder,"*.scumpos").All(f=>!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(f)).Contains("Synthetic")),"Names are encrypted on disk");
            a.Update(null,now);Await(()=>b.Snapshot().Length==0);Check(true,"Pausing sharing removes the local position");
            b.Dispose();Check(b.Snapshot().Length==0,"Disconnect clears local peer visibility immediately");b=null;
        }finally { if(a!=null)a.Dispose();if(b!=null)b.Dispose();if(outsider!=null)outsider.Dispose();Thread.Sleep(300);string resolved=Path.GetFullPath(folder),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!resolved.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("MiniMap-private-",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup path");Directory.Delete(resolved,true); }
        var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        DirectPositions server=null,friend=null,second=null,wrong=null;
        try {
            string secret=Convert.ToBase64String(key);now=DateTime.UtcNow;
            Func<DateTime> hostClock=()=>DateTime.UtcNow.AddHours(-2),friendClock=()=>DateTime.UtcNow.AddHours(-2).AddSeconds(10),secondClock=()=>DateTime.UtcNow.AddDays(1);
            server=new DirectPositions(true,"",port,secret,"Host fixture",false,hostClock);friend=new DirectPositions(false,"127.0.0.1",port,secret,"Friend fixture",false,friendClock);second=new DirectPositions(false,"127.0.0.1",port,secret,"Second fixture",false,secondClock);
            server.Update(new Position { X=100,Y=200,Yaw=90 },hostClock());friend.Update(new Position { X=300,Y=400,Yaw=45 },friendClock());second.Update(new Position { X=500,Y=600,Yaw=0 },secondClock());
            var rally=new SharedMapMarker { Id=new string('A',64),Name="Rally fixture",X=.2,Y=.3 };
            var death=new SharedMapMarker { Id=new string('B',64),Name="Death fixture",X=.4,Y=.5,Death=true };
            var friendRally=new SharedMapMarker { Id=new string('A',64),Name="Friend rally",X=.6,Y=.7 };
            server.UpdateMarkers(new[]{rally,death});friend.UpdateMarkers(new[]{friendRally});
            Await(()=>server.Snapshot().Length==2&&friend.Snapshot().Length==2&&second.Snapshot().Length==2);
            Check(friend.Ready&&friend.Snapshot().Any(p=>p.Name=="Second fixture"&&p.Position.X==500),"Direct host relays authenticated positions between joined clients");
            Check(server.Snapshot().Any(p=>p.Name=="Friend fixture"&&p.Position.X==300)&&friend.Snapshot().Any(p=>p.Name=="Host fixture"&&p.Position.X==100),"Both players see each other with the ten-second clock difference that previously caused one-way sharing");
            Await(()=>friend.SnapshotMarkers().Length==2&&server.SnapshotMarkers().Length==1&&second.SnapshotMarkers().Length==3);
            Check(friend.SnapshotMarkers().Any(marker=>marker.Death&&marker.Owner=="Host fixture")&&second.SnapshotMarkers().Count(marker=>marker.Id==rally.Id)==2,"Death markers and waypoints reach every member, with marker identities scoped to their owner");
            var manyMarkers=Enumerable.Range(0,128).Select(index=>new SharedMapMarker { Id=index.ToString("X64"),Name=new string('w',96),X=.25,Y=.35 }).ToArray();server.UpdateMarkers(manyMarkers);
            Await(()=>friend.SnapshotMarkers().Length==128);Check(true,"A bounded full marker snapshot larger than the old 1 KB frame limit is delivered intact");
            rally.Name="Renamed rally";rally.X=.25;server.UpdateMarkers(new[]{rally,death});friend.UpdateMarkers(new SharedMapMarker[0]);
            Await(()=>friend.SnapshotMarkers().Length==2&&friend.SnapshotMarkers().Any(marker=>marker.Name=="Renamed rally"&&marker.X==.25)&&server.SnapshotMarkers().Length==0&&second.SnapshotMarkers().Length==2);
            Check(true,"Marker rename, movement and owner deletion replace the shared snapshot without stale duplicates");friend.UpdateMarkers(new[]{friendRally});Await(()=>server.SnapshotMarkers().Length==1);
            friend.Update(new Position { X=301,Y=400,Yaw=45 },friendClock().AddSeconds(-12));
            Await(()=>second.Snapshot().Any(p=>p.Name=="Friend fixture"&&p.Position.X==301));
            Check((secondClock()-second.Snapshot().Single(p=>p.Name=="Friend fixture").Updated).TotalSeconds>=11,"Host forwarding preserves capture age instead of refreshing old locations");
            friend.Update(new Position { X=302,Y=400,Yaw=45 },friendClock());
            Await(()=>server.Snapshot().Any(p=>p.Name=="Friend fixture"&&p.Position.X==302));
            using(var replay=new System.Net.Sockets.TcpClient("127.0.0.1",port)) {
                string replayId=Guid.NewGuid().ToString("N");var stream=replay.GetStream();
                Action<long,int,int> send=(number,age,x)=> { var frame=PrivatePositions.Seal(key,"3\t"+replayId+"\tReplay fixture\t"+number+"\t"+age+"\t0\t"+x+"\t200\t0\t90");var header=BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(frame.Length));stream.Write(header,0,4);stream.Write(frame,0,frame.Length); };
                send(1,0,900);Await(()=>server.Snapshot().Any(p=>p.Name=="Replay fixture"&&p.Position.X==900));
                send(1,0,999);Thread.Sleep(150);Check(server.Snapshot().Single(p=>p.Name=="Replay fixture").Position.X==900,"Replayed sequence cannot replace a received position");
                send(2,31000,999);Thread.Sleep(150);Check(server.Snapshot().Single(p=>p.Name=="Replay fixture").Position.X==900,"Already stale network samples are rejected without relying on remote clocks");
                send(3,0,901);Await(()=>server.Snapshot().Any(p=>p.Name=="Replay fixture"&&p.Position.X==901));
            }
            Await(()=>server.Snapshot().Length==2);
            wrong=new DirectPositions(false,"127.0.0.1",port,PrivatePositions.NewKey(),"Wrong key");wrong.Update(new Position { X=700 },now);
            Await(()=>wrong.Error!=null);Check(!wrong.Ready&&wrong.Snapshot().Length==0&&server.Snapshot().Length==2,"Wrong-key direct join fails closed without leaking or publishing positions");
            using(var raw=new System.Net.Sockets.TcpClient("127.0.0.1",port)) { raw.GetStream().Write(new byte[4],0,4);raw.ReceiveTimeout=3000;Check(raw.GetStream().ReadByte()==-1,"Invalid network frame is disconnected before allocation"); }
            friend.Update(null,now);Await(()=>server.Snapshot().Length==1&&second.Snapshot().Length==1);Check(true,"Pausing direct sharing withdraws the position from the whole group");
            Check(server.SnapshotMarkers().Any(marker=>marker.Owner=="Friend fixture"),"Pausing live capture preserves the connected owner's shared saved markers");
            friend.Dispose();Check(friend.Snapshot().Length==0&&friend.SnapshotMarkers().Length==0&&!friend.Ready,"Direct disconnect clears peer and marker visibility immediately");friend=null;Await(()=>server.SnapshotMarkers().Length==0);
            server.Dispose();server=null;Await(()=>!second.Ready);Check(second.Snapshot().Length==0&&second.SnapshotMarkers().Length==0,"Losing the host closes the session and atomically clears positions and markers");
        }finally { if(server!=null)server.Dispose();if(friend!=null)friend.Dispose();if(second!=null)second.Dispose();if(wrong!=null)wrong.Dispose(); }
    }
}
