using System;
using System.Linq;
using System.Collections.Generic;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    internal sealed partial class WpfFieldConsoleRuntime {
        readonly SquadRoutePlanner ownRoutePlanner=new SquadRoutePlanner((a,b,mode)=>RoadRouter.Instance.FindRoute(a,b,mode));
        readonly SquadRoutePlanner squadRoutePlanner=new SquadRoutePlanner((a,b,mode)=>RoadRouter.Instance.FindRoute(a,b,mode));
        sealed class RemoteRoute { internal string Signature;internal DrawingPoint? Origin;internal DateTime Requested;internal readonly SquadRoutePlanner Planner=new SquadRoutePlanner((a,b,mode)=>RoadRouter.Instance.FindRoute(a,b,mode)); }
        readonly Dictionary<string,RemoteRoute> remoteRoutes=new Dictionary<string,RemoteRoute>(StringComparer.Ordinal);
        string ownRouteSignature,activeRouteSignature;DrawingPoint? ownPlanOrigin,activePlanOrigin;DateTime ownPlanRequested,activePlanRequested;
        static string StopSignature(SharedMapMarker[] stops) { return string.Join(";",stops.Select(m=>m.Id+":"+m.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+m.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+m.Name)); }
        static DrawingPoint[] StopPoints(SharedMapMarker[] stops) { return stops.Select(m=>new DrawingPoint((float)m.X,(float)m.Y)).ToArray(); }
        static bool OriginChanged(DrawingPoint? previous,DrawingPoint? next) { return previous.HasValue!=next.HasValue||previous.HasValue&&next.HasValue&&DestinationSearch.Metres(previous.Value,next.Value)>=25; }
        static DrawingPoint? PlanningPoint(Position position) { if(position==null)return null;var p=MapWindow.ToMap(position);return !float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&p.X>=0&&p.X<=1&&p.Y>=0&&p.Y<=1?(DrawingPoint?)p:null; }
        DrawingPoint? PlanningOrigin() { var state=engine.WpfSnapshot(false);return state.Position!=null&&PrivatePositions.Fresh(state.Updated,DateTime.UtcNow)?PlanningPoint(state.Position):null; }
        SharedMapMarker[] ReceivedSquadMarkers(bool visible=true) { return (privatePositions==null?new SharedMapMarker[0]:privatePositions.SnapshotMarkers()).Concat(relayGroups==null?new SharedMapMarker[0]:relayGroups.Markers()).Where(m=>!visible||PlayerMarkerVisible(m)).ToArray(); }
        SharedMapMarker[] OwnSquadPublication() {
            // Following a squad route neither republishes it nor advertises a
            // duplicate solo route from each follower into their other groups.
            bool followingShared=activeStops.Length>0&&activeStops[0].OwnerId!=null&&SquadState.IsShared(activeStops[0]);
            return squad.Local().Where(m=>!followingShared||!SquadState.IsStop(m)).ToArray();
        }
        void FollowSquadPeer(string id) {
            StopSquadNavigation();followingPeer=id;nextSquadRoute=DateTime.MinValue;filterIdentity=null;Refresh();
        }
        void ChangeOwnRouteKind(bool shared) { squad.SetShared(shared);OwnRouteEdited(); }
        void RemoveOwnRouteStop(string id) { if(squad.RemoveStop(id))OwnRouteEdited(); }
        void UndoOwnRouteStop() { squad.RemoveLast();OwnRouteEdited(); }
        void ClearOwnRoute() { ClearLocalRoute(false); }
        void ClearMapRoute() { ClearLocalRoute(true); }
        void ClearLocalRoute(bool stopNavigation) {
            bool ownActive=activeStops.Length>0&&activeStops[0].OwnerId==null;
            squad.ClearRoute();ownRouteSignature=null;ownRoutePlanner.Clear();if(stopNavigation||ownActive)StopSquadNavigation();filterIdentity=null;Refresh();
        }
        void OwnRouteEdited() {
            var next=squad.Stops;bool ownActive=activeStops.Length>0&&activeStops[0].OwnerId==null;
            if(next.Length==0) { if(ownActive)StopSquadNavigation(); }
            else {
                activeStop=ownActive?AlignedStop(next):0;followingPeer=null;activeStops=next;SetSquadStop();
            }
            ownRouteSignature=null;UpdateRoutePreviews();filterIdentity=null;Refresh();
        }
        int AlignedStop(SharedMapMarker[] next) {
            if(activeStop<activeStops.Length) {
                var previous=activeStops[activeStop];string identity=previous.Id.Substring(42);
                int found=identity!=new string('0',22)?Array.FindIndex(next,m=>m.Id.Substring(42)==identity):-1;
                if(found<0)found=Array.FindIndex(next,m=>m.Name==previous.Name&&m.X==previous.X&&m.Y==previous.Y);
                if(found>=0)return found;
            }
            return Math.Min(activeStop,next.Length-1);
        }
        bool UsesOwnNavigationPlan { get { return activeStop==0&&activeStops.Length>0&&activeStops[0].OwnerId==null; } }
        void UpdateRoutePreviews() {
            var now=DateTime.UtcNow;var origin=PlanningOrigin();var own=squad.Stops;string signature=engine.WpfRouteMode+":"+StopSignature(own);
            if(own.Length==0) { if(ownRouteSignature!="")ownRoutePlanner.Clear();ownRouteSignature=""; }
            else if(signature!=ownRouteSignature||now-ownPlanRequested>=TimeSpan.FromSeconds(2)&&OriginChanged(ownPlanOrigin,origin)) {
                ownRouteSignature=signature;ownPlanOrigin=origin;ownPlanRequested=now;ownRoutePlanner.Update(origin,StopPoints(own),engine.WpfRouteMode);
            }
            if(activeStops.Length==0||UsesOwnNavigationPlan) { if(activeRouteSignature!=null)squadRoutePlanner.Clear();activeRouteSignature=null; }
            else {
                var remaining=activeStops.Skip(activeStop).ToArray();signature=engine.WpfRouteMode+":"+StopSignature(remaining);
                if(remaining.Length>0&&(signature!=activeRouteSignature||now-activePlanRequested>=TimeSpan.FromSeconds(2)&&OriginChanged(activePlanOrigin,origin))) {
                    activeRouteSignature=signature;activePlanOrigin=origin;activePlanRequested=now;squadRoutePlanner.Update(origin,StopPoints(remaining),engine.WpfRouteMode);
                }
            }
            var markers=ReceivedSquadMarkers();var routes=SquadState.Routes(markers).ToList();
            routes.AddRange(markers.Where(m=>m.Id==SharedMapMarker.GpsId).Select(m=>new[]{m}));var alive=new HashSet<string>(StringComparer.Ordinal);
            foreach(var route in routes.Take(256)) {
                string owner=route[0].OwnerId??"";if((PlayerVisibility(owner)&16)==0)continue;
                string key=route[0].Id==SharedMapMarker.GpsId?owner+":gps":SquadState.RouteKey(route[0]);alive.Add(key);
                var peer=playerRoster.FirstOrDefault(p=>p.Id==owner&&p.Position!=null);DrawingPoint? from=peer==null?(DrawingPoint?)null:PlanningPoint(peer.Position);
                RemoteRoute remote;if(!remoteRoutes.TryGetValue(key,out remote))remoteRoutes[key]=remote=new RemoteRoute();signature=engine.WpfRouteMode+":"+StopSignature(route);
                if(signature!=remote.Signature||now-remote.Requested>=TimeSpan.FromSeconds(2)&&OriginChanged(remote.Origin,from)) { remote.Signature=signature;remote.Origin=from;remote.Requested=now;remote.Planner.Update(from,StopPoints(route),engine.WpfRouteMode); }
            }
            foreach(string key in remoteRoutes.Keys.Where(key=>!alive.Contains(key)).ToArray()) { remoteRoutes[key].Planner.Dispose();remoteRoutes.Remove(key); }
        }
        void SyncSelectedSharedRoute() {
            if(activeStops.Length==0||activeStops[0].OwnerId==null)return;
            string key=SquadState.RouteKey(activeStops[0]);var current=SquadState.Routes(ReceivedSquadMarkers(false)).FirstOrDefault(r=>SquadState.RouteKey(r[0])==key);
            if(current==null) { StopSquadNavigation();return; }
            if(StopSignature(current)!=StopSignature(activeStops)) { activeStop=AlignedStop(current);activeStops=current;SetSquadStop(); }
        }
        void AddRouteState(WpfMapState state,WpfMapState compact) {
            state.LocalRoute=compact.LocalRoute=squad.Stops;state.OwnRoutePlan=compact.OwnRoutePlan=ownRoutePlanner.Plan;
            state.SquadPlan=compact.SquadPlan=UsesOwnNavigationPlan?ownRoutePlanner.Plan:squadRoutePlanner.Plan;state.SquadPlanning=compact.SquadPlanning=UsesOwnNavigationPlan?ownRoutePlanner.Busy:squadRoutePlanner.Busy;
            state.SquadNavigating=compact.SquadNavigating=followingPeer!=null||activeStops.Length>0;
            state.ActiveSquadRoute=compact.ActiveSquadRoute=activeStops.Length>0?SquadState.RouteKey(activeStops[0]):null;
            state.ActiveSquadShared=compact.ActiveSquadShared=activeStops.Length>0&&SquadState.IsShared(activeStops[0]);
            if(state.LocalRoute.Length>0) {
                var own=state.OwnRoutePlan;double distance=0;var from=state.Position==null?new DrawingPoint((float)state.LocalRoute[0].X,(float)state.LocalRoute[0].Y):MapWindow.ToMap(state.Position);
                foreach(var stop in state.LocalRoute) { var to=new DrawingPoint((float)stop.X,(float)stop.Y);distance+=DestinationSearch.Metres(from,to);from=to; }
                state.OwnRouteSummary=compact.OwnRouteSummary=engine.WpfTravelSummary(own==null?distance:own.Distance,own!=null&&own.MissingRoads==0,ownRoutePlanner.Busy,own==null?(double?)null:own.EstimatedSeconds);
            }
            if(activeStops.Length>activeStop&&state.Target!=null&&state.Position!=null) {
                var plan=state.SquadPlan;
                double distance=0;var from=MapWindow.ToMap(state.Position);
                foreach(var stop in activeStops.Skip(activeStop)) { var to=new DrawingPoint((float)stop.X,(float)stop.Y);distance+=DestinationSearch.Metres(from,to);from=to; }
                bool road=plan!=null&&plan.HasOrigin&&plan.MissingRoads==0;
                if(plan!=null&&plan.HasOrigin)distance=plan.Distance;
                state.RouteSummary=compact.RouteSummary=S("NavigationNext")+": "+state.RouteSummary+" · "+S("NavigationRemaining")+": "+engine.WpfTravelSummary(distance,road,state.SquadPlanning,plan==null?(double?)null:plan.EstimatedSeconds);
            }
            var markers=ReceivedSquadMarkers();var descriptions=SquadState.Routes(markers).Select(r=>new { Key=SquadState.RouteKey(r[0]),Marker=r[0] }).Concat(markers.Where(m=>m.Id==SharedMapMarker.GpsId).Select(m=>new { Key=(m.OwnerId??"")+":gps",Marker=m })).ToArray();
            state.OtherRoutePlans=compact.OtherRoutePlans=remoteRoutes.Where(p=>p.Value.Planner.Plan!=null).Select(p=> {
                var marker=descriptions.FirstOrDefault(d=>d.Key==p.Key);return new SquadRouteDrawing { Key=p.Key,Name=marker==null?"":marker.Marker.Owner,Shared=marker!=null&&SquadState.IsShared(marker.Marker),Plan=p.Value.Planner.Plan };
            }).ToArray();
        }
        void DisposeRoutePreviews() { ownRoutePlanner.Dispose();squadRoutePlanner.Dispose();foreach(var route in remoteRoutes.Values)route.Planner.Dispose();remoteRoutes.Clear(); }
    }
}
