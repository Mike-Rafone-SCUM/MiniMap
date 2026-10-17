using System;
using System.IO;
using System.Linq;
using System.Threading;
using ScumMiniMap;
// The standalone protocol harness needs only a display-name seed, not UI resources.
namespace ScumMiniMap { internal static class Localization { internal static string Get(string key) { return "Survivor"; } } }
class PersistentGroupTests {
    static void Check(bool value,string message) { if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message); }
    static void Await(Func<bool> condition,string message) { for(int i=0;i<150;i++) { if(condition()) { Check(true,message);return; }Thread.Sleep(100); }throw new Exception(message); }
    static void Main(string[] args) {
        string address=args.Length>0?args[0]:DirectPositions.RelayAddress;int port=args.Length>1?int.Parse(args[1]):DirectPositions.RelayPort;
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-groups-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        RelayGroupStore owner=null,member=null,outside=null;
        try {
            owner=new RelayGroupStore(Path.Combine(root,"owner"),address,port);member=new RelayGroupStore(Path.Combine(root,"member"),address,port);outside=new RelayGroupStore(Path.Combine(root,"outside"),address,port);
            owner.SetName("Synthetic owner");member.SetName("Synthetic member");outside.SetName("Synthetic old-code check");
            var first=owner.Create();string old=first.Code;var second=owner.Create();
            using(var badPin=new DirectPositions(false,address,port,first.Key,"Synthetic bad pin",false,null,true,new string('0',64),first.Id))Await(()=>badPin.Error!=null&&!badPin.Ready,"Incorrect relay certificate pin fails closed");
            var joinedFirst=member.Join(old);var joinedSecond=member.Join(second.Code);
            Await(()=>owner.Sessions.All(s=>s.Ready)&&member.Sessions.All(s=>s.Ready),"One player joins two persistent groups at once over pinned TLS");
            foreach(var session in owner.Sessions) { session.Update(new Position { X=100,Y=200,Yaw=90 },DateTime.UtcNow);session.UpdateMarkers(new[]{new SharedMapMarker { Id=new string('A',64),Name="Synthetic group marker",X=.2,Y=.3 }}); }
            Await(()=>member.Roster(true).Length==2&&member.Markers().Length==2,"Multiple groups deliver positions and markers without mixing identities");
            Check(member.Roster().Select(p=>p.Id).Distinct().Count()==2&&member.Markers().Select(m=>m.OwnerId).Distinct().Count()==2,"Player and marker visibility identities are scoped to their group");
            bool denied=false;try { member.Rotate(joinedFirst); }catch(InvalidOperationException) { denied=true; }Check(denied,"A joining member does not gain ownership or code-refresh permission");
            member.Leave(joinedFirst);Check(member.Sessions.Length==1&&joinedSecond.Session.Ready,"Leaving one group preserves the other active group");member.Connect(joinedFirst);Await(()=>joinedFirst.Session.Ready,"Saved codes reconnect without asking the owner for a new code");
            owner.Rotate(first);Check(first.Code!=old,"Owner refresh creates a different persistent code");
            Await(()=>joinedFirst.Session.Error!=null&&!joinedFirst.Session.Ready&&joinedFirst.Session.Snapshot().Length==0,"Owner refresh disconnects old-code members and clears their received locations");
            Check(joinedSecond.Session.Ready,"Refreshing one group leaves other groups connected");
            var retired=outside.Join(old);Await(()=>retired.Session.Error!=null&&!retired.Session.Ready,"Retired group code cannot reconnect to the relay");
            member.Join(first.Code);Await(()=>joinedFirst.Session.Ready,"The owner's new code restores membership");
            byte[] saved=File.ReadAllBytes(Path.Combine(root,"owner","private-groups.dat"));string bytes=System.Text.Encoding.UTF8.GetString(saved);
            Check(!bytes.Contains(first.Code)&&!bytes.Contains(first.Owner)&&!bytes.Contains("Synthetic owner"),"Saved codes, owner credentials and names are protected with Windows DPAPI");
            owner.Dispose();owner=null;
            using(var reloaded=new RelayGroupStore(Path.Combine(root,"owner"),address,port)) {
                Check(reloaded.Groups.Length==2&&reloaded.Groups.Single(g=>g.Id==first.Id).Code==first.Code&&reloaded.Groups.All(g=>g.IsOwner)&&reloaded.Sessions.Length==0,"Codes and ownership survive app restart while sharing starts disabled");
            }
            Check(RelayMembership.Parse(first.Code).Code==first.Code,"Shareable code round-trips without exposing the separate owner credential");
            foreach(string invalid in new[]{"",old.Replace("MM1-","MM2-"),old+"a",old.Substring(1)}) { bool rejected=false;try { RelayMembership.Parse(invalid); }catch { rejected=true; }Check(rejected,"Malformed group code is rejected before connection"); }
        }finally {
            if(owner!=null)owner.Dispose();if(member!=null)member.Dispose();if(outside!=null)outside.Dispose();
            string full=Path.GetFullPath(root),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("MiniMap-groups-"))throw new Exception("Unsafe test cleanup");Directory.Delete(full,true);
        }
    }
}
