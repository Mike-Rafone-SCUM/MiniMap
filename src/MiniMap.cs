using System;



using System.Diagnostics;



using System.Drawing;



using System.Globalization;



using System.IO;



using System.Reflection;



using System.Runtime.InteropServices;

using System.Threading;



using System.Text.RegularExpressions;



using System.Windows.Forms;



using System.Threading.Tasks;



using System.Drawing.Imaging;



using System.Collections.Generic;

using System.Linq;



using System.Drawing.Drawing2D;



[assembly: AssemblyTitle("SkynettMiniMap")]



[assembly: AssemblyDescription("Tactical Real-Time Overlay for SCUM")]



[assembly: AssemblyCompany("MikeRafone")]



[assembly: AssemblyProduct("SkynettMiniMap")]



[assembly: AssemblyCopyright("Copyright Â© 2026 MikeRafone")]



[assembly: AssemblyVersion("1.4.9.0")]



[assembly: AssemblyFileVersion("1.4.9.0")]



[assembly: AssemblyInformationalVersion("1.4.9")]



namespace ScumMiniMap {







    public sealed partial class MapWindow:Form {



        readonly MapCanvas canvas=new MapCanvas();



        bool TrackingEnabled { get { return !IntensiveWorkPaused && !diagnosticMode && !closing && !adminCommandBusy && !shortcutCaptureOpen; } }



        readonly Label status=new Label();
        readonly Panel settingsFooter=new Panel();
        readonly Button settingsDoneBtn=new Button();
        readonly Button settingsOpenFolderBtn=new Button();

        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();



        readonly MapMotion motion=new MapMotion();



        readonly Stopwatch motionClock=Stopwatch.StartNew();



        PointF fixedMapCentre=new PointF(.5f,.5f);



        bool fixedCentreReady;



        string terrainKey;



        internal int TerrainBuilds;
        const int TerrainMargin=64;
        float terrainLeft,terrainTop;



        double filteredSpeed;



        readonly Image map;
        readonly MapTilePyramid mapTiles;



        readonly List<Bitmap> mapLevels=new List<Bitmap>();



        Bitmap overlayFrame=new Bitmap(400,240,PixelFormat.Format32bppPArgb);



        Bitmap fullMap=new Bitmap(400,240,PixelFormat.Format32bppPArgb);



        // Keep sector lines opt-in. They are useful for grid searches but obscure the
        // terrain at normal zoom, especially on the full map and circular minimap.
        bool gridLabels=true,gridBorders=false,showStatus=true,autoZoom=true;



        bool showHeading=true,showCompass=true,zoneChime=false;



        string statusPos="Below";



        string overlayShape="Circle";



        int copyIntervalMs=Program.MinimumCopyIntervalMs;



        int mapOpacity=70;



        int gridOpacity=7;

        bool locationHistory=true,locationHistoryTimestamps=false;
        readonly List<LocationHistoryPoint> locationHistoryPoints=new List<LocationHistoryPoint>();
        int locationHistoryRevision;
        int locationHistoryPathRevision=-1;
        GraphicsPath locationHistoryPath;
        int locationHistoryMinutes=30;
        const int LocationHistoryLimit=14401;
        const int LocationHistoryTimestampIntervalSeconds=30;
        Point? locationHistoryHoverPoint;
        int displayedLocationHistorySegment=-1;

        sealed class LocationHistoryPoint {
            internal PointF MapPoint;
            internal DateTime SampledAtUtc;
        }



        int labelSize=16;



        bool showZones=true;



























        bool showCustomWaypoints=true;



        bool showZoneLabels=true;



        bool smartLabelLod=true;



        bool fullMapActive;






        float fullMapZoom=1.0f;



        PointF fullMapPan=new PointF(0.5f,0.5f);



        int fullMapOpacity=100;
        bool fullMapZoomOpacityOverride;
        internal int EffectiveFullMapOpacity { get { return overlay!=null && overlay.FullMapClickThrough?0:fullMapZoomOpacityOverride?100:fullMapOpacity; } }
        void RestoreFullMapOpacityAfterTeleport() {
            if(!fullMapZoomOpacityOverride)return;
            fullMapZoomOpacityOverride=false;
            InvalidateFullMapSidebar();lastFrameKey=null;
            if(!closing && !IsDisposed)RenderOverlay();
        }



        bool draggingSlider;



        bool draggingMap;



        Point dragStart;



        PointF panStart;



        Point mouseDownPt;



        DateTime mouseDownTime = DateTime.MinValue;



        bool mouseMoved;



        PointF? pendingFullMapWaypointMapPoint;



        readonly bool[] fallbackKeyDown=new bool[256];



        bool fallbackKeysArmed;



        DateTime lastMToggleTime = DateTime.MinValue;






        bool sidebarWildlifeExpanded = true;
        bool sidebarZonesExpanded = false;
        readonly HashSet<string> disabledZoneLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);



        // Sidebar state and scrolling



        HashSet<string> sidebarExpandedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bunkers", "Outposts", "Hunting" };



        int sidebarScrollY = 0;



        int sidebarMaxScroll = 0;



        bool draggingSidebarScrollbar;



        int scrollbarDragStartY, scrollbarDragStartScrollY;



        Rectangle sidebarScrollbarThumbRect;



        Rectangle sidebarScrollbarTrackRect;



        Bitmap fullMapSidebarFrame;



        int fullMapSidebarRevision;



        int renderedFullMapSidebarRevision=-1;



        // Pinned header rectangles



        Rectangle sidebarZoomInRect;



        Rectangle sidebarZoomOutRect;



        Rectangle sidebarZoomResetRect;



        Rectangle sidebarOpacityTrackRect;



        Rectangle sidebarSearchRect;



        Rectangle sidebarClearWaypointRect;



        // Dynamic clickable items in scrollable sidebar



        sealed class SidebarClickableItem {



            public Rectangle ScreenRect;



            public Action OnClick;



        }



        readonly List<SidebarClickableItem> sidebarClickables = new List<SidebarClickableItem>();



        Position pin;



        MapZone searchTarget;

        Color routeGuidanceColor = OverlayTheme.Route;
        Color playerConeColor = OverlayTheme.Accent;
        Color routeHistoryColor = Color.FromArgb(164,177,188);
        static readonly GraphicsPath ConeBasePath = CreateConeBasePath();
        static GraphicsPath CreateConeBasePath() {
            GraphicsPath path = new GraphicsPath();
            List<PointF> arcPoints = new List<PointF>();
            arcPoints.Add(new PointF(0, 0));
            float coneDist = 58f;
            float halfAngle = 36f * (float)(Math.PI / 180.0);
            int steps = 14;
            for(int i = 0; i <= steps; i++) {
                float a = -halfAngle + (2f * halfAngle * i / steps);
                arcPoints.Add(new PointF(coneDist * (float)Math.Cos(a), coneDist * (float)Math.Sin(a)));
            }
            path.AddPolygon(arcPoints.ToArray());
            return path;
        }
        PathGradientBrush cachedConeBrush;
        Color cachedConeBrushColor = Color.Empty;
        PathGradientBrush GetConeBrush(Color playerColor) {
            if(cachedConeBrush == null || cachedConeBrushColor != playerColor) {
                if(cachedConeBrush != null) { try { cachedConeBrush.Dispose(); } catch {} }
                cachedConeBrush = new PathGradientBrush(ConeBasePath);
                cachedConeBrush.CenterPoint = new PointF(0, 0);
                cachedConeBrush.CenterColor = Color.FromArgb(140, playerColor.R, playerColor.G, playerColor.B);
                cachedConeBrush.SurroundColors = new Color[] { Color.FromArgb(0, playerColor.R, playerColor.G, playerColor.B) };
                cachedConeBrushColor = playerColor;
            }
            return cachedConeBrush;
        }
        readonly List<MapZone> categoryDrawList = new List<MapZone>();
        readonly List<MapZone> customZoneDrawList = new List<MapZone>();
        readonly List<MapZone> waypointDrawList = new List<MapZone>();
        PointF[] routeScreenPoints=new PointF[0];
        readonly System.Text.StringBuilder frameKeyBuilder = new System.Text.StringBuilder(256);
        bool copyInProgress;



        // These are the SCUM bindings used by MiniMap. They default to SCUM's standard



        // Map (M), Chat (T), and coordinate-copy (NumPad Divide, no modifier) bindings, but can be captured



        // from the user's controls in the key rebinding wizard.



        int scumMapKey=0x4D;



        int scumChatKey=0x54;
        int scumMapScanCode,scumChatScanCode;
        int settingsShortcutKey=0x24,pinShortcutKey=0x2D,searchShortcutKey=0x2E;
        int settingsShortcutScanCode,pinShortcutScanCode,searchShortcutScanCode;
        bool shortcutCaptureOpen;



        int scumCopyModifierKey=Program.DefaultCopyModifierKey;



        int scumCopyKey=Program.DefaultCopyKey;
        int scumCopyScanCode;



        RoadRoute activeRoute;



        PointF lastRoutePlayerPt = PointF.Empty;



        MapZone lastRouteTarget;



        bool routeCalculating;
        long routeGeneration;



        List<MapZone> zones=new List<MapZone>();



        ScumMapStore scumMap;



        bool showScumMap=true;



        string pendingDisabledCats="v2;-2008,-2007,-2006,-2005,-2004,-2003,-2002,-2001,-2000,-1014,-1013,-1012,-1011,-1010,-1009,-1008,-1007,-1006,-1005,-1004,-1003,-1002,-1001,-1000,4,5,6,7,9,12,13,14,17,19,20,21,22,23,24,25,28,29,30,32,34,36,37,38,39,40,41,42,43,44,45,47,48,49,50,52,53,54,55,56,57,58,59,60,61,62,63,64,78,81,210,250,251,252,279,320,421,642,643,712,761,762,774,776,777,778,789,790,803,858,859,860,861,862,863,864,865,866,867,868,869,871,872,873,874";



        int savedWidth=240,savedHeight=240,savedLeft=-1,savedTop=-1;



        readonly string dataFolder;



        string zonesPath;
        string customWaypointsPath;
        MapSidecarPaths mapSidecarPaths;
        DateTime lastZonesFileWriteTimeUtc = DateTime.MinValue;
        DateTime lastWaypointsFileWriteTimeUtc = DateTime.MinValue;



        readonly string settingsPath;



        bool isFirstLaunch=false;
        bool updateResetPending=false;
        bool suppressCopyKeyReminder=false;



        DateTime saveAfter=DateTime.MaxValue;



        NumericUpDown widthOption,heightOption;



        string lastFrameKey;



        // The location label is used both to decide whether a frame changed and to draw the HUD.



        // Keep the expensive zone lookup stable while smooth-motion frames are being rendered.



        string cachedLocationDescription;



        Position cachedLocationPosition;



        MapZone cachedLocationTarget;



        RoadRoute cachedLocationRoute;






        int locationDescriptionRevision;



        int cachedLocationDescriptionRevision=-1;



        internal int PresentedFrames;



        bool canvasStale;



        string renderedInfobarText;

        string renderedInfobarBase;



        DateTime infobarScrollStart=DateTime.MinValue;



        bool infobarScrollActive;



        readonly OverlayWindow overlay;



        readonly GameKeys keys;



        readonly ChatState chat=new ChatState();



        readonly NotifyIcon tray;



        readonly ContextMenuStrip trayMenu=new ContextMenuStrip();



        readonly SettingsPanel bar;



        bool tickBusy,closing;
        int renderQueued;



        DateTime resumeAfter=DateTime.MinValue;



        Position position;



        PointF? previousMapPoint;



        DateTime previousTime=DateTime.MinValue;



        float targetZoom=1.0f;



                string lastVisitedAnchorName;
        PointF lastVisitedAnchorPoint;
        struct LocationAnchor {
            public string Name;
            public string DisplayName { get { return Zone!=null?Zone.DisplayName:Localization.GetMapObjectName(Name); } }
            public PointF Point;
            public MapZone Zone;
            public double Radius;
            public double DistanceTo(PointF p) {
                if (Zone != null) return Zone.DistanceTo(p);
                double dx = Point.X - p.X, dy = Point.Y - p.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            public bool Contains(PointF p) {
                if (Zone != null) return Zone.Contains(p);
                return DistanceTo(p) <= (Radius > 0 ? Radius : 0.015);
            }
        }

        List<LocationAnchor> cachedLocationAnchors;
        List<LocationAnchor> GetLocationAnchors() {
            if(cachedLocationAnchors != null) return cachedLocationAnchors;
            List<LocationAnchor> list = new List<LocationAnchor>();
            if (zones != null) {
                foreach (MapZone z in zones) {
                    if (z != null && !string.IsNullOrWhiteSpace(z.Name)) {
                        list.Add(new LocationAnchor { Name = z.Name, Point = z.Centroid, Zone = z, Radius = 0 });
                    }
                }
            }
            if (scumMap != null && scumMap.Markers != null) {
                foreach (ScumMapMarker m in scumMap.Markers) {
                    int cid = m.CategoryId;
                    // Settlements, major regional POIs, Outposts, and major Bunkers
                    if (cid == 27 || cid == 26 || cid == 80 || cid == 1 || cid == 456 || cid == 763) {
                        string name = scumMap.GetMarkerDisplayName(m);
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (name == "Bunkers" || name == "Bunker") {
                            name = DestinationSearch.Sector(m.Point) + " Bunker";
                        }

                        double rad = 0.015;
                        if (cid == 27) {
                            rad = 0.018;
                        } else if (name.IndexOf("City", StringComparison.OrdinalIgnoreCase) >= 0 || name.Equals("Novigrad", StringComparison.OrdinalIgnoreCase)) {
                            rad = 0.035;
                        } else if (cid == 80) {
                            rad = 0.014;
                        } else if (cid == 1 || cid == 456 || cid == 763) {
                            rad = 0.010;
                        }

                        list.Add(new LocationAnchor { Name = name, Point = m.Point, Zone = null, Radius = rad });
                    }
                }
            }
            cachedLocationAnchors = list;
            return list;
        }


        MapZone lastVisitedZone;



        DateTime lastVisitedTime=DateTime.MinValue;



        DateTime updated=DateTime.MinValue,next=DateTime.MinValue,sent;



        uint sequence;



        bool pending;
        bool clipboardRestoreAllowed;



        IDataObject savedDataObject;
        bool clipboardSnapshotReady;



        float zoom=32f;



        int maxZoom=32;



        int autoZoomMin=5;



        int autoZoomMax=32;



        int zoomStepPercent=10;



        int failures;



        int attempts,responses;



        bool wasGameFocused;
        volatile bool inventoryInputLocked;
        bool adminMapClickArmed;
        DateTime nextInventoryProbe=DateTime.MinValue;
        int inventoryProbeMisses;



        bool hiddenByFocusLoss;
        bool wasObsFocused;
        DateTime lastAltDownTime = DateTime.MinValue;



        readonly string diagnosticPath;



        string lastDiagnostic="";



        string note="";



        readonly bool diagnosticMode;



