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
        Check(Native.IsObsProcessName("obs64") && Native.IsObsProcessName("OBS32") && !Native.IsObsProcessName("obs64-helper") && !Native.IsObsProcessName("SCUM") && !Native.IsObsProcessName(null),"Only exact OBS process identities retain the overlay; they do not identify SCUM input targets");
        int calls=0,movement=0;
        using(var batch=new BatchedWheel(steps=> { calls++;movement+=steps; })) {
            for(int i=0;i<100;i++)batch.Add(120);
            Check(calls==0,"Wheel burst queues no synchronous map rendering");
            batch.Flush();Check(calls==1 && movement==100,"One wheel batch combines a hundred notches");
            batch.Flush();Check(calls==1,"Settled wheel leaves no replay backlog");
            batch.Add(60);batch.Flush();Check(calls==1,"Partial wheel notch waits for remaining movement");
            batch.Add(60);batch.Flush();Check(calls==2 && movement==101,"High-resolution wheel remainder is preserved");
            batch.Add(120);batch.Add(-120);batch.Flush();Check(calls==2,"Opposite wheel movements cancel before expensive work");
            batch.Add(120);batch.Cancel();batch.Flush();Check(calls==2,"Closing or hiding cancels pending wheel movement");
            batch.Add(int.MaxValue);batch.Flush();Check(movement==357,"Extreme wheel burst stays bounded");
        }
        int zoomCalls=0;float zoomFactor=0;
        using(var overlay=new OverlayWindow(()=>{},()=>{},z=> { zoomCalls++;zoomFactor=z; },()=>{})) {
            IntPtr handle=overlay.Handle;
            Check((GetWindowLong(handle,-20)&0x80)==0,"OBS Window Capture can enumerate the overlay: tool-window flag is absent");
            Check((GetWindowLong(handle,-20)&0x80000)!=0 && !overlay.ShowInTaskbar,"Capture-compatible overlay retains layered transparency and no extra taskbar button");
            Check((GetWindowLong(handle,-20)&0x20)!=0,"New overlay passes gameplay clicks through");
            overlay.SetGameFocus(false);
            Check((GetWindowLong(handle,-20)&0x20)==0,"Desktop compact map receives clicks for dragging and resizing");
            var wheel=typeof(System.Windows.Forms.Control).GetMethod("OnMouseWheel",BindingFlags.Instance|BindingFlags.NonPublic);
            var overlayBatch=(BatchedWheel)typeof(OverlayWindow).GetField("mouseWheel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(overlay);
            var wheelEvent=new System.Windows.Forms.HandledMouseEventArgs(System.Windows.Forms.MouseButtons.None,0,30,40,120);
            wheel.Invoke(overlay,new object[]{wheelEvent});wheel.Invoke(overlay,new object[]{new System.Windows.Forms.HandledMouseEventArgs(System.Windows.Forms.MouseButtons.None,0,30,40,120)});
            Check(wheelEvent.Handled && zoomCalls==0,"Overlay consumes wheel input without synchronous zoom or parent propagation");
            overlayBatch.Flush();Check(zoomCalls==1 && Math.Abs(zoomFactor-2.25f)<.001,"Compact map applies combined zoom once");
            int fullDelta=0;Point fullAnchor=Point.Empty;
            overlay.FullMapMode=true;overlay.OnFullMapWheel=(delta,point)=> { fullDelta=delta;fullAnchor=point; };
            for(int i=0;i<12;i++)wheel.Invoke(overlay,new object[]{new System.Windows.Forms.HandledMouseEventArgs(System.Windows.Forms.MouseButtons.None,0,30,40,120)});
            Check(fullDelta==0,"Full map burst does not render every intermediate wheel event");
            overlayBatch.Flush();Check(fullDelta==1440 && fullAnchor==new Point(30,40),"Full map receives combined delta at the event's map position");
            wheel.Invoke(overlay,new object[]{new System.Windows.Forms.HandledMouseEventArgs(System.Windows.Forms.MouseButtons.None,0,30,40,120)});
            overlay.FullMapMode=false;overlayBatch.Flush();Check(zoomCalls==1,"Changing map mode cancels stale wheel input");
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
    static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control parent) {
        foreach(System.Windows.Forms.Control child in parent.Controls) {
            yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }
    static void CheckQuickStartTutorial() {
        AppLanguage original=Localization.Current;
        try {
            var bindings=new AppControlBindings();
            bindings.Set(AppControlAction.Settings,0x77,0);
            bindings.Set(AppControlAction.Search,0x78,0);
            foreach(AppLanguage language in Enum.GetValues(typeof(AppLanguage))) {
                Localization.Current=language;
                using(var guide=new StartupGuideDialog(0x4D,0x54,Program.DefaultCopyModifierKey,Program.DefaultCopyKey,null,appControls:bindings)) {
                    var labels=Descendants(guide).OfType<System.Windows.Forms.Label>().ToList();
                    Check(labels.Any(label=>label.Text==Localization.T("TutorialSettings","F8"))
                        && labels.Any(label=>label.Text==Localization.T("TutorialSearch","F9"))
                        && labels.Any(label=>label.Text==Localization.T("TutorialMap","M")),
                        "Quick start uses current settings, search and map bindings in "+language);
                    foreach(var label in labels.Where(label=>label.Text==Localization.T("TutorialMap","M") || label.Text==Localization.T("TutorialSearch","F9"))) {
                        int required=System.Windows.Forms.TextRenderer.MeasureText(label.Text,label.Font,new Size(label.Width,int.MaxValue),System.Windows.Forms.TextFormatFlags.WordBreak).Height;
                        Check(label.Height>=required && label.Bottom<=label.Parent.Height,"Quick start instructions fit their cards in "+language);
                    }
                }
            }
            Localization.Current=AppLanguage.English;
            bindings.Set(AppControlAction.Search,0,0);
            using(var guide=new StartupGuideDialog(0x4D,0x54,Program.DefaultCopyModifierKey,Program.DefaultCopyKey,null,appControls:bindings))
                Check(Descendants(guide).OfType<System.Windows.Forms.Label>().Any(label=>label.Text==Localization.Get("TutorialUnbound")),
                    "Quick start explains how to assign an unbound shortcut");
        } finally { Localization.Current=original; }
    }
    static System.Windows.Forms.ComboBox FindLanguageCombo(System.Windows.Forms.Control parent) {
        foreach(System.Windows.Forms.Control child in parent.Controls) {
            var combo=child as System.Windows.Forms.ComboBox;
            if(combo!=null && combo.Items.Count==Localization.LanguageDisplayNames.Length && (string)combo.Items[0]=="English") return combo;
            var nested=FindLanguageCombo(child);
            if(nested!=null) return nested;
        }
        return null;
    }
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
    static void CheckManualZoneEditor(string folder,string resources) {
        using(var map=new Bitmap(512,512))
        using(var editor=new ZoneEditor(map,new List<MapZone>{new MapZone { Name="Existing town",Argb=Color.Red.ToArgb(),Layer="Towns",Category=ZoneCategory.Town,Points=new[]{new PointF(.1f,.1f),new PointF(.3f,.1f),new PointF(.3f,.3f),new PointF(.1f,.3f)} }},Path.Combine(folder,"manual-zones.tsv"),value=>{})) {
            OverlayTheme.Frame(editor,Localization.Get("HeaderMapZones"),()=>{});editor.CreateControl();editor.PerformLayout();
            Func<string,object> field=name=>typeof(ZoneEditor).GetField(name,Hidden).GetValue(editor);
            Action<string,object[]> invoke=(name,args)=>typeof(ZoneEditor).GetMethod(name,Hidden).Invoke(editor,args);
            var target=(System.Windows.Forms.Control)field("target");target.Size=new Size(500,500);
            var tool=(System.Windows.Forms.ComboBox)field("drawingTool");var label=(System.Windows.Forms.TextBox)field("name");var zones=(List<MapZone>)field("zones");
            Func<RectangleF> rect=()=> (RectangleF)typeof(ZoneEditor).GetMethod("Fit",Hidden).Invoke(editor,new object[]{target,map});
            Func<float,float,Point> screen=(x,y)=> { var r=rect();return new Point((int)Math.Round(r.Left+x*r.Width),(int)Math.Round(r.Top+y*r.Height)); };
            tool.SelectedIndex=1;label.Text="Manual town";
            foreach(var point in new[]{screen(.15f,.15f),screen(.25f,.15f),screen(.25f,.25f)})invoke("ClickImage",new object[]{false,point});
            invoke("AddZone",new object[0]);
            Check(zones.Count==2 && zones[1].Name=="Manual town" && zones[1].Points.Length==3 && zones[0].Name=="Existing town",
                "Direct manual drawing works inside existing zones without a screenshot or calibration");
            Point anchor=screen(.6f,.55f);var before=new object[]{target,false,anchor,PointF.Empty};
            typeof(ZoneEditor).GetMethod("ImagePoint",Hidden).Invoke(editor,before);
            invoke("ZoomEditor",new object[]{target,false,120,anchor});var after=new object[]{target,false,anchor,PointF.Empty};
            typeof(ZoneEditor).GetMethod("ImagePoint",Hidden).Invoke(editor,after);
            var a=(PointF)before[3];var b=(PointF)after[3];
            Check(Math.Abs(a.X-b.X)<.0001 && Math.Abs(a.Y-b.Y)<.0001,"Import zoom preserves the map point under the mouse");
            Check((float)field("sourceView").GetType().GetField("Zoom",Hidden).GetValue(field("sourceView"))==1,
                "Destination zoom does not change the screenshot viewport");
            tool.SelectedIndex=2;label.Text="Rectangle town";invoke("SetRectangleDraft",new object[]{new PointF(.55f,.55f),new PointF(.8f,.8f)});invoke("AddZone",new object[0]);
            Check(zones.Count==3 && zones[2].Points.Length==4 && zones[2].Points[2]==new PointF(.8f,.8f),"Manual rectangle drawing retains map coordinates while zoomed");
            tool.SelectedIndex=4;invoke("SetRectangleDraft",new object[]{new PointF(.5f,.5f),new PointF(.7f,.5f)});
            var circle=(System.Collections.Generic.List<PointF>)typeof(ZoneEditor).GetField("manualDraft",Hidden).GetValue(editor);
            Check(circle.Count==256 && circle.All(p=>Math.Abs(Math.Sqrt(Math.Pow(p.X-.5,2)+Math.Pow(p.Y-.5,2))-.2)<.00001),"Circle tool generates equal radii at every angle while zoomed");
            invoke("ClearDraft",new object[0]);
            Action<string,System.Windows.Forms.MouseButtons,Point> mouse=(eventName,button,point)=>typeof(System.Windows.Forms.Control).GetMethod(eventName,Hidden).Invoke(target,new object[]{new System.Windows.Forms.MouseEventArgs(button,1,point.X,point.Y,0)});
            invoke("SetSelectedZoneIndex",new object[]{2});var beforeShape=(PointF[])zones[2].Points.Clone();
            invoke("ChangeZoneShape",new object[]{2});Check(zones[2].Points.Length==256,"Selected zone converts to a circle without adding another zone");
            invoke("ChangeZoneShape",new object[]{1});Check(zones[2].Points.Length==4 && Math.Abs((zones[2].Points[1].X-zones[2].Points[0].X)-(zones[2].Points[2].Y-zones[2].Points[1].Y))<.0001,"Selected zone converts to a square");
            invoke("UndoEditorPoint",new object[0]);invoke("UndoEditorPoint",new object[0]);Check(zones[2].Points.SequenceEqual(beforeShape),"Undo restores geometry after shape conversions");
            tool.SelectedIndex=5;mouse("OnMouseDown",System.Windows.Forms.MouseButtons.Left,screen(.675f,.675f));mouse("OnMouseMove",System.Windows.Forms.MouseButtons.Left,screen(.725f,.725f));mouse("OnMouseUp",System.Windows.Forms.MouseButtons.Left,screen(.725f,.725f));
            Check(Math.Abs(zones[2].Points[0].X-.6)<.005 && Math.Abs(zones[2].Points[0].Y-.6)<.005,"Whole-zone drag moves the outline without changing its shape");invoke("UndoEditorPoint",new object[0]);Check(zones[2].Points.SequenceEqual(beforeShape),"Undo restores a moved zone");
            tool.SelectedIndex=3;invoke("SetSelectedZoneIndex",new object[]{2});var rectangle=zones[2];
            int inserted=(int)typeof(ZoneEditor).GetMethod("InsertOutlineVertex",Hidden).Invoke(editor,new object[]{rectangle,rect(),screen(.675f,.55f)});
            Check(inserted==1 && rectangle.Points.Length==5,"Outline editing inserts a vertex on the selected edge");

            mouse("OnMouseDown",System.Windows.Forms.MouseButtons.Left,screen(.55f,.55f));mouse("OnMouseMove",System.Windows.Forms.MouseButtons.Left,screen(.5f,.5f));mouse("OnMouseUp",System.Windows.Forms.MouseButtons.Left,screen(.5f,.5f));
            Check(Math.Abs(rectangle.Points[0].X-.5)<.005 && Math.Abs(rectangle.Points[0].Y-.5)<.005,"Dragging a vertex updates the selected outline in map coordinates");
            // Consume the click corresponding to the drag, then remove the inserted vertex.
            mouse("OnMouseClick",System.Windows.Forms.MouseButtons.Left,screen(.5f,.5f));mouse("OnMouseClick",System.Windows.Forms.MouseButtons.Right,screen(rectangle.Points[1].X,rectangle.Points[1].Y));
            Check(rectangle.Points.Length==4,"Right-click removes an outline vertex without deleting its zone");
            invoke("UndoEditorPoint",new object[0]);Check(rectangle.Points.Length==5,"Outline undo restores a removed vertex");
            invoke("UndoEditorPoint",new object[0]);invoke("UndoEditorPoint",new object[0]);
            Check(rectangle.Points.Length==4 && rectangle.Points[0]==new PointF(.55f,.55f),"Outline undo restores dragged and inserted vertices without affecting other zones");
            mouse("OnMouseDown",System.Windows.Forms.MouseButtons.Left,screen(.55f,.55f));target.Capture=false;
            mouse("OnMouseMove",System.Windows.Forms.MouseButtons.None,screen(.6f,.6f));
            Check(rectangle.Points[0]==new PointF(.55f,.55f),"Losing mouse capture cancels vertex dragging instead of editing on later hover");
            var sourceZones=(List<MapZone>)field("sourceZones");var pairs=(Dictionary<MapZone,MapZone>)field("importedSources");var origin=zones[1].Clone();sourceZones.Add(origin);pairs[zones[1]]=origin;
            invoke("SetSelectedZoneIndex",new object[]{1});label.Text="Correct town";invoke("Rename",new object[]{false});invoke("MoveZone",new object[]{1});
            Check(origin.Name=="Correct town" && zones[0].Name=="Existing town" && Object.ReferenceEquals(pairs[zones[2]],origin),
                "Imported names remain paired to their correct outline after manual additions and reordering");
            invoke("RememberOutline",new object[]{zones[2]});pairs.Remove(zones[2]);sourceZones.Remove(origin);
            label.Text="Reviewed town";invoke("Rename",new object[]{false});invoke("UndoEditorPoint",new object[0]);
            Check(origin.Name=="Reviewed town" && Object.ReferenceEquals(pairs[zones[2]],origin),"Undoing an outline edit keeps a later corrected name paired with its source preview");
            ZoneStore.Save(Path.Combine(folder,"manual-zones.tsv"),zones);var loaded=ZoneStore.Load(Path.Combine(folder,"manual-zones.tsv"));
            Check(loaded.Count==3 && loaded[1].Points.Length==4 && loaded[2].Name=="Reviewed town","Manual geometry and corrected names survive save and reload");
            using(var image=Image.FromFile(Path.Combine(resources,"map.png")))using(var graphics=Graphics.FromImage(map))graphics.DrawImage(image,0,0,map.Width,map.Height);
            Set(editor,"screenshot",new Bitmap(map));Set(editor,"automatic",true);
            editor.PerformLayout();
            using(var preview=new Bitmap(editor.Width,editor.Height)) { editor.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));preview.Save(Path.GetFullPath(Path.Combine(resources,"..","release","checks","zone-editor-manual-preview.png"))); }
        }
    }
    static void CheckDeathScreens(string resources) {
        CheckDeathRecording(resources);
        CheckManualDeathRemoval();
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
    static void CheckManualDeathRemoval() {
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-death-clear-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            File.WriteAllText(Path.Combine(root,"settings.ini"),"Welcomed=True");
            using(var map=new Bitmap(32,32))map.Save(Path.Combine(root,"map.png"));
            string otherMap=Path.Combine(root,"alternate.png");
            using(var map=new Bitmap(32,32)) { using(var graphics=Graphics.FromImage(map))graphics.Clear(Color.White);map.Save(otherMap); }
            var otherPaths=MapSidecarPaths.ForMap(root,otherMap);
            var otherDeath=new MapZone { Name="Other map death",Category=ZoneCategory.Custom,Layer=MapZone.DeathMarkerLayer,Points=new[]{new PointF(.2f,.2f)} };
            MapSidecarStore.Save(otherPaths,new List<MapZone>{otherDeath});
            byte[] otherSaved=File.ReadAllBytes(otherPaths.WaypointsPath);
            using(var window=new MapWindow(root,true)) {
                var paths=(MapSidecarPaths)typeof(MapWindow).GetField("mapSidecarPaths",Hidden).GetValue(window);
                var normal=new MapZone { Name="Death location personal waypoint",Category=ZoneCategory.Custom,Layer="Personal",Points=new[]{new PointF(.3f,.3f)} };
                var area=new MapZone { Name="Keep custom area",Category=ZoneCategory.Custom,Layer="Personal",Points=new[]{new PointF(.1f,.1f),new PointF(.2f,.1f),new PointF(.2f,.2f)} };
                var dated=new MapZone { Name="Death location 2026-10-07 12:00:00",Category=ZoneCategory.Custom,Layer=MapZone.DeathMarkerLayer,Points=new[]{new PointF(.4f,.4f)} };
                var timestamped=new MapZone { Name="Death",Category=ZoneCategory.Custom,Layer=MapZone.DeathMarkerLayer,DeathCreatedUtc=DateTime.UtcNow.AddMinutes(-4),Points=new[]{new PointF(.6f,.6f)} };
                var candidate=new List<MapZone>{normal,area,dated,timestamped};
                Check((bool)typeof(MapWindow).GetMethod("TryCommitZonesCore",Hidden).Invoke(window,new object[]{candidate,null,true}),
                    "Death removal fixture persists ordinary annotations and both dated and timestamped marker styles");
                Set(window,"position",new Position { X=100,Y=200,Z=300,Yaw=0 });
                Check(window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.Saved,
                    "Death removal fixture includes a marker from the automatic detector");
                var route=new RoadRoute { Success=true,Polyline=new[]{new PointF(.5f,.5f),dated.Centroid},JunctionIndices=new int[0] };
                var pinned=new Position { X=777,Y=888,Z=999 };
                Set(window,"searchTarget",dated);Set(window,"lastRouteTarget",dated);Set(window,"activeRoute",route);Set(window,"voiceTarget",dated);Set(window,"pin",pinned);
                window.ClearReachedDeathMarkers(new PointF(.8f,.8f));
                int generation=(int)typeof(MapWindow).GetField("deathProbeGeneration",Hidden).GetValue(window);
                Set(window,"zonesLoadFailed",true);
                Check(!window.ClearDeathMarkers() && MapSidecarStore.Reload(paths).Count(marker=>marker.IsDeathMarker)==3
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window),dated)
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("activeRoute",Hidden).GetValue(window),route)
                    && (int)typeof(MapWindow).GetField("deathProbeGeneration",Hidden).GetValue(window)==generation,
                    "Failed death-marker saves retain all saved markers, their active destination and detector state");
                Set(window,"zonesLoadFailed",false);
                Set(window,"scumMap",null);Set(window,"adminPanelDismissedForFullMap",true);
                typeof(MapWindow).GetMethod("SetFullMap",Hidden).Invoke(window,new object[]{true});
                var overlay=(OverlayWindow)typeof(MapWindow).GetField("overlay",Hidden).GetValue(window);
                overlay.TopMost=false;overlay.Location=new Point(-32000,-32000);overlay.Show();
                typeof(MapWindow).GetMethod("RenderOverlay",Hidden).Invoke(window,null);
                var clickables=(System.Collections.IEnumerable)typeof(MapWindow).GetField("sidebarClickables",Hidden).GetValue(window);
                var remove=clickables.Cast<object>().Select(item=>(Action)item.GetType().GetField("OnClick").GetValue(item))
                    .Single(action=>action.Method.Name=="RemoveDeathMarkersFromSidebar");
                int presented=(int)typeof(MapWindow).GetField("PresentedFrames",Hidden).GetValue(window);
                remove();
                List<MapZone> saved=MapSidecarStore.Reload(paths);
                Check(saved.Count==2 && saved.Any(marker=>marker.Name==normal.Name && marker.Layer==normal.Layer)
                    && saved.Any(marker=>marker.Name==area.Name && marker.Points.Length==3) && !saved.Any(marker=>marker.IsDeathMarker),
                    "The actual sidebar button removes every death-marker style durably while preserving ordinary waypoints and polygons");
                Check(typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window)==null
                    && typeof(MapWindow).GetField("activeRoute",Hidden).GetValue(window)==null
                    && typeof(MapWindow).GetField("lastRouteTarget",Hidden).GetValue(window)==null
                    && typeof(MapWindow).GetField("voiceTarget",Hidden).GetValue(window)==null
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("pin",Hidden).GetValue(window),pinned),
                    "Removing the selected death destination clears its route and voice guidance while preserving the separate pin");
                Check((int)typeof(MapWindow).GetField("PresentedFrames",Hidden).GetValue(window)>presented
                    && (int)typeof(MapWindow).GetField("renderedFullMapSidebarRevision",Hidden).GetValue(window)==(int)typeof(MapWindow).GetField("fullMapSidebarRevision",Hidden).GetValue(window)
                    && (string)typeof(MapWindow).GetField("note",Hidden).GetValue(window)==Localization.T("NoteDeathMarkersRemoved",3),
                    "Sidebar removal immediately presents the updated map and refreshes cached sidebar state with its removed count");
                Check(File.ReadAllBytes(otherPaths.WaypointsPath).SequenceEqual(otherSaved)
                    && (int)typeof(MapWindow).GetField("deathProbeGeneration",Hidden).GetValue(window)==generation+1,
                    "Death removal leaves another map's annotations unchanged and invalidates pending detector captures");
                clickables=(System.Collections.IEnumerable)typeof(MapWindow).GetField("sidebarClickables",Hidden).GetValue(window);
                Check(!clickables.Cast<object>().Select(item=>(Action)item.GetType().GetField("OnClick").GetValue(item)).Any(action=>action.Method.Name=="RemoveDeathMarkersFromSidebar")
                    && !window.ClearDeathMarkers(),
                    "The empty Remove death markers button has no active click action and repeated removal is a no-op");
                Check(window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.None
                    && !MapSidecarStore.Reload(paths).Any(marker=>marker.IsDeathMarker),
                    "A still-visible death screen cannot recreate explicitly removed markers");
                for(int miss=0;miss<13;miss++)window.ApplyDeathScreenSample(false,null,DateTime.UtcNow);
                Check(window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.None,
                    "A brief death-screen detection dropout cannot resurrect removed markers");
                for(int miss=0;miss<14;miss++)window.ApplyDeathScreenSample(false,null,DateTime.UtcNow);
                Check(window.ApplyDeathScreenSample(true,window.CaptureDeathMarkerPosition(),DateTime.UtcNow)==DeathMarkerOutcome.Saved
                    && MapSidecarStore.Reload(paths).Count(marker=>marker.IsDeathMarker)==1,
                    "Sustained clear gameplay rearms automatic detection and a later real death still saves normally");
                Set(window,"searchTarget",normal);Set(window,"lastRouteTarget",normal);Set(window,"activeRoute",route);
                Check(window.ClearDeathMarkers() && Object.ReferenceEquals(typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window),normal)
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("activeRoute",Hidden).GetValue(window),route),
                    "Clearing other death markers preserves navigation to an ordinary waypoint");
                Check((bool)typeof(MapWindow).GetMethod("TryCommitZonesCore",Hidden).Invoke(window,new object[]{candidate,null,true}),
                    "Individual death-marker fixture saves two markers and ordinary annotations");
                Set(window,"showCustomWaypoints",true);Set(window,"fullMapZoom",2f);Set(window,"fullMapPan",new PointF(.6f,.6f));
                Set(window,"searchTarget",dated);Set(window,"activeRoute",route);Set(window,"voiceTarget",dated);
                Point markerPoint=new Point(overlay.Height/2,overlay.Height/2);
                Action<Point> rightClick=point=>typeof(MapWindow).GetMethod("HandleFullMapMouseDown",Hidden).Invoke(window,new object[]{point,System.Windows.Forms.MouseButtons.Right});
                Set(window,"zonesLoadFailed",true);
                rightClick(markerPoint);
                Check(MapSidecarStore.Reload(paths).Count(marker=>marker.IsDeathMarker)==2
                    && (float)typeof(MapWindow).GetField("fullMapZoom",Hidden).GetValue(window)==2f,
                    "Failed right-click removal retains saved markers and does not reset zoom");
                Set(window,"zonesLoadFailed",false);
                rightClick(markerPoint);
                Check(MapSidecarStore.Reload(paths).Count(marker=>marker.IsDeathMarker)==1
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window),dated)
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("activeRoute",Hidden).GetValue(window),route)
                    && (float)typeof(MapWindow).GetField("fullMapZoom",Hidden).GetValue(window)==2f,
                    "Right-click at a zoomed death marker removes only that marker and preserves unrelated navigation and zoom");
                Point datedPoint=new Point((int)Math.Round(overlay.Height*.1),(int)Math.Round(overlay.Height*.1));
                rightClick(datedPoint);
                Check(!MapSidecarStore.Reload(paths).Any(marker=>marker.IsDeathMarker)
                    && MapSidecarStore.Reload(paths).Count==2
                    && typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window)==null
                    && typeof(MapWindow).GetField("voiceTarget",Hidden).GetValue(window)==null,
                    "Right-clicking the selected death destination removes it durably and clears its navigation while retaining ordinary annotations");
                Check(!window.RemoveDeathMarker(normal) && !window.RemoveDeathMarker(timestamped),
                    "Individual removal rejects ordinary annotations and stale marker references");
                Set(window,"adminToolsEnabled",true);Set(window,"fullMapZoom",2f);Set(window,"fullMapPan",new PointF(.6f,.6f));
                Set(window,"searchTarget",normal);Set(window,"activeRoute",route);
                Point contextPoint=new Point(overlay.Height/2,overlay.Height/2);
                var pointArgs=new object[]{contextPoint,PointF.Empty};
                Check((bool)typeof(MapWindow).GetMethod("TryGetFullMapPoint",Hidden).Invoke(window,pointArgs),"Zoomed teleport fixture has a valid map point");
                rightClick(contextPoint);
                var captured=(PointF?)typeof(MapWindow).GetField("pendingFullMapWaypointMapPoint",Hidden).GetValue(window);
                Check(captured.HasValue && captured.Value==(PointF)pointArgs[1]
                    && (float)typeof(MapWindow).GetField("fullMapZoom",Hidden).GetValue(window)==2f
                    && (PointF)typeof(MapWindow).GetField("fullMapPan",Hidden).GetValue(window)==new PointF(.6f,.6f)
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("searchTarget",Hidden).GetValue(window),normal)
                    && Object.ReferenceEquals(typeof(MapWindow).GetField("activeRoute",Hidden).GetValue(window),route),
                    "Opening the zoomed admin context menu preserves view, navigation and exact teleport coordinates");
                overlay.Hide();
            }
            using(var reopened=new MapWindow(root,true))
                Check(!((List<MapZone>)typeof(MapWindow).GetField("zones",Hidden).GetValue(reopened)).Any(marker=>marker.IsDeathMarker),
                    "Removed death markers stay removed after application restart");
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
        CheckQuickStartTutorial();
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
        // Imports add a map hash, staging ID and atomic-save suffix beneath this fixture.
        // Keep its temporary prefix short enough for .NET Framework's MAX_PATH limit.
        string testFolder=Path.Combine(Path.GetTempPath(),"mmr-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testFolder);
        CheckMapSidecars(testFolder);
        CheckManualZoneEditor(testFolder,args[0]);
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
            Set(adminWindow,"fullMapOpacity",35);Set(adminWindow,"fullMapZoomOpacityOverride",true);
            Check(adminWindow.EffectiveFullMapOpacity==100,"Zoom opacity fixture starts fully visible");
            adminOverlay.UpdateFullMapClickThrough(true,true);
            Check(adminWindow.EffectiveFullMapOpacity==0 && (int)typeof(MapWindow).GetField("fullMapOpacity",Hidden).GetValue(adminWindow)==35,
                "Ctrl passthrough overrides zoom opacity with zero without changing the configured value");
            adminOverlay.UpdateFullMapClickThrough(false,true);
            Check(adminWindow.EffectiveFullMapOpacity==35 && !adminOverlay.FullMapClickThrough,
                "Releasing Ctrl restores configured opacity instead of the previous zoom override");
            Set(adminWindow,"fullMapZoomOpacityOverride",true);adminOverlay.UpdateFullMapClickThrough(true,true);
            adminOverlay.UpdateFullMapClickThrough(true,false);
            Check(adminWindow.EffectiveFullMapOpacity==35 && !adminOverlay.FullMapClickThrough,
                "Focus loss clears temporary transparency without leaving the zoom override active");
            Set(adminWindow,"fullMapZoomOpacityOverride",true);adminOverlay.UpdateFullMapClickThrough(true,true);
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
            Check(adminWindow.EffectiveFullMapOpacity==35 && (int)typeof(MapWindow).GetField("fullMapOpacity",Hidden).GetValue(adminWindow)==35,
                "Completing a passed-through player click restores configured opacity without changing it");
            Check(!(bool)typeof(MapWindow).GetField("fullMapActive",Hidden).GetValue(adminWindow) && !adminOverlay.FullMapMode && !adminOverlay.FullMapClickThrough,
                "Admin click cleanup collapses the expanded map and clears Ctrl reveal mode");
            Console.WriteLine("Admin cleanup state: chat="+adminChat.Paused+", observed="+typeof(MapWindow).GetField("observedChatPaused",Hidden).GetValue(adminWindow)+
                ", inventory="+typeof(MapWindow).GetField("inventoryInputLocked",Hidden).GetValue(adminWindow)+", pending="+typeof(MapWindow).GetField("pending",Hidden).GetValue(adminWindow)+
                ", resume="+((DateTime)typeof(MapWindow).GetField("resumeAfter",Hidden).GetValue(adminWindow)).ToString("O")+", now="+DateTime.UtcNow.ToString("O"));
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
            IntPtr settingsHandle=window.Handle;
            var settingsBar=(System.Windows.Forms.Control)typeof(MapWindow).GetField("bar",Hidden).GetValue(window);
            foreach(int languageIndex in new[]{10,5,8,12,0}) {
                var combo=FindLanguageCombo(settingsBar);
                Check(combo!=null,"Settings language selector exists");
                IntPtr comboHandle=combo.Handle;
                combo.SelectedIndex=languageIndex;
                Check(!combo.IsDisposed && combo.Handle==comboHandle,"Language selection retains native dropdown until notification returns: "+languageIndex);
                System.Windows.Forms.Application.DoEvents();
                Check(combo.IsDisposed && (int)Localization.Current==languageIndex,"Queued settings rebuild applies selected language: "+languageIndex);
            }
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
