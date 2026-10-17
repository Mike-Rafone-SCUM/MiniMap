using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    // WPF owns layout, composition, text, input controls and both map views.
    // The existing native controller is a service behind this presentation engine.
    internal static class WpfFieldTheme {
        internal static readonly Brush Ground=ColorBrush("#0E1114"),Panel=ColorBrush("#151A1F"),Raised=ColorBrush("#1D242B"),Line=ColorBrush("#2A333C"),Ink=ColorBrush("#E9EEF2"),Muted=ColorBrush("#A4B1BC"),Amber=ColorBrush("#FFB547"),Cyan=ColorBrush("#35D6C8");
        internal static Brush ColorBrush(string hex) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b; }
        internal static TextBlock Text(string text,double size=15,Brush ink=null,bool bold=false) { return new TextBlock { Text=text,FontSize=size,Foreground=ink??Ink,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis }; }
        internal static Button Button(string label,Action action,bool primary=false) { var b=new Button { Content=label,MinHeight=40,Padding=new Thickness(16,8,16,8),Margin=new Thickness(4,0,0,0),Background=primary?Amber:Panel,Foreground=primary?Ground:Ink };b.Click+=(s,e)=>action();return b; }
        internal static Border Card(UIElement content,Thickness? padding=null) { return new Border { Background=Panel,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=padding??new Thickness(16),Child=content }; }
        internal static ResourceDictionary Resources() { return (ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='BorderBrush' Value='#2A333C'/><Setter Property='BorderThickness' Value='1'/><Setter Property='FontSize' Value='15'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='body' CornerRadius='6' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='Opacity' Value='.85'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='#A4B1BC'/><Setter TargetName='body' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='body' Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type TextBox}'><Setter Property='Foreground' Value='#E9EEF2'/><Setter Property='Background' Value='#0E1114'/><Setter Property='BorderBrush' Value='#2A333C'/><Setter Property='BorderThickness' Value='1'/><Setter Property='CaretBrush' Value='#FFB547'/><Setter Property='Padding' Value='12,9'/><Setter Property='FontSize' Value='17'/></Style>
<Style TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='#E9EEF2'/><Setter Property='FontSize' Value='16'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Grid MinHeight='40'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='42'/></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment='Center'/><Border x:Name='pill' Grid.Column='1' Height='22' CornerRadius='11' Background='#2A333C' VerticalAlignment='Center'><Ellipse x:Name='knob' Width='16' Height='16' Margin='3' Fill='#A4B1BC' HorizontalAlignment='Left'/></Border></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='pill' Property='Background' Value='#FFB547'/><Setter TargetName='knob' Property='Fill' Value='#0E1114'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='pill' Property='BorderBrush' Value='#E9EEF2'/><Setter TargetName='pill' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ListBox}'><Setter Property='Background' Value='#151A1F'/><Setter Property='Foreground' Value='#E9EEF2'/><Setter Property='BorderThickness' Value='0'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/></Style>
<Style TargetType='{x:Type ListBoxItem}'><Setter Property='Padding' Value='12,10'/><Setter Property='Margin' Value='0,2'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='row' CornerRadius='6' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='#1D242B'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='BorderBrush' Value='#A4B1BC'/><Setter TargetName='row' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBox}'><Setter Property='FontSize' Value='15'/><Setter Property='MinHeight' Value='36'/><Setter Property='Foreground' Value='#E9EEF2'/><Setter Property='Background' Value='#1D242B'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'><Grid><Border Background='#1D242B' BorderBrush='#2A333C' BorderThickness='1' CornerRadius='6'/><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='Transparent' Padding='12,8'><TextBlock Text='⌄' Foreground='#A4B1BC' HorizontalAlignment='Right'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='12,6,32,6' VerticalAlignment='Center' IsHitTestVisible='False'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='#1D242B' BorderBrush='#7E8F9F' BorderThickness='1' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='260'><ScrollViewer><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type Slider}'><Setter Property='MinHeight' Value='28'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Slider}'><Grid><Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' IsDirectionReversed='{TemplateBinding IsDirectionReversed}'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge'><RepeatButton.Template><ControlTemplate><Border Height='4' CornerRadius='2' Background='#FFB547'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge'><RepeatButton.Template><ControlTemplate><Border Height='4' CornerRadius='2' Background='#2A333C'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width='12' Height='12'><Thumb.Template><ControlTemplate><Ellipse Fill='#FFB547' Stroke='#0E1114' StrokeThickness='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='10'/><Setter Property='Background' Value='#151A1F'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='#151A1F'><Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate><Border Background='#64717C' CornerRadius='4' Margin='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>"); }
    }

    internal sealed class WpfSatnavTicker:Grid {
        readonly TextBlock label;
        readonly TranslateTransform scroll=new TranslateTransform();
        double lastOverflow=-1;
        static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(WpfSatnavTicker),new PropertyMetadata(0.0,(s,e)=> { var ticker=(WpfSatnavTicker)s;ticker.scroll.X=-Math.Max(0,ticker.lastOverflow)*(double)e.NewValue; }));
        internal WpfSatnavTicker() {
            ClipToBounds=true;Height=20;var canvas=new Canvas();label=WpfFieldTheme.Text("",14,WpfFieldTheme.Cyan);label.TextTrimming=TextTrimming.None;label.RenderTransform=scroll;canvas.Children.Add(label);Children.Add(canvas);
            SizeChanged+=(s,e)=>UpdateScroll();IsVisibleChanged+=(s,e)=>UpdateScroll();Loaded+=(s,e)=>UpdateScroll();Unloaded+=(s,e)=>BeginAnimation(ProgressProperty,null);
        }
        internal string Text { get { return label.Text; } set { if(label.Text==value)return;label.Text=value??"";ToolTip=label.Text;UpdateScroll(); } }
        internal Brush Foreground { get { return label.Foreground; } set { label.Foreground=value; } }
        internal double Overflow { get { label.Measure(new Size(double.PositiveInfinity,20));return Math.Max(0,label.DesiredSize.Width-ActualWidth); } }
        void UpdateScroll() {
            double overflow=Overflow;
            if(!IsVisible||ActualWidth<=0||overflow<=1) { BeginAnimation(ProgressProperty,null);scroll.X=0;lastOverflow=-1;return; }
            lastOverflow=overflow;scroll.X=-overflow*(double)GetValue(ProgressProperty);
            if(HasAnimatedProperties)return;
            double travel=Math.Max(2,overflow/28);
            var animation=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(travel+2),AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel+1))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel+2))));
            BeginAnimation(ProgressProperty,animation);
        }
    }
    internal sealed class WpfZoneCanvas:FrameworkElement {
        readonly ZoneEditor editor;readonly bool source;
        BitmapSource frame;bool dirty=true;int clickCount;
        internal WpfZoneCanvas(ZoneEditor editor,bool source) {
            this.editor=editor;this.source=source;ClipToBounds=true;Focusable=true;Cursor=Cursors.Cross;
            SizeChanged+=(s,e)=>Redraw();
            MouseDown+=(s,e)=> { Focus();clickCount=e.ClickCount;if(e.ChangedButton==MouseButton.Left&&e.ClickCount==2) { Send("double",e.GetPosition(this),System.Windows.Forms.MouseButtons.Left,0,2);e.Handled=true;return; }Send("down",e.GetPosition(this),Button(e.ChangedButton));CaptureMouse();e.Handled=true; };
            MouseMove+=(s,e)=> { if(IsMouseCaptured)Send("move",e.GetPosition(this),e.MiddleButton==MouseButtonState.Pressed?System.Windows.Forms.MouseButtons.Middle:System.Windows.Forms.MouseButtons.Left); };
            MouseUp+=(s,e)=> { var button=Button(e.ChangedButton);Send("up",e.GetPosition(this),button);if(clickCount<2)Send("click",e.GetPosition(this),button);ReleaseMouseCapture();e.Handled=true; };
            MouseWheel+=(s,e)=> { Send("wheel",e.GetPosition(this),System.Windows.Forms.MouseButtons.None,e.Delta);e.Handled=true; };
            LostMouseCapture+=(s,e)=>editor.WpfCancelPointer();
        }
        static System.Windows.Forms.MouseButtons Button(MouseButton button) { return button==MouseButton.Middle?System.Windows.Forms.MouseButtons.Middle:button==MouseButton.Right?System.Windows.Forms.MouseButtons.Right:System.Windows.Forms.MouseButtons.Left; }
        void Send(string kind,Point at,System.Windows.Forms.MouseButtons button,int delta=0,int clicks=1) { editor.WpfPointer(source,kind,button,(int)at.X,(int)at.Y,delta,clicks);Redraw(); }
        internal void Redraw() { dirty=true;InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);if(ActualWidth<1||ActualHeight<1||editor.IsDisposed)return;
            if(dirty) {
                using(var bitmap=editor.WpfRender(source,(int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight))) {
                    var bits=bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    try { frame=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Pbgra32,null,bits.Scan0,bits.Stride*bitmap.Height,bits.Stride);frame.Freeze(); }finally { bitmap.UnlockBits(bits); }
                }dirty=false;
            }
            if(frame!=null)dc.DrawImage(frame,new Rect(0,0,ActualWidth,ActualHeight));
        }
    }
    internal sealed class WpfWindowOwner:System.Windows.Forms.IWin32Window { public IntPtr Handle { get;private set; }internal WpfWindowOwner(IntPtr handle) { Handle=handle; } }
    internal sealed class WpfMapLabel { internal string Text;internal Point At;internal bool Major; }
    internal sealed class WpfPoiCluster {
        internal DrawingPoint Point;
        internal readonly List<ScumMapMarker> Members=new List<ScumMapMarker>();
        internal double Radius;
        internal bool Mixed;
        internal DrawingGroup Icon;
        internal int IconColor;
        internal Pen Outline;
    }
    internal sealed class WpfPoiCache {
        internal bool Clustering;
        internal double Side;
        internal double Spacing;
        internal float Zoom;
        internal string Filter;
        internal int Count;
        internal WpfPoiCluster[] Clusters;
        internal ScumMapMarker[] OrderedMarkers;
        internal Dictionary<ScumMapMarker,string> Labels;
        internal string LabelLanguage;
    }
    internal sealed class WpfMapSurface:FrameworkElement {
        // Layered windows pass fully transparent pixels to the game before WPF
        // hit testing runs. One alpha step keeps the tactical map clickable at 0%.
        static readonly Brush tacticalHitSurface=WpfFieldTheme.ColorBrush("#01000000");
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[],StreamGeometry> zoneGeometryCache=new System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[],StreamGeometry>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[][],StreamGeometry> habitatGeometryCache=new System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[][],StreamGeometry>();
        sealed class GeometryBounds { internal Rect Value;internal GeometryBounds(StreamGeometry geometry) { Value=geometry.Bounds; } }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StreamGeometry,GeometryBounds> geometryBoundsCache=new System.Runtime.CompilerServices.ConditionalWeakTable<StreamGeometry,GeometryBounds>();
        static StreamGeometry WorldGeometry(DrawingPoint[][] rings) {
            var geometry=new StreamGeometry { FillRule=FillRule.Nonzero };using(var context=geometry.Open())foreach(var ring in rings) { if(ring==null||ring.Length<3)continue;context.BeginFigure(new Point(ring[0].X,ring[0].Y),true,true);context.PolyLineTo(ring.Skip(1).Select(p=>new Point(p.X,p.Y)).ToArray(),true,false); }geometry.Freeze();return geometry;
        }
        internal WpfMapState State;
        internal bool Compact;
        sealed class PoiStamp { internal Point At;internal double Radius,Scale;internal DrawingGroup Icon;internal Pen Outline; }
        static readonly System.Threading.SemaphoreSlim poiBuildSlots=new System.Threading.SemaphoreSlim(1);
        System.Threading.CancellationTokenSource poiBuild;
        WpfPoiCluster[] requestedPoiLayout,drawnPoiLayout;
        double requestedPoiSide,drawnPoiSide;
        DrawingGroup poiScene;
        long poiGeneration;
        internal int PoiSceneBuilds;
        internal bool PoiSceneRasterized;
        static void PostPoiScene(Dispatcher dispatcher,Action apply) { if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(apply,DispatcherPriority.Background); }
        void DrawRetainedPois(DrawingContext dc,double side,double left,double top,double width,double height) {
            Dispatcher.VerifyAccess();
            var layout=PoiClusters(State,width,height,false);
            if(layout.Length<=256) {
                foreach(var cluster in layout) { ScumMapCategory category;State.Store.CategoryById.TryGetValue(cluster.Members[0].CategoryId,out category);DrawPoi(dc,new Point(left+cluster.Point.X*side,top+cluster.Point.Y*side),category,cluster); }return;
            }
            if(!object.ReferenceEquals(layout,requestedPoiLayout)||side!=requestedPoiSide) {
                requestedPoiLayout=layout;requestedPoiSide=side;
                if(poiBuild!=null)poiBuild.Cancel();var cancel=new System.Threading.CancellationTokenSource();poiBuild=cancel;
                long generation=System.Threading.Interlocked.Increment(ref poiGeneration);
                var dispatcher=Dispatcher;
                double pixelSide=side*VisualTreeHelper.GetDpi(this).DpiScaleX;
                var stamps=new PoiStamp[layout.Length];
                for(int i=0;i<layout.Length;i++) {
                    var cluster=layout[i];ScumMapCategory category;State.Store.CategoryById.TryGetValue(cluster.Members[0].CategoryId,out category);EnsurePoiAssets(category,cluster);
                    stamps[i]=new PoiStamp { At=new Point(cluster.Point.X*side,cluster.Point.Y*side),Radius=cluster.Radius,Scale=(cluster.Members.Count>1?11:cluster.Radius*1.45)/12,Icon=cluster.Icon,Outline=cluster.Outline };
                }
                // The worker receives copied coordinates and frozen assets only.
                // It never reads controls, State, live clusters or mutable stores.
                System.Threading.Tasks.Task.Run(async()=> {
                    bool entered=false;System.Threading.Thread workerThread=null;System.Threading.ThreadPriority priority=System.Threading.ThreadPriority.Normal;try {
                        await poiBuildSlots.WaitAsync(cancel.Token);entered=true;cancel.Token.ThrowIfCancellationRequested();
                        workerThread=System.Threading.Thread.CurrentThread;priority=workerThread.Priority;workerThread.Priority=System.Threading.ThreadPriority.BelowNormal;
                        var drawing=new DrawingGroup();using(var context=drawing.Open())for(int i=0;i<stamps.Length;i++) {
                            if((i&127)==0)cancel.Token.ThrowIfCancellationRequested();var stamp=stamps[i];
                            context.DrawEllipse(WpfFieldTheme.Ground,stamp.Outline,stamp.At,stamp.Radius,stamp.Radius);
                            context.PushTransform(new MatrixTransform(stamp.Scale,0,0,stamp.Scale,stamp.At.X,stamp.At.Y));context.DrawDrawing(stamp.Icon);context.Pop();
                        }
                        drawing.Freeze();cancel.Token.ThrowIfCancellationRequested();
                        // Freeze a bounded raster once, rather than asking composition
                        // to replay thousands of icon drawings for each position/click.
                        // Large zoom levels retain vectors instead of allocating huge images.
                        bool rasterized=pixelSide<=4096;
                        if(rasterized) {
                            int pixels=Math.Max(1,(int)Math.Ceiling(pixelSide));
                            var visual=new DrawingVisual();using(var context=visual.RenderOpen()) { context.PushTransform(new ScaleTransform(pixels/side,pixels/side));context.DrawDrawing(drawing);context.Pop(); }
                            var bitmap=new RenderTargetBitmap(pixels,pixels,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
                            var cached=new DrawingGroup();using(var context=cached.Open())context.DrawImage(bitmap,new Rect(0,0,side,side));cached.Freeze();drawing=cached;
                        }
                        cancel.Token.ThrowIfCancellationRequested();
                        var completed=drawing;
                        PostPoiScene(dispatcher,()=> { if(generation!=poiGeneration)return;poiScene=completed;PoiSceneRasterized=rasterized;drawnPoiLayout=layout;drawnPoiSide=side;PoiSceneBuilds++;InvalidateVisual(); });
                    }catch(OperationCanceledException) { }
                    catch(Exception error) { Program.LogException("PoiScene",error); }
                    finally { if(workerThread!=null)workerThread.Priority=priority;if(entered)poiBuildSlots.Release(); }
                });
            }
            if(poiScene!=null&&object.ReferenceEquals(drawnPoiLayout,layout)) {
                dc.PushTransform(new TranslateTransform(left,top));double scale=side/drawnPoiSide;dc.PushTransform(new ScaleTransform(scale,scale));dc.DrawDrawing(poiScene);dc.Pop();dc.Pop();
            }
        }
        double renderRotation;
        internal static double Rotation(WpfMapState state,bool compact) { return compact&&state.RotateMap&&state.Position!=null?90-state.Position.Yaw:0; }
        internal static Point RotatePoint(Point p,double angle,double width,double height) {
            double radians=angle*Math.PI/180,c=Math.Cos(radians),s=Math.Sin(radians),x=p.X-width/2,y=p.Y-height/2;
            return new Point(width/2+x*c-y*s,height/2+x*s+y*c);
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScumMapStore,WpfPoiCache[]> poiCaches=new System.Runtime.CompilerServices.ConditionalWeakTable<ScumMapStore,WpfPoiCache[]>();
        sealed class PoiCellComparer:IEqualityComparer<Point> {
            public bool Equals(Point a,Point b) { return a.X==b.X&&a.Y==b.Y; }
            public int GetHashCode(Point point) { unchecked { return (int)point.X*73856093^(int)point.Y*19349663; } }
        }
        static readonly PoiCellComparer poiCellComparer=new PoiCellComparer();
        internal static double PoiRadius(double zoom,bool compact) { return Math.Min(compact?7.5:9, (compact?4.5:5.5)+Math.Log(Math.Max(1,zoom),2)*.8); }
        internal static WpfPoiCluster[] PoiClusters(WpfMapState state,double width,double height,bool compact) {
            if(state==null||state.Store==null||!state.Store.MasterEnabled||width<=0||height<=0)return new WpfPoiCluster[0];
            double side=MapSide(state,width,height,compact),spacing=Math.Max(compact?36:42,Math.Min(compact?64:80,Math.Min(width,height)/(compact?5:12)));
            var cache=poiCaches.GetValue(state.Store,store=>new[]{new WpfPoiCache(),new WpfPoiCache()})[compact?0:1];
            string filter=state.Store.GetFilterHashKey();
            if(!state.PoiClustering&&cache.Clusters!=null&&!cache.Clustering&&cache.Filter==filter&&cache.Count==state.Store.Markers.Count) {
                double radius=PoiRadius(state.Zoom,compact);
                if(cache.Clusters.Length>0&&cache.Clusters[0].Radius!=radius)foreach(var cluster in cache.Clusters)cluster.Radius=radius;
                cache.Zoom=state.Zoom;cache.Side=side;cache.Spacing=spacing;
                return cache.Clusters;
            }
            if(cache.Clusters!=null&&cache.Clustering==state.PoiClustering&&cache.Side==side&&cache.Spacing==spacing&&cache.Zoom==state.Zoom&&cache.Filter==filter&&cache.Count==state.Store.Markers.Count)return cache.Clusters;
            var bins=new Dictionary<Point,List<WpfPoiCluster>>(poiCellComparer);var clusters=new List<WpfPoiCluster>();
            if(cache.OrderedMarkers==null||cache.Filter!=filter||cache.Count!=state.Store.Markers.Count) {
                cache.Labels=null;
                var priorities=state.Store.CategoryById.ToDictionary(entry=>entry.Key,entry=> { string symbol=PoiSymbol(entry.Value);return symbol=="$"?0:symbol=="B"?1:symbol=="⌂"?2:3; });
                cache.OrderedMarkers=state.Store.Markers.Where(m=>state.Store.IsCategoryEnabled(m.CategoryId)).OrderBy(m=>priorities.ContainsKey(m.CategoryId)?priorities[m.CategoryId]:3).ThenBy(m=>m.X).ThenBy(m=>m.Y).ThenBy(m=>m.Id).ToArray();
            }
            foreach(var marker in cache.OrderedMarkers) {
                if(!state.PoiClustering) { var single=new WpfPoiCluster { Point=marker.Point };single.Members.Add(marker);clusters.Add(single);continue; }
                double x=marker.X*side,y=marker.Y*side;int bx=(int)Math.Floor(x/spacing),by=(int)Math.Floor(y/spacing);WpfPoiCluster nearest=null;double distance=spacing*spacing;
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++) {
                    List<WpfPoiCluster> candidates;if(!bins.TryGetValue(new Point(bx+dx,by+dy),out candidates))continue;
                    foreach(var candidate in candidates) { double cx=x-candidate.Point.X*side,cy=y-candidate.Point.Y*side,d=cx*cx+cy*cy;if(d<distance) { nearest=candidate;distance=d; } }
                }
                if(nearest==null) { nearest=new WpfPoiCluster { Point=marker.Point };clusters.Add(nearest);var cell=new Point(bx,by);List<WpfPoiCluster> bucket;if(!bins.TryGetValue(cell,out bucket))bins.Add(cell,bucket=new List<WpfPoiCluster>());bucket.Add(nearest); }
                nearest.Members.Add(marker);
            }
            foreach(var cluster in clusters) { cluster.Radius=cluster.Members.Count==1?PoiRadius(state.Zoom,compact):Math.Min(compact?10:13,(compact?7.5:9.5)+Math.Log10(cluster.Members.Count)*1.5);cluster.Mixed=cluster.Members.Any(member=>member.CategoryId!=cluster.Members[0].CategoryId); }
            cache.Clustering=state.PoiClustering;cache.Side=side;cache.Spacing=spacing;cache.Zoom=state.Zoom;cache.Filter=filter;cache.Count=state.Store.Markers.Count;cache.Clusters=clusters.ToArray();return cache.Clusters;
        }
        internal DrawingGroup BaseMap;
        internal WpfTerrainTiles Terrain;
        internal Action<DrawingPoint> PanChanged;
        internal Action ClearRouteRequested;
        internal Action<MapZone> DeleteWaypointRequested;
        internal Action<string> HoverChanged;
        internal void ClearHover() { ToolTip=null;hoverText=null;if(HoverChanged!=null)HoverChanged(null); }
        long lastHover;
        string hoverText;
        internal string HoverDetails(DrawingPoint at) {
            if(State==null)return null;
            var store=State.Store;var parts=new List<string>();double side=MapSide(State,ActualWidth,ActualHeight,Compact);
            foreach(var shared in State.SharedMarkers.Where(sharedItem=>MarkerDistance(new DrawingPoint((float)sharedItem.X,(float)sharedItem.Y),at)<=14/side))parts.Add(SharedMarkerLabel(shared));
            if(store==null||!store.MasterEnabled)return parts.Count==0?null:string.Join(Environment.NewLine,parts);
            var cluster=PoiClusters(State,ActualWidth,ActualHeight,Compact).Where(c=>MarkerDistance(c.Point,at)<=(c.Radius+3)/side).OrderBy(c=>MarkerDistance(c.Point,at)).FirstOrDefault();
            var marker=cluster==null?null:cluster.Members[0];
            if(cluster!=null&&cluster.Members.Count>1) { parts.Add(cluster.Members.Count.ToString(CultureInfo.CurrentCulture)+" × "+string.Join(", ",cluster.Members.Select(m=>Localization.GetMapObjectName(m.Title)).Distinct().Take(6))+(cluster.Members.Count>6?" …":""));marker=null; }
            if(marker!=null) { ScumMapCategory category;store.CategoryById.TryGetValue(marker.CategoryId,out category);parts.Add(Localization.GetMapObjectName(marker.Title));if(category!=null)parts.Add(Localization.GetCategoryName(category.Name)); }
            foreach(var habitat in store.Habitats.Where(h=>store.IsHabitatEnabled(h)&&h.Contains(at))) {
                var species=habitat.CategoryIds.Where(store.IsCategoryEnabled).Where(store.CategoryById.ContainsKey).Select(id=>Localization.GetCategoryName(store.CategoryById[id].Name)).Distinct().OrderBy(n=>n);
                parts.Add(Localization.GetMapObjectName(habitat.Name)+": "+string.Join(", ",species));
            }
            return parts.Count==0?null:string.Join(Environment.NewLine,parts.Distinct());
        }
        internal static void DrawPlayer(DrawingContext dc,Point p,double yaw,Brush ink,bool heading) {
            dc.PushTransform(new RotateTransform(yaw-90,p.X,p.Y));
            var arrow=new StreamGeometry();using(var context=arrow.Open()) { context.BeginFigure(new Point(p.X,p.Y-13),true,true);context.LineTo(new Point(p.X+9,p.Y+10),true,false);context.LineTo(new Point(p.X,p.Y+5),true,false);context.LineTo(new Point(p.X-9,p.Y+10),true,false); }
            if(heading)dc.DrawGeometry(ink,new Pen(WpfFieldTheme.Ground,2),arrow);else dc.DrawEllipse(ink,new Pen(WpfFieldTheme.Ground,2),p,7,7);dc.Pop();
        }
        static void DrawRoutePlan(DrawingContext dc,SquadRoutePlan plan,bool shared,Func<DrawingPoint,Point> point) {
            for(int i=0;i<plan.Legs.Length;i++) { var pen=new Pen(shared?WpfFieldTheme.Cyan:WpfFieldTheme.Amber,shared?3:2);if(!plan.Verified[i])pen.DashStyle=DashStyles.Dash;DrawLine(dc,plan.Legs[i].Select(point),pen); }
        }
        static string SharedMarkerLabel(SharedMapMarker marker) { int kind;string name=SquadState.IsPing(marker)&&int.TryParse(marker.Id.Substring(8,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out kind)&&kind<3?Localization.Get(new[]{"PingDanger","PingLoot","PingPickup"}[kind]):marker.Death?Localization.Get("DeathName"):marker.Name;return (SquadState.IsStop(marker)?Localization.Get(SquadState.IsShared(marker)?"SquadRoute":"SoloRoute")+" · ":marker.Id==SharedMapMarker.GpsId?Localization.Get("SoloRoute")+" · ":"")+marker.Owner+" · "+(SquadState.IsStop(marker)?(SquadState.StopIndex(marker)+1)+". ":"")+name; }
        SharedMapMarker SharedMarkerAt(DrawingPoint at) { return State==null?null:State.SharedMarkers.Where(marker=>MarkerDistance(new DrawingPoint((float)marker.X,(float)marker.Y),at)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).OrderBy(marker=>MarkerDistance(new DrawingPoint((float)marker.X,(float)marker.Y),at)).FirstOrDefault(); }
        void NavigateTo(DrawingPoint at) {
            var marker=SharedMarkerAt(at);
            if(marker!=null&&NavigateShared!=null)NavigateShared(new MapZone { Name=SharedMarkerLabel(marker),Category=ZoneCategory.Custom,Layer="Shared markers",Points=new[]{new DrawingPoint((float)marker.X,(float)marker.Y)},Argb=System.Drawing.Color.Cyan.ToArgb() });
            else if(NavigateAt!=null)NavigateAt(at);
        }
        void UpdateHover(Point at) {
            long now=Environment.TickCount&int.MaxValue;if(now-lastHover<80&&now>=lastHover)return;lastHover=now;
            DrawingPoint point;string text=drag.HasValue||contextPointer.HasValue||openContext!=null||!TryMapPoint(at,out point)?null:HoverDetails(point);if(text==hoverText)return;hoverText=text;
            ToolTip=text==null?null:new ToolTip { Content=new TextBlock { Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=380 },Background=WpfFieldTheme.Panel,Foreground=WpfFieldTheme.Ink,BorderBrush=WpfFieldTheme.Line,Padding=new Thickness(12) };
            if(HoverChanged!=null)HoverChanged(text);
        }
        internal bool HitsDestination(DrawingPoint point) { if(State==null||State.Target==null)return false;return MarkerDistance(point,State.Target.Centroid)<=12/MapSide(State,ActualWidth,ActualHeight,Compact); }
        static double MarkerDistance(DrawingPoint a,DrawingPoint b) { return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)); }
        internal ContextMenu BuildContextMenu(DrawingPoint target) {
            var menu=new ContextMenu { Background=WpfFieldTheme.Panel,Foreground=WpfFieldTheme.Ink };
            menu.Items.Add(new MenuItem { Header=DestinationSearch.Sector(target),IsEnabled=false });
            var navigate=new MenuItem { Header=Localization.Get("MapGpsDestination"),ToolTip=Localization.Get("MapGpsSavedHelp") };navigate.Click+=(s,e)=>NavigateTo(target);menu.Items.Add(navigate);
            var save=new MenuItem { Header=Localization.Get("MapSaveLocation"),ToolTip=Localization.Get("MapGpsSavedHelp") };save.Click+=(s,e)=> { if(WaypointAt!=null)WaypointAt(target); };menu.Items.Add(save);
            if(RouteStopAt!=null&&State!=null) {
                var routeMenu=new MenuItem { Header=Localization.Get("SquadTools") };
                var stop=new MenuItem { Header=Localization.Get("SquadAddStop") };stop.Click+=(s,e)=>RouteStopAt(target);menu.Items.Insert(1,stop);
                var clicked=State.SharedMarkers.Where(m=>SquadState.IsStop(m)&&MarkerDistance(new DrawingPoint((float)m.X,(float)m.Y),target)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).FirstOrDefault();var chosen=clicked!=null?SquadState.Routes(State.SharedMarkers).FirstOrDefault(r=>SquadState.RouteKey(r[0])==SquadState.RouteKey(clicked)):State.LocalRoute;
                var start=new MenuItem { Header=Localization.Get("SquadStartRoute"),IsEnabled=chosen!=null&&chosen.Length>0 };start.Click+=(s,e)=> { if(RouteNavigate!=null)RouteNavigate(chosen); };routeMenu.Items.Add(start);
                if(clicked!=null&&clicked.OwnerId==null&&State.LocalRoute.Any(m=>m.Id==clicked.Id)) { var remove=new MenuItem { Header=Localization.Get("SquadDelete")+": "+clicked.Name };remove.Click+=(s,e)=> { if(RouteRemove!=null)RouteRemove(clicked.Id); };routeMenu.Items.Add(remove); }
                var ownMenu=new MenuItem { Header=Localization.Get("OwnRoute") };foreach(bool shared in new[]{false,true}) { bool kind=shared;var mode=new MenuItem { Header=Localization.Get(shared?"SquadRoute":"SoloRoute"),IsCheckable=true,IsChecked=State.LocalRoute.Length>0&&SquadState.IsShared(State.LocalRoute[0])==shared };mode.Click+=(s,e)=> { if(RouteKind!=null)RouteKind(kind); };ownMenu.Items.Add(mode); }routeMenu.Items.Add(ownMenu);
                var undo=new MenuItem { Header=Localization.Get("SquadUndoStop"),IsEnabled=State.LocalRoute.Length>0 };undo.Click+=(s,e)=> { if(RouteUndo!=null)RouteUndo(); };routeMenu.Items.Add(undo);
                var clearLocal=new MenuItem { Header=Localization.Get("WpfClearRoute"),IsEnabled=State.LocalRoute.Length>0 };clearLocal.Click+=(s,e)=> { if(RouteClear!=null)RouteClear(); };routeMenu.Items.Add(clearLocal);
                var end=new MenuItem { Header=Localization.Get("SquadStopNav"),IsEnabled=State.SquadNavigating };end.Click+=(s,e)=> { if(ClearRouteRequested!=null)ClearRouteRequested(); };routeMenu.Items.Add(end);menu.Items.Add(routeMenu);
            }
            if(NavigatePeer!=null&&State!=null)foreach(var peer in State.Peers.Where(p=>MarkerDistance(MapWindow.ToMap(p.Position),target)<=14/MapSide(State,ActualWidth,ActualHeight,Compact))) { string id=peer.Id;var follow=new MenuItem { Header=Localization.Get("SquadFollow")+": "+peer.Name };follow.Click+=(s,e)=>NavigatePeer(id);menu.Items.Add(follow); }
            if(PingAt!=null)for(int i=0;i<3;i++) { int kind=i;var ping=new MenuItem { Header=Localization.Get(new[]{"PingDanger","PingLoot","PingPickup"}[i]) };ping.Click+=(s,e)=>PingAt(kind,target);menu.Items.Add(ping); }
            menu.Items.Add(new Separator());var clear=new MenuItem { Header=Localization.Get("WpfClearRoute"),IsEnabled=State!=null&&State.Target!=null };clear.Click+=(s,e)=> { if(ClearRouteRequested!=null)ClearRouteRequested(); };menu.Items.Add(clear);
            var saved=State==null||State.Saved==null?null:State.Saved.Where(z=>z.Points!=null&&z.Points.Length==1&&MarkerDistance(target,z.Centroid)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).OrderByDescending(z=>z.IsDeathMarker).ThenBy(z=>MarkerDistance(target,z.Centroid)).FirstOrDefault();
            if(saved!=null&&MarkerDistance(target,saved.Centroid)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)) { var delete=new MenuItem { Header=Localization.T("DeleteWaypointNamed",saved.DisplayName) };delete.Click+=(s,e)=> { if(DeleteWaypointRequested!=null)DeleteWaypointRequested(saved); };menu.Items.Add(delete); }
            return menu;
        }
        internal Action<int> ZoomRequested;
        internal Action<DrawingPoint> NavigateAt,WaypointAt,RouteStopAt;
        internal Action<int,DrawingPoint> PingAt;
        internal Action<SharedMapMarker[]> RouteNavigate;
        internal Action<string> RouteRemove,NavigatePeer;
        internal Action<bool> RouteKind;
        internal Action RouteUndo,RouteClear;
        internal Action<MapZone> NavigateShared;
        internal Canvas ContextHost;
        Border openContext;
        Point contextAnchor;
        internal bool ContextMenuOpen { get { return openContext!=null; } }
        Point? contextPointer;
        internal bool BeginContextPointer(Point point) {
            DrawingPoint target;if(!TryMapPoint(point,out target))return false;
            CancelPointer();ClearHover();contextPointer=point;CaptureMouse();return true;
        }
        internal bool EndContextPointer(Point point) {
            if(!contextPointer.HasValue)return false;
            var origin=contextPointer.Value;contextPointer=null;
            if(IsMouseCaptured)ReleaseMouseCapture();
            return OpenContextMenu(origin);
        }
        internal bool OpenContextMenu(Point point) {
            DrawingPoint target;if(ContextHost==null||!TryMapPoint(point,out target))return false;
            CloseContextMenu();ClearHover();contextAnchor=point;
            // Keep every level in the map's existing HWND. Native Popup/MenuItem
            // windows need activation/capture that conflicts with SCUM's focus.
            var actions=BuildContextMenu(target);
            var scroll=new ScrollViewer { Content=ContextItems(actions.Items),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(80,ActualHeight-24) };
            openContext=WpfFieldTheme.Card(scroll,new Thickness(6));openContext.Width=Math.Min(350,Math.Max(180,ActualWidth-16));
            openContext.SizeChanged+=(s,e)=>PositionContextMenu();ContextHost.Children.Add(openContext);PositionContextMenu();return true;
        }
        StackPanel ContextItems(ItemCollection items) {
            var panel=new StackPanel();
            foreach(var value in items) {
                var item=value as MenuItem;
                if(item==null) { panel.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(4,5,4,5) });continue; }
                string title=(item.IsChecked?"✓ ":"")+Convert.ToString(item.Header,CultureInfo.CurrentCulture);
                if(item.HasItems) {
                    var children=ContextItems(item.Items);children.Margin=new Thickness(12,0,0,0);children.Visibility=Visibility.Collapsed;
                    Button expand=null;expand=WpfFieldTheme.Button("▸ "+title,()=> { bool show=children.Visibility!=Visibility.Visible;children.Visibility=show?Visibility.Visible:Visibility.Collapsed;expand.Content=(show?"▾ ":"▸ ")+title; });
                    expand.IsEnabled=item.IsEnabled;expand.HorizontalContentAlignment=HorizontalAlignment.Left;expand.Margin=new Thickness(0,1,0,1);expand.MinHeight=32;panel.Children.Add(expand);panel.Children.Add(children);
                } else {
                    var captured=item;var action=WpfFieldTheme.Button(title,()=> { CloseContextMenu();captured.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent,captured)); });
                    action.ToolTip=item.ToolTip;action.IsEnabled=item.IsEnabled;action.HorizontalContentAlignment=HorizontalAlignment.Left;action.Margin=new Thickness(0,1,0,1);action.MinHeight=32;action.Padding=new Thickness(10,6,10,6);
                    action.Content=new TextBlock { Text=title,TextWrapping=TextWrapping.Wrap };panel.Children.Add(action);
                }
            }
            return panel;
        }
        void PositionContextMenu() {
            if(openContext==null)return;
            openContext.Measure(new Size(openContext.Width,Math.Max(80,ActualHeight-16)));
            Canvas.SetLeft(openContext,Math.Max(8,Math.Min(contextAnchor.X,ActualWidth-openContext.DesiredSize.Width-8)));
            Canvas.SetTop(openContext,Math.Max(8,Math.Min(contextAnchor.Y,ActualHeight-openContext.DesiredSize.Height-8)));
        }
        internal bool DismissContextOutside(Point point) {
            if(openContext==null)return false;
            var bounds=new Rect(Canvas.GetLeft(openContext),Canvas.GetTop(openContext),openContext.ActualWidth,openContext.ActualHeight);
            if(bounds.Contains(point))return false;CloseContextMenu();return true;
        }
        internal void CloseContextMenu() {
            contextPointer=null;if(IsMouseCaptured)ReleaseMouseCapture();
            var menu=openContext;if(menu==null)return;openContext=null;if(ContextHost!=null)ContextHost.Children.Remove(menu);
        }
        Point? drag;
        DrawingPoint pan;
        bool dragging;
        internal static double MapSide(WpfMapState state,double width,double height,bool compact) { return (compact?Math.Max(width,height):Math.Min(width,height))*Math.Max(1,state.Zoom); }
        internal static DrawingPoint CameraCentre(WpfMapState state,double width,double height,bool compact) {
            var centre=compact&&state.Position!=null?MapWindow.ToMap(state.Position):state.Pan;
            if(compact&&state.RotateMap&&state.Position!=null)return centre;
            double side=MapSide(state,width,height,compact);
            if(side<=0)return new DrawingPoint(.5f,.5f);
            double halfX=Math.Min(.5,width/(2*side)),halfY=Math.Min(.5,height/(2*side));
            return new DrawingPoint((float)Math.Max(halfX,Math.Min(1-halfX,centre.X)),(float)Math.Max(halfY,Math.Min(1-halfY,centre.Y)));
        }
        internal bool TryMapPoint(Point p,out DrawingPoint target) {
            target=new DrawingPoint();
            if(Compact||State==null||ActualWidth<=0||ActualHeight<=0||p.X<0||p.Y<0||p.X>ActualWidth||p.Y>ActualHeight)return false;
            double side=MapSide(State,ActualWidth,ActualHeight,Compact);if(side<=0)return false;
            target=new DrawingPoint((float)(CameraCentre(State,ActualWidth,ActualHeight,Compact).X+(p.X-ActualWidth/2)/side),(float)(CameraCentre(State,ActualWidth,ActualHeight,Compact).Y+(p.Y-ActualHeight/2)/side));
            return target.X>=0&&target.X<=1&&target.Y>=0&&target.Y<=1;
        }
        internal void BeginPointer(Point p) { if(Compact||State==null)return;ClearHover();drag=p;pan=CameraCentre(State,ActualWidth,ActualHeight,Compact);dragging=false; }
        internal void MovePointer(Point p) {
            if(!drag.HasValue||State==null)return;
            double dx=p.X-drag.Value.X,dy=p.Y-drag.Value.Y;
            if(!dragging&&Math.Abs(dx)<=SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(dy)<=SystemParameters.MinimumVerticalDragDistance)return;
            dragging=true;double side=MapSide(State,ActualWidth,ActualHeight,Compact);if(side<=0)return;
            if(PanChanged!=null)PanChanged(new DrawingPoint((float)Math.Max(0,Math.Min(1,pan.X-dx/side)),(float)Math.Max(0,Math.Min(1,pan.Y-dy/side))));
        }
        internal void EndPointer(Point p) {
            if(!drag.HasValue)return;MovePointer(p);bool click=!dragging;CancelPointer();DrawingPoint target;
            if(click&&TryMapPoint(p,out target)) { if(HitsDestination(target)&&ClearRouteRequested!=null)ClearRouteRequested();else NavigateTo(target); }
        }
        internal void CancelPointer() { drag=null;dragging=false; }
        internal WpfMapSurface() {
            FlowDirection=FlowDirection.LeftToRight;ClipToBounds=true;Focusable=true;
            PreviewMouseRightButtonDown+=(sender,args)=> { if(BeginContextPointer(args.GetPosition(this)))args.Handled=true; };
            PreviewMouseRightButtonUp+=(sender,args)=> { if(EndContextPointer(args.GetPosition(this)))args.Handled=true; };
            MouseWheel+=(s,e)=> { if(ZoomRequested!=null)ZoomRequested(e.Delta>0?1:-1);e.Handled=true; };
            MouseLeftButtonDown+=(s,e)=> { if(Compact||State==null)return;BeginPointer(e.GetPosition(this));CaptureMouse();e.Handled=true; };
            MouseMove+=(s,e)=> { if(drag.HasValue&&e.LeftButton!=MouseButtonState.Pressed)CancelPointer();else MovePointer(e.GetPosition(this));UpdateHover(e.GetPosition(this)); };
            MouseLeave+=(s,e)=> { ToolTip=null;hoverText=null; };
            ToolTipService.SetInitialShowDelay(this,250);ToolTipService.SetShowDuration(this,30000);
            MouseLeftButtonUp+=(s,e)=> { if(!drag.HasValue)return;EndPointer(e.GetPosition(this));ReleaseMouseCapture();e.Handled=true; };
            LostMouseCapture+=(s,e)=> { CancelPointer();contextPointer=null; };
            IsVisibleChanged+=(s,e)=> { if(!IsVisible)CloseContextMenu(); };
            Unloaded+=(s,e)=> { if(poiBuild!=null)poiBuild.Cancel();System.Threading.Interlocked.Increment(ref poiGeneration);requestedPoiLayout=null; };
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
            if(Compact&&State!=null&&State.Shape=="Circle")dc.PushClip(new EllipseGeometry(new Point(w/2,h/2),Math.Min(w,h)/2,Math.Min(w,h)/2));else dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h),Compact?10:0,Compact?10:0));
            dc.DrawRectangle(Compact?Brushes.Transparent:tacticalHitSurface,null,new Rect(0,0,w,h));
            if(State==null) { dc.Pop();return; }
            var routeInk=new SolidColorBrush(Color.FromArgb((byte)(State.RouteColor>>24),(byte)(State.RouteColor>>16),(byte)(State.RouteColor>>8),(byte)State.RouteColor));
            var playerInk=new SolidColorBrush(Color.FromArgb((byte)(State.PlayerColor>>24),(byte)(State.PlayerColor>>16),(byte)(State.PlayerColor>>8),(byte)State.PlayerColor));
            double side=MapSide(State,w,h,Compact);DrawingPoint centre=CameraCentre(State,w,h,Compact);
            double left=w/2-side*centre.X,top=h/2-side*centre.Y;
            double rotation=renderRotation=Rotation(State,Compact);
            double overscan=rotation==0?0:Math.Max(w,h);
            var layerViewport=new Rect(-overscan,-overscan,w+2*overscan,h+2*overscan);
            if(State.EdgeFade) {
                if(State.Shape=="Circle") {
                    var fade=new RadialGradientBrush();fade.GradientStops.Add(new GradientStop(Colors.White,0));fade.GradientStops.Add(new GradientStop(Colors.White,.8));fade.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.PushOpacityMask(fade);
                } else dc.PushOpacityMask(EdgeFadeBrush(false));
                if(State.Shape!="Circle")dc.PushOpacityMask(EdgeFadeBrush(true));
            }
            dc.PushTransform(new RotateTransform(rotation,w/2,h/2));
            dc.PushClip(new RectangleGeometry(new Rect(left,top,side,side)));
            Func<DrawingPoint,Point> point=p=>new Point(left+p.X*side,top+p.Y*side);
            dc.PushOpacity(State.Opacity/100.0);
            if(Terrain!=null)Terrain.DrawRotated(dc,w,h,left,top,side,VisualTreeHelper.GetDpi(this).DpiScaleX,rotation==0?0:Math.Max(w,h));
            else { dc.PushTransform(new TranslateTransform(left,top));dc.PushTransform(new ScaleTransform(side/1024,side/1024));if(BaseMap!=null)dc.DrawDrawing(BaseMap);dc.Pop();dc.Pop(); }
            dc.Pop();
            if(State.GridOpacity>0&&(State.Grid||State.GridLabels)) {
                dc.PushOpacity(State.GridOpacity/100.0);
                if(State.Grid)for(int i=1;i<5;i++) { var pen=new Pen(WpfFieldTheme.Ink,1);dc.DrawLine(pen,new Point(left+i*side/5,top),new Point(left+i*side/5,top+side));dc.DrawLine(pen,new Point(left,top+i*side/5),new Point(left+side,top+i*side/5)); }
                if(State.GridLabels)for(int row=0;row<5;row++)for(int col=0;col<5;col++)Label(dc,""+"DCBAZ"[row]+(4-col),new Point(left+col*side/5+8,top+row*side/5+8),WpfFieldTheme.Ink,Math.Max(9,State.LabelSize));
                dc.Pop();
            }
            if(State.Store!=null&&State.Store.MasterEnabled)foreach(var habitat in State.Store.Habitats.AsEnumerable().Reverse().Where(State.Store.IsHabitatEnabled)) {
                if(habitat.Rings==null)continue;var geometry=habitatGeometryCache.GetValue(habitat.Rings,WorldGeometry);var bounds=geometryBoundsCache.GetValue(geometry,g=>new GeometryBounds(g)).Value;bounds.Transform(new Matrix(side,0,0,side,left,top));if(!bounds.IntersectsWith(layerViewport))continue;
                var color=Color.FromRgb((byte)(habitat.Color>>16),(byte)(habitat.Color>>8),(byte)habitat.Color);
                dc.PushTransform(new MatrixTransform(side,0,0,side,left,top));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(12,color.R,color.G,color.B)),new Pen(new SolidColorBrush(Color.FromArgb(100,color.R,color.G,color.B)),.8/side),geometry);dc.Pop();
            }
            if(State.History&&State.Trail.Length>1)DrawLine(dc,State.Trail.Select(point),new Pen(new SolidColorBrush(Color.FromArgb((byte)(State.HistoryColor>>24),(byte)(State.HistoryColor>>16),(byte)(State.HistoryColor>>8),(byte)State.HistoryColor)),2));
            if(State.Zones)foreach(var zone in State.Saved.Where(z=>z.Points!=null&&z.Points.Length>2)) {
                var outline=Color.FromArgb((byte)(zone.Argb>>24),(byte)(zone.Argb>>16),(byte)(zone.Argb>>8),(byte)zone.Argb);int fillArgb=zone.FillArgb??(((zone.IsFaction||zone.Category==ZoneCategory.Faction?75:40)<<24)|(zone.Argb&0xFFFFFF));
                var geometry=zoneGeometryCache.GetValue(zone.Points,points=>WorldGeometry(new[]{points}));var bounds=geometryBoundsCache.GetValue(geometry,g=>new GeometryBounds(g)).Value;bounds.Transform(new Matrix(side,0,0,side,left,top));if(!bounds.IntersectsWith(layerViewport))continue;
                dc.PushTransform(new MatrixTransform(side,0,0,side,left,top));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(fillArgb>>24),(byte)(fillArgb>>16),(byte)(fillArgb>>8),(byte)fillArgb)),new Pen(new SolidColorBrush(outline),1.8/side),geometry);dc.Pop();
            }
            if(State.Zones)foreach(var zone in State.Saved.Where(z=>MapWindow.WpfCustomZone(z)&&z.Points.Length<3)) {
                var color=Color.FromArgb((byte)(zone.Argb>>24),(byte)(zone.Argb>>16),(byte)(zone.Argb>>8),(byte)zone.Argb);dc.DrawEllipse(new SolidColorBrush(color),new Pen(WpfFieldTheme.Ground,1.5),point(zone.Points[0]),6,6);
            }
            if(State.Waypoints)foreach(var zone in State.Saved.Where(z=>z.Points!=null&&z.Points.Length==1&&!MapWindow.WpfCustomZone(z)&&!z.IsDeathMarker))dc.DrawEllipse(null,new Pen(WpfFieldTheme.Cyan,2),point(zone.Centroid),5,5);
            if(State.Store!=null&&State.Store.MasterEnabled) {
                if(!Compact)DrawRetainedPois(dc,side,left,top,w,h);
                else foreach(var cluster in PoiClusters(State,w,h,Compact)) {
                    var at=point(cluster.Point);var screen=RotatePoint(at,rotation,w,h);
                    if(screen.X<-14||screen.Y<-14||screen.X>w+14||screen.Y>h+14)continue;
                    ScumMapCategory category;State.Store.CategoryById.TryGetValue(cluster.Members[0].CategoryId,out category);DrawPoi(dc,at,category,cluster);
                }

            }
            foreach(var label in SelectLabels(State,w,h,Compact))Label(dc,label.Text,RotatePoint(label.At,-rotation,w,h),label.Major?WpfFieldTheme.Ink:WpfFieldTheme.Muted,Math.Max(6,State.LabelSize)+(label.Major?2:0));
            if(State.Route!=null&&State.Route.Success&&State.Position!=null)DrawLine(dc,new[]{MapWindow.ToMap(State.Position)}.Concat(State.Route.Polyline??new DrawingPoint[0]).Concat(State.Target==null?new DrawingPoint[0]:new[]{State.Target.Centroid}).Select(point),new Pen(routeInk,4));
            if(State.Target!=null) { var p=point(State.Target.Centroid);dc.DrawEllipse(null,new Pen(routeInk,3),p,9,9);Label(dc,State.Target.DisplayName,p+new Vector(12,-30),WpfFieldTheme.Cyan,13); }
            if(State.Position!=null) {
                DrawPlayer(dc,point(MapWindow.ToMap(State.Position)),State.Position.Yaw,playerInk,State.Heading);
            }
            foreach(var peer in State.Peers) {
                var p=point(MapWindow.ToMap(peer.Position));DrawPlayer(dc,p,peer.Position.Yaw,WpfFieldTheme.Cyan,State.Heading);
                Label(dc,peer.Name,p+new Vector(11,-18),WpfFieldTheme.Cyan,12);
            }
            string ownKey=State.LocalRoute.Length>0?SquadState.RouteKey(State.LocalRoute[0]):null;
            if(State.OwnRoutePlan!=null&&ownKey!=State.ActiveSquadRoute)DrawRoutePlan(dc,State.OwnRoutePlan,State.LocalRoute.Length>0&&SquadState.IsShared(State.LocalRoute[0]),point);
            if(State.SquadPlan!=null)DrawRoutePlan(dc,State.SquadPlan,State.ActiveSquadShared,point);
            foreach(var plan in State.OtherRoutePlans.Where(p=>p.Key!=State.ActiveSquadRoute))DrawRoutePlan(dc,plan.Plan,plan.Shared,point);
            foreach(var route in SquadState.Routes(State.SharedMarkers)) {
                string key=SquadState.RouteKey(route[0]);bool ready=key==ownKey&&State.OwnRoutePlan!=null||key==State.ActiveSquadRoute&&State.SquadPlan!=null||State.OtherRoutePlans.Any(p=>p.Key==key);
                if(!ready)for(int i=1;i<route.Length;i++) { var pen=new Pen(SquadState.IsShared(route[0])?WpfFieldTheme.Cyan:WpfFieldTheme.Amber,1) { DashStyle=DashStyles.Dot };dc.DrawLine(pen,point(new DrawingPoint((float)route[i-1].X,(float)route[i-1].Y)),point(new DrawingPoint((float)route[i].X,(float)route[i].Y))); }
            }
            foreach(var marker in State.SharedMarkers) {
                var at=point(new DrawingPoint((float)marker.X,(float)marker.Y));var ink=marker.Death?WpfFieldTheme.ColorBrush("#FF654F"):WpfFieldTheme.Cyan;
                if(SquadState.IsStop(marker)||marker.Id==SharedMapMarker.GpsId)ink=SquadState.IsShared(marker)?WpfFieldTheme.Cyan:WpfFieldTheme.Amber;
                if(SquadState.IsPing(marker))ink=marker.Id.Substring(8,2)=="00"?WpfFieldTheme.ColorBrush("#FF654F"):marker.Id.Substring(8,2)=="01"?WpfFieldTheme.Amber:WpfFieldTheme.Cyan;
                if(marker.Death) { dc.DrawRoundedRectangle(WpfFieldTheme.Ground,new Pen(ink,2),new Rect(at.X-7,at.Y-10,14,19),3,3);dc.DrawLine(new Pen(ink,2),at+new Vector(0,-5),at+new Vector(0,4));dc.DrawLine(new Pen(ink,2),at+new Vector(-4,-1),at+new Vector(4,-1)); }
                else { var diamond=new StreamGeometry();using(var shape=diamond.Open()) { shape.BeginFigure(at+new Vector(0,-8),true,true);shape.LineTo(at+new Vector(7,0),true,false);shape.LineTo(at+new Vector(0,8),true,false);shape.LineTo(at+new Vector(-7,0),true,false); }dc.DrawGeometry(WpfFieldTheme.Ground,new Pen(ink,2),diamond); }
                Label(dc,SharedMarkerLabel(marker),at+new Vector(11,-19),ink,12);
            }
            if(Compact&&State.Compass)Label(dc,"N",new Point(w/2-7,h/2-Math.Min(w,h)/2+10),WpfFieldTheme.Ink,11);
            foreach(var death in State.Saved.Where(z=>z.IsDeathMarker&&z.Points!=null&&z.Points.Length==1)) {
                var p=point(death.Centroid);var ink=WpfFieldTheme.ColorBrush("#FF654F");dc.DrawRoundedRectangle(WpfFieldTheme.Ground,new Pen(ink,2),new Rect(p.X-8,p.Y-11,16,20),4,4);
                dc.DrawLine(new Pen(ink,2),new Point(p.X,p.Y-6),new Point(p.X,p.Y+4));dc.DrawLine(new Pen(ink,2),new Point(p.X-4,p.Y-2),new Point(p.X+4,p.Y-2));
                Label(dc,death.DisplayName,p+new Vector(12,-21),ink,Compact?11:13);
            }
            dc.Pop(); // Map bounds: edge guidance belongs to the viewport, including transparent margins.
            dc.Pop(); // Map rotation.
            if(State.EdgeFade) { if(State.Shape!="Circle")dc.Pop();dc.Pop(); }
            DrawDestinationEdge(dc,w,h,p=>RotatePoint(point(p),rotation,w,h),routeInk);
            DrawPeerEdges(dc,w,h,p=>RotatePoint(point(p),rotation,w,h));
            dc.Pop();
        }
        static Brush EdgeFadeBrush(bool vertical) {
            var fade=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=vertical?new Point(0,1):new Point(1,0) };
            fade.GradientStops.Add(new GradientStop(Colors.Transparent,0));fade.GradientStops.Add(new GradientStop(Colors.White,.08));fade.GradientStops.Add(new GradientStop(Colors.White,.92));fade.GradientStops.Add(new GradientStop(Colors.Transparent,1));return fade;
        }
        void DrawDestinationEdge(DrawingContext dc,double width,double height,Func<DrawingPoint,Point> screen,Brush ink) {
            if(State.Target==null||State.Position==null)return;
            DrawEdgeArrow(dc,width,height,screen(State.Target.Centroid),ink,null);
        }
        void DrawPeerEdges(DrawingContext dc,double width,double height,Func<DrawingPoint,Point> screen) {
            if(!Compact)return;
            foreach(var peer in State.Peers)DrawEdgeArrow(dc,width,height,screen(MapWindow.ToMap(peer.Position)),WpfFieldTheme.Cyan,peer.Name);
            foreach(var marker in State.SharedMarkers.Where(shared=>shared.Id==SharedMapMarker.GpsId))DrawEdgeArrow(dc,width,height,screen(new DrawingPoint((float)marker.X,(float)marker.Y)),WpfFieldTheme.Cyan,SharedMarkerLabel(marker));
        }
        void DrawEdgeArrow(DrawingContext dc,double width,double height,Point destination,Brush ink,string name) {
            bool circle=Compact&&State.Shape=="Circle";
            double radius=Math.Min(width,height)/2;
            bool visible=circle?(destination-new Point(width/2,height/2)).Length<=radius:destination.X>=0&&destination.X<=width&&destination.Y>=0&&destination.Y<=height;
            if(visible)return;
            var bounds=circle?new System.Drawing.RectangleF((float)(width/2-radius),(float)(height/2-radius),(float)(radius*2),(float)(radius*2)):new System.Drawing.RectangleF(0,0,(float)width,(float)height);
            DrawingPoint tip,direction;
            if(!MapWindow.TryDestinationEdge(bounds,circle,new DrawingPoint((float)(width/2),(float)(height/2)),new DrawingPoint((float)destination.X,(float)destination.Y),out tip,out direction))return;
            var rear=new Point(tip.X-direction.X*13,tip.Y-direction.Y*13);var arrow=new StreamGeometry();
            using(var context=arrow.Open()) { context.BeginFigure(new Point(tip.X,tip.Y),true,true);context.LineTo(rear+new Vector(-direction.Y*6,direction.X*6),true,false);context.LineTo(rear+new Vector(direction.Y*6,-direction.X*6),true,false); }
            dc.DrawGeometry(ink,new Pen(WpfFieldTheme.Ground,2),arrow);
            if(name!=null) {
                var text=new FormattedText(name,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Barlow Semi Condensed, Segoe UI"),12,ink,1.0) { MaxTextWidth=Math.Max(1,Math.Min(180,width-40)),Trimming=TextTrimming.CharacterEllipsis };
                double x=direction.X>.3?tip.X-22-text.Width:direction.X<-.3?tip.X+22:tip.X-text.Width/2;
                double y=direction.Y>.3?tip.Y-22-text.Height:direction.Y<-.3?tip.Y+22:tip.Y-text.Height/2;
                var at=new Point(Math.Max(10,Math.Min(width-text.Width-10,x)),Math.Max(10,Math.Min(height-text.Height-10,y)));
                dc.DrawRoundedRectangle(WpfFieldTheme.Ground,null,new Rect(at.X-4,at.Y-1,text.Width+8,text.Height+2),3,3);dc.DrawText(text,at);
            }
        }
        internal static WpfMapLabel[] SelectLabels(WpfMapState state,double width,double height,bool compact) {
            if(state==null||!state.Labels||width<=0||height<=0)return new WpfMapLabel[0];
            double side=MapSide(state,width,height,compact);DrawingPoint centre=CameraCentre(state,width,height,compact);
            Func<DrawingPoint,Point> screen=p=>RotatePoint(new Point(width/2+(p.X-centre.X)*side,height/2+(p.Y-centre.Y)*side),Rotation(state,compact),width,height);
            var candidates=new List<WpfMapLabel>();
            foreach(var zone in state.Saved) {
                if(zone.Points==null||zone.Points.Length==0||string.IsNullOrWhiteSpace(zone.Name))continue;
                if(MapWindow.WpfCustomZone(zone)?!state.Zones:zone.Points.Length==1&&!state.Waypoints)continue;
                candidates.Add(new WpfMapLabel { Text=zone.DisplayName,At=screen(MapWindow.WpfZoneCentre(zone)),Major=zone.Points.Length>1 });
            }
            if(state.Store!=null&&state.Store.MasterEnabled)foreach(var cluster in PoiClusters(state,width,height,compact)) {
                var marker=cluster.Members.FirstOrDefault(m=>m.CategoryId==27||m.CategoryId==26||m.CategoryId==80)??cluster.Members[0];
                var at=screen(marker.Point);if(at.X<0||at.Y<0||at.X>width||at.Y>height)continue;
                string name=PoiLabel(state,marker,compact);if(string.IsNullOrWhiteSpace(name))continue;
                candidates.Add(new WpfMapLabel { Text=name,At=at,Major=marker.CategoryId==27||marker.CategoryId==26||marker.CategoryId==80 });
            }
            var result=new List<WpfMapLabel>();var occupied=new List<Rect>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in candidates.Where(c=>c.At.X>=0&&c.At.Y>=0&&c.At.X<=width&&c.At.Y<=height).OrderByDescending(c=>c.Major).ThenBy(c=>(c.At-new Point(width/2,height/2)).LengthSquared)) {
                if(names.Contains(item.Text))continue;
                double size=state.LabelSize>0?state.LabelSize:12;
                var text=LabelText(item.Text,item.Major?size+2:size,WpfFieldTheme.Ink);
                double labelWidth=Math.Min(220,text.Width)+8,labelHeight=text.Height+4;
                foreach(var offset in new[]{new Vector(9,-labelHeight),new Vector(9,8),new Vector(-labelWidth-9,-labelHeight),new Vector(-labelWidth-9,8)}) {
                    var at=item.At+offset;var box=new Rect(at.X-4,at.Y-1,labelWidth,labelHeight);
                    if(box.Left<0||box.Top<0||box.Right>width||box.Bottom>height||occupied.Any(r=>r.IntersectsWith(box)))continue;
                    result.Add(new WpfMapLabel { Text=item.Text,At=at,Major=item.Major });occupied.Add(box);names.Add(item.Text);break;
                }
                if(result.Count>=Math.Max(2,Math.Min(compact?3:60,(int)(width*height/(compact?24000:14000)))))break;
            }
            return result.ToArray();
        }
        static void DrawLine(DrawingContext dc,IEnumerable<Point> points,Pen pen) { var all=points.ToArray();if(all.Length<2)return;var geometry=new StreamGeometry();using(var c=geometry.Open()) { c.BeginFigure(all[0],false,false);c.PolyLineTo(all.Skip(1).ToArray(),true,false); }geometry.Freeze();pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;dc.DrawGeometry(null,pen,geometry); }
        static string PoiLabel(WpfMapState state,ScumMapMarker marker,bool compact) {
            var cache=poiCaches.GetValue(state.Store,store=>new[]{new WpfPoiCache(),new WpfPoiCache()})[compact?0:1];
            if(cache.Labels==null||cache.LabelLanguage!=Localization.CurrentCode) { cache.Labels=new Dictionary<ScumMapMarker,string>();cache.LabelLanguage=Localization.CurrentCode; }
            string result;if(!cache.Labels.TryGetValue(marker,out result)) { result=Localization.GetMapObjectName(state.Store.GetMarkerDisplayName(marker));cache.Labels.Add(marker,result); }return result;
        }
        struct LabelKey:IEquatable<LabelKey> {
            internal string Text;internal double Size;internal Color Color;
            public bool Equals(LabelKey other) { return Text==other.Text&&Size==other.Size&&Color==other.Color; }
            public override bool Equals(object other) { return other is LabelKey&&Equals((LabelKey)other); }
            public override int GetHashCode() { unchecked { return (Text.GetHashCode()*397^Size.GetHashCode())*397^Color.GetHashCode(); } }
        }
        static readonly Typeface labelTypeface=new Typeface("Barlow Semi Condensed, Segoe UI"),poiTypeface=new Typeface("Segoe UI");
        static readonly Dictionary<LabelKey,FormattedText> labelTexts=new Dictionary<LabelKey,FormattedText>();
        static readonly Queue<LabelKey> labelTextOrder=new Queue<LabelKey>();
        static string labelCulture;
        static FormattedText LabelText(string text,double size,Brush ink) {
            if(labelCulture!=CultureInfo.CurrentCulture.Name) { labelCulture=CultureInfo.CurrentCulture.Name;labelTexts.Clear();labelTextOrder.Clear(); }
            var key=new LabelKey { Text=text,Size=size,Color=((SolidColorBrush)ink).Color };FormattedText result;
            if(labelTexts.TryGetValue(key,out result))return result;
            result=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,labelTypeface,size,ink,1.0) { MaxTextWidth=220,MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis };
            if(labelTexts.Count>=1024)labelTexts.Remove(labelTextOrder.Dequeue());labelTexts.Add(key,result);labelTextOrder.Enqueue(key);return result;
        }
        void Label(DrawingContext dc,string text,Point at,Brush ink,double size) { dc.PushTransform(new RotateTransform(-renderRotation,at.X,at.Y));var f=LabelText(text,size,ink);dc.DrawRoundedRectangle(WpfFieldTheme.Ground,null,new Rect(at.X-4,at.Y-1,Math.Min(220,f.Width)+8,f.Height+2),3,3);dc.DrawText(f,at);dc.Pop(); }
        internal static string PoiSymbol(ScumMapCategory category) {
            string name=category==null?"":(category.Section+" "+category.Name).ToLowerInvariant();
            if(name.Contains("fish"))return "≈";if(name.Contains("hunt"))return "⌖";
            if(name.Contains("gas")||name.Contains("fuel"))return "F";
            if(name.Contains("bunker"))return "B";if(name.Contains("outpost")||name.Contains("trader"))return "$";
            if(name.Contains("hospital")||name.Contains("medic"))return "+";
            if(name.Contains("police"))return "P";if(name.Contains("military"))return "★";
            if(name.Contains("town")||name.Contains("city"))return "⌂";
            if(name.Contains("vehicle"))return "V";return "◆";
        }
        static readonly Dictionary<string,DrawingGroup> poiIcons=new Dictionary<string,DrawingGroup>();
        static readonly Queue<string> poiIconOrder=new Queue<string>();
        static readonly Dictionary<string,Pen> poiOutlinePens=new Dictionary<string,Pen>();
        void DrawPoi(DrawingContext dc,Point at,ScumMapCategory category,WpfPoiCluster cluster) {
            EnsurePoiAssets(category,cluster);
            double radius=cluster.Radius;
            dc.DrawEllipse(WpfFieldTheme.Ground,cluster.Outline,at,radius,radius);
            double scale=(cluster.Members.Count>1?(Compact?9:11):radius*1.45)/12;
            var transform=new Matrix(scale,0,0,scale,0,0);if(renderRotation!=0)transform.Rotate(-renderRotation);transform.Translate(at.X,at.Y);
            dc.PushTransform(new MatrixTransform(transform));dc.DrawDrawing(cluster.Icon);dc.Pop();
        }
        void EnsurePoiAssets(ScumMapCategory category,WpfPoiCluster cluster) {
            int color=category==null?unchecked((int)0xFFA4B1BC):category.ColorBackground;
            if(cluster.Icon==null) {
                string label=cluster.Members.Count>1?(cluster.Members.Count>99?"99+":cluster.Members.Count.ToString(CultureInfo.InvariantCulture)):PoiSymbol(category);
                string key=label;
                DrawingGroup icon;
                if(!poiIcons.TryGetValue(key,out icon)) {
                icon=new DrawingGroup();using(var drawing=icon.Open()) {
                    // One frozen vector glyph per symbol/count. Zoom changes its
                    // transform, never its font layout or drawing asset.
                    var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,poiTypeface,12,Brushes.White,1.0);
                    drawing.DrawText(text,new Point(-text.Width/2,-text.Height/2));
                }icon.Freeze();
                if(poiIcons.Count>=512)poiIcons.Remove(poiIconOrder.Dequeue());
                poiIcons.Add(key,icon);poiIconOrder.Enqueue(key);
                }
                cluster.Icon=icon;
            }
            if(cluster.Outline==null||cluster.IconColor!=color) {
            string penKey=color+"|"+cluster.Mixed;Pen outline;
            if(!poiOutlinePens.TryGetValue(penKey,out outline)) {
                var ink=cluster.Mixed?WpfFieldTheme.Amber:new SolidColorBrush(Color.FromRgb((byte)(color>>16),(byte)(color>>8),(byte)color));
                outline=new Pen(ink,2);outline.Freeze();if(poiOutlinePens.Count>=256)poiOutlinePens.Clear();poiOutlinePens.Add(penKey,outline);
            }
            cluster.Outline=outline;cluster.IconColor=color;
            }
        }
    }

    // Use the same calibrated pyramid as the main map, decoding only visible tiles.
    internal sealed class WpfTerrainTiles:IDisposable {
        sealed class Level { internal int Width,Height,Columns,Rows;internal long[] Offsets;internal int[] Lengths; }
        sealed class Tile { internal long Key;internal BitmapSource Image; }
        readonly Stream stream;
        readonly List<Level> levels=new List<Level>();
        readonly Dictionary<long,LinkedListNode<Tile>> cache=new Dictionary<long,LinkedListNode<Tile>>();
        readonly LinkedList<Tile> recent=new LinkedList<Tile>();
        internal int CachedTileCount { get { return cache.Count; } }
        internal WpfTerrainTiles() {
            stream=InstallationAssets.Open("map-tiles.bin");
            if(stream==null||!stream.CanSeek)throw new InvalidDataException("SCUM map tiles are missing.");
            using(var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true)) {
                if(new string(reader.ReadChars(4))!="MTL2"||reader.ReadInt32()!=512)throw new InvalidDataException("Invalid map tile header.");
                int count=reader.ReadInt32();if(count<1||count>16)throw new InvalidDataException("Invalid map levels.");
                for(int i=0;i<count;i++) {
                    var level=new Level { Width=reader.ReadInt32(),Height=reader.ReadInt32(),Columns=reader.ReadInt32(),Rows=reader.ReadInt32() };
                    if(level.Width<1||level.Height<1||level.Columns!=(level.Width+511)/512||level.Rows!=(level.Height+511)/512||(long)level.Columns*level.Rows>4096)throw new InvalidDataException("Invalid map dimensions.");
                    int tiles=level.Columns*level.Rows;level.Offsets=new long[tiles];level.Lengths=new int[tiles];
                    for(int j=0;j<tiles;j++) { level.Offsets[j]=reader.ReadInt64();level.Lengths[j]=reader.ReadInt32();if(level.Lengths[j]<1||level.Lengths[j]>8*1024*1024||level.Offsets[j]<12||level.Offsets[j]>stream.Length-level.Lengths[j])throw new InvalidDataException("Invalid map tile."); }
                    levels.Add(level);
                }
            }
        }
        BitmapSource ReadTile(int levelIndex,int column,int row) {
            long key=((long)levelIndex<<32)|((long)row<<16)|(uint)column;LinkedListNode<Tile> cached;
            if(cache.TryGetValue(key,out cached)) { recent.Remove(cached);recent.AddFirst(cached);return cached.Value.Image; }
            var level=levels[levelIndex];int index=row*level.Columns+column;var bytes=new byte[level.Lengths[index]];stream.Position=level.Offsets[index];int read=0;
            while(read<bytes.Length) { int count=stream.Read(bytes,read,bytes.Length-read);if(count==0)throw new EndOfStreamException();read+=count; }
            BitmapSource image;using(var encoded=new MemoryStream(bytes,false)) { var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=encoded;bitmap.EndInit();bitmap.Freeze();image=bitmap; }
            cached=new LinkedListNode<Tile>(new Tile { Key=key,Image=image });recent.AddFirst(cached);cache.Add(key,cached);
            if(cache.Count>48) { var oldest=recent.Last;recent.RemoveLast();cache.Remove(oldest.Value.Key); }
            return image;
        }
        internal void Draw(DrawingContext dc,double width,double height,double left,double top,double side,double dpi=1) { DrawRotated(dc,width,height,left,top,side,dpi,0); }
        internal void DrawRotated(DrawingContext dc,double width,double height,double left,double top,double side,double dpi,double margin) {
            int index=0;for(int i=1;i<levels.Count;i++) { if(levels[i].Width<side*dpi)break;index=i; }
            var level=levels[index];double scale=side/level.Width;
            double x1=Math.Max(0,(-margin-left)/scale),y1=Math.Max(0,(-margin-top)/scale),x2=Math.Min(level.Width,(width+margin-left)/scale),y2=Math.Min(level.Height,(height+margin-top)/scale);
            if(x2<=x1||y2<=y1)return;
            int firstX=Math.Max(0,(int)Math.Floor(x1/512)),firstY=Math.Max(0,(int)Math.Floor(y1/512));
            int lastX=Math.Min(level.Columns-1,(int)Math.Ceiling(x2/512)-1),lastY=Math.Min(level.Rows-1,(int)Math.Ceiling(y2/512)-1);
            for(int row=firstY;row<=lastY;row++)for(int column=firstX;column<=lastX;column++) {
                var tile=ReadTile(index,column,row);double x=column*512,y=row*512;
                // A two-pixel gutter preserves bilinear sampling at tile edges.
                dc.PushClip(new RectangleGeometry(new Rect(left+x*scale,top+y*scale,Math.Min(512,level.Width-x)*scale,Math.Min(512,level.Height-y)*scale)));
                dc.DrawImage(tile,new Rect(left+Math.Max(0,x-2)*scale,top+Math.Max(0,y-2)*scale,tile.PixelWidth*scale,tile.PixelHeight*scale));dc.Pop();
            }
        }
        internal DrawingGroup Overview() { var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Draw(dc,905,905,0,0,905);var image=new RenderTargetBitmap(905,905,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();var drawing=new DrawingGroup();using(var dc=drawing.Open())dc.DrawImage(image,new Rect(0,0,1024,1024));drawing.Freeze();return drawing; }
        public void Dispose() { cache.Clear();recent.Clear();stream.Dispose(); }
    }

    internal sealed partial class WpfFieldConsoleRuntime:IDisposable {
        internal static WpfFieldConsoleRuntime Active;
        readonly MapWindow engine;
        DirectPositions privatePositions;
        RelayGroupStore relayGroups;
        readonly Dictionary<string,int> playerVisibility=new Dictionary<string,int>(StringComparer.Ordinal);
        PrivatePositions.Peer[] playerRoster=new PrivatePositions.Peer[0];
        int PlayerVisibility(string id) { int value;return id!=null&&playerVisibility.TryGetValue(id,out value)?value:31; }
        bool PlayerMarkerVisible(SharedMapMarker marker) { return (PlayerVisibility(marker.OwnerId)&(marker.Death?8:marker.Id==SharedMapMarker.GpsId?2:SquadState.IsStop(marker)?16:4))!=0; }
        void AddPlayers(Panel container) {
            if(privatePositions==null&&(relayGroups==null||relayGroups.Sessions.Length==0)&&playerRoster.Length==0)return;
            container.Children.Add(WpfFieldTheme.Text(Localization.Get("SharedPlayers"),11,WpfFieldTheme.Muted,true));
            if(playerRoster.Length==0)container.Children.Add(WpfFieldTheme.Text(Localization.Get("PlayerWaiting"),12,WpfFieldTheme.Muted));
            foreach(var player in playerRoster) {
                string ownerId=player.Id;var body=new StackPanel();body.Children.Add(WpfFieldTheme.Text(player.Name,16,null,true));
                var statusLabel=WpfFieldTheme.Text(PeerStatus(player),11,WpfFieldTheme.Muted);statusLabel.TextWrapping=TextWrapping.Wrap;statusLabel.TextTrimming=TextTrimming.None;playerStatusLabels[ownerId]=statusLabel;body.Children.Add(statusLabel);
                var follow=WpfFieldTheme.Button(S(followingPeer==ownerId?"SquadStopNav":"SquadFollow"),()=> { if(followingPeer==ownerId) { followingPeer=null;squadTarget=null;engine.WpfClear(); }else { engine.WpfClear();followingPeer=ownerId;activeStops=new SharedMapMarker[0];squadTarget=null;squadRoutePlanner.Clear();activeRouteSignature=null;nextSquadRoute=DateTime.MinValue; }filterIdentity=null;Refresh(); });follow.IsEnabled=player.Position!=null||followingPeer==ownerId;body.Children.Add(follow);
                var pills=new WrapPanel { Margin=new Thickness(0,8,0,0) };
                string[] labels={"PlayerLiveLocation","PlayerGps","PlayerSaved","PlayerDeaths","RouteList"};
                for(int index=0;index<labels.Length;index++) {
                    int bit=1<<index;bool selected=(PlayerVisibility(ownerId)&bit)!=0;
                    var pill=new ToggleButton { Content=Localization.Get(labels[index]),IsChecked=selected,Tag="player:"+ownerId+":"+bit,Margin=new Thickness(0,0,5,5),Padding=new Thickness(9,4,9,4),FontSize=12,Foreground=WpfFieldTheme.Ink,Background=selected?WpfFieldTheme.Amber:WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Cursor=Cursors.Hand };
                    var template=new ControlTemplate(typeof(ToggleButton));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));border.AppendChild(content);template.VisualTree=border;pill.Template=template;
                    System.Windows.Automation.AutomationProperties.SetName(pill,player.Name+": "+Localization.Get(labels[index]));
                    pill.Click+=(s,e)=> { int mask=PlayerVisibility(ownerId);playerVisibility[ownerId]=pill.IsChecked==true?mask|bit:mask&~bit;pill.Background=pill.IsChecked==true?WpfFieldTheme.Amber:WpfFieldTheme.Panel;lastRefresh=-1000;Refresh(); };pills.Children.Add(pill);
                }
                body.Children.Add(pills);var card=WpfFieldTheme.Card(body,new Thickness(10));card.Margin=new Thickness(0,8,0,10);container.Children.Add(card);
            }
        }
        internal Func<System.Threading.Tasks.Task<string>> publicAddressLookup=null;
        readonly Application application;
        readonly DrawingGroup geography;
        readonly WpfTerrainTiles terrain;
        readonly DispatcherTimer refresh;
        readonly bool diagnostic;
        readonly AppZoneFolder zoneFolder;
        System.Threading.Tasks.Task<string> zoneOpenRead;
        DateTime nextZoneOpenRead;
        bool zoneFileActive;
        bool hudMoving;
        long nextHudRaise,nextRefreshError;
        System.Drawing.Rectangle lastHudBounds;
        Window tactical,hud;
        Window settingsWindow;
        volatile bool settingsToggleOpen;
        WpfMapSurface tacticalMap,hudMap;
        StackPanel sidebar;
        Grid hudLayout;
        Border hudStatus;
        TextBlock targetTitle,targetDetail,hudLocation,hudSector,routePreviewTitle,routePreviewDetail;
        WpfSatnavTicker hudRoute;
        Border targetCard,routePreviewCard;
        Border hoverCard;
        TextBlock hoverInfo;
        void ShowHoverDetails(string text) { hoverInfo.Text=text??"";hoverCard.Visibility=string.IsNullOrWhiteSpace(text)?Visibility.Collapsed:Visibility.Visible; }
        readonly HashSet<string> expanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bunkers" };
        readonly List<Window> dialogs=new List<Window>();
        bool rebuilding,disposed,hudEnabled=true;
        readonly MapMouseShield mouseShield=new MapMouseShield();
        volatile bool tacticalMouseShieldActive;
        IntPtr gameMapWindow;
        Button followButton,zoomResetButton;
        Slider tacticalOpacity;
        bool syncingOpacity;
        string filterIdentity,tacticalVisualIdentity,hudVisualIdentity;
        readonly System.Diagnostics.Stopwatch refreshClock=System.Diagnostics.Stopwatch.StartNew();
        long lastRefresh=-1000;
        long lastRouteTextRefresh=-1000;
        Button deathRemovalButton;
        string chromeLanguage;
        IntPtr inputTacticalHandle;
        volatile IntPtr[] inputDialogHandles=new IntPtr[0];
        volatile bool inputEditingText;
        void RefreshInputOwnership() {
            application.Dispatcher.VerifyAccess();
            System.Threading.Interlocked.Exchange(ref inputTacticalHandle,tactical==null?IntPtr.Zero:new WindowInteropHelper(tactical).Handle);
            inputDialogHandles=dialogs.Select(w=>new WindowInteropHelper(w).Handle).Where(h=>h!=IntPtr.Zero).ToArray();
            var focused=Keyboard.FocusedElement as DependencyObject;var owner=focused==null?null:System.Windows.Window.GetWindow(focused);
            inputEditingText=owner!=null&&owner.IsVisible&&EditingText(Keyboard.FocusedElement);
        }
        static void RefreshLocalizedChrome(DependencyObject root) {
            var element=root as FrameworkElement;string tag=element==null?null:element.Tag as string;
            if(tag!=null&&(tag.StartsWith("text:")||tag.StartsWith("tooltip:"))) {
                string value=Localization.Get(tag.Substring(tag.IndexOf(':')+1));
                if(tag.StartsWith("text:")) { var text=element as TextBlock;var content=element as ContentControl;if(text!=null)text.Text=value;else if(content!=null)content.Content=value; }
                element.ToolTip=value;System.Windows.Automation.AutomationProperties.SetName(element,value);
            }
            foreach(object child in LogicalTreeHelper.GetChildren(root)) { var dependency=child as DependencyObject;if(dependency!=null)RefreshLocalizedChrome(dependency); }
        }
        static string VisualIdentity(WpfMapState state) {
            var key=new System.Text.StringBuilder();key.Append(Localization.Current);
            foreach(var field in typeof(WpfMapState).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)) {
                if(field.Name=="Updated")continue;var value=field.GetValue(state);
                if(value==null) { key.Append("null|");continue; }
                if(value is Position) { var p=(Position)value;key.Append(p.X).Append(',').Append(p.Y).Append(',').Append(p.Yaw); }
                else if(value is ScumMapStore)key.Append(((ScumMapStore)value).GetFilterHashKey());
                else if(value is PrivatePositions.Peer[])foreach(var peer in (PrivatePositions.Peer[])value)key.Append(peer.Name).Append(peer.Position.X).Append(peer.Position.Y).Append(peer.Position.Yaw);
                else if(value is SquadRoutePlan)key.Append(((SquadRoutePlan)value).Revision);
                else if(value is SquadRouteDrawing[])foreach(var route in (SquadRouteDrawing[])value)key.Append(route.Key).Append(route.Name).Append(route.Shared).Append(route.Plan.Revision);
                else if(value is SharedMapMarker[])foreach(var marker in (SharedMapMarker[])value)key.Append('|').Append(marker.OwnerId).Append('|').Append(marker.Owner).Append('|').Append(marker.Id).Append('|').Append(marker.Name).Append('|').Append(marker.Death).Append('|').Append(marker.X).Append('|').Append(marker.Y);
                else if(value is MapZone[]||value is DrawingPoint[]||value is WpfZoneOption[])key.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value));
                else key.Append(value);
                key.Append('|');
            }
            return key.ToString();
        }
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="ShowWindow")]static extern bool ShowNativeWindow(IntPtr window,int command);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")]static extern bool ClipCursor(IntPtr rectangle);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
        internal WpfFieldConsoleRuntime(string folder,bool diagnostic=false) {
            this.diagnostic=diagnostic;application=Application.Current??new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };application.Resources.MergedDictionaries.Add(WpfFieldTheme.Resources());
            engine=new MapWindow(folder,diagnostic);var handle=engine.Handle;engine.WpfInitializeDefaults();
            if(!diagnostic)zoneFolder=new AppZoneFolder(AppDomain.CurrentDomain.BaseDirectory);
            if(diagnostic)((System.Windows.Forms.Timer)typeof(MapWindow).GetField("timer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(engine)).Stop();
            RoadRouter.Instance.InitializeFromResource();terrain=new WpfTerrainTiles();geography=terrain.Overview();
            string fonts=Path.Combine(folder,"field-console-fonts");Directory.CreateDirectory(fonts);
            foreach(string font in new[]{"BarlowSemiCondensed-Regular.ttf","BarlowSemiCondensed-SemiBold.ttf"})using(var stream=typeof(MapWindow).Assembly.GetManifestResourceStream(font))if(stream!=null)using(var file=File.Create(Path.Combine(fonts,font)))stream.CopyTo(file);
            application.Resources[typeof(Window)]=new Style(typeof(Window)) { Setters={new Setter(Control.FontFamilyProperty,new FontFamily(new Uri(fonts+Path.DirectorySeparatorChar),"./#Barlow Semi Condensed")),new Setter(Control.ForegroundProperty,WpfFieldTheme.Ink),new Setter(Control.BackgroundProperty,WpfFieldTheme.Ground),new Setter(Control.FontSizeProperty,15.0)} };
            tactical=BuildTactical();hud=BuildHud();
            tactical.IsVisibleChanged+=(s,e)=> { tacticalMouseShieldActive=tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized;if(!tactical.IsVisible)tacticalMap.CloseContextMenu(); };
            tactical.StateChanged+=(s,e)=>tacticalMouseShieldActive=tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized;
            tactical.Closed+=(s,e)=>tacticalMouseShieldActive=false;
            refresh=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) };refresh.Tick+=(s,e)=> {
                // DispatcherTimer does not schedule its next tick if its callback escapes.
                try { Refresh(); }catch(Exception error) { if(refreshClock.ElapsedMilliseconds>=nextRefreshError) { nextRefreshError=refreshClock.ElapsedMilliseconds+10000;Program.LogException("WpfRefresh",error); } }
            };
            if(!diagnostic)refresh.Start();
        }
        internal static void Run(string folder) {
            using(var runtime=new WpfFieldConsoleRuntime(folder)) {
                Active=runtime;
                runtime.StartViews();
                Program.CompletePostUpdate(folder,runtime.ShowPostUpdate);
                if(runtime.engine.WpfNeedsOnboarding)runtime.ShowOnboarding();
                runtime.application.Run();Active=null;
            }
        }
        internal Window BuildPostUpdate(string result) {
            var window=Window(Localization.Get("UpdateCheckTitle"),560,340);var layout=new DockPanel { Margin=new Thickness(24) };
            var done=WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true);done.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(done,Dock.Bottom);layout.Children.Add(done);
            var text=WpfFieldTheme.Text(result,16);text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;layout.Children.Add(new ScrollViewer { Content=text,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });window.Content=layout;return window;
        }
        void ShowPostUpdate(string result) { Modal(BuildPostUpdate(result)); }
        internal bool SuppressMapMouse(int message,System.Drawing.Point point,uint data) {
            // Called on the mouse hook thread: never read WPF dependency properties here.
            if(!tacticalMouseShieldActive)return mouseShield.Suppress(message,data,false,false,false);
            bool active=TacticalMouseShieldEnabled(tacticalMouseShieldActive,Native.IsOurWindow(Native.GetForegroundWindow()));
            if(!active)return mouseShield.Suppress(message,data,false,false,false);
            IntPtr target=Native.WindowFromPoint(point);
            bool inside=Native.IsOurWindow(target);
            // The game HWND was validated on the UI thread when opening the map.
            // Never enumerate processes or touch controls in a low-level hook.
            IntPtr game=System.Threading.Interlocked.CompareExchange(ref gameMapWindow,IntPtr.Zero,IntPtr.Zero);
            return mouseShield.Suppress(message,data,true,inside,!inside&&game!=IntPtr.Zero&&GetAncestor(target,2)==game);
        }
        internal static bool TacticalMouseShieldEnabled(bool visible,bool appFocused) { return visible&&appFocused; }
        internal static bool ShouldHideTactical(bool visible,bool gameFocused,bool appFocused) { return visible&&!gameFocused&&!appFocused; }
        internal void SyncTacticalFocus(bool gameFocused,bool appFocused) {
            if(ShouldHideTactical(tactical.IsVisible,gameFocused,appFocused)) { tacticalMouseShieldActive=false;tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false); }
            else if(tactical.IsVisible)tactical.Topmost=true;
        }
        internal void SetTactical(bool active) { if(active) { if(!tactical.IsVisible)ToggleTactical(); }else CloseTactical(); }
        internal bool TryDismissTactical(int key,int scan,bool chatPaused) {
            if(!tactical.IsVisible||dialogs.Count!=0||EditingText(Keyboard.FocusedElement))return false;
            var binding=engine.WpfSnapshot(false);bool mapKey=PhysicalKeyCapture.Matches(key,scan,binding.MapKey,binding.MapScan);
            if(key!=0x1B&&key!=0x09&&(!mapKey||chatPaused))return false;
            if(key==0x1B&&tacticalMap.ContextMenuOpen) { tacticalMap.CloseContextMenu();return true; }
            CloseTactical(mapKey&&new WindowInteropHelper(tactical).Handle==Native.GetForegroundWindow());return true;
        }
        static IntPtr TacticalWindowMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
            // MA_NOACTIVATE keeps keyboard/held movement in SCUM while still
            // delivering this mouse click to the map's controls.
            if(message==0x21) {handled=true;return new IntPtr(3);}
            return IntPtr.Zero;
        }
        internal void CloseTactical(bool closeGameMap=false) { tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false);if(hudEnabled)hud.Show();if(closeGameMap&&!diagnostic&&gameMapWindow!=IntPtr.Zero) { var binding=engine.WpfSnapshot(false);if(!binding.TrackingPaused)Native.ReturnMapKey(gameMapWindow,binding.MapKey,binding.MapScan); }Refresh(); }
        internal System.Drawing.Rectangle LastMonitorBounds;
        internal void FitTacticalToMonitor() {
            var monitor=System.Windows.Forms.Screen.FromHandle(gameMapWindow==IntPtr.Zero?Native.GetForegroundWindow():gameMapWindow);
            var bounds=monitor.Bounds;LastMonitorBounds=bounds;tactical.WindowState=WindowState.Normal;tactical.ResizeMode=ResizeMode.NoResize;tactical.WindowStartupLocation=WindowStartupLocation.Manual;
            if(diagnostic) { bounds.X=-32000;bounds.Y=-32000; }
            var handle=new WindowInteropHelper(tactical).EnsureHandle();
            if(IsIconic(handle))ShowNativeWindow(handle,4); // SW_SHOWNOACTIVATE clears native minimization.
            // WPF can retain the iconic window size after minimization. Update its
            // layout properties as well as the native bounds before rendering again.
            var source=HwndSource.FromHwnd(handle);var size=source.CompositionTarget.TransformFromDevice.Transform(new Point(bounds.Width,bounds.Height));
            tactical.Width=size.X;tactical.Height=size.Y;
            SetWindowPos(handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010);
            tactical.UpdateLayout();
        }
        internal void StartViews() { tactical.Hide();engine.WpfTactical(false);if(hudEnabled)hud.Show();Refresh(); }
        internal static bool ShouldShowHud(bool enabled,bool tacticalVisible) { return enabled&&!tacticalVisible; }
        internal static System.Drawing.Rectangle RecoverHudBounds(System.Drawing.Rectangle desired,System.Drawing.Rectangle[] monitors) {
            if(monitors==null||monitors.Length==0)return desired;
            foreach(var monitor in monitors) { var visible=System.Drawing.Rectangle.Intersect(desired,monitor);if(visible.Width>=Math.Min(monitor.Width,desired.Width)&&visible.Height>=Math.Min(monitor.Height,desired.Height))return desired; }
            var nearest=monitors.OrderBy(m=>Math.Pow(Math.Max(m.Left-desired.Left,Math.Max(0,(double)desired.Left-m.Right)),2)+Math.Pow(Math.Max(m.Top-desired.Top,Math.Max(0,(double)desired.Top-m.Bottom)),2)).First();
            return new System.Drawing.Rectangle(Math.Max(nearest.Left,Math.Min(desired.Left,nearest.Right-desired.Width)),Math.Max(nearest.Top,Math.Min(desired.Top,nearest.Bottom-desired.Height)),desired.Width,desired.Height);
        }
        bool TacticalVisible { get { return tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized&&!IsIconic(new WindowInteropHelper(tactical).Handle); } }
        void SyncHudVisibility(bool force=false) {
            bool show=ShouldShowHud(hudEnabled,TacticalVisible)&&!hudMoving;
            if(!show) { if(hud.IsVisible)hud.Hide();return; }
            bool restore=!hud.IsVisible||hud.WindowState==WindowState.Minimized;
            if(hud.WindowState!=WindowState.Normal)hud.WindowState=WindowState.Normal;
            if(!hud.IsVisible)hud.Show();hud.Topmost=true;
            var handle=new WindowInteropHelper(hud).EnsureHandle();if(IsIconic(handle)) { ShowNativeWindow(handle,4);restore=true; }
            if(force||restore) { hudVisualIdentity=null;hudMap.InvalidateVisual(); }
            if(diagnostic)return;
            var state=engine.WpfSnapshot(false);hud.Width=state.HudWidth;hud.Height=state.HudHeight+(state.Status?60:0);
            var source=HwndSource.FromHwnd(handle);if(source==null||source.CompositionTarget==null)return;
            var size=source.CompositionTarget.TransformToDevice.Transform(new Point(hud.Width,hud.Height));
            var bounds=RecoverHudBounds(new System.Drawing.Rectangle(state.HudLeft,state.HudTop,Math.Max(1,(int)Math.Ceiling(size.X)),Math.Max(1,(int)Math.Ceiling(size.Y))),System.Windows.Forms.Screen.AllScreens.Select(s=>s.WorkingArea).ToArray());
            if(bounds.X!=state.HudLeft||bounds.Y!=state.HudTop)engine.WpfMoveHud(bounds.X,bounds.Y);
            bool raise=dialogs.Count==0&&(force||restore||refreshClock.ElapsedMilliseconds>=nextHudRaise);
            if(raise)nextHudRaise=refreshClock.ElapsedMilliseconds+2000;
            if(raise||restore||bounds!=lastHudBounds)SetWindowPos(handle,raise?new IntPtr(-1):IntPtr.Zero,bounds.X,bounds.Y,0,0,(uint)(0x0001|0x0010|0x0040|(raise?0:0x0004)));
            lastHudBounds=bounds;
        }
        internal void ToggleHud() {
            bool recover=!hudEnabled||!hud.IsVisible||hud.WindowState==WindowState.Minimized||IsIconic(new WindowInteropHelper(hud).Handle)||TacticalVisible;
            if(TacticalVisible) { tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false); }
            hudEnabled=recover;SyncHudVisibility(true);
        }
        Window Window(string title,double w,double h) {
            var window=new Window { Title=title,Width=w,Height=h,MinWidth=Math.Min(w,420),MinHeight=Math.Min(h,300),WindowStartupLocation=WindowStartupLocation.CenterScreen,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.CanResizeWithGrip,Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink,FlowDirection=Localization.Current==AppLanguage.Arabic?FlowDirection.RightToLeft:FlowDirection.LeftToRight };
            window.SourceInitialized+=(s,e)=>RefreshInputOwnership();
            window.GotKeyboardFocus+=(s,e)=>RefreshInputOwnership();window.LostKeyboardFocus+=(s,e)=>inputEditingText=EditingText(e.NewFocus);
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape) { if(window==tactical&&tacticalMap!=null&&tacticalMap.ContextMenuOpen)tacticalMap.CloseContextMenu();else window.Close();e.Handled=true; } };
            return window;
        }
        Window BuildTactical() {
            var window=Window("SCUM MiniMap — Field console",1280,850);window.MinWidth=900;window.MinHeight=620;window.Topmost=true;window.ShowActivated=false;
            window.AllowsTransparency=true;window.Background=Brushes.Transparent;
            window.SourceInitialized+=(s,e)=> {
                var handle=new WindowInteropHelper(window).Handle;
                SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000);
                HwndSource.FromHwnd(handle).AddHook(TacticalWindowMessage);
            };
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(320) });
            tacticalMap=new WpfMapSurface { BaseMap=geography,Terrain=terrain };
            tacticalMap.RouteStopAt=AddSquadStop;tacticalMap.PingAt=(kind,p)=>SendPing(kind,p);
            tacticalMap.RouteNavigate=StartSquadRoute;tacticalMap.RouteRemove=RemoveOwnRouteStop;tacticalMap.RouteKind=ChangeOwnRouteKind;tacticalMap.RouteUndo=UndoOwnRouteStop;tacticalMap.RouteClear=ClearOwnRoute;tacticalMap.NavigatePeer=FollowSquadPeer;
            tacticalMap.ClearRouteRequested=()=> { StopSquadNavigation();Refresh(); };tacticalMap.DeleteWaypointRequested=marker=> { engine.WpfDeleteWaypoint(marker);Refresh(); };
            tacticalMap.NavigateAt=p=> { engine.WpfTarget(engine.WpfClickedTarget(p,(float)(8/Math.Max(1,Math.Min(tacticalMap.ActualWidth,tacticalMap.ActualHeight)*engine.WpfSnapshot(true).Zoom))));Refresh(); };tacticalMap.WaypointAt=p=>ShowAddWaypoint(p);tacticalMap.ZoomRequested=d=> { engine.WpfZoom(true,d);Refresh(); };tacticalMap.PanChanged=p=> { engine.WpfPan(p);Refresh(); };
            tacticalMap.NavigateShared=sharedTarget=> { engine.WpfTarget(sharedTarget);Refresh(); };
            // The sidebar overlays the full monitor viewport; toggling it must not move the camera.
            var scene=new Grid();Grid.SetColumnSpan(scene,2);scene.Children.Add(tacticalMap);
            var contextLayer=new Canvas();tacticalMap.ContextHost=contextLayer;Grid.SetColumnSpan(contextLayer,2);Panel.SetZIndex(contextLayer,200);grid.Children.Add(contextLayer);
            window.PreviewMouseDown+=(s,e)=> { if(tacticalMap.DismissContextOutside(e.GetPosition(contextLayer))&&e.ChangedButton==MouseButton.Left)e.Handled=true; };
            var target=new Grid();target.ColumnDefinitions.Add(new ColumnDefinition());target.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var text=new StackPanel();text.Children.Add(WpfFieldTheme.Text(Localization.Get("FieldTarget"),11,WpfFieldTheme.Muted,true));targetTitle=WpfFieldTheme.Text("",21,null,true);targetDetail=WpfFieldTheme.Text("",14,WpfFieldTheme.Muted);targetDetail.TextWrapping=TextWrapping.Wrap;targetDetail.TextTrimming=TextTrimming.None;text.Children.Add(targetTitle);text.Children.Add(targetDetail);target.Children.Add(text);
            ((FrameworkElement)text.Children[0]).Tag="text:FieldTarget";
            var clear=WpfFieldTheme.Button(Localization.Get("ClearWaypoint"),()=> { StopSquadNavigation();Refresh(); });clear.MaxWidth=112;clear.FontSize=13;Grid.SetColumn(clear,1);target.Children.Add(clear);
            clear.Tag="text:ClearWaypoint";
            var topCards=new StackPanel { Width=408,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(24) };
            targetCard=WpfFieldTheme.Card(target);topCards.Children.Add(targetCard);
            targetCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };
            var preview=new StackPanel();routePreviewTitle=WpfFieldTheme.Text("",17,null,true);preview.Children.Add(routePreviewTitle);
            routePreviewDetail=WpfFieldTheme.Text("",14,WpfFieldTheme.Muted);routePreviewDetail.TextWrapping=TextWrapping.Wrap;routePreviewDetail.TextTrimming=TextTrimming.None;preview.Children.Add(routePreviewDetail);
            var startPreview=WpfFieldTheme.Button(Localization.Get("SquadStartRoute"),()=>StartSquadRoute(squad.Stops));startPreview.Tag="text:SquadStartRoute";preview.Children.Add(startPreview);
            routePreviewCard=WpfFieldTheme.Card(preview);routePreviewCard.Margin=new Thickness(0,8,0,0);routePreviewCard.Visibility=Visibility.Collapsed;topCards.Children.Add(routePreviewCard);
            routePreviewCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };
            hoverInfo=WpfFieldTheme.Text("",15);hoverInfo.TextWrapping=TextWrapping.Wrap;hoverInfo.TextTrimming=TextTrimming.None;
            hoverCard=WpfFieldTheme.Card(new ScrollViewer { Content=hoverInfo,MaxHeight=240,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });hoverCard.Margin=new Thickness(0,8,0,0);hoverCard.Visibility=Visibility.Collapsed;topCards.Children.Add(hoverCard);scene.Children.Add(topCards);tacticalMap.HoverChanged=ShowHoverDetails;
            // The details card is an overlay on the map, not a dead area for its
            // context menu. Preserve normal left-click/scroll behavior in it.
            hoverCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };
            var controls=new StackPanel { Orientation=Orientation.Horizontal };controls.Children.Add(WpfFieldTheme.Button("−",()=> { engine.WpfZoom(true,-1);Refresh(); }));zoomResetButton=WpfFieldTheme.Button("1.0×",()=> { engine.WpfNativeTool(AppControlAction.ResetZoom);Refresh(); });controls.Children.Add(zoomResetButton);controls.Children.Add(WpfFieldTheme.Button("+",()=> { engine.WpfZoom(true,1);Refresh(); }));
            ((FrameworkElement)controls.Children[0]).Tag="tooltip:ControlZoomOut";zoomResetButton.Tag="tooltip:ControlResetZoom";((FrameworkElement)controls.Children[2]).Tag="tooltip:ControlZoomIn";
            followButton=WpfFieldTheme.Button(Localization.Get("WpfFollowPlayer"),()=> { engine.WpfFollow();Refresh(); });followButton.Tag="text:WpfFollowPlayer";controls.Children.Add(followButton);
            controls.Children.Add(WpfFieldTheme.Text(Localization.Get("OpacityLabel"),12,WpfFieldTheme.Muted));var opacity=new Slider { Minimum=0,Maximum=100,Value=engine.WpfSnapshot(true).Opacity,Width=90,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),ToolTip=Localization.Get("MapOpacity") };tacticalOpacity=opacity;
            ((FrameworkElement)controls.Children[controls.Children.Count-1]).Tag="text:OpacityLabel";opacity.Tag="tooltip:MapOpacity";
            opacity.ValueChanged+=(s,e)=> { if(syncingOpacity)return;engine.WpfTacticalOpacity((int)opacity.Value);Refresh(); };controls.Children.Add(opacity);
            foreach(var button in controls.Children.OfType<Button>()) { button.MinHeight=32;button.Padding=new Thickness(10,5,10,5);button.FontSize=14; }
            var toolbar=new DockPanel();var move=new Thumb { Width=18,Margin=new Thickness(0,0,4,0),Cursor=Cursors.SizeAll,Tag="tooltip:WpfMoveControls" };DockPanel.SetDock(move,Dock.Left);toolbar.Children.Add(move);
            var resize=new Thumb { Width=12,Margin=new Thickness(6,0,0,0),Cursor=Cursors.SizeNWSE,Tag="tooltip:WpfResizeControls" };DockPanel.SetDock(resize,Dock.Right);toolbar.Children.Add(resize);toolbar.Children.Add(controls);
            var chrome=WpfFieldTheme.Card(toolbar,new Thickness(8));chrome.HorizontalAlignment=HorizontalAlignment.Left;chrome.VerticalAlignment=VerticalAlignment.Bottom;chrome.Margin=new Thickness(24);scene.Children.Add(chrome);grid.Children.Add(scene);
            double controlScale=1;
            Action clampControls=()=> { chrome.Margin=new Thickness(Math.Max(8,Math.Min(chrome.Margin.Left,Math.Max(8,scene.ActualWidth-chrome.ActualWidth-8))),0,0,Math.Max(8,Math.Min(chrome.Margin.Bottom,Math.Max(8,scene.ActualHeight-chrome.ActualHeight-8)))); };
            move.DragDelta+=(s,e)=> { chrome.Margin=new Thickness(chrome.Margin.Left+e.HorizontalChange,0,0,chrome.Margin.Bottom-e.VerticalChange);clampControls(); };
            resize.DragDelta+=(s,e)=> { controlScale=Math.Max(.7,Math.Min(1.6,controlScale+e.HorizontalChange/400));chrome.LayoutTransform=new ScaleTransform(controlScale,controlScale); };
            chrome.SizeChanged+=(s,e)=>clampControls();scene.SizeChanged+=(s,e)=>clampControls();
            var toolbarMenu=new ContextMenu();var resetControls=new MenuItem { Header=Localization.Get("WpfResetControls") };toolbarMenu.Opened+=(s,e)=>resetControls.Header=Localization.Get("WpfResetControls");resetControls.Click+=(s,e)=> { controlScale=1;chrome.LayoutTransform=Transform.Identity;chrome.Margin=new Thickness(24); };toolbarMenu.Items.Add(resetControls);chrome.ContextMenu=toolbarMenu;
            sidebar=new StackPanel { Margin=new Thickness(24) };var scroller=new ScrollViewer { Content=sidebar,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=WpfFieldTheme.Panel };Grid.SetColumn(scroller,1);grid.Children.Add(scroller);window.Content=grid;
            var sidebarToggle=WpfFieldTheme.Button("›",()=> { bool collapse=scroller.Visibility==Visibility.Visible;scroller.Visibility=collapse?Visibility.Collapsed:Visibility.Visible;grid.ColumnDefinitions[1].Width=new GridLength(collapse?0:320); });sidebarToggle.Width=36;sidebarToggle.Height=36;sidebarToggle.MinHeight=36;sidebarToggle.Padding=new Thickness(0);sidebarToggle.HorizontalAlignment=HorizontalAlignment.Right;sidebarToggle.VerticalAlignment=VerticalAlignment.Top;sidebarToggle.Margin=new Thickness(12,54,332,0);sidebarToggle.ToolTip=Localization.Get("SidebarTitle");System.Windows.Automation.AutomationProperties.SetName(sidebarToggle,Localization.Get("SidebarTitle"));Grid.SetColumnSpan(sidebarToggle,2);grid.Children.Add(sidebarToggle);
            var closeOverlay=WpfFieldTheme.Button("×",()=>CloseTactical(true));closeOverlay.Width=36;closeOverlay.Height=36;closeOverlay.MinHeight=36;closeOverlay.Padding=new Thickness(0);closeOverlay.HorizontalAlignment=HorizontalAlignment.Right;closeOverlay.VerticalAlignment=VerticalAlignment.Top;closeOverlay.Margin=new Thickness(12,12,332,0);closeOverlay.ToolTip=Localization.Get("Close");System.Windows.Automation.AutomationProperties.SetName(closeOverlay,Localization.Get("Close"));Grid.SetColumnSpan(closeOverlay,2);grid.Children.Add(closeOverlay);
            Func<object,string,int,Action,Button> floating=(icon,label,y,click)=> { var button=WpfFieldTheme.Button("",click);button.Content=icon;button.Tag="tooltip:"+label;button.Width=button.Height=button.MinHeight=36;button.Padding=new Thickness(0);button.HorizontalAlignment=HorizontalAlignment.Right;button.VerticalAlignment=VerticalAlignment.Top;button.Margin=new Thickness(12,y,332,0);ToolTipService.SetInitialShowDelay(button,250);ToolTipService.SetShowOnDisabled(button,true);Grid.SetColumnSpan(button,2);grid.Children.Add(button);return button; };
            var settings=floating("⚙","Settings",96,ShowSettings);settings.FontFamily=new FontFamily("Segoe UI Symbol");settings.FontSize=23;
            var help=floating("?","OpenStartupGuide",138,ShowOnboarding);help.FontSize=23;
            var grave=new System.Windows.Shapes.Path { Data=Geometry.Parse("M 3,19 L 3,7 C 3,0 17,0 17,7 L 17,19 Z M 10,6 L 10,15 M 6,10 L 14,10"),Stroke=WpfFieldTheme.Ink,StrokeThickness=1.8,Width=20,Height=22,Stretch=Stretch.Uniform };
            deathRemovalButton=floating(grave,"ClearDeathMarkers",180,()=> { engine.ClearDeathMarkers();Refresh(); });
            sidebarToggle.Tag="tooltip:WpfCollapseSidebar";closeOverlay.Tag="tooltip:Close";
            sidebarToggle.Click+=(s,e)=> { bool open=scroller.Visibility==Visibility.Visible;sidebarToggle.Content=open?"›":"‹";sidebarToggle.Tag=open?"tooltip:WpfCollapseSidebar":"tooltip:WpfExpandSidebar";RefreshLocalizedChrome(sidebarToggle);sidebarToggle.Margin=new Thickness(12,54,open?332:12,0);closeOverlay.Margin=new Thickness(12,12,open?332:12,0);foreach(var button in new[]{settings,help,deathRemovalButton})button.Margin=new Thickness(12,button.Margin.Top,open?332:12,0); };
            RefreshLocalizedChrome(grid);
            window.Closing+=(s,e)=> { if(!disposed&&!diagnostic) { e.Cancel=true;window.Hide();engine.WpfTactical(false);if(hudEnabled)hud.Show(); } };
            return window;
        }
        Window BuildHud() {
            var window=new Window { Title="SCUM MiniMap HUD",Width=240,Height=300,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Left=48,Top=48 };
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(240) });layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(60) });
            hudMap=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain };layout.Children.Add(hudMap);var text=new StackPanel();var locationRow=new DockPanel { Margin=new Thickness(0,0,0,3) };hudSector=WpfFieldTheme.Text("",13,WpfFieldTheme.Amber,true);hudSector.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(hudSector,Dock.Left);locationRow.Children.Add(hudSector);hudLocation=WpfFieldTheme.Text("",14,null,true);locationRow.Children.Add(hudLocation);hudRoute=new WpfSatnavTicker();text.Children.Add(locationRow);text.Children.Add(hudRoute);
            var status=WpfFieldTheme.Card(text,new Thickness(12,5,12,5));status.Margin=new Thickness(0,6,0,0);Grid.SetRow(status,1);layout.Children.Add(status);hudLayout=layout;hudStatus=status;window.Content=layout;
            window.SourceInitialized+=(s,e)=> { IntPtr handle=new WindowInteropHelper(window).Handle;SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000|0x20); };
            window.Closing+=(s,e)=> { if(!disposed) { e.Cancel=true;window.Hide(); } };return window;
        }
        void RebuildSidebar(WpfMapState state) {
            playerStatusLabels.Clear();sidebar.Children.Clear();var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });heading.Children.Add(WpfFieldTheme.Text(Localization.Get("SidebarTitle"),24,null,true));var key=WpfFieldTheme.Card(WpfFieldTheme.Text(PhysicalKeyCapture.KeyName(state.MapKey),14),new Thickness(10,5,10,5));Grid.SetColumn(key,1);heading.Children.Add(key);sidebar.Children.Add(heading);
            var search=WpfFieldTheme.Button(Localization.Get("SearchTitle")+"  "+PhysicalKeyCapture.KeyName(state.Bindings.Key(AppControlAction.Search)),ShowSearch);search.Margin=new Thickness(0,18,0,22);sidebar.Children.Add(search);
            sidebar.Children.Add(WpfFieldTheme.Button(S("SquadTools"),ShowSquadTools));
            AddPlayers(sidebar);
            sidebar.Children.Add(WpfFieldTheme.Text(Localization.Get("SidebarLayers"),11,WpfFieldTheme.Muted,true));
            var customRow=new DockPanel();var customMaster=new CheckBox { IsChecked=state.Zones,Width=42,Tag="custom-zones:master",ToolTip=Localization.Get("ShowSavedZones") };customMaster.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfLayer("zones",true);Refresh(); } };customMaster.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfLayer("zones",false);Refresh(); } };DockPanel.SetDock(customMaster,Dock.Right);customRow.Children.Add(customMaster);
            var customExpand=WpfFieldTheme.Button((state.CustomZonesExpanded?"⌄  ":"›  ")+Localization.Get("ShowSavedZones"),()=> { engine.WpfExpandZones();filterIdentity=null;Refresh(); });customExpand.Tag="custom-zones:expand";customExpand.HorizontalContentAlignment=HorizontalAlignment.Left;customExpand.Padding=new Thickness(0,8,0,8);customExpand.Margin=new Thickness(0);customExpand.Background=Brushes.Transparent;customRow.Children.Add(customExpand);sidebar.Children.Add(customRow);
            if(state.CustomZonesExpanded&&state.CustomZones!=null)foreach(var group in state.CustomZones.GroupBy(z=>z.Layer).OrderBy(g=>g.Key,StringComparer.CurrentCultureIgnoreCase)) {
                string layer=group.Key;var layerToggle=new CheckBox { Content=layer,IsChecked=group.First().LayerEnabled,Margin=new Thickness(12,4,0,4),Tag="custom-zone-layer:"+layer };layerToggle.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneLayerToggle(layer,true);filterIdentity=null;Refresh(); } };layerToggle.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneLayerToggle(layer,false);filterIdentity=null;Refresh(); } };sidebar.Children.Add(layerToggle);
                foreach(var option in group.OrderBy(z=>z.Zone.DisplayName,StringComparer.CurrentCultureIgnoreCase)) {
                    string zoneKey=option.Key;var row=new CheckBox { Content=option.Zone.DisplayName,IsChecked=option.Enabled,Margin=new Thickness(28,2,0,2),Tag="custom-zone:"+zoneKey,ToolTip=option.Zone.DisplayName };
                    row.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneToggle(zoneKey,true);filterIdentity=null;Refresh(); } };row.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneToggle(zoneKey,false);filterIdentity=null;Refresh(); } };sidebar.Children.Add(row);
                }
            }
            AddLayer(sidebar,"waypoints",Localization.Get("ShowCustomWaypoints"),state.Waypoints);AddLayer(sidebar,"grid",Localization.Get("GridBorders"),state.Grid);AddLayer(sidebar,"history",Localization.Get("HistoryEnabled"),state.History);AddLayer(sidebar,"labels",Localization.Get("ShowZoneLabels"),state.Labels);
            var header=new DockPanel { Margin=new Thickness(0,22,0,12) };var off=WpfFieldTheme.Button(Localization.Get("DeselectAll"),()=>FilterAll(false));var all=WpfFieldTheme.Button(Localization.Get("SelectAll"),()=>FilterAll(true));off.Padding=all.Padding=new Thickness(10,4,10,4);off.MinHeight=all.MinHeight=30;DockPanel.SetDock(off,Dock.Right);DockPanel.SetDock(all,Dock.Right);header.Children.Add(off);header.Children.Add(all);header.Children.Add(WpfFieldTheme.Text(Localization.Get("SidebarCategories"),11,WpfFieldTheme.Muted,true));sidebar.Children.Add(header);
            header.LastChildFill=false;var categoryTitle=header.Children[2];header.Children.Remove(categoryTitle);DockPanel.SetDock(categoryTitle,Dock.Top);header.Children.Insert(0,categoryTitle);((FrameworkElement)categoryTitle).Margin=new Thickness(0,0,0,8);
            if(state.Store!=null)foreach(string section in state.Store.Sections) {
                var categories=state.Store.CategoriesBySection[section];bool open=expanded.Contains(section);int enabled=categories.Count(c=>state.Store.IsCategoryEnabled(c.Id));
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition());title.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });title.Children.Add(WpfFieldTheme.Text((open?"⌄  ":"›  ")+Localization.GetSectionName(section),16,null,true));var count=WpfFieldTheme.Text(enabled+" / "+categories.Count,12,WpfFieldTheme.Muted);Grid.SetColumn(count,1);title.Children.Add(count);
                var button=new Button { Content=title,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent,Foreground=WpfFieldTheme.Ink,Padding=new Thickness(0,8,0,8),BorderThickness=new Thickness(0) };
                string captured=section;button.Tag="poi-expand:"+section;button.Click+=(s,e)=> { if(!expanded.Remove(captured))expanded.Add(captured);filterIdentity=null;Refresh(); };
                var groupRow=new DockPanel();var groupToggle=new CheckBox { IsThreeState=true,IsChecked=enabled==0?(bool?)false:enabled==categories.Count?(bool?)true:null,Width=42,Margin=new Thickness(8,0,0,0),ToolTip=Localization.GetSectionName(section),Tag="poi-section:"+section };
                System.Windows.Automation.AutomationProperties.SetName(groupToggle,Localization.GetSectionName(section));groupToggle.Click+=(s,e)=>FilterSection(captured,enabled!=categories.Count);DockPanel.SetDock(groupToggle,Dock.Right);groupRow.Children.Add(groupToggle);groupRow.Children.Add(button);sidebar.Children.Add(groupRow);
                if(open)foreach(var category in categories.OrderBy(c=>c.Name)) {
                    var row=new CheckBox { Content=Localization.GetCategoryName(category.Name)+"  "+category.MarkerCount,IsChecked=state.Store.IsCategoryEnabled(category.Id),Margin=new Thickness(16,0,0,0),Tag="poi-category:"+category.Id };
                    int id=category.Id;row.Checked+=(s,e)=>Category(id,true);row.Unchecked+=(s,e)=>Category(id,false);sidebar.Children.Add(row);
                }
                sidebar.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(0,6,0,6) });
            }
        }
        void AddLayer(Panel container,string key,string label,bool value) { var box=new CheckBox { Content=label,IsChecked=value,Margin=new Thickness(0,3,0,3) };box.Checked+=(s,e)=> { engine.WpfLayer(key,true);Refresh(); };box.Unchecked+=(s,e)=> { engine.WpfLayer(key,false);Refresh(); };container.Children.Add(box); }
        void Category(int id,bool enabled) { if(rebuilding)return;engine.WpfSnapshot(true).Store.SetCategoryEnabled(id,enabled);engine.WpfSave();filterIdentity=null;Refresh(); }
        void FilterAll(bool enabled) { var store=engine.WpfSnapshot(true).Store;if(store==null)return;store.SetAllEnabled(enabled);engine.WpfSave();filterIdentity=null;Refresh(); }
        void FilterSection(string section,bool enabled) { var store=engine.WpfSnapshot(true).Store;if(store==null||!store.CategoriesBySection.ContainsKey(section))return;foreach(var category in store.CategoriesBySection[section])store.SetCategoryEnabled(category.Id,enabled);engine.WpfSave();filterIdentity=null;Refresh(); }
        MapZone lastPresentedTarget;
        internal void Refresh() {
            application.Dispatcher.VerifyAccess();
            if(disposed)return;
            RefreshSetupPreview();
            // The native controller also requests refreshes. Coalesce those with the WPF timer.
            bool targetChanged=!object.ReferenceEquals(lastPresentedTarget,engine.WpfCurrentTarget);
            if(!diagnostic&&!targetChanged&&refreshClock.ElapsedMilliseconds-lastRefresh<100)return;
            lastRefresh=refreshClock.ElapsedMilliseconds;
            if(!diagnostic&&tactical.IsVisible) { var foreground=Native.GetForegroundWindow();SyncTacticalFocus(Native.IsGameWindow(foreground),Native.IsOurWindow(foreground)); }
            List<MapZone> refreshedZones;
            if(zoneFolder!=null&&zoneFolder.Poll(out refreshedZones))engine.WpfRefreshFolderZones(refreshedZones);
            if(!diagnostic&&!zoneFileActive&&dialogs.Count==0) {
                if(zoneOpenRead!=null&&zoneOpenRead.IsCompleted) {
                    string request=null;try { request=zoneOpenRead.GetAwaiter().GetResult(); }catch(Exception error) { Program.LogException("ZoneOpenQueue",error); }finally { zoneOpenRead=null; }
                    if(request!=null) { ShowZoneFile(request);return; }
                }
                if(zoneOpenRead==null&&DateTime.UtcNow>=nextZoneOpenRead) { nextZoneOpenRead=DateTime.UtcNow.AddSeconds(1);string profile=engine.WpfDataFolder;zoneOpenRead=System.Threading.Tasks.Task.Run(()=>ZoneFileOpen.Take(profile)); }
            }
            if(relayGroups!=null)relayGroups.SynchronizeNames();
            var sharingSessions=(relayGroups==null?new DirectPositions[0]:relayGroups.Sessions).Concat(privatePositions==null?new DirectPositions[0]:new[]{privatePositions}).ToArray();
            if(sharingSessions.Length>0) {
                var sample=engine.WpfSnapshot(false);var markers=OwnSquadPublication().Concat(engine.WpfSharedMarkers()).Take(128).ToArray();foreach(var session in sharingSessions) { session.Update(sample.Tracking&&!sample.TrackingPaused?sample.Position:null,sample.Updated);session.UpdateMarkers(relayGroups==null?markers:relayGroups.MarkersFor(session,markers)); }
            }
            UpdateSquad();UpdateRoutePreviews();foreach(var peer in playerRoster) { TextBlock label;if(playerStatusLabels.TryGetValue(peer.Id,out label))label.Text=PeerStatus(peer); }
            if(!diagnostic) {
                SyncHudVisibility();
                if(!TacticalVisible&&!hud.IsVisible)return;
            }
            bool fullView=diagnostic||TacticalVisible;
            var state=engine.WpfSnapshot(fullView);
            targetChanged|=!object.ReferenceEquals(lastPresentedTarget,state.Target);
            lastPresentedTarget=state.Target;
            syncingOpacity=true;try { tacticalOpacity.Value=state.Opacity;tacticalOpacity.IsEnabled=state.Zoom<=1; }finally { syncingOpacity=false; }
            var compact=fullView?engine.WpfSnapshot(false):state;
            if(sharingSessions.Length>0) {

                state.Peers=compact.Peers=(privatePositions==null?new PrivatePositions.Peer[0]:privatePositions.Snapshot()).Concat(relayGroups==null?new PrivatePositions.Peer[0]:relayGroups.Roster(true)).Where(peer=>(PlayerVisibility(peer.Id)&1)!=0).ToArray();
                state.SharedMarkers=compact.SharedMarkers=squad.Receive((privatePositions==null?new SharedMapMarker[0]:privatePositions.SnapshotMarkers()).Concat(relayGroups==null?new SharedMapMarker[0]:relayGroups.Markers()).Where(PlayerMarkerVisible)).Concat(squad.Local().Select(m=> { m.Owner=S("PrivateGroupSurvivor");return m; })).ToArray();
            }
            else state.SharedMarkers=compact.SharedMarkers=squad.Local();
            AddRouteState(state,compact);
            deathRemovalButton.IsEnabled=state.Saved.Any(marker=>marker.IsDeathMarker);if(chromeLanguage!=Localization.CurrentCode) { chromeLanguage=Localization.CurrentCode;RefreshLocalizedChrome(tactical);if(settingsWindow!=null)RefreshLocalizedChrome(settingsWindow); }
            if(diagnostic||TacticalVisible) { string key=VisualIdentity(state);if(key!=tacticalVisualIdentity) { tacticalVisualIdentity=key;tacticalMap.State=state;tacticalMap.InvalidateVisual(); } }
            if(diagnostic||hud.IsVisible) { string key=VisualIdentity(compact);if(key!=hudVisualIdentity) { hudVisualIdentity=key;hudMap.State=compact;hudMap.InvalidateVisual(); } }
            hud.Width=compact.HudWidth;hud.Height=compact.HudHeight+(compact.Status?60:0);
            hudLayout.RowDefinitions[0].Height=new GridLength(compact.StatusAbove&&compact.Status?60:compact.HudHeight);
            hudLayout.RowDefinitions[1].Height=new GridLength(compact.StatusAbove&&compact.Status?compact.HudHeight:compact.Status?60:0);
            Grid.SetRow(hudMap,compact.StatusAbove&&compact.Status?1:0);Grid.SetRow(hudStatus,compact.StatusAbove&&compact.Status?0:1);hudStatus.Visibility=compact.Status?Visibility.Visible:Visibility.Collapsed;
if(followButton!=null)followButton.Background=engine.WpfFollowing?WpfFieldTheme.Amber:WpfFieldTheme.Panel;
            if(zoomResetButton!=null)zoomResetButton.Content=state.Zoom.ToString("0.0",CultureInfo.CurrentCulture)+"×";
            targetCard.Visibility=state.Target==null?Visibility.Collapsed:Visibility.Visible;
            if(hudRoute.Foreground==null||((SolidColorBrush)hudRoute.Foreground).Color!=Color.FromArgb((byte)(state.RouteColor>>24),(byte)(state.RouteColor>>16),(byte)(state.RouteColor>>8),(byte)state.RouteColor))hudRoute.Foreground=new SolidColorBrush(Color.FromArgb((byte)(state.RouteColor>>24),(byte)(state.RouteColor>>16),(byte)(state.RouteColor>>8),(byte)state.RouteColor));
            targetTitle.Text=state.Target==null?"":state.Target.DisplayName;targetDetail.Text=state.RouteSummary;
            routePreviewCard.Visibility=state.LocalRoute.Length>0&&state.ActiveSquadRoute==null?Visibility.Visible:Visibility.Collapsed;
            routePreviewTitle.Text=Localization.Get("OwnRoute")+" · "+Localization.Get(squad.Shared?"SquadRoute":"SoloRoute")+" · "+state.LocalRoute.Length+" / 16";routePreviewTitle.Foreground=squad.Shared?WpfFieldTheme.Cyan:WpfFieldTheme.Amber;routePreviewDetail.Text=state.OwnRouteSummary??"";
            string sector=compact.Position==null?"":DestinationSearch.Sector(MapWindow.ToMap(compact.Position));string locationLabel=compact.Location;
            if(sector.Length>0&&locationLabel.StartsWith(sector+" - ",StringComparison.Ordinal))locationLabel=locationLabel.Substring(sector.Length+3);
            hudSector.Text=sector;hudSector.Visibility=sector.Length==0?Visibility.Collapsed:Visibility.Visible;hudLocation.Text=locationLabel;hudLocation.ToolTip=compact.Location;
            if(targetChanged||refreshClock.ElapsedMilliseconds-lastRouteTextRefresh>=500||state.Target==null||compact.TrackingPaused) {
                lastRouteTextRefresh=refreshClock.ElapsedMilliseconds;
                hudRoute.Text=compact.TrackingPaused?compact.TrackingStatus:state.Target==null?"": "→ "+state.Target.DisplayName+" · "+state.RouteSummary;
            }
            string identity=(state.Store==null?"":state.Store.GetFilterHashKey())+"|"+state.Grid+state.Zones+state.Waypoints+state.History+state.Labels+Localization.Current+"|"+state.Saved.Length+"|"+state.Saved.Count(m=>m.IsDeathMarker)+"|"+state.MapKey+"|"+state.Bindings.Key(AppControlAction.Search);
            identity+="|"+state.CustomZonesExpanded+"|"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.CustomZones);
            identity+="|players:"+(sharingSessions.Length==0?"off":sharingSessions.Any(session=>session.Ready)?"ready":"waiting")+string.Join(";",playerRoster.Select(peer=>peer.Id+":"+peer.Name+":"+(peer.Position!=null)));
            if(fullView&&identity!=filterIdentity) { filterIdentity=identity;rebuilding=true;try { RebuildSidebar(state); }finally { rebuilding=false; } }
            
        }
        internal void Handle(AppControlAction action) {
            switch(action) {
                case AppControlAction.PingDanger:SendPing(0);break;case AppControlAction.PingLoot:SendPing(1);break;case AppControlAction.PingPickup:SendPing(2);break;
                case AppControlAction.Settings:ShowSettings();break;case AppControlAction.Search:ShowSearch();break;
                case AppControlAction.ToggleOverlay:ToggleHud();break;
                case AppControlAction.AddWaypoint:ShowAddWaypoint();break;case AppControlAction.ClearWaypoint:StopSquadNavigation();break;
                case AppControlAction.ZoomIn:engine.WpfZoom(TacticalVisible,1);break;case AppControlAction.ZoomOut:engine.WpfZoom(TacticalVisible,-1);break;
                case AppControlAction.StartupGuide:ShowOnboarding();break;case AppControlAction.ZoneEditor:ShowZoneWizard();break;default:engine.WpfNativeTool(action);break;
            }
            Refresh();
        }
        internal void RequestExit() { application.Shutdown(); }
        internal bool IsSettingsToggleKey(int key,int scan) { return settingsToggleOpen&&engine.WpfSettingsKeyMatches(key,scan); }
        void UpdateSettingsToggle() { settingsToggleOpen=settingsWindow!=null&&settingsWindow.IsVisible&&dialogs.Count==1&&dialogs[0]==settingsWindow;RefreshInputOwnership(); }
        internal void ToggleTactical(bool gameKey=false) {
            if(tactical.IsVisible) {
                bool forward=gameKey&&new WindowInteropHelper(tactical).Handle==Native.GetForegroundWindow();
                tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false);if(hudEnabled)hud.Show();
                if(forward&&!diagnostic) { var binding=engine.WpfSnapshot(false);Native.ReturnMapKey(gameMapWindow,binding.MapKey,binding.MapScan); }
            } else {
                gameMapWindow=Native.GameFocused()?Native.GetForegroundWindow():IntPtr.Zero;
                hud.Hide();engine.WpfTactical(true);engine.WpfFollow();tactical.WindowState=WindowState.Normal;tactical.Show();FitTacticalToMonitor();
            }
            Refresh();
        }

        internal bool OwnsForeground(IntPtr handle) {
            // Also called on the keyboard-hook thread. Only read published values.
            if(handle==IntPtr.Zero||inputEditingText)return false;
            return handle==System.Threading.Interlocked.CompareExchange(ref inputTacticalHandle,IntPtr.Zero,IntPtr.Zero)||Array.IndexOf(inputDialogHandles,handle)>=0;
        }
        internal static bool EditingText(IInputElement focus) { return focus is TextBox || focus is PasswordBox || focus is ComboBox; }
        void FocusInteractive(Window window) {
            if(diagnostic||!window.IsVisible)return;
            var handle=new WindowInteropHelper(window).Handle;
            var focused=Keyboard.FocusedElement as DependencyObject;
            window.Cursor=Cursors.Arrow;Native.ForceForeground(handle);window.Activate();
            if(focused!=null&&System.Windows.Window.GetWindow(focused)==window)Keyboard.Focus((IInputElement)focused);
            else { window.Focus();window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); }
            if(Native.GetForegroundWindow()==handle)ClipCursor(IntPtr.Zero);
        }
        void RestoreGameplayFocus() {
            if(diagnostic||gameMapWindow==IntPtr.Zero)return;
            if(Native.IsOurWindow(Native.GetForegroundWindow())&&Native.IsGameWindow(gameMapWindow))Native.SetForegroundWindow(gameMapWindow);
        }
        void Modal(Window window) {
            // A dialog opened from the HUD has no topmost tactical owner to keep it above SCUM.
            window.Topmost=true;window.ShowActivated=!diagnostic;
            if(diagnostic) { window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000; }
            if(dialogs.Count>0)window.Owner=dialogs[dialogs.Count-1];else if(tactical.IsVisible)window.Owner=tactical;
            if(window.Owner!=null&&!diagnostic&&window.WindowStartupLocation!=WindowStartupLocation.Manual)window.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            window.Loaded+=(s,e)=>FocusInteractive(window);
            window.ContentRendered+=(s,e)=>UpdateSettingsToggle();
            dialogs.Add(window);engine.WpfModal(true);
            UpdateSettingsToggle();
            var previousForeground=Native.GetForegroundWindow();
            try { window.ShowDialog(); }finally {
                dialogs.Remove(window);UpdateSettingsToggle();engine.WpfModal(dialogs.Count>0);engine.WpfSave();Refresh();
                if(dialogs.Count>0)FocusInteractive(dialogs[dialogs.Count-1]);else if(tactical.IsVisible)RestoreGameplayFocus();
                else if(!diagnostic&&Native.IsGameWindow(previousForeground))Native.SetForegroundWindow(previousForeground);
            }
        }
        void ShowSearch() { Modal(BuildSearch()); }
        Window BuildSearch() {
            var window=Window(Localization.Get("SearchTitle"),560,480);window.ResizeMode=ResizeMode.NoResize;var layout=new DockPanel { Margin=new Thickness(20) };
            var row=new DockPanel();var escape=WpfFieldTheme.Button("Esc",()=>window.Close());DockPanel.SetDock(escape,Dock.Right);row.Children.Add(escape);var input=new TextBox { MaxLength=120,Text=diagnostic?"grid D4":"" };row.Children.Add(input);DockPanel.SetDock(row,Dock.Top);layout.Children.Add(row);
            var hint=WpfFieldTheme.Text(Localization.Get("SearchHint"),12,WpfFieldTheme.Muted);hint.Margin=new Thickness(0,12,0,12);DockPanel.SetDock(hint,Dock.Top);layout.Children.Add(hint);
            var footer=new DockPanel { Margin=new Thickness(0,14,0,0) };var count=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);var list=new ListBox();
            Action choose=()=> { var item=list.SelectedItem as ListBoxItem;if(item==null)return;engine.WpfTarget((MapZone)item.Tag);window.Close(); };
            var go=WpfFieldTheme.Button(Localization.Get("SetWaypoint"),choose,true);var clear=WpfFieldTheme.Button(Localization.Get("ClearWaypoint"),()=> { engine.WpfClear();window.Close(); });clear.IsEnabled=engine.WpfSnapshot(true).Target!=null;DockPanel.SetDock(go,Dock.Right);DockPanel.SetDock(clear,Dock.Right);footer.Children.Add(go);footer.Children.Add(clear);footer.Children.Add(count);DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(list);
            Action populate=()=> { list.Items.Clear();var results=engine.WpfSearch(input.Text);var state=engine.WpfSnapshot(true);foreach(var result in results) { var text=new StackPanel();text.Children.Add(WpfFieldTheme.Text(result.DisplayName,17,null,true));string detail=result.Name.StartsWith("Grid ")?Localization.Get("SectorCentre"):DestinationSearch.Sector(result.Centroid)+" · "+(result.Subtitle??"");if(state.Position!=null&&!result.Name.StartsWith("Grid "))detail+=" · "+DestinationSearch.Distance(DestinationSearch.Metres(MapWindow.ToMap(state.Position),result.Centroid));text.Children.Add(WpfFieldTheme.Text(detail,13,WpfFieldTheme.Muted));list.Items.Add(new ListBoxItem { Content=text,Tag=result }); }if(list.Items.Count>0)list.SelectedIndex=0;go.IsEnabled=results.Count>0;count.Text=results.Count==0?Localization.Get("NoMatches"):results.Count.ToString(); };
            input.TextChanged+=(s,e)=>populate();input.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Down||e.Key==Key.Up) { list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(e.Key==Key.Down?1:-1)));list.ScrollIntoView(list.SelectedItem);e.Handled=true; }else if(e.Key==Key.Enter) { choose();e.Handled=true; } };list.MouseDoubleClick+=(s,e)=>choose();list.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter)choose(); };
            window.Content=WpfFieldTheme.Card(layout,new Thickness(0));window.Loaded+=(s,e)=>input.Focus();populate();return window;
        }
        void ShowSettings() {
            if(settingsWindow!=null) { if(settingsToggleOpen)settingsWindow.Close();else if(dialogs.Count>0)FocusInteractive(dialogs[dialogs.Count-1]);return; }
            settingsWindow=BuildSettings(0);
            try { Modal(settingsWindow); }finally { settingsWindow=null;settingsToggleOpen=false; }
        }
        void ShowMoveHud() {
            hudMoving=true;hud.Hide();
            try { Modal(BuildHudMoveEditor()); }finally { hudMoving=false;SyncHudVisibility(true);Refresh(); }
        }
        Window BuildHudMoveEditor() {
            var state=engine.WpfSnapshot(false);var window=Window(Localization.Get("WpfMoveMinimap"),state.HudWidth,state.HudHeight+(state.Status?60:0)+68);
            window.MinWidth=state.HudWidth;window.MinHeight=window.Height;window.ResizeMode=ResizeMode.NoResize;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=hud.Left;window.Top=hud.Top-28;
            var layout=new DockPanel();var grip=WpfFieldTheme.Text("↔  "+Localization.Get("WpfDragMinimap"),14,null,true);var handle=new Border { Background=WpfFieldTheme.Raised,Height=28,Cursor=Cursors.SizeAll,Child=grip };handle.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed)window.DragMove(); };DockPanel.SetDock(handle,Dock.Top);layout.Children.Add(handle);
            var done=WpfFieldTheme.Button(Localization.Get("Done"),()=> { var source=HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());var location=source.CompositionTarget.TransformToDevice.Transform(new Point(window.Left,window.Top+28));engine.WpfMoveHud((int)Math.Round(location.X),(int)Math.Round(location.Y));window.Close(); },true);DockPanel.SetDock(done,Dock.Bottom);layout.Children.Add(done);
            var preview=new Grid();preview.RowDefinitions.Add(new RowDefinition { Height=new GridLength(state.StatusAbove&&state.Status?60:state.HudHeight) });preview.RowDefinitions.Add(new RowDefinition { Height=new GridLength(state.StatusAbove&&state.Status?state.HudHeight:state.Status?60:0) });var map=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain,State=state };Grid.SetRow(map,state.StatusAbove&&state.Status?1:0);preview.Children.Add(map);var info=WpfFieldTheme.Card(WpfFieldTheme.Text(state.Location,14),new Thickness(12));Grid.SetRow(info,state.StatusAbove&&state.Status?0:1);info.Visibility=state.Status?Visibility.Visible:Visibility.Collapsed;preview.Children.Add(info);preview.Cursor=Cursors.SizeAll;preview.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed)window.DragMove(); };layout.Children.Add(preview);window.Content=layout;return window;
        }
        void ShowPrivatePositions() { Modal(BuildRelayGroups()); }
        Window BuildRelayGroups() {
            var window=Window(Localization.Get("PrivatePositions"),620,720);var body=new StackPanel { Margin=new Thickness(24) };
            var help=WpfFieldTheme.Text(Localization.Get("PrivateGroupHelp"),14,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;body.Children.Add(help);
            var form=new StackPanel();body.Children.Add(form);var status=WpfFieldTheme.Text("",13,WpfFieldTheme.Amber);status.TextWrapping=TextWrapping.Wrap;
            try { if(relayGroups==null)relayGroups=new RelayGroupStore(engine.WpfDataFolder); }
            catch { status.Text=Localization.Get("PrivatePositionInvalid");body.Children.Add(status);body.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close()));window.Content=body;return window; }
            form.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionName")));var name=new TextBox { Text=relayGroups.DisplayName,MaxLength=32,Margin=new Thickness(0,4,0,12) };name.Tag="group-name";form.Children.Add(name);
            form.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivateGroupCode")));var code=new PasswordBox { MaxLength=128,Margin=new Thickness(0,4,0,12),MinHeight=40,FontSize=16,Padding=new Thickness(12,9,12,9),Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink };code.Tag="group-code";form.Children.Add(code);
            form.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivateGroupNameLabel")));var groupLabel=new TextBox { MaxLength=48,Margin=new Thickness(0,4,0,12),Tag="new-group-label" };form.Children.Add(groupLabel);
            bool closed=false,busy=false;var rows=new StackPanel { Margin=new Thickness(0,12,0,12) };Action draw=null;
            Action<Func<RelayMembership>,bool> run=(action,copy)=> {
                if(busy)return;busy=true;form.IsEnabled=false;status.Text=Localization.Get("PrivatePositionConnecting");string display=name.Text;
                System.Threading.Tasks.Task.Run(()=> { relayGroups.SetName(display);return action(); }).ContinueWith(task=>window.Dispatcher.BeginInvoke(new Action(()=> {
                    busy=false;if(task.IsFaulted)task.Exception.Handle(error=>true);if(closed)return;form.IsEnabled=true;
                    if(task.Status==System.Threading.Tasks.TaskStatus.RanToCompletion) {
                        var group=task.Result;if(group!=null&&group.Pending=="") { code.Password=group.Code;if(copy)try { System.Windows.Clipboard.SetText(group.Code); }catch { } }
                        if(group==null)code.Clear();status.Text=Localization.Get(group==null?"PrivateGroupDeleted":"PrivatePositionConnecting");
                    }else status.Text=Localization.Get("PrivatePositionInvalid");draw();Refresh();
                })));
            };
            var join=WpfFieldTheme.Button(Localization.Get("PrivateGroupConnect"),()=> { string value=code.Password;run(()=>relayGroups.Join(value),false); },true);join.Tag="group-connect";form.Children.Add(join);
            var create=WpfFieldTheme.Button(Localization.Get("PrivateGroupCreate"),()=> { string label=groupLabel.Text;run(()=>relayGroups.Create(label),true); });create.Tag="group-create";form.Children.Add(create);form.Children.Add(rows);body.Children.Add(status);
            draw=()=> {
                rows.Children.Clear();foreach(var group in relayGroups.Groups) {
                    var captured=group;var card=new StackPanel { Margin=new Thickness(0,6,0,6) };
                    var title=WpfFieldTheme.Text(group.Label+(group.IsOwner?" · "+Localization.Get("PrivateGroupOwner"):""),16,null,true);title.Tag="group-title:"+group.Id;card.Children.Add(title);
                    var groupName=new TextBox { Text=group.Name,MaxLength=48,Margin=new Thickness(0,8,0,4),Tag="rename-group:"+group.Id };card.Children.Add(groupName);var rename=WpfFieldTheme.Button(Localization.Get("PrivateGroupRename"),()=> { string label=groupName.Text;run(()=> { relayGroups.Rename(captured,label);return captured; },false); });card.Children.Add(rename);
                    string state=group.Pending!=""||group.Session!=null&&group.Session.Error!=null?Localization.Get("PrivatePositionInvalid"):group.Session==null?Localization.Get("PrivateGroupLeave"):group.Session.Ready?Localization.Get("PlayerConnected")+" · "+group.Session.Connections:Localization.Get("PrivatePositionConnecting");
                    var stateText=WpfFieldTheme.Text(state,12,WpfFieldTheme.Muted);stateText.Tag="group-state:"+group.Id;card.Children.Add(stateText);
                    var buttons=new WrapPanel();
                    var connect=WpfFieldTheme.Button(Localization.Get(group.Session==null||group.Session.Error!=null?"PrivateGroupConnect":"PrivateGroupLeave"),()=> {
                        if(captured.Session!=null&&captured.Session.Error==null) { relayGroups.Leave(captured);draw();Refresh(); }
                        else run(()=> { relayGroups.Connect(captured);return captured; },false);
                    });connect.Tag="group-toggle:"+group.Id;connect.IsEnabled=group.Pending!="DELETE";buttons.Children.Add(connect);
                    var copy=WpfFieldTheme.Button(Localization.Get("PrivateGroupCopy"),()=> { code.Password=captured.Code;try { System.Windows.Clipboard.SetText(captured.Code); }catch { } });copy.IsEnabled=group.Pending=="";buttons.Children.Add(copy);
                    if(group.IsOwner) {
                        var rotate=WpfFieldTheme.Button(Localization.Get("PrivateGroupRotate"),()=>run(()=> { relayGroups.Rotate(captured);return captured; },true));rotate.Tag="group-rotate:"+group.Id;rotate.IsEnabled=group.Pending!="DELETE";buttons.Children.Add(rotate);
                        var delete=WpfFieldTheme.Button(Localization.Get("PrivateGroupDelete"),()=> {
                            if(MessageBox.Show(window,Localization.T("PrivateGroupDeleteConfirm",captured.Label),Localization.Get("PrivateGroupDelete"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                            run(()=> { relayGroups.Delete(captured);return null; },false);
                        });delete.Tag="group-delete:"+group.Id;delete.IsEnabled=group.Pending==""||group.Pending=="DELETE";buttons.Children.Add(delete);
                    }
                    card.Children.Add(buttons);rows.Children.Add(WpfFieldTheme.Card(card,new Thickness(12)));
                }
            };draw();
            body.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true));
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=> { if(!busy)foreach(var label in ChildrenOf<TextBlock>(rows).Where(t=>(t.Tag as string??"").StartsWith("group-state:"))) {
                string id=((string)label.Tag).Substring(12);var group=relayGroups.Groups.FirstOrDefault(g=>g.Id==id);if(group!=null)label.Text=group.Pending!=""||group.Session!=null&&group.Session.Error!=null?Localization.Get("PrivatePositionInvalid"):group.Session==null?Localization.Get("PrivateGroupLeave"):group.Session.Ready?Localization.Get("PlayerConnected")+" · "+group.Session.Connections:Localization.Get("PrivatePositionConnecting");
            } if(!busy)foreach(var title in ChildrenOf<TextBlock>(rows).Where(t=>(t.Tag as string??"" ).StartsWith("group-title:"))) { var group=relayGroups.Groups.FirstOrDefault(g=>g.Id==((string)title.Tag).Substring(12));if(group!=null)title.Text=group.Label+(group.IsOwner?" · "+Localization.Get("PrivateGroupOwner"):""); } };timer.Start();window.Closed+=(s,e)=> { closed=true;timer.Stop();code.Clear(); };window.ContentRendered+=(s,e)=>code.Focus();window.Content=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };return window;
        }
        static IEnumerable<T> ChildrenOf<T>(DependencyObject root) where T:DependencyObject {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);var item=child as T;if(item!=null)yield return item;foreach(var nested in ChildrenOf<T>(child))yield return nested; }
        }
        Window BuildPrivatePositions() {
            var window=Window(Localization.Get("PrivatePositions"),560,690);var body=new StackPanel { Margin=new Thickness(24) };
            body.Children.Add(new TextBlock { Text=Localization.Get("PrivatePositionHelp"),Foreground=WpfFieldTheme.Ink,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16) });
            body.Children.Add(new TextBlock { Text=Localization.Get("PrivatePositionMarkersHelp")+" "+Localization.Get("PrivatePositionGpsHelp"),Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) });
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionName")));var name=new TextBox { MaxLength=32,Margin=new Thickness(0,4,0,12) };body.Children.Add(name);
            var mode=new ComboBox { ItemsSource=new[]{Localization.Get("PrivatePositionHost"),Localization.Get("PrivatePositionJoin")},SelectedIndex=1,Margin=new Thickness(0,4,0,12) };body.Children.Add(mode);
            var addressLabel=WpfFieldTheme.Text(Localization.Get("PrivatePositionAddress"));body.Children.Add(addressLabel);var address=new TextBox { Tag="private-position-address",MaxLength=253,Margin=new Thickness(0,4,0,12) };body.Children.Add(address);
            var lookupInfo=new TextBlock { Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,8) };
            bool hostMode=false,lookupBusy=false,closed=false;string hostAddress="",joinAddress="";
            Button detectButton=null;Action detect=null;detect=()=> {
                if(lookupBusy||!hostMode)return;lookupBusy=true;detectButton.IsEnabled=false;string original=address.Text;lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp")+Environment.NewLine+Localization.Get("PrivatePositionConnecting");
                var lookup=publicAddressLookup??(diagnostic?(Func<System.Threading.Tasks.Task<string>>)(()=>System.Threading.Tasks.Task.FromResult("8.8.8.8")):(()=>System.Threading.Tasks.Task.Run(()=>PublicPositionAddress.Lookup())));
                System.Threading.Tasks.Task<string> pending;try { pending=lookup(); }catch(Exception) { pending=System.Threading.Tasks.Task.FromResult<string>(null); }
                pending.ContinueWith(result=>window.Dispatcher.BeginInvoke(new Action(()=> {
                    lookupBusy=false;if(result.IsFaulted)result.Exception.Handle(error=>true);if(closed)return;detectButton.IsEnabled=privatePositions==null;string found=result.Status==System.Threading.Tasks.TaskStatus.RanToCompletion?PublicPositionAddress.Parse(result.Result):null;
                    if(found==null) { lookupInfo.Text=Localization.Get("PrivatePositionPublicIpFailed");return; }
                    if(hostMode&&address.Text==original) { address.Text=found;if(privatePositions!=null&&privatePositions.Host)privatePositions.SetShareAddress(found); }else if(!hostMode&&hostAddress==original)hostAddress=found;
                    lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp");
                })));
            };
            detectButton=WpfFieldTheme.Button(Localization.Get("PrivatePositionDetectPublicIp"),()=>detect());detectButton.Tag="private-position-detect-public-ip";detectButton.Visibility=Visibility.Collapsed;body.Children.Add(detectButton);body.Children.Add(lookupInfo);
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionPort")));var port=new TextBox { Text="47831",MaxLength=5,Margin=new Thickness(0,4,0,12) };body.Children.Add(port);
            var localAddress=new TextBlock { Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed };body.Children.Add(localAddress);
            System.Threading.Tasks.Task.Run(()=> {
                string addresses="";try { addresses=string.Join(Environment.NewLine,System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up).SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!System.Net.IPAddress.IsLoopback(a.Address)&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>n.Name+": "+a.Address)).Distinct()); }catch(Exception) {}
                window.Dispatcher.BeginInvoke(new Action(()=> { if(!closed)localAddress.Text=Localization.Get("PrivatePositionLocalAddresses")+Environment.NewLine+addresses; }));
            });
            mode.SelectionChanged+=(s,e)=> {
                if(hostMode)hostAddress=address.Text;else joinAddress=address.Text;hostMode=mode.SelectedIndex==0;address.Text=hostMode?hostAddress:joinAddress;
                addressLabel.Text=Localization.Get(hostMode?"PrivatePositionShareAddress":"PrivatePositionAddress");localAddress.Visibility=lookupInfo.Visibility=detectButton.Visibility=hostMode?Visibility.Visible:Visibility.Collapsed;
                lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp");if(hostMode&&string.IsNullOrWhiteSpace(address.Text))detect();
            };
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionKey")));var secret=new PasswordBox { Margin=new Thickness(0,4,0,12),FontSize=16,MaxLength=64,MinHeight=40,Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(12,9,12,9) };body.Children.Add(secret);
            var generateKey=WpfFieldTheme.Button(Localization.Get("PrivatePositionNewKey"),()=> { secret.Password=PrivatePositions.NewKey();System.Windows.Clipboard.SetText(secret.Password); });body.Children.Add(generateKey);
            var status=new TextBlock { Foreground=WpfFieldTheme.Amber,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) };body.Children.Add(status);
            var enabled=new CheckBox { Content=Localization.Get("PrivatePositions"),IsChecked=privatePositions!=null,Margin=new Thickness(0,12,0,12) };body.Children.Add(enabled);
            if(privatePositions!=null) { if(privatePositions.Host)hostAddress=privatePositions.Address;else joinAddress=privatePositions.Address;name.Text=privatePositions.Name;mode.SelectedIndex=privatePositions.Host?0:1;address.Text=privatePositions.Address;port.Text=privatePositions.Port.ToString(CultureInfo.InvariantCulture); }
            Action<bool> inputs=active=> { name.IsEnabled=mode.IsEnabled=port.IsEnabled=secret.IsEnabled=generateKey.IsEnabled=!active;detectButton.IsEnabled=!active&&!lookupBusy;address.IsEnabled=true;address.IsReadOnly=active; };
            inputs(privatePositions!=null);
            enabled.Checked+=(s,e)=> {
                try { int number;if(!int.TryParse(port.Text,out number))throw new ArgumentException();privatePositions=new DirectPositions(mode.SelectedIndex==0,address.Text,number,secret.Password,name.Text,!diagnostic);status.Text=Localization.Get("PrivatePositionConnecting");inputs(true);Refresh(); }
                catch(Exception) { status.Text=Localization.Get("PrivatePositionInvalid");enabled.IsChecked=false; }
            };
            enabled.Unchecked+=(s,e)=> { if(privatePositions!=null) { privatePositions.Dispose();privatePositions=null; }inputs(false);Refresh(); };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=> { if(privatePositions!=null) {
                status.Text=privatePositions.Error!=null?Localization.Get("PrivatePositionInvalid"):privatePositions.Ready?Localization.Get("PrivatePositionConnected")+" · "+privatePositions.Connections:Localization.Get("PrivatePositionConnecting");
                if(privatePositions.Host&&privatePositions.RouterFinished) {
                    if(!privatePositions.RouterReady)status.Text+=Environment.NewLine+Localization.Get("PrivatePositionRouterManual");
                    if(string.IsNullOrWhiteSpace(address.Text)&&!string.IsNullOrWhiteSpace(privatePositions.Address))address.Text=privatePositions.Address;
                }
                if(privatePositions.Host&&privatePositions.FirewallFinished)status.Text+=Environment.NewLine+Localization.Get(privatePositions.FirewallReady?"PrivatePositionFirewallReady":"PrivatePositionFirewallFailed");
            } };timer.Start();
            body.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true));window.Closed+=(s,e)=> { closed=true;timer.Stop();secret.Clear(); };window.ContentRendered+=(s,e)=>name.Focus();window.Content=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };return window;
        }
        Window BuildSettings(int initial) {
            var window=Window(Localization.Get("Settings"),480,650);var root=new DockPanel { Margin=new Thickness(24) };var heading=new DockPanel();var version=WpfFieldTheme.Text("v"+MapWindow.VersionString+(Program.IsTestBuild?" "+Localization.Get("WpfTestBuild"):""),13,WpfFieldTheme.Muted);DockPanel.SetDock(version,Dock.Right);heading.Children.Add(version);heading.Children.Add(WpfFieldTheme.Text(Localization.Get("Settings"),28,null,true));DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var nav=new UniformGrid { Columns=3,Margin=new Thickness(0,20,0,16) };DockPanel.SetDock(nav,Dock.Top);root.Children.Add(nav);
            var footer=new DockPanel { Margin=new Thickness(0,16,0,0) };var done=WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true);DockPanel.SetDock(done,Dock.Right);footer.Children.Add(done);var moveHud=WpfFieldTheme.Button(Localization.Get("WpfMoveMinimap"),ShowMoveHud);DockPanel.SetDock(moveHud,Dock.Top);footer.Children.Add(moveHud);footer.Children.Add(WpfFieldTheme.Button(Localization.Get("OpenDataFolder"),()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","\""+engine.WpfDataFolder+"\"") { UseShellExecute=true })));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            done.Tag="text:Done";moveHud.Tag="text:WpfMoveMinimap";((FrameworkElement)footer.Children[2]).Tag="text:OpenDataFolder";
            var body=new StackPanel();root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            int currentPage=initial,settingsRevision=-1;Action<int> page=null;page=index=> {
                currentPage=index;settingsRevision=engine.WpfSettingsRevision;
                window.Title=Localization.Get("Settings");((TextBlock)heading.Children[1]).Text=window.Title;version.Text="v"+MapWindow.VersionString+(Program.IsTestBuild?" "+Localization.Get("WpfTestBuild"):"");
                string[] tabKeys={"FieldGeneral","FieldShortcuts","FieldAppearance"};for(int tab=0;tab<nav.Children.Count;tab++)((Button)nav.Children[tab]).Content=Localization.Get(tabKeys[tab]);RefreshLocalizedChrome(root);
                body.Children.Clear();var state=engine.WpfSnapshot(true);
                if(index==1) { foreach(var action in AppControlBindings.AllActions) { var captured=action;var row=new DockPanel { Margin=new Thickness(0,3,0,3) };var key=WpfFieldTheme.Button(PhysicalKeyCapture.KeyName(state.Bindings.Key(action)),()=> { CaptureKey(Localization.Get(AppControlBindings.LabelKey(captured)),(vk,scan)=>engine.WpfBind(captured,vk,scan),true);page(1); });key.MinWidth=88;DockPanel.SetDock(key,Dock.Right);row.Children.Add(key);row.Children.Add(WpfFieldTheme.Text(Localization.Get(AppControlBindings.LabelKey(action)),16));body.Children.Add(row);body.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line }); }
                    foreach(var setting in engine.WpfSettings(1).Where(item=>item.Control is TacticalCheckBox))body.Children.Add(ConvertSetting(setting.Control,()=>page(1)));
                }
                else {
                    string group=null;
                    foreach(var setting in engine.WpfSettings(index)) {
                        if(setting.Group!=group) { group=setting.Group;var title=WpfFieldTheme.Text(group,13,WpfFieldTheme.Muted,true);title.Margin=new Thickness(0,18,0,10);body.Children.Add(title); }
                        var control=ConvertSetting(setting.Control,()=> { engine.WpfRebuildSettings();page(index);Refresh(); });
                        if(control!=null)body.Children.Add(control);
                    }
                    if(index==2)AddAppearanceControls(body,state);
                    if(index==0)body.Children.Insert(0,WpfFieldTheme.Button(Localization.Get("PrivatePositions"),ShowPrivatePositions));
                }
            };
            string[] labels={"FieldGeneral","FieldShortcuts","FieldAppearance"};for(int i=0;i<3;i++) { int index=i;nav.Children.Add(WpfFieldTheme.Button(Localization.Get(labels[i]),()=>page(index))); }page(initial);window.Activated+=(s,e)=> { if(settingsRevision!=engine.WpfSettingsRevision)page(currentPage); };window.Content=root;return window;
        }
        void AddAppearanceControls(StackPanel body,WpfMapState state) {
                        var rotate=new CheckBox { Content=Localization.Get("RotateMinimap"),IsChecked=state.RotateMap,Margin=new Thickness(0,8,0,8),Tag="rotate-minimap" };
                        rotate.Checked+=(s,e)=> { engine.WpfRotate(true);Refresh(); };rotate.Unchecked+=(s,e)=> { engine.WpfRotate(false);Refresh(); };body.Children.Insert(0,rotate);
                        var clusterPois=new CheckBox { Content=Localization.Get("PoiClustering"),IsChecked=state.PoiClustering,Margin=new Thickness(0,8,0,8),Tag="poi-clustering" };
                        clusterPois.Checked+=(s,e)=> { engine.WpfPoiClustering(true);Refresh(); };clusterPois.Unchecked+=(s,e)=> { engine.WpfPoiClustering(false);Refresh(); };body.Children.Insert(1,clusterPois);
                        var shape=new ComboBox { ItemsSource=new[]{Localization.Get("ZeSquare"),Localization.Get("ZeCircle")},SelectedIndex=state.Shape=="Circle"?1:0,Margin=new Thickness(0,14,0,14),Tag="minimap-shape" };
                        shape.SelectionChanged+=(s,e)=> { engine.WpfAppearance(shape.SelectedIndex==1?"Circle":"Square",engine.WpfSnapshot(false).Opacity);Refresh(); };body.Children.Insert(0,shape);
        }
        void AddSetupSettings(StackPanel body,int tab,Action rebuild,string onlyGroup=null) {
            string group=null;
            foreach(var setting in engine.WpfSettings(tab).OrderBy(item=>onlyGroup=="SecMapZones"&&item.Control.Controls.Cast<System.Windows.Forms.Control>().Any(child=>child.Tag as string=="swatch")?0:1)) {
                if(onlyGroup!=null&&setting.GroupKey!=onlyGroup)continue;
                if(setting.Control.Text==Localization.Get("OpenStartupGuide")||setting.Control.Text==Localization.Get("KeyRebindingWizard"))continue;
                if(setting.Group!=group) { group=setting.Group;var title=WpfFieldTheme.Text(group,13,WpfFieldTheme.Muted,true);title.Margin=new Thickness(0,18,0,10);body.Children.Add(title); }
                var control=ConvertSetting(setting.Control,()=> { engine.WpfRebuildSettings();rebuild();Refresh(); });if(control!=null)body.Children.Add(control);
            }
        }
        FrameworkElement ConvertSetting(System.Windows.Forms.Control source,Action rebuild) {
            if(source.IsDisposed)return null;
            var customCheck=source as TacticalCheckBox;
            if(customCheck!=null) { var c=new CheckBox { Content=customCheck.Text,IsChecked=customCheck.Checked,Margin=new Thickness(0,4,0,4) };c.Checked+=(s,e)=> { customCheck.Checked=true;Refresh(); };c.Unchecked+=(s,e)=> { customCheck.Checked=false;Refresh(); };return c; }
            var number=source as System.Windows.Forms.NumericUpDown;
            if(number!=null) { var input=new TextBox { Text=number.Value.ToString(CultureInfo.InvariantCulture),MinWidth=70,MaxWidth=100,Margin=new Thickness(8,0,0,0),ToolTip=number.Minimum+" – "+number.Maximum };input.LostKeyboardFocus+=(s,e)=> { decimal n;if(decimal.TryParse(input.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out n)&&n>=number.Minimum&&n<=number.Maximum) { number.Value=n;Refresh(); }else input.Text=number.Value.ToString(CultureInfo.InvariantCulture); };return input; }
            var slider=source as TacticalSlider;
            if(slider!=null) { var c=new Slider { Minimum=slider.Minimum,Maximum=slider.Maximum,Value=slider.Value,Width=180,Margin=new Thickness(8,10,0,10) };c.ValueChanged+=(s,e)=> { slider.Value=(int)c.Value;Refresh(); };return c; }
            var combo=source as System.Windows.Forms.ComboBox;
            if(combo!=null) { var c=new ComboBox { ItemsSource=combo.Items.Cast<object>().ToArray(),SelectedIndex=combo.SelectedIndex,MinWidth=140,Margin=new Thickness(4,4,4,12) };c.SelectionChanged+=(s,e)=> { if(combo.IsDisposed)return;combo.SelectedIndex=c.SelectedIndex;if(combo.Items.Count==Localization.LanguageDisplayNames.Length)Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,rebuild);else Refresh(); };return c; }
            var button=source as System.Windows.Forms.Button;
            if(button!=null) {
                Button b=null;b=WpfFieldTheme.Button(button.Text,()=> {
                    if(button.Text==Localization.Get("OpenStartupGuide")||button.Text==Localization.Get("KeyRebindingWizard"))ShowOnboarding();
                    else if(button.Text==Localization.Get("MapZonesScreenshot"))ShowZoneWizard();
                    else if(button.Text==Localization.Get("ConfigurePoiFilters"))Modal(BuildPoiFilters());
                    else if(!button.IsDisposed)typeof(System.Windows.Forms.Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});
                    if((string)button.Tag=="swatch"&&!button.IsDisposed)RefreshSwatch(b,button);
                    Refresh();
                });b.HorizontalAlignment=HorizontalAlignment.Left;b.IsEnabled=button.Enabled;b.Margin=new Thickness(0,4,0,8);
                if((string)button.Tag=="swatch") { RefreshSwatch(b,button);b.Content="●";b.MinWidth=70; }b.Tag=button.Name;return b;
            }
            var label=source as System.Windows.Forms.Label;
            if(label!=null) { var t=WpfFieldTheme.Text(label.Text,15,label.ForeColor==OverlayTheme.InkMuted?WpfFieldTheme.Muted:WpfFieldTheme.Ink);t.TextWrapping=TextWrapping.Wrap;t.MaxWidth=Math.Max(100,label.Width);t.Margin=new Thickness(0,4,0,8);return t; }
            if(source.Name=="AdminToolsToggle")return WpfFieldTheme.Button(Localization.Get("AdminToolsEnabled"),()=>typeof(System.Windows.Forms.Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(source,new object[]{EventArgs.Empty}));
            if(source.Controls.Count>0) { var panel=new StackPanel { Orientation=source is System.Windows.Forms.FlowLayoutPanel&&source.Height<50?Orientation.Horizontal:Orientation.Vertical,Margin=new Thickness(0,4,0,4) };foreach(System.Windows.Forms.Control child in source.Controls) { var c=ConvertSetting(child,rebuild);if(c!=null)panel.Children.Add(c); }return panel; }
            return null;
        }
        WpfMapSurface setupPreview;
        Window BuildPoiFilters() {
            var window=Window(Localization.Get("ConfigurePoiFilters"),540,650);var root=new DockPanel { Margin=new Thickness(24) };var footer=new StackPanel { Orientation=Orientation.Horizontal };var body=new StackPanel();Action draw=null;
            footer.Children.Add(WpfFieldTheme.Button(Localization.Get("SelectAll"),()=> { engine.WpfSnapshot(true).Store.SetAllEnabled(true);engine.WpfSave();draw();Refresh(); }));footer.Children.Add(WpfFieldTheme.Button(Localization.Get("DeselectAll"),()=> { engine.WpfSnapshot(true).Store.SetAllEnabled(false);engine.WpfSave();draw();Refresh(); }));footer.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            draw=()=> { body.Children.Clear();var store=engine.WpfSnapshot(true).Store;if(store==null)return;foreach(string section in store.Sections) { var rows=new StackPanel();foreach(var category in store.CategoriesBySection[section]) { int categoryId=category.Id;var check=new CheckBox { Content=Localization.GetCategoryName(category.Name),IsChecked=store.IsCategoryEnabled(categoryId),Tag="setup-poi:"+categoryId,Margin=new Thickness(12,4,0,4) };check.Click+=(s,e)=> { store.SetCategoryEnabled(categoryId,check.IsChecked==true);engine.WpfSave();Refresh(); };rows.Children.Add(check); }body.Children.Add(new Expander { Header=Localization.GetSectionName(section),Content=rows,IsExpanded=true,Margin=new Thickness(0,8,0,8) }); } };
            root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });draw();window.Content=root;return window;
        }
        void RefreshSetupPreview() {
            if(setupPreview==null)return;var state=engine.WpfSnapshot(false);if(state.Position==null)state.Position=new Position { X=0,Y=0,Yaw=90 };setupPreview.Width=210.0*state.HudWidth/Math.Max(state.HudWidth,state.HudHeight);setupPreview.Height=210.0*state.HudHeight/Math.Max(state.HudWidth,state.HudHeight);setupPreview.State=state;setupPreview.InvalidateVisual();
        }
        void ShowOnboarding() { Modal(BuildOnboarding(0)); }
        internal void ShowZoneWizard(string file=null) {
            if(!application.Dispatcher.CheckAccess()) { application.Dispatcher.BeginInvoke(new Action(()=>ShowZoneWizard(file)));return; }
            if(file!=null&&Path.GetExtension(file).Equals(".tsv",StringComparison.OrdinalIgnoreCase)) { ShowZoneFile(file);return; }
            Modal(BuildZoneWizard(file));
        }
        internal void ShowZoneAssociation() {
            try { ZoneFileOpen.Register(System.Windows.Forms.Application.ExecutablePath);ShowZoneMessage(Localization.Get("ZoneAssociationInfo")); }
            catch(Exception error) { Program.LogException("ZoneFileAssociation",error);ShowZoneMessage(Localization.T("ZoneOpenError",error.Message)); }
        }
        void ShowZoneMessage(string message) { var window=Window(Localization.Get("ZoneEditorTitle"),560,280);var layout=new StackPanel { Margin=new Thickness(22) };var text=WpfFieldTheme.Text(message,15);text.TextWrapping=TextWrapping.Wrap;layout.Children.Add(text);layout.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true));window.Content=new ScrollViewer { Content=layout };Modal(window); }
        async void ShowZoneFile(string file) {
            if(zoneFileActive||disposed)return;zoneFileActive=true;
            try {
                var imported=await System.Threading.Tasks.Task.Run(()=> { if(!Path.GetExtension(file).Equals(".tsv",StringComparison.OrdinalIgnoreCase)||new FileInfo(file).Length>32L*1024*1024)throw new InvalidDataException("Invalid zone TSV file.");return ZoneStore.Load(file); });
                if(disposed)return;if(imported.Count==0)throw new InvalidDataException("The file contains no zones.");
                string initial=Path.GetFileNameWithoutExtension(file);if(initial.Equals("zones",StringComparison.OrdinalIgnoreCase))initial=Path.GetFileName(Path.GetDirectoryName(file));
                string layer=null;Modal(BuildZonePackName(initial,value=>layer=value));if(layer==null||disposed)return;
                var wizard=BuildZoneWizard();try { ((ZoneEditor)wizard.Tag).WpfImportZonePack(imported,layer);Modal(wizard); }finally { wizard.Close(); }
            }catch(Exception error) { Program.LogException("ZoneFileImport",error);if(!disposed)ShowZoneMessage(Localization.T("ZoneOpenError",error.Message)); }
            finally { zoneFileActive=false; }
        }
        Window BuildZonePackName(string initial,Action<string> accepted) {
            var window=Window(Localization.Get("NameThisMap"),520,250);window.ResizeMode=ResizeMode.NoResize;
            var layout=new StackPanel { Margin=new Thickness(22) };layout.Children.Add(WpfFieldTheme.Text(Localization.Get("NameThisMap"),23));
            var input=new TextBox { Tag="zone-pack-name",Text=initial==null?"":initial.Substring(0,Math.Min(80,initial.Length)),MaxLength=80,Margin=new Thickness(0,18,0,14) };layout.Children.Add(input);
            var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };var cancel=WpfFieldTheme.Button(Localization.Get("Cancel"),()=>window.Close());row.Children.Add(cancel);
            var add=WpfFieldTheme.Button(Localization.Get("ZeImportFile"),()=> { if(string.IsNullOrWhiteSpace(input.Text))return;accepted(input.Text.Trim());window.Close(); },true);add.Tag="zone-pack-accept";add.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);row.Children.Add(add);layout.Children.Add(row);input.TextChanged+=(s,e)=>add.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);input.KeyDown+=(s,e)=> { if(e.Key==Key.Enter&&add.IsEnabled) { accepted(input.Text.Trim());window.Close();e.Handled=true; }else if(e.Key==Key.Escape)window.Close(); };window.Loaded+=(s,e)=> { input.Focus();input.SelectAll(); };window.Content=layout;return window;
        }
        Window BuildZoneWizard(string file=null) {
            var editor=engine.WpfZoneEditor();var nativeHandle=editor.Handle;var window=Window(Localization.Get("ZoneEditorTitle"),1450,920);window.MinWidth=1100;window.MinHeight=740;window.Tag=editor;
            editor.WpfOwner=new WpfWindowOwner(new WindowInteropHelper(window).EnsureHandle());
            var root=new DockPanel { Margin=new Thickness(20) };var header=new DockPanel { Margin=new Thickness(0,0,0,14) };var close=WpfFieldTheme.Button(Localization.Get("Cancel"),()=>window.Close());DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(WpfFieldTheme.Text(Localization.Get("ZoneEditorTitle"),26,null,true));header.MouseLeftButtonDown+=(s,e)=> { if(e.OriginalSource is TextBlock)window.DragMove(); };DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var instruction=WpfFieldTheme.Text(editor.WpfInstruction,15,WpfFieldTheme.Amber);instruction.TextWrapping=TextWrapping.Wrap;
            var footer=new DockPanel();var save=WpfFieldTheme.Button(Localization.Get("ZeSaveZones"),()=>editor.WpfClick(editor.WpfAction("ZeSaveZones")),true);save.Tag="ZeSaveZones";DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);footer.Children.Add(new ScrollViewer { Content=instruction,MaxHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });var footerCard=WpfFieldTheme.Card(footer,new Thickness(12));footerCard.Margin=new Thickness(0,14,0,0);DockPanel.SetDock(footerCard,Dock.Bottom);root.Children.Add(footerCard);
            var workspace=new Grid();workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(250) });workspace.ColumnDefinitions.Add(new ColumnDefinition());workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(280) });root.Children.Add(workspace);
            var tools=new StackPanel();var toolScroll=new ScrollViewer { Content=tools,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };workspace.Children.Add(WpfFieldTheme.Card(toolScroll,new Thickness(14)));
            var sourceCanvas=new WpfZoneCanvas(editor,true);var targetCanvas=new WpfZoneCanvas(editor,false);
            Action redraw=()=> { sourceCanvas.Redraw();targetCanvas.Redraw(); };
            bool syncing=false;var buttons=new List<KeyValuePair<Button,System.Windows.Forms.Control>>();
            Func<string,Button> action=key=> { var native=editor.WpfAction(key);var button=WpfFieldTheme.Button(Localization.Get(key),()=> { if(key=="ZeZoneColour")editor.WpfChooseOutlineColour();else editor.WpfClick(native);redraw(); });button.Tag=key;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,3,0,3);button.FontSize=14;buttons.Add(new KeyValuePair<Button,System.Windows.Forms.Control>(button,native));return button; };
            tools.Children.Add(WpfFieldTheme.Text("01  "+Localization.Get("ZeImportTitle"),16,null,true));foreach(var key in new[]{"ZeOpenScreenshot","ZeImportFile","ZeExport","ZeManualFallback"})tools.Children.Add(action(key));
            tools.Children.Add(WpfFieldTheme.Text(Localization.Get("ZeLayer"),12,WpfFieldTheme.Muted));var layer=new ComboBox { Margin=new Thickness(0,5,0,6) };layer.SelectionChanged+=(s,e)=> { if(!syncing&&layer.SelectedIndex>=0) { editor.WpfLayer.SelectedIndex=layer.SelectedIndex;redraw(); } };tools.Children.Add(layer);tools.Children.Add(action("ZeNewLayer"));tools.Children.Add(action("ZeDeleteLayer"));
            tools.Children.Add(WpfFieldTheme.Text("02  "+Localization.Get("HeaderMapZones"),16,null,true));var drawButtons=new List<Button>();
            for(int i=0;i<editor.WpfDrawingTool.Items.Count;i++) { int tool=i;var button=WpfFieldTheme.Button(editor.WpfDrawingTool.Items[i].ToString(),()=> { editor.WpfDrawingTool.SelectedIndex=tool;redraw(); });button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,3,0,3);drawButtons.Add(button);tools.Children.Add(button); }
            foreach(var key in new[]{"ZeUndoPoint","ZeClearDraft","ControlResetZoom"})tools.Children.Add(action(key));
            var mapGrid=new Grid { Margin=new Thickness(14,0,14,0) };mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(0) });mapGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(mapGrid,1);workspace.Children.Add(mapGrid);
            var sourcePanel=new DockPanel();var sourceTitle=WpfFieldTheme.Text(Localization.Get("ZeOpenScreenshot"),14,WpfFieldTheme.Muted);DockPanel.SetDock(sourceTitle,Dock.Top);sourcePanel.Children.Add(sourceTitle);sourcePanel.Children.Add(sourceCanvas);mapGrid.Children.Add(sourcePanel);
            var targetPanel=new DockPanel();var mapTitle=WpfFieldTheme.Text(Localization.Get("HeaderMapZones"),16,null,true);DockPanel.SetDock(mapTitle,Dock.Top);targetPanel.Children.Add(mapTitle);targetPanel.Children.Add(targetCanvas);Grid.SetColumn(targetPanel,1);mapGrid.Children.Add(targetPanel);
            var review=new StackPanel();var reviewScroll=new ScrollViewer { Content=review,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };var reviewCard=WpfFieldTheme.Card(reviewScroll,new Thickness(14));Grid.SetColumn(reviewCard,2);workspace.Children.Add(reviewCard);
            review.Children.Add(WpfFieldTheme.Text("03  "+Localization.Get("ZeFilterZones"),16,null,true));var filter=new TextBox { Text=editor.WpfFilter.Text,Margin=new Thickness(0,8,0,8) };filter.TextChanged+=(s,e)=> { if(!syncing)editor.WpfFilter.Text=filter.Text; };review.Children.Add(filter);
            var zoneTypes=Enum.GetValues(typeof(ZoneCategory)).Cast<ZoneCategory>().ToArray();
            var typeKeys=new[]{"ZeTypeCity","ZeTypeTown","ZeTypeFarm","ZeTypeTrader","ZeTypeFaction","ZeTypeMilitary","ZeTypeBunker","ZeTypeGasStation","ZeTypeCustom","ZeTypeUnknown"};
            var typeFilter=new ComboBox { Tag="zone-type-filter",ItemsSource=new[]{Localization.Get("ZeAllTypes")}.Concat(typeKeys.Select(Localization.Get)).ToArray(),SelectedIndex=0,Margin=new Thickness(0,0,0,8) };
            typeFilter.SelectionChanged+=(s,e)=> { if(!syncing) { editor.WpfCategoryFilter=typeFilter.SelectedIndex<=0?(ZoneCategory?)null:zoneTypes[typeFilter.SelectedIndex-1];redraw(); } };review.Children.Add(typeFilter);
            var selectionHelp=WpfFieldTheme.Text(Localization.Get("ZeMultiHelp"),12,WpfFieldTheme.Muted);selectionHelp.TextWrapping=TextWrapping.Wrap;review.Children.Add(selectionHelp);
            var zonesList=new ListBox { Tag="zone-selection-list",SelectionMode=SelectionMode.Extended,MinHeight=160,MaxHeight=260 };zonesList.SelectionChanged+=(s,e)=> { if(!syncing) { editor.WpfSelectVisible(zonesList.SelectedItems.Cast<object>().Select(item=>zonesList.Items.IndexOf(item)).ToArray());redraw(); } };review.Children.Add(zonesList);
            var selectAll=WpfFieldTheme.Button(Localization.Get("SelectAll"),()=> { zonesList.SelectAll(); });selectAll.Tag="zone-select-all";review.Children.Add(selectAll);
            var selectColour=WpfFieldTheme.Button(Localization.Get("ZeSelectSameColour"),()=> { editor.WpfSelectSameColour();redraw(); });selectColour.Tag="zone-select-colour";review.Children.Add(selectColour);
            var details=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);details.TextWrapping=TextWrapping.Wrap;details.Margin=new Thickness(0,8,0,8);review.Children.Add(details);
            var zoneName=new TextBox { Text=editor.WpfName.Text,MaxLength=80,Margin=new Thickness(0,8,0,8) };zoneName.TextChanged+=(s,e)=> { if(!syncing)editor.WpfName.Text=zoneName.Text; };zoneName.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { editor.WpfNameEnter();redraw();e.Handled=true; } };review.Children.Add(zoneName);
            review.Children.Add(action("ZeZoneColour"));
            var fillColour=WpfFieldTheme.Button(Localization.Get("ZoneFillColour"),()=> { editor.WpfChooseFillColour();redraw(); });fillColour.HorizontalAlignment=HorizontalAlignment.Stretch;fillColour.Margin=new Thickness(0,8,0,4);review.Children.Add(fillColour);
            var fillOpacityLabel=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);review.Children.Add(fillOpacityLabel);var fillOpacity=new Slider { Minimum=0,Maximum=100,Value=16,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(0,4,0,8) };fillOpacity.ValueChanged+=(s,e)=> { if(!syncing) { editor.WpfSetFillOpacity((int)fillOpacity.Value);redraw(); } };review.Children.Add(fillOpacity);
            foreach(var key in new[]{"ZeAddZone","ZeApplyName","ZeDuplicate","ZeMoveUp","ZeMoveDown","ZeDeselect","ZeDeleteSelected","ZeDeleteAll"})review.Children.Add(action(key));
            var nativeShape=editor.WpfAction("ZoneShape") as System.Windows.Forms.ComboBox;var shape=new ComboBox { ItemsSource=nativeShape.Items.Cast<object>().ToArray(),SelectedIndex=0,Margin=new Thickness(0,8,0,8) };shape.SelectionChanged+=(s,e)=> { if(shape.SelectedIndex>=0)nativeShape.SelectedIndex=shape.SelectedIndex; };review.Children.Add(shape);review.Children.Add(action("ZeChangeShape"));
            var fill=new CheckBox { Content=Localization.Get("ZeFillZones"),IsChecked=true };fill.Checked+=(s,e)=> { var native=editor.WpfAction("ZeFillZones") as System.Windows.Forms.CheckBox;if(native!=null)native.Checked=true;redraw(); };fill.Unchecked+=(s,e)=> { var native=editor.WpfAction("ZeFillZones") as System.Windows.Forms.CheckBox;if(native!=null)native.Checked=false;redraw(); };review.Children.Add(fill);
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };string previousInstruction=null,previousDetails=null;
            Action sync=()=> { if(editor.IsDisposed)return;syncing=true;try {
                var layers=editor.WpfLayer.Items.Cast<object>().ToArray();if(!layer.Items.Cast<object>().SequenceEqual(layers))layer.ItemsSource=layers;layer.SelectedIndex=editor.WpfLayer.SelectedIndex;
                var items=editor.WpfList.Items.Cast<object>().ToArray();if(!zonesList.Items.Cast<object>().SequenceEqual(items))zonesList.ItemsSource=items;
                var selected=editor.WpfSelectedVisible;if(!zonesList.SelectedItems.Cast<object>().Select(item=>zonesList.Items.IndexOf(item)).OrderBy(i=>i).SequenceEqual(selected)) { zonesList.UnselectAll();foreach(int i in selected)zonesList.SelectedItems.Add(zonesList.Items[i]); }
                if(zoneName.Text!=editor.WpfName.Text)zoneName.Text=editor.WpfName.Text;
                instruction.Text=editor.WpfInstruction;details.Text=editor.WpfDetails;
                if(previousInstruction!=instruction.Text||previousDetails!=details.Text||editor.WpfBusy)redraw();previousInstruction=instruction.Text;previousDetails=details.Text;
                mapGrid.ColumnDefinitions[0].Width=editor.WpfHasScreenshot?new GridLength(1,GridUnitType.Star):new GridLength(0);sourcePanel.Visibility=editor.WpfHasScreenshot?Visibility.Visible:Visibility.Collapsed;
                foreach(var pair in buttons) { pair.Key.IsEnabled=pair.Value!=null&&!pair.Value.IsDisposed&&pair.Value.Enabled&&!editor.WpfBusy;if((string)pair.Key.Tag=="ZeZoneColour") { var colour=editor.WpfColour;pair.Key.Background=new SolidColorBrush(Color.FromRgb(colour.R,colour.G,colour.B));pair.Key.Foreground=colour.GetBrightness()>.5?WpfFieldTheme.Ground:WpfFieldTheme.Ink; } }
                for(int i=0;i<drawButtons.Count;i++) { drawButtons[i].Background=i==editor.WpfDrawingTool.SelectedIndex?WpfFieldTheme.Amber:WpfFieldTheme.Panel;drawButtons[i].Foreground=i==editor.WpfDrawingTool.SelectedIndex?WpfFieldTheme.Ground:WpfFieldTheme.Ink;drawButtons[i].IsEnabled=!editor.WpfBusy&&((i!=3&&i!=5)||selected.Length<=1); }
                shape.IsEnabled=!editor.WpfBusy&&selected.Length==1;var shapeButton=buttons.First(p=>(string)p.Key.Tag=="ZeChangeShape").Key;shapeButton.IsEnabled=shape.IsEnabled;
                save.IsEnabled=!editor.WpfBusy;zoneName.IsEnabled=filter.IsEnabled=layer.IsEnabled=zonesList.IsEnabled=typeFilter.IsEnabled=selectAll.IsEnabled=selectColour.IsEnabled=!editor.WpfBusy;
                foreach(var pair in buttons.Where(p=>(string)p.Key.Tag=="ZeMoveUp"||(string)p.Key.Tag=="ZeMoveDown"))pair.Key.IsEnabled=!editor.WpfBusy&&selected.Length==1;
                var fillColor=editor.WpfFillColour;fillColour.Background=new SolidColorBrush(Color.FromRgb(fillColor.R,fillColor.G,fillColor.B));fillColour.Foreground=fillColor.GetBrightness()>.5?WpfFieldTheme.Ground:WpfFieldTheme.Ink;fillOpacity.Value=Math.Round(fillColor.A*100.0/255);fillOpacityLabel.Text=Localization.Get("ZoneFillOpacity")+" · "+fillOpacity.Value+"%";fillColour.IsEnabled=fillOpacity.IsEnabled=!editor.WpfBusy;
            }finally { syncing=false; } };
            timer.Tick+=(s,e)=>sync();editor.FormClosed+=(s,e)=> { if(window.IsVisible)window.Close(); };
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&!(Keyboard.FocusedElement is TextBox)) { editor.WpfClick(editor.WpfAction("ZeUndoPoint"));redraw();e.Handled=true; } };
            window.Loaded+=async(s,e)=> { timer.Start();if(!string.IsNullOrEmpty(file))await editor.ImportAutomatic(file); };
            window.Closing+=(s,e)=> { if(editor.WpfBusy)e.Cancel=true; };
            window.Closed+=(s,e)=> { timer.Stop();editor.Close();editor.Dispose();Refresh(); };window.Content=root;sync();return window;
        }
        static void RefreshSwatch(Button target,System.Windows.Forms.Button source) { var color=source.BackColor;target.Background=new SolidColorBrush(Color.FromArgb(color.A,color.R,color.G,color.B));target.ToolTip=System.Drawing.ColorTranslator.ToHtml(color); }
        Window BuildOnboarding(int initial) {
            engine.WpfRebuildSettings();
            var window=Window(Localization.Get("WizardTitle"),1100,720);window.MinWidth=960;window.MinHeight=620;var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(240) });layout.ColumnDefinitions.Add(new ColumnDefinition());
            var rail=new DockPanel { Background=WpfFieldTheme.Panel,LastChildFill=true };var brand=new StackPanel { Margin=new Thickness(28,30,20,30) };brand.Children.Add(WpfFieldTheme.Text("SKYNETT",26,null,true));brand.Children.Add(WpfFieldTheme.Text(Localization.Get("WpfGettingStarted"),11,WpfFieldTheme.Muted));DockPanel.SetDock(brand,Dock.Top);rail.Children.Add(brand);
            Action draw=null;var language=new ComboBox { ItemsSource=Localization.LanguageDisplayNames,SelectedIndex=(int)Localization.Current,Margin=new Thickness(24,20,24,24) };language.SelectionChanged+=(s,e)=> { Localization.Current=Localization.LanguageAt(language.SelectedIndex);engine.WpfRebuildSettings();filterIdentity=null;Refresh();if(draw!=null)draw(); };DockPanel.SetDock(language,Dock.Bottom);rail.Children.Add(language);var steps=new StackPanel { Margin=new Thickness(18,10,18,0) };rail.Children.Add(new ScrollViewer { Content=steps,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });layout.Children.Add(rail);
            var right=new DockPanel { Margin=new Thickness(36,28,36,28) };Grid.SetColumn(right,1);layout.Children.Add(right);var footer=new DockPanel { Margin=new Thickness(0,20,0,0) };DockPanel.SetDock(footer,Dock.Bottom);right.Children.Add(footer);var content=new StackPanel();right.Children.Add(new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });int step=Math.Max(0,Math.Min(8,initial)),visited=step;
            draw=()=> {
                window.Title=Localization.Get("WizardTitle");((TextBlock)brand.Children[1]).Text=Localization.Get("WpfGettingStarted");
                setupPreview=null;content.Children.Clear();footer.Children.Clear();steps.Children.Clear();string[] stages={"WizardTabOverview","WizardTabControls","WizardTabKeybinds","WizardTabSafety","WizardTabFeatures","FieldAppearance","SecMapZones","SecTrackingZoom","PrivatePositions"};
                visited=Math.Max(visited,step);for(int i=0;i<stages.Length;i++) { int index=i;string label=(i+1)+"    "+System.Text.RegularExpressions.Regex.Replace(Localization.Get(stages[i]),@"^\s*\d+[.\s]+","");Action navigate=()=> { step=index;draw(); };var button=i>step?HoldToProceed.Button(window,label,navigate):WpfFieldTheme.Button(label,navigate);button.IsEnabled=i<=visited;button.Background=i==step?WpfFieldTheme.Raised:Brushes.Transparent;button.Foreground=i==step?WpfFieldTheme.Amber:WpfFieldTheme.Muted;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,4,0,4);steps.Children.Add(button); }
                content.Children.Add(WpfFieldTheme.Text(Localization.T("WizardStepIndicator",step+1,stages.Length),12,WpfFieldTheme.Muted));content.Children.Add(WpfFieldTheme.Text(step==2?Localization.Get("FieldMatchKeys"):Localization.Get(stages[step]),32,null,true));
                if(step==2) {
                    var help=WpfFieldTheme.Text(Program.CopyBindingHelp,16,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.Margin=new Thickness(0,16,0,20);content.Children.Add(help);
                    var illustration=new Grid();illustration.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(220) });illustration.ColumnDefinitions.Add(new ColumnDefinition());var pad=new UniformGrid { Columns=4,Rows=4,Margin=new Thickness(0,0,28,0) };
                    foreach(string key in new[]{"Num","/","*","−","7","8","9","+","4","5","6","","1","2","3","Ent"}) { var box=WpfFieldTheme.Card(WpfFieldTheme.Text(key,14,key=="/"?WpfFieldTheme.Amber:WpfFieldTheme.Muted,true),new Thickness(6));box.Margin=new Thickness(3);if(key=="/")box.BorderBrush=WpfFieldTheme.Amber;pad.Children.Add(box); }illustration.Children.Add(pad);var copyHelp=WpfFieldTheme.Text(Localization.Get("CopyReminderBody"),16,WpfFieldTheme.Muted);copyHelp.TextWrapping=TextWrapping.Wrap;Grid.SetColumn(copyHelp,1);illustration.Children.Add(copyHelp);var card=WpfFieldTheme.Card(illustration);card.Margin=new Thickness(0,0,0,22);content.Children.Add(card);
                    var state=engine.WpfSnapshot(true);string[] labels={"WizardKeyMap","WizardKeyChat","WizardKeyCopy"};int[] values={state.MapKey,state.ChatKey,state.CopyKey};for(int i=0;i<3;i++) { int index=i;var row=new DockPanel { Margin=new Thickness(0,6,0,6) };var capture=WpfFieldTheme.Button(Localization.Get("WizardBtnCapture"),()=> { CaptureKey(Localization.Get(labels[index]),(vk,scan)=>engine.WpfGameBind(index,vk,scan),false);draw(); });DockPanel.SetDock(capture,Dock.Right);row.Children.Add(capture);var key=WpfFieldTheme.Card(WpfFieldTheme.Text(PhysicalKeyCapture.KeyName(values[i]),16,null,true),new Thickness(12,8,12,8));key.MinWidth=70;key.Margin=new Thickness(12,0,12,0);DockPanel.SetDock(key,Dock.Right);row.Children.Add(key);row.Children.Add(WpfFieldTheme.Text(Localization.Get(labels[i]),17));content.Children.Add(row); }
                    var single=new CheckBox { Content=Localization.Get("KeyWizardNoModifier"),IsChecked=state.CopyModifier==0,Margin=new Thickness(0,12,0,4) };single.Checked+=(s,e)=>engine.WpfSingleCopyKey(true);single.Unchecked+=(s,e)=>engine.WpfSingleCopyKey(false);content.Children.Add(single);
                }else if(step==5) {
                    var appearance=new Grid();appearance.ColumnDefinitions.Add(new ColumnDefinition());appearance.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(220) });var controls=new StackPanel();appearance.Children.Add(controls);AddSetupSettings(controls,2,draw,"SecAppearance");AddAppearanceControls(controls,engine.WpfSnapshot(false));
                    var preview=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain,Width=210,Height=210,VerticalAlignment=VerticalAlignment.Top,Tag="setup-preview" };Grid.SetColumn(preview,1);appearance.Children.Add(preview);content.Children.Add(appearance);setupPreview=preview;
                    content.Children.Add(WpfFieldTheme.Button(Localization.Get("WpfMoveMinimap"),ShowMoveHud));
                }else if(step==6) {
                    AddSetupSettings(content,2,draw,"SecMapZones");var defaults=WpfFieldTheme.Button(Localization.Get("SetupDefaultColours"),()=> { engine.WpfDefaultColours();draw();Refresh(); });defaults.Tag="setup-default-colours";content.Children.Insert(2,defaults);
                }else if(step==7) {
                    AddSetupSettings(content,0,draw);
                }else if(step==8) {
                    foreach(string helpKey in new[]{"PrivateGroupHelp","PrivatePositionGpsHelp","PrivatePositionMarkersHelp","SetupSharingVisibility"}) { var help=WpfFieldTheme.Text(Localization.Get(helpKey),16,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.Margin=new Thickness(0,18,0,0);content.Children.Add(help); }
                    var connection=WpfFieldTheme.Button(Localization.Get("PrivatePositions"),ShowPrivatePositions,true);connection.Tag="setup-private-positions";content.Children.Add(connection);
                }else {
                    string[] copy=step==0?new[]{"TutorialWelcome","WpfTutorialFilters","WizardLayoutDescription"}:step==1?new[]{"WzKeyMDesc","WzKeyHomeDesc","WzKeyDeleteDesc","WzKeyEndDesc"}:step==3?new[]{"WzSafeDesc1","WzSafeDesc2","WzSafeDesc3"}:new[]{"WzFeatDesc1","WzFeatDesc2","WzFeatDesc3"};
                    foreach(string key in copy) { var paragraph=WpfFieldTheme.Text(Localization.Get(key),18,WpfFieldTheme.Muted);paragraph.TextWrapping=TextWrapping.Wrap;var card=WpfFieldTheme.Card(paragraph);card.Margin=new Thickness(0,20,0,0);content.Children.Add(card); }
                    if(step==1)content.Children.Add(WpfFieldTheme.Button(Localization.Get("FieldShortcuts"),()=>Modal(BuildSettings(1))));
                }
                var next=HoldToProceed.Button(window,Localization.Get("WizardHoldNext"),()=> { if(step==8) { engine.WpfSave();window.Close(); }else { step++;draw(); } },true);next.Tag="setup-next-hold";DockPanel.SetDock(next,Dock.Right);footer.Children.Add(next);var back=WpfFieldTheme.Button(Localization.Get("WizardBtnPrev"),()=> { step--;draw(); });back.IsEnabled=step>0;back.MinWidth=110;back.HorizontalAlignment=HorizontalAlignment.Left;
                footer.Children.Add(back);RefreshSetupPreview();
            };draw();window.Closed+=(s,e)=> { setupPreview=null;engine.WpfSave(); };window.Content=layout;RefreshSetupPreview();return window;
        }
        void CaptureKey(string title,Func<int,int,string> validate,bool unbind) {
            var window=Window(title,460,220);var layout=new StackPanel { Margin=new Thickness(24) };var instruction=WpfFieldTheme.Text(Localization.Get("ShortcutPrompt"),18);instruction.TextWrapping=TextWrapping.Wrap;layout.Children.Add(instruction);if(unbind)layout.Children.Add(WpfFieldTheme.Button(Localization.Get("ControlUnbind"),()=> { string error=validate(0,0);if(error==null)window.Close();else instruction.Text=error; }));window.Content=layout;
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape)return;e.Handled=true;if(Keyboard.Modifiers!=ModifierKeys.None) { instruction.Text=Localization.Get("ShortcutSingleKey");return; }int vk=KeyInterop.VirtualKeyFromKey(e.Key);int scan;if(!PhysicalKeyCapture.TryGet(vk,out scan)) { instruction.Text=Localization.Get("ShortcutRetry");return; }string error=validate(vk,scan);if(error==null)window.Close();else instruction.Text=error; };Modal(window);
        }
        void ShowAddWaypoint(DrawingPoint? point=null) { var window=Window(Localization.Get("SetWaypoint"),440,220);var content=new StackPanel { Margin=new Thickness(24) };var name=new TextBox { MaxLength=120 };content.Children.Add(name);content.Children.Add(WpfFieldTheme.Button(Localization.Get("SetWaypoint"),()=> { if(!string.IsNullOrWhiteSpace(name.Text)) { engine.WpfWaypoint(name.Text.Trim(),point);window.Close(); } },true));window.Content=content;Modal(window); }
        internal void Preview(string folder) {
            Directory.CreateDirectory(folder);engine.WpfPreviewPosition("{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");engine.WpfTarget(engine.WpfSearch("grid D4").First());
            var end=DateTime.UtcNow.AddSeconds(5);while(engine.WpfSnapshot(true).Calculating&&DateTime.UtcNow<end) { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(10); }
            Refresh();Capture(tactical,Path.Combine(folder,"tactical-map.png"));for(int i=0;i<9;i++)using(new WindowLifetime(BuildOnboarding(i)))Capture(WindowLifetime.Current,Path.Combine(folder,"onboarding-"+(i+1)+".png"));
            for(int i=0;i<3;i++)using(new WindowLifetime(BuildSettings(i)))Capture(WindowLifetime.Current,Path.Combine(folder,"settings-"+new[]{"general","shortcuts","appearance"}[i]+".png"));
            using(new WindowLifetime(BuildSearch()))Capture(WindowLifetime.Current,Path.Combine(folder,"search.png"));foreach(string shape in new[]{"Square","Circle"}) { engine.WpfAppearance(shape,100);Refresh();Capture(hud,Path.Combine(folder,"hud-"+shape.ToLowerInvariant()+".png")); }
        }
        internal static void Capture(Window window,string file) { window.ShowActivated=false;window.Left=-32000;window.Top=-32000;window.Show();window.UpdateLayout();var content=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))encoder.Save(stream);window.Hide(); }
        sealed class WindowLifetime:IDisposable { internal static Window Current;readonly Window window;internal WindowLifetime(Window window) { this.window=window;Current=window; }public void Dispose() { window.Close(); } }
        public void Dispose() { disposed=true;DisposeRoutePreviews();if(privatePositions!=null)privatePositions.Dispose();if(relayGroups!=null)relayGroups.Dispose();refresh.Stop();hud.Close();tactical.Close();engine.WpfSave();engine.Dispose();terrain.Dispose(); }
    }
}
