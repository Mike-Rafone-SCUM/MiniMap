using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Wheel handlers only record intent. A single UI timer applies the combined
    // movement, so a fast wheel cannot schedule a render/rebuild for every notch.
    internal sealed class BatchedWheel:IDisposable {
        readonly Timer timer;
        readonly Action<int> apply;
        int pending;
        bool disposed;
        internal bool Waiting { get { return !disposed && timer.Enabled; } }
        internal BatchedWheel(Action<int> apply) {
            this.apply=apply;timer=new Timer { Interval=50 };
            timer.Tick+=(s,e)=>Flush();
        }
        internal void Add(int delta) {
            if(disposed)return;
            pending=(int)Math.Max(-120L*256,Math.Min(120L*256,(long)pending+delta));
            if(!timer.Enabled)timer.Start();
        }
        internal void Flush() {
            timer.Stop();if(disposed)return;
            int steps=pending/120;pending%=120;
            if(steps!=0)apply(steps);
        }
        internal void Cancel() { timer.Stop();pending=0; }
        public void Dispose() { if(disposed)return;disposed=true;timer.Dispose();pending=0; }
    }
    internal static class FullMapSidebarStyle {
        internal static readonly Color Background=Color.FromArgb(14,17,20);
        internal static readonly Color Border=Color.FromArgb(42,52,61);
        internal static readonly Color Divider=Color.FromArgb(60,60,75,90);
        internal static readonly Color ButtonSurface=Color.FromArgb(22,27,32);
        internal static readonly Color SelectedSurface=Color.FromArgb(45,56,68);
        internal static readonly Color Muted=Color.FromArgb(160,170,180);
        internal static readonly Color Secondary=Color.FromArgb(120,135,150);
        internal static readonly Color Ink=Color.WhiteSmoke;
        internal static readonly Color MiniInk=Color.FromArgb(180,190,200);
        internal static readonly Color SelectedInk=Color.FromArgb(255,174,51);
        internal static readonly Color Accent=Color.FromArgb(255,159,28);
        internal static readonly Color Arrow=Color.SkyBlue;
        internal static readonly Color SectionExpanded=Color.FromArgb(200,22,27,32);
        internal static readonly Color SectionCollapsed=Color.FromArgb(160,16,20,24);
        internal static readonly Color SectionBorder=Color.FromArgb(140,42,52,61);
        internal static readonly Color ScrollTrack=Color.FromArgb(40,255,255,255);
        internal static readonly Color ScrollThumb=Color.FromArgb(160,70,140,200);
        internal static readonly Color ScrollThumbDragging=Color.FromArgb(220,80,170,240);

        static GraphicsPath RoundedPath(Rectangle rect,int diameter) {
            var path=new GraphicsPath();
            int arc=Math.Min(diameter,Math.Min(rect.Width,rect.Height));
            if(arc<=0)return path;
            path.AddArc(rect.X,rect.Y,arc,arc,180,90);
            path.AddArc(rect.Right-arc,rect.Y,arc,arc,270,90);
            path.AddArc(rect.Right-arc,rect.Bottom-arc,arc,arc,0,90);
            path.AddArc(rect.X,rect.Bottom-arc,arc,arc,90,90);
            path.CloseFigure();return path;
        }
        internal static void DrawMiniButton(Graphics graphics,Rectangle rect,string text,bool selected) {
            using(var font=new Font("Segoe UI",7f,FontStyle.Bold))DrawMiniButton(graphics,rect,text,selected,font,true,false);
        }
        internal static void DrawMiniButton(Graphics graphics,Rectangle rect,string text,bool selected,Font font,bool enabled,bool compact=true) {
            if(rect.Width<=0 || rect.Height<=0)return;
            using(var path=RoundedPath(rect,6)) {
                using(var fill=new SolidBrush(selected && enabled?SelectedSurface:ButtonSurface))graphics.FillPath(fill,path);
                using(var border=new Pen(selected && enabled?Accent:Border,1f))graphics.DrawPath(border,path);
            }
            DrawText(graphics,rect,text,font,!enabled?Secondary:(selected?SelectedInk:MiniInk),compact);
        }
        internal static void DrawActionButton(Graphics graphics,Rectangle rect,string text,Color background) {
            using(var font=new Font("Segoe UI",7.5f,FontStyle.Bold))DrawActionButton(graphics,rect,text,background,font,true,false);
        }
        internal static void DrawActionButton(Graphics graphics,Rectangle rect,string text,Color background,Font font,bool enabled,bool compact=true) {
            if(rect.Width<=0 || rect.Height<=0)return;
            using(var path=RoundedPath(rect,7)) {
                using(var fill=new SolidBrush(background))graphics.FillPath(fill,path);
                using(var border=new Pen(OverlayTheme.Border,1f))graphics.DrawPath(border,path);
            }
            DrawText(graphics,rect,text,font,enabled?Ink:Secondary,compact);
        }
        static void DrawText(Graphics graphics,Rectangle rect,string text,Font font,Color ink,bool compact) {
            using(var brush=new SolidBrush(ink))
            using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,
                Trimming=compact?StringTrimming.EllipsisCharacter:StringTrimming.None,FormatFlags=compact?StringFormatFlags.NoWrap:0 })
                graphics.DrawString(text??"",font,brush,rect,format);
        }
    }

    internal sealed class SidebarButton:Button {
        readonly Font sidebarFont=new Font("Segoe UI",7.5f,FontStyle.Bold);
        bool selected,sectionHeader,expanded;
        internal bool Pill;
        internal bool Selected { get { return selected; } set { if(selected!=value) { selected=value;Invalidate(); } } }
        internal bool SectionHeader { get { return sectionHeader; } set { if(sectionHeader!=value) { sectionHeader=value;Invalidate(); } } }
        internal bool Expanded { get { return expanded; } set { if(expanded!=value) { expanded=value;Invalidate(); } } }
        internal SidebarButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;UseVisualStyleBackColor=false;BackColor=FullMapSidebarStyle.Background;
            ForeColor=FullMapSidebarStyle.Ink;Font=sidebarFont;Cursor=Cursors.Hand;AutoEllipsis=true;
        }
        protected override void Dispose(bool disposing) { if(disposing)sidebarFont.Dispose();base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            var rect=new Rectangle(0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
            if(sectionHeader) {
                using(var fill=new SolidBrush(expanded?FullMapSidebarStyle.SectionExpanded:FullMapSidebarStyle.SectionCollapsed))e.Graphics.FillRectangle(fill,rect);
                using(var border=new Pen(FullMapSidebarStyle.SectionBorder,1f))e.Graphics.DrawRectangle(border,rect);
                using(var arrowFont=new Font("Segoe UI",7f,FontStyle.Bold))
                using(var arrow=new SolidBrush(Enabled?FullMapSidebarStyle.Arrow:FullMapSidebarStyle.Secondary))
                    e.Graphics.DrawString(expanded?"\u25bc":"\u25ba",arrowFont,arrow,6,6);
                using(var textBrush=new SolidBrush(Enabled?FullMapSidebarStyle.Ink:FullMapSidebarStyle.Secondary))
                using(var format=new StringFormat { LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,
                    FormatFlags=StringFormatFlags.NoWrap })
                    e.Graphics.DrawString(Text,Font,textBrush,new Rectangle(22,0,Math.Max(0,Width-26),Height),format);
            } else if(Pill && rect.Width>rect.Height && rect.Height>0) {
                int diameter=rect.Height;
                using(var shape=new GraphicsPath()) {
                    shape.AddArc(rect.Left,rect.Top,diameter,diameter,90,180);
                    shape.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,180);shape.CloseFigure();
                    using(var fill=new SolidBrush(selected?FullMapSidebarStyle.SelectedSurface:FullMapSidebarStyle.ButtonSurface))e.Graphics.FillPath(fill,shape);
                    using(var border=new Pen(selected?OverlayTheme.Accent:FullMapSidebarStyle.Border))e.Graphics.DrawPath(border,shape);
                }
                TextRenderer.DrawText(e.Graphics,Text,Font,rect,Enabled?(selected?OverlayTheme.Accent:FullMapSidebarStyle.Ink):FullMapSidebarStyle.Secondary,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            } else if(Height<=22 || selected)FullMapSidebarStyle.DrawMiniButton(e.Graphics,rect,Text,selected,Font,Enabled);
            else FullMapSidebarStyle.DrawActionButton(e.Graphics,rect,Text,OverlayTheme.Surface,Font,Enabled);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(rect,-4,-4),FullMapSidebarStyle.Muted,BackColor);
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e);Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e);Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e);Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e);Invalidate(); }
    }

    internal sealed class SidebarRadioButton:RadioButton {
        readonly Font sidebarFont=new Font("Segoe UI",7f,FontStyle.Bold);
        internal SidebarRadioButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            Appearance=Appearance.Button;FlatStyle=FlatStyle.Flat;UseVisualStyleBackColor=false;
            BackColor=FullMapSidebarStyle.Background;Font=sidebarFont;Cursor=Cursors.Hand;
        }
        protected override void Dispose(bool disposing) { if(disposing)sidebarFont.Dispose();base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            var rect=new Rectangle(0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
            FullMapSidebarStyle.DrawMiniButton(e.Graphics,rect,Text,Checked,Font,Enabled);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(rect,-4,-4),FullMapSidebarStyle.Muted,BackColor);
        }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e);Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e);Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e);Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e);Invalidate(); }
    }

    internal sealed class SidebarSection:GroupBox {
        internal SidebarSection() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            BackColor=FullMapSidebarStyle.Background;ForeColor=FullMapSidebarStyle.Muted;
        }
        public override Rectangle DisplayRectangle {
            get { return new Rectangle(Padding.Left,Padding.Top,Math.Max(0,ClientSize.Width-Padding.Horizontal),Math.Max(0,ClientSize.Height-Padding.Vertical)); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);
            if(!string.IsNullOrEmpty(Text))TextRenderer.DrawText(e.Graphics,Text,Font,
                new Rectangle(Padding.Left,0,Math.Max(0,Width-Padding.Horizontal),Font.Height+2),ForeColor,
                TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class SidebarScrollPanel:Panel {
        const int BarWidth=4,BarInset=2;
        readonly Panel viewport;
        readonly System.Collections.Generic.HashSet<Control> hooked=new System.Collections.Generic.HashSet<Control>();
        internal Panel Content { get; private set; }
        int minimumContentWidth=240,maximumScrollX,maximumScrollY;
        readonly BatchedWheel wheel;
        int contentUpdateDepth;
        internal event EventHandler ViewportChanged;
        internal Action<int> WheelNavigation;
        internal Rectangle VisibleContentBounds { get { return new Rectangle(scrollOffset,viewport.ClientSize); } }
        internal void BeginContentUpdate() { contentUpdateDepth++; }
        internal void EndContentUpdate() { if(contentUpdateDepth>0 && --contentUpdateDepth==0)RefreshContentLayout(); }
        Point scrollOffset;
        bool layingOut,draggingHorizontal,draggingVertical;
        Point dragStart,dragOffset;
        Rectangle verticalTrack,verticalThumb,horizontalTrack,horizontalThumb;
        internal int MinimumContentWidth { get { return minimumContentWidth; } set { minimumContentWidth=Math.Max(0,value);RefreshContentLayout(); } }
        internal int MaximumScrollX { get { return maximumScrollX; } }
        internal int MaximumScrollY { get { return maximumScrollY; } }
        internal Point ScrollOffset { get { return scrollOffset; } set { SetScrollOffset(value); } }
        internal SidebarScrollPanel() {
            wheel=new BatchedWheel(steps=> {
                if(IsDisposed || Disposing)return;
                if(WheelNavigation!=null)WheelNavigation(-steps);
                else if(maximumScrollX>0 && (((ModifierKeys&Keys.Shift)!=0) || maximumScrollY==0))ScrollBy(-steps*48,0);
                else ScrollBy(0,-steps*48);
            });
            VisibleChanged+=(s,e)=> { if(!Visible)wheel.Cancel(); };
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            SetStyle(ControlStyles.Selectable,false);TabStop=false;AutoScroll=false;
            BackColor=FullMapSidebarStyle.Background;Padding=new Padding(10,0,10,0);
            viewport=new Panel { BackColor=FullMapSidebarStyle.Background,TabStop=false };
            Content=new Panel { BackColor=FullMapSidebarStyle.Background,TabStop=false };
            viewport.Controls.Add(Content);Controls.Add(viewport);
            Content.ControlAdded+=(s,e)=> { Hook(e.Control);RefreshContentLayout(); };
            Content.ControlRemoved+=(s,e)=> { Unhook(e.Control);RefreshContentLayout(); };
            Content.Layout+=(s,e)=>RefreshContentLayout();
            viewport.MouseWheel+=ChildWheel;Content.MouseWheel+=ChildWheel;
        }
        void Hook(Control control) {
            if(!hooked.Add(control))return;
            control.SizeChanged+=ChildLayoutChanged;control.VisibleChanged+=ChildLayoutChanged;control.DockChanged+=ChildLayoutChanged;
            control.GotFocus+=ChildGotFocus;control.ControlAdded+=DescendantAdded;control.ControlRemoved+=DescendantRemoved;
            if(!(control is NumericUpDown) && !InNumericInput(control))control.MouseWheel+=ChildWheel;
            foreach(Control child in control.Controls)Hook(child);
        }
        void Unhook(Control control) {
            if(!hooked.Remove(control))return;
            control.SizeChanged-=ChildLayoutChanged;control.VisibleChanged-=ChildLayoutChanged;control.DockChanged-=ChildLayoutChanged;
            control.GotFocus-=ChildGotFocus;control.ControlAdded-=DescendantAdded;control.ControlRemoved-=DescendantRemoved;control.MouseWheel-=ChildWheel;
            foreach(Control child in control.Controls)Unhook(child);
        }
        static bool InNumericInput(Control control) {
            for(Control parent=control.Parent;parent!=null;parent=parent.Parent)if(parent is NumericUpDown)return true;
            return false;
        }
        void DescendantAdded(object sender,ControlEventArgs e) { Hook(e.Control);RefreshContentLayout(); }
        void DescendantRemoved(object sender,ControlEventArgs e) { Unhook(e.Control);RefreshContentLayout(); }
        void ChildLayoutChanged(object sender,EventArgs e) { if(!layingOut)RefreshContentLayout(); }
        void ChildGotFocus(object sender,EventArgs e) { ScrollControlIntoView((Control)sender); }
        void ChildWheel(object sender,MouseEventArgs e) { HandleWheel(e); }
        void HandleWheel(MouseEventArgs e) {
            var handled=e as HandledMouseEventArgs;if(handled!=null && handled.Handled)return;
            wheel.Add(e.Delta);
            if(handled!=null)handled.Handled=true;
        }
        protected override void Dispose(bool disposing) { if(disposing)wheel.Dispose();base.Dispose(disposing); }
        internal void RefreshContentLayout() {
            if(contentUpdateDepth>0 || layingOut || viewport==null || Content==null || IsDisposed || Disposing)return;
            Rectangle previousViewport=VisibleContentBounds;
            layingOut=true;
            try {
                int contentHeight=0;
                foreach(Control control in Content.Controls)if(control.Visible && control.Dock==DockStyle.Top)contentHeight+=Math.Max(0,control.Height);
                bool horizontal=false,vertical=false;
                int viewWidth=0,viewHeight=0,contentWidth=0;
                for(int pass=0;pass<3;pass++) {
                    int extraRight=vertical?Math.Max(0,BarWidth+BarInset-Padding.Right):0;
                    int extraBottom=horizontal?Math.Max(0,BarWidth+BarInset-Padding.Bottom):0;
                    viewWidth=Math.Max(0,ClientSize.Width-Padding.Horizontal-extraRight);
                    viewHeight=Math.Max(0,ClientSize.Height-Padding.Vertical-extraBottom);
                    contentWidth=Math.Max(minimumContentWidth,viewWidth);
                    horizontal=contentWidth>viewWidth;vertical=contentHeight>viewHeight;
                }
                viewport.Bounds=new Rectangle(Padding.Left,Padding.Top,viewWidth,viewHeight);
                Content.SuspendLayout();
                try {
                    Content.Size=new Size(contentWidth,Math.Max(contentHeight,viewHeight));
                    int y=0;
                    for(int index=Content.Controls.Count-1;index>=0;index--) {
                        Control control=Content.Controls[index];
                        if(!control.Visible || control.Dock!=DockStyle.Top)continue;
                        control.SetBounds(0,y,contentWidth,control.Height);y+=control.Height;
                    }
                } finally { Content.ResumeLayout(true); }
                maximumScrollX=Math.Max(0,contentWidth-viewWidth);maximumScrollY=Math.Max(0,contentHeight-viewHeight);
                SetScrollOffset(scrollOffset);UpdateScrollbars();Invalidate();
            } finally { layingOut=false; }
            if(previousViewport!=VisibleContentBounds && ViewportChanged!=null)ViewportChanged(this,EventArgs.Empty);
        }
        void SetScrollOffset(Point value) {
            Point previous=scrollOffset;
            scrollOffset=new Point(Math.Max(0,Math.Min(maximumScrollX,value.X)),Math.Max(0,Math.Min(maximumScrollY,value.Y)));
            if(Content!=null)Content.Location=new Point(-scrollOffset.X,-scrollOffset.Y);
            UpdateScrollbars();Invalidate();
            if(previous!=scrollOffset && !layingOut && ViewportChanged!=null)ViewportChanged(this,EventArgs.Empty);
        }
        internal void ScrollBy(int dx,int dy) {
            int x=(int)Math.Max(0L,Math.Min((long)maximumScrollX,(long)scrollOffset.X+dx));
            int y=(int)Math.Max(0L,Math.Min((long)maximumScrollY,(long)scrollOffset.Y+dy));
            SetScrollOffset(new Point(x,y));
        }
        internal new void ScrollControlIntoView(Control control) {
            if(control==null || viewport==null || Content==null)return;
            Point position=Point.Empty;Control current=control;
            while(current!=null && current!=Content) { position.Offset(current.Left,current.Top);current=current.Parent; }
            if(current!=Content)return;
            int x=scrollOffset.X,y=scrollOffset.Y;
            if(control.Width>viewport.Width || position.X<x)x=position.X;
            else if(position.X+control.Width>x+viewport.Width)x=position.X+control.Width-viewport.Width;
            if(control.Height>viewport.Height || position.Y<y)y=position.Y;
            else if(position.Y+control.Height>y+viewport.Height)y=position.Y+control.Height-viewport.Height;
            SetScrollOffset(new Point(x,y));
        }
        void UpdateScrollbars() {
            if(viewport==null || Content==null)return;
            verticalTrack=maximumScrollY>0?new Rectangle(Math.Max(0,ClientSize.Width-BarWidth-BarInset),viewport.Top,BarWidth,viewport.Height):Rectangle.Empty;
            horizontalTrack=maximumScrollX>0?new Rectangle(viewport.Left,Math.Max(0,ClientSize.Height-BarWidth-BarInset),viewport.Width,BarWidth):Rectangle.Empty;
            verticalThumb=Thumb(verticalTrack,viewport.Height,Content.Height,scrollOffset.Y,maximumScrollY,true);
            horizontalThumb=Thumb(horizontalTrack,viewport.Width,Content.Width,scrollOffset.X,maximumScrollX,false);
        }
        static Rectangle Thumb(Rectangle track,int visible,int total,int offset,int maximum,bool vertical) {
            int length=vertical?track.Height:track.Width;
            if(length<=0 || maximum<=0)return Rectangle.Empty;
            int size=Math.Min(length,Math.Max(24,(int)((long)visible*length/Math.Max(1,total))));
            int start=(int)((long)offset*(length-size)/maximum);
            return vertical?new Rectangle(track.X,track.Y+start,track.Width,size):new Rectangle(track.X+start,track.Y,size,track.Height);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);UpdateScrollbars();
            using(var track=new SolidBrush(FullMapSidebarStyle.ScrollTrack)) {
                if(!verticalTrack.IsEmpty)e.Graphics.FillRectangle(track,verticalTrack);
                if(!horizontalTrack.IsEmpty)e.Graphics.FillRectangle(track,horizontalTrack);
            }
            using(var thumb=new SolidBrush(draggingVertical || draggingHorizontal?FullMapSidebarStyle.ScrollThumbDragging:FullMapSidebarStyle.ScrollThumb)) {
                if(!verticalThumb.IsEmpty)e.Graphics.FillRectangle(thumb,verticalThumb);
                if(!horizontalThumb.IsEmpty)e.Graphics.FillRectangle(thumb,horizontalThumb);
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e) { HandleWheel(e);base.OnMouseWheel(e); }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;UpdateScrollbars();
            if(maximumScrollY>0 && Rectangle.Inflate(verticalTrack,4,0).Contains(e.Location)) {
                if(!verticalThumb.Contains(e.Location)) {
                    int travel=verticalTrack.Height-verticalThumb.Height;
                    int value=travel<=0?0:(int)((long)(e.Y-verticalTrack.Top-verticalThumb.Height/2)*maximumScrollY/travel);
                    SetScrollOffset(new Point(scrollOffset.X,value));
                }
                draggingVertical=true;
            } else if(maximumScrollX>0 && Rectangle.Inflate(horizontalTrack,0,4).Contains(e.Location)) {
                if(!horizontalThumb.Contains(e.Location)) {
                    int travel=horizontalTrack.Width-horizontalThumb.Width;
                    int value=travel<=0?0:(int)((long)(e.X-horizontalTrack.Left-horizontalThumb.Width/2)*maximumScrollX/travel);
                    SetScrollOffset(new Point(value,scrollOffset.Y));
                }
                draggingHorizontal=true;
            }
            if(draggingVertical || draggingHorizontal) { dragStart=e.Location;dragOffset=scrollOffset;Capture=true;Invalidate(); }
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if(draggingVertical) {
                int travel=verticalTrack.Height-verticalThumb.Height;
                int change=travel<=0?0:(int)((long)(e.Y-dragStart.Y)*maximumScrollY/travel);
                SetScrollOffset(new Point(scrollOffset.X,dragOffset.Y+change));
            } else if(draggingHorizontal) {
                int travel=horizontalTrack.Width-horizontalThumb.Width;
                int change=travel<=0?0:(int)((long)(e.X-dragStart.X)*maximumScrollX/travel);
                SetScrollOffset(new Point(dragOffset.X+change,scrollOffset.Y));
            }
        }
        void CancelDragging() { draggingHorizontal=draggingVertical=false;if(Capture)Capture=false;Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e);if(e.Button==MouseButtons.Left)CancelDragging(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e);if(!Capture)CancelDragging(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e);RefreshContentLayout(); }
        protected override void OnPaddingChanged(EventArgs e) { base.OnPaddingChanged(e);RefreshContentLayout(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e);if(!Visible)CancelDragging();RefreshContentLayout(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e);if(!Enabled)CancelDragging(); }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e);if(Parent==null)CancelDragging(); }
    }

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

        public static void Frame(Form form,string title,Action dismiss,int headerHeight=52) {
            form.FormBorderStyle=FormBorderStyle.None; form.TopMost=true; form.ShowInTaskbar=true;
            form.Padding=new Padding(1); form.BackColor=Background; form.Font=new Font("Segoe UI",9);
            Panel header=new Panel { Dock=DockStyle.Top,Height=headerHeight,Padding=new Padding(14,8,8,8),BackColor=Surface };
            Label caption=new Label { Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Accent };
            Button close=new Button { Name="CloseWindow",Text=Localization.Get("Close"),Dock=DockStyle.Right,Width=62,TabStop=false,CausesValidation=false };
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
        double began,duration,lastSample;
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
            start=Point; target=point;
            // Heading describes the latest observed view direction, not travel motion.
            // Apply it immediately rather than delaying it behind position smoothing.
            Yaw+=HeadingDelta(Yaw,yaw);
            // Reach the latest observed position promptly without predicting beyond it.
            began=now; duration=Math.Max(.06,Math.Min(.18,initialized?interval*.5:.1));
            lastSample=now; initialized=true;
        }
        public bool Advance(double now) {
            if(!initialized)return false;
            double t=Math.Max(0,Math.Min(1,(now-began)/duration));
            PointF next=new PointF((float)(start.X+(target.X-start.X)*t),(float)(start.Y+(target.Y-start.Y)*t));
            bool changed=next!=Point;
            Point=next; return changed;
        }
        public static void SelfTest() {
            MapMotion m=new MapMotion(); m.Sample(new PointF(.5f,.5f),359,0,false);
            m.Sample(new PointF(.501f,.5f),1,.25,false); m.Advance(.3125);
            if(Math.Abs(m.Point.X-.5005)>.000001 || Math.Abs(m.Yaw-361)>.001)throw new Exception("Motion midpoint or immediate heading update failed.");
            m.Advance(2); if(Math.Abs(m.Point.X-.501)>.000001 || m.Advance(3))throw new Exception("Motion overshoot or idle redraw.");
            m.Sample(new PointF(.9f,.9f),45,3,false); if(m.Point.X!=.9f)throw new Exception("Teleport must snap.");
            m.Sample(new PointF(.900005f,.9f),45.5,4,false);
            if(m.Point.X!=.9f || m.Yaw!=45.0)throw new Exception("Stationary micro-jitter must be deadbanded.");
        }
    }
    public sealed class ChatState {
        internal long Revision { get; private set; }
        internal bool AllowsQueuedShortcut(long revision) { return !Paused && Revision==revision; }
        DateTime openTime = DateTime.MinValue;
        public bool Paused {
            get {
                if(openTime == DateTime.MinValue) return false;
                // An idle chat box still owns typing; only an explicit close releases it.
                return true;
            }
        }
        public void Key(int key,int chatOpenKey=0x54,int copyKey=0,int scanCode=0,int chatOpenScanCode=0) {
            // Enter (0x0D) and Escape (0x1B) close chat in SCUM
            if(key==0x0D || key==0x1B) {
                Revision++;
                openTime = DateTime.MinValue;
                return;
            }
            // Opening chat via configured chat key, '/' or keypad '/'
            if(PhysicalKeyCapture.Matches(key,scanCode,chatOpenKey,chatOpenScanCode) || ((key==0xBF || key==0x6F) && key!=copyKey)) {
                Revision++;
                openTime = DateTime.UtcNow;
                return;
            }
            // While chat is already open, any keypress (Tab to cycle channels, typing letters/numbers,
            // space, backspace, etc.) extends the active chat session so it never times out mid-typing.
            if(Paused) {
                openTime = DateTime.UtcNow;
            }
        }
        public void Reset() { Revision++;openTime = DateTime.MinValue; }
    }
    // Hook and polling must share edges: replaying an old Enter after a newer T
    // would clear the chat gate while SCUM's text box is still open.
    public sealed class ChatKeyMonitor {
        readonly bool[] held=new bool[256];
        public bool Observe(int key,bool down,bool focused,ChatState chat,int openKey,int copyKey=0,int scanCode=0,int chatOpenScanCode=0) {
            if(key<0 || key>=held.Length) return false;
            bool fresh=down && !held[key];
            held[key]=down;
            if(!focused || !fresh || chat==null) return false;
            chat.Key(key,openKey,copyKey,scanCode,chatOpenScanCode);
            return true;
        }
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
            // A missed key-up must not leave M (or any other shortcut) permanently held.
            for(int key=0;key<held.Length;key++) if(held[key] && !isDown(key)) held[key]=false;
            for(int i=0; i<ModifierKeys.Length; i++) {
                int key = ModifierKeys[i];
                held[key] = isDown(key);
            }
        }
    }
    // Observes shortcut transitions and activity timestamps. No typed text is collected or suppressed.
    internal static class PhysicalKeyCapture {
        internal static string KeyName(int key) {
            if(key==0)return Localization.Get("KeyNone");
            if(key==0xBF)return "/";

            if(key==0x6F)return "Num /";

            if(key==0x20)return "Space";
            if(key==0x11 || key==0xA2 || key==0xA3)return "Ctrl";
            if(key==0x10 || key==0xA0 || key==0xA1)return "Shift";
            if(key==0x12 || key==0xA4 || key==0xA5)return "Alt";
            if(key==0xDC)return "\\";

            string name=((Keys)key).ToString();

            return string.IsNullOrEmpty(name)?"VK "+key.ToString(System.Globalization.CultureInfo.InvariantCulture):name;

        }
        internal static bool IsModifierKey(int key) {

            return key==0x10 || key==0xA0 || key==0xA1 || key==0x11 || key==0xA2 || key==0xA3 ||

                key==0x12 || key==0xA4 || key==0xA5 || key==0x5B || key==0x5C;

        }
        static readonly object sync=new object();
        static int lastKey,lastScan;
        static DateTime lastTime;
        internal static void Observe(int key,uint scan,uint flags) {
            int encoded=EncodeScanCode(scan,flags);
            if(key<=0 || key>=256 || !Valid(encoded)) return;
            lock(sync) { lastKey=key; lastScan=encoded; lastTime=DateTime.UtcNow; }
        }
        internal static int EncodeScanCode(uint scan,uint flags) { return scan>0 && scan<=255?(int)scan|((flags&1)!=0?0x100:0):0; }
        internal static bool TryGet(int key,out int scan) {
            lock(sync) {
                scan=lastKey==key && (DateTime.UtcNow-lastTime).TotalSeconds<3?lastScan:0;
                return scan!=0;
            }
        }
        internal static bool Valid(int scan) { return (scan&0xFF)>0 && (scan&0xFF)<=255 && (scan&~0x1FF)==0; }
        internal static bool Matches(int key,int scan,int configuredKey,int configuredScan) {
            return Valid(configuredScan) && Valid(scan)?scan==configuredScan:key==configuredKey;
        }
        internal static bool SameBinding(int firstKey,int firstScan,int secondKey,int secondScan) {
            return Valid(firstScan) && Valid(secondScan)?firstScan==secondScan:firstKey==secondKey;
        }
        internal static bool HasConflicts(int[] keys,int[] scans) {
            if(keys==null || scans==null || keys.Length!=scans.Length) throw new ArgumentException("Key and scan-code lists must match.");
            for(int i=0;i<keys.Length;i++) for(int j=i+1;j<keys.Length;j++)
                if(SameBinding(keys[i],scans[i],keys[j],scans[j])) return true;
            return false;
        }
    }
    sealed class GameKeys:IDisposable {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [StructLayout(LayoutKind.Sequential)] struct KeyboardData { public uint key,scan,flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MouseData { public int x,y; public uint data,flags,time; public UIntPtr extra; }
        readonly HookProc callback;
        readonly HookProc mouseCallback;
        readonly Action<int,int> action;
        readonly Func<int,int,bool> shouldDispatch;
        readonly Func<int,int,bool> sharedGameBinding;
        readonly ChatState chatState;
        readonly Func<int> getChatOpenKey;
        readonly Func<int> getCopyModifier;
        readonly Func<int> getCopyKey;
        readonly Func<int> getChatScanCode;
        readonly Func<bool> inventoryOwnsInput;
        readonly PhysicalKeyTransitions transitions=new PhysicalKeyTransitions();
        readonly bool[] suppressedShortcuts=new bool[256];
        IntPtr hook;
        IntPtr mouseHook;
        DateTime nextHookRefresh=DateTime.MinValue;
        bool disposed,wasFocused;
        IntPtr sampledGameWindow;
        readonly ChatKeyMonitor chatKeys=new ChatKeyMonitor();
        static DateTime lastMouseAction=DateTime.MinValue;
        public static DateTime LastUserInputTime = DateTime.MinValue;
        internal static long PhysicalInputRevision;
        public static DateTime LastFreshKeyDownTime = DateTime.MinValue;
        internal static bool UserActiveRecently(int windowMs = 90) {
            return (DateTime.UtcNow - LastFreshKeyDownTime).TotalMilliseconds < windowMs || (DateTime.UtcNow-lastMouseAction).TotalMilliseconds<400;
        }
        public bool Available { get { return hook!=IntPtr.Zero && mouseHook!=IntPtr.Zero; } }
        public bool ShortcutModifiersDown(bool includeShift) { return transitions.ShortcutModifiersDown(includeShift); }
        internal bool PhysicalControlDown { get { return transitions.IsDown(0x11) || transitions.IsDown(0xA2) || transitions.IsDown(0xA3); } }
        internal Action<int,Point> OnPhysicalMouse;
        internal static void ClearInputCooldowns() {
            lastMouseAction=LastUserInputTime=LastFreshKeyDownTime=DateTime.MinValue;
        }
        public GameKeys(Action<int,int> action,Func<int,int,bool> shouldDispatch,ChatState chatState=null,Func<int> getChatOpenKey=null,Func<int> getCopyModifier=null,Func<bool> inventoryOwnsInput=null,Func<int> getCopyKey=null,Func<int> getChatScanCode=null,Func<int,int,bool> sharedGameBinding=null) {
            this.sharedGameBinding=sharedGameBinding;
            this.action=action; this.shouldDispatch=shouldDispatch; this.chatState=chatState; this.getChatOpenKey=getChatOpenKey; this.getCopyModifier=getCopyModifier; this.inventoryOwnsInput=inventoryOwnsInput; this.getCopyKey=getCopyKey; this.getChatScanCode=getChatScanCode; callback=OnKey;
            mouseCallback=OnMouse;
            RefreshHooks();
        }
        void RefreshHooks() {
            if(disposed) return;
            if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook);
            if(mouseHook!=IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
            int keyboardError=hook==IntPtr.Zero?Marshal.GetLastWin32Error():0;
            mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
            int mouseError=mouseHook==IntPtr.Zero?Marshal.GetLastWin32Error():0;
            if(Native.InputTraceActive) Native.TraceInput("hooks keyboard_installed="+(hook!=IntPtr.Zero)+" keyboard_error="+keyboardError+
                " mouse_installed="+(mouseHook!=IntPtr.Zero)+" mouse_error="+mouseError);
            nextHookRefresh=DateTime.UtcNow.AddSeconds(30);
        }
        public void Resync(bool focused) {
            if(disposed) return;
            if(Native.InputTraceActive && focused!=wasFocused) {
                Native.TraceInput("game focus="+focused);
                Native.TraceFocusSecurity(focused);
            }
            // Windows may silently remove a hook after a long UI stall; a non-zero handle
            // alone is not proof it still receives input. Refresh without changing chat state.
            if(!Native.CopyInProgress && !Native.AdminInputInProgress && (DateTime.UtcNow>=nextHookRefresh || (focused && !wasFocused))) RefreshHooks();
            wasFocused=focused;
            sampledGameWindow=focused?Native.GetForegroundWindow():IntPtr.Zero;
            if(!Native.CopyInProgress && !Native.AdminInputInProgress) {
                transitions.Resync(key => (Native.GetAsyncKeyState(key)&0x8000)!=0);
                for(int key=0;key<suppressedShortcuts.Length;key++)
                    if(suppressedShortcuts[key] && (Native.GetAsyncKeyState(key)&0x8000)==0) suppressedShortcuts[key]=false;
            }
            // GetAsyncKeyState includes our injected copy key. The hook already
            // handles physical chat input during copying and ignores injected events.
            if(Native.CopyInProgress || Native.AdminInputInProgress) return;
            int openKey=getChatOpenKey!=null?getChatOpenKey():0x54;
            int copyKey=getCopyKey!=null?getCopyKey():0;
            int chatScan=getChatScanCode!=null?getChatScanCode():0;
            foreach(int key in new[]{openKey,0xBF,0x6F,0x0D,0x1B}) {
                if(key<0 || key>=256) continue;
                if(key==copyKey && key!=openKey && (key==0xBF || key==0x6F)) continue;
                bool down=(Native.GetAsyncKeyState(key)&0x8000)!=0;
                if(chatKeys.Observe(key,down,focused,chatState,openKey,copyKey,0,chatScan)) {
                    Native.CancelActiveCopy();
                }
            }
        }
        IntPtr OnMouse(int code,IntPtr message,IntPtr data) {
            int kind=message.ToInt32();
            if(code>=0 && kind!=0x200 && Native.GameFocused()) {
                MouseData mouse=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                if((mouse.flags&1)==0) PhysicalInputRevision++;
                if((mouse.flags&1)==0 && OnPhysicalMouse!=null) OnPhysicalMouse(kind,new Point(mouse.x,mouse.y));
                if((mouse.flags&1)==0 && MouseActionBlocksCopy(kind,getCopyModifier!=null?getCopyModifier():0xA2)) {
                    lastMouseAction=DateTime.UtcNow;
                    Native.CancelActiveCopy();
                }
            }
            return CallNextHookEx(mouseHook,code,message,data);
        }
        internal static bool MouseActionBlocksCopy(int message,int modifier) {
            // Wheel events cannot collide with a modifier that is no longer being injected.
            return (message!=0x20A && message!=0x20E) || modifier>0;
        }
        internal bool ConsumesBinding(int key,int scanCode) {
            return (shouldDispatch==null || shouldDispatch(key,scanCode)) &&
                (sharedGameBinding==null || !sharedGameBinding(key,scanCode));
        }
        IntPtr OnKey(int code,IntPtr message,IntPtr data) {
            if(code>=0) {
                KeyboardData key=(KeyboardData)Marshal.PtrToStructure(data,typeof(KeyboardData));
                int k=(int)key.key;
                int eventScan=PhysicalKeyCapture.EncodeScanCode(key.scan,key.flags);
                bool isDown=message.ToInt32()==0x100 || message.ToInt32()==0x104;
                bool injected=(key.flags&0x10)!=0;
                if(Native.ShouldSuppressCoordinateInput(injected,isDown,key.extra)) {
                    if(Native.InputTraceActive)Native.TraceInput("copy delivery blocked vk="+k+" scan="+eventScan+" chat="+(chatState!=null && chatState.Paused));
                    return new IntPtr(1);
                }
                bool traceKey=Native.InputTraceActive && key.extra!=Native.AdminInputTag && !Native.IsCoordinateCopyInput(key.extra) &&
                    (k==0x57 || k==0x10 || k==0xA0 || k==0xA1 || k==0x6F ||
                    (shouldDispatch!=null && shouldDispatch(k,eventScan)) || (getCopyKey!=null && k==getCopyKey()));
                // Injected events must never alter physical held-key or chat state, even after copying ends.
                if(injected) {
                    if(traceKey) Native.TraceInput("hook vk="+k+" scan="+eventScan+" flags="+key.flags+" message="+message.ToInt32()+
                        " source=injected decision=ignored-injected fresh=not-observed chat="+(chatState!=null && chatState.Paused)+
                        " inventory=not-queried modifiers="+transitions.ShortcutModifiersDown(true)+" dispatch=False suppressed=False");
                    return CallNextHookEx(hook,code,message,data);
                }
                PhysicalInputRevision++;
                bool shortcutWasSuppressed=k>=0 && k<suppressedShortcuts.Length && suppressedShortcuts[k];
                if(shortcutWasSuppressed) {
                    if(!isDown) suppressedShortcuts[k]=false;
                    if(traceKey) Native.TraceInput("hook vk="+k+" scan="+eventScan+" flags="+key.flags+" message="+message.ToInt32()+
                        " source=physical decision=suppressed-held-edge fresh=not-observed chat="+(chatState!=null && chatState.Paused)+
                        " inventory=not-queried modifiers="+transitions.ShortcutModifiersDown(true)+" dispatch=False suppressed=True");
                    return new IntPtr(1);
                }
                if(isDown) PhysicalKeyCapture.Observe(k,key.scan,key.flags);
                // The hook runs synchronously on the Windows input path. Use the
                // focus sample maintained by Resync() instead of querying the
                // foreground process for every key transition. Validate the cheap
                // window handle too: desktop typing just after Alt-Tab must not
                // open a phantom chat gate using the previous timer's focus sample.
                IntPtr foreground=Native.GetForegroundWindow();
                bool gameFocused=FocusStillMatches(wasFocused,sampledGameWindow,foreground);
                int openKey=getChatOpenKey!=null?getChatOpenKey():0x54;
                int copyKey=getCopyKey!=null?getCopyKey():0;
                int chatScan=getChatScanCode!=null?getChatScanCode():0;
                // A chat/map press can arrive before the sampler sees focus return.
                // Resolve watched keys against the actual foreground window at that event.
                gameFocused=ShortcutEventFocused(gameFocused,(shouldDispatch!=null && shouldDispatch(k,eventScan)) || PhysicalKeyCapture.Matches(k,eventScan,openKey,chatScan),()=>Native.IsGameWindow(foreground));
                bool chatWasPaused=chatState!=null && chatState.Paused;
                chatKeys.Observe(k,isDown,gameFocused,chatState,openKey,copyKey,eventScan,chatScan);
                bool chatIsPaused=chatState!=null && chatState.Paused;
                bool fresh=transitions.Update(k,isDown);
                bool altDown=transitions.IsDown(0x12) || transitions.IsDown(0xA4) || transitions.IsDown(0xA5)
                    || (Native.GetAsyncKeyState(0x12)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA4)&0x8000)!=0 || (Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
                if(Native.CopyInProgress && isDown && (k==0x12 || k==0xA4 || k==0xA5 || k==0x09 || k==0x5B || k==0x5C)) {
                    Native.CancelActiveCopy();
                }
                if(k==0x09 && altDown) {
                    Native.LastAltTabTime=DateTime.UtcNow;
                }
                if(gameFocused) {
                    LastUserInputTime=DateTime.UtcNow;
                    if(fresh && isDown) {
                        LastFreshKeyDownTime=DateTime.UtcNow;
                        Native.CancelActiveCopy();
                    }
                }
                // Dispatch only keys that can affect the application. The hook still tracks
                // every physical key so modifier/chat state remains accurate, but forwarding
                // all typed keys creates a BeginInvoke storm and makes focus transitions prone
                // to losing the matching key-up event.
                bool chatOwnsKey=ChatOwnsShortcut(chatWasPaused,chatIsPaused);
                bool gameUiOwnsInput=gameFocused && inventoryOwnsInput!=null && inventoryOwnsInput();
                bool watched=shouldDispatch==null || shouldDispatch(k,eventScan);
                bool chatControlKey=PhysicalKeyCapture.Matches(k,eventScan,openKey,chatScan) || k==0xBF || k==0x6F || k==0x09 || k==0x0D || k==0x1B;
                bool appShortcut=gameFocused && fresh && isDown && !chatOwnsKey && !gameUiOwnsInput && !chatControlKey && ConsumesBinding(k,eventScan)
                    && !(transitions.ShortcutModifiersDown(true) || altDown || (Native.GetAsyncKeyState(0x5B)&0x8000)!=0 || (Native.GetAsyncKeyState(0x5C)&0x8000)!=0);
                bool dispatched=fresh && isDown && !chatOwnsKey && !gameUiOwnsInput && !(k==0x09 && altDown) && watched;
                if(traceKey) Native.TraceInput("hook vk="+k+" scan="+eventScan+" flags="+key.flags+" message="+message.ToInt32()+
                    " source=physical fresh="+fresh+" down="+isDown+" focused="+gameFocused+" watched="+watched+
                    " chat_before="+chatWasPaused+" chat_after="+chatIsPaused+" chat_owns="+chatOwnsKey+
                    " inventory="+gameUiOwnsInput+" modifiers="+transitions.ShortcutModifiersDown(true)+" alt="+altDown+
                    " dispatch="+dispatched+" suppressed="+appShortcut+" copy_in_progress="+Native.CopyInProgress);
                if(dispatched) {
                    // App-only shortcuts consume both edges. The configured map key
                    // also belongs to SCUM: dispatch it here without swallowing it.
                    if(appShortcut && k>=0 && k<suppressedShortcuts.Length) suppressedShortcuts[k]=true;
                    action(k,eventScan);
                }
                if(appShortcut) return new IntPtr(1);
            }
            return CallNextHookEx(hook,code,message,data);
        }
        internal static bool ChatOwnsShortcut(bool wasPaused,bool isPaused) {
            // Preserve ownership at the physical event, before asynchronous UI dispatch.
            // This includes Escape/Enter closing chat and Tab changing its channel.
            return wasPaused || isPaused;
        }
        internal static bool ShortcutEventFocused(bool sampled,bool watched,Func<bool> verify) { return sampled || (watched && verify()); }
        internal static bool FocusStillMatches(bool focused,IntPtr sampledWindow,IntPtr foreground) {
            return focused && sampledWindow!=IntPtr.Zero && sampledWindow==foreground;
        }
        public void Dispose() {
            disposed=true;
            Native.CancelActiveCopy();
            if(hook!=IntPtr.Zero) { UnhookWindowsHookEx(hook); hook=IntPtr.Zero; }
            if(mouseHook!=IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook=IntPtr.Zero; }
        }
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
        bool movingMinimap;
        Point minimapDragOffset;
        bool fullMapMode;
        bool fullMapClickThrough;
        bool hiddenForClickThrough;
        bool fullMapRightButtonDown;
        internal bool FullMapClickThrough { get { return fullMapClickThrough; } }
        public Action OnFullMapClickThrough;
        public Action<bool> OnFullMapClickThroughChanged;
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
        Point wheelPoint;
        readonly BatchedWheel mouseWheel;
        public Action<Point, MouseButtons> OnFullMapMouseDown;
        public Action<Point, MouseButtons> OnFullMapMouseMove;
        public Action OnFullMapMouseLeave;
        public Action<Point, MouseButtons> OnFullMapMouseUp;
        public Action<Point> OnFullMapAddWaypoint;
        public Action OnRouteColorRequested;
        readonly ToolStripMenuItem fullMapRouteColorMenu;
        public Action OnPlayerColorRequested;
        readonly ToolStripMenuItem fullMapPlayerColorMenu;
        readonly ToolStripMenuItem fullMapAdminMenu;
        internal Action<ToolStripMenuItem> PopulateAdminCommands;
        public Func<bool> CanAddFullMapWaypoint;
        public Func<bool> CanShowFullMapContextMenu;
        public Action<ContextMenuStrip,Point> PopulateMarkerContextMenu;
        internal void CloseAdminContextMenu() {
            menu.Close(ToolStripDropDownCloseReason.ItemClicked);
            Native.ReleaseCapture();Cursor.Clip=Rectangle.Empty;
        }
        public Func<bool> AdminToolsEnabled;
        Point fullMapContextPoint;
        Point fullMapContextScreenPoint;

        internal static int FullMapAdminPanelWidth(Rectangle workingArea,Rectangle mapBounds,int requestedWidth) {
            return Math.Max(0,Math.Min(requestedWidth,mapBounds.Left-workingArea.Left-MapWindow.AdminControlsGap));
        }
        internal static Rectangle AdminPanelBounds(Rectangle workingArea,Rectangle mapBounds,int requestedWidth,int requestedHeight,out bool dockLeft) {
            int left=Math.Max(0,mapBounds.Left-workingArea.Left-MapWindow.AdminControlsGap);
            int right=Math.Max(0,workingArea.Right-mapBounds.Right-MapWindow.AdminControlsGap);
            // Select the same side for the tab and expanded panel. Prefer the left
            // where the controls have useful room, otherwise use the larger side.
            dockLeft=left>=240 || left>=right;
            if(workingArea.Width<=0 || workingArea.Height<=0 || mapBounds.Width<=0 || mapBounds.Height<=0)return Rectangle.Empty;
            int available=dockLeft?left:right;
            bool inset=available<MapWindow.AdminControlsCollapsedWidth;
            if(inset)available=Math.Min(workingArea.Width,mapBounds.Width);
            int width=Math.Min(Math.Max(MapWindow.AdminControlsCollapsedWidth,requestedWidth),available);
            int height=Math.Min(Math.Max(1,requestedHeight),Math.Max(1,workingArea.Height-24));
            int x=inset?(dockLeft?mapBounds.Left:mapBounds.Right-width)
                :(dockLeft?mapBounds.Left-width-MapWindow.AdminControlsGap:mapBounds.Right+MapWindow.AdminControlsGap);
            // A map filling both screen edges keeps its tab against the map's
            // inside edge rather than producing a detached or inaccessible window.
            x=Math.Max(workingArea.Left,Math.Min(x,workingArea.Right-width));
            int y=Math.Max(workingArea.Top,Math.Min(mapBounds.Top+72,workingArea.Bottom-height));
            return new Rectangle(x,y,width,height);
        }
        internal static Rectangle FullMapBounds(Rectangle area) {
            if(area.Width<=0 || area.Height<=0)return Rectangle.Empty;
            int mapSide=area.Height;
            int x=area.Left+(area.Width-mapSide)/2;
            int sidebarWidth=Math.Min(260,Math.Max(0,area.Right-x-mapSide));
            return new Rectangle(x,area.Top,mapSide+sidebarWidth,mapSide);
        }
        public bool FullMapMode {
            get { return fullMapMode; }
            set {
                if(fullMapMode==value) return;
                if(mouseWheel!=null)mouseWheel.Cancel();
                if(movingMinimap) { movingMinimap=false; Capture=false; }
                ChangingMapMode=true;
                try {
                fullMapMode=value;
                // Physical Ctrl polling belongs to the live sampling timer;
                // diagnostic windows must not inherit the user's game input.
                if(!value) UpdateFullMapClickThrough(false,Native.GameFocused());
                fullMapRightButtonDown=false;
                if(value) {
                    savedSize=Size;
                    savedLocation=Location;
                    Screen scr=Screen.FromControl(this);
                    try {
                        IntPtr fg=Native.GetForegroundWindow();
                        if(fg!=IntPtr.Zero && Native.IsGameWindow(fg)) scr=Screen.FromHandle(fg);
                    } catch {}
                    Rectangle bounds=FullMapBounds(scr.Bounds);
                    MinimumSize=Size.Empty;
                    MaximumSize=Size.Empty;
                    SetWindowPos(Handle,IntPtr.Zero,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0014);
                    Size=bounds.Size;Location=bounds.Location;
                } else {
                    MinimumSize=new Size(240,240);
                    MaximumSize=new Size(800,800);
                    if(savedSize.Width>0 && savedSize.Height>0) {
                        SetWindowPos(Handle,IntPtr.Zero,savedLocation.X,savedLocation.Y,savedSize.Width,savedSize.Height,0x0014);
                        Size=savedSize;
                        Location=savedLocation;
                    }
                }
                ApplyMousePassthrough();
                if(fullMapWaypointMenu!=null) fullMapWaypointMenu.Visible=value;
                if(fullMapRouteColorMenu!=null) fullMapRouteColorMenu.Visible=value;
                if(fullMapPlayerColorMenu!=null) fullMapPlayerColorMenu.Visible=value;
                if(fullMapAdminMenu!=null) fullMapAdminMenu.Visible=value;
                } finally { ChangingMapMode=false; }
            }
        }
        public OverlayWindow(Action settings,Action exit,Action<float> zoom,Action search) {
            mouseWheel=new BatchedWheel(steps=> {
                if(IsDisposed || Disposing || fullMapClickThrough)return;
                if(fullMapMode) { if(OnFullMapWheel!=null)OnFullMapWheel(steps*120,wheelPoint); }
                else if(!through)zoom((float)Math.Pow(1.5,Math.Max(-32,Math.Min(32,steps))));
            });
            var wheel=mouseWheel;
            Disposed+=(s,e)=>wheel.Dispose();
            VisibleChanged+=(s,e)=> { if(!Visible)wheel.Cancel(); };
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
            fullMapAdminMenu=new ToolStripMenuItem("Admin commands") { Visible=false };
            fullMapAdminMenu.DropDownItems.Add("Edit commands...");
            fullMapAdminMenu.DropDownOpening+=(s,e)=> { if(PopulateAdminCommands!=null)PopulateAdminCommands(fullMapAdminMenu); };
            menu.Items.Add(fullMapAdminMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.Get("SearchPlaceOrGrid"),null,(s,e)=>search());
            menu.Items.Add(Localization.Get("Settings"),null,(s,e)=>settings());
            menu.Items.Add(Localization.Get("TrayShowHide"),null,(s,e)=>Hide());
            menu.Items.Add(Localization.Get("TrayExit"),null,(s,e)=>exit());
            menu.Opening+=(s,e)=> {
                Native.ReleaseCapture();
                Cursor.Clip=Rectangle.Empty;
                fullMapAdminMenu.Visible=fullMapMode && AdminToolsEnabled!=null && AdminToolsEnabled();
                if(PopulateMarkerContextMenu!=null)PopulateMarkerContextMenu(menu,fullMapContextPoint);
                if(fullMapMode && CanAddFullMapWaypoint!=null) fullMapWaypointMenu.Enabled=CanAddFullMapWaypoint();
                else fullMapWaypointMenu.Enabled=true;
            };
            OverlayTheme.Menu(menu);
            Action languageChanged=()=> {
                try { if(!IsDisposed) { fullMapWaypointMenu.Text=Localization.Get("AddCustomWaypointHere"); fullMapRouteColorMenu.Text=Localization.Get("RouteGuidanceColor"); fullMapPlayerColorMenu.Text=Localization.Get("PlayerMarkerColor"); } } catch {}
            };
            Localization.LanguageChanged+=languageChanged;
            Disposed+=(s,e)=>Localization.LanguageChanged-=languageChanged;
            MouseCaptureChanged+=(s,e)=> { if(!Capture) movingMinimap=false; };
            VisibleChanged+=(s,e)=> {
                // A focus-return or map-opening Show must not put the window
                // back above SCUM while the user is still holding Ctrl.
                if(fullMapClickThrough && Visible) {
                    hiddenForClickThrough=true;
                    base.Hide();
                }
            };
            MouseDown+=(s,e)=> {
                if(fullMapClickThrough) return;
                if(fullMapMode) {
                    if(e.Button==MouseButtons.Right) {
                        fullMapRightButtonDown=true;
                        fullMapContextPoint=e.Location;
                        fullMapContextScreenPoint=PointToScreen(e.Location);
                        if(OnFullMapMouseDown!=null) OnFullMapMouseDown(e.Location, e.Button);
                        return;
                    }
                    if(OnFullMapMouseDown!=null) OnFullMapMouseDown(e.Location, e.Button);
                } else if(!through && e.Button==MouseButtons.Right) {
                    menu.Show(this,e.Location);
                } else if(!through && e.Button==MouseButtons.Left) {
                    // Keep movement on this non-activating layered window rather than
                    // entering a caption move loop with no actual caption.
                    minimapDragOffset=e.Location;
                    movingMinimap=true;
                    Capture=true;
                }
            };
            MouseMove+=(s,e)=> {
                if(fullMapClickThrough) return;
                if(movingMinimap && !fullMapMode && !through) {
                    Point screen=PointToScreen(e.Location);
                    Location=new Point(screen.X-minimapDragOffset.X,screen.Y-minimapDragOffset.Y);
                    return;
                }
                if(fullMapMode) {
                    if(OnFullMapMouseMove!=null) OnFullMapMouseMove(e.Location, e.Button);
                }
            };
            MouseLeave+=(s,e)=> {
                if(fullMapMode && OnFullMapMouseLeave!=null) OnFullMapMouseLeave();
            };
            MouseUp+=(s,e)=> {
                if(fullMapClickThrough) return;
                if(movingMinimap && e.Button==MouseButtons.Left) {
                    movingMinimap=false;
                    Capture=false;
                }
                if(fullMapMode) {
                    if(e.Button==MouseButtons.Right) {
                        bool ownedPress=fullMapRightButtonDown;
                        fullMapRightButtonDown=false;
                        if(ownedPress && !menu.Visible && (CanShowFullMapContextMenu==null || CanShowFullMapContextMenu()))
                            menu.Show(fullMapContextScreenPoint);
                        return;
                    }
                    if(OnFullMapMouseUp!=null) OnFullMapMouseUp(e.Location, e.Button);
                }
            };
            MouseWheel+=(s,e)=> {
                if(fullMapClickThrough) return;
                var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
                wheelPoint=e.Location;wheel.Add(e.Delta);
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get {
            CreateParams p=base.CreateParams;
            // OBS excludes WS_EX_TOOLWINDOW from Window Capture enumeration.
            // ShowInTaskbar=false retains the hidden owner; clearing this flag
            // exposes the actual layered overlay without adding a taskbar button.
            p.ExStyle=(p.ExStyle&~0x80)|0x80000|0x8000000;
            if(fullMapClickThrough || (through && !fullMapMode))p.ExStyle|=0x20;
            return p;
        } }
        internal void UpdateFullMapClickThrough(bool controlHeld,bool gameFocused) {
            bool enabled=fullMapMode && controlHeld && gameFocused;
            if(fullMapClickThrough==enabled) {
                if(!enabled && hiddenForClickThrough && gameFocused) RestoreClickThroughVisibility();
                return;
            }
            fullMapClickThrough=enabled;
            if(enabled) {
                mouseWheel.Cancel();
                fullMapRightButtonDown=false;
                Capture=false;
                if(menu.Visible) menu.Close();
                if(OnFullMapClickThrough!=null) OnFullMapClickThrough();
                if(Visible) { hiddenForClickThrough=true; base.Hide(); }
            }
            ApplyMousePassthrough();
            if(!enabled && hiddenForClickThrough && gameFocused) RestoreClickThroughVisibility();
            if(OnFullMapClickThroughChanged!=null)OnFullMapClickThroughChanged(enabled);
        }
        void RestoreClickThroughVisibility() {
            hiddenForClickThrough=false;
            if(!IsDisposed && !Disposing) Show();
        }
        public new void Hide() {
            // Explicit hide requests take precedence over a pending Ctrl restore.
            hiddenForClickThrough=false;
            base.Hide();
        }
        void ApplyMousePassthrough() {
            if(!IsHandleCreated) return;
            int style=GetWindowLong(Handle,-20);
            style|=0x8000000;
            // SCUM keeps keyboard focus; its open map supplies the free cursor.
            // The compact overlay remains click-through during normal gameplay.
            style=fullMapClickThrough || (through && !fullMapMode)?style|0x20:style&~0x20;
            SetWindowLong(Handle,-20,style);
            SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);
        }
        public void SetGameFocus(bool focused) {
            if(through==focused) return;
            through=focused;
            if(focused && movingMinimap) { movingMinimap=false; Capture=false; }
            if(focused && menu!=null && menu.Visible) menu.Close();
            ApplyMousePassthrough();
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x84) {
                // The full map intentionally owns right-clicks for its context menu.
                // Outside full-map mode, leave the right button transparent so SCUM's
                // ADS hold remains gameplay-controlled.
                if(fullMapMode) { m.Result=new IntPtr(fullMapClickThrough?-1:1); return; }
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
        static Rectangle? cachedViewport;
        internal static RectangleF CircleBounds(Size size,bool hasMapBounds,Rectangle? viewport=null) {
            if(viewport.HasValue) {
                Rectangle area=viewport.Value;
                float diameter=Math.Min(area.Width,area.Height);
                return new RectangleF(area.Left+(area.Width-diameter)/2,area.Top+(area.Height-diameter)/2,diameter,diameter);
            }
            float radius=Math.Min(size.Width,hasMapBounds?size.Height-34:size.Height)/2f;
            float cx=size.Width/2f,cy=hasMapBounds?radius+2:size.Height/2f;
            return new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
        }
        // Rebuild only when dimensions, shape, or appearance options change.
        public static void Fade(Bitmap bitmap,bool fade=true,int opacity=94,int mapLeft=-1,int mapTop=-1,int mapRight=-1,int mapBottom=-1,string shape="Square",Rectangle? viewport=null) {
            // A fully opaque, non-faded bitmap is already ready for UpdateLayeredWindow.
            // Avoid LockBits and a full per-pixel marshal/copy pass in full-map mode.
            if(!fade && opacity>=100 && shape!="Circle") return;
            if(fadeMask==null || fadeWidth!=bitmap.Width || fadeHeight!=bitmap.Height || cachedFade!=fade || cachedOpacity!=opacity
                || cachedMapLeft!=mapLeft || cachedMapTop!=mapTop || cachedMapRight!=mapRight || cachedMapBottom!=mapBottom || cachedShape!=shape || cachedViewport!=viewport) {
                cachedViewport=viewport;
                cachedFade=fade; cachedOpacity=opacity;
                cachedMapLeft=mapLeft; cachedMapTop=mapTop; cachedMapRight=mapRight; cachedMapBottom=mapBottom; cachedShape=shape;
                fadeWidth=bitmap.Width; fadeHeight=bitmap.Height;
                fadeMask=new byte[fadeWidth*fadeHeight];
                bool hasMapBounds=(mapLeft!=-1 && mapTop!=-1 && mapRight!=-1 && mapBottom!=-1);
                double cx=fadeWidth/2.0, cy=fadeHeight/2.0;
                double maxRadius=Math.Min(fadeWidth,fadeHeight)/2.0;
                if(shape=="Circle") {
                    RectangleF circle=CircleBounds(bitmap.Size,hasMapBounds,viewport);
                    maxRadius=circle.Width/2;
                    cx=circle.Left+maxRadius;
                    cy=circle.Top+maxRadius;
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