        public MapWindow(string folder,bool diagnostic=false) {



            diagnosticMode=diagnostic;



            dataFolder=folder;



            diagnosticPath=Path.Combine(folder,"automatic.log");



            settingsPath=Path.Combine(folder,"settings.ini");



            LoadSettings();
            TraceShortcutBindings();



            note=Localization.Get("NoteAutoWaiting");



            try { scumMap=ScumMapStore.Load(dataFolder); } catch { scumMap=new ScumMapStore(); }



            if(scumMap!=null) {



                scumMap.MasterEnabled=showScumMap;



                if(pendingDisabledCats!=null) scumMap.ApplyDisabledCategoriesString(pendingDisabledCats);



            }



            Text=Localization.T("HeaderSettings",VersionString+(Program.IsTestBuild?" TEST":"")); TopMost=true; ClientSize=new Size(450,640); MinimumSize=new Size(450,480);



            BackColor=OverlayTheme.Background; ForeColor=OverlayTheme.Ink;



            Font=OverlayTheme.UiFont(9); StartPosition=FormStartPosition.Manual;



            Location=new Point(Screen.PrimaryScreen.WorkingArea.Right-450,80);



            string mapFile=Path.Combine(folder,"map.png");



            string mapWarning;
            bool customMapLoaded=false;
            Stream tileResource=InstallationAssets.Open("map-tiles.bin");
            mapWarning=null;
            if(tileResource!=null) {
                if(File.Exists(mapFile)) {
                    try { using(var file=File.OpenRead(mapFile)) map=SafeMapImage.Decode(file); customMapLoaded=true; }
                    catch(Exception ex) {
                        if(!(ex is IOException) && !(ex is InvalidDataException) && !(ex is UnauthorizedAccessException)
                            && !(ex is ArgumentException) && !(ex is OutOfMemoryException)
                            && !(ex is System.Runtime.InteropServices.ExternalException)) throw;
                        mapWarning=Localization.Get("CustomMapRecovery");
                    }
                }
                if(map==null) { mapTiles=new MapTilePyramid(tileResource); map=mapTiles.Overview(); }
                else tileResource.Dispose();
            } else map=SafeMapImage.Load(mapFile,()=>System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("map.png"),out mapWarning);
            if(tileResource==null && File.Exists(mapFile) && mapWarning==null) customMapLoaded=true;
            if(mapWarning!=null && !diagnosticMode) Shown+=(sender,e)=>RunUiModal(() => MessageBox.Show(mapWarning,Localization.Get("ZoneEditorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Warning));

            mapSidecarPaths=MapSidecarPaths.ForMap(dataFolder,customMapLoaded?mapFile:null);
            mapSidecarPaths.EnsureImage(map);
            zonesPath=mapSidecarPaths.ZonesPath;
            customWaypointsPath=mapSidecarPaths.WaypointsPath;
            try {
                bool migrated;
                zones=MapSidecarStore.Load(mapSidecarPaths,Path.Combine(dataFolder,"zones.tsv"),!customMapLoaded,out migrated);
                if(File.Exists(zonesPath)) lastZonesFileWriteTimeUtc=File.GetLastWriteTimeUtc(zonesPath);
                if(File.Exists(customWaypointsPath)) lastWaypointsFileWriteTimeUtc=File.GetLastWriteTimeUtc(customWaypointsPath);
            } catch(InvalidDataException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); }
              catch(IOException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); }
              catch(UnauthorizedAccessException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); }

            Image level=map;



            while(mapTiles==null && level.Width>512 && level.Height>512) {



                Bitmap reduced=new Bitmap(level.Width/2,level.Height/2,PixelFormat.Format32bppPArgb);



                using(Graphics graphics=Graphics.FromImage(reduced)) {



                    graphics.CompositingMode=CompositingMode.SourceCopy;



                    graphics.InterpolationMode=level.Width>2048?InterpolationMode.Bilinear:InterpolationMode.HighQualityBicubic;



                    graphics.DrawImage(level,0,0,reduced.Width,reduced.Height);



                }



                mapLevels.Add(reduced); level=reduced;



            }



            if(mapLevels.Count>0) {



                using(Graphics g=Graphics.FromImage(fullMap))g.DrawImage(mapLevels[mapLevels.Count-1],0,0,360,360);



            } else {



                using(Graphics g=Graphics.FromImage(fullMap))g.DrawImage(map,0,0,360,360);



            }



            overlay=new OverlayWindow(ShowSettings,ExitApp,factor=> {



                autoZoom=false;



                zoom=Math.Max(1,Math.Min(maxZoom,zoom*factor));



                targetZoom=zoom;



                SettingsChanged();



                lastFrameKey=null;



                terrainKey=null;



                RenderOverlay();



            },ShowZoneSearch);



                        overlay.Size=new Size(savedWidth,savedHeight);

            if(savedLeft>=0 && savedTop>=0) {
                try {
                    Rectangle area=Screen.FromPoint(new Point(savedLeft,savedTop)).WorkingArea;
                    overlay.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-savedWidth,savedLeft)),Math.Max(area.Top,Math.Min(area.Bottom-savedHeight,savedTop)));
                } catch {}
            } else {
                try {
                    Rectangle area=Screen.PrimaryScreen.WorkingArea;
                    const int margin=20;
                    savedLeft=Math.Max(area.Left,area.Right-savedWidth-margin);
                    savedTop=Math.Max(area.Top,area.Top+margin);
                    overlay.Location=new Point(savedLeft,savedTop);
                } catch {}
            }



            overlay.OnFullMapWheel = HandleFullMapWheel;



            overlay.OnFullMapMouseDown = HandleFullMapMouseDown;



            overlay.OnFullMapMouseMove = HandleFullMapMouseMove;
            overlay.OnFullMapMouseLeave = () => UpdateLocationHistoryHover(null);
            overlay.OnFullMapClickThrough = () => {
                draggingMap=false; draggingSlider=false; mouseMoved=true;
                UpdateLocationHistoryHover(null);
                Native.CancelActiveCopy();
                SyncFullMapAdminControls(false,true);
            };
            overlay.OnFullMapClickThroughChanged=enabled=> {
                if(!enabled)fullMapZoomOpacityOverride=false;
                InvalidateFullMapSidebar();lastFrameKey=null;
                if(!closing && !IsDisposed && !overlay.ChangingMapMode)RenderOverlay();
            };
            overlay.VisibleChanged+=(s,e)=> {
                if(overlay.Visible && ToolWindowVisible) { toolMapRestore=true;overlay.Hide();return; }
                AdminControlsMapModeChanged();
            };



            overlay.OnFullMapMouseUp = HandleFullMapMouseUp;



            overlay.OnFullMapAddWaypoint = AddCustomWaypointAtScreenPoint;

            overlay.OnRouteColorRequested = ChangeRouteColor;
            overlay.OnPlayerColorRequested = ChangePlayerColor;



            overlay.CanAddFullMapWaypoint = () => pendingFullMapWaypointMapPoint.HasValue;



            overlay.CanShowFullMapContextMenu = () => fullMapActive && !closing && !IsDisposed && !Native.CopyInProgress;
            overlay.AdminToolsEnabled=()=>adminToolsEnabled;
            overlay.PopulateAdminCommands=PopulateFullMapAdminMenu;
            overlay.PopulateMarkerContextMenu=PopulateAdminMarkerContextMenu;



            // A global low-level hook is not guaranteed to run on this form's UI thread.



            // Marshal it before touching WinForms so a hotkey cannot leave a dialog or



            // overlay in a half-open state after a thread-affinity exception.



            keys=new GameKeys((key,scanCode) => {



                if(closing || IsDisposed || !IsHandleCreated || (shortcutCaptureOpen && !(WpfFieldConsoleRuntime.Active!=null&&WpfFieldConsoleRuntime.Active.IsSettingsToggleKey(key,scanCode)))) {
                    return;
                }



                int bindingRevision=controlBindingRevision;
                long chatRevision=chat.Revision;
                IntPtr origin=Native.GetForegroundWindow();
                try { BeginInvoke((Action)(()=> {
                    TraceShortcutContext("queued",key,scanCode);
                    if(key>0 && key<fallbackKeyDown.Length) fallbackKeyDown[key]=true;
                    if(bindingRevision!=controlBindingRevision || !chat.AllowsQueuedShortcut(chatRevision) || Native.GetForegroundWindow()!=origin || adminCommandBusy) {
                        if(Native.InputTraceActive) Native.TraceInput("shortcut queued rejected vk="+key+" scan="+scanCode+
                            " bindingsChanged="+(bindingRevision!=controlBindingRevision)+" chatChanged="+!chat.AllowsQueuedShortcut(chatRevision)+
                            " focusChanged="+(Native.GetForegroundWindow()!=origin)+" admin="+adminCommandBusy);
                        return;
                    }
                    OnGameKey(key,scanCode);
                })); } catch(InvalidOperationException) { }



            }, IsWatchedPhysicalKey, chat, () => scumChatKey, () => scumCopyModifierKey, () => inventoryInputLocked, () => scumCopyKey, () => scumChatScanCode,
                (key,scan)=>PhysicalKeyCapture.Matches(key,scan,scumMapKey,scumMapScanCode));
            keys.SuppressPhysicalMouse=(message,point,data)=>WpfFieldConsoleRuntime.Active!=null&&WpfFieldConsoleRuntime.Active.SuppressMapMouse(message,point,data);
            keys.OnPhysicalMouse = (message,point) => {
                if(closing || IsDisposed || !IsHandleCreated) return;
                // All engine/UI access belongs on the UI thread, outside the hook.
                try { BeginInvoke(new Action(()=> {
                    if(GameKeys.MouseActionBlocksCopy(message,scumCopyModifierKey))Native.CancelActiveCopy();
                    if(!closing && !IsDisposed && ObserveAdminMapMouse(message,point))CompleteAdminMapClick();
                })); } catch(InvalidOperationException) {}
            };



            try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon=(Icon)SystemIcons.Application.Clone(); }
            ShowInTaskbar=true;
            Resize+=HandleTaskbarRestore;
            tray=new NotifyIcon { Icon=Icon,Text="SCUM MiniMap v"+VersionString+(Program.IsTestBuild?" TEST":""),Visible=true };
            tray.BalloonTipClicked+=(s,e)=>CheckForUpdates(true);



            tray.ContextMenuStrip=trayMenu;



            BuildTrayMenu();



            tray.DoubleClick+=(s,e)=>ShowSettings();



            bar=new SettingsPanel { Dock=DockStyle.Fill,Padding=new Padding(14,8,14,8),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true };

            settingsFooter.Dock=DockStyle.Bottom;
            settingsFooter.Height=74;
            settingsFooter.Padding=new Padding(12,4,12,6);
            settingsFooter.BackColor=OverlayTheme.Surface;

            status.Dock=DockStyle.Top;
            status.Height=32;
            status.Padding=new Padding(2,0,2,0);
            status.Font=OverlayTheme.UiFont(7.8f);
            status.ForeColor=OverlayTheme.InkMuted;
            status.BackColor=OverlayTheme.Surface;

            FlowLayoutPanel footerActions=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=32,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty };
            settingsDoneBtn.Text=Localization.Get("Done");
            settingsDoneBtn.Width=100;
            settingsDoneBtn.Height=28;
            settingsDoneBtn.CausesValidation=false;
            settingsDoneBtn.Click+=(s,e)=>DismissSettings();

            settingsOpenFolderBtn.Text=Localization.Get("OpenDataFolder");
            settingsOpenFolderBtn.Width=140;
            settingsOpenFolderBtn.Height=28;
            settingsOpenFolderBtn.Click+=(s,e)=> {
                try {
                    if(!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
                    Process.Start(new ProcessStartInfo { FileName="explorer.exe",Arguments="\""+dataFolder+"\"",UseShellExecute=true });
                } catch(Exception ex) {
                    RunUiModal(() => MessageBox.Show(UiDialogOwner,Localization.T("DataFolderOpenError",ex.Message)));
                }
            };

            footerActions.Controls.Add(settingsDoneBtn);
            footerActions.Controls.Add(settingsOpenFolderBtn);
            settingsFooter.Controls.Add(footerActions);
            settingsFooter.Controls.Add(status);

            BuildSettingsPanel();

            canvas.Dock=DockStyle.Fill; canvas.BackColor=OverlayTheme.Background; canvas.Paint+=PaintMap;

            Controls.Add(bar); Controls.Add(settingsFooter);



            FieldConsoleSettingsFrame.Compose(this,DismissSettings);



            KeyDown+=(s,e)=> {



                int settingsEventScan=0;
                PhysicalKeyCapture.TryGet((int)e.KeyCode,out settingsEventScan);
                if(PhysicalKeyCapture.Matches((int)e.KeyCode,settingsEventScan,settingsShortcutKey,settingsShortcutScanCode) && e.Modifiers==Keys.None) {



                    if((DateTime.UtcNow - lastHomeAction).TotalMilliseconds > 450) {



                        lastHomeAction = DateTime.UtcNow;



                        ShowSettings();



                        e.Handled=true; e.SuppressKeyPress=true;



                    }



                } else if(e.KeyCode==Keys.Escape) {



                    DismissSettings();



                    e.Handled=true; e.SuppressKeyPress=true;



                }



            };



            overlay.SizeChanged+=(s,e)=> {
                PositionFullMapAdminControls();
                if(overlay.FullMapMode || overlay.ChangingMapMode) return;
                savedWidth=overlay.Width; savedHeight=overlay.Height;



                widthOption.Value=Math.Max(240,Math.Min(800,overlay.Width));



                heightOption.Value=Math.Max(240,Math.Min(800,overlay.Height));



                SettingsChanged();



            };



            overlay.LocationChanged+=(s,e)=> {
                PositionFullMapAdminControls();
                if(overlay.FullMapMode || overlay.ChangingMapMode) return;
                savedLeft=overlay.Left; savedTop=overlay.Top;
                saveAfter=DateTime.UtcNow.AddMilliseconds(700);
            };



            sequence=Native.GetClipboardSequenceNumber();



            try { if(Clipboard.ContainsText()) Accept(Clipboard.GetText()); } catch(ExternalException) {}



            timer.Interval=16; timer.Tick+=Tick; timer.Start();



            System.Threading.ThreadPool.QueueUserWorkItem(_ => { RoadRouter.Instance.InitializeFromResource(); });



            Shown+=(s,e)=> {



                WindowState=FormWindowState.Minimized;



                bool focused=Native.GameFocused() || Native.IsOurWindow(Native.GetForegroundWindow());



                wasGameFocused=focused;



                overlay.SetGameFocus(Native.GameFocused());



                if(!diagnosticMode) { failures=0; next=DateTime.UtcNow; }



                overlay.Show();



                RenderOverlay();



                if(isFirstLaunch && !diagnosticMode && !Program.IsShortcutDiagnosticSession) {

                    OpenStartupGuide();

                }

                if(updateResetPending && !isFirstLaunch && !diagnosticMode && !Program.IsShortcutDiagnosticSession) {
                    DialogResult reset = RunUiModal(() => MessageBox.Show(
                        Localization.Get("UpdateResetBody"),
                        Localization.Get("UpdateResetTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question));
                    if(reset==DialogResult.Yes) {
                        try { if(File.Exists(settingsPath)) File.Delete(settingsPath); } catch(Exception ex) { Program.LogException("Reset update settings",ex); }
                        Application.Restart();
                        return;
                    }
                    updateResetPending=false;
                    SaveSettings();
                }
                if(!diagnosticMode && !suppressCopyKeyReminder && !Program.IsShortcutDiagnosticSession) ShowCopyKeyReminder();



                if(!Program.IsTestBuild && !diagnosticMode && string.IsNullOrEmpty(Program.LocalUpdateSource)) {



                    Task.Delay(3500).ContinueWith(t => {



                        try { BeginInvoke(new Action(() => CheckForUpdates(false))); } catch {}



                    });



                }



            };



            FormClosing+=(s,e)=> {



                if(SettingsVisible && !closing && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; DismissSettings(); return; }



                if(tickBusy) { e.Cancel=true; closing=true; BeginInvoke(new Action(()=> { if(!tickBusy)Close(); })); }



            };



            FormClosed+=(s,e)=>ReleaseResources();



        }



        void BuildTrayMenu() {
            while(trayMenu.Items.Count > 0) {
                ToolStripItem itm = trayMenu.Items[0];
                trayMenu.Items.RemoveAt(0);
                itm.Dispose();
            }

            trayMenu.Items.Add(Localization.Get("Settings"),null,(s,e)=>ShowSettings());
            trayMenu.Items.Add("Admin commands",null,(s,e)=>ShowAdminCommands());
            trayMenu.Items.Add(Localization.Get("FullMap"),null,(s,e)=>TriggerFullMap());
            trayMenu.Items.Add(Localization.Get("SearchPlaceOrGrid"),null,(s,e)=>ShowZoneSearch());
            trayMenu.Items.Add(Localization.Get("TrayCheckUpdates"),null,(s,e)=>CheckForUpdates(true)).Enabled=!Program.IsTestBuild;
            trayMenu.Items.Add(Localization.Get("JoinDiscord"),null,(s,e)=>{ try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName="https://discord.gg/MYzcGaFDMn", UseShellExecute=true }); } catch {} });
            trayMenu.Items.Add(Localization.Get("TrayShowHide"),null,(s,e)=>ToggleOverlay());
            trayMenu.Items.Add(Localization.Get("TrayExit"),null,(s,e)=>ExitApp());

            OverlayTheme.Menu(trayMenu);
        }

        static void DisposeChildControls(Control parent) {
            if(parent==null) return;
            for(int i=parent.Controls.Count-1; i>=0; i--) {
                Control child=parent.Controls[i];
                // Control.Dispose owns its descendants. Removing every nested child
                // individually causes repeated native layout work during a rebuild.
                child.Dispose();
            }
        }

        void BuildSettingsPanel() {
            bar.SuspendLayout();
            DisposeChildControls(bar);
            bar.Controls.Clear();

            // 1. GENERAL & GUIDES
            OverlayTheme.Section(bar, Localization.Get("SecGeneral"));

            FlowLayoutPanel langRow = new FlowLayoutPanel { Width = 365, Height = 32 };
            langRow.Controls.Add(new Label { Text = Localization.Get("Language"), Width = 160, Padding = new Padding(0, 5, 0, 0) });
            TacticalComboBox langCombo = new TacticalComboBox { Width = 160 };
            langCombo.Items.AddRange(Localization.LanguageDisplayNames);
            langCombo.SelectedIndex = Enum.IsDefined(typeof(AppLanguage),Localization.Current) ? (int)Localization.Current : 0;
            langCombo.SelectedIndexChanged += (s, e) => {
                AppLanguage newLang = Localization.LanguageAt(langCombo.SelectedIndex);
                if (Localization.Current != newLang) {
                    // The native combo must finish its selection notification before
                    // rebuilding the panel disposes its window handle.
                    BeginInvoke((MethodInvoker)delegate {
                        if (IsDisposed || Disposing || langCombo.IsDisposed) return;
                        Localization.Current = newLang;
                        Text = Localization.T("HeaderSettings", VersionString+(Program.IsTestBuild?" TEST":""));
                        BuildTrayMenu();
                        BuildSettingsPanel();
                        terrainKey = null;
                        lastFrameKey = null;
                        if (SettingsVisible) canvas.Invalidate();
                        SettingsChanged();
                        RenderOverlay();
                    });
                }
            };
            langRow.Controls.Add(langCombo);
            bar.Controls.Add(langRow);

            Button guideButton = new Button { Text = Localization.Get("OpenStartupGuide"), Width = 280 };
            guideButton.Click += (s, e) => OpenStartupGuide();
            bar.Controls.Add(guideButton);

            Button keyWizardButton = new Button { Text = Localization.Get("KeyRebindingWizard"), Width = 280 };
            keyWizardButton.Click += (s, e) => ShowKeyRebindingWizard();
            bar.Controls.Add(keyWizardButton);
            OverlayTheme.Section(bar, Localization.Get("ShortcutHeading"));
            AddShortcutSettings(bar);
            OverlayTheme.Section(bar, Localization.Get("SecGeneral"));
            var adminToggle=new Panel { Name="AdminToolsToggle",Width=365,Height=32,Cursor=Cursors.Hand };
            adminToggle.Paint+=(s,e)=> { DrawSwitchPill(e.Graphics,new Rectangle(8,9,30,14),adminToolsEnabled,false);TextRenderer.DrawText(e.Graphics,Localization.Get("AdminToolsEnabled"),adminToggle.Font,new Rectangle(48,0,310,32),OverlayTheme.Ink,TextFormatFlags.VerticalCenter); };
            adminToggle.Click+=(s,e)=> { SetAdminToolsEnabled(!adminToolsEnabled);adminToggle.Invalidate(); };
            bar.Controls.Add(adminToggle);

            // 2. GRID & OVERLAY
            OverlayTheme.Section(bar, Localization.Get("SecMapZones"));
            AddCheck(bar, Localization.Get("GridLabels"), gridLabels, value => gridLabels = value);
            AddCheck(bar, Localization.Get("GridBorders"), gridBorders, value => gridBorders = value);
            AddCheck(bar, Localization.Get("HistoryEnabled"), locationHistory, value => { locationHistory=value; terrainKey=null; });
            AddCheck(bar, Localization.Get("HistoryHover"), locationHistoryTimestamps, value => { locationHistoryTimestamps=value; lastFrameKey=null; });
            FlowLayoutPanel historyRow=new FlowLayoutPanel { Width=365,Height=32,WrapContents=false };
            historyRow.Controls.Add(new Label { Text=Localization.Get("HistoryDuration"),Width=210,Padding=new Padding(0,5,0,0) });
            NumericUpDown historyDuration=new NumericUpDown { Minimum=1,Maximum=240,Value=locationHistoryMinutes,Width=90 };
            historyDuration.ValueChanged+=(s,e)=> {
                locationHistoryMinutes=(int)historyDuration.Value;
                PruneLocationHistory(DateTime.UtcNow);
                SettingsChanged();
                lastFrameKey=null;
                RenderOverlay();
            };
            historyRow.Controls.Add(historyDuration);
            bar.Controls.Add(historyRow);
            Button clearHistory=new Button { Text=Localization.Get("HistoryClear"),Width=280 };
            clearHistory.Click+=(s,e)=> { ClearLocationHistory(); RenderOverlay(); };
            bar.Controls.Add(clearHistory);
            FlowLayoutPanel gridRow = new FlowLayoutPanel { Width = 365, Height = 32, WrapContents = false };
            Label gridValue = new Label { Text = Localization.T("GridOpacity", gridOpacity), Width = 150, Padding = new Padding(0, 5, 0, 0) };
            TacticalSlider gridSlider = new TacticalSlider { Minimum = 0, Maximum = 100, Value = gridOpacity, Width = 190, Height = 24, Margin = new Padding(3, 4, 3, 3) };
            gridSlider.ValueChanged += (s, e) => {
                gridOpacity = gridSlider.Value;
                gridValue.Text = Localization.T("GridOpacity", gridOpacity);
                SettingsChanged();
            };
            gridRow.Controls.Add(gridValue);
            gridRow.Controls.Add(gridSlider);
            bar.Controls.Add(gridRow);

            // 3. WAYPOINTS & POI LAYERS
            AddCheck(bar, Localization.Get("ShowCustomWaypoints"), showCustomWaypoints, value => { showCustomWaypoints = value; });
            bar.Controls.Add(new Label { Text = Localization.Get("CustomWaypointsDescription"), Width = 365, Height = 32, AutoSize = false, ForeColor = OverlayTheme.InkMuted, Margin = new Padding(3, -2, 3, 4) });

            FlowLayoutPanel routeColorRow = new FlowLayoutPanel { Width = 365, Height = 34, Margin = new Padding(3, 2, 3, 6) };
            routeColorRow.Controls.Add(new Label { Text = Localization.Get("RouteGuidanceColor"), Width = 230, Padding = new Padding(0, 5, 0, 0) });
            Button routeColorBtn = new Button {
                Width = 90,
                Height = 24,
                BackColor = routeGuidanceColor,
                FlatStyle = FlatStyle.Flat,
                Text = "",
                Cursor = Cursors.Hand,
                Tag = "swatch"
            };
            routeColorBtn.FlatAppearance.BorderColor = OverlayTheme.Border;
            routeColorBtn.FlatAppearance.BorderSize = 1;
            routeColorBtn.Click += (s, e) => {
                ChangeRouteColor();
                routeColorBtn.BackColor = routeGuidanceColor;
            };
            routeColorRow.Controls.Add(routeColorBtn);
            bar.Controls.Add(routeColorRow);

            FlowLayoutPanel playerColorRow = new FlowLayoutPanel { Width = 365, Height = 34, Margin = new Padding(3, 2, 3, 6) };
            playerColorRow.Controls.Add(new Label { Text = Localization.Get("PlayerMarkerColor"), Width = 230, Padding = new Padding(0, 5, 0, 0) });
            Button playerColorBtn = new Button {
                Width = 90,
                Height = 24,
                BackColor = playerConeColor,
                FlatStyle = FlatStyle.Flat,
                Text = "",
                Cursor = Cursors.Hand,
                Tag = "swatch"
            };
            playerColorBtn.FlatAppearance.BorderColor = OverlayTheme.Border;
            playerColorBtn.FlatAppearance.BorderSize = 1;
            playerColorBtn.Click += (s, e) => {
                ChangePlayerColor();
                playerColorBtn.BackColor = playerConeColor;
            };
            playerColorRow.Controls.Add(playerColorBtn);
            bar.Controls.Add(playerColorRow);

            var historyColorRow=new FlowLayoutPanel { Width=365,Height=34,Margin=new Padding(3,2,3,6) };
            historyColorRow.Controls.Add(new Label { Text=Localization.Get("RouteHistoryColor"),Width=230,Padding=new Padding(0,5,0,0) });
            var historyColorBtn=new Button { Name="RouteHistoryColor",Width=90,Height=24,BackColor=routeHistoryColor,FlatStyle=FlatStyle.Flat,Text="",Cursor=Cursors.Hand,Tag="swatch" };
            historyColorBtn.FlatAppearance.BorderColor=OverlayTheme.Border;historyColorBtn.FlatAppearance.BorderSize=1;
            historyColorBtn.Click+=(s,e)=> { ChangeHistoryColor();historyColorBtn.BackColor=routeHistoryColor; };
            historyColorRow.Controls.Add(historyColorBtn);bar.Controls.Add(historyColorRow);

            AddCheck(bar, Localization.Get("ShowScumMap"), showScumMap, value => {
                showScumMap = value;
                if (scumMap != null) scumMap.MasterEnabled = value;
            });
            Button poiFilterButton = new Button { Text = Localization.Get("ConfigurePoiFilters"), Width = 280 };
            poiFilterButton.Click += (s, e) => {
                if (scumMap == null) return;
                using (PoiFilterDialog dlg = new PoiFilterDialog(scumMap, () => {
                    SettingsChanged();
                    if (SettingsVisible) canvas.Invalidate();
                    RenderOverlay();
                })) {
                    RunUiModal(() => dlg.ShowDialog(UiDialogOwner));
                }
            };
            bar.Controls.Add(poiFilterButton);

            AddCheck(bar, Localization.Get("ShowSavedZones"), showZones, value => { showZones = value; });
            bar.Controls.Add(new Label { Text = Localization.Get("CustomZonesDescription"), Width = 365, Height = 42, AutoSize = false, ForeColor = OverlayTheme.InkMuted, Margin = new Padding(3, -2, 3, 4) });
            Button zoneButton = new Button { Text = Localization.Get("MapZonesScreenshot"), Width = 280 };
            zoneButton.Click += (s, e) => OpenZoneImport(null);
            bar.Controls.Add(zoneButton);
            var zoneFilesButton=new Button { Text=Localization.Get("EnableZoneTsv"),Width=280 };
            zoneFilesButton.Click+=(s,e)=> { if(WpfFieldConsoleRuntime.Active!=null)WpfFieldConsoleRuntime.Active.ShowZoneAssociation();else try { ZoneFileOpen.Register(Application.ExecutablePath);RunUiModal(()=>MessageBox.Show(UiDialogOwner,Localization.Get("ZoneAssociationInfo"),Localization.Get("ZoneEditorTitle"))); }catch(Exception error) { Program.LogException("ZoneFileAssociation",error); } };bar.Controls.Add(zoneFilesButton);
            Button clearZonesBtn = new Button { Text = Localization.Get("ClearAllCustomZones"), Width = 280 };
            clearZonesBtn.Click += (s, e) => {
                if (zones == null || zones.Count == 0) {
                    RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("NoCustomZonesToDelete"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                    return;
                }
                if (RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("ClearAllZonesConfirm", zones.Count), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning)) == DialogResult.Yes) {
                    if(!TryCommitZones(new List<MapZone>(),this)) return;
                    searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null;
                    SettingsChanged();
                    RenderOverlay();
                    RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("AllZonesDeletedDone"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                }
            };
            bar.Controls.Add(clearZonesBtn);

            AddCheck(bar, Localization.Get("ShowZoneLabels"), showZoneLabels, value => { showZoneLabels = value; });
            AddCheck(bar, Localization.Get("SmartLabelLod"), smartLabelLod, value => { smartLabelLod = value; });
            AddNumber(bar, Localization.Get("ZoneLabelSize"), 6, 24, labelSize, value => labelSize = value);

            // 4. DISPLAY & HUD LAYOUT
            OverlayTheme.Section(bar, Localization.Get("SecAppearance"));
            widthOption = AddNumber(bar, Localization.Get("MapWidth"), 240, 800, overlay.MinimapBounds.Width, value => overlay.SetMinimapSize(new Size(value,overlay.MinimapBounds.Height)));
            heightOption = AddNumber(bar, Localization.Get("MapHeight"), 240, 800, overlay.MinimapBounds.Height, value => overlay.SetMinimapSize(new Size(overlay.MinimapBounds.Width,value)));
            AddNumber(bar, Localization.Get("MapOpacity"), 30, 100, mapOpacity, value => mapOpacity = value);

            FlowLayoutPanel posRow = new FlowLayoutPanel { Width = 365, Height = 32 };
            posRow.Controls.Add(new Label { Text = Localization.Get("LocationBarPosition"), Width = 160, Padding = new Padding(0, 5, 0, 0) });
            TacticalComboBox posCombo = new TacticalComboBox { Width = 130 };
            posCombo.Items.AddRange(new object[] { Localization.Get("PosBelow"), Localization.Get("PosAbove") });
            posCombo.SelectedIndex = statusPos == "Above" ? 1 : 0;
            posCombo.SelectedIndexChanged += (s, e) => {
                statusPos = posCombo.SelectedIndex == 1 ? "Above" : "Below";
                SettingsChanged();
            };
            posRow.Controls.Add(posCombo);
            bar.Controls.Add(posRow);

            AddCheck(bar, Localization.Get("ShowStatus"), showStatus, value => showStatus = value);

            AddCheck(bar, Localization.Get("ShowHeading"), showHeading, value => showHeading = value);
            AddCheck(bar, Localization.Get("ShowCompass"), showCompass, value => showCompass = value);
            AddCheck(bar, Localization.Get("ZoneChime"), zoneChime, value => zoneChime = value);

            BuildVoiceSettings(bar);

            // 5. GPS TRACKING & AUTO-ZOOM
            OverlayTheme.Section(bar, Localization.Get("SecTrackingZoom"));
            AddCheck(bar, Localization.Get("AutoZoomSpeed"), autoZoom, value => { autoZoom = value; });
            NumericUpDown autoMinOption = null, autoMaxOption = null;
            autoMinOption = AddNumber(bar, Localization.Get("AutoZoomMin"), 1, 32, autoZoomMin, value => {
                autoZoomMin = value;
                if (autoMaxOption != null && autoMaxOption.Value < value) autoMaxOption.Value = value;
            });
            autoMaxOption = AddNumber(bar, Localization.Get("AutoZoomMax"), 1, 32, autoZoomMax, value => {
                autoZoomMax = value;
                if (autoMinOption.Value > value) autoMinOption.Value = value;
            });
            AddNumber(bar, Localization.Get("MaxZoom"), 4, 32, maxZoom, value => { maxZoom = value; zoom = Math.Min(maxZoom, zoom); targetZoom = Math.Min(maxZoom, targetZoom); });
            AddNumber(bar, Localization.Get("ZoomStep"), 10, 100, zoomStepPercent, value => { zoomStepPercent = value; });
            AddNumber(bar, Localization.Get("PositionInterval"), Program.TrackingIntervalMs(0,scumCopyModifierKey), 10000, Program.TrackingIntervalMs(copyIntervalMs,scumCopyModifierKey), value => { copyIntervalMs = value; next = DateTime.UtcNow; }).Increment = 50;
            bar.Controls.Add(new Label { Text=Program.CopyBindingHelp,Width=365,Height=100 });

            // 6. TOOLS & DATA
            OverlayTheme.Section(bar, Localization.Get("SecToolsShortcuts"));
            Button resetMapBtn = new Button { Text = Localization.Get("ResetDefaultMap"), Width = 280 };
            resetMapBtn.Click += (s, e) => {
                if (RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("ResetDefaultMapConfirm"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question)) == DialogResult.Yes) {
                    try {
                        string customMap = Path.Combine(dataFolder, "map.png");
                        if (File.Exists(customMap)) File.Delete(customMap);
                        RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("ResetDefaultMapDone"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                    } catch (Exception ex) {
                        RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("MapResetFailed", ex.Message), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                    }
                }
            };
            bar.Controls.Add(resetMapBtn);

            Button updateBtn = new Button { Text = Localization.Get("CheckUpdatesGitHub"), Width = 280, Enabled = !Program.IsTestBuild };
            updateBtn.Click += (s, e) => CheckForUpdates(true);
            bar.Controls.Add(updateBtn);

            Button discordBtn = new Button { Text = Localization.Get("JoinDiscord"), Width = 280 };
            discordBtn.Click += (s, e) => {
                try {
                    Process.Start(new ProcessStartInfo("https://discord.gg/MYzcGaFDMn") { UseShellExecute = true });
                } catch {}
            };
            bar.Controls.Add(discordBtn);

            BuildFieldConsoleSettingsTabs();
            OverlayTheme.Style(bar);
            if(settingsFooter != null) {
                settingsDoneBtn.Text = Localization.Get("Done");
                settingsOpenFolderBtn.Text = Localization.Get("OpenDataFolder");
                OverlayTheme.Style(settingsFooter);
                settingsDoneBtn.BackColor=OverlayTheme.Accent;
                settingsDoneBtn.ForeColor=OverlayTheme.Background;
            }
            bar.ResumeLayout(true);
        }



        public const string VersionString = "2.3.23";



        public static readonly Version CurrentVersion = new Version(VersionString + ".0");



        bool updateCheckRunning;
        readonly UpdateCheckSchedule updateCheckSchedule=new UpdateCheckSchedule(DateTime.UtcNow);
        string notifiedUpdateVersion;



        static void RestartWithVerifiedUpdate(string staged,string installed) {
            string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScumMiniMap");
            Directory.CreateDirectory(profile);
            UpdateInstaller.Start(staged,installed,Path.Combine(profile,"update-result.txt"));
        }

        async Task DownloadAndInstallUpdate(UpdateService service,UpdateRelease release) {



            string installed=Application.ExecutablePath;



            if(string.IsNullOrEmpty(installed) || !File.Exists(installed)) throw new FileNotFoundException("Installed executable was not found.",installed);



            string staged=installed+"."+Guid.NewGuid().ToString("N")+".update.exe";



            note=Localization.Get("UpdateDownloading");



            try {



                await Task.Run(()=>service.Download(release,staged));



                if(closing || IsDisposed) { UpdatePackage.Cleanup(staged); return; }
                await Task.Run(()=>RestartWithVerifiedUpdate(staged,installed));



            } catch {



                try { UpdatePackage.Cleanup(staged); } catch { }



                throw;



            }



        }



        public async void RunLocalUpdateTest(string sourceFolder) {



            if(updateCheckRunning || closing || IsDisposed) return;



            updateCheckRunning=true;



            try {



                string folder=Path.GetFullPath(sourceFolder ?? "");



                string manifest=Path.Combine(folder,"update.txt");



                string candidate=Path.Combine(folder,"SkynettMiniMap.exe");



                if(!File.Exists(manifest) || !File.Exists(candidate))



                    throw new FileNotFoundException("Local update test requires update.txt and SkynettMiniMap.exe in the selected folder.");



                // This transport intentionally ignores its GitHub-shaped URI and reads only



                // the two explicit local test files. It exercises the production parser,



                // checksum verifier, staging, replacement, and restart paths offline.



                var service=new UpdateService(UpdateService.Repository,Path.Combine(dataFolder,"local-update-test-"+Guid.NewGuid().ToString("N")),



                    (uri,limit)=> {



                        string path=uri.AbsolutePath.EndsWith("update.txt",StringComparison.OrdinalIgnoreCase)?manifest:candidate;



                        byte[] bytes=File.ReadAllBytes(path);



                        if(bytes.Length>limit) throw new InvalidDataException("Local update test file exceeds the size limit.");



                        return bytes;



                    });



                UpdateRelease release=await Task.Run(()=>service.Check(true));



                if(release.Version<=CurrentVersion) throw new InvalidDataException("The local update manifest must specify a version newer than v"+VersionString+".");



                if(RunUiModal(() => MessageBox.Show(UiDialogOwner,Localization.T("UpdateAvailableDialogMsg",VersionString,release.VersionText,release.Filename),



                    "Local Update Test",MessageBoxButtons.YesNo,MessageBoxIcon.Information))!=DialogResult.Yes) return;



                await DownloadAndInstallUpdate(service,release);



                if(closing || IsDisposed) return;



                note=Localization.Get("UpdateReadyRestarting");



                closing=true;



                Close();



            } catch(Exception ex) {



                if(!closing && !IsDisposed) RunUiModal(() => MessageBox.Show(UiDialogOwner,"Local update test failed: "+ex.Message,"Local Update Test",MessageBoxButtons.OK,MessageBoxIcon.Warning));



            } finally { updateCheckRunning=false; }



        }



        async void CheckForUpdates(bool userInitiated,bool periodic=false) {



            if(Program.IsTestBuild || updateCheckRunning || closing || IsDisposed || diagnosticMode) return;



            updateCheckRunning = true;
            updateCheckSchedule.RecordCheck(DateTime.UtcNow);



            try {



                var service = new UpdateService(UpdateService.Repository,



                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScumMiniMap", "updates"), null);



                UpdateRelease release = await Task.Run(() => service.Check(userInitiated));



                if(closing || IsDisposed) return;



                bool updateAvailable = release.Version > CurrentVersion;
                if(updateAvailable) {



                    note = Localization.T("UpdateAvailableNote", release.VersionText, PhysicalKeyCapture.KeyName(settingsShortcutKey));
                    if(periodic) {
                        if(notifiedUpdateVersion!=release.VersionText) {
                            notifiedUpdateVersion=release.VersionText;
                            tray.ShowBalloonTip(5000,Localization.Get("UpdateAvailableDialogTitle"),note,ToolTipIcon.Info);
                        }
                        return;
                    }
                    notifiedUpdateVersion=release.VersionText;



                    if(RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("UpdateAvailableDialogMsg", VersionString, release.VersionText, release.Filename),



                        Localization.Get("UpdateAvailableDialogTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information)) != DialogResult.Yes) return;



                    await DownloadAndInstallUpdate(service,release);



                    if(closing || IsDisposed) return;



                    note=Localization.Get("UpdateDownloadComplete");



                    note=Localization.Get("UpdateReadyRestarting");



                    closing=true;



                    Close();



                } else if(userInitiated) {



                    RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("UpdateUpToDateMsg", VersionString, release.VersionText), Localization.Get("UpdateUpToDateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information));



                }



            } catch(UpdateRetryException ex) {



                if(userInitiated && !closing && !IsDisposed) RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("UpdateRateLimited",ex.RetryAt.ToLocalTime().ToString("g")), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning));



            } catch(System.Net.WebException) {



                if(userInitiated && !closing && !IsDisposed) RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("UpdateConnectError"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning));



            } catch(FormatException) {



                if(userInitiated && !closing && !IsDisposed) RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.Get("UpdateVersionUnknown"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning));



            } catch(Exception ex) {



                if(userInitiated && !closing && !IsDisposed) RunUiModal(() => MessageBox.Show(UiDialogOwner, Localization.T("UpdateFailedMsg", ex.Message), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning));



            } finally { updateCheckRunning = false; }



        }



        bool resourcesReleased;



        void ReleaseResources() {



            if(resourcesReleased)return; resourcesReleased=true; routeGeneration++;



            timer.Stop(); timer.Dispose();



            if(keys!=null)keys.Dispose();
            if(voicePlayer!=null)voicePlayer.Dispose();



            if(tray!=null) { tray.Visible=false; tray.Dispose(); }



            if(overlay!=null)overlay.Dispose();



            overlayFrame.Dispose(); fullMap.Dispose();



            if(fullMapSidebarFrame!=null)fullMapSidebarFrame.Dispose();



            foreach(Bitmap cachedLevel in mapLevels)cachedLevel.Dispose();
            if(mapTiles!=null)mapTiles.Dispose();



            if(map!=null)map.Dispose();
            if(cachedConeBrush!=null) { try { cachedConeBrush.Dispose(); } catch {} cachedConeBrush=null; }
            if(locationHistoryPath!=null) { locationHistoryPath.Dispose(); locationHistoryPath=null; }



        }



        protected override void Dispose(bool disposing) {



            if(disposing)ReleaseResources();



            base.Dispose(disposing);



        }



        void ExitApp() { if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.RequestExit();return; }closing=true; SaveSettings(); Close(); }





        T RunUiModal<T>(Func<T> showDialog) {
            Form activeOwner=Form.ActiveForm;
            bool previousPanelOpening=panelOpening, previousCapture=shortcutCaptureOpen;
            bool resumeTimer=timer.Enabled, restoreOverlay=overlay.Visible;
            panelOpening=true;
            shortcutCaptureOpen=true;
            timer.Stop();
            try {
                Native.CancelActiveCopy();
                PrepareUiInput();
                overlay.Hide();
                return showDialog();
            }
            finally {
                panelOpening=previousPanelOpening;
                shortcutCaptureOpen=previousCapture;
                fallbackKeysArmed=false;
                if(!closing && !IsDisposed) {
                    if(restoreOverlay)overlay.Show();
                    if(activeOwner!=null && !activeOwner.IsDisposed && activeOwner.Visible && activeOwner.Enabled
                        && !diagnosticMode && Native.IsOurWindow(Native.GetForegroundWindow()))activeOwner.BringToFront();
                    if(resumeTimer)timer.Start();
                }
            }
        }

        void PrepareUiInput() {
            adminControlsCommandReturn=false;
            Native.CancelActiveCopy();
            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;
            if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)adminCommandsPanel.Hide();
        }

        IWin32Window UiDialogOwner { get { return SettingsVisible && IsHandleCreated ? (IWin32Window)this : null; } }

        bool ToolWindowVisible {
            get { return (spawnCartDialog!=null && !spawnCartDialog.IsDisposed && spawnCartDialog.Visible)
                || (adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed && adminPlayersPanel.Visible)
                || adminProfileWindows.Any(profile=>!profile.IsDisposed && profile.Visible); }
        }

        bool toolMapRestore;
        int adminPlayerToolOpening;
        bool IntensiveWorkPaused {
            get { return musicalScumbagOpen || AdminPlayerWorkPaused; }
        }
        bool AdminPlayerWorkPaused {
            get { return adminPlayerToolOpening>0
                || (adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed && adminPlayersPanel.Visible)
                || adminProfileWindows.Any(profile=>!profile.IsDisposed && profile.Visible); }
        }
        internal static bool CanRestoreAdminTool(IntPtr foreground,IntPtr game,bool ownWindow) {
            return ownWindow || (game!=IntPtr.Zero && foreground==game);
        }
        void RestoreAdminTool(Form tool,IntPtr game) {
            if(closing || IsDisposed || tool==null || tool.IsDisposed || !tool.Enabled)return;
            if(playerToolsAwaitingReopen && IsPlayerTool(tool))return;
            IntPtr foreground=Native.GetForegroundWindow();
            if(!diagnosticMode && !CanRestoreAdminTool(foreground,game,Native.IsOurWindow(foreground)))return;
            Native.ReleaseCapture();Cursor.Clip=Rectangle.Empty;
            tool.WindowState=FormWindowState.Normal;tool.TopMost=true;
            tool.Show();tool.BringToFront();
            if(!diagnosticMode) { tool.Activate();Native.ForceForeground(tool.Handle); }
        }
        void ProtectToolWindow(Form tool) {
            Action update=()=> {
                UpdateIntensiveWorkPause();
                if(ToolWindowVisible) {
                    toolMapRestore=toolMapRestore || overlay.Visible;
                    PrepareUiInput();overlay.Hide();
                } else if(toolMapRestore && !adminCommandBusy && !closing && !overlay.IsDisposed) {
                    toolMapRestore=false;
                    overlay.Show();
                }
            };
            tool.VisibleChanged+=(s,e)=>update();
            tool.FormClosed+=(s,e)=>update();
            tool.Disposed+=(s,e)=>update();
        }

        public void OpenStartupGuide(bool bindingsOnly=false) {
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => OpenStartupGuide(bindingsOnly)));
                return;
            }
            try {
                IWin32Window owner = (SettingsVisible && IsHandleCreated) ? (IWin32Window)this : null;
                using(StartupGuideDialog guide = new StartupGuideDialog(
                    scumMapKey, scumChatKey, scumCopyModifierKey, scumCopyKey,
                    (mKey, cKey, modKey, cpKey, scanCode, mapScanCode, chatScanCode) => {
                        scumMapKey = mKey;
                        scumChatKey = cKey;
                        scumMapScanCode = mapScanCode;
                        scumChatScanCode = chatScanCode;
                        scumCopyModifierKey = modKey;
                        scumCopyKey = cpKey;
                        scumCopyScanCode = scanCode;
                        SettingsChanged();
                    },
                    () => {
                        SettingsChanged();
                        if(SettingsVisible) canvas.Invalidate();
                        RenderOverlay();
                    },scumCopyScanCode,scumMapScanCode,scumChatScanCode,settingsShortcutKey,pinShortcutKey,searchShortcutKey,settingsShortcutScanCode,pinShortcutScanCode,searchShortcutScanCode,CurrentAppControls(),ApplyAppControls)) {
                    if(owner == null) guide.StartPosition = FormStartPosition.CenterScreen;
                    if(bindingsOnly) guide.ShowKeyBindings();
                    Native.CancelActiveCopy();
                    RunUiModal(() => guide.ShowDialog(owner));
                    BuildSettingsPanel();
                    SaveSettings();
                    if(guide.LayoutRequested) {
                        SetFullMap(false);
                        layoutSettingsRequested=true;
                        ShowSettings();
                    }
                }
            } catch(Exception ex) {
                Program.LogException("StartupGuide", ex);
            }
        }

        void ShowCopyKeyReminder() {
            using(var reminder=new CopyKeyReminderDialog()) {
                if(RunUiModal(() => reminder.ShowDialog())==DialogResult.OK && reminder.DoNotShowAgain) {
                    suppressCopyKeyReminder=true;
                    SaveSettings();
                }
            }
        }

        public void OpenZoneImport(string file) {
            if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.ShowZoneWizard(file);return; }
            if(zonesLoadFailed) { if(!diagnosticMode) RunUiModal(() => MessageBox.Show(Localization.Get("NoteZonesLoadFail"))); return; }
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => OpenZoneImport(file)));
                return;
            }

            bool visible = overlay != null && overlay.Visible;
            if(visible) {
                Native.ReleaseCapture();
                overlay.Hide();
            }

            IWin32Window owner = (SettingsVisible && IsHandleCreated) ? (IWin32Window)this : null;
            try {
                List<MapZone> existingWaypoints=zones.Where(IsCustomWaypointZone).ToList();
                List<MapZone> editableZones=zones.Where(zone=>!IsCustomWaypointZone(zone)).ToList();
                using(ZoneEditor editor=new ZoneEditor(map,editableZones,zonesPath,value=> {
                    zones=value.Concat(existingWaypoints).ToList();
                    try { if(File.Exists(zonesPath)) lastZonesFileWriteTimeUtc=File.GetLastWriteTimeUtc(zonesPath); } catch {}
                    SettingsChanged();
                },existingWaypoints)) {
                    if(!string.IsNullOrEmpty(file))editor.Shown+=async(s,e)=>await editor.ImportAutomatic(file);
                    OverlayTheme.Frame(editor,Localization.Get("HeaderMapZones"),()=>editor.Close());
                    if(owner == null) editor.StartPosition = FormStartPosition.CenterScreen;
                    RunUiModal(() => editor.ShowDialog(owner));
                }
            } catch(Exception ex) {
                Program.LogException("ZoneEditor", ex);
                RunUiModal(() => MessageBox.Show(owner,Localization.T("ZoneEditorOpenError",ex.Message),Localization.Get("ZoneEditorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Error));
            } finally {
                if(visible && overlay != null && !overlay.IsDisposed) {
                    overlay.Show();
                    RenderOverlay();
                }
            }
        }



        public void CheckAutomaticImport(string file,string output) {



            Exception failure=null;



            using(ZoneEditor editor=new ZoneEditor(map,new List<MapZone>(),zonesPath,value=>{})) {



                editor.Shown+=async(s,e)=> {



                    try { await editor.ImportAutomatic(file);editor.PreviewAutomatic(output); }



                    catch(Exception ex) { failure=ex; }finally { editor.Close(); }



                };



                RunUiModal(() => editor.ShowDialog());



            }



            if(failure!=null)throw failure;



        }















        void ShowKeyRebindingWizard() {
            OpenStartupGuide(true);
        }



        void ToggleOverlay() {
            if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.Handle(AppControlAction.ToggleOverlay);return; }



            if(overlay.Visible) {



                overlay.Hide();



            } else {



                overlay.SetGameFocus(Native.GameFocused());



                overlay.Show();



                lastFrameKey=null;



                terrainKey=null;



                hiddenByFocusLoss=false;



                RenderOverlay();



            }



        }



        static MapZone GridTarget(string query) {



            query=Regex.Replace(query??"",@"\b(grid|sector|square)\b","",RegexOptions.IgnoreCase);



            string grid=Regex.Replace((query??"").Trim().ToUpperInvariant(), @"\s+", "");



            if(!Regex.IsMatch(grid,@"^[DCBAZ][0-4]$")) return null;



            int row="DCBAZ".IndexOf(grid[0]), col=4-(grid[1]-'0');



            double x=617505-(col+0.5)*304132, y=617953-(row+0.5)*304356;



            return new MapZone { Name="Grid "+grid, Points=new PointF[]{ToMap(new Position { X=x,Y=y })} };



        }



        static List<MapZone> FindDestinations(List<MapZone> places,string query) {



            List<MapZone> matches=DestinationSearch.Find(places,query,null);



            MapZone grid=GridTarget(query); if(grid!=null) matches.Insert(0,grid);



            return matches;



        }



        bool searchOpen;



        async void ShowZoneSearch() {



            if(searchOpen || panelOpening) return;



            searchOpen=true;



            GameFocusReturn searchFocus=new GameFocusReturn(); searchFocus.Capture();



            IntPtr origin=Native.GetForegroundWindow();



            try {



                while(Native.CopyInProgress) await Task.Delay(5);



                if(closing || IsDisposed) return;

                Native.ReleaseCapture();
                Cursor.Clip=Rectangle.Empty;

                using(Form dlg=new Form()) {



                    dlg.Text=Localization.Get("SearchTitle"); dlg.FormBorderStyle=FormBorderStyle.FixedToolWindow;



                    dlg.StartPosition=FormStartPosition.Manual; dlg.TopMost=true; dlg.ShowInTaskbar=true;



                    dlg.ClientSize=new Size(520,460);



                    dlg.BackColor=OverlayTheme.Background; dlg.ForeColor=OverlayTheme.Ink;



                    dlg.Font=OverlayTheme.UiFont(9);



                    Rectangle area=Screen.FromControl(overlay).WorkingArea;



                    dlg.Location=new Point(Math.Max(area.Left,Math.Min(overlay.Left,area.Right-dlg.Width)),



                        Math.Max(area.Top,Math.Min(overlay.Top+20,area.Bottom-dlg.Height)));



                    Label hint=new Label { Dock=DockStyle.Top,Height=32,Text=Localization.Get("SearchHint") };



                    TextBox search=new TextBox { MaxLength=120,Dock=DockStyle.Top,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink };



                    ListBox list=new ListBox { Dock=DockStyle.Fill,BackColor=OverlayTheme.Background,ForeColor=OverlayTheme.Ink,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48 };



                    Label status=new Label { Dock=DockStyle.Bottom,Height=30 };



                    FlowLayoutPanel buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=54,Padding=new Padding(8),FlowDirection=FlowDirection.RightToLeft,WrapContents=false };



                    Button go=new Button { Text=Localization.Get("SetWaypoint"),Width=125 };



                    Button clear=new Button { Text=Localization.Get("ClearWaypoint"),Width=135,Enabled=searchTarget!=null };



                    Button cancel=new Button { Text=Localization.Get("Cancel"),Width=80,DialogResult=DialogResult.Cancel };



                    OverlayTheme.Style(dlg);
            go.BackColor=OverlayTheme.Accent; go.ForeColor=Color.FromArgb(14,17,20); go.Font=OverlayTheme.UiFont(9f, FontStyle.Bold);
            buttons.Controls.AddRange(new Control[]{go,clear,cancel});



                    List<MapZone> matches=new List<MapZone>();



                    MapZone selected=null;



                    list.DrawItem+=(s,e)=> {



                        if(e.Index<0 || e.Index>=matches.Count)return;



                        MapZone result=matches[e.Index];



                        bool active=(e.State&DrawItemState.Selected)!=0;



                        using(Brush fill=new SolidBrush(active?OverlayTheme.SurfaceCard:OverlayTheme.Surface)) e.Graphics.FillRectangle(fill,e.Bounds);



                        Rectangle title=new Rectangle(e.Bounds.Left+12,e.Bounds.Top+5,e.Bounds.Width-24,20);



                        Rectangle detail=new Rectangle(title.Left,e.Bounds.Top+27,title.Width,17);



                        TextRenderer.DrawText(e.Graphics,result.DisplayName,list.Font,title,active?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);



                        string catLabel=!string.IsNullOrEmpty(result.Subtitle)?Localization.GetCategoryName(result.Subtitle):(result.Name.StartsWith("Grid ",StringComparison.Ordinal)?Localization.Get("SectorCentre"):(result.IsFaction?Localization.Get("FactionPOI"):Localization.Get("Place")));



                        string description=DestinationSearch.Sector(result.Centroid)+" / "+catLabel;



                        if(position!=null) description+=" / "+DestinationSearch.Distance(DestinationSearch.Metres(result.Centroid,ToMap(position)));



                        TextRenderer.DrawText(e.Graphics,description,list.Font,detail,OverlayTheme.InkMuted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);



                        e.DrawFocusRectangle();



                    };



                    Action populate=()=> {



                        matches=DestinationSearch.Find(zones,search.Text,position==null?(PointF?)null:ToMap(position));



                        if(scumMap!=null) {



                            List<MapZone> scumMatches = DestinationSearch.FindScumMap(scumMap, search.Text, position==null?(PointF?)null:ToMap(position), 35);



                            foreach(MapZone sz in scumMatches) {



                                bool duplicate = false;



                                for(int mi = 0; mi < matches.Count; mi++) {



                                    if(string.Equals(matches[mi].Name, sz.Name, StringComparison.OrdinalIgnoreCase)) {



                                        duplicate = true;



                                        break;



                                    }



                                }



                                if(!duplicate) matches.Add(sz);



                            }



                        }



                        bool factionOnly=Regex.IsMatch(search.Text??"",@"\bfactions?\b",RegexOptions.IgnoreCase);



                        if(!factionOnly) { MapZone grid=GridTarget(search.Text); if(grid!=null) matches.Insert(0,grid); }



                        list.BeginUpdate(); list.Items.Clear();



                        foreach(MapZone zone in matches) {



                            PointF point=zone.Centroid;



                            string displayName = zone.DisplayName;



                            string catLabel=!string.IsNullOrEmpty(zone.Subtitle)?Localization.GetCategoryName(zone.Subtitle):(zone.Name.StartsWith("Grid ",StringComparison.Ordinal)?Localization.Get("SectorCentre"):(zone.IsFaction?Localization.Get("FactionPOI"):Localization.Get("Place")));



                            list.Items.Add(displayName+"  /  "+DestinationSearch.Sector(point)+"  /  "+catLabel+(position==null?"":"  /  "+DestinationSearch.Distance(DestinationSearch.Metres(point,ToMap(position)))));



                        }



                        if(matches.Count>0) list.SelectedIndex=0;



                        list.EndUpdate(); go.Enabled=matches.Count>0;



                        status.Text=matches.Count==0?Localization.Get("NoMatches"):Localization.T("SearchResults",matches.Count);



                    };



                    Action choose=()=> { if(list.SelectedIndex>=0) { selected=matches[list.SelectedIndex]; dlg.DialogResult=DialogResult.OK; } };



                    ContextMenuStrip listMenu=new ContextMenuStrip();



                    ToolStripMenuItem deleteWp=new ToolStripMenuItem(Localization.Get("DeleteWaypoint"),null,(s,e)=> {



                        if(list.SelectedIndex>=0 && list.SelectedIndex<matches.Count) {



                            MapZone z=matches[list.SelectedIndex];



                            if(z.Category==ZoneCategory.Custom) {



                                string displayName=z.DisplayName;



                                if(ConfirmWaypointRemoval(displayName,dlg)) {



                                    var candidate=new List<MapZone>(zones); candidate.Remove(z);
                                    if(!TryCommitZones(candidate,dlg)) return;



                                    if(searchTarget==z) { searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null; }







                                    populate();



                                    SettingsChanged();



                                    RenderOverlay();



                                }



                            }



                        }



                    });



                    listMenu.Items.Add(deleteWp);



                    list.MouseDown+=(s,e)=> {



                        if(e.Button==MouseButtons.Right) {



                            int idx=list.IndexFromPoint(e.Location);



                            if(idx>=0 && idx<matches.Count) {



                                list.SelectedIndex=idx;



                                MapZone z=matches[idx];



                                if(z.Category==ZoneCategory.Custom) {



                                    deleteWp.Text=Localization.T("DeleteWaypointNamed",z.DisplayName);



                                    listMenu.Show(list,e.Location);



                                }



                            }



                        }



                    };



                    list.KeyDown+=(s,e)=> {



                        if(e.KeyCode==Keys.Delete && list.SelectedIndex>=0 && list.SelectedIndex<matches.Count && matches[list.SelectedIndex].Category==ZoneCategory.Custom) {



                            deleteWp.PerformClick();



                            e.Handled=true;



                        }



                    };



                    search.TextChanged+=(s,e)=>populate();



                    go.Click+=(s,e)=>choose(); list.DoubleClick+=(s,e)=>choose();



                    clear.Click+=(s,e)=> { ClearActiveWaypoint(); dlg.DialogResult=DialogResult.Cancel; };



                    search.KeyDown+=(s,e)=> {



                        if((e.KeyCode==Keys.Down || e.KeyCode==Keys.Up) && list.Items.Count>0) {



                            list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(e.KeyCode==Keys.Down?1:-1)));



                            e.Handled=true; e.SuppressKeyPress=true;



                        }



                    };



                    dlg.AcceptButton=go; dlg.CancelButton=cancel;



                    dlg.Controls.Add(list); dlg.Controls.Add(search); dlg.Controls.Add(hint); dlg.Controls.Add(status); dlg.Controls.Add(buttons);



                    FieldConsoleSearchView.Compose(dlg,search,hint,list,status,buttons,go,clear,cancel);
                    go.BackColor=OverlayTheme.Accent; go.ForeColor=Color.FromArgb(14,17,20); go.FlatAppearance.BorderColor=OverlayTheme.AccentHover; go.Font=OverlayTheme.UiFont(9f, FontStyle.Bold);



                    OverlayTheme.Anchor(dlg,overlay);



                    bool dismissing=false;



                    dlg.FormClosing+=(s,e)=> { dismissing=true; searchFocus.Restore(); };



                    populate();



                    dlg.Shown+=(s,e)=> {



                        // Defer until the modal window has completed its initial layout/activation.



                        dlg.BeginInvoke(new Action(()=> {



                            if(diagnosticMode || dlg.IsDisposed || !dlg.Visible || dismissing) return;



                            IntPtr foreground=Native.GetForegroundWindow();



                            // Do not undo an Alt-Tab that happened while the panel was opening.



                            if(foreground!=origin && foreground!=dlg.Handle) return;



                            Native.SetForegroundWindow(dlg.Handle);



                            if(Native.GetForegroundWindow()!=dlg.Handle) return;



                            dlg.Activate(); dlg.ActiveControl=search; search.Focus();



                            // Explicit search opening should put both typing and mouse input in the box.



                            Cursor.Position=search.PointToScreen(new Point(search.ClientSize.Width/2,search.ClientSize.Height/2));



                            search.Cursor=Cursors.IBeam;



                        }));



                    };



                    if(RunUiModal(() => dlg.ShowDialog(SettingsVisible?this:null))==DialogResult.OK && selected!=null) {



                        searchTarget=selected;



                        note=Localization.T("NoteWaypointSet", selected.DisplayName, PhysicalKeyCapture.KeyName(searchShortcutKey));



                        UpdateRouteAsync(position!=null?ToMap(position):new PointF(.5f,.5f), selected);



                        SettingsChanged();



                    }



                }



            } finally { searchOpen=false; resumeAfter=DateTime.UtcNow.AddMilliseconds(400); }



        }



        DateTime lastHomeAction = DateTime.MinValue;



        DateTime lastDeleteAction = DateTime.MinValue;



        DateTime lastEndAction = DateTime.MinValue;



        DateTime lastInsertAction = DateTime.MinValue;



        DateTime lastPgUpAction = DateTime.MinValue;



        DateTime lastPgDnAction = DateTime.MinValue;



        void FocusSettingsShortcut() {



            if((DateTime.UtcNow - lastHomeAction).TotalMilliseconds < 450) return;



            lastHomeAction = DateTime.UtcNow;



            ShowSettings();



        }



        void TriggerZoneSearch() {



            if((DateTime.UtcNow - lastDeleteAction).TotalMilliseconds < 450) return;



            lastDeleteAction = DateTime.UtcNow;



            ShowZoneSearch();



        }



        void TriggerToggleOverlay() {



            if((DateTime.UtcNow - lastEndAction).TotalMilliseconds < 450) return;



            lastEndAction = DateTime.UtcNow;



            ToggleOverlay();



        }



        DateTime lastDeathMarkerAction;
        bool DeathMarkerKeyAvailable() {
            var bindings=CurrentAppControls();
            return bindings.Key(AppControlAction.DeathMarker)!=0
                && bindings.BindingError(scumMapKey,scumMapScanCode,scumChatKey,scumChatScanCode,scumCopyKey,scumCopyScanCode)==null;
        }
        void TriggerDeathMarker() {
            if(IsHandleCreated && InvokeRequired) { BeginInvoke(new Action(TriggerDeathMarker)); return; }
            if(!HotkeyContextAllowed() || adminCommandBusy || !DeathMarkerKeyAvailable()) return;
            if((DateTime.UtcNow-lastDeathMarkerAction).TotalMilliseconds<450) return;
            lastDeathMarkerAction=DateTime.UtcNow;
            if(position!=null) SaveDeathMarker(position,false);
        }
        bool SaveDeathMarker(Position deathPosition,bool automatic) {
            PointF point=ToMap(deathPosition);
            if(float.IsNaN(point.X) || float.IsNaN(point.Y) || point.X<0 || point.X>1 || point.Y<0 || point.Y>1) return false;
            // Preserve the captured point across subsequent coordinate updates and respawns.
            DateTime deathTimeUtc=DateTime.UtcNow;
            var marker=new MapZone { Name=Localization.Get("DeathName")+" "+deathTimeUtc.ToString("o",CultureInfo.InvariantCulture),DeathCreatedUtc=deathTimeUtc,
                Layer=MapZone.DeathMarkerLayer, Category=ZoneCategory.Custom,
                Argb=Color.FromArgb(255,225,210,190).ToArgb(), Points=new[]{point} };
            var candidate=new List<MapZone>(zones);
            candidate.RemoveAll(z=>z.IsDeathMarker && z.DistanceTo(point)<0.00001);
            candidate.Add(marker);
            if(!TryCommitZonesCore(candidate,null,automatic)) return false;
            note=Localization.T("WaypointSaved",marker.DisplayName);
            SettingsChanged();
            RenderOverlay();
            return true;
        }

        void TriggerPin() {
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(TriggerPin));
                return;
            }
            if(panelOpening || searchOpen) return;
            if((DateTime.UtcNow - lastInsertAction).TotalMilliseconds < 450) return;
            lastInsertAction = DateTime.UtcNow;



            if(position==null) return;



            PointF mapPt = ToMap(position);



            MapZone existing = null;



            if(zones != null) {



                foreach(MapZone z in zones) {



                    if(z != null && z.Category==ZoneCategory.Custom && z.Points!=null && z.Points.Length>0) {



                        double dx=z.Points[0].X-mapPt.X, dy=z.Points[0].Y-mapPt.Y;



                        if(Math.Sqrt(dx*dx+dy*dy)<0.005) { existing=z; break; }



                    }



                }



            }



            if(existing!=null) {



                string displayName=existing.DisplayName;



                if(ConfirmWaypointRemoval(displayName,null)) {



                    var candidate=new List<MapZone>(zones); candidate.Remove(existing);
                    if(!TryCommitZones(candidate,null)) return;
                    pin=null;
                    if(searchTarget==existing) { searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null; }







                    note=Localization.T("WaypointDeleted",displayName);



                    SettingsChanged();



                    RenderOverlay();



                }



                return;



            }



            SaveCustomWaypoint(mapPt,new Position { X=position.X, Y=position.Y, Z=position.Z, Yaw=position.Yaw });



        }



        void ChangeRouteColor() {
            using(ColorDialog dlg = new ColorDialog()) {
                dlg.Color = routeGuidanceColor;
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                IWin32Window owner = (SettingsVisible && IsHandleCreated) ? (IWin32Window)this : (overlay != null && overlay.IsHandleCreated ? (IWin32Window)overlay : null);
                DialogResult res = owner != null ? RunUiModal(() => dlg.ShowDialog(owner)) : RunUiModal(() => dlg.ShowDialog());
                if(res == DialogResult.OK) {
                    routeGuidanceColor = dlg.Color;
                    SettingsChanged();
                    if(SettingsVisible) {
                        BuildSettingsPanel();
                        canvas.Invalidate();
                    }
                    RenderOverlay();
                }
            }
        }

        void ChangeHistoryColor() {
            using(var dlg=new ColorDialog { Color=routeHistoryColor,FullOpen=true,AnyColor=true }) {
                IWin32Window owner=(SettingsVisible&&IsHandleCreated)?(IWin32Window)this:(overlay!=null&&overlay.IsHandleCreated?(IWin32Window)overlay:null);
                var result=owner!=null?RunUiModal(()=>dlg.ShowDialog(owner)):RunUiModal(()=>dlg.ShowDialog());
                if(result!=DialogResult.OK)return;
                routeHistoryColor=dlg.Color;SettingsChanged();lastFrameKey=null;
                if(SettingsVisible) { BuildSettingsPanel();canvas.Invalidate(); }
                RenderOverlay();
            }
        }

                void ChangePlayerColor() {
            using(ColorDialog dlg = new ColorDialog()) {
                dlg.Color = playerConeColor;
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                IWin32Window owner = (SettingsVisible && IsHandleCreated) ? (IWin32Window)this : (overlay != null && overlay.IsHandleCreated ? (IWin32Window)overlay : null);
                DialogResult res = owner != null ? RunUiModal(() => dlg.ShowDialog(owner)) : RunUiModal(() => dlg.ShowDialog());
                if(res == DialogResult.OK) {
                    playerConeColor = dlg.Color;
                    SettingsChanged();
                    if(SettingsVisible) {
                        BuildSettingsPanel();
                        canvas.Invalidate();
                    }
                    lastFrameKey = null;
                    RenderOverlay();
                }
            }
        }

        void AddCustomWaypointAtScreenPoint(Point pt) {
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => AddCustomWaypointAtScreenPoint(pt)));
                return;
            }
            Native.ReleaseCapture();
            PointF mapPt;
            if(pendingFullMapWaypointMapPoint.HasValue) {
                mapPt=pendingFullMapWaypointMapPoint.Value;
                pendingFullMapWaypointMapPoint=null;
            } else if(!TryGetFullMapPoint(pt,out mapPt)) return;

            SaveCustomWaypoint(mapPt,new Position {
                X=617718.0-mapPt.X*1521618.0,
                Y=618618.0-mapPt.Y*1523618.0,
                Z=position!=null?position.Z:0,
                Yaw=position!=null?position.Yaw:0
            });
        }

        void SaveCustomWaypoint(PointF mapPt,Position waypointPosition) {
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => SaveCustomWaypoint(mapPt, waypointPosition)));
                return;
            }
            if(mapPt.X<0 || mapPt.X>1 || mapPt.Y<0 || mapPt.Y>1) return;
            if(panelOpening || searchOpen) return;

            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;

            GameFocusReturn focus=new GameFocusReturn(); focus.Capture();

            bool previousPanelOpening=panelOpening;
            panelOpening=true;

            try {
                using(Form prompt=new Form {
                    ClientSize=new Size(380,180),
                    Text=Localization.Get("WaypointNameTitle"),
                    StartPosition=FormStartPosition.CenterScreen,
                    FormBorderStyle=FormBorderStyle.None,
                    MaximizeBox=false,
                    MinimizeBox=false,
                    TopMost=true,
                    ShowInTaskbar=true,
                    Cursor=Cursors.Default
                }) {
                    OverlayTheme.Frame(prompt, Localization.Get("WaypointNameTitle"), () => prompt.DialogResult = DialogResult.Cancel);
                    Label lbl=new Label { Left=20,Top=64,Width=340,Text=Localization.Get("WaypointNamePrompt"),TabIndex=1,ForeColor=OverlayTheme.InkMuted,Font=OverlayTheme.UiFont(9f) };
                    TextBox txt=new TextBox { Left=20,Top=92,Width=340,TabIndex=0,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink,BorderStyle=BorderStyle.FixedSingle,Font=OverlayTheme.UiFont(10f) };
                    Button ok=new Button { Text=Localization.Get("Done"),Left=190,Top=132,Width=80,Height=30,DialogResult=DialogResult.OK,TabIndex=2,BackColor=OverlayTheme.Accent,ForeColor=Color.FromArgb(12,12,12),Font=OverlayTheme.UiFont(9f, FontStyle.Bold),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
                    ok.FlatAppearance.BorderColor=OverlayTheme.AccentHover;
                    Button cn=new Button { Text=Localization.Get("Cancel"),Left=280,Top=132,Width=80,Height=30,DialogResult=DialogResult.Cancel,TabIndex=3,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
                    cn.FlatAppearance.BorderColor=OverlayTheme.Border;
                    prompt.Controls.AddRange(new Control[]{txt,lbl,ok,cn});
                    prompt.ActiveControl=txt;
                    prompt.AcceptButton=ok; prompt.CancelButton=cn;
                    txt.KeyDown += (s, e) => { if(e.KeyCode == Keys.Enter) { ok.PerformClick(); e.Handled = true; e.SuppressKeyPress = true; } };
                    prompt.Shown += (s, e) => prompt.BeginInvoke(new Action(() => {
                        if(prompt.IsDisposed) return;
                        Native.ReleaseCapture();
                        Cursor.Clip=Rectangle.Empty;
                        Native.ForceForeground(prompt.Handle);
                        prompt.Activate();
                        prompt.ActiveControl=txt;
                        txt.Focus();
                        txt.SelectAll();
                        Cursor.Position=txt.PointToScreen(new Point(txt.ClientSize.Width/2,txt.ClientSize.Height/2));
                        txt.Cursor=Cursors.IBeam;
                    }));
                    IWin32Window owner = (SettingsVisible && IsHandleCreated) ? (IWin32Window)this : null;
                    if(RunUiModal(() => prompt.ShowDialog(owner))==DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text)) {
                        string name=txt.Text.Trim();
                        if(name.Length>80) name=name.Substring(0,80);
                        MapZone wp=new MapZone { Name=name, Argb=Color.FromArgb(255,0,200,255).ToArgb(), Points=new PointF[]{mapPt}, Category=ZoneCategory.Custom };
                        var candidate=new List<MapZone>(zones); candidate.Add(wp);
                        if(!TryCommitZones(candidate,prompt)) return;
                        pin=waypointPosition;

                        note=Localization.T("WaypointSaved",name);
                        SettingsChanged();
                        RenderOverlay();
                    }
                }
            } catch(Exception ex) {
                Program.LogException("SaveCustomWaypoint", ex);
            } finally {
                panelOpening=previousPanelOpening;
                resumeAfter=DateTime.UtcNow.AddMilliseconds(400);
                next=resumeAfter;
                fallbackKeysArmed=false;
                focus.Restore();
            }
        }



        bool zonesLoadFailed;
        bool TryCommitZones(List<MapZone> candidate,IWin32Window owner) { return TryCommitZonesCore(candidate,owner,false); }
        bool TryCommitZonesCore(List<MapZone> candidate,IWin32Window owner,bool silent) {
            try {
                if(zonesLoadFailed) throw new InvalidDataException(Localization.Get("NoteZonesLoadFail"));
                MapSidecarStore.Save(mapSidecarPaths,candidate);
                zones=candidate;
                try { lastZonesFileWriteTimeUtc=File.GetLastWriteTimeUtc(zonesPath); } catch(IOException) {} catch(UnauthorizedAccessException) {}
                try { lastWaypointsFileWriteTimeUtc=File.GetLastWriteTimeUtc(customWaypointsPath); } catch(IOException) {} catch(UnauthorizedAccessException) {}
                return true;
            } catch(Exception ex) {
                if(!(ex is IOException) && !(ex is InvalidDataException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException)) throw;
                note=Localization.T("ZeSaveZonesError",ex.Message);
                Program.LogException("SaveWaypoints",ex);
                if(!diagnosticMode && !silent) RunUiModal(() => MessageBox.Show(owner,note,Localization.Get("ZoneEditorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Error));
                return false;
            }
        }

        bool ConfirmWaypointRemoval(string displayName,Form owner) {
            if(IsHandleCreated && InvokeRequired) {
                return (bool)Invoke(new Func<string, Form, bool>(ConfirmWaypointRemoval), displayName, owner);
            }
            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;

            GameFocusReturn focus=new GameFocusReturn(); focus.Capture();

            bool previousPanelOpening=panelOpening;

            panelOpening=true;

            try {

                using(Form prompt=CreateWaypointRemovalPrompt(displayName)) {
                    IWin32Window dialogOwner=owner ?? ((SettingsVisible && IsHandleCreated) ? (IWin32Window)this : null);
                    return RunUiModal(() => prompt.ShowDialog(dialogOwner))==DialogResult.Yes;

                }

            } catch(Exception ex) {
                Program.LogException("ConfirmWaypointRemoval", ex);
                return false;
            } finally {



                panelOpening=previousPanelOpening;



                resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



                focus.Restore();



            }



        }



        internal static Form CreateWaypointRemovalPrompt(string displayName) {
            Form prompt=new Form { Text=Localization.Get("RemoveWaypoint"),ClientSize=new Size(420,170),
                StartPosition=FormStartPosition.CenterScreen,MaximizeBox=false,MinimizeBox=false,TopMost=true,ShowInTaskbar=true,Cursor=Cursors.Default };
            OverlayTheme.Frame(prompt, Localization.Get("RemoveWaypoint"), () => prompt.DialogResult = DialogResult.Cancel);
            Label message=new Label { Text=Localization.T("WaypointDeleteConfirm",displayName),Left=20,Top=64,Width=380,Height=50,ForeColor=OverlayTheme.Ink,Font=OverlayTheme.UiFont(9.25f) };
            Button remove=new Button { Text=Localization.Get("RemoveWaypoint"),Left=140,Top=122,Width=150,Height=32,DialogResult=DialogResult.Yes,TabIndex=1,BackColor=OverlayTheme.Accent,ForeColor=Color.FromArgb(12,12,12),Font=OverlayTheme.UiFont(9f, FontStyle.Bold),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
            remove.FlatAppearance.BorderColor=OverlayTheme.AccentHover;
            Button cancel=new Button { Text=Localization.Get("Cancel"),Left=300,Top=122,Width=100,Height=32,DialogResult=DialogResult.Cancel,TabIndex=0,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
            cancel.FlatAppearance.BorderColor=OverlayTheme.Border;
            prompt.Controls.AddRange(new Control[]{message,remove,cancel});
            prompt.AcceptButton=cancel; prompt.CancelButton=cancel;
            prompt.Shown+=(s,e)=>prompt.BeginInvoke(new Action(()=> {



                if(prompt.IsDisposed) return;

                Native.ReleaseCapture();
                Native.ForceForeground(prompt.Handle);



                prompt.Activate(); prompt.ActiveControl=cancel; cancel.Focus();



                Cursor.Clip=Rectangle.Empty;



                Cursor.Position=cancel.PointToScreen(new Point(cancel.Width/2,cancel.Height/2));



            }));



            return prompt;



        }



        void TriggerZoomIn() {



            if((DateTime.UtcNow - lastPgUpAction).TotalMilliseconds < 120) return;



            lastPgUpAction = DateTime.UtcNow;
            if(fullMapActive) {
                AdjustFullMapZoom(120,new Point(overlay.Height/2,overlay.Height/2));
                return;
            }




            autoZoom=false;



            float step=1f+(zoomStepPercent/100f);



            zoom=Math.Min(maxZoom,zoom*step);



            targetZoom=zoom;



            SettingsChanged();



            lastFrameKey=null;



            terrainKey=null;



            RenderOverlay();



        }



        void TriggerZoomOut() {



            if((DateTime.UtcNow - lastPgDnAction).TotalMilliseconds < 120) return;



            lastPgDnAction = DateTime.UtcNow;
            if(fullMapActive) {
                AdjustFullMapZoom(-120,new Point(overlay.Height/2,overlay.Height/2));
                return;
            }




            autoZoom=false;



            float step=1f+(zoomStepPercent/100f);



            zoom=Math.Max(1,zoom/step);



            targetZoom=zoom;



            SettingsChanged();



            lastFrameKey=null;



            terrainKey=null;



            RenderOverlay();



        }



        void UpdateOpacityFromSidebarMouse(int mouseX) {
            if(sidebarOpacityTrackRect.Width <= 0) return;
            float frac = (float)(mouseX - sidebarOpacityTrackRect.Left) / (float)sidebarOpacityTrackRect.Width;
            frac = Math.Max(0f, Math.Min(1f, frac));
            int newOpacity = (int)Math.Round(20 + frac * 80);
            if(newOpacity != fullMapOpacity) {
                fullMapOpacity = newOpacity;
                InvalidateFullMapSidebar();
                lastFrameKey = null;
                RenderOverlay();
            }
        }

        int inputGeneration;
        bool observedChatPaused;
        void SyncChatState() {
            bool paused=chat.Paused;
            if(paused==observedChatPaused) return;
            observedChatPaused=paused; inputGeneration++;
            if(paused) {
                Native.CancelActiveCopy();
                note=Localization.Get("NoteChatOpen");
                if(fullMapActive) SetFullMap(false);
            } else {
                resumeAfter=DateTime.UtcNow.AddMilliseconds(1200); next=resumeAfter;
                note=Localization.Get("NoteChatClosed");
            }
        }



        bool HotkeyContextAllowed(bool settingsShortcut=false) {



            return !diagnosticMode && !closing && !IsDisposed && IsHandleCreated &&



                !AdminPlayerWorkPaused && !adminCommandBusy && !panelOpening && !shortcutCaptureOpen && !chat.Paused && !inventoryInputLocked &&



                (!SettingsVisible || settingsShortcut) && !searchOpen && AppShortcutWindowFocused(settingsShortcut);



        }



        bool IsWatchedPhysicalKey(int key,int scanCode) {
            if(WpfFieldConsoleRuntime.Active!=null&&WpfFieldConsoleRuntime.Active.IsSettingsToggleKey(key,scanCode))return true;
            if(shortcutCaptureOpen) return false;
            if(PhysicalKeyCapture.Matches(key,scanCode,scumMapKey,scumMapScanCode)
                || PhysicalKeyCapture.Matches(key,scanCode,scumChatKey,scumChatScanCode)) return true;
            AppControlAction action;
            if(TryGetAppControl(key,scanCode,out action)) return true;
            return key==0x09 || key==0x0D || key==0x1B || key==0x6F || key==0xBF;
        }



        bool FallbackKeyPressed(int key) {



            bool down=(Native.GetAsyncKeyState(key)&0x8000)!=0;



            bool pressed=fallbackKeysArmed && down && !fallbackKeyDown[key];



            fallbackKeyDown[key]=down;



            return pressed;



        }



        void ArmFallbackKeys() {



            // Seed the state on focus gain. A key held while Alt-Tabbing back must not



            // fire as a new in-game shortcut.



            for(int key=0;key<fallbackKeyDown.Length;key++)



                fallbackKeyDown[key]=(Native.GetAsyncKeyState(key)&0x8000)!=0;



            fallbackKeysArmed=true;



        }



        void OnGameKey(int key,int scanCode=0) {



            try {



                OnGameKeyCore(key,scanCode);



            } catch(Exception ex) {



                Program.LogException("OnGameKey", ex);



            }



        }



        void OnGameKeyCore(int key,int scanCode=0) {
            TraceShortcutContext("UI received",key,scanCode);
            // Settings remains closable after clicking back into SCUM, even while modal gates are set.
            if(WpfFieldConsoleRuntime.Active!=null&&WpfFieldConsoleRuntime.Active.IsSettingsToggleKey(key,scanCode)
                && !closing&&!IsDisposed&&!keys.ShortcutModifiersDown(true)&&AppShortcutWindowFocused(true)) {
                WpfFieldConsoleRuntime.Active.Handle(AppControlAction.Settings);return;
            }
            if(shortcutCaptureOpen) return;



            // Physical chat transitions still matter while a coordinate copy is being cancelled.



            bool settingsKey=PhysicalKeyCapture.Matches(key,scanCode,settingsShortcutKey,settingsShortcutScanCode);
            bool inputWindowFocused=AppShortcutWindowFocused(settingsKey);
            // Dismiss the visible map before tracking/chat/inventory gates. A
            // stale controller flag must never strand a WPF window on screen.
            if(WpfFieldConsoleRuntime.Active!=null&&!closing&&!IsDisposed&&!panelOpening&&inputWindowFocused
                &&!keys.ShortcutModifiersDown(false)&&WpfFieldConsoleRuntime.Active.TryDismissTactical(key,scanCode,chat.Paused))return;



            if(AdminPlayerWorkPaused || diagnosticMode || closing || IsDisposed || panelOpening || adminCommandBusy || !inputWindowFocused) { inputGeneration++; return; }



            // Use physical hook state: the sampler's injected Ctrl is not a user modifier.



            // Shift is also used for sprinting and must not swallow M or chat transitions.



            bool mapKey=PhysicalKeyCapture.Matches(key,scanCode,scumMapKey,scumMapScanCode);
            bool chatKey=PhysicalKeyCapture.Matches(key,scanCode,scumChatKey,scumChatScanCode) || key==0xBF || key==0x6F || key==0x0D || key==0x1B || key==0x09;



            if(keys.ShortcutModifiersDown(!mapKey && !chatKey)) return;



            bool wasChat=observedChatPaused;
            SyncChatState();

            // Escape belongs to chat first if chat was actively open



            if(wasChat && key==0x1B) return;



            // If chat is open and typing, do not swallow in-game text or trigger hotkeys



            if((chat.Paused || inventoryInputLocked) && key!=0x1B && key!=0x0D) return;



            if(!HotkeyContextAllowed(settingsKey)) return;



            AppControlAction appAction;
            if(!mapKey && TryGetAppControl(key,scanCode,out appAction)) {
                DispatchAppControl(appAction);
                return;
            }
            Action action=null;
            if(mapKey) action=TriggerFullMap;
            else if((key==0x09 || key==0x1B) && fullMapActive) action=()=> { if(WpfFieldConsoleRuntime.Active!=null)WpfFieldConsoleRuntime.Active.CloseTactical();else SetFullMap(false); };

            if(action==null) return;
            if(Native.InputTraceActive) Native.TraceInput("shortcut game action vk="+key+" scan="+scanCode+" map="+mapKey);



            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



            action();



        }



        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); }



        protected override void WndProc(ref Message m) { base.WndProc(ref m); }



        internal void Accept(string text) {



            Position p=Position.Parse(text); if(p==null)return;



            DateTime now=DateTime.UtcNow;



            PointF currentPt=ToMap(p);
            ClearReachedDeathMarkers(currentPt);



            double nowSec=motionClock.Elapsed.TotalSeconds;



            if(position!=null) {



                PointF oldPt=ToMap(position);



                double dSq=(currentPt.X-oldPt.X)*(currentPt.X-oldPt.X)+(currentPt.Y-oldPt.Y)*(currentPt.Y-oldPt.Y);



                if(dSq>0.000001) previousMapPoint=oldPt;



                double dt=Math.Max(.05,(now-updated).TotalSeconds);



                double distance=DestinationSearch.Metres(currentPt,oldPt);



                double speed=distance>150 || dt>3?0:distance/dt;



                filteredSpeed+=(speed-filteredSpeed)*(1-Math.Exp(-dt/1.2));



                if(speed<0.1 && filteredSpeed<0.4) filteredSpeed=0;



                if(autoZoom) {



                    float high=Math.Max(1,autoZoomMax), low=Math.Min(high,Math.Max(1,autoZoomMin));



                    float vehicleSpeedKmH = (float)(filteredSpeed * 3.6);



                    float desired = vehicleSpeedKmH <= 18f ? high : high - (float)Math.Min(1.0, (vehicleSpeedKmH - 18f) / 80f) * (high - low);



                    if(!fixedCentreReady) { targetZoom=desired; zoom=desired; }



                    else if(filteredSpeed<0.4) { targetZoom=high; }



                    else if(Math.Abs(desired-targetZoom)>0.5f) { targetZoom=desired; }



                }



                previousTime = now;



                motion.Sample(currentPt,p.Yaw,nowSec,false);



            } else {



                motion.Sample(currentPt,p.Yaw,nowSec,true);



            }



            if(zones!=null) {



                foreach(MapZone z in zones) {
                    if(z != null && z.Points != null && z.Points.Length >= 3) {
                        string zLayer = string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer;
                        if(!showZones || disabledZoneLayers.Contains(zLayer)) continue;
                    }
                    if(z != null && z.Contains(currentPt)) {



                        if(lastVisitedZone!=z && zoneChime) {



                            try { System.Media.SystemSounds.Asterisk.Play(); } catch {}



                        }



                        lastVisitedZone=z;



                        lastVisitedTime=DateTime.UtcNow;



                        break;



                    }



                }



            }



            if(!object.ReferenceEquals(searchTarget,wpfUnsharedTarget)&&DestinationReached(searchTarget,currentPt)) {
                VoiceArrived();



                note=Localization.T("NoteWaypointArrived",searchTarget.DisplayName);



                searchTarget=null;



                activeRoute=null; routeGeneration++;



                lastRouteTarget=null;



                lastFrameKey=null;



            }



            fixedMapCentre=currentPt;



            if(!fixedCentreReady) { fixedCentreReady=true; terrainKey=null; }



            position=p; updated=now; if(SettingsVisible)canvas.Invalidate();
            RecordLocationHistory(currentPt,now);



            if(searchTarget!=null) {
                // Compare this sample with the previous route before a routine refresh can hide a wrong turn.
                if(!diagnosticMode) { nextVoiceUpdate=DateTime.MinValue; TickVoice(now,Native.GameFocused()); }
                UpdateRouteAsync(currentPt,searchTarget);
            }



            RenderOverlay();



        }



        bool FallbackBindingPressed(int key,int scanCode) {
            if(key<=0 || key>=fallbackKeyDown.Length) return false;
            if(PhysicalKeyCapture.Valid(scanCode)) {
                int virtualKey=Native.ScanCodeToVirtualKey(scanCode);
                return virtualKey>0 && virtualKey<256 && FallbackKeyPressed(virtualKey);
            }
            return FallbackKeyPressed(key);
        }

        void RecordLocationHistory(PointF point,DateTime sampledAtUtc) {
            PruneLocationHistory(sampledAtUtc);
            if(!locationHistory) return;
            if(locationHistoryPoints.Count>0) {
                LocationHistoryPoint last=locationHistoryPoints[locationHistoryPoints.Count-1];
                // Bound trail density independently of the coordinate polling frequency.
                if((sampledAtUtc-last.SampledAtUtc).TotalSeconds<1) return;
                double dx=point.X-last.MapPoint.X,dy=point.Y-last.MapPoint.Y;
                if(dx*dx+dy*dy<0.00000004 && (sampledAtUtc-last.SampledAtUtc).TotalSeconds<15) return;
            }
            locationHistoryPoints.Add(new LocationHistoryPoint { MapPoint=point,SampledAtUtc=sampledAtUtc });
            if(locationHistoryPoints.Count>LocationHistoryLimit) locationHistoryPoints.RemoveRange(0,locationHistoryPoints.Count-LocationHistoryLimit);
            locationHistoryRevision++;
            terrainKey=null; lastFrameKey=null;
        }

        void PruneLocationHistory(DateTime now) {
            DateTime cutoff=now.AddMinutes(-locationHistoryMinutes);
            int expired=0;
            while(expired<locationHistoryPoints.Count && locationHistoryPoints[expired].SampledAtUtc<cutoff) expired++;
            if(expired==0) return;
            locationHistoryPoints.RemoveRange(0,expired);
            InvalidateLocationHistory();
        }

        void ClearLocationHistory() {
            locationHistoryPoints.Clear();
            InvalidateLocationHistory();
        }

        void InvalidateLocationHistory() {
            locationHistoryRevision++;
            if(locationHistoryPath!=null) { locationHistoryPath.Dispose(); locationHistoryPath=null; }
            locationHistoryHoverPoint=null;
            displayedLocationHistorySegment=-1;
            terrainKey=null; lastFrameKey=null;
        }






        void TriggerFullMap() {
            if((DateTime.UtcNow-lastMToggleTime).TotalMilliseconds<250) return;
            if(!fullMapActive && (chat.Paused || inventoryInputLocked)) return;
            lastMToggleTime = DateTime.UtcNow;



            if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.ToggleTactical(true);return; }
            SetFullMap(!fullMapActive);



        }



        internal bool ObserveAdminMapMouse(int message,Point screenPoint) {
            if(message==0x201) {
                Rectangle mapArea=new Rectangle(overlay.Left,overlay.Top,overlay.Height,overlay.Height);
                adminMapClickArmed=fullMapActive && overlay.FullMapClickThrough && mapArea.Contains(screenPoint);
            }
            if(message!=0x202) return false;
            bool completed=adminMapClickArmed && fullMapActive;
            adminMapClickArmed=false;
            return completed;
        }
        internal void CompleteAdminMapClick() {
            if(!fullMapActive || closing || IsDisposed) return;
            DateTime now=DateTime.UtcNow;
            Native.CancelActiveCopy();
            inputGeneration++;
            // A map click does not close SCUM's chat box. Retain its input owner.
            observedChatPaused=chat.Paused;
            inventoryInputLocked=false; inventoryProbeMisses=0;
            nextInventoryProbe=now.AddMilliseconds(500);
            pending=false; failures=0; resumeAfter=now; next=now;
            draggingMap=false; draggingSlider=false; draggingSidebarScrollbar=false;
            overlay.Capture=false;
            GameKeys.ClearInputCooldowns();
            SetFullMap(false);
        }

        void SetFullMap(bool active) {
            // Keep the visible WPF window and controller flag in the same state
            // for chat, focus and all other legacy close paths.
            if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.SetTactical(active);return; }



            if(fullMapActive==active) return;



            fullMapActive=active;
            adminControlsCommandReturn=false;
            adminMapClickArmed=false;



            if(!chat.Paused) chat.Reset();



            if(fullMapActive) {
                try {
                    DateTime zonesWrite=File.Exists(zonesPath)?File.GetLastWriteTimeUtc(zonesPath):DateTime.MinValue;
                    DateTime waypointsWrite=File.Exists(customWaypointsPath)?File.GetLastWriteTimeUtc(customWaypointsPath):DateTime.MinValue;
                    if(zonesWrite>lastZonesFileWriteTimeUtc || waypointsWrite>lastWaypointsFileWriteTimeUtc) {
                        zones=MapSidecarStore.Reload(mapSidecarPaths);
                        lastZonesFileWriteTimeUtc=zonesWrite;
                        lastWaypointsFileWriteTimeUtc=waypointsWrite;
                        InvalidateFullMapSidebar();
                        terrainKey=null;
                        lastFrameKey=null;
                    }
                } catch {}



                if(!overlay.Visible) {



                    overlay.Show();



                    hiddenByFocusLoss = false;



                }



            } else {






                fullMapZoom = 1.0f;
                fullMapZoomOpacityOverride=false;



                fullMapPan = new PointF(0.5f, 0.5f);



                draggingSlider = false;



                draggingMap = false;



            }



            overlay.FullMapMode = fullMapActive;
            AdminControlsMapModeChanged();

            // The physical map key reaches SCUM too. Keep its keyboard focus so
            // key-up, the next map toggle and Escape reach the underlying game map.



            lastFrameKey = null;



            terrainKey = null;



            note = fullMapActive ? Localization.Get("NoteFullMapOn") : Localization.Get("NoteFullMapOff");



            RenderOverlay();



        }



        bool FullMapInputAllowed() {



            if(fullMapActive && !overlay.FullMapClickThrough && (diagnosticMode || (!closing && !IsDisposed && !Native.CopyInProgress))) return true;



            draggingSlider=false; draggingMap=false; mouseMoved=true;



            return false;



        }



        void HandleFullMapWheel(int delta, Point pt) {



            if(!FullMapInputAllowed()) return;



            int mapDim = overlay.Height;



            if(pt.X >= mapDim) {



                // Scroll sidebar content



                int deltaScroll = -(delta/120)*48;



                sidebarScrollY = Math.Max(0, Math.Min(sidebarMaxScroll, sidebarScrollY + deltaScroll));



                InvalidateFullMapSidebar();



                lastFrameKey = null;



                RenderOverlay();



                return;



            }



            AdjustFullMapZoom(delta,pt);
        }



        void HandleFullMapMouseDown(Point pt, MouseButtons btn) {



            if(btn==MouseButtons.Right) pendingFullMapWaypointMapPoint=null;



            // Copy key-up cleanup must not discard a physical sidebar click.
            bool sidebarInput=fullMapActive && !overlay.FullMapClickThrough && pt.X>=overlay.Height && !closing && !IsDisposed;
            if(!sidebarInput && !FullMapInputAllowed()) return;
            if(sidebarInput)Native.CancelActiveCopy();



            if(btn!=MouseButtons.Right) pendingFullMapWaypointMapPoint=null;



            int mapW = fullMapActive ? overlay.Height : overlay.Width;



            if(pt.X >= mapW) {



                // Sidebar interaction



                if(btn==MouseButtons.Right) pendingFullMapWaypointMapPoint=null;



                if(btn == MouseButtons.Left) {



                    // Pinned Header



                    if(sidebarOpacityTrackRect.Contains(pt) || (pt.X >= sidebarOpacityTrackRect.Left && pt.X <= sidebarOpacityTrackRect.Right && Math.Abs(pt.Y - sidebarOpacityTrackRect.Top) <= 14)) {



                        draggingSlider = true;



                        UpdateOpacityFromSidebarMouse(pt.X);



                        return;



                    }



                    if(sidebarZoomInRect.Contains(pt)) {



                        TriggerZoomIn();



                        return;



                    }



                    if(sidebarZoomOutRect.Contains(pt)) {



                        TriggerZoomOut();



                        return;



                    }



                    if(sidebarZoomResetRect.Contains(pt)) {
                        ResetMapZoom();
                        return;
                    }
                    if(sidebarSearchRect.Contains(pt)) {



                        TriggerZoneSearch();



                        return;



                    }



                    if(!sidebarClearWaypointRect.IsEmpty && sidebarClearWaypointRect.Contains(pt)) {
                        ClearActiveWaypoint();
                        return;
                    }
                    // Scrollbar thumb drag



                    if(!sidebarScrollbarThumbRect.IsEmpty && sidebarScrollbarThumbRect.Contains(pt)) {



                        draggingSidebarScrollbar = true;



                        scrollbarDragStartY = pt.Y;



                        scrollbarDragStartScrollY = sidebarScrollY;



                        InvalidateFullMapSidebar();



                        return;



                    }



                    if(!sidebarScrollbarTrackRect.IsEmpty && sidebarScrollbarTrackRect.Contains(pt)) {



                        float scrollFrac = (float)(pt.Y - sidebarScrollbarTrackRect.Top) / (float)sidebarScrollbarTrackRect.Height;



                        sidebarScrollY = Math.Max(0, Math.Min(sidebarMaxScroll, (int)(scrollFrac * sidebarMaxScroll)));



                        InvalidateFullMapSidebar();



                        lastFrameKey = null;



                        RenderOverlay();



                        return;



                    }



                    // Dynamic scrollable items



                    for(int i = sidebarClickables.Count - 1; i >= 0; i--) {



                        var item = sidebarClickables[i];



                        if(item.ScreenRect.Contains(pt)) {



                            item.OnClick();



                            return;



                        }



                    }



                }



                return;



            }



            if(HandleFieldConsoleChromeDown(pt,btn))return;

            // Map interaction (pt.X < mapW)



            if(btn == MouseButtons.Left) {



                mouseDownPt = pt;



                mouseDownTime = DateTime.UtcNow;



                mouseMoved = false;



                if(fullMapZoom > 1.0f) {



                    draggingMap = true;



                    dragStart = pt;



                    panStart = fullMapPan;



                }



            } else if(btn == MouseButtons.Right) {

                if(TryRemoveDeathMarkerAtScreenPoint(pt))return;



                PointF rightClickMapPoint;



                pendingFullMapWaypointMapPoint=TryGetFullMapPoint(pt,out rightClickMapPoint)?(PointF?)rightClickMapPoint:null;
                // Opening an admin context menu must not change the view or clear
                // navigation before its captured teleport target is used.
                if(adminToolsEnabled && pendingFullMapWaypointMapPoint.HasValue)return;



                if(fullMapZoom > 1.0f) {



                    fullMapZoom = 1.0f;
                    fullMapZoomOpacityOverride=false;



                    fullMapPan = new PointF(0.5f, 0.5f);



                    InvalidateFullMapSidebar();



                    lastFrameKey = null; terrainKey = null;



                    RenderOverlay();



                } else if(searchTarget != null || pin != null) {



                    searchTarget = null;



                    pin = null;



                    activeRoute = null; routeGeneration++;



                    lastRouteTarget = null;



                    note = Localization.Get("NoteWaypointCleared");



                    SettingsChanged();



                    lastFrameKey = null; terrainKey = null;



                    RenderOverlay();



                }



            }



        }



        void HandleFullMapMouseMove(Point pt, MouseButtons btn) {



            if(!FullMapInputAllowed()) return;

            UpdateLocationHistoryHover(btn==MouseButtons.None && !draggingMap && !draggingSlider && !draggingSidebarScrollbar ? (Point?)pt : null);



            if(draggingSlider) {



                UpdateOpacityFromSidebarMouse(pt.X);



                return;



            }



            if(draggingSidebarScrollbar && sidebarMaxScroll > 0) {



                int dy = pt.Y - scrollbarDragStartY;



                int trackH = sidebarScrollbarTrackRect.Height - sidebarScrollbarThumbRect.Height;



                if(trackH > 0) {



                    float scrollFrac = (float)dy / trackH;



                    sidebarScrollY = Math.Max(0, Math.Min(sidebarMaxScroll, (int)(scrollbarDragStartScrollY + scrollFrac * sidebarMaxScroll)));



                    InvalidateFullMapSidebar();



                    lastFrameKey = null;



                    RenderOverlay();



                }



                return;



            }



            if(Math.Abs(pt.X - mouseDownPt.X) > 5 || Math.Abs(pt.Y - mouseDownPt.Y) > 5) {



                mouseMoved = true;



            }



            if(draggingMap && fullMapZoom > 1.0f && overlay.Width > 0 && overlay.Height > 0) {



                int mapDim = overlay.Height;



                float side = mapDim * fullMapZoom;



                float dx = (pt.X - dragStart.X) / side;



                float dy = (pt.Y - dragStart.Y) / side;



                fullMapPan.X = Math.Max(0.05f, Math.Min(0.95f, panStart.X - dx));



                fullMapPan.Y = Math.Max(0.05f, Math.Min(0.95f, panStart.Y - dy));



                // Panning changes the camera only; retain terrain coverage and sidebar controls.
                lastFrameKey = null;



                RenderOverlay();



            }



        }



        void HandleFullMapMouseUp(Point pt, MouseButtons btn) {
            if(fieldConsoleChromePress) { fieldConsoleChromePress=false;return; }



            if(!FullMapInputAllowed()) return;



            if(draggingSlider) {



                draggingSlider = false;



                InvalidateFullMapSidebar();



                SaveSettings();



                return;



            }



            if(draggingSidebarScrollbar) {



                draggingSidebarScrollbar = false;



                InvalidateFullMapSidebar();



                return;



            }



            if(draggingMap) {



                draggingMap = false;

                if(mouseMoved) {
                    terrainKey=null;
                    lastFrameKey=null;
                    RenderOverlay();
                }



            }



            int mapW = fullMapActive ? overlay.Height : overlay.Width;



            if(btn == MouseButtons.Left && !mouseMoved && (DateTime.UtcNow - mouseDownTime).TotalMilliseconds < 500) {



                if(pt.X < mapW) {



                    PlaceGpsMarkerAtScreenPoint(pt);



                }



            }



        }



        void RegisterClickable(Rectangle rect, Rectangle viewport, Action onClick) {



            if(rect.Bottom < viewport.Top || rect.Top > viewport.Bottom) return;



            sidebarClickables.Add(new SidebarClickableItem { ScreenRect = rect, OnClick = onClick });



        }



        void DrawScrollableToggle(Graphics g, int x, int y, int w, int h, string label, bool enabled, Rectangle viewport, Action onClick) {



            Rectangle rowRect = new Rectangle(x, y, w, h);



            RegisterClickable(rowRect, viewport, onClick);



            int swW = 22, swH = 12;



            int swY = y + (h - swH) / 2;



            Rectangle swRect = new Rectangle(x, swY, swW, swH);



            DrawSwitchPill(g, swRect, enabled, false);



            using (Font f = new Font("Segoe UI", 7.2f, enabled ? FontStyle.Bold : FontStyle.Regular))



            using (Brush textBrush = new SolidBrush(enabled ? Color.WhiteSmoke : Color.FromArgb(160, 170, 180))) {



                g.DrawString(label, f, textBrush, x + swW + 7, y + 2.0f);



            }



        }



        void DrawSwitchPill(Graphics g, Rectangle rect, bool enabled, bool partial) {



            using (GraphicsPath swPath = new GraphicsPath()) {



                int r = rect.Height;



                swPath.AddArc(rect.X, rect.Y, r, r, 90, 180);



                swPath.AddArc(rect.Right - r, rect.Y, r, r, 270, 180);



                swPath.CloseFigure();



                Color swColor = enabled ? OverlayTheme.Accent : partial ? Color.FromArgb(160, 105, 20) : Color.FromArgb(28, 34, 40);



                using (Brush b = new SolidBrush(swColor)) g.FillPath(b, swPath);



                using (Pen p = new Pen(enabled ? OverlayTheme.AccentHover : Color.FromArgb(48, 58, 68), 1.0f)) g.DrawPath(p, swPath);



            }



            int knobR = (rect.Height - 4) / 2;



            int knobX = enabled ? rect.Right - knobR - 3 : partial ? rect.Left + (rect.Width - knobR * 2) / 2 + knobR : rect.Left + knobR + 3;



            int knobY = rect.Top + rect.Height / 2;



            Color knobColor = enabled ? Color.FromArgb(14, 17, 20) : Color.FromArgb(139, 148, 158);
            using (Brush kb = new SolidBrush(knobColor)) {



                g.FillEllipse(kb, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);
            using (Pen kp = new Pen(enabled ? Color.FromArgb(255, 210, 100) : Color.FromArgb(70, 80, 90), 1.0f)) g.DrawEllipse(kp, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);



            }



        }



        int DrawEmbeddedWildlifeBiomes(Graphics g, int x, int y, int w, Rectangle viewportRect) {



            int cardH = 144;



            Rectangle wlCardRect = new Rectangle(x, y, w, cardH);



            using (GraphicsPath cardPath = new GraphicsPath()) {



                int r = 8;



                cardPath.AddArc(wlCardRect.X, wlCardRect.Y, r, r, 180, 90);



                cardPath.AddArc(wlCardRect.Right - r, wlCardRect.Y, r, r, 270, 90);



                cardPath.AddArc(wlCardRect.Right - r, wlCardRect.Bottom - r, r, r, 0, 90);



                cardPath.AddArc(wlCardRect.X, wlCardRect.Bottom - r, r, r, 90, 90);



                cardPath.CloseFigure();



                using (Brush bg = new SolidBrush(Color.FromArgb(170, 16, 22, 30))) g.FillPath(bg, cardPath);



                using (Pen border = new Pen(Color.FromArgb(100, 50, 70, 90), 1.0f)) g.DrawPath(border, cardPath);



            }



            var biomes = new[] {



                new { Color = Color.FromArgb(0, 168, 120), Key = "BiomeMediterranean", Animals = new[] { "Boar", "Chicken", "Donkey", "Goat", "Rabbit" } },



                new { Color = Color.FromArgb(255, 219, 43), Key = "BiomeMeadow", Animals = new[] { "Bear", "Boar", "Deer", "Horse", "Rabbit", "Wolf" } },



                new { Color = Color.FromArgb(65, 181, 73), Key = "BiomeForest", Animals = new[] { "Bear", "Boar", "Deer", "Rabbit", "Wolf" } },



                new { Color = Color.FromArgb(221, 221, 221), Key = "BiomeMountain", Animals = new[] { "Bear", "Deer", "Goat", "Rabbit", "Wolf" } }



            };



            using (Font biomeFont = OverlayTheme.UiFont(6.8f, FontStyle.Bold))



            using (Font animFont = OverlayTheme.UiFont(6.4f, FontStyle.Regular))



            using (Brush animBrush = new SolidBrush(Color.FromArgb(190, 200, 210))) {



                for (int i = 0; i < biomes.Length; i++) {



                    var b = biomes[i];



                    int rowY = y + 5 + i * 27;



                    using (Brush sBrush = new SolidBrush(Color.FromArgb(180, b.Color))) {



                        g.FillEllipse(sBrush, x + 6, rowY + 3, 7, 7);



                    }



                    using (Pen sPen = new Pen(b.Color, 1.0f)) {



                        g.DrawEllipse(sPen, x + 6, rowY + 3, 7, 7);



                    }



                    string biomeName = Localization.Get(b.Key);



                    using (Brush bBrush = new SolidBrush(b.Color)) {



                        g.DrawString(biomeName, biomeFont, bBrush, x + 18, rowY);



                    }



                    string animStr = string.Join(", ", Array.ConvertAll(b.Animals, a => Localization.Get(a)));



                    g.DrawString(animStr, animFont, animBrush, x + 18, rowY + 12);



                }



            }



            using (Font noteFont = OverlayTheme.UiFont(5.8f, FontStyle.Italic))



            using (Brush noteBrush = new SolidBrush(Color.FromArgb(135, 155, 170))) {



                Rectangle noteRect = new Rectangle(x + 6, y + 116, w - 12, 26);



                g.DrawString(Localization.Get("HuntingLegendNote"), noteFont, noteBrush, noteRect);



            }



            return y + cardH + 6;



        }



        void DrawSidebarDivider(Graphics g, int x, int y, int w) {



            using (Pen p = new Pen(Color.FromArgb(60, 60, 75, 90), 1.0f)) {



                g.DrawLine(p, x, y, x + w, y);



            }



        }



        void DrawMiniButton(Graphics g, Rectangle rect, string text, bool active) {
            FullMapSidebarStyle.DrawMiniButton(g,rect,text,active);
        }

        void DrawActionButton(Graphics g, Rectangle rect, string text, Color baseColor) {
            FullMapSidebarStyle.DrawActionButton(g,rect,text,baseColor);
        }

        void DrawFullMapSidebar(Graphics g,int sidebarX,int sidebarW,int sidebarH) {
            DrawFieldConsoleSidebar(g,sidebarX,sidebarW,sidebarH);
        }




        void DrawCachedFullMapSidebar(Graphics g,int sidebarX,int sidebarW,int sidebarH) {



            if(fullMapSidebarFrame==null || fullMapSidebarFrame.Width!=overlay.Width || fullMapSidebarFrame.Height!=overlay.Height) {



                if(fullMapSidebarFrame!=null)fullMapSidebarFrame.Dispose();



                fullMapSidebarFrame=new Bitmap(overlay.Width,overlay.Height,PixelFormat.Format32bppPArgb);



                renderedFullMapSidebarRevision=-1;



            }



            if(renderedFullMapSidebarRevision!=fullMapSidebarRevision) {



                using(Graphics sidebarGraphics=Graphics.FromImage(fullMapSidebarFrame)) {



                    sidebarGraphics.Clear(Color.FromArgb(0,0,0,0));



                    DrawFullMapSidebar(sidebarGraphics,sidebarX,sidebarW,sidebarH);



                }



                renderedFullMapSidebarRevision=fullMapSidebarRevision;



            }



            g.DrawImageUnscaled(fullMapSidebarFrame,0,0);



        }



        static string SectorFromMapPt(PointF p) {



            int col = Math.Max(0, Math.Min(4, (int)Math.Floor(p.X * 5f)));



            int row = Math.Max(0, Math.Min(4, (int)Math.Floor(p.Y * 5f)));



            string[] letters = new string[] { "D", "C", "B", "A", "Z" };



            return letters[row] + (4 - col);



        }



        bool TryGetFullMapPoint(Point pt,out PointF mapPt) {



            mapPt=PointF.Empty;



            if(!fullMapActive || overlay.Width<=0 || overlay.Height<=0)return false;



            int mapDim=overlay.Height;



            if(pt.X<0 || pt.X>=mapDim || pt.Y<0 || pt.Y>=mapDim)return false;



            float side=mapDim*fullMapZoom;



            float left=mapDim/2f-side*fullMapPan.X;



            float top=overlay.Height/2f-side*fullMapPan.Y;



            mapPt=new PointF((pt.X-left)/side,(pt.Y-top)/side);



            return mapPt.X>=0f && mapPt.X<=1f && mapPt.Y>=0f && mapPt.Y<=1f;



        }



        void PlaceGpsMarkerAtScreenPoint(Point pt) {



            PointF clickMapPt;



            if(!TryGetFullMapPoint(pt,out clickMapPt))return;



            float clickMapX=clickMapPt.X;



            float clickMapY=clickMapPt.Y;



            int mapDim=overlay.Height;



            float side=mapDim*fullMapZoom;



            float left=mapDim/2f-side*fullMapPan.X;



            float top=overlay.Height/2f-side*fullMapPan.Y;



            // 1. If clicking near active target, toggle/clear it



            if(searchTarget != null) {



                PointF st = searchTarget.Centroid;



                float stScreenX = left + st.X * side;



                float stScreenY = top + st.Y * side;



                float distSq = (pt.X - stScreenX) * (pt.X - stScreenX) + (pt.Y - stScreenY) * (pt.Y - stScreenY);



                if(distSq < 18f * 18f) {



                    searchTarget = null;



                    pin = null;



                    activeRoute = null; routeGeneration++;



                    lastRouteTarget = null;



                    note = Localization.Get("NoteWaypointCleared");



                    SettingsChanged();



                    lastFrameKey = null;



                    terrainKey = null;



                    RenderOverlay();



                    return;



                }



            }



            // World coordinates from map projection



            double worldX = 617718.0 - (double)clickMapX * 1521618.0;



            double worldY = 618618.0 - (double)clickMapY * 1523618.0;



            double worldZ = position != null ? position.Z : 0;



            // 2. Check if clicked near an existing POI in zones (within 14px)



            MapZone clickedZone = null;



            float closestDistSq = 14f * 14f;



            if(zones != null) {



                foreach(MapZone z in zones) {



                    PointF c = z.Centroid;



                    float zx = left + c.X * side;



                    float zy = top + c.Y * side;



                    float d2 = (pt.X - zx) * (pt.X - zx) + (pt.Y - zy) * (pt.Y - zy);



                    if(d2 < closestDistSq) {



                        closestDistSq = d2;



                        clickedZone = z;



                    }



                }



            }



            ScumMapMarker clickedMarker = (showScumMap && scumMap != null) ? scumMap.HitTest(pt, new RectangleF(left, top, side, side), 14f) : null;



            if(clickedZone != null) {



                searchTarget = clickedZone;



                pin = new Position { X = worldX, Y = worldY, Z = worldZ };



                note = Localization.T("NoteWaypointSet", clickedZone.DisplayName, PhysicalKeyCapture.KeyName(searchShortcutKey));



            } else if(clickedMarker != null) {



                string mName = scumMap.GetMarkerDisplayName(clickedMarker);



                PointF mPt = clickedMarker.Point;



                string sec = SectorFromMapPt(mPt);



                searchTarget = new MapZone {



                    Name = mName,
                    IsMapObject = true,



                    Points = new PointF[] { mPt },



                    Category = ZoneCategory.Custom



                };



                pin = new Position { X = worldX, Y = worldY, Z = worldZ };



                note = Localization.T("NoteWaypointSet", searchTarget.DisplayName, PhysicalKeyCapture.KeyName(searchShortcutKey));



            } else {



                string sec = SectorFromMapPt(clickMapPt);



                string label = Localization.Get("GpsMarker") + " (" + sec + ")";



                ScumMapHabitat habitat=showScumMap && scumMap!=null ? scumMap.HabitatAt(clickMapPt) : null;



                if(habitat!=null) label=scumMap.HabitatDisplayName(habitat)+" ("+sec+")";



                searchTarget = new MapZone {



                    Name = label,



                    Subtitle = habitat!=null ? habitat.Summary : null,



                    Points = new PointF[] { clickMapPt },



                    Category = ZoneCategory.Custom



                };



                pin = new Position { X = worldX, Y = worldY, Z = worldZ };



                note = Localization.T("NoteWaypointSet", searchTarget.Name, PhysicalKeyCapture.KeyName(searchShortcutKey));



            }



            UpdateRouteAsync(position != null ? ToMap(position) : new PointF(.5f, .5f), searchTarget);



            SettingsChanged();



            lastFrameKey = null;



            terrainKey = null;



            RenderOverlay();



        }



        DateTime nextTimingLog=DateTime.MinValue;
        DateTime nextTickRender=DateTime.MinValue;
        int timingLogBusy;
        int diagnosticWriteBusy;
        double lastTickStarted;
        void LogTrackingTiming(double started,double gap) {
            if(!Program.IsTestBuild || DateTime.UtcNow<nextTimingLog) return;
            nextTimingLog=DateTime.UtcNow.AddSeconds(2);
            if(System.Threading.Interlocked.Exchange(ref timingLogBusy,1)!=0) return;
            string entry=string.Format(CultureInfo.InvariantCulture,
                "{0:o} tick_ms={1:F1} gap_ms={2:F1} sample_age_ms={3:F0} pending={4} pending_ms={5:F0} copying={6} failures={7} chat={8} recent_input={9} interval_ms={10}\r\n",
                DateTime.UtcNow,motionClock.Elapsed.TotalMilliseconds-started,gap,position==null?-1:(DateTime.UtcNow-updated).TotalMilliseconds,
                pending,pending?(DateTime.UtcNow-sent).TotalMilliseconds:0,copyInProgress,failures,chat.Paused,Native.UserTypingOrActive(),Program.TrackingIntervalMs(copyIntervalMs,scumCopyModifierKey));
            string path=Path.Combine(dataFolder,"tracking-timing.log");
            ThreadPool.QueueUserWorkItem(_=> {
                try { if(!File.Exists(path) || new FileInfo(path).Length<1048576) File.AppendAllText(path,entry); }
                catch(IOException) {} catch(UnauthorizedAccessException) {}
                finally { System.Threading.Interlocked.Exchange(ref timingLogBusy,0); }
            });
        }
        volatile bool musicalScumbagOpen;
        int musicProcessCheckBusy;
        DateTime nextMusicProcessCheck=DateTime.MinValue;
        bool intensiveWorkWasPaused;
        void CheckMusicProcess(DateTime now) {
            if(now<nextMusicProcessCheck) return;
            nextMusicProcessCheck=now.AddSeconds(1);
            if(Interlocked.Exchange(ref musicProcessCheckBusy,1)!=0) return;
            // Process discovery must not stall the UI or compete with the music input loop.
            ThreadPool.QueueUserWorkItem(_=> {
                try {
                    var processes=Process.GetProcessesByName("MusicalScumbag");
                    try { musicalScumbagOpen=processes.Length>0; }
                    finally { foreach(var process in processes) process.Dispose(); }
                } catch { /* Keep the last known state until discovery succeeds. */ }
                finally { Interlocked.Exchange(ref musicProcessCheckBusy,0); }
            });
        }
        bool UpdateIntensiveWorkPause() {
            if(closing || IsDisposed || resourcesReleased) return true;
            if(IntensiveWorkPaused) {
                if(!intensiveWorkWasPaused) {
                    intensiveWorkWasPaused=true;
                    Native.CancelActiveCopy();
                    inputGeneration++;
                    pending=false;
                    clipboardSnapshotReady=false;
                    fallbackKeysArmed=false;
                }
                if(DateTime.UtcNow>=saveAfter) SaveSettings();
                timer.Interval=250;
                return true;
            }
            if(intensiveWorkWasPaused) {
                intensiveWorkWasPaused=false;
                failures=0;
                next=DateTime.UtcNow;
                lastTickStarted=0;
                lastFrameKey=null;
                timer.Interval=100;
            }
            return false;
        }
        void PollMusicPausedInput() {
            // Keep manual controls and copied coordinates usable without automatic
            // copy injection, animation, or repeated overlay rendering.
            bool gameFocused=Native.GameFocused();
            keys.Resync(gameFocused);
            SyncChatState();
            overlay.SetGameFocus(gameFocused);
            overlay.UpdateFullMapClickThrough(keys.PhysicalControlDown,gameFocused);
            if(adminCommandBusy || closing || IsDisposed) return;
            uint current=Native.GetClipboardSequenceNumber();
            if(current==sequence) return;
            string text;
            if(!Native.TryReadCoordinateClipboard(out text)) return;
            sequence=current;
            if(Position.Parse(text)!=null) Accept(text);
        }
        void Tick(object sender,EventArgs args) {
            Debug.Assert(!InvokeRequired,"Sampling timer must run on the WinForms UI thread.");
            if(diagnosticMode || closing || IsDisposed) return;
            if(tickBusy) return;
            CheckMusicProcess(DateTime.UtcNow);
            if(UpdateIntensiveWorkPause()) {
                if(musicalScumbagOpen && !AdminPlayerWorkPaused) PollMusicPausedInput();
                return;
            }
            double tickStarted=motionClock.Elapsed.TotalMilliseconds;
            double tickGap=lastTickStarted==0?0:tickStarted-lastTickStarted;
            lastTickStarted=tickStarted;
            tickBusy=true;
            try {
            DateTime now=DateTime.UtcNow;
            if(now>=saveAfter)SaveSettings();
            PruneLocationHistory(now);
            if(!Program.IsTestBuild && string.IsNullOrEmpty(Program.LocalUpdateSource)
                && updateCheckSchedule.TryBegin(now,!updateCheckRunning && !adminCommandBusy && !relativeTeleportBusy
                    && !copyInProgress && !Native.CopyInProgress && !chat.Paused && !inventoryInputLocked))
                CheckForUpdates(false,true);
            // Menus, dialogs, Alt-Tab and injected copy input can make a low-level hook
            // miss a matching key-up. Reconcile the hook's transition view with Windows'
            // physical state before it can block sampling or a shortcut.
            bool gameFocused=Native.GameFocused();
            PollDeathBanner(now,gameFocused);
            RefreshDeathLabels(now);
            UpdateInventoryInputGate(now,gameFocused,()=>Native.HasSelectedTopNavigationTab(Native.GetForegroundWindow()));
            keys.Resync(gameFocused);
            // Use reconciled physical Ctrl state. Injected Ctrl from location
            // copying must not hide the map or cancel its own copy operation.
            overlay.UpdateFullMapClickThrough(keys.PhysicalControlDown,gameFocused);
            SyncChatState();
            TickVoice(now,gameFocused);
            bool ourWindowFocused=Native.IsOurWindow(Native.GetForegroundWindow());
            bool gameOrOurFocused=gameFocused || ourWindowFocused;
            bool obsFocused=Native.ObsFocused();
            int desiredTickInterval=gameOrOurFocused ? 16 : 100;
            if(timer.Interval!=desiredTickInterval) timer.Interval=desiredTickInterval;
            bool isAltDown=(Native.GetAsyncKeyState(0x12)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA4)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
            if(isAltDown) lastAltDownTime=now;
            bool altTabRecent=isAltDown || (now - lastAltDownTime).TotalMilliseconds < 500 || (now - Native.LastAltTabTime).TotalMilliseconds < 500;
            if(!altTabRecent) {
                overlay.SetGameFocus(gameFocused);
                // Keep the actual map available to OBS's window picker. This
                // affects visibility only; game input still requires gameFocused.
                if(obsFocused && hiddenByFocusLoss && !ToolWindowVisible) {
                    overlay.Show();RenderOverlay();hiddenByFocusLoss=false;
                }
                if(gameFocused && !wasGameFocused) {
                    // SCUM regained focus: restore overlay and engage Auto mode
                    if(hiddenByFocusLoss) { overlay.Show(); RenderOverlay(); hiddenByFocusLoss=false; }
                    // Automatically resume sampling when gameplay regains focus.
                    failures=0;
                    next=now; // trigger immediate request
                }
                if(!gameOrOurFocused && !obsFocused && (wasGameFocused || wasObsFocused) && !SettingsVisible && !searchOpen) {
                    // Both SCUM and our overlay lost focus: hide overlay entirely and cease input
                    if(overlay.Visible) { overlay.Hide(); hiddenByFocusLoss=true; }
                }
                wasGameFocused=gameOrOurFocused;
                wasObsFocused=obsFocused;
            }
            SyncFullMapAdminControls(gameOrOurFocused,keys.PhysicalControlDown || altTabRecent || chat.Paused
                || inventoryInputLocked || searchOpen || panelOpening || SettingsVisible);
            if(gameFocused && Native.IsRightMouseDown()) {
                resumeAfter = now.AddMilliseconds(600);
            }
            if(!gameFocused) inputGeneration++;
            // Alt-Tab hides the overlay above without changing SCUM's map state.
            // Restore the same expanded state when focus returns.
            // Hardware polling fallback for non-text hotkeys in case the low-level hook drops.
            // Do not poll M or Escape here: polling cannot tell whether SCUM's chat box owns the
            // keystroke, while the physical hook can.
            bool shortcutFocused=AppShortcutWindowFocused();
            TraceShortcutState();
            if(shortcutFocused && !adminCommandBusy && !panelOpening && !shortcutCaptureOpen && !SettingsVisible && !searchOpen && !chat.Paused && !inventoryInputLocked && !keys.ShortcutModifiersDown(true)) {
                if(!fallbackKeysArmed) ArmFallbackKeys();
                // Every application binding shares the hook's dispatcher and gates.
                // The SCUM map/chat controls continue to belong to the physical hook.
                foreach(var action in AppControlBindings.AllActions) {
                    int binding=AppControlKey(action);
                    if(binding!=0 && FallbackBindingPressed(binding,AppControlScanCode(action))) {
                        if(Native.InputTraceActive) Native.TraceInput("shortcut polling edge action="+action+" vk="+binding+" scan="+AppControlScanCode(action));
                        DispatchAppControl(action);
                    }
                    if(!HotkeyContextAllowed()) break;
                }
            } else {
                fallbackKeysArmed=false;
            }
            // Cursor visibility is not an authoritative map-open signal.
            if(UpdateIntensiveWorkPause()) return;
            motion.Advance(motionClock.Elapsed.TotalSeconds);
            if(autoZoom && Math.Abs(targetZoom-zoom)>0.001f) {
                float zoomDelta=targetZoom-zoom;
                if(Math.Abs(zoomDelta)>0.008f) {
                    zoom+=zoomDelta*0.16f;
                } else {
                    zoom=targetZoom;
                }
            }
            try {
                uint current=Native.GetClipboardSequenceNumber();
                if(current!=sequence && !adminCommandBusy) {
                    string text;
                    if(!Native.TryReadCoordinateClipboard(out text)) throw new ExternalException("Clipboard is busy.");
                    Position p=Position.Parse(text);
                    if(Native.InputTraceActive && pending) Native.TraceInput("copy clipboard changed parsed="+(p!=null));
                    sequence=current;
                    if(p!=null) {
                        Accept(text); failures=0;
                        if(pending) { responses++; note=Localization.Get("NoteCoordReceived"); }
                        if(pending && clipboardRestoreAllowed && Native.GetClipboardSequenceNumber()==current) {
                            try {
                                if(savedDataObject!=null) Clipboard.SetDataObject(savedDataObject,true,0,0);
                                else Native.TryClearClipboard();
                            } catch {}
                            sequence=Native.GetClipboardSequenceNumber();
                        }
                    }
                    if(p!=null) pending=false;
                    else { clipboardSnapshotReady=false; if(pending) clipboardRestoreAllowed=false; }
                }
                if(pending && !copyInProgress && (now-sent).TotalMilliseconds>Program.CopyResponseTimeoutMs(scumCopyModifierKey)) {
                    if(Native.InputTraceActive) Native.TraceInput("copy response timed out");
                    pending=false; failures++; note=Localization.T("NoteNoCoordsAttempt",failures);
                    if(failures>=3) {
                        next=now.AddMilliseconds(Program.CopyRetryDelayMs(scumCopyModifierKey));
                        note=scumCopyKey==Program.DefaultCopyKey && scumCopyScanCode==0?
                            Localization.Get("NoteCaptureCopyKey"):Localization.T("NoteNoCoordsRetry",Program.CopyRetryDelayMs(scumCopyModifierKey)/1000);
                    }
                }
                if(TrackingEnabled && !pending) {
                    if(chat.Paused)note=Localization.Get("NoteChatOpen");
                    else if(Native.IsRightMouseDown())note=Localization.Get("NoteAdsActive");
                    else if(!keys.Available)note=Localization.Get("NoteChatMonitorUnavailable");
                    else if(!gameFocused) note=Localization.Get("NoteWaitingForeground");
                    else if(Native.KeysBusy(scumCopyModifierKey,scumCopyKey,fullMapActive)) note=Localization.Get("NoteWaitingUserKeys");
                }
                if(TrackingEnabled && keys.Available && !panelOpening && !SettingsVisible && !searchOpen && !chat.Paused && !inventoryInputLocked && !pending && !copyInProgress && !altTabRecent && now>=resumeAfter && now>=next && gameFocused && !Native.KeysBusy(scumCopyModifierKey,scumCopyKey,fullMapActive)) {
                    int interval=Program.TrackingIntervalMs(copyIntervalMs,scumCopyModifierKey);
                    next=now.AddMilliseconds(interval);
                    PerformCopyAsync();
                }
            } catch(ExternalException) { note=Localization.Get("NoteClipboardBusy"); }



            string age=position==null?Localization.Get("WaitingForCoordinates"):string.Format(CultureInfo.InvariantCulture,"X {0:F0}  Y {1:F0}  Z {2:F0}  |  {3}",position.X,position.Y,position.Z,Localization.T("SecondsAgo",(int)(now-updated).TotalSeconds));



            if(SettingsVisible) {
                string statusText=age+"  |  "+Localization.T("StatusSummary",attempts,responses)+(string.IsNullOrEmpty(note)?"":Environment.NewLine+note);
                if(status.Text!=statusText) status.Text=statusText;
            }



            // Routine successes are visible in Settings; avoid synchronous disk I/O every copy cycle.



            bool routine=pending || note==Localization.Get("NoteCoordReceived");



            string diagnostic=routine?Localization.Get("DiagCopyActive"):note;



            if(diagnostic!=lastDiagnostic) {



                lastDiagnostic=diagnostic;



                if(System.Threading.Interlocked.Exchange(ref diagnosticWriteBusy,1)==0) {
                    string line=DateTime.UtcNow.ToString("o")+" "+diagnostic+Environment.NewLine;
                    ThreadPool.QueueUserWorkItem(_=> {
                        try { File.AppendAllText(diagnosticPath,line); } catch(IOException) {} catch(UnauthorizedAccessException) {}
                        finally { System.Threading.Interlocked.Exchange(ref diagnosticWriteBusy,0); }
                    });
                }



            }



            bool stale=position!=null && (now-updated).TotalSeconds>5;



            if(stale!=canvasStale) { canvasStale=stale; if(SettingsVisible)canvas.Invalidate(); }



            if(now>=nextTickRender) {
                nextTickRender=now.AddMilliseconds(33);
                RenderOverlay();
            }



            } catch(Exception ex) {



                Program.LogException("Tick", ex);



            } finally {



                tickBusy=false;
                LogTrackingTiming(tickStarted,tickGap);



                if(closing) {



                    try { BeginInvoke(new Action(Close)); } catch {}



                }



            }



        }

        void BeginCopyRequest() { clipboardRestoreAllowed=true; pending=true; sent=DateTime.UtcNow; }
        void CompleteCopyRequest(CopyResult result) {
            // A response consumed during the chord has already cleared pending.
            if(result!=CopyResult.Sent) pending=false;
        }

        async void PerformCopyAsync() {
            if(IntensiveWorkPaused || adminCommandBusy || copyInProgress || closing || IsDisposed) return;
            copyInProgress = true;
            try {
                if(!clipboardSnapshotReady) {
                    uint beforeCapture=Native.GetClipboardSequenceNumber();
                    IDataObject snapshot=await Native.CaptureClipboardAsync();
                    if(IntensiveWorkPaused || adminCommandBusy || closing || IsDisposed || Native.GetClipboardSequenceNumber()!=beforeCapture) return;
                    savedDataObject=snapshot; clipboardSnapshotReady=true;
                }
                sequence = Native.GetClipboardSequenceNumber();
                if(Native.GameFocused() && !Native.KeysBusy(scumCopyModifierKey, scumCopyKey,fullMapActive) && !Native.IsAltOrTabOrWinDown()) {
                    attempts++;
                    BeginCopyRequest();
                    CopyResult copyResult = await Native.Copy(
                        () => !IntensiveWorkPaused && !closing && !panelOpening && !SettingsVisible && !searchOpen && TrackingEnabled && !chat.Paused && !inventoryInputLocked && DateTime.UtcNow >= resumeAfter,
                        scumCopyModifierKey, scumCopyKey,()=>fullMapActive,scumCopyScanCode,
                        // The delivery hook runs on the input thread. Read only input
                        // gates here; UI/control inspection stays in allowed() above.
                        ()=>!closing && !panelOpening && !searchOpen && !diagnosticMode && !adminCommandBusy
                            && !shortcutCaptureOpen && !chat.Paused && !inventoryInputLocked);
                    CompleteCopyRequest(copyResult);
                    if(pending) note=Localization.Get("NoteAutoCopyActive");
                    if(copyResult != CopyResult.Sent) {
                        if(copyResult == CopyResult.Cancelled) note = Localization.Get("NoteCopyCancelled");
                        else { failures = 3; next = DateTime.UtcNow.AddSeconds(5); note = Localization.T("NoteCopyErrorRetry", Native.CopyError); }
                    }
                }
            } catch(Exception ex) {
                pending=false;
                Program.LogException("ClipboardCopy",ex);
                note = Localization.Get("NoteClipboardBusy");
            } finally {
                copyInProgress = false;
            }
        }



        string GetLocationDescription() {



            // OverlayKey runs on every timer tick, including the smooth-motion frames that do



            // not change the sampled position.  Avoid rescanning/sorting all zones twice per



            // presented frame until an input that can change this label actually changes.



            if(cachedLocationDescription!=null &&



                ReferenceEquals(cachedLocationPosition,position) &&



                ReferenceEquals(cachedLocationTarget,searchTarget) &&



                ReferenceEquals(cachedLocationRoute,activeRoute) &&






                cachedLocationDescriptionRevision==locationDescriptionRevision) {



                return cachedLocationDescription;



            }



            string description=BuildLocationDescription();
            if(searchTarget!=null && activeRoute!=null && !activeRoute.Success) description+=" | "+RouteUnavailableText;



            cachedLocationDescription=description;



            cachedLocationPosition=position;



            cachedLocationTarget=searchTarget;



            cachedLocationRoute=activeRoute;






            cachedLocationDescriptionRevision=locationDescriptionRevision;



            return description;



        }



                string BuildLocationDescription() {
            if (position == null) return Localization.Get("AwaitingPosition");
            PointF p = ToMap(position);
            int col = Math.Max(0, Math.Min(4, (int)Math.Floor(p.X * 5f)));
            int row = Math.Max(0, Math.Min(4, (int)Math.Floor(p.Y * 5f)));
            string[] letters = new string[] { "D", "C", "B", "A", "Z" };
            string sector = letters[row] + (4 - col);
            string result = sector;
            bool foundSpecific = false;

            List<LocationAnchor> anchors = GetLocationAnchors();

            // 1. Inside an anchor / zone
            foreach (LocationAnchor a in anchors) {
                if (a.Contains(p)) {
                    if (a.Zone != null) {
                        lastVisitedZone = a.Zone;
                    }
                    lastVisitedAnchorName = a.Name;
                    lastVisitedAnchorPoint = a.Point;
                    lastVisitedTime = DateTime.UtcNow;
                    result = sector + " - " + a.DisplayName;
                    foundSpecific = true;
                    break;
                }
            }

            if (!foundSpecific && anchors.Count > 0) {
                // 2. Sort anchors by distance to current player point
                var sortedByDist = new List<KeyValuePair<double, LocationAnchor>>();
                foreach (LocationAnchor a in anchors) {
                    double dist = a.DistanceTo(p);
                    sortedByDist.Add(new KeyValuePair<double, LocationAnchor>(dist, a));
                }
                sortedByDist.Sort((a, b) => a.Key.CompareTo(b.Key));

                LocationAnchor closest = sortedByDist[0].Value;
                double closestDist = sortedByDist[0].Key;
                LocationAnchor second = sortedByDist.Count > 1 ? sortedByDist[1].Value : default(LocationAnchor);
                double secondDist = sortedByDist.Count > 1 ? sortedByDist[1].Key : double.MaxValue;
                bool hasSecond = sortedByDist.Count > 1;

                // 3. Motion & heading analysis
                if (previousMapPoint.HasValue) {
                    PointF prev = previousMapPoint.Value;
                    double moveVx = p.X - prev.X;
                    double moveVy = p.Y - prev.Y;
                    double moveSpeed = Math.Sqrt(moveVx * moveVx + moveVy * moveVy);

                    if (moveSpeed > 0.0003) {
                        // Check if actively moving away from last visited anchor
                        if (!string.IsNullOrEmpty(lastVisitedAnchorName) && (DateTime.UtcNow - lastVisitedTime).TotalMinutes < 15) {
                            double dxLast = lastVisitedAnchorPoint.X - p.X, dyLast = lastVisitedAnchorPoint.Y - p.Y;
                            double dToLast = Math.Sqrt(dxLast * dxLast + dyLast * dyLast);
                            double dxPrev = lastVisitedAnchorPoint.X - prev.X, dyPrev = lastVisitedAnchorPoint.Y - prev.Y;
                            double prevDToLast = Math.Sqrt(dxPrev * dxPrev + dyPrev * dyPrev);

                            if (dToLast > prevDToLast && dToLast < 0.055) {
                                LocationAnchor targetAnchor = (closest.Name == lastVisitedAnchorName && hasSecond) ? second : closest;
                                double targetDist = targetAnchor.DistanceTo(p);
                                double prevTargetDist = targetAnchor.DistanceTo(prev);

                                if (targetDist < prevTargetDist && targetDist < 0.075) {
                                    result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedAnchorName), targetAnchor.DisplayName);
                                    foundSpecific = true;
                                } else {
                                    result = Localization.T("LocLeaving", sector, Localization.GetZoneName(lastVisitedAnchorName));
                                    foundSpecific = true;
                                }
                            }
                        }

                        // Check if actively approaching any candidate ahead
                        if (!foundSpecific) {
                            foreach (var kvp in sortedByDist) {
                                if (kvp.Key > 0.075) break;
                                LocationAnchor candidate = kvp.Value;
                                double curD = kvp.Key;
                                double prevD = candidate.DistanceTo(prev);

                                if (curD < prevD && (prevD - curD) > 0.0001) {
                                    if (!string.IsNullOrEmpty(lastVisitedAnchorName) && candidate.Name != lastVisitedAnchorName &&
                                        (DateTime.UtcNow - lastVisitedTime).TotalMinutes < 10) {
                                        double dxL = lastVisitedAnchorPoint.X - p.X, dyL = lastVisitedAnchorPoint.Y - p.Y;
                                        if (Math.Sqrt(dxL * dxL + dyL * dyL) < 0.06) {
                                            result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedAnchorName), candidate.DisplayName);
                                            foundSpecific = true;
                                            break;
                                        }
                                    }
                                    result = Localization.T("LocApproaching", sector, candidate.DisplayName);
                                    foundSpecific = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 4. Proximity & Between
                if (!foundSpecific) {
                    if (closestDist < 0.035) {
                        result = Localization.T("LocNear", sector, closest.DisplayName);
                    } else if (closestDist < 0.08 && hasSecond && secondDist < 0.08 && Math.Abs(closestDist - secondDist) < 0.035) {
                        result = Localization.T("LocBetween", sector, closest.DisplayName, second.DisplayName);
                    } else if (closestDist < 0.08) {
                        result = Localization.T("LocNear", sector, closest.DisplayName);
                    }
                }
            }


            // 5. Trip information (Destination, distance, approx time / ETA)
            if (searchTarget != null) {
                string targetName = searchTarget.DisplayName;
                double distM;
                string distSuffix = "";
                if (activeRoute != null && activeRoute.Success) {
                    distM = activeRoute.TotalDistanceMeters;
                    distSuffix = Localization.Get("RoadSuffix");
                } else {
                    PointF targetPt = searchTarget.Centroid;
                    double dxM = (targetPt.X - p.X) * 1521618 / 100.0;
                    double dyM = (targetPt.Y - p.Y) * 1523618 / 100.0;
                    distM = Math.Sqrt(dxM * dxM + dyM * dyM);
                }
                string distStr = distM >= 1000 ? string.Format(CultureInfo.InvariantCulture, "{0:F1} km{1}", distM / 1000.0, distSuffix) : ((int)Math.Round(distM)) + "m" + distSuffix;
                double speed = filteredSpeed > 1.2 ? filteredSpeed : (activeRoute != null && activeRoute.Success ? 13.9 : 4.2);
                double etaSec = distM / Math.Max(0.5, speed);
                string etaStr;
                if (etaSec < 60) etaStr = "<1m";
                else if (etaSec < 3600) etaStr = string.Format(CultureInfo.InvariantCulture, "{0}m", (int)Math.Round(etaSec / 60.0));
                else {
                    int h = (int)(etaSec / 3600);
                    int m = (int)((etaSec % 3600) / 60.0);
                    etaStr = string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", h, m);
                }
                result += string.Format(CultureInfo.InvariantCulture, "  |  -> {0}: {1} (~{2})", targetName, distStr, etaStr);
            }

            return result;
        }



        string OverlayKey() {
            int mx=motion!=null?(int)Math.Round(motion.Point.X*100000):0;
            int my=motion!=null?(int)Math.Round(motion.Point.Y*100000):0;
            int myaw=motion!=null?(int)Math.Round(motion.Yaw*10):0;
            frameKeyBuilder.Length=0;
            frameKeyBuilder.Append(position==null?0:position.X).Append('|')
                .Append(position==null?0:position.Y).Append('|')
                .Append(position==null?0:position.Z).Append('|')
                .Append(position==null?0:position.Yaw).Append('|')
                .Append(zoom).Append('|')
                .Append(chat.Paused).Append('|')
                .Append(VoiceStatus).Append('|')
                .Append(TrackingEnabled).Append('|')
                .Append(overlay.Width).Append('|')
                .Append(overlay.Height).Append('|')
                .Append(showStatus).Append('|')
                .Append(statusPos).Append('|')
                .Append(overlayShape).Append('|')
                .Append(showHeading).Append('|')
                .Append(showCompass).Append('|')
                .Append(GetLocationDescription()).Append('|')
                .Append(searchTarget==null?"":searchTarget.Name).Append('|')
                .Append(searchTarget==null?PointF.Empty:searchTarget.Centroid).Append('|')
                .Append(routeGuidanceColor.ToArgb()).Append('|')
                .Append(playerConeColor.ToArgb()).Append('|')
                .Append(routeHistoryColor.ToArgb()).Append('|')
                .Append(mx).Append('|')
                .Append(my).Append('|')
                .Append(myaw).Append('|')
                .Append(fullMapActive).Append('|')
                .Append((int)Math.Round(fullMapZoom*100)).Append('|')
                .Append((int)Math.Round(fullMapPan.X*1000)).Append('|')
                .Append((int)Math.Round(fullMapPan.Y*1000)).Append('|')
                .Append(EffectiveFullMapOpacity).Append('|')
                .Append(InfobarScrollFrame()).Append('|')
                .Append(showAdminPlayers).Append('|')
                .Append(adminPlayerRevision).Append('|')
                .Append(AdminPlayerFrame());
            return frameKeyBuilder.ToString();
        }



        int InfobarScrollFrame() {



            if(!infobarScrollActive || infobarScrollStart==DateTime.MinValue)return 0;



            return (int)Math.Max(0,(DateTime.UtcNow-infobarScrollStart).TotalMilliseconds/50.0);



        }



        public static string GetScrollBaseKey(string s) {
            if(string.IsNullOrEmpty(s)) return string.Empty;
            int arrow = s.IndexOf("->", StringComparison.Ordinal);
            if(arrow >= 0) {
                int colon = s.IndexOf(':', arrow);
                if(colon > arrow) {
                    return s.Substring(0, colon).TrimEnd();
                }
                return s.Substring(0, arrow).TrimEnd();
            }
            return s;
        }

        void DrawScrollingInfobarText(Graphics g,string text,Font font,Brush brush,RectangleF bounds) {

            SizeF textSize=g.MeasureString(text,font);

            bool shouldScroll=textSize.Width>bounds.Width+0.5f;

            string baseKey=GetScrollBaseKey(text);

            if(!shouldScroll) {

                infobarScrollActive=false;

                infobarScrollStart=DateTime.MinValue;

                renderedInfobarText=text;

                renderedInfobarBase=baseKey;

            } else {

                if(!infobarScrollActive || infobarScrollStart==DateTime.MinValue || !string.Equals(renderedInfobarBase,baseKey,StringComparison.Ordinal)) {

                    infobarScrollStart=DateTime.UtcNow;

                }

                infobarScrollActive=true;

                renderedInfobarText=text;

                renderedInfobarBase=baseKey;

            }



            float offset=0;



            if(infobarScrollActive) {



                float travel=textSize.Width-bounds.Width;



                const int pauseMs=1100;



                int travelMs=Math.Max(1100,(int)Math.Round(travel*28.0));



                int cycle=pauseMs+travelMs+pauseMs+travelMs;



                int phase=(int)((DateTime.UtcNow-infobarScrollStart).TotalMilliseconds%cycle);



                if(phase<pauseMs) {



                    offset=0;



                } else if(phase<pauseMs+travelMs) {



                    float t=(phase-pauseMs)/(float)travelMs;



                    float eased=(float)(0.5-0.5*Math.Cos(t*Math.PI));



                    offset=travel*eased;



                } else if(phase<pauseMs+travelMs+pauseMs) {



                    offset=travel;



                } else {



                    float t=(phase-(pauseMs+travelMs+pauseMs))/(float)travelMs;



                    float eased=(float)(0.5-0.5*Math.Cos(t*Math.PI));



                    offset=travel*(1f-eased);



                }



            }



            GraphicsState state=g.Save();



            try {



                g.SetClip(Rectangle.Ceiling(bounds));



                g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAlias;



                g.DrawString(text,font,brush,bounds.X-offset,bounds.Y+(bounds.Height-textSize.Height)/2f);



            } finally { g.Restore(state); }



        }



        Bitmap OverlayBitmap() {



            if(overlayFrame.Width!=overlay.Width || overlayFrame.Height!=overlay.Height || fullMap.Width!=overlay.Width+TerrainMargin*2 || fullMap.Height!=overlay.Height+TerrainMargin*2) {



                terrainKey=null; overlayFrame.Dispose(); fullMap.Dispose();



                overlayFrame=new Bitmap(overlay.Width,overlay.Height,PixelFormat.Format32bppPArgb);



                fullMap=new Bitmap(overlay.Width+TerrainMargin*2,overlay.Height+TerrainMargin*2,PixelFormat.Format32bppPArgb);



            }



            Bitmap bitmap=overlayFrame;



            const int barH=52;



            bool barActive=showStatus && !fullMapActive;



            bool barAbove=barActive && statusPos=="Above";



            int mapY=barAbove?barH:0;



            int mapH=barActive?bitmap.Height-barH:bitmap.Height;



            if(mapH<10) mapH=bitmap.Height;



            int mapW=fullMapActive?bitmap.Height:bitmap.Width;



            int sidebarW=fullMapActive?Math.Max(0,bitmap.Width-bitmap.Height):0;



            if(!barActive) {

                renderedInfobarText=null;

                renderedInfobarBase=null;

                infobarScrollActive=false;

                infobarScrollStart=DateTime.MinValue;

            }



            using(Graphics g=Graphics.FromImage(bitmap)) {



                g.Clear(Color.FromArgb(0,0,0,0));



                // Render the map portion



                DrawMapArea(g,new Rectangle(0,mapY,mapW,mapH));



            }



            // Apply subtle edge fade strictly within the map bounds and fade as player approaches/reaches the edge of the world map



            Rectangle mapBounds=new Rectangle(0,mapY,mapW,mapH);



            float currentZoom=fullMapActive?fullMapZoom:zoom;



            float side=Math.Min(mapBounds.Width,mapBounds.Height)*currentZoom;



            PointF p=position==null?new PointF(.5f,.5f):(motion!=null && motion.Point.X>0?motion.Point:ToMap(position));



            PointF centrePt=fullMapActive?fullMapPan:(currentZoom==1?new PointF(.5f,.5f):p);



            int mapLeft=(int)Math.Round(mapBounds.Left+mapBounds.Width/2f-side*centrePt.X);



            int mapTop=(int)Math.Round(mapBounds.Top+mapBounds.Height/2f-side*centrePt.Y);



            int mapRight=(int)Math.Round(mapLeft+side);



            int mapBottom=(int)Math.Round(mapTop+side);



            OverlayWindow.ApplyOpacityAndShape(bitmap,fullMapActive?EffectiveFullMapOpacity:mapOpacity,zoom==1?mapLeft:-1,zoom==1?mapTop:-1,zoom==1?mapRight:-1,zoom==1?mapBottom:-1,fullMapActive?"Square":overlayShape,mapBounds);

            // Keep destination guidance crisp above the terrain's edge fade.
            using(Graphics g=Graphics.FromImage(bitmap)) {
                DrawDestinationIndicator(g,mapBounds,zoom==1 && mapLeft!=-1 && mapTop!=-1 && mapRight!=-1 && mapBottom!=-1);
            }



            // Now draw the crisp HUD location info banner outside/over the map so it is not faded



            if(barActive) {
                using(Graphics g=Graphics.FromImage(bitmap)) { g.SmoothingMode=SmoothingMode.AntiAlias;DrawFieldConsoleHud(g,bitmap,barAbove,barH); }
            }

            if(fullMapActive) {



                using(Graphics g = Graphics.FromImage(bitmap)) {



                    g.SmoothingMode = SmoothingMode.AntiAlias;



                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;



                    // Right sidebar panel for all controls (cleanly separated from the map)



                    if(sidebarW > 0) {



                        DrawCachedFullMapSidebar(g, mapW, sidebarW, bitmap.Height);
                        DrawFieldConsoleMapChrome(g,mapW,bitmap.Height);



                    }



                }



            }



            return bitmap;



        }



        void RenderOverlay() {
            if(InvokeRequired) {
                if(Interlocked.Exchange(ref renderQueued,1)!=0)return;
                try {
                    BeginInvoke(new Action(()=> {
                        Interlocked.Exchange(ref renderQueued,0);
                        RenderOverlay();
                    }));
                } catch(InvalidOperationException) { Interlocked.Exchange(ref renderQueued,0); }
                return;
            }
            if(WpfFieldConsoleRuntime.Active!=null) { WpfFieldConsoleRuntime.Active.Refresh();return; }
            Debug.Assert(!InvokeRequired,"Overlay rendering must run on the WinForms UI thread.");
            if(AdminPlayerWorkPaused) return;



            if(!overlay.Visible || overlay.IsDisposed)return;



            string frameKey=OverlayKey();



            if(frameKey==lastFrameKey)return;



            overlay.Present(OverlayBitmap()); lastFrameKey=frameKey; PresentedFrames++;



        }



        public static PointF ToMap(Position p) {



            return new PointF((float)((617718-p.X)/1521618),(float)((618618-p.Y)/1523618));



        }



        void PaintMap(object sender,PaintEventArgs e) {
            Debug.Assert(!InvokeRequired,"Map painting must run on the WinForms UI thread.");



            DrawMapArea(e.Graphics,new Rectangle(0,0,canvas.Width,canvas.Height));



        }






        void DrawMapArea(Graphics g,Rectangle bounds) {



            using(Brush b=new SolidBrush(Color.FromArgb(12,17,22))) g.FillRectangle(b,bounds);



            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;

            float effectiveZoom=fullMapActive?fullMapZoom:zoom;
            float side=Math.Min(bounds.Width,bounds.Height)*effectiveZoom;
            PointF p=position==null?new PointF(.5f,.5f):(motion!=null && motion.Point.X>0?motion.Point:ToMap(position));
            PointF mapCentre=fullMapActive?fullMapPan:(effectiveZoom==1?new PointF(.5f,.5f):p);
            float cx=bounds.Left+bounds.Width/2f;
            float cy=bounds.Top+bounds.Height/2f;
            bool isPlayerCentered=!fullMapActive && effectiveZoom>1;
            float left=isPlayerCentered?(cx-side*p.X):(cx-side*mapCentre.X);
            float top=isPlayerCentered?(cy-side*p.Y):(cy-side*mapCentre.Y);

            string terrain=string.Join("|",new object[]{bounds,effectiveZoom,gridLabels,gridBorders,gridOpacity,showZones,string.Join(",",disabledZoneLayers),showCustomWaypoints,showScumMap,(scumMap!=null?scumMap.GetFilterHashKey():""),showZoneLabels,smartLabelLod,fullMapActive,labelSize,Localization.CurrentCode});
            // A padded viewport can follow small movements without repainting all terrain/POI.
            // Zoom, layers, language and settings remain part of the cache key.
            if(terrainKey!=terrain || Math.Abs(left-terrainLeft)>TerrainMargin || Math.Abs(top-terrainTop)>TerrainMargin) {
                using(Graphics background=Graphics.FromImage(fullMap)) {
                    background.Clear(Color.FromArgb(12,17,22));
                    background.TranslateTransform(TerrainMargin,TerrainMargin);
                    // The terrain buffer includes the sidebar, but only map pixels can be shown.
                    background.SetClip(new RectangleF(bounds.Left-TerrainMargin,bounds.Top-TerrainMargin,
                        bounds.Width+TerrainMargin*2,bounds.Height+TerrainMargin*2));
                    background.SmoothingMode=SmoothingMode.AntiAlias;
                    background.InterpolationMode=draggingMap?InterpolationMode.NearestNeighbor:InterpolationMode.Bilinear;
                    background.PixelOffsetMode=PixelOffsetMode.HighSpeed;
                    // Crop before scaling: GDI+ otherwise processes a huge zoomed image
                    // even though only a small window onto it is visible.
                    RectangleF visibleTexture=RectangleF.Intersect(background.VisibleClipBounds,new RectangleF(left,top,side,side));
                    if(visibleTexture.Width>0 && visibleTexture.Height>0) {
                        if(mapTiles!=null) {
                            // Bundled tiles are opaque. Avoid alpha blending while copying them.
                            background.CompositingMode=CompositingMode.SourceCopy;
                            try { mapTiles.Draw(background,visibleTexture,left,top,side,draggingMap?0.75f:1f,fullMapActive && !draggingMap); }
                            finally { background.CompositingMode=CompositingMode.SourceOver; }
                        } else {
                            Image texture=map;
                            foreach(Bitmap level in mapLevels) { if(level.Width<side || level.Height<side)break; texture=level; }
                            float textureScale=texture.Width/side, textureScaleY=texture.Height/side;
                            background.DrawImage(texture,visibleTexture,new RectangleF(
                                (visibleTexture.Left-left)*textureScale,(visibleTexture.Top-top)*textureScaleY,
                                visibleTexture.Width*textureScale,visibleTexture.Height*textureScaleY),GraphicsUnit.Pixel);
                        }
                    }
                    using(var wash=new SolidBrush(Color.FromArgb(145,14,25,22)))background.FillRectangle(wash,visibleTexture);
                    DrawGrid(background,left,top,side);
                    float currentZoom=fullMapActive?fullMapZoom:zoom;

                    categoryDrawList.Clear();
                    customZoneDrawList.Clear();
                    waypointDrawList.Clear();
                    if(zones!=null) {
                        for(int zi=0; zi<zones.Count; zi++) {
                            MapZone zone=zones[zi];
                            if(IsCustomWaypointZone(zone)) {
                                if(showCustomWaypoints) waypointDrawList.Add(zone);
                            } else if(zone!=null && zone.Points!=null && zone.Points.Length>=3) {
                                string zLayer = string.IsNullOrWhiteSpace(zone.Layer) ? "Default" : zone.Layer;
                                if(showZones && !disabledZoneLayers.Contains(zLayer)) customZoneDrawList.Add(zone);
                            } else categoryDrawList.Add(zone);
                        }
                    }
                    if(categoryDrawList.Count>0)ZoneStore.Draw(background,categoryDrawList,new RectangleF(left,top,side,side),true,labelSize,true,null,showZoneLabels,smartLabelLod,currentZoom);
                    if(customZoneDrawList.Count>0)ZoneStore.Draw(background,customZoneDrawList,new RectangleF(left,top,side,side),true,labelSize,false,null,showZoneLabels,smartLabelLod,currentZoom);
                    if(waypointDrawList.Count>0)ZoneStore.Draw(background,waypointDrawList,new RectangleF(left,top,side,side),false,labelSize,false,null,showZoneLabels,smartLabelLod,currentZoom);



                    if(showScumMap && scumMap!=null)scumMap.Draw(background,new RectangleF(left,top,side,side),currentZoom,showZoneLabels,labelSize,smartLabelLod);



                }



                terrainLeft=left; terrainTop=top;
                terrainKey=terrain; TerrainBuilds++;



            }



            GraphicsState terrainState=g.Save();
            try {
                g.SetClip(bounds,CombineMode.Intersect);
                g.DrawImageUnscaled(fullMap,(int)Math.Round(left-terrainLeft)-TerrainMargin,(int)Math.Round(top-terrainTop)-TerrainMargin);
                DrawLocationHistory(g,left,top,side);
                DrawAdminPlayers(g,bounds,left,top,side);
            } finally { g.Restore(terrainState); }



            // Draw search navigation line to target zone



            if(searchTarget!=null && position!=null) {



                PointF targetPt=searchTarget.Centroid;



                float tx=left+targetPt.X*side, ty=top+targetPt.Y*side;



                float px=isPlayerCentered?cx:(left+p.X*side), py=isPlayerCentered?cy:(top+p.Y*side);



                if(activeRoute!=null && activeRoute.Success && activeRoute.Polyline!=null && activeRoute.Polyline.Length>1) {



                    // Draw feeder from player to road entry point if off-road



                    if(activeRoute.EntryDistanceMeters>15.0) {



                        float enx=left+activeRoute.RoadEntryPoint.X*side, eny=top+activeRoute.RoadEntryPoint.Y*side;



                        using(Pen feederPen=new Pen(Color.FromArgb(220,routeGuidanceColor),3.0f)) {



                            feederPen.DashStyle=DashStyle.Dash;



                            feederPen.DashPattern=new float[]{5f,3f};



                            g.DrawLine(feederPen,px,py,enx,eny);



                        }



                    }



                    // Draw feeder from road exit point to target if off-road



                    if(activeRoute.ExitDistanceMeters>15.0) {



                        float exx=left+activeRoute.RoadExitPoint.X*side, exy=top+activeRoute.RoadExitPoint.Y*side;



                        using(Pen feederPen=new Pen(Color.FromArgb(220,routeGuidanceColor),3.0f)) {



                            feederPen.DashStyle=DashStyle.Dash;



                            feederPen.DashPattern=new float[]{5f,3f};



                            g.DrawLine(feederPen,exx,exy,tx,ty);



                        }



                    }



                    // Draw curved road polyline with subtle glow



                    if(routeScreenPoints.Length!=activeRoute.Polyline.Length)
                        routeScreenPoints=new PointF[activeRoute.Polyline.Length];



                    for(int i=0; i<routeScreenPoints.Length; i++) {



                        routeScreenPoints[i]=new PointF(left+activeRoute.Polyline[i].X*side, top+activeRoute.Polyline[i].Y*side);



                    }



                    using(Pen glowPen=new Pen(Color.FromArgb(70,routeGuidanceColor),4.2f)) {



                        glowPen.LineJoin=LineJoin.Round;



                        g.DrawLines(glowPen,routeScreenPoints);



                    }



                    using(Pen routePen=new Pen(Color.FromArgb(235,routeGuidanceColor),2.2f)) {



                        routePen.LineJoin=LineJoin.Round;



                        g.DrawLines(routePen,routeScreenPoints);



                    }




                }

                // Target zone marker (pulsing ring)



                using(Pen ringPen=new Pen(Color.FromArgb(200,routeGuidanceColor),2.0f)) {



                    g.DrawEllipse(ringPen,tx-10,ty-10,20,20);



                }



                using(Brush dotBrush=new SolidBrush(Color.FromArgb(240,routeGuidanceColor))) {



                    g.FillEllipse(dotBrush,tx-4,ty-4,8,8);



                }



                // Zone name label at target



                string targetDisplayName=searchTarget.DisplayName;



                using(Font navFont=OverlayTheme.UiFont(7.5f, FontStyle.Bold)) {



                    SizeF sz=g.MeasureString(targetDisplayName,navFont);



                    float lx=tx-sz.Width/2f, ly=ty-16-sz.Height;



                    using(Brush bgBrush=new SolidBrush(Color.FromArgb(200,14,17,20))) {



                        g.FillRectangle(bgBrush,lx-3,ly-1,sz.Width+6,sz.Height+2);



                    }



                    using(Brush textBrush=new SolidBrush(Color.FromArgb(255,routeGuidanceColor))) {



                        g.DrawString(targetDisplayName,navFont,textBrush,lx,ly);



                    }



                }



                // Distance readout at midpoint of the line



                double distM;



                string distSuffix="";



                if(activeRoute!=null && activeRoute.Success) {



                    distM=activeRoute.TotalDistanceMeters;



                    distSuffix=Localization.Get("RoadSuffix");



                } else {



                    PointF actual=ToMap(position);



                    double dxM=(targetPt.X-actual.X)*1521618/100.0;



                    double dyM=(targetPt.Y-actual.Y)*1523618/100.0;



                    distM=Math.Sqrt(dxM*dxM+dyM*dyM);



                }



                string distStr=activeRoute!=null && !activeRoute.Success?RouteUnavailableText:(distM>1000?string.Format(CultureInfo.InvariantCulture,"{0:F1} km{1}",distM/1000.0,distSuffix):((int)distM)+"m"+distSuffix);



                float mx=(px+tx)/2f, my=(py+ty)/2f;



                using(Font distFont=OverlayTheme.UiFont(7f, FontStyle.Bold)) {



                    SizeF dsz=g.MeasureString(distStr,distFont);



                    using(Brush dbg=new SolidBrush(Color.FromArgb(180,10,14,18))) {



                        g.FillRectangle(dbg,mx-dsz.Width/2f-2,my-dsz.Height/2f-1,dsz.Width+4,dsz.Height+2);



                    }



                    g.DrawString(distStr,distFont,Brushes.WhiteSmoke,mx-dsz.Width/2f,my-dsz.Height/2f);



                }



            }



            // Draw player position and heading cone



            if(position!=null) {



                float x=isPlayerCentered?cx:(left+p.X*side), y=isPlayerCentered?cy:(top+p.Y*side);



                Color playerColor = (DateTime.UtcNow-updated).TotalSeconds>5 ? Color.FromArgb(Math.Max(0, playerConeColor.R - 60), Math.Max(0, playerConeColor.G - 60), Math.Max(0, playerConeColor.B - 40)) : playerConeColor;



                // Draw FOV / Directional heading cone if enabled



                if(showHeading) {
                    double drawYaw = motion!=null && motion.Point.X>0 ? motion.Yaw : position.Yaw;
                    float yawDeg = (float)(drawYaw - 180.0);
                    GraphicsState gs = g.Save();
                    try {
                        g.TranslateTransform(x, y);
                        g.RotateTransform(yawDeg);
                        PathGradientBrush pgb = GetConeBrush(playerColor);
                        g.FillPath(pgb, ConeBasePath);
                    } finally {
                        g.Restore(gs);
                    }
                }



                // Player dot and outline



                using(Brush b=new SolidBrush(playerColor)) {



                    g.FillEllipse(Brushes.Black,x-7,y-7,14,14);



                    g.FillEllipse(b,x-5,y-5,10,10);



                }



            }



            // Draw cardinal compass indicators (N, S, E, W) around map perimeter



            if(showCompass) {



                using(Font compFont=OverlayTheme.UiFont(8.5f, FontStyle.Bold))



                using(Brush compBrush=new SolidBrush(Color.FromArgb(200,Color.WhiteSmoke)))



                using(Brush nBrush=new SolidBrush(Color.FromArgb(230,Color.Coral))) {











                    g.DrawString("N",compFont,nBrush,cx-5,bounds.Top+4);



                    g.DrawString("S",compFont,compBrush,cx-5,bounds.Bottom-18);



                    g.DrawString("W",compFont,compBrush,bounds.Left+5,cy-7);



                    g.DrawString("E",compFont,compBrush,bounds.Right-16,cy-7);



                }



            }



        }



        void DrawLocationHistory(Graphics g,float left,float top,float side) {
            if(!locationHistory || locationHistoryPoints.Count<2 || side<=0) return;
            if(locationHistoryPath==null || locationHistoryPathRevision!=locationHistoryRevision) {
                if(locationHistoryPath!=null) locationHistoryPath.Dispose();
                PointF[] points=new PointF[locationHistoryPoints.Count];
                for(int i=0;i<points.Length;i++) points[i]=locationHistoryPoints[i].MapPoint;
                locationHistoryPath=new GraphicsPath();
                locationHistoryPath.AddLines(points);
                locationHistoryPathRevision=locationHistoryRevision;
            }
            GraphicsState pathState=g.Save();
            try {
                g.TranslateTransform(left,top);
                g.ScaleTransform(side,side);
                using(Pen trail=new Pen(Color.FromArgb(205,routeHistoryColor),2.2f/side) { LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round })
                    g.DrawPath(trail,locationHistoryPath);
            } finally { g.Restore(pathState); }
            if(!fullMapActive || !locationHistoryTimestamps || !locationHistoryHoverPoint.HasValue) return;
            PointF hover;
            int segment=FindLocationHistoryHoverSegment(locationHistoryHoverPoint.Value,left,top,side,out hover);
            displayedLocationHistorySegment=segment;
            if(segment<0) return;
            using(Font font=OverlayTheme.UiFont(9f, FontStyle.Bold))
            using(Brush background=new SolidBrush(Color.FromArgb(155,8,14,20)))
            using(Brush foreground=new SolidBrush(Color.FromArgb(230,225,245,255))) {
                string age=FormatHistoryAge(DateTime.UtcNow-locationHistoryPoints[segment].SampledAtUtc);
                SizeF size=g.MeasureString(age,font);
                float x=hover.X+8;
                if(x+size.Width+4>overlay.Height) x=hover.X-size.Width-12;
                float y=hover.Y-size.Height-6;
                if(y<0) y=hover.Y+6;
                g.FillRectangle(background,x,y,size.Width+4,size.Height+2);
                g.DrawString(age,font,foreground,x+2,y+1);
            }
        }

        int FindLocationHistoryHoverSegment(Point mouse,float left,float top,float side,out PointF closest) {
            closest=PointF.Empty;
            if(!locationHistory || locationHistoryPoints.Count<2 || side<=0) return -1;
            float bestDistanceSquared=49f; // Seven screen pixels around the drawn trail.
            int bestSegment=-1;
            for(int i=1;i<locationHistoryPoints.Count;i++) {
                PointF a=locationHistoryPoints[i-1].MapPoint,b=locationHistoryPoints[i].MapPoint;
                float ax=left+a.X*side,ay=top+a.Y*side;
                float bx=left+b.X*side,by=top+b.Y*side;
                float dx=bx-ax,dy=by-ay;
                float lengthSquared=dx*dx+dy*dy;
                float t=lengthSquared>0 ? Math.Max(0f,Math.Min(1f,((mouse.X-ax)*dx+(mouse.Y-ay)*dy)/lengthSquared)) : 0f;
                float x=ax+t*dx,y=ay+t*dy;
                float offsetX=mouse.X-x,offsetY=mouse.Y-y;
                float distanceSquared=offsetX*offsetX+offsetY*offsetY;
                if(distanceSquared>=bestDistanceSquared) continue;
                bestDistanceSquared=distanceSquared;
                bestSegment=i;
                closest=new PointF(x,y);
            }
            return bestSegment;
        }

        void UpdateLocationHistoryHover(Point? point) {
            locationHistoryHoverPoint=point;
            int segment=-1;
            if(point.HasValue && fullMapActive && locationHistory && locationHistoryTimestamps && overlay.Height>0 &&
                point.Value.X>=0 && point.Value.X<overlay.Height && point.Value.Y>=0 && point.Value.Y<overlay.Height) {
                float side=overlay.Height*fullMapZoom;
                float left=overlay.Height/2f-side*fullMapPan.X;
                float top=overlay.Height/2f-side*fullMapPan.Y;
                PointF closest;
                segment=FindLocationHistoryHoverSegment(point.Value,left,top,side,out closest);
            }
            if(segment==displayedLocationHistorySegment) return;
            displayedLocationHistorySegment=segment;
            lastFrameKey=null;
            RenderOverlay();
        }

        static string FormatHistoryAge(TimeSpan age) {
            if(age.TotalSeconds<60) return Localization.T("HistorySecondsAgo",Math.Max(0,(int)age.TotalSeconds/LocationHistoryTimestampIntervalSeconds)*LocationHistoryTimestampIntervalSeconds);
            if(age.TotalMinutes<60) return Localization.T("HistoryMinutesAgo",(int)age.TotalMinutes);
            if(age.TotalHours<24) return Localization.T("HistoryHoursAgo",(int)age.TotalHours);
            return Localization.T("HistoryDaysAgo",(int)age.TotalDays);
        }

        void DrawGrid(Graphics g,float left,float top,float side) {



            if((!gridBorders && !gridLabels) || gridOpacity==0)return;



            // Sector boundaries from the same map source as the coordinate projection.



            double[] cols={617505,313373,9241,-294891,-599023,-903155};



            double[] rows={617953,313597,9241,-295115,-599471,-903827};



            float[] xs=new float[6],ys=new float[6];



            for(int i=0;i<6;i++) { xs[i]=left+(float)((617718-cols[i])/1521618)*side; ys[i]=top+(float)((618618-rows[i])/1523618)*side; }



            int alpha=gridOpacity*255/100;



            if(gridBorders) using(Pen pen=new Pen(Color.FromArgb(alpha,235,241,245),1)) {



                for(int i=0;i<6;i++) { g.DrawLine(pen,xs[i],ys[0],xs[i],ys[5]); g.DrawLine(pen,xs[0],ys[i],xs[5],ys[i]); }



            }



            if(gridLabels) using(Font font=new Font("Segoe UI",Math.Max(9,Math.Min(14,side/32)),FontStyle.Bold)) {



                string[] letters={"D","C","B","A","Z"};



                for(int r=0;r<5;r++)for(int c=0;c<5;c++) {



                    string label=letters[r]+(4-c); float x=xs[c]+5,y=ys[r]+4;



                    SizeF size=g.MeasureString(label,font);



                    using(Brush b=new SolidBrush(Color.FromArgb(alpha*160/255,10,15,20)))g.FillRectangle(b,x-2,y,size.Width+4,size.Height);



                    using(Brush b=new SolidBrush(Color.FromArgb(alpha,Color.WhiteSmoke)))g.DrawString(label,font,b,x,y);



                }



            }



        }



        void UpdateRouteAsync(PointF playerPt, MapZone target) { RequestRouteAsync(playerPt,target,false); }
        bool RequestRouteAsync(PointF playerPt, MapZone target,bool force) {
            if(closing || IsDisposed) return false;
            if(target==null || !RoadRouter.Instance.IsLoaded) { activeRoute=null; routeGeneration++; lastRouteTarget=null; return false; }
            if(!Object.ReferenceEquals(lastRouteTarget,target)) activeRoute=null;
            if(!force && lastRouteTarget==target && lastRoutePlayerPt!=PointF.Empty && RoadRouter.DistanceMeters(lastRoutePlayerPt,playerPt)<25 && activeRoute!=null) return false;
            if(routeCalculating) return false;
            // Ensure a UI handle exists before queueing; async continuations in diagnostic
            // hosts do not necessarily have a WinForms synchronization context.
            IntPtr uiHandle=Handle;
            routeCalculating=true;
            long request=routeGeneration;
            PointF targetPoint=target.Centroid;
            RoadRouteMode requestedMode=wpfRouteMode;
            ThreadPool.QueueUserWorkItem(state=> {
                RoadRoute result=null; Exception failure=null;
                try { result=RoadRouter.Instance.FindRoute(playerPt,targetPoint,requestedMode); }
                catch(Exception ex) { failure=ex; }
                try {
                    if(IsDisposed) return;
                    BeginInvoke((Action)(()=> {
                        routeCalculating=false;
                        if(wpfQueuedTarget!=null && request!=routeGeneration) { var queued=wpfQueuedTarget;wpfQueuedTarget=null;if(Object.ReferenceEquals(searchTarget,queued)&&position!=null)UpdateRouteAsync(ToMap(position),queued); }
                        if(failure!=null) { Program.LogException("Route",failure); if(force) FinishVoiceReroute(false); return; }
                        if(closing || IsDisposed || request!=routeGeneration || !Object.ReferenceEquals(searchTarget,target) || target.Centroid!=targetPoint) return;
                        activeRoute=result; lastRouteTarget=target; lastRoutePlayerPt=playerPt;
                        if(force) FinishVoiceReroute(result!=null && result.Success);
                        lastFrameKey=null;
                        if(SettingsVisible) canvas.Invalidate();
                        RenderOverlay();
                    }));
                } catch(InvalidOperationException) { /* Window closed while the result was queued. */ }
            });
            return true;
        }

        static bool DestinationReached(MapZone target,PointF current) {



            return target!=null && DestinationSearch.Metres(current,target.Centroid)<=30;



        }



    }

}
