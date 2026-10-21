using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScumMiniMap {
    internal sealed partial class WpfMapSurface {
        // Index lightweight records once; prepare graphics only for visible cells.
        sealed class PoiIndex {
            readonly Dictionary<Point,List<WpfPoiCluster>> cells=new Dictionary<Point,List<WpfPoiCluster>>(poiCellComparer);
            readonly Dictionary<WpfPoiCluster,int> order=new Dictionary<WpfPoiCluster,int>();
            internal PoiIndex(WpfPoiCluster[] layout) {
                foreach(var cluster in layout) {
                    order.Add(cluster,order.Count);
                    var key=new Point(Math.Floor(cluster.Point.X*64),Math.Floor(cluster.Point.Y*64));List<WpfPoiCluster> cell;
                    if(!cells.TryGetValue(key,out cell))cells.Add(key,cell=new List<WpfPoiCluster>());cell.Add(cluster);
                }
            }
            internal IEnumerable<WpfPoiCluster> Query(Rect bounds) {
                return QueryCells(bounds).OrderBy(cluster=>order[cluster]);
            }
            IEnumerable<WpfPoiCluster> QueryCells(Rect bounds) {
                int x1=Math.Max(-1,(int)Math.Floor(bounds.Left*64)),y1=Math.Max(-1,(int)Math.Floor(bounds.Top*64));
                int x2=Math.Min(64,(int)Math.Floor(bounds.Right*64)),y2=Math.Min(64,(int)Math.Floor(bounds.Bottom*64));
                for(int y=y1;y<=y2;y++)for(int x=x1;x<=x2;x++) {
                    List<WpfPoiCluster> cell;if(!cells.TryGetValue(new Point(x,y),out cell))continue;
                    foreach(var cluster in cell)if(bounds.Contains(new Point(cluster.Point.X,cluster.Point.Y)))yield return cluster;
                }
            }
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WpfPoiCluster[],PoiIndex> poiIndexes=new System.Runtime.CompilerServices.ConditionalWeakTable<WpfPoiCluster[],PoiIndex>();
        static PoiIndex Index(WpfPoiCluster[] layout) { return poiIndexes.GetValue(layout,items=>new PoiIndex(items)); }
        static Rect ViewBounds(WpfMapState state,double width,double height,bool compact,double margin) {
            double side=MapSide(state,width,height,compact);var centre=CameraCentre(state,width,height,compact);
            double angle=Rotation(state,compact)*Math.PI/180,c=Math.Abs(Math.Cos(angle)),s=Math.Abs(Math.Sin(angle));
            double halfWidth=(width*c+height*s)/2+margin,halfHeight=(width*s+height*c)/2+margin;
            return new Rect(centre.X-halfWidth/side,centre.Y-halfHeight/side,halfWidth*2/side,halfHeight*2/side);
        }
        internal static IEnumerable<WpfPoiCluster> VisiblePoiClusters(WpfMapState state,double width,double height,bool compact) {
            if(width<=0||height<=0)return new WpfPoiCluster[0];
            return Index(PoiClusters(state,width,height,compact)).Query(ViewBounds(state,width,height,compact,24));
        }
        static bool VisibleMarker(Point at,double width,double height,double rotation) {
            var screen=RotatePoint(at,rotation,width,height);
            return screen.X>=-252&&screen.X<=width+24&&screen.Y>=-40&&screen.Y<=height+48;
        }
        sealed class StreamedPoiTile {
            internal Point Key;internal Rect Bounds;internal DrawingGroup Drawing;internal int Pixels;internal long Used;
        }
        readonly Dictionary<Point,StreamedPoiTile> streamedPoiTiles=new Dictionary<Point,StreamedPoiTile>(poiCellComparer);
        bool poiBatchRunning;
        long poiUse;
        double streamedPoiDpi,streamedPoiSpan;
        internal int PoiTilesBuilt,PoiStreamResets;
        internal bool PoiViewportReady;
        internal int PoiCachedTiles { get { return streamedPoiTiles.Count; } }
        internal int PoiCachedPixels { get { return streamedPoiTiles.Values.Sum(tile=>tile.Pixels); } }
        internal void ResetPoiStream() {
            if(poiBuild!=null) { poiBuild.Cancel();poiBuild=null; }
            System.Threading.Interlocked.Increment(ref poiGeneration);poiBatchRunning=false;
            requestedPoiLayout=drawnPoiLayout=null;streamedPoiTiles.Clear();poiSceneTiles=null;PoiViewportReady=false;PoiStreamResets++;
        }
        void StreamPois(DrawingContext dc,double side,double left,double top,double width,double height) {
            Dispatcher.VerifyAccess();PoiTilesDrawn=0;
            var layout=PoiClusters(State,width,height,false);double dpi=PoiRasterDensity(width,height,VisualTreeHelper.GetDpi(this).DpiScaleX);
            double span=Math.Max(512,Math.Ceiling(Math.Max(width,height)/2048)*512);
            if(!object.ReferenceEquals(layout,requestedPoiLayout)||requestedPoiSide!=side||streamedPoiDpi!=dpi||streamedPoiSpan!=span) {
                ResetPoiStream();requestedPoiLayout=layout;requestedPoiSide=drawnPoiSide=side;streamedPoiDpi=dpi;streamedPoiSpan=span;
            }
            var bounds=new Rect((-left-24)/side,(-top-24)/side,(width+48)/side,(height+48)/side);
            var wanted=Index(layout).Query(bounds).GroupBy(cluster=>new Point(Math.Floor(cluster.Point.X*side/span),Math.Floor(cluster.Point.Y*side/span))).OrderBy(cell=>cell.Key.Y).ThenBy(cell=>cell.Key.X).ToArray();
            var missing=new List<IGrouping<Point,WpfPoiCluster>>();
            foreach(var cell in wanted) {
                StreamedPoiTile tile;if(streamedPoiTiles.TryGetValue(cell.Key,out tile)) { tile.Used=++poiUse;dc.PushTransform(new TranslateTransform(left,top));dc.DrawDrawing(tile.Drawing);dc.Pop();PoiTilesDrawn++; }
                else missing.Add(cell);
            }
            PoiViewportReady=missing.Count==0;
            if(PoiViewportReady)drawnPoiLayout=layout;
            // There is one small batch in flight. The next frame requests the
            // latest viewport, never a backlog of cells from earlier camera positions.
            if(!poiBatchRunning&&missing.Count>0) {
                var batch=new List<Tuple<StreamedPoiTile,PoiStamp[]>>();
                foreach(var cell in missing.OrderBy(cell=>Math.Abs((cell.Key.X+.5)*span+left-width/2)+Math.Abs((cell.Key.Y+.5)*span+top-height/2)).Take(4)) {
                    var tile=new StreamedPoiTile { Key=cell.Key,Bounds=new Rect(cell.Key.X*span-24,cell.Key.Y*span-24,span+48,span+48) };
                    var cellBounds=new Rect(cell.Key.X*span/side,cell.Key.Y*span/side,span/side,span/side);cellBounds.Inflate(1e-7,1e-7);
                    var stamps=new List<PoiStamp>();
                    // Complete each cell so panning within it cannot omit icons.
                    foreach(var cluster in Index(layout).Query(cellBounds)) {
                        if(Math.Floor(cluster.Point.X*side/span)!=cell.Key.X||Math.Floor(cluster.Point.Y*side/span)!=cell.Key.Y)continue;
                        ScumMapCategory category;State.Store.CategoryById.TryGetValue(cluster.Members[0].CategoryId,out category);EnsurePoiAssets(category,cluster);
                        stamps.Add(new PoiStamp { At=new Point(cluster.Point.X*side,cluster.Point.Y*side),Radius=cluster.Radius,Scale=(cluster.Members.Count>1?11:cluster.Radius*1.45)/12,Icon=cluster.Icon,Outline=cluster.Outline });
                    }
                    batch.Add(Tuple.Create(tile,stamps.ToArray()));
                }
                QueuePoiBatch(batch,layout,side,dpi);
            }
        }
        void QueuePoiBatch(List<Tuple<StreamedPoiTile,PoiStamp[]>> batch,WpfPoiCluster[] layout,double side,double dpi) {
            poiBatchRunning=true;var cancel=new System.Threading.CancellationTokenSource();poiBuild=cancel;long generation=poiGeneration;var dispatcher=Dispatcher;
            System.Threading.Tasks.Task.Run(async()=> {
                bool entered=false;
                try {
                    await poiBuildSlots.WaitAsync(cancel.Token);entered=true;
                    foreach(var item in batch) {
                        cancel.Token.ThrowIfCancellationRequested();var tile=item.Item1;var drawing=RecordPois(item.Item2,cancel.Token);
                        if(side*dpi<=4096) {
                            int pixels=(int)Math.Ceiling(tile.Bounds.Width*dpi);var visual=new DrawingVisual();
                            double scale=pixels/tile.Bounds.Width;
                            using(var context=visual.RenderOpen()) { context.PushTransform(new MatrixTransform(scale,0,0,scale,-tile.Bounds.X*scale,-tile.Bounds.Y*scale));context.DrawDrawing(drawing);context.Pop(); }
                            var image=new RenderTargetBitmap(pixels,pixels,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();
                            drawing=new DrawingGroup();using(var context=drawing.Open())context.DrawImage(image,tile.Bounds);drawing.Freeze();tile.Pixels=pixels*pixels;
                        }
                        tile.Drawing=drawing;
                    }
                    cancel.Token.ThrowIfCancellationRequested();
                    PostPoiScene(dispatcher,()=> {
                        if(generation!=poiGeneration)return;
                        foreach(var item in batch) { var tile=item.Item1;tile.Used=++poiUse;streamedPoiTiles[tile.Key]=tile;PoiTilesBuilt++; }
                        while(streamedPoiTiles.Count>48||PoiCachedPixels>16*1024*1024)streamedPoiTiles.Remove(streamedPoiTiles.Values.OrderBy(tile=>tile.Used).First().Key);
                        poiSceneTiles=streamedPoiTiles.Values.Select(tile=>new PoiSceneTile { Bounds=tile.Bounds,Drawing=tile.Drawing }).ToArray();
                        PoiSceneRasterized=side*dpi<=4096;drawnPoiLayout=layout;drawnPoiSide=side;PoiSceneBuilds++;poiBatchRunning=false;poiBuild=null;RequestRedraw();
                    });
                }catch(OperationCanceledException) { }
                catch(Exception error) { Program.LogException("PoiStream",error);PostPoiScene(dispatcher,()=> { if(generation==poiGeneration) { poiBatchRunning=false;poiBuild=null; } }); }
                finally { if(entered)poiBuildSlots.Release();PostPoiScene(dispatcher,()=> { if(object.ReferenceEquals(poiBuild,cancel))poiBuild=null;cancel.Dispose(); }); }
            });
        }
        internal static double PoiRasterDensity(double width,double height,double dpi) { return Math.Min(dpi,Math.Min(2,1600/Math.Max(width,height))); }
        sealed class RouteChunk {
            internal int Start,End;internal Rect Bounds;
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Drawing.PointF[],RouteChunk[]> routeChunks=new System.Runtime.CompilerServices.ConditionalWeakTable<System.Drawing.PointF[],RouteChunk[]>();
        static RouteChunk[] IndexRoute(System.Drawing.PointF[] points) {
            var chunks=new List<RouteChunk>();
            for(int start=0;start<points.Length-1;start+=128) {
                int end=Math.Min(points.Length-1,start+128);double x1=points[start].X,x2=x1,y1=points[start].Y,y2=y1;
                for(int i=start+1;i<=end;i++) { x1=Math.Min(x1,points[i].X);x2=Math.Max(x2,points[i].X);y1=Math.Min(y1,points[i].Y);y2=Math.Max(y2,points[i].Y); }
                chunks.Add(new RouteChunk { Start=start,End=end,Bounds=new Rect(x1,y1,x2-x1,y2-y1) });
            }
            return chunks.ToArray();
        }
        internal int RouteVerticesDrawn;
        void DrawStreamedRoute(DrawingContext dc,System.Drawing.PointF[] points,Brush ink,bool verified,Func<System.Drawing.PointF,Point> point) {
            if(points==null||points.Length<2)return;
            var viewport=ViewBounds(State,ActualWidth,ActualHeight,Compact,8);int first=-1,last=-1;
            Action flush=()=> {
                if(first<0)return;var pen=new Pen(ink,RouteThickness);if(!verified)pen.DashStyle=DashStyles.Dash;
                RouteVerticesDrawn+=last-first+1;DrawLine(dc,Enumerable.Range(first,last-first+1).Select(i=>point(points[i])),pen);first=-1;
            };
            // Contiguous visible chunks stay in one figure, preserving joins.
            // Segments crossing the viewport survive even if both ends are outside.
            foreach(var chunk in routeChunks.GetValue(points,IndexRoute)) {
                if(!chunk.Bounds.IntersectsWith(viewport)) { flush();continue; }
                if(first<0)first=chunk.Start;last=chunk.End;
            }
            flush();
        }
        void UpdateSharedLayer(string camera) {
            var key=new System.Text.StringBuilder(camera).Append('|').Append(Localization.CurrentCode).Append('|').Append(State.LocalPeerId).Append('|').Append(State.RouteColor).Append('|').Append(State.ActiveSquadRoute).Append('|').Append(State.ZoneRevision).Append('|').Append(Identity(State.Saved));
            foreach(var marker in State.SharedMarkers)key.Append('|').Append(marker.OwnerId).Append('|').Append(marker.Owner).Append('|').Append(marker.Id).Append('|').Append(marker.Name).Append('|').Append(marker.Death).Append('|').Append(marker.X).Append('|').Append(marker.Y);
            key.Append('|').Append(Identity(State.OwnRoutePlan)).Append('|').Append(Identity(State.SquadPlan));
            key.Append('|').Append(Identity(State.Route)).Append('|').Append(Identity(State.Target)).Append('|').Append(State.Position!=null);
            foreach(var plan in State.OtherRoutePlans)key.Append('|').Append(plan.Key).Append('|').Append(Identity(plan.Plan));
            foreach(var marker in State.LocalRoute)key.Append('|').Append(SquadState.RouteKey(marker));
            foreach(var death in State.Saved.Where(zone=>zone.IsDeathMarker))key.Append('|').Append(death.DisplayName);
            string next=key.ToString();if(sharedLayerKey==next)return;
            using(var context=sharedLayer.RenderOpen())DrawMapPass(context,MapPass.Shared);sharedLayerKey=next;SharedLayerBuilds++;
        }
    }
}
