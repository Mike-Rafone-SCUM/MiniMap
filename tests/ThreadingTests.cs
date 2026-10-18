using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using ScumMiniMap;
namespace ScumMiniMap { internal static class Localization { internal static string Get(string key) { return "Survivor"; } } }
class ThreadingTests {
    static void Check(bool condition,string text) { if(!condition)throw new Exception(text);Console.WriteLine("PASS: "+text); }
    static void Await(Func<bool> ready) { var until=DateTime.UtcNow.AddSeconds(5);while(!ready()&&DateTime.UtcNow<until)Thread.Sleep(10);if(!ready())throw new Exception("Fixture timed out"); }
    static void Fast(Action read,string text) { var task=Task.Run(read);Check(task.Wait(500),text); }
    static void GroupPersistence(string root) {
        var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();Task writing=null;
        var store=new RelayGroupStore(root,null,0,null,(file,bytes)=> { entered.Set();if(!release.Wait(5000))throw new TimeoutException();Directory.CreateDirectory(root);File.WriteAllBytes(file,bytes); });
        try {
            writing=Task.Run(()=>store.SetName("Synthetic owner"));Check(entered.Wait(5000),"Fixture blocks a protected group-file write");
            Fast(()=> { var a=store.Groups;var b=store.GroupViews;var c=store.Sessions;var e=store.Roster();var f=store.Markers();store.SynchronizeNames(); },"UI group reads and name synchronization do not wait for slow protected storage");
            Fast(store.Dispose,"Closing group sharing does not wait for a blocked credential write");
        }finally { release.Set();if(writing!=null)writing.GetAwaiter().GetResult();store.Dispose();entered.Dispose();release.Dispose(); }
        using(var loaded=new RelayGroupStore(root))Check(loaded.DisplayName=="Synthetic owner","Serialized protected persistence finishes consistently after nonblocking shutdown");
        int count=0;
        using(var broken=new RelayGroupStore(Path.Combine(root,"failure"),null,0,null,(file,bytes)=> { count++;throw new IOException("Synthetic disk failure"); })) {
            bool failed=false;try { broken.Create("Synthetic squad"); }catch(IOException) { failed=true; }
            Check(failed&&count==1&&broken.Groups.Length==1&&broken.Groups[0].Pending=="CREATE"&&broken.Sessions.Length==0,"Failed durable credential journaling fails closed before group management or connection");
        }
    }
    static void RouterShutdown() {
        const int port=47830;var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();bool block=false;int deleted=0,created=0;
        byte[] address={0,128,0,0,0,0,0,1,8,8,8,8};byte[] accepted={0,130,0,0,0,0,0,1,(byte)(port>>8),(byte)(port&255),(byte)(port>>8),(byte)(port&255),0,0,2,88};
        Func<System.Net.IPAddress,byte[],byte[]> exchange=(gateway,request)=> {
            if(request.Length==2) { if(Volatile.Read(ref block)) { entered.Set();if(!release.Wait(5000))throw new TimeoutException(); }return (byte[])address.Clone(); }
            if(request.Skip(8).All(b=>b==0))Interlocked.Increment(ref deleted);else Interlocked.Increment(ref created);return (byte[])accepted.Clone();
        };
        PeerRouter mapping=null,replacement=null;Task renewal=null;Task<PeerRouter> opening=null;
        try {
            mapping=PeerRouter.Open(port,new[]{System.Net.IPAddress.Parse("192.168.1.1")},exchange);Check(mapping!=null&&mapping.Ready,"Synthetic router starts with an accepted local lease");
            Volatile.Write(ref block,true);renewal=Task.Run(()=>typeof(PeerRouter).GetMethod("Renew",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(mapping,new object[]{null}));Check(entered.Wait(5000),"Fixture holds a router renewal inside network exchange");
            Fast(mapping.Dispose,"Disconnect returns while router renewal is blocked");Check(!mapping.Ready&&Volatile.Read(ref deleted)==0,"Stopped router is not advertised ready and withdrawal waits for the old renewal");
            opening=Task.Run(()=>PeerRouter.Open(port,new[]{System.Net.IPAddress.Parse("192.168.1.1")},exchange));Thread.Sleep(75);Check(!opening.IsCompleted&&Volatile.Read(ref created)==1,"Replacement lease waits for withdrawal without holding the lifecycle lock");
            Fast(mapping.Dispose,"Repeated shutdown stays nonblocking while replacement waits");
            Volatile.Write(ref block,false);release.Set();renewal.GetAwaiter().GetResult();replacement=opening.GetAwaiter().GetResult();Check(deleted==1&&created==3&&replacement.Ready&&!mapping.Ready,"Old renewal settles before withdrawal and the replacement remains valid");
            mapping.Dispose();Check(deleted==1,"Late repeated disposal cannot withdraw a replacement lease");replacement.Dispose();Await(()=>Volatile.Read(ref deleted)==2);
        }finally { Volatile.Write(ref block,false);release.Set();if(renewal!=null)renewal.Wait();if(mapping!=null)mapping.Dispose();if(opening!=null) { replacement=opening.GetAwaiter().GetResult();if(replacement!=null)replacement.Dispose(); }entered.Dispose();release.Dispose(); }
    }
    static void GroupNameSynchronization(string root) {
        Directory.CreateDirectory(root);var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();int writes=0;bool fail=false;
        var store=new RelayGroupStore(root,null,0,null,(file,bytes)=> { int count=Interlocked.Increment(ref writes);if(count==1) { entered.Set();if(!release.Wait(5000))throw new TimeoutException(); }if(Volatile.Read(ref fail))throw new IOException("Synthetic name persistence failure");File.WriteAllBytes(file,bytes); });
        DirectPositions host=null,friend=null;
        try {
            var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();string secret=PrivatePositions.NewKey();
            host=new DirectPositions(true,"",port,secret,"Synthetic host",false);friend=new DirectPositions(false,"127.0.0.1",port,secret,"Synthetic friend",false);
            var group=new RelayMembership { Id=new string('a',32),Key=secret,Owner="",Pending="",Name="",Session=host };((System.Collections.IList)typeof(RelayGroupStore).GetField("groups",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(store)).Add(group);
            var remote=new RelayMembership();remote.Rename("First crew");friend.UpdateMarkers(new[]{new SharedMapMarker { Id=remote.NameStamp,Name=remote.Name,X=0,Y=0 }});Await(()=>host.SnapshotMarkers().Length==1);
            Fast(store.SynchronizeNames,"Remote group-name synchronization returns before protected persistence finishes");Check(entered.Wait(5000),"Asynchronous group-name persistence reached the blocked writer");var old=store.GroupViews.Single();
            remote.Rename("Latest crew");friend.UpdateMarkers(new[]{new SharedMapMarker { Id=remote.NameStamp,Name=remote.Name,X=0,Y=0 }});Await(()=>host.SnapshotMarkers().Single().Name=="Latest crew");Fast(store.SynchronizeNames,"A newer group name remains usable while an earlier name is being saved");
            Check(old.Name=="First crew"&&store.GroupViews.Single().Name=="Latest crew","UI membership views remain stable when the worker-visible membership changes");release.Set();Await(()=>Volatile.Read(ref writes)>=2);Await(()=>!((bool)typeof(RelayGroupStore).GetField("nameSaveWorker",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(store)));
            using(var loaded=new RelayGroupStore(root))Check(loaded.Groups.Single().Name=="Latest crew","Coalesced persistence writes the latest name rather than an obsolete snapshot");
            Volatile.Write(ref fail,true);remote.Rename("Retry crew");friend.UpdateMarkers(new[]{new SharedMapMarker { Id=remote.NameStamp,Name=remote.Name,X=0,Y=0 }});Await(()=>host.SnapshotMarkers().Single().Name=="Retry crew");store.SynchronizeNames();Await(()=>store.PersistenceError!=null);Check(store.GroupViews.Single().Name=="Retry crew","Background save failures remain observable without discarding the current group name");
            Volatile.Write(ref fail,false);Await(()=> { store.SynchronizeNames();return store.PersistenceError==null; });using(var loaded=new RelayGroupStore(root))Check(loaded.Groups.Single().Name=="Retry crew","Failed asynchronous name saves retry and recover durably");
        }finally { release.Set();store.Dispose();if(host!=null)host.Dispose();if(friend!=null)friend.Dispose();entered.Dispose();release.Dispose(); }
    }
    static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-threading-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try { GroupPersistence(root);GroupNameSynchronization(Path.Combine(root,"names"));RouterShutdown();Console.WriteLine("Threading regressions passed with synthetic data and no live relay access."); }
        finally { string temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string full=Path.GetFullPath(root);if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("MiniMap-threading-"))throw new Exception("Unsafe fixture cleanup");Directory.Delete(full,true); }
    }
}
