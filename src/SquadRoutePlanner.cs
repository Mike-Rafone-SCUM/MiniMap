using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ScumMiniMap {
    internal sealed class SquadRoutePlan {
        internal int Revision;
        internal PointF[][] Legs;
        internal bool[] Verified;
        internal double Distance;
        internal bool HasOrigin;
        internal int MissingRoads { get { return Verified.Count(v=>!v); } }
    }
    internal sealed class SquadRouteDrawing { internal string Key,Name;internal bool Shared;internal SquadRoutePlan Plan; }
    // A single bounded worker coalesces edits and abandons obsolete work between
    // legs. All road searches run away from the WPF dispatcher.
    internal sealed class SquadRoutePlanner:IDisposable {
        sealed class Request { internal int Revision;internal PointF? Origin;internal PointF[] Stops; }
        readonly object gate=new object();readonly Func<PointF,PointF,RoadRoute> calculate;
        static readonly SemaphoreSlim calculations=new SemaphoreSlim(2);
        int revision;bool worker,disposed,busy;Request pending;SquadRoutePlan plan;
        internal SquadRoutePlanner(Func<PointF,PointF,RoadRoute> calculate) { this.calculate=calculate; }
        internal bool Busy { get { lock(gate)return busy; } }
        internal SquadRoutePlan Plan { get { lock(gate)return plan; } }
        static bool Valid(PointF p) { return !float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&p.X>=0&&p.X<=1&&p.Y>=0&&p.Y<=1; }
        internal void Update(PointF? origin,PointF[] stops) {
            if(stops==null||stops.Length<1||stops.Length>16||stops.Any(p=>!Valid(p))||origin.HasValue&&!Valid(origin.Value))throw new ArgumentException();
            lock(gate) {
                if(disposed)return;pending=new Request { Revision=++revision,Origin=origin,Stops=(PointF[])stops.Clone() };plan=null;busy=true;
                if(!worker) { worker=true;Task.Run((Func<Task>)Work); }
            }
        }
        bool Current(Request request) { lock(gate)return !disposed&&request.Revision==revision; }
        RoadRoute CalculateBackground(PointF from,PointF to) {
            var thread=Thread.CurrentThread;var previous=thread.Priority;
            try { thread.Priority=ThreadPriority.BelowNormal;return calculate(from,to); }
            finally { thread.Priority=previous; }
        }
        async Task Work() {
            while(true) {
                Request request;lock(gate) { if(disposed||pending==null) { worker=false;return; }request=pending;pending=null; }
                await Task.Delay(150);if(!Current(request))continue;
                var legs=new List<PointF[]>();var verified=new List<bool>();double distance=0;
                PointF from=request.Origin??request.Stops[0];int first=request.Origin.HasValue?0:1;
                for(int i=first;i<request.Stops.Length;i++) {
                    if(!Current(request))break;PointF to=request.Stops[i];RoadRoute route=null;
                    await calculations.WaitAsync();
                    try { if(Current(request))route=await Task.Run(()=>CalculateBackground(from,to)); }catch(Exception) { }finally { calculations.Release(); }
                    if(!Current(request))break;
                    bool good=route!=null&&route.Success&&route.Polyline!=null&&route.Polyline.Length>0&&route.Polyline.All(Valid);
                    legs.Add(good?new[]{from}.Concat(route.Polyline).Concat(new[]{to}).ToArray():new[]{from,to});verified.Add(good);
                    double length=route==null?double.NaN:route.TotalDistanceMeters;
                    distance+=!double.IsNaN(length)&&!double.IsInfinity(length)&&length>=0?length:RoadRouter.DistanceMeters(from,to);
                    from=to;
                }
                lock(gate)if(!disposed&&request.Revision==revision) { plan=new SquadRoutePlan { Revision=request.Revision,Legs=legs.ToArray(),Verified=verified.ToArray(),Distance=distance,HasOrigin=request.Origin.HasValue };busy=false; }
            }
        }
        internal void Clear() { lock(gate) { revision++;pending=null;plan=null;busy=false; } }
        public void Dispose() { lock(gate) { disposed=true;revision++;pending=null;plan=null;busy=false; } }
    }
}
