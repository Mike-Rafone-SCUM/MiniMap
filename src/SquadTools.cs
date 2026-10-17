using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        internal MapPreset WpfCapturePreset(string name) {
            return new MapPreset { Name=name,Map=mapSidecarPaths.MapId,Categories=scumMap==null?"":scumMap.GetDisabledCategoriesString(),ZoneLayers=string.Join(";",disabledZoneLayers),Zones=string.Join(";",disabledWpfZones),Grid=gridBorders,Waypoints=showCustomWaypoints,History=locationHistory,Labels=showZoneLabels,ShowZones=showZones,Poi=showScumMap&&(scumMap==null||scumMap.MasterEnabled),Rotate=wpfRotateMap,Cluster=wpfPoiClustering,Opacity=mapOpacity,Zoom=zoom,TacticalOpacity=fullMapOpacity,TacticalZoom=fullMapZoom };
        }
        internal bool WpfApplyPreset(MapPreset p) {
            if(p.Map!=mapSidecarPaths.MapId)return false;
            gridBorders=p.Grid;showCustomWaypoints=p.Waypoints;locationHistory=p.History;showZoneLabels=p.Labels;showZones=p.ShowZones;showScumMap=p.Poi;
            if(scumMap!=null) { scumMap.MasterEnabled=p.Poi;scumMap.ApplyDisabledCategoriesString(p.Categories); }
            disabledZoneLayers.Clear();foreach(string s in p.ZoneLayers.Split(';').Where(s=>s.Length>0))disabledZoneLayers.Add(s);
            disabledWpfZones.Clear();foreach(string s in p.Zones.Split(';').Where(s=>s.Length>0))disabledWpfZones.Add(s);
            fullMapOpacity=p.TacticalOpacity;fullMapZoom=Math.Max(1,Math.Min(maxZoom,p.TacticalZoom));wpfRotateMap=p.Rotate;wpfPoiClustering=p.Cluster;mapOpacity=p.Opacity;autoZoom=false;zoom=targetZoom=Math.Max(1,Math.Min(maxZoom,p.Zoom));SettingsChanged();SaveSettings();WpfRebuildSettings();return true;
        }
    }
    internal sealed partial class WpfFieldConsoleRuntime {
        readonly SquadState squad=new SquadState();
        readonly Dictionary<string,PrivatePositions.Peer> knownPlayers=new Dictionary<string,PrivatePositions.Peer>(StringComparer.Ordinal);
        readonly Dictionary<string,DateTime> playerSeen=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        readonly Dictionary<string,TextBlock> playerStatusLabels=new Dictionary<string,TextBlock>(StringComparer.Ordinal);
        string followingPeer;MapZone squadTarget;DateTime nextSquadRoute;
        SharedMapMarker[] activeStops=new SharedMapMarker[0];int activeStop;
        static string S(string key) { return Localization.Get(key); }
        void StopSquadNavigation() { followingPeer=null;activeStops=new SharedMapMarker[0];squadTarget=null;squadRoutePlanner.Clear();activeRouteSignature=null;engine.WpfClear(); }
        static TextBlock SquadHelp(string key) { var text=WpfFieldTheme.Text(S(key),13,WpfFieldTheme.Muted);text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;text.Margin=new Thickness(0,10,0,10);return text; }
        void SendPing(int kind,DrawingPoint? at=null) {
            var state=engine.WpfSnapshot(false);var sessions=(relayGroups==null?new DirectPositions[0]:relayGroups.Sessions).Concat(privatePositions==null?new DirectPositions[0]:new[]{privatePositions});
            if(!sessions.Any(s=>s.Ready)||!state.Tracking||state.TrackingPaused||state.Position==null||!PrivatePositions.Fresh(state.Updated,DateTime.UtcNow))return;
            var p=at??MapWindow.ToMap(state.Position);string[] keys={"PingDanger","PingLoot","PingPickup"};squad.Ping(kind,S(keys[kind]),p.X,p.Y);Refresh();
        }
        string PeerStatus(PrivatePositions.Peer p) {
            DateTime seen;bool connected=playerSeen.TryGetValue(p.Id,out seen)&&(DateTime.UtcNow-seen).TotalSeconds<3;
            string age=p.Updated==DateTime.MinValue?S("PlayerNoLocation"):S("SquadUpdated")+" "+Math.Max(0,(int)(DateTime.UtcNow-p.Updated).TotalSeconds)+" s";
            return S(connected?"PlayerConnected":"SquadOffline")+" · "+(p.Position==null?S("PlayerNoLocation"):S("PlayerLiveLocation"))+" · "+age;
        }
        void UpdateSquad() {
            var now=DateTime.UtcNow;
            var current=(privatePositions==null?new PrivatePositions.Peer[0]:privatePositions.Roster()).Concat(relayGroups==null?new PrivatePositions.Peer[0]:relayGroups.Roster()).ToArray();
            foreach(var p in current) { PrivatePositions.Peer previous;if(p.Position==null)p.Updated=knownPlayers.TryGetValue(p.Id,out previous)?previous.Updated:DateTime.MinValue;knownPlayers[p.Id]=p;playerSeen[p.Id]=now; }
            foreach(string id in playerSeen.Where(p=>now-p.Value>TimeSpan.FromMinutes(5)).Select(p=>p.Key).ToArray()) { playerSeen.Remove(id);knownPlayers.Remove(id); }
            playerRoster=knownPlayers.Values.OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).Select(p=>new PrivatePositions.Peer { Id=p.Id,Name=p.Name,Updated=p.Updated,Position=current.Any(c=>c.Id==p.Id)?p.Position:null }).ToArray();
            SyncSelectedSharedRoute();
            var state=engine.WpfSnapshot(false);
            // A manual destination or Clear takes precedence over squad navigation.
            if(squadTarget!=null&&!object.ReferenceEquals(squadTarget,state.Target)||(followingPeer!=null&&squadTarget==null&&state.Target!=null)) { followingPeer=null;activeStops=new SharedMapMarker[0];squadTarget=null;squadRoutePlanner.Clear();activeRouteSignature=null; }
            if(followingPeer!=null) {
                var peer=current.FirstOrDefault(p=>p.Id==followingPeer);
                if(peer==null||peer.Position==null) { bool clear=squadTarget!=null;squadTarget=null;if(clear)engine.WpfClear();return; }
                var destination=MapWindow.ToMap(peer.Position);
                if(now>=nextSquadRoute&&(squadTarget==null||DestinationSearch.Metres(squadTarget.Centroid,destination)>=25)) { squadTarget=new MapZone { Name=S("SquadFollow")+": "+peer.Name,Points=new[]{destination} };engine.WpfSquadTarget(squadTarget);nextSquadRoute=now.AddSeconds(2); }
            }
            else if(activeStops.Length>0&&activeStop<activeStops.Length) {
                int previousStop=activeStop;
                if(state.Position!=null&&state.Tracking&&!state.TrackingPaused&&PrivatePositions.Fresh(state.Updated,now)&&DestinationSearch.Metres(MapWindow.ToMap(state.Position),new DrawingPoint((float)activeStops[activeStop].X,(float)activeStops[activeStop].Y))<40)activeStop++;
                if(activeStop>=activeStops.Length) { engine.WpfClear();activeStops=new SharedMapMarker[0];squadTarget=null; }
                else if(previousStop!=activeStop||squadTarget==null||!object.ReferenceEquals(squadTarget,state.Target))SetSquadStop();
                else { var stop=activeStops[activeStop];if(squadTarget.Centroid.X!=(float)stop.X||squadTarget.Centroid.Y!=(float)stop.Y)SetSquadStop(); }
            }
        }
        void SetSquadStop() { var m=activeStops[activeStop];squadTarget=new MapZone { Name=(activeStop+1)+"/"+activeStops.Length+" · "+m.Name,Points=new[]{new DrawingPoint((float)m.X,(float)m.Y)} };engine.WpfSquadTarget(squadTarget); }
        void StartSquadRoute(SharedMapMarker[] stops) { if(stops.Length==0)return;followingPeer=null;activeStops=stops.Select(m=>m.Copy()).ToArray();activeStop=0;SetSquadStop();Refresh(); }
        void AddSquadStop(DrawingPoint at) { var m=engine.WpfClickedTarget(at,.003f);if(squad.AddStop(m.DisplayName,at.X,at.Y))OwnRouteEdited(); }
        void ShowSquadTools() {
            var window=Window(S("SquadTools"),620,740);var body=new StackPanel { Margin=new Thickness(22) };var scroll=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var layout=new DockPanel();var header=new DockPanel { Margin=new Thickness(22,16,22,0) };var done=WpfFieldTheme.Button(S("Done"),()=>window.Close());DockPanel.SetDock(done,Dock.Right);header.Children.Add(done);header.Children.Add(WpfFieldTheme.Text(S("SquadTools"),24,null,true));header.MouseLeftButtonDown+=(s,e)=> { if(e.OriginalSource is TextBlock)window.DragMove(); };DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);layout.Children.Add(scroll);window.Content=layout;
            body.Children.Add(SquadHelp("SquadPingHelp"));
            var pingRow=new WrapPanel();string[] keys={"PingDanger","PingLoot","PingPickup"};for(int i=0;i<3;i++) { int k=i;pingRow.Children.Add(WpfFieldTheme.Button(S(keys[i]),()=>SendPing(k))); }body.Children.Add(pingRow);
            body.Children.Add(SquadHelp("SquadRouteHelp"));body.Children.Add(SquadHelp("RouteAutoHelp"));var routes=new StackPanel();body.Children.Add(routes);
            Action redraw=null;redraw=()=> {
                routes.Children.Clear();var local=squad.Stops;
                routes.Children.Add(WpfFieldTheme.Text(S(squad.Shared?"SquadRoute":"SoloRoute")+" · "+local.Length+" / 16",17,null,true));
                var kind=new WrapPanel();kind.Children.Add(WpfFieldTheme.Button(S("SoloRoute"),()=> { ChangeOwnRouteKind(false);redraw(); },!squad.Shared));kind.Children.Add(WpfFieldTheme.Button(S("SquadRoute"),()=> { ChangeOwnRouteKind(true);redraw(); },squad.Shared));routes.Children.Add(kind);
                var plan=ownRoutePlanner.Plan;routes.Children.Add(WpfFieldTheme.Text(ownRoutePlanner.Busy?S("FieldCalculating"):plan==null?"":engine.WpfTravelSummary(plan.Distance,plan.MissingRoads==0),13,WpfFieldTheme.Muted));
                foreach(var m in local)routes.Children.Add(WpfFieldTheme.Text((SquadState.StopIndex(m)+1)+". "+m.Name));
                var row=new WrapPanel();row.Children.Add(WpfFieldTheme.Button(S("SquadAddStop"),()=> { var t=engine.WpfSnapshot(true).Target;if(t!=null)AddSquadStop(t.Centroid);redraw(); }));row.Children.Add(WpfFieldTheme.Button(S("SquadUndoStop"),()=> { UndoOwnRouteStop();redraw(); }));row.Children.Add(WpfFieldTheme.Button(S("WpfClearRoute"),()=> { ClearOwnRoute();redraw(); }));routes.Children.Add(row);
                var own=WpfFieldTheme.Button(S("SquadStartRoute"),()=> { StartSquadRoute(local);window.Close(); });own.IsEnabled=local.Length>0;routes.Children.Add(own);
                var remote=(privatePositions==null?new SharedMapMarker[0]:privatePositions.SnapshotMarkers()).Concat(relayGroups==null?new SharedMapMarker[0]:relayGroups.Markers());
                foreach(var route in SquadState.Routes(remote)) { var copy=route;routes.Children.Add(WpfFieldTheme.Button(S("SquadStartRoute")+": "+route[0].Owner+" · "+route.Length,()=> { StartSquadRoute(copy);window.Close(); })); }
            };redraw();
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };timer.Tick+=(s,e)=>redraw();window.Closed+=(s,e)=>timer.Stop();timer.Start();
            body.Children.Add(WpfFieldTheme.Button(S("SquadStopNav"),()=> { StopSquadNavigation();Refresh(); }));
            body.Children.Add(WpfFieldTheme.Text(S("SquadPresets"),19,null,true));
            string file=Path.Combine(engine.WpfDataFolder,"map-presets.dat");var presets=new List<MapPreset>();
            try { presets.AddRange(MapPresetFile.Load(file)); }catch(Exception e) { Program.LogException("MapPresets",e);body.Children.Add(WpfFieldTheme.Text(S("PrivatePositionInvalid"))); }
            var presetName=new TextBox { MaxLength=64,Text=S("SquadLooting"),Margin=new Thickness(0,8,0,8) };body.Children.Add(presetName);
            var names=new WrapPanel();foreach(string key in new[]{"SquadLooting","SquadDriving","SquadExploring"}) { string n=S(key);names.Children.Add(WpfFieldTheme.Button(n,()=>presetName.Text=n)); }body.Children.Add(names);
            var saved=new StackPanel();Action showSaved=null;Action save=()=> { try { MapPresetFile.Save(file,presets);showSaved(); }catch(Exception e) { Program.LogException("MapPresetsSave",e);System.Windows.MessageBox.Show(S("PrivatePositionInvalid")); } };
            showSaved=()=> { saved.Children.Clear();foreach(var p in presets.ToArray()) { var row=new WrapPanel();row.Children.Add(WpfFieldTheme.Button(p.Name,()=> { if(engine.WpfApplyPreset(p)) { filterIdentity=null;Refresh(); } }));row.Children.Add(WpfFieldTheme.Button(S("SquadDelete"),()=> { presets.Remove(p);save(); }));saved.Children.Add(row); } };
            body.Children.Add(WpfFieldTheme.Button(S("SquadSavePreset"),()=> { string name=presetName.Text.Trim();if(name.Length==0)return;var p=engine.WpfCapturePreset(name);presets.RemoveAll(v=>v.Name==name&&v.Map==p.Map);if(presets.Count>=24)return;presets.Add(p);save(); }));body.Children.Add(saved);showSaved();Modal(window);
        }
    }
}
