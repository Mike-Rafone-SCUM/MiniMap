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
        public double TotalDistanceMeters;
        public double RoadDistanceMeters;
        public double EntryDistanceMeters;
        public double ExitDistanceMeters;
    }

    public class RoadRouter {
        private static RoadRouter instance;
        public static RoadRouter Instance {
            get {
                if (instance == null) instance = new RoadRouter();
                return instance;
            }
        }

        public bool IsLoaded { get; private set; }

        private struct RoadNode {
            public float U;
            public float V;
        }

        private struct RoadEdge {
            public int U;
            public int V;
            public float LengthMeters;
            public PointF[] Points;
        }

        private struct HalfEdge {
            public int TargetNode;
            public float Weight;
            public int EdgeIndex;
            public bool Forward;
        }

        private RoadNode[] nodes;
        private RoadEdge[] edges;
        private List<HalfEdge>[] adj;
        private int gridRes;
        private ushort[][] spatialGrid;

        private const double MapScaleX = 1521618.0 / 100.0; // 15216.18 meters across map
        private const double MapScaleY = 1523618.0 / 100.0; // 15236.18 meters across map

        public static double DistanceMeters(PointF a, PointF b) {
            double dx = (b.X - a.X) * MapScaleX;
            double dy = (b.Y - a.Y) * MapScaleY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public void InitializeFromResource() {
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
                // Fallback: check file on disk if running unbundled (e.g. via PowerShell Add-Type)
                string[] candidates = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScumMiniMap", "roads.bin"),
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
                // If road asset fails to load, gracefully disable road routing
                IsLoaded = false;
            }
        }

        public void LoadBinary(BinaryReader br) {
            byte[] magic = br.ReadBytes(4);
            if (magic[0] != 'R' || magic[1] != 'O' || magic[2] != 'A' || magic[3] != 'D') return;
            uint version = br.ReadUInt32();
            if (version != 1) return;

            int nodeCount = br.ReadInt32();
            int edgeCount = br.ReadInt32();
            gridRes = br.ReadInt32();

            nodes = new RoadNode[nodeCount];
            for (int i = 0; i < nodeCount; i++) {
                nodes[i] = new RoadNode { U = br.ReadSingle(), V = br.ReadSingle() };
            }

            edges = new RoadEdge[edgeCount];
            adj = new List<HalfEdge>[nodeCount];
            for (int i = 0; i < nodeCount; i++) adj[i] = new List<HalfEdge>();

            for (int i = 0; i < edgeCount; i++) {
                int u = br.ReadInt32();
                int v = br.ReadInt32();
                float lengthM = br.ReadSingle();
                ushort ptCount = br.ReadUInt16();
                PointF[] pts = new PointF[ptCount];
                for (int p = 0; p < ptCount; p++) {
                    pts[p] = new PointF(br.ReadSingle(), br.ReadSingle());
                }
                edges[i] = new RoadEdge { U = u, V = v, LengthMeters = lengthM, Points = pts };
                adj[u].Add(new HalfEdge { TargetNode = v, Weight = lengthM, EdgeIndex = i, Forward = true });
                adj[v].Add(new HalfEdge { TargetNode = u, Weight = lengthM, EdgeIndex = i, Forward = false });
            }

            int totalCells = gridRes * gridRes;
            spatialGrid = new ushort[totalCells][];
            for (int i = 0; i < totalCells; i++) {
                ushort count = br.ReadUInt16();
                ushort[] list = new ushort[count];
                for (int c = 0; c < count; c++) list[c] = br.ReadUInt16();
                spatialGrid[i] = list;
            }

            IsLoaded = true;
        }

        private struct SnapResult {
            public int EdgeIndex;
            public PointF Projected;
            public double DistanceMeters;
            public int SegmentIndex;
            public double SegmentT;
        }

        private SnapResult SnapToRoad(PointF pt, double maxSearchMeters = 2000.0) {
            SnapResult best = new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };
            if (!IsLoaded || edges == null || edges.Length == 0) return best;

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
                                if (d < best.DistanceMeters) {
                                    best.DistanceMeters = d;
                                    best.EdgeIndex = eid;
                                    best.Projected = proj;
                                    best.SegmentIndex = i;
                                    best.SegmentT = t;
                                    foundInRing = true;
                                }
                            }
                        }
                    }
                }
                // Once we find a match in ring R, checking one more ring guarantees we found the global closest
                if (foundInRing && best.DistanceMeters <= maxSearchMeters && r >= 1) break;
            }

            return best;
        }

        private class MinHeap {
            private struct Entry {
                public double Priority;
                public int Node;
            }
            private Entry[] heap = new Entry[256];
            public int Count { get; private set; }

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

            // Direct distance threshold: if < 40m, direct line is sufficient
            if (result.TotalDistanceMeters < 40.0) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                return result;
            }

            SnapResult snapStart = SnapToRoad(playerPt);
            SnapResult snapEnd = SnapToRoad(targetPt);

            if (snapStart.EdgeIndex < 0 || snapEnd.EdgeIndex < 0) return result;

            result.RoadEntryPoint = snapStart.Projected;
            result.RoadExitPoint = snapEnd.Projected;
            result.EntryDistanceMeters = snapStart.DistanceMeters;
            result.ExitDistanceMeters = snapEnd.DistanceMeters;

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

            // Case 2: A* between start edge endpoints and end edge endpoints
            RoadEdge eStart = edges[snapStart.EdgeIndex];
            RoadEdge eEnd = edges[snapEnd.EdgeIndex];

            int[] startNodes = new int[] { eStart.U, eStart.V };
            int targetNodeA = eEnd.U;
            int targetNodeB = eEnd.V;

            // Compute distance from start projected point to each start node along the start edge
            double distToU = 0;
            for (int i = 0; i <= snapStart.SegmentIndex; i++) {
                PointF p1 = (i == 0) ? eStart.Points[0] : eStart.Points[i];
                PointF p2 = (i == snapStart.SegmentIndex) ? snapStart.Projected : eStart.Points[i + 1];
                distToU += DistanceMeters(p1, p2);
            }
            double distToV = Math.Max(0.0, eStart.LengthMeters - distToU);

            PointF targetRefPos = new PointF(nodes[targetNodeA].U, nodes[targetNodeA].V);

            // A* state
            Dictionary<int, double> gScore = new Dictionary<int, double>(1024);
            Dictionary<int, HalfEdge> cameFrom = new Dictionary<int, HalfEdge>(1024);
            MinHeap pq = new MinHeap();

            gScore[eStart.U] = distToU;
            pq.Push(distToU + DistanceMeters(new PointF(nodes[eStart.U].U, nodes[eStart.U].V), targetRefPos), eStart.U);

            gScore[eStart.V] = distToV;
            pq.Push(distToV + DistanceMeters(new PointF(nodes[eStart.V].U, nodes[eStart.V].V), targetRefPos), eStart.V);

            int reachedEndNode = -1;

            while (pq.Count > 0) {
                int current = pq.Pop();

                if (current == targetNodeA || current == targetNodeB) {
                    reachedEndNode = current;
                    break;
                }

                double curG = gScore[current];
                List<HalfEdge> outgoing = adj[current];
                for (int i = 0; i < outgoing.Count; i++) {
                    HalfEdge edge = outgoing[i];
                    int neighbor = edge.TargetNode;
                    double tentG = curG + edge.Weight;

                    double existingG;
                    if (!gScore.TryGetValue(neighbor, out existingG) || tentG < existingG) {
                        gScore[neighbor] = tentG;
                        cameFrom[neighbor] = edge;
                        PointF nPos = new PointF(nodes[neighbor].U, nodes[neighbor].V);
                        double h = tentG + DistanceMeters(nPos, targetRefPos);
                        pq.Push(h, neighbor);
                    }
                }
            }

            if (reachedEndNode < 0) {
                // No connected road route exists (e.g. across water to separate island)
                return result;
            }

            // Reconstruct path of edges
            List<HalfEdge> edgePath = new List<HalfEdge>();
            int trace = reachedEndNode;
            while (trace != eStart.U && trace != eStart.V) {
                HalfEdge he = cameFrom[trace];
                edgePath.Add(he);
                trace = he.Forward ? edges[he.EdgeIndex].U : edges[he.EdgeIndex].V;
            }
            edgePath.Reverse();

            // Build continuous polyline
            List<PointF> polyline = new List<PointF>();

            // 1. From entry projected point along start edge to the chosen start node
            int chosenStartNode = trace;
            if (chosenStartNode == eStart.U) {
                // Go backward from proj to U (index 0)
                polyline.Add(snapStart.Projected);
                for (int i = snapStart.SegmentIndex; i >= 0; i--) polyline.Add(eStart.Points[i]);
            } else {
                // Go forward from proj to V (index pts.Length - 1)
                polyline.Add(snapStart.Projected);
                for (int i = snapStart.SegmentIndex + 1; i < eStart.Points.Length; i++) polyline.Add(eStart.Points[i]);
            }

            // 2. Intermediate edges
            for (int e = 0; e < edgePath.Count; e++) {
                HalfEdge he = edgePath[e];
                PointF[] pts = edges[he.EdgeIndex].Points;
                if (he.Forward) {
                    // Node U -> Node V
                    for (int i = 1; i < pts.Length; i++) polyline.Add(pts[i]);
                } else {
                    // Node V -> Node U
                    for (int i = pts.Length - 2; i >= 0; i--) polyline.Add(pts[i]);
                }
            }

            // 3. Along end edge from reachedEndNode to exit projected point
            if (reachedEndNode == eEnd.U) {
                // Forward from U (index 0) to proj
                for (int i = 1; i <= snapEnd.SegmentIndex; i++) polyline.Add(eEnd.Points[i]);
                polyline.Add(snapEnd.Projected);
            } else {
                // Backward from V (index pts.Length - 1) to proj
                for (int i = eEnd.Points.Length - 2; i > snapEnd.SegmentIndex; i--) polyline.Add(eEnd.Points[i]);
                polyline.Add(snapEnd.Projected);
            }

            double roadLen = 0;
            for (int i = 0; i < polyline.Count - 1; i++) roadLen += DistanceMeters(polyline[i], polyline[i + 1]);

            result.Polyline = polyline.ToArray();
            result.RoadDistanceMeters = roadLen;
            result.TotalDistanceMeters = snapStart.DistanceMeters + roadLen + snapEnd.DistanceMeters;
            result.Success = true;
            return result;
        }
    }
}
