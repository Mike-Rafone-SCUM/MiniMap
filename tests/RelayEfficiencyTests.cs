using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using ScumMiniMap;
namespace ScumMiniMap { internal static class Localization { internal static string Get(string key) { return "Survivor"; } } }
class RelayEfficiencyTests {
 static void Check(bool value,string message) { if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message); }
 static void Await(Func<bool> test,string message) { for(int i=0;i<160;i++) { if(test()) { Check(true,message);return; }Thread.Sleep(100); }throw new Exception(message); }
 static void Main(string[] args) {
  string folder=Path.Combine(Path.GetTempPath(),"MiniMap-efficiency-"+Guid.NewGuid().ToString("N"));
  using(var owner=new RelayGroupStore(Path.Combine(folder,"owner"),args[0],int.Parse(args[1]),args[2]))
  using(var member=new RelayGroupStore(Path.Combine(folder,"member"),args[0],int.Parse(args[1]),args[2])) {
   owner.SetName("Efficiency owner");member.SetName("Efficiency member");var group=owner.Create("Efficiency fixture");var joined=member.Join(group.Code);
   Await(()=>group.Session.Ready&&joined.Session.Ready&&group.Session.CompactUpdates&&joined.Session.CompactUpdates,"New peers negotiate compact updates");
   var markers=Enumerable.Range(0,100).Select(i=>new SharedMapMarker { Id=i.ToString("X64"),Name=new string('x',90)+i,X=.2,Y=.3 }).ToArray();
   group.Session.UpdateMarkers(markers);group.Session.Update(new Position { X=100,Y=200,Yaw=90 },DateTime.UtcNow);
   Await(()=>joined.Session.SnapshotMarkers().Length==100,"Full marker snapshot reaches a new peer");
   long start=Interlocked.Read(ref group.Session.SentBytes);Thread.Sleep(10100);long compactBytes=Interlocked.Read(ref group.Session.SentBytes)-start;
   Check(joined.Session.SnapshotMarkers().Length==100,"Position-only updates retain all unchanged markers across periodic full refreshes");
   using(var older=new DirectPositions(false,args[0],int.Parse(args[1]),group.Key,"Older client",false,null,true,args[2],group.Id,null,new string('e',64),null,false)) {
    Await(()=>older.Ready&&!group.Session.CompactUpdates&&!joined.Session.CompactUpdates,"An older client makes the room fall back to compatible full snapshots");
    Await(()=>older.SnapshotMarkers().Length==100,"An older client receives markers without compact packet errors");
    start=Interlocked.Read(ref group.Session.SentBytes);Thread.Sleep(10100);long fullBytes=Interlocked.Read(ref group.Session.SentBytes)-start;
    Check(compactBytes<fullBytes/2,"Negotiated updates use less than half the full-snapshot bandwidth for unchanged markers");Console.WriteLine("BYTES: compact="+compactBytes+" full="+fullBytes);
   }
   Await(()=>group.Session.CompactUpdates&&joined.Session.CompactUpdates,"Compact updates resume after the older client leaves");
   group.Session.UpdateMarkers(new SharedMapMarker[0]);Await(()=>joined.Session.SnapshotMarkers().Length==0,"Deleting all markers immediately replaces the retained snapshot");
   var interrupted=joined.Session;var links=(System.Collections.IEnumerable)typeof(DirectPositions).GetField("links",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(interrupted);
   foreach(var link in links.Cast<object>().ToArray())((System.Net.Sockets.TcpClient)link.GetType().GetField("Client",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(link)).Close();
   var clock=System.Diagnostics.Stopwatch.StartNew();Await(()=>joined.Session!=interrupted&&joined.Session.Ready,"A dropped connection recovers automatically using the fast initial retry");
   Check(clock.Elapsed.TotalSeconds<8,"Initial reconnection completes before the previous thirty-second delay");
   owner.Delete(group);
  }
  string full=Path.GetFullPath(folder),temp=Path.GetFullPath(Path.GetTempPath());if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("MiniMap-efficiency-"))throw new Exception("Unsafe cleanup");Directory.Delete(full,true);
 }
}
