using System;
using System.IO;
using System.Linq;
using System.Threading;
using ScumMiniMap;
class SquadStateTests {
    static void Check(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name); }
    static void Await(Func<bool> condition) { for(int i=0;i<100;i++) { if(condition())return;Thread.Sleep(100); }throw new Exception("Encrypted exchange timed out"); }
    static void Main() {
        var now=DateTime.UtcNow;var sender=new SquadState(()=>now);var receiver=new SquadState(()=>now);
        Check(sender.Ping(0,"Danger",.2,.3),"Danger ping accepted at a valid position");Check(!sender.Ping(1,"Loot",.2,.3),"Repeated ping presses are rate limited");
        var ping=sender.Local().Single();Check(SharedMapMarker.Valid(ping)&&SquadState.IsPing(ping),"Ping fits the existing encrypted marker protocol");ping.OwnerId="groupA:player";
        Check(receiver.Receive(new[]{ping}).Length==1,"Incoming ping visible");now=now.AddSeconds(59);Check(receiver.Receive(new[]{ping}).Length==1,"Heartbeat updates do not reset ping age");now=now.AddSeconds(1);
        Check(sender.Local().Length==0&&receiver.Receive(new[]{ping}).Length==0,"Both sender and receiver expire pings after 60 seconds");
        ping.OwnerId="groupB:player";Check(receiver.Receive(new[]{ping}).Length==1,"Expiry is independently scoped per group and player");Check(!sender.Ping(0,"Invalid",double.NaN,.3),"Invalid coordinates cannot become shared pings");
        for(int i=0;i<16;i++)Check(sender.AddStop("Stop "+i,.1+i*.01,.2),"Ordered route stop "+i+" accepted");Check(!sender.AddStop("Overflow",.5,.5),"Route is bounded to 16 stops");
        var route=sender.Stops.Reverse().ToArray();foreach(var m in route)m.OwnerId="A";Check(SquadState.Routes(route).Single().Select(m=>m.Name).SequenceEqual(sender.Stops.Select(m=>m.Name)),"Shuffled encrypted snapshots restore route order");
        Check(SquadState.Routes(route.Where(m=>SquadState.StopIndex(m)!=2)).Length==0,"Incomplete route cannot silently skip a missing stop");
        var other=route.Select(m=>m.Copy()).ToArray();foreach(var m in other)m.OwnerId="B";Check(SquadState.Routes(route.Concat(other)).Length==2,"Equal route IDs from different groups or owners do not merge");
        sender.RemoveLast();Check(sender.Stops.Length==15,"Removing the last stop preserves contiguous order");sender.ClearRoute();Check(sender.Stops.Length==0,"Clearing withdraws shared route stops");
        var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();string key=PrivatePositions.NewKey();
        using(var host=new DirectPositions(true,"",port,key,"Leader"))using(var client=new DirectPositions(false,"127.0.0.1",port,key,"Scout")) {
            now=now.AddSeconds(2);sender.Ping(1,"Loot",.4,.5);sender.AddStop("Bunker",.1,.2);sender.AddStop("Fuel",.3,.4);sender.AddStop("Trader",.5,.6);host.UpdateMarkers(sender.Local());
            Await(()=>client.SnapshotMarkers().Length==4);var markers=client.SnapshotMarkers();Check(markers.Count(SquadState.IsPing)==1&&SquadState.Routes(markers).Single().Length==3,"Encrypted TCP exchanges pings and complete ordered routes without protocol changes");
            now=now.AddSeconds(61);host.UpdateMarkers(sender.Local());Await(()=>client.SnapshotMarkers().Length==3);Check(!client.SnapshotMarkers().Any(SquadState.IsPing),"Expired ping is withdrawn from the network snapshot");
        }
        string folder=Path.Combine(Path.GetTempPath(),"MiniMap-squad-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"presets.dat");
        try {
            var preset=new MapPreset { Name="Looting",Map="default",Categories="v2;1,4",ZoneLayers="Hidden",Zones="key",Grid=true,Waypoints=true,History=false,Labels=true,ShowZones=true,Poi=true,Rotate=true,Cluster=false,Opacity=73,Zoom=4,TacticalOpacity=63,TacticalZoom=3 };
            MapPresetFile.Save(file,new[]{preset});var loaded=MapPresetFile.Load(file).Single();Check(loaded.Name==preset.Name&&loaded.Categories==preset.Categories&&loaded.Opacity==73&&loaded.Zoom==4&&loaded.TacticalOpacity==63&&loaded.TacticalZoom==3&&loaded.Rotate&&!loaded.Cluster,"Map preset persists categories, layers and both map view settings");
            preset.Opacity=92;MapPresetFile.Save(file,new[]{preset});Check(MapPresetFile.Load(file).Single().Opacity==92,"Saving replaces an existing preset file atomically");
            File.WriteAllBytes(file,new byte[]{1,2,3});bool rejected=false;try { MapPresetFile.Load(file); }catch(IOException) { rejected=true; }Check(rejected,"Truncated presets fail closed");
        }finally { foreach(string item in Directory.GetFiles(folder))File.Delete(item);Directory.Delete(folder); }
    }
}
