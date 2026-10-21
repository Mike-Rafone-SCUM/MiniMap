using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Windows.Threading;

class MapStreamingTests {
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static object Get(object o,string n) { var t=o.GetType();var f=t.GetField(n,F);return f!=null?f.GetValue(o):t.GetProperty(n,F).GetValue(o,null); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,F).SetValue(o,v); }
    static object Call(object o,string n,params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static object Static(Type t,string n,params object[] a) { return t.GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,a); }
    static void Check(bool v,string message) { if(!v)throw new Exception(message);Console.WriteLine("PASS: "+message); }
    static void Pump() { var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>frame.Continue=false),DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);System.Threading.Thread.Sleep(5); }
    static void Settle(object surface) {
        var timer=Stopwatch.StartNew();do { Draw(surface);Pump();if((bool)Get(surface,"PoiViewportReady")&&!(bool)Get(surface,"poiBatchRunning"))return; }while(timer.ElapsedMilliseconds<15000);
        throw new Exception("Visible POI stream did not settle");
    }
    static void Draw(object surface) { var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())Call(surface,"OnRender",dc); }
    static byte[] Pixels(DrawingVisual visual,int width,int height) { var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var bytes=new byte[width*height*4];bitmap.CopyPixels(bytes,width*4,0);return bytes; }
    static void MeasureGroup(Assembly assembly,object engine) {
        var state=Call(engine,"WpfSnapshot",true);var store=Get(state,"Store");Call(store,"SetAllEnabled",true);Set(store,"MasterEnabled",true);Set(state,"Opacity",0);Set(state,"PoiClustering",false);Set(state,"Zoom",8f);Set(state,"RotateMap",false);Set(state,"Pan",new System.Drawing.PointF(.5f,.5f));
        var peerType=assembly.GetType("ScumMiniMap.PrivatePositions+Peer");var markerType=assembly.GetType("ScumMiniMap.SharedMapMarker");var peers=Array.CreateInstance(peerType,16);var markers=Array.CreateInstance(markerType,1024);
        for(int i=0;i<16;i++) {var peer=Activator.CreateInstance(peerType,true);Set(peer,"Id",(i+1).ToString("x32"));Set(peer,"Name","Synthetic member "+i);Set(peer,"Position",Get(state,"Position"));peers.SetValue(peer,i);}
        for(int i=0;i<1024;i++) {var marker=Activator.CreateInstance(markerType,true);Set(marker,"Id",(i+1).ToString("x64"));Set(marker,"OwnerId",(i/64+1).ToString("x32"));Set(marker,"Owner","Synthetic member "+i/64);Set(marker,"Name","Camp "+i);Set(marker,"X",(i%32+.5)/32);Set(marker,"Y",(i/32+.5)/32);markers.SetValue(marker,i);}
        Set(state,"Peers",peers);Set(state,"SharedMarkers",markers);
        foreach(bool compact in new[]{false,true}) {
            var surface=(FrameworkElement)Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfMapSurface"),true);Set(surface,"State",state);Set(surface,"Compact",compact);double width=compact?300:800,height=compact?300:600;surface.Measure(new Size(width,height));surface.Arrange(new Rect(0,0,width,height));
            for(int i=0;i<80;i++) {Draw(surface);Pump();}
            var samples=new double[120];for(int i=0;i<samples.Length;i++) {Set(Get(state,"Position"),"Yaw",(double)i);var watch=Stopwatch.StartNew();Call(surface,"PublishState",state);Draw(surface);watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;}
            Array.Sort(samples);Console.WriteLine("GROUP "+(compact?"HUD":"Tactical")+" 16 players/1024 waypoints: median="+samples[60].ToString("F3")+" ms; p95="+samples[114].ToString("F3")+" ms");
            if(surface.GetType().GetMethod("ResetPoiStream",F)!=null)Call(surface,"ResetPoiStream");
        }
    }
    [STAThread]static void Main(string[] args) {
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var runtime=Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{Path.GetFullPath(args[1]),true},null);
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");
            if(Environment.GetEnvironmentVariable("MINIMAP_STREAM_BENCHMARK")=="1") {MeasureGroup(assembly,engine);return;}
            var state=Call(engine,"WpfSnapshot",true);var store=Get(state,"Store");Call(store,"SetAllEnabled",true);Set(store,"MasterEnabled",true);
            Set(state,"Saved",Array.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"),0));Set(state,"Trail",new System.Drawing.PointF[0]);Set(state,"Opacity",0);Set(state,"Zones",false);Set(state,"Labels",false);Set(state,"History",false);Set(state,"Grid",false);Set(state,"GridLabels",false);Set(state,"PoiClustering",false);Set(state,"Zoom",16f);Set(state,"Pan",new System.Drawing.PointF(.5f,.5f));
            var surface=(FrameworkElement)Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfMapSurface"),true);Set(surface,"State",state);surface.Measure(new Size(800,600));surface.Arrange(new Rect(0,0,800,600));
            Settle(surface);var type=surface.GetType();var all=((IEnumerable)Static(type,"PoiClusters",state,800d,600d,false)).Cast<object>().ToArray();
            var visible=((IEnumerable)Static(type,"VisiblePoiClusters",state,800d,600d,false)).Cast<object>().ToArray();
            Check(visible.Length>0&&visible.Length<all.Length/4,"High zoom queries a small spatial subset of the full POI catalogue");
            Check(all.Count(c=>Get(c,"Icon")!=null)<all.Length/4,"Cold off-screen POIs have no prepared icon assets");
            double side=(double)Static(type,"MapSide",state,800d,600d,false);var centre=(System.Drawing.PointF)Static(type,"CameraCentre",state,800d,600d,false);double left=400-side*centre.X,top=300-side*centre.Y;
            Func<bool,byte[]> render=streamed=> {
                var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) { dc.PushClip(new RectangleGeometry(new Rect(0,0,800,600)));
                    if(streamed)Call(surface,"DrawRetainedPois",dc,side,left,top,800d,600d);
                    else {
                        dc.PushTransform(new TranslateTransform(left,top));double span=(double)Get(surface,"streamedPoiSpan");
                        foreach(var cell in all.GroupBy(cluster=> {var p=(System.Drawing.PointF)Get(cluster,"Point");return new Point(Math.Floor(p.X*side/span),Math.Floor(p.Y*side/span));}).OrderBy(cell=>cell.Key.Y).ThenBy(cell=>cell.Key.X))foreach(var cluster in cell) {
                            var p=(System.Drawing.PointF)Get(cluster,"Point");var members=((IEnumerable)Get(cluster,"Members")).Cast<object>().ToArray();var categoryMap=(IDictionary)Get(store,"CategoryById");var category=categoryMap[Get(members[0],"CategoryId")];Call(surface,"DrawPoi",dc,new Point(p.X*side,p.Y*side),category,cluster);
                        }
                        dc.Pop();
                    }
                    dc.Pop(); }
                return Pixels(visual,800,600);
            };
            Check(render(true).SequenceEqual(render(false)),"Streamed vector tiles preserve every visible POI pixel, including cell and viewport edges");
            var route=Enumerable.Range(0,8193).Select(i=>new System.Drawing.PointF(i/8192f,.5f+(float)Math.Sin(i*.01)*.005f)).ToArray();
            Func<System.Drawing.PointF,Point> project=p=>new Point(left+p.X*side,top+p.Y*side);
            Func<bool,byte[]> routePixels=streamed=> {
                var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {dc.PushClip(new RectangleGeometry(new Rect(0,0,800,600)));
                    if(streamed)Call(surface,"DrawStreamedRoute",dc,route,Brushes.Lime,true,project);
                    else Static(type,"DrawLine",dc,route.Select(project),new Pen(Brushes.Lime,4));dc.Pop();}
                return Pixels(visual,800,600);
            };
            Set(surface,"RouteVerticesDrawn",0);Check(routePixels(true).SequenceEqual(routePixels(false)),"Culled route chunks preserve the complete route's visible pixels");
            Check((int)Get(surface,"RouteVerticesDrawn")>0&&(int)Get(surface,"RouteVerticesDrawn")<route.Length/4,"Long routes project only viewport-intersecting chunks");
            foreach(double size in new[]{512d,800d,1024d,1600d,2048d,4096d}) {
                double density=(double)Static(type,"PoiRasterDensity",size,size,2d),span=Math.Max(512,Math.Ceiling(size/2048)*512),cells=Math.Ceiling((size+48)/span)+1,pixels=Math.Ceiling((span+48)*density);
                Check(cells*cells*pixels*pixels<=16*1024*1024,"A dense square viewport at 200% DPI fits the raster cache budget: "+size);
            }
            foreach(float x in new[]{.08f,.25f,.45f,.65f,.85f,.95f,.5f}) {Set(state,"Pan",new System.Drawing.PointF(x,.5f));Settle(surface);Check((int)Get(surface,"PoiCachedTiles")<=48&&(int)Get(surface,"PoiCachedPixels")<=16*1024*1024,"Panning keeps the tile and pixel cache bounded at X="+x);}
            int count=(int)Get(surface,"PoiTilesBuilt");Settle(surface);Check((int)Get(surface,"PoiTilesBuilt")==count,"Revisiting a warm viewport performs no new POI preparation");
            // A synthetic group changes heading sixty times without changing camera.
            var peer=Activator.CreateInstance(assembly.GetType("ScumMiniMap.PrivatePositions+Peer"),true);Set(peer,"Id","0123456789abcdef0123456789abcdef");Set(peer,"Name","Synthetic member");Set(peer,"Position",Get(state,"Position"));var peers=Array.CreateInstance(peer.GetType(),1);peers.SetValue(peer,0);Set(state,"Peers",peers);
            var marker=Activator.CreateInstance(assembly.GetType("ScumMiniMap.SharedMapMarker"),true);Set(marker,"Id","fixture");Set(marker,"OwnerId","0123456789abcdef0123456789abcdef");Set(marker,"Owner","Synthetic member");Set(marker,"Name","Saved camp");Set(marker,"X",.5d);Set(marker,"Y",.5d);var markers=Array.CreateInstance(marker.GetType(),1);markers.SetValue(marker,0);Set(state,"SharedMarkers",markers);
            foreach(bool compact in new[]{false,true}) {
                Set(surface,"Compact",compact);Set(state,"RotateMap",false);Set(state,"Zoom",compact?4f:16f);Call(surface,"PublishState",state);Draw(surface);
                if(!compact)Settle(surface);
                int terrain=(int)Get(surface,"TerrainLayerBuilds"),annotations=(int)Get(surface,"AnnotationLayerBuilds"),shared=(int)Get(surface,"SharedLayerBuilds");
                var watch=Stopwatch.StartNew();for(int i=0;i<60;i++) {Set(Get(state,"Position"),"Yaw",(double)i);Call(surface,"PublishState",state);Draw(surface);}watch.Stop();
                Console.WriteLine((compact?"HUD":"Tactical")+" group heading update: "+(watch.Elapsed.TotalMilliseconds/60).ToString("F3")+" ms/frame");
                Check((int)Get(surface,"TerrainLayerBuilds")==terrain&&(int)Get(surface,"AnnotationLayerBuilds")==annotations&&(int)Get(surface,"SharedLayerBuilds")==shared,"Group heading updates retain terrain, POIs, waypoints and routes: compact="+compact);
                Set(marker,"Name","Renamed camp");Call(surface,"PublishState",state);Draw(surface);Check((int)Get(surface,"SharedLayerBuilds")==shared+1,"Renaming a shared marker refreshes its layer immediately: compact="+compact);Set(marker,"Name","Saved camp");
                Set(state,"SharedMarkers",Array.CreateInstance(marker.GetType(),0));Call(surface,"PublishState",state);Draw(surface);Check((int)Get(surface,"SharedLayerBuilds")==shared+2,"Withdrawn shared markers invalidate retained graphics: compact="+compact);Set(state,"SharedMarkers",markers);
            }
            Set(surface,"Compact",false);Set(state,"Zoom",2f);Draw(surface);int generation=(int)Get(surface,"PoiStreamResets");var first=all[0];int categoryId=(int)Get(((IEnumerable)Get(first,"Members")).Cast<object>().First(),"CategoryId");Call(store,"SetCategoryEnabled",categoryId,false);Draw(surface);Settle(surface);
            Check((int)Get(surface,"PoiStreamResets")>generation,"A filter change cancels the old stream before preparing replacement tiles");
            var filtered=((IEnumerable)Static(type,"PoiClusters",state,800d,600d,false)).Cast<object>().SelectMany(c=>((IEnumerable)Get(c,"Members")).Cast<object>());Check(filtered.All(m=>(int)Get(m,"CategoryId")!=categoryId),"Late POI completion cannot restore a disabled category");
            Call(surface,"ResetPoiStream");Pump();Check((int)Get(surface,"PoiCachedTiles")==0,"Unloading releases retained POI tiles and cancels outstanding work");
        }finally {((IDisposable)runtime).Dispose();}
    }
}
