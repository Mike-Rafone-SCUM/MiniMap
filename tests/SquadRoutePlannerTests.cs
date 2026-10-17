using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using ScumMiniMap;
class SquadRoutePlannerTests {
    static void Check(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name); }
    static void Await(Func<bool> condition) { var end=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<end) { if(condition())return;Thread.Sleep(10); }throw new Exception("Route planning timed out"); }
    static RoadRoute Route(PointF from,PointF to) { return new RoadRoute { Success=true,Polyline=new[]{from,new PointF((from.X+to.X)/2,(from.Y+to.Y)/2),to},TotalDistanceMeters=100 }; }
    static void Main() {
        var stops=new[]{new PointF(.2f,.2f),new PointF(.3f,.3f),new PointF(.4f,.4f)};
        using(var planner=new SquadRoutePlanner(Route)) {
            planner.Update(new PointF(.1f,.1f),stops);Await(()=>planner.Plan!=null);var plan=planner.Plan;
            Check(plan.Legs.Length==3&&plan.Verified.All(v=>v)&&plan.Distance==300,"Every stage is road-calculated automatically, including the player-to-first-stop leg");
            Check(plan.Legs[1].First()==stops[0]&&plan.Legs[1].Last()==stops[1],"Route stages preserve the placed stop order");
            planner.Update(null,stops);Await(()=>planner.Plan!=null);Check(planner.Plan.Legs.Length==2,"Inter-stop routes still calculate without a fresh player origin");
            var captured=(PointF[])stops.Clone();planner.Update(null,captured);captured[1]=new PointF(.9f,.9f);Await(()=>planner.Plan!=null);Check(planner.Plan.Legs[0].Last()==stops[1],"Pending work owns an immutable copy of placed markers");
            bool rejected=false;try { planner.Update(null,new[]{new PointF(float.NaN,.2f)}); }catch(ArgumentException) { rejected=true; }Check(rejected,"Invalid coordinates never enter a route search");
        }
        using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)) {
            int calls=0;
            using(var planner=new SquadRoutePlanner((a,b)=> { if(Interlocked.Increment(ref calls)==1) { entered.Set();release.WaitOne(3000); }return Route(a,b); })) {
                planner.Update(new PointF(.1f,.1f),stops);Check(entered.WaitOne(3000),"Route calculation runs off the calling thread");
                var latest=new[]{new PointF(.7f,.7f),new PointF(.8f,.8f)};planner.Update(new PointF(.6f,.6f),latest);release.Set();Await(()=>planner.Plan!=null);
                Check(planner.Plan.Legs.Length==2&&planner.Plan.Legs.Last().Last()==latest.Last(),"An edited route discards obsolete results and calculates the newest stops");
                Check(calls==3,"Obsolete work stops between legs rather than finishing the old route");
            }
        }
        int searches=0;using(var planner=new SquadRoutePlanner((a,b)=> { Interlocked.Increment(ref searches);return Route(a,b); })) {
            for(int i=0;i<100;i++)planner.Update(null,new[]{new PointF(.1f,.1f),new PointF(.2f+i*.001f,.3f)});
            Await(()=>planner.Plan!=null);Check(searches<5&&Math.Abs(planner.Plan.Legs[0].Last().X-.299f)<.0001,"Rapid marker placement is coalesced without flooding the road router");
            planner.Clear();Check(planner.Plan==null&&!planner.Busy,"Clearing a route immediately removes its preview and busy state");
        }
        using(var planner=new SquadRoutePlanner((a,b)=> { throw new InvalidOperationException(); })) {
            planner.Update(new PointF(.1f,.1f),stops);Await(()=>planner.Plan!=null);Check(planner.Plan.MissingRoads==3&&planner.Plan.Verified.All(v=>!v)&&planner.Plan.Distance>0,"Failed road legs remain explicitly unverified direct guidance");
        }
        using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)) {
            var planner=new SquadRoutePlanner((a,b)=> { entered.Set();release.WaitOne(3000);return Route(a,b); });planner.Update(new PointF(.1f,.1f),stops);Check(entered.WaitOne(3000),"Disposal fixture starts a route search");planner.Dispose();release.Set();Thread.Sleep(200);Check(planner.Plan==null&&!planner.Busy,"Closing the app cannot publish late route results");
        }
    }
}
