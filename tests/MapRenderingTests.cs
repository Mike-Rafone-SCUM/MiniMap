using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Linq;
using ScumMiniMap;

static class MapRenderingTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
    static void CheckMapSidecars(string root) {
        string folder=Path.Combine(root,"sidecars");
        Directory.CreateDirectory(folder);
        string mapA=Path.Combine(folder,"map-a.png"),mapACopy=Path.Combine(folder,"copy.png"),mapB=Path.Combine(folder,"map-b.png");
        File.WriteAllBytes(mapA,new byte[]{1,2,3,4});
        File.Copy(mapA,mapACopy);
        File.WriteAllBytes(mapB,new byte[]{4,3,2,1});
        MapSidecarPaths pathsA=MapSidecarPaths.ForMap(folder,mapA);
        MapSidecarPaths pathsSame=MapSidecarPaths.ForMap(folder,mapACopy);
        MapSidecarPaths pathsB=MapSidecarPaths.ForMap(folder,mapB);
        MapSidecarPaths pathsDefault=MapSidecarPaths.ForMap(folder,null);
        Check(pathsA.MapId==pathsSame.MapId && pathsA.MapId!=pathsB.MapId && pathsDefault.MapId=="default" && pathsA.MapId!=pathsDefault.MapId,
            "Each custom map image receives a stable, isolated sidecar identity");
        Check(Path.GetFileName(pathsA.ZonesPath)=="zones.tsv" && Path.GetFileName(pathsA.WaypointsPath)=="customwaypoints.tsv",
            "Map sidecars use separate zones.tsv and customwaypoints.tsv files");

        string legacy=Path.Combine(folder,"zones.tsv");
        var oldPolygon=new MapZone { Name="Old polygon",Argb=Color.Red.ToArgb(),Category=ZoneCategory.Custom,Points=new[]{new PointF(.1f,.1f),new PointF(.3f,.1f),new PointF(.2f,.3f)} };
        var oldWaypoint=new MapZone { Name="Old waypoint",Argb=Color.Cyan.ToArgb(),Category=ZoneCategory.Custom,Points=new[]{new PointF(.7f,.8f)} };
        ZoneStore.Save(legacy,new List<MapZone>{oldPolygon,oldWaypoint});
        string legacyContents=File.ReadAllText(legacy);
        bool migrated;
        List<MapZone> loadedA=MapSidecarStore.Load(pathsA,legacy,false,out migrated);
        Check(migrated && loadedA.Count==2 && loadedA.Any(z=>z.Name=="Old polygon") && loadedA.Any(z=>z.Name=="Old waypoint"),
            "Legacy zones migrate to the active map without losing annotations");
        Check(ZoneStore.Load(pathsA.ZonesPath).Count==1 && ZoneStore.Load(pathsA.WaypointsPath).Count==1 && File.ReadAllText(legacy)==legacyContents,
            "Migration separates old waypoints and preserves the original zones file");

        List<MapZone> loadedB=MapSidecarStore.Load(pathsB,legacy,false,out migrated);
        Check(!migrated && loadedB.Count==0,"Legacy annotations are not copied into a different custom map");
        var newPolygon=new MapZone { Name="Map B polygon",Argb=Color.Blue.ToArgb(),Category=ZoneCategory.Custom,Points=new[]{new PointF(.2f,.2f),new PointF(.4f,.2f),new PointF(.3f,.4f)} };
        var newWaypoint=new MapZone { Name="Map B waypoint",Argb=Color.Cyan.ToArgb(),Category=ZoneCategory.Custom,Points=new[]{new PointF(.8f,.7f)} };
        MapSidecarStore.Save(pathsB,new List<MapZone>{newPolygon,newWaypoint});
        Check(MapSidecarStore.Reload(pathsA).Any(z=>z.Name=="Old waypoint") && MapSidecarStore.Reload(pathsB).Any(z=>z.Name=="Map B waypoint") &&
            MapSidecarStore.Reload(pathsB).All(z=>z.Name!="Old waypoint"),"Saving and reloading map B cannot overwrite or mix map A annotations");
    }
    static byte[] SolidTile(int width) {
        using(var tile=new Bitmap(width,512))
        using(var graphics=Graphics.FromImage(tile))
        using(var data=new MemoryStream()) {
            graphics.Clear(Color.FromArgb(40,100,160));
            tile.Save(data,System.Drawing.Imaging.ImageFormat.Png);
            return data.ToArray();
        }
    }
    static MemoryStream SeamFixture() {
        byte[] first=SolidTile(514),second=SolidTile(514);
        var stream=new MemoryStream();
        using(var writer=new BinaryWriter(stream,System.Text.Encoding.ASCII,true)) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("MTL2"));
            writer.Write(512); writer.Write(1);
            writer.Write(1024); writer.Write(512); writer.Write(2); writer.Write(1);
            long start=12+16+24;
            writer.Write(start); writer.Write(first.Length);
            writer.Write(start+first.Length); writer.Write(second.Length);
            writer.Write(first); writer.Write(second);
        }
        stream.Position=0; return stream;
    }
    [STAThread] static void Main(string[] args) {
        Check(Program.DataFolderName==(Program.IsTestBuild?"ScumMiniMap-ResponsivenessTest":"ScumMiniMap"),"Build uses the appropriate data folder");
        Check(Native.CursorBlocksCopy(true,false) && !Native.CursorBlocksCopy(true,true) && !Native.CursorBlocksCopy(false,false),"Visible cursor blocks ordinary menus but permits full-map tracking");
        PointF edge,direction;
        foreach(bool circle in new[]{true,false}) foreach(PointF target in new[]{new PointF(1,0),new PointF(-1,0),new PointF(0,1),new PointF(0,-1),new PointF(1,1)}) {
            RectangleF ring=new RectangleF(0,28,240,212);
            Check(MapWindow.TryDestinationEdge(ring,circle,PointF.Empty,target,out edge,out direction) && ring.Contains(edge) &&
                Math.Sign(edge.X-120)==Math.Sign(target.X) && Math.Sign(edge.Y-134)==Math.Sign(target.Y),"Destination edge follows map bearing within "+(circle?"circular":"rectangular")+" bounds: "+target);
        }
        Check(!MapWindow.TryDestinationEdge(new RectangleF(0,0,240,240),true,PointF.Empty,PointF.Empty,out edge,out direction),"No arbitrary direction when player and destination coincide");
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
        using(var fixture=SeamFixture())
        using(var tiles=new MapTilePyramid(fixture))
        using(var frame=new Bitmap(777,777)) {
            for(int mode=0;mode<3;mode++) {
                var interpolation=mode==1?System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor:System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                using(var graphics=Graphics.FromImage(frame)) {
                    graphics.Clear(Color.Black);
                    graphics.InterpolationMode=interpolation;
                    tiles.Draw(graphics,new RectangleF(0,0,777,777),0,0,777,.75f,mode==2);
                }
                bool solid=true;
                for(int x=385;x<=392;x++) {
                    Color pixel=frame.GetPixel(x,200);
                    if(Math.Abs(pixel.R-40)>3 || Math.Abs(pixel.G-100)>3 || Math.Abs(pixel.B-160)>3) solid=false;
                }
                Check(solid,"Scaled tile joins have no dark gaps with "+(mode==2?"cached bilinear":interpolation.ToString()));
            }
        }
        using(var tileStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("map-tiles.bin"))
        using(var tiles=new MapTilePyramid(tileStream))
        using(var tileFrame=new Bitmap(600,600))
        using(var overview=tiles.Overview()) {
            using(var graphics=Graphics.FromImage(tileFrame)) tiles.Draw(graphics,new RectangleF(0,0,600,600),0,0,600);
            Check(overview.Width>=768 && overview.Width<=1024 && tileFrame.GetPixel(300,300).A>0,"Tile pyramid renders the bundled map without decoding the full image");
        }
        string testFolder=Path.Combine(Path.GetTempPath(),"MiniMap-tile-render-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testFolder);
        CheckMapSidecars(testFolder);
        string customMap=Path.Combine(testFolder,"map.png");
        File.WriteAllText(customMap,"invalid image");
        using(var recovery=new MapWindow(testFolder,true))
            Check(typeof(MapWindow).GetField("mapTiles",Hidden).GetValue(recovery)!=null,"Invalid custom map falls back to bundled tiles");
        File.Delete(customMap);
        using(var window=new MapWindow(testFolder,true)) {
            Check(typeof(MapWindow).GetField("mapTiles",Hidden).GetValue(window)!=null,"Bundled map uses tiles in the application renderer");
            Set(window,"locationHistory",true);
            Set(window,"locationHistoryTimestamps",true);
            var recordHistory=typeof(MapWindow).GetMethod("RecordLocationHistory",Hidden);
            var drawHistory=typeof(MapWindow).GetMethod("DrawLocationHistory",Hidden);
            DateTime historyTime=DateTime.UtcNow.AddMinutes(-2);
            recordHistory.Invoke(window,new object[]{new PointF(.1f,.1f),historyTime});
            recordHistory.Invoke(window,new object[]{new PointF(.2f,.1f),historyTime.AddSeconds(30)});
            using(var plain=new Bitmap(200,200))
            using(var hovered=new Bitmap(200,200)) {
                Set(window,"locationHistoryHoverPoint",new Point(30,20));
                Set(window,"fullMapActive",false);
                using(var g=Graphics.FromImage(plain)) drawHistory.Invoke(window,new object[]{g,0f,0f,200f});
                Set(window,"fullMapActive",true);
                using(var g=Graphics.FromImage(hovered)) drawHistory.Invoke(window,new object[]{g,0f,0f,200f});
                bool hoverLabelVisible=false;
                for(int y=0;y<200;y++) for(int x=0;x<200;x++)
                    if(plain.GetPixel(x,y)!=hovered.GetPixel(x,y)) hoverLabelVisible=true;
                Check(hoverLabelVisible,"History age appears on the full map when the trail is hovered; hover="+
                    typeof(MapWindow).GetField("locationHistoryHoverPoint",Hidden).GetValue(window)+", segment="+
                    typeof(MapWindow).GetField("displayedLocationHistorySegment",Hidden).GetValue(window));
                Set(window,"locationHistoryHoverPoint",null);
                using(var g=Graphics.FromImage(hovered)) {
                    g.Clear(Color.Transparent);
                    drawHistory.Invoke(window,new object[]{g,0f,0f,200f});
                }
                bool noUnhoveredLabel=true;
                for(int y=0;y<200;y++) for(int x=0;x<200;x++)
                    if(plain.GetPixel(x,y)!=hovered.GetPixel(x,y)) noUnhoveredLabel=false;
                Check(noUnhoveredLabel,"History age stays hidden on the minimap and unhovered full map");
            }
            var history=(System.Collections.IList)typeof(MapWindow).GetField("locationHistoryPoints",Hidden).GetValue(window);
            Set(window,"locationHistoryMinutes",1);
            typeof(MapWindow).GetMethod("PruneLocationHistory",Hidden).Invoke(window,new object[]{DateTime.UtcNow});
            Check(history.Count==0,"Shorter history duration expires old trail points without new movement");
            recordHistory.Invoke(window,new object[]{new PointF(.1f,.1f),DateTime.UtcNow});
            recordHistory.Invoke(window,new object[]{new PointF(.2f,.1f),DateTime.UtcNow.AddSeconds(2)});
            typeof(MapWindow).GetMethod("ClearLocationHistory",Hidden).Invoke(window,null);
            Check(history.Count==0 && typeof(MapWindow).GetField("locationHistoryPath",Hidden).GetValue(window)==null,
                "Clear route history removes points and cached drawing");
            recordHistory.Invoke(window,new object[]{new PointF(.3f,.1f),DateTime.UtcNow.AddSeconds(3)});
            Check(history.Count==1,"Tracking starts a fresh trail after clearing");
            Set(window,"fullMapActive",false);
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
            Set(window,"draggingMap",false); Set(window,"fullMapPan",new PointF(.5f,.5f));
            Set(window,"playerConeColor",Color.Magenta);
            window.Accept("{X=-143091 Y=-143191 Z=10|P=0 Y=20 R=0}");
            marker.Advance(double.MaxValue);
            draw(); int stationaryTerrain=window.TerrainBuilds;
            string beforeKey=(string)typeof(MapWindow).GetMethod("OverlayKey",Hidden).Invoke(window,null);
            window.Accept("{X=-173523.36 Y=-143191 Z=10|P=0 Y=90 R=0}");
            marker.Advance(double.MaxValue);
            string afterKey=(string)typeof(MapWindow).GetMethod("OverlayKey",Hidden).Invoke(window,null);
            Bitmap moved=draw();
            Check(beforeKey!=afterKey && window.TerrainBuilds==stationaryTerrain,"Full-map position samples invalidate the frame without rebuilding stationary terrain");
            Check(moved.GetPixel(348,300).ToArgb()==Color.Magenta.ToArgb() && moved.GetPixel(300,300).ToArgb()!=Color.Magenta.ToArgb(),"Full-map player marker moves to the new position over cached terrain");
            Set(window,"fullMapActive",false); overlay.SetMinimapSize(new Size(240,240));
            Set(window,"overlayShape","Circle"); Set(window,"showStatus",true); Set(window,"statusPos","Below");
            Set(window,"edgeFade",true); Set(window,"routeGuidanceColor",Color.Cyan);
            Set(window,"searchTarget",new MapZone { Name="Destination",Points=new[]{new PointF(.8f,.5f)} });
            marker.Point=new PointF(.5f,.5f);
            Bitmap indicated=draw();
            Check(indicated.GetPixel(207,109).G>180 && indicated.GetPixel(207,109).B>180,"Destination edge arrow remains visible above the circular edge fade");
            indicated.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DestinationIndicatorPreview.png"));
            Set(window,"statusPos","Above");
            Set(window,"searchTarget",new MapZone { Name="Destination",Points=new[]{new PointF(.5f,.2f)} });
            Bitmap above=draw();
            Color indicatorPixel=above.GetPixel(123,46);
            Check(indicatorPixel.G>180 && indicatorPixel.B>180,"North destination indicator stays below an above-map status bar");
            Set(window,"searchTarget",null);
            Bitmap cleared=draw();
            Check(cleared.GetPixel(123,46).ToArgb()!=indicatorPixel.ToArgb(),"Clearing navigation removes its edge marker");
            using(var opaqueCircle=new Bitmap(100,100)) {
                using(var graphics=Graphics.FromImage(opaqueCircle)) graphics.Clear(Color.White);
                OverlayWindow.Fade(opaqueCircle,false,100,shape:"Circle");
                Check(opaqueCircle.GetPixel(0,0).A==0 && opaqueCircle.GetPixel(50,50).A==255,"Circular shape remains clipped with fade disabled at full opacity");
            }
        }
        Directory.Delete(testFolder,true);
    }
}
