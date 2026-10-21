using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Collections.Generic;
class SquadUiTests {
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static object Get(object o,string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,F).SetValue(o,v); }
    static object Call(object o,string n,params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static void Check(bool v,string n) { if(!v)throw new Exception(n);Console.WriteLine("PASS: "+n); }
    static bool Overlap(Rect[] bounds) { for(int i=0;i<bounds.Length;i++)for(int j=i+1;j<bounds.Length;j++) { var shared=Rect.Intersect(bounds[i],bounds[j]);if(!shared.IsEmpty&&shared.Width>0&&shared.Height>0)return true; }return false; }
    static void Pump() { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(20); }
    static void Await(Func<bool> test) { var end=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<end) { if(test())return;Pump(); }throw new Exception("UI fixture timed out"); }
    static void Capture(Window window,string path) { window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file); }
    static IEnumerable<DependencyObject> Children(DependencyObject p) { foreach(object c in LogicalTreeHelper.GetChildren(p))if(c is DependencyObject) { yield return (DependencyObject)c;foreach(var d in Children((DependencyObject)c))yield return d; } }
    static void PressKey(UIElement target,Key key) { target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),Environment.TickCount,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });Pump(); }
    static IEnumerable<GeometryDrawing> GeometryDrawings(Drawing drawing) {
        var geometry=drawing as GeometryDrawing;if(geometry!=null)yield return geometry;
        var group=drawing as DrawingGroup;if(group!=null)foreach(var child in group.Children)foreach(var item in GeometryDrawings(child))yield return item;
    }
    static void CheckRouteAppearance(object runtime,string folder) {
        var assembly=runtime.GetType().Assembly;var surfaceType=assembly.GetType("ScumMiniMap.WpfMapSurface");
        var memberInk=surfaceType.GetMethod("MemberInk",BindingFlags.Static|BindingFlags.NonPublic);
        Func<string,Color> color=id=>((SolidColorBrush)memberInk.Invoke(null,new object[]{id})).Color;
        string alice="11111111111111111111111111111111",bob="22222222222222222222222222222222";
        Check(color(alice)==color("group:"+alice)&&color(alice)==color("other-group:"+alice),"Member colors agree across viewers and group prefixes");
        Check(color(alice)!=color(bob),"Different synthetic members have different assigned colors");
        Check(color(alice)==color("11111111abcdef0123456789abcdef01"),"Fresh connection identities retain the member's assigned color");
        var storeType=assembly.GetType("ScumMiniMap.RelayGroupStore");string profile=Path.Combine(folder,"color-profile");
        Func<string,object> store=path=>Activator.CreateInstance(storeType,F,null,new object[]{path,"127.0.0.1",47832,null,null},null);
        string seed;using(var first=(IDisposable)store(profile))seed=(string)Get(first,"MemberColorSeed");
        using(var reloaded=(IDisposable)store(profile))Check((string)Get(reloaded,"MemberColorSeed")==seed,"Assigned member color survives a profile reload without connecting to a relay");
        using(var other=(IDisposable)store(Path.Combine(folder,"other-color-profile")))Check((string)Get(other,"MemberColorSeed")!=seed,"Different profiles receive separate persistent member colors");
        foreach(bool compact in new[]{false,true}) {
            var state=Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfMapState"),true);
            Set(state,"Saved",Array.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"),0));Set(state,"Trail",new System.Drawing.PointF[0]);Set(state,"Pan",new System.Drawing.PointF(.5f,.5f));Set(state,"Zoom",1f);Set(state,"Shape","Square");Set(state,"LocalPeerId",alice);Set(state,"RouteColor",System.Drawing.Color.Lime.ToArgb());
            var position=Activator.CreateInstance(assembly.GetType("ScumMiniMap.Position"));Set(position,"X",0d);Set(position,"Y",0d);Set(state,"Position",position);
            var road=Activator.CreateInstance(assembly.GetType("ScumMiniMap.RoadRoute"));Set(road,"Success",true);Set(road,"Polyline",new[]{new System.Drawing.PointF(.2f,.4f),new System.Drawing.PointF(.8f,.4f)});Set(state,"Route",road);
            var plan=Activator.CreateInstance(assembly.GetType("ScumMiniMap.SquadRoutePlan"),true);Set(plan,"Legs",new[]{new[]{new System.Drawing.PointF(.2f,.6f),new System.Drawing.PointF(.8f,.6f)},new[]{new System.Drawing.PointF(.2f,.7f),new System.Drawing.PointF(.8f,.7f)}});Set(plan,"Verified",new[]{true,false});
            var routeType=assembly.GetType("ScumMiniMap.SquadRouteDrawing");var remote=Activator.CreateInstance(routeType,true);Set(remote,"Key","group:"+bob+":route");Set(remote,"Plan",plan);var routes=Array.CreateInstance(routeType,1);routes.SetValue(remote,0);Set(state,"OtherRoutePlans",routes);
            var markerType=assembly.GetType("ScumMiniMap.SharedMapMarker");var stops=Array.CreateInstance(markerType,2);
            for(int i=0;i<2;i++) { var stop=Activator.CreateInstance(markerType,true);Set(stop,"Id","53485244"+new string('a',32)+i.ToString("X2")+new string('b',22));Set(stop,"Name","Pending stop");Set(stop,"Owner","Bob");Set(stop,"OwnerId","group:"+bob);Set(stop,"X",.2+i*.6);Set(stop,"Y",.8);stops.SetValue(stop,i); }Set(state,"SharedMarkers",stops);
            var surface=(FrameworkElement)Activator.CreateInstance(surfaceType,true);Set(surface,"State",state);Set(surface,"Compact",compact);surface.Measure(new Size(640,640));surface.Arrange(new Rect(0,0,640,640));
            var drawing=new DrawingGroup();using(var dc=drawing.Open())Call(surface,"DrawMap",dc);
            var lines=GeometryDrawings(drawing).Where(g=>g.Pen!=null&&g.Geometry.Bounds.Width>100&&g.Pen.Brush is SolidColorBrush).ToArray();
            var local=lines.Where(g=>((SolidColorBrush)g.Pen.Brush).Color==color(alice)).ToArray();var others=lines.Where(g=>((SolidColorBrush)g.Pen.Brush).Color==color(bob)).ToArray();
            Check(local.Length==2&&others.Length==3&&local.Concat(others).All(g=>g.Pen.Thickness==4),"Personal GPS connector and retained path, verified, unverified and pending squad routes have equal thickness: "+compact);
            Check(others.Any(g=>g.Pen.DashStyle.Dashes.Count>0),"Unverified squad routing retains dashed guidance: "+compact);
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawDrawing(drawing);var bitmap=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,compact?"route-colors-hud.png":"route-colors-tactical.png")))png.Save(file);
        }
    }
    static void CheckViewportPixels(object runtime,string folder) {
        var assembly=runtime.GetType().Assembly;var surfaceType=assembly.GetType("ScumMiniMap.WpfMapSurface");var stateType=assembly.GetType("ScumMiniMap.WpfMapState");
        foreach(bool compact in new[]{false,true})foreach(float zoom in new[]{1f,4f,16f}) {
            var state=Activator.CreateInstance(stateType,true);Set(state,"Saved",Array.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"),0));Set(state,"Trail",new System.Drawing.PointF[0]);Set(state,"Grid",true);Set(state,"GridLabels",true);Set(state,"GridOpacity",100);Set(state,"LabelSize",16);Set(state,"Pan",new System.Drawing.PointF(.42f,.47f));Set(state,"Zoom",zoom);Set(state,"Shape","Square");
            if(compact) {var position=Activator.CreateInstance(assembly.GetType("ScumMiniMap.Position"));Set(position,"X",-143091d);Set(position,"Y",-142091d);Set(position,"Yaw",45d);Set(state,"Position",position);Set(state,"RotateMap",true);}
            var surface=(FrameworkElement)Activator.CreateInstance(surfaceType,true);Set(surface,"State",state);Set(surface,"Compact",compact);surface.Measure(new Size(640,360));surface.Arrange(new Rect(0,0,640,360));
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) {Call(surface,"DrawMap",dc);dc.PushClip(new RectangleGeometry(new Rect(0,0,640,360)));Set(surface,"renderRotation",0d);foreach(var at in new[]{new Point(-200,60),new Point(-20,100),new Point(630,120),new Point(120,-8),new Point(120,355),new Point(300,180)})Call(surface,"Label",dc,"Viewport edge fixture",at,Brushes.White,16d);dc.Pop();}
            var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var pixels=new byte[640*360*4];bitmap.CopyPixels(pixels,640*4,0);
            string key=(compact?"rotated":"tactical")+"-"+zoom;File.WriteAllBytes(Path.Combine(folder,key+".pixels"),pixels);
            string reference=Environment.GetEnvironmentVariable("MINIMAP_CAMERA_PIXEL_REFERENCE");if(!string.IsNullOrEmpty(reference)) {var before=File.ReadAllBytes(Path.Combine(reference,key+".pixels"));Check(before.SequenceEqual(pixels),"Grid, rotation and partially visible labels retain identical pixels: "+key);}
        }
    }
    static void CheckCameraInteraction(object runtime,string folder,bool verify) {
        var engine=Get(runtime,"engine");var map=(FrameworkElement)Get(runtime,"tacticalMap");var window=(Window)Get(runtime,"tactical");
        window.WindowState=WindowState.Normal;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000;window.Width=Environment.GetEnvironmentVariable("MINIMAP_CAMERA_4K")=="1"?3840:1280;window.Height=Environment.GetEnvironmentVariable("MINIMAP_CAMERA_4K")=="1"?2160:760;window.ShowActivated=false;window.Show();window.UpdateLayout();
        string cacheScale=Environment.GetEnvironmentVariable("MINIMAP_CAMERA_CACHE_SCALE");if(!string.IsNullOrEmpty(cacheScale))map.CacheMode=new BitmapCache(double.Parse(cacheScale,System.Globalization.CultureInfo.InvariantCulture));
        Call(engine,"WpfPoiClustering",false);Set(engine,"fullMapZoom",8f);Set(engine,"fullMapOpacity",100);Call(runtime,"Refresh");
        Action render=()=> {map.UpdateLayout();var bitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(map);};
        render();Await(()=> {render();return (int)Get(map,"PoiSceneBuilds")>0;});
        var options=Get(engine,"cachedZoneOptions");var anchors=Get(engine,"cachedLocationAnchors");var revision=Get(engine,"wpfZoneRevision");
        var pan=(Action<System.Drawing.PointF>)Get(map,"PanChanged");var eventTimes=new List<double>();var renderTimes=new List<double>();
        for(int i=0;i<24;i++) { var clock=System.Diagnostics.Stopwatch.StartNew();pan(new System.Drawing.PointF(.40f+i*.002f,.45f));clock.Stop();eventTimes.Add(clock.Elapsed.TotalMilliseconds);clock.Restart();render();clock.Stop();renderTimes.Add(clock.Elapsed.TotalMilliseconds);Pump(); }
        eventTimes.Sort();renderTimes.Sort();Console.WriteLine("CAMERA pan event median="+eventTimes[12].ToString("F2")+" ms, p95="+eventTimes[22].ToString("F2")+" ms; composition median="+renderTimes[12].ToString("F2")+" ms, p95="+renderTimes[22].ToString("F2")+" ms; zone revision change="+(!object.Equals(revision,Get(engine,"wpfZoneRevision"))));
        var blankVisual=new DrawingVisual();using(var dc=blankVisual.RenderOpen())dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,map.ActualWidth,map.ActualHeight));var blankTimes=new List<double>();
        for(int i=0;i<12;i++) {var clock=System.Diagnostics.Stopwatch.StartNew();var bitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(blankVisual);clock.Stop();blankTimes.Add(clock.Elapsed.TotalMilliseconds);}blankTimes.Sort();
        Console.WriteLine("CAMERA blank bitmap median="+blankTimes[6].ToString("F2")+" ms; scene overhead="+(renderTimes[12]-blankTimes[6]).ToString("F2")+" ms");
        if(Environment.GetEnvironmentVariable("MINIMAP_CAMERA_LAYERS")=="1") {
            Call(runtime,"EndCameraInteraction");render();
            Func<double> measure=()=> {var times=new List<double>();for(int i=0;i<10;i++) {map.InvalidateVisual();var clock=System.Diagnostics.Stopwatch.StartNew();render();clock.Stop();times.Add(clock.Elapsed.TotalMilliseconds);}times.Sort();return times[5];};
            var layerState=Get(map,"State");var store=Get(layerState,"Store");var terrain=Get(map,"Terrain");var background=Get(map,"BaseMap");
            Console.WriteLine("LAYERS all="+measure().ToString("F2"));
            var overview=Get(terrain,"overview");Set(terrain,"overview",null);Console.WriteLine("LAYERS without overview="+measure().ToString("F2"));Set(terrain,"overview",overview);
            var recordClock=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<12;i++) {var drawing=new DrawingGroup();using(var dc=drawing.Open())Call(map,"OnRender",dc);}recordClock.Stop();Console.WriteLine("LAYERS recording="+(recordClock.Elapsed.TotalMilliseconds/12).ToString("F2")+" ms; saved="+((Array)Get(layerState,"Saved")).Length);
            Set(map,"Terrain",null);Set(map,"BaseMap",null);Console.WriteLine("LAYERS without terrain="+measure().ToString("F2"));Set(map,"Terrain",terrain);Set(map,"BaseMap",background);
            var zones=Get(layerState,"Zones");Set(layerState,"Zones",false);Console.WriteLine("LAYERS without zones="+measure().ToString("F2"));Set(layerState,"Zones",zones);
            var labels=Get(layerState,"Labels");Set(layerState,"Labels",false);Console.WriteLine("LAYERS without labels="+measure().ToString("F2"));Set(layerState,"Labels",labels);
            var master=Get(store,"MasterEnabled");Set(store,"MasterEnabled",false);Console.WriteLine("LAYERS without catalogue="+measure().ToString("F2"));Set(store,"MasterEnabled",master);
            var waypoints=Get(layerState,"Waypoints");Set(layerState,"Waypoints",false);Console.WriteLine("LAYERS without waypoints="+measure().ToString("F2"));Set(layerState,"Waypoints",waypoints);
            RenderOptions.SetBitmapScalingMode(map,BitmapScalingMode.NearestNeighbor);Console.WriteLine("LAYERS nearest sampling="+measure().ToString("F2"));
            return;
        }
        File.WriteAllText(Path.Combine(folder,"camera-performance.csv"),"event_median_ms,event_p95_ms,composition_median_ms,composition_p95_ms\n"+string.Join(",",new[]{eventTimes[12],eventTimes[22],renderTimes[12],renderTimes[22]}.Select(n=>n.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));
        var slider=(Slider)Get(runtime,"tacticalOpacity");Console.WriteLine("CAMERA opacity enabled at 8x="+slider.IsEnabled);
        if(!verify)return;
        // Gesture previews deliberately reuse the original static scene. Check
        // current-view spatial culling after the full-detail idle draw returns.
        Call(runtime,"EndCameraInteraction");render();Await(()=> {render();return (bool)Get(map,"PoiViewportReady");});
        var tiles=(Array)Get(map,"poiSceneTiles");Check(tiles!=null&&tiles.Length>=(int)Get(map,"PoiTilesDrawn")&&tiles.Length<=48&&(int)Get(map,"PoiTilesDrawn")>0,"Large-zoom POI composition submits only visible spatial groups");
        var state=Get(map,"State");double width=map.ActualWidth,height=map.ActualHeight;
        double side=(double)map.GetType().GetMethod("MapSide",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{state,width,height,false});
        var centre=(System.Drawing.PointF)map.GetType().GetMethod("CameraCentre",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{state,width,height,false});
        double left=width/2-side*centre.X,top=height/2-side*centre.Y,scale=side/(double)Get(map,"drawnPoiSide");
        Func<bool,byte[]> poiPixels=culled=> { var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) { dc.PushClip(new RectangleGeometry(new Rect(0,0,width,height)));if(culled)Call(map,"DrawRetainedPois",dc,side,left,top,width,height);else { dc.PushTransform(new TranslateTransform(left,top));dc.PushTransform(new ScaleTransform(scale,scale));foreach(var tile in tiles)dc.DrawDrawing((DrawingGroup)Get(tile,"Drawing"));dc.Pop();dc.Pop(); }dc.Pop(); }var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[(int)width*(int)height*4];bitmap.CopyPixels(pixels,(int)width*4,0);return pixels; };
        Check(poiPixels(true).SequenceEqual(poiPixels(false)),"Visible POI pixels match the complete drawing with no missing edge icons");
        Check(object.Equals(revision,Get(engine,"wpfZoneRevision"))&&object.ReferenceEquals(options,Get(engine,"cachedZoneOptions"))&&object.ReferenceEquals(anchors,Get(engine,"cachedLocationAnchors")),"Panning preserves zone controls and location caches");
        Check(slider.IsEnabled,"Opacity stays adjustable while zoomed in");slider.Value=35;Pump();Call(runtime,"Refresh");
        Check((int)Get(Get(map,"State"),"Opacity")==35&&(int)Get(engine,"fullMapOpacity")==35&&((TextBlock)Get(runtime,"tacticalOpacityValue")).Text=="35%","Zoomed opacity updates the actual terrain state, saved preference and percentage together");
        var zoom=(Action<int>)Get(map,"ZoomRequested");zoom(-1);zoom(1);Pump();
        Check((int)Get(Get(map,"State"),"Opacity")==35&&slider.IsEnabled,"Zooming preserves the player's selected transparency");
        Check(object.Equals(revision,Get(engine,"wpfZoneRevision")),"Zoom and opacity changes preserve unrelated zone caches");
        Await(()=>RenderOptions.GetBitmapScalingMode(map)==BitmapScalingMode.Unspecified);Check(true,"Smooth bitmap sampling returns after camera interaction settles");
        bool wasDiagnostic=(bool)Get(engine,"diagnosticMode");try {Set(engine,"diagnosticMode",false);Call(engine,"WpfSave");Set(engine,"fullMapOpacity",99);Set(engine,"wpfTacticalOpacityOverride",false);Call(engine,"LoadSettings");Check((int)Get(Call(engine,"WpfSnapshot",true),"Opacity")==35&&(bool)Get(engine,"wpfTacticalOpacityOverride"),"Explicit zoomed transparency survives saving and reloading the isolated profile");}finally {Set(engine,"diagnosticMode",wasDiagnostic);}
        Capture(window,Path.Combine(folder,"zoomed-opacity.png"));
    }
    static void CheckSquadIntegration(object runtime,string folder) {
        var engine=Get(runtime,"engine");var squad=Get(runtime,"squad");var map=Get(runtime,"tacticalMap");
        Func<Array> stops=()=>(Array)squad.GetType().GetProperty("Stops",F).GetValue(squad,null);
        var search=(Window)Call(runtime,"BuildSearch");
        try {
            search.Show();Pump();var input=Children(search).OfType<TextBox>().Single(c=>c.Tag as string=="search-query");
            var add=Children(search).OfType<Button>().Single(c=>c.Tag as string=="search-add-stop");input.Text="zzzz_no_such_location_987654321";
            Check(!add.IsEnabled,"Search cannot add a route stop without a result");input.Text="grid D4";Pump();
            var selected=(ListBoxItem)Children(search).OfType<ListBox>().Single().SelectedItem;var destination=selected.Tag;
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!search.IsVisible&&stops().Length==1&&(string)Get(stops().GetValue(0),"Name")== (string)destination.GetType().GetProperty("DisplayName").GetValue(destination,null),"Search adds its selected named location directly to the active route");
            Check(Get(engine,"searchTarget")!=null&&((Array)Get(runtime,"activeStops")).Length==1,"A stop added from search uses the existing automatic GPS navigation");
            Call(runtime,"NavigateDestination",destination);Call(runtime,"UpdateSquad");
            Check(object.ReferenceEquals(Get(engine,"searchTarget"),destination)&&((Array)Get(runtime,"activeStops")).Length==0&&stops().Length==1,"Ordinary GPS navigation immediately replaces route navigation and retains its editable stops");
        }finally {search.Close();}
        var tools=(Window)Call(runtime,"BuildSquadTools",0);
        try {
            tools.Show();Pump();
            var modeButtons=Children(tools).OfType<Button>().Where(b=>(b.Tag as string??"").StartsWith("route-mode-")).ToArray();
            Check(modeButtons.Length==3,"Routes exposes Quickest, Shortest and Paved roads only");
            var stopsBefore=stops().Length;
            modeButtons.Single(b=>b.Tag as string=="route-mode-Quickest").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Get(engine,"wpfRouteMode").ToString()=="Quickest"&&stops().Length==stopsBefore,"Quickest changes the routing preference without removing stops");
            modeButtons.Single(b=>b.Tag as string=="route-mode-PavedOnly").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Get(engine,"wpfRouteMode").ToString()=="PavedOnly","Paved-only selection reaches the actual navigation engine");
            bool modeDiagnostic=(bool)Get(engine,"diagnosticMode");
            try { Set(engine,"diagnosticMode",false);Call(engine,"WpfSave");Set(engine,"wpfRouteMode",Enum.Parse(Get(engine,"wpfRouteMode").GetType(),"Quickest"));Call(engine,"LoadSettings");Check(Get(engine,"wpfRouteMode").ToString()=="PavedOnly","Routing preference survives saving and loading the isolated profile"); }
            finally { Set(engine,"diagnosticMode",modeDiagnostic); }
            Capture(tools,Path.Combine(folder,"road-route-modes.png"));
            modeButtons.Single(b=>b.Tag as string=="route-mode-Shortest").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Children(tools).OfType<Button>().Any(b=>b.Tag as string=="route-remove:"+(string)Get(stops().GetValue(0),"Id")),"Routes exposes removal of an individual named stop");
            var remove=Children(tools).OfType<Button>().Single(b=>(b.Tag as string??"").StartsWith("route-remove:"));remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(stops().Length==0&&Get(engine,"searchTarget")!=null,"Removing an inactive local stop preserves the current manual GPS destination");
            Children(tools).OfType<Button>().Single(b=>b.Tag as string=="squad-tab-1").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Children(tools).OfType<Button>().Where(b=>(b.Tag as string??"").StartsWith("squad-ping-")).All(b=>!b.IsEnabled),"Squad pings are visibly unavailable without a connected group");
            Children(tools).OfType<Button>().Single(b=>b.Tag as string=="squad-tab-2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var name=Children(tools).OfType<TextBox>().Single();name.Text="Synthetic retained preset";name.Focus();
            var end=DateTime.UtcNow.AddSeconds(2.2);while(DateTime.UtcNow<end)Pump();
            Check(name.IsKeyboardFocused&&name.Text=="Synthetic retained preset","Live route refresh preserves preset text and keyboard focus");
            Capture(tools,Path.Combine(folder,"integrated-presets.png"));
            Children(tools).OfType<Button>().Single(b=>b.Tag as string=="squad-tab-0").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var addTarget=Children(tools).OfType<Button>().Single(b=>b.Tag as string=="route-add-target");addTarget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(stops().Length==1&&((Array)Get(runtime,"activeStops")).Length==1&&!addTarget.IsEnabled,"Routes turns the current manual GPS destination into an active stop and prevents re-adding its own navigation target");
            Capture(tools,Path.Combine(folder,"integrated-routes.png"));
        }finally {tools.Close();Call(runtime,"ClearMapRoute");}
        for(int i=0;i<16;i++)Call(squad,"AddStop","Synthetic limit "+i,.2d+i*.001,.3d);
        search=(Window)Call(runtime,"BuildSearch");
        try { Check(!Children(search).OfType<Button>().Single(b=>b.Tag as string=="search-add-stop").IsEnabled,"Search disables Add route stop at the sixteen-stop limit"); }
        finally {search.Close();Call(runtime,"ClearMapRoute");}
        bool wasDiagnostic=(bool)Get(engine,"diagnosticMode"),wasPanel=(bool)Get(engine,"panelOpening"),wasCapture=(bool)Get(engine,"shortcutCaptureOpen");var wasUpdated=Get(engine,"updated");
        try {
            Set(engine,"diagnosticMode",false);Set(engine,"panelOpening",true);Set(engine,"shortcutCaptureOpen",true);Set(engine,"updated",DateTime.UtcNow);
            var pingChat=Get(engine,"chat");
            Console.WriteLine("PING PREFLIGHT: paused="+engine.GetType().GetProperty("IntensiveWorkPaused",F).GetValue(engine,null)+", closing="+Get(engine,"closing")+", busy="+Get(engine,"adminCommandBusy")+", chat="+pingChat.GetType().GetProperty("Paused",F).GetValue(pingChat,null)+", inventory="+Get(engine,"inventoryInputLocked")+", position="+(Get(engine,"position")!=null));
            Check(!(bool)Call(engine,"WpfCanPing",false)&&(bool)Call(engine,"WpfCanPing",true),"Only explicit squad-dialog pings may use a fresh position while dialog input protection is active");
            Check((bool)Get(engine,"panelOpening")&&(bool)Get(engine,"shortcutCaptureOpen"),"Ping eligibility never resumes coordinate capture or releases dialog input protection");
            Set(engine,"updated",DateTime.UtcNow.AddMinutes(-1));Check(!(bool)Call(engine,"WpfCanPing",true),"Squad-dialog pings reject stale positions");Set(engine,"updated",DateTime.UtcNow);
            Set(engine,"inventoryInputLocked",true);try {Check(!(bool)Call(engine,"WpfCanPing",true),"Squad-dialog pings retain inventory pause protection");}finally {Set(engine,"inventoryInputLocked",false);}
        }finally {Set(engine,"diagnosticMode",wasDiagnostic);Set(engine,"panelOpening",wasPanel);Set(engine,"shortcutCaptureOpen",wasCapture);Set(engine,"updated",wasUpdated);}
    }
    static void CheckSaveLocation(object runtime,string folder) {
        var engine=Get(runtime,"engine");var point=new System.Drawing.PointF(.41f,.42f);int before=((System.Collections.IList)Get(engine,"zones")).Count;
        var window=(Window)Call(runtime,"BuildAddWaypoint",point);
        try {
            window.Show();Pump();
            var name=Children(window).OfType<TextBox>().Single(c=>c.Tag as string=="waypoint-name");
            var save=Children(window).OfType<Button>().Single(c=>c.Tag as string=="waypoint-save");
            Await(()=>name.IsKeyboardFocused);
            Check(name.IsKeyboardFocused&&!save.IsEnabled,"Save location starts in the name field with Save disabled");
            var namePeer=new System.Windows.Automation.Peers.TextBoxAutomationPeer(name);
            Check(!string.IsNullOrWhiteSpace(namePeer.GetName())&&namePeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Value)!=null,"Save location name exposes its label and editable value to assistive tools");
            var savePeer=new System.Windows.Automation.Peers.ButtonAutomationPeer(save);
            Check(savePeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!=null,"Save location exposes a native automation Invoke action");
            name.Text="   ";PressKey(name,Key.Enter);
            Check(!save.IsEnabled&&window.IsVisible&&((System.Collections.IList)Get(engine,"zones")).Count==before,"Whitespace and Enter cannot save an empty location");
            name.Text="  Synthetic keyboard location  ";Check(save.IsEnabled,"A nonempty location name enables Save");
            Capture(window,Path.Combine(folder,"save-location.png"));PressKey(name,Key.Enter);
            var saved=(System.Collections.IList)Get(engine,"zones");var marker=saved[saved.Count-1];
            Check(!window.IsVisible&&saved.Count==before+1&&(string)Get(marker,"Name")=="Synthetic keyboard location","Enter saves one trimmed location and closes the dialog");
            var points=(System.Drawing.PointF[])Get(marker,"Points");Check(points.Length==1&&points[0]==point,"Save location preserves the original clicked coordinates");
            Check((bool)Call(engine,"WpfDeleteWaypoint",marker),"Save-location fixture cleans its persisted synthetic marker");
        }finally {window.Close();}
        window=(Window)Call(runtime,"BuildAddWaypoint",point);bool loadFailed=(bool)Get(engine,"zonesLoadFailed");
        try {
            window.Show();Pump();var name=Children(window).OfType<TextBox>().Single();name.Text="Synthetic retry";Set(engine,"zonesLoadFailed",true);PressKey(name,Key.Enter);
            Check(window.IsVisible&&name.Text=="Synthetic retry"&&((System.Collections.IList)Get(engine,"zones")).Count==before,"A failed location save retains the name and dialog without committing a marker");
            Set(engine,"zonesLoadFailed",loadFailed);Children(window).OfType<Button>().Single(c=>c.Tag as string=="waypoint-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!window.IsVisible&&((System.Collections.IList)Get(engine,"zones")).Count==before,"Cancel after a failed save leaves saved locations unchanged");
        }finally {Set(engine,"zonesLoadFailed",loadFailed);window.Close();}
        window=(Window)Call(runtime,"BuildAddWaypoint",point);
        try {
            window.Show();Pump();var name=Children(window).OfType<TextBox>().Single();name.Text="Synthetic cancelled";PressKey(name,Key.Escape);
            Check(!window.IsVisible&&((System.Collections.IList)Get(engine,"zones")).Count==before,"Escape cancels a typed location without saving");
        }finally {window.Close();}
        var position=Get(engine,"position");Set(engine,"position",null);
        try {
            window=(Window)Call(runtime,"BuildAddWaypoint",new object[]{null});window.Show();Pump();Children(window).OfType<TextBox>().Single().Text="Unavailable location";
            Check(!Children(window).OfType<Button>().Single(c=>c.Tag as string=="waypoint-save").IsEnabled,"Saving current location stays disabled until a position is available");
        }finally {window.Close();Set(engine,"position",position);}
        bool modalFocused=false,modalProtected=false;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
            var actual=((System.Collections.IEnumerable)Get(runtime,"dialogs")).Cast<Window>().Single();var name=Children(actual).OfType<TextBox>().Single();
            modalFocused=name.IsKeyboardFocused;modalProtected=(bool)Get(engine,"panelOpening");name.Text="Synthetic modal location";
            Children(actual).OfType<Button>().Single(b=>b.Tag as string=="waypoint-save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        Call(runtime,"ShowAddWaypoint",point);
        var modalMarkers=(System.Collections.IList)Get(engine,"zones");var modalMarker=modalMarkers[modalMarkers.Count-1];
        Check(modalFocused&&modalProtected&&modalMarkers.Count==before+1&&!(bool)Get(engine,"panelOpening"),"Actual Save location modal focuses the field, protects game input, saves on click and releases input protection");
        Check((bool)Call(engine,"WpfDeleteWaypoint",modalMarker),"Actual-modal fixture removes its saved synthetic marker");
        foreach(double scale in new[]{1.5,2.0}) {
            window=(Window)Call(runtime,"BuildAddWaypoint",point);
            try {
                window.Width=720;window.Height=520;((FrameworkElement)window.Content).LayoutTransform=new ScaleTransform(scale,scale);window.Show();Pump();window.UpdateLayout();
                var editors=Children(window).OfType<FrameworkElement>().Where(e=>e is Button||e is TextBox).ToArray();
                Check(editors.All(e=> {var rect=e.TransformToAncestor(window).TransformBounds(new Rect(e.RenderSize));return rect.Left>=0&&rect.Top>=0&&rect.Right<=window.ActualWidth&&rect.Bottom<=window.ActualHeight;}),"Save location controls fit with simulated UI magnification: "+scale);
                Capture(window,Path.Combine(folder,"save-location-scale-"+(int)(scale*100)+".png"));
            }finally {window.Close();}
        }
    }
    static void CheckUsability(object runtime,string folder) {
        var search=(Window)Call(runtime,"BuildSearch");
        try {
            search.Show();Pump();
            var input=Children(search).OfType<TextBox>().Single(c=>c.Tag as string=="search-query");
            var results=Children(search).OfType<ListBox>().Single(c=>c.Tag as string=="search-results");
            Check(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(input)),"Search query has a descriptive accessible name");
            input.Text="zzzz_no_such_location_987654321";Pump();
            Check(results.Items.Count==0,"Unmatched search shows an empty results list");
            PressKey(input,Key.Down);PressKey(input,Key.Up);PressKey(input,Key.Enter);
            Check(search.IsVisible&&results.SelectedIndex==-1,"Arrow keys and Enter on empty search preserve the dialog without selecting a result");
            input.Text="grid D4";Pump();PressKey(input,Key.Down);
            Check(results.SelectedItem!=null,"Search keyboard selection recovers when results return");
            Check(System.Windows.Automation.AutomationProperties.GetName((ListBoxItem)results.SelectedItem).Contains("D4"),"Search result announces its destination and detail instead of a panel type");
            Capture(search,Path.Combine(folder,"search-accessible.png"));
        }finally { search.Close(); }
        var settings=(Window)Call(runtime,"BuildSettings",2);
        try {
            settings.Show();Pump();
            var combo=Children(settings).OfType<ComboBox>().First();combo.Focus();combo.IsDropDownOpen=true;Pump();
            PressKey(combo,Key.Escape);
            Check(settings.IsVisible&&!combo.IsDropDownOpen,"First Escape dismisses a settings dropdown and keeps Settings open");
            combo.ApplyTemplate();var outline=(Border)combo.Template.FindName("outline",combo);
            Check(outline!=null&&outline.BorderThickness.Left==2,"Focused dropdown has a visible focus outline");
            var slider=Children(settings).OfType<Slider>().First();slider.Focus();Pump();slider.ApplyTemplate();
            var focus=(Border)slider.Template.FindName("focus",slider);
            Check(focus!=null&&((SolidColorBrush)focus.BorderBrush).Color.A!=0,"Focused slider has a visible focus outline");
            Capture(settings,Path.Combine(folder,"settings-keyboard-focus.png"));
            PressKey(slider,Key.Escape);Check(!settings.IsVisible,"Escape closes Settings after its dropdown has been dismissed");
        }finally { settings.Close(); }
        settings=(Window)Call(runtime,"BuildSettings",0);
        try {
            settings.Show();Pump();var numbers=Children(settings).OfType<TextBox>().Where(c=>c.Tag as string=="setting-number").ToArray();
            var auto=Children(settings).OfType<CheckBox>().Single(c=>c.Tag as string=="auto-launch-scum");
            Check(!auto.IsEnabled,"Diagnostic settings cannot register an automatic startup task");
            Check(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(auto)),"Automatic launch has an accessible text label");
            Check(auto.Content is TextBlock&&((TextBlock)auto.Content).TextWrapping==TextWrapping.Wrap,"Automatic launch label wraps in narrow settings windows");
            Capture(settings,Path.Combine(folder,"settings-auto-launch.png"));
            Check(numbers.Length>0&&numbers.All(c=>!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(c))),"Numeric settings expose their existing labels to assistive tools");
            Check(numbers.All(c=>!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(c))),"Numeric settings expose their valid ranges to assistive tools");
            bool bindingEditor=false;var editorTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
            editorTimer.Tick+=(s,e)=> {editorTimer.Stop();var editor=Application.Current.Windows.OfType<Window>().Single(w=>w.IsVisible&&w!=settings&&Children(w).OfType<Button>().Any(b=>b.Tag as string=="game-binding-0"));bindingEditor=true;editor.Close();};
            editorTimer.Start();try {
                Children(settings).OfType<Button>().Single(b=>b.Content as string=="SCUM and MiniMap key rebinding wizard...").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(bindingEditor,"Rebinding wizard opens the binding editor");
            }finally {editorTimer.Stop();}
        }finally { settings.Close(); }
        settings=(Window)Call(runtime,"BuildSettings",1);
        try {
            settings.Show();Pump();var keys=Children(settings).OfType<Button>().Where(c=>c.MinWidth==88).ToArray();
            Check(keys.Length>0&&keys.All(c=>System.Windows.Automation.AutomationProperties.GetName(c).Contains(": ")),"Shortcut buttons identify both the action and its current binding");
            Check(new[]{"game-binding-0","game-binding-1","game-binding-2"}.All(tag=>Children(settings).OfType<Button>().Any(c=>c.Tag as string==tag)),"Shortcuts expose editable SCUM map, chat and copy bindings");
            Capture(settings,Path.Combine(folder,"settings-game-bindings.png"));
            var engine=Get(runtime,"engine");var oldBinding=Call(engine,"WpfSnapshot",false);bool captured=false;
            var captureTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
            captureTimer.Tick+=(s,e)=> {
                captureTimer.Stop();var captureWindow=Application.Current.Windows.OfType<Window>().Single(w=>w!=settings&&w.Title=="SCUM map key");
                runtime.GetType().Assembly.GetType("ScumMiniMap.PhysicalKeyCapture").GetMethod("Observe",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{0x79,0x44u,0x10u});
                PressKey(captureWindow,Key.F10);captured=true;
            };
            captureTimer.Start();try {
                Children(settings).OfType<Button>().Single(c=>c.Tag as string=="game-binding-0").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(captured&&(int)Get(Call(engine,"WpfSnapshot",false),"MapKey")==0x79,"Map key capture applies the chosen binding through the settings UI");
                Check(Children(settings).OfType<Button>().Single(c=>c.Tag as string=="game-binding-0").Content as string=="F10","Map binding label refreshes after capture");
            }finally {captureTimer.Stop();Call(engine,"WpfGameBind",0,Get(oldBinding,"MapKey"),Get(oldBinding,"MapScan"));}
        }finally { settings.Close(); }
        using(var parent=new System.Windows.Forms.FlowLayoutPanel())using(var combo=new System.Windows.Forms.ComboBox()) {
            parent.Controls.Add(new System.Windows.Forms.Label {Text="Synthetic setting"});parent.Controls.Add(combo);combo.Enabled=false;
            var converted=(ComboBox)Call(runtime,"ConvertSetting",combo,(Action)(()=>{}));
            Check(!converted.IsEnabled&&System.Windows.Automation.AutomationProperties.GetName(converted)=="Synthetic setting","Converted dropdown retains its disabled state and associated label");
        }
        var currentLanguage=runtime.GetType().Assembly.GetType("ScumMiniMap.Localization").GetProperty("Current");var savedLanguage=currentLanguage.GetValue(null,null);
        try {
            foreach(var language in Enum.GetValues(currentLanguage.PropertyType)) {
                currentLanguage.SetValue(null,language,null);search=(Window)Call(runtime,"BuildSearch");
                try {
                    search.ShowActivated=false;search.Left=-32000;search.Top=-32000;search.Show();Pump();search.UpdateLayout();
                    var buttons=Children(search).OfType<Button>().Where(b=>b.Tag as string=="search-clear-route"||b.Tag as string=="search-navigate"||b.Tag as string=="search-add-stop").ToArray();
                    var bounds=buttons.Select(b=>b.TransformToAncestor(search).TransformBounds(new Rect(b.RenderSize))).ToArray();
                    Check(bounds.Length==3&&bounds.All(r=>r.Left>=0&&r.Right<=search.ActualWidth&&r.Top>=0&&r.Bottom<=search.ActualHeight)&&!Overlap(bounds),"Search actions fit without overlap: "+language);
                    var direct=(string)runtime.GetType().Assembly.GetType("ScumMiniMap.Localization").GetMethod("Get").Invoke(null,new object[]{"FieldDirect"});
                    Check(Children(search).OfType<ListBoxItem>().Any(item=>System.Windows.Automation.AutomationProperties.GetName(item).Contains(direct+": ")),"Search distances explicitly identify direct distance: "+language);
                    if(language.ToString()=="Arabic"||language.ToString()=="German")Capture(search,Path.Combine(folder,"search-"+language+".png"));
                }finally {search.Close();}
                var saveWindow=(Window)Call(runtime,"BuildAddWaypoint",new System.Drawing.PointF(.41f,.42f));
                try {
                    saveWindow.ShowActivated=false;saveWindow.Left=-32000;saveWindow.Top=-32000;saveWindow.Show();Pump();saveWindow.UpdateLayout();
                    var saveButtons=Children(saveWindow).OfType<Button>().ToArray();var saveBounds=saveButtons.Select(b=>b.TransformToAncestor(saveWindow).TransformBounds(new Rect(b.RenderSize))).ToArray();
                    Check(saveBounds.Length==2&&saveBounds.All(r=>r.Left>=0&&r.Right<=saveWindow.ActualWidth&&r.Top>=0&&r.Bottom<=saveWindow.ActualHeight)&&!saveBounds[0].IntersectsWith(saveBounds[1]),"Save location actions fit without overlap: "+language);
                }finally {saveWindow.Close();}
                var squadWindow=(Window)Call(runtime,"BuildSquadTools",2);
                try {
                    squadWindow.ShowActivated=false;squadWindow.Left=-32000;squadWindow.Top=-32000;squadWindow.Width=420;squadWindow.Show();Pump();squadWindow.UpdateLayout();
                    var tabs=Children(squadWindow).OfType<Button>().Where(b=>(b.Tag as string??"").StartsWith("squad-tab-")).ToArray();
                    var tabBounds=tabs.Select(b=>b.TransformToAncestor(squadWindow).TransformBounds(new Rect(b.RenderSize))).ToArray();
                    Check(tabs.Length==3&&tabBounds.All(r=>r.Left>=0&&r.Right<=squadWindow.ActualWidth)&&!Overlap(tabBounds),"Linked squad/navigation tabs fit the minimum window width: "+language);
                    if(language.ToString()=="Arabic"||language.ToString()=="German")Capture(squadWindow,Path.Combine(folder,"squad-navigation-"+language+".png"));
                }finally {squadWindow.Close();}
                var autoSettings=(Window)Call(runtime,"BuildSettings",0);
                try {
                    autoSettings.ShowActivated=false;autoSettings.Left=-32000;autoSettings.Top=-32000;autoSettings.Width=420;autoSettings.Show();Pump();autoSettings.UpdateLayout();
                    var auto=Children(autoSettings).OfType<CheckBox>().Single(c=>c.Tag as string=="auto-launch-scum");
                    var label=(TextBlock)auto.Content;
                    var translated=(string)runtime.GetType().Assembly.GetType("ScumMiniMap.Localization").GetMethod("Get").Invoke(null,new object[]{"ScumAutoLaunchLabel"});
                    Check(label.Text==translated&&!auto.IsEnabled,"Automatic launch uses the selected language and remains safe in diagnostics: "+language);
                    var labelBounds=label.TransformToAncestor(autoSettings).TransformBounds(new Rect(label.RenderSize));
                    Check(labelBounds.Left>=0&&labelBounds.Right<=autoSettings.ActualWidth,"Automatic launch label fits narrow settings: "+language);
                    if(language.ToString()=="Arabic"||language.ToString()=="German")Capture(autoSettings,Path.Combine(folder,"auto-launch-"+language+".png"));
                }finally {autoSettings.Close();}
            }
        }finally {currentLanguage.SetValue(null,savedLanguage,null);}
    }
    static void CheckTacticalLayers(object runtime,string folder) {
        var assembly=runtime.GetType().Assembly;var engine=Get(runtime,"engine");var map=(FrameworkElement)Get(runtime,"tacticalMap");var window=(Window)Get(runtime,"tactical");
        window.ShowActivated=false;window.Left=-32000;window.Top=-32000;window.Width=1280;window.Height=760;window.Show();window.UpdateLayout();Call(engine,"WpfPan",new System.Drawing.PointF(.5f,.5f));Set(engine,"fullMapZoom",4f);Call(runtime,"Refresh");
        Func<byte[]> render=()=> {var bitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(map);var bytes=new byte[(int)map.ActualWidth*(int)map.ActualHeight*4];bitmap.CopyPixels(bytes,(int)map.ActualWidth*4,0);return bytes;};
        render();Await(()=> {render();return (bool)Get(map,"PoiViewportReady");});for(int i=0;i<12;i++)Pump();render();
        var state=Get(map,"State");int terrain=(int)Get(map,"TerrainLayerBuilds"),annotations=(int)Get(map,"AnnotationLayerBuilds"),live=(int)Get(map,"LiveLayerBuilds");var pixels=render();
        var target=Activator.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"));Set(target,"Name","Synthetic live GPS");Set(target,"Points",new[]{new System.Drawing.PointF(.52f,.52f)});Set(state,"Target",target);Call(map,"PublishState",state);var updated=render();
        Check(!pixels.SequenceEqual(updated),"A live GPS change updates the actual tactical pixels");
        Check((int)Get(map,"TerrainLayerBuilds")==terrain&&(int)Get(map,"AnnotationLayerBuilds")==annotations&&(int)Get(map,"LiveLayerBuilds")>live,"Live GPS updates redraw their overlay without rebuilding terrain or annotations");
        var position=Get(state,"Position");var copy=Activator.CreateInstance(position.GetType());foreach(string field in new[]{"X","Y","Z","Yaw"})Set(copy,field,Get(position,field));Set(copy,"Yaw",45d);Set(state,"Position",copy);
        for(int i=0;i<60;i++) {Set(copy,"Yaw",(double)(i*6));Call(map,"PublishState",state);}
        Check((int)Get(map,"TerrainLayerBuilds")==terrain&&(int)Get(map,"AnnotationLayerBuilds")==annotations,"Sixty player-heading updates retain the terrain and annotation visuals");
        var reference=new DrawingVisual();using(var dc=reference.RenderOpen())Call(map,"DrawMap",dc);var referenceBitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);referenceBitmap.Render(reference);var referencePixels=new byte[updated.Length];referenceBitmap.CopyPixels(referencePixels,(int)map.ActualWidth*4,0);
        // Separate alpha surfaces can round a channel by one 8-bit step during composition.
        Check(render().Zip(referencePixels,(a,b)=>Math.Abs(a-b)).All(delta=>delta<=1),"Separated tactical layers match the combined renderer within one alpha-composition rounding step");
        Set(state,"History",true);Set(state,"Trail",new[]{new System.Drawing.PointF(.49f,.49f),new System.Drawing.PointF(.51f,.51f)});Call(map,"PublishState",state);render();Check((int)Get(map,"TerrainLayerBuilds")==terrain&&(int)Get(map,"AnnotationLayerBuilds")==annotations,"A changed history trail keeps the expensive static layers");
        Set(map,"TerrainRevision",(int)Get(map,"TerrainRevision")+1);Call(map,"PublishState",state);render();Check((int)Get(map,"TerrainLayerBuilds")>terrain&&(int)Get(map,"AnnotationLayerBuilds")==annotations,"A decoded terrain tile invalidates terrain without rebuilding annotation visuals");
        annotations=(int)Get(map,"AnnotationLayerBuilds");Set(state,"Labels",!(bool)Get(state,"Labels"));Call(map,"PublishState",state);render();Check((int)Get(map,"AnnotationLayerBuilds")>annotations,"A label visibility change refreshes the annotation layer immediately");
        Check(VisualTreeHelper.GetChildrenCount(map)==5,"The tactical map retains independent terrain, history, annotation, shared and live visual layers");
        bool opaque=Environment.GetEnvironmentVariable("SCUM_MINIMAP_TAC_OPAQUE")=="1"||Environment.GetCommandLineArgs().Contains("--fast-tac-test");Check(window.AllowsTransparency!=opaque,"The tactical window uses the requested transparent/solid test mode");Check(((Window)Get(runtime,"hud")).AllowsTransparency,"The compact HUD stays transparent in either tactical mode");
        if(opaque&&(bool)Get(map,"DirectTerrain")) {int frames=(int)Get(map,"CameraSceneBuilds");((Action<System.Drawing.PointF>)Get(map,"PanChanged"))(new System.Drawing.PointF(.51f,.5f));render();for(int i=0;i<5;i++)Pump();Check((int)Get(map,"CameraSceneBuilds")==frames&&!(bool)Get(map,"cameraWorker")&&Get(map,"pendingCameraScene")==null,"Solid accelerated tactical movement bypasses the software terrain-frame worker");Call(runtime,"EndCameraInteraction");render();}
        Capture(window,Path.Combine(folder,opaque?"tactical-solid.png":"tactical-transparent.png"));window.Hide();
    }
    static void CheckCameraBurst(object runtime,string folder,bool verify) {
        var engine=Get(runtime,"engine");var map=(FrameworkElement)Get(runtime,"tacticalMap");var window=(Window)Get(runtime,"tactical");
        window.ShowActivated=false;window.Left=-32000;window.Top=-32000;window.Width=1920;window.Height=1080;window.Show();window.UpdateLayout();
        Call(engine,"WpfPoiClustering",true);Call(engine,"WpfLayer","labels",true);Set(engine,"fullMapZoom",5f);Call(runtime,"Refresh");Call(runtime,"FilterAll",true);
        Action record=()=> {var group=new DrawingGroup();using(var dc=group.Open())Call(map,"OnRender",dc);};
        record();Await(()=> {record();return Get(map,"requestedPoiLayout")==null||Get(map,"drawnPoiLayout")!=null&&Object.ReferenceEquals(Get(map,"drawnPoiLayout"),Get(map,"requestedPoiLayout"));});
        if(verify) {Await(()=> {record();return Get(map,"idleCameraScene")!=null;});Check(true,"A completed stationary map prepares a worker frame before the first camera input");}
        var layout=Get(map,"requestedPoiLayout");var labels=Get(map,"labelLayout");int sceneBuilds=(int)Get(map,"PoiSceneBuilds");var elapsed=new List<double>();int layoutsChanged=0,labelLayoutsChanged=0;
        int terrainBuilds=(int)Get(map,"TerrainLayerBuilds"),annotationBuilds=(int)Get(map,"AnnotationLayerBuilds");var snapshot=Get(map,"staticDrawing");var snapshotKey=Get(map,"queuedTerrainKey");
        var pan=(Action<System.Drawing.PointF>)Get(map,"PanChanged");var zoom=(Action<int>)Get(map,"ZoomRequested");var cpu=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<48;i++) {
            var clock=System.Diagnostics.Stopwatch.StartNew();pan(new System.Drawing.PointF(.4f+i*.0004f,.45f));zoom(i%2==0?1:-1);record();clock.Stop();elapsed.Add(clock.Elapsed.TotalMilliseconds);
            if(!Object.ReferenceEquals(layout,Get(map,"requestedPoiLayout")))layoutsChanged++;if(!Object.ReferenceEquals(labels,Get(map,"labelLayout")))labelLayoutsChanged++;
        }
        cpu.Stop();elapsed.Sort();Console.WriteLine("BURST clustered 1920x1080: median="+elapsed[24].ToString("F3")+" ms, p95="+elapsed[45].ToString("F3")+" ms, total="+cpu.Elapsed.TotalMilliseconds.ToString("F1")+" ms, POI layout changes="+layoutsChanged+", label layout changes="+labelLayoutsChanged);
        File.WriteAllText(Path.Combine(folder,"camera-burst.csv"),"median_ms,p95_ms,total_ms,poi_layout_changes,label_layout_changes\n"+string.Join(",",new[]{elapsed[24],elapsed[45],cpu.Elapsed.TotalMilliseconds,(double)layoutsChanged,(double)labelLayoutsChanged}.Select(n=>n.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))));
        if(!verify)return;
        Check(layoutsChanged==0&&labelLayoutsChanged==0&&(int)Get(map,"PoiSceneBuilds")==sceneBuilds,"A rapid zoom/pan burst reprojects existing POIs and labels without rebuilding either layout");
        Check((int)Get(map,"TerrainLayerBuilds")==terrainBuilds&&(int)Get(map,"AnnotationLayerBuilds")==annotationBuilds,"A prepared transparent gesture rebuilds neither terrain nor annotation drawings");
        Check(Object.ReferenceEquals(snapshot,Get(map,"staticDrawing"))&&Object.Equals(snapshotKey,Get(map,"queuedTerrainKey")),"A prepared transparent gesture queues no replacement raster snapshots");
        Set(map,"TerrainRevision",(int)Get(map,"TerrainRevision")+1);record();Check((int)Get(map,"TerrainLayerBuilds")==terrainBuilds,"A terrain completion during movement defers its drawing rebuild until idle");
        Check(elapsed[45]<100,"Warm clustered camera recording stays under 100 ms at p95 on this test machine");
        Check((bool)Get(map,"CameraInteracting")&&((DispatcherTimer)Get(map,"cameraFrame")).IsEnabled,"Camera input keeps one coalesced redraw timer pending");
        Check(Get(map,"cameraScene")!=null,"The first camera burst immediately reuses the stationary frame");
        var timer=(DispatcherTimer)Get(map,"cameraFrame");for(int i=0;i<100;i++)pan(new System.Drawing.PointF(.5f+i*.0001f,.5f));
        Check(Object.ReferenceEquals(timer,Get(map,"cameraFrame"))&&Math.Abs(((System.Drawing.PointF)Get(Get(map,"State"),"Pan")).X-.5099)<.00001,"A hundred pending pan requests share one frame and retain the latest camera position");
        Call(map,"UpdateHover",new Point(500,400));Check(Get(map,"hoverText")==null,"Camera movement skips expensive hover searches and hides stale tooltips");zoom(1);
        Await(()=> {pan(new System.Drawing.PointF(.5099f,.5f));record();return (int)Get(map,"CameraSceneBuilds")>0;});
        var scene=Get(map,"cameraScene");var image=(BitmapSource)Get(scene,"Image");Check(image.IsFrozen&&Math.Max(image.PixelWidth,image.PixelHeight)<=1600,"Camera composition uses a frozen worker-rendered frame capped at 1600 pixels");
        Capture(window,Path.Combine(folder,"camera-moving.png"));
        var cameraState=Get(map,"State");double cameraSide=(double)map.GetType().GetMethod("MapSide",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{cameraState,map.ActualWidth,map.ActualHeight,false});var cameraCentre=(System.Drawing.PointF)map.GetType().GetMethod("CameraCentre",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{cameraState,map.ActualWidth,map.ActualHeight,false});
        var syntheticVisual=new DrawingVisual();using(var dc=syntheticVisual.RenderOpen()) {dc.DrawRectangle(Brushes.Red,null,new Rect(0,0,100,100));dc.DrawRectangle(Brushes.Blue,null,new Rect(45,45,10,10));}var syntheticImage=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32);syntheticImage.Render(syntheticVisual);syntheticImage.Freeze();var syntheticScene=Activator.CreateInstance(scene.GetType(),true);Set(syntheticScene,"Image",syntheticImage);Set(syntheticScene,"Width",map.ActualWidth);Set(syntheticScene,"Height",map.ActualHeight);Set(syntheticScene,"Side",cameraSide);Set(syntheticScene,"Centre",new System.Drawing.PointF(cameraCentre.X+.03f,cameraCentre.Y));Set(syntheticScene,"Opacity",(int)Get(cameraState,"Opacity"));Set(map,"cameraScene",syntheticScene);
        var projectedVisual=new DrawingVisual();using(var dc=projectedVisual.RenderOpen())Call(map,"DrawCameraScene",dc,new DrawingGroup(),map.ActualWidth,map.ActualHeight);var projectedImage=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);projectedImage.Render(projectedVisual);var projectedPixels=new byte[(int)map.ActualWidth*(int)map.ActualHeight*4];projectedImage.CopyPixels(projectedPixels,(int)map.ActualWidth*4,0);int projectedIndex=((int)(map.ActualHeight/2)*(int)map.ActualWidth+(int)(map.ActualWidth/2+.03*cameraSide))*4;Check(projectedPixels[projectedIndex]>200&&projectedPixels[projectedIndex+2]<20,"A pending worker frame is projected at the current camera position rather than its captured position");Set(map,"cameraScene",scene);
        Func<byte[]> previewPixels=()=> {record();var bitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(map);var pixels=new byte[(int)map.ActualWidth*(int)map.ActualHeight*4];bitmap.CopyPixels(pixels,(int)map.ActualWidth*4,0);return pixels;};
        var previousTarget=Get(cameraState,"Target");var beforeNavigation=previewPixels();var liveTarget=Activator.CreateInstance(runtime.GetType().Assembly.GetType("ScumMiniMap.MapZone"));Set(liveTarget,"Name","Synthetic moving GPS");Set(liveTarget,"Points",new[]{new System.Drawing.PointF(cameraCentre.X+.01f,cameraCentre.Y)});Set(cameraState,"Target",liveTarget);Call(map,"PublishState",cameraState);
        Check(!beforeNavigation.SequenceEqual(previewPixels())&&Object.ReferenceEquals(snapshot,Get(map,"staticDrawing")),"A new GPS destination changes actual preview pixels without replacing the static snapshot");Set(cameraState,"Target",previousTarget);Call(map,"PublishState",cameraState);
        var frameTimes=new List<double>();for(int i=0;i<12;i++) {pan(new System.Drawing.PointF(.5099f+i*.0001f,.5f));var frameClock=System.Diagnostics.Stopwatch.StartNew();var bitmap=new RenderTargetBitmap((int)map.ActualWidth,(int)map.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(map);frameClock.Stop();frameTimes.Add(frameClock.Elapsed.TotalMilliseconds);Pump();}frameTimes.Sort();Console.WriteLine("BURST worker-frame composition median="+frameTimes[6].ToString("F3")+" ms, p95="+frameTimes[11].ToString("F3")+" ms");
        bool frameDuringInput=false;var inputClock=System.Diagnostics.Stopwatch.StartNew();var inputFrame=new DispatcherFrame();int inputEvents=0;
        EventHandler observedFrame=(s,e)=>frameDuringInput=inputFrame.Continue;timer.Tick+=observedFrame;
        Action input=null;input=()=> {inputEvents++;if(inputClock.ElapsedMilliseconds>=250)inputFrame.Continue=false;else map.Dispatcher.BeginInvoke(input,DispatcherPriority.Input);};
        try {pan(new System.Drawing.PointF(.51f,.5f));map.Dispatcher.BeginInvoke(input,DispatcherPriority.Input);Dispatcher.PushFrame(inputFrame);}finally {timer.Tick-=observedFrame;}
        Check(frameDuringInput&&inputEvents>0,"A pending camera frame is serviced while synthetic input work is still queued");
        Call(map,"BeginPointer",new Point(500,400));Call(map,"MovePointer",new Point(540,425));var held=System.Diagnostics.Stopwatch.StartNew();while(held.ElapsedMilliseconds<450)Pump();
        Check((bool)Get(map,"CameraInteracting")&&(bool)map.GetType().GetProperty("PointerCameraActive",F).GetValue(map,null),"Holding a drag open keeps the lightweight preview active across settle ticks");Call(map,"CancelPointer");
        Await(()=>!(bool)Get(map,"CameraInteracting"));record();Await(()=> {record();return Object.ReferenceEquals(Get(map,"drawnPoiLayout"),Get(map,"requestedPoiLayout"));});
        Check(!Object.ReferenceEquals(labels,Get(map,"labelLayout"))&&!Object.ReferenceEquals(layout,Get(map,"requestedPoiLayout")),"Camera idle restores current-zoom clustering and a fresh label layout");
        Check(RenderOptions.GetBitmapScalingMode(map)==BitmapScalingMode.Unspecified&&!timer.IsEnabled,"Camera idle restores smooth sampling and stops pending gesture frames");
        var pending=Get(map,"pendingCameraScene");Check(Get(map,"cameraScene")==null&&(pending==null||(int)Get(pending,"Generation")== (int)Get(map,"cameraGeneration")),"Ending a gesture discards its image and rejects obsolete queued snapshots while allowing idle preparation");Await(()=>!(bool)Get(map,"cameraWorker")&&!(bool)Get(map,"cameraApplyPosted"));Check(Get(map,"cameraScene")==null,"An old worker completion cannot restore a gesture frame after idle");
        var store=Get(Get(map,"State"),"Store");var categories=(System.Collections.IList)Get(store,"Categories");int category=(int)Get(categories[0],"Id");pan(new System.Drawing.PointF(.51f,.51f));Call(runtime,"Category",category,false);
        Check(!(bool)Get(map,"CameraInteracting"),"A POI filter action exits retained gesture mode immediately");Call(runtime,"Category",category,true);
        window.Show();window.UpdateLayout();Await(()=>map.IsVisible);
        Call(map,"BeginPointer",new Point(500,400));Call(map,"MovePointer",new Point(540,425));window.Hide();
        // Descendant IsVisibleChanged follows the window's visibility update.
        Await(()=>!(bool)map.GetType().GetProperty("PointerCameraActive",F).GetValue(map,null));
        Check(!(bool)map.GetType().GetProperty("PointerCameraActive",F).GetValue(map,null),"Hiding the map cancels a held drag so reopening cannot retain a stuck preview");
    }
    static void CheckCompactGuidance(object runtime,string folder) {
        var assembly=runtime.GetType().Assembly;var surfaceType=assembly.GetType("ScumMiniMap.WpfMapSurface");var stateType=assembly.GetType("ScumMiniMap.WpfMapState");var markerType=assembly.GetType("ScumMiniMap.SharedMapMarker");
        var position=Get(Call(Get(runtime,"engine"),"WpfSnapshot",false),"Position");var centre=(System.Drawing.PointF)assembly.GetType("ScumMiniMap.MapWindow").GetMethod("ToMap",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new[]{position});
        var squad=Activator.CreateInstance(assembly.GetType("ScumMiniMap.SquadState"),F,null,new object[]{null},null);Call(squad,"AddStop","Short stop",(double)centre.X+.02,(double)centre.Y);var stop=((Array)Call(squad,"Local")).GetValue(0);
        Func<object,bool,byte[]> render=(state,compact)=> {var map=(FrameworkElement)Activator.CreateInstance(surfaceType,true);Set(map,"State",state);Set(map,"Compact",compact);map.Measure(new Size(300,300));map.Arrange(new Rect(0,0,300,300));var bitmap=new RenderTargetBitmap(300,300,96,96,PixelFormats.Pbgra32);bitmap.Render(map);var pixels=new byte[300*300*4];bitmap.CopyPixels(pixels,300*4,0);return pixels;};
        foreach(string shape in new[]{"Circle","Square"})foreach(bool outside in new[]{false,true}) {
            var state=Activator.CreateInstance(stateType,true);Set(state,"Position",position);Set(state,"Pan",centre);Set(state,"Zoom",8f);Set(state,"Shape",shape);Set(state,"RouteColor",System.Drawing.Color.Orange.ToArgb());Set(state,"Saved",Array.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"),0));Set(state,"Trail",new System.Drawing.PointF[0]);
            var markers=Array.CreateInstance(markerType,3);for(int i=0;i<2;i++) {var marker=Activator.CreateInstance(markerType);Set(marker,"Id",markerType.GetField("GpsId",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null));Set(marker,"Owner","Player "+i);Set(marker,"OwnerId","fixture-"+i);Set(marker,"Name","Short GPS");Set(marker,"X",(double)centre.X+(outside?.7:.02));Set(marker,"Y",(double)centre.Y+i*.003);markers.SetValue(marker,i);}Set(stop,"X",(double)centre.X+(outside?.7:.02));markers.SetValue(stop,2);Set(state,"SharedMarkers",markers);
            var before=render(state,true);var tacticalBefore=render(state,false);
            foreach(var marker in markers) {Set(marker,"Owner","Two synthetic teammates with a very long squad name");Set(marker,"Name","Fixture crew 2.0 · B3 Water dispensers · a destination long enough to wrap across the minimap");}
            var after=render(state,true);Check(before.SequenceEqual(after),"Compact "+shape+" guidance ignores long repeated GPS/route names: "+(outside?"edge arrows":"visible markers"));
            Set(state,"SharedMarkers",Array.CreateInstance(markerType,0));Check(!after.SequenceEqual(render(state,true)),"Compact "+shape+" retains GPS/route "+(outside?"direction arrows":"symbols"));
            if(!outside) {Set(state,"SharedMarkers",markers);Check(!tacticalBefore.SequenceEqual(render(state,false)),"Tactical "+shape+" retains full GPS and route names");}
            var target=Activator.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"));Set(target,"Name","Short target");Set(target,"Points",new[]{new System.Drawing.PointF(centre.X+.02f,centre.Y)});Set(state,"SharedMarkers",Array.CreateInstance(markerType,0));Set(state,"Target",target);before=render(state,true);tacticalBefore=render(state,false);Set(target,"Name","A very long selected route destination that must stay out of the compact map");Check(before.SequenceEqual(render(state,true)),"Compact "+shape+" selected destination keeps its ring without a text box");Check(!tacticalBefore.SequenceEqual(render(state,false)),"Tactical "+shape+" selected destination retains its name");Set(state,"Target",null);Check(!before.SequenceEqual(render(state,true)),"Compact "+shape+" selected destination ring remains visible");
        }
        var edgeSurface=Activator.CreateInstance(surfaceType,true);var edgeDrawing=new DrawingVisual();using(var dc=edgeDrawing.RenderOpen())Call(edgeSurface,"DrawEdgeArrow",dc,300d,300d,new Point(1000,150),Brushes.Cyan,"An extremely long teammate name that would otherwise wrap into several rows");var edgeBitmap=new RenderTargetBitmap(300,300,96,96,PixelFormats.Pbgra32);edgeBitmap.Render(edgeDrawing);var edgePixels=new byte[300*300*4];edgeBitmap.CopyPixels(edgePixels,300*4,0);var occupiedRows=Enumerable.Range(0,300).Where(y=>Enumerable.Range(0,300).Any(x=>edgePixels[(y*300+x)*4+3]>0)).ToArray();Check(occupiedRows.Length>0&&occupiedRows.Last()-occupiedRows.First()<24,"Long teammate edge names stay within one line beside the direction arrow");
    }
    [STAThread]static void Main(string[] args) {
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));string folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"settings.ini"),"Welcomed=True\nLanguage=en\nWpfDefaultsRevision=1\n");
        var type=asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime");object runtime=Activator.CreateInstance(type,F,null,new object[]{folder,true},null);type.GetField("Active",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,runtime);
        if(Environment.GetEnvironmentVariable("MINIMAP_TEST_TILE_TRACE")=="1") { var trace=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };trace.Tick+=(s,e)=> { var tiles=Get(runtime,"terrain");Console.WriteLine("TRACE tiles cache="+((System.Collections.IDictionary)Get(tiles,"cache")).Count+" pending="+Get(tiles,"pendingTiles").GetType().GetProperty("Count").GetValue(Get(tiles,"pendingTiles"),null)+" queued="+((System.Collections.ICollection)Get(tiles,"tileQueue")).Count+" levels="+string.Join(",",((System.Collections.IEnumerable)Get(tiles,"levels")).Cast<object>().Select(l=>Get(l,"Width"))));foreach(string n in new[]{"tacticalMap","hudMap"}) {var map=Get(runtime,n);Console.WriteLine("TRACE "+n+" size="+((FrameworkElement)map).ActualWidth+"x"+((FrameworkElement)map).ActualHeight+" zoom="+Get(Get(map,"State"),"Zoom"));} };trace.Start(); }
        IDisposable host=null,friend=null;
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");
            if(Environment.GetEnvironmentVariable("MINIMAP_CAMERA_LABELS_ONLY")=="1") {CheckViewportPixels(runtime,folder);return;}
            if(Environment.GetEnvironmentVariable("MINIMAP_CAMERA_PROBE_ONLY")=="1") {CheckCameraInteraction(runtime,folder,Environment.GetEnvironmentVariable("MINIMAP_CAMERA_BASELINE")!="1");return;}
            if(Environment.GetEnvironmentVariable("MINIMAP_COMPACT_GUIDANCE_ONLY")=="1") {CheckCompactGuidance(runtime,folder);return;}
            if(Environment.GetEnvironmentVariable("MINIMAP_CAMERA_BURST_ONLY")=="1") {CheckCameraBurst(runtime,folder,Environment.GetEnvironmentVariable("MINIMAP_CAMERA_BASELINE")!="1");return;}
            if(Environment.GetEnvironmentVariable("MINIMAP_TAC_LAYERS_ONLY")=="1") {CheckTacticalLayers(runtime,folder);return;}
            CheckRouteAppearance(runtime,folder);
            CheckCompactGuidance(runtime,folder);
            CheckSaveLocation(runtime,folder);
            CheckUsability(runtime,folder);
            CheckSquadIntegration(runtime,folder);
            var needsWizard=engine.GetType().GetProperty("WpfNeedsOnboarding",F);
            Check((bool)needsWizard.GetValue(engine,null),"Existing profiles without a completed-version marker require the update wizard");
            Set(engine,"wpfOnboardingVersion","0.0.1");
            Check(Path.GetFullPath((string)Get(engine,"settingsPath")).StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Wizard persistence fixture writes only its isolated test profile");
            Set(engine,"diagnosticMode",false);try { Call(engine,"WpfSave"); }finally { Set(engine,"diagnosticMode",true); }
            Check((bool)needsWizard.GetValue(engine,null)&&File.ReadAllText(Path.Combine(folder,"settings.ini")).Contains("OnboardingVersion=0.0.1"),"Ordinary settings saves do not mark an updated wizard completed");
            var incompleteWizard=(Window)Call(runtime,"BuildOnboarding",0);incompleteWizard.Close();
            Check((bool)needsWizard.GetValue(engine,null),"Closing an incomplete wizard leaves it required at the next launch");
            int savedCopy=(int)Get(engine,"scumCopyKey");
            Set(engine,"diagnosticMode",false);try { Call(engine,"WpfCompleteOnboarding"); }finally { Set(engine,"diagnosticMode",true); }
            string completedVersion=(string)Get(engine,"wpfOnboardingVersion");
            Check(!(bool)needsWizard.GetValue(engine,null)&&File.ReadAllText(Path.Combine(folder,"settings.ini")).Contains("OnboardingVersion="+completedVersion)&&(int)Get(engine,"scumCopyKey")==savedCopy,"Completing the wizard persists this version without resetting the copy binding");
            Set(engine,"wpfOnboardingVersion","");Call(engine,"LoadSettings");
            Check(!(bool)needsWizard.GetValue(engine,null)&&(string)Get(engine,"wpfOnboardingVersion")==completedVersion,"Reloading the completed version does not repeat setup on ordinary restarts");
            Set(engine,"wpfOnboardingVersion","0.0.1");Check((bool)needsWizard.GetValue(engine,null),"A different app version requires onboarding again");Set(engine,"wpfOnboardingVersion",completedVersion);
            Set(engine,"wpfOnboardingVersion","0.0.1");
            Set(engine,"diagnosticMode",false);try {Call(engine,"WpfSetHideOnboardingGuide",true);}finally {Set(engine,"diagnosticMode",true);}
            Check((bool)needsWizard.GetValue(engine,null)&&File.ReadAllText(Path.Combine(folder,"settings.ini")).Contains("HideOnboardingGuide=True"),"Legacy don't-show-again cannot suppress required setup after an update");
            Set(engine,"hideOnboardingGuide",false);Call(engine,"LoadSettings");Check((bool)Get(engine,"hideOnboardingGuide")&&(bool)needsWizard.GetValue(engine,null),"Legacy opt-out remains ignored after reloading an updated profile");
            Set(engine,"wpfOnboardingVersion","");Check((bool)needsWizard.GetValue(engine,null),"An unfinished first configuration cannot be skipped with the update-guide opt-out");
            Set(engine,"diagnosticMode",false);try {Call(engine,"WpfSave");}finally {Set(engine,"diagnosticMode",true);}Call(engine,"LoadSettings");Check((bool)needsWizard.GetValue(engine,null)&&File.ReadAllText(Path.Combine(folder,"settings.ini")).Contains("OnboardingVersion="),"Saving preferences before completing first setup still requires configuration after restart");
            Set(engine,"wpfOnboardingVersion","0.0.1");Set(engine,"isFirstLaunch",true);Check((bool)needsWizard.GetValue(engine,null),"First install requires full configuration even when automatic update guidance is disabled");Set(engine,"isFirstLaunch",false);
            Set(engine,"diagnosticMode",false);try {Call(engine,"WpfSetHideOnboardingGuide",false);}finally {Set(engine,"diagnosticMode",true);}Check((bool)needsWizard.GetValue(engine,null),"Turning automatic guidance back on restores the pending update prompt");Set(engine,"wpfOnboardingVersion",completedVersion);
            var tutorial=(Window)Call(runtime,"BuildFirstTimeGuide");
            try {
                Action<int,bool> mark=(index,value)=> {var box=Children(tutorial).OfType<CheckBox>().Single(c=>c.Tag as string=="guide-read-"+index);box.IsChecked=value;box.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
                Func<Button> done=()=>Children(tutorial).OfType<Button>().Single(b=>b.Tag as string=="guide-done");
                Check(Children(tutorial).OfType<CheckBox>().Count(c=>(c.Tag as string??"").StartsWith("guide-read-"))==6&&!done().IsEnabled,"Getting-started guide has six readable acknowledgements and waits for them");
                Check(!Children(tutorial).OfType<Button>().Any(b=>b.Tag as string=="guide-capture-copy"||b.Tag as string=="guide-use-numpad"),"Default number-pad users have no rebinding action to perform");
                Check(Children(tutorial).OfType<TextBlock>().Any(t=>t.Text.Contains("do not need to change"))&&Children(tutorial).OfType<TextBlock>().Where(t=>t.Text.Contains("Copy Location")).All(t=>t.TextWrapping==TextWrapping.Wrap||t.Tag as string=="guide-current-key"),"Number-pad instructions explicitly say to leave MiniMap's default key alone");
                for(int i=0;i<6;i++)mark(i,true);Check(done().IsEnabled,"Reading every line enables Done");mark(0,false);Check(!done().IsEnabled,"Unchecking a line disables completion again");mark(0,true);
                tutorial.ShowActivated=false;tutorial.WindowStartupLocation=WindowStartupLocation.Manual;tutorial.Left=-32000;tutorial.Top=-32000;tutorial.Show();tutorial.UpdateLayout();Capture(tutorial,Path.Combine(folder,"guide-numpad.png"));
                int beforeKey=(int)Get(engine,"scumCopyKey");
                Children(tutorial).OfType<RadioButton>().Single(b=>b.Tag as string=="guide-no-numpad").IsChecked=true;
                Check(!done().IsEnabled&&(int)Get(engine,"scumCopyKey")==beforeKey,"Selecting no number pad resets key acknowledgements without silently changing bindings");
                var captureCopy=Children(tutorial).OfType<Button>().Single(b=>b.Tag as string=="guide-capture-copy");Check(!captureCopy.IsEnabled,"No-number-pad path asks users to set SCUM first");mark(2,true);
                captureCopy=Children(tutorial).OfType<Button>().Single(b=>b.Tag as string=="guide-capture-copy");Check(captureCopy.IsEnabled,"Acknowledging SCUM's key enables the in-guide MiniMap capture button");
                var captureTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};bool captured=false;captureTimer.Tick+=(sender,e)=> {captureTimer.Stop();var captureWindow=Application.Current.Windows.OfType<Window>().Single(w=>w!=tutorial&&w.Title=="Copy Coordinate Key");Call(engine,"WpfGameBind",2,0x76,0);captured=true;captureWindow.Close();};captureTimer.Start();
                captureCopy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(captured&&Children(tutorial).OfType<TextBlock>().Any(t=>t.Tag as string=="guide-current-key"&&t.Text.Contains("F7")),"Actual guide capture button opens key capture and refreshes the chosen MiniMap key");
                for(int i=0;i<6;i++)mark(i,true);Check(done().IsEnabled,"No-number-pad path completes after matching MiniMap and acknowledging each line");
                Check(!Children(tutorial).OfType<CheckBox>().Any(c=>c.Tag as string=="guide-dont-show"),"Required setup offers no don't-show-again bypass");
                tutorial.UpdateLayout();Capture(tutorial,Path.Combine(folder,"guide-no-numpad.png"));Call(engine,"WpfGameBind",2,beforeKey,0);
            }finally { tutorial.Close(); }
            var tutorialWizard=(Window)Call(runtime,"BuildOnboarding",0);try { Check(Children(tutorialWizard).OfType<CheckBox>().Count(c=>(c.Tag as string??"").StartsWith("guide-read-"))==6,"Detailed first-time setup uses the same accessible checklist"); }finally { tutorialWizard.Close(); }
            Set(engine,"wpfOnboardingVersion","");Set(engine,"isFirstLaunch",true);
            var guideSettings=(Window)Call(runtime,"BuildSettings",0);guideSettings.ContentRendered+=(s,e)=>Children(guideSettings).OfType<Button>().Single(b=>b.Tag as string=="first-time-guide").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Call(runtime,"Modal",guideSettings);Pump();var switchingGuide=(Window)Get(runtime,"setupWindow");
            Check(!guideSettings.IsVisible&&switchingGuide!=null&&switchingGuide.IsEnabled,"Actual Settings guide button closes its modal parent before showing interactive setup");
            Check(switchingGuide.IsVisible&&!switchingGuide.Topmost&&switchingGuide.Owner==null&&switchingGuide.ShowInTaskbar&&switchingGuide.WindowStyle==WindowStyle.SingleBorderWindow,"Setup opens modeless with normal taskbar/minimize controls and no topmost map owner");
            var rememberedTick=Children(switchingGuide).OfType<CheckBox>().Single(c=>c.Tag as string=="guide-read-0");rememberedTick.IsChecked=true;rememberedTick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            double movedGuideLeft=switchingGuide.Left+40,movedGuideTop=switchingGuide.Top+30;switchingGuide.Left=movedGuideLeft;switchingGuide.Top=movedGuideTop;switchingGuide.WindowState=WindowState.Minimized;
            Call(runtime,"SetSetupInputFocus",true);Check((bool)Get(engine,"panelOpening"),"Focused setup protects its own controls from game shortcuts");
            Call(runtime,"SetSetupInputFocus",false);Check(!(bool)Get(engine,"panelOpening")&&!(bool)Get(engine,"shortcutCaptureOpen")&&switchingGuide.IsVisible,"Switching away leaves setup open and releases game input and coordinate tracking");
            Call(runtime,"ShowSetupWindow",false);Check(Object.ReferenceEquals(switchingGuide,Get(runtime,"setupWindow"))&&rememberedTick.IsChecked==true,"Returning to setup reuses the same window and keeps checklist progress");
            Check(switchingGuide.WindowState==WindowState.Normal&&switchingGuide.Left==movedGuideLeft&&switchingGuide.Top==movedGuideTop,"Setup can be moved aside, minimized and restored without resetting its position");
            string beforeSetupVersion=(string)Get(engine,"wpfOnboardingVersion");
            foreach(var read in Children(switchingGuide).OfType<CheckBox>().Where(c=>(c.Tag as string??"").StartsWith("guide-read-")).ToArray()) {read.IsChecked=true;read.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
            for(int page=0;page<1;page++)Children(switchingGuide).OfType<Button>().Single(b=>b.Tag as string=="setup-next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(switchingGuide.IsVisible&&Children(switchingGuide).OfType<FrameworkElement>().Any(c=>c.Tag as string=="setup-preview")&&Children(switchingGuide).OfType<ComboBox>().Any(c=>c.Tag as string=="minimap-shape"),"Actual first-time setup continues from the checklist into editable Appearance with a live preview");
            Check(!Children(switchingGuide).OfType<FrameworkElement>().Any(c=>(c.Tag as string??"").StartsWith("setup-tour-")),"Full configuration has no arrow overlay or walkthrough coach");
            switchingGuide.UpdateLayout();Capture(switchingGuide,Path.Combine(folder,"setup-appearance.png"));
            Children(switchingGuide).OfType<Button>().Single(b=>b.Tag as string=="setup-next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Children(switchingGuide).OfType<Button>().Any(b=>b.Tag as string=="setup-default-colours"),"First-time setup includes map and zone settings after Appearance");
            Children(switchingGuide).OfType<Button>().Single(b=>b.Tag as string=="setup-next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Children(switchingGuide).OfType<TextBox>().Any(c=>c.Tag as string=="setting-number"),"First-time setup includes tracking and zoom settings");
            Children(switchingGuide).OfType<Button>().Single(b=>b.Tag as string=="setup-next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Children(switchingGuide).OfType<Button>().Any(b=>b.Tag as string=="setup-private-positions")&&(string)Get(engine,"wpfOnboardingVersion")==beforeSetupVersion,"First-time setup reaches sharing without marking completion before the final Done button");
            Children(switchingGuide).OfType<Button>().Single(b=>b.Tag as string=="setup-next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!switchingGuide.IsVisible&&!(bool)needsWizard.GetValue(engine,null)&&!(bool)Get(engine,"isFirstLaunch"),"Only final Done completes the full first-install configuration");
            var adminAction=Enum.Parse(asm.GetType("ScumMiniMap.AppControlAction"),"AdminCommands");
            var trayAdmin=((System.Windows.Forms.ContextMenuStrip)Get(engine,"trayMenu")).Items.Cast<System.Windows.Forms.ToolStripItem>().Single(item=>item.Text=="Admin commands");
            trayAdmin.PerformClick();Pump();
            Check((bool)Get(engine,"adminToolsEnabled")&&((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Actual tray Admin commands enables and opens tools from their default disabled state");
            Call(engine,"DismissAdminControls");Call(engine,"SetAdminToolsEnabled",false);
            var adminSettings=(Window)Call(runtime,"BuildSettings",0);
            try {
                Check(Children(adminSettings).OfType<Button>().Any(b=>b.Tag as string=="first-time-guide"),"Settings exposes the first-time setup guide for reopening later");
                var adminCheck=Children(adminSettings).OfType<CheckBox>().Single(c=>c.Tag as string=="admin-tools-enabled");
                Check(adminCheck.IsChecked==false,"WPF Settings shows the actual disabled admin-tools state");
                var openAdmin=Children(adminSettings).OfType<Button>().Single(b=>b.Tag as string=="open-admin-commands");
                openAdmin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                Check(adminCheck.IsChecked==true&&((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Actual Settings Admin commands button enables and opens the palette");
                adminCheck.IsChecked=false;adminCheck.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                Check(!(bool)Get(engine,"adminToolsEnabled")&&!((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Settings checkbox disables admin tools and hides the palette");
                openAdmin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                Check(adminCheck.IsChecked==true&&((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Settings Open button works again after disabling admin tools");
            }finally { adminSettings.Close();Call(engine,"DismissAdminControls"); }
            trayAdmin.PerformClick();Pump();
            var modalAdminSettings=(Window)Call(runtime,"BuildSettings",0);
            modalAdminSettings.ContentRendered+=(s,e)=>Children(modalAdminSettings).OfType<Button>().Single(b=>b.Tag as string=="open-admin-commands").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Call(runtime,"Modal",modalAdminSettings);Pump();
            Check(!modalAdminSettings.IsVisible&&((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible&&((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Enabled,"Real modal Settings closes before reopening an enabled interactive admin palette");
            Call(engine,"DismissAdminControls");
            var dockingMap=(Window)Get(runtime,"tactical");dockingMap.Show();dockingMap.UpdateLayout();
            trayAdmin.PerformClick();Pump();
            var dockedAdmin=(System.Windows.Forms.Form)Get(engine,"adminCommandsPanel");
            var dockMapBounds=(System.Drawing.Rectangle)type.GetProperty("AdminMapBounds",F).GetValue(runtime,null);
            var dockWork=System.Windows.Forms.Screen.FromControl(dockedAdmin).WorkingArea;
            Check(dockedAdmin.Left==Math.Max(dockWork.Left,Math.Min(dockMapBounds.Left+4,dockWork.Right-dockedAdmin.Width)),"Admin palette docks snugly against the actual WPF map's left edge");
            dockingMap.Hide();
            var dragHeader=dockedAdmin.Controls.Find("AdminControlsTitle",true).Single();
            var beforeDrag=dockedAdmin.Location;
            typeof(System.Windows.Forms.Control).GetMethod("OnMouseDown",F).Invoke(dragHeader,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,10,10,0)});
            typeof(System.Windows.Forms.Control).GetMethod("OnMouseMove",F).Invoke(dragHeader,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,0,80,90,0)});
            typeof(System.Windows.Forms.Control).GetMethod("OnMouseUp",F).Invoke(dragHeader,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,80,90,0)});
            var afterDrag=dockedAdmin.Location;
            Check(afterDrag!=beforeDrag&&Get(engine,"wpfAdminControlsLocation")!=null&&!dragHeader.Capture,"Dragging the real admin header moves the panel and releases mouse capture");
            Call(engine,"DismissAdminControls");trayAdmin.PerformClick();Pump();
            Check(dockedAdmin.Location==afterDrag,"Reopening admin commands preserves the dragged location");
            Call(engine,"SetAdminControlsExpanded",false);Call(engine,"SetAdminControlsExpanded",true);
            Check(dockedAdmin.Location==afterDrag,"Collapsing and expanding admin controls preserves the dragged location");
            Call(engine,"DismissAdminControls");
            foreach(bool tacticalOpen in new[]{false,true}) {
                Call(engine,"WpfTactical",tacticalOpen);Call(runtime,"Handle",adminAction);Pump();
                var admin=(System.Windows.Forms.Form)Get(engine,"adminCommandsPanel");
                Check(admin!=null&&admin.Visible&&(bool)Get(engine,"adminPanelExpanded"),"WPF Admin commands opens expanded with tactical map "+tacticalOpen);
                Check(!((System.Windows.Forms.Form)Get(engine,"overlay")).Visible,"Opening WPF admin tools does not show the legacy map");
                Check(System.Windows.Forms.Screen.FromControl(admin).WorkingArea.Contains(admin.Bounds),"WPF admin controls fit on screen");
                Check(admin.Controls.Find("AdminCommandCard",true).Length>0&&admin.Controls.Find("AddAdminCommand",true).Single().Visible,"Opened WPF admin palette contains command cards and its editor action");
                using(var adminPreview=new System.Drawing.Bitmap(admin.Width,admin.Height)) { admin.DrawToBitmap(adminPreview,new System.Drawing.Rectangle(0,0,admin.Width,admin.Height));adminPreview.Save(Path.Combine(folder,"admin-commands-"+tacticalOpen+".png")); }
                Call(engine,"SyncFullMapAdminControls",true,true);
                Check(admin.Visible,"WPF admin controls remain open while WPF settings suppress map input");
                Call(engine,"SyncFullMapAdminControls",false,false);Check(!admin.Visible,"WPF admin tools hide when another application has focus");
                Call(engine,"SyncFullMapAdminControls",true,false);Check(admin.Visible,"Requested WPF admin tools return with application focus");
                Call(engine,"DismissAdminControls");Call(engine,"SyncFullMapAdminControls",true,false);
                Check(!admin.Visible,"Dismissed WPF admin controls stay closed across refreshes");
                Call(runtime,"Handle",adminAction);Check(Object.ReferenceEquals(admin,Get(engine,"adminCommandsPanel"))&&admin.Visible,"Repeated WPF admin action reuses and reopens the controls");
                Call(engine,"DismissAdminControls");
            }
            Call(runtime,"Handle",adminAction);Call(engine,"SetAdminToolsEnabled",false);Call(engine,"SetAdminToolsEnabled",true);Call(engine,"SyncFullMapAdminControls",true,false);
            Check(!((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Re-enabling admin tools does not resurrect a closed palette");
            Call(engine,"SetAdminToolsEnabled",false);Call(engine,"WpfTactical",false);
            var hud=(Window)Get(runtime,"hud");hud.Left=-32000;hud.Top=-32000;
            var surface=asm.GetType("ScumMiniMap.WpfMapSurface");var arrow=surface.GetMethod("DrawPlayer",BindingFlags.NonPublic|BindingFlags.Static);var pixels=new List<byte[]>();
            foreach(int yaw in new[]{0,90}) { var visual=new DrawingVisual();using(var dc=visual.RenderOpen())arrow.Invoke(null,new object[]{dc,new Point(32,32),(double)yaw,Brushes.Cyan,true});var image=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);image.Render(visual);var bytes=new byte[64*64*4];image.CopyPixels(bytes,64*4,0);pixels.Add(bytes);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(folder,"player-arrow-"+yaw+".png")))png.Save(stream); }
            Check(!pixels[0].SequenceEqual(pixels[1]),"Shared self/teammate arrow renderer turns with player heading");
            var arrowState=Call(engine,"WpfSnapshot",true);var arrowPos=Activator.CreateInstance(Get(engine,"position").GetType());var arrowPeer=Activator.CreateInstance(asm.GetType("ScumMiniMap.PrivatePositions+Peer"),true);Set(arrowPeer,"Name","Arrow fixture");Set(arrowPeer,"Position",arrowPos);var arrowPeers=Array.CreateInstance(arrowPeer.GetType(),1);arrowPeers.SetValue(arrowPeer,0);Set(arrowState,"Peers",arrowPeers);var identity=type.GetMethod("VisualIdentity",BindingFlags.NonPublic|BindingFlags.Static);string previousIdentity=(string)identity.Invoke(null,new[]{arrowState});Set(arrowPos,"Yaw",90d);Check(previousIdentity!=(string)identity.Invoke(null,new[]{arrowState}),"Turning in place invalidates teammate map rendering");
            var groups=(Window)Call(runtime,"BuildRelayGroups");try { Check(Children(groups).OfType<TextBox>().Any(box=>(string)box.Tag=="new-group-label"&&box.MaxLength==48),"Group creation exposes a separate group-name field"); }finally { groups.Close(); }
            var memberships=(System.Collections.IList)Get(Get(runtime,"relayGroups"),"groups");var membershipType=asm.GetType("ScumMiniMap.RelayMembership");
            var ownerGroup=Activator.CreateInstance(membershipType,true);Set(ownerGroup,"Id",new string('a',32));Set(ownerGroup,"Key",Convert.ToBase64String(new byte[32]));Set(ownerGroup,"Owner",new string('b',64));Set(ownerGroup,"Pending","");Set(ownerGroup,"Name","Synthetic owned group");memberships.Add(ownerGroup);
            var memberGroup=Activator.CreateInstance(membershipType,true);Set(memberGroup,"Id",new string('c',32));Set(memberGroup,"Key",Convert.ToBase64String(new byte[32]));Set(memberGroup,"Owner","");Set(memberGroup,"Pending","");Set(memberGroup,"Name","Synthetic joined group");memberships.Add(memberGroup);
            groups=(Window)Call(runtime,"BuildRelayGroups");try {
                var groupButtons=Children(groups).OfType<Button>().ToArray();
                Check(groupButtons.Count(b=>(b.Tag as string??"").StartsWith("group-delete:"))==1&&groupButtons.Any(b=>(string)b.Tag=="group-delete:"+new string('a',32)),"Only group creators receive the Delete group control");
                Check(!groupButtons.Any(b=>(b.Content as string??"").Contains(" / ")),"Private groups no longer expose the direct IP Host/Join button");
            }finally { groups.Close();memberships.Clear(); }
            var tactical=(Window)Get(runtime,"tactical");Call(runtime,"ToggleTactical",false);tactical.Left=-32000;tactical.Top=-32000;Check(tactical.IsVisible&&(bool)Get(engine,"fullMapActive"),"Opening map synchronizes visible window and engine flag");
            var contextMap=Get(runtime,"tacticalMap");tactical.UpdateLayout();
            var adminPoint=new System.Drawing.PointF(.2f,.3f);
            var noAdmin=(ContextMenu)Call(contextMap,"BuildContextMenu",adminPoint);
            Check(!noAdmin.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Admin commands"),"Map context omits admin actions when admin tools are disabled");
            Call(engine,"SetAdminToolsEnabled",true);
            var withAdmin=(ContextMenu)Call(contextMap,"BuildContextMenu",adminPoint);
            var adminRoot=withAdmin.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Admin commands");
            var teleportHere=adminRoot.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Teleport here");
            var expectedTeleport=engine.GetType().GetMethod("BuildMapTeleportCommand",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{adminPoint});
            Check(teleportHere.IsEnabled&&object.Equals(teleportHere.Tag,expectedTeleport)&&((string)teleportHere.ToolTip).Contains("Z is 0"),"Right-click teleport uses the clicked map coordinates and discloses unavailable height");
            Check(adminRoot.Items.OfType<MenuItem>().Any(item=>item.Items.OfType<MenuItem>().Any(child=>(string)child.Header=="ON")),"Restored admin context preserves explicit ON/OFF commands");
            Set(engine,"adminCommandBusy",true);
            var busyAdmin=(ContextMenu)Call(contextMap,"BuildContextMenu",adminPoint);
            Check(busyAdmin.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Admin commands").Items.OfType<MenuItem>().All(item=>!item.IsEnabled),"All restored admin context actions disable while an admin command is busy");
            Set(engine,"adminCommandBusy",false);
            Call(contextMap,"OpenContextMenu",new Point(((FrameworkElement)contextMap).ActualWidth/2,((FrameworkElement)contextMap).ActualHeight/2));Pump();
            var adminPopup=(Border)Get(contextMap,"openContext");
            var expandAdmin=Children(adminPopup).OfType<Button>().Single(button=>button.Content as string=="▸ Admin commands");expandAdmin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(Children(adminPopup).OfType<Button>().Any(button=>button.IsVisible&&button.Content is TextBlock&&((TextBlock)button.Content).Text=="Teleport here")&&PresentationSource.FromVisual(adminPopup)==PresentationSource.FromVisual((System.Windows.Media.Visual)contextMap),"Restored teleport actions expand inside the existing map window without a native popup");
            Capture(tactical,Path.Combine(folder,"right-click-admin-teleport.png"));
            Call(contextMap,"CloseContextMenu");Call(engine,"SetAdminToolsEnabled",false);
            var opacityControl=(Slider)Get(runtime,"tacticalOpacity");var opacityValue=(TextBlock)Get(runtime,"tacticalOpacityValue");
            Check(opacityValue.Text==opacityControl.Value.ToString("0",System.Globalization.CultureInfo.CurrentCulture)+"%","Map toolbar shows the current opacity percentage beside its slider");
            var tacticalHandle=new System.Windows.Interop.WindowInteropHelper(tactical).Handle;Check(System.Threading.Tasks.Task.Run(()=> (bool)Call(runtime,"OwnsForeground",tacticalHandle)).Result,"Keyboard-hook ownership reads a UI-published snapshot safely from a worker");
            bool wrongThreadRejected=false;System.Threading.Tasks.Task.Run(()=> { try { Call(runtime,"Refresh"); }catch(TargetInvocationException e) { wrongThreadRejected=e.InnerException is InvalidOperationException; } }).Wait();Check(wrongThreadRejected,"Control refresh fails fast on a worker instead of touching UI across threads");
            bool snapshotRejected=false;System.Threading.Tasks.Task.Run(()=> { try { Call(engine,"WpfSnapshot",false); }catch(TargetInvocationException e) { snapshotRejected=e.InnerException is InvalidOperationException; } }).Wait();Check(snapshotRejected,"Map snapshots reject worker access before reading UI-owned caches");
            object targetBefore=Get(engine,"searchTarget");bool targetRejected=false;System.Threading.Tasks.Task.Run(()=> { try { Call(engine,"WpfTarget",targetBefore); }catch(TargetInvocationException e) { targetRejected=e.InnerException is InvalidOperationException; } }).Wait();Check(targetRejected&&object.ReferenceEquals(targetBefore,Get(engine,"searchTarget")),"Worker navigation mutations fail before changing the current target");
            System.Threading.Tasks.Task.Run(()=>Call(engine,"RenderOverlay")).Wait();Pump();Check(true,"Worker render notifications marshal back to the UI before entering WPF refresh");
            Check(VisualTreeHelper.GetParent((Canvas)Get(contextMap,"ContextHost"))==tactical.Content,"Context overlay is above the full window including its sidebar");
            var clickPoint=new Point(((FrameworkElement)contextMap).ActualWidth/2,((FrameworkElement)contextMap).ActualHeight/2);
            for(int attempt=0;attempt<8;attempt++) {
                Check((bool)Call(contextMap,"BeginContextPointer",clickPoint),"Map accepts right-button press "+(attempt+1));
                Check((bool)Call(contextMap,"EndContextPointer",new Point(-20,-20)),"Right-button release after pointer drift still opens the menu "+(attempt+1));
                Await(()=> { tactical.UpdateLayout();var candidate=(Border)Get(contextMap,"openContext");return candidate!=null&&candidate.IsVisible&&candidate.ActualWidth>0; });var popup=(Border)Get(contextMap,"openContext");
                double expectedX=popup==null?0:Math.Max(8,Math.Min(clickPoint.X,((FrameworkElement)contextMap).ActualWidth-popup.DesiredSize.Width-8)),expectedY=popup==null?0:Math.Max(8,Math.Min(clickPoint.Y,((FrameworkElement)contextMap).ActualHeight-popup.DesiredSize.Height-8));
                Check(popup!=null&&popup.IsVisible&&Math.Abs(Canvas.GetLeft(popup)-expectedX)<1&&Math.Abs(Canvas.GetTop(popup)-expectedY)<1,"Context menu stays anchored to the original click within map bounds "+(attempt+1)+" ("+Canvas.GetLeft(popup)+","+Canvas.GetTop(popup)+" expected "+expectedX+","+expectedY+")");
                Check(PresentationSource.FromVisual(popup)==PresentationSource.FromVisual((System.Windows.Media.Visual)contextMap),"Context menu shares the map HWND without native popup activation "+(attempt+1));
                Call(contextMap,"CloseContextMenu");Pump();
            }
            var native=asm.GetType("ScumMiniMap.Native");var foreground=native.GetMethod("GetForegroundWindow",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);var beforeMenu=(IntPtr)foreground.Invoke(null,null);
            Call(contextMap,"OpenContextMenu",clickPoint);for(int tick=0;tick<30;tick++) { Call(runtime,"SyncTacticalFocus",true,false);Call(runtime,"Refresh");Pump(); }
            Check(Get(contextMap,"openContext")!=null&&(IntPtr)foreground.Invoke(null,null)==beforeMenu,"Menu survives repeated refreshes without stealing foreground focus");
            Check((bool)Call(runtime,"TryDismissTactical",0x1B,0,false)&&tactical.IsVisible&&Get(contextMap,"openContext")==null,"Escape closes only the in-map menu before closing the map");
            Call(contextMap,"OpenContextMenu",clickPoint);Check((bool)Call(contextMap,"DismissContextOutside",new Point(0,0))&&Get(contextMap,"openContext")==null,"Outside click dismisses the in-map menu");
            Call(engine,"WpfPoiClustering",false);Call(runtime,"Refresh");var draw=surface.GetMethod("OnRender",F);var frame=new DrawingGroup();using(var dc=frame.Open())draw.Invoke(contextMap,new object[]{dc});
            Await(()=> { var pending=new DrawingGroup();using(var dc=pending.Open())draw.Invoke(contextMap,new object[]{dc});return (bool)Get(contextMap,"PoiViewportReady"); });Check(((Array)Get(contextMap,"poiSceneTiles")).Cast<object>().All(tile=>((DrawingGroup)Get(tile,"Drawing")).IsFrozen),"Dense POI workers publish only frozen visible-cell drawings to the UI");
            Check((bool)Get(contextMap,"PoiSceneRasterized"),"Dense full-map composition uses bounded frozen tile images rather than replaying every icon");
            var terrainLoader=Get(runtime,"terrain");Await(()=>!(bool)Get(terrainLoader,"tileWorker")&&(int)Get(terrainLoader,"pendingTiles").GetType().GetProperty("Count").GetValue(Get(terrainLoader,"pendingTiles"),null)==0);
            var composition=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<3;i++) { var bitmap=new RenderTargetBitmap((int)((FrameworkElement)contextMap).ActualWidth,(int)((FrameworkElement)contextMap).ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render((Visual)contextMap); }composition.Stop();Console.WriteLine("Full-map composition average="+(composition.Elapsed.TotalMilliseconds/3).ToString("F1")+" ms");
            if(Environment.GetEnvironmentVariable("MINIMAP_TEST_COMPONENT_TRACE")=="1") {
                var componentState=Get(contextMap,"State");var originalStore=Get(componentState,"Store");bool originalLabels=(bool)Get(componentState,"Labels"),originalZones=(bool)Get(componentState,"Zones");
                Action<string> measure=name=> { ((FrameworkElement)contextMap).InvalidateVisual();var clock=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<3;i++) {var b=new RenderTargetBitmap((int)((FrameworkElement)contextMap).ActualWidth,(int)((FrameworkElement)contextMap).ActualHeight,96,96,PixelFormats.Pbgra32);b.Render((Visual)contextMap);}Console.WriteLine("COMPONENT "+name+"="+(clock.Elapsed.TotalMilliseconds/3).ToString("F1")); };
                Set(componentState,"Labels",false);measure("without labels");Set(componentState,"Zones",false);measure("without labels or saved zones");Set(componentState,"Store",null);measure("without labels, zones or POIs/habitats");Set(contextMap,"State",null);measure("blank map surface");Set(contextMap,"State",componentState);Set(componentState,"Store",originalStore);Set(componentState,"Labels",originalLabels);Set(componentState,"Zones",originalZones);((FrameworkElement)contextMap).InvalidateVisual();
            }
            var completeState=Get(contextMap,"State");double blankReadback;
            try {
                Set(contextMap,"State",null);((FrameworkElement)contextMap).InvalidateVisual();var blankClock=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<3;i++) {var bitmap=new RenderTargetBitmap((int)((FrameworkElement)contextMap).ActualWidth,(int)((FrameworkElement)contextMap).ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render((Visual)contextMap);}blankReadback=blankClock.Elapsed.TotalMilliseconds/3;
            }finally {Set(contextMap,"State",completeState);((FrameworkElement)contextMap).InvalidateVisual();}
            double sceneOverhead=composition.Elapsed.TotalMilliseconds/3-blankReadback;Console.WriteLine("Blank bitmap readback="+blankReadback.ToString("F1")+" ms; map scene overhead="+sceneOverhead.ToString("F1")+" ms");
            Check(sceneOverhead<75&&composition.Elapsed.TotalMilliseconds/3<1000,"Full-map scene work stays responsive above the independently measured blank readback cost");
            var quickTarget=Call(engine,"WpfClickedTarget",new System.Drawing.PointF(.45f,.52f),.001f);int zoneRevision=(int)Get(engine,"wpfZoneRevision");Call(engine,"WpfTarget",quickTarget);Call(runtime,"Refresh");
            Check(object.ReferenceEquals(Get(Get(contextMap,"State"),"Target"),quickTarget)&&(int)Get(engine,"wpfZoneRevision")==zoneRevision,"GPS marker is published immediately without invalidating unrelated zone caches");
            Set(engine,"activeRoute",null);Set(engine,"routeCalculating",true);string pendingSummary=(string)Call(engine,"FieldConsoleRouteSummary");Check(pendingSummary.Contains("ETA")&&pendingSummary.Contains("Calculating"),"Pending road calculation still shows direct distance and an estimated travel time");Set(engine,"routeCalculating",false);Check(((string)Call(engine,"FieldConsoleRouteSummary")).Contains("ETA"),"Direct-only navigation retains its estimated travel time");Call(engine,"WpfClear");Call(runtime,"Refresh");
            int builds=(int)Get(contextMap,"PoiSceneBuilds");var renderClock=System.Diagnostics.Stopwatch.StartNew();for(int frameIndex=0;frameIndex<12;frameIndex++) { var group=new DrawingGroup();using(var dc=group.Open())draw.Invoke(contextMap,new object[]{dc}); }renderClock.Stop();Console.WriteLine("Dense map redraw average="+(renderClock.Elapsed.TotalMilliseconds/12).ToString("F1")+" ms");
            Check(renderClock.Elapsed.TotalMilliseconds/12<50&&(int)Get(contextMap,"PoiSceneBuilds")==builds,"Dense-map redraws reuse retained graphics without rebuilding on the UI thread");
            var filterStore=Get(Get(contextMap,"State"),"Store");var filterCategory=((System.Collections.IEnumerable)Get(filterStore,"Categories")).Cast<object>().First(c=>Convert.ToInt32(Get(c,"MarkerCount"))>0);int filterId=Convert.ToInt32(Get(filterCategory,"Id"));
            var filterTiming=System.Diagnostics.Stopwatch.StartNew();Call(runtime,"Category",filterId,false);var filterFrame=new DrawingGroup();using(var dc=filterFrame.Open())draw.Invoke(contextMap,new object[]{dc});
            Check(filterTiming.ElapsedMilliseconds<300,"A POI toggle records its first updated map frame promptly: "+filterTiming.ElapsedMilliseconds+" ms");
            Await(()=> { var pending=new DrawingGroup();using(var dc=pending.Open())draw.Invoke(contextMap,new object[]{dc});return (bool)Get(contextMap,"PoiViewportReady"); });filterTiming.Stop();Console.WriteLine("POI toggle through completed scene="+filterTiming.ElapsedMilliseconds+" ms");Check(filterTiming.ElapsedMilliseconds<1000,"POI changes finish without the reported two-second delay");
            Call(runtime,"Category",filterId,true);var restoredFrame=new DrawingGroup();using(var dc=restoredFrame.Open())draw.Invoke(contextMap,new object[]{dc});Await(()=> { var pending=new DrawingGroup();using(var dc=pending.Open())draw.Invoke(contextMap,new object[]{dc});return (bool)Get(contextMap,"PoiViewportReady"); });
            Set(runtime,"diagnostic",false);Set(runtime,"lastRefresh",long.MaxValue);
            try {
                Call(contextMap,"NavigateTo",new System.Drawing.PointF(.46f,.53f));Check((long)Get(runtime,"lastRefresh")!=long.MaxValue,"A real click bypasses production refresh throttling even immediately after another refresh");
                Set(engine,"fullMapZoom",1f);Set(engine,"fullMapOpacity",0);Set(runtime,"lastRefresh",long.MaxValue);((Action<int>)Get(contextMap,"ZoomRequested"))(1);
                Check(Convert.ToDouble(Get(Get(contextMap,"State"),"Zoom"))>1,"Wheel zoom updates the camera even while a full refresh is throttled");
                var wheelSnapshot=Call(engine,"WpfSnapshot",true);
                Check((int)Get(wheelSnapshot,"Opacity")==100&&Convert.ToDouble(Get(wheelSnapshot,"Zoom"))>1,"Wheel zoom retains opaque full-map terrain independent of foreground visibility");
                // Production focus handling may hide this off-screen fixture when
                // another app owns the foreground. Do not inspect its stale map
                // frame as though it were visible; publish a diagnostic full view.
                if(!tactical.IsVisible) { Set(runtime,"diagnostic",true);Call(runtime,"Refresh"); }
                Check((int)Get(Get(contextMap,"State"),"Opacity")==100&&Convert.ToDouble(Get(Get(contextMap,"State"),"Zoom"))>1,"Wheel zoom immediately publishes visible opaque terrain despite refresh throttling");
            }
            finally { Set(runtime,"diagnostic",true);Call(engine,"WpfClear");if(!tactical.IsVisible)Call(runtime,"ToggleTactical",false);Call(runtime,"Refresh"); }
            Call(engine,"WpfPoiClustering",true);Call(runtime,"Refresh");
            Call(contextMap,"OpenContextMenu",clickPoint);Pump();
            var inline=(Border)Get(contextMap,"openContext");var expandSquad=Children(inline).OfType<Button>().Single(b=>(b.Content as string)=="▸ Routes");expandSquad.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(Canvas.GetLeft(inline)>=0&&Canvas.GetTop(inline)>=0&&Canvas.GetLeft(inline)+inline.ActualWidth<=((FrameworkElement)contextMap).ActualWidth&&Canvas.GetTop(inline)+inline.ActualHeight<=((FrameworkElement)contextMap).ActualHeight,"Expanded squad menu stays within the map edges");
            var menuImage=new RenderTargetBitmap((int)tactical.ActualWidth,(int)tactical.ActualHeight,96,96,PixelFormats.Pbgra32);menuImage.Render(tactical);var menuPng=new PngBitmapEncoder();menuPng.Frames.Add(BitmapFrame.Create(menuImage));using(var file=File.Create(Path.Combine(folder,"map-context-menu.png")))menuPng.Save(file);
            var inlineStop=Children(inline).OfType<Button>().Single(b=>b.Content is TextBlock&&((TextBlock)b.Content).Text=="Add route stop");inlineStop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Get(contextMap,"openContext")==null&&((Array)Get(runtime,"squad").GetType().GetProperty("Stops",F).GetValue(Get(runtime,"squad"),null)).Length==1,"Clicking an actual nested in-map route action adds its stop and dismisses the menu");
            Check(((Array)Get(runtime,"activeStops")).Length==1&&Get(engine,"searchTarget")!=null&&((Border)Get(runtime,"targetCard")).Visibility==Visibility.Visible,"The first placed stop automatically starts and displays GPS navigation");
            var clearMenu=(ContextMenu)Call(contextMap,"BuildContextMenu",new System.Drawing.PointF(.5f,.5f));var stopMenu=clearMenu.Items.OfType<MenuItem>().Single(m=>(string)m.Header=="Routes").Items.OfType<MenuItem>().Single(m=>(string)m.Header=="Stop navigation");stopMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(Get(engine,"searchTarget")==null&&((Array)Get(runtime,"squad").GetType().GetProperty("Stops",F).GetValue(Get(runtime,"squad"),null)).Length==1,"Stop navigation preserves the route for manual resumption");
            clearMenu=(ContextMenu)Call(contextMap,"BuildContextMenu",new System.Drawing.PointF(.5f,.5f));var clearMapMenu=clearMenu.Items.OfType<MenuItem>().Single(m=>(string)m.Header=="Clear route");Check(clearMapMenu.IsEnabled,"The primary Clear route action remains enabled for a stopped solo route");clearMapMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(Get(engine,"searchTarget")==null&&((Array)Get(runtime,"activeStops")).Length==0&&((Array)Call(runtime,"OwnSquadPublication")).Length==0&&((Array)Get(Get(contextMap,"State"),"LocalRoute")).Length==0,"Primary Clear route removes the solo route, navigation, drawing stops and group publication together");
            Call(runtime,"AddSquadStop",new System.Drawing.PointF(.51f,.52f));Children((Border)Get(runtime,"targetCard")).OfType<Button>().Single(b=>(string)b.Tag=="text:WpfClearRoute").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((Array)Get(Get(contextMap,"State"),"LocalRoute")).Length==0&&Get(engine,"searchTarget")==null,"The GPS card Clear route button removes the complete solo route");
            Call(runtime,"AddSquadStop",new System.Drawing.PointF(.51f,.52f));Call(runtime,"StopSquadNavigation");
            var searchClear=(Window)Call(runtime,"BuildSearch");
            try {
                var clearFromSearch=Children(searchClear).OfType<Button>().Single(b=>b.Tag as string=="search-clear-route");
                Check(clearFromSearch.IsEnabled,"Search can clear a stopped route with no active GPS destination");
                clearFromSearch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for(int tick=0;tick<3;tick++) {Pump();Call(runtime,"Refresh");}
                Check(((Array)Get(Get(contextMap,"State"),"LocalRoute")).Length==0&&Get(engine,"searchTarget")==null&&((Array)Call(runtime,"OwnSquadPublication")).Length==0,"Search Clear route removes stops, navigation and publication across refreshes");
            }finally {searchClear.Close();}
            Call(runtime,"AddSquadStop",new System.Drawing.PointF(.52f,.53f));Call(runtime,"Handle",Enum.Parse(asm.GetType("ScumMiniMap.AppControlAction"),"ClearWaypoint"));
            Check(((Array)Get(Get(contextMap,"State"),"LocalRoute")).Length==0&&Get(engine,"searchTarget")==null,"The clear shortcut removes route stops as well as its GPS target");
            Check(!(bool)Call(contextMap,"OpenContextMenu",new Point(-1,-1)),"Clicks outside the map cannot open its context menu");
            Call(contextMap,"OpenContextMenu",clickPoint);
            Call(runtime,"SyncTacticalFocus",true,false);Check(tactical.IsVisible,"Gameplay focus keeps full map visible");Call(runtime,"SyncTacticalFocus",false,true);Check(tactical.IsVisible,"MiniMap controls remain usable");
            Call(runtime,"SyncTacticalFocus",false,false);Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive")&&!(bool)Get(runtime,"tacticalMouseShieldActive"),"Alt-tab hides full map, clears controller flag and releases mouse shield");
            Check(Get(contextMap,"openContext")==null,"Hiding the map also dismisses its context menu");
            Call(runtime,"SyncTacticalFocus",true,false);Check(!tactical.IsVisible,"Returning to SCUM does not reopen a dismissed map");Call(runtime,"ToggleTactical",false);Call(engine,"SetFullMap",false);Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive"),"Legacy chat and close paths hide the actual WPF window");
            for(int i=0;i<4;i++) { Call(runtime,"ToggleTactical",false);Call(runtime,"CloseTactical",false); }Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive"),"Repeated open and close requests do not leave a stuck map");
            Call(runtime,"ToggleTactical",false);Set(engine,"fullMapActive",false);Check((bool)Call(runtime,"TryDismissTactical",0x1B,0,true)&&!tactical.IsVisible,"Escape dismisses the visible map even with a stale flag and paused tracking");
            var direct=asm.GetType("ScumMiniMap.DirectPositions");var privateType=asm.GetType("ScumMiniMap.PrivatePositions");string key=(string)privateType.GetMethod("NewKey",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
            host=(IDisposable)Activator.CreateInstance(direct,F,null,new object[]{true,"",port,key,"Lead",false,null},null);friend=(IDisposable)Activator.CreateInstance(direct,F,null,new object[]{false,"127.0.0.1",port,key,"Brother",false,null},null);Set(runtime,"privatePositions",host);
            var pos=Get(engine,"position");Call(friend,"Update",pos,DateTime.UtcNow);Await(()=>((Array)Call(host,"Roster")).Length==1);Call(runtime,"Refresh");var roster=(Array)Get(runtime,"playerRoster");var peer=roster.GetValue(0);string id=(string)Get(peer,"Id");Check(((string)Call(runtime,"PeerStatus",peer)).Contains("Last updated"),"Teammate status shows capture age");
            Set(runtime,"followingPeer",id);Call(runtime,"UpdateSquad");var target=Get(engine,"searchTarget");Check(target!=null&&((string)Get(target,"Name")).Contains("Brother"),"Following targets the selected teammate");
            Check(!((Array)Call(engine,"WpfSharedMarkers")).Cast<object>().Any(m=>(string)Get(m,"Id")==new string('F',64)),"Following cannot bridge a teammate location into other groups as a GPS marker");
            var moved=Activator.CreateInstance(pos.GetType());foreach(string n in new[]{"X","Y","Z","Yaw"})Set(moved,n,Get(pos,n));Set(moved,"X",Convert.ToDouble(Get(pos,"X"))+20000);Call(friend,"Update",moved,DateTime.UtcNow);Set(runtime,"nextSquadRoute",DateTime.MinValue);Await(()=> { var p=((Array)Call(host,"Snapshot")).GetValue(0);return Convert.ToDouble(Get(Get(p,"Position"),"X"))==Convert.ToDouble(Get(moved,"X")); });Call(runtime,"UpdateSquad");Check(!object.ReferenceEquals(target,Get(engine,"searchTarget")),"Moving teammate updates the navigation destination");
            Call(friend,"Update",null,DateTime.UtcNow);Await(()=>Get(((Array)Call(host,"Roster")).GetValue(0),"Position")==null);Call(runtime,"UpdateSquad");Check(Get(engine,"searchTarget")==null&&(string)Get(runtime,"followingPeer")==id,"Stale or paused teammate position removes navigation until a fresh position arrives");
            Call(friend,"Update",moved,DateTime.UtcNow);Await(()=>Get(((Array)Call(host,"Roster")).GetValue(0),"Position")!=null);Await(()=> { Call(runtime,"UpdateSquad");return Get(engine,"searchTarget")!=null; });Check(true,"Following resumes when the teammate publishes a fresh location");
            Call(runtime,"StopSquadNavigation");Check(Get(runtime,"followingPeer")==null,"Explicit stop cancels follow even while the teammate is stale");
            var squad=Get(runtime,"squad");var toMap=engine.GetType().GetMethod("ToMap",BindingFlags.Public|BindingFlags.Static);var firstPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{pos});var secondPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{moved});var finalPos=Activator.CreateInstance(pos.GetType());foreach(string n in new[]{"X","Y","Z","Yaw"})Set(finalPos,n,Get(pos,n));Set(finalPos,"X",Convert.ToDouble(Get(pos,"X"))+40000);var finalPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{finalPos});
            Call(squad,"AddStop","Bunker",(double)firstPoint.X,(double)firstPoint.Y);Call(squad,"AddStop","Fuel",(double)secondPoint.X,(double)secondPoint.Y);Call(squad,"AddStop","Trader",(double)finalPoint.X,(double)finalPoint.Y);
            // Arrival requires a fresh sample, independent of time spent in
            // the preceding layout, rendering and network fixtures.
            Set(engine,"updated",DateTime.UtcNow);
            Set(engine,"diagnosticMode",false);
            try { var stops=squad.GetType().GetProperty("Stops",F).GetValue(squad,null);Call(runtime,"StartSquadRoute",stops);Check((int)Get(runtime,"activeStop")==1,"Starting at the first stop advances to the second stop");string trip=(string)Get(Get(contextMap,"State"),"RouteSummary");Check(trip.Contains("Next stop:")&&trip.Contains("Remaining route:")&&trip.Contains("ETA"),"Multi-stop GPS displays next-leg and remaining-journey distance and ETA");Set(engine,"position",moved);Set(engine,"updated",DateTime.UtcNow);Call(runtime,"UpdateSquad");Check((int)Get(runtime,"activeStop")==2&&((string)Get(Get(engine,"searchTarget"),"Name")).StartsWith("3/3"),"Arrival advances ordered route navigation to the next stop");Set(engine,"position",finalPos);Call(runtime,"UpdateSquad");Check(Get(engine,"searchTarget")==null&&((Array)Get(runtime,"activeStops")).Length==0,"Completing the final route stop clears navigation"); }
            finally { Set(engine,"diagnosticMode",true); }
            Call(runtime,"AddSquadStop",new System.Drawing.PointF(finalPoint.X+.01f,finalPoint.Y+.01f));var ownPlanner=Get(runtime,"ownRoutePlanner");Await(()=>ownPlanner.GetType().GetProperty("Plan",F).GetValue(ownPlanner,null)!=null);
            var preview=ownPlanner.GetType().GetProperty("Plan",F).GetValue(ownPlanner,null);Check(((Array)Get(preview,"Legs")).Length==4,"Placing a stop automatically calculates every stage without pressing Start route");
            Call(runtime,"Refresh");Check(((Array)Get(runtime,"activeStops")).Length==4&&((TextBlock)Get(runtime,"targetDetail")).Text.Contains("Remaining route:")&&((TextBlock)Get(runtime,"targetDetail")).Text.Contains("ETA"),"Adding a stop automatically activates the complete updated route and its GPS readout");
            Check(object.ReferenceEquals(Get(Get(contextMap,"State"),"SquadPlan"),preview),"Automatically started navigation reuses the completed own-route plan instead of repeating its calculation");
            Call(runtime,"StopSquadNavigation");Call(runtime,"Refresh");Check(((Border)Get(runtime,"routePreviewCard")).Visibility==Visibility.Visible&&((TextBlock)Get(runtime,"routePreviewDetail")).Text.Contains("ETA"),"Stopping navigation keeps the route preview available without automatically restarting");
            Capture(tactical,Path.Combine(folder,"route-gps-preview.png"));
            Children((Border)Get(runtime,"routePreviewCard")).OfType<Button>().Single(b=>(string)b.Tag=="text:SquadStartRoute").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((Array)Get(runtime,"activeStops")).Length==4&&((Border)Get(runtime,"routePreviewCard")).Visibility==Visibility.Collapsed,"The full-map route card starts GPS navigation without opening squad tools");Call(runtime,"StopSquadNavigation");Call(runtime,"Refresh");
            Call(runtime,"Refresh");var map=Get(runtime,"tacticalMap");var menu=(ContextMenu)Call(map,"BuildContextMenu",new System.Drawing.PointF(finalPoint.X+.01f,finalPoint.Y+.01f));var squadMenu=menu.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Routes");
            Check(menu.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Add route stop")&&squadMenu.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Start route")&&squadMenu.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Remove last stop"),"Right-click exposes Add route stop directly, with start and undo actions in squad tools");
            Check(menu.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Navigate here (GPS)")&&menu.Items.OfType<MenuItem>().Any(item=>(string)item.Header=="Save location"),"Temporary GPS navigation and saved locations have distinct labels");
            var kindMenu=squadMenu.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Your route");var shared=kindMenu.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Shared route");shared.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check((bool)squad.GetType().GetProperty("Shared",F).GetValue(squad,null),"Right-click switches your route to explicitly shared mode");
            Call(runtime,"Refresh");menu=(ContextMenu)Call(map,"BuildContextMenu",new System.Drawing.PointF(finalPoint.X+.01f,finalPoint.Y+.01f));squadMenu=menu.Items.OfType<MenuItem>().Single(item=>(string)item.Header=="Routes");var remove=squadMenu.Items.OfType<MenuItem>().Single(item=>((string)item.Header).StartsWith("Delete:"));remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Await(()=>ownPlanner.GetType().GetProperty("Plan",F).GetValue(ownPlanner,null)!=null);Check(((Array)squad.GetType().GetProperty("Stops",F).GetValue(squad,null)).Length==3,"Right-click removes a placed stop and recalculates the remaining stages");
            Call(friend,"Update",moved,DateTime.UtcNow);var friendMarkers=Array.CreateInstance(asm.GetType("ScumMiniMap.SharedMapMarker"),1);var gps=Activator.CreateInstance(friendMarkers.GetType().GetElementType(),true);Set(gps,"Id",new string('F',64));Set(gps,"Name","Friend destination");Set(gps,"X",.4d);Set(gps,"Y",.5d);friendMarkers.SetValue(gps,0);Call(friend,"UpdateMarkers",friendMarkers);Await(()=>((Array)Call(host,"SnapshotMarkers")).Length==1);Await(()=> { Call(runtime,"Refresh");return ((Array)Get(Get(map,"State"),"OtherRoutePlans")).Length==1; });Check(true,"Another player's solo GPS route is calculated and displayed automatically");
            var remoteSquad=Activator.CreateInstance(squad.GetType(),F,null,new object[]{null},null);Call(remoteSquad,"SetShared",true);Call(remoteSquad,"AddStop","Shared bunker",.2d,.3d);Call(remoteSquad,"AddStop","Shared trader",.4d,.5d);Call(friend,"UpdateMarkers",Call(remoteSquad,"Local"));Await(()=>((Array)Call(host,"SnapshotMarkers")).Length==2);Call(runtime,"StartSquadRoute",Call(host,"SnapshotMarkers"));Check(!((Array)Call(runtime,"OwnSquadPublication")).Cast<object>().Any(m=>((string)Get(m,"Id")).StartsWith("534F4C4F")||((string)Get(m,"Id")).StartsWith("53485244")),"Following a shared route suppresses duplicate personal route publications");
            Call(remoteSquad,"AddStop","Shared fuel",.5d,.6d);Call(friend,"UpdateMarkers",Call(remoteSquad,"Local"));Await(()=>((Array)Call(host,"SnapshotMarkers")).Length==3);Call(runtime,"Refresh");Check(((Array)Get(runtime,"activeStops")).Length==3,"A followed shared route updates when its owner places another stage");Call(runtime,"StopSquadNavigation");
            Call(runtime,"Refresh");Children((Border)Get(runtime,"routePreviewCard")).OfType<Button>().Single(b=>(string)b.Tag=="text:WpfClearRoute").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(int tick=0;tick<3;tick++) { Pump();Call(runtime,"Refresh"); }
            Check(((Array)Get(Get(contextMap,"State"),"LocalRoute")).Length==0&&Get(Get(contextMap,"State"),"OwnRoutePlan")==null&&Get(engine,"searchTarget")==null&&((Border)Get(runtime,"routePreviewCard")).Visibility==Visibility.Collapsed,"Clearing a stopped solo route removes its preview permanently across refreshes");
            Check(((Array)Call(host,"SnapshotMarkers")).Length==3,"Clearing your route preserves another player's shared route");
            Call(runtime,"Refresh");menu=(ContextMenu)Call(map,"BuildContextMenu",secondPoint);Check(menu.Items.OfType<MenuItem>().Any(item=>((string)item.Header).StartsWith("Follow teammate:")),"Right-click on a teammate exposes moving-teammate navigation");
            var preset=Call(engine,"WpfCapturePreset","Driving");Call(engine,"WpfLayer","grid",!(bool)Get(preset,"Grid"));Call(engine,"WpfApplyPreset",preset);Check((bool)Get(Call(engine,"WpfSnapshot",false),"Grid")== (bool)Get(preset,"Grid"),"Applying preset restores actual map layers");
            bool inspected=false;var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) };timer.Tick+=(s,e)=> {
                timer.Stop();try { var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Squad tools");dialog.Left=-32000;dialog.Top=-32000;dialog.UpdateLayout();var labels=Children(dialog).OfType<Button>().Select(b=>b.Content as string).ToArray();Check(labels.Contains("Danger")&&labels.Contains("Loot")&&labels.Contains("Pick me up")&&!labels.Contains("Save current map preset")&&labels.Contains("Routes")&&labels.Contains("Map presets"),"Squad panel groups pings and sharing with navigation to routes and map presets");var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(dialog);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"squad-tools.png")))encoder.Save(file);inspected=true;dialog.Close(); }catch { foreach(var w in Application.Current.Windows.OfType<Window>().ToArray())if(w!=tactical)w.Close();throw; }
            };timer.Start();Call(runtime,"ShowSquadTools");Check(inspected,"Actual squad panel rendered and closed successfully");
            bool pingInspected=false;var pingTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};var previousUpdate=Get(engine,"updated");
            Set(engine,"diagnosticMode",false);Set(engine,"updated",DateTime.UtcNow);
            pingTimer.Tick+=(s,e)=> { pingTimer.Stop();var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Tag as string=="squad-tools");try {
                var danger=Children(dialog).OfType<Button>().Single(b=>b.Tag as string=="squad-ping-0");Check(danger.IsEnabled,"An actual connected Squad dialog enables explicit pings from a fresh captured position");danger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((Array)Call(squad,"Local")).Cast<object>().Any(m=>((string)Get(m,"Id")).StartsWith("50494E47"))&&(bool)Get(engine,"panelOpening")&&(bool)Get(engine,"shortcutCaptureOpen"),"An actual Squad ping is created while game input and coordinate capture remain protected");pingInspected=true;
            }finally {dialog.Close();} };
            try {pingTimer.Start();Call(runtime,"ShowSquadTools");Check(pingInspected,"Explicit ping completes through the real modal path");}finally {pingTimer.Stop();Set(engine,"diagnosticMode",true);Set(engine,"updated",previousUpdate);}
            trayAdmin.PerformClick();Pump();
            Check(((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Tray Exit regression starts with admin commands still open");
            ((System.Windows.Forms.ContextMenuStrip)Get(engine,"trayMenu")).Items.Cast<System.Windows.Forms.ToolStripItem>().Last().PerformClick();
            Check((bool)Get(engine,"closing")&&!((System.Windows.Forms.Form)Get(engine,"adminCommandsPanel")).Visible,"Actual tray Exit closes admin windows without requiring a separate close");
            var exitingApplication=Application.Current;exitingApplication.Run();
            Check(exitingApplication.Dispatcher.HasShutdownFinished,"Tray Exit finishes the WPF application loop while admin controls were open");
        }finally { type.GetField("Active",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);Set(runtime,"privatePositions",null);if(friend!=null)friend.Dispose();if(host!=null)host.Dispose();((IDisposable)runtime).Dispose(); }
    }
}
