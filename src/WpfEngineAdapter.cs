using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;

namespace ScumMiniMap {
    internal sealed class WpfMapState {
        internal Position Position;
        internal DateTime Updated;
        internal MapZone Target;
        internal RoadRoute Route;
        internal bool Calculating,Grid,Zones,Waypoints,History,Labels,Tracking,Heading,Compass,Status;
        internal int HudWidth,HudHeight,HudLeft,HudTop,PlayerColor,RouteColor,HistoryColor;
        internal bool StatusAbove;
        internal bool RotateMap;
        internal bool PoiClustering=true;
        internal PrivatePositions.Peer[] Peers=new PrivatePositions.Peer[0];
        internal SharedMapMarker[] SharedMarkers=new SharedMapMarker[0];
        internal SquadRoutePlan SquadPlan,OwnRoutePlan;
        internal SquadRouteDrawing[] OtherRoutePlans=new SquadRouteDrawing[0];
        internal SharedMapMarker[] LocalRoute=new SharedMapMarker[0];
        internal string ActiveSquadRoute,OwnRouteSummary;
        internal bool SquadPlanning,SquadNavigating,ActiveSquadShared;
        internal bool GridLabels,SmartLabels;
        internal int GridOpacity,LabelSize;
        internal int ZoneRevision;
        internal ScumMapStore Store;
        internal MapZone[] Saved;
        internal WpfZoneOption[] CustomZones;
        internal bool CustomZonesExpanded;
        internal PointF[] Trail;
        internal string Location,Shape,RouteSummary,TrackingStatus;
        internal bool TrackingPaused;
        internal int Opacity,MapKey,MapScan,ChatKey,CopyKey,CopyModifier;
        internal float Zoom;
        internal PointF Pan;
        internal AppControlBindings Bindings;
    }
    internal sealed class WpfSettingDescriptor { internal string Group,GroupKey;internal int Tab;internal System.Windows.Forms.Control Control; }
    internal sealed class WpfZoneOption { internal MapZone Zone;internal string Key,Layer;internal bool Enabled,LayerEnabled; }
    public sealed partial class MapWindow {
        readonly int wpfOwnerThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        void WpfVerifyAccess() { if(System.Threading.Thread.CurrentThread.ManagedThreadId!=wpfOwnerThread)throw new InvalidOperationException("Map state belongs to the UI thread."); }
        readonly List<WpfSettingDescriptor> wpfSettings=new List<WpfSettingDescriptor>();
        int wpfDefaultsRevision;
        internal bool wpfRotateMap;
        internal bool wpfPoiClustering=true;
        bool wpfTacticalOpacityOverride;
        int WpfTacticalOpacityValue { get { return fullMapZoom>1&&!wpfTacticalOpacityOverride?100:fullMapOpacity; } }
        internal void WpfRotate(bool enabled) { wpfRotateMap=enabled;SaveSettings(); }
        internal void WpfPoiClustering(bool enabled) { wpfPoiClustering=enabled;SaveSettings(); }
        internal void WpfDefaultColours() { routeGuidanceColor=OverlayTheme.Route;playerConeColor=OverlayTheme.Accent;routeHistoryColor=Color.FromArgb(164,177,188);SettingsChanged();SaveSettings();WpfRebuildSettings(); }
        readonly HashSet<string> disabledWpfZones=new HashSet<string>(StringComparer.Ordinal);
        sealed class ZoneKeyCache { public ZoneKeyCache() {}internal string Name,Layer,Key;internal PointF[] Points;internal PointF Centre; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MapZone,ZoneKeyCache> zoneKeys=new System.Runtime.CompilerServices.ConditionalWeakTable<MapZone,ZoneKeyCache>();
        internal int wpfZoneRevision;
        int cachedZoneRevision=-1,cachedZoneCount=-1;
        List<MapZone> cachedZoneList;
        MapZone[] cachedVisibleZones;
        WpfZoneOption[] cachedZoneOptions;
        int cachedTrailRevision=-1;
        PointF[] cachedTrail;
        List<MapZone> appFolderZones=new List<MapZone>();
        IEnumerable<MapZone> WpfAllZones() {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var zone in zones.Concat(appFolderZones))if(keys.Add(WpfZoneKey(zone)))yield return zone;
        }
        internal void WpfRefreshFolderZones(List<MapZone> value) {
            // Downloaded packs use the default SCUM map coordinates.
            appFolderZones=mapSidecarPaths.MapId!="default"?new List<MapZone>():value;
            SettingsChanged();
        }
        void WpfRefreshZoneCache() {
            if(cachedZoneRevision==wpfZoneRevision&&cachedZoneList==zones&&cachedZoneCount==(zones==null?0:zones.Count))return;
            var all=WpfAllZones().ToArray();
            var visible=all.Where(WpfZoneVisible).ToArray();if(cachedVisibleZones==null||!cachedVisibleZones.SequenceEqual(visible))cachedVisibleZones=visible;
            var options=all.Where(WpfCustomZone).Select(z=>new WpfZoneOption { Zone=z,Key=WpfZoneKey(z),Layer=string.IsNullOrWhiteSpace(z.Layer)?"Default":z.Layer,Enabled=!disabledWpfZones.Contains(WpfZoneKey(z)),LayerEnabled=!disabledZoneLayers.Contains(string.IsNullOrWhiteSpace(z.Layer)?"Default":z.Layer) }).ToArray();
            if(cachedZoneOptions==null||cachedZoneOptions.Length!=options.Length||cachedZoneOptions.Where((z,i)=>z.Zone!=options[i].Zone||z.Key!=options[i].Key||z.Enabled!=options[i].Enabled||z.LayerEnabled!=options[i].LayerEnabled).Any())cachedZoneOptions=options;
            cachedZoneRevision=wpfZoneRevision;cachedZoneList=zones;cachedZoneCount=zones==null?0:zones.Count;
        }
        internal static bool WpfCustomZone(MapZone zone) {
            return zone!=null&&zone.Points!=null&&zone.Points.Length>0&&!zone.IsDeathMarker&&(zone.Points.Length>1||(!string.IsNullOrWhiteSpace(zone.Layer)&&zone.Layer!="Default"&&zone.Layer!="Custom Waypoints"));
        }
        internal static string WpfZoneKey(MapZone zone) {
            var cached=zoneKeys.GetOrCreateValue(zone);if(cached.Key!=null&&cached.Name==zone.Name&&cached.Layer==zone.Layer&&cached.Points==zone.Points)return cached.Key;
            string value=zone.Layer+"\n"+zone.Name+"\n"+string.Join(";",zone.Points.Select(p=>p.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            using(var hash=System.Security.Cryptography.SHA256.Create())cached.Key=Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)));
            cached.Name=zone.Name;cached.Layer=zone.Layer;cached.Points=zone.Points;cached.Centre=zone.Centroid;return cached.Key;
        }
        internal static PointF WpfZoneCentre(MapZone zone) {
            var cached=zoneKeys.GetOrCreateValue(zone);if(cached.Key==null||cached.Name!=zone.Name||cached.Layer!=zone.Layer||cached.Points!=zone.Points)WpfZoneKey(zone);
            if(cached.Centre==PointF.Empty&&zone.Points.Length>0)cached.Centre=zone.Centroid;return cached.Centre;
        }
        internal void WpfZoneToggle(string key,bool enabled) {
            if(enabled) { disabledWpfZones.Remove(key);var zone=WpfAllZones().FirstOrDefault(z=>WpfCustomZone(z)&&WpfZoneKey(z)==key);if(zone!=null)disabledZoneLayers.Remove(string.IsNullOrWhiteSpace(zone.Layer)?"Default":zone.Layer);showZones=true; }
            else disabledWpfZones.Add(key);SettingsChanged();SaveSettings();
        }
        internal void WpfZoneLayerToggle(string layer,bool enabled) { if(enabled)disabledZoneLayers.Remove(layer);else disabledZoneLayers.Add(layer);if(enabled)showZones=true;SettingsChanged();SaveSettings(); }
        internal void WpfExpandZones() { sidebarZonesExpanded=!sidebarZonesExpanded;SettingsChanged();SaveSettings(); }
        internal void WpfInitializeDefaults() {
            if(scumMap!=null)foreach(var category in scumMap.Categories)category.DefaultEnabled=true;
            if(wpfDefaultsRevision>=2)return;
            if(wpfDefaultsRevision<1) { showScumMap=true;if(scumMap!=null) { scumMap.MasterEnabled=true;scumMap.SetAllEnabled(true); }fullMapOpacity=0; }
            if(labelSize==6)labelSize=16;
            wpfDefaultsRevision=2;SettingsChanged();SaveSettings();
        }
        internal void WpfTacticalOpacity(int opacity) { WpfVerifyAccess();wpfTacticalOpacityOverride=true;fullMapOpacity=Math.Max(0,Math.Min(100,opacity));WpfCameraChanged(); }
        void WpfCameraChanged() { WpfVerifyAccess();terrainKey=null;lastFrameKey=null;saveAfter=DateTime.UtcNow.AddMilliseconds(700); }
        internal void WpfUpdateCamera(WpfMapState state,bool tactical) {
            WpfVerifyAccess();if(tactical&&wpfFollowPlayer&&position!=null)fullMapPan=ToMap(position);
            state.Zoom=tactical?fullMapZoom:zoom;state.Opacity=tactical?WpfTacticalOpacityValue:mapOpacity;state.Pan=fullMapPan;
        }
        void WpfCaptureSettings(IEnumerable<System.Windows.Forms.Control> controls) {
            WpfSettingsRevision++;wpfSettings.Clear();string group="",groupKey=null;int tab=0;
            foreach(var control in controls) {
                if((string)control.Tag=="section") { group=control.Text;groupKey=new[]{"ShortcutHeading","SecGeneral","SecMapZones","SecAppearance","VoiceHeading","SecTrackingZoom","SecToolsShortcuts"}.FirstOrDefault(key=>group==Localization.Get(key));tab=groupKey=="ShortcutHeading"?1:(groupKey=="SecMapZones"||groupKey=="SecAppearance")?2:0;continue; }
                wpfSettings.Add(new WpfSettingDescriptor { Group=group,GroupKey=groupKey,Tab=tab,Control=control });
            }
        }
        internal int WpfSettingsRevision { get;private set; }
        internal WpfSettingDescriptor[] WpfSettings(int tab) { return wpfSettings.Where(s=>s.Tab==tab&&!s.Control.IsDisposed).ToArray(); }
        internal void WpfRebuildSettings() { BuildSettingsPanel(); }
        bool wpfFollowPlayer=true;
        internal bool WpfFollowing { get { return wpfFollowPlayer; } }
        internal void WpfFollow() { wpfFollowPlayer=true; }
        internal string WpfTrackingStatus() {
            if(musicalScumbagOpen)return Localization.Get("FieldPaused")+" — MusicalScumbag";
            if(AdminPlayerWorkPaused)return Localization.Get("FieldPaused")+" — "+Localization.Get("PlayersTitle");
            return note;
        }
        internal bool WpfZoneVisible(MapZone zone) {
            return zone!=null&&(!WpfCustomZone(zone)||(!disabledZoneLayers.Contains(string.IsNullOrWhiteSpace(zone.Layer)?"Default":zone.Layer)&&!disabledWpfZones.Contains(WpfZoneKey(zone))));
        }
        internal SharedMapMarker[] WpfSharedMarkers() {
            var markers=new List<SharedMapMarker>();
            if(searchTarget!=null&&!object.ReferenceEquals(searchTarget,wpfUnsharedTarget)&&searchTarget.Points!=null&&searchTarget.Points.Length>0) {
                var point=searchTarget.Centroid;string name=searchTarget.DisplayName??"";if(name.Length>96)name=name.Substring(0,96);
                var gps=new SharedMapMarker { Id=SharedMapMarker.GpsId,Name=name,X=point.X,Y=point.Y };
                if(SharedMapMarker.Valid(gps))markers.Add(gps);
            }
            markers.AddRange((zones??new List<MapZone>()).Where(zone=>zone.Category==ZoneCategory.Custom&&zone.Points!=null&&zone.Points.Length==1)
                .OrderByDescending(zone=>zone.IsDeathMarker).ThenByDescending(zone=>zone.DeathCreatedUtc??DateTime.MinValue).Take(128)
                .Select(zone=>new SharedMapMarker { Id=BitConverter.ToString(Convert.FromBase64String(WpfZoneKey(zone))).Replace("-",""),Name=(zone.IsDeathMarker?Localization.Get("DeathName"):zone.DisplayName)??"",Death=zone.IsDeathMarker,X=zone.Points[0].X,Y=zone.Points[0].Y })
                .Select(marker=> { if(marker.Name.Length>96)marker.Name=marker.Name.Substring(0,96);return marker; }).Where(SharedMapMarker.Valid));
            return markers.Take(128).ToArray();
        }
        internal WpfMapState WpfSnapshot(bool tactical) {
            WpfVerifyAccess();
            WpfRefreshZoneCache();
            if(cachedTrailRevision!=locationHistoryRevision) { cachedTrail=locationHistoryPoints.Select(p=>p.MapPoint).ToArray();cachedTrailRevision=locationHistoryRevision; }
            if(tactical&&wpfFollowPlayer&&position!=null)fullMapPan=ToMap(position);
            return new WpfMapState { Position=position,Updated=updated,Target=searchTarget,Route=activeRoute,Calculating=routeCalculating,
                RotateMap=wpfRotateMap,PoiClustering=wpfPoiClustering,ZoneRevision=wpfZoneRevision,CustomZonesExpanded=sidebarZonesExpanded,CustomZones=cachedZoneOptions,
                Grid=gridBorders,GridLabels=gridLabels,GridOpacity=gridOpacity,LabelSize=labelSize,SmartLabels=smartLabelLod,Zones=showZones,Waypoints=showCustomWaypoints,History=locationHistory,Labels=showZoneLabels,Tracking=TrackingEnabled,
                Heading=showHeading,Compass=showCompass,Status=showStatus,StatusAbove=statusPos=="Above",HudWidth=overlay.MinimapBounds.Width,HudHeight=overlay.MinimapBounds.Height,HudLeft=overlay.MinimapBounds.Left,HudTop=overlay.MinimapBounds.Top,PlayerColor=playerConeColor.ToArgb(),RouteColor=routeGuidanceColor.ToArgb(),Store=scumMap,Saved=cachedVisibleZones,Trail=cachedTrail,
                HistoryColor=routeHistoryColor.ToArgb(),TrackingStatus=WpfTrackingStatus(),TrackingPaused=IntensiveWorkPaused||chat.Paused||inventoryInputLocked,
                Location=position==null?WpfTrackingStatus():GetLocationDescription().Split(new[]{"  |  -> "},StringSplitOptions.None)[0],
                Shape=overlayShape,RouteSummary=FieldConsoleRouteSummary(),Opacity=tactical?WpfTacticalOpacityValue:mapOpacity,Zoom=tactical?fullMapZoom:zoom,Pan=fullMapPan,
                MapKey=scumMapKey,MapScan=scumMapScanCode,ChatKey=scumChatKey,CopyKey=scumCopyKey,CopyModifier=scumCopyModifierKey,Bindings=CurrentAppControls() };
        }
        internal bool WpfCanPing(bool fromSquadTools) {
            WpfVerifyAccess();
            // An explicit ping may use a fresh captured position while this
            // dialog protects game input. It never resumes coordinate capture.
            return (!shortcutCaptureOpen||fromSquadTools&&panelOpening)&&!IntensiveWorkPaused&&!diagnosticMode&&!closing&&!adminCommandBusy&&!chat.Paused&&!inventoryInputLocked&&position!=null&&PrivatePositions.Fresh(updated,DateTime.UtcNow);
        }
        internal List<MapZone> WpfSearch(string text) {
            PointF? origin=position==null?(PointF?)null:ToMap(position);var matches=DestinationSearch.Find(WpfAllZones().ToList(),text,origin);
            if(scumMap!=null)matches.AddRange(DestinationSearch.FindScumMap(scumMap,text,origin,100));
            var grid=GridTarget(text);if(grid!=null)matches.Insert(0,grid);return matches;
        }
        internal MapZone WpfClickedTarget(PointF point,float hitRadius) {
            string name=null;bool mapObject=false;
            var savedPoint=WpfAllZones().Where(z=>WpfCustomZone(z)&&z.Points!=null&&z.Points.Length>0&&z.Points.Length<3)
                .Select(z=>new { Zone=z,Distance=(z.Points[0].X-point.X)*(z.Points[0].X-point.X)+(z.Points[0].Y-point.Y)*(z.Points[0].Y-point.Y) }).Where(z=>z.Distance<=hitRadius*hitRadius).OrderBy(z=>z.Distance).FirstOrDefault();
            if(savedPoint!=null) { name=savedPoint.Zone.Name;mapObject=savedPoint.Zone.IsMapObject; }
            if(string.IsNullOrWhiteSpace(name)&&scumMap!=null) {
                var marker=scumMap.Markers
                    .Select(m=>new { Marker=m,Distance=(m.X-point.X)*(m.X-point.X)+(m.Y-point.Y)*(m.Y-point.Y) })
                    .Where(m=>m.Distance<=hitRadius*hitRadius).OrderBy(m=>m.Distance).FirstOrDefault();
                if(marker!=null) { name=scumMap.GetMarkerDisplayName(marker.Marker);mapObject=true; }
            }
            if(string.IsNullOrWhiteSpace(name)&&zones!=null) {
                var area=WpfAllZones().Where(z=>z.Points!=null&&z.Points.Length>2&&!string.IsNullOrWhiteSpace(z.Name)&&z.Contains(point))
                    .OrderBy(z=>(z.Points.Max(p=>p.X)-z.Points.Min(p=>p.X))*(z.Points.Max(p=>p.Y)-z.Points.Min(p=>p.Y))).FirstOrDefault();
                if(area!=null) { name=area.Name;mapObject=area.IsMapObject; }
            }
            if(string.IsNullOrWhiteSpace(name)&&scumMap!=null) {
                var town=scumMap.Settlements.Select(t=>new { Town=t,Distance=(t.X-point.X)*(t.X-point.X)+(t.Y-point.Y)*(t.Y-point.Y) })
                    .Where(t=>t.Distance<=t.Town.Radius*t.Town.Radius).OrderBy(t=>t.Distance).FirstOrDefault();
                if(town!=null) { name=town.Town.Name;mapObject=true; }
            }
            if(string.IsNullOrWhiteSpace(name))name=DestinationSearch.Sector(point);
            return new MapZone { Name=name,IsMapObject=mapObject,Points=new[]{point} };
        }
        MapZone wpfQueuedTarget;
        MapZone wpfUnsharedTarget;
        internal MapZone WpfCurrentTarget { get { return searchTarget; } }
        internal void WpfSquadTarget(MapZone target) { WpfVerifyAccess();wpfUnsharedTarget=target;WpfTarget(target); }
        internal void WpfTarget(MapZone target) { WpfVerifyAccess();searchTarget=target;pin=null;activeRoute=null;routeGeneration++;lastRouteTarget=null;lastFrameKey=null;saveAfter=DateTime.UtcNow.AddMilliseconds(700);if(position!=null) { if(routeCalculating)wpfQueuedTarget=target;else UpdateRouteAsync(ToMap(position),target); } }
        internal void WpfClear() { WpfVerifyAccess();ClearActiveWaypoint(); }
        internal void WpfModal(bool open) { panelOpening=open;shortcutCaptureOpen=open;Native.CancelActiveCopy();fallbackKeysArmed=false; }
        internal bool WpfSettingsKeyMatches(int key,int scan) { return MatchesAppControl(AppControlAction.Settings,key,scan); }
        internal void WpfLayer(string key,bool value) {
            switch(key) { case "zones":showZones=value;break;case "waypoints":showCustomWaypoints=value;break;case "grid":gridBorders=value;break;case "history":locationHistory=value;break;case "labels":showZoneLabels=value;break; }
            SettingsChanged();
        }
        internal void WpfAppearance(string shape,int opacity) { overlayShape=shape=="Circle"?"Circle":"Square";mapOpacity=Math.Max(30,Math.Min(100,opacity));SettingsChanged();SaveSettings(); }
        string wpfOnboardingVersion="";
        bool hideOnboardingGuide;
        internal bool WpfHideOnboardingGuide { get { return hideOnboardingGuide; } }
        internal void WpfSetHideOnboardingGuide(bool hide) { hideOnboardingGuide=hide;SaveSettings(); }
        internal bool WpfNeedsOnboarding { get { return isFirstLaunch || wpfOnboardingVersion!=VersionString; } }
        internal void WpfCompleteOnboarding() { wpfOnboardingVersion=VersionString;isFirstLaunch=false;SaveSettings(); }
        internal void WpfPreviewPosition(string value) { if(!diagnosticMode)throw new InvalidOperationException("Preview requires diagnostic mode.");Accept(value);zoom=targetZoom=4f;autoZoom=false; }
        internal void WpfTactical(bool active) { fullMapActive=active;Native.CancelActiveCopy();inputGeneration++;fallbackKeysArmed=false; }
        internal void WpfZoom(bool tactical,int direction) {
            WpfVerifyAccess();
            float step=1f+zoomStepPercent/100f;
            if(tactical)fullMapZoom=Math.Max(1,Math.Min(maxZoom,fullMapZoom*(float)Math.Pow(step,direction)));
            else { autoZoom=false;zoom=Math.Max(1,Math.Min(maxZoom,zoom*(float)Math.Pow(step,direction)));targetZoom=zoom; }
            WpfCameraChanged();
        }
        internal void WpfPan(PointF point) { WpfVerifyAccess();wpfFollowPlayer=false;fullMapPan=point;WpfCameraChanged(); }
        internal string WpfBind(AppControlAction action,int key,int scan) {
            string error=AppControlBindingError(action,key,scan);if(error!=null)return error;
            var bindings=CurrentAppControls();bindings.Set(action,key,scan);ApplyAppControls(bindings);return null;
        }
        internal void WpfSingleCopyKey(bool single) { Native.CancelActiveCopy();scumCopyModifierKey=single?0:0x11;inputGeneration++;SettingsChanged();SaveSettings(); }
        internal string WpfGameBind(int index,int key,int scan) {
            if(key<=0||key>=256||key==0x1B||key==0x0D||key==0x09||PhysicalKeyCapture.IsModifierKey(key))return Localization.Get("KeyWizardNonModifier");
            int[] gameKeys={scumMapKey,scumChatKey,scumCopyKey},scans={scumMapScanCode,scumChatScanCode,scumCopyScanCode};gameKeys[index]=key;scans[index]=scan;
            if(PhysicalKeyCapture.HasConflicts(gameKeys,scans))return Localization.Get("KeyWizardDifferentKeys");
            string error=CurrentAppControls().BindingError(gameKeys[0],scans[0],gameKeys[1],scans[1],gameKeys[2],scans[2]);if(error!=null)return error;
            scumMapKey=gameKeys[0];scumChatKey=gameKeys[1];scumCopyKey=gameKeys[2];scumMapScanCode=scans[0];scumChatScanCode=scans[1];scumCopyScanCode=scans[2];inputGeneration++;SettingsChanged();SaveSettings();return null;
        }
        internal void WpfSave() { SaveSettings(); }
        internal void WpfScheduleSave() { WpfVerifyAccess();saveAfter=DateTime.UtcNow.AddMilliseconds(700); }
        internal void WpfMoveHud(int left,int top) { overlay.SetMinimapLocation(new Point(left,top));SettingsChanged();SaveSettings(); }
        internal bool WpfDeleteWaypoint(MapZone marker) {
            if(marker==null||zones==null||!zones.Contains(marker)||marker.Points==null||marker.Points.Length!=1)return false;
            if(marker.IsDeathMarker)return RemoveDeathMarker(marker);
            var candidate=new List<MapZone>(zones);candidate.Remove(marker);
            if(!TryCommitZones(candidate,this))return false;
            if(searchTarget==marker)WpfClear();SettingsChanged();return true;
        }
        internal ZoneEditor WpfZoneEditor() {
            if(zonesLoadFailed)throw new InvalidOperationException(Localization.Get("NoteZonesLoadFail"));
            var waypoints=zones.Where(IsCustomWaypointZone).ToList();
            return new ZoneEditor(map,zones.Where(z=>!IsCustomWaypointZone(z)).ToList(),zonesPath,value=> {
                zones=value.Concat(waypoints).ToList();
                try { if(File.Exists(zonesPath))lastZonesFileWriteTimeUtc=File.GetLastWriteTimeUtc(zonesPath); }catch(IOException) { }
                SettingsChanged();
            },waypoints) { WpfMode=true };
        }
        internal bool WpfWaypoint(string name,PointF? point) {
            WpfVerifyAccess();
            if(string.IsNullOrWhiteSpace(name)||!point.HasValue&&position==null)return false;PointF destination=point.HasValue?point.Value:ToMap(position);
            var next=new List<MapZone>(zones??new List<MapZone>());next.Add(new MapZone { Name=name,Category=ZoneCategory.Custom,Argb=OverlayTheme.Route.ToArgb(),Points=new[]{destination},Layer="Custom Waypoints" });
            if(!TryCommitZones(next,this))return false;
            SettingsChanged();return true;
        }
        internal void WpfNativeTool(AppControlAction action) {
            switch(action) { case AppControlAction.DeathMarker:TriggerDeathMarker();break;case AppControlAction.ZoneEditor:OpenZoneImport(null);break;case AppControlAction.AdminCommands:ShowAdminCommands();break;case AppControlAction.AdminPlayers:ShowAdminPlayers();break;case AppControlAction.ResetZoom:fullMapZoom=zoom=targetZoom=1;SettingsChanged();break; }
        }
        internal void WpfExit() { ExitApp(); }
        internal void WpfPrepareExit() {
            WpfVerifyAccess();if(closing)return;
            closing=true;wpfAdminControlsRequested=false;adminControlsCommandReturn=false;
            Native.CancelActiveCopy();timer.Stop();SaveSettings();
            foreach(var tool in new System.Windows.Forms.Form[]{adminCommandsPanel,adminPlayersPanel,spawnCartDialog}.Concat(adminProfileWindows).ToArray())
                if(tool!=null&&!tool.IsDisposed)tool.Close();
        }
        internal string WpfDataFolder { get { return dataFolder; } }
    }
}
