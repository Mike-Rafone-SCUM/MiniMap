using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Presentation primitives are shared by native dialogs and the layered map.
    // They know no game keys, clipboard contents, storage paths or route workers.
    internal static class FieldConsolePaint {
        internal static GraphicsPath Shape(Rectangle r,int radius=8) {
            var p=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            if(d<1)return p;
            p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        internal static void Card(Graphics g,Rectangle r,Color fill,Color edge,int radius=8) {
            using(var p=Shape(r,radius)) { using(var b=new SolidBrush(fill))g.FillPath(b,p);using(var pen=new Pen(edge))g.DrawPath(pen,p); }
        }
        internal static void Text(Graphics g,string text,Rectangle r,float size,Color ink,bool bold=false,bool center=false) {
            using(var f=OverlayTheme.UiFont(size,bold?FontStyle.Bold:FontStyle.Regular))
            using(var b=new SolidBrush(ink))
            using(var format=new StringFormat { LineAlignment=StringAlignment.Center,Alignment=center?StringAlignment.Center:StringAlignment.Near,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap })g.DrawString(text,f,b,r,format);
        }
        internal static void Key(Graphics g,Rectangle r,string text) { Card(g,r,OverlayTheme.SurfaceCard,OverlayTheme.Border,6);Text(g,text,r,10,OverlayTheme.Ink,true,true); }
        internal static void Switch(Graphics g,Rectangle r,bool on,bool mixed=false) {
            Card(g,r,on||mixed?OverlayTheme.Accent:OverlayTheme.SurfaceCard,on||mixed?OverlayTheme.Accent:OverlayTheme.Border,r.Height/2);
            int d=r.Height-6;using(var b=new SolidBrush(on||mixed?OverlayTheme.Background:OverlayTheme.InkMuted))g.FillEllipse(b,on?r.Right-d-3:mixed?r.Left+(r.Width-d)/2:r.Left+3,r.Top+3,d,d);
        }
    }

    internal class FieldConsoleButton:Button {
        internal bool Primary=false;
        internal bool Selected;
        bool hover;
        internal FieldConsoleButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=OverlayTheme.UiFont(11,FontStyle.Bold);Height=40;Cursor=Cursors.Hand;BackColor=OverlayTheme.Background;
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e);hover=true;Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e);hover=false;Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?OverlayTheme.Background:Parent.BackColor);
            var r=new Rectangle(0,0,Width-1,Height-1);
            Color bg=Primary&&Enabled?(hover?OverlayTheme.AccentHover:OverlayTheme.Accent):Selected||hover?OverlayTheme.SurfaceCard:OverlayTheme.Surface;
            FieldConsolePaint.Card(e.Graphics,r,bg,Focused?OverlayTheme.BorderHighlight:Selected?OverlayTheme.Accent:OverlayTheme.Border,7);
            FieldConsolePaint.Text(e.Graphics,Text,r,11,!Enabled?OverlayTheme.InkMuted:Primary?OverlayTheme.Background:OverlayTheme.Ink,true,true);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(r,-4,-4),OverlayTheme.Ink,BackColor);
        }
    }

    internal sealed class FieldConsoleStepButton:FieldConsoleButton {
        internal int Step=1;
        internal bool Complete=false;
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(OverlayTheme.Surface);
            if(Selected)FieldConsolePaint.Card(e.Graphics,new Rectangle(0,0,Width-1,Height-1),OverlayTheme.SurfaceCard,OverlayTheme.SurfaceCard,7);
            var circle=new Rectangle(12,(Height-26)/2,26,26);
            if(Selected)using(var b=new SolidBrush(OverlayTheme.Accent))e.Graphics.FillEllipse(b,circle);
            else using(var pen=new Pen(Complete?OverlayTheme.Route:OverlayTheme.BorderHighlight))e.Graphics.DrawEllipse(pen,circle);
            FieldConsolePaint.Text(e.Graphics,Step.ToString(),circle,10,Selected?OverlayTheme.Background:Complete?OverlayTheme.Route:OverlayTheme.InkMuted,false,true);
            string label=System.Text.RegularExpressions.Regex.Replace(Text,@"^\s*\d+[.\s]+","");
            FieldConsolePaint.Text(e.Graphics,label,new Rectangle(50,0,Width-58,Height),12,Selected?OverlayTheme.Ink:OverlayTheme.InkMuted);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Width-4,Height-4),OverlayTheme.Ink,BackColor);
        }
    }

    internal static class FieldConsoleSettingsFrame {
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        internal static void Compose(Form form,Action dismiss) {
            form.FormBorderStyle=FormBorderStyle.None;form.Padding=new Padding(1);form.BackColor=OverlayTheme.Background;form.Font=OverlayTheme.UiFont(11);form.KeyPreview=true;
            var header=new Panel { Dock=DockStyle.Top,Height=64,Padding=new Padding(20,10,20,8),BackColor=OverlayTheme.Background };
            var title=new Label { Text=Localization.Get("Settings"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=OverlayTheme.UiFont(22,FontStyle.Bold),ForeColor=OverlayTheme.Ink };
            var version=new Label { Text="v"+MapWindow.VersionString+(Program.IsTestBuild?" TEST":""),Dock=DockStyle.Right,Width=100,TextAlign=ContentAlignment.MiddleRight,Font=OverlayTheme.UiFont(10),ForeColor=OverlayTheme.InkMuted };
            title.MouseDown+=(sender,args)=> { if(args.Button==MouseButtons.Left) { ReleaseCapture();SendMessage(form.Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
            header.Controls.Add(title);header.Controls.Add(version);form.Controls.Add(header);
            form.TextChanged+=(sender,args)=>title.Text=Localization.Get("Settings");
            form.Paint+=(sender,args)=> { using(var edge=new Pen(OverlayTheme.Border))args.Graphics.DrawRectangle(edge,0,0,form.ClientSize.Width-1,form.ClientSize.Height-1); };
            form.KeyDown+=(sender,args)=> { if(args.KeyCode==Keys.Escape) { dismiss();args.Handled=true;args.SuppressKeyPress=true; } };
        }
    }

    internal sealed class FieldConsoleNumpad:Control {
        internal int CopyKey;
        internal string Help;
        internal FieldConsoleNumpad() { DoubleBuffered=true;TabStop=false;BackColor=OverlayTheme.Background; }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            FieldConsolePaint.Card(g,new Rectangle(0,0,Width-1,Height-1),OverlayTheme.Surface,OverlayTheme.Border);
            string[] keys={"Num","/","*","−","7","8","9","+","4","5","6","1","2","3","Ent"};
            for(int i=0;i<keys.Length;i++) {
                int row=i<8?i/4:2+(i-8)/3,col=i<8?i%4:(i-8)%3;
                var r=new Rectangle(18+col*46,18+row*36,40,30);
                bool selected=CopyKey==0x6F&&keys[i]=="/";
                FieldConsolePaint.Card(g,r,selected?Color.FromArgb(47,37,20):OverlayTheme.SurfaceCard,selected?OverlayTheme.Accent:OverlayTheme.Border,5);
                FieldConsolePaint.Text(g,keys[i],r,11,selected?OverlayTheme.Accent:OverlayTheme.InkMuted,true,true);
            }
            FieldConsolePaint.Text(g,PhysicalKeyCapture.KeyName(CopyKey),new Rectangle(234,28,Width-250,30),13,OverlayTheme.Accent,true);
            using(var f=OverlayTheme.UiFont(11))using(var b=new SolidBrush(OverlayTheme.InkMuted))
                g.DrawString(Help,f,b,new RectangleF(234,68,Math.Max(80,Width-254),108));
        }
    }

    internal sealed class FieldConsoleSettingsView:UserControl {
        readonly FlowLayoutPanel[] pages=new FlowLayoutPanel[3];
        readonly FieldConsoleButton[] tabs=new FieldConsoleButton[3];
        readonly Action<int> onSelection;
        int selected;
        internal int SelectedIndex { get { return selected; } set { SelectPage(value); } }
        internal FieldConsoleSettingsView(IEnumerable<Control> controls,int initial,Action<int> changed) {
            Name="FieldConsoleSettingsView";BackColor=OverlayTheme.Background;onSelection=changed;AutoScaleMode=AutoScaleMode.None;
            var nav=new TableLayoutPanel { Dock=DockStyle.Top,Height=48,ColumnCount=3,RowCount=1,Padding=new Padding(0,0,0,8),BackColor=OverlayTheme.Background };
            string[] labels={Localization.Get("FieldGeneral"),Localization.Get("FieldShortcuts"),Localization.Get("FieldAppearance")};
            for(int i=0;i<3;i++) {
                int index=i;nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
                tabs[i]=new FieldConsoleButton { Text=labels[i],Dock=DockStyle.Fill,Margin=new Padding(0,0,i==2?0:4,0),AccessibleName=labels[i] };
                tabs[i].Click+=(sender,args)=>SelectPage(index);nav.Controls.Add(tabs[i],i,0);
                pages[i]=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,12,0,12),BackColor=OverlayTheme.Background };
                Controls.Add(pages[i]);
            }
            Controls.Add(nav);int group=0;
            foreach(var c in controls) {
                if((string)c.Tag=="section") {
                    string title=c.Text;
                    group=title==Localization.Get("ShortcutHeading")?1:(title==Localization.Get("SecMapZones")||title==Localization.Get("SecAppearance"))?2:0;
                    if(title!=Localization.Get("ShortcutHeading"))pages[group].Controls.Add(new Label { Text=title,Width=365,Height=34,ForeColor=OverlayTheme.InkMuted,Font=OverlayTheme.UiFont(11,FontStyle.Bold),Margin=new Padding(0,12,0,6) });
                    c.Dispose();continue;
                }
                if(c is Label&&c.Text==Localization.Get("ShortcutHeading")) { c.Dispose();continue; }
                c.Margin=new Padding(0,4,0,4);pages[group].Controls.Add(c);
            }
            SelectPage(initial);
        }
        void SelectPage(int index) {
            selected=Math.Max(0,Math.Min(2,index));
            for(int i=0;i<3;i++) { pages[i].Visible=i==selected;tabs[i].Selected=i==selected;tabs[i].Invalidate(); }
            if(onSelection!=null)onSelection(selected);
        }
    }

    internal static class FieldConsoleSearchView {
        internal static void Compose(Form form,TextBox input,Label hint,ListBox results,Label count,FlowLayoutPanel actions,Button go,Button clear,Button cancel) {
            form.Controls.Clear();form.Padding=new Padding(20);form.ClientSize=new Size(560,500);form.Font=OverlayTheme.UiFont(12);
            form.BackColor=OverlayTheme.Background;form.FormBorderStyle=FormBorderStyle.None;
            var query=new Panel { Dock=DockStyle.Top,Height=54,BackColor=OverlayTheme.Surface,Padding=new Padding(14,12,12,10) };
            input.BorderStyle=BorderStyle.None;input.Font=OverlayTheme.UiFont(14,FontStyle.Bold);input.Dock=DockStyle.Fill;input.BackColor=OverlayTheme.Surface;
            var escape=new FieldConsoleButton { Text="Esc",Dock=DockStyle.Right,Width=46,DialogResult=DialogResult.Cancel,AccessibleName=Localization.Get("Cancel") };
            query.Controls.Add(input);query.Controls.Add(escape);
            hint.Height=40;hint.ForeColor=OverlayTheme.InkMuted;hint.Font=OverlayTheme.UiFont(9);hint.Padding=new Padding(0,10,0,4);
            results.BorderStyle=BorderStyle.None;results.Font=OverlayTheme.UiFont(12);results.ItemHeight=54;results.BackColor=OverlayTheme.Surface;
            actions.Controls.Clear();actions.Height=60;actions.Padding=new Padding(0,12,0,0);actions.FlowDirection=FlowDirection.RightToLeft;
            go.Width=148;go.Height=42;clear.Width=148;clear.Height=42;cancel.Visible=false;
            actions.Controls.Add(go);actions.Controls.Add(clear);
            count.Dock=DockStyle.Left;count.Width=184;count.Height=42;count.Font=OverlayTheme.UiFont(10);count.ForeColor=OverlayTheme.InkMuted;
            var footer=new Panel { Dock=DockStyle.Bottom,Height=60,BackColor=OverlayTheme.Background };
            actions.Dock=DockStyle.Right;actions.Width=320;footer.Controls.Add(actions);footer.Controls.Add(count);
            form.Controls.Add(results);form.Controls.Add(hint);form.Controls.Add(query);form.Controls.Add(footer);
            form.KeyPreview=true;form.KeyDown+=(sender,args)=> { if(args.KeyCode==Keys.Escape) { form.DialogResult=DialogResult.Cancel;args.Handled=true;args.SuppressKeyPress=true; } };
            form.Paint+=(sender,args)=> { using(var pen=new Pen(OverlayTheme.Border))args.Graphics.DrawRectangle(pen,0,0,form.ClientSize.Width-1,form.ClientSize.Height-1); };
            go.BackColor=OverlayTheme.Accent;go.ForeColor=OverlayTheme.Background;
        }
    }

    public sealed partial class MapWindow {
        bool fieldConsoleChromePress;
        Rectangle fieldConsoleTargetCard;
        string FieldConsoleRouteSummary() {
            if(searchTarget==null)return "";
            if(position==null)return Localization.Get("WaitingForCoordinates");
            bool road=activeRoute!=null&&activeRoute.Success;
            double distance=road?activeRoute.TotalDistanceMeters:DestinationSearch.Metres(ToMap(position),searchTarget.Centroid);
            return WpfTravelSummary(distance,road,routeCalculating,road?(double?)activeRoute.EstimatedSeconds:null);
        }
        internal string WpfTravelSummary(double distance,bool road,bool calculating=false,double? estimatedSeconds=null) {
            double speed=filteredSpeed>1.2?filteredSpeed:13.9;
            int minutes=Math.Max(1,(int)Math.Ceiling((road&&estimatedSeconds.HasValue?estimatedSeconds.Value:distance/Math.Max(.5,speed))/60));
            return DestinationSearch.Distance(distance)+(road?Localization.Get("RoadSuffix"):" ("+Localization.Get("FieldDirect")+")")+" · "+Localization.Get("NavigationEta")+" "+Localization.T("FieldMinutes",minutes)+(calculating?" · "+Localization.Get("FieldCalculating"):road?"":" · "+Localization.Get("FieldNoRoad"));
        }
        void DrawFieldConsoleSidebar(Graphics g,int x,int w,int h) {
            sidebarClickables.Clear();sidebarOpacityTrackRect=sidebarZoomInRect=sidebarZoomOutRect=sidebarZoomResetRect=sidebarClearWaypointRect=Rectangle.Empty;
            using(var bg=new SolidBrush(OverlayTheme.Surface))g.FillRectangle(bg,x,0,w,h);
            using(var edge=new Pen(OverlayTheme.Border))g.DrawLine(edge,x,0,x,h);
            int left=x+24,width=Math.Max(100,w-48);
            FieldConsolePaint.Text(g,Localization.Get("SidebarTitle"),new Rectangle(left,20,width-46,40),19,OverlayTheme.Ink,true);
            FieldConsolePaint.Key(g,new Rectangle(x+w-62,24,38,34),PhysicalKeyCapture.KeyName(scumMapKey));
            sidebarSearchRect=new Rectangle(left,80,width,44);
            FieldConsolePaint.Card(g,sidebarSearchRect,OverlayTheme.Background,OverlayTheme.Border,7);
            FieldConsolePaint.Text(g,Localization.Get("SearchTitle"),new Rectangle(left+14,82,width-75,40),12,OverlayTheme.InkMuted);
            FieldConsolePaint.Key(g,new Rectangle(left+width-49,89,36,26),PhysicalKeyCapture.KeyName(searchShortcutKey));
            Rectangle viewport=new Rectangle(left,144,width,h-156);var state=g.Save();g.SetClip(viewport);
            int y=144-sidebarScrollY;
            FieldConsolePaint.Text(g,Localization.Get("SidebarLayers"),new Rectangle(left,y,width,26),9,OverlayTheme.InkMuted,true);y+=32;
            FieldConsoleLayer(g,left,width,ref y,Localization.Get("ShowSavedZones"),showZones,viewport,()=>showZones=!showZones);
            FieldConsoleLayer(g,left,width,ref y,Localization.Get("ShowCustomWaypoints"),showCustomWaypoints,viewport,()=>showCustomWaypoints=!showCustomWaypoints);
            FieldConsoleLayer(g,left,width,ref y,Localization.Get("GridBorders"),gridBorders,viewport,()=>gridBorders=!gridBorders);
            FieldConsoleLayer(g,left,width,ref y,Localization.Get("HistoryEnabled"),locationHistory,viewport,()=>locationHistory=!locationHistory);
            FieldConsoleLayer(g,left,width,ref y,Localization.Get("ShowZoneLabels"),showZoneLabels,viewport,()=>showZoneLabels=!showZoneLabels);
            y+=26;FieldConsolePaint.Text(g,Localization.Get("SidebarCategories"),new Rectangle(left,y,width-98,32),9,OverlayTheme.InkMuted,true);
            var all=new Rectangle(left+width-150,y,72,30);var off=new Rectangle(left+width-72,y,72,30);
            FieldConsolePaint.Key(g,all,Localization.Get("SelectAll"));FieldConsolePaint.Key(g,off,Localization.Get("DeselectAll"));
            RegisterClickable(all,viewport,()=>FieldConsoleFilterAll(true));RegisterClickable(off,viewport,()=>FieldConsoleFilterAll(false));y+=42;
            if(scumMap!=null)foreach(string section in scumMap.Sections) {
                var cats=scumMap.CategoriesBySection[section];int enabled=cats.Count(c=>scumMap.IsCategoryEnabled(c.Id));bool expanded=sidebarExpandedSections.Contains(section);
                var row=new Rectangle(left,y,width,40);
                FieldConsolePaint.Text(g,expanded?"⌄":"›",new Rectangle(left,y,18,40),14,OverlayTheme.InkMuted);
                FieldConsolePaint.Text(g,Localization.GetSectionName(section),new Rectangle(left+24,y,width-84,40),12,OverlayTheme.Ink,true);
                FieldConsolePaint.Text(g,enabled+" / "+cats.Count,new Rectangle(left+width-58,y,58,40),10,OverlayTheme.InkMuted,false,true);
                string captured=section;RegisterClickable(row,viewport,()=> { if(sidebarExpandedSections.Contains(captured))sidebarExpandedSections.Remove(captured);else sidebarExpandedSections.Add(captured);FieldConsoleChanged(); });y+=40;
                if(expanded)foreach(var category in cats.OrderBy(c=>c.Name)) {
                    var item=new Rectangle(left,y,width,38);bool on=scumMap.IsCategoryEnabled(category.Id);
                    using(var b=new SolidBrush(Color.FromArgb(category.ColorIcon)))g.FillRectangle(b,left+2,y+16,8,8);
                    FieldConsolePaint.Text(g,Localization.GetCategoryName(category.Name),new Rectangle(left+20,y,width-104,38),11,OverlayTheme.Ink);
                    FieldConsolePaint.Text(g,category.MarkerCount.ToString(CultureInfo.InvariantCulture),new Rectangle(left+width-82,y,34,38),9,OverlayTheme.InkMuted,false,true);
                    FieldConsolePaint.Switch(g,new Rectangle(left+width-40,y+9,38,20),on);
                    int id=category.Id;RegisterClickable(item,viewport,()=> { scumMap.SetCategoryEnabled(id,!scumMap.IsCategoryEnabled(id));if(!scumMap.MasterEnabled) { scumMap.MasterEnabled=true;showScumMap=true; }FieldConsoleChanged(); });y+=38;
                }
                using(var pen=new Pen(OverlayTheme.Border))g.DrawLine(pen,left,y+3,left+width,y+3);y+=10;
            }
            // Existing specialized tools remain accessible in the new scene.
            y+=12;var tools=new Rectangle(left,y,width,38);FieldConsolePaint.Key(g,tools,Localization.Get("SidebarZoneEditor"));RegisterClickable(tools,viewport,()=>OpenZoneImport(null));y+=48;
            var deaths=new Rectangle(left,y,width,38);FieldConsolePaint.Key(g,deaths,Localization.Get("ClearDeathMarkers"));if(zones.Any(marker=>marker.IsDeathMarker))RegisterClickable(deaths,viewport,RemoveDeathMarkersFromSidebar);y+=48;
            var settings=new Rectangle(left,y,width,38);FieldConsolePaint.Key(g,settings,Localization.Get("Settings"));RegisterClickable(settings,viewport,()=>ShowSettings());y+=48;
            int total=y+sidebarScrollY-viewport.Top;sidebarMaxScroll=Math.Max(0,total-viewport.Height+12);sidebarScrollY=Math.Max(0,Math.Min(sidebarScrollY,sidebarMaxScroll));g.Restore(state);
            sidebarScrollbarTrackRect=sidebarScrollbarThumbRect=Rectangle.Empty;
            if(sidebarMaxScroll>0) {
                sidebarScrollbarTrackRect=new Rectangle(x+w-8,viewport.Top,4,viewport.Height);
                int thumbH=Math.Max(32,viewport.Height*viewport.Height/Math.Max(1,total));
                sidebarScrollbarThumbRect=new Rectangle(x+w-8,viewport.Top+sidebarScrollY*(viewport.Height-thumbH)/sidebarMaxScroll,4,thumbH);
                using(var b=new SolidBrush(OverlayTheme.Border))g.FillRectangle(b,sidebarScrollbarTrackRect);
                using(var b=new SolidBrush(OverlayTheme.InkMuted))g.FillRectangle(b,sidebarScrollbarThumbRect);
            }
        }
        void FieldConsoleLayer(Graphics g,int x,int w,ref int y,string label,bool value,Rectangle viewport,Action toggle) {
            var row=new Rectangle(x,y,w,40);FieldConsolePaint.Text(g,label,new Rectangle(x,y,w-54,40),12,OverlayTheme.Ink);
            FieldConsolePaint.Switch(g,new Rectangle(x+w-40,y+10,38,20),value);
            RegisterClickable(row,viewport,()=> { toggle();FieldConsoleChanged(); });y+=42;
        }
        void FieldConsoleChanged() { SettingsChanged();lastFrameKey=null;terrainKey=null;RenderOverlay(); }
        void FieldConsoleFilterAll(bool on) { if(scumMap==null)return;scumMap.SetAllEnabled(on);if(on) { scumMap.MasterEnabled=true;showScumMap=true; }FieldConsoleChanged(); }
        void DrawFieldConsoleMapChrome(Graphics g,int mapWidth,int h) {
            fieldConsoleTargetCard=Rectangle.Empty;sidebarClearWaypointRect=Rectangle.Empty;
            if(searchTarget!=null) {
                int width=Math.Min(440,mapWidth-48);fieldConsoleTargetCard=new Rectangle(24,24,width,106);
                FieldConsolePaint.Card(g,fieldConsoleTargetCard,OverlayTheme.Surface,OverlayTheme.Border,9);
                using(var b=new SolidBrush(routeGuidanceColor))g.FillEllipse(b,42,67,11,11);
                FieldConsolePaint.Text(g,Localization.Get("FieldTarget"),new Rectangle(66,32,width-152,20),9,OverlayTheme.InkMuted,true);
                FieldConsolePaint.Text(g,searchTarget.DisplayName,new Rectangle(66,54,width-156,28),17,OverlayTheme.Ink,true);
                FieldConsolePaint.Text(g,FieldConsoleRouteSummary(),new Rectangle(66,86,width-80,24),10,OverlayTheme.InkMuted);
                sidebarClearWaypointRect=new Rectangle(24+width-80,49,62,38);FieldConsolePaint.Key(g,sidebarClearWaypointRect,Localization.Get("ClearWaypoint"));
            }
            var chrome=new Rectangle(24,h-76,Math.Min(390,mapWidth-48),52);FieldConsolePaint.Card(g,chrome,OverlayTheme.Surface,OverlayTheme.Border);
            sidebarZoomOutRect=new Rectangle(chrome.Left+10,chrome.Top+9,32,34);sidebarZoomResetRect=new Rectangle(chrome.Left+46,chrome.Top+9,58,34);sidebarZoomInRect=new Rectangle(chrome.Left+108,chrome.Top+9,32,34);
            FieldConsolePaint.Text(g,"−",sidebarZoomOutRect,18,OverlayTheme.Ink,false,true);FieldConsolePaint.Text(g,fullMapZoom.ToString("0.0",CultureInfo.InvariantCulture)+"×",sidebarZoomResetRect,12,OverlayTheme.Ink,true,true);FieldConsolePaint.Text(g,"+",sidebarZoomInRect,18,OverlayTheme.Ink,false,true);
            FieldConsolePaint.Text(g,Localization.Get("OpacityLabel"),new Rectangle(chrome.Left+150,chrome.Top+6,100,38),9,OverlayTheme.InkMuted,true);
            sidebarOpacityTrackRect=new Rectangle(chrome.Left+256,chrome.Top+24,Math.Max(24,chrome.Width-278),4);
            using(var pen=new Pen(OverlayTheme.Border,4))g.DrawLine(pen,sidebarOpacityTrackRect.Left,sidebarOpacityTrackRect.Top,sidebarOpacityTrackRect.Right,sidebarOpacityTrackRect.Top);
            float fraction=(EffectiveFullMapOpacity-20)/80f;using(var pen=new Pen(OverlayTheme.Accent,4))g.DrawLine(pen,sidebarOpacityTrackRect.Left,sidebarOpacityTrackRect.Top,sidebarOpacityTrackRect.Left+sidebarOpacityTrackRect.Width*fraction,sidebarOpacityTrackRect.Top);
        }
        bool HandleFieldConsoleChromeDown(Point point,MouseButtons button) {
            if(button!=MouseButtons.Left)return false;
            if(sidebarClearWaypointRect.Contains(point)) { fieldConsoleChromePress=true;ClearActiveWaypoint();return true; }
            if(sidebarZoomInRect.Contains(point)) { fieldConsoleChromePress=true;TriggerZoomIn();return true; }
            if(sidebarZoomOutRect.Contains(point)) { fieldConsoleChromePress=true;TriggerZoomOut();return true; }
            if(sidebarZoomResetRect.Contains(point)) { fieldConsoleChromePress=true;ResetMapZoom();return true; }
            if(Rectangle.Inflate(sidebarOpacityTrackRect,8,15).Contains(point)) { draggingSlider=true;UpdateOpacityFromSidebarMouse(point.X);return true; }
            if(fieldConsoleTargetCard.Contains(point)) { fieldConsoleChromePress=true;return true; }
            return false;
        }
        void DrawFieldConsoleHud(Graphics g,Bitmap frame,bool above,int height) {
            int y=above?0:frame.Height-height;
            var card=new Rectangle(6,y+2,frame.Width-12,height-5);FieldConsolePaint.Card(g,card,OverlayTheme.Surface,OverlayTheme.Border,7);
            string location=GetLocationDescription();int trip=location.LastIndexOf("  |  -> ",StringComparison.Ordinal);if(trip>=0)location=location.Substring(0,trip);
            using(var font=OverlayTheme.UiFont(10,FontStyle.Bold))using(var ink=new SolidBrush(OverlayTheme.Ink))
                DrawScrollingInfobarText(g,location,font,ink,new RectangleF(18,y+4,frame.Width-36,22));
            if(searchTarget!=null)FieldConsolePaint.Text(g,searchTarget.DisplayName+" · "+FieldConsoleRouteSummary(),new Rectangle(18,y+26,frame.Width-36,22),9,routeGuidanceColor);
            else FieldConsolePaint.Text(g,note,new Rectangle(18,y+26,frame.Width-36,22),9,OverlayTheme.InkMuted);
        }
    }
}
