using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ScumMiniMap {
    public class RoadRoute {
        public bool Success;
        public PointF PlayerPoint;
        public PointF RoadEntryPoint;
        public PointF RoadExitPoint;
        public PointF TargetPoint;
        public PointF[] Polyline; // Full route including entry/exit or just road path
        public int[] JunctionIndices=new int[0]; // Actual branch nodes along Polyline, not geometry bends.
        public double TotalDistanceMeters;
        public double RoadDistanceMeters;
        public double EntryDistanceMeters;
        public double ExitDistanceMeters;
        public bool HasWaterTransit;
        public PointF WaterDeparturePoint;
        public PointF WaterArrivalPoint;
        public double WaterDistanceMeters;
    }

    public class RoadRouter {
        private static readonly RoadRouter instance = new RoadRouter();
        public static RoadRouter Instance {
            get {
                return instance;
            }
        }

        private volatile bool isLoaded;
        private readonly object loadLock = new object();
        public bool IsLoaded { get { return isLoaded; } private set { isLoaded=value; } }

        public struct RoadNode {
            public float U;
            public float V;
        }

        public struct RoadEdge {
            public int U;
            public int V;
            public float LengthMeters;
            public PointF[] Points;
            public bool IsBridge;
        }

        public struct HalfEdge {
            public int TargetNode;
            public float Weight;
            public int EdgeIndex;
            public bool Forward;
        }

        private List<RoadNode> nodesList;
        private List<RoadEdge> edgesList;
        private List<HalfEdge>[] adj;
        private int gridRes;
        private List<ushort>[] spatialGridList;

        // Fast lookup arrays
        private RoadNode[] nodes;
        private RoadEdge[] edges;
        private ushort[][] spatialGrid;

        // Connected component cache
        private int[] nodeComponent;
        private int componentCount;
        private List<int>[] componentNodes;
        private int mainComponentId;

        // Reusable zero-allocation A* search state
        private readonly object searchLock = new object();
        private double[] gScores;
        private HalfEdge[] cameFrom;
        private uint[] visitedEpoch;
        private uint currentEpoch;
        private MinHeap pq;

        private const double MapScaleX = 1521618.0 / 100.0; // 15216.18 meters across map
        private const double MapScaleY = 1523618.0 / 100.0; // 15236.18 meters across map

        public static double DistanceMeters(PointF a, PointF b) {
            double dx = (b.X - a.X) * MapScaleX;
            double dy = (b.Y - a.Y) * MapScaleY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double DistanceMeters(float u1, float v1, float u2, float v2) {
            double dx = (u2 - u1) * MapScaleX;
            double dy = (v2 - v1) * MapScaleY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public void InitializeFromResource() {
            lock(loadLock) { InitializeCore(); }
        }
        private void InitializeCore() {
            if (IsLoaded) return;
            try {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream raw = asm.GetManifestResourceStream("roads.bin")) {
                    if (raw != null) {
                        using (GZipStream gz = new GZipStream(raw, CompressionMode.Decompress))
                        using (BinaryReader br = new BinaryReader(gz)) {
                            LoadBinary(br);
                            return;
                        }
                    }
                }
                // Fallback: check file on disk if running unbundled
                string[] candidates = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScumMiniMap", "resources", "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScumMiniMap", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "resources", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "ScumMiniMap", "resources", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "ScumMiniMap", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "roads.bin")
                };
                foreach (string path in candidates) {
                    if (File.Exists(path)) {
                        using (FileStream fs = File.OpenRead(path))
                        using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                        using (BinaryReader br = new BinaryReader(gz)) {
                            LoadBinary(br);
                            return;
                        }
                    }
                }
            } catch {
                IsLoaded = false;
            }
        }

        static float ReadCoordinate(BinaryReader reader) {
            float value=reader.ReadSingle();
            if(float.IsNaN(value)||float.IsInfinity(value)||value<-.1f||value>1.1f) throw new InvalidDataException("Invalid road coordinate.");
            return value;
        }
        public void LoadBinary(BinaryReader br) {
            byte[] magic = br.ReadBytes(4);
            IsLoaded=false;
            if (magic.Length!=4) throw new InvalidDataException("Truncated road header.");
            if (magic[0] != 'R' || magic[1] != 'O' || magic[2] != 'A' || magic[3] != 'D') throw new InvalidDataException("Invalid road header.");
            uint version = br.ReadUInt32();
            if (version != 1) throw new InvalidDataException("Unsupported road version.");

            int nodeCount = br.ReadInt32();
            int edgeCount = br.ReadInt32();
            gridRes = br.ReadInt32();
            if(nodeCount<1 || nodeCount>100000 || edgeCount<1 || edgeCount>60000 || gridRes<1 || gridRes>512)
                throw new InvalidDataException("Road counts exceed supported limits.");
            long totalPoints=0, totalReferences=0;

            nodesList = new List<RoadNode>(nodeCount + 64);
            for (int i = 0; i < nodeCount; i++) {
                nodesList.Add(new RoadNode { U = ReadCoordinate(br), V = ReadCoordinate(br) });
            }

            edgesList = new List<RoadEdge>(edgeCount + 2048);
            adj = new List<HalfEdge>[nodeCount + 2048];
            for (int i = 0; i < adj.Length; i++) adj[i] = new List<HalfEdge>(4);

            for (int i = 0; i < edgeCount; i++) {
                int u = br.ReadInt32();
                int v = br.ReadInt32();
                float lengthM = br.ReadSingle();
                if(u<0 || v<0 || u>=nodeCount || v>=nodeCount || float.IsNaN(lengthM) || float.IsInfinity(lengthM) || lengthM<=0 || lengthM>100000)
                    throw new InvalidDataException("Invalid road edge.");
                ushort ptCount = br.ReadUInt16();
                totalPoints+=ptCount;
                if(ptCount<2 || totalPoints>4000000) throw new InvalidDataException("Road geometry exceeds supported limits.");
                PointF[] pts = new PointF[ptCount];
                for (int p = 0; p < ptCount; p++) {
                    pts[p] = new PointF(ReadCoordinate(br), ReadCoordinate(br));
                }
                edgesList.Add(new RoadEdge { U = u, V = v, LengthMeters = lengthM, Points = pts, IsBridge = false });
                adj[u].Add(new HalfEdge { TargetNode = v, Weight = lengthM, EdgeIndex = i, Forward = true });
                adj[v].Add(new HalfEdge { TargetNode = u, Weight = lengthM, EdgeIndex = i, Forward = false });
            }

            int totalCells = gridRes * gridRes;
            spatialGridList = new List<ushort>[totalCells];
            for (int i = 0; i < totalCells; i++) {
                ushort count = br.ReadUInt16();
                totalReferences+=count;
                if(totalReferences>4000000) throw new InvalidDataException("Road index exceeds supported limits.");
                List<ushort> list = new List<ushort>(count + 8);
                for (int c = 0; c < count; c++) { ushort edge=br.ReadUInt16(); if(edge>=edgeCount) throw new InvalidDataException("Invalid road index."); list.Add(edge); }
                spatialGridList[i] = list;
            }

            // Synthesize major bridges across water
            AddBridges();

            // Stitch digitized micro-gaps at intersections (<= 35m)
            HealMicroGaps(35.0);

            // Commit final arrays for high-performance direct indexing
            nodes = nodesList.ToArray();
            edges = edgesList.ToArray();
            spatialGrid = new ushort[totalCells][];
            for (int i = 0; i < totalCells; i++) {
                spatialGrid[i] = spatialGridList[i].ToArray();
            }

            // Analyze and index connected components
            ComputeConnectedComponents();

            // Allocate reusable zero-allocation A* search buffers
            int totalNodes = nodes.Length;
            gScores = new double[totalNodes];
            cameFrom = new HalfEdge[totalNodes];
            visitedEpoch = new uint[totalNodes];
            currentEpoch = 1;
            pq = new MinHeap(1024);

            IsLoaded = true;
        }

        private void AddBridgeEdge(int u, int v, PointF[] curvePts, string name) {
            if (u < 0 || u >= nodesList.Count || v < 0 || v >= nodesList.Count) return;
            PointF pU = new PointF(nodesList[u].U, nodesList[u].V);
            PointF pV = new PointF(nodesList[v].U, nodesList[v].V);

            List<PointF> pts = new List<PointF>();
            pts.Add(pU);
            if (curvePts != null && curvePts.Length > 0) {
                for (int i = 0; i < curvePts.Length; i++) pts.Add(curvePts[i]);
            }
            pts.Add(pV);

            double totalLen = 0;
            for (int i = 0; i < pts.Count - 1; i++) {
                totalLen += DistanceMeters(pts[i], pts[i + 1]);
            }

            int edgeIdx = edgesList.Count;
            if (edgeIdx >= ushort.MaxValue) return; // Grid uses ushort indices

            RoadEdge bridgeEdge = new RoadEdge {
                U = u,
                V = v,
                LengthMeters = (float)totalLen,
                Points = pts.ToArray(),
                IsBridge = true
            };
            edgesList.Add(bridgeEdge);

            adj[u].Add(new HalfEdge { TargetNode = v, Weight = (float)totalLen, EdgeIndex = edgeIdx, Forward = true });
            adj[v].Add(new HalfEdge { TargetNode = u, Weight = (float)totalLen, EdgeIndex = edgeIdx, Forward = false });

            // Insert into spatial grid
            IndexEdgeInGrid(edgeIdx, bridgeEdge.Points);
        }

        private void AddBridges() {
            // 1. East Highway Suspension Bridge (A0 <-> Z0)
            // Connects mainland eastern highway (Node 9026) to south island (Node 8991)
            PointF[] eastBridgeCurve = new PointF[] {
                new PointF(0.91458f, 0.73791f),
                new PointF(0.91506f, 0.74176f),
                new PointF(0.91554f, 0.74561f),
                new PointF(0.91603f, 0.74945f),
                new PointF(0.91644f, 0.75330f),
                new PointF(0.91720f, 0.75715f)
            };
            AddBridgeEdge(9026, 8991, eastBridgeCurve, "East Suspension Bridge");

            // Approaches
            AddBridgeEdge(9030, 9026, null, "East North Approach");
            AddBridgeEdge(8991, 8992, null, "East South Approach");

            // 2. West Highway Suspension Bridge (A3 <-> Z3 Rogoznica)
            // Connects mainland highway (Node 10927) to south island Rogoznica (Node 9916)
            PointF[] westBridgeCurve = new PointF[] {
                new PointF(0.28624f, 0.75685f),
                new PointF(0.29611f, 0.76082f),
                new PointF(0.29687f, 0.76874f),
                new PointF(0.29687f, 0.77666f),
                new PointF(0.29660f, 0.78459f),
                new PointF(0.29611f, 0.79251f),
                new PointF(0.29577f, 0.80043f),
                new PointF(0.29521f, 0.80836f),
                new PointF(0.29459f, 0.81628f),
                new PointF(0.29411f, 0.82420f),
                new PointF(0.29397f, 0.82816f)
            };
            AddBridgeEdge(10927, 9916, westBridgeCurve, "West Rogoznica Bridge");
            AddBridgeEdge(8520, 10927, null, "West North Approach");
            AddBridgeEdge(9916, 9922, null, "West South Approach");

            // 3. Central Diagonal Railway Bridge (A2 <-> Z2)
            // Connects mainland railway abutment (Node 8182) to south island railway abutment (Node 10858)
            // High-precision straight diagonal deck following the physical railway tracks on the master map
            PointF[] diagBridgeCurve = new PointF[] {
                new PointF(0.47155f, 0.75171f),
                new PointF(0.47686f, 0.75694f),
                new PointF(0.48216f, 0.76218f),
                new PointF(0.48747f, 0.76741f)
            };
            AddBridgeEdge(8182, 10858, diagBridgeCurve, "Central Diagonal Railway Bridge");
            AddBridgeEdge(8183, 8182, null, "Central Diagonal North Approach");
            AddBridgeEdge(10858, 8151, null, "Central Diagonal South Approach");
            AddBridgeEdge(8151, 8150, null, "Central Diagonal Rail Continuation");

            // 4. Central-East Highway Bridge (A1 <-> Z1)
            // Connects mainland highway (Node 9409) across central-east channel to south island (Node 9407)
            PointF[] centralEastHighwayCurve = new PointF[] {
                new PointF(0.64554f, 0.72757f),
                new PointF(0.64409f, 0.73448f),
                new PointF(0.64250f, 0.74139f),
                new PointF(0.64105f, 0.74829f),
                new PointF(0.63953f, 0.75520f),
                new PointF(0.63794f, 0.76210f),
                new PointF(0.63614f, 0.76901f),
                new PointF(0.63366f, 0.77591f),
                new PointF(0.63110f, 0.78282f),
                new PointF(0.62848f, 0.78972f)
            };
            AddBridgeEdge(9409, 9407, centralEastHighwayCurve, "Central-East Highway Bridge");
            AddBridgeEdge(9434, 9409, null, "Central-East North Approach");
            AddBridgeEdge(9407, 9408, null, "Central-East South Approach");

            // 5. C2 Dam Crest Crossing
            // Connects dam crest road (Node 3007) to west facilities (Node 2986)
            PointF[] damCurve = new PointF[] {
                new PointF(0.4180f, 0.2325f)
            };
            AddBridgeEdge(3007, 2986, damCurve, "C2 Dam Crossing");
        }

        private void IndexEdgeInGrid(int edgeIdx, PointF[] pts) {
            if(edgeIdx<0 || edgeIdx>ushort.MaxValue) throw new InvalidDataException("Too many synthesized road edges.");
            ushort eVal = (ushort)edgeIdx;
            HashSet<int> touchedCells = new HashSet<int>();
            for (int i = 0; i < pts.Length - 1; i++) {
                PointF a = pts[i];
                PointF b = pts[i + 1];
                int gx1 = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(Math.Min(a.X, b.X) * gridRes)));
                int gx2 = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(Math.Max(a.X, b.X) * gridRes)));
                int gy1 = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(Math.Min(a.Y, b.Y) * gridRes)));
                int gy2 = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(Math.Max(a.Y, b.Y) * gridRes)));

                for (int cy = gy1; cy <= gy2; cy++) {
                    for (int cx = gx1; cx <= gx2; cx++) {
                        int cell = cy * gridRes + cx;
                        if (!touchedCells.Contains(cell)) {
                            touchedCells.Add(cell);
                            spatialGridList[cell].Add(eVal);
                        }
                    }
                }
            }
        }

        private void HealMicroGaps(double maxDistanceMeters) {
            int nodeCount = nodesList.Count;
            List<int> termini = new List<int>();
            for (int i = 0; i < nodeCount; i++) {
                if (adj[i].Count > 0 && adj[i].Count <= 2) termini.Add(i);
            }

            int healRes = 128;
            List<int>[] healGrid = new List<int>[healRes * healRes];
            for (int i = 0; i < healGrid.Length; i++) healGrid[i] = new List<int>();

            for (int i = 0; i < termini.Count; i++) {
                int n = termini[i];
                int gx = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(nodesList[n].U * healRes)));
                int gy = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(nodesList[n].V * healRes)));
                healGrid[gy * healRes + gx].Add(n);
            }

            double cellMeters = 15220.0 / healRes;
            int radiusCells = Math.Max(1, (int)Math.Ceiling(maxDistanceMeters / cellMeters));

            // Pass 1: Standard micro-gap stitch <= 35m
            for (int i = 0; i < termini.Count; i++) {
                int u = termini[i];
                float uX = nodesList[u].U, uY = nodesList[u].V;
                int gx = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(uX * healRes)));
                int gy = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(uY * healRes)));

                int bestV = -1;
                double bestDist = maxDistanceMeters;

                for (int dy = -radiusCells; dy <= radiusCells; dy++) {
                    int cy = gy + dy;
                    if (cy < 0 || cy >= healRes) continue;
                    for (int dx = -radiusCells; dx <= radiusCells; dx++) {
                        int cx = gx + dx;
                        if (cx < 0 || cx >= healRes) continue;

                        List<int> cellNodes = healGrid[cy * healRes + cx];
                        for (int k = 0; k < cellNodes.Count; k++) {
                            int v = cellNodes[k];
                            if (v <= u) continue;

                            bool alreadyLinked = false;
                            for (int a = 0; a < adj[u].Count; a++) {
                                if (adj[u][a].TargetNode == v) { alreadyLinked = true; break; }
                            }
                            if (alreadyLinked) continue;

                            double d = DistanceMeters(uX, uY, nodesList[v].U, nodesList[v].V);
                            if (d <= bestDist) {
                                bestDist = d;
                                bestV = v;
                            }
                        }
                    }
                }

                if (bestV >= 0 && edgesList.Count < ushort.MaxValue) {
                    int edgeIdx = edgesList.Count;
                    PointF[] stitchPts = new PointF[] {
                        new PointF(nodesList[u].U, nodesList[u].V),
                        new PointF(nodesList[bestV].U, nodesList[bestV].V)
                    };
                    RoadEdge stitchEdge = new RoadEdge {
                        U = u,
                        V = bestV,
                        LengthMeters = (float)bestDist,
                        Points = stitchPts,
                        IsBridge = false
                    };
                    edgesList.Add(stitchEdge);
                    adj[u].Add(new HalfEdge { TargetNode = bestV, Weight = (float)bestDist, EdgeIndex = edgeIdx, Forward = true });
                    adj[bestV].Add(new HalfEdge { TargetNode = u, Weight = (float)bestDist, EdgeIndex = edgeIdx, Forward = false });
                    IndexEdgeInGrid(edgeIdx, stitchPts);
                }
            }

            // Pass 2: Cross-component healing <= 65m
            int[] tempComp = new int[nodeCount];
            for (int i = 0; i < nodeCount; i++) tempComp[i] = -1;
            int cCount = 0;
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < nodeCount; i++) {
                if (tempComp[i] >= 0) continue;
                int c = cCount++;
                tempComp[i] = c;
                q.Enqueue(i);
                while (q.Count > 0) {
                    int curr = q.Dequeue();
                    List<HalfEdge> outgoing = adj[curr];
                    for (int k = 0; k < outgoing.Count; k++) {
                        int nbr = outgoing[k].TargetNode;
                        if (tempComp[nbr] < 0) {
                            tempComp[nbr] = c;
                            q.Enqueue(nbr);
                        }
                    }
                }
            }

            double crossDistLimit = 65.0;
            int crossRadiusCells = Math.Max(1, (int)Math.Ceiling(crossDistLimit / cellMeters));

            for (int i = 0; i < termini.Count; i++) {
                int u = termini[i];
                float uX = nodesList[u].U, uY = nodesList[u].V;
                int gx = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(uX * healRes)));
                int gy = Math.Max(0, Math.Min(healRes - 1, (int)Math.Floor(uY * healRes)));

                int bestV = -1;
                double bestDist = crossDistLimit;

                for (int dy = -crossRadiusCells; dy <= crossRadiusCells; dy++) {
                    int cy = gy + dy;
                    if (cy < 0 || cy >= healRes) continue;
                    for (int dx = -crossRadiusCells; dx <= crossRadiusCells; dx++) {
                        int cx = gx + dx;
                        if (cx < 0 || cx >= healRes) continue;

                        List<int> cellNodes = healGrid[cy * healRes + cx];
                        for (int k = 0; k < cellNodes.Count; k++) {
                            int v = cellNodes[k];
                            if (tempComp[u] == tempComp[v]) continue; // ONLY cross-component!

                            bool alreadyLinked = false;
                            for (int a = 0; a < adj[u].Count; a++) {
                                if (adj[u][a].TargetNode == v) { alreadyLinked = true; break; }
                            }
                            if (alreadyLinked) continue;

                            double d = DistanceMeters(uX, uY, nodesList[v].U, nodesList[v].V);
                            if (d <= bestDist) {
                                bestDist = d;
                                bestV = v;
                            }
                        }
                    }
                }

                if (bestV >= 0 && edgesList.Count < ushort.MaxValue) {
                    int edgeIdx = edgesList.Count;
                    PointF[] stitchPts = new PointF[] {
                        new PointF(nodesList[u].U, nodesList[u].V),
                        new PointF(nodesList[bestV].U, nodesList[bestV].V)
                    };
                    RoadEdge stitchEdge = new RoadEdge {
                        U = u,
                        V = bestV,
                        LengthMeters = (float)bestDist,
                        Points = stitchPts,
                        IsBridge = false
                    };
                    edgesList.Add(stitchEdge);
                    adj[u].Add(new HalfEdge { TargetNode = bestV, Weight = (float)bestDist, EdgeIndex = edgeIdx, Forward = true });
                    adj[bestV].Add(new HalfEdge { TargetNode = u, Weight = (float)bestDist, EdgeIndex = edgeIdx, Forward = false });
                    IndexEdgeInGrid(edgeIdx, stitchPts);

                    int oldComp = tempComp[bestV];
                    int newComp = tempComp[u];
                    for (int n = 0; n < nodeCount; n++) {
                        if (tempComp[n] == oldComp) tempComp[n] = newComp;
                    }
                }
            }
        }

        private void ComputeConnectedComponents() {
            int count = nodes.Length;
            nodeComponent = new int[count];
            for (int i = 0; i < count; i++) nodeComponent[i] = -1;

            List<List<int>> comps = new List<List<int>>();
            Queue<int> q = new Queue<int>();

            for (int i = 0; i < count; i++) {
                if (nodeComponent[i] >= 0) continue;
                int cIdx = comps.Count;
                List<int> comp = new List<int>();
                q.Enqueue(i);
                nodeComponent[i] = cIdx;

                while (q.Count > 0) {
                    int curr = q.Dequeue();
                    comp.Add(curr);
                    List<HalfEdge> outgoing = adj[curr];
                    for (int k = 0; k < outgoing.Count; k++) {
                        int nbr = outgoing[k].TargetNode;
                        if (nodeComponent[nbr] < 0) {
                            nodeComponent[nbr] = cIdx;
                            q.Enqueue(nbr);
                        }
                    }
                }
                comps.Add(comp);
            }

            componentCount = comps.Count;
            componentNodes = comps.ToArray();

            int maxCount = -1;
            mainComponentId = 0;
            for (int c = 0; c < comps.Count; c++) {
                if (comps[c].Count > maxCount) {
                    maxCount = comps[c].Count;
                    mainComponentId = c;
                }
            }
        }

        public struct SnapResult {
            public int EdgeIndex;
            public PointF Projected;
            public double DistanceMeters;
            public int SegmentIndex;
            public double SegmentT;
        }

        public SnapResult SnapToComponent(PointF pt, int targetComponent, double maxSearchMeters = 3500.0) {
            if (!IsLoaded || edges == null || edges.Length == 0)
                return new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };

            int gu = (int)Math.Floor(pt.X * gridRes);
            int gv = (int)Math.Floor(pt.Y * gridRes);
            gu = Math.Max(0, Math.Min(gridRes - 1, gu));
            gv = Math.Max(0, Math.Min(gridRes - 1, gv));

            HashSet<int> inspected = new HashSet<int>();
            int maxRadius = Math.Max(1, (int)Math.Ceiling(maxSearchMeters / (15220.0 / gridRes)));

            double bestDist = double.MaxValue;
            SnapResult bestSnap = new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };

            for (int r = 0; r <= maxRadius; r++) {
                for (int dx = -r; dx <= r; dx++) {
                    for (int dy = -r; dy <= r; dy++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int cx = gu + dx;
                        int cy = gv + dy;
                        if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;

                        ushort[] cellEdges = spatialGrid[cy * gridRes + cx];
                        if (cellEdges == null) continue;

                        for (int e = 0; e < cellEdges.Length; e++) {
                            int eid = cellEdges[e];
                            if (inspected.Contains(eid)) continue;
                            inspected.Add(eid);

                            if (nodeComponent[edges[eid].U] != targetComponent) continue;

                            PointF[] pts = edges[eid].Points;
                            for (int i = 0; i < pts.Length - 1; i++) {
                                PointF a = pts[i];
                                PointF b = pts[i + 1];
                                double ldx = b.X - a.X;
                                double ldy = b.Y - a.Y;
                                double l2 = ldx * ldx + ldy * ldy;
                                double t = 0;
                                if (l2 > 1e-12) {
                                    t = Math.Max(0.0, Math.Min(1.0, ((pt.X - a.X) * ldx + (pt.Y - a.Y) * ldy) / l2));
                                }
                                PointF proj = new PointF((float)(a.X + t * ldx), (float)(a.Y + t * ldy));
                                double d = DistanceMeters(pt, proj);
                                if (d < bestDist) {
                                    bestDist = d;
                                    bestSnap = new SnapResult {
                                        EdgeIndex = eid,
                                        Projected = proj,
                                        DistanceMeters = d,
                                        SegmentIndex = i,
                                        SegmentT = t
                                    };
                                }
                            }
                        }
                    }
                }
                double ringMinMeters = Math.Max(0, (r - 1) * (15220.0 / gridRes));
                if (bestDist < ringMinMeters) break;
            }

            return bestSnap;
        }

        public List<SnapResult> SnapToRoadCandidates(PointF pt, double maxSearchMeters = 3000.0, int maxCandidates = 8) {
            List<SnapResult> results = new List<SnapResult>(maxCandidates);
            if (!IsLoaded || edges == null || edges.Length == 0) return results;

            int gu = (int)Math.Floor(pt.X * gridRes);
            int gv = (int)Math.Floor(pt.Y * gridRes);
            gu = Math.Max(0, Math.Min(gridRes - 1, gu));
            gv = Math.Max(0, Math.Min(gridRes - 1, gv));

            HashSet<int> inspected = new HashSet<int>();
            int maxRadius = Math.Max(1, (int)Math.Ceiling(maxSearchMeters / (15220.0 / gridRes)));

            for (int r = 0; r <= maxRadius; r++) {
                bool foundInRing = false;
                for (int dx = -r; dx <= r; dx++) {
                    for (int dy = -r; dy <= r; dy++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int cx = gu + dx;
                        int cy = gv + dy;
                        if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;

                        ushort[] cellEdges = spatialGrid[cy * gridRes + cx];
                        if (cellEdges == null) continue;

                        for (int e = 0; e < cellEdges.Length; e++) {
                            int eid = cellEdges[e];
                            if (inspected.Contains(eid)) continue;
                            inspected.Add(eid);

                            PointF[] pts = edges[eid].Points;
                            double bestEdgeDist = double.MaxValue;
                            SnapResult bestEdgeSnap = default(SnapResult);

                            for (int i = 0; i < pts.Length - 1; i++) {
                                PointF a = pts[i];
                                PointF b = pts[i + 1];
                                double ldx = b.X - a.X;
                                double ldy = b.Y - a.Y;
                                double l2 = ldx * ldx + ldy * ldy;
                                double t = 0;
                                if (l2 > 1e-12) {
                                    t = Math.Max(0.0, Math.Min(1.0, ((pt.X - a.X) * ldx + (pt.Y - a.Y) * ldy) / l2));
                                }
                                PointF proj = new PointF((float)(a.X + t * ldx), (float)(a.Y + t * ldy));
                                double d = DistanceMeters(pt, proj);
                                if (d < bestEdgeDist) {
                                    bestEdgeDist = d;
                                    bestEdgeSnap = new SnapResult {
                                        EdgeIndex = eid,
                                        Projected = proj,
                                        DistanceMeters = d,
                                        SegmentIndex = i,
                                        SegmentT = t
                                    };
                                }
                            }

                            if (bestEdgeDist <= maxSearchMeters) {
                                foundInRing = true;
                                int ins = 0;
                                while (ins < results.Count && results[ins].DistanceMeters <= bestEdgeDist) ins++;
                                if (ins < maxCandidates) {
                                    results.Insert(ins, bestEdgeSnap);
                                    if (results.Count > maxCandidates) results.RemoveAt(results.Count - 1);
                                }
                            }
                        }
                    }
                }
                if (foundInRing && results.Count >= maxCandidates && r >= 2) break;
            }

            // Guarantee that if a road on mainComponentId is within search radius, it is represented
            bool hasMain = false;
            for (int i = 0; i < results.Count; i++) {
                if (nodeComponent[edges[results[i].EdgeIndex].U] == mainComponentId) {
                    hasMain = true;
                    break;
                }
            }
            if (!hasMain) {
                SnapResult mainSnap = SnapToComponent(pt, mainComponentId, maxSearchMeters);
                if (mainSnap.EdgeIndex >= 0) {
                    results.Add(mainSnap);
                }
            }

            return results;
        }

        public SnapResult SnapToRoad(PointF pt, double maxSearchMeters = 2000.0) {
            List<SnapResult> cands = SnapToRoadCandidates(pt, maxSearchMeters, 1);
            if (cands.Count > 0) return cands[0];
            return new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };
        }

        private class MinHeap {
            private struct Entry {
                public double Priority;
                public int Node;
            }
            private Entry[] heap;
            public int Count { get; private set; }

            public MinHeap(int initialCapacity = 512) {
                heap = new Entry[initialCapacity];
            }

            public void Clear() {
                Count = 0;
            }

            public void Push(double priority, int node) {
                if (Count == heap.Length) {
                    Array.Resize(ref heap, heap.Length * 2);
                }
                int i = Count++;
                heap[i] = new Entry { Priority = priority, Node = node };
                while (i > 0) {
                    int p = (i - 1) >> 1;
                    if (heap[p].Priority <= heap[i].Priority) break;
                    Entry tmp = heap[p]; heap[p] = heap[i]; heap[i] = tmp;
                    i = p;
                }
            }

            public int Pop() {
                int res = heap[0].Node;
                Count--;
                if (Count > 0) {
                    heap[0] = heap[Count];
                    int i = 0;
                    while (true) {
                        int left = (i << 1) + 1;
                        if (left >= Count) break;
                        int right = left + 1;
                        int best = (right < Count && heap[right].Priority < heap[left].Priority) ? right : left;
                        if (heap[i].Priority <= heap[best].Priority) break;
                        Entry tmp = heap[i]; heap[i] = heap[best]; heap[best] = tmp;
                        i = best;
                    }
                }
                return res;
            }
        }

        public RoadRoute FindRoute(PointF playerPt, PointF targetPt) {
            RoadRoute result = new RoadRoute {
                Success = false,
                PlayerPoint = playerPt,
                TargetPoint = targetPt,
                TotalDistanceMeters = DistanceMeters(playerPt, targetPt)
            };

            if (!IsLoaded) return result;

            // Direct distance threshold: if < 35m, direct line is sufficient
            if (result.TotalDistanceMeters < 35.0) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                result.Success = true;
                return result;
            }

            List<SnapResult> startCands = SnapToRoadCandidates(playerPt, 3500.0, 8);
            List<SnapResult> endCands = SnapToRoadCandidates(targetPt, 3500.0, 8);

            if (startCands.Count == 0 || endCands.Count == 0) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                return result;
            }

            // 1. Try all candidate combinations where compStart == compEnd (pure land/bridge route)
            RoadRoute bestConnectedRoute = null;
            double bestTotalDist = double.MaxValue;

            for (int s = 0; s < startCands.Count; s++) {
                SnapResult snapStart = startCands[s];
                int compStart = nodeComponent[edges[snapStart.EdgeIndex].U];

                for (int e = 0; e < endCands.Count; e++) {
                    SnapResult snapEnd = endCands[e];
                    int compEnd = nodeComponent[edges[snapEnd.EdgeIndex].U];

                    if (compStart == compEnd) {
                        RoadRoute candidateRoute = ComputeConnectedRoute(playerPt, targetPt, snapStart, snapEnd);
                        if (candidateRoute != null && candidateRoute.Success) {
                            if (candidateRoute.TotalDistanceMeters < bestTotalDist) {
                                bestTotalDist = candidateRoute.TotalDistanceMeters;
                                bestConnectedRoute = candidateRoute;
                            }
                        }
                    }
                }
            }

            if (bestConnectedRoute != null) {
                return bestConnectedRoute;
            }

            // 2. Both endpoints on main landmass fallback (pure land/bridge route, zero water)
            SnapResult snapStartMain = SnapToComponent(playerPt, mainComponentId, 4000.0);
            SnapResult snapEndMain = SnapToComponent(targetPt, mainComponentId, 4000.0);

            if (snapStartMain.EdgeIndex >= 0 && snapEndMain.EdgeIndex >= 0) {
                RoadRoute mainRoute = ComputeConnectedRoute(playerPt, targetPt, snapStartMain, snapEndMain);
                if (mainRoute != null && mainRoute.Success) {
                    return mainRoute;
                }
            }

            // 3. Fallback: ONLY for offshore islands where water traversal is absolutely unavoidable!
            return ComputeMultiModalRoute(playerPt, targetPt, startCands[0], endCands[0]);
        }

        private RoadRoute ComputeConnectedRoute(PointF playerPt, PointF targetPt, SnapResult snapStart, SnapResult snapEnd) {
            RoadRoute result = new RoadRoute {
                Success = false,
                PlayerPoint = playerPt,
                TargetPoint = targetPt,
                RoadEntryPoint = snapStart.Projected,
                RoadExitPoint = snapEnd.Projected,
                EntryDistanceMeters = snapStart.DistanceMeters,
                ExitDistanceMeters = snapEnd.DistanceMeters
            };

            // Direct walk optimization: if direct walk is shorter than driving off-road to road and back
            double directDist = DistanceMeters(playerPt, targetPt);
            if (directDist < 60.0 && snapStart.DistanceMeters + snapEnd.DistanceMeters > directDist * 1.5) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                result.TotalDistanceMeters = directDist;
                result.Success = true;
                return result;
            }

            // Case 1: Start and end snap to the exact same edge
            if (snapStart.EdgeIndex == snapEnd.EdgeIndex) {
                RoadEdge edge = edges[snapStart.EdgeIndex];
                List<PointF> subPath = new List<PointF>();
                subPath.Add(snapStart.Projected);

                if (snapStart.SegmentIndex <= snapEnd.SegmentIndex) {
                    for (int i = snapStart.SegmentIndex + 1; i <= snapEnd.SegmentIndex; i++) {
                        subPath.Add(edge.Points[i]);
                    }
                } else {
                    for (int i = snapStart.SegmentIndex; i > snapEnd.SegmentIndex; i--) {
                        subPath.Add(edge.Points[i]);
                    }
                }
                subPath.Add(snapEnd.Projected);

                double subLen = 0;
                for (int i = 0; i < subPath.Count - 1; i++) subLen += DistanceMeters(subPath[i], subPath[i + 1]);
                result.RoadDistanceMeters = subLen;
                result.TotalDistanceMeters = snapStart.DistanceMeters + subLen + snapEnd.DistanceMeters;
                result.Polyline = subPath.ToArray();
                result.Success = true;
                return result;
            }

            // Case 2: Zero-allocation A* search between endpoints
            RoadEdge eStart = edges[snapStart.EdgeIndex];
            RoadEdge eEnd = edges[snapEnd.EdgeIndex];

            // Compute distance from start projected point to each start node
            double distToU = 0;
            for (int i = 0; i <= snapStart.SegmentIndex; i++) {
                PointF p1 = (i == 0) ? eStart.Points[0] : eStart.Points[i];
                PointF p2 = (i == snapStart.SegmentIndex) ? snapStart.Projected : eStart.Points[i + 1];
                distToU += DistanceMeters(p1, p2);
            }
            double distToV = Math.Max(0.0, eStart.LengthMeters - distToU);

            int targetNodeA = eEnd.U;
            int targetNodeB = eEnd.V;
            PointF targetPosA = new PointF(nodes[targetNodeA].U, nodes[targetNodeA].V);
            PointF targetPosB = new PointF(nodes[targetNodeB].U, nodes[targetNodeB].V);

            int reachedEndNode = -1;
            int chosenStartNode = -1;

            lock (searchLock) {
                currentEpoch++;
                if (currentEpoch == 0) {
                    Array.Clear(visitedEpoch, 0, visitedEpoch.Length);
                    currentEpoch = 1;
                }
                pq.Clear();

                // Initialize start nodes
                gScores[eStart.U] = distToU;
                visitedEpoch[eStart.U] = currentEpoch;
                double hU = Math.Min(DistanceMeters(nodes[eStart.U].U, nodes[eStart.U].V, targetPosA.X, targetPosA.Y),
                                     DistanceMeters(nodes[eStart.U].U, nodes[eStart.U].V, targetPosB.X, targetPosB.Y));
                pq.Push(distToU + hU, eStart.U);

                gScores[eStart.V] = distToV;
                visitedEpoch[eStart.V] = currentEpoch;
                double hV = Math.Min(DistanceMeters(nodes[eStart.V].U, nodes[eStart.V].V, targetPosA.X, targetPosA.Y),
                                     DistanceMeters(nodes[eStart.V].U, nodes[eStart.V].V, targetPosB.X, targetPosB.Y));
                pq.Push(distToV + hV, eStart.V);

                while (pq.Count > 0) {
                    int current = pq.Pop();

                    if (current == targetNodeA || current == targetNodeB) {
                        reachedEndNode = current;
                        break;
                    }

                    double curG = gScores[current];
                    List<HalfEdge> outgoing = adj[current];
                    for (int i = 0; i < outgoing.Count; i++) {
                        HalfEdge edge = outgoing[i];
                        int neighbor = edge.TargetNode;
                        double tentG = curG + edge.Weight;

                        if (visitedEpoch[neighbor] != currentEpoch || tentG < gScores[neighbor]) {
                            gScores[neighbor] = tentG;
                            visitedEpoch[neighbor] = currentEpoch;
                            cameFrom[neighbor] = edge;

                            double hN = Math.Min(DistanceMeters(nodes[neighbor].U, nodes[neighbor].V, targetPosA.X, targetPosA.Y),
                                                 DistanceMeters(nodes[neighbor].U, nodes[neighbor].V, targetPosB.X, targetPosB.Y));
                            pq.Push(tentG + hN, neighbor);
                        }
                    }
                }

                if (reachedEndNode < 0) return null;

                // Trace back path
                List<HalfEdge> edgePath = new List<HalfEdge>();
                int trace = reachedEndNode;
                while (trace != eStart.U && trace != eStart.V) {
                    HalfEdge he = cameFrom[trace];
                    edgePath.Add(he);
                    trace = he.Forward ? edges[he.EdgeIndex].U : edges[he.EdgeIndex].V;
                }
                chosenStartNode = trace;
                edgePath.Reverse();

                // Construct smooth polyline
                List<PointF> polyline = new List<PointF>();
                List<int> junctions = new List<int>();

                // 1. From entry projected point along start edge to chosen start node
                if (chosenStartNode == eStart.U) {
                    polyline.Add(snapStart.Projected);
                    for (int i = snapStart.SegmentIndex; i >= 0; i--) polyline.Add(eStart.Points[i]);
                } else {
                    polyline.Add(snapStart.Projected);
                    for (int i = snapStart.SegmentIndex + 1; i < eStart.Points.Length; i++) polyline.Add(eStart.Points[i]);
                }

                // 2. Intermediate edges
                if(adj[chosenStartNode].Count>=3) junctions.Add(polyline.Count-1);
                for (int e = 0; e < edgePath.Count; e++) {
                    HalfEdge he = edgePath[e];
                    PointF[] pts = edges[he.EdgeIndex].Points;
                    if (he.Forward) {
                        for (int i = 1; i < pts.Length; i++) polyline.Add(pts[i]);
                    } else {
                        for (int i = pts.Length - 2; i >= 0; i--) polyline.Add(pts[i]);
                    }
                    if(adj[he.TargetNode].Count>=3) junctions.Add(polyline.Count-1);
                }

                // 3. Along end edge from reachedEndNode to exit projected point
                if (reachedEndNode == eEnd.U) {
                    for (int i = 1; i <= snapEnd.SegmentIndex; i++) polyline.Add(eEnd.Points[i]);
                    polyline.Add(snapEnd.Projected);
                } else {
                    for (int i = eEnd.Points.Length - 2; i > snapEnd.SegmentIndex; i--) polyline.Add(eEnd.Points[i]);
                    polyline.Add(snapEnd.Projected);
                }

                double roadLen = 0;
                for (int i = 0; i < polyline.Count - 1; i++) roadLen += DistanceMeters(polyline[i], polyline[i + 1]);

                result.Polyline = polyline.ToArray();
                result.JunctionIndices = junctions.ToArray();
                result.RoadDistanceMeters = roadLen;
                result.TotalDistanceMeters = snapStart.DistanceMeters + roadLen + snapEnd.DistanceMeters;
                result.Success = true;
                return result;
            }
        }

        private RoadRoute ComputeMultiModalRoute(PointF playerPt, PointF targetPt, SnapResult snapStart, SnapResult snapEnd) {
            // Find coastal departure/arrival node pair connecting start component to end component
            int compStart = nodeComponent[edges[snapStart.EdgeIndex].U];
            int compEnd = nodeComponent[edges[snapEnd.EdgeIndex].U];

            List<int> nodesS = componentNodes[compStart];
            List<int> nodesE = componentNodes[compEnd];

            int bestDep = -1;
            int bestArr = -1;
            double bestCost = double.MaxValue;

            // Subsample large components to keep query sub-millisecond
            int stepS = Math.Max(1, nodesS.Count / 80);
            int stepE = Math.Max(1, nodesE.Count / 80);

            for (int i = 0; i < nodesS.Count; i += stepS) {
                int ns = nodesS[i];
                float us = nodes[ns].U, vs = nodes[ns].V;
                double dStart = DistanceMeters(playerPt.X, playerPt.Y, us, vs);

                for (int j = 0; j < nodesE.Count; j += stepE) {
                    int ne = nodesE[j];
                    float ue = nodes[ne].U, ve = nodes[ne].V;
                    double dWater = DistanceMeters(us, vs, ue, ve);
                    double dTarget = DistanceMeters(ue, ve, targetPt.X, targetPt.Y);

                    double totalCost = dStart + dWater * 1.8 + dTarget;
                    if (totalCost < bestCost) {
                        bestCost = totalCost;
                        bestDep = ns;
                        bestArr = ne;
                    }
                }
            }

            if (bestDep < 0 || bestArr < 0) {
                return new RoadRoute {
                    Success = false,
                    PlayerPoint = playerPt,
                    TargetPoint = targetPt,
                    Polyline = new PointF[] { playerPt, targetPt },
                    TotalDistanceMeters = DistanceMeters(playerPt, targetPt)
                };
            }

            PointF depPt = new PointF(nodes[bestDep].U, nodes[bestDep].V);
            PointF arrPt = new PointF(nodes[bestArr].U, nodes[bestArr].V);

            // Compute land leg 1: Player -> departure point
            SnapResult snapDep = SnapToRoad(depPt, 500.0);
            RoadRoute leg1 = (snapDep.EdgeIndex >= 0) ? ComputeConnectedRoute(playerPt, depPt, snapStart, snapDep) : null;

            // Compute land leg 2: Arrival point -> Target
            SnapResult snapArr = SnapToRoad(arrPt, 500.0);
            RoadRoute leg2 = (snapArr.EdgeIndex >= 0) ? ComputeConnectedRoute(arrPt, targetPt, snapArr, snapEnd) : null;

            List<PointF> fullPoly = new List<PointF>();
            if (leg1 != null && leg1.Success && leg1.Polyline != null) {
                for (int i = 0; i < leg1.Polyline.Length; i++) fullPoly.Add(leg1.Polyline[i]);
            } else {
                fullPoly.Add(playerPt);
                fullPoly.Add(depPt);
            }

            // Nautical link
            fullPoly.Add(arrPt);

            if (leg2 != null && leg2.Success && leg2.Polyline != null) {
                for (int i = 1; i < leg2.Polyline.Length; i++) fullPoly.Add(leg2.Polyline[i]);
            } else {
                fullPoly.Add(targetPt);
            }

            double waterDist = DistanceMeters(depPt, arrPt);
            double totalDist = (leg1 != null ? leg1.TotalDistanceMeters : DistanceMeters(playerPt, depPt)) +
                               waterDist +
                               (leg2 != null ? leg2.TotalDistanceMeters : DistanceMeters(arrPt, targetPt));

            return new RoadRoute {
                Success = true,
                PlayerPoint = playerPt,
                RoadEntryPoint = snapStart.Projected,
                RoadExitPoint = snapEnd.Projected,
                TargetPoint = targetPt,
                Polyline = fullPoly.ToArray(),
                TotalDistanceMeters = totalDist,
                RoadDistanceMeters = totalDist - waterDist,
                EntryDistanceMeters = snapStart.DistanceMeters,
                ExitDistanceMeters = snapEnd.DistanceMeters,
                HasWaterTransit = true,
                WaterDeparturePoint = depPt,
                WaterArrivalPoint = arrPt,
                WaterDistanceMeters = waterDist
            };
        }
    }
}
