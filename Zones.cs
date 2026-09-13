using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;

namespace ScumMiniMap {
    public enum ZoneCategory { City, Town, Farm, Trader, Faction, Military, GasStation, Custom, Unknown }
    public sealed class MapZone {
        public string Name;
        public int Argb;
        public PointF[] Points;
        public ZoneCategory Category;
        static readonly HashSet<string> FactionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "D4 Clock house", "D4 Stable ruins", "D4 City - Pharmacy", "D4 City - Police Station",
            "D3 City Warehouse", "D3 - General Store", "D3 Little town - House",
            "D2 Little bridge", "D2 Lumbermill cabin",
            "D1 Graveyard", "D1 Food store", "D1 Farm",
            "C4 Wine farm", "C4 WWII Bunker",
            "C3 Little cabin",
            "C1 ww2 bunker", "C1 Little farm",
            "B3 WWII Bunker", "B3 Little farm",
            "B2 Little Farm", "B2 WWII Bunker", "B2 Farm",
            "B1 Barn", "B1 WWII Bunker", "B1 Little Farm",
            "A4 Street between 2 houses", "A4 Lighthouse", "A4 House ruins",
            "A3 Farm", "A3 WWII bunker", "A3 Island WWII bunker",
            "A2 Store with ATM", "A2 Stables",
            "A1 Stables", "A1 School",
            "A0 Offices", "A0 Camping",
            "Z4 Lighthouse", "Z4 WWII Bunker", "Z4 Boatyard", "Z4 Graveyard",
            "Z3 WWII Bunker", "Z3 Boat yard", "Z3 Island Lighthouse",
            "Z2 light house",
            "Z0 WWII bunker", "Z0 Lighthouse", "Z0 WWII Bunker South"
        };
        public static bool IsFactionName(string name) { return !string.IsNullOrEmpty(name) && FactionNames.Contains(name); }
        public bool IsFaction { get { return Argb == -1 || IsFactionName(Name); } }
        public bool IsFuelStation { get { return Argb == -30720 || (Points != null && Points.Length < 3 && Name != null && Name.IndexOf("Gas", StringComparison.OrdinalIgnoreCase) >= 0); } }
        public MapZone Clone() { return new MapZone { Name=Name,Argb=Argb,Points=(PointF[])Points.Clone() }; }
        public PointF Centroid {
            get {
                if(Points==null || Points.Length==0) return new PointF(0,0);
                float x=0,y=0;
                for(int i=0;i<Points.Length;i++) { x+=Points[i].X; y+=Points[i].Y; }
                return new PointF(x/Points.Length,y/Points.Length);
            }
        }
        public double DistanceTo(PointF p) {
            if(Points==null || Points.Length==0) return double.MaxValue;
            if(Contains(p)) return 0;
            double minDistSq=double.MaxValue;
            for(int i=0,j=Points.Length-1; i<Points.Length; j=i++) {
                PointF a=Points[j], b=Points[i];
                double dx=b.X-a.X, dy=b.Y-a.Y;
                double lenSq=dx*dx+dy*dy;
                double u = lenSq>0 ? ((p.X-a.X)*dx + (p.Y-a.Y)*dy)/lenSq : 0;
                u = Math.Max(0,Math.Min(1,u));
                double px = a.X + u*dx, py = a.Y + u*dy;
                double dSq = (p.X-px)*(p.X-px) + (p.Y-py)*(p.Y-py);
                if(dSq<minDistSq) minDistSq=dSq;
            }
            return Math.Sqrt(minDistSq);
        }
        public bool Contains(PointF p) {
            if(Points==null || Points.Length<3) return false;
            bool inside=false;
            for(int i=0,j=Points.Length-1; i<Points.Length; j=i++) {
                if(((Points[i].Y>p.Y)!=(Points[j].Y>p.Y)) &&
                    (p.X < (Points[j].X-Points[i].X)*(p.Y-Points[i].Y)/(Points[j].Y-Points[i].Y) + Points[i].X)) {
                    inside=!inside;
                }
            }
            return inside;
        }
    }
    public static class ZoneStore {
        static string SafeUnescape(string value) {
            try { return Uri.UnescapeDataString(value); }
            catch(UriFormatException) { return value; }
        }
        public static ZoneCategory Classify(MapZone z) {
            if(z.IsFuelStation) return ZoneCategory.GasStation;
            if(z.IsFaction) return ZoneCategory.Faction;
            if(z.Argb==-16737281) return ZoneCategory.Custom; // cyan 0xFF00C8FF
            string n=z.Name??"";
            string upper=n.ToUpperInvariant();
            if(z.Argb==-993486 || z.Argb==-1358291) { // gold or red = military
                return ZoneCategory.Military;
            }
            if(upper.Contains("FARM") || upper.Contains("RANCH") || upper.Contains("VINEYARD") || upper.Contains("ORCHARD")) return ZoneCategory.Farm;
            if(upper.Contains("OUTPOST") || upper.Contains("TRADER") || z.Argb==-9843406) return ZoneCategory.Trader;
            // Large settlements vs small towns: check for known city names
            string[] cities = { "SAMOBOR", "KLENOVNIK", "PRIBOJ" };
            foreach(string c in cities) if(upper.Contains(c)) return ZoneCategory.City;
            if(z.Points!=null && z.Points.Length>=3 && z.Argb==-6895376) return ZoneCategory.Town;
            return ZoneCategory.Unknown;
        }
        public static List<MapZone> Load(string path) {
            if(File.Exists(path)) {
                using(Stream stream=File.OpenRead(path)) return Load(stream);
            }
            using(Stream stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("zones.tsv")) {
                if(stream!=null) return Load(stream);
            }
            return new List<MapZone>();
        }
        public static List<MapZone> Load(Stream stream) {
            List<MapZone> result=new List<MapZone>();
            if(stream==null)return result;
            using(StreamReader reader=new StreamReader(stream)) {
                string line;
                while((line=reader.ReadLine())!=null) {
                    string[] parts=line.Split('\t'); int color;
                    if((parts.Length!=3 && parts.Length!=4) || !int.TryParse(parts[1],out color))continue;
                    List<PointF> points=new List<PointF>(); bool valid=true;
                    foreach(string point in parts[2].Split(';')) {
                        string[] xy=point.Split(','); float x,y;
                        if(xy.Length!=2 || !float.TryParse(xy[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x) || !float.TryParse(xy[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y) || float.IsNaN(x) || float.IsNaN(y) || x<-.1f || x>1.1f || y<-.1f || y>1.1f) { valid=false;break; }
                        points.Add(new PointF(Math.Max(0,Math.Min(1,x)),Math.Max(0,Math.Min(1,y))));
                    }
                    if(valid && points.Count>=1 && points.Count<=512 && result.Count<500) {
                        MapZone zone = new MapZone { Name=SafeUnescape(parts[0]),Argb=color,Points=points.ToArray() };
                        if(parts.Length>3) {
                            ZoneCategory cat;
                            if(Enum.TryParse(parts[3], out cat)) zone.Category = cat;
                        } else {
                            zone.Category = Classify(zone);
                        }
                        result.Add(zone);
                    }
                }
            }
            return result;
        }
        public static void Save(string path,List<MapZone> zones) {
            string temp=path+".tmp";
            List<string> lines = new List<string>();
            foreach(MapZone z in zones) {
                if(z.Points==null || z.Points.Length==0) continue;
                lines.Add(Uri.EscapeDataString(z.Name)+"\t"+z.Argb+"\t"+string.Join(";",z.Points.Select(p=>p.X.ToString("R",CultureInfo.InvariantCulture)+","+p.Y.ToString("R",CultureInfo.InvariantCulture)))+"\t"+z.Category);
            }
            File.WriteAllLines(temp,lines);
            if(File.Exists(path))File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public static void DrawFuelIcon(Graphics g,float cx,float cy,float r=10f) {
            using(Brush shadow=new SolidBrush(Color.FromArgb(90,0,0,0))) g.FillEllipse(shadow,cx-r,cy-r+1.5f,r*2,r*2);
            using(Brush badge=new SolidBrush(Color.FromArgb(255,140,0))) g.FillEllipse(badge,cx-r,cy-r,r*2,r*2);
            using(Pen border=new Pen(Color.FromArgb(220,20,25,30),1.4f)) g.DrawEllipse(border,cx-r,cy-r,r*2,r*2);
            using(Brush white=new SolidBrush(Color.White)) g.FillRectangle(white,cx-4.5f,cy-4f,5f,8.5f);
            using(Brush cutout=new SolidBrush(Color.FromArgb(255,140,0))) g.FillRectangle(cutout,cx-3.5f,cy-2.5f,3f,2.2f);
            using(Pen whitePen=new Pen(Color.White,1.4f)) {
                whitePen.StartCap=LineCap.Round; whitePen.EndCap=LineCap.Round;
                g.DrawLine(whitePen,cx+0.5f,cy-2f,cx+4f,cy-1f);
                g.DrawLine(whitePen,cx+4f,cy-1f,cx+4f,cy+4f);
                g.DrawLine(whitePen,cx+4f,cy+4f,cx+2.5f,cy+2.5f);
            }
        }
        public static void Draw(Graphics g,IEnumerable<MapZone> zones,RectangleF rect,bool labels,float fontSize=9f,bool showGasStations=true,HashSet<ZoneCategory> hidden=null) {
            using(Font font=new Font("Segoe UI",Math.Max(6f,Math.Min(24f,fontSize)),FontStyle.Bold)) {
                StringFormat sf=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center };
                List<RectangleF> placed=new List<RectangleF>();
                List<KeyValuePair<string,RectangleF>> labelItems=new List<KeyValuePair<string,RectangleF>>();
                foreach(MapZone zone in zones??Enumerable.Empty<MapZone>()) {
                    if(zone==null || zone.Points==null || zone.Points.Length==0) continue;
                    if(hidden!=null && hidden.Contains(zone.Category)) continue;
                    if(zone.Points.Length>=3) {
                        if(!labels) continue;
                        PointF[] points=zone.Points.Select(p=>new PointF(rect.Left+p.X*rect.Width,rect.Top+p.Y*rect.Height)).ToArray();
                        Color color=Color.FromArgb(zone.Argb);
                        using(Brush fill=new SolidBrush(Color.FromArgb(45,color)))g.FillPolygon(fill,points);
                        using(Pen pen=new Pen(Color.FromArgb(210,color),2))g.DrawPolygon(pen,points);
                        if(!string.IsNullOrEmpty(zone.Name)) {
                            string displayName = Localization.GetZoneName(zone.Name);
                            float cx=points.Average(p=>p.X),cy=points.Average(p=>p.Y);
                            SizeF sz=g.MeasureString(displayName,font);
                            RectangleF box=new RectangleF(cx-sz.Width/2f-4f,cy-sz.Height/2f-2f,sz.Width+8f,sz.Height+4f);
                            for(int pass=0;pass<10;pass++) {
                                bool clash=false;
                                for(int j=0;j<placed.Count;j++) {
                                    if(box.IntersectsWith(placed[j])) {
                                        clash=true;
                                        box.Y=placed[j].Bottom+2f;
                                    }
                                }
                                if(!clash)break;
                            }
                            placed.Add(box);
                            labelItems.Add(new KeyValuePair<string,RectangleF>(displayName,box));
                        }
                    } else if(showGasStations) {
                        float cx=rect.Left+zone.Points[0].X*rect.Width, cy=rect.Top+zone.Points[0].Y*rect.Height;
                        if(cx>=rect.Left-20 && cx<=rect.Right+20 && cy>=rect.Top-20 && cy<=rect.Bottom+20) {
                            DrawFuelIcon(g,cx,cy);
                        }
                    }
                }
                foreach(KeyValuePair<string,RectangleF> item in labelItems) {
                    using(Brush bg=new SolidBrush(Color.FromArgb(170,12,17,22)))g.FillRectangle(bg,item.Value);
                    using(Pen border=new Pen(Color.FromArgb(130,255,255,255),1f))g.DrawRectangle(border,item.Value.X,item.Value.Y,item.Value.Width,item.Value.Height);
                    g.DrawString(item.Key,font,Brushes.Black,item.Value.X+item.Value.Width/2f+1f,item.Value.Y+item.Value.Height/2f+1f,sf);
                    g.DrawString(item.Key,font,Brushes.White,item.Value.X+item.Value.Width/2f,item.Value.Y+item.Value.Height/2f,sf);
                }
            }
        }
        public static PointF Align(PointF point,PointF s1,PointF s2,PointF m1,PointF m2) {
            if(Math.Abs(s2.X-s1.X)<.05 || Math.Abs(s2.Y-s1.Y)<.05 || Math.Abs(m2.X-m1.X)<.02 || Math.Abs(m2.Y-m1.Y)<.02)throw new ArgumentException("Choose two landmarks separated both horizontally and vertically.");
            float sx=(m2.X-m1.X)/(s2.X-s1.X),sy=(m2.Y-m1.Y)/(s2.Y-s1.Y);
            if(sx<=0 || sy<=0)throw new ArgumentException("Use matching landmarks in the same order on a north-up map.");
            return new PointF(m1.X+(point.X-s1.X)*sx,m1.Y+(point.Y-s1.Y)*sy);
        }
        public static void SelfTest() {
            using(MemoryStream stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Edge\t1\t-0.02,0.2;0.1,0.2;0.1,0.3\nInvalid\t1\t-4,0.2;0.1,0.2;0.1,0.3"))) {
                List<MapZone> edge=Load(stream);
                if(edge.Count!=1 || edge[0].Points[0].X!=0)throw new Exception("Map-edge zone validation failed.");
            }
            PointF p=Align(new PointF(.5f,.5f),new PointF(.1f,.1f),new PointF(.9f,.9f),new PointF(.2f,.3f),new PointF(.6f,.7f));
            if(Math.Abs(p.X-.4f)>.0001 || Math.Abs(p.Y-.5f)>.0001)throw new Exception("Screenshot alignment failed.");
            bool rejected=false;try { Align(p,p,p,p,p); } catch(ArgumentException) { rejected=true; }
            if(!rejected)throw new Exception("Degenerate alignment accepted.");
            string path=Path.GetTempFileName();
            try {
                List<MapZone> sample=new List<MapZone>{
                    new MapZone { Name="Test zone / 1",Argb=Color.Red.ToArgb(),Points=new[]{new PointF(.2f,.2f),new PointF(.5f,.2f),new PointF(.5f,.5f)} },
                    new MapZone { Name="Gas Station - Test",Argb=-30720,Points=new[]{new PointF(.3f,.4f)} }
                };
                Save(path,sample);List<MapZone> loaded=Load(path);
                if(loaded.Count!=2 || loaded[0].Name!=sample[0].Name || loaded[1].Name!=sample[1].Name || loaded[1].Points[0]!=sample[1].Points[0])throw new Exception("Zone persistence failed.");
            } finally { File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp"); }
        }
    }
    sealed class ZoneEditor:Form {
        readonly MapCanvas source=new MapCanvas(),target=new MapCanvas();
        readonly Image map;
        Image screenshot;
        readonly Label instruction=new Label();
        readonly ListBox list=new ListBox();
        readonly TextBox name=new TextBox { Width=140,Text=Localization.Get("ZeNewZone") };
        readonly List<PointF> draft=new List<PointF>();
        readonly string path;
        readonly Action<List<MapZone>> saved;
        readonly List<MapZone> zones;
        PointF s1,s2,m1,m2;
        int step;
        Color color=Color.OrangeRed;
        bool automatic,busy;
        List<MapZone> sourceZones=new List<MapZone>();
        public ZoneEditor(Image map,List<MapZone> existing,string path,Action<List<MapZone>> saved) {
            this.map=map;this.path=path;this.saved=saved;zones=existing.Select(z=>z.Clone()).ToList();
            Text=Localization.Get("ZoneEditorTitle");ClientSize=new Size(1120,720);MinimumSize=new Size(900,600);StartPosition=FormStartPosition.CenterParent;
            BackColor=Color.FromArgb(22,27,32);ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",9);
            FlowLayoutPanel bar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=76,Padding=new Padding(8) };
            Button open=new Button { Text=Localization.Get("ZeOpenScreenshot"),Width=130 };open.Click+=(s,e)=>OpenScreenshot();bar.Controls.Add(open);
            Button reset=new Button { Text=Localization.Get("ZeManualFallback"),Width=115 };reset.Click+=(s,e)=> { if(busy)return;automatic=false;step=0;draft.Clear();foreach(Control control in bar.Controls)if((string)control.Tag=="manual")control.Visible=true;UpdateInstruction(); };bar.Controls.Add(reset);
            name.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Enter) { e.SuppressKeyPress=true;Rename(false); } };
            bar.Controls.Add(name);
            Button choose=new Button { Text=Localization.Get("ZeZoneColour"),Width=100 };choose.Click+=(s,e)=> { using(ColorDialog dialog=new ColorDialog { Color=color })if(dialog.ShowDialog(this)==DialogResult.OK)color=dialog.Color; };bar.Controls.Add(choose);
            Button undo=new Button { Text=Localization.Get("ZeUndoPoint"),Width=100,Tag="manual",Visible=false };undo.Click+=(s,e)=> { if(draft.Count>0)draft.RemoveAt(draft.Count-1);RefreshViews(); };bar.Controls.Add(undo);
            Button add=new Button { Text=Localization.Get("ZeAddZone"),Width=90,Tag="manual",Visible=false };add.Click+=(s,e)=>AddZone();bar.Controls.Add(add);
            Button rename=new Button { Text=Localization.Get("ZeApplyName"),Width=105 };rename.Click+=(s,e)=>Rename(false);bar.Controls.Add(rename);
            Button group=new Button { Text=Localization.Get("ZeNameColour"),Width=130 };group.Click+=(s,e)=>Rename(true);bar.Controls.Add(group);
            Button save=new Button { Text=Localization.Get("ZeSaveZones"),Width=105 };save.Click+=(s,e)=> {
                if(busy)return;
                if(draft.Count>0) { MessageBox.Show(this,Localization.Get("ZeDraftWarning"));return; }
                try { ZoneStore.Save(path,zones);saved(zones.Select(z=>z.Clone()).ToList());DialogResult=DialogResult.OK;Close(); }
                catch(IOException ex) { MessageBox.Show(this,Localization.T("ZeSaveZonesError",ex.Message)); }catch(UnauthorizedAccessException ex) { MessageBox.Show(this,ex.Message); }
            };bar.Controls.Add(save);
            instruction.Dock=DockStyle.Top;instruction.Height=48;instruction.Padding=new Padding(8);
            SplitContainer split=new SplitContainer { Dock=DockStyle.Fill,Size=new Size(1100,500),SplitterDistance=550 };
            source.Dock=DockStyle.Fill;target.Dock=DockStyle.Fill;source.BackColor=target.BackColor=Color.FromArgb(12,17,22);
            split.Panel1.Controls.Add(source);split.Panel2.Controls.Add(target);
            FlowLayoutPanel bottom=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=76,Padding=new Padding(8) };
            list.Width=700;list.Height=58;bottom.Controls.Add(list);
            Button delete=new Button { Text=Localization.Get("ZeDeleteSelected"),Width=130 };delete.Click+=(s,e)=> { if(!busy && list.SelectedIndex>=0) { int index=list.SelectedIndex;zones.RemoveAt(index);if(automatic && index<sourceZones.Count)sourceZones.RemoveAt(index);RefreshViews(); } };bottom.Controls.Add(delete);
            Controls.Add(split);Controls.Add(instruction);Controls.Add(bar);Controls.Add(bottom);
            source.Paint+=(s,e)=>DrawSource(e.Graphics);target.Paint+=(s,e)=>DrawTarget(e.Graphics);
            source.MouseClick+=(s,e)=>ClickImage(true,e.Location);target.MouseClick+=(s,e)=>ClickImage(false,e.Location);
            list.SelectedIndexChanged+=(s,e)=> { if(list.SelectedIndex>=0) { name.Text=zones[list.SelectedIndex].Name;color=Color.FromArgb(zones[list.SelectedIndex].Argb); }source.Invalidate();target.Invalidate(); };
            FormClosed+=(s,e)=> { if(screenshot!=null)screenshot.Dispose(); };
            RefreshViews();UpdateInstruction();
        }
        RectangleF Fit(Control panel,Image image) {
            float scale=Math.Min((float)panel.Width/image.Width,(float)panel.Height/image.Height);
            return new RectangleF((panel.Width-image.Width*scale)/2,(panel.Height-image.Height*scale)/2,image.Width*scale,image.Height*scale);
        }
        async void OpenScreenshot() {
            if(busy)return;
            using(OpenFileDialog dialog=new OpenFileDialog { Filter=Localization.Get("ZeMapScreenshotsFilter"),Title=Localization.Get("ZeSelectScreenshotTitle") }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                await ImportAutomatic(dialog.FileName);
            }
        }
        public async Task ImportAutomatic(string file) {
            if(busy)return;busy=true;
            instruction.Text=Localization.Get("ZeAligningLocally");
            string folder=Path.GetDirectoryName(path),temp=Path.Combine(Path.GetTempPath(),"scum-zones-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                string python=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Python","Python312","python.exe");
                string config=Path.Combine(folder,"python-path.txt");if(File.Exists(config))python=File.ReadAllText(config).Trim();
                if(!File.Exists(python))python="python.exe";
                string output=Path.Combine(temp,"zones.tsv"),sourceOutput=Path.Combine(temp,"source.tsv");
                Func<string,string> quote=value=>"\""+value.Replace("\"","")+"\"";
                string referenceFile=Path.Combine(temp,"reference.png");
                map.Save(referenceFile,System.Drawing.Imaging.ImageFormat.Png);
                ProcessStartInfo info=new ProcessStartInfo(python,quote(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"detect_zones.py"))+" --input "+quote(file)+" --reference "+quote(referenceFile)+" --output "+quote(output)+" --source-output "+quote(sourceOutput)) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
                string report=await Task.Run(()=> {
                    using(Process process=Process.Start(info)) {
                        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
                        if(!process.WaitForExit(60000)) { process.Kill();throw new IOException("Detection timed out. Try a smaller screenshot."); }
                        Task.WaitAll(stdout,stderr);
                        if(process.ExitCode!=0)throw new IOException(stderr.Result.Trim());return stdout.Result.Trim();
                    }
                });
                if(IsDisposed)return;
                List<MapZone> imported=ZoneStore.Load(output),sourceImported=ZoneStore.Load(sourceOutput);
                if(imported.Count==0 || imported.Count!=sourceImported.Count)throw new IOException("No usable zone outlines returned.");
                using(Image loaded=Image.FromFile(file)) { Image copy=new Bitmap(loaded);if(screenshot!=null)screenshot.Dispose();screenshot=copy; }
                zones.Clear();zones.AddRange(imported);sourceZones=sourceImported;draft.Clear();automatic=true;step=4;
                RefreshViews();list.SelectedIndex=0;UpdateInstruction();
                try { File.WriteAllText(Path.Combine(folder,"last-detection.json"),report); }catch(IOException) {}catch(UnauthorizedAccessException) {}
            } catch(Exception ex) {
                if(!IsDisposed) { instruction.Text=Localization.Get("ZeAutoFailedKept");MessageBox.Show(this,Localization.T("ZeCouldNotImportAuto",ex.Message)); }
            } finally {
                busy=false;
                // Only files generated inside this unique import directory are removed.
                try {
                    foreach(string leaf in new[]{"zones.tsv","source.tsv"}) { string generated=Path.Combine(temp,leaf);if(File.Exists(generated))File.Delete(generated); }
                    Directory.Delete(temp, true);
                }catch(IOException) {}catch(UnauthorizedAccessException) {}
            }
        }
        public int DetectedCount { get { return automatic?zones.Count:0; } }
        public void PreviewAutomatic(string output) {
            if(!automatic || zones.Count==0)throw new Exception("Automatic screenshot import returned no zones.");
            list.SelectedIndex=0;name.Text="Naming check";Rename(false);
            if(zones[0].Name!="Naming check" || sourceZones[0].Name!="Naming check")throw new Exception("Automatic zone rename failed.");
            Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(output); }
        }
        void Rename(bool sameColour) {
            if(busy || list.SelectedIndex<0)return;
            if(string.IsNullOrWhiteSpace(name.Text) || name.Text.Length>80) { MessageBox.Show(this,Localization.Get("ZeLabelLengthWarning"));return; }
            int index=list.SelectedIndex,original=zones[index].Argb;
            for(int i=0;i<zones.Count;i++)if(i==index || (sameColour && zones[i].Argb==original)) {
                zones[i].Name=name.Text.Trim();zones[i].Argb=color.ToArgb();
                if(automatic && i<sourceZones.Count) { sourceZones[i].Name=zones[i].Name;sourceZones[i].Argb=zones[i].Argb; }
            }
            RefreshViews();list.SelectedIndex=index;
        }
        void ClickImage(bool isSource,Point point) {
            if(busy)return;
            if(isSource && screenshot==null)return;
            RectangleF rect=Fit(isSource?source:target,isSource?screenshot:map);if(!rect.Contains(point))return;
            PointF p=new PointF((point.X-rect.X)/rect.Width,(point.Y-rect.Y)/rect.Height);
            if(automatic || zones.Count>0) {
                List<MapZone> items=(automatic && isSource)?sourceZones:zones;
                for(int i=items.Count-1;i>=0;i--) {
                    if(items[i].Contains(p)) {
                        list.SelectedIndex=i;
                        name.Focus();
                        name.SelectAll();
                        return;
                    }
                }
                if(automatic)return;
            }
            if(screenshot==null)return;
            if(step==0 && isSource) { s1=p;step=1; }
            else if(step==1 && !isSource) { m1=p;step=2; }
            else if(step==2 && isSource) { s2=p;step=3; }
            else if(step==3 && !isSource) {
                m2=p;try { ZoneStore.Align(s1,s1,s2,m1,m2);step=4; }catch(ArgumentException ex) { MessageBox.Show(this,ex.Message);step=0; }
            } else if(step==4 && isSource && draft.Count<512) {
                PointF mapped=ZoneStore.Align(p,s1,s2,m1,m2);
                if(mapped.X<0 || mapped.X>1 || mapped.Y<0 || mapped.Y>1) { MessageBox.Show(this,Localization.Get("ZeOutsideMapWarning"));return; }
                draft.Add(p);
            }
            UpdateInstruction();
        }
        void UpdateInstruction() {
            if(automatic) {
                instruction.Text=Localization.T("ZeDetectedOutlinesPrompt",zones.Count);
                source.Invalidate();target.Invalidate();return;
            }
            string[] steps={
                Localization.Get("ZeManualStep0"),
                Localization.Get("ZeManualStep1"),
                Localization.Get("ZeManualStep2"),
                Localization.Get("ZeManualStep3"),
                Localization.Get("ZeManualStep4")
            };
            instruction.Text=screenshot==null?Localization.Get("ZeInitialPrompt"):steps[step];
            source.Invalidate();target.Invalidate();
        }
        void RefreshViews() { list.Items.Clear();foreach(MapZone z in zones)list.Items.Add(z.Name);source.Invalidate();target.Invalidate(); }
        void AddZone() {
            if(automatic || busy)return;
            if(step!=4 || draft.Count<3) { MessageBox.Show(this,Localization.Get("ZePointsRequirementWarning"));return; }
            if(string.IsNullOrWhiteSpace(name.Text) || name.Text.Length>80 || zones.Count>=500) { MessageBox.Show(this,Localization.Get("ZeZonesMaxWarning"));return; }
            zones.Add(new MapZone { Name=name.Text.Trim(),Argb=color.ToArgb(),Points=draft.Select(p=>ZoneStore.Align(p,s1,s2,m1,m2)).ToArray() });draft.Clear();RefreshViews();
        }
        public void CheckAndPreview(string output) {
            screenshot=new Bitmap(map); Show();Application.DoEvents();
            Action<bool,float,float> click=(isSource,x,y)=> {
                RectangleF rect=Fit(isSource?source:target,isSource?screenshot:map);
                ClickImage(isSource,new Point((int)(rect.X+x*rect.Width),(int)(rect.Y+y*rect.Height)));
            };
            click(true,.1f,.1f);click(false,.1f,.1f);click(true,.9f,.9f);click(false,.9f,.9f);
            if(step!=4)throw new Exception("Editor landmark alignment failed.");
            click(true,.3f,.3f);click(true,.6f,.3f);click(true,.6f,.6f);
            int before=zones.Count;name.Text="Example zone";AddZone();
            if(zones.Count!=before+1 || zones[before].Points.Length!=3)throw new Exception("Zone tracing failed.");
            Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(output); }
            Close();
        }
        void Cross(Graphics g,RectangleF rect,PointF p,string text) { float x=rect.X+p.X*rect.Width,y=rect.Y+p.Y*rect.Height;g.DrawEllipse(Pens.Cyan,x-5,y-5,10,10);g.DrawString(text,Font,Brushes.Cyan,x+7,y); }
        void DrawSource(Graphics g) {
            if(screenshot==null)return;RectangleF rect=Fit(source,screenshot);g.DrawImage(screenshot,rect);
            if(automatic) { DrawDetected(g,sourceZones,rect);return; }
            if(step>=1)Cross(g,rect,s1,"1");if(step>=3)Cross(g,rect,s2,"2");
            PointF[] points=draft.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
            if(points.Length>1)using(Pen pen=new Pen(color,2))g.DrawLines(pen,points);
            foreach(PointF p in points)g.FillEllipse(Brushes.White,p.X-3,p.Y-3,6,6);
        }
        void DrawTarget(Graphics g) {
            RectangleF rect=Fit(target,map);g.DrawImage(map,rect);
            if(automatic) { DrawDetected(g,zones,rect);return; }
            ZoneStore.Draw(g,zones,rect,true);
            if(list.SelectedIndex>=0 && list.SelectedIndex<zones.Count && zones[list.SelectedIndex].Points!=null && zones[list.SelectedIndex].Points.Length>0) {
                PointF[] points=zones[list.SelectedIndex].Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                if(points.Length>=3) using(Pen pen=new Pen(Color.White,3))g.DrawPolygon(pen,points);
                else using(Pen pen=new Pen(Color.White,3))g.DrawEllipse(pen,points[0].X-12,points[0].Y-12,24,24);
            }
            if(step>=2)Cross(g,rect,m1,"1");if(step>=4)Cross(g,rect,m2,"2");
            if(step==4 && draft.Count>=3)ZoneStore.Draw(g,new[]{new MapZone { Name=name.Text,Argb=color.ToArgb(),Points=draft.Select(p=>ZoneStore.Align(p,s1,s2,m1,m2)).ToArray() }},rect,false);
        }
        void DrawDetected(Graphics g,List<MapZone> items,RectangleF rect) {
            ZoneStore.Draw(g,items,rect,false);
            for(int i=0;i<items.Count;i++) {
                PointF[] points=items[i].Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                if(list.SelectedIndex==i) {
                    if(points.Length>=3) using(Pen pen=new Pen(Color.White,3))g.DrawPolygon(pen,points);
                    else using(Pen pen=new Pen(Color.White,3))g.DrawEllipse(pen,points[0].X-12,points[0].Y-12,24,24);
                }
                float x=points.Average(p=>p.X),y=points.Average(p=>p.Y);
                g.DrawString((i+1).ToString(),Font,Brushes.Black,x+1,y+1);g.DrawString((i+1).ToString(),Font,Brushes.White,x,y);
            }
        }
    }
}
