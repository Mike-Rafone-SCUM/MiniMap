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



using System.Drawing.Drawing2D;



[assembly: AssemblyTitle("SkynettMiniMap")]



[assembly: AssemblyDescription("Tactical Real-Time Overlay for SCUM")]



[assembly: AssemblyCompany("MikeRafone")]



[assembly: AssemblyProduct("SkynettMiniMap")]



[assembly: AssemblyCopyright("Copyright Â© 2026 MikeRafone")]



[assembly: AssemblyVersion("1.4.4.0")]



[assembly: AssemblyFileVersion("1.4.4.0")]



[assembly: AssemblyInformationalVersion("1.4.4")]



namespace ScumMiniMap {



    public sealed class Position {



        public double X, Y, Z, Yaw;



        static readonly Regex Format = new Regex(@"^\s*\{X=(?<x>-?\d+(?:\.\d+)?)\s+Y=(?<y>-?\d+(?:\.\d+)?)\s+Z=(?<z>-?\d+(?:\.\d+)?)\|P=(?<p>-?\d+(?:\.\d+)?)\s+Y=(?<yaw>-?\d+(?:\.\d+)?)\s+R=(?<r>-?\d+(?:\.\d+)?)\}\s*$", RegexOptions.CultureInvariant);



        public static Position Parse(string text) {



            if (text == null || text.Length > 512) return null;



            Match m = Format.Match(text);



            if (!m.Success) return null;



            double x, y, z, yaw;



            if (!Read(m,"x",out x) || !Read(m,"y",out y) || !Read(m,"z",out z) || !Read(m,"yaw",out yaw)) return null;



            if (Math.Abs(x)>2000000 || Math.Abs(y)>2000000 || Math.Abs(z)>2000000 || Math.Abs(yaw)>360) return null;



            return new Position { X=x,Y=y,Z=z,Yaw=yaw };



        }



        static bool Read(Match m,string key,out double value) {



            return double.TryParse(m.Groups[key].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !double.IsInfinity(value) && !double.IsNaN(value);



        }



    }



    enum CopyResult { Sent, Cancelled, Failed }



