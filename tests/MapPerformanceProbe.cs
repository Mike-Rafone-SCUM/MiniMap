using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using ScumMiniMap;

static class MapPerformanceProbe {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    [STAThread] static void Main(string[] args) {
        Console.WriteLine("scenario,frames,median_ms,p95_ms,terrain_builds");
        using(var window=new MapWindow(args[0],true)) {
            Set(window,"position",new Position { X=-143091,Y=-142091,Yaw=45 });
            Set(window,"autoZoom",false); Set(window,"zoom",16f);
            var overlay=(OverlayWindow)typeof(MapWindow).GetField("overlay",Hidden).GetValue(window);
            var motion=(MapMotion)typeof(MapWindow).GetField("motion",Hidden).GetValue(window);
            var draw=(Func<Bitmap>)Delegate.CreateDelegate(typeof(Func<Bitmap>),window,typeof(MapWindow).GetMethod("OverlayBitmap",Hidden));
            foreach(int size in new[]{240,600}) {
                overlay.SetMinimapSize(new Size(size,size));
                foreach(bool moving in new[]{false,true}) {
                    motion.Point=new PointF(.5f,.5f); motion.Yaw=45;
                    for(int i=0;i<12;i++) draw();
                    int before=window.TerrainBuilds;
                    double[] elapsed=new double[180];
                    for(int i=0;i<elapsed.Length;i++) {
                        if(moving) motion.Point=new PointF(.5f+i*.00004f,.5f+i*.00002f);
                        motion.Yaw=45+i*.25;
                        var clock=Stopwatch.StartNew(); draw(); clock.Stop();
                        elapsed[i]=clock.Elapsed.TotalMilliseconds;
                    }
                    Array.Sort(elapsed);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0}_{1},{2},{3:F3},{4:F3},{5}",size,moving?"moving":"heading",elapsed.Length,elapsed[elapsed.Length/2],elapsed[(int)(elapsed.Length*.95)],window.TerrainBuilds-before));
                }
            }
        }
    }
}
