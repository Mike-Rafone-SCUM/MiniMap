using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Shared visual language for interactive HUD panels and popup menus (Tactical Survival Hardware aesthetic).
    public static class OverlayTheme {
        public static readonly Color Background=Color.FromArgb(12,12,12);
        public static readonly Color Surface=Color.FromArgb(20,20,21);
        public static readonly Color SurfaceCard=Color.FromArgb(27,27,28);
        public static readonly Color Accent=Color.FromArgb(255,159,28);
        public static readonly Color AccentHover=Color.FromArgb(255,178,55);
        public static readonly Color Border=Color.FromArgb(50,48,44);
        public static readonly Color BorderHighlight=Color.FromArgb(82,78,70);
        public static readonly Color Ink=Color.FromArgb(240,238,232);
        public static readonly Color InkMuted=Color.FromArgb(150,146,138);

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);

        public static void Style(Control root) {
            root.ForeColor=Ink;
            Button button=root as Button;
            if(button!=null) {
                if((string)button.Tag == "swatch") return;
                button.FlatStyle=FlatStyle.Flat;
                button.FlatAppearance.BorderSize=1;
                button.FlatAppearance.BorderColor=Border;
                button.FlatAppearance.MouseOverBackColor=Color.FromArgb(34,32,28);
                button.FlatAppearance.MouseDownBackColor=Color.FromArgb(48,45,40);
                button.BackColor=Surface;
                button.UseVisualStyleBackColor=false;
                button.Cursor=Cursors.Hand;
                button.Height=Math.Max(28,button.Height);
            } else if(root is TextBox || root is ListBox || root is NumericUpDown) {
                root.BackColor=Color.FromArgb(14,17,20);
                root.ForeColor=Ink;
            } else if(root is TreeView) {
                TreeView tv=root as TreeView;
                tv.BackColor=Color.FromArgb(14,17,20);
                tv.ForeColor=Ink;
                tv.LineColor=Border;
            } else {
                root.BackColor=Background;
            }
            CheckBox check=root as CheckBox;
            if(check!=null) {
                check.FlatStyle=FlatStyle.Flat;
                check.Cursor=Cursors.Hand;
                check.ForeColor=Ink;
                check.FlatAppearance.BorderSize=1;
                check.FlatAppearance.BorderColor=Border;
                check.FlatAppearance.CheckedBackColor=Accent;
                check.FlatAppearance.MouseDownBackColor=Color.FromArgb(48,45,40);
                check.FlatAppearance.MouseOverBackColor=Color.FromArgb(34,32,28);
            }
            ComboBox combo=root as ComboBox;
            if(combo!=null && !(combo is TacticalComboBox)) {
                combo.FlatStyle=FlatStyle.Flat;
                combo.BackColor=Surface;
                combo.ForeColor=Ink;
                combo.DrawMode=DrawMode.OwnerDrawFixed;
                combo.ItemHeight=22;
                if((string)combo.Tag != "styled") {
                    combo.Tag = "styled";
                    combo.DrawItem+=(s,e)=> {
                        if(e.Index<0) return;
                        bool sel=(e.State & DrawItemState.Selected)!=0;
                        using(Brush b=new SolidBrush(sel?Color.FromArgb(45,35,20):Surface)) e.Graphics.FillRectangle(b,e.Bounds);
                        string itmText=combo.Items[e.Index].ToString();
                        TextRenderer.DrawText(e.Graphics, itmText, combo.Font, e.Bounds, sel?Accent:Ink, TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
                    };
                }
            }
            if(!(root is NumericUpDown)) {
                foreach(Control child in root.Controls) Style(child);
            }
        }

        public static void Frame(Form form,string title,Action dismiss) {
            form.FormBorderStyle=FormBorderStyle.None; form.TopMost=true; form.ShowInTaskbar=true;
            form.Padding=new Padding(1); form.BackColor=Background; form.Font=new Font("Segoe UI",9);
            Panel header=new Panel { Dock=DockStyle.Top,Height=52,Padding=new Padding(14,8,8,8),BackColor=Surface };
            Label caption=new Label { Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Accent };
            Button close=new Button { Text=Localization.Get("Close"),Dock=DockStyle.Right,Width=62,TabStop=false };
            close.Click+=(s,e)=>dismiss();
            caption.MouseDown+=(s,e)=> { if(e.Button==MouseButtons.Left) { ReleaseCapture(); SendMessage(form.Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
            header.Controls.Add(caption); header.Controls.Add(close); form.Controls.Add(header);
            form.TextChanged+=(s,e)=>caption.Text=form.Text;
            Action languageChanged=()=> { if(!form.IsDisposed) close.Text=Localization.Get("Close"); };
            Localization.LanguageChanged+=languageChanged;
            form.Disposed+=(s,e)=>Localization.LanguageChanged-=languageChanged;
            Style(form); caption.ForeColor=Accent;
            form.Paint+=(s,e)=> { using(Pen pen=new Pen(Border,1f)) e.Graphics.DrawRectangle(pen,0,0,form.ClientSize.Width-1,form.ClientSize.Height-1); };
            form.KeyPreview=true;
            form.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Escape) { dismiss(); e.Handled=true; e.SuppressKeyPress=true; } };
        }

        internal static void Anchor(Form panel,Form map) {
            Screen scr = null;
            try {
                if(map != null && map.IsHandleCreated) scr = Screen.FromControl(map);
                else scr = Screen.PrimaryScreen;
            } catch {
                scr = Screen.PrimaryScreen;
            }
            Rectangle area = scr != null ? scr.WorkingArea : Screen.PrimaryScreen.WorkingArea;
            OverlayWindow ow = map as OverlayWindow;
            if(ow != null && ow.FullMapMode) {
                panel.Location = new Point(area.Left + Math.Max(0, (area.Width - panel.Width) / 2),
                                           area.Top + Math.Max(20, (area.Height - panel.Height) / 2));
                return;
            }
            if(map != null && map.Visible) {
                int x = map.Left - panel.Width - 12;
                if(x < area.Left) x = map.Right + 12;
                panel.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - panel.Width)),
                                           Math.Max(area.Top, Math.Min(map.Top, area.Bottom - panel.Height)));
            } else {
                int x = area.Right - panel.Width - 24;
                int y = area.Top + 60;
                panel.Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, Math.Min(y, area.Bottom - panel.Height)));
            }
        }

        internal static Label Section(FlowLayoutPanel bar,string title) {
            Label marker=new Label { Text=title,Tag="section",AutoSize=true }; bar.Controls.Add(marker); return marker;
        }

        internal static void Sections(FlowLayoutPanel bar) {
            Control[] original=new Control[bar.Controls.Count]; bar.Controls.CopyTo(original,0); bar.Controls.Clear();
            FlowLayoutPanel page=null;
            int sectionWidth = Math.Max(380, bar.ClientSize.Width > 0 ? bar.ClientSize.Width - bar.Padding.Horizontal - 4 : 400);
            foreach(Control item in original) {
                if((string)item.Tag=="section") {
                    string sectionTitle=item.Text;
                    bool startExpanded = true;
                    TacticalSectionHeader header = new TacticalSectionHeader(sectionTitle, startExpanded);
                    header.Width = sectionWidth;
                    page=new FlowLayoutPanel { Width=header.Width,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(4,2,0,10),Visible=startExpanded };
                    FlowLayoutPanel target=page;
                    TacticalSectionHeader targetHeader=header;
                    targetHeader.ToggleRequested+=(s,e)=> {
                        try {
                            bar.SuspendLayout();
                            target.Visible=targetHeader.Expanded;
                            bar.ResumeLayout(true);
                        } catch {}
                    };
                    bar.Controls.Add(header); bar.Controls.Add(page); item.Dispose();
                } else if((string)item.Tag=="footer") {
                    bar.Controls.Add(item);
                } else if(page!=null) {
                    page.Controls.Add(item);
                }
            }
        }

        internal static void Menu(ContextMenuStrip menu) {
            menu.Renderer=new HudMenuRenderer(); menu.ShowImageMargin=false; menu.BackColor=Background; menu.ForeColor=Ink;
            menu.Font=new Font("Segoe UI",9); menu.Padding=new Padding(5);
            foreach(ToolStripItem item in menu.Items) { item.Padding=new Padding(12,7,12,7); item.ForeColor=Ink; }
        }

        sealed class HudMenuRenderer:ToolStripProfessionalRenderer {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Background); }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
                if(e.Item.Selected) {
                    using(Brush b=new SolidBrush(Surface)) e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));
                    using(Pen p=new Pen(Border)) e.Graphics.DrawRectangle(p,new Rectangle(0,0,e.Item.Size.Width-1,e.Item.Size.Height-1));
                }
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor=e.Item.Selected?Accent:Ink; base.OnRenderItemText(e); }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using(Pen p=new Pen(Border)) e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1); }
        }
    }
    // FlowLayoutPanel that prevents automatic snapping/jumping to top when child controls are focused or clicked.
    sealed class SettingsPanel:FlowLayoutPanel {
        [DllImport("uxtheme.dll", ExactSpelling=true, CharSet=CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        public SettingsPanel() {
            DoubleBuffered=true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch {}
        }
        protected override Point ScrollToControl(Control activeControl) {
            return AutoScrollPosition;
        }
    }
    // Return focus only when an explicitly opened panel still owns it.
    // Alt-Tab to another application must never be undone.
    sealed class GameFocusReturn {
        IntPtr game;
        uint gamePid;
        internal void Capture() {
            game=Native.GetForegroundWindow();
            if(!Native.IsGameWindow(game)) game=IntPtr.Zero;
            Native.GetWindowThreadProcessId(game,out gamePid);
        }
        internal static bool ShouldRestore(bool validGame,uint targetPid,uint savedPid,uint foregroundPid,uint appPid) {
            return validGame && targetPid==savedPid && foregroundPid==appPid;
        }
        internal void Restore() {
            IntPtr target=game; game=IntPtr.Zero;
            uint foregroundPid,targetPid;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out foregroundPid);
            Native.GetWindowThreadProcessId(target,out targetPid);
            using(System.Diagnostics.Process app=System.Diagnostics.Process.GetCurrentProcess()) {
                if(ShouldRestore(Native.IsGameWindow(target),targetPid,gamePid,foregroundPid,(uint)app.Id)) Native.SetForegroundWindow(target);
            }
        }
    }
    public sealed class MapMotion {
        public PointF Point;
        public double Yaw;
        PointF start,target;
        double startYaw,targetYaw,began,duration,lastSample;
        bool initialized;
        public static double HeadingDelta(double from,double to) { return ((to-from+540)%360+360)%360-180; }
        public void Sample(PointF point,double yaw,double now,bool snap) {
            Advance(now);
            double interval=now-lastSample;
            if(!initialized || snap || interval>3 || DestinationSearch.Metres(Point,point)>150) {
                Point=point; Yaw=yaw;
            }
            if(initialized && !snap && DestinationSearch.Metres(Point,point)<0.30) point=Point;
            if(initialized && !snap && Math.Abs(HeadingDelta(Yaw,yaw))<1.2) yaw=Yaw;
            start=Point; target=point; startYaw=Yaw; targetYaw=Yaw+HeadingDelta(Yaw,yaw);
            began=now; duration=Math.Max(.1,Math.Min(3.0,initialized?interval:.5));
            lastSample=now; initialized=true;
        }
        public bool Advance(double now) {
            if(!initialized)return false;
            double t=Math.Max(0,Math.Min(1,(now-began)/duration));
            PointF next=new PointF((float)(start.X+(target.X-start.X)*t),(float)(start.Y+(target.Y-start.Y)*t));
            double heading=startYaw+(targetYaw-startYaw)*t;
            bool changed=next!=Point || Math.Abs(heading-Yaw)>.0001;
            Point=next; Yaw=heading; return changed;
        }
        public static void SelfTest() {
            MapMotion m=new MapMotion(); m.Sample(new PointF(.5f,.5f),359,0,false);
            m.Sample(new PointF(.501f,.5f),1,.25,false); m.Advance(.375);
            if(Math.Abs(m.Point.X-.5005)>.000001 || Math.Abs(m.Yaw-360)>.001)throw new Exception("Motion midpoint or heading wrap failed.");
            m.Advance(2); if(Math.Abs(m.Point.X-.501)>.000001 || m.Advance(3))throw new Exception("Motion overshoot or idle redraw.");
            m.Sample(new PointF(.9f,.9f),45,3,false); if(m.Point.X!=.9f)throw new Exception("Teleport must snap.");
            m.Sample(new PointF(.900005f,.9f),45.5,4,false);
            if(m.Point.X!=.9f || m.Yaw!=45.0)throw new Exception("Stationary micro-jitter must be deadbanded.");
        }
    }
    public sealed class ChatState {
        DateTime openTime = DateTime.MinValue;
        public bool Paused {
            get {
                if(openTime == DateTime.MinValue) return false;
                if((DateTime.UtcNow - openTime).TotalSeconds > 25) {
                    openTime = DateTime.MinValue;
                    return false;
                }
                return true;
            }
        }
        public void Key(int key,int chatOpenKey=0x54) {
            // Enter (0x0D) and Escape (0x1B) close chat in SCUM
            if(key==0x0D || key==0x1B) {
                openTime = DateTime.MinValue;
                return;
            }
            // Opening chat via configured chat key, '/' or keypad '/'
            if(key==chatOpenKey || key==0xBF || key==0x6F) {
                openTime = DateTime.UtcNow;
                return;
            }
            // While chat is already open, any keypress (Tab to cycle channels, typing letters/numbers,
            // space, backspace, etc.) extends the active chat session so it never times out mid-typing.
            if(Paused) {
                openTime = DateTime.UtcNow;
            }
        }
        public void Reset() { openTime = DateTime.MinValue; }
    }
    public sealed class PhysicalKeyTransitions {
        readonly bool[] held=new bool[256];
        static readonly int[] ModifierKeys = new int[] {
            0x11, 0xA2, 0xA3, // Ctrl, LCtrl, RCtrl
            0x12, 0xA4, 0xA5, // Alt, LAlt, RAlt
            0x5B, 0x5C,       // LWin, RWin
            0x10, 0xA0, 0xA1  // Shift, LShift, RShift
        };
        public bool IsDown(int key) {
            return key>=0 && key<held.Length && held[key];
        }
        public bool ShortcutModifiersDown(bool includeShift) {
            return held[0x11] || held[0xA2] || held[0xA3] ||
                held[0x12] || held[0xA4] || held[0xA5] || held[0x5B] || held[0x5C] ||
                (includeShift && (held[0x10] || held[0xA0] || held[0xA1]));
        }
        public bool Update(int key,bool down) {
            if(key<0 || key>=held.Length) return false;
            bool fresh=down && !held[key];
            held[key]=down;
            return fresh;
        }
        public void Resync(Func<int,bool> isDown) {
            if(isDown==null) return;
            for(int i=0; i<ModifierKeys.Length; i++) {
                int key = ModifierKeys[i];
                held[key] = isDown(key);
            }
        }
    }
    // Observes shortcut transitions and activity timestamps. No typed text is collected or suppressed.
    sealed class GameKeys:IDisposable {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [StructLayout(LayoutKind.Sequential)] struct KeyboardData { public uint key,scan,flags,time; public UIntPtr extra; }
        readonly HookProc callback;
        readonly Action<int> action;
        readonly Func<int,bool> shouldDispatch;
        readonly ChatState chatState;
        readonly Func<int> getChatOpenKey;
        readonly PhysicalKeyTransitions transitions=new PhysicalKeyTransitions();
        IntPtr hook;
        public static DateTime LastUserInputTime = DateTime.MinValue;
        public static DateTime LastFreshKeyDownTime = DateTime.MinValue;
        internal static bool UserActiveRecently(int windowMs = 90) {
            return (DateTime.UtcNow - LastFreshKeyDownTime).TotalMilliseconds < windowMs;
        }
        public bool Available { get { return hook!=IntPtr.Zero; } }
        public bool ShortcutModifiersDown(bool includeShift) { return transitions.ShortcutModifiersDown(includeShift); }
        public GameKeys(Action<int> action,Func<int,bool> shouldDispatch,ChatState chatState=null,Func<int> getChatOpenKey=null) {
            this.action=action; this.shouldDispatch=shouldDispatch; this.chatState=chatState; this.getChatOpenKey=getChatOpenKey; callback=OnKey;
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        }
        public void Resync() { transitions.Resync(key => (Native.GetAsyncKeyState(key)&0x8000)!=0); }
        IntPtr OnKey(int code,IntPtr message,IntPtr data) {
            if(code>=0) {
                KeyboardData key=(KeyboardData)Marshal.PtrToStructure(data,typeof(KeyboardData));
                // Injected events must never alter physical held-key or chat state, even after copying ends.
                if((key.flags & 0x10)!=0) return CallNextHookEx(hook,code,message,data);
                int k=(int)key.key;
                bool isDown=message.ToInt32()==0x100 || message.ToInt32()==0x104;
                bool fresh=transitions.Update(k,isDown);
                bool altDown=transitions.IsDown(0x12) || transitions.IsDown(0xA4) || transitions.IsDown(0xA5)
                    || (Native.GetAsyncKeyState(0x12)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA4)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
                if(Native.CopyInProgress && isDown && (k==0x12 || k==0xA4 || k==0xA5 || k==0x09 || k==0x5B || k==0x5C)) {
                    Native.EmergencyReleaseModifier();
                }
                if(k==0x09 && altDown) {
                    Native.LastAltTabTime=DateTime.UtcNow;
                }
                if(Native.GameFocused()) {
                    LastUserInputTime=DateTime.UtcNow;
                    if(fresh && isDown) {
                        LastFreshKeyDownTime=DateTime.UtcNow;
                        if(chatState!=null) {
                            int openKey = getChatOpenKey != null ? getChatOpenKey() : 0x54;
                            chatState.Key(k, openKey);
                        }
                    }
                }
                // Dispatch only keys that can affect the application. The hook still tracks
                // every physical key so modifier/chat state remains accurate, but forwarding
                // all typed keys creates a BeginInvoke storm and makes focus transitions prone
                // to losing the matching key-up event.
                if(fresh && isDown && !(k==0x09 && altDown) && (shouldDispatch==null || shouldDispatch(k))) action(k);
            }
            return CallNextHookEx(hook,code,message,data);
        }
        public void Dispose() { if(hook!=IntPtr.Zero) { UnhookWindowsHookEx(hook); hook=IntPtr.Zero; } }
    }
    sealed class OverlayWindow:Form {
        [StructLayout(LayoutKind.Sequential)] struct XY { public int X,Y; public XY(int x,int y) { X=x;Y=y; } }
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend { public byte Op,Flags,Alpha,Format; }
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dc,ref XY dest,ref XY size,IntPtr source,ref XY origin,uint key,ref Blend blend,uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        bool through=true;
        bool fullMapMode;
        public bool ChangingMapMode { get; private set; }
        Size savedSize;
        Point savedLocation;
        public Rectangle MinimapBounds { get { return fullMapMode ? new Rectangle(savedLocation,savedSize) : Bounds; } }
        public void SetMinimapSize(Size size) {
            size=new Size(Math.Max(240,Math.Min(800,size.Width)),Math.Max(240,Math.Min(800,size.Height)));
            if(fullMapMode) savedSize=size; else Size=size;
        }
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem fullMapWaypointMenu;
        public Action<int, Point> OnFullMapWheel;
        public Action<Point, MouseButtons> OnFullMapMouseDown;
        public Action<Point, MouseButtons> OnFullMapMouseMove;
        public Action<Point, MouseButtons> OnFullMapMouseUp;
        public Action<Point> OnFullMapAddWaypoint;
        public Action OnRouteColorRequested;
        readonly ToolStripMenuItem fullMapRouteColorMenu;
        public Action OnPlayerColorRequested;
        readonly ToolStripMenuItem fullMapPlayerColorMenu;
        public Func<bool> CanAddFullMapWaypoint;
        public Func<bool> CanShowFullMapContextMenu;
        Point fullMapContextPoint;
        Point fullMapContextScreenPoint;

        public bool FullMapMode {
            get { return fullMapMode; }
            set {
                if(fullMapMode==value) return;
                ChangingMapMode=true;
                try {
                fullMapMode=value;
                if(value) {
                    savedSize=Size;
                    savedLocation=Location;
                    Screen scr=Screen.FromControl(this);
                    try {
                        IntPtr fg=Native.GetForegroundWindow();
                        if(fg!=IntPtr.Zero && Native.IsGameWindow(fg)) scr=Screen.FromHandle(fg);
                    } catch {}
                    Rectangle area=scr.Bounds;
                    int mapDim=area.Height;
                    int mapX=area.Left+(area.Width-mapDim)/2;
                    int mapY=area.Top;
                    int rightSpace=area.Right-(mapX+mapDim);
                    int sidebarWidth=Math.Min(260,Math.Max(0,rightSpace));
                    int totalW=mapDim+sidebarWidth;
                    MinimumSize=Size.Empty;
                    MaximumSize=Size.Empty;
                    SetWindowPos(Handle,IntPtr.Zero,mapX,mapY,totalW,mapDim,0x0014);
                    Size=new Size(totalW,mapDim);
                    Location=new Point(mapX,mapY);
                    int style=GetWindowLong(Handle,-20);
                    style&=~0x20;
                    SetWindowLong(Handle,-20,style);
                } else {
                    MinimumSize=new Size(240,240);
                    MaximumSize=new Size(800,800);
                    if(savedSize.Width>0 && savedSize.Height>0) {
                        SetWindowPos(Handle,IntPtr.Zero,savedLocation.X,savedLocation.Y,savedSize.Width,savedSize.Height,0x0014);
                        Size=savedSize;
                        Location=savedLocation;
                    }
                    int style=GetWindowLong(Handle,-20);
                    style|=0x20;
                    SetWindowLong(Handle,-20,style);
                }
                if(fullMapWaypointMenu!=null) fullMapWaypointMenu.Visible=value;
                if(fullMapRouteColorMenu!=null) fullMapRouteColorMenu.Visible=value;
                if(fullMapPlayerColorMenu!=null) fullMapPlayerColorMenu.Visible=value;
                } finally { ChangingMapMode=false; }
            }
        }
        public OverlayWindow(Action settings,Action exit,Action<float> zoom,Action search) {
            Text="SCUM Minimap Overlay"; FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false; TopMost=true; Size=new Size(360,360);
            MinimumSize=new Size(240,240); MaximumSize=new Size(800,800);
            StartPosition=FormStartPosition.Manual; Location=new Point(Screen.PrimaryScreen.WorkingArea.Right-390,70);
            menu=new ContextMenuStrip();
            fullMapWaypointMenu=new ToolStripMenuItem(Localization.Get("AddCustomWaypointHere"),null,(s,e)=> {
                if(!fullMapMode || OnFullMapAddWaypoint==null) return;
                Point mapPoint=fullMapContextPoint;
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                ReleaseCapture();
                // Let the native context menu finish closing before opening the modal
                // waypoint prompt; otherwise the menu can consume the first click/focus.
                try { BeginInvoke((Action)(()=>OnFullMapAddWaypoint(mapPoint))); }
                catch(InvalidOperationException) { }
            });
            fullMapWaypointMenu.Visible=false;
            menu.Items.Add(fullMapWaypointMenu);
            fullMapRouteColorMenu=new ToolStripMenuItem(Localization.Get("RouteGuidanceColor"),null,(s,e)=> {
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                try { BeginInvoke((Action)(()=> { if(OnRouteColorRequested!=null) OnRouteColorRequested(); })); }
                catch(InvalidOperationException) {}
            });
            fullMapRouteColorMenu.Visible=false;
            menu.Items.Add(fullMapRouteColorMenu);
            fullMapPlayerColorMenu=new ToolStripMenuItem(Localization.Get("PlayerMarkerColor"),null,(s,e)=> {
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                try { BeginInvoke((Action)(()=> { if(OnPlayerColorRequested!=null) OnPlayerColorRequested(); })); }
                catch(InvalidOperationException) {}
            });
            fullMapPlayerColorMenu.Visible=false;
            menu.Items.Add(fullMapPlayerColorMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.Get("SearchPlaceOrGrid"),null,(s,e)=>search());
            menu.Items.Add(Localization.Get("Settings"),null,(s,e)=>settings());
            menu.Items.Add(Localization.Get("TrayShowHide"),null,(s,e)=>Hide());
            menu.Items.Add(Localization.Get("TrayExit"),null,(s,e)=>exit());
            menu.Opening+=(s,e)=> {
                if(fullMapMode && CanAddFullMapWaypoint!=null) fullMapWaypointMenu.Enabled=CanAddFullMapWaypoint();
                else fullMapWaypointMenu.Enabled=true;
            };
            OverlayTheme.Menu(menu);
            Action languageChanged=()=> {
                try { if(!IsDisposed) { fullMapWaypointMenu.Text=Localization.Get("AddCustomWaypointHere"); fullMapRouteColorMenu.Text=Localization.Get("RouteGuidanceColor"); fullMapPlayerColorMenu.Text=Localization.Get("PlayerMarkerColor"); } } catch {}
            };
            Localization.LanguageChanged+=languageChanged;
            Disposed+=(s,e)=>Localization.LanguageChanged-=languageChanged;
            MouseHover+=(s,e)=> { if(!through && !Native.GameFocused() && !menu.Visible) menu.Show(this,new Point(ClientSize.Width-16,24)); };
            MouseDown+=(s,e)=> {
                if(fullMapMode) {
                    if(e.Button==MouseButtons.Right) {
                        fullMapContextPoint=e.Location;
                        fullMapContextScreenPoint=PointToScreen(e.Location);
                        if(OnFullMapMouseDown!=null) OnFullMapMouseDown(e.Location, e.Button);
                        return;
                    }
                    if(OnFullMapMouseDown!=null) OnFullMapMouseDown(e.Location, e.Button);
                } else if(!through && e.Button==MouseButtons.Right) {
                    menu.Show(this,e.Location);
                } else if(!through && e.Button==MouseButtons.Left) {
                    ReleaseCapture(); SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);
                }
            };
            MouseMove+=(s,e)=> {
                if(fullMapMode) {
                    if(OnFullMapMouseMove!=null) OnFullMapMouseMove(e.Location, e.Button);
                }
            };
            MouseUp+=(s,e)=> {
                if(fullMapMode) {
                    if(e.Button==MouseButtons.Right) {
                        if(!menu.Visible && (CanShowFullMapContextMenu==null || CanShowFullMapContextMenu()))
                            menu.Show(fullMapContextScreenPoint);
                        return;
                    }
                    if(OnFullMapMouseUp!=null) OnFullMapMouseUp(e.Location, e.Button);
                }
            };
            MouseWheel+=(s,e)=> {
                if(fullMapMode) {
                    if(OnFullMapWheel!=null) OnFullMapWheel(e.Delta, PointToClient(Cursor.Position));
                } else if(!through) {
                    zoom(e.Delta>0?1.5f:1/1.5f);
                }
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ExStyle|=0x80000|0x8000000|0x80; if(through)p.ExStyle|=0x20; return p; } }
        public void SetGameFocus(bool focused) {
            if(fullMapMode) return;
            if(through==focused) return;
            through=focused;
            int style=GetWindowLong(Handle,-20);
            style|=0x8000000;
            style=focused?style|0x20:style&~0x20;
            if(focused && menu!=null && menu.Visible) menu.Close();
            SetWindowLong(Handle,-20,style);
            SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84) {
                // The full map intentionally owns right-clicks for its context menu.
                // Outside full-map mode, leave the right button transparent so SCUM's
                // ADS hold remains gameplay-controlled.
                if(fullMapMode) { m.Result=new IntPtr(1); return; }
                if(Native.IsRightMouseDown()) { m.Result=new IntPtr(-1); return; }
                if(Native.GameFocused()) { m.Result=new IntPtr(-1); return; }
                long packed=m.LParam.ToInt64();
                Point point=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));
                bool left=point.X<16,right=point.X>=ClientSize.Width-16,top=point.Y<16,bottom=point.Y>=ClientSize.Height-16;
                int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
                m.Result=new IntPtr(hit); return;
            }
            if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        static int fadeWidth,fadeHeight;
        static byte[] fadeMask;
        static byte[] fadeBuffer;
        static readonly byte[] fadeTable=CreateFadeTable();
        static byte[] CreateFadeTable() {
            byte[] table=new byte[65536];
            for(int a=0;a<256;a++)for(int c=0;c<256;c++)table[(a<<8)|c]=(byte)((a*c+127)/255);
            return table;
        }
        static bool cachedFade=true;
        static int cachedOpacity=94;
        static int cachedMapLeft=-1,cachedMapTop=-1,cachedMapRight=-1,cachedMapBottom=-1;
        static string cachedShape="Square";
        // Rebuild only when dimensions, shape, or appearance options change.
        public static void Fade(Bitmap bitmap,bool fade=true,int opacity=94,int mapLeft=-1,int mapTop=-1,int mapRight=-1,int mapBottom=-1,string shape="Square") {
            // A fully opaque, non-faded bitmap is already ready for UpdateLayeredWindow.
            // Avoid LockBits and a full per-pixel marshal/copy pass in full-map mode.
            if(!fade && opacity>=100) return;
            if(fadeMask==null || fadeWidth!=bitmap.Width || fadeHeight!=bitmap.Height || cachedFade!=fade || cachedOpacity!=opacity
                || cachedMapLeft!=mapLeft || cachedMapTop!=mapTop || cachedMapRight!=mapRight || cachedMapBottom!=mapBottom || cachedShape!=shape) {
                cachedFade=fade; cachedOpacity=opacity;
                cachedMapLeft=mapLeft; cachedMapTop=mapTop; cachedMapRight=mapRight; cachedMapBottom=mapBottom; cachedShape=shape;
                fadeWidth=bitmap.Width; fadeHeight=bitmap.Height;
                fadeMask=new byte[fadeWidth*fadeHeight];
                bool hasMapBounds=(mapLeft!=-1 && mapTop!=-1 && mapRight!=-1 && mapBottom!=-1);
                double cx=fadeWidth/2.0, cy=fadeHeight/2.0;
                double maxRadius=Math.Min(fadeWidth,fadeHeight)/2.0;
                if(shape=="Circle" && hasMapBounds) {
                    // Fit circle inside the actual mapBounds area (excluding status bar)
                    double mapAreaH = Math.Min(fadeHeight - 34.0, (double)fadeHeight);
                    maxRadius = Math.Min(fadeWidth, mapAreaH) / 2.0;
                    cx = fadeWidth / 2.0;
                    cy = maxRadius + 2.0; // Place circle at top, leaving bottom 30px clear for info pill
                }
                for(int y=0;y<fadeHeight;y++)for(int x=0;x<fadeWidth;x++) {
                    double t=1.0;
                    if(shape=="Circle") {
                        double dx=x-cx, dy=y-cy;
                        double dist=Math.Sqrt(dx*dx+dy*dy);
                        double diff=maxRadius-dist;
                        if(diff<=0) t=0;
                        else if(fade && diff<24) t=diff/24.0;
                        else t=1.0;
                    } else {
                        double edge=Math.Min(Math.Min(x,fadeWidth-1-x),Math.Min(y,fadeHeight-1-y));
                        t=fade?Math.Min(1,edge/28):1;
                    }
                    if(hasMapBounds && t>0) {
                        double mapEdge=Math.Min(Math.Min(x-mapLeft,mapRight-x),Math.Min(y-mapTop,mapBottom-y));
                        if(mapEdge<=0) t=0;
                        else if(fade && mapEdge<28) t=Math.Min(t,mapEdge/28);
                    }
                    fadeMask[y*fadeWidth+x]=(byte)Math.Round(t*t*(3-2*t)*opacity/100*255);
                }
            }
            BitmapData data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
            try {
                int stride=data.Stride;
                int h=bitmap.Height;
                int w=bitmap.Width;
                int totalBytes=stride*h;
                if(fadeBuffer==null || fadeBuffer.Length<totalBytes) fadeBuffer=new byte[totalBytes];
                Marshal.Copy(data.Scan0,fadeBuffer,0,totalBytes);
                for(int y=0;y<h;y++) {
                    int rowOffset=y*stride;
                    int maskOffset=y*fadeWidth;
                    for(int x=0;x<w;x++) {
                        int a=fadeMask[maskOffset+x]<<8;
                        int i=rowOffset+x*4;
                        fadeBuffer[i]=fadeTable[a|fadeBuffer[i]];
                        fadeBuffer[i+1]=fadeTable[a|fadeBuffer[i+1]];
                        fadeBuffer[i+2]=fadeTable[a|fadeBuffer[i+2]];
                        fadeBuffer[i+3]=fadeTable[a|fadeBuffer[i+3]];
                    }
                }
                Marshal.Copy(fadeBuffer,0,data.Scan0,totalBytes);
            } finally { bitmap.UnlockBits(data); }
        }
        public void Present(Bitmap bitmap) {
            if(bitmap==null || IsDisposed || !IsHandleCreated) return;
            IntPtr screen=GetDC(IntPtr.Zero),memory=IntPtr.Zero,hbitmap=IntPtr.Zero,old=IntPtr.Zero;
            try {
                memory=CreateCompatibleDC(screen);
                if(memory==IntPtr.Zero) return;
                hbitmap=bitmap.GetHbitmap(Color.FromArgb(0));
                if(hbitmap==IntPtr.Zero) return;
                old=SelectObject(memory,hbitmap);
                XY dest=new XY(Left,Top),size=new XY(bitmap.Width,bitmap.Height),origin=new XY(0,0);
                Blend blend=new Blend { Alpha=255,Format=1 };
                UpdateLayeredWindow(Handle,screen,ref dest,ref size,memory,ref origin,0,ref blend,2);
            } catch {
                // Suppress transient GDI / window update failures during display state switches
            } finally {
                if(old!=IntPtr.Zero) { try { SelectObject(memory,old); } catch {} }
                if(hbitmap!=IntPtr.Zero) { try { DeleteObject(hbitmap); } catch {} }
                if(memory!=IntPtr.Zero) { try { DeleteDC(memory); } catch {} }
                if(screen!=IntPtr.Zero) { try { ReleaseDC(IntPtr.Zero,screen); } catch {} }
            }
        }
    }

    public class TacticalSectionHeader : Control {
        bool _expanded = true;
        bool _hover = false;
        string _title = "";

        public event EventHandler ToggleRequested;

        Font _headerFont;

        public TacticalSectionHeader(string title, bool expanded) {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _title = title ?? "";
            _expanded = expanded;
            Height = 36;
            Width = 390;
            Cursor = Cursors.Hand;
            BackColor = OverlayTheme.Surface;
            ForeColor = OverlayTheme.Ink;
            _headerFont = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            Font = _headerFont;
            Margin = new Padding(0, 4, 0, 3);
        }

        protected override void Dispose(bool disposing) {
            if(disposing) {
                if(_headerFont != null) {
                    _headerFont.Dispose();
                    _headerFont = null;
                }
            }
            base.Dispose(disposing);
        }

        public bool Expanded {
            get { return _expanded; }
            set { _expanded = value; Invalidate(); }
        }

        public string Title {
            get { return _title; }
            set { _title = value ?? ""; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            _expanded = !_expanded;
            Invalidate();
            if(ToggleRequested != null) {
                try { ToggleRequested(this, EventArgs.Empty); }
                catch {}
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = _hover ? Color.FromArgb(34, 32, 28) : OverlayTheme.Surface;
            using(Brush b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);

            using(Pen p = new Pen(OverlayTheme.Border, 1f)) {
                g.DrawLine(p, 0, 0, Width, 0);
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }

            using(Brush b = new SolidBrush(_expanded ? OverlayTheme.Accent : Color.FromArgb(60, 58, 54))) {
                g.FillRectangle(b, 0, 0, 4, Height);
            }

            PointF[] arrow;
            if (_expanded) {
                arrow = new PointF[] {
                    new PointF(18f, 15f),
                    new PointF(28f, 15f),
                    new PointF(23f, 21f)
                };
            } else {
                arrow = new PointF[] {
                    new PointF(19f, 13f),
                    new PointF(26f, 18f),
                    new PointF(19f, 23f)
                };
            }
            using(Brush ab = new SolidBrush(_expanded ? OverlayTheme.Accent : OverlayTheme.InkMuted)) {
                g.FillPolygon(ab, arrow);
            }

            Rectangle textRect = new Rectangle(36, 0, Width - 42, Height);
            Color textCol = _expanded ? OverlayTheme.Accent : (_hover ? OverlayTheme.Ink : OverlayTheme.InkMuted);
            TextRenderer.DrawText(g, _title.ToUpperInvariant(), Font, textRect, textCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class TacticalComboBox : ComboBox {
        public TacticalComboBox() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = OverlayTheme.Surface;
            ForeColor = OverlayTheme.Ink;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 22;
        }

        protected override void OnDrawItem(DrawItemEventArgs e) {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (Brush b = new SolidBrush(sel ? Color.FromArgb(50, 42, 25) : OverlayTheme.Surface)) {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            string text = Items[e.Index].ToString();
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, sel ? OverlayTheme.Accent : OverlayTheme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Brush b = new SolidBrush(OverlayTheme.Surface)) {
                g.FillRectangle(b, ClientRectangle);
            }
            using (Pen p = new Pen(OverlayTheme.Border, 1f)) {
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }

            string text = SelectedItem != null ? SelectedItem.ToString() : Text;
            Rectangle textRect = new Rectangle(8, 0, Width - 28, Height);
            TextRenderer.DrawText(g, text, Font, textRect, OverlayTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            PointF[] arrow = new PointF[] {
                new PointF(Width - 16, Height / 2 - 2),
                new PointF(Width - 8, Height / 2 - 2),
                new PointF(Width - 12, Height / 2 + 3)
            };
            using (Brush ab = new SolidBrush(OverlayTheme.Accent)) {
                g.FillPolygon(ab, arrow);
            }
        }
    }

    public class TacticalSlider : Control {
        int _min = 0;
        int _max = 100;
        int _value = 0;
        bool _dragging = false;

        public event EventHandler ValueChanged;

        public TacticalSlider() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Height = 24;
            Width = 180;
            Cursor = Cursors.Hand;
            BackColor = OverlayTheme.Background;
        }

        public int Minimum {
            get { return _min; }
            set { _min = value; Invalidate(); }
        }

        public int Maximum {
            get { return _max; }
            set { _max = Math.Max(_min + 1, value); Invalidate(); }
        }

        public int Value {
            get { return _value; }
            set {
                int v = Math.Max(_min, Math.Min(_max, value));
                if (_value != v) {
                    _value = v;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        void UpdateFromX(int x) {
            int trackPad = 8;
            int trackW = Width - trackPad * 2;
            if (trackW <= 0) return;
            float ratio = Math.Max(0f, Math.Min(1f, (float)(x - trackPad) / trackW));
            Value = (int)Math.Round(_min + ratio * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) {
                _dragging = true;
                Capture = true;
                UpdateFromX(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (_dragging) UpdateFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackPad = 8;
            int trackH = 6;
            int trackY = (Height - trackH) / 2;
            int trackW = Width - trackPad * 2;

            using (GraphicsPath trackPath = new GraphicsPath()) {
                trackPath.AddArc(trackPad, trackY, trackH, trackH, 90, 180);
                trackPath.AddArc(trackPad + trackW - trackH, trackY, trackH, trackH, 270, 180);
                trackPath.CloseFigure();
                using (Brush b = new SolidBrush(OverlayTheme.Surface)) g.FillPath(b, trackPath);
                using (Pen p = new Pen(OverlayTheme.Border, 1f)) g.DrawPath(p, trackPath);
            }

            float frac = _max > _min ? (float)(_value - _min) / (_max - _min) : 0f;
            int fillW = Math.Max(trackH, (int)Math.Round(trackW * frac));
            using (GraphicsPath fillPath = new GraphicsPath()) {
                fillPath.AddArc(trackPad, trackY, trackH, trackH, 90, 180);
                fillPath.AddArc(trackPad + fillW - trackH, trackY, trackH, trackH, 270, 180);
                fillPath.CloseFigure();
                using (Brush fb = new SolidBrush(OverlayTheme.Accent)) g.FillPath(fb, fillPath);
            }

            float knobX = trackPad + trackW * frac;
            float knobY = Height / 2f;
            float knobR = 6.0f;
            using (Brush kb = new SolidBrush(OverlayTheme.Ink)) g.FillEllipse(kb, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);
            using (Pen kp = new Pen(OverlayTheme.Accent, 2.0f)) g.DrawEllipse(kp, knobX - knobR, knobY - knobR, knobR * 2, knobR * 2);
        }
    }

    public class TacticalCheckBox : Control {
        bool _checked = false;
        bool _hover = false;
        string _text = "";

        public event EventHandler CheckedChanged;

        public TacticalCheckBox() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Height = 26;
            Width = 365;
            Cursor = Cursors.Hand;
            BackColor = OverlayTheme.Background;
            ForeColor = OverlayTheme.Ink;
            Font = new Font("Segoe UI", 9f);
        }

        public bool Checked {
            get { return _checked; }
            set {
                if (_checked != value) {
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public override string Text {
            get { return _text; }
            set { _text = value ?? ""; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int boxSize = 16;
            int boxX = 3;
            int boxY = (Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            if (_checked) {
                using (Brush b = new SolidBrush(OverlayTheme.Accent)) g.FillRectangle(b, boxRect);
                using (Pen p = new Pen(_hover ? OverlayTheme.AccentHover : Color.FromArgb(255, 185, 60), 1.2f)) g.DrawRectangle(p, boxRect);

                using (Pen checkPen = new Pen(OverlayTheme.Background, 2.2f)) {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLines(checkPen, new PointF[] {
                        new PointF(boxX + 3.5f, boxY + 8f),
                        new PointF(boxX + 6.5f, boxY + 11.5f),
                        new PointF(boxX + 12f, boxY + 4.5f)
                    });
                }
            } else {
                using (Brush b = new SolidBrush(OverlayTheme.Surface)) g.FillRectangle(b, boxRect);
                using (Pen p = new Pen(_hover ? OverlayTheme.AccentHover : OverlayTheme.Border, 1.2f)) g.DrawRectangle(p, boxRect);
            }

            int textX = boxX + boxSize + 9;
            int textW = Width - textX - 4;
            Rectangle textRect = new Rectangle(textX, 1, textW, Height - 2);
            Color txtCol = _checked ? OverlayTheme.Ink : (_hover ? Color.FromArgb(225, 220, 215) : OverlayTheme.InkMuted);
            TextRenderer.DrawText(g, _text, Font, textRect, txtCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
