using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScumMiniMap {
    public sealed class ScumMapCategory {
        public int Id;
        public string Section;
        public string Name;
        public int ColorBackground;
        public int ColorIcon;
        public bool DefaultEnabled;
        public int MarkerCount;

        public override string ToString() {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1})", Name, MarkerCount);
        }
    }

    public sealed class ScumMapMarker {
        public int Id;
        public int CategoryId;
        public float X; // 0..1 normalized map coordinate
        public float Y; // 0..1 normalized map coordinate
        public string Title;

        public PointF Point { get { return new PointF(X, Y); } }
    }

    public sealed class ScumMapHabitat {
        public string Name,Summary;
        public int Color;
        public int[] CategoryIds;
        public PointF[][] Rings;
        internal float MinX, MaxX, MinY, MaxY;
        internal bool BoundsReady;
        public void ComputeBounds() {
            if(Rings==null || Rings.Length==0) { MinX=MaxX=MinY=MaxY=0; BoundsReady=true; return; }
            MinX=float.MaxValue; MaxX=float.MinValue;
            MinY=float.MaxValue; MaxY=float.MinValue;
            foreach(PointF[] ring in Rings) {
                if(ring==null) continue;
                for(int i=0; i<ring.Length; i++) {
                    float x=ring[i].X, y=ring[i].Y;
                    if(x<MinX) MinX=x; if(x>MaxX) MaxX=x;
                    if(y<MinY) MinY=y; if(y>MaxY) MaxY=y;
                }
            }
            BoundsReady=true;
        }
        public bool Contains(PointF point) {
            if(!BoundsReady) ComputeBounds();
            if(point.X < MinX || point.X > MaxX || point.Y < MinY || point.Y > MaxY) return false;
            int winding=0;
            foreach(PointF[] ring in Rings) {
                for(int i=0,j=ring.Length-1;i<ring.Length;j=i++) {
                    PointF a=ring[j],b=ring[i];
                    double cross=((double)b.X-a.X)*(point.Y-a.Y)-((double)point.X-a.X)*(b.Y-a.Y);
                    if(Math.Abs(cross)<1e-12 && point.X>=Math.Min(a.X,b.X) && point.X<=Math.Max(a.X,b.X) && point.Y>=Math.Min(a.Y,b.Y) && point.Y<=Math.Max(a.Y,b.Y)) return true;
                    if(a.Y<=point.Y) { if(b.Y>point.Y && cross>0) winding++; }
                    else if(b.Y<=point.Y && cross<0) winding--;
                }
            }
            return winding!=0;
        }
        public GraphicsPath CreatePath() {
            GraphicsPath path=new GraphicsPath(FillMode.Winding);
            foreach(PointF[] ring in Rings) if(ring.Length>=3) path.AddPolygon(ring);
            return path;
        }
    }

    public sealed class ScumMapStore {
        public readonly List<ScumMapHabitat> Habitats=new List<ScumMapHabitat>();
        public readonly List<ScumMapCategory> Categories = new List<ScumMapCategory>();
        public readonly Dictionary<int, ScumMapCategory> CategoryById = new Dictionary<int, ScumMapCategory>();
        public readonly List<string> Sections = new List<string>();
        public readonly Dictionary<string, List<ScumMapCategory>> CategoriesBySection = new Dictionary<string, List<ScumMapCategory>>(StringComparer.OrdinalIgnoreCase);
        public readonly List<ScumMapMarker> Markers = new List<ScumMapMarker>();

        // 32x32 spatial index covering 0..1 map coordinate space
        const int GridSize = 32;
        readonly List<int>[,] _grid = new List<int>[GridSize, GridSize];

        // Filter state
        readonly HashSet<int> _disabledCategories = new HashSet<int>();
        public bool MasterEnabled = true;

        public struct SettlementAnchor {
            public string Name;
            public float X;
            public float Y;
            public float Radius;
        }
        readonly List<SettlementAnchor> _settlements = new List<SettlementAnchor>();
        public List<SettlementAnchor> Settlements { get { return _settlements; } }

        public void IndexSettlements() {
            _settlements.Clear();
            for (int i = 0; i < Markers.Count; i++) {
                ScumMapMarker sm = Markers[i];
                if (sm.CategoryId == 27 && !string.IsNullOrEmpty(sm.Title)) {
                    _settlements.Add(new SettlementAnchor { Name = sm.Title.Trim(), X = sm.X, Y = sm.Y, Radius = 0.020f });
                } else if (sm.CategoryId == 26 && !string.IsNullOrEmpty(sm.Title) && (sm.Title.IndexOf("City", StringComparison.OrdinalIgnoreCase) >= 0 || sm.Title.Equals("Novigrad", StringComparison.OrdinalIgnoreCase))) {
                    string sname = sm.Title.Replace(" City", "").Trim();
                    _settlements.Add(new SettlementAnchor { Name = sname, X = sm.X, Y = sm.Y, Radius = 0.040f });
                } else if (sm.CategoryId == 80 && !string.IsNullOrEmpty(sm.Title)) {
                    _settlements.Add(new SettlementAnchor { Name = sm.Title.Trim(), X = sm.X, Y = sm.Y, Radius = 0.015f });
                }
            }
        }

        public static string ActivitySection(int categoryId) {
            switch(categoryId) {
                case 858: case 81: return "Fishing";
                case 869: case 859: case 17: case 776: case 24: return "Hunting";
                default:
                    if (categoryId <= -1000 && categoryId > -2000) return "Fishing";
                    if (categoryId <= -2000 && categoryId > -3000) return "Hunting";
                    return null;
            }
        }

        public ScumMapStore() {
            for (int x = 0; x < GridSize; x++) {
                for (int y = 0; y < GridSize; y++) {
                    _grid[x, y] = new List<int>();
                }
            }
        }

        public static ScumMapStore Load(string folder) {
            ScumMapStore store = new ScumMapStore();
            string binPath = Path.Combine(folder ?? "", "scummap.bin");
            if (File.Exists(binPath)) {
                try {
                    using (Stream s = File.OpenRead(binPath)) { store.ReadBinary(s); return store; }
                } catch(IOException) { store=new ScumMapStore(); }
                  catch(InvalidDataException) { store=new ScumMapStore(); }
                  catch(UnauthorizedAccessException) { store=new ScumMapStore(); }
            }
            using (Stream s = typeof(ScumMapStore).Assembly.GetManifestResourceStream("scummap.bin")) {
                if (s != null) {
                    store.ReadBinary(s);
                    return store;
                }
            }
            return store;
        }

        public void ReadBinary(Stream stream) {
            if (stream == null) return;
            using (BinaryReader r = new BinaryReader(stream, Encoding.UTF8)) {
                byte[] magic = r.ReadBytes(4);
                if (magic.Length < 4 || magic[0] != 'S' || magic[1] != 'C' || magic[2] != 'M' || magic[3] != 'P') {
                    throw new InvalidDataException("Invalid scummap.bin magic header.");
                }
                ushort version = r.ReadUInt16();
                if(version!=1 && version!=2) throw new InvalidDataException("Unsupported map data version.");
                ushort catCount = r.ReadUInt16();
                if(catCount>4096) throw new InvalidDataException("Too many map categories.");
                long textBytes=0, geometryPoints=0, ringCount=0;

                Categories.Clear();
                CategoryById.Clear();
                Sections.Clear();
                CategoriesBySection.Clear();
                _disabledCategories.Clear();
                Habitats.Clear();

                for (int i = 0; i < catCount; i++) {
                    int id = r.ReadInt32();
                    byte secLen = r.ReadByte();
                    string sec = ReadExactText(r,secLen);
                    sec = ActivitySection(id) ?? sec;
                    byte nameLen = r.ReadByte();
                    string name = ReadExactText(r,nameLen);
                    int bg = r.ReadInt32();
                    int fg = r.ReadInt32();
                    bool def = r.ReadByte() != 0;

                    ScumMapCategory cat = new ScumMapCategory {
                        Id = id,
                        Section = sec,
                        Name = name,
                        ColorBackground = bg,
                        ColorIcon = fg,
                        DefaultEnabled = def
                    };
                    Categories.Add(cat);
                    if(CategoryById.ContainsKey(id)) throw new InvalidDataException("Duplicate map category.");
                    CategoryById[id] = cat;

                    if (!CategoriesBySection.ContainsKey(sec)) {
                        Sections.Add(sec);
                        CategoriesBySection[sec] = new List<ScumMapCategory>();
                    }
                    CategoriesBySection[sec].Add(cat);

                    if (!def) {
                        _disabledCategories.Add(id);
                    }
                }

                int markerCount = ReadCount(r,200000);
                Markers.Clear();
                for (int x = 0; x < GridSize; x++) {
                    for (int y = 0; y < GridSize; y++) {
                        _grid[x, y].Clear();
                    }
                }

                for (int i = 0; i < markerCount; i++) {
                    int id = r.ReadInt32();
                    int catId = r.ReadInt32();
                    float x = r.ReadSingle();
                    float y = r.ReadSingle();
                    if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||!CategoryById.ContainsKey(catId))
                        throw new InvalidDataException("Invalid map marker.");
                    ushort titleLen = r.ReadUInt16();
                    textBytes+=titleLen;
                    if(textBytes>16*1024*1024) throw new InvalidDataException("Map text exceeds the size limit.");
                    string title = titleLen > 0 ? ReadExactText(r,titleLen) : null;

                    ScumMapMarker m = new ScumMapMarker {
                        Id = id,
                        CategoryId = catId,
                        X = Math.Max(0f, Math.Min(1f, x)),
                        Y = Math.Max(0f, Math.Min(1f, y)),
                        Title = title
                    };
                    int markerIdx = Markers.Count;
                    Markers.Add(m);

                    if (CategoryById.ContainsKey(catId)) {
                        CategoryById[catId].MarkerCount++;
                    }

                    int gx = Math.Max(0, Math.Min(GridSize - 1, (int)(m.X * GridSize)));
                    int gy = Math.Max(0, Math.Min(GridSize - 1, (int)(m.Y * GridSize)));
                    _grid[gx, gy].Add(markerIdx);
                }
                if(version>=2) {
                    int count=ReadCount(r,10000);
                    for(int i=0;i<count;i++) {
                        ScumMapHabitat habitat=new ScumMapHabitat { Name=ReadText(r),Summary=ReadText(r),Color=r.ReadInt32() };
                        textBytes+=habitat.Name.Length+habitat.Summary.Length;
                        if(textBytes>16*1024*1024) throw new InvalidDataException("Habitat text exceeds the size limit.");
                        habitat.CategoryIds=new int[ReadCount(r,100)];
                        for(int j=0;j<habitat.CategoryIds.Length;j++) {
                            int id=r.ReadInt32();
                            if(!CategoryById.ContainsKey(id)) throw new InvalidDataException("Unknown habitat category.");
                            habitat.CategoryIds[j]=id; CategoryById[id].MarkerCount++;
                        }
                        int rings=ReadCount(r,10000); ringCount+=rings;
                        if(ringCount>20000) throw new InvalidDataException("Too many habitat rings.");
                        habitat.Rings=new PointF[rings][];
                        for(int j=0;j<habitat.Rings.Length;j++) {
                            int points=ReadCount(r,100000);
                            geometryPoints+=points;
                            if(geometryPoints>2000000) throw new InvalidDataException("Habitat geometry exceeds supported limits.");
                            if(points<3) throw new InvalidDataException("Invalid habitat ring.");
                            habitat.Rings[j]=new PointF[points];
                            for(int k=0;k<points;k++) {
                                float x=r.ReadSingle(),y=r.ReadSingle();
                                if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||Math.Abs(x)>10||Math.Abs(y)>10)
                                    throw new InvalidDataException("Invalid habitat coordinate.");
                                habitat.Rings[j][k]=new PointF(x,y);
                            }
                        }
                        Habitats.Add(habitat);
                    }
                }
            }
            IndexSettlements();
        }
        static string ReadExactText(BinaryReader reader,int count) {
            byte[] bytes=reader.ReadBytes(count);
            if(bytes.Length!=count) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
    static int ReadCount(BinaryReader r,int maximum) {
            int n=r.ReadInt32(); if(n<0 || n>maximum) throw new InvalidDataException("Invalid habitat data length."); return n;
        }
        static string ReadText(BinaryReader r) {
            int count=ReadCount(r,100000); byte[] data=r.ReadBytes(count);
            if(data.Length!=count) throw new EndOfStreamException(); return Encoding.UTF8.GetString(data);
        }
        public bool IsHabitatEnabled(ScumMapHabitat h) { return MasterEnabled && h.CategoryIds.Any(IsCategoryEnabled); }
        public string HabitatDisplayName(ScumMapHabitat h) {
            string[] species=h.CategoryIds.Where(IsCategoryEnabled).Select(id=>Localization.GetCategoryName(CategoryById[id].Name)).OrderBy(name=>name).ToArray();
            string label=string.Join(", ",species.Take(3));
            if(species.Length>3) label+=" +"+(species.Length-3);
            return h.Name+" — "+label;
        }
        public ScumMapHabitat HabitatAt(PointF point) {
            // Prefer specific fishing volumes over island-wide biomes, and the smallest overlapping volume.
            ScumMapHabitat best=null; float area=float.MaxValue;
            foreach(ScumMapHabitat h in Habitats) {
                if(!IsHabitatEnabled(h)) continue;
                using(GraphicsPath p=h.CreatePath()) {
                    RectangleF bounds=p.GetBounds(); float size=bounds.Width*bounds.Height;
                    if(size<area && bounds.Contains(point) && h.Contains(point)) { best=h; area=size; }
                }
            }
            return best;
        }
        void DrawHabitats(Graphics g,RectangleF mapBounds) {
            GraphicsState state=g.Save();
            try {
                g.SetClip(mapBounds,CombineMode.Intersect);
                RectangleF clip = g.VisibleClipBounds;
                for(int hi=Habitats.Count-1; hi>=0; hi--) {
                    ScumMapHabitat h = Habitats[hi];
                    if(!IsHabitatEnabled(h)) continue;
                    if(!h.BoundsReady) h.ComputeBounds();
                    float screenMinX = mapBounds.Left + h.MinX * mapBounds.Width;
                    float screenMaxX = mapBounds.Left + h.MaxX * mapBounds.Width;
                    float screenMinY = mapBounds.Top + h.MinY * mapBounds.Height;
                    float screenMaxY = mapBounds.Top + h.MaxY * mapBounds.Height;
                    if(screenMaxX < clip.Left || screenMinX > clip.Right || screenMaxY < clip.Top || screenMinY > clip.Bottom) continue;
                    using(GraphicsPath p=h.CreatePath())
                    using(Matrix transform=new Matrix(mapBounds.Width,0,0,mapBounds.Height,mapBounds.Left,mapBounds.Top)) {
                        p.Transform(transform);
                        using(Brush fill=new SolidBrush(System.Drawing.Color.FromArgb(35,System.Drawing.Color.FromArgb(h.Color)))) g.FillPath(fill,p);
                        using(Pen outline=new Pen(System.Drawing.Color.FromArgb(175,System.Drawing.Color.FromArgb(h.Color)),1.1f)) g.DrawPath(outline,p);
                    }
                }
            } finally { g.Restore(state); }
        }

        public bool IsCategoryEnabled(int catId) {
            return MasterEnabled && !_disabledCategories.Contains(catId);
        }

        public void SetCategoryEnabled(int catId, bool enabled) {
            if (enabled) _disabledCategories.Remove(catId);
            else _disabledCategories.Add(catId);
        }

        public bool IsSectionEnabled(string section) {
            if (!MasterEnabled) return false;
            List<ScumMapCategory> cats;
            if (!CategoriesBySection.TryGetValue(section, out cats) || cats.Count == 0) return false;
            foreach (var c in cats) {
                if (_disabledCategories.Contains(c.Id)) return false;
            }
            return true;
        }

        public bool IsSectionPartiallyEnabled(string section) {
            if (!MasterEnabled) return false;
            List<ScumMapCategory> cats;
            if (!CategoriesBySection.TryGetValue(section, out cats) || cats.Count == 0) return false;
            int enabledCount = 0;
            foreach (var c in cats) {
                if (!_disabledCategories.Contains(c.Id)) enabledCount++;
            }
            return enabledCount > 0 && enabledCount < cats.Count;
        }

        public void SetSectionEnabled(string section, bool enabled) {
            List<ScumMapCategory> cats;
            if (CategoriesBySection.TryGetValue(section, out cats)) {
                foreach (var c in cats) {
                    if (enabled) _disabledCategories.Remove(c.Id);
                    else _disabledCategories.Add(c.Id);
                }
            }
        }

        public void SetAllEnabled(bool enabled) {
            _disabledCategories.Clear();
            if (!enabled) {
                foreach (var c in Categories) _disabledCategories.Add(c.Id);
            }
        }

        public void ResetToDefaults() {
            _disabledCategories.Clear();
            foreach (var c in Categories) {
                if (!c.DefaultEnabled) _disabledCategories.Add(c.Id);
            }
        }

        public string GetDisabledCategoriesString() {
            return "v2;"+string.Join(",", _disabledCategories.OrderBy(id=>id).Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }

        public void ApplyDisabledCategoriesString(string value) {
            _disabledCategories.Clear();
            // Existing preferences predate habitats: leave these new, dense layers opt-in.
            if(value==null || !value.StartsWith("v2;",StringComparison.Ordinal))
                foreach(var c in Categories) if(c.Id<0) _disabledCategories.Add(c.Id);
            if (string.IsNullOrEmpty(value)) return;
            string[] parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts) {
                int id;
                if (int.TryParse(p.Trim(), out id)) {
                    _disabledCategories.Add(id);
                }
            }
        }

        public string GetFilterHashKey() {
            return MasterEnabled + ":" + string.Join(",", _disabledCategories.OrderBy(x => x));
        }

        public static string SectorOf(float x, float y) {
            int col = Math.Max(0, Math.Min(4, (int)Math.Floor(x * 5f)));
            int row = Math.Max(0, Math.Min(4, (int)Math.Floor(y * 5f)));
            return "DCBAZ"[row].ToString() + (4 - col);
        }

        public static string Singularize(string name) {
            if (string.IsNullOrWhiteSpace(name)) return "POI";
            string clean = name.Trim();
            string low = clean.ToLowerInvariant();
            switch (low) {
                case "police stations": case "police station": return "Police Station";
                case "gas stations": case "gas station": return "Gas Station";
                case "food shop": case "food shops": return "Food Shop";
                case "pharmacies": case "pharmacy": return "Pharmacy";
                case "bars": case "bar": return "Bar";
                case "pubs": case "pub": return "Pub";
                case "clubs": case "club": return "Club";
                case "restaurants": case "restaurant": return "Restaurant";
                case "gun shops": case "gun shop": return "Gun Shop";
                case "hunting shops": case "hunting shop": return "Hunting Shop";
                case "vehicle repair shops": case "vehicle repair shop": return "Vehicle Repair Shop";
                case "car garages": case "car garage": return "Car Garage";
                case "workshops": case "workshop": return "Workshop";
                case "schools": case "school": return "School";
                case "churches": case "church": return "Church";
                case "hospitals": case "hospital": return "Hospital";
                case "lighthouses": case "lighthouse": return "Lighthouse";
                case "military warehouses": case "military warehouse": return "Military Warehouse";
                case "military hangars": case "military hangar": return "Military Hangar";
                case "bunkers": case "bunker": return "Bunker";
                case "abandoned bunkers": case "abandoned bunker": return "Abandoned Bunker";
                case "secret bunkers": case "secret bunker": return "Secret Bunker";
                case "ww2 bunkers": case "ww2 bunker": return "WW2 Bunker";
                case "bunker hatch doors": case "bunker hatch": return "Bunker Hatch";
                case "research facilities": case "research facility": return "Research Facility";
                case "general goods": return "General Goods Store";
                case "armory": return "Armory";
                case "mechanic": return "Mechanic";
                case "saloon": return "Saloon";
                case "hairdresser": return "Hairdresser";
                case "killboxes": case "killbox": return "Killbox";
                case "mine entrances": case "mine entrance": return "Mine Entrance";
                case "industrial storage silos": return "Storage Silo";
                case "log cabin": return "Log Cabin";
                case "storage shed": return "Storage Shed";
                case "warehouses": case "warehouse": return "Warehouse";
                case "small docks": case "small dock": case "dock": return "Dock";
                case "quest boards": case "quest board": return "Quest Board";
                case "hunting camps": case "hunting camp": return "Hunting Camp";
                case "hunting towers": case "hunting tower": return "Hunting Tower";
                case "drill press / lathe": return "Workshop Lathe";
                case "red toolboxes": case "toolbox": return "Toolbox";
                case "medical containers": return "Medical Container";
                case "lockers": case "locker": return "Locker";
                case "hazmat suit lockers": return "Hazmat Locker";
                case "radiation equipment lockers": return "Radiation Locker";
                case "depleted uranium crates": return "Uranium Crate";
                case "propane tank": return "Propane Tank";
                case "atm": return "ATM";
                default: return clean;
            }
        }

        public string GetMarkerDisplayName(ScumMapMarker m) {
            if (m == null) return "POI";
            string sec = SectorOf(m.X, m.Y);
            int cid = m.CategoryId;

            // 1. Bunkers: Standard, Abandoned, Secret, WW2, Research Facility
            if (cid == 1) {
                if (!string.IsNullOrEmpty(m.Title) && !m.Title.Equals("Bunkers", StringComparison.OrdinalIgnoreCase) && !m.Title.Equals("Bunker", StringComparison.OrdinalIgnoreCase)) {
                    return m.Title.IndexOf(sec, StringComparison.OrdinalIgnoreCase) >= 0 ? m.Title : sec + " " + m.Title;
                }
                return sec + " Bunker";
            }
            if (cid == 456) {
                return sec + " Abandoned Bunker";
            }
            if (cid == 763) {
                return sec + " Secret Bunker";
            }
            if (cid == 14) {
                return sec + " WW2 Bunker";
            }
            if (cid == 874) {
                return sec + " Research Facility";
            }

            // 2. Settlement entities themselves (Villages, Cities, Outposts)
            if (cid == 27 && !string.IsNullOrEmpty(m.Title)) return m.Title;
            if (cid == 26 && !string.IsNullOrEmpty(m.Title)) return m.Title;
            if (cid == 80 && !string.IsNullOrEmpty(m.Title)) return m.Title;

            // 3. POIs inside or near settlements
            ScumMapCategory cat;
            CategoryById.TryGetValue(cid, out cat);
            string baseName = !string.IsNullOrEmpty(m.Title) ? m.Title : (cat != null ? cat.Name : "POI");
            string cleanType = Singularize(baseName);

            if (_settlements.Count == 0 && Markers.Count > 0) IndexSettlements();

            string nearestSettlement = null;
            double minD = double.MaxValue;
            for (int i = 0; i < _settlements.Count; i++) {
                SettlementAnchor si = _settlements[i];
                double dx = m.X - si.X, dy = m.Y - si.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= si.Radius && d < minD) {
                    minD = d;
                    nearestSettlement = si.Name;
                }
            }

            if (!string.IsNullOrEmpty(nearestSettlement)) {
                if (cleanType.StartsWith(nearestSettlement, StringComparison.OrdinalIgnoreCase)) return cleanType;
                return nearestSettlement + " " + cleanType;
            } else {
                if (cleanType.StartsWith(sec, StringComparison.OrdinalIgnoreCase)) return cleanType;
                return sec + " " + cleanType;
            }
        }

        static SolidBrush CachedBrush(Dictionary<int,SolidBrush> cache,int argb) {
            SolidBrush brush;
            if(!cache.TryGetValue(argb,out brush)) {
                brush=new SolidBrush(Color.FromArgb(argb));
                cache.Add(argb,brush);
            }
            return brush;
        }

        static Pen CachedPen(Dictionary<int,Pen> cache,int argb) {
            Pen pen;
            if(!cache.TryGetValue(argb,out pen)) {
                pen=new Pen(Color.FromArgb(argb),1.2f);
                cache.Add(argb,pen);
            }
            return pen;
        }

        public void Draw(Graphics g, RectangleF mapScreenBounds, float zoom, bool showLabels, float fontSize, bool smartLod) {
            if (!MasterEnabled || mapScreenBounds.Width<=0 || mapScreenBounds.Height<=0) return;
            DrawHabitats(g,mapScreenBounds);
            if(Markers.Count==0) return;

            // Compute visible map normalized rectangle [0..1]
            float leftNorm = Math.Max(0f, Math.Min(1f, -mapScreenBounds.Left / mapScreenBounds.Width));
            float rightNorm = Math.Max(0f, Math.Min(1f, (g.VisibleClipBounds.Width - mapScreenBounds.Left) / mapScreenBounds.Width));
            float topNorm = Math.Max(0f, Math.Min(1f, -mapScreenBounds.Top / mapScreenBounds.Height));
            float bottomNorm = Math.Max(0f, Math.Min(1f, (g.VisibleClipBounds.Height - mapScreenBounds.Top) / mapScreenBounds.Height));

            if (leftNorm > rightNorm || topNorm > bottomNorm) return;

            // Expand bounds slightly to avoid popping at edges (5% margin)
            float marginX = 25f / mapScreenBounds.Width;
            float marginY = 25f / mapScreenBounds.Height;
            float queryMinX = Math.Max(0f, leftNorm - marginX);
            float queryMaxX = Math.Min(1f, rightNorm + marginX);
            float queryMinY = Math.Max(0f, topNorm - marginY);
            float queryMaxY = Math.Min(1f, bottomNorm + marginY);

            int minCellX = Math.Max(0, Math.Min(GridSize - 1, (int)(queryMinX * GridSize)));
            int maxCellX = Math.Max(0, Math.Min(GridSize - 1, (int)(queryMaxX * GridSize)));
            int minCellY = Math.Max(0, Math.Min(GridSize - 1, (int)(queryMinY * GridSize)));
            int maxCellY = Math.Max(0, Math.Min(GridSize - 1, (int)(queryMaxY * GridSize)));

            bool zoomedIn = zoom >= 2.8f;
            float markerRadius = zoomedIn ? 5.5f : (zoom >= 1.6f ? 4f : 3f);

            List<KeyValuePair<string, RectangleF>> labelBoxes = new List<KeyValuePair<string, RectangleF>>();
            Font font = null;
            if (showLabels && (zoomedIn || !smartLod)) {
                font = new Font("Segoe UI", Math.Max(6f, Math.Min(18f, fontSize - 1f)), FontStyle.Bold);
            }
            Dictionary<int,SolidBrush> brushCache=new Dictionary<int,SolidBrush>();
            Dictionary<int,Pen> iconPenCache=new Dictionary<int,Pen>();
            SolidBrush shadowBrush=new SolidBrush(Color.FromArgb(90,0,0,0));
            Pen markerBorderPen=new Pen(Color.FromArgb(200,20,25,30),1.2f);
            SolidBrush labelBackgroundBrush=new SolidBrush(Color.FromArgb(185,12,17,22));
            Pen labelBorderPen=new Pen(Color.FromArgb(130,255,255,255),1f);

            try {
                for (int gx = minCellX; gx <= maxCellX; gx++) {
                    for (int gy = minCellY; gy <= maxCellY; gy++) {
                        List<int> cell = _grid[gx, gy];
                        int count = cell.Count;
                        for (int i = 0; i < count; i++) {
                            int markerIdx = cell[i];
                            ScumMapMarker m = Markers[markerIdx];

                            if (_disabledCategories.Contains(m.CategoryId)) continue;
                            if (m.X < queryMinX || m.X > queryMaxX || m.Y < queryMinY || m.Y > queryMaxY) continue;

                            float screenX = mapScreenBounds.Left + m.X * mapScreenBounds.Width;
                            float screenY = mapScreenBounds.Top + m.Y * mapScreenBounds.Height;

                            ScumMapCategory cat;
                            CategoryById.TryGetValue(m.CategoryId, out cat);
                            int bgArgb = cat != null ? cat.ColorBackground : unchecked((int)0xFF4488CC);
                            int fgArgb = cat != null ? cat.ColorIcon : unchecked((int)0xFFFFFFFF);

                            // Draw shadow & pin circle using resources shared by this terrain build.
                            g.FillEllipse(shadowBrush, screenX - markerRadius, screenY - markerRadius + 1.2f, markerRadius * 2, markerRadius * 2);
                            g.FillEllipse(CachedBrush(brushCache,bgArgb), screenX - markerRadius, screenY - markerRadius, markerRadius * 2, markerRadius * 2);
                            g.DrawEllipse(markerBorderPen, screenX - markerRadius, screenY - markerRadius, markerRadius * 2, markerRadius * 2);
                            string activity=ActivitySection(m.CategoryId);
                            if(activity!=null) {
                                float r=markerRadius;
                                Pen symbol=CachedPen(iconPenCache,fgArgb);
                                if(activity=="Fishing") {
                                    g.DrawEllipse(symbol,screenX-r*.55f,screenY-r*.3f,r*.8f,r*.6f);
                                    g.DrawLine(symbol,screenX+r*.2f,screenY,screenX+r*.65f,screenY-r*.4f);
                                    g.DrawLine(symbol,screenX+r*.65f,screenY-r*.4f,screenX+r*.65f,screenY+r*.4f);
                                    g.DrawLine(symbol,screenX+r*.65f,screenY+r*.4f,screenX+r*.2f,screenY);
                                } else {
                                    g.DrawEllipse(symbol,screenX-r*.45f,screenY-r*.45f,r*.9f,r*.9f);
                                    g.DrawLine(symbol,screenX-r*.8f,screenY,screenX+r*.8f,screenY);
                                    g.DrawLine(symbol,screenX,screenY-r*.8f,screenX,screenY+r*.8f);
                                }
                            } else if (markerRadius >= 4f) {
                                float dotR = markerRadius * 0.45f;
                                g.FillEllipse(CachedBrush(brushCache,fgArgb), screenX - dotR, screenY - dotR, dotR * 2, dotR * 2);
                            }

                            // Optional label for visible markers
                            if (font != null && (!smartLod || zoomedIn || (cat != null && (cat.Section == "Outposts" || cat.Section == "Bunkers")))) {
                                string name = GetMarkerDisplayName(m);
                                if (!string.IsNullOrEmpty(name)) {
                                    string displayName = Localization.GetZoneName(name);
                                    SizeF sz = g.MeasureString(displayName, font);
                                    RectangleF box = new RectangleF(screenX - sz.Width / 2f - 3f, screenY + markerRadius + 2f, sz.Width + 6f, sz.Height + 2f);
                                    labelBoxes.Add(new KeyValuePair<string, RectangleF>(displayName, box));
                                }
                            }
                        }
                    }
                }

                // Draw labels in batch
                if (labelBoxes.Count > 0 && font != null) {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {
                        foreach (var item in labelBoxes) {
                            g.FillRectangle(labelBackgroundBrush, item.Value);
                            g.DrawRectangle(labelBorderPen, item.Value.X, item.Value.Y, item.Value.Width, item.Value.Height);
                            g.DrawString(item.Key, font, Brushes.Black, item.Value.X + item.Value.Width / 2f + 1f, item.Value.Y + item.Value.Height / 2f + 1f, sf);
                            g.DrawString(item.Key, font, Brushes.White, item.Value.X + item.Value.Width / 2f, item.Value.Y + item.Value.Height / 2f, sf);
                        }
                    }
                }
            } finally {
                if (font != null) font.Dispose();
                shadowBrush.Dispose(); markerBorderPen.Dispose();
                labelBackgroundBrush.Dispose(); labelBorderPen.Dispose();
                foreach(SolidBrush brush in brushCache.Values)brush.Dispose();
                foreach(Pen pen in iconPenCache.Values)pen.Dispose();
            }
        }

        public ScumMapMarker HitTest(PointF screenPt, RectangleF mapScreenBounds, float maxDistancePixels) {
            if (!MasterEnabled || Markers.Count == 0) return null;

            float mapX = (screenPt.X - mapScreenBounds.Left) / mapScreenBounds.Width;
            float mapY = (screenPt.Y - mapScreenBounds.Top) / mapScreenBounds.Height;
            if (mapX < 0f || mapX > 1f || mapY < 0f || mapY > 1f) return null;

            float maxDistNorm = maxDistancePixels / Math.Min(mapScreenBounds.Width, mapScreenBounds.Height);
            float maxDistNormSq = maxDistNorm * maxDistNorm;

            int cellX = Math.Max(0, Math.Min(GridSize - 1, (int)(mapX * GridSize)));
            int cellY = Math.Max(0, Math.Min(GridSize - 1, (int)(mapY * GridSize)));

            ScumMapMarker closest = null;
            float closestDistSq = maxDistNormSq;

            int rMinX = Math.Max(0, cellX - 1), rMaxX = Math.Min(GridSize - 1, cellX + 1);
            int rMinY = Math.Max(0, cellY - 1), rMaxY = Math.Min(GridSize - 1, cellY + 1);

            for (int gx = rMinX; gx <= rMaxX; gx++) {
                for (int gy = rMinY; gy <= rMaxY; gy++) {
                    foreach (int idx in _grid[gx, gy]) {
                        ScumMapMarker m = Markers[idx];
                        if (_disabledCategories.Contains(m.CategoryId)) continue;
                        float dx = m.X - mapX;
                        float dy = m.Y - mapY;
                        float distSq = dx * dx + dy * dy;
                        if (distSq < closestDistSq) {
                            closestDistSq = distSq;
                            closest = m;
                        }
                    }
                }
            }
            return closest;
        }

        public static void SelfTest() {
            ScumMapStore store = new ScumMapStore();
            if (store.Markers.Count > 0) throw new Exception("Expected empty store initially.");
            if(ActivitySection(858)!="Fishing" || ActivitySection(869)!="Hunting" || ActivitySection(24)!="Hunting" || ActivitySection(8)!=null)
                throw new Exception("Fishing/hunting classification failed.");
            for(int id=1;id<=20;id++) store.SetCategoryEnabled(id,false);
            string originalHash=store.GetFilterHashKey();
            store.SetCategoryEnabled(20,true); store.SetCategoryEnabled(21,false);
            if(originalHash==store.GetFilterHashKey()) throw new Exception("Filter change did not invalidate map cache.");
            ScumMapHabitat hole=new ScumMapHabitat { Rings=new[]{
                new[]{new PointF(0,0),new PointF(1,0),new PointF(1,1),new PointF(0,1)},
                new[]{new PointF(.25f,.25f),new PointF(.25f,.75f),new PointF(.75f,.75f),new PointF(.75f,.25f)} } };
            if(!hole.Contains(new PointF(.1f,.1f)) || hole.Contains(new PointF(.5f,.5f)) || hole.Contains(new PointF(2,2)))
                throw new Exception("Habitat winding/hole test failed.");
            // Test grid bounds
            int gx = Math.Max(0, Math.Min(GridSize - 1, (int)(0.5f * GridSize)));
            if (gx < 0 || gx >= GridSize) throw new Exception("Grid clamping failed.");
        }
    }
}
