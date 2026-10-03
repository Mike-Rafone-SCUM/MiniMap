using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Linq;
using ScumMiniMap;

static class MapRenderingTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr window,int index);
    static void CheckOverlayMouseInteraction() {
        using(var overlay=new OverlayWindow(()=>{},()=>{},z=>{},()=>{})) {
            IntPtr handle=overlay.Handle;
            Check((GetWindowLong(handle,-20)&0x20)!=0,"New overlay passes gameplay clicks through");
            overlay.SetGameFocus(false);
            Check((GetWindowLong(handle,-20)&0x20)==0,"Desktop compact map receives clicks for dragging and resizing");
            Point location=new Point(100,120);
            overlay.Location=location;
            Action<string,int,int> mouse=(name,x,y)=>typeof(System.Windows.Forms.Control).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,
                new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,x,y,0)});
            mouse("OnMouseDown",80,90);
            mouse("OnMouseMove",120,110);
            Check(overlay.Location==new Point(140,140),"Dragging compact map moves the real window by pointer displacement");
            mouse("OnMouseUp",80,90);
            mouse("OnMouseMove",150,160);
            Check(overlay.Location==new Point(140,140) && !overlay.Capture,"Releasing drag ends capture and stops window movement");
            mouse("OnMouseDown",80,90);
            overlay.SetGameFocus(true);
            mouse("OnMouseMove",150,160);
            Check(overlay.Location==new Point(140,140) && !overlay.Capture,"Gameplay focus cancels an active drag");
            overlay.SetGameFocus(false);
            location=overlay.Location;
            overlay.FullMapMode=true;
            Check((GetWindowLong(handle,-20)&0x20)==0,"Full map receives desktop pointer input");
            overlay.FullMapMode=false;
            Check(overlay.Location==location && (GetWindowLong(handle,-20)&0x20)==0,"Closing full map restores position and compact-map interaction");
            foreach(bool fullMap in new[]{false,true}) {
                overlay.FullMapMode=fullMap;
                overlay.SetGameFocus(true);
                Check(((GetWindowLong(handle,-20)&0x20)!=0)==!fullMap,"Only compact mode passes gameplay mouse input through, full map="+fullMap);
                Check((GetWindowLong(handle,-20)&0x8000000)!=0,"Map retains non-activating style, full map="+fullMap);
                overlay.SetGameFocus(false);
                Check((GetWindowLong(handle,-20)&0x20)==0,"Leaving gameplay restores mouse interaction, full map="+fullMap);
            }
        }
    }
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
    static void CheckDeathScreens(string resources) {
        CheckDeathRecording(resources);
        string path=Path.GetFullPath(Path.Combine(resources,"..","tests","fixtures","scum-death-screen.jpg"));
        using(var original=new Bitmap(path)) {
            Check(DeathBannerDetector.Matches(original),"Supplied full death-screen screenshot is recognised without calibration");
            using(var capture=original.Clone(new Rectangle(original.Width/10,0,original.Width*8/10,original.Height*8/10),System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                Check(DeathBannerDetector.Matches(capture),"Native capture region includes both death heading and respawn panel");
            foreach(Size size in new[]{new Size(1280,720),new Size(1920,1080),new Size(2560,1440)}) using(var scaled=new Bitmap(original,size))
                Check(DeathBannerDetector.Matches(scaled),"Death screen is recognised at "+size.Width+"x"+size.Height);
            using(var reference=new Bitmap(original,new Size(960,540))) {
                foreach(string text in new[]{"LOCAL MESSAGE","LOKALE MELDUNG","MESSAGE LOCAL","СООБЩЕНИЕ","本地提示","رسالة محلية"}) using(var sample=new Bitmap(reference)) {
                    using(var g=Graphics.FromImage(sample)) using(var heading=new Font("Segoe UI",28)) using(var label=new Font("Segoe UI",11)) {
                        g.FillRectangle(Brushes.Black,300,105,370,58);
                        using(var centered=new StringFormat { Alignment=StringAlignment.Center }) g.DrawString(text,heading,Brushes.Red,new PointF(480,111),centered);
                        for(int i=0;i<3;i++) using(var fill=new SolidBrush(Color.FromArgb(i==0?28:12,i==0?28:12,i==0?28:12))) {
                            g.FillRectangle(fill,378,217+i*29,201,25);
                            g.DrawString(text,label,i==0?Brushes.White:Brushes.DimGray,new PointF(395,219+i*29));
                        }
                    }
                    Check(DeathBannerDetector.Matches(sample),"Respawn layout is recognised with unrelated heading and labels: "+text);
                }
                using(var withMap=new Bitmap(960,540)) {
                    using(var g=Graphics.FromImage(withMap)) {
                        g.Clear(Color.Black);
                        g.DrawImage(reference,new Rectangle(140,100,370,235),new Rectangle(300,100,370,235),GraphicsUnit.Pixel);
                        g.FillRectangle(Brushes.DarkOliveGreen,600,70,350,380);
                        for(int i=0;i<7;i++) { g.DrawLine(Pens.Gray,600+i*50,70,600+i*50,450); g.DrawLine(Pens.Gray,600,70+i*50,950,70+i*50); }
                        using(var font=new Font("Segoe UI",18)) g.DrawString("MAP",font,Brushes.Red,710,120);
                    }
                    Check(DeathBannerDetector.Matches(withMap),"Death controls shifted left remain recognised beside simulated right-side map content");
                }
                using(var leftPanel=new Bitmap(reference)) {
                    using(var g=Graphics.FromImage(leftPanel)) {
                        g.FillRectangle(Brushes.Black,350,200,280,140);
                        g.DrawImage(reference,new Rectangle(190,200,280,140),new Rectangle(350,200,280,140),GraphicsUnit.Pixel);
                        g.FillRectangle(Brushes.DarkOliveGreen,650,70,290,380);
                    }
                    Check(DeathBannerDetector.Matches(leftPanel),"Centred heading can accompany respawn choices shifted left for a map");
                }
                foreach(string missing in new[]{"heading","panel","third row"}) using(var sample=new Bitmap(reference)) {
                    using(var g=Graphics.FromImage(sample)) {
                        if(missing=="heading") g.FillRectangle(Brushes.Black,300,105,370,58);
                        else if(missing=="panel") g.FillRectangle(Brushes.Black,350,200,280,110);
                        else g.FillRectangle(Brushes.Black,350,274,280,29);
                    }
                    Check(!DeathBannerDetector.Matches(sample),"Incomplete death layout is rejected: missing "+missing);
                }
                using(var solidHeading=new Bitmap(reference)) {
                    using(var g=Graphics.FromImage(solidHeading)) { g.FillRectangle(Brushes.Black,300,105,370,58); g.FillRectangle(Brushes.Red,338,118,285,32); }
                    Check(!DeathBannerDetector.Matches(solidHeading),"Solid red warning bar with menu rows is rejected");
                }
            }
        }
        using(var empty=new Bitmap(960,540)) Check(!DeathBannerDetector.Matches(empty),"Blank gameplay cannot place an automatic death marker");
    }
    static void CheckDeathRecording(string resources) {
        string folder=Path.GetFullPath(Path.Combine(resources,"..","tests","fixtures","death-recording"));
        string[] names={"fade-001.png","fade-004.png","fade-008.png","fade-016.png"};
        var samples=new List<bool>();
        for(int i=0;i<names.Length;i++) using(var frame=new Bitmap(Path.Combine(folder,names[i]))) {
            Check(DeathBannerDetector.Matches(frame)==(i>0),"Actual recording recognises gameplay, death onset, fade and outlined respawn selection: "+names[i]);
            using(var capture=frame.Clone(new Rectangle(frame.Width/10,0,frame.Width*8/10,frame.Height*8/10),System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                bool visible=DeathBannerDetector.Matches(capture);
                Check(visible==(i>0),"Live capture region recognises recorded frame: "+names[i]);
                samples.Add(visible);
            }
        }
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-death-replay-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using(var map=new Bitmap(64,64)) map.Save(Path.Combine(root,"map.png"));
            using(var window=new MapWindow(root,true)) {
                var deathPosition=new Position { X=12345,Y=54321,Z=100,Yaw=0 };
                DateTime lastSample=DateTime.UtcNow.AddSeconds(-95);
                Set(window,"position",deathPosition); Set(window,"updated",lastSample);
                Position snapshot=window.CaptureDeathMarkerPosition();
                Check(snapshot!=null && !Object.ReferenceEquals(snapshot,deathPosition),"Location older than 30 seconds remains available as an independent death snapshot");
                int saved=0;
                for(int i=0;i<samples.Count;i++) {
                    DateTime sampleTime=(DateTime)typeof(MapWindow).GetField("updated",Hidden).GetValue(window);
                    DeathMarkerOutcome outcome=window.ApplyDeathScreenSample(samples[i],window.CaptureDeathMarkerPosition(),sampleTime);
                    if(outcome==DeathMarkerOutcome.Saved) saved++;
                    if(i==1)Check(outcome==DeathMarkerOutcome.Saved,"First complete death layout saves immediately without a second matching frame");
                    if(i==1) {
                        Set(window,"position",new Position { X=-123456,Y=-54321,Z=100,Yaw=0 });
                        Set(window,"updated",DateTime.UtcNow);
                    }
                }
                string waypointPath=(string)typeof(MapWindow).GetField("customWaypointsPath",Hidden).GetValue(window);
                List<MapZone> markers=ZoneStore.Load(waypointPath).Where(z=>z.IsDeathMarker).ToList();
                Check(saved==1 && markers.Count==1 && markers[0].Points[0]==MapWindow.ToMap(deathPosition),
                    "Recorded death produces one persistent marker from the 95-second-old location before respawn updates");
                bool repeated=false;
                for(int i=0;i<10;i++) repeated|=window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)!=DeathMarkerOutcome.None;
                Check(!repeated,"Visible death screen does not create repeated markers");
                for(int i=0;i<13;i++)window.ApplyDeathScreenSample(false,null,DateTime.UtcNow);
                Check(window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.None,
                    "Intermittent misses for under ten seconds do not duplicate the death marker");
                MapZone death=markers[0];
                Check(death.DeathCreatedUtc.HasValue && death.Clone().DeathCreatedUtc==death.DeathCreatedUtc,
                    "Death timestamp persists through saving, loading and cloning");
                DateTime created=death.DeathCreatedUtc.Value;
                Check(death.DisplayNameAt(created.AddMinutes(12))==Localization.Get("DeathName")+" "+Localization.T("HistoryMinutesAgo",12)
                    && death.DisplayNameAt(created.AddHours(3))==Localization.Get("DeathName")+" "+Localization.T("HistoryHoursAgo",3),
                    "Death labels express elapsed minutes and hours instead of dates");
                var legacy=death.Clone();legacy.DeathCreatedUtc=null;
                legacy.Name="Death location "+created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
                Check(legacy.DeathTimeUtc.HasValue && legacy.DisplayNameAt(created.AddMinutes(12))==death.DisplayNameAt(created.AddMinutes(12)),
                    "Previous dated death markers display relative age without calibration");
                window.ClearReachedDeathMarkers(death.Centroid);
                Check(ZoneStore.Load(waypointPath).Any(z=>z.IsDeathMarker),"Death marker survives initial coordinates at the death location");
                var far=new PointF(death.Centroid.X+.01f,death.Centroid.Y);
                window.ClearReachedDeathMarkers(far);
                window.ClearReachedDeathMarkers(new PointF(death.Centroid.X+.003f,death.Centroid.Y));
                Check(ZoneStore.Load(waypointPath).Any(z=>z.IsDeathMarker),"Death marker remains while more than 25 metres away");
                var liveZones=(List<MapZone>)typeof(MapWindow).GetField("zones",Hidden).GetValue(window);
                liveZones.Add(new MapZone { Name="Keep normal waypoint",Category=ZoneCategory.Custom,Points=new[]{death.Centroid} });
                Set(window,"searchTarget",liveZones.First(z=>z.IsDeathMarker));
                window.ClearReachedDeathMarkers(death.Centroid);
                List<MapZone> cleared=ZoneStore.Load(waypointPath);
                Check(!cleared.Any(z=>z.IsDeathMarker) && cleared.Any(z=>z.Name=="Keep normal waypoint")
                    && typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window)==null,
                    "Returning to a death marker removes it persistently, clears its destination and keeps ordinary waypoints");
            }
            string missingFolder=Path.Combine(root,"missing-location"); Directory.CreateDirectory(missingFolder);
            using(var map=new Bitmap(64,64)) map.Save(Path.Combine(missingFolder,"map.png"));
            using(var emptyWindow=new MapWindow(missingFolder,true)) {
                Set(emptyWindow,"position",null);
                Check(emptyWindow.ApplyDeathScreenSample(true,emptyWindow.CaptureDeathMarkerPosition(),DateTime.MinValue)==DeathMarkerOutcome.MissingPosition
                    && emptyWindow.ApplyDeathScreenSample(true,emptyWindow.CaptureDeathMarkerPosition(),DateTime.MinValue)==DeathMarkerOutcome.None,
                    "Confirmed death without any coordinates reports missing location");
                Set(emptyWindow,"position",new Position { X=0,Y=0,Z=0,Yaw=0 });
                Check(emptyWindow.ApplyDeathScreenSample(true,emptyWindow.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.None,
                    "Receiving respawn coordinates cannot fill a missing death location after confirmation");
            }
        } finally { Directory.Delete(root,true); }
    }
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
        Check(File.ReadAllBytes(pathsA.ImagePath).SequenceEqual(File.ReadAllBytes(mapA)) &&
            File.ReadAllBytes(pathsB.ImagePath).SequenceEqual(File.ReadAllBytes(mapB)),
            "Custom map images are retained in their respective map folders");
        using(var defaultImage=new Bitmap(32,32)) pathsDefault.EnsureImage(defaultImage);
        Check(File.Exists(pathsDefault.ImagePath),"Default map image is retained beside its annotations");

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

        string importA=MapSidecarStore.SaveImport(pathsA.Folder,mapA,new List<MapZone>{oldPolygon},new List<MapZone>{oldPolygon},"{\"zones\":1}");
        string importAgain=MapSidecarStore.SaveImport(pathsA.Folder,mapA,new List<MapZone>{newPolygon},null,null);
        string importB=MapSidecarStore.SaveImport(pathsB.Folder,mapB,new List<MapZone>{newPolygon},null,null);
        Check(Path.GetDirectoryName(importA)==Path.Combine(pathsA.Folder,"imports") &&
            Path.GetDirectoryName(importB)==Path.Combine(pathsB.Folder,"imports") && importA!=importAgain,
            "Image imports stay under the relevant map and repeated imports do not overwrite each other");
        Check(File.ReadAllBytes(Path.Combine(importA,"source.png")).SequenceEqual(File.ReadAllBytes(mapA)) &&
            ZoneStore.Load(Path.Combine(importA,"zones.tsv"))[0].Name=="Old polygon" &&
            ZoneStore.Load(Path.Combine(importA,"source-zones.tsv"))[0].Name=="Old polygon" &&
            File.Exists(Path.Combine(importA,"detection.json")),
            "Imported image, map-coordinate TSV, source-coordinate TSV and detection report persist together");
        bool importRejected=false;
        try { MapSidecarStore.SaveImport(pathsA.Folder,mapA,new List<MapZone>{null},null,null); }
        catch(InvalidDataException) { importRejected=true; }
        Check(importRejected && Directory.GetDirectories(Path.Combine(pathsA.Folder,"imports")).Length==2 &&
            ZoneStore.Load(Path.Combine(importA,"zones.tsv"))[0].Name=="Old polygon",
            "Failed import leaves no partial folder and preserves existing imports");
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
        CheckDeathScreens(args.Length>0?args[0]:Path.Combine(Environment.CurrentDirectory,"resources"));
        CheckOverlayMouseInteraction();
        Check(Program.DataFolderName==(Program.IsTestBuild?"ScumMiniMap-ResponsivenessTest":"ScumMiniMap"),"Build uses the appropriate data folder");
        Check(Native.CursorBlocksCopy(true,false) && !Native.CursorBlocksCopy(true,true) && !Native.CursorBlocksCopy(false,false),"Visible cursor blocks ordinary menus but permits full-map tracking");
        PointF edge,direction;
        foreach(bool circle in new[]{true,false}) foreach(PointF target in new[]{new PointF(1,0),new PointF(-1,0),new PointF(0,1),new PointF(0,-1),new PointF(1,1)}) {
            RectangleF ring=new RectangleF(0,28,240,212);
            Check(MapWindow.TryDestinationEdge(ring,circle,PointF.Empty,target,out edge,out direction) && ring.Contains(edge) &&
                Math.Sign(edge.X-120)==Math.Sign(target.X) && Math.Sign(edge.Y-134)==Math.Sign(target.Y),"Destination edge follows map bearing within "+(circle?"circular":"rectangular")+" bounds: "+target);
        }
        Check(!MapWindow.TryDestinationEdge(new RectangleF(0,0,240,240),true,PointF.Empty,PointF.Empty,out edge,out direction),"No arbitrary direction when player and destination coincide");
        Check(Program.TrackingIntervalMs(250,0)==250 && Program.TrackingIntervalMs(700,0)==700,"Fast tracking cadence has no stationary slowdown and respects slower settings");
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
        using(var adminWindow=new MapWindow(testFolder,true)) {
            var adminOverlay=(OverlayWindow)typeof(MapWindow).GetField("overlay",Hidden).GetValue(adminWindow);
            var setMap=typeof(MapWindow).GetMethod("SetFullMap",Hidden);
            setMap.Invoke(adminWindow,new object[]{true});
            Point click=new Point(adminOverlay.Left+50,adminOverlay.Top+50);
            Check(!adminWindow.ObserveAdminMapMouse(0x201,click) && !adminWindow.ObserveAdminMapMouse(0x202,click),
                "Normal MiniMap clicks do not trigger admin cleanup");
            adminOverlay.UpdateFullMapClickThrough(true,true);
            Point outside=new Point(adminOverlay.Right+50,adminOverlay.Top+50);
            adminWindow.ObserveAdminMapMouse(0x201,outside);
            Check(!adminWindow.ObserveAdminMapMouse(0x202,outside),"Ctrl-click outside the exposed map does not trigger cleanup");
            adminWindow.ObserveAdminMapMouse(0x201,click);
            Check(!adminWindow.ObserveAdminMapMouse(0x205,click),"Right mouse release does not complete a left admin click");
            Check(adminWindow.ObserveAdminMapMouse(0x202,click),"Exposed map Ctrl-click completes on left mouse release");
            var adminChat=(ChatState)typeof(MapWindow).GetField("chat",Hidden).GetValue(adminWindow);
            adminChat.Key(0x54);
            Set(adminWindow,"observedChatPaused",true);
            Set(adminWindow,"inventoryInputLocked",true);
            Set(adminWindow,"pending",true);
            Set(adminWindow,"resumeAfter",DateTime.UtcNow.AddMinutes(1));
            adminWindow.CompleteAdminMapClick();
            Check(!(bool)typeof(MapWindow).GetField("fullMapActive",Hidden).GetValue(adminWindow) && !adminOverlay.FullMapMode && !adminOverlay.FullMapClickThrough,
                "Admin click cleanup collapses the expanded map and clears Ctrl reveal mode");
            Check(!adminChat.Paused && !(bool)typeof(MapWindow).GetField("observedChatPaused",Hidden).GetValue(adminWindow) &&
                !(bool)typeof(MapWindow).GetField("inventoryInputLocked",Hidden).GetValue(adminWindow) &&
                !(bool)typeof(MapWindow).GetField("pending",Hidden).GetValue(adminWindow) &&
                (DateTime)typeof(MapWindow).GetField("resumeAfter",Hidden).GetValue(adminWindow)<=DateTime.UtcNow,
                "Admin cleanup clears stale chat, inventory and pending-copy blockers together");
            adminChat.Key(0x54);
            adminWindow.CompleteAdminMapClick();
            Check(adminChat.Paused,"Cleanup cannot clear a newly opened chat after the map is closed");
        }
        using(var window=new MapWindow(testFolder,true)) {
            Check(typeof(MapWindow).GetField("mapTiles",Hidden).GetValue(window)!=null,"Bundled map uses tiles in the application renderer");
            Set(window,"locationHistory",true);
            Set(window,"locationHistoryTimestamps",true);
            // The constructor may seed the trail from SCUM's live clipboard.
            typeof(MapWindow).GetMethod("ClearLocationHistory",Hidden).Invoke(window,null);
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
