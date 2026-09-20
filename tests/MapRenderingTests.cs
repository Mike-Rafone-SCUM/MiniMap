using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using ScumMiniMap;

static class MapRenderingTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
    [STAThread] static void Main(string[] args) {
        Check(Program.DataFolderName==(Program.IsTestBuild?"ScumMiniMap-ResponsivenessTest":"ScumMiniMap"),"Build uses the appropriate data folder");
        Check(Program.TrackingIntervalMs(250)==250 && Program.TrackingIntervalMs(700)==700,"Fast tracking cadence has no stationary slowdown and respects slower settings");
        Check(Program.UpgradeCopyInterval(1000,false)==250 && Program.UpgradeCopyInterval(1000,true)==1000 &&
            Program.UpgradeCopyInterval(3000,false)==3000,"Previous default upgrades once without replacing slower custom intervals");
        var motion=new MapMotion();
        motion.Sample(new PointF(.5f,.5f),359,0,false);
        motion.Sample(new PointF(.501f,.5f),1,1,false);
        Check(Math.Abs(motion.Yaw-361)<.01,"Heading updates immediately at sample receipt");
        motion.Advance(1.09);
        Check(motion.Point.X>.5f && motion.Point.X<.501f && Math.Abs(motion.Yaw-361)<.01,"Position interpolates while heading already matches the latest sample");
        motion.Advance(1.18);
        Check(Math.Abs(motion.Point.X-.501)<.000001,"One-second samples settle within 180 ms");
        motion.Advance(9);
        Check(Math.Abs(motion.Point.X-.501)<.000001,"No extrapolation beyond the latest observed position");
        using(var window=new MapWindow(args[0],true)) {
            var overlay=(OverlayWindow)typeof(MapWindow).GetField("overlay",Hidden).GetValue(window);
            var marker=(MapMotion)typeof(MapWindow).GetField("motion",Hidden).GetValue(window);
            var draw=(Func<Bitmap>)Delegate.CreateDelegate(typeof(Func<Bitmap>),window,typeof(MapWindow).GetMethod("OverlayBitmap",Hidden));
            foreach(string flag in new[]{"autoZoom","showScumMap","showHeading","showCompass","showStatus","gridLabels","gridBorders","edgeFade"}) Set(window,flag,false);
            Set(window,"zones",new List<MapZone>()); Set(window,"mapOpacity",100); Set(window,"overlayShape","Square");
            Set(window,"zoom",8f); Set(window,"position",new Position { X=-143091,Y=-142091 });
            // Cover the original buffer dimensions as well as the new compact default.
            foreach(Size size in new[]{new Size(400,240),new Size(240,240),new Size(600,600)}) {
                overlay.SetMinimapSize(size); marker.Point=new PointF(.5f,.5f); Set(window,"terrainKey",null);
                draw(); int builds=window.TerrainBuilds;
                marker.Point=new PointF(.5f+12f/(Math.Min(size.Width,size.Height)*8),.5f);
                using(var reused=(Bitmap)draw().Clone()) {
                    Check(window.TerrainBuilds==builds,"Small camera movement reuses terrain at "+size);
                    Set(window,"terrainKey",null);
                    Bitmap rebuilt=draw(); long error=0; int samples=0;
                    for(int y=8;y<reused.Height-8;y+=3) for(int x=8;x<reused.Width-8;x+=3) {
                        Color a=reused.GetPixel(x,y),b=rebuilt.GetPixel(x,y);
                        error+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B); samples+=3;
                    }
                    Check(error/(double)samples<3.0,"Cached camera translation aligns with a fresh render at "+size);
                }
                builds=window.TerrainBuilds;
                marker.Point=new PointF(.65f,.65f); draw();
                Check(window.TerrainBuilds==builds+1,"Movement beyond cached coverage rebuilds terrain at "+size);
                builds=window.TerrainBuilds; Set(window,"zoom",9f); draw();
                Check(window.TerrainBuilds==builds+1,"Zoom invalidates cached terrain at "+size);
                Set(window,"zoom",8f);
            }
            Set(window,"fullMapActive",true); Set(window,"fullMapZoom",4f);
            Set(window,"fullMapPan",new PointF(.5f,.5f)); Set(window,"terrainKey",null);
            draw(); int panBuilds=window.TerrainBuilds;
            Set(window,"draggingMap",true); Set(window,"dragStart",new Point(100,100));
            Set(window,"panStart",new PointF(.5f,.5f));
            typeof(MapWindow).GetMethod("HandleFullMapMouseMove",Hidden).Invoke(window,new object[]{new Point(110,100),System.Windows.Forms.MouseButtons.Left});
            draw();
            Check(window.TerrainBuilds==panBuilds,"Full-map drag reuses terrain for small pans");
            Set(window,"gridBorders",true); draw();
            Check(window.TerrainBuilds==panBuilds+1,"Layer changes invalidate full-map terrain immediately");
        }
    }
}
