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
    public static class SafeMapImage {
        public static Bitmap Decode(Stream stream) {
            if(stream==null) throw new FileNotFoundException("The default map is missing.");
            if(stream.CanSeek && stream.Length>128L*1024*1024) throw new InvalidDataException("Map file exceeds 128 MB.");
            using(Image image=Image.FromStream(stream,false,false)) {
                if(image.Width<1 || image.Height<1 || image.Width>16384 || image.Height>16384 || (long)image.Width*image.Height>256L*1024*1024)
                    throw new InvalidDataException("Map dimensions exceed the supported memory budget.");
                return new Bitmap(image);
            }
        }
        public static Bitmap Load(string custom,Func<Stream> embedded,out string warning) {
            warning=null;
            if(File.Exists(custom)) {
                try { using(var file=File.OpenRead(custom)) return Decode(file); }
                catch(Exception ex) {
                    if(!(ex is IOException) && !(ex is InvalidDataException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is OutOfMemoryException) && !(ex is System.Runtime.InteropServices.ExternalException)) throw;
                    warning=Localization.Get("CustomMapRecovery");
                }
            }
            using(Stream resource=embedded()) return Decode(resource);
        }
    }
    public enum ZoneCategory { City, Town, Farm, Trader, Faction, Military, Bunker, GasStation, Custom, Unknown }
    public sealed class MapZone {
        public string Name;
        public string Subtitle;
        public int Argb;
        public PointF[] Points;
        public ZoneCategory Category;
        static readonly HashSet<string> FactionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "D4 Clock house", "D4 Stable ruins", "D4 City - Pharmacy", "D4 City - Police Station",
            "D3 City Warehouse", "D3 - General Store", "D3 Gas Station", "D3 Little town - House",
            "D2 Little bridge", "D2 Lumbermill cabin", "D2 Little town - House",
            "D1 Graveyard", "D1 Food store", "D1 Farm",
            "C4 Wine farm", "C4 WWII Bunker",
            "C3 Little cabin", "C3 Workshop", "C3 Garage",
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
        public bool IsFaction { get { return Argb == -1 || Argb == -657931 || (Argb & 0xFFFFFF) == 0xFFFFFF || IsFactionName(Name); } }
        public bool IsFuelStation { get { return Argb == -30720 || (Points != null && Points.Length < 3 && Name != null && Name.IndexOf("Gas", StringComparison.OrdinalIgnoreCase) >= 0); } }
        public string Layer = "Default";
        public MapZone Clone() { return new MapZone { Name=Name,Subtitle=Subtitle,Argb=Argb,Points=Points==null?new PointF[0]:(PointF[])Points.Clone(),Category=Category,Layer=Layer }; }
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
        internal float minX, maxX, minY, maxY;
        internal bool boundsReady;
        public void ComputeBounds() {
            if(Points==null || Points.Length==0) { minX=maxX=minY=maxY=0; boundsReady=true; return; }
            minX=maxX=Points[0].X; minY=maxY=Points[0].Y;
            for(int i=1;i<Points.Length;i++) {
                float px=Points[i].X, py=Points[i].Y;
                if(px<minX) minX=px; if(px>maxX) maxX=px;
                if(py<minY) minY=py; if(py>maxY) maxY=py;
            }
            boundsReady=true;
        }
        public bool Contains(PointF p) {
            if(Points==null || Points.Length<3) return false;
            if(!boundsReady) ComputeBounds();
            if(p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY) return false;
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
        public const int MaxZones=10000, MaxPoints=512, MaxLineLength=65536;
        const long MaxTextChars=32*1024*1024;
        static string ReadBoundedLine(TextReader reader,ref long total) {
            var line=new System.Text.StringBuilder(); int c;
            while((c=reader.Read())!=-1) {
                if(++total>MaxTextChars) throw new InvalidDataException("Zone file exceeds the size limit.");
                if(c=='\n') return line.ToString().TrimEnd('\r');
                if(line.Length>=MaxLineLength) throw new InvalidDataException("Zone line exceeds the size limit.");
                line.Append((char)c);
            }
            return line.Length==0?null:line.ToString().TrimEnd('\r');
        }
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
            if(upper.Contains("BUNKER")) return ZoneCategory.Bunker;
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
                string line; long total=0;
                while((line=ReadBoundedLine(reader,ref total))!=null) {
                    if(string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts=line.Split('\t'); int color;
                    if(parts.Length<3 || !int.TryParse(parts[1],out color)) throw new InvalidDataException("Invalid zone record.");
                    List<PointF> points=new List<PointF>(); bool valid=true;
                    foreach(string point in parts[2].Split(';')) {
                        string[] xy=point.Split(','); float x,y;
                        if(xy.Length!=2 || !float.TryParse(xy[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x) || !float.TryParse(xy[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y) || float.IsNaN(x) || float.IsNaN(y) || x<-.1f || x>1.1f || y<-.1f || y>1.1f) { valid=false;break; }
                        if(points.Count>=MaxPoints) throw new InvalidDataException("Zone has too many points.");
                        points.Add(new PointF(Math.Max(0,Math.Min(1,x)),Math.Max(0,Math.Min(1,y))));
                    }
                    if(!valid || points.Count==0) throw new InvalidDataException("Invalid zone coordinates.");
                    if(result.Count>=MaxZones) throw new InvalidDataException("Zone file exceeds the supported zone count.");
                    {
                        MapZone zone = new MapZone { Name=SafeUnescape(parts[0]),Argb=color,Points=points.ToArray() };
                        if(parts.Length>3) {
                            ZoneCategory cat;
                            if(Enum.TryParse(parts[3], out cat) && Enum.IsDefined(typeof(ZoneCategory),cat)) zone.Category = cat;
                            else zone.Category = Classify(zone);
                        } else {
                            zone.Category = Classify(zone);
                        }
                        if(parts.Length>4 && !string.IsNullOrWhiteSpace(parts[4])) {
                            zone.Layer = SafeUnescape(parts[4]);
                        } else {
                            zone.Layer = "Default";
                        }
                        if(parts.Length<=3 && (zone.Name??"").ToUpperInvariant().Contains("BUNKER") && !zone.IsFuelStation && !zone.IsFaction && zone.Category != ZoneCategory.Faction) zone.Category = ZoneCategory.Bunker;
                        result.Add(zone);
                    }
                }
            }
            return result;
        }
        public static void Save(string path,List<MapZone> zones) {
            if(zones==null || zones.Count>MaxZones) throw new InvalidDataException("Too many zones to save.");
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            var lines=new List<string>(); long total=0;
            foreach(MapZone z in zones) {
                if(z==null || z.Name==null || z.Points==null || z.Points.Length==0 || z.Points.Length>MaxPoints || !Enum.IsDefined(typeof(ZoneCategory),z.Category))
                    throw new InvalidDataException("Invalid zone; nothing was saved.");
                foreach(PointF point in z.Points)
                    if(float.IsNaN(point.X)||float.IsInfinity(point.X)||float.IsNaN(point.Y)||float.IsInfinity(point.Y)||point.X<0||point.X>1||point.Y<0||point.Y>1)
                        throw new InvalidDataException("Invalid zone coordinates; nothing was saved.");
                string layer=string.IsNullOrWhiteSpace(z.Layer)?"Default":z.Layer;
                string line=Uri.EscapeDataString(z.Name)+"\t"+z.Argb+"\t"+string.Join(";",z.Points.Select(pt=>pt.X.ToString("R",CultureInfo.InvariantCulture)+","+pt.Y.ToString("R",CultureInfo.InvariantCulture)))+"\t"+z.Category+"\t"+Uri.EscapeDataString(layer);
                total+=line.Length+2;
                if(line.Length>MaxLineLength || total>MaxTextChars) throw new InvalidDataException("Zone data exceeds the size limit.");
                lines.Add(line);
            }
            try {
                File.WriteAllLines(temp,lines);
                if(File.Exists(path))File.Replace(temp,path,null); else File.Move(temp,path);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
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
        public static void Draw(Graphics g,IEnumerable<MapZone> zones,RectangleF rect,bool showPolygons,float fontSize=9f,bool showGasStations=true,HashSet<ZoneCategory> hidden=null,bool showLabels=true,bool smartLod=true,float zoom=1.0f) {
            using(Font font=new Font("Segoe UI",Math.Max(6f,Math.Min(24f,fontSize)),FontStyle.Bold)) {
                StringFormat sf=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center };
                List<RectangleF> placed=new List<RectangleF>();
                List<KeyValuePair<string,RectangleF>> labelItems=new List<KeyValuePair<string,RectangleF>>();
                bool zoomedOut = smartLod && zoom < 2.5f;
                RectangleF clip = g.VisibleClipBounds;
                if(clip.Width <= 0 || clip.Height <= 0) clip = new RectangleF(0, 0, 4000, 4000);
                foreach(MapZone zone in zones??Enumerable.Empty<MapZone>()) {
                    if(zone==null || zone.Points==null || zone.Points.Length==0) continue;
                    if(hidden!=null && hidden.Contains(zone.Category)) continue;
                    if(zone.Points.Length>=3) {
                        if(!showPolygons) continue;
                        if(!zone.boundsReady) zone.ComputeBounds();
                        float polyMinX = rect.Left + zone.minX * rect.Width;
                        float polyMaxX = rect.Left + zone.maxX * rect.Width;
                        float polyMinY = rect.Top + zone.minY * rect.Height;
                        float polyMaxY = rect.Top + zone.maxY * rect.Height;
                        if(polyMaxX < clip.Left - 10 || polyMinX > clip.Right + 10 || polyMaxY < clip.Top - 10 || polyMinY > clip.Bottom + 10) continue;

                        PointF[] points = new PointF[zone.Points.Length];
                        float cx = 0, cy = 0;
                        for(int pi = 0; pi < zone.Points.Length; pi++) {
                            float px = rect.Left + zone.Points[pi].X * rect.Width;
                            float py = rect.Top + zone.Points[pi].Y * rect.Height;
                            points[pi] = new PointF(px, py);
                            cx += px; cy += py;
                        }
                        cx /= zone.Points.Length; cy /= zone.Points.Length;

                        Color color=Color.FromArgb(zone.Argb);
                        int fillAlpha = (zone.IsFaction || zone.Category == ZoneCategory.Faction) ? 75 : 40;
                        float penWidth = (zone.IsFaction || zone.Category == ZoneCategory.Faction) ? 2.5f : 1.8f;
                        using(Brush fill=new SolidBrush(Color.FromArgb(fillAlpha,color)))g.FillPolygon(fill,points);
                        using(Pen pen=new Pen(Color.FromArgb(220,color),penWidth))g.DrawPolygon(pen,points);
                        bool isMajor = zone.Category==ZoneCategory.City || zone.Category==ZoneCategory.Trader || zone.Category==ZoneCategory.Custom || zone.Category==ZoneCategory.Faction || zone.IsFaction;
                        if(showLabels && (!zoomedOut || isMajor) && !string.IsNullOrEmpty(zone.Name)) {
                            string displayName = Localization.GetZoneName(zone.Name);
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
                    } else {
                        float cx=rect.Left+zone.Points[0].X*rect.Width, cy=rect.Top+zone.Points[0].Y*rect.Height;
                        if(cx>=clip.Left-20 && cx<=clip.Right+20 && cy>=clip.Top-20 && cy<=clip.Bottom+20) {
                            if(zone.IsFuelStation) {
                                if(showGasStations) DrawFuelIcon(g,cx,cy);
                            } else {
                                Color c=Color.FromArgb(zone.Argb);
                                using(Brush shadow=new SolidBrush(Color.FromArgb(90,0,0,0))) g.FillEllipse(shadow,cx-7,cy-7+1.5f,14,14);
                                using(Brush badge=new SolidBrush(c)) g.FillEllipse(badge,cx-7,cy-7,14,14);
                                using(Pen border=new Pen(Color.FromArgb(220,20,25,30),1.4f)) g.DrawEllipse(border,cx-7,cy-7,14,14);
                                using(Brush dot=new SolidBrush(Color.White)) g.FillEllipse(dot,cx-2.5f,cy-2.5f,5,5);
                                if(showLabels && !string.IsNullOrEmpty(zone.Name)) {
                                    string displayName = Localization.GetZoneName(zone.Name);
                                    SizeF sz=g.MeasureString(displayName,font);
                                    RectangleF box=new RectangleF(cx-sz.Width/2f-4f,cy+9f,sz.Width+8f,sz.Height+4f);
                                    placed.Add(box);
                                    labelItems.Add(new KeyValuePair<string,RectangleF>(displayName,box));
                                }
                            }
                        }
                    }
                }
                foreach(KeyValuePair<string,RectangleF> item in labelItems) {
                    using(Brush bg=new SolidBrush(Color.FromArgb(180,12,17,22)))g.FillRectangle(bg,item.Value);
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
            using(MemoryStream stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Edge\t1\t-0.02,0.2;0.1,0.2;0.1,0.3"))) {
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
    public sealed class ZoneEditor:Form {
        readonly MapCanvas source=new MapCanvas(),target=new MapCanvas();
        readonly Image map;
        Image editorPreview;
        Image screenshot;
        readonly Label instruction=new Label();
        readonly ListBox list=new ListBox();
        readonly TextBox name=new TextBox { Width=150,Text=Localization.Get("ZeNewZone") };
        readonly TextBox filterBox=new TextBox { Width=130 };
        readonly Label detailsLabel=new Label();
        readonly ComboBox cboLayer=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=130 };
        readonly Button btnNewLayer=new Button { Width=65 };
        readonly Button btnDeleteLayer=new Button { Width=95 };
        readonly CheckBox chkFill=new CheckBox();
        readonly List<int> visibleIndices=new List<int>();
        readonly List<PointF> draft=new List<PointF>();
        readonly string path;
        readonly Action<List<MapZone>> saved;
        readonly List<MapZone> zones;
        PointF s1,s2,m1,m2;
        int step;
        Color color=Color.OrangeRed;
        bool automatic,busy,fillZones=true;
        Timer busyTimer;
        float spinAngle=0f;
        List<MapZone> sourceZones=new List<MapZone>();

        string GetSelectedLayer() {
            if(cboLayer.SelectedIndex<=0) return null;
            return cboLayer.SelectedItem as string;
        }

        void PopulateLayerCombo(string selectLayer = null) {
            string prev = selectLayer ?? (cboLayer.SelectedItem as string);
            cboLayer.BeginUpdate();
            cboLayer.Items.Clear();
            cboLayer.Items.Add(Localization.Get("ZeAllLayers"));
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(MapZone z in zones) {
                string lay = string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer;
                if(seen.Add(lay)) cboLayer.Items.Add(lay);
            }
            cboLayer.EndUpdate();
            if(!string.IsNullOrEmpty(prev) && cboLayer.Items.Contains(prev)) {
                cboLayer.SelectedItem = prev;
            } else {
                cboLayer.SelectedIndex = 0;
            }
            btnDeleteLayer.Enabled = GetSelectedLayer() != null;
        }

        int GetSelectedZoneIndex() {
            if(list.SelectedIndex>=0 && list.SelectedIndex<visibleIndices.Count)
                return visibleIndices[list.SelectedIndex];
            return -1;
        }

        void SetSelectedZoneIndex(int zoneIndex) {
            if(zoneIndex<0) { list.SelectedIndex=-1; return; }
            for(int i=0;i<visibleIndices.Count;i++) {
                if(visibleIndices[i]==zoneIndex) {
                    list.SelectedIndex=i;
                    return;
                }
            }
            list.SelectedIndex=-1;
        }

        static string SectorFromMapPt(PointF p) {
            int col = Math.Max(0, Math.Min(4, (int)Math.Floor(p.X * 5f)));
            int row = Math.Max(0, Math.Min(4, (int)Math.Floor(p.Y * 5f)));
            string[] letters = new string[] { "D", "C", "B", "A", "Z" };
            return letters[row] + (4 - col);
        }

        void UpdateDetails() {
            int selIdx = GetSelectedZoneIndex();
            string selectedLayer = GetSelectedLayer();
            if (selIdx >= 0 && selIdx < zones.Count) {
                MapZone z = zones[selIdx];
                int ptCount = z.Points != null ? z.Points.Length : 0;
                PointF center = z.Centroid;
                string sec = SectorFromMapPt(center);
                Color c = Color.FromArgb(z.Argb);
                string lay = string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer;
                detailsLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "Zone {0}/{1} | \"{2}\" [{3}] | {4} pts | Sector {5} | #{6:X2}{7:X2}{8:X2}",
                    selIdx + 1, zones.Count, z.Name??string.Empty, lay, ptCount, sec, c.R, c.G, c.B);
            } else {
                detailsLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "Total: {0} zones ({1} visible){2} | Select a zone to inspect or edit",
                    zones.Count, visibleIndices.Count, selectedLayer != null ? " in layer '" + selectedLayer + "'" : "");
            }
        }

        void RefreshViews() {
            int prevSelectedZone = GetSelectedZoneIndex();
            list.BeginUpdate();
            list.Items.Clear();
            visibleIndices.Clear();
            string selectedLayer = GetSelectedLayer();
            string q = (filterBox.Text ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < zones.Count; i++) {
                MapZone z = zones[i];
                string lay = string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer;
                if(selectedLayer != null && !string.Equals(lay, selectedLayer, StringComparison.OrdinalIgnoreCase)) continue;
                string zoneName=z.Name??string.Empty;
                if (string.IsNullOrEmpty(q) || zoneName.ToLowerInvariant().Contains(q)) {
                    visibleIndices.Add(i);
                    string tag = (selectedLayer == null) ? (" [" + lay + "]") : "";
                    list.Items.Add(string.Format(CultureInfo.InvariantCulture, "#{0}: {1}{2}", i + 1, zoneName, tag));
                }
            }
            list.EndUpdate();
            if (prevSelectedZone >= 0) SetSelectedZoneIndex(prevSelectedZone);
            UpdateDetails();
            source.Invalidate();
            target.Invalidate();
        }

        enum ImportMode { NewLayer, Append, Replace, Cancel }

        ImportMode ShowImportDialog(string fileName, string targetLayer, out string newLayerName) {
            newLayerName = Path.GetFileNameWithoutExtension(fileName);
            if(string.IsNullOrWhiteSpace(newLayerName)) newLayerName = "Imported Layer";

            using(Form dlg = new Form {
                ClientSize = new Size(450, 260),
                Text = Localization.Get("ZeImportActionTitle"),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = OverlayTheme.Background,
                ForeColor = OverlayTheme.Ink
            }) {
                OverlayTheme.Frame(dlg, Localization.Get("ZeImportActionTitle"), () => dlg.DialogResult = DialogResult.Cancel);

                Label lblPrompt = new Label {
                    Left = 20, Top = 46, Width = 410, Height = 36,
                    Text = Localization.T("ZeImportModePrompt", Path.GetFileName(fileName)),
                    ForeColor = OverlayTheme.InkMuted, Font = new Font("Segoe UI", 9f)
                };

                RadioButton rbNew = new RadioButton {
                    Left = 24, Top = 86, Width = 145,
                    Text = Localization.Get("ZeImportNewLayer"),
                    Checked = true, AutoSize = true, ForeColor = OverlayTheme.Ink, Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };

                TextBox txtNewName = new TextBox {
                    Left = 185, Top = 84, Width = 235,
                    Text = newLayerName, BackColor = OverlayTheme.Surface, ForeColor = OverlayTheme.Ink,
                    BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f)
                };

                string activeLay = !string.IsNullOrEmpty(targetLayer) ? targetLayer : "Default";

                RadioButton rbAppend = new RadioButton {
                    Left = 24, Top = 120, Width = 400,
                    Text = Localization.T("ZeImportAppend", activeLay),
                    AutoSize = true, ForeColor = OverlayTheme.Ink, Font = new Font("Segoe UI", 9f)
                };

                RadioButton rbReplace = new RadioButton {
                    Left = 24, Top = 152, Width = 400,
                    Text = Localization.T("ZeImportReplace", activeLay),
                    AutoSize = true, ForeColor = OverlayTheme.Ink, Font = new Font("Segoe UI", 9f)
                };

                Button btnOk = new Button {
                    Text = Localization.Get("Done"), Left = 240, Top = 204, Width = 90, Height = 32,
                    DialogResult = DialogResult.OK, BackColor = OverlayTheme.Accent, ForeColor = Color.FromArgb(12, 12, 12),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
                };
                btnOk.FlatAppearance.BorderColor = OverlayTheme.AccentHover;

                Button btnCancel = new Button {
                    Text = Localization.Get("Cancel"), Left = 340, Top = 204, Width = 80, Height = 32,
                    DialogResult = DialogResult.Cancel, BackColor = OverlayTheme.Surface, ForeColor = OverlayTheme.Ink,
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
                };
                btnCancel.FlatAppearance.BorderColor = OverlayTheme.Border;

                dlg.Controls.AddRange(new Control[] { lblPrompt, rbNew, txtNewName, rbAppend, rbReplace, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                rbNew.CheckedChanged += (s, e) => { txtNewName.Enabled = rbNew.Checked; };

                if(dlg.ShowDialog(this) == DialogResult.OK) {
                    if(rbNew.Checked) {
                        newLayerName = (!string.IsNullOrWhiteSpace(txtNewName.Text)) ? txtNewName.Text.Trim() : "Layer " + (cboLayer.Items.Count);
                        return ImportMode.NewLayer;
                    }
                    if(rbAppend.Checked) return ImportMode.Append;
                    if(rbReplace.Checked) return ImportMode.Replace;
                }
                return ImportMode.Cancel;
            }
        }

        string PromptNewLayerName() {
            using(Form prompt = new Form {
                ClientSize = new Size(360, 160),
                Text = Localization.Get("ZeNewLayerTitle"),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, BackColor = OverlayTheme.Background, ForeColor = OverlayTheme.Ink
            }) {
                OverlayTheme.Frame(prompt, Localization.Get("ZeNewLayerTitle"), () => prompt.DialogResult = DialogResult.Cancel);
                Label lbl = new Label { Left = 20, Top = 50, Width = 320, Text = Localization.Get("ZeNewLayerPrompt"), ForeColor = OverlayTheme.InkMuted, Font = new Font("Segoe UI", 9f) };
                TextBox txt = new TextBox { Left = 20, Top = 76, Width = 320, Text = "Layer " + cboLayer.Items.Count, BackColor = OverlayTheme.Surface, ForeColor = OverlayTheme.Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f) };
                Button ok = new Button { Text = Localization.Get("Done"), Left = 170, Top = 114, Width = 80, Height = 30, DialogResult = DialogResult.OK, BackColor = OverlayTheme.Accent, ForeColor = Color.FromArgb(12, 12, 12), Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                ok.FlatAppearance.BorderColor = OverlayTheme.AccentHover;
                Button cn = new Button { Text = Localization.Get("Cancel"), Left = 260, Top = 114, Width = 80, Height = 30, DialogResult = DialogResult.Cancel, BackColor = OverlayTheme.Surface, ForeColor = OverlayTheme.Ink, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                cn.FlatAppearance.BorderColor = OverlayTheme.Border;
                prompt.Controls.AddRange(new Control[] { lbl, txt, ok, cn });
                prompt.AcceptButton = ok; prompt.CancelButton = cn;
                txt.KeyDown += (s, e) => { if(e.KeyCode == Keys.Enter) { ok.PerformClick(); e.Handled = true; e.SuppressKeyPress = true; } };
                if(prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text)) {
                    return txt.Text.Trim();
                }
                return null;
            }
        }

        void DeleteCurrentLayer() {
            string selectedLayer = GetSelectedLayer();
            if(string.IsNullOrEmpty(selectedLayer)) return;
            int count = zones.Count(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, selectedLayer, StringComparison.OrdinalIgnoreCase));
            if(MessageBox.Show(this, Localization.T("ZeDeleteLayerConfirm", selectedLayer, count), Localization.Get("ZeDeleteLayer"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                zones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, selectedLayer, StringComparison.OrdinalIgnoreCase));
                sourceZones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, selectedLayer, StringComparison.OrdinalIgnoreCase));
                draft.Clear();
                PopulateLayerCombo();
                RefreshViews();
                UpdateInstruction();
            }
        }

        public ZoneEditor(Image map,List<MapZone> existing,string path,Action<List<MapZone>> saved) {
            this.map=map;this.path=path;this.saved=saved;zones=new List<MapZone>();
            if(map!=null && (map.Width>2048 || map.Height>2048)) {
                int maxDim=2048;
                float s=Math.Min((float)maxDim/map.Width,(float)maxDim/map.Height);
                Bitmap preview=new Bitmap((int)(map.Width*s),(int)(map.Height*s),System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(preview)) {
                    g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    g.DrawImage(map,0,0,preview.Width,preview.Height);
                }
                editorPreview=preview;
            } else {
                editorPreview=map;
            }
            foreach(MapZone existingZone in existing??new List<MapZone>()) if(existingZone!=null) zones.Add(existingZone.Clone());
            Text=Localization.Get("ZoneEditorTitle");ClientSize=new Size(1150,740);MinimumSize=new Size(920,620);StartPosition=FormStartPosition.CenterScreen;
            BackColor=OverlayTheme.Background;ForeColor=OverlayTheme.Ink;Font=new Font("Segoe UI",9);

            FlowLayoutPanel bar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=76,Padding=new Padding(8) };
            Button open=new Button { Text=Localization.Get("ZeOpenScreenshot"),Width=125 };
            open.Click+=(s,e)=>OpenScreenshot();
            Button importFile=new Button { Text=Localization.Get("ZeImportFile"),Width=90 };
            importFile.Click+=(s,e)=>ImportFile();
            Button exportFile=new Button { Text=Localization.Get("ZeExport"),Width=75 };
            exportFile.Click+=(s,e)=>ExportZones();
            Label lblLayer=new Label { Text=Localization.Get("ZeLayer"),AutoSize=true,Margin=new Padding(6,8,2,3),ForeColor=OverlayTheme.InkMuted };
            btnNewLayer.Text=Localization.Get("ZeNewLayer");
            btnNewLayer.Click+=(s,e)=> {
                if(busy)return;
                string newName=PromptNewLayerName();
                if(!string.IsNullOrWhiteSpace(newName)) {
                    if(!cboLayer.Items.Contains(newName)) {
                        cboLayer.Items.Add(newName);
                    }
                    cboLayer.SelectedItem=newName;
                    btnDeleteLayer.Enabled=true;
                    RefreshViews();
                }
            };
            btnDeleteLayer.Text=Localization.Get("ZeDeleteLayer");
            btnDeleteLayer.Click+=(s,e)=> {
                if(busy)return;
                DeleteCurrentLayer();
            };
            cboLayer.SelectedIndexChanged+=(s,e)=> {
                btnDeleteLayer.Enabled=GetSelectedLayer()!=null;
                RefreshViews();
            };
            Button reset=new Button { Text=Localization.Get("ZeManualFallback"),Width=115 };
            reset.Click+=(s,e)=> { if(busy)return;automatic=false;step=0;draft.Clear();foreach(Control control in bar.Controls)if((string)control.Tag=="manual")control.Visible=true;UpdateInstruction(); };
            name.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Enter) { e.SuppressKeyPress=true;Rename(false); } };
            Button choose=new Button { Text=Localization.Get("ZeZoneColour"),Width=95 };
            choose.Click+=(s,e)=> { using(ColorDialog dialog=new ColorDialog { Color=color })if(dialog.ShowDialog(this)==DialogResult.OK)color=dialog.Color; };
            Button undo=new Button { Text=Localization.Get("ZeUndoPoint"),Width=90,Tag="manual",Visible=false };
            undo.Click+=(s,e)=> { if(draft.Count>0)draft.RemoveAt(draft.Count-1);RefreshViews(); };
            Button clearDraft=new Button { Text=Localization.Get("ZeClearDraft"),Width=85,Tag="manual",Visible=false };
            clearDraft.Click+=(s,e)=>ClearDraft();
            Button add=new Button { Text=Localization.Get("ZeAddZone"),Width=85,Tag="manual",Visible=false };
            add.Click+=(s,e)=>AddZone();
            chkFill.Text=Localization.Get("ZeFillZones");
            chkFill.Checked=true;chkFill.AutoSize=true;chkFill.ForeColor=Color.FromArgb(190,205,220);chkFill.Margin=new Padding(6,8,6,3);
            chkFill.CheckedChanged+=(s,e)=> { fillZones=chkFill.Checked;source.Invalidate();target.Invalidate(); };
            Button save=new Button { Text=Localization.Get("ZeSaveZones"),Width=105,BackColor=Color.FromArgb(20,70,40),ForeColor=Color.White };
            save.Click+=(s,e)=> {
                if(busy)return;
                if(draft.Count>0) { MessageBox.Show(this,Localization.Get("ZeDraftWarning"));return; }
                try { ZoneStore.Save(path,zones);saved(zones.Select(z=>z.Clone()).ToList());DialogResult=DialogResult.OK;Close(); }
                catch(InvalidDataException ex) { MessageBox.Show(this,Localization.T("ZeSaveZonesError",ex.Message)); }
                catch(IOException ex) { MessageBox.Show(this,Localization.T("ZeSaveZonesError",ex.Message)); }catch(UnauthorizedAccessException ex) { MessageBox.Show(this,ex.Message); }
            };

            bar.Controls.Add(open);
            bar.Controls.Add(importFile);
            bar.Controls.Add(exportFile);
            bar.Controls.Add(lblLayer);
            bar.Controls.Add(cboLayer);
            bar.Controls.Add(btnNewLayer);
            bar.Controls.Add(btnDeleteLayer);
            bar.Controls.Add(reset);
            bar.Controls.Add(name);
            bar.Controls.Add(choose);
            bar.Controls.Add(add);
            bar.Controls.Add(undo);
            bar.Controls.Add(clearDraft);
            bar.Controls.Add(chkFill);
            bar.Controls.Add(save);

            instruction.Dock=DockStyle.Top;instruction.Height=44;instruction.Padding=new Padding(8);
            SplitContainer split=new SplitContainer { Dock=DockStyle.Fill,Size=new Size(1130,500),SplitterDistance=565 };
            source.Dock=DockStyle.Fill;target.Dock=DockStyle.Fill;source.BackColor=target.BackColor=Color.FromArgb(12,17,22);
            split.Panel1.Controls.Add(source);split.Panel2.Controls.Add(target);

            Panel bottom=new Panel { Dock=DockStyle.Bottom,Height=104,Padding=new Padding(8),BackColor=Color.FromArgb(22,27,32) };
            FlowLayoutPanel listRow=new FlowLayoutPanel { Dock=DockStyle.Top,Height=46,WrapContents=false };
            list.Width=520;list.Height=42;list.IntegralHeight=false;list.BackColor=Color.FromArgb(28,33,40);list.ForeColor=Color.WhiteSmoke;
            Label filterLbl=new Label { Text=Localization.Get("ZeFilterZones"),AutoSize=true,Margin=new Padding(6,6,2,0) };
            filterBox.TextChanged+=(s,e)=>RefreshViews();
            detailsLabel.AutoSize=false;detailsLabel.Width=410;detailsLabel.Height=42;detailsLabel.Padding=new Padding(8,4,4,4);detailsLabel.ForeColor=Color.FromArgb(175,195,215);

            listRow.Controls.Add(list);
            listRow.Controls.Add(filterLbl);
            listRow.Controls.Add(filterBox);
            listRow.Controls.Add(detailsLabel);

            FlowLayoutPanel actRow=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=44,WrapContents=false,Padding=new Padding(0,4,0,0) };
            Button rename=new Button { Text=Localization.Get("ZeApplyName"),Width=95 };rename.Click+=(s,e)=>Rename(false);
            Button recolorGroup=new Button { Text=Localization.Get("ZeRecolorGroup"),Width=110 };recolorGroup.Click+=(s,e)=>RecolorGroup();
            Button group=new Button { Text=Localization.Get("ZeNameColour"),Width=125 };group.Click+=(s,e)=>Rename(true);
            Button moveUp=new Button { Text="▲ "+Localization.Get("ZeMoveUp"),Width=70 };moveUp.Click+=(s,e)=>MoveZone(-1);
            Button moveDown=new Button { Text="▼ "+Localization.Get("ZeMoveDown"),Width=70 };moveDown.Click+=(s,e)=>MoveZone(1);
            Button duplicate=new Button { Text=Localization.Get("ZeDuplicate"),Width=85 };duplicate.Click+=(s,e)=>DuplicateSelected();
            Button deselect=new Button { Text=Localization.Get("ZeDeselect"),Width=80 };deselect.Click+=(s,e)=>DeselectSelection();
            Button delete=new Button { Text=Localization.Get("ZeDeleteSelected"),Width=115 };delete.Click+=(s,e)=>DeleteSelected();
            Button deleteAll=new Button { Text=Localization.Get("ZeDeleteAll"),Width=110,BackColor=Color.FromArgb(85,25,25),ForeColor=Color.White };deleteAll.Click+=(s,e)=>DeleteAll();

            actRow.Controls.Add(rename);
            actRow.Controls.Add(recolorGroup);
            actRow.Controls.Add(group);
            actRow.Controls.Add(moveUp);
            actRow.Controls.Add(moveDown);
            actRow.Controls.Add(duplicate);
            actRow.Controls.Add(deselect);
            actRow.Controls.Add(delete);
            actRow.Controls.Add(deleteAll);

            bottom.Controls.Add(listRow);bottom.Controls.Add(actRow);
            Controls.Add(split);Controls.Add(instruction);Controls.Add(bar);Controls.Add(bottom);

            source.Paint+=(s,e)=>DrawSource(e.Graphics);target.Paint+=(s,e)=>DrawTarget(e.Graphics);
            source.MouseClick+=(s,e)=>ClickImage(true,e.Location);target.MouseClick+=(s,e)=>ClickImage(false,e.Location);
            list.SelectedIndexChanged+=(s,e)=> {
                int selIdx=GetSelectedZoneIndex();
                if(selIdx>=0 && selIdx<zones.Count) {
                    name.Text=zones[selIdx].Name;
                    color=Color.FromArgb(zones[selIdx].Argb);
                }
                UpdateDetails();
                source.Invalidate();target.Invalidate();
            };
            busyTimer=new Timer { Interval=25 };
            busyTimer.Tick+=(s,e)=> {
                if(!busy)return;
                spinAngle=(spinAngle+12f)%360f;
                if(source!=null && !source.IsDisposed)source.Invalidate();
                if(target!=null && !target.IsDisposed)target.Invalidate();
            };
            FormClosed+=(s,e)=> {
                if(busyTimer!=null) { busyTimer.Stop(); busyTimer.Dispose(); }
                if(screenshot!=null)screenshot.Dispose();
                if(editorPreview!=null && editorPreview!=map)editorPreview.Dispose();
            };
            OverlayTheme.Style(this);
            bar.BackColor=OverlayTheme.Surface;
            bottom.BackColor=OverlayTheme.Surface;
            instruction.BackColor=OverlayTheme.Surface;
            instruction.ForeColor=OverlayTheme.Accent;
            cboLayer.BackColor=OverlayTheme.Surface;
            cboLayer.ForeColor=OverlayTheme.Ink;
            btnNewLayer.BackColor=OverlayTheme.Surface;
            btnNewLayer.ForeColor=OverlayTheme.Ink;
            btnDeleteLayer.BackColor=OverlayTheme.Surface;
            btnDeleteLayer.ForeColor=OverlayTheme.Ink;
            name.BackColor=OverlayTheme.Background;
            name.ForeColor=OverlayTheme.Ink;
            filterBox.BackColor=OverlayTheme.Background;
            filterBox.ForeColor=OverlayTheme.Ink;
            list.BackColor=OverlayTheme.Background;
            list.ForeColor=OverlayTheme.Ink;
            save.BackColor=OverlayTheme.Accent;
            save.ForeColor=Color.FromArgb(14,17,20);
            PopulateLayerCombo();
            RefreshViews();UpdateInstruction();
        }
        RectangleF Fit(Control panel,Image image) {
            float scale=Math.Min((float)panel.Width/image.Width,(float)panel.Height/image.Height);
            return new RectangleF((panel.Width-image.Width*scale)/2,(panel.Height-image.Height*scale)/2,image.Width*scale,image.Height*scale);
        }
        async void OpenScreenshot() {
            if(busy)return;
            try {
                using(OpenFileDialog dialog=new OpenFileDialog { Filter=Localization.Get("ZeMapScreenshotsFilter"),Title=Localization.Get("ZeSelectScreenshotTitle") }) {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    await ImportAutomatic(dialog.FileName);
                }
            } catch(Exception ex) {
                try { MessageBox.Show(this, ex.Message, Localization.Get("ZoneEditorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error); } catch {}
            }
        }
        public async Task ImportAutomatic(string file) {
            if(busy)return;
            ImportMode mode = ImportMode.NewLayer;
            string chosenLayer = Path.GetFileNameWithoutExtension(file);
            if(string.IsNullOrWhiteSpace(chosenLayer)) chosenLayer = "Faction War";

            if(zones.Count > 0) {
                mode = ShowImportDialog(file, GetSelectedLayer() ?? "Default", out chosenLayer);
                if(mode == ImportMode.Cancel) return;
            }

            busy=true;
            spinAngle=0f;
            if(busyTimer!=null)busyTimer.Start();
            source.Invalidate();
            target.Invalidate();
            instruction.Text=Localization.Get("ZeAligningLocally");
            string folder=Path.GetDirectoryName(path),temp=Path.Combine(Path.GetTempPath(),"scum-zones-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                string python=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Python","Python312","python.exe");
                string config=Path.Combine(folder,"python-path.txt");if(File.Exists(config))python=File.ReadAllText(config).Trim();
                if(!File.Exists(python)) {
                    string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string[] pyCandidates = new string[] {
                        Path.Combine(localApp, "Programs", "Python", "Python313", "python.exe"),
                        Path.Combine(localApp, "Programs", "Python", "Python311", "python.exe"),
                        Path.Combine(localApp, "Programs", "Python", "Python310", "python.exe")
                    };
                    foreach(string pyCand in pyCandidates) {
                        if(File.Exists(pyCand)) { python = pyCand; break; }
                    }
                }
                if(!File.Exists(python))python="python.exe";
                string output=Path.Combine(temp,"zones.tsv"),sourceOutput=Path.Combine(temp,"source.tsv");
                Func<string,string> quote=value=>"\""+value.Replace("\"","")+"\"";
                string referenceFile=Path.Combine(temp,"reference.png");
                string[] mapCandidates = new string[] {
                    Path.Combine(folder, "map.png"),
                    Path.Combine(folder, "..", "resources", "map.png"),
                    Path.Combine(folder, "resources", "map.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "map.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "resources", "map.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "map.png")
                };
                foreach(string cand in mapCandidates) {
                    try {
                        if(File.Exists(cand)) {
                            File.Copy(cand, referenceFile, true);
                            break;
                        }
                    } catch {}
                }
                if(!File.Exists(referenceFile)) {
                    try {
                        using(Stream s = typeof(MapZone).Assembly.GetManifestResourceStream("map.png")) {
                            if(s != null) {
                                using(FileStream fs = new FileStream(referenceFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
                                    s.CopyTo(fs);
                                }
                            }
                        }
                    } catch {}
                }
                if(!File.Exists(referenceFile) && map != null) {
                    try {
                        lock(map) {
                            map.Save(referenceFile, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    } catch {}
                }
                if(!File.Exists(referenceFile)) {
                    throw new FileNotFoundException("Base map reference image could not be prepared for zone detection.");
                }
                string detectScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detect_zones.py");
                if(!File.Exists(detectScript)) {
                    string[] candidates = new string[] {
                        Path.Combine(folder, "detect_zones.py"),
                        Path.Combine(folder, "..", "detect_zones.py"),
                        Path.Combine(folder, "..", "packaging", "detect_zones.py"),
                        Path.Combine(folder, "packaging", "detect_zones.py"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packaging", "detect_zones.py"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "packaging", "detect_zones.py"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SkynettMiniMap", "detect_zones.py"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "detect_zones.py")
                    };
                    foreach(string cand in candidates) {
                        try {
                            if(File.Exists(cand)) { detectScript = Path.GetFullPath(cand); break; }
                        } catch {}
                    }
                }
                if(!File.Exists(detectScript)) {
                    try {
                        using(Stream stream = typeof(MapZone).Assembly.GetManifestResourceStream("detect_zones.py")) {
                            if(stream != null) {
                                string extracted = Path.Combine(temp, "detect_zones.py");
                                using(FileStream fs = new FileStream(extracted, FileMode.Create, FileAccess.Write)) {
                                    stream.CopyTo(fs);
                                }
                                detectScript = extracted;
                            }
                        }
                    } catch {}
                }
                if(!File.Exists(detectScript)) {
                    throw new FileNotFoundException("detect_zones.py could not be found on disk or embedded resources.");
                }
                string scummapFile = null;
                string[] scummapCandidates = new string[] {
                    Path.Combine(folder, "scummap.bin"),
                    Path.Combine(folder, "..", "resources", "scummap.bin"),
                    Path.Combine(folder, "resources", "scummap.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "scummap.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "resources", "scummap.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scummap.bin")
                };
                foreach(string cand in scummapCandidates) {
                    try {
                        if(File.Exists(cand)) { scummapFile = Path.GetFullPath(cand); break; }
                    } catch {}
                }
                if(string.IsNullOrEmpty(scummapFile)) {
                    try {
                        using(Stream s = typeof(ScumMapStore).Assembly.GetManifestResourceStream("scummap.bin")) {
                            if(s != null) {
                                string extracted = Path.Combine(temp, "scummap.bin");
                                using(FileStream fs = new FileStream(extracted, FileMode.Create, FileAccess.Write, FileShare.None)) {
                                    s.CopyTo(fs);
                                }
                                scummapFile = extracted;
                            }
                        }
                    } catch {}
                }
                string scumArg = (!string.IsNullOrEmpty(scummapFile) && File.Exists(scummapFile)) ? (" --scummap " + quote(scummapFile)) : "";
                ProcessStartInfo info=new ProcessStartInfo(python,quote(detectScript)+" --input "+quote(file)+" --reference "+quote(referenceFile)+" --output "+quote(output)+" --source-output "+quote(sourceOutput)+scumArg) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
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

                string targetLayerName = (mode == ImportMode.NewLayer) ? chosenLayer : (GetSelectedLayer() ?? "Default");
                foreach(MapZone importedZone in imported) {
                    importedZone.Category = importedZone.IsFaction ? ZoneCategory.Faction : ZoneStore.Classify(importedZone);
                    if(importedZone.Category == ZoneCategory.Unknown) importedZone.Category = ZoneCategory.Custom;
                    importedZone.Layer = targetLayerName;
                }
                foreach(MapZone sourceZone in sourceImported) {
                    sourceZone.Category = sourceZone.IsFaction ? ZoneCategory.Faction : ZoneStore.Classify(sourceZone);
                    if(sourceZone.Category == ZoneCategory.Unknown) sourceZone.Category = ZoneCategory.Custom;
                    sourceZone.Layer = targetLayerName;
                }
                using(Image loaded=Image.FromFile(file)) { Image copy=new Bitmap(loaded);if(screenshot!=null)screenshot.Dispose();screenshot=copy; }

                if(mode == ImportMode.Replace) {
                    zones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase));
                    sourceZones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase));
                }

                int insertIdx = zones.Count;
                zones.AddRange(imported);sourceZones.AddRange(sourceImported);draft.Clear();automatic=true;step=4;
                PopulateLayerCombo(targetLayerName);
                RefreshViews();if(zones.Count>0)SetSelectedZoneIndex(insertIdx);UpdateInstruction();
                try { File.WriteAllText(Path.Combine(folder,"last-detection.json"),report); }catch(IOException) {}catch(UnauthorizedAccessException) {}
            } catch(Exception ex) {
                if(!IsDisposed) { instruction.Text=Localization.Get("ZeAutoFailedKept");MessageBox.Show(this,Localization.T("ZeCouldNotImportAuto",ex.Message)); }
            } finally {
                if(busyTimer!=null)busyTimer.Stop();
                busy=false;
                if(!IsDisposed) {
                    source.Invalidate();
                    target.Invalidate();
                }
                // Only files generated inside this unique import directory are removed.
                try {
                    foreach(string leaf in new[]{"zones.tsv","source.tsv","reference.png","detect_zones.py","scummap.bin"}) {
                        string generated=Path.Combine(temp,leaf);
                        if(File.Exists(generated)) {
                            try { File.Delete(generated); } catch {}
                        }
                    }
                    if(Directory.Exists(temp)) Directory.Delete(temp, true);
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
            int index=GetSelectedZoneIndex();
            if(index<0 || index>=zones.Count)return;
            if(string.IsNullOrWhiteSpace(name.Text) || name.Text.Length>80) { MessageBox.Show(this,Localization.Get("ZeLabelLengthWarning"));return; }
            int original=zones[index].Argb;
            for(int i=0;i<zones.Count;i++)if(i==index || (sameColour && zones[i].Argb==original)) {
                zones[i].Name=name.Text.Trim();zones[i].Argb=color.ToArgb();
                if(automatic && i<sourceZones.Count) { sourceZones[i].Name=zones[i].Name;sourceZones[i].Argb=zones[i].Argb; }
            }
            RefreshViews();SetSelectedZoneIndex(index);
        }
        void RecolorGroup() {
            if(busy || list.SelectedIndex<0)return;
            int index=GetSelectedZoneIndex();
            if(index<0 || index>=zones.Count)return;
            int originalArgb=zones[index].Argb;
            int newArgb=color.ToArgb();
            for(int i=0;i<zones.Count;i++)if(zones[i].Argb==originalArgb) {
                zones[i].Argb=newArgb;
                if(automatic && i<sourceZones.Count)sourceZones[i].Argb=newArgb;
            }
            RefreshViews();SetSelectedZoneIndex(index);
        }
        void MoveZone(int direction) {
            if(busy)return;
            int idx=GetSelectedZoneIndex();
            int targetIdx=idx+direction;
            if(idx<0 || targetIdx<0 || targetIdx>=zones.Count)return;
            MapZone temp=zones[idx];zones[idx]=zones[targetIdx];zones[targetIdx]=temp;
            if(automatic && idx<sourceZones.Count && targetIdx<sourceZones.Count) {
                MapZone stemp=sourceZones[idx];sourceZones[idx]=sourceZones[targetIdx];sourceZones[targetIdx]=stemp;
            }
            RefreshViews();SetSelectedZoneIndex(targetIdx);
        }
        void DuplicateSelected() {
            if(busy)return;
            int idx=GetSelectedZoneIndex();
            if(idx<0 || idx>=zones.Count)return;
            MapZone copy=zones[idx].Clone();
            copy.Name=zones[idx].Name+" (Copy)";
            zones.Insert(idx+1,copy);
            if(automatic && idx<sourceZones.Count) {
                sourceZones.Insert(idx+1,sourceZones[idx].Clone());
            }
            RefreshViews();SetSelectedZoneIndex(idx+1);
        }
        void DeselectSelection() {
            list.SelectedIndex=-1;name.Text=Localization.Get("ZeNewZone");
            UpdateDetails();source.Invalidate();target.Invalidate();
        }
        void ClearDraft() {
            draft.Clear();RefreshViews();UpdateInstruction();
        }
        void DeleteSelected() {
            if(busy)return;
            int index=GetSelectedZoneIndex();
            if(index>=0 && index<zones.Count) {
                zones.RemoveAt(index);
                if(automatic && index<sourceZones.Count)sourceZones.RemoveAt(index);
                RefreshViews();
                if(zones.Count>0) {
                    int nextIdx=Math.Min(index,zones.Count-1);
                    SetSelectedZoneIndex(nextIdx);
                } else {
                    name.Text=Localization.Get("ZeNewZone");
                }
                UpdateInstruction();
            }
        }
        void DeleteAll() {
            if(busy || zones.Count==0)return;
            if(MessageBox.Show(this,Localization.Get("ZeDeleteAllConfirm"),Localization.Get("ZeDeleteAllTitle"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes) {
                zones.Clear();sourceZones.Clear();draft.Clear();
                PopulateLayerCombo();
                RefreshViews();name.Text=Localization.Get("ZeNewZone");
                UpdateInstruction();
            }
        }
        void ExportZones() {
            if(busy)return;
            if(zones.Count==0) {
                MessageBox.Show(this,Localization.Get("ZeNoZonesToExport"),Localization.Get("ZeExportTitle"),MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            using(SaveFileDialog sfd=new SaveFileDialog {
                Filter="JSON files (*.json)|*.json|TSV files (*.tsv)|*.tsv|All files (*.*)|*.*",
                Title=Localization.Get("ZeExportTitle"),FileName="zones.json"
            }) {
                if(sfd.ShowDialog(this)==DialogResult.OK) {
                    try {
                        ZoneStore.Save(sfd.FileName,zones);
                        MessageBox.Show(this,Localization.T("ZeExportSuccess",zones.Count,Path.GetFileName(sfd.FileName)),Localization.Get("ZeExportTitle"),MessageBoxButtons.OK,MessageBoxIcon.Information);
                    } catch(Exception ex) {
                        MessageBox.Show(this,Localization.T("ZeSaveZonesError",ex.Message));
                    }
                }
            }
        }
        void ImportFile() {
            if(busy)return;
            using(OpenFileDialog ofd=new OpenFileDialog {
                Filter="Zone files (*.json;*.tsv)|*.json;*.tsv|All files (*.*)|*.*",
                Title=Localization.Get("ZeImportTitle")
            }) {
                if(ofd.ShowDialog(this)==DialogResult.OK) {
                    try {
                        List<MapZone> imported=ZoneStore.Load(ofd.FileName);
                        if(imported.Count==0) {
                            MessageBox.Show(this,Localization.Get("ZeNoUsableZonesFound"),Localization.Get("ZeImportTitle"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                            return;
                        }

                        ImportMode mode = ImportMode.NewLayer;
                        string chosenLayer = Path.GetFileNameWithoutExtension(ofd.FileName);
                        if(string.IsNullOrWhiteSpace(chosenLayer)) chosenLayer = "Imported Layer";

                        if(zones.Count > 0) {
                            mode = ShowImportDialog(ofd.FileName, GetSelectedLayer() ?? "Default", out chosenLayer);
                            if(mode == ImportMode.Cancel) return;
                        }

                        string targetLayerName = (mode == ImportMode.NewLayer) ? chosenLayer : (GetSelectedLayer() ?? "Default");

                        foreach(MapZone importedZone in imported) {
                            if(importedZone.Category == ZoneCategory.Unknown) {
                                importedZone.Category = importedZone.IsFaction ? ZoneCategory.Faction : ZoneStore.Classify(importedZone);
                                if(importedZone.Category == ZoneCategory.Unknown) importedZone.Category = ZoneCategory.Custom;
                            }
                            if(mode == ImportMode.NewLayer || string.IsNullOrWhiteSpace(importedZone.Layer) || importedZone.Layer == "Default") {
                                importedZone.Layer = targetLayerName;
                            }
                        }

                        if(mode == ImportMode.Replace) {
                            zones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase));
                            sourceZones.RemoveAll(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase));
                        }

                        int insertIdx = zones.Count;
                        zones.AddRange(imported);draft.Clear();
                        PopulateLayerCombo(targetLayerName);
                        RefreshViews();
                        if(zones.Count>0)SetSelectedZoneIndex(insertIdx);UpdateInstruction();
                        MessageBox.Show(this,Localization.T("ZeImportSuccess",imported.Count,Path.GetFileName(ofd.FileName)),Localization.Get("ZeImportTitle"),MessageBoxButtons.OK,MessageBoxIcon.Information);
                    } catch(Exception ex) {
                        MessageBox.Show(this,Localization.T("ZeCouldNotImportAuto",ex.Message));
                    }
                }
            }
        }
        void ClickImage(bool isSource,Point point) {
            if(busy)return;
            if(isSource && screenshot==null)return;
            RectangleF rect=Fit(isSource?source:target,isSource?screenshot:map);if(!rect.Contains(point))return;
            PointF p=new PointF((point.X-rect.X)/rect.Width,(point.Y-rect.Y)/rect.Height);
            if(automatic || zones.Count>0) {
                string selectedLayer = GetSelectedLayer();
                List<MapZone> items=(automatic && isSource)?sourceZones:zones;
                for(int i=items.Count-1;i>=0;i--) {
                    if(selectedLayer != null) {
                        string lay = string.IsNullOrWhiteSpace(items[i].Layer) ? "Default" : items[i].Layer;
                        if(!string.Equals(lay, selectedLayer, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    if(items[i].Contains(p)) {
                        SetSelectedZoneIndex(i);
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
        void AddZone() {
            if(automatic || busy)return;
            if(step!=4 || draft.Count<3) { MessageBox.Show(this,Localization.Get("ZePointsRequirementWarning"));return; }
            if(string.IsNullOrWhiteSpace(name.Text) || name.Text.Length>80 || zones.Count>=500) { MessageBox.Show(this,Localization.Get("ZeZonesMaxWarning"));return; }
            string targetLayer = GetSelectedLayer() ?? "Default";
            zones.Add(new MapZone { Name=name.Text.Trim(),Argb=color.ToArgb(),Points=draft.Select(p=>ZoneStore.Align(p,s1,s2,m1,m2)).ToArray(),Category=ZoneCategory.Custom,Layer=targetLayer });
            draft.Clear();
            PopulateLayerCombo(targetLayer);
            RefreshViews();
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
        void DrawSpinner(Graphics g,Rectangle bounds,string message) {
            if(bounds.Width<60 || bounds.Height<60)return;
            SmoothingMode prevSmooth=g.SmoothingMode;
            g.SmoothingMode=SmoothingMode.AntiAlias;

            float cardW=Math.Min(360f,bounds.Width-30f);
            float cardH=136f;
            float cx=bounds.X+bounds.Width/2f;
            float cy=bounds.Y+bounds.Height/2f;
            RectangleF cardRect=new RectangleF(cx-cardW/2f,cy-cardH/2f,cardW,cardH);

            using(GraphicsPath path=new GraphicsPath()) {
                float r=12f;
                path.AddArc(cardRect.X,cardRect.Y,r*2,r*2,180,90);
                path.AddArc(cardRect.Right-r*2,cardRect.Y,r*2,r*2,270,90);
                path.AddArc(cardRect.Right-r*2,cardRect.Bottom-r*2,r*2,r*2,0,90);
                path.AddArc(cardRect.X,cardRect.Bottom-r*2,r*2,r*2,90,90);
                path.CloseFigure();

                using(Brush bgBrush=new SolidBrush(Color.FromArgb(220,18,20,24))) {
                    g.FillPath(bgBrush,path);
                }
                using(Pen borderPen=new Pen(Color.FromArgb(160,OverlayTheme.BorderHighlight),1.5f)) {
                    g.DrawPath(borderPen,path);
                }
            }

            float spinR=22f;
            float spinCenterY=cy-20f;
            RectangleF spinRect=new RectangleF(cx-spinR,spinCenterY-spinR,spinR*2,spinR*2);

            using(Pen trackPen=new Pen(Color.FromArgb(40,255,255,255),4f)) {
                g.DrawEllipse(trackPen,spinRect);
            }
            using(Pen arcPen=new Pen(OverlayTheme.Accent,4f)) {
                arcPen.StartCap=LineCap.Round;
                arcPen.EndCap=LineCap.Round;
                g.DrawArc(arcPen,spinRect,spinAngle,100f);
            }
            using(Brush dotBrush=new SolidBrush(Color.FromArgb(180,OverlayTheme.Accent))) {
                g.FillEllipse(dotBrush,cx-3f,spinCenterY-3f,6f,6f);
            }

            string text=string.IsNullOrEmpty(message)?Localization.Get("ZeAligningLocally"):message;
            using(Font font=new Font("Segoe UI",9.5f,FontStyle.Bold))
            using(StringFormat sf=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisWord }) {
                RectangleF textRect=new RectangleF(cardRect.X+14f,cy+12f,cardRect.Width-28f,48f);
                using(Brush shadowBrush=new SolidBrush(Color.FromArgb(200,0,0,0))) {
                    g.DrawString(text,font,shadowBrush,new RectangleF(textRect.X+1f,textRect.Y+1f,textRect.Width,textRect.Height),sf);
                }
                using(Brush textBrush=new SolidBrush(OverlayTheme.Ink)) {
                    g.DrawString(text,font,textBrush,textRect,sf);
                }
            }

            g.SmoothingMode=prevSmooth;
        }
        void DrawSource(Graphics g) {
            if(screenshot!=null) {
                RectangleF rect=Fit(source,screenshot);g.DrawImage(screenshot,rect);
                string selectedLayer = GetSelectedLayer();
                List<MapZone> activeSource = (selectedLayer == null) ? sourceZones : sourceZones.Where(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, selectedLayer, StringComparison.OrdinalIgnoreCase)).ToList();
                if(automatic) { DrawDetected(g,activeSource,rect); }
                else {
                    if(step>=1)Cross(g,rect,s1,"1");if(step>=3)Cross(g,rect,s2,"2");
                    PointF[] points=draft.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                    if(points.Length>1)using(Pen pen=new Pen(color,2))g.DrawLines(pen,points);
                    foreach(PointF p in points)g.FillEllipse(Brushes.White,p.X-3,p.Y-3,6,6);
                }
            }
            if(busy)DrawSpinner(g,source.ClientRectangle,Localization.Get("ZeAligningLocally"));
        }
        void DrawTarget(Graphics g) {
            Image targetMap=editorPreview??map;
            if(targetMap!=null) {
                RectangleF rect=Fit(target,targetMap);g.DrawImage(targetMap,rect);
                string selectedLayer = GetSelectedLayer();
                List<MapZone> activeZones = (selectedLayer == null) ? zones : zones.Where(z => string.Equals(string.IsNullOrWhiteSpace(z.Layer) ? "Default" : z.Layer, selectedLayer, StringComparison.OrdinalIgnoreCase)).ToList();
                if(automatic) { DrawDetected(g,activeZones,rect); }
                else {
                    if(fillZones) ZoneStore.Draw(g,activeZones,rect,true);
                    else {
                        foreach(MapZone item in activeZones) {
                            if(item.Points==null || item.Points.Length<2) continue;
                            PointF[] pts=item.Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                            using(Pen pen=new Pen(Color.FromArgb(item.Argb),2f)) {
                                if(pts.Length>=3) g.DrawPolygon(pen,pts);
                                else g.DrawLines(pen,pts);
                            }
                        }
                    }
                    int selIdx=GetSelectedZoneIndex();
                    if(selIdx>=0 && selIdx<zones.Count && zones[selIdx].Points!=null && zones[selIdx].Points.Length>0) {
                        PointF[] points=zones[selIdx].Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                        if(points.Length>=3) {
                            using(Pen bgPen=new Pen(Color.FromArgb(200,0,0,0),4.5f))g.DrawPolygon(bgPen,points);
                            using(Pen pen=new Pen(OverlayTheme.Accent,2.5f))g.DrawPolygon(pen,points);
                        } else {
                            using(Pen bgPen=new Pen(Color.FromArgb(200,0,0,0),4.5f))g.DrawEllipse(bgPen,points[0].X-12,points[0].Y-12,24,24);
                            using(Pen pen=new Pen(OverlayTheme.Accent,2.5f))g.DrawEllipse(pen,points[0].X-12,points[0].Y-12,24,24);
                        }
                    }
                    if(step>=2)Cross(g,rect,m1,"1");if(step>=4)Cross(g,rect,m2,"2");
                    if(step==4 && draft.Count>=3)ZoneStore.Draw(g,new[]{new MapZone { Name=name.Text,Argb=color.ToArgb(),Points=draft.Select(p=>ZoneStore.Align(p,s1,s2,m1,m2)).ToArray(),Layer=selectedLayer??"Default" }},rect,true);
                }
            }
            if(busy)DrawSpinner(g,target.ClientRectangle,Localization.Get("ZeAligningLocally"));
        }
        void DrawDetected(Graphics g,List<MapZone> items,RectangleF rect) {
            if(fillZones) ZoneStore.Draw(g,items,rect,true,9f,false,null,false);
            else {
                foreach(MapZone item in items) {
                    if(item.Points==null || item.Points.Length<2) continue;
                    PointF[] pts=item.Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                    using(Pen pen=new Pen(Color.FromArgb(item.Argb),2f)) {
                        if(pts.Length>=3) g.DrawPolygon(pen,pts);
                        else g.DrawLines(pen,pts);
                    }
                }
            }
            int selIdx=GetSelectedZoneIndex();
            MapZone selectedZone = (selIdx >= 0 && selIdx < zones.Count) ? zones[selIdx] : null;
            for(int i=0;i<items.Count;i++) {
                PointF[] points=items[i].Points.Select(p=>new PointF(rect.X+p.X*rect.Width,rect.Y+p.Y*rect.Height)).ToArray();
                if(items[i] == selectedZone || (selIdx == i && items.Count == zones.Count)) {
                    if(points.Length>=3) {
                        using(Pen bgPen=new Pen(Color.FromArgb(200,0,0,0),4.5f))g.DrawPolygon(bgPen,points);
                        using(Pen pen=new Pen(OverlayTheme.Accent,2.5f))g.DrawPolygon(pen,points);
                    } else {
                        using(Pen bgPen=new Pen(Color.FromArgb(200,0,0,0),4.5f))g.DrawEllipse(bgPen,points[0].X-12,points[0].Y-12,24,24);
                        using(Pen pen=new Pen(OverlayTheme.Accent,2.5f))g.DrawEllipse(pen,points[0].X-12,points[0].Y-12,24,24);
                    }
                }
                float x=points.Average(p=>p.X),y=points.Average(p=>p.Y);
                int num = zones.IndexOf(items[i]) + 1;
                if(num <= 0) num = i + 1;
                g.DrawString(num.ToString(),Font,Brushes.Black,x+1,y+1);
                g.DrawString(num.ToString(),Font,Brushes.White,x,y);
            }
        }
    }
}