    static class Native {



        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();



        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);



        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);



        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();



        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);



        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);



        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);

        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();



        [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] inputs,int size);



        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }



        [StructLayout(LayoutKind.Explicit)] struct UNION {



            [FieldOffset(0)] public KEY keyboard;



            [FieldOffset(0)] public MOUSE mouse;



        }



        [StructLayout(LayoutKind.Sequential)] struct KEY { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }



        [StructLayout(LayoutKind.Sequential)] struct MOUSE { public int x,y; public uint data,flags,time; public UIntPtr extra; }



        static uint cachedPid;



        static bool cachedGame;



        static long cacheUntil;



        static readonly uint currentProcessId=ReadCurrentProcessId();



        internal static DateTime LastAltTabTime = DateTime.MinValue;
        static uint knownGamePid = 0;
        static readonly int[] busyKeys={
            0x02,             // Right mouse
            0x10, 0xA0, 0xA1, // Shift, LShift, RShift
            0x11, 0xA2, 0xA3, // Ctrl, LCtrl, RCtrl
            0x12, 0xA4, 0xA5, // Alt, LAlt, RAlt
            0x43,             // 'C'
            0x5B, 0x5C,       // LWin, RWin
            0x09,             // Tab
            0x20,             // Space
            0x45,             // 'E'
            0x46,             // 'F'
            0x52              // 'R'
        };

        static uint ReadCurrentProcessId() {
            using(Process process=Process.GetCurrentProcess()) return (uint)process.Id;
        }

        internal static bool IsRightMouseDown() {
            return (GetAsyncKeyState(0x02)&0x8000)!=0;
        }

        internal static bool GameFocused() {
            IntPtr fg=GetForegroundWindow();
            if(fg==IntPtr.Zero) return false;
            uint pid; GetWindowThreadProcessId(fg,out pid);
            // Process identity is revalidated after the short foreground cache expires.
            long now=Stopwatch.GetTimestamp();
            if(pid==cachedPid && now<cacheUntil)return cachedGame;
            cachedPid=pid; cacheUntil=now+(Stopwatch.Frequency/4); // 250ms cache for instant focus detection
            try { using (Process p=Process.GetProcessById((int)pid)) {
                cachedGame=p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);
                if(cachedGame) knownGamePid=pid;
                return cachedGame;
            }} catch { cachedGame=false; }
            return cachedGame;
        }

        internal static bool IsAltOrTabOrWinDown() {
            return (GetAsyncKeyState(0x12)&0x8000)!=0
                || (GetAsyncKeyState(0xA4)&0x8000)!=0
                || (GetAsyncKeyState(0xA5)&0x8000)!=0
                || (GetAsyncKeyState(0x09)&0x8000)!=0
                || (GetAsyncKeyState(0x5B)&0x8000)!=0
                || (GetAsyncKeyState(0x5C)&0x8000)!=0;
        }

        internal static void EmergencyReleaseModifier(int modifierKey=0xA2) {
            if(modifierKey<=0) return;
            try { Key((uint)modifierKey, true); } catch {}
        }



        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);



        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd,IntPtr hWndInsertAfter,int X,int Y,int cx,int cy,uint uFlags);



        internal static void ForceForeground(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window)) return;



            SetWindowPos(window,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0040); // HWND_TOPMOST, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW



            SetForegroundWindow(window);



        }



        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);



        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);



        internal static bool IsOurWindow(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window)) return false;



            uint pid; GetWindowThreadProcessId(window,out pid);



            return pid==currentProcessId;



        }



        internal static bool IsGameWindow(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window) || IsIconic(window)) return false;



            uint pid; GetWindowThreadProcessId(window,out pid);



            try { using(Process p=Process.GetProcessById((int)pid)) {



                return p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);



            } } catch { return false; }



        }



        internal static bool CopyInProgress;



        internal static bool KeysBusy(int copyModifierKey=0xA2,int copyKey=0x43) {



            if(UserTypingOrActive()) return true;



            if(IsCursorVisible()) return true;



            foreach(int k in busyKeys) if ((GetAsyncKeyState(k)&0x8000)!=0) return true;



            if(copyModifierKey>0 && copyModifierKey<256 && (GetAsyncKeyState(copyModifierKey)&0x8000)!=0) return true;



            if(copyKey>0 && copyKey<256 && (GetAsyncKeyState(copyKey)&0x8000)!=0) return true;



            return false;



        }



        [DllImport("user32.dll")] internal static extern bool GetCursorInfo(out CURSORINFO pci);



        [StructLayout(LayoutKind.Sequential)] internal struct CURSORINFO {



            public int cbSize;



            public int flags;



            public IntPtr hCursor;



            public POINT ptScreenPos;



        }



        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x,y; }



        const int CURSOR_SHOWING = 0x00000001;



        internal static bool IsCursorVisible() {



            try {



                CURSORINFO ci=new CURSORINFO();



                ci.cbSize=Marshal.SizeOf(typeof(CURSORINFO));



                if(GetCursorInfo(out ci)) {



                    return (ci.flags & CURSOR_SHOWING) != 0;



                }



            } catch {}



            return false;



        }



        internal static bool UserTypingOrActive() {



            return GameKeys.UserActiveRecently(90);



        }



        internal static string CopyError="";



        static bool Key(uint vk,bool up) {



            INPUT input=new INPUT(); input.type=1;



            input.data.keyboard.vk=(ushort)vk;



            input.data.keyboard.scan=(ushort)MapVirtualKey(vk,0);



            input.data.keyboard.flags=8u|(up?2u:0u);



            bool ok=SendInput(1,new INPUT[]{input},Marshal.SizeOf(typeof(INPUT)))==1;



            if(!ok) CopyError="SendInput failed (Windows error "+Marshal.GetLastWin32Error()+").";



            return ok;



        }



        internal static async Task<CopyResult> Copy(Func<bool> allowed,int copyModifierKey=0xA2,int copyKey=0x43) {



            CopyError="";



            if(CopyInProgress || !allowed() || !GameFocused() || KeysBusy(copyModifierKey,copyKey) || IsAltOrTabOrWinDown()) { CopyError="Focus, chat, or modifier keys detected."; return CopyResult.Cancelled; }



            IntPtr game=GetForegroundWindow();



            CopyInProgress=true;



            try {



                bool sent=await CopyChord(Key,Task.Delay,()=>allowed() && GetForegroundWindow()==game && GameFocused()
                    && !UserTypingOrActive() && !IsCursorVisible() && !IsRightMouseDown() && !IsAltOrTabOrWinDown(),
                    copyModifierKey,copyKey);
                return ClassifyCopy(sent,CopyError);
            } finally { CopyInProgress=false; }
        }

        internal static CopyResult ClassifyCopy(bool sent,string error) {
            return sent?CopyResult.Sent:(string.IsNullOrEmpty(error)?CopyResult.Cancelled:CopyResult.Failed);
        }

        // Leave Control down across game frames on both sides of C. Never send C
        // after a focus/user-input change, and retain failed key-ups for cleanup.
        internal static async Task<bool> CopyChord(Func<uint,bool,bool> key,Func<int,Task> delay,Func<bool> canPressC,int modifierKey=0xA2,int copyKey=0x43) {
            bool ctrl=false,c=false,ok=false;
            try {
                if(modifierKey>0) {
                    ctrl=key((uint)modifierKey,false);
                    if(ctrl) {
                        await delay(60);
                        if(canPressC()) {
                            c=key((uint)copyKey,false);
                            if(c) {
                                await delay(30);
                                ok=key((uint)copyKey,true);
                                c=!ok;
                            }
                            await delay(80);
                        }
                    }
                } else {
                    if(canPressC()) {
                        c=key((uint)copyKey,false);
                        if(c) {
                            await delay(30);
                            ok=key((uint)copyKey,true);
                            c=!ok;
                        }
                        await delay(80);
                    }
                }



            } finally {



                if(c) { bool released=key((uint)copyKey,true); c=!released; ok=false; }



                // A second cleanup attempt precedes releasing the modifier.



                if(c) { key((uint)copyKey,true); ok=false; }



                if(ctrl && !key((uint)modifierKey,true)) { key((uint)modifierKey,true); ok=false; }



            }



            return ok;



        }



    }



    sealed class MapCanvas:Panel { public MapCanvas() { DoubleBuffered=true; ResizeRedraw=true; } }



    public sealed class MapWindow:Form {



        readonly MapCanvas canvas=new MapCanvas();



        bool TrackingEnabled { get { return !diagnosticMode && !closing; } }



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



        double filteredSpeed;



        readonly Image map;



        readonly List<Bitmap> mapLevels=new List<Bitmap>();



        Bitmap overlayFrame=new Bitmap(400,240,PixelFormat.Format32bppPArgb);



        Bitmap fullMap=new Bitmap(400,240,PixelFormat.Format32bppPArgb);



        bool gridLabels=true,gridBorders=true,edgeFade=true,showStatus=true,autoZoom=true;



        bool showHeading=true,showCompass=true,showElevation=false,zoneChime=false;



        string statusPos="Below";



        string overlayShape="Circle";



        int copyIntervalMs=1000;



        int mapOpacity=70;



        int gridOpacity=7;



        int labelSize=6;



        bool showZones=false;



        bool showGasStations=false;



        bool showCities=false;



        bool showTowns=true;



        bool showFarms=false;



        bool showTraders=false;



        bool showFactions=false;



        bool showMilitary=false;



        bool showBunkers=false;



        bool showCustomWaypoints=true;



        bool showZoneLabels=true;



        bool smartLabelLod=true;



        bool fullMapActive;



        DateTime fullMapOpenedAt=DateTime.MinValue;



        float fullMapZoom=1.0f;



        PointF fullMapPan=new PointF(0.5f,0.5f);



        int fullMapOpacity=100;



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



        bool showHuntingLegend = true;



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

        Color routeGuidanceColor = Color.FromArgb(255, 159, 28);
        Color playerConeColor = Color.FromArgb(255, 174, 51);
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
        readonly System.Text.StringBuilder frameKeyBuilder = new System.Text.StringBuilder(256);
        bool copyInProgress;



        // These are the SCUM bindings used by MiniMap. They default to SCUM's standard



        // Map (M), Chat (T), and coordinate-copy (Ctrl+C) bindings, but can be captured



        // from the user's controls in the key rebinding wizard.



        int scumMapKey=0x4D;



        int scumChatKey=0x54;



        int scumCopyModifierKey=0xA2;



        int scumCopyKey=0x43;



        Form keyWizardForm;



        Label keyWizardStatus,keyWizardMapValue,keyWizardChatValue,keyWizardCopyModifierValue,keyWizardCopyValue;



        CheckBox keyWizardNoModifierCheck;



        Button keyWizardModifierCapture;



        int keyWizardCaptureTarget;



        int keyWizardMapKey,keyWizardChatKey,keyWizardCopyModifierKey,keyWizardCopyKey;



        RoadRoute activeRoute;



        PointF lastRoutePlayerPt = PointF.Empty;



        MapZone lastRouteTarget;



        bool routeCalculating;
        long routeGeneration;



        List<MapZone> zones=new List<MapZone>();



        ScumMapStore scumMap;



        bool showScumMap=true;



        string pendingDisabledCats="v2;-2008,-2007,-2006,-2005,-2004,-2003,-2002,-2001,-2000,-1014,-1013,-1012,-1011,-1010,-1009,-1008,-1007,-1006,-1005,-1004,-1003,-1002,-1001,-1000,4,5,6,7,9,12,13,14,17,19,20,21,22,23,24,25,28,29,30,32,34,36,37,38,39,40,41,42,43,44,45,47,48,49,50,52,53,54,55,56,57,58,59,60,61,62,63,64,78,81,210,250,251,252,279,320,421,642,643,712,761,762,774,776,777,778,789,790,803,858,859,860,861,862,863,864,865,866,867,868,869,871,872,873,874";



        int savedWidth=300,savedHeight=300,savedLeft=-1,savedTop=-1;



        readonly string dataFolder;



        readonly string zonesPath;
        DateTime lastZonesFileWriteTimeUtc = DateTime.MinValue;



        readonly string settingsPath;



        bool isFirstLaunch=false;



        DateTime saveAfter=DateTime.MaxValue;



        NumericUpDown widthOption,heightOption;



        string lastFrameKey;



        // The location label is used both to decide whether a frame changed and to draw the HUD.



        // Keep the expensive zone lookup stable while smooth-motion frames are being rendered.



        string cachedLocationDescription;



        Position cachedLocationPosition;



        MapZone cachedLocationTarget;



        RoadRoute cachedLocationRoute;



        bool cachedLocationShowElevation;



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



        float zoom=32f;



        int maxZoom=32;



        int autoZoomMin=5;



        int autoZoomMax=32;



        int zoomStepPercent=10;



        int failures;



        int attempts,responses;



        bool wasGameFocused;



        bool hiddenByFocusLoss;
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



            zonesPath=Path.Combine(folder,"zones.tsv");



            LoadSettings();



            note=Localization.Get("NoteAutoWaiting");



            try {
                zones=ZoneStore.Load(zonesPath);
                if(File.Exists(zonesPath)) lastZonesFileWriteTimeUtc = File.GetLastWriteTimeUtc(zonesPath);
            } catch(InvalidDataException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); } catch(IOException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); } catch(UnauthorizedAccessException) { zonesLoadFailed=true; note=Localization.Get("NoteZonesLoadFail"); }



            try { scumMap=ScumMapStore.Load(dataFolder); } catch { scumMap=new ScumMapStore(); }



            if(scumMap!=null) {



                scumMap.MasterEnabled=showScumMap;



                if(pendingDisabledCats!=null) scumMap.ApplyDisabledCategoriesString(pendingDisabledCats);



            }



            Text=Localization.T("HeaderSettings",VersionString); TopMost=true; ClientSize=new Size(450,640); MinimumSize=new Size(450,480);



            BackColor=OverlayTheme.Background; ForeColor=OverlayTheme.Ink;



            Font=new Font("Segoe UI",9); StartPosition=FormStartPosition.Manual;



            Location=new Point(Screen.PrimaryScreen.WorkingArea.Right-450,80);



            string mapFile=Path.Combine(folder,"map.png");



            string mapWarning;
            map=SafeMapImage.Load(mapFile,()=>System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("map.png"),out mapWarning);
            if(mapWarning!=null && !diagnosticMode) Shown+=(sender,e)=>MessageBox.Show(mapWarning,Localization.Get("ZoneEditorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Warning);

            Image level=map;



            while(level.Width>512 && level.Height>512) {



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



            overlay.OnFullMapMouseUp = HandleFullMapMouseUp;



            overlay.OnFullMapAddWaypoint = AddCustomWaypointAtScreenPoint;

            overlay.OnRouteColorRequested = ChangeRouteColor;
            overlay.OnPlayerColorRequested = ChangePlayerColor;



            overlay.CanAddFullMapWaypoint = () => pendingFullMapWaypointMapPoint.HasValue;



            overlay.CanShowFullMapContextMenu = () => fullMapActive && !closing && !IsDisposed && !Native.CopyInProgress;



            // A global low-level hook is not guaranteed to run on this form's UI thread.



            // Marshal it before touching WinForms so a hotkey cannot leave a dialog or



            // overlay in a half-open state after a thread-affinity exception.



            keys=new GameKeys(key => {



                if(closing || IsDisposed || !IsHandleCreated) return;



                try { BeginInvoke((Action)(()=>OnGameKey(key))); } catch(InvalidOperationException) { }



            }, IsWatchedGameKey, chat, () => scumChatKey);



            tray=new NotifyIcon { Icon=SystemIcons.Application,Text="SkynettMiniMap v"+VersionString,Visible=true };



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
            status.Font=new Font("Segoe UI",7.8f);
            status.ForeColor=OverlayTheme.InkMuted;
            status.BackColor=OverlayTheme.Surface;

            FlowLayoutPanel footerActions=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=32,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty };
            settingsDoneBtn.Text=Localization.Get("Done");
            settingsDoneBtn.Width=100;
            settingsDoneBtn.Height=28;
            settingsDoneBtn.Click+=(s,e)=>DismissSettings();

            settingsOpenFolderBtn.Text=Localization.Get("OpenDataFolder");
            settingsOpenFolderBtn.Width=140;
            settingsOpenFolderBtn.Height=28;
            settingsOpenFolderBtn.Click+=(s,e)=> {
                try {
                    if(!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
                    Process.Start(new ProcessStartInfo { FileName="explorer.exe",Arguments="\""+dataFolder+"\"",UseShellExecute=true });
                } catch(Exception ex) {
                    MessageBox.Show(this,Localization.T("DataFolderOpenError",ex.Message));
                }
            };

            footerActions.Controls.Add(settingsDoneBtn);
            footerActions.Controls.Add(settingsOpenFolderBtn);
            settingsFooter.Controls.Add(footerActions);
            settingsFooter.Controls.Add(status);

            BuildSettingsPanel();

            canvas.Dock=DockStyle.Fill; canvas.BackColor=OverlayTheme.Background; canvas.Paint+=PaintMap;

            Controls.Add(bar); Controls.Add(settingsFooter);



            OverlayTheme.Frame(this,Localization.T("HeaderSettings",VersionString),DismissSettings);



            KeyDown+=(s,e)=> {



                if(e.KeyCode==Keys.Home) {



                    if((DateTime.UtcNow - lastHomeAction).TotalMilliseconds > 450) {



                        lastHomeAction = DateTime.UtcNow;



                        DismissSettings();



                        e.Handled=true; e.SuppressKeyPress=true;



                    }



                } else if(e.KeyCode==Keys.Escape) {



                    DismissSettings();



                    e.Handled=true; e.SuppressKeyPress=true;



                }



            };



            overlay.SizeChanged+=(s,e)=> {



                widthOption.Value=Math.Max(240,Math.Min(800,overlay.Width));



                heightOption.Value=Math.Max(240,Math.Min(800,overlay.Height));



                SettingsChanged();



            };



            overlay.LocationChanged+=(s,e)=> { saveAfter=DateTime.UtcNow.AddMilliseconds(700); };



            sequence=Native.GetClipboardSequenceNumber();



            try { if(Clipboard.ContainsText()) Accept(Clipboard.GetText()); } catch(ExternalException) {}



            timer.Interval=33; timer.Tick+=Tick; timer.Start();



            System.Threading.ThreadPool.QueueUserWorkItem(_ => { RoadRouter.Instance.InitializeFromResource(); });



            Shown+=(s,e)=> {



                Hide();



                bool focused=Native.GameFocused() || Native.IsOurWindow(Native.GetForegroundWindow());



                wasGameFocused=focused;



                overlay.SetGameFocus(Native.GameFocused());



                if(!diagnosticMode) { failures=0; next=DateTime.UtcNow; }



                overlay.Show();



                RenderOverlay();



                if(isFirstLaunch && !diagnosticMode) {

                    OpenStartupGuide();

                }



                if(!diagnosticMode && string.IsNullOrEmpty(Program.LocalUpdateSource)) {



                    Task.Delay(3500).ContinueWith(t => {



                        try { BeginInvoke(new Action(() => CheckForUpdates(false))); } catch {}



                    });



                }



            };



            FormClosing+=(s,e)=> {



                if(Visible && !closing && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; DismissSettings(); return; }



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
            trayMenu.Items.Add(Localization.Get("FullMap"),null,(s,e)=>TriggerFullMap());
            trayMenu.Items.Add(Localization.Get("SearchPlaceOrGrid"),null,(s,e)=>ShowZoneSearch());
            trayMenu.Items.Add(Localization.Get("TrayCheckUpdates"),null,(s,e)=>CheckForUpdates(true));
            trayMenu.Items.Add(Localization.Get("JoinDiscord"),null,(s,e)=>{ try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName="https://discord.gg/MYzcGaFDMn", UseShellExecute=true }); } catch {} });
            trayMenu.Items.Add(Localization.Get("TrayShowHide"),null,(s,e)=>ToggleOverlay());
            trayMenu.Items.Add(Localization.Get("TrayExit"),null,(s,e)=>ExitApp());

            OverlayTheme.Menu(trayMenu);
        }

        static void DisposeChildControls(Control parent) {
            if(parent==null) return;
            for(int i=parent.Controls.Count-1; i>=0; i--) {
                Control child=parent.Controls[i];
                parent.Controls.RemoveAt(i);
                DisposeChildControls(child);
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
            langCombo.Items.AddRange(new object[] {
                "English",
                "Español (Argentina)",
                "Français",
                "Deutsch",
                "Nederlands",
                "Русский",
                "中文 (简体)",
                "Türkçe",
                "العربية"
            });
            switch (Localization.Current) {
                case AppLanguage.SpanishArgentina: langCombo.SelectedIndex = 1; break;
                case AppLanguage.French: langCombo.SelectedIndex = 2; break;
                case AppLanguage.German: langCombo.SelectedIndex = 3; break;
                case AppLanguage.Dutch: langCombo.SelectedIndex = 4; break;
                case AppLanguage.Russian: langCombo.SelectedIndex = 5; break;
                case AppLanguage.Chinese: langCombo.SelectedIndex = 6; break;
                case AppLanguage.Turkish: langCombo.SelectedIndex = 7; break;
                case AppLanguage.Arabic: langCombo.SelectedIndex = 8; break;
                default: langCombo.SelectedIndex = 0; break;
            }
            langCombo.SelectedIndexChanged += (s, e) => {
                AppLanguage newLang;
                switch (langCombo.SelectedIndex) {
                    case 1: newLang = AppLanguage.SpanishArgentina; break;
                    case 2: newLang = AppLanguage.French; break;
                    case 3: newLang = AppLanguage.German; break;
                    case 4: newLang = AppLanguage.Dutch; break;
                    case 5: newLang = AppLanguage.Russian; break;
                    case 6: newLang = AppLanguage.Chinese; break;
                    case 7: newLang = AppLanguage.Turkish; break;
                    case 8: newLang = AppLanguage.Arabic; break;
                    default: newLang = AppLanguage.English; break;
                }
                if (Localization.Current != newLang) {
                    Localization.Current = newLang;
                    Text = Localization.T("HeaderSettings", VersionString);
                    BuildTrayMenu();
                    BuildSettingsPanel();
                    terrainKey = null;
                    lastFrameKey = null;
                    if (Visible) canvas.Invalidate();
                    SettingsChanged();
                    RenderOverlay();
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

            // 2. GRID & OVERLAY
            OverlayTheme.Section(bar, Localization.Get("SecMapZones"));
            AddCheck(bar, Localization.Get("GridLabels"), gridLabels, value => gridLabels = value);
            AddCheck(bar, Localization.Get("GridBorders"), gridBorders, value => gridBorders = value);
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
            AddCheck(bar, Localization.Get("ShowCustomWaypoints"), showCustomWaypoints, value => { showCustomWaypoints = value; SettingsChanged(); });
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

            AddCheck(bar, Localization.Get("ShowScumMap"), showScumMap, value => {
                showScumMap = value;
                if (scumMap != null) scumMap.MasterEnabled = value;
                SettingsChanged();
            });
            Button poiFilterButton = new Button { Text = Localization.Get("ConfigurePoiFilters"), Width = 280 };
            poiFilterButton.Click += (s, e) => {
                if (scumMap == null) return;
                using (PoiFilterDialog dlg = new PoiFilterDialog(scumMap, () => {
                    SettingsChanged();
                    if (Visible) canvas.Invalidate();
                    RenderOverlay();
                })) {
                    dlg.ShowDialog(this);
                }
            };
            bar.Controls.Add(poiFilterButton);

            AddCheck(bar, Localization.Get("ShowSavedZones"), showZones, value => { showZones = value; SettingsChanged(); });
            bar.Controls.Add(new Label { Text = Localization.Get("CustomZonesDescription"), Width = 365, Height = 42, AutoSize = false, ForeColor = OverlayTheme.InkMuted, Margin = new Padding(3, -2, 3, 4) });
            Button zoneButton = new Button { Text = Localization.Get("MapZonesScreenshot"), Width = 280 };
            zoneButton.Click += (s, e) => OpenZoneImport(null);
            bar.Controls.Add(zoneButton);
            Button clearZonesBtn = new Button { Text = Localization.Get("ClearAllCustomZones"), Width = 280 };
            clearZonesBtn.Click += (s, e) => {
                if (zones == null || zones.Count == 0) {
                    MessageBox.Show(this, Localization.Get("NoCustomZonesToDelete"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (MessageBox.Show(this, Localization.T("ClearAllZonesConfirm", zones.Count), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                    if(!TryCommitZones(new List<MapZone>(),this)) return;
                    searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null;
                    SettingsChanged();
                    RenderOverlay();
                    MessageBox.Show(this, Localization.Get("AllZonesDeletedDone"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            bar.Controls.Add(clearZonesBtn);

            AddCheck(bar, Localization.Get("ShowZoneLabels"), showZoneLabels, value => { showZoneLabels = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("SmartLabelLod"), smartLabelLod, value => { smartLabelLod = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowHuntingLegend"), showHuntingLegend, value => { showHuntingLegend = value; SettingsChanged(); });
            AddNumber(bar, Localization.Get("ZoneLabelSize"), 6, 24, labelSize, value => labelSize = value);

            // 4. DISPLAY & HUD LAYOUT
            OverlayTheme.Section(bar, Localization.Get("SecAppearance"));
            widthOption = AddNumber(bar, Localization.Get("MapWidth"), 240, 800, overlay.Width, value => overlay.Width = value);
            heightOption = AddNumber(bar, Localization.Get("MapHeight"), 240, 800, overlay.Height, value => overlay.Height = value);
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
            AddCheck(bar, Localization.Get("FadeEdges"), edgeFade, value => edgeFade = value);
            AddCheck(bar, Localization.Get("ShowHeading"), showHeading, value => showHeading = value);
            AddCheck(bar, Localization.Get("ShowCompass"), showCompass, value => showCompass = value);
            AddCheck(bar, Localization.Get("ShowElevation"), showElevation, value => showElevation = value);
            AddCheck(bar, Localization.Get("ZoneChime"), zoneChime, value => zoneChime = value);

            // 5. GPS TRACKING & AUTO-ZOOM
            OverlayTheme.Section(bar, Localization.Get("SecTrackingZoom"));
            AddCheck(bar, Localization.Get("AutoZoomSpeed"), autoZoom, value => { autoZoom = value; SettingsChanged(); });
            AddNumber(bar, Localization.Get("AutoZoomMin"), 1, 32, autoZoomMin, value => { autoZoomMin = value; autoZoomMax = Math.Max(autoZoomMax, value); SettingsChanged(); });
            AddNumber(bar, Localization.Get("AutoZoomMax"), 1, 32, autoZoomMax, value => { autoZoomMax = value; autoZoomMin = Math.Min(autoZoomMin, value); SettingsChanged(); });
            AddNumber(bar, Localization.Get("MaxZoom"), 4, 32, maxZoom, value => { maxZoom = value; zoom = Math.Min(maxZoom, zoom); targetZoom = Math.Min(maxZoom, targetZoom); SettingsChanged(); });
            AddNumber(bar, Localization.Get("ZoomStep"), 10, 100, zoomStepPercent, value => { zoomStepPercent = value; SettingsChanged(); });
            AddNumber(bar, Localization.Get("PositionInterval"), 1000, 10000, copyIntervalMs, value => { copyIntervalMs = value; next = DateTime.UtcNow; SettingsChanged(); }).Increment = 250;

            // 6. TOOLS & DATA
            OverlayTheme.Section(bar, Localization.Get("SecToolsShortcuts"));
            Button resetMapBtn = new Button { Text = Localization.Get("ResetDefaultMap"), Width = 280 };
            resetMapBtn.Click += (s, e) => {
                if (MessageBox.Show(this, Localization.Get("ResetMapConfirm"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                    try {
                        string customMap = Path.Combine(dataFolder, "map.png");
                        if (File.Exists(customMap)) File.Delete(customMap);
                        MessageBox.Show(this, Localization.Get("MapResetDone"), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) {
                        MessageBox.Show(this, Localization.T("MapResetFailed", ex.Message), Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            bar.Controls.Add(resetMapBtn);

            Button updateBtn = new Button { Text = Localization.Get("CheckUpdatesGitHub"), Width = 280 };
            updateBtn.Click += (s, e) => CheckForUpdates(true);
            bar.Controls.Add(updateBtn);

            Button discordBtn = new Button { Text = Localization.Get("JoinDiscord"), Width = 280 };
            discordBtn.Click += (s, e) => {
                try {
                    Process.Start(new ProcessStartInfo("https://discord.gg/MYzcGaFDMn") { UseShellExecute = true });
                } catch {}
            };
            bar.Controls.Add(discordBtn);

            OverlayTheme.Sections(bar);
            OverlayTheme.Style(bar);
            if(settingsFooter != null) {
                settingsDoneBtn.Text = Localization.Get("Done");
                settingsOpenFolderBtn.Text = Localization.Get("OpenDataFolder");
                OverlayTheme.Style(settingsFooter);
            }
            bar.ResumeLayout(true);
        }



        public const string VersionString = "1.4.4";



        public static readonly Version CurrentVersion = new Version(1, 4, 4, 0);



        bool updateCheckRunning;



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



                if(closing || IsDisposed) { File.Delete(staged); return; }
                await Task.Run(()=>RestartWithVerifiedUpdate(staged,installed));



            } catch {



                try { if(File.Exists(staged)) File.Delete(staged); } catch { }



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



                if(MessageBox.Show(this,Localization.T("UpdateAvailableDialogMsg",VersionString,release.VersionText,release.Filename),



                    "Local Update Test",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes) return;



                await DownloadAndInstallUpdate(service,release);



                if(closing || IsDisposed) return;



                MessageBox.Show(this,Localization.Get("UpdateReadyRestarting"),"Local Update Test",MessageBoxButtons.OK,MessageBoxIcon.Information);



                closing=true;



                Close();



            } catch(Exception ex) {



                if(!closing && !IsDisposed) MessageBox.Show(this,"Local update test failed: "+ex.Message,"Local Update Test",MessageBoxButtons.OK,MessageBoxIcon.Warning);



            } finally { updateCheckRunning=false; }



        }



        async void CheckForUpdates(bool userInitiated) {



            if(updateCheckRunning || closing || IsDisposed || diagnosticMode) return;



            updateCheckRunning = true;



            try {



                var service = new UpdateService(UpdateService.Repository,



                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScumMiniMap", "updates"), null);



                UpdateRelease release = await Task.Run(() => service.Check(userInitiated));



                if(closing || IsDisposed) return;



                bool updateAvailable = release.Version > CurrentVersion;
                if(updateAvailable) {



                    note = Localization.T("UpdateAvailableNote", release.VersionText);



                    if(MessageBox.Show(this, Localization.T("UpdateAvailableDialogMsg", VersionString, release.VersionText, release.Filename),



                        Localization.Get("UpdateAvailableDialogTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;



                    await DownloadAndInstallUpdate(service,release);



                    if(closing || IsDisposed) return;



                    note=Localization.Get("UpdateDownloadComplete");



                    MessageBox.Show(this, Localization.Get("UpdateReadyRestarting"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);



                    closing=true;



                    Close();



                } else if(userInitiated) {



                    MessageBox.Show(this, Localization.T("UpdateUpToDateMsg", VersionString, release.VersionText), Localization.Get("UpdateUpToDateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);



                }



            } catch(UpdateRetryException ex) {



                if(userInitiated && !closing && !IsDisposed) MessageBox.Show(this, Localization.T("UpdateRateLimited",ex.RetryAt.ToLocalTime().ToString("g")), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);



            } catch(System.Net.WebException) {



                if(userInitiated && !closing && !IsDisposed) MessageBox.Show(this, Localization.Get("UpdateConnectError"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);



            } catch(FormatException) {



                if(userInitiated && !closing && !IsDisposed) MessageBox.Show(this, Localization.Get("UpdateVersionUnknown"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);



            } catch(Exception ex) {



                if(userInitiated && !closing && !IsDisposed) MessageBox.Show(this, Localization.T("UpdateFailedMsg", ex.Message), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);



            } finally { updateCheckRunning = false; }



        }



        bool resourcesReleased;



        void ReleaseResources() {



            if(resourcesReleased)return; resourcesReleased=true; routeGeneration++;



            timer.Stop(); timer.Dispose();



            if(keys!=null)keys.Dispose();



            if(tray!=null) { tray.Visible=false; tray.Dispose(); }



            if(overlay!=null)overlay.Dispose();



            overlayFrame.Dispose(); fullMap.Dispose();



            if(fullMapSidebarFrame!=null)fullMapSidebarFrame.Dispose();



            foreach(Bitmap cachedLevel in mapLevels)cachedLevel.Dispose();



            if(map!=null)map.Dispose();
            if(cachedConeBrush!=null) { try { cachedConeBrush.Dispose(); } catch {} cachedConeBrush=null; }



        }



        protected override void Dispose(bool disposing) {



            if(disposing)ReleaseResources();



            base.Dispose(disposing);



        }



        void ExitApp() { closing=true; SaveSettings(); Close(); }



        

        public void OpenStartupGuide() {
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => OpenStartupGuide()));
                return;
            }
            try {
                IWin32Window owner = (Visible && IsHandleCreated) ? (IWin32Window)this : null;
                using(StartupGuideDialog guide = new StartupGuideDialog(
                    scumMapKey, scumChatKey, scumCopyModifierKey, scumCopyKey,
                    (mKey, cKey, modKey, cpKey) => {
                        scumMapKey = mKey;
                        scumChatKey = cKey;
                        scumCopyModifierKey = modKey;
                        scumCopyKey = cpKey;
                        SettingsChanged();
                    },
                    () => {
                        SettingsChanged();
                        if(Visible) canvas.Invalidate();
                        RenderOverlay();
                    })) {
                    if(owner == null) guide.StartPosition = FormStartPosition.CenterScreen;
                    guide.ShowDialog(owner);
                }
            } catch(Exception ex) {
                Program.LogException("StartupGuide", ex);
            }
        }

        public void OpenZoneImport(string file) {
            if(zonesLoadFailed) { if(!diagnosticMode) MessageBox.Show(Localization.Get("NoteZonesLoadFail")); return; }
            if(IsHandleCreated && InvokeRequired) {
                BeginInvoke(new Action(() => OpenZoneImport(file)));
                return;
            }

            bool visible = overlay != null && overlay.Visible;
            if(visible) {
                Native.ReleaseCapture();
                overlay.Hide();
            }

            IWin32Window owner = (Visible && IsHandleCreated) ? (IWin32Window)this : null;
            try {
                using(ZoneEditor editor=new ZoneEditor(map,zones,zonesPath,value=> { zones=value; try { if(File.Exists(zonesPath)) lastZonesFileWriteTimeUtc = File.GetLastWriteTimeUtc(zonesPath); } catch {} SettingsChanged(); })) {
                    if(!string.IsNullOrEmpty(file))editor.Shown+=async(s,e)=>await editor.ImportAutomatic(file);
                    OverlayTheme.Frame(editor,Localization.Get("HeaderMapZones"),()=>editor.Close());
                    if(owner == null) editor.StartPosition = FormStartPosition.CenterScreen;
                    editor.ShowDialog(owner);
                }
            } catch(Exception ex) {
                Program.LogException("ZoneEditor", ex);
                MessageBox.Show(owner,"Could not open the custom zone editor.\n\n"+ex.Message,"Custom Zone Editor",MessageBoxButtons.OK,MessageBoxIcon.Error);
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



                editor.ShowDialog();



            }



            if(failure!=null)throw failure;



        }



        static string KeyName(int key) {
            if(key==0)return Localization.Get("KeyNone");
            if(key==0xBF)return "/";



            if(key==0x6F)return "Num /";



            if(key==0x20)return "Space";



            string name=((Keys)key).ToString();



            return string.IsNullOrEmpty(name)?"VK "+key.ToString(CultureInfo.InvariantCulture):name;



        }



        static bool IsModifierKey(int key) {



            return key==0x10 || key==0xA0 || key==0xA1 || key==0x11 || key==0xA2 || key==0xA3 ||



                key==0x12 || key==0xA4 || key==0xA5 || key==0x5B || key==0x5C;



        }



        void CaptureKeyWizardKey(int key) {



            if(keyWizardForm==null || keyWizardCaptureTarget==0)return;



            if(key==0x1B) {



                keyWizardCaptureTarget=0;



                keyWizardStatus.Text=Localization.Get("KeyWizardCaptureCancelled");



                return;



            }



            bool modifier=IsModifierKey(key);



            if(keyWizardCaptureTarget==3) {
                if(key==0x08 || key==0x2E) {
                    keyWizardCopyModifierKey=0;
                    if(keyWizardNoModifierCheck!=null) keyWizardNoModifierCheck.Checked=true;
                    if(keyWizardCopyModifierValue!=null) keyWizardCopyModifierValue.Text=Localization.Get("KeyNone");
                    if(keyWizardModifierCapture!=null) keyWizardModifierCapture.Enabled=false;
                    keyWizardCaptureTarget=0;
                    keyWizardStatus.Text=Localization.T("KeyWizardCaptured",Localization.Get("KeyNone"));
                    return;
                }
                if(!modifier) {
                    keyWizardStatus.Text=Localization.Get("KeyWizardModifierRequired");
                    return;
                }
            }



            if(keyWizardCaptureTarget!=3 && modifier) {



                keyWizardStatus.Text=Localization.Get("KeyWizardNonModifier");



                return;



            }



            if(keyWizardCaptureTarget==1) {



                keyWizardMapKey=key;



                keyWizardMapValue.Text=KeyName(key);



            } else if(keyWizardCaptureTarget==2) {



                keyWizardChatKey=key;



                keyWizardChatValue.Text=KeyName(key);



            } else if(keyWizardCaptureTarget==3) {



                keyWizardCopyModifierKey=key;
                if(keyWizardNoModifierCheck!=null) keyWizardNoModifierCheck.Checked=false;



                keyWizardCopyModifierValue.Text=KeyName(key);



            } else {



                keyWizardCopyKey=key;



                keyWizardCopyValue.Text=KeyName(key);



            }



            string captured=KeyName(key);



            keyWizardCaptureTarget=0;



            keyWizardStatus.Text=Localization.T("KeyWizardCaptured",captured);



        }



        void ShowKeyRebindingWizard() {



            if(keyWizardForm!=null)return;



            keyWizardMapKey=scumMapKey;



            keyWizardChatKey=scumChatKey;



            keyWizardCopyModifierKey=scumCopyModifierKey;



            keyWizardCopyKey=scumCopyKey;



            using(Form dlg=new Form { Text=Localization.Get("KeyWizardTitle"),ClientSize=new Size(460,390),StartPosition=FormStartPosition.CenterScreen,



                FormBorderStyle=FormBorderStyle.FixedToolWindow,MaximizeBox=false,MinimizeBox=false,TopMost=true,KeyPreview=true }) {



                keyWizardForm=dlg;



                Label intro=new Label { Left=16,Top=62,Width=420,Height=42,Text=Localization.Get("KeyWizardIntro") };



                Label mapLabel=new Label { Left=16,Top=108,Width=170,Height=26,Text=Localization.Get("KeyWizardMapKey"),Padding=new Padding(0,5,0,0) };



                keyWizardMapValue=new Label { Left=190,Top=108,Width=100,Height=26,Text=KeyName(keyWizardMapKey),TextAlign=ContentAlignment.MiddleCenter,BackColor=OverlayTheme.Surface };



                Button mapCapture=new Button { Left=300,Top=107,Width=130,Text=Localization.Get("KeyWizardPressKey") };



                Label chatLabel=new Label { Left=16,Top=144,Width=170,Height=26,Text=Localization.Get("KeyWizardChatKey"),Padding=new Padding(0,5,0,0) };



                keyWizardChatValue=new Label { Left=190,Top=144,Width=100,Height=26,Text=KeyName(keyWizardChatKey),TextAlign=ContentAlignment.MiddleCenter,BackColor=OverlayTheme.Surface };



                Button chatCapture=new Button { Left=300,Top=143,Width=130,Text=Localization.Get("KeyWizardPressKey") };



                Label modifierLabel=new Label { Left=16,Top=180,Width=170,Height=26,Text=Localization.Get("KeyWizardCopyModifier"),Padding=new Padding(0,5,0,0) };



                keyWizardCopyModifierValue=new Label { Left=190,Top=180,Width=100,Height=26,Text=KeyName(keyWizardCopyModifierKey),TextAlign=ContentAlignment.MiddleCenter,BackColor=OverlayTheme.Surface };



                Button modifierCapture=new Button { Left=300,Top=179,Width=130,Text=Localization.Get("KeyWizardPressKey") };
                keyWizardModifierCapture=modifierCapture;
                if(keyWizardCopyModifierKey==0) modifierCapture.Enabled=false;

                CheckBox noModifierCheck=new CheckBox { Left=16,Top=212,Width=414,Height=24,Text=Localization.Get("KeyWizardNoModifier"),ForeColor=OverlayTheme.Ink,Checked=(keyWizardCopyModifierKey==0),Cursor=Cursors.Hand };
                keyWizardNoModifierCheck=noModifierCheck;
                noModifierCheck.CheckedChanged+=(s,e)=> {
                    if(noModifierCheck.Checked) {
                        keyWizardCopyModifierKey=0;
                        keyWizardCopyModifierValue.Text=Localization.Get("KeyNone");
                        modifierCapture.Enabled=false;
                        if(keyWizardCaptureTarget==3) {
                            keyWizardCaptureTarget=0;
                            keyWizardStatus.Text=Localization.Get("KeyWizardReady");
                        }
                    } else {
                        if(keyWizardCopyModifierKey==0) keyWizardCopyModifierKey=0xA2;
                        keyWizardCopyModifierValue.Text=KeyName(keyWizardCopyModifierKey);
                        modifierCapture.Enabled=true;
                    }
                };



                Label copyLabel=new Label { Left=16,Top=244,Width=170,Height=26,Text=Localization.Get("KeyWizardCopyKey"),Padding=new Padding(0,5,0,0) };



                keyWizardCopyValue=new Label { Left=190,Top=244,Width=100,Height=26,Text=KeyName(keyWizardCopyKey),TextAlign=ContentAlignment.MiddleCenter,BackColor=OverlayTheme.Surface };



                Button copyCapture=new Button { Left=300,Top=243,Width=130,Text=Localization.Get("KeyWizardPressKey") };



                keyWizardStatus=new Label { Left=16,Top=280,Width=414,Height=34,Text=Localization.Get("KeyWizardReady") };



                Button save=new Button { Left=220,Top=328,Width=100,Text=Localization.Get("KeyWizardSave"),DialogResult=DialogResult.OK };



                Button cancel=new Button { Left=330,Top=328,Width=100,Text=Localization.Get("KeyWizardCancel"),DialogResult=DialogResult.Cancel };



                mapCapture.Click+=(s,e)=> { keyWizardCaptureTarget=1; keyWizardStatus.Text=Localization.Get("KeyWizardPressMap"); dlg.Activate(); };



                chatCapture.Click+=(s,e)=> { keyWizardCaptureTarget=2; keyWizardStatus.Text=Localization.Get("KeyWizardPressChat"); dlg.Activate(); };



                modifierCapture.Click+=(s,e)=> { keyWizardCaptureTarget=3; keyWizardStatus.Text=Localization.Get("KeyWizardPressCopyModifier"); dlg.Activate(); };



                copyCapture.Click+=(s,e)=> { keyWizardCaptureTarget=4; keyWizardStatus.Text=Localization.Get("KeyWizardPressCopyKey"); dlg.Activate(); };



                save.Click+=(s,e)=> {



                    if(keyWizardMapKey==keyWizardChatKey) {



                        keyWizardStatus.Text=Localization.Get("KeyWizardDifferentKeys");



                        dlg.DialogResult=DialogResult.None;



                    } else if(keyWizardCopyModifierKey!=0 && !IsModifierKey(keyWizardCopyModifierKey)) {



                        keyWizardStatus.Text=Localization.Get("KeyWizardCopyPairInvalid");



                        dlg.DialogResult=DialogResult.None;



                    } else if(IsModifierKey(keyWizardCopyKey) || keyWizardCopyKey<=0 || keyWizardCopyKey>=256) {



                        keyWizardStatus.Text=Localization.Get("KeyWizardCopyPairInvalid");



                        dlg.DialogResult=DialogResult.None;



                    } else if(keyWizardCopyModifierKey==0 && (keyWizardCopyKey==keyWizardMapKey || keyWizardCopyKey==keyWizardChatKey)) {



                        keyWizardStatus.Text=Localization.Get("KeyWizardDifferentKeys");



                        dlg.DialogResult=DialogResult.None;



                    }



                };



                dlg.KeyDown+=(s,e)=> {



                    if(e.KeyCode==Keys.Escape && keyWizardCaptureTarget!=0) {



                        keyWizardCaptureTarget=0;



                        keyWizardStatus.Text=Localization.Get("KeyWizardCaptureCancelled");



                        e.Handled=true; e.SuppressKeyPress=true;



                    }



                };



                dlg.Controls.AddRange(new Control[]{intro,mapLabel,keyWizardMapValue,mapCapture,chatLabel,keyWizardChatValue,chatCapture,modifierLabel,keyWizardCopyModifierValue,modifierCapture,noModifierCheck,copyLabel,keyWizardCopyValue,copyCapture,keyWizardStatus,save,cancel});



                OverlayTheme.Frame(dlg,Localization.Get("KeyWizardTitle"),()=>dlg.DialogResult=DialogResult.Cancel);



                dlg.AcceptButton=save; dlg.CancelButton=cancel;



                dlg.Shown+=(s,e)=> { Native.ForceForeground(dlg.Handle); dlg.Activate(); }; 



                dlg.FormClosing+=(s,e)=> { keyWizardCaptureTarget=0; keyWizardForm=null; keyWizardStatus=null; keyWizardMapValue=null; keyWizardChatValue=null; keyWizardCopyModifierValue=null; keyWizardCopyValue=null; keyWizardNoModifierCheck=null; keyWizardModifierCapture=null; };



                if(dlg.ShowDialog(this)==DialogResult.OK) {



                    scumMapKey=keyWizardMapKey;



                    scumChatKey=keyWizardChatKey;



                    scumCopyModifierKey=keyWizardCopyModifierKey;



                    scumCopyKey=keyWizardCopyKey;



                    SettingsChanged();



                    BuildSettingsPanel();



                    note=Localization.Get("KeyWizardSaved");



                    RenderOverlay();



                }



            }



        }



        void AddCheck(FlowLayoutPanel panel,string text,bool value,Action<bool> changed) {



            TacticalCheckBox box = new TacticalCheckBox { Text = text, Checked = value, Width = 365, Margin = new Padding(0, 2, 0, 2) };



            box.CheckedChanged+=(s,e)=> { changed(box.Checked); SettingsChanged(); }; panel.Controls.Add(box);



        }



        NumericUpDown AddNumber(FlowLayoutPanel panel,string text,int min,int max,int value,Action<int> changed) {



            FlowLayoutPanel row=new FlowLayoutPanel { Width=365,Height=31 };



            row.Controls.Add(new Label { Text=text,Width=220,Padding=new Padding(0,5,0,0) });



            NumericUpDown number=new NumericUpDown { Minimum=min,Maximum=max,Value=value,Width=100 };



            number.ValueChanged+=(s,e)=> { changed((int)number.Value); SettingsChanged(); }; row.Controls.Add(number); panel.Controls.Add(row); return number;



        }



        void InvalidateFullMapSidebar() { fullMapSidebarRevision++; }



        void SettingsChanged() { terrainKey=null; lastFrameKey=null; cachedLocationAnchors=null; locationDescriptionRevision++; InvalidateFullMapSidebar(); saveAfter=DateTime.UtcNow.AddMilliseconds(700); }



        static bool IsCustomWaypointZone(MapZone zone) {



            // Saved custom zones are polygons; saved waypoints are the cyan, single-point



            // entries created by Insert or the full-map context menu.



            return zone!=null && zone.Points!=null && zone.Points.Length==1;



        }



        HashSet<ZoneCategory> BuildHiddenCategories() {



            var hidden = new HashSet<ZoneCategory>();



            if(!showCities) hidden.Add(ZoneCategory.City);



            if(!showTowns) hidden.Add(ZoneCategory.Town);



            if(!showFarms) hidden.Add(ZoneCategory.Farm);



            if(!showTraders) hidden.Add(ZoneCategory.Trader);



            if(!showFactions) hidden.Add(ZoneCategory.Faction);



            if(!showMilitary) hidden.Add(ZoneCategory.Military);



            if(!showBunkers) hidden.Add(ZoneCategory.Bunker);



            return hidden.Count==0 ? null : hidden;



        }



        static int ReadCopyInterval(int value,bool legacy) {



            // Limit synthetic shortcuts to one per second; preserve slower custom values.



            if(legacy) return value<=1?1000:Math.Min(10,value)*1000;



            return Math.Max(1000,Math.Min(10000,value));



        }



        void LoadSettings() {



            try {



                if(!File.Exists(settingsPath)) {



                    isFirstLaunch = true;



                    Localization.DetectSystemLanguage();



                    SaveSettings();



                    return;



                }



                string[] lines;



                try { lines=File.ReadAllLines(settingsPath); }



                catch(IOException) { return; }



                catch(UnauthorizedAccessException) { return; }



                bool foundWelcomed=false, foundLanguage=false;



                int width=savedWidth,height=savedHeight,left=overlay!=null?overlay.Left:savedLeft,top=overlay!=null?overlay.Top:savedTop;



                foreach(string line in lines) {



                    try {



                        if(string.IsNullOrEmpty(line)) continue;



                        int eqIdx=line.IndexOf('=');



                        if(eqIdx<1 || eqIdx>=line.Length-1) continue;



                        string key=line.Substring(0,eqIdx).Trim();



                        string val=line.Substring(eqIdx+1).Trim();



                        if(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;



                        int n; bool b; float z;



                        if(key=="Welcomed") { foundWelcomed=true; continue; }



                        if(key=="Language") { Localization.SetLanguage(val); foundLanguage=true; continue; }



                        if(bool.TryParse(val,out b)) {



                            switch(key) { case "GridLabels":gridLabels=b;break;case "GridBorders":gridBorders=b;break;case "EdgeFade":edgeFade=b;break;case "ShowStatus":showStatus=b;break;case "ShowZones":showZones=b;break;case "ShowGasStations":showGasStations=b;break;case "AutoZoom":autoZoom=b;break;case "ShowHeading":showHeading=b;break;case "ShowCompass":showCompass=b;break;case "ShowElevation":showElevation=b;break;case "ZoneChime":zoneChime=b;break;case "ShowCities":showCities=b;break;case "ShowTowns":showTowns=b;break;case "ShowFarms":showFarms=b;break;case "ShowTraders":showTraders=b;break;case "ShowFactions":showFactions=b;break;case "ShowMilitary":showMilitary=b;break;case "ShowBunkers":showBunkers=b;break;case "ShowCustomWaypoints":showCustomWaypoints=b;break;case "ShowScumMap":showScumMap=b;break;case "ShowZoneLabels":showZoneLabels=b;break;case "SmartLabelLod":smartLabelLod=b;break;case "ShowHuntingLegend":showHuntingLegend=b;break;case "SidebarWildlifeExpanded":sidebarWildlifeExpanded=b;break;case "SidebarZonesExpanded":sidebarZonesExpanded=b;break; }
                        }
                        if(key=="DisabledZoneLayers") {
                            disabledZoneLayers.Clear();
                            if(!string.IsNullOrWhiteSpace(val)) {
                                foreach(string dl in val.Split(new[]{',',';'}, StringSplitOptions.RemoveEmptyEntries)) {
                                    string trimmed = dl.Trim();
                                    if(!string.IsNullOrEmpty(trimmed)) disabledZoneLayers.Add(trimmed);
                                }
                            }
                        }
                        if(key=="ScumMapDisabledCats") {
                            if(scumMap!=null) scumMap.ApplyDisabledCategoriesString(val);
                            else pendingDisabledCats=val;
                        }



                        if(key=="StatusPos") { statusPos=val=="Above"?"Above":"Below"; }



                        if(key=="Shape") { overlayShape=val=="Circle"?"Circle":"Square"; }
                        if(key=="PlayerColor" || key=="PlayerConeColor") {
                            try {
                                if(val.StartsWith("#")) playerConeColor = ColorTranslator.FromHtml(val);
                                else if(int.TryParse(val, out n)) playerConeColor = Color.FromArgb(n);
                            } catch {}
                            continue;
                        }
                        if(key=="RouteColor") {
                            try {
                                if(val.StartsWith("#")) routeGuidanceColor = ColorTranslator.FromHtml(val);
                                else if(int.TryParse(val, out n)) routeGuidanceColor = Color.FromArgb(n);
                            } catch {}
                            continue;
                        }



                        if(int.TryParse(val,out n)) {



                            switch(key) { case "Width":width=Math.Max(240,Math.Min(800,n));break;case "Height":height=Math.Max(240,Math.Min(800,n));break;case "Left":left=n;break;case "Top":top=n;break;case "Opacity":mapOpacity=Math.Max(30,Math.Min(100,n));break;case "FullMapOpacity":fullMapOpacity=Math.Max(20,Math.Min(100,n));break;case "GridOpacity":gridOpacity=Math.Max(0,Math.Min(100,n));break;case "LabelSize":labelSize=Math.Max(6,Math.Min(24,n));break;case "MaxZoom":maxZoom=Math.Max(4,Math.Min(32,n));break;case "AutoZoomMin":autoZoomMin=Math.Max(1,Math.Min(32,n));break;case "AutoZoomMax":autoZoomMax=Math.Max(1,Math.Min(32,n));break;case "ZoomStep":zoomStepPercent=Math.Max(10,Math.Min(100,n));break;case "CopyInterval":copyIntervalMs=ReadCopyInterval(n,true);break;case "CopyIntervalMs":copyIntervalMs=ReadCopyInterval(n,false);break;case "ScumMapKey":if(n>0&&n<256)scumMapKey=n;break;case "ScumChatKey":if(n>0&&n<256)scumChatKey=n;break;case "ScumCopyModifierKey":if(n==0||IsModifierKey(n))scumCopyModifierKey=n;break;case "ScumCopyKey":if(n>0&&n<256&&!IsModifierKey(n))scumCopyKey=n;break; }



                        }



                        if(key=="Zoom" && float.TryParse(val,NumberStyles.Float,CultureInfo.InvariantCulture,out z) && !float.IsNaN(z) && !float.IsInfinity(z)) { zoom=Math.Max(1,Math.Min(maxZoom,z)); targetZoom=zoom; }



                    } catch { /* skip malformed line */ }



                }



                if(!foundWelcomed) isFirstLaunch=true;



                if(!foundLanguage) Localization.DetectSystemLanguage();



                if(scumMapKey==scumChatKey) {



                    // A malformed or hand-edited config must not make the map key permanently



                    // indistinguishable from the chat key.



                    scumMapKey=0x4D;



                    scumChatKey=0x54;



                }



                if(scumCopyModifierKey!=0 && !IsModifierKey(scumCopyModifierKey)) scumCopyModifierKey=0xA2;



                if(scumCopyKey<=0 || scumCopyKey>=256 || IsModifierKey(scumCopyKey)) scumCopyKey=0x43;



                savedWidth=width; savedHeight=height; savedLeft=left; savedTop=top;



                if(overlay!=null) {



                    overlay.Size=new Size(width,height);



                    try {



                        Rectangle area=Screen.FromPoint(new Point(left,top)).WorkingArea;



                        overlay.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-width,left)),Math.Max(area.Top,Math.Min(area.Bottom-height,top)));



                    } catch { /* default position if screen detection fails */ }



                }



            } catch { /* catch-all: proceed with defaults on any unexpected error */ }



        }



        void SaveSettings() {



            if(diagnosticMode)return;



            saveAfter=DateTime.MaxValue;



            try { File.WriteAllLines(settingsPath+".tmp",new string[]{"Welcomed=True","Language="+Localization.CurrentCode,"GridLabels="+gridLabels,"GridBorders="+gridBorders,"GridOpacity="+gridOpacity,"ShowZones="+showZones,"ShowGasStations="+showGasStations,"LabelSize="+labelSize,"EdgeFade="+edgeFade,"Shape="+overlayShape,"ShowHeading="+showHeading,"ShowCompass="+showCompass,"ShowElevation="+showElevation,"ZoneChime="+zoneChime,"CopyIntervalMs="+copyIntervalMs,"AutoZoom="+autoZoom,"AutoZoomMin="+autoZoomMin,"AutoZoomMax="+autoZoomMax,"ShowStatus="+showStatus,"StatusPos="+statusPos,"Opacity="+mapOpacity,"FullMapOpacity="+fullMapOpacity,"Width="+(overlay!=null?overlay.Width:savedWidth),"Height="+(overlay!=null?overlay.Height:savedHeight),"Left="+(overlay!=null?overlay.Left:savedLeft),"Top="+(overlay!=null?overlay.Top:savedTop),"Zoom="+zoom.ToString(CultureInfo.InvariantCulture),"MaxZoom="+maxZoom,"ZoomStep="+zoomStepPercent,"ScumMapKey="+scumMapKey,"ScumChatKey="+scumChatKey,"ScumCopyModifierKey="+scumCopyModifierKey,"ScumCopyKey="+scumCopyKey,"ShowCities="+showCities,"ShowTowns="+showTowns,"ShowFarms="+showFarms,"ShowTraders="+showTraders,"ShowFactions="+showFactions,"ShowMilitary="+showMilitary,"ShowBunkers="+showBunkers,"ShowCustomWaypoints="+showCustomWaypoints,"ShowScumMap="+showScumMap,"ScumMapDisabledCats="+(scumMap!=null?scumMap.GetDisabledCategoriesString():""),"ShowZoneLabels="+showZoneLabels,"SmartLabelLod="+smartLabelLod,"ShowHuntingLegend="+showHuntingLegend,"SidebarWildlifeExpanded="+sidebarWildlifeExpanded,"SidebarZonesExpanded="+sidebarZonesExpanded,"DisabledZoneLayers="+string.Join(";",disabledZoneLayers),"RouteColor="+ColorTranslator.ToHtml(routeGuidanceColor),"PlayerColor="+ColorTranslator.ToHtml(playerConeColor)});



                if(File.Exists(settingsPath)) File.Replace(settingsPath+".tmp",settingsPath,settingsPath+".bak"); else File.Move(settingsPath+".tmp",settingsPath); }



            catch(IOException) { note=Localization.Get("NoteSettingsSaveFail"); } catch(UnauthorizedAccessException) { note=Localization.Get("NoteSettingsSaveFail"); }



        }



        readonly GameFocusReturn settingsFocus=new GameFocusReturn();



        bool panelOpening;



        void DismissSettings() {



            SaveSettings(); settingsFocus.Restore(); Hide();



            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



        }



        async void ShowSettings() {
            if(panelOpening || searchOpen) return;
            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;
            if(Visible) {



                Height=Math.Min(640,Screen.FromControl(overlay).WorkingArea.Height - 40);



                OverlayTheme.Anchor(this,overlay);



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);



                return;



            }



            panelOpening=true;



            lastHomeAction=DateTime.UtcNow;



            settingsFocus.Capture();



            try {



                while(Native.CopyInProgress) await Task.Delay(5);



                if(closing || IsDisposed) return;



                Height=Math.Min(640,Screen.FromControl(overlay).WorkingArea.Height - 40);



                OverlayTheme.Anchor(this,overlay);



                Show();



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);



            } finally { panelOpening=false; }



        }



        void ToggleOverlay() {



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



                    dlg.ClientSize=new Size(430,390);



                    dlg.BackColor=OverlayTheme.Background; dlg.ForeColor=OverlayTheme.Ink;



                    dlg.Font=new Font("Segoe UI",9);



                    Rectangle area=Screen.FromControl(overlay).WorkingArea;



                    dlg.Location=new Point(Math.Max(area.Left,Math.Min(overlay.Left,area.Right-dlg.Width)),



                        Math.Max(area.Top,Math.Min(overlay.Top+20,area.Bottom-dlg.Height)));



                    Label hint=new Label { Dock=DockStyle.Top,Height=32,Text=Localization.Get("SearchHint") };



                    TextBox search=new TextBox { MaxLength=120,Dock=DockStyle.Top,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink };



                    ListBox list=new ListBox { Dock=DockStyle.Fill,BackColor=OverlayTheme.Background,ForeColor=OverlayTheme.Ink,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48 };



                    Label status=new Label { Dock=DockStyle.Bottom,Height=30 };



                    FlowLayoutPanel buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=38 };



                    Button go=new Button { Text=Localization.Get("SetWaypoint"),Width=125 };



                    Button clear=new Button { Text=Localization.Get("ClearWaypoint"),Width=135,Enabled=searchTarget!=null };



                    Button cancel=new Button { Text=Localization.Get("Cancel"),Width=80,DialogResult=DialogResult.Cancel };



                    OverlayTheme.Style(dlg);
            go.BackColor=OverlayTheme.Accent; go.ForeColor=Color.FromArgb(14,17,20); go.Font=new Font("Segoe UI",9f,FontStyle.Bold);
            buttons.Controls.AddRange(new Control[]{go,clear,cancel});



                    List<MapZone> matches=new List<MapZone>();



                    MapZone selected=null;



                    list.DrawItem+=(s,e)=> {



                        if(e.Index<0 || e.Index>=matches.Count)return;



                        MapZone result=matches[e.Index];



                        bool active=(e.State&DrawItemState.Selected)!=0;



                        using(Brush fill=new SolidBrush(active?Color.FromArgb(45,35,20):OverlayTheme.Surface)) e.Graphics.FillRectangle(fill,e.Bounds);



                        Rectangle title=new Rectangle(e.Bounds.Left+12,e.Bounds.Top+5,e.Bounds.Width-24,20);



                        Rectangle detail=new Rectangle(title.Left,e.Bounds.Top+27,title.Width,17);



                        TextRenderer.DrawText(e.Graphics,Localization.GetZoneName(result.Name),list.Font,title,active?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);



                        string catLabel=!string.IsNullOrEmpty(result.Subtitle)?Localization.GetCategoryName(result.Subtitle):(result.Name.StartsWith("Grid ",StringComparison.Ordinal)?Localization.Get("SectorCentre"):(result.IsFaction?Localization.Get("FactionPOI"):Localization.Get("Place")));



                        string description=DestinationSearch.Sector(result.Centroid)+" / "+catLabel;



                        if(position!=null) description+=" / "+DestinationSearch.Distance(DestinationSearch.Metres(result.Centroid,ToMap(position)));



                        TextRenderer.DrawText(e.Graphics,description,list.Font,detail,OverlayTheme.InkMuted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);



                        e.DrawFocusRectangle();



                    };



                    Action populate=()=> {



                        matches=DestinationSearch.Find(zones,search.Text,position==null?(PointF?)null:ToMap(position));



                        if(showScumMap && scumMap!=null) {



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



                            string displayName = Localization.GetZoneName(zone.Name);



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



                                string displayName=Localization.GetZoneName(z.Name);



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



                                    deleteWp.Text=Localization.T("DeleteWaypointNamed",Localization.GetZoneName(z.Name));



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



                    clear.Click+=(s,e)=> { searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null; note=Localization.Get("NoteWaypointCleared"); SettingsChanged(); dlg.DialogResult=DialogResult.Cancel; };



                    search.KeyDown+=(s,e)=> {



                        if((e.KeyCode==Keys.Down || e.KeyCode==Keys.Up) && list.Items.Count>0) {



                            list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(e.KeyCode==Keys.Down?1:-1)));



                            e.Handled=true; e.SuppressKeyPress=true;



                        }



                    };



                    dlg.AcceptButton=go; dlg.CancelButton=cancel;



                    dlg.Controls.Add(list); dlg.Controls.Add(search); dlg.Controls.Add(hint); dlg.Controls.Add(status); dlg.Controls.Add(buttons);



                    OverlayTheme.Frame(dlg,Localization.Get("HeaderWaypoint"),()=>dlg.DialogResult=DialogResult.Cancel);
                    go.BackColor=OverlayTheme.Accent; go.ForeColor=Color.FromArgb(14,17,20); go.FlatAppearance.BorderColor=OverlayTheme.AccentHover; go.Font=new Font("Segoe UI",9f,FontStyle.Bold);



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



                    if(dlg.ShowDialog(Visible?this:null)==DialogResult.OK && selected!=null) {



                        searchTarget=selected;



                        note=Localization.T("NoteWaypointSet", Localization.GetZoneName(selected.Name));



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



        void ToggleSettings() {



            if((DateTime.UtcNow - lastHomeAction).TotalMilliseconds < 450) return;



            lastHomeAction = DateTime.UtcNow;



            if(Visible && Native.GetForegroundWindow() == Handle) DismissSettings();



            else ShowSettings();



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



                string displayName=Localization.GetZoneName(existing.Name);



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
                IWin32Window owner = (Visible && IsHandleCreated) ? (IWin32Window)this : (overlay != null && overlay.IsHandleCreated ? (IWin32Window)overlay : null);
                DialogResult res = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                if(res == DialogResult.OK) {
                    routeGuidanceColor = dlg.Color;
                    SettingsChanged();
                    if(Visible) {
                        BuildSettingsPanel();
                        canvas.Invalidate();
                    }
                    RenderOverlay();
                }
            }
        }

                void ChangePlayerColor() {
            using(ColorDialog dlg = new ColorDialog()) {
                dlg.Color = playerConeColor;
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                IWin32Window owner = (Visible && IsHandleCreated) ? (IWin32Window)this : (overlay != null && overlay.IsHandleCreated ? (IWin32Window)overlay : null);
                DialogResult res = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                if(res == DialogResult.OK) {
                    playerConeColor = dlg.Color;
                    SettingsChanged();
                    if(Visible) {
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
                    Label lbl=new Label { Left=20,Top=64,Width=340,Text=Localization.Get("WaypointNamePrompt"),TabIndex=1,ForeColor=OverlayTheme.InkMuted,Font=new Font("Segoe UI",9f) };
                    TextBox txt=new TextBox { Left=20,Top=92,Width=340,TabIndex=0,BackColor=OverlayTheme.Surface,ForeColor=OverlayTheme.Ink,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",10f) };
                    Button ok=new Button { Text=Localization.Get("Done"),Left=190,Top=132,Width=80,Height=30,DialogResult=DialogResult.OK,TabIndex=2,BackColor=OverlayTheme.Accent,ForeColor=Color.FromArgb(12,12,12),Font=new Font("Segoe UI",9f,FontStyle.Bold),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
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
                    IWin32Window owner = (Visible && IsHandleCreated) ? (IWin32Window)this : null;
                    if(prompt.ShowDialog(owner)==DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text)) {
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
        bool TryCommitZones(List<MapZone> candidate,IWin32Window owner) {
            try {
                if(zonesLoadFailed) throw new InvalidDataException(Localization.Get("NoteZonesLoadFail"));
                ZoneStore.Save(zonesPath,candidate);
                zones=candidate;
                try { lastZonesFileWriteTimeUtc=File.GetLastWriteTimeUtc(zonesPath); } catch(IOException) {} catch(UnauthorizedAccessException) {}
                return true;
            } catch(Exception ex) {
                if(!(ex is IOException) && !(ex is InvalidDataException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException)) throw;
                note=Localization.T("ZeSaveZonesError",ex.Message);
                Program.LogException("SaveWaypoints",ex);
                if(!diagnosticMode) MessageBox.Show(owner,note,Localization.Get("ZoneEditorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Error);
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
                    IWin32Window dialogOwner=owner ?? ((Visible && IsHandleCreated) ? (IWin32Window)this : null);
                    return prompt.ShowDialog(dialogOwner)==DialogResult.Yes;

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
            Label message=new Label { Text=Localization.T("WaypointDeleteConfirm",displayName),Left=20,Top=64,Width=380,Height=50,ForeColor=OverlayTheme.Ink,Font=new Font("Segoe UI",9.25f) };
            Button remove=new Button { Text=Localization.Get("RemoveWaypoint"),Left=140,Top=122,Width=150,Height=32,DialogResult=DialogResult.Yes,TabIndex=1,BackColor=OverlayTheme.Accent,ForeColor=Color.FromArgb(12,12,12),Font=new Font("Segoe UI",9f,FontStyle.Bold),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand };
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



            autoZoom=false;



            float step=1f+(zoomStepPercent/100f);



            zoom=Math.Max(1,zoom/step);



            targetZoom=zoom;



            SettingsChanged();



            lastFrameKey=null;



            terrainKey=null;



            RenderOverlay();



        }



        int inputGeneration;



        bool HotkeyContextAllowed() {



            return !diagnosticMode && !closing && !IsDisposed && IsHandleCreated &&



                keys.Available && !panelOpening &&



                !Visible && !searchOpen && (Native.GameFocused() || (fullMapActive && Native.IsOurWindow(Native.GetForegroundWindow())));



        }



        bool IsWatchedGameKey(int key) {



            if(keyWizardCaptureTarget!=0) return true;



            if(key==scumMapKey || key==scumChatKey) return true;



            switch(key) {



                case 0x09: case 0x0D: case 0x1B: case 0x21: case 0x22:



                case 0x23: case 0x24: case 0x2D: case 0x2E:



                case 0x6F: case 0xBF: return true;



                default: return false;



            }



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



        void OnGameKey(int key) {



            try {



                OnGameKeyCore(key);



            } catch(Exception ex) {



                Program.LogException("OnGameKey", ex);



            }



        }



        void OnGameKeyCore(int key) {



            if(keyWizardCaptureTarget!=0) {



                CaptureKeyWizardKey(key);



                return;



            }



            // Physical chat transitions still matter while a coordinate copy is being cancelled.



            bool inputWindowFocused=Native.GameFocused() || (fullMapActive && Native.IsOurWindow(Native.GetForegroundWindow()));



            if(diagnosticMode || closing || IsDisposed || panelOpening || !inputWindowFocused) { inputGeneration++; return; }



            // Use physical hook state: the sampler's injected Ctrl is not a user modifier.



            // Shift is also used for sprinting and must not swallow M or chat transitions.



            bool chatKey=key==scumChatKey || key==0xBF || key==0x6F || key==0x0D || key==0x1B || key==0x09;



            if(keys.ShortcutModifiersDown(key!=scumMapKey && !chatKey)) return;



            bool wasChat=chat.Paused;



            chat.Key(key,scumChatKey);



            if(wasChat!=chat.Paused) {



                inputGeneration++;



                if(chat.Paused) {



                    note=Localization.Get("NoteChatOpen");



                    if(fullMapActive) SetFullMap(false);



                }



                else { resumeAfter=DateTime.UtcNow.AddMilliseconds(1200); next=resumeAfter; note=Localization.Get("NoteChatClosed"); }



            }



            // Escape belongs to chat first if chat was actively open



            if(wasChat && key==0x1B) return;



            // If chat is open and typing, do not swallow in-game text or trigger hotkeys



            if(chat.Paused && key!=0x1B && key!=0x0D) return;



            if(!HotkeyContextAllowed()) return;



            Action action=null;



            if(key==scumMapKey) action=TriggerFullMap;



            else switch(key) {



                case 0x23: action=TriggerToggleOverlay; break;



                case 0x24: action=ToggleSettings; break;



                case 0x2E: action=TriggerZoneSearch; break;



                case 0x21: action=TriggerZoomIn; break;



                case 0x22: action=TriggerZoomOut; break;



                case 0x2D: action=TriggerPin; break;



                case 0x1B: if(fullMapActive) action=()=>SetFullMap(false); break;



            }



            if(action==null) return;



            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



            action();



        }



        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); }



        protected override void WndProc(ref Message m) { base.WndProc(ref m); }



        internal void Accept(string text) {



            Position p=Position.Parse(text); if(p==null)return;



            DateTime now=DateTime.UtcNow;



            PointF currentPt=ToMap(p);



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



            if(DestinationReached(searchTarget,currentPt)) {



                note=Localization.T("NoteWaypointArrived",Localization.GetZoneName(searchTarget.Name));



                searchTarget=null;



                activeRoute=null; routeGeneration++;



                lastRouteTarget=null;



                lastFrameKey=null;



            }



            fixedMapCentre=currentPt;



            if(!fixedCentreReady) { fixedCentreReady=true; terrainKey=null; }



            position=p; updated=now; if(Visible)canvas.Invalidate();



            if(searchTarget!=null) UpdateRouteAsync(currentPt,searchTarget);



            RenderOverlay();



        }



        bool IsPlayerInOutpostZone() {



            if(position == null || zones == null) return false;



            PointF pt = ToMap(position);



            for(int i = 0; i < zones.Count; i++) {



                MapZone z = zones[i];



                if(z != null && (z.Name ?? "").IndexOf("Outpost", StringComparison.OrdinalIgnoreCase) >= 0 && z.Contains(pt)) {



                    return true;



                }



            }



            return false;



        }



        void TriggerFullMap() {
            if((DateTime.UtcNow-lastMToggleTime).TotalMilliseconds<250) return;
            if(!fullMapActive && chat.Paused) return;
            lastMToggleTime = DateTime.UtcNow;



            SetFullMap(!fullMapActive);



        }



        void SetFullMap(bool active) {



            if(fullMapActive==active) return;



            fullMapActive=active;



            if(!chat.Paused) chat.Reset();



            if(fullMapActive) {
                try {
                    if(File.Exists(zonesPath)) {
                        DateTime wt = File.GetLastWriteTimeUtc(zonesPath);
                        if(lastZonesFileWriteTimeUtc != DateTime.MinValue && wt > lastZonesFileWriteTimeUtc) {
                            lastZonesFileWriteTimeUtc = wt;
                            zones = ZoneStore.Load(zonesPath);
                            InvalidateFullMapSidebar();
                            terrainKey = null;
                            lastFrameKey = null;
                        }
                    }
                } catch {}
                fullMapOpenedAt=DateTime.UtcNow;



                if(!overlay.Visible) {



                    overlay.Show();



                    hiddenByFocusLoss = false;



                }



            } else {



                fullMapOpenedAt=DateTime.MinValue;



                fullMapZoom = 1.0f;



                fullMapPan = new PointF(0.5f, 0.5f);



                draggingSlider = false;



                draggingMap = false;



            }



            overlay.FullMapMode = fullMapActive;



            lastFrameKey = null;



            terrainKey = null;



            note = fullMapActive ? Localization.Get("NoteFullMapOn") : Localization.Get("NoteFullMapOff");



            RenderOverlay();



        }



        bool FullMapInputAllowed() {



            if(fullMapActive && (diagnosticMode || (!closing && !IsDisposed && !Native.CopyInProgress))) return true;



            draggingSlider=false; draggingMap=false; mouseMoved=true;



            return false;



        }



        void HandleFullMapWheel(int delta, Point pt) {



            if(!FullMapInputAllowed()) return;



            int mapDim = overlay.Height;



            if(pt.X >= mapDim) {



                // Scroll sidebar content



                int deltaScroll = delta > 0 ? -48 : 48;



                sidebarScrollY = Math.Max(0, Math.Min(sidebarMaxScroll, sidebarScrollY + deltaScroll));



                InvalidateFullMapSidebar();



                lastFrameKey = null;



                RenderOverlay();



                return;



            }



            float factor = delta > 0 ? 1.25f : (1.0f / 1.25f);



            float newZoom = Math.Max(1.0f, Math.Min(16.0f, fullMapZoom * factor));



            if(Math.Abs(newZoom - 1.0f) < 0.05f) {



                fullMapZoom = 1.0f;



                fullMapPan = new PointF(0.5f, 0.5f);



            } else {



                if(overlay.Width > 0 && overlay.Height > 0) {



                    float sideBefore = mapDim * fullMapZoom;



                    float sideAfter = mapDim * newZoom;



                    float mapXBefore = fullMapPan.X + (pt.X - mapDim / 2f) / sideBefore;



                    float mapYBefore = fullMapPan.Y + (pt.Y - mapDim / 2f) / sideBefore;



                    fullMapPan.X = mapXBefore - (pt.X - mapDim / 2f) / sideAfter;



                    fullMapPan.Y = mapYBefore - (pt.Y - mapDim / 2f) / sideAfter;



                    fullMapPan.X = Math.Max(0.05f, Math.Min(0.95f, fullMapPan.X));



                    fullMapPan.Y = Math.Max(0.05f, Math.Min(0.95f, fullMapPan.Y));



                }



                fullMapZoom = newZoom;



            }



            InvalidateFullMapSidebar();



            lastFrameKey = null; terrainKey = null;



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



        void HandleFullMapMouseDown(Point pt, MouseButtons btn) {



            if(btn==MouseButtons.Right) pendingFullMapWaypointMapPoint=null;



            if(!FullMapInputAllowed()) return;



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



                        HandleFullMapWheel(120, new Point(mapW / 2, overlay.Height / 2));



                        return;



                    }



                    if(sidebarZoomOutRect.Contains(pt)) {



                        HandleFullMapWheel(-120, new Point(mapW / 2, overlay.Height / 2));



                        return;



                    }



                    if(sidebarZoomResetRect.Contains(pt)) {



                        fullMapZoom = 1.0f;



                        fullMapPan = new PointF(0.5f, 0.5f);



                        InvalidateFullMapSidebar();



                        lastFrameKey = null; terrainKey = null;



                        RenderOverlay();



                        return;



                    }



                    if(sidebarSearchRect.Contains(pt)) {



                        TriggerZoneSearch();



                        return;



                    }



                    if(!sidebarClearWaypointRect.IsEmpty && sidebarClearWaypointRect.Contains(pt)) {



                        searchTarget = null;



                        pin = null;



                        activeRoute = null; routeGeneration++;



                        lastRouteTarget = null;



                        note = Localization.Get("NoteWaypointCleared");



                        SettingsChanged();



                        lastFrameKey = null; terrainKey = null;



                        RenderOverlay();



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



                PointF rightClickMapPoint;



                pendingFullMapWaypointMapPoint=TryGetFullMapPoint(pt,out rightClickMapPoint)?(PointF?)rightClickMapPoint:null;



                if(fullMapZoom > 1.0f) {



                    fullMapZoom = 1.0f;



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



                InvalidateFullMapSidebar();



                lastFrameKey = null; terrainKey = null;



                RenderOverlay();



            }



        }



        void HandleFullMapMouseUp(Point pt, MouseButtons btn) {



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



            using (Font biomeFont = new Font("Segoe UI", 6.8f, FontStyle.Bold))



            using (Font animFont = new Font("Segoe UI", 6.4f, FontStyle.Regular))



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



            using (Font noteFont = new Font("Segoe UI", 5.8f, FontStyle.Italic))



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



            using (GraphicsPath path = new GraphicsPath()) {



                int r = 6;



                path.AddArc(rect.X, rect.Y, r, r, 180, 90);



                path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);



                path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);



                path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);



                path.CloseFigure();



                Color bg = active ? Color.FromArgb(45, 56, 68) : Color.FromArgb(22, 27, 32);



                using (Brush b = new SolidBrush(bg)) g.FillPath(b, path);



                using (Pen p = new Pen(active ? Color.FromArgb(255, 159, 28) : Color.FromArgb(42, 52, 61), 1.0f)) g.DrawPath(p, path);



            }



            using (Font f = new Font("Segoe UI", 7.0f, FontStyle.Bold))



            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {



                Brush b = active ? new SolidBrush(Color.FromArgb(255, 174, 51)) : new SolidBrush(Color.FromArgb(180, 190, 200));



                g.DrawString(text, f, b, rect, sf);



            }



        }



        void DrawActionButton(Graphics g, Rectangle rect, string text, Color baseColor) {



            using (GraphicsPath path = new GraphicsPath()) {



                int r = 7;



                path.AddArc(rect.X, rect.Y, r, r, 180, 90);



                path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);



                path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);



                path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);



                path.CloseFigure();



                using (Brush b = new SolidBrush(baseColor)) g.FillPath(b, path);



                using (Pen p = new Pen(OverlayTheme.Border, 1.0f)) g.DrawPath(p, path);



            }



            using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))



            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {



                g.DrawString(text, f, Brushes.WhiteSmoke, rect, sf);



            }



        }



        void DrawFullMapSidebar(Graphics g, int sidebarX, int sidebarW, int sidebarH) {



            sidebarClickables.Clear();



            int pad = 10;



            int innerX = sidebarX + pad;



            int innerW = sidebarW - pad * 2;



            // Background



            using (Brush bg = new SolidBrush(Color.FromArgb(242, 14, 17, 20))) {



                g.FillRectangle(bg, sidebarX, 0, sidebarW, sidebarH);



            }



            using (Pen border = new Pen(Color.FromArgb(180, 42, 52, 61), 1.5f)) {



                g.DrawLine(border, sidebarX, 0, sidebarX, sidebarH);



            }



            // === PINNED HEADER (Y = 12 to 142) ===



            int curY = 12;



            using (Font titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold)) {



                using (Brush tb = new SolidBrush(OverlayTheme.Accent)) g.DrawString(Localization.Get("SidebarTitle"), titleFont, tb, innerX, curY);



            }



            curY += 24;



            string zoomText = string.Format(CultureInfo.InvariantCulture, "ZOOM {0:0.0}x", fullMapZoom);



            using (Font zoomFont = new Font("Segoe UI", 8.0f, FontStyle.Bold)) {



                using (Brush zb = new SolidBrush(OverlayTheme.AccentHover)) g.DrawString(zoomText, zoomFont, zb, innerX, curY + 3);



            }



            int btnH = 20;



            int rstW = 26;



            sidebarZoomResetRect = new Rectangle(innerX + innerW - rstW, curY, rstW, btnH);



            DrawMiniButton(g, sidebarZoomResetRect, "1x", fullMapZoom > 1.01f);



            int plusW = 22;



            sidebarZoomInRect = new Rectangle(sidebarZoomResetRect.Left - plusW - 3, curY, plusW, btnH);



            DrawMiniButton(g, sidebarZoomInRect, "+", fullMapZoom < 16.0f);



            int minusW = 22;



            sidebarZoomOutRect = new Rectangle(sidebarZoomInRect.Left - minusW - 3, curY, minusW, btnH);



            DrawMiniButton(g, sidebarZoomOutRect, "-", fullMapZoom > 1.01f);



            curY += 28;



            string opText = string.Format(CultureInfo.InvariantCulture, "{0} {1}%", Localization.Get("OpacityLabel"), fullMapOpacity);



            using (Font font = new Font("Segoe UI", 7.2f, FontStyle.Bold)) {



                g.DrawString(opText, font, Brushes.WhiteSmoke, innerX, curY);



            }



            curY += 16;



            int trackH = 6;



            sidebarOpacityTrackRect = new Rectangle(innerX, curY, innerW, trackH);



            using (GraphicsPath trackPath = new GraphicsPath()) {



                trackPath.AddArc(sidebarOpacityTrackRect.X, sidebarOpacityTrackRect.Y, trackH, trackH, 90, 180);



                trackPath.AddArc(sidebarOpacityTrackRect.Right - trackH, sidebarOpacityTrackRect.Y, trackH, trackH, 270, 180);



                trackPath.CloseFigure();



                using (Brush tb = new SolidBrush(Color.FromArgb(180, 22, 27, 32))) g.FillPath(tb, trackPath);



            }



            float frac = (fullMapOpacity - 20) / 80f;



            int fillW = Math.Max(trackH, (int)Math.Round(innerW * frac));



            using (GraphicsPath fillPath = new GraphicsPath()) {



                fillPath.AddArc(sidebarOpacityTrackRect.X, sidebarOpacityTrackRect.Y, trackH, trackH, 90, 180);



                fillPath.AddArc(sidebarOpacityTrackRect.X + fillW - trackH, sidebarOpacityTrackRect.Y, trackH, trackH, 270, 180);



                fillPath.CloseFigure();



                using (Brush fb = new SolidBrush(Color.FromArgb(240, 255, 159, 28))) g.FillPath(fb, fillPath);



            }



            float knobX = sidebarOpacityTrackRect.X + innerW * frac;



            float knobY = sidebarOpacityTrackRect.Y + trackH / 2f;



            float knobR = 5.5f;



            using (Brush kb = new SolidBrush(Color.White)) g.FillEllipse(kb, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);



            using (Pen kp = new Pen(Color.FromArgb(255, 159, 28), 1.5f)) g.DrawEllipse(kp, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);



            curY += 20;



            sidebarSearchRect = new Rectangle(innerX, curY, innerW, 26);



            DrawActionButton(g, sidebarSearchRect, Localization.Get("SidebarSearchBtn"), OverlayTheme.SurfaceCard);



            curY += 30;



            if (searchTarget != null || pin != null) {



                string targetName = searchTarget != null ? searchTarget.Name : "GPS Pin";



                if (targetName.Length > 20) targetName = targetName.Substring(0, 18) + "...";



                using (Font tf = new Font("Segoe UI", 7.0f, FontStyle.Italic)) {



                    using (Brush tb = new SolidBrush(OverlayTheme.Accent)) g.DrawString("Target: " + targetName, tf, tb, innerX, curY + 2);



                }



                sidebarClearWaypointRect = new Rectangle(innerX + innerW - 55, curY, 55, 20);



                DrawMiniButton(g, sidebarClearWaypointRect, "Clear", true);



                curY += 24;



            } else {



                sidebarClearWaypointRect = Rectangle.Empty;



            }



            DrawSidebarDivider(g, innerX, curY, innerW);



            curY += 6;



            int scrollStartY = curY;



            int viewportH = sidebarH - scrollStartY - 10;



            Rectangle viewportRect = new Rectangle(sidebarX, scrollStartY, sidebarW, viewportH);



            GraphicsState origState = g.Save();



            g.SetClip(viewportRect);



            int itemY = scrollStartY - sidebarScrollY;



            // === 1. MAP LAYERS SECTION ===



            using (Font secFont = new Font("Segoe UI", 7.5f, FontStyle.Bold)) {



                g.DrawString(Localization.Get("SidebarLayers"), secFont, Brushes.LightSlateGray, innerX, itemY);



            }



            itemY += 18;



            int toggleH = 20;



            int toggleGap = 4;



            // Collect custom layers and their zone counts
            var customLayers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if(zones != null) {
                for(int i = 0; i < zones.Count; i++) {
                    MapZone z = zones[i];
                    if(z != null && !IsCustomWaypointZone(z) && z.Points != null && z.Points.Length >= 3) {
                        string lName = string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer;
                        int c;
                        customLayers.TryGetValue(lName, out c);
                        customLayers[lName] = c + 1;
                    }
                }
            }

            int totalCustomZones = 0;
            int activeCustomLayers = 0;
            foreach(var kvp in customLayers) {
                totalCustomZones += kvp.Value;
                if(!disabledZoneLayers.Contains(kvp.Key)) activeCustomLayers++;
            }

            bool isZonesEnabled;
            bool isZonesPartial = false;
            if(customLayers.Count == 0) {
                isZonesEnabled = showZones;
            } else {
                if(!showZones || activeCustomLayers == 0) {
                    isZonesEnabled = false;
                } else if(activeCustomLayers == customLayers.Count) {
                    isZonesEnabled = true;
                } else {
                    isZonesEnabled = true;
                    isZonesPartial = true;
                }
            }

            int zoneSecH = 22;
            Rectangle zoneHeaderRect = new Rectangle(innerX, itemY, innerW, zoneSecH);

            RegisterClickable(zoneHeaderRect, viewportRect, () => {
                sidebarZonesExpanded = !sidebarZonesExpanded;
                InvalidateFullMapSidebar();
                lastFrameKey = null;
                RenderOverlay();
            });

            Color zoneSecBg = sidebarZonesExpanded ? Color.FromArgb(40, 24, 32, 42) : Color.FromArgb(20, 20, 26, 32);
            using (Brush b = new SolidBrush(zoneSecBg)) g.FillRectangle(b, zoneHeaderRect);
            using (Pen zoneSecBorder = new Pen(Color.FromArgb(140, 42, 52, 61), 1.0f)) g.DrawRectangle(zoneSecBorder, zoneHeaderRect);

            string zoneArrow = sidebarZonesExpanded ? "▼" : "►";
            using (Font zoneArrowFont = new Font("Segoe UI", 7.0f, FontStyle.Bold)) {
                g.DrawString(zoneArrow, zoneArrowFont, Brushes.SkyBlue, innerX + 5, itemY + 5.5f);
            }

            int zoneSwW = 24, zoneSwH = 12;
            int zoneSwX = innerX + 18;
            int zoneSwY = itemY + (zoneSecH - zoneSwH) / 2;
            Rectangle zoneSwRect = new Rectangle(zoneSwX, zoneSwY, zoneSwW, zoneSwH);
            DrawSwitchPill(g, zoneSwRect, isZonesEnabled, isZonesPartial);

            RegisterClickable(new Rectangle(zoneSwX - 2, itemY, zoneSwW + 4, zoneSecH), viewportRect, () => {
                if(!showZones) {
                    showZones = true;
                    disabledZoneLayers.Clear();
                } else if(isZonesPartial) {
                    disabledZoneLayers.Clear();
                } else {
                    showZones = false;
                }
                SettingsChanged();
                lastFrameKey = null; terrainKey = null;
                RenderOverlay();
            });

            using (Font zoneHeaderFont = new Font("Segoe UI", 7.4f, isZonesEnabled ? FontStyle.Bold : FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(isZonesEnabled ? Color.WhiteSmoke : Color.FromArgb(160, 170, 180))) {
                g.DrawString(Localization.Get("SidebarLayerZones"), zoneHeaderFont, textBrush, zoneSwX + zoneSwW + 6, itemY + 3.5f);
            }

            string zoneBadgeText = customLayers.Count > 0 ? (totalCustomZones + " zones") : "0";
            using (Font zoneBadgeFont = new Font("Segoe UI", 6.8f, FontStyle.Regular))
            using (Brush zoneBadgeBrush = new SolidBrush(Color.FromArgb(120, 140, 160))) {
                SizeF zoneBadgeSize = g.MeasureString(zoneBadgeText, zoneBadgeFont);
                g.DrawString(zoneBadgeText, zoneBadgeFont, zoneBadgeBrush, innerX + innerW - zoneBadgeSize.Width - 4, itemY + 4.5f);
            }

            itemY += zoneSecH + toggleGap;

            if (sidebarZonesExpanded) {
                if (customLayers.Count == 0) {
                    using (Font noLayersFont = new Font("Segoe UI", 7.0f, FontStyle.Italic))
                    using (Brush noLayersBrush = new SolidBrush(Color.FromArgb(120, 135, 150))) {
                        g.DrawString(Localization.Get("SidebarNoCustomLayers"), noLayersFont, noLayersBrush, innerX + 26, itemY + 2);
                    }
                    itemY += 16 + toggleGap;
                } else {
                    foreach (var kvp in customLayers) {
                        string layerName = kvp.Key;
                        int zoneCount = kvp.Value;
                        bool isLayerActive = showZones && !disabledZoneLayers.Contains(layerName);

                        int subX = innerX + 16;
                        int subW = innerW - 16;
                        int subH = 19;
                        Rectangle subRowRect = new Rectangle(subX, itemY, subW, subH);

                        int childSwW = 20, childSwH = 11;
                        int childSwX = subX + 6;
                        int childSwY = itemY + (subH - childSwH) / 2;
                        Rectangle childSwRect = new Rectangle(childSwX, childSwY, childSwW, childSwH);

                        DrawSwitchPill(g, childSwRect, isLayerActive, false);

                        using (Font layerFont = new Font("Segoe UI", 7.0f, isLayerActive ? FontStyle.Bold : FontStyle.Regular))
                        using (Brush layerBrush = new SolidBrush(isLayerActive ? Color.WhiteSmoke : Color.FromArgb(145, 155, 165))) {
                            g.DrawString(layerName, layerFont, layerBrush, childSwX + childSwW + 5, itemY + 2.0f);
                        }

                        string countStr = "[" + zoneCount + "]";
                        using (Font countFont = new Font("Segoe UI", 6.8f, FontStyle.Regular))
                        using (Brush countBrush = new SolidBrush(Color.FromArgb(110, 130, 150))) {
                            SizeF cSize = g.MeasureString(countStr, countFont);
                            g.DrawString(countStr, countFont, countBrush, subX + subW - cSize.Width - 4, itemY + 2.5f);
                        }

                        string captureLayer = layerName;
                        RegisterClickable(subRowRect, viewportRect, () => {
                            if (disabledZoneLayers.Contains(captureLayer)) {
                                disabledZoneLayers.Remove(captureLayer);
                                showZones = true;
                            } else {
                                disabledZoneLayers.Add(captureLayer);
                            }
                            SettingsChanged();
                            lastFrameKey = null; terrainKey = null;
                            RenderOverlay();
                        });

                        itemY += subH + 2;
                    }
                    itemY += 2;
                }
            }



            DrawScrollableToggle(g, innerX, itemY, innerW, toggleH, Localization.Get("SidebarLayerWaypoints"), showCustomWaypoints, viewportRect, () => {



                showCustomWaypoints = !showCustomWaypoints;



                SettingsChanged();



                lastFrameKey = null; terrainKey = null;



                RenderOverlay();



            });



            itemY += toggleH + toggleGap;



            DrawScrollableToggle(g, innerX, itemY, innerW, toggleH, Localization.Get("SidebarLayerGas"), showGasStations, viewportRect, () => {



                showGasStations = !showGasStations;



                SettingsChanged();



                lastFrameKey = null; terrainKey = null;



                RenderOverlay();



            });



            itemY += toggleH + toggleGap;



            DrawScrollableToggle(g, innerX, itemY, innerW, toggleH, Localization.Get("SidebarLayerGrid"), gridBorders, viewportRect, () => {



                gridBorders = !gridBorders;



                SettingsChanged();



                lastFrameKey = null; terrainKey = null;



                RenderOverlay();



            });



            itemY += toggleH + toggleGap;



            DrawScrollableToggle(g, innerX, itemY, innerW, toggleH, Localization.Get("SidebarLayerLabels"), showZoneLabels, viewportRect, () => {



                showZoneLabels = !showZoneLabels;



                SettingsChanged();



                lastFrameKey = null; terrainKey = null;



                RenderOverlay();



            });



            itemY += toggleH + toggleGap + 4;



            DrawSidebarDivider(g, innerX, itemY, innerW);



            itemY += 10;



            // === 2. FULL POI CATEGORIES ACCORDION SYSTEM ===



            using (Font secFont = new Font("Segoe UI", 7.5f, FontStyle.Bold)) {



                using (Brush sb = new SolidBrush(OverlayTheme.Accent)) g.DrawString("POI CATEGORIES", secFont, sb, innerX, itemY + 2);



            }



            Rectangle allBtnRect = new Rectangle(innerX + innerW - 54, itemY, 25, 18);



            DrawMiniButton(g, allBtnRect, "All", true);



            RegisterClickable(allBtnRect, viewportRect, () => {



                if (scumMap != null) {



                    scumMap.SetAllEnabled(true);



                    showScumMap = true;



                    scumMap.MasterEnabled = true;



                    SettingsChanged();



                    lastFrameKey = null; terrainKey = null;



                    RenderOverlay();



                }



            });



            Rectangle noneBtnRect = new Rectangle(innerX + innerW - 26, itemY, 26, 18);



            DrawMiniButton(g, noneBtnRect, "Off", true);



            RegisterClickable(noneBtnRect, viewportRect, () => {



                if (scumMap != null) {



                    scumMap.SetAllEnabled(false);



                    SettingsChanged();



                    lastFrameKey = null; terrainKey = null;



                    RenderOverlay();



                }



            });



            itemY += 24;



            if (scumMap != null && scumMap.Sections != null) {



                foreach (string sec in scumMap.Sections) {



                    List<ScumMapCategory> cats;



                    if (!scumMap.CategoriesBySection.TryGetValue(sec, out cats) || cats == null || cats.Count == 0) continue;



                    bool isExpanded = sidebarExpandedSections.Contains(sec);



                    bool isSecEnabled = scumMap.IsSectionEnabled(sec);



                    bool isSecPartial = scumMap.IsSectionPartiallyEnabled(sec);



                    int secH = 26;



                    Rectangle secBoxRect = new Rectangle(innerX, itemY, innerW, secH);



                    using (Brush secBg = new SolidBrush(isExpanded ? Color.FromArgb(200, 22, 27, 32) : Color.FromArgb(160, 16, 20, 24))) {



                        g.FillRectangle(secBg, secBoxRect);



                    }



                    using (Pen secBorder = new Pen(Color.FromArgb(140, 42, 52, 61), 1.0f)) {



                        g.DrawRectangle(secBorder, secBoxRect);



                    }



                    string currentSec = sec;



                    RegisterClickable(secBoxRect, viewportRect, () => {



                        if (sidebarExpandedSections.Contains(currentSec)) sidebarExpandedSections.Remove(currentSec);



                        else sidebarExpandedSections.Add(currentSec);



                        InvalidateFullMapSidebar();



                        lastFrameKey = null;



                        RenderOverlay();



                    });



                    string arrow = isExpanded ? "▼" : "►";



                    using (Font arrowFont = new Font("Segoe UI", 7.0f, FontStyle.Bold)) {



                        g.DrawString(arrow, arrowFont, Brushes.SkyBlue, innerX + 6, itemY + 6);



                    }



                    int swW = 24, swH = 12;



                    int swX = innerX + 20;



                    int swY = itemY + (secH - swH) / 2;



                    Rectangle swRect = new Rectangle(swX, swY, swW, swH);



                    DrawSwitchPill(g, swRect, isSecEnabled, isSecPartial);



                    RegisterClickable(new Rectangle(swX - 2, itemY, swW + 4, secH), viewportRect, () => {



                        bool nextState = !isSecEnabled;



                        scumMap.SetSectionEnabled(currentSec, nextState);



                        if (nextState) { showScumMap = true; scumMap.MasterEnabled = true; }



                        SettingsChanged();



                        lastFrameKey = null; terrainKey = null;



                        RenderOverlay();



                    });



                    string secTitle = Localization.GetSectionName(sec);



                    using (Font sf = new Font("Segoe UI", 7.5f, FontStyle.Bold))



                    using (Brush sb = new SolidBrush(isSecEnabled ? Color.WhiteSmoke : Color.FromArgb(165, 175, 185))) {



                        g.DrawString(secTitle, sf, sb, swX + swW + 6, itemY + 5);



                    }



                    int enabledInSec = 0;



                    foreach (var c in cats) if (scumMap.IsCategoryEnabled(c.Id)) enabledInSec++;



                    string countStr = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", enabledInSec, cats.Count);



                    using (Font cf = new Font("Segoe UI", 6.8f, FontStyle.Regular))



                    using (Brush cb = new SolidBrush(enabledInSec > 0 ? Color.FromArgb(255, 159, 28) : Color.FromArgb(130, 140, 150))) {



                        SizeF cSz = g.MeasureString(countStr, cf);



                        g.DrawString(countStr, cf, cb, innerX + innerW - cSz.Width - 6, itemY + 6);



                    }



                    itemY += secH + 3;



                    if (isExpanded) {



                        if (sec == "Hunting") {



                            itemY = DrawEmbeddedWildlifeBiomes(g, innerX + 6, itemY, innerW - 6, viewportRect);



                        }



                        int catH = 20;



                        int catIndent = innerX + 14;



                        int catRowW = innerW - 14;



                        foreach (var cat in cats) {



                            var currentCat = cat;



                            bool isCatEnabled = scumMap.IsCategoryEnabled(cat.Id);



                            Rectangle catRowRect = new Rectangle(catIndent, itemY, catRowW, catH);



                            int cswW = 20, cswH = 11;



                            int cswX = catIndent;



                            int cswY = itemY + (catH - cswH) / 2;



                            Rectangle cswRect = new Rectangle(cswX, cswY, cswW, cswH);



                            DrawSwitchPill(g, cswRect, isCatEnabled, false);



                            RegisterClickable(catRowRect, viewportRect, () => {



                                scumMap.SetCategoryEnabled(currentCat.Id, !isCatEnabled);



                                if (!isCatEnabled) { showScumMap = true; scumMap.MasterEnabled = true; }



                                SettingsChanged();



                                lastFrameKey = null; terrainKey = null;



                                RenderOverlay();



                            });



                            Color catCol = Color.FromArgb(cat.ColorBackground);



                            if (catCol.A == 0) catCol = OverlayTheme.Accent;



                            using (Brush dotB = new SolidBrush(catCol)) {



                                g.FillEllipse(dotB, cswX + cswW + 5, itemY + 7, 6, 6);



                            }



                            string catName = Localization.GetCategoryName(cat.Name);



                            using (Font catFont = new Font("Segoe UI", 6.8f, isCatEnabled ? FontStyle.Bold : FontStyle.Regular))



                            using (Brush catBrush = new SolidBrush(isCatEnabled ? Color.FromArgb(220, 230, 240) : Color.FromArgb(145, 155, 165))) {



                                g.DrawString(catName, catFont, catBrush, cswX + cswW + 14, itemY + 3.5f);



                            }



                            if (cat.MarkerCount > 0) {



                                using (Font mFont = new Font("Segoe UI", 6.0f, FontStyle.Regular))



                                using (Brush mBrush = new SolidBrush(Color.FromArgb(115, 130, 145))) {



                                    string mStr = cat.MarkerCount.ToString(CultureInfo.InvariantCulture);



                                    SizeF mSz = g.MeasureString(mStr, mFont);



                                    g.DrawString(mStr, mFont, mBrush, catIndent + catRowW - mSz.Width - 4, itemY + 4.5f);



                                }



                            }



                            itemY += catH + 1;



                        }



                        itemY += 4;



                    }



                }



            }



            // === 3. QUICK TOOLS & FOOTER ===



            DrawSidebarDivider(g, innerX, itemY, innerW);



            itemY += 10;



            using (Font secFont = new Font("Segoe UI", 7.5f, FontStyle.Bold)) {



                g.DrawString(Localization.Get("SidebarTools"), secFont, Brushes.LightSlateGray, innerX, itemY);



            }



            itemY += 18;



            Rectangle toolEditorRect = new Rectangle(innerX, itemY, innerW, 24);



            DrawActionButton(g, toolEditorRect, Localization.Get("SidebarZoneEditor"), OverlayTheme.Surface);
            RegisterClickable(toolEditorRect, viewportRect, () => { if(IsHandleCreated) BeginInvoke(new Action(() => OpenZoneImport(null))); else OpenZoneImport(null); });
            itemY += 28;

            Rectangle toolGuideRect = new Rectangle(innerX, itemY, innerW, 24);
            DrawActionButton(g, toolGuideRect, Localization.Get("OpenStartupGuide"), OverlayTheme.Surface);
            RegisterClickable(toolGuideRect, viewportRect, () => { if(IsHandleCreated) BeginInvoke(new Action(() => OpenStartupGuide())); else OpenStartupGuide(); });
            itemY += 28;

            Rectangle toolSettingsRect = new Rectangle(innerX, itemY, innerW, 24);
            DrawActionButton(g, toolSettingsRect, Localization.Get("SidebarSettings"), OverlayTheme.Surface);
            RegisterClickable(toolSettingsRect, viewportRect, () => { if(IsHandleCreated) BeginInvoke(new Action(() => ShowSettings())); else ShowSettings(); });



            itemY += 34;



            // Tips Footer



            DrawSidebarDivider(g, innerX, itemY, innerW);



            itemY += 8;



            using (Font tipFont = new Font("Segoe UI", 6.6f, FontStyle.Regular))



            using (Brush tipBrush = new SolidBrush(Color.FromArgb(120, 135, 150))) {



                g.DrawString("Click & Drag: Pan / Pin GPS", tipFont, tipBrush, innerX, itemY);



                itemY += 14;



                g.DrawString("Right-Click: Reset Zoom / Clear", tipFont, tipBrush, innerX, itemY);



                itemY += 14;



                g.DrawString("Scroll Sidebar: Wheel / Scrollbar", tipFont, tipBrush, innerX, itemY);



                itemY += 14;



                g.DrawString("M / Esc: Exit Full Map", tipFont, tipBrush, innerX, itemY);



                itemY += 24;



            }



            int totalContentHeight = (itemY + sidebarScrollY) - scrollStartY;



            sidebarMaxScroll = Math.Max(0, totalContentHeight - viewportH + 20);



            g.Restore(origState);



            // === 4. VERTICAL SCROLLBAR ON RIGHT EDGE ===



            if (sidebarMaxScroll > 0) {



                int barW = 4;



                int barX = sidebarX + sidebarW - barW - 2;



                int trackY = scrollStartY;



                int sbTrackH = viewportH;



                sidebarScrollbarTrackRect = new Rectangle(barX, trackY, barW, sbTrackH);



                using (Brush trkB = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) {



                    g.FillRectangle(trkB, sidebarScrollbarTrackRect);



                }



                int thumbH = Math.Max(24, (int)((float)viewportH / totalContentHeight * sbTrackH));



                int thumbY = trackY + (int)((float)sidebarScrollY / sidebarMaxScroll * (sbTrackH - thumbH));



                sidebarScrollbarThumbRect = new Rectangle(barX, thumbY, barW, thumbH);



                using (Brush thmB = new SolidBrush(draggingSidebarScrollbar ? Color.FromArgb(220, 80, 170, 240) : Color.FromArgb(160, 70, 140, 200))) {



                    g.FillRectangle(thmB, sidebarScrollbarThumbRect);



                }



            } else {



                sidebarScrollbarThumbRect = Rectangle.Empty;



                sidebarScrollbarTrackRect = Rectangle.Empty;



            }



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



                note = Localization.T("NoteWaypointSet", Localization.GetZoneName(clickedZone.Name));



            } else if(clickedMarker != null) {



                string mName = scumMap.GetMarkerDisplayName(clickedMarker);



                PointF mPt = clickedMarker.Point;



                string sec = SectorFromMapPt(mPt);



                searchTarget = new MapZone {



                    Name = mName,



                    Points = new PointF[] { mPt },



                    Category = ZoneCategory.Custom



                };



                pin = new Position { X = worldX, Y = worldY, Z = worldZ };



                note = Localization.T("NoteWaypointSet", Localization.GetZoneName(mName));



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



                note = Localization.T("NoteWaypointSet", searchTarget.Name);



            }



            UpdateRouteAsync(position != null ? ToMap(position) : new PointF(.5f, .5f), searchTarget);



            SettingsChanged();



            lastFrameKey = null;



            terrainKey = null;



            RenderOverlay();



        }



        void Tick(object sender,EventArgs args) {
            Debug.Assert(!InvokeRequired,"Sampling timer must run on the WinForms UI thread.");
            if(diagnosticMode || closing || IsDisposed) return;
            if(tickBusy) return;
            tickBusy=true;
            try {
            DateTime now=DateTime.UtcNow;
            if(now>=saveAfter)SaveSettings();
            // Menus, dialogs, Alt-Tab and injected copy input can make a low-level hook
            // miss a matching key-up. Reconcile the hook's transition view with Windows'
            // physical state before it can block sampling or a shortcut.
            keys.Resync();
            bool gameFocused=Native.GameFocused();
            bool ourWindowFocused=Native.IsOurWindow(Native.GetForegroundWindow());
            bool gameOrOurFocused=gameFocused || ourWindowFocused;
            bool isAltDown=(Native.GetAsyncKeyState(0x12)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA4)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
            if(isAltDown) lastAltDownTime=now;
            bool altTabRecent=isAltDown || (now - lastAltDownTime).TotalMilliseconds < 500 || (now - Native.LastAltTabTime).TotalMilliseconds < 500;
            if(!altTabRecent) {
                overlay.SetGameFocus(gameFocused);
                if(gameFocused && !wasGameFocused) {
                    // SCUM regained focus: restore overlay and engage Auto mode
                    if(hiddenByFocusLoss) { overlay.Show(); RenderOverlay(); hiddenByFocusLoss=false; }
                    // Automatically resume sampling when gameplay regains focus.
                    failures=0;
                    next=now; // trigger immediate request
                }
                if(!gameOrOurFocused && wasGameFocused && !Visible && !searchOpen) {
                    // Both SCUM and our overlay lost focus: hide overlay entirely and cease input
                    if(overlay.Visible) { overlay.Hide(); hiddenByFocusLoss=true; }
                }
                wasGameFocused=gameOrOurFocused;
            }
            if(gameFocused && Native.IsRightMouseDown()) {
                resumeAfter = now.AddMilliseconds(600);
            }
            if(!gameFocused) inputGeneration++;
            // Showing the full map can briefly move the foreground window while the
            // layered form is being sized. Do not close it during that transition;
            // still close it after the grace period when the user really Alt-Tabs away.
            if(fullMapActive && !gameOrOurFocused && (now-fullMapOpenedAt).TotalMilliseconds>750) SetFullMap(false);
            // Hardware polling fallback for non-text hotkeys in case the low-level hook drops.
            // Do not poll M here: polling cannot tell whether SCUM's chat box owns the
            // keystroke, while the physical hook can.
            bool shortcutFocused=gameFocused || (fullMapActive && ourWindowFocused);
            if(shortcutFocused && !panelOpening) {
                if(!fallbackKeysArmed) ArmFallbackKeys();
                if(fullMapActive && FallbackKeyPressed(0x1B)) SetFullMap(false);
                if(!chat.Paused && FallbackKeyPressed(scumMapKey)) TriggerFullMap();
                if(FallbackKeyPressed(0x23)) TriggerToggleOverlay();
                if(FallbackKeyPressed(0x24)) ToggleSettings();
                if(FallbackKeyPressed(0x2E)) TriggerZoneSearch();
                if(FallbackKeyPressed(0x2D)) {
                    if(IsHandleCreated) BeginInvoke(new Action(TriggerPin));
                    else TriggerPin();
                }
                if(FallbackKeyPressed(0x21)) TriggerZoomIn();
                if(FallbackKeyPressed(0x22)) TriggerZoomOut();
            } else {
                fallbackKeysArmed=false;
            }
            // Cursor visibility is not an authoritative map-open signal.
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
                if(current!=sequence) {
                    string text=Clipboard.ContainsText()?Clipboard.GetText():null;
                    Position p=Position.Parse(text);
                    sequence=current;
                    if(p!=null) {
                        Accept(text); failures=0;
                        if(pending) { responses++; note=Localization.Get("NoteCoordReceived"); }
                        if(pending && clipboardRestoreAllowed && Native.GetClipboardSequenceNumber()==current) {
                            try {
                                if(savedDataObject!=null) Clipboard.SetDataObject(savedDataObject,true);
                                else Clipboard.Clear();
                            } catch {}
                            sequence=Native.GetClipboardSequenceNumber();
                        }
                    }
                    if(p!=null) pending=false;
                    else if(pending) clipboardRestoreAllowed=false;
                }
                if(pending && !copyInProgress && (now-sent).TotalSeconds>1) {
                    pending=false; failures++; note=Localization.T("NoteNoCoordsAttempt",failures);
                    if(failures>=3) { next=now.AddSeconds(5); note=Localization.Get("NoteNoCoordsRetry"); }
                }
                if(TrackingEnabled && !pending) {
                    if(chat.Paused)note=Localization.Get("NoteChatOpen");
                    else if(Native.IsRightMouseDown())note=Localization.Get("NoteAdsActive");
                    else if(!keys.Available)note=Localization.Get("NoteChatMonitorUnavailable");
                    else if(!Native.GameFocused()) note=Localization.Get("NoteWaitingForeground");
                    else if(Native.KeysBusy(scumCopyModifierKey,scumCopyKey)) note=Localization.Get("NoteWaitingUserKeys");
                }
                if(TrackingEnabled && keys.Available && !panelOpening && !Visible && !searchOpen && !chat.Paused && !pending && !copyInProgress && !altTabRecent && now>=resumeAfter && now>=next && Native.GameFocused() && !Native.KeysBusy(scumCopyModifierKey,scumCopyKey)) {
                    int interval=copyIntervalMs;
                    if(position!=null && filteredSpeed<0.5) interval=Math.Max(copyIntervalMs,1000);
                    next=now.AddMilliseconds(interval);
                    PerformCopyAsync();
                }
            } catch(ExternalException) { note=Localization.Get("NoteClipboardBusy"); }



            string age=position==null?Localization.Get("WaitingForCoordinates"):string.Format(CultureInfo.InvariantCulture,"X {0:F0}  Y {1:F0}  Z {2:F0}  |  {3}",position.X,position.Y,position.Z,Localization.T("SecondsAgo",(int)(now-updated).TotalSeconds));



            if(Visible) {
                string statusText=age+"  |  "+Localization.T("StatusSummary",attempts,responses)+(string.IsNullOrEmpty(note)?"":Environment.NewLine+note);
                if(status.Text!=statusText) status.Text=statusText;
            }



            // Routine successes are visible in Settings; avoid synchronous disk I/O every copy cycle.



            bool routine=pending || note==Localization.Get("NoteCoordReceived");



            string diagnostic=routine?Localization.Get("DiagCopyActive"):note;



            if(diagnostic!=lastDiagnostic) {



                lastDiagnostic=diagnostic;



                try { File.AppendAllText(diagnosticPath,DateTime.UtcNow.ToString("o")+" "+diagnostic+Environment.NewLine); } catch(IOException) {} catch(UnauthorizedAccessException) {}



            }



            bool stale=position!=null && (now-updated).TotalSeconds>5;



            if(stale!=canvasStale) { canvasStale=stale; if(Visible)canvas.Invalidate(); }



            RenderOverlay();



            } catch(Exception ex) {



                Program.LogException("Tick", ex);



            } finally {



                tickBusy=false;



                if(closing) {



                    try { BeginInvoke(new Action(Close)); } catch {}



                }



            }



        }

        void BeginCopyRequest() { clipboardRestoreAllowed=true; pending=true; sent=DateTime.UtcNow; }
        void CompleteCopyRequest(CopyResult result) {
            sent=DateTime.UtcNow;
            // A response consumed during the chord has already cleared pending.
            if(result!=CopyResult.Sent) pending=false;
        }

        async void PerformCopyAsync() {
            if(copyInProgress || closing || IsDisposed) return;
            copyInProgress = true;
            try {
                IDataObject original=Clipboard.GetDataObject();
                DataObject snapshot=new DataObject();
                if(original!=null) foreach(string format in original.GetFormats(false)) snapshot.SetData(format,false,original.GetData(format,false));
                savedDataObject=snapshot;
                sequence = Native.GetClipboardSequenceNumber();
                if(Native.GameFocused() && !Native.KeysBusy(scumCopyModifierKey, scumCopyKey) && !Native.IsAltOrTabOrWinDown()) {
                    attempts++;
                    BeginCopyRequest();
                    CopyResult copyResult = await Native.Copy(
                        () => !closing && !panelOpening && !Visible && !searchOpen && TrackingEnabled && !chat.Paused && DateTime.UtcNow >= resumeAfter,
                        scumCopyModifierKey, scumCopyKey);
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



        public void SaveOverlayPreview(string path) { OverlayBitmap().Save(path,ImageFormat.Png); }



        public void CheckSearchPreview(string output) {



            if(!diagnosticMode)throw new InvalidOperationException("Search check requires diagnostic mode.");



            Exception failure=null; bool checkedDialog=false;



            using(System.Windows.Forms.Timer check=new System.Windows.Forms.Timer { Interval=100 }) {



                check.Tick+=(sender,args)=> {



                    Form dialog=null;



                    foreach(Form open in Application.OpenForms) if(open.Text=="Search place or grid" || open.Text==Localization.Get("SearchTitle"))dialog=open;



                    if(dialog==null)return;



                    check.Stop();



                    try {



                        TextBox input=null; ListBox results=null; Button choose=null;



                        foreach(Control control in dialog.Controls) {



                            if(control is TextBox)input=(TextBox)control;



                            if(control is ListBox)results=(ListBox)control;



                            foreach(Control child in control.Controls) if(child is Button && (child.Text=="Set waypoint" || child.Text==Localization.Get("SetWaypoint")))choose=(Button)child;



                        }



                        if(input==null || results==null || choose==null)throw new Exception("Search controls missing.");



                        input.Text="airport";



                        if(results.Items.Count==0)throw new Exception("Default airfield is missing from search.");



                        input.Text="zzzzzzzz";



                        if(results.Items.Count!=0 || choose.Enabled)throw new Exception("No-results state allowed navigation.");



                        input.Text="bunker C3";



                        input.Text="grid D4";



                        if(results.Items.Count==0 || !choose.Enabled)throw new Exception("Grid search has no selectable result.");



                        using(Bitmap bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(output,ImageFormat.Png); }



                        checkedDialog=true; choose.PerformClick();



                    } catch(Exception ex) { failure=ex; dialog.Close(); }



                };



                check.Start(); ShowZoneSearch();



            }



            if(failure!=null)throw failure;



            if(!checkedDialog || searchTarget==null || searchTarget.Name!="Grid D4")throw new Exception("Search did not create the selected waypoint.");



        }



        public void SetTestSearchTarget(MapZone target) {
            searchTarget = target;
            cachedLocationTarget = null;
        }



        public void SaveSettingsPreview(string path) {



            ShowSettings(); PerformLayout();



            using(Bitmap bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(path,ImageFormat.Png); }



            Hide();



        }



        public void CheckZoneEditor(string path) {



            using(ZoneEditor editor=new ZoneEditor(map,new List<MapZone>(),zonesPath,value=>{}))editor.CheckAndPreview(path);



        }



        public void CheckStaticTerrain() {



            Position original=position; bool originalAuto=autoZoom; float originalZoom=zoom;



            try {



                autoZoom=false;



                zoom=1;



                OverlayBitmap(); int builds=TerrainBuilds;



                Position source=position??new Position();



                position=new Position { X=source.X+1000,Y=source.Y+1000,Z=source.Z,Yaw=source.Yaw+10 };



                OverlayBitmap();



                if(TerrainBuilds!=builds)throw new Exception("Full map view unexpectedly rebuilt or moved the terrain.");



                zoom=4; OverlayBitmap();



                if(TerrainBuilds<=builds)throw new Exception("Zoom did not refresh the terrain cache.");



            } finally { position=original; autoZoom=originalAuto; zoom=originalZoom; terrainKey=null; lastFrameKey=null; }



        }



        public void CheckAppearance() {



            Size previous=overlay.Size; bool oldLabels=gridLabels,oldBorders=gridBorders,oldAutoZoom=autoZoom; float oldZoom=zoom;



            overlay.Size=new Size(480,300); zoom=1; autoZoom=false;



            Bitmap resized=OverlayBitmap();



            if(resized.Width!=480 || resized.Height!=300)throw new Exception("Overlay did not resize.");



            gridLabels=false;gridBorders=false;



            gridBorders=true;



            // Compare whole images: a border switch must change raster content.



            using(Bitmap before=(Bitmap)OverlayBitmap().Clone()) {



                gridBorders=false; Bitmap after=OverlayBitmap(); bool different=false;



                for(int y=30;y<270&&!different;y++)for(int x=90;x<390;x++)if(before.GetPixel(x,y)!=after.GetPixel(x,y)) { different=true;break; }



                if(!different)throw new Exception("Grid border switch did not change the map.");



            }



            overlay.Size=previous;gridLabels=oldLabels;gridBorders=oldBorders;zoom=oldZoom;autoZoom=oldAutoZoom;targetZoom=oldZoom;



            lastFrameKey=null;saveAfter=DateTime.MaxValue;



        }



        string GetLocationDescription() {



            // OverlayKey runs on every timer tick, including the smooth-motion frames that do



            // not change the sampled position.  Avoid rescanning/sorting all zones twice per



            // presented frame until an input that can change this label actually changes.



            if(cachedLocationDescription!=null &&



                ReferenceEquals(cachedLocationPosition,position) &&



                ReferenceEquals(cachedLocationTarget,searchTarget) &&



                ReferenceEquals(cachedLocationRoute,activeRoute) &&



                cachedLocationShowElevation==showElevation &&



                cachedLocationDescriptionRevision==locationDescriptionRevision) {



                return cachedLocationDescription;



            }



            string description=BuildLocationDescription();



            cachedLocationDescription=description;



            cachedLocationPosition=position;



            cachedLocationTarget=searchTarget;



            cachedLocationRoute=activeRoute;



            cachedLocationShowElevation=showElevation;



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
                    result = sector + " - " + Localization.GetZoneName(a.Name);
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
                                    result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedAnchorName), Localization.GetZoneName(targetAnchor.Name));
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
                                            result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedAnchorName), Localization.GetZoneName(candidate.Name));
                                            foundSpecific = true;
                                            break;
                                        }
                                    }
                                    result = Localization.T("LocApproaching", sector, Localization.GetZoneName(candidate.Name));
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
                        result = Localization.T("LocNear", sector, Localization.GetZoneName(closest.Name));
                    } else if (closestDist < 0.08 && hasSecond && secondDist < 0.08 && Math.Abs(closestDist - secondDist) < 0.035) {
                        result = Localization.T("LocBetween", sector, Localization.GetZoneName(closest.Name), Localization.GetZoneName(second.Name));
                    } else if (closestDist < 0.08) {
                        result = Localization.T("LocNear", sector, Localization.GetZoneName(closest.Name));
                    }
                }
            }

            
            // 5. Trip information (Destination, distance, approx time / ETA)
            if (searchTarget != null) {
                string targetName = Localization.GetZoneName(searchTarget.Name);
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
                .Append(TrackingEnabled).Append('|')
                .Append(overlay.Width).Append('|')
                .Append(overlay.Height).Append('|')
                .Append(showStatus).Append('|')
                .Append(statusPos).Append('|')
                .Append(overlayShape).Append('|')
                .Append(showHeading).Append('|')
                .Append(showCompass).Append('|')
                .Append(showElevation).Append('|')
                .Append(GetLocationDescription()).Append('|')
                .Append(searchTarget==null?"":searchTarget.Name).Append('|')
                .Append(routeGuidanceColor.ToArgb()).Append('|')
                .Append(playerConeColor.ToArgb()).Append('|')
                .Append(mx).Append('|')
                .Append(my).Append('|')
                .Append(myaw).Append('|')
                .Append(fullMapActive).Append('|')
                .Append((int)Math.Round(fullMapZoom*100)).Append('|')
                .Append((int)Math.Round(fullMapPan.X*1000)).Append('|')
                .Append((int)Math.Round(fullMapPan.Y*1000)).Append('|')
                .Append(fullMapOpacity).Append('|')
                .Append(InfobarScrollFrame());
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



            if(overlayFrame.Width!=overlay.Width || overlayFrame.Height!=overlay.Height) {



                terrainKey=null; overlayFrame.Dispose(); fullMap.Dispose();



                overlayFrame=new Bitmap(overlay.Width,overlay.Height,PixelFormat.Format32bppPArgb);



                fullMap=new Bitmap(overlay.Width,overlay.Height,PixelFormat.Format32bppPArgb);



            }



            Bitmap bitmap=overlayFrame;



            const int barH=28;



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



            OverlayWindow.Fade(bitmap,fullMapActive?false:edgeFade,fullMapActive?fullMapOpacity:mapOpacity,zoom==1?mapLeft:-1,zoom==1?mapTop:-1,zoom==1?mapRight:-1,zoom==1?mapBottom:-1,fullMapActive?"Square":overlayShape);



            // Now draw the crisp HUD location info banner outside/over the map so it is not faded



            if(barActive) {



                using(Graphics g=Graphics.FromImage(bitmap)) {



                    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;



                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;



                    string loc=GetLocationDescription();



                    Color badgeColor=chat.Paused?Color.FromArgb(240,195,65):(searchTarget!=null?routeGuidanceColor:(TrackingEnabled?Color.FromArgb(57,211,83):Color.FromArgb(150,146,138)));



                    using(Font locFont=new Font("Segoe UI",8.25f,FontStyle.Bold))



                    {



                        SizeF locSz=g.MeasureString(loc,locFont);



                        if(overlayShape=="Circle") {



                            int pillH = 26;



                            int pillW = Math.Min(bitmap.Width - 12, Math.Max(160, (int)Math.Ceiling(locSz.Width) + 38));



                            int pillX = (bitmap.Width - pillW) / 2;



                            int pillY = barAbove ? 2 : bitmap.Height - pillH + 1;



                            Rectangle pillRect = new Rectangle(pillX, pillY, pillW, pillH);



                            using(GraphicsPath pillPath = new GraphicsPath()) {



                                int r = pillH;



                                pillPath.AddArc(pillRect.X, pillRect.Y, r, r, 90, 180);



                                pillPath.AddArc(pillRect.Right - r, pillRect.Y, r, r, 270, 180);



                                pillPath.CloseFigure();



                                using(Brush bg = new SolidBrush(Color.FromArgb(245, 12, 12, 12))) {



                                    g.FillPath(bg, pillPath);



                                }



                                using(Pen border = new Pen(searchTarget != null ? routeGuidanceColor : Color.FromArgb(220, 50, 48, 44), 1.2f)) {



                                    g.DrawPath(border, pillPath);



                                }



                            }



                            using(Brush b = new SolidBrush(badgeColor)) {



                                g.FillEllipse(b, pillX + 11, pillY + (pillH - 8) / 2, 8, 8);



                            }



                            RectangleF textRect=new RectangleF(pillX+24,pillY+1,pillW-36,pillH-2);



                            DrawScrollingInfobarText(g,loc,locFont,Brushes.White,textRect);



                        } else {



                            int barY = barAbove ? 0 : bitmap.Height - barH;



                            using(Brush b = new SolidBrush(Color.FromArgb(245, 12, 12, 12))) {



                                g.FillRectangle(b, 0, barY, bitmap.Width, barH);



                            }



                            using(Pen border = new Pen(searchTarget != null ? routeGuidanceColor : Color.FromArgb(200, 50, 48, 44), 1.0f)) {



                                g.DrawLine(border, 0, barAbove ? barH - 1 : barY, bitmap.Width, barAbove ? barH - 1 : barY);



                            }



                            using(Brush b = new SolidBrush(badgeColor)) {



                                g.FillEllipse(b, 10, barY + (barH - 8) / 2, 8, 8);



                            }



                            RectangleF textRect=new RectangleF(24,barY+1,Math.Max(1,bitmap.Width-34),barH-2);



                            DrawScrollingInfobarText(g,loc,locFont,Brushes.White,textRect);



                        }



                    }



                }



            }



            if(fullMapActive) {



                using(Graphics g = Graphics.FromImage(bitmap)) {



                    g.SmoothingMode = SmoothingMode.AntiAlias;



                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;



                    // Right sidebar panel for all controls (cleanly separated from the map)



                    if(sidebarW > 0) {



                        DrawCachedFullMapSidebar(g, mapW, sidebarW, bitmap.Height);



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
            Debug.Assert(!InvokeRequired,"Overlay rendering must run on the WinForms UI thread.");



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



        void DrawMap(Graphics g,int width,int height) {



            DrawMapArea(g,new Rectangle(0,0,width,height));



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

            string terrain=string.Join("|",new object[]{bounds,effectiveZoom,isPlayerCentered?(int)Math.Round(left*8):(int)Math.Round(left*4),isPlayerCentered?(int)Math.Round(top*8):(int)Math.Round(top*4),gridLabels,gridBorders,gridOpacity,showZones,string.Join(",",disabledZoneLayers),showGasStations,showCities,showTowns,showFarms,showTraders,showFactions,showMilitary,showBunkers,showCustomWaypoints,showScumMap,(scumMap!=null?scumMap.GetFilterHashKey():""),showZoneLabels,smartLabelLod,fullMapActive,labelSize,Localization.CurrentCode});
            if(terrainKey!=terrain) {
                using(Graphics background=Graphics.FromImage(fullMap)) {
                    background.Clear(Color.FromArgb(0,0,0,0));
                    using(Brush bgBrush=new SolidBrush(Color.FromArgb(12,17,22))) background.FillRectangle(bgBrush,bounds);
                    background.SmoothingMode=SmoothingMode.AntiAlias;
                    background.InterpolationMode=InterpolationMode.Bilinear;
                    background.PixelOffsetMode=PixelOffsetMode.HighSpeed;
                    Image texture=map;
                    foreach(Bitmap level in mapLevels) { if(level.Width<side || level.Height<side)break; texture=level; }
                    background.DrawImage(texture,left,top,side,side);
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
                    if(categoryDrawList.Count>0)ZoneStore.Draw(background,categoryDrawList,new RectangleF(left,top,side,side),true,labelSize,showGasStations,BuildHiddenCategories(),showZoneLabels,smartLabelLod,currentZoom);
                    if(customZoneDrawList.Count>0)ZoneStore.Draw(background,customZoneDrawList,new RectangleF(left,top,side,side),true,labelSize,false,null,showZoneLabels,smartLabelLod,currentZoom);
                    if(waypointDrawList.Count>0)ZoneStore.Draw(background,waypointDrawList,new RectangleF(left,top,side,side),false,labelSize,false,null,showZoneLabels,smartLabelLod,currentZoom);



                    if(showScumMap && scumMap!=null)scumMap.Draw(background,new RectangleF(left,top,side,side),currentZoom,showZoneLabels,labelSize,smartLabelLod);



                }



                terrainKey=terrain; TerrainBuilds++;



            }



            g.DrawImageUnscaled(fullMap,0,0);



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



                    PointF[] screenPts=new PointF[activeRoute.Polyline.Length];



                    for(int i=0; i<screenPts.Length; i++) {



                        screenPts[i]=new PointF(left+activeRoute.Polyline[i].X*side, top+activeRoute.Polyline[i].Y*side);



                    }



                    using(Pen glowPen=new Pen(Color.FromArgb(70,routeGuidanceColor),4.2f)) {



                        glowPen.LineJoin=LineJoin.Round;



                        g.DrawLines(glowPen,screenPts);



                    }



                    using(Pen routePen=new Pen(Color.FromArgb(235,routeGuidanceColor),2.2f)) {



                        routePen.LineJoin=LineJoin.Round;



                        g.DrawLines(routePen,screenPts);



                    }



                    if(activeRoute.HasWaterTransit && activeRoute.WaterDeparturePoint!=PointF.Empty && activeRoute.WaterArrivalPoint!=PointF.Empty) {



                        float wdx=left+activeRoute.WaterDeparturePoint.X*side, wdy=top+activeRoute.WaterDeparturePoint.Y*side;



                        float wax=left+activeRoute.WaterArrivalPoint.X*side, way=top+activeRoute.WaterArrivalPoint.Y*side;



                        using(Pen waterPen=new Pen(Color.FromArgb(240,60,190,255),3.2f)) {



                            waterPen.DashStyle=DashStyle.Dash;



                            waterPen.DashPattern=new float[]{6f,4f};



                            g.DrawLine(waterPen,wdx,wdy,wax,way);



                        }



                    }



                } else {



                    // Fallback to straight dashed navigation line



                    using(Pen navPen=new Pen(Color.FromArgb(220,routeGuidanceColor),3.0f)) {



                        navPen.DashStyle=DashStyle.Dash;



                        navPen.DashPattern=new float[]{6f,4f};



                        g.DrawLine(navPen,px,py,tx,ty);



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



                string targetDisplayName=Localization.GetZoneName(searchTarget.Name);



                using(Font navFont=new Font("Segoe UI",7.5f,FontStyle.Bold)) {



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



                string distStr=distM>1000?string.Format(CultureInfo.InvariantCulture,"{0:F1} km{1}",distM/1000.0,distSuffix):((int)distM)+"m"+distSuffix;



                float mx=(px+tx)/2f, my=(py+ty)/2f;



                using(Font distFont=new Font("Segoe UI",7f,FontStyle.Bold)) {



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



                using(Font compFont=new Font("Segoe UI",8.5f,FontStyle.Bold))



                using(Brush compBrush=new SolidBrush(Color.FromArgb(200,Color.WhiteSmoke)))



                using(Brush nBrush=new SolidBrush(Color.FromArgb(230,Color.Coral))) {











                    g.DrawString("N",compFont,nBrush,cx-5,bounds.Top+4);



                    g.DrawString("S",compFont,compBrush,cx-5,bounds.Bottom-18);



                    g.DrawString("W",compFont,compBrush,bounds.Left+5,cy-7);



                    g.DrawString("E",compFont,compBrush,bounds.Right-16,cy-7);



                }



            }



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



        void UpdateRouteAsync(PointF playerPt, MapZone target) {
            if(closing || IsDisposed) return;
            if(target==null || !RoadRouter.Instance.IsLoaded) { activeRoute=null; routeGeneration++; lastRouteTarget=null; return; }
            if(!Object.ReferenceEquals(lastRouteTarget,target)) activeRoute=null;
            if(lastRouteTarget==target && lastRoutePlayerPt!=PointF.Empty && RoadRouter.DistanceMeters(lastRoutePlayerPt,playerPt)<25 && activeRoute!=null) return;
            if(routeCalculating) return;
            // Ensure a UI handle exists before queueing; async continuations in diagnostic
            // hosts do not necessarily have a WinForms synchronization context.
            IntPtr uiHandle=Handle;
            routeCalculating=true;
            long request=routeGeneration;
            PointF targetPoint=target.Centroid;
            ThreadPool.QueueUserWorkItem(state=> {
                RoadRoute result=null; Exception failure=null;
                try { result=RoadRouter.Instance.FindRoute(playerPt,targetPoint); }
                catch(Exception ex) { failure=ex; }
                try {
                    if(IsDisposed) return;
                    BeginInvoke((Action)(()=> {
                        routeCalculating=false;
                        if(failure!=null) { Program.LogException("Route",failure); return; }
                        if(closing || IsDisposed || request!=routeGeneration || !Object.ReferenceEquals(searchTarget,target) || target.Centroid!=targetPoint) return;
                        activeRoute=result; lastRouteTarget=target; lastRoutePlayerPt=playerPt;
                        lastFrameKey=null;
                        if(Visible) canvas.Invalidate();
                        RenderOverlay();
                    }));
                } catch(InvalidOperationException) { /* Window closed while the result was queued. */ }
            });
        }

        static bool DestinationReached(MapZone target,PointF current) {



            return target!=null && DestinationSearch.Metres(current,target.Centroid)<=30;



        }



        public static void SelfTest() {



            var arrivalTarget=new MapZone { Name="Destination",Points=new[]{new PointF(.5f,.5f)} };



            if(!DestinationReached(arrivalTarget,new PointF(.5f,.5f))



                || !DestinationReached(arrivalTarget,new PointF(.501f,.5f))



                || DestinationReached(arrivalTarget,new PointF(.503f,.5f))



                || DestinationReached(null,new PointF(.5f,.5f))) throw new Exception("Waypoint arrival radius failed.");



            if(Native.ClassifyCopy(true,"")!=CopyResult.Sent



                || Native.ClassifyCopy(false,"")!=CopyResult.Cancelled



                || Native.ClassifyCopy(false,"SendInput failed")!=CopyResult.Failed) throw new Exception("Copy cancellation must not trigger failure backoff.");



            var chord=new List<string>();



            Func<uint,bool,bool> record=(key,up)=> { chord.Add(key+":"+up); return true; };



            Func<int,Task> noDelay=ms=>Task.FromResult(0);



            if(!Native.CopyChord(record,noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,162:True") throw new Exception("Copy chord ordering failed.");



            chord.Clear();



            if(Native.CopyChord(record,noDelay,()=>false).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,162:True") throw new Exception("Cancelled copy emitted C.");



            chord.Clear();



            bool failedRelease=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==67 && up && !failedRelease) { failedRelease=true; return false; } return true; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,67:True,162:True") throw new Exception("Failed C release was not retried before Control release.");



            chord.Clear();



            if(Native.CopyChord((key,up)=> { record(key,up); return key!=162 || up; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False") throw new Exception("Failed Control press emitted C.");



            chord.Clear();



            if(Native.CopyChord((key,up)=> { record(key,up); return key!=67 || up; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,162:True") throw new Exception("Failed C press did not release Control.");



            chord.Clear();



            bool ctrlReleaseFailed=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==162 && up && !ctrlReleaseFailed) { ctrlReleaseFailed=true; return false; } return true; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,162:True,162:True") throw new Exception("Failed Control release was not retried.");



            chord.Clear();



            try {



                Native.CopyChord(record,ms=> { if(ms==30)throw new InvalidOperationException("Simulated interruption"); return Task.FromResult(0); },()=>true).GetAwaiter().GetResult();



                throw new Exception("Simulated input interruption was ignored.");



            } catch(InvalidOperationException) {



            }



            chord.Clear();



            if(!Native.CopyChord(record,noDelay,()=>true,0,67).GetAwaiter().GetResult()



                || string.Join(",",chord)!="67:False,67:True") throw new Exception("Single key copy chord ordering failed.");



            chord.Clear();



            if(Native.CopyChord(record,noDelay,()=>false,0,67).GetAwaiter().GetResult()



                || chord.Count!=0) throw new Exception("Cancelled single key copy emitted key.");



            chord.Clear();



            bool singleReleaseFailed=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==67 && up && !singleReleaseFailed) { singleReleaseFailed=true; return false; } return true; },noDelay,()=>true,0,67).GetAwaiter().GetResult()



                || string.Join(",",chord)!="67:False,67:True,67:True") throw new Exception("Failed single key release was not retried.");



            ZoneStore.SelfTest();



            DestinationSearch.SelfTest();



            Localization.SelfTest();



            MapMotion.SelfTest();



            ScumMapStore.SelfTest();



            if(ReadCopyInterval(1,true)!=1000 || ReadCopyInterval(3,true)!=3000 || ReadCopyInterval(0,false)!=1000 || ReadCopyInterval(500,false)!=1000 || ReadCopyInterval(int.MaxValue,false)!=10000)throw new Exception("Sampling interval migration failed.");



            if(!GameFocusReturn.ShouldRestore(true,10,10,20,20)



                || GameFocusReturn.ShouldRestore(true,10,10,30,20)



                || GameFocusReturn.ShouldRestore(false,10,10,20,20)



                || GameFocusReturn.ShouldRestore(true,11,10,20,20)) throw new Exception("Focus return policy failed.");



            foreach(char row in "DCBAZ") for(int col=0;col<5;col++) {



                MapZone target=GridTarget(row.ToString()+col);



                PointF point=target.Centroid;



                if(point.X<=0 || point.X>=1 || point.Y<=0 || point.Y>=1) throw new Exception("Grid target outside map.");



                if(Math.Abs(point.X-(4-col+0.5)/5)>0.002 || Math.Abs(point.Y-("DCBAZ".IndexOf(row)+0.5)/5)>0.002) throw new Exception("Grid orientation incorrect.");



            }



            if(GridTarget(" c 3 ")==null || GridTarget("E3")!=null || GridTarget("C5")!=null || GridTarget("C33")!=null) throw new Exception("Grid validation failed.");



            MapZone first=new MapZone { Name="Town",Points=new[]{new PointF(.2f,.3f)} };



            MapZone second=new MapZone { Name="Town",Points=new[]{new PointF(.7f,.8f)} };



            List<MapZone> found=FindDestinations(new List<MapZone>{first,second}," tOw ");



            if(found.Count!=2 || !object.ReferenceEquals(found[1],second)) throw new Exception("Place search lost duplicate identity.");



            if(FindDestinations(new List<MapZone>(),"Z0").Count!=1 || FindDestinations(new List<MapZone>{first},"missing").Count!=0) throw new Exception("Empty search handling failed.");



            ChatState state=new ChatState();



            PhysicalKeyTransitions transitions=new PhysicalKeyTransitions();



            bool previousCopy=Native.CopyInProgress;



            try {



                Native.CopyInProgress=true;



                if(transitions.ShortcutModifiersDown(false)) throw new Exception("Sampler blocked physical map input.");



                transitions.Update(0xA0,true);



                if(transitions.ShortcutModifiersDown(false) || !transitions.ShortcutModifiersDown(true))



                    throw new Exception("Sprint modifier policy failed.");



                transitions.Update(0xA2,true);



                if(!transitions.ShortcutModifiersDown(false)) throw new Exception("Physical Ctrl not detected.");



                transitions.Update(0xA2,false);



                transitions.Update(0xA0,false);



                if(transitions.ShortcutModifiersDown(true)) throw new Exception("Released modifiers remained active.");



            } finally { Native.CopyInProgress=previousCopy; }



            if(!transitions.Update(0x4D,true)) throw new Exception("First map press lost.");



            for(int repeat=0;repeat<100;repeat++)



                if(transitions.Update(0x4D,true)) throw new Exception("Held map key repeated.");



            if(transitions.Update(0x4D,false) || !transitions.Update(0x4D,true))



                throw new Exception("Map key did not rearm on release.");



            if(transitions.Update(-1,true) || transitions.Update(256,true)) throw new Exception("Invalid key accepted.");



            state.Key(0x54); state.Key(0x54);



            if(!state.Paused)throw new Exception("T must pause without toggling.");



            state.Key(0x4D); if(!state.Paused)throw new Exception("Map key cleared chat gate.");



            state.Key(0xBF); if(!state.Paused)throw new Exception("Typing must preserve chat pause.");



            state.Key(0x09); if(!state.Paused)throw new Exception("Tab while chat open must preserve chat pause.");



            state.Key(0x4D); if(!state.Paused)throw new Exception("Typing after Tab must preserve chat pause.");



            state.Key(0x0D); if(state.Paused)throw new Exception("Enter must resume.");



            state.Key(0x09); if(state.Paused)throw new Exception("Tab while chat closed must not open chat.");



            state.Key(0x54); state.Key(0x1B); if(state.Paused)throw new Exception("Escape must resume.");



            using(Bitmap fade=new Bitmap(60,60,PixelFormat.Format32bppPArgb)) {



                using(Graphics g=Graphics.FromImage(fade))g.Clear(Color.White);



                OverlayWindow.Fade(fade);



                if(fade.GetPixel(0,30).A!=0 || fade.GetPixel(29,29).A<230 || fade.GetPixel(10,30).A>=fade.GetPixel(29,29).A)throw new Exception("Edge alpha fade failed.");



            }



            Position p=Position.Parse("{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");



            if(p==null || p.Y!=-270302.625 || p.Yaw!=13.952554) throw new Exception("Coordinate/rotation parsing failed.");



            if(Position.Parse("hello")!=null || Position.Parse("prefix {X=1 Y=2 Z=3|P=0 Y=0 R=0}")!=null) throw new Exception("Invalid clipboard text accepted.");



            PointF corner=ToMap(new Position { X=617718,Y=618618 });



            PointF end=ToMap(new Position { X=-903900,Y=-905000 });



            if(corner.X!=0 || corner.Y!=0 || Math.Abs(end.X-1)>.00001 || Math.Abs(end.Y-1)>.00001) throw new Exception("Map projection failed.");



            if(RoadRouter.Instance.IsLoaded) {



                var testRoute=RoadRouter.Instance.FindRoute(new PointF(0.62767f,0.63618f),new PointF(0.67270f,0.03878f));



                if(!testRoute.Success || testRoute.Polyline==null || testRoute.Polyline.Length<10) {



                    throw new Exception("Road route self-test failed.");



                }



            }



        }



    }



    public static class Program {



        internal static string LocalUpdateSource;



        internal static void LogException(string source, Exception ex) {



            try {



                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScumMiniMap");



                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);



                string logFile = Path.Combine(appData, "automatic.log");



                string msg = string.Format("{0} [{1}] {2}: {3}{4}{5}{4}",



                    DateTime.UtcNow.ToString("o"),



                    source,



                    ex != null ? ex.GetType().FullName : "Unknown",



                    ex != null ? ex.Message : "No message",



                    Environment.NewLine,



                    ex != null ? ex.StackTrace : "");



                File.AppendAllText(logFile, msg);



            } catch {}



        }



        [STAThread]



        public static void Main(string[] args) {
            if(args!=null && args.Length==2 && args[0]=="-ApplyVerifiedUpdate") { UpdateInstaller.Run(args[1]); return; }



            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);



            Application.ThreadException += (s, e) => LogException("ThreadException", e.Exception);



            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException("UnhandledException", e.ExceptionObject as Exception);



            TaskScheduler.UnobservedTaskException += (s, e) => {



                LogException("UnobservedTaskException", e.Exception);



                e.SetObserved();



            };



            Application.EnableVisualStyles();



            string baseFolder=AppDomain.CurrentDomain.BaseDirectory;



            string appData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScumMiniMap");



            try {



                if(!Directory.Exists(appData)) Directory.CreateDirectory(appData);



                // Migrate existing config files from executable folder if present



                string[] toMigrate=new[]{"settings.ini","automatic.log"};



                foreach(string f in toMigrate) {



                    string src=Path.Combine(baseFolder,f);



                    string dst=Path.Combine(appData,f);



                    if(File.Exists(src) && !File.Exists(dst)) {



                        try { File.Copy(src,dst); } catch {}



                    }



                }



            } catch {}



            if(args!=null && args.Length>0) {



                bool check=false,preview=false,checkAuto=false;



                string screenshot=null,localUpdateSource=null;



                for(int i=0;i<args.Length;i++) {



                    string a=args[i];



                    if(a.Equals("-Check",StringComparison.OrdinalIgnoreCase)||a.Equals("/Check",StringComparison.OrdinalIgnoreCase)||a.Equals("--check",StringComparison.OrdinalIgnoreCase)) check=true;



                    else if(a.Equals("-Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("/Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("--preview",StringComparison.OrdinalIgnoreCase)) preview=true;



                    else if(a.Equals("-CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("/CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("--check-automatic",StringComparison.OrdinalIgnoreCase)) checkAuto=true;



                    else if((a.Equals("-ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("/ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("--import-screenshot",StringComparison.OrdinalIgnoreCase)) && i+1<args.Length) { screenshot=args[++i]; }



                    else if((a.Equals("-TestLocalUpdate",StringComparison.OrdinalIgnoreCase)||a.Equals("/TestLocalUpdate",StringComparison.OrdinalIgnoreCase)||a.Equals("--test-local-update",StringComparison.OrdinalIgnoreCase)) && i+1<args.Length) { localUpdateSource=args[++i]; }



                }



                LocalUpdateSource=localUpdateSource;



                if(check) {



                    MapWindow.SelfTest();



                    Console.WriteLine("Compilation and coordinate checks passed.");



                    return;



                }



                if(checkAuto) {



                    using(MapWindow miniForm=new MapWindow(appData,true)) {



                        miniForm.CheckAutomaticImport(screenshot,Path.Combine(baseFolder,"automatic-editor-preview.png"));



                    }



                    Console.WriteLine("Automatic import and naming checks passed.");



                    return;



                }



                if(preview) {



                    using(MapWindow miniForm=new MapWindow(appData,true)) {



                        miniForm.Accept("{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");



                        miniForm.Show();



                        Application.DoEvents();



                        miniForm.SaveOverlayPreview(Path.Combine(baseFolder,"preview.png"));



                        miniForm.SaveSettingsPreview(Path.Combine(baseFolder,"settings-preview.png"));



                        miniForm.CheckSearchPreview(Path.Combine(baseFolder,"search-preview.png"));



                        miniForm.CheckAppearance();



                        miniForm.CheckStaticTerrain();



                        miniForm.CheckZoneEditor(Path.Combine(baseFolder,"zones-preview.png"));



                    }



                    return;



                }



            }



            bool createdNew;



            using(System.Threading.Mutex mutex=new System.Threading.Mutex(true,"Local\\ScumMiniMapOverlay",out createdNew)) {



                if(!createdNew) return;



                try {



                    using(MapWindow window=new MapWindow(appData)) {
                        string updateStatus=Path.Combine(appData,"update-result.txt");
                        window.Shown+=(s,e)=>UpdateInstaller.CleanupHelper(updateStatus);
                        if(File.Exists(updateStatus)) {
                            string result=File.ReadAllText(updateStatus); File.Delete(updateStatus);
                            window.Shown+=(s,e)=>MessageBox.Show(result,Localization.Get("UpdateCheckTitle"));
                        }



                        if(!string.IsNullOrEmpty(LocalUpdateSource)) window.Shown+=(s,e)=>window.RunLocalUpdateTest(LocalUpdateSource);



                        Application.Run(window);



                    }



                } catch(Exception ex) {



                    LogException("ApplicationRun", ex);



                }



            }



        }



    }



}



