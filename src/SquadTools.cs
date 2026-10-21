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
            return new MapPreset { Name=name,Map=mapSidecarPaths.MapId,Categories=scumMap==null?"":scumMap.GetDisabledCategoriesString(),ZoneLayers=string.Join(";",disabledZoneLayers),Zones=string.Join(";",disabledWpfZones),Grid=gridBorders,Waypoints=showCustomWaypoints,History=locationHistory,Labels=showZoneLabels,ShowZones=showZones,Poi=showScumMap&&(scumMap==null||scumMap.MasterEnabled),Rotate=wpfRotateMap,Cluster=wpfPoiClustering,Opacity=mapOpacity,Zoom=zoom,TacticalOpacity=WpfTacticalOpacityValue,TacticalZoom=fullMapZoom };
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
        bool CanSendPing() {
            var sessions=(relayGroups==null?new DirectPositions[0]:relayGroups.Sessions).Concat(privatePositions==null?new DirectPositions[0]:new[]{privatePositions});
            bool squadDialog=dialogs.Count>0&&dialogs[dialogs.Count-1].Tag as string=="squad-tools";
            return sessions.Any(s=>s.Ready)&&engine.WpfCanPing(squadDialog);
        }
        static IEnumerable<TextBlock> SquadPlayerLabels(Panel players) { return players.Children.OfType<Border>().Select(b=>b.Child).OfType<StackPanel>().SelectMany(p=>p.Children.OfType<TextBlock>()).Where(t=>t.Tag is string); }
        void SendPing(int kind,DrawingPoint? at=null) {
            if(!CanSendPing())return;var state=engine.WpfSnapshot(false);
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
        void NavigateDestination(MapZone destination) { StopSquadNavigation();engine.WpfTarget(destination);filterIdentity=null;RefreshInteraction(); }
        void AddSquadDestination(MapZone destination) { if(destination!=null&&squad.AddStop(destination.DisplayName,destination.Centroid.X,destination.Centroid.Y))OwnRouteEdited(); }
        void AddSquadStop(DrawingPoint at) { AddSquadDestination(engine.WpfClickedTarget(at,.003f)); }
        void ShowSquadTools() { Modal(BuildSquadTools(1)); }
        void ShowRoutes() { Modal(BuildSquadTools(0)); }
        void ShowMapPresets() { Modal(BuildSquadTools(2)); }
        Window BuildSquadTools(int initial) {
            var window=Window(S("SquadTools"),680,760);window.Tag="squad-tools";window.WindowStyle=WindowStyle.SingleBorderWindow;
            var body=new StackPanel { Margin=new Thickness(22) };var scroll=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var layout=new DockPanel();var header=new DockPanel { Margin=new Thickness(22,16,22,0) };var done=WpfFieldTheme.Button(S("Done"),()=>window.Close());DockPanel.SetDock(done,Dock.Right);header.Children.Add(done);header.Children.Add(WpfFieldTheme.Text(S("SquadTools"),24,null,true));header.MouseLeftButtonDown+=(s,e)=> { if(e.OriginalSource is TextBlock)window.DragMove(); };DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);layout.Children.Add(scroll);window.Content=layout;
            var tabs=new WrapPanel { Margin=new Thickness(22,12,22,0) };DockPanel.SetDock(tabs,Dock.Top);layout.Children.Insert(1,tabs);
            var pages=new[]{new StackPanel(),new StackPanel(),new StackPanel()};var pageHost=body;int selected=-1;
            string[] tabKeys={"RouteList","SquadTools","SquadPresets"};
            Action<int> select=index=> { selected=index;window.Title=S(tabKeys[index]);((TextBlock)header.Children[1]).Text=window.Title;pageHost.Children.Clear();pageHost.Children.Add(pages[index]);scroll.ScrollToTop();for(int i=0;i<tabs.Children.Count;i++) { var button=(Button)tabs.Children[i];button.Background=i==index?WpfFieldTheme.Amber:WpfFieldTheme.Panel;button.Foreground=i==index?WpfFieldTheme.Ground:WpfFieldTheme.Ink; } };
            for(int i=0;i<3;i++) { int index=i;var tab=WpfFieldTheme.Button(S(tabKeys[i]),()=>select(index));tab.Margin=new Thickness(0,0,6,6);tab.Tag="squad-tab-"+i;tabs.Children.Add(tab); }
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==System.Windows.Input.Key.Tab&&(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0) { select((selected+((System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Shift)!=0?2:1))%3);((Button)tabs.Children[selected]).Focus();e.Handled=true; } };
            var routePage=pages[0];routePage.Children.Add(SquadHelp("SquadRouteHelp"));routePage.Children.Add(SquadHelp("RouteAutoHelp"));
            var navigation=WpfFieldTheme.Text("",16);navigation.TextWrapping=TextWrapping.Wrap;navigation.TextTrimming=TextTrimming.None;routePage.Children.Add(navigation);
            Action redraw=null;
            routePage.Children.Add(SquadHelp("RouteModeHelp"));
            var modes=new WrapPanel();var modeButtons=new List<Button>();
            foreach(RoadRouteMode mode in Enum.GetValues(typeof(RoadRouteMode))) {
                var choice=mode;var button=WpfFieldTheme.Button(S("RouteMode"+mode),()=> {
                    engine.WpfSetRouteMode(choice);ownRouteSignature=null;activeRouteSignature=null;
                    UpdateRoutePreviews();filterIdentity=null;Refresh();redraw();
                });button.Tag="route-mode-"+mode;modeButtons.Add(button);modes.Children.Add(button);
            }
            routePage.Children.Add(modes);
            var navActions=new WrapPanel();var addTarget=WpfFieldTheme.Button(S("SquadAddStop"),()=> { AddSquadDestination(engine.WpfSnapshot(true).Target);redraw(); });addTarget.Tag="route-add-target";addTarget.ToolTip=S("MapGpsSavedHelp");navActions.Children.Add(addTarget);
            var stopNavigation=WpfFieldTheme.Button(S("SquadStopNav"),()=> { StopSquadNavigation();filterIdentity=null;Refresh();redraw(); });stopNavigation.Tag="route-stop-navigation";navActions.Children.Add(stopNavigation);routePage.Children.Add(navActions);
            var routes=new StackPanel();routePage.Children.Add(routes);string routeIdentity=null;
            redraw=()=> {
                foreach(var button in modeButtons) { bool active=(string)button.Tag=="route-mode-"+engine.WpfRouteMode;button.Background=active?WpfFieldTheme.Amber:WpfFieldTheme.Panel;button.Foreground=active?WpfFieldTheme.Ground:WpfFieldTheme.Ink; }
                var state=engine.WpfSnapshot(true);navigation.Text=S("FieldTarget")+": "+(state.Target==null?S("KeyNone"):state.Target.DisplayName)+(state.Target==null?"":" · "+state.RouteSummary);
                addTarget.IsEnabled=state.Target!=null&&activeStops.Length==0&&followingPeer==null&&squad.Stops.Length<SquadState.MaximumStops;
                stopNavigation.IsEnabled=state.Target!=null||followingPeer!=null||activeStops.Length>0;
                var local=squad.Stops;var remote=SquadState.Routes(ReceivedSquadMarkers());
                var plan=ownRoutePlanner.Plan;string identity=StopSignature(local)+"|"+squad.Shared+"|"+ownRoutePlanner.Busy+"|"+(plan==null?0:plan.Revision)+"|"+string.Join("|",remote.Select(r=>SquadState.RouteKey(r[0])+":"+r[0].Owner+":"+StopSignature(r)));
                if(identity==routeIdentity)return;routeIdentity=identity;routes.Children.Clear();
                routes.Children.Add(WpfFieldTheme.Text(S(squad.Shared?"SquadRoute":"SoloRoute")+" · "+local.Length+" / 16",17,null,true));
                var kind=new WrapPanel();kind.Children.Add(WpfFieldTheme.Button(S("SoloRoute"),()=> { ChangeOwnRouteKind(false);redraw(); },!squad.Shared));kind.Children.Add(WpfFieldTheme.Button(S("SquadRoute"),()=> { ChangeOwnRouteKind(true);redraw(); },squad.Shared));routes.Children.Add(kind);
                routes.Children.Add(WpfFieldTheme.Text(state.OwnRouteSummary??(ownRoutePlanner.Busy?S("FieldCalculating"):plan==null?"":engine.WpfTravelSummary(plan.Distance,plan.MissingRoads==0)),13,WpfFieldTheme.Muted));
                foreach(var m in local) { var marker=m;var stopRow=new DockPanel();var remove=WpfFieldTheme.Button(S("SquadDelete"),()=> { RemoveOwnRouteStop(marker.Id);redraw(); });remove.Tag="route-remove:"+m.Id;DockPanel.SetDock(remove,Dock.Right);stopRow.Children.Add(remove);var label=WpfFieldTheme.Text((SquadState.StopIndex(m)+1)+". "+m.Name);label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.None;stopRow.Children.Add(label);routes.Children.Add(stopRow); }
                var row=new WrapPanel();var undo=WpfFieldTheme.Button(S("SquadUndoStop"),()=> { UndoOwnRouteStop();redraw(); });undo.IsEnabled=local.Length>0;row.Children.Add(undo);var clear=WpfFieldTheme.Button(S("WpfClearRoute"),()=> { ClearOwnRoute();redraw(); });clear.IsEnabled=local.Length>0;row.Children.Add(clear);routes.Children.Add(row);
                var own=WpfFieldTheme.Button(S("SquadStartRoute"),()=> { StartSquadRoute(local);window.Close(); });own.IsEnabled=local.Length>0;routes.Children.Add(own);
                foreach(var route in remote) { var copy=route;routes.Children.Add(WpfFieldTheme.Button(S("SquadStartRoute")+": "+route[0].Owner+" · "+route.Length,()=> { StartSquadRoute(copy);window.Close(); })); }
            };redraw();
            var squadPage=pages[1];squadPage.Children.Add(WpfFieldTheme.Button(S("PrivatePositions"),ShowPrivatePositions));squadPage.Children.Add(SquadHelp("SquadPingHelp"));
            var pingRow=new WrapPanel();var pingButtons=new List<Button>();string[] keys={"PingDanger","PingLoot","PingPickup"};for(int i=0;i<3;i++) { int k=i;var ping=WpfFieldTheme.Button(S(keys[i]),()=>SendPing(k));ping.Tag="squad-ping-"+i;pingButtons.Add(ping);pingRow.Children.Add(ping); }squadPage.Children.Add(pingRow);
            var players=new StackPanel();squadPage.Children.Add(players);string playerIdentity=null;
            Action update=()=> { redraw();bool ready=CanSendPing();foreach(var ping in pingButtons)ping.IsEnabled=ready;string identity=string.Join(";",playerRoster.Select(p=>p.Id+":"+p.Name+":"+(p.Position!=null)+":"+PlayerVisibility(p.Id)))+"|"+followingPeer;if(identity!=playerIdentity) { playerIdentity=identity;players.Children.Clear();AddPlayers(players,false); }foreach(var label in SquadPlayerLabels(players)) { var peer=playerRoster.FirstOrDefault(p=>p.Id==(string)label.Tag);if(peer!=null)label.Text=PeerStatus(peer); } };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=>update();window.Loaded+=(s,e)=> { update();timer.Start(); };window.Closed+=(s,e)=>timer.Stop();
            body=pages[2];
            body.Children.Add(WpfFieldTheme.Text(S("SquadPresets"),19,null,true));
            string file=Path.Combine(engine.WpfDataFolder,"map-presets.dat");var presets=new List<MapPreset>();
            try { presets.AddRange(MapPresetFile.Load(file)); }catch(Exception e) { Program.LogException("MapPresets",e);body.Children.Add(WpfFieldTheme.Text(S("PrivatePositionInvalid"))); }
            var presetName=new TextBox { MaxLength=64,Text=S("SquadLooting"),Margin=new Thickness(0,8,0,8) };System.Windows.Automation.AutomationProperties.SetName(presetName,S("SquadPresets"));body.Children.Add(presetName);
            var names=new WrapPanel();foreach(string key in new[]{"SquadLooting","SquadDriving","SquadExploring"}) { string n=S(key);names.Children.Add(WpfFieldTheme.Button(n,()=>presetName.Text=n)); }body.Children.Add(names);
            var saved=new StackPanel();Action showSaved=null;Action save=()=> { try { MapPresetFile.Save(file,presets);showSaved(); }catch(Exception e) { Program.LogException("MapPresetsSave",e);System.Windows.MessageBox.Show(S("PrivatePositionInvalid")); } };
            showSaved=()=> { saved.Children.Clear();foreach(var p in presets.ToArray()) { var row=new WrapPanel();row.Children.Add(WpfFieldTheme.Button(p.Name,()=> { if(engine.WpfApplyPreset(p)) { filterIdentity=null;Refresh(); } }));row.Children.Add(WpfFieldTheme.Button(S("SquadDelete"),()=> { presets.Remove(p);save(); }));saved.Children.Add(row); } };
            body.Children.Add(WpfFieldTheme.Button(S("SquadSavePreset"),()=> { string name=presetName.Text.Trim();if(name.Length==0)return;var p=engine.WpfCapturePreset(name);presets.RemoveAll(v=>v.Name==name&&v.Map==p.Map);if(presets.Count>=24)return;presets.Add(p);save(); }));body.Children.Add(saved);showSaved();select(Math.Max(0,Math.Min(2,initial)));return window;
        }
    }
}
