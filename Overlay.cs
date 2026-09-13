using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Shared visual language for interactive HUD panels and popup menus.
    static class OverlayTheme {
        internal static readonly Color Background=Color.FromArgb(18,24,30), Surface=Color.FromArgb(28,37,46), Accent=Color.FromArgb(100,220,255), Ink=Color.FromArgb(225,234,240);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        internal static void Style(Control root) {
            root.ForeColor=Ink;
            Button button=root as Button;
            if(button!=null) {
                button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderColor=Color.FromArgb(49,66,78);
                button.FlatAppearance.MouseOverBackColor=Color.FromArgb(42,70,84);
                button.FlatAppearance.MouseDownBackColor=Color.FromArgb(51,91,106);
                button.BackColor=Surface; button.UseVisualStyleBackColor=false; button.Cursor=Cursors.Hand;
                button.Height=Math.Max(28,button.Height);
            } else if(root is TextBox || root is ListBox || root is NumericUpDown || root is ComboBox) root.BackColor=Surface;
            else root.BackColor=Background;
            CheckBox check=root as CheckBox;
            if(check!=null) { check.FlatStyle=FlatStyle.Flat; check.Cursor=Cursors.Hand; }
            ComboBox combo=root as ComboBox;
            if(combo!=null) combo.FlatStyle=FlatStyle.Flat;
            foreach(Control child in root.Controls) Style(child);
        }
        internal static void Frame(Form form,string title,Action dismiss) {
            form.FormBorderStyle=FormBorderStyle.None; form.TopMost=true; form.ShowInTaskbar=true;
            form.Padding=new Padding(1); form.BackColor=Background; form.Font=new Font("Segoe UI",9);
            Panel header=new Panel { Dock=DockStyle.Top,Height=52,Padding=new Padding(14,8,8,8) };
            Label caption=new Label { Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold) };
            Button close=new Button { Text=Localization.Get("Close"),Dock=DockStyle.Right,Width=62,TabStop=true };
            close.Click+=(s,e)=>dismiss();
            caption.MouseDown+=(s,e)=> { if(e.Button==MouseButtons.Left) { ReleaseCapture(); SendMessage(form.Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
            header.Controls.Add(caption); header.Controls.Add(close); form.Controls.Add(header);
            form.TextChanged+=(s,e)=>caption.Text=form.Text;
            Localization.LanguageChanged+=()=> { try { if(!form.IsDisposed) close.Text=Localization.Get("Close"); } catch {} };
            Style(form); caption.ForeColor=Accent;
            form.Paint+=(s,e)=> { using(Pen pen=new Pen(Color.FromArgb(57,82,96))) e.Graphics.DrawRectangle(pen,0,0,form.ClientSize.Width-1,form.ClientSize.Height-1); };
            form.KeyPreview=true;
            form.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Escape) { dismiss(); e.Handled=true; e.SuppressKeyPress=true; } };
        }
        internal static void Anchor(Form panel,Form map) {
            Rectangle area=Screen.FromControl(map).WorkingArea;
            int x=map.Left-panel.Width-12;
            if(x<area.Left) x=map.Right+12;
            panel.Location=new Point(Math.Max(area.Left,Math.Min(x,area.Right-panel.Width)),Math.Max(area.Top,Math.Min(map.Top,area.Bottom-panel.Height)));
        }
        internal static Label Section(FlowLayoutPanel bar,string title) {
            Label marker=new Label { Text=title,Tag="section",AutoSize=true }; bar.Controls.Add(marker); return marker;
        }
        internal static void Sections(FlowLayoutPanel bar) {
            Control[] original=new Control[bar.Controls.Count]; bar.Controls.CopyTo(original,0); bar.Controls.Clear();
            FlowLayoutPanel page=null;
            foreach(Control item in original) {
                if((string)item.Tag=="section") {
                    string sectionTitle=item.Text;
                    Button tab=new Button { Text="▼  "+sectionTitle,Width=385,Height=34,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,4,0,2),ForeColor=Accent };
                    page=new FlowLayoutPanel { Width=385,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(4,0,0,8),Visible=true };
                    FlowLayoutPanel target=page;
                    Button targetTab=tab;
                    targetTab.Click+=(s,e)=> {
                        bar.SuspendLayout();
                        target.Visible=!target.Visible;
                        targetTab.Text=(target.Visible?"▼  ":"►  ")+sectionTitle;
                        targetTab.ForeColor=target.Visible?Accent:Ink;
                        bar.ResumeLayout(true);
                    };
                    bar.Controls.Add(tab); bar.Controls.Add(page); item.Dispose();
                } else if(page!=null) page.Controls.Add(item);
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
                if(e.Item.Selected) using(Brush b=new SolidBrush(Surface)) e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor=e.Item.Selected?Accent:Ink; base.OnRenderItemText(e); }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using(Pen p=new Pen(Color.FromArgb(57,82,96))) e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1); }
        }
    }
    // FlowLayoutPanel that prevents automatic snapping/jumping to top when child controls are focused or clicked.
    sealed class SettingsPanel:FlowLayoutPanel {
        public SettingsPanel() {
            DoubleBuffered=true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
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
    internal sealed class MapMotion {
        internal PointF Point;
        internal double Yaw;
        PointF start,target;
        double startYaw,targetYaw,began,duration,lastSample;
        bool initialized;
        internal static double HeadingDelta(double from,double to) { return ((to-from+540)%360+360)%360-180; }
        internal void Sample(PointF point,double yaw,double now,bool snap) {
            Advance(now);
            double interval=now-lastSample;
            if(!initialized || snap || interval>3 || DestinationSearch.Metres(Point,point)>150) {
                Point=point; Yaw=yaw;
            }
            start=Point; target=point; startYaw=Yaw; targetYaw=Yaw+HeadingDelta(Yaw,yaw);
            began=now; duration=Math.Max(.1,Math.Min(.5,initialized?interval:.25));
            lastSample=now; initialized=true;
        }
        internal bool Advance(double now) {
            if(!initialized)return false;
            double t=Math.Max(0,Math.Min(1,(now-began)/duration));
            PointF next=new PointF((float)(start.X+(target.X-start.X)*t),(float)(start.Y+(target.Y-start.Y)*t));
            double heading=startYaw+(targetYaw-startYaw)*t;
            bool changed=next!=Point || Math.Abs(heading-Yaw)>.0001;
            Point=next; Yaw=heading; return changed;
        }
        internal static void SelfTest() {
            MapMotion m=new MapMotion(); m.Sample(new PointF(.5f,.5f),359,0,false);
            m.Sample(new PointF(.501f,.5f),1,.25,false); m.Advance(.375);
            if(Math.Abs(m.Point.X-.5005)>.000001 || Math.Abs(m.Yaw-360)>.001)throw new Exception("Motion midpoint or heading wrap failed.");
            m.Advance(2); if(Math.Abs(m.Point.X-.501)>.000001 || m.Advance(3))throw new Exception("Motion overshoot or idle redraw.");
            m.Sample(new PointF(.9f,.9f),45,3,false); if(m.Point.X!=.9f)throw new Exception("Teleport must snap.");
        }
    }
    public sealed class ChatState {
        public bool Paused { get; private set; }
        public void Key(int key) {
            // T (0x54), Slash / (0xBF), Keypad / (0x6F) open chat/command box
            if(key==0x54 || key==0xBF || key==0x6F) Paused=true;
            else if(key==0x0D || key==0x1B) Paused=false;
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
        readonly bool[] held=new bool[256];
        IntPtr hook;
        public static DateTime LastUserInputTime = DateTime.MinValue;
        public static DateTime LastFreshKeyDownTime = DateTime.MinValue;
        internal static bool UserActiveRecently(int windowMs = 90) {
            return (DateTime.UtcNow - LastFreshKeyDownTime).TotalMilliseconds < windowMs;
        }
        public bool Available { get { return hook!=IntPtr.Zero; } }
        public GameKeys(Action<int> action) {
            this.action=action; callback=OnKey;
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        }
        IntPtr OnKey(int code,IntPtr message,IntPtr data) {
            if(code>=0) {
                KeyboardData key=(KeyboardData)Marshal.PtrToStructure(data,typeof(KeyboardData));
                // Synthetic shortcuts during automatic coordinate copy must not alter physical held-key or chat state.
                if(Native.CopyInProgress && (key.flags & 0x10)!=0) return CallNextHookEx(hook,code,message,data);
                int k=(int)key.key;
                bool isDown=message.ToInt32()==0x100 || message.ToInt32()==0x104;
                bool fresh=isDown && (k>=0 && k<256 ? (!held[k] || (Native.GetAsyncKeyState(k)&0x8000)==0) : true);
                if(k>=0 && k<256) held[k]=isDown;
                if(Native.GameFocused()) {
                    LastUserInputTime=DateTime.UtcNow;
                    if(fresh && isDown) LastFreshKeyDownTime=DateTime.UtcNow;
                }
                if(k==0x54 || k==0x0D || k==0x1B || k==0xBF || k==0x6F || k==0x21 || k==0x22 || k==0x23 || k==0x24 || k==0x2D || k==0x2E || k==0x4D) {
                    if(fresh && isDown) action(k);
                }
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
        Size savedSize;
        Point savedLocation;
        public bool FullMapMode {
            get { return fullMapMode; }
            set {
                if(fullMapMode==value) return;
                fullMapMode=value;
                if(value) {
                    savedSize=Size;
                    savedLocation=Location;
                    Rectangle area=Screen.FromControl(this).Bounds;
                    MaximumSize=new Size(area.Width,area.Height);
                    Size=new Size(area.Width,area.Height);
                    Location=area.Location;
                } else {
                    MaximumSize=new Size(800,800);
                    Size=savedSize;
                    Location=savedLocation;
                }
            }
        }
        public OverlayWindow(Action settings,Action exit,Action<float> zoom,Action search) {
            Text="SCUM Minimap Overlay"; FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false; TopMost=true; Size=new Size(360,360);
            MinimumSize=new Size(240,240); MaximumSize=new Size(800,800);
            StartPosition=FormStartPosition.Manual; Location=new Point(Screen.PrimaryScreen.WorkingArea.Right-390,70);
            ContextMenuStrip menu=new ContextMenuStrip();
            menu.Items.Add("Search place or grid...",null,(s,e)=>search());
            menu.Items.Add("Settings",null,(s,e)=>settings());
            menu.Items.Add("Hide map",null,(s,e)=>Hide());
            menu.Items.Add("Exit minimap",null,(s,e)=>exit());
            OverlayTheme.Menu(menu); ContextMenuStrip=menu;
            MouseHover+=(s,e)=> { if(!through && !Native.GameFocused() && Form.ActiveForm!=null && !menu.Visible) menu.Show(this,new Point(ClientSize.Width-16,24)); };
            MouseDown+=(s,e)=> { if(!through && e.Button==MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
            MouseWheel+=(s,e)=> { if(!through) zoom(e.Delta>0?1.5f:1/1.5f); };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ExStyle|=0x80000|0x8000000|0x80; if(through)p.ExStyle|=0x20; return p; } }
        public void SetGameFocus(bool focused) {
            if(through==focused)return;
            through=focused;
            int style=GetWindowLong(Handle,-20);
            style|=0x8000000;
            style=focused?style|0x20:style&~0x20;
            if(focused && ContextMenuStrip.Visible) ContextMenuStrip.Close();
            SetWindowLong(Handle,-20,style);
            SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84 && Native.GameFocused()) { m.Result=new IntPtr(-1); return; }
            if(m.Msg==0x84) {
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
        static byte[] fadeMask,fadeRow;
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
            if(fadeMask==null || fadeWidth!=bitmap.Width || fadeHeight!=bitmap.Height || cachedFade!=fade || cachedOpacity!=opacity
                || cachedMapLeft!=mapLeft || cachedMapTop!=mapTop || cachedMapRight!=mapRight || cachedMapBottom!=mapBottom || cachedShape!=shape) {
                cachedFade=fade; cachedOpacity=opacity;
                cachedMapLeft=mapLeft; cachedMapTop=mapTop; cachedMapRight=mapRight; cachedMapBottom=mapBottom; cachedShape=shape;
                fadeWidth=bitmap.Width; fadeHeight=bitmap.Height;
                fadeMask=new byte[fadeWidth*fadeHeight]; fadeRow=new byte[fadeWidth*4];
                bool hasMapBounds=(mapLeft!=-1 && mapTop!=-1 && mapRight!=-1 && mapBottom!=-1);
                double cx=fadeWidth/2.0, cy=fadeHeight/2.0;
                double maxRadius=Math.Min(fadeWidth,fadeHeight)/2.0;
                if(shape=="Circle" && hasMapBounds) {
                    // Fit circle inside the actual mapBounds area (excluding status bar)
                    int mbH = mapBottom - mapTop;
                    int mbW = mapRight - mapLeft;
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
                for(int y=0;y<bitmap.Height;y++) {
                    IntPtr address=IntPtr.Add(data.Scan0,y*data.Stride); Marshal.Copy(address,fadeRow,0,fadeRow.Length);
                    int offset=y*fadeWidth;
                    for(int x=0;x<bitmap.Width;x++) {
                        int a=fadeMask[offset+x]<<8,i=x*4;
                        fadeRow[i]=fadeTable[a|fadeRow[i]];
                        fadeRow[i+1]=fadeTable[a|fadeRow[i+1]];
                        fadeRow[i+2]=fadeTable[a|fadeRow[i+2]];
                        fadeRow[i+3]=fadeTable[a|fadeRow[i+3]];
                    }
                    Marshal.Copy(fadeRow,0,address,fadeRow.Length);
                }
            } finally { bitmap.UnlockBits(data); }
        }
        public void Present(Bitmap bitmap) {
            IntPtr screen=GetDC(IntPtr.Zero),memory=IntPtr.Zero,hbitmap=IntPtr.Zero,old=IntPtr.Zero;
            try {
                memory=CreateCompatibleDC(screen); hbitmap=bitmap.GetHbitmap(Color.FromArgb(0)); old=SelectObject(memory,hbitmap);
                XY dest=new XY(Left,Top),size=new XY(bitmap.Width,bitmap.Height),origin=new XY(0,0);
                Blend blend=new Blend { Alpha=255,Format=1 };
                if(!UpdateLayeredWindow(Handle,screen,ref dest,ref size,memory,ref origin,0,ref blend,2)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            } finally {
                if(old!=IntPtr.Zero)SelectObject(memory,old);
                if(hbitmap!=IntPtr.Zero)DeleteObject(hbitmap);
                if(memory!=IntPtr.Zero)DeleteDC(memory);
                if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);
            }
        }
    }
}
