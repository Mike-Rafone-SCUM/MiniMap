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
            Set(window,"locationHistory",true);
            var recordHistory=typeof(MapWindow).GetMethod("RecordLocationHistory",Hidden);
            DateTime sampledAt=DateTime.UtcNow.AddMinutes(-30);
            for(int i=0;i<1800;i++)
                recordHistory.Invoke(window,new object[]{new PointF(.2f+i*.0003f,.45f+(float)Math.Sin(i*.02)*.08f),sampledAt.AddSeconds(i)});
            overlay.SetMinimapSize(new Size(600,600));
            Set(window,"fullMapActive",true); Set(window,"fullMapZoom",1f);
            for(int i=0;i<12;i++) draw();
            double[] historyElapsed=new double[180];
            for(int i=0;i<historyElapsed.Length;i++) {
                motion.Yaw=45+i*.25;
                var clock=Stopwatch.StartNew(); draw(); clock.Stop();
                historyElapsed[i]=clock.Elapsed.TotalMilliseconds;
            }
            Array.Sort(historyElapsed);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"600_full_history,{0},{1:F3},{2:F3},{3}",historyElapsed.Length,historyElapsed[historyElapsed.Length/2],historyElapsed[(int)(historyElapsed.Length*.95)],0));
            Set(window,"fullMapZoom",4f); Set(window,"locationHistory",false);
            Set(window,"terrainKey",null); draw();
            Set(window,"draggingMap",true);
            double[] panElapsed=new double[60];
            int terrainBeforePan=window.TerrainBuilds;
            for(int i=0;i<panElapsed.Length;i++) {
                Set(window,"fullMapPan",new PointF(.30f+i*.007f,.5f));
                var clock=Stopwatch.StartNew(); draw(); clock.Stop();
                panElapsed[i]=clock.Elapsed.TotalMilliseconds;
            }
            Array.Sort(panElapsed);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"600_full_pan,{0},{1:F3},{2:F3},{3}",panElapsed.Length,panElapsed[panElapsed.Length/2],panElapsed[(int)(panElapsed.Length*.95)],window.TerrainBuilds-terrainBeforePan));
            Set(window,"draggingMap",false); Set(window,"terrainKey",null);
            var settleClock=Stopwatch.StartNew(); draw(); settleClock.Stop();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"600_full_pan_settle,1,{0:F3},{0:F3},1",settleClock.Elapsed.TotalMilliseconds));
        }
    }
}
