using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
[assembly: AssemblyVersion("1.3.2.0")]
[assembly: AssemblyFileVersion("1.3.2.0")]
[assembly: AssemblyInformationalVersion("1.3.2")]

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
        static readonly int[] busyKeys={0x10,0x11,0x12,0x43,0x5B,0x5C,0x09,0x20,0x45,0x46,0x52};
        internal static bool GameFocused() {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            long now=Stopwatch.GetTimestamp();
            if(pid==cachedPid && now<cacheUntil)return cachedGame;
            cachedPid=pid; cacheUntil=now+(Stopwatch.Frequency/4); // 250ms cache for instant focus detection
            try { using (Process p=Process.GetProcessById((int)pid)) {
                cachedGame=p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);
            }} catch { cachedGame=false; }
            return cachedGame;
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
        internal static bool IsGameWindow(IntPtr window) {
            if(window==IntPtr.Zero || !IsWindow(window) || IsIconic(window)) return false;
            uint pid; GetWindowThreadProcessId(window,out pid);
            try { using(Process p=Process.GetProcessById((int)pid)) {
                return p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);
            } } catch { return false; }
        }
        internal static bool CopyInProgress;
        internal static bool KeysBusy() {
            if(UserTypingOrActive()) return true;
            if(IsCursorVisible()) return true;
            foreach(int k in busyKeys) if ((GetAsyncKeyState(k)&0x8000)!=0) return true;
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
        internal static async Task<CopyResult> Copy(Func<bool> allowed) {
            CopyError="";
            if(CopyInProgress || !allowed() || !GameFocused() || KeysBusy()) { CopyError="Focus, chat, or modifier keys detected."; return CopyResult.Cancelled; }
            IntPtr game=GetForegroundWindow();
            CopyInProgress=true;
            try {
                bool sent=await CopyChord(Key,Task.Delay,()=>allowed() && GetForegroundWindow()==game && GameFocused()
                    && !UserTypingOrActive() && !IsCursorVisible() && (GetAsyncKeyState(0xA2)&0x8000)!=0);
                return ClassifyCopy(sent,CopyError);
            } finally { CopyInProgress=false; }
        }
        internal static CopyResult ClassifyCopy(bool sent,string error) {
            return sent?CopyResult.Sent:(string.IsNullOrEmpty(error)?CopyResult.Cancelled:CopyResult.Failed);
        }
        // Leave Control down across game frames on both sides of C. Never send C
        // after a focus/user-input change, and retain failed key-ups for cleanup.
        internal static async Task<bool> CopyChord(Func<uint,bool,bool> key,Func<int,Task> delay,Func<bool> canPressC) {
            bool ctrl=false,c=false,ok=false;
            try {
                ctrl=key(0xA2,false);
                if(ctrl) {
                    await delay(60);
                    if(canPressC()) {
                        c=key(0x43,false);
                        if(c) {
                            await delay(30);
                            ok=key(0x43,true);
                            c=!ok;
                        }
                    }
                    await delay(80);
                }
            } finally {
                if(c) { bool released=key(0x43,true); c=!released; ok=false; }
                // A second cleanup attempt precedes releasing the modifier.
                if(c) { key(0x43,true); ok=false; }
                if(ctrl && !key(0xA2,true)) { key(0xA2,true); ok=false; }
            }
            return ok;
        }

    }
    sealed class MapCanvas:Panel { public MapCanvas() { DoubleBuffered=true; ResizeRedraw=true; } }
    public sealed class MapWindow:Form {
        readonly MapCanvas canvas=new MapCanvas();
        bool TrackingEnabled { get { return !diagnosticMode && !closing; } }
        readonly Label status=new Label();
        readonly Timer timer=new Timer();
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
        bool showZones=true;
        bool showGasStations=true;
        bool showCities=true;
        bool showTowns=true;
        bool showFarms=true;
        bool showTraders=true;
        bool showFactions=true;
        bool showMilitary=true;
        bool showCustomWaypoints=true;
        bool showZoneLabels=true;
        bool smartLabelLod=true;
        bool fullMapActive;
        DateTime lastMAction = DateTime.MinValue;
        bool lastMDown;
                Position pin;
        MapZone searchTarget;
        RoadRoute activeRoute;
        PointF lastRoutePlayerPt = PointF.Empty;
        MapZone lastRouteTarget;
        bool routeCalculating;
        List<MapZone> zones=new List<MapZone>();
        readonly string dataFolder;
        readonly string zonesPath;
        readonly string settingsPath;
        bool isFirstLaunch=false;
        DateTime saveAfter=DateTime.MaxValue;
        NumericUpDown widthOption,heightOption;
        string lastFrameKey;
        internal int PresentedFrames;
        bool canvasStale;
        readonly OverlayWindow overlay;
        readonly GameKeys keys;
        readonly ChatState chat=new ChatState();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip trayMenu=new ContextMenuStrip();
        readonly SettingsPanel bar;
        bool tickBusy,closing;
        DateTime resumeAfter=DateTime.MinValue;
        Position position;
        PointF? previousMapPoint;
        DateTime previousTime=DateTime.MinValue;
        float targetZoom=1.0f;
        MapZone lastVisitedZone;
        DateTime lastVisitedTime=DateTime.MinValue;
        DateTime updated=DateTime.MinValue,next=DateTime.MinValue,sent;
        uint sequence;
        bool pending;
        IDataObject savedDataObject;
        float zoom=1.0f;
        int maxZoom=32;
        int autoZoomMin=6;
        int autoZoomMax=16;
        int zoomStepPercent=10;
        int failures;
        int attempts,responses;
        bool wasGameFocused;
        bool hiddenByFocusLoss;
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
            try { zones=ZoneStore.Load(zonesPath); } catch(IOException) { note=Localization.Get("NoteZonesLoadFail"); } catch(UnauthorizedAccessException) { note=Localization.Get("NoteZonesLoadFail"); }
            Text=Localization.T("HeaderSettings",VersionString); TopMost=true; ClientSize=new Size(430,560); MinimumSize=new Size(430,460);
            BackColor=Color.FromArgb(22,27,32); ForeColor=Color.WhiteSmoke;
            Font=new Font("Segoe UI",9); StartPosition=FormStartPosition.Manual;
            Location=new Point(Screen.PrimaryScreen.WorkingArea.Right-450,80);
            string mapFile=Path.Combine(folder,"map.png");
            if(File.Exists(mapFile)) {
                using(Image source=Image.FromFile(mapFile)) map=new Bitmap(source);
            } else {
                using(Stream s=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("map.png")) {
                    if(s==null) throw new FileNotFoundException("map.png could not be found as a file or embedded resource.");
                    using(Image source=Image.FromStream(s)) map=new Bitmap(source);
                }
            }
            Image level=map;
            while(level.Width>512 && level.Height>512) {
                Bitmap reduced=new Bitmap(level.Width/2,level.Height/2,PixelFormat.Format32bppPArgb);
                using(Graphics graphics=Graphics.FromImage(reduced)) {
                    graphics.CompositingMode=CompositingMode.SourceCopy;
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(level,0,0,reduced.Width,reduced.Height);
                }
                mapLevels.Add(reduced); level=reduced;
            }
            using(Graphics g=Graphics.FromImage(fullMap))g.DrawImage(map,0,0,360,360);
            overlay=new OverlayWindow(ShowSettings,ExitApp,factor=> { autoZoom=false; zoom=Math.Max(1,Math.Min(maxZoom,zoom*factor)); targetZoom=zoom; SettingsChanged(); },ShowZoneSearch);
            keys=new GameKeys(OnGameKey);
            tray=new NotifyIcon { Icon=SystemIcons.Application,Text="SkynettMiniMap v"+VersionString,Visible=true };
            tray.ContextMenuStrip=trayMenu;
            BuildTrayMenu();
            tray.DoubleClick+=(s,e)=>ShowSettings();
            bar=new SettingsPanel { Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true };
            BuildSettingsPanel();
            status.Dock=DockStyle.Bottom; status.Height=95; status.Padding=new Padding(8);
            canvas.Dock=DockStyle.Fill; canvas.BackColor=Color.FromArgb(12,17,22); canvas.Paint+=PaintMap;
            Controls.Add(bar); Controls.Add(status);
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
            timer.Interval=40; timer.Tick+=Tick; timer.Start();
            System.Threading.ThreadPool.QueueUserWorkItem(_ => { RoadRouter.Instance.InitializeFromResource(); });
            Shown+=(s,e)=> {
                Hide();
                bool focused=Native.GameFocused();
                wasGameFocused=focused;
                overlay.SetGameFocus(focused);
                if(!diagnosticMode) { failures=0; next=DateTime.UtcNow; }
                overlay.Show();
                RenderOverlay();

                if(isFirstLaunch && !diagnosticMode) {
                    string welcomeMsg = Localization.T("WelcomeMsg", VersionString);
                    MessageBox.Show(welcomeMsg, Localization.T("WelcomeTitle", VersionString), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                if(!diagnosticMode) {
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
            trayMenu.Items.Clear();
            trayMenu.Items.Add(Localization.Get("Settings"),null,(s,e)=>ShowSettings());
            trayMenu.Items.Add(Localization.Get("SearchPlaceOrGrid"),null,(s,e)=>ShowZoneSearch());
            trayMenu.Items.Add(Localization.Get("TrayCheckUpdates"),null,(s,e)=>CheckForUpdates(true));
            trayMenu.Items.Add(Localization.Get("JoinDiscord"),null,(s,e)=>{ try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName="https://discord.gg/MYzcGaFDMn", UseShellExecute=true }); } catch {} });
            trayMenu.Items.Add(Localization.Get("TrayShowHide"),null,(s,e)=>ToggleOverlay());
            trayMenu.Items.Add(Localization.Get("TrayExit"),null,(s,e)=>ExitApp());
            OverlayTheme.Menu(trayMenu);
        }

        void BuildSettingsPanel() {
            bar.SuspendLayout();
            bar.Controls.Clear();

            // Section: Language
            OverlayTheme.Section(bar, Localization.Get("SecLanguage"));
            FlowLayoutPanel langRow = new FlowLayoutPanel { Width = 365, Height = 32 };
            langRow.Controls.Add(new Label { Text = Localization.Get("Language"), Width = 160, Padding = new Padding(0, 5, 0, 0) });
            ComboBox langCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            langCombo.Items.AddRange(new object[] { "English", "EspaÃ±ol (Argentina)" });
            langCombo.SelectedIndex = Localization.Current == AppLanguage.SpanishArgentina ? 1 : 0;
            langCombo.SelectedIndexChanged += (s, e) => {
                AppLanguage newLang = langCombo.SelectedIndex == 1 ? AppLanguage.SpanishArgentina : AppLanguage.English;
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

            // Section: Navigation
            OverlayTheme.Section(bar, Localization.Get("SecNavigation"));
            bar.Controls.Add(new Label { Text = Localization.Get("AutoTrackingActive"), AutoSize = true, Margin = new Padding(3, 5, 3, 8) });
            Button searchButton = new Button { Text = Localization.Get("SearchPlaceOrGrid"), Width = 280 };
            searchButton.Click += (s, e) => ShowZoneSearch();
            bar.Controls.Add(searchButton);

            // Section: Map and zones
            OverlayTheme.Section(bar, Localization.Get("SecMapZones"));
            AddCheck(bar, Localization.Get("GridLabels"), gridLabels, value => gridLabels = value);
            AddCheck(bar, Localization.Get("GridBorders"), gridBorders, value => gridBorders = value);
            FlowLayoutPanel gridRow = new FlowLayoutPanel { Width = 365, Height = 45 };
            Label gridValue = new Label { Text = Localization.T("GridOpacity", gridOpacity), Width = 175, Padding = new Padding(0, 7, 0, 0) };
            TrackBar gridSlider = new TrackBar { Minimum = 0, Maximum = 100, Value = gridOpacity, TickFrequency = 10, Width = 180, Height = 40 };
            gridSlider.ValueChanged += (s, e) => {
                gridOpacity = gridSlider.Value;
                gridValue.Text = Localization.T("GridOpacity", gridOpacity);
                SettingsChanged();
            };
            gridRow.Controls.Add(gridValue);
            gridRow.Controls.Add(gridSlider);
            bar.Controls.Add(gridRow);
            AddCheck(bar, Localization.Get("ShowSavedZones"), showZones, value => { showZones = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowZoneLabels"), showZoneLabels, value => { showZoneLabels = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("SmartLabelLod"), smartLabelLod, value => { smartLabelLod = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowGasStations"), showGasStations, value => { showGasStations = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowCities"), showCities, value => { showCities = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowTowns"), showTowns, value => { showTowns = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowFarms"), showFarms, value => { showFarms = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowTraders"), showTraders, value => { showTraders = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowFactions"), showFactions, value => { showFactions = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowMilitary"), showMilitary, value => { showMilitary = value; SettingsChanged(); });
            AddCheck(bar, Localization.Get("ShowCustomWaypoints"), showCustomWaypoints, value => { showCustomWaypoints = value; SettingsChanged(); });
            AddNumber(bar, Localization.Get("ZoneLabelSize"), 6, 24, labelSize, value => labelSize = value);
            Button zoneButton = new Button { Text = Localization.Get("MapZonesScreenshot"), Width = 280 };
            zoneButton.Click += (s, e) => OpenZoneImport(null);
            bar.Controls.Add(zoneButton);

            // Section: Appearance
            OverlayTheme.Section(bar, Localization.Get("SecAppearance"));
            AddCheck(bar, Localization.Get("FadeEdges"), edgeFade, value => edgeFade = value);
            AddCheck(bar, Localization.Get("ShowHeading"), showHeading, value => showHeading = value);
            AddCheck(bar, Localization.Get("ShowCompass"), showCompass, value => showCompass = value);
            AddCheck(bar, Localization.Get("ShowElevation"), showElevation, value => showElevation = value);
            AddCheck(bar, Localization.Get("ZoneChime"), zoneChime, value => zoneChime = value);

            // Section: Tracking and zoom
            OverlayTheme.Section(bar, Localization.Get("SecTrackingZoom"));
            AddCheck(bar, Localization.Get("AutoZoomSpeed"), autoZoom, value => { autoZoom = value; SettingsChanged(); });
            AddNumber(bar, Localization.Get("AutoZoomMax"), 1, 32, autoZoomMax, value => { autoZoomMax = value; autoZoomMin = Math.Min(autoZoomMin, value); SettingsChanged(); });
            AddNumber(bar, Localization.Get("AutoZoomMin"), 1, 32, autoZoomMin, value => { autoZoomMin = value; autoZoomMax = Math.Max(autoZoomMax, value); SettingsChanged(); });
            AddNumber(bar, Localization.Get("PositionInterval"), 1000, 10000, copyIntervalMs, value => { copyIntervalMs = value; next = DateTime.UtcNow; SettingsChanged(); }).Increment = 250;

            // Section: Layout
            OverlayTheme.Section(bar, Localization.Get("SecLayout"));
            AddCheck(bar, Localization.Get("ShowStatus"), showStatus, value => showStatus = value);
            FlowLayoutPanel posRow = new FlowLayoutPanel { Width = 365, Height = 32 };
            posRow.Controls.Add(new Label { Text = Localization.Get("LocationBarPosition"), Width = 160, Padding = new Padding(0, 5, 0, 0) });
            ComboBox posCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            posCombo.Items.AddRange(new object[] { Localization.Get("PosBelow"), Localization.Get("PosAbove") });
            posCombo.SelectedIndex = statusPos == "Above" ? 1 : 0;
            posCombo.SelectedIndexChanged += (s, e) => {
                statusPos = posCombo.SelectedIndex == 1 ? "Above" : "Below";
                SettingsChanged();
            };
            posRow.Controls.Add(posCombo);
            bar.Controls.Add(posRow);
            widthOption = AddNumber(bar, Localization.Get("MapWidth"), 240, 800, overlay.Width, value => overlay.Width = value);
            heightOption = AddNumber(bar, Localization.Get("MapHeight"), 240, 800, overlay.Height, value => overlay.Height = value);
            AddNumber(bar, Localization.Get("MapOpacity"), 30, 100, mapOpacity, value => mapOpacity = value);
            AddNumber(bar, Localization.Get("MaxZoom"), 4, 32, maxZoom, value => { maxZoom = value; zoom = Math.Min(maxZoom, zoom); targetZoom = Math.Min(maxZoom, targetZoom); SettingsChanged(); });
            AddNumber(bar, Localization.Get("ZoomStep"), 10, 100, zoomStepPercent, value => { zoomStepPercent = value; SettingsChanged(); });
            FlowLayoutPanel zoomBar = new FlowLayoutPanel { Width = 365, Height = 36 };
            Button minus = new Button { Text = Localization.Get("ZoomOut"), Width = 110 };
            minus.Click += (s, e) => { autoZoom = false; float step = 1f + (zoomStepPercent / 100f); zoom = Math.Max(1, zoom / step); targetZoom = zoom; SettingsChanged(); };
            zoomBar.Controls.Add(minus);
            Button plus = new Button { Text = Localization.Get("ZoomIn"), Width = 110 };
            plus.Click += (s, e) => { autoZoom = false; float step = 1f + (zoomStepPercent / 100f); zoom = Math.Min(maxZoom, zoom * step); targetZoom = zoom; SettingsChanged(); };
            zoomBar.Controls.Add(plus);
            Button full = new Button { Text = Localization.Get("FullMap"), Width = 100 };
            full.Click += (s, e) => { autoZoom = false; zoom = 1; targetZoom = 1; SettingsChanged(); };
            zoomBar.Controls.Add(full);
            bar.Controls.Add(zoomBar);
            Button centre = new Button { Text = Localization.Get("CentreMap"), Width = 280 };
            centre.Click += (s, e) => {
                if (position != null) {
                    fixedMapCentre = ToMap(position);
                    fixedCentreReady = true;
                    motion.Sample(ToMap(position), position.Yaw, motionClock.Elapsed.TotalSeconds, true);
                    SettingsChanged();
                    RenderOverlay();
                }
            };
            bar.Controls.Add(centre);

            // Section: Tools and shortcuts
            OverlayTheme.Section(bar, Localization.Get("SecToolsShortcuts"));
            Button customMapBtn = new Button { Text = Localization.Get("ImportCustomMap"), Width = 280 };
            customMapBtn.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*", Title = Localization.Get("CustomMapSelectTitle") }) {
                    if (ofd.ShowDialog(this) == DialogResult.OK) {
                        try {
                            using(Image preview = Image.FromFile(ofd.FileName)) {
                                if(preview.Width < 1024 || preview.Height < 1024) { MessageBox.Show(this, Localization.Get("CustomMapTooSmall"), Localization.Get("CustomMapConfirmTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                                if(MessageBox.Show(this, Localization.T("CustomMapDimensions", preview.Width, preview.Height), Localization.Get("CustomMapConfirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                            }
                            string targetPath = Path.Combine(dataFolder, "map.png");
                            File.Copy(ofd.FileName, targetPath, true);
                            MessageBox.Show(this, Localization.Get("CustomMapSuccessMsg"), Localization.Get("CustomMapSuccessTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        } catch (Exception ex) {
                            MessageBox.Show(this, Localization.T("CustomMapErrorMsg", ex.Message));
                        }
                    }
                }
            };
            bar.Controls.Add(customMapBtn);
            Button resetMapBtn = new Button { Text = Localization.Get("ResetDefaultMap"), Width = 280 };
            resetMapBtn.Click += (s, e) => {
                string targetPath = Path.Combine(dataFolder, "map.png");
                if(!File.Exists(targetPath)) { MessageBox.Show(this, Localization.Get("ResetDefaultMapDone"), Localization.Get("CustomMapConfirmTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                if(MessageBox.Show(this, Localization.Get("ResetDefaultMapConfirm"), Localization.Get("CustomMapConfirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                    try { File.Delete(targetPath); MessageBox.Show(this, Localization.Get("ResetDefaultMapDone"), Localization.Get("CustomMapConfirmTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch(Exception ex) { MessageBox.Show(this, ex.Message); }
                }
            };
            bar.Controls.Add(resetMapBtn);
            Button updateBtn = new Button { Text = Localization.Get("CheckUpdatesGitHub"), Width = 280 };
            updateBtn.Click += (s, e) => CheckForUpdates(true);
            bar.Controls.Add(updateBtn);
            Button discordBtn = new Button { Text = Localization.Get("JoinDiscord"), Width = 280 };
            discordBtn.Click += (s, e) => {
                try {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://discord.gg/MYzcGaFDMn", UseShellExecute = true });
                } catch (Exception ex) {
                    MessageBox.Show(this, Localization.T("DiscordOpenError", ex.Message));
                }
            };
            bar.Controls.Add(discordBtn);
            Label hint = new Label { Text = Localization.Get("HotkeysHint"), AutoSize = true, Margin = new Padding(3, 10, 3, 10) };
            bar.Controls.Add(hint);
            FlowLayoutPanel actionButtons = new FlowLayoutPanel { Width = 365, Height = 40, Margin = new Padding(0, 0, 0, 15) };
            Button openFolder = new Button { Text = Localization.Get("OpenDataFolder"), Width = 140, Height = 28 };
            openFolder.Click += (s, e) => {
                try {
                    if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + dataFolder + "\"", UseShellExecute = true });
                } catch (Exception ex) {
                    MessageBox.Show(this, Localization.T("DataFolderOpenError", ex.Message));
                }
            };
            Button done = new Button { Text = Localization.Get("Done"), Width = 100, Height = 28 };
            done.Click += (s, e) => DismissSettings();
            actionButtons.Controls.Add(openFolder);
            actionButtons.Controls.Add(done);
            bar.Controls.Add(actionButtons);

            OverlayTheme.Sections(bar);
            bar.ResumeLayout(true);
        }
        public const string VersionString = "1.3.2";
        public static readonly Version CurrentVersion = new Version(1, 3, 2, 0);
        bool updateCheckRunning;
        async void CheckForUpdates(bool userInitiated) {
            if(updateCheckRunning || closing || IsDisposed || diagnosticMode) return;
            updateCheckRunning = true;
            try {
                var service = new UpdateService(UpdateService.Repository,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScumMiniMap", "updates"), null);
                UpdateRelease release = await Task.Run(() => service.Check(userInitiated));
                if(closing || IsDisposed) return;
                if(release.Version > CurrentVersion) {
                    note = Localization.T("UpdateAvailableNote", release.VersionText);
                    if(!userInitiated) {
                        tray.ShowBalloonTip(10000, Localization.Get("UpdateAvailableDialogTitle"), note, ToolTipIcon.Info);
                        return;
                    }
                    if(MessageBox.Show(this, Localization.T("UpdateAvailableDialogMsg", VersionString, release.VersionText, release.Filename),
                        Localization.Get("UpdateAvailableDialogTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    using(var save = new SaveFileDialog { Title=Localization.Get("UpdateSaveTitle"), FileName=release.Filename, Filter="Windows executable (*.exe)|*.exe", OverwritePrompt=true }) {
                        if(save.ShowDialog(this) != DialogResult.OK) return;
                        if(File.Exists(save.FileName)) {
                            MessageBox.Show(this, Localization.Get("UpdateChooseNewFile"), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        note=Localization.Get("UpdateDownloading");
                        string destination=save.FileName;
                        await Task.Run(() => service.Download(release,destination));
                        if(closing || IsDisposed) return;
                        note=Localization.Get("UpdateDownloadComplete");
                        MessageBox.Show(this, Localization.T("UpdateDownloadedMsg",destination), Localization.Get("UpdateCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
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
            if(resourcesReleased)return; resourcesReleased=true;
            timer.Stop(); timer.Dispose();
            if(keys!=null)keys.Dispose();
            if(tray!=null) { tray.Visible=false; tray.Dispose(); }
            if(overlay!=null)overlay.Dispose();
            overlayFrame.Dispose(); fullMap.Dispose();
            foreach(Bitmap cachedLevel in mapLevels)cachedLevel.Dispose();
            if(map!=null)map.Dispose();
        }
        protected override void Dispose(bool disposing) {
            if(disposing)ReleaseResources();
            base.Dispose(disposing);
        }
        void ExitApp() { closing=true; SaveSettings(); Close(); }
        public void OpenZoneImport(string file) {
            bool visible=overlay.Visible;overlay.Hide();
            try {
                using(ZoneEditor editor=new ZoneEditor(map,zones,zonesPath,value=> { zones=value;SettingsChanged(); })) {
                    if(!string.IsNullOrEmpty(file))editor.Shown+=async(s,e)=>await editor.ImportAutomatic(file);
                    OverlayTheme.Frame(editor,Localization.Get("HeaderMapZones"),()=>editor.Close());
                    editor.ShowDialog(this);
                }
            } finally { if(visible) { overlay.Show();RenderOverlay(); } }
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
        void AddCheck(FlowLayoutPanel panel,string text,bool value,Action<bool> changed) {
            CheckBox box=new CheckBox { Text=text,Checked=value,AutoSize=true,Margin=new Padding(3,5,3,5) };
            box.CheckedChanged+=(s,e)=> { changed(box.Checked); SettingsChanged(); }; panel.Controls.Add(box);
        }
        NumericUpDown AddNumber(FlowLayoutPanel panel,string text,int min,int max,int value,Action<int> changed) {
            FlowLayoutPanel row=new FlowLayoutPanel { Width=365,Height=31 };
            row.Controls.Add(new Label { Text=text,Width=220,Padding=new Padding(0,5,0,0) });
            NumericUpDown number=new NumericUpDown { Minimum=min,Maximum=max,Value=value,Width=100 };
            number.ValueChanged+=(s,e)=> { changed((int)number.Value); SettingsChanged(); }; row.Controls.Add(number); panel.Controls.Add(row); return number;
        }
        void SettingsChanged() { terrainKey=null; lastFrameKey=null; saveAfter=DateTime.UtcNow.AddMilliseconds(700); }
        HashSet<ZoneCategory> BuildHiddenCategories() {
            var hidden = new HashSet<ZoneCategory>();
            if(!showCities) hidden.Add(ZoneCategory.City);
            if(!showTowns) hidden.Add(ZoneCategory.Town);
            if(!showFarms) hidden.Add(ZoneCategory.Farm);
            if(!showTraders) hidden.Add(ZoneCategory.Trader);
            if(!showFactions) hidden.Add(ZoneCategory.Faction);
            if(!showMilitary) hidden.Add(ZoneCategory.Military);
            if(!showCustomWaypoints) hidden.Add(ZoneCategory.Custom);
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
                int width=400,height=240,left=overlay.Left,top=overlay.Top;
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
                            switch(key) { case "GridLabels":gridLabels=b;break;case "GridBorders":gridBorders=b;break;case "EdgeFade":edgeFade=b;break;case "ShowStatus":showStatus=b;break;case "ShowZones":showZones=b;break;case "ShowGasStations":showGasStations=b;break;case "AutoZoom":autoZoom=b;break;case "ShowHeading":showHeading=b;break;case "ShowCompass":showCompass=b;break;case "ShowElevation":showElevation=b;break;case "ZoneChime":zoneChime=b;break;case "ShowCities":showCities=b;break;case "ShowTowns":showTowns=b;break;case "ShowFarms":showFarms=b;break;case "ShowTraders":showTraders=b;break;case "ShowFactions":showFactions=b;break;case "ShowMilitary":showMilitary=b;break;case "ShowCustomWaypoints":showCustomWaypoints=b;break;case "ShowZoneLabels":showZoneLabels=b;break;case "SmartLabelLod":smartLabelLod=b;break; }
                        }
                        if(key=="StatusPos") { statusPos=val=="Above"?"Above":"Below"; }
                        if(key=="Shape") { overlayShape=val=="Circle"?"Circle":"Square"; }
                        if(int.TryParse(val,out n)) {
                            switch(key) { case "Width":width=Math.Max(240,Math.Min(800,n));break;case "Height":height=Math.Max(240,Math.Min(800,n));break;case "Left":left=n;break;case "Top":top=n;break;case "Opacity":mapOpacity=Math.Max(30,Math.Min(100,n));break;case "GridOpacity":gridOpacity=Math.Max(0,Math.Min(100,n));break;case "LabelSize":labelSize=Math.Max(6,Math.Min(24,n));break;case "MaxZoom":maxZoom=Math.Max(4,Math.Min(32,n));break;case "AutoZoomMin":autoZoomMin=Math.Max(1,Math.Min(32,n));break;case "AutoZoomMax":autoZoomMax=Math.Max(1,Math.Min(32,n));break;case "ZoomStep":zoomStepPercent=Math.Max(10,Math.Min(100,n));break;case "CopyInterval":copyIntervalMs=ReadCopyInterval(n,true);break;case "CopyIntervalMs":copyIntervalMs=ReadCopyInterval(n,false);break; }
                        }
                        if(key=="Zoom" && float.TryParse(val,NumberStyles.Float,CultureInfo.InvariantCulture,out z) && !float.IsNaN(z) && !float.IsInfinity(z)) { zoom=Math.Max(1,Math.Min(maxZoom,z)); targetZoom=zoom; }
                    } catch { /* skip malformed line */ }
                }
                if(!foundWelcomed) isFirstLaunch=true;
                if(!foundLanguage) Localization.DetectSystemLanguage();
                overlay.Size=new Size(width,height);
                try {
                    Rectangle area=Screen.FromPoint(new Point(left,top)).WorkingArea;
                    overlay.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-width,left)),Math.Max(area.Top,Math.Min(area.Bottom-height,top)));
                } catch { /* default position if screen detection fails */ }
            } catch { /* catch-all: proceed with defaults on any unexpected error */ }
        }
        void SaveSettings() {
            if(diagnosticMode)return;
            saveAfter=DateTime.MaxValue;
            try { File.WriteAllLines(settingsPath+".tmp",new string[]{"Welcomed=True","Language="+Localization.CurrentCode,"GridLabels="+gridLabels,"GridBorders="+gridBorders,"GridOpacity="+gridOpacity,"ShowZones="+showZones,"ShowGasStations="+showGasStations,"LabelSize="+labelSize,"EdgeFade="+edgeFade,"Shape="+overlayShape,"ShowHeading="+showHeading,"ShowCompass="+showCompass,"ShowElevation="+showElevation,"ZoneChime="+zoneChime,"CopyIntervalMs="+copyIntervalMs,"AutoZoom="+autoZoom,"AutoZoomMin="+autoZoomMin,"AutoZoomMax="+autoZoomMax,"ShowStatus="+showStatus,"StatusPos="+statusPos,"Opacity="+mapOpacity,"Width="+overlay.Width,"Height="+overlay.Height,"Left="+overlay.Left,"Top="+overlay.Top,"Zoom="+zoom.ToString(CultureInfo.InvariantCulture),"MaxZoom="+maxZoom,"ZoomStep="+zoomStepPercent,"ShowCities="+showCities,"ShowTowns="+showTowns,"ShowFarms="+showFarms,"ShowTraders="+showTraders,"ShowFactions="+showFactions,"ShowMilitary="+showMilitary,"ShowCustomWaypoints="+showCustomWaypoints,"ShowZoneLabels="+showZoneLabels,"SmartLabelLod="+smartLabelLod});
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
            if(Visible) {
                Height=Math.Min(560,Screen.FromControl(overlay).WorkingArea.Height);
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
                Height=Math.Min(560,Screen.FromControl(overlay).WorkingArea.Height);
                OverlayTheme.Anchor(this,overlay);
                Show();
                BringToFront();
                Activate();
                Native.ForceForeground(Handle);
            } finally { panelOpening=false; }
        }
        void ToggleOverlay() { if(overlay.Visible)overlay.Hide(); else { overlay.SetGameFocus(Native.GameFocused()); overlay.Show(); RenderOverlay(); } }
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
                using(Form dlg=new Form()) {
                    dlg.Text=Localization.Get("SearchTitle"); dlg.FormBorderStyle=FormBorderStyle.FixedToolWindow;
                    dlg.StartPosition=FormStartPosition.Manual; dlg.TopMost=true; dlg.ShowInTaskbar=true;
                    dlg.ClientSize=new Size(430,390);
                    dlg.BackColor=Color.FromArgb(22,27,32); dlg.ForeColor=Color.WhiteSmoke;
                    dlg.Font=new Font("Segoe UI",9);
                    Rectangle area=Screen.FromControl(overlay).WorkingArea;
                    dlg.Location=new Point(Math.Max(area.Left,Math.Min(overlay.Left,area.Right-dlg.Width)),
                        Math.Max(area.Top,Math.Min(overlay.Top+20,area.Bottom-dlg.Height)));
                    Label hint=new Label { Dock=DockStyle.Top,Height=32,Text=Localization.Get("SearchHint") };
                    TextBox search=new TextBox { MaxLength=120,Dock=DockStyle.Top,BackColor=Color.FromArgb(34,40,48),ForeColor=Color.WhiteSmoke };
                    ListBox list=new ListBox { Dock=DockStyle.Fill,BackColor=Color.FromArgb(28,33,40),ForeColor=Color.WhiteSmoke,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48 };
                    Label status=new Label { Dock=DockStyle.Bottom,Height=30 };
                    FlowLayoutPanel buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=38 };
                    Button go=new Button { Text=Localization.Get("SetWaypoint"),Width=125 };
                    Button clear=new Button { Text=Localization.Get("ClearWaypoint"),Width=135,Enabled=searchTarget!=null };
                    Button cancel=new Button { Text=Localization.Get("Cancel"),Width=80,DialogResult=DialogResult.Cancel };
                    buttons.Controls.AddRange(new Control[]{go,clear,cancel});
                    List<MapZone> matches=new List<MapZone>();
                    MapZone selected=null;
                    list.DrawItem+=(s,e)=> {
                        if(e.Index<0 || e.Index>=matches.Count)return;
                        MapZone result=matches[e.Index];
                        bool active=(e.State&DrawItemState.Selected)!=0;
                        using(Brush fill=new SolidBrush(active?Color.FromArgb(35,66,80):OverlayTheme.Surface)) e.Graphics.FillRectangle(fill,e.Bounds);
                        Rectangle title=new Rectangle(e.Bounds.Left+12,e.Bounds.Top+5,e.Bounds.Width-24,20);
                        Rectangle detail=new Rectangle(title.Left,e.Bounds.Top+27,title.Width,17);
                        TextRenderer.DrawText(e.Graphics,Localization.GetZoneName(result.Name),list.Font,title,active?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                        string description=DestinationSearch.Sector(result.Centroid)+(result.Name.StartsWith("Grid ",StringComparison.Ordinal)?Localization.Get("SectorCentre"):(result.IsFaction?Localization.Get("FactionPOI"):Localization.Get("Place")));
                        if(position!=null) description+=" / "+DestinationSearch.Distance(DestinationSearch.Metres(result.Centroid,ToMap(position)));
                        TextRenderer.DrawText(e.Graphics,description,list.Font,detail,Color.FromArgb(161,181,192),TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                        e.DrawFocusRectangle();
                    };
                    Action populate=()=> {
                        matches=DestinationSearch.Find(zones,search.Text,position==null?(PointF?)null:ToMap(position));
                        bool factionOnly=Regex.IsMatch(search.Text??"",@"\bfactions?\b",RegexOptions.IgnoreCase);
                        if(!factionOnly) { MapZone grid=GridTarget(search.Text); if(grid!=null) matches.Insert(0,grid); }
                        list.BeginUpdate(); list.Items.Clear();
                        foreach(MapZone zone in matches) {
                            PointF point=zone.Centroid;
                            string displayName = Localization.GetZoneName(zone.Name);
                            list.Items.Add(displayName+"  /  "+DestinationSearch.Sector(point)+(position==null?"":"  /  "+DestinationSearch.Distance(DestinationSearch.Metres(point,ToMap(position)))));
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
                                if(MessageBox.Show(dlg,Localization.T("WaypointDeleteConfirm",displayName),Localization.Get("WaypointNameTitle"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes) {
                                    zones.Remove(z);
                                    if(searchTarget==z) { searchTarget=null; activeRoute=null; lastRouteTarget=null; }
                                    try { ZoneStore.Save(zonesPath,zones); } catch {}
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
                    clear.Click+=(s,e)=> { searchTarget=null; activeRoute=null; lastRouteTarget=null; note=Localization.Get("NoteWaypointCleared"); SettingsChanged(); dlg.DialogResult=DialogResult.Cancel; };
                    search.KeyDown+=(s,e)=> {
                        if((e.KeyCode==Keys.Down || e.KeyCode==Keys.Up) && list.Items.Count>0) {
                            list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(e.KeyCode==Keys.Down?1:-1)));
                            e.Handled=true; e.SuppressKeyPress=true;
                        }
                    };
                    dlg.AcceptButton=go; dlg.CancelButton=cancel;
                    dlg.Controls.Add(list); dlg.Controls.Add(search); dlg.Controls.Add(hint); dlg.Controls.Add(status); dlg.Controls.Add(buttons);
                    OverlayTheme.Frame(dlg,Localization.Get("HeaderWaypoint"),()=>dlg.DialogResult=DialogResult.Cancel);
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
            if((DateTime.UtcNow - lastInsertAction).TotalMilliseconds < 450) return;
            lastInsertAction = DateTime.UtcNow;
            if(position==null) return;
            PointF mapPt = ToMap(position);
            MapZone existing = null;
            foreach(MapZone z in zones) {
                if(z.Category==ZoneCategory.Custom && z.Points!=null && z.Points.Length>0) {
                    double dx=z.Points[0].X-mapPt.X, dy=z.Points[0].Y-mapPt.Y;
                    if(Math.Sqrt(dx*dx+dy*dy)<0.005) { existing=z; break; }
                }
            }
            if(existing!=null) {
                string displayName=Localization.GetZoneName(existing.Name);
                if(MessageBox.Show(Localization.T("WaypointDeleteConfirm",displayName), Localization.Get("WaypointNameTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question)==DialogResult.Yes) {
                    zones.Remove(existing); pin=null;
                    try { ZoneStore.Save(zonesPath,zones); } catch {}
                    note=Localization.T("WaypointDeleted",displayName);
                    SettingsChanged();
                    RenderOverlay();
                }
                return;
            }
            using(Form prompt=new Form { Width=340,Height=160,Text=Localization.Get("WaypointNameTitle"),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,TopMost=true }) {
                Label lbl=new Label { Left=15,Top=15,Width=290,Text=Localization.Get("WaypointNamePrompt"),TabIndex=1 };
                TextBox txt=new TextBox { Left=15,Top=45,Width=290,TabIndex=0 };
                Button ok=new Button { Text=Localization.Get("Done"),Left=135,Top=80,Width=80,DialogResult=DialogResult.OK,TabIndex=2 };
                Button cn=new Button { Text=Localization.Get("Cancel"),Left=225,Top=80,Width=80,DialogResult=DialogResult.Cancel,TabIndex=3 };
                prompt.Controls.AddRange(new Control[]{txt,lbl,ok,cn});
                prompt.ActiveControl=txt;
                prompt.AcceptButton=ok; prompt.CancelButton=cn;
                txt.KeyDown += (s, e) => { if(e.KeyCode == Keys.Enter) { ok.PerformClick(); e.Handled = true; e.SuppressKeyPress = true; } };
                prompt.Shown += (s, e) => {
                    Native.ForceForeground(prompt.Handle);
                    prompt.Activate();
                    txt.Focus();
                    txt.SelectAll();
                };
                if(prompt.ShowDialog()==DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text)) {
                    string name=txt.Text.Trim();
                    if(name.Length>80) name=name.Substring(0,80);
                    MapZone wp=new MapZone { Name=name, Argb=Color.FromArgb(255,0,200,255).ToArgb(), Points=new PointF[]{mapPt}, Category=ZoneCategory.Custom };
                    zones.Add(wp); pin=new Position { X=position.X, Y=position.Y, Z=position.Z, Yaw=position.Yaw };
                    try { ZoneStore.Save(zonesPath,zones); } catch {}
                    note=Localization.T("WaypointSaved",name);
                    SettingsChanged();
                    RenderOverlay();
                }
            }
        }

        void TriggerZoomIn() {
            if((DateTime.UtcNow - lastPgUpAction).TotalMilliseconds < 120) return;
            lastPgUpAction = DateTime.UtcNow;
            autoZoom=false;
            float step=1f+(zoomStepPercent/100f);
            zoom=Math.Min(maxZoom,zoom*step);
            targetZoom=zoom;
            SettingsChanged();
        }

        void TriggerZoomOut() {
            if((DateTime.UtcNow - lastPgDnAction).TotalMilliseconds < 120) return;
            lastPgDnAction = DateTime.UtcNow;
            autoZoom=false;
            float step=1f+(zoomStepPercent/100f);
            zoom=Math.Max(1,zoom/step);
            targetZoom=zoom;
            SettingsChanged();
        }

        void OnGameKey(int key) {
            if(diagnosticMode)return;
            // End key (0x23): Show / Hide overlay
            if(key==0x23) {
                lastEndDown=true;
                BeginInvoke(new Action(TriggerToggleOverlay));
                return;
            }
            // Home key (0x24): Open / Close Settings
            if(key==0x24) {
                lastHomeDown=true;
                BeginInvoke(new Action(ToggleSettings));
                return;
            }
            // Delete key (0x2E): Zone search / navigate
            if(key==0x2E) {
                lastDeleteDown=true;
                BeginInvoke(new Action(TriggerZoneSearch));
                return;
            }
            // Page Up (0x21): Zoom in
            if(key==0x21) {
                lastPgUpDown=true;
                BeginInvoke(new Action(TriggerZoomIn));
                return;
            }
            // Page Down (0x22): Zoom out
            if(key==0x22) {
                lastPgDnDown=true;
                BeginInvoke(new Action(TriggerZoomOut));
                return;
            }
            // Insert key (0x2D): Save custom waypoint at current position
            if(key==0x2D) {
                lastInsertDown=true;
                BeginInvoke(new Action(TriggerPin));
                return;
            }
            // M key (0x4D): Toggle Full Map Mode
            if(key==0x4D && Native.GameFocused() && !chat.Paused) {
                lastMDown=true;
                BeginInvoke(new Action(TriggerFullMap));
                return;
            }
            if(key==0x1B && fullMapActive) {
                BeginInvoke(new Action(TriggerFullMap));
            }
            if(Native.GameFocused()) {
                chat.Key(key);
                if(chat.Paused) note=Localization.Get("NoteChatOpen");
                else { resumeAfter=DateTime.UtcNow.AddMilliseconds(1200); next=resumeAfter; note=Localization.Get("NoteChatClosed"); }
            }
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
                    float desired=filteredSpeed<0.4?high:high-(float)Math.Max(0,Math.Min(1,(filteredSpeed*3.6-3)/97))*(high-low);
                    if(!fixedCentreReady) { targetZoom=desired; zoom=desired; }
                    else if(filteredSpeed<0.4) { targetZoom=high; }
                    else if(Math.Abs(desired-targetZoom)>0.25f) { targetZoom=desired; }
                }
                previousTime = now;
                motion.Sample(currentPt,p.Yaw,nowSec,false);
            } else {
                motion.Sample(currentPt,p.Yaw,nowSec,true);
            }
            if(zones!=null) {
                foreach(MapZone z in zones) {
                    if(z.Contains(currentPt)) {
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
                activeRoute=null;
                lastRouteTarget=null;
                lastFrameKey=null;
            }
            fixedMapCentre=currentPt;
            if(!fixedCentreReady) { fixedCentreReady=true; terrainKey=null; }
            position=p; updated=now; if(Visible)canvas.Invalidate();
            if(searchTarget!=null) UpdateRouteAsync(currentPt,searchTarget);
            RenderOverlay();
        }
        bool lastHomeDown, lastDeleteDown, lastEndDown, lastInsertDown, lastPgUpDown, lastPgDnDown;
        void CheckHotkeysAsync() {
            if(diagnosticMode || closing || IsDisposed) return;

            bool isHome = (Native.GetAsyncKeyState(0x24) & 0x8000) != 0;
            if(isHome && !lastHomeDown) {
                ToggleSettings();
            }
            lastHomeDown = isHome;

            bool isDelete = (Native.GetAsyncKeyState(0x2E) & 0x8000) != 0;
            if(isDelete && !lastDeleteDown) {
                TriggerZoneSearch();
            }
            lastDeleteDown = isDelete;

            bool isEnd = (Native.GetAsyncKeyState(0x23) & 0x8000) != 0;
            if(isEnd && !lastEndDown) {
                TriggerToggleOverlay();
            }
            lastEndDown = isEnd;

            bool isInsert = (Native.GetAsyncKeyState(0x2D) & 0x8000) != 0;
            if(isInsert && !lastInsertDown) {
                TriggerPin();
            }
            lastInsertDown = isInsert;

            bool isPgUp = (Native.GetAsyncKeyState(0x21) & 0x8000) != 0;
            if(isPgUp && !lastPgUpDown) {
                TriggerZoomIn();
            }
            lastPgUpDown = isPgUp;

            bool isPgDn = (Native.GetAsyncKeyState(0x22) & 0x8000) != 0;
            if(isPgDn && !lastPgDnDown) {
                TriggerZoomOut();
            }
            lastPgDnDown = isPgDn;

            bool isM = (Native.GetAsyncKeyState(0x4D) & 0x8000) != 0;
            if(isM && !lastMDown && Native.GameFocused() && !chat.Paused) {
                TriggerFullMap();
            }
            lastMDown = isM;
        }
        void TriggerFullMap() {
            if((DateTime.UtcNow - lastMAction).TotalMilliseconds < 450) return;
            lastMAction = DateTime.UtcNow;
            fullMapActive = !fullMapActive;
            overlay.FullMapMode = fullMapActive;
            lastFrameKey = null;
            terrainKey = null;
            note = fullMapActive ? Localization.Get("NoteFullMapOn") : Localization.Get("NoteFullMapOff");
            RenderOverlay();
        }
        async void Tick(object sender,EventArgs args) {
            if(diagnosticMode || closing || IsDisposed) return;
            CheckHotkeysAsync();
            if(tickBusy) return;
            tickBusy=true;
            try {
            DateTime now=DateTime.UtcNow;
            if(now>=saveAfter)SaveSettings();
            bool gameFocused=Native.GameFocused();
            overlay.SetGameFocus(gameFocused);
            if(gameFocused && !wasGameFocused) {
                // SCUM regained focus: restore overlay and engage Auto mode
                if(hiddenByFocusLoss) { overlay.Show(); RenderOverlay(); hiddenByFocusLoss=false; }
                // Automatically resume sampling when gameplay regains focus.
                failures=0;
                next=now; // trigger immediate request
            }
            if(!gameFocused && wasGameFocused && !Visible && !searchOpen) {
                // SCUM lost focus: hide overlay entirely and cease input
                if(overlay.Visible) { overlay.Hide(); hiddenByFocusLoss=true; }
            }
            wasGameFocused=gameFocused;
            if(fullMapActive && !gameFocused) {

                fullMapActive=false; overlay.FullMapMode=false; lastFrameKey=null; terrainKey=null; note=Localization.Get("NoteFullMapOff"); RenderOverlay();

            } else if(fullMapActive && (DateTime.UtcNow - lastMAction).TotalMilliseconds > 600 && !Native.IsCursorVisible()) {

                fullMapActive=false; overlay.FullMapMode=false; lastFrameKey=null; terrainKey=null; note=Localization.Get("NoteFullMapOff"); RenderOverlay();

            }
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
                        if(pending && Native.GetClipboardSequenceNumber()==current) {
                            try {
                                if(savedDataObject!=null) Clipboard.SetDataObject(savedDataObject);
                                else Clipboard.Clear();
                            } catch {}
                            sequence=Native.GetClipboardSequenceNumber();
                        }
                    }
                    if(p!=null) pending=false;
                }
                if(pending && (now-sent).TotalSeconds>1) {
                    pending=false; failures++; note=Localization.T("NoteNoCoordsAttempt",failures);
                    if(failures>=3) { next=now.AddSeconds(5); note=Localization.Get("NoteNoCoordsRetry"); }
                }
                if(TrackingEnabled && !pending) {
                    if(chat.Paused)note=Localization.Get("NoteChatOpen");
                    else if(!keys.Available)note=Localization.Get("NoteChatMonitorUnavailable");
                    else if(!Native.GameFocused()) note=Localization.Get("NoteWaitingForeground");
                    else if(Native.KeysBusy()) note=Localization.Get("NoteWaitingUserKeys");
                }
                if(TrackingEnabled && keys.Available && !panelOpening && !Visible && !searchOpen && !chat.Paused && !pending && now>=resumeAfter && now>=next && Native.GameFocused() && !Native.KeysBusy()) {
                    int interval=copyIntervalMs;
                    if(position!=null && filteredSpeed<0.5) interval=Math.Max(copyIntervalMs,1000);
                    next=now.AddMilliseconds(interval);
                    try {
                        savedDataObject=Clipboard.GetDataObject();
                    } catch { savedDataObject=null; }
                    sequence=Native.GetClipboardSequenceNumber();
                    if(Native.GameFocused() && !Native.KeysBusy()) {
                        attempts++; CopyResult copyResult=await Native.Copy(()=>!closing && !panelOpening && !Visible && !searchOpen && TrackingEnabled && !chat.Paused && DateTime.UtcNow>=resumeAfter); sent=DateTime.UtcNow;
                        pending=copyResult==CopyResult.Sent;
                        note=pending?Localization.Get("NoteAutoCopyActive"):Localization.Get("NoteCopyRejected");
                        if(!pending) {
                            if(copyResult==CopyResult.Cancelled) note=Localization.Get("NoteCopyCancelled");
                            else { failures=3; next=DateTime.UtcNow.AddSeconds(5); note=Localization.T("NoteCopyErrorRetry",Native.CopyError); }
                        }
                    }
                }
            } catch(ExternalException) { note=Localization.Get("NoteClipboardBusy"); }
            string age=position==null?Localization.Get("WaitingForCoordinates"):string.Format(CultureInfo.InvariantCulture,"X {0:F0}  Y {1:F0}  Z {2:F0}  |  {3}",position.X,position.Y,position.Z,Localization.T("SecondsAgo",(int)(now-updated).TotalSeconds));
            if(Visible) {
                string statusText=age+Environment.NewLine+Localization.T("StatusSummary",attempts,responses)+Environment.NewLine+note;
                if(status.Text!=statusText) { bar.SuspendLayout(); status.Text=statusText; bar.ResumeLayout(false); }
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
            } finally { tickBusy=false; if(closing)BeginInvoke(new Action(Close)); }
        }
        public void SaveOverlayPreview(string path) { OverlayBitmap().Save(path,ImageFormat.Png); }
        public void CheckSearchPreview(string output) {
            if(!diagnosticMode)throw new InvalidOperationException("Search check requires diagnostic mode.");
            Exception failure=null; bool checkedDialog=false;
            using(Timer check=new Timer { Interval=100 }) {
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
        string OverlayAge() {
            if(position==null)return Localization.Get("AwaitingPositionShort");
            double seconds=Math.Max(0,(DateTime.UtcNow-updated).TotalSeconds);
            return seconds<=5?Localization.Get("Live"):Localization.T("SecondsAgo",(int)(seconds/5)*5);
        }
        string GetLocationDescription() {
            if(position==null) return Localization.Get("AwaitingPosition");
            PointF p=ToMap(position);
            int col=(int)Math.Floor(p.X*5f);
            int row=(int)Math.Floor(p.Y*5f);
            col=Math.Max(0,Math.Min(4,col));
            row=Math.Max(0,Math.Min(4,row));
            string[] letters=new string[]{"D","C","B","A","Z"};
            string sector=letters[row]+(4-col);

            string result = sector;
            bool foundSpecific = false;

            // 1. Inside a zone
            foreach(MapZone z in zones) {
                if(z.Contains(p)) {
                    lastVisitedZone=z;
                    lastVisitedTime=DateTime.UtcNow;
                    result = sector+" - "+Localization.GetZoneName(z.Name);
                    foundSpecific = true;
                    break;
                }
            }

            if(!foundSpecific) {
                // 2. Measure true boundary distances to all zones
                var sortedByDist=new List<KeyValuePair<double,MapZone>>();
                foreach(MapZone z in zones) {
                    double dist=z.DistanceTo(p);
                    sortedByDist.Add(new KeyValuePair<double,MapZone>(dist,z));
                }
                sortedByDist.Sort((a,b)=>a.Key.CompareTo(b.Key));

                MapZone closest=sortedByDist[0].Value;
                double closestDist=sortedByDist[0].Key;
                MapZone second=sortedByDist.Count>1 ? sortedByDist[1].Value : null;
                double secondDist=sortedByDist.Count>1 ? sortedByDist[1].Key : double.MaxValue;

                // 3. Movement vector and heading analysis
                if(previousMapPoint.HasValue) {
                    PointF prev=previousMapPoint.Value;
                    double moveVx = p.X - prev.X;
                    double moveVy = p.Y - prev.Y;
                    double moveSpeed = Math.Sqrt(moveVx*moveVx + moveVy*moveVy);

                    if(moveSpeed > 0.0003) {
                        // Check if actively moving away from last visited zone
                        if(lastVisitedZone != null && (DateTime.UtcNow-lastVisitedTime).TotalMinutes < 15) {
                            double dToLast = lastVisitedZone.DistanceTo(p);
                            double prevDToLast = lastVisitedZone.DistanceTo(prev);
                            if(dToLast > prevDToLast && dToLast < 0.045) {
                                PointF targetCentroid = closest == lastVisitedZone && second != null ? second.Centroid : closest.Centroid;
                                MapZone targetZone = closest == lastVisitedZone && second != null ? second : closest;
                                double targetDist = targetZone.DistanceTo(p);
                                double prevTargetDist = targetZone.DistanceTo(prev);

                                if(targetDist < prevTargetDist && targetDist < 0.065) {
                                    result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedZone.Name), Localization.GetZoneName(targetZone.Name));
                                    foundSpecific = true;
                                } else {
                                    result = Localization.T("LocLeaving", sector, Localization.GetZoneName(lastVisitedZone.Name));
                                    foundSpecific = true;
                                }
                            }
                        }

                        if(!foundSpecific) {
                            foreach(var kvp in sortedByDist) {
                                if(kvp.Key > 0.06) break;
                                MapZone candidate = kvp.Value;
                                double curD = kvp.Key;
                                double prevD = candidate.DistanceTo(prev);
                                if(curD < prevD && (prevD - curD) > 0.0001) {
                                    if(lastVisitedZone != null && candidate != lastVisitedZone && (DateTime.UtcNow-lastVisitedTime).TotalMinutes < 10 && lastVisitedZone.DistanceTo(p) < 0.05) {
                                        result = Localization.T("LocLeavingApproaching", sector, Localization.GetZoneName(lastVisitedZone.Name), Localization.GetZoneName(candidate.Name));
                                    } else {
                                        result = Localization.T("LocApproaching", sector, Localization.GetZoneName(candidate.Name));
                                    }
                                    foundSpecific = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if(!foundSpecific) {
                    if(closestDist < 0.035) {
                        result = Localization.T("LocNear", sector, Localization.GetZoneName(closest.Name));
                    } else if(closestDist < 0.065 && secondDist < 0.065 && Math.Abs(closestDist-secondDist) < 0.025) {
                        result = Localization.T("LocBetween", sector, Localization.GetZoneName(closest.Name), Localization.GetZoneName(second.Name));
                    } else if(closestDist < 0.065) {
                        result = Localization.T("LocNear", sector, Localization.GetZoneName(closest.Name));
                    }
                }
            }

            // Append Elevation
            if(showElevation && position!=null) {
                double zMeters = position.Z / 100.0;
                result += Localization.T("ElevationMeters", (int)Math.Round(zMeters));
            }

            // Append pin info if set
            if(pin!=null && position!=null) {
                double wdx = (pin.X - position.X) / 100.0;
                double wdy = (pin.Y - position.Y) / 100.0;
                double wdist = Math.Sqrt(wdx*wdx + wdy*wdy);
                string dist = wdist > 1000 ? string.Format(CultureInfo.InvariantCulture,"{0:F1}km",wdist/1000.0) : ((int)wdist)+"m";
                result += Localization.T("PinDistance", dist);
            }

            if(searchTarget!=null) {
                result += Localization.T("ToDestination", Localization.GetZoneName(searchTarget.Name));
                if(position!=null) {
                    double distance;
                    string suffix = "";
                    if(activeRoute!=null && activeRoute.Success) {
                        distance = activeRoute.TotalDistanceMeters;
                        suffix = Localization.Get("RoadSuffix");
                    } else {
                        PointF target=searchTarget.Centroid, current=ToMap(position);
                        double dx=(target.X-current.X)*15216.18,dy=(target.Y-current.Y)*15236.18;
                        distance=Math.Sqrt(dx*dx+dy*dy);
                    }
                    result += " "+(distance>=1000?(distance/1000).ToString("F1",CultureInfo.InvariantCulture)+"km":((int)distance)+"m") + suffix;
                }
            }
            return result;
        }
        string OverlayKey() {
            int mx=motion!=null?(int)Math.Round(motion.Point.X*100000):0;
            int my=motion!=null?(int)Math.Round(motion.Point.Y*100000):0;
            int myaw=motion!=null?(int)Math.Round(motion.Yaw*10):0;
            return string.Format(CultureInfo.InvariantCulture,"{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}|{20}|{21}",
                position==null?0:position.X,position==null?0:position.Y,position==null?0:position.Z,position==null?0:position.Yaw,
                zoom,chat.Paused,TrackingEnabled,OverlayAge(),overlay.Width,overlay.Height,showStatus,statusPos,overlayShape,showHeading,showCompass,showElevation,GetLocationDescription(),searchTarget==null?"":searchTarget.Name,
                mx,my,myaw,fullMapActive);
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

            using(Graphics g=Graphics.FromImage(bitmap)) {
                g.Clear(Color.FromArgb(0,0,0,0));
                // Render the map portion
                DrawMapArea(g,new Rectangle(0,mapY,bitmap.Width,mapH));
            }
            // Apply subtle edge fade strictly within the map bounds and fade as player approaches/reaches the edge of the world map
            Rectangle mapBounds=new Rectangle(0,mapY,bitmap.Width,mapH);
            float currentZoom=fullMapActive?1.0f:zoom;
            float side=Math.Min(mapBounds.Width,mapBounds.Height)*currentZoom;
            PointF p=position==null?new PointF(.5f,.5f):(motion!=null && motion.Point.X>0?motion.Point:ToMap(position));
            PointF centrePt=(fullMapActive || currentZoom==1)?new PointF(.5f,.5f):p;
            int mapLeft=(int)Math.Round(mapBounds.Left+mapBounds.Width/2f-side*centrePt.X);
            int mapTop=(int)Math.Round(mapBounds.Top+mapBounds.Height/2f-side*centrePt.Y);
            int mapRight=(int)Math.Round(mapLeft+side);
            int mapBottom=(int)Math.Round(mapTop+side);
            OverlayWindow.Fade(bitmap,fullMapActive?false:edgeFade,fullMapActive?100:mapOpacity,zoom==1?mapLeft:-1,zoom==1?mapTop:-1,zoom==1?mapRight:-1,zoom==1?mapBottom:-1,fullMapActive?"Square":overlayShape);

            // Now draw the crisp HUD location info banner outside/over the map so it is not faded
            if(barActive) {
                using(Graphics g=Graphics.FromImage(bitmap)) {
                    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    string loc=GetLocationDescription();
                    string mode=chat.Paused?"CHAT":!keys.Available?"WAIT":failures>=3?"RETRY":"AUTO";
                    string age=OverlayAge();
                    string rightTag=mode+" | "+age;
                    Color badgeColor=chat.Paused?Color.FromArgb(240,195,65):TrackingEnabled?Color.FromArgb(60,225,130):Color.FromArgb(190,195,200);

                    using(Font locFont=new Font("Segoe UI",9.0f,FontStyle.Bold))
                    using(Font tagFont=new Font("Segoe UI",8.0f,FontStyle.Regular)) {
                        SizeF rightSz=g.MeasureString(rightTag,tagFont);
                        SizeF locSz=g.MeasureString(loc,locFont);

                        if(overlayShape=="Circle") {
                            // Circular HUD: Sleek floating curved pill banner positioned cleanly below the circle
                            int pillH = 26;
                            int pillW = Math.Min(bitmap.Width - 36, Math.Max(160, (int)(locSz.Width + rightSz.Width + 38)));
                            int pillX = (bitmap.Width - pillW) / 2;
                            int pillY = barAbove ? 2 : bitmap.Height - pillH - 2;
                            Rectangle pillRect = new Rectangle(pillX, pillY, pillW, pillH);

                            // Draw rounded capsule pill
                            using(GraphicsPath pillPath = new GraphicsPath()) {
                                int r = pillH; // pill radius
                                pillPath.AddArc(pillRect.X, pillRect.Y, r, r, 90, 180);
                                pillPath.AddArc(pillRect.Right - r, pillRect.Y, r, r, 270, 180);
                                pillPath.CloseFigure();

                                using(Brush bg = new SolidBrush(Color.FromArgb(225, 10, 14, 18))) {
                                    g.FillPath(bg, pillPath);
                                }
                                using(Pen border = new Pen(Color.FromArgb(180, 55, 68, 80), 1.2f)) {
                                    g.DrawPath(border, pillPath);
                                }
                            }

                            // Draw status badge on right inside pill
                            float rx = pillRect.Right - rightSz.Width - 14;
                            float ry = pillRect.Top + (pillH - rightSz.Height) / 2f;
                            using(Brush badgeBrush = new SolidBrush(badgeColor)) {
                                g.DrawString(rightTag, tagFont, badgeBrush, rx, ry);
                            }

                            // Draw location text on left inside pill
                            float maxLocW = Math.Max(40, rx - pillRect.Left - 16);
                            RectangleF locBox = new RectangleF(pillRect.Left + 14, pillRect.Top, maxLocW, pillH);
                            StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                            g.DrawString(loc, locFont, Brushes.WhiteSmoke, locBox, sf);
                        } else {
                            // Square HUD: Clean edge-to-edge docking bar
                            Rectangle barRect=new Rectangle(0,barAbove?0:bitmap.Height-barH,bitmap.Width,barH);
                            using(Brush bg=new SolidBrush(Color.FromArgb(235,12,17,22))) g.FillRectangle(bg,barRect);
                            using(Pen border=new Pen(Color.FromArgb(160,45,55,65),1)) {
                                if(barAbove) g.DrawLine(border,0,barH-1,bitmap.Width,barH-1);
                                else g.DrawLine(border,0,barRect.Top,bitmap.Width,barRect.Top);
                            }

                            // Draw right status badge
                            float rightX=barRect.Right-rightSz.Width-12;
                            float rightY=barRect.Top+(barH-rightSz.Height)/2f;
                            using(Brush badgeBrush=new SolidBrush(badgeColor)) {
                                g.DrawString(rightTag,tagFont,badgeBrush,rightX,rightY);
                            }

                            // Draw location text on left
                            float maxLocW=Math.Max(50,rightX-24);
                            RectangleF locBox=new RectangleF(12,barRect.Top,maxLocW,barH);
                            StringFormat sf=new StringFormat { LineAlignment=StringAlignment.Center, Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap };
                            g.DrawString(loc,locFont,Brushes.WhiteSmoke,locBox,sf);
                        }
                    }
                }
            }
            return bitmap;
        }
        void RenderOverlay() {
            if(!overlay.Visible || overlay.IsDisposed)return;
            string frameKey=OverlayKey();
            if(frameKey==lastFrameKey)return;
            overlay.Present(OverlayBitmap()); lastFrameKey=frameKey; PresentedFrames++;
        }
        public static PointF ToMap(Position p) {
            return new PointF((float)((617018-p.X)/1521618),(float)((619018-p.Y)/1523618));
        }
        void PaintMap(object sender,PaintEventArgs e) {
            DrawMapArea(e.Graphics,new Rectangle(0,0,canvas.Width,canvas.Height));
        }
        void DrawMap(Graphics g,int width,int height) {
            DrawMapArea(g,new Rectangle(0,0,width,height));
        }
        void DrawMapArea(Graphics g,Rectangle bounds) {
            using(Brush b=new SolidBrush(Color.FromArgb(12,17,22))) g.FillRectangle(b,bounds);
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            float effectiveZoom=fullMapActive?1.0f:zoom;
            float side=Math.Min(bounds.Width,bounds.Height)*effectiveZoom;
            PointF p=position==null?new PointF(.5f,.5f):(motion!=null && motion.Point.X>0?motion.Point:ToMap(position));
            PointF mapCentre=effectiveZoom==1?new PointF(.5f,.5f):p;
            float left=bounds.Left+bounds.Width/2f-side*mapCentre.X;
            float top=bounds.Top+bounds.Height/2f-side*mapCentre.Y;
            string terrain=string.Join("|",new object[]{bounds,zoom,(int)Math.Round(left),(int)Math.Round(top),gridLabels,gridBorders,gridOpacity,showZones,showGasStations,showCities,showTowns,showFarms,showTraders,showFactions,showMilitary,showCustomWaypoints,showZoneLabels,smartLabelLod,fullMapActive,labelSize,Localization.CurrentCode});
            if(terrainKey!=terrain) {
                using(Graphics background=Graphics.FromImage(fullMap)) {
                    background.Clear(Color.FromArgb(12,17,22));
                    background.SmoothingMode=SmoothingMode.AntiAlias;
                    background.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    background.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    Image texture=map;
                    foreach(Bitmap level in mapLevels) { if(level.Width<side || level.Height<side)break; texture=level; }
                    background.DrawImage(texture,left,top,side,side);
                    DrawGrid(background,left,top,side);
                    float currentZoom=fullMapActive?1.0f:zoom;
                    if(showZones || showGasStations)ZoneStore.Draw(background,zones,new RectangleF(left,top,side,side),showZones,labelSize,showGasStations,BuildHiddenCategories(),showZoneLabels,smartLabelLod,currentZoom);
                }
                terrainKey=terrain; TerrainBuilds++;
            }
            g.DrawImageUnscaled(fullMap,0,0);

            // Draw custom pin marker if set
            if(pin!=null) {
                PointF wpt=ToMap(pin);
                float wx=left+wpt.X*side, wy=top+wpt.Y*side;
                using(Brush wb=new SolidBrush(Color.FromArgb(240,230,40)))
                using(Pen wp=new Pen(Color.FromArgb(30,30,30),1.5f)) {
                    PointF[] diamond=new PointF[]{
                        new PointF(wx,wy-9), new PointF(wx+7,wy),
                        new PointF(wx,wy+9), new PointF(wx-7,wy)
                    };
                    g.FillPolygon(wb,diamond);
                    g.DrawPolygon(wp,diamond);
                    using(Font wfont=new Font("Segoe UI",7.5f,FontStyle.Bold)) {
                        g.DrawString("PIN",wfont,Brushes.Black,wx-19,wy+10);
                        g.DrawString("PIN",wfont,Brushes.Yellow,wx-20,wy+9);
                    }
                }
            }

            // Draw search navigation line to target zone
            if(searchTarget!=null && position!=null) {
                PointF targetPt=searchTarget.Centroid;
                float tx=left+targetPt.X*side, ty=top+targetPt.Y*side;
                float px=left+p.X*side, py=top+p.Y*side;

                if(activeRoute!=null && activeRoute.Success && activeRoute.Polyline!=null && activeRoute.Polyline.Length>1) {
                    // Draw feeder from player to road entry point if off-road
                    if(activeRoute.EntryDistanceMeters>15.0) {
                        float enx=left+activeRoute.RoadEntryPoint.X*side, eny=top+activeRoute.RoadEntryPoint.Y*side;
                        using(Pen feederPen=new Pen(Color.FromArgb(220,100,220,255),3.0f)) {
                            feederPen.DashStyle=DashStyle.Dash;
                            feederPen.DashPattern=new float[]{5f,3f};
                            g.DrawLine(feederPen,px,py,enx,eny);
                        }
                    }

                    // Draw feeder from road exit point to target if off-road
                    if(activeRoute.ExitDistanceMeters>15.0) {
                        float exx=left+activeRoute.RoadExitPoint.X*side, exy=top+activeRoute.RoadExitPoint.Y*side;
                        using(Pen feederPen=new Pen(Color.FromArgb(220,100,220,255),3.0f)) {
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
                    using(Pen glowPen=new Pen(Color.FromArgb(85,15,120,240),4.2f)) {
                        glowPen.LineJoin=LineJoin.Round;
                        g.DrawLines(glowPen,screenPts);
                    }
                    using(Pen routePen=new Pen(Color.FromArgb(230,100,225,255),2.2f)) {
                        routePen.LineJoin=LineJoin.Round;
                        g.DrawLines(routePen,screenPts);
                    }
                } else {
                    // Fallback to straight dashed navigation line
                    using(Pen navPen=new Pen(Color.FromArgb(220,100,220,255),3.0f)) {
                        navPen.DashStyle=DashStyle.Dash;
                        navPen.DashPattern=new float[]{6f,4f};
                        g.DrawLine(navPen,px,py,tx,ty);
                    }
                }

                // Target zone marker (pulsing ring)
                using(Pen ringPen=new Pen(Color.FromArgb(180,100,220,255),2.0f)) {
                    g.DrawEllipse(ringPen,tx-10,ty-10,20,20);
                }
                using(Brush dotBrush=new SolidBrush(Color.FromArgb(220,100,220,255))) {
                    g.FillEllipse(dotBrush,tx-4,ty-4,8,8);
                }

                // Zone name label at target
                string targetDisplayName=Localization.GetZoneName(searchTarget.Name);
                using(Font navFont=new Font("Segoe UI",7.5f,FontStyle.Bold)) {
                    SizeF sz=g.MeasureString(targetDisplayName,navFont);
                    float lx=tx-sz.Width/2f, ly=ty-16-sz.Height;
                    using(Brush bgBrush=new SolidBrush(Color.FromArgb(190,10,14,18))) {
                        g.FillRectangle(bgBrush,lx-3,ly-1,sz.Width+6,sz.Height+2);
                    }
                    using(Brush textBrush=new SolidBrush(Color.FromArgb(240,100,220,255))) {
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
                float x=left+p.X*side,y=top+p.Y*side;
                Color playerColor = (DateTime.UtcNow-updated).TotalSeconds>5 ? Color.Orange : Color.Cyan;

                // Draw FOV / Directional heading cone if enabled
                if(showHeading) {
                    // SCUM Unreal Engine Yaw adjusted by -90Â° to match screen orientation
                    double drawYaw = motion!=null && motion.Point.X>0 ? motion.Yaw : position.Yaw;
                    float yawRad = (float)((drawYaw - 180.0) * Math.PI / 180.0);
                    float coneDist = 58f;
                    float halfAngle = 36f * (float)(Math.PI / 180.0);
                    PointF p1 = new PointF(x, y);

                    // Create curved arc cone geometry
                    using(GraphicsPath conePath = new GraphicsPath()) {
                        List<PointF> arcPoints = new List<PointF>();
                        arcPoints.Add(p1);
                        int steps = 14;
                        for(int i = 0; i <= steps; i++) {
                            float a = (yawRad - halfAngle) + (2f * halfAngle * i / steps);
                            arcPoints.Add(new PointF(x + coneDist * (float)Math.Cos(a), y + coneDist * (float)Math.Sin(a)));
                        }
                        conePath.AddPolygon(arcPoints.ToArray());

                        // Render smooth radial fade out toward the perimeter
                        using(PathGradientBrush pgb = new PathGradientBrush(conePath)) {
                            pgb.CenterPoint = p1;
                            pgb.CenterColor = Color.FromArgb(140, playerColor.R, playerColor.G, playerColor.B);
                            pgb.SurroundColors = new Color[] { Color.FromArgb(0, playerColor.R, playerColor.G, playerColor.B) };
                            g.FillPath(pgb, conePath);
                        }
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
                    float cx=bounds.Left+bounds.Width/2f;
                    float cy=bounds.Top+bounds.Height/2f;
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
            for(int i=0;i<6;i++) { xs[i]=left+(float)((616818-cols[i])/1521618)*side; ys[i]=top+(float)((618818-rows[i])/1523618)*side; }
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
            if(target==null || !RoadRouter.Instance.IsLoaded) {
                activeRoute=null; lastRouteTarget=null; return;
            }
            if(lastRouteTarget==target && lastRoutePlayerPt!=PointF.Empty) {
                double movedM=RoadRouter.DistanceMeters(lastRoutePlayerPt, playerPt);
                if(movedM<25.0 && activeRoute!=null) return;
            }
            if(routeCalculating) return;
            routeCalculating=true;
            lastRoutePlayerPt=playerPt;
            lastRouteTarget=target;
            PointF tgtPt=target.Centroid;

            System.Threading.ThreadPool.QueueUserWorkItem(_ => {
                try {
                    RoadRoute r=RoadRouter.Instance.FindRoute(playerPt, tgtPt);
                    if(!IsDisposed && IsHandleCreated) {
                        BeginInvoke(new Action(()=> {
                            activeRoute=r;
                            routeCalculating=false;
                            lastFrameKey=null;
                            if(Visible) canvas.Invalidate();
                            RenderOverlay();
                        }));
                    } else {
                        activeRoute=r;
                        routeCalculating=false;
                    }
                } catch {
                    routeCalculating=false;
                }
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
                if(string.Join(",",chord)!="162:False,67:False,67:True,162:True") throw new Exception("Interrupted chord did not release both keys.");
            }
            ZoneStore.SelfTest();
            DestinationSearch.SelfTest();
            Localization.SelfTest();
            MapMotion.SelfTest();
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
            state.Key(0x54); state.Key(0x54);
            if(!state.Paused)throw new Exception("T must pause without toggling.");
            state.Key(0xBF); if(!state.Paused)throw new Exception("Typing must preserve chat pause.");
            state.Key(0x0D); if(state.Paused)throw new Exception("Enter must resume.");
            state.Key(0x54); state.Key(0x1B); if(state.Paused)throw new Exception("Escape must resume.");
            using(Bitmap fade=new Bitmap(60,60,PixelFormat.Format32bppPArgb)) {
                using(Graphics g=Graphics.FromImage(fade))g.Clear(Color.White);
                OverlayWindow.Fade(fade);
                if(fade.GetPixel(0,30).A!=0 || fade.GetPixel(29,29).A<230 || fade.GetPixel(10,30).A>=fade.GetPixel(29,29).A)throw new Exception("Edge alpha fade failed.");
            }
            Position p=Position.Parse("{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");
            if(p==null || p.Y!=-270302.625 || p.Yaw!=13.952554) throw new Exception("Coordinate/rotation parsing failed.");
            if(Position.Parse("hello")!=null || Position.Parse("prefix {X=1 Y=2 Z=3|P=0 Y=0 R=0}")!=null) throw new Exception("Invalid clipboard text accepted.");
            PointF corner=ToMap(new Position { X=617018,Y=619018 });
            PointF end=ToMap(new Position { X=-904600,Y=-904600 });
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
        [STAThread]
        public static void Main(string[] args) {
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
                string screenshot=null;
                for(int i=0;i<args.Length;i++) {
                    string a=args[i];
                    if(a.Equals("-Check",StringComparison.OrdinalIgnoreCase)||a.Equals("/Check",StringComparison.OrdinalIgnoreCase)||a.Equals("--check",StringComparison.OrdinalIgnoreCase)) check=true;
                    else if(a.Equals("-Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("/Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("--preview",StringComparison.OrdinalIgnoreCase)) preview=true;
                    else if(a.Equals("-CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("/CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("--check-automatic",StringComparison.OrdinalIgnoreCase)) checkAuto=true;
                    else if((a.Equals("-ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("/ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("--import-screenshot",StringComparison.OrdinalIgnoreCase)) && i+1<args.Length) { screenshot=args[++i]; }
                }
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
                using(MapWindow window=new MapWindow(appData)) {
                    Application.Run(window);
                }
            }
        }
    }
}
