using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using ScumMiniMap;

public static class CapturedRoadTests {
    static void Check(bool value,string label) {
        if(!value) throw new Exception(label);
        Console.WriteLine("PASS: "+label);
    }
    static PointF P(float x,float y) { return new PointF(x,y); }
    static void LoadFixture(RoadRouter router,PointF[] nodes,int[][] edges,bool bridge=false) {
        using(var stream=new MemoryStream()) {
            var writer=new BinaryWriter(stream);
            writer.Write(new byte[]{82,79,65,68}); writer.Write((uint)3);
            writer.Write(nodes.Length); writer.Write(edges.Length); writer.Write(1);
            foreach(var p in nodes) { writer.Write(p.X); writer.Write(p.Y); }
            foreach(var edge in edges) {
                writer.Write(edge[0]); writer.Write(edge[1]); writer.Write(100f); writer.Write((byte)(bridge?1:0)); writer.Write((ushort)2);
                foreach(int i in edge) { writer.Write(nodes[i].X); writer.Write(nodes[i].Y); }
            }
            writer.Write((ushort)edges.Length);
            for(int i=0;i<edges.Length;i++) writer.Write((ushort)i);
            writer.Flush(); stream.Position=0; router.LoadBinary(new BinaryReader(stream));
        }
    }
    public static int Main() {
        try {
            var router=RoadRouter.Instance; router.InitializeFromResource();
            Check(router.IsLoaded,"Captured classified road network loads");
            var geometry=router.PresentationGeometry();
            Check(geometry.Length>12720,"Runtime loads classified roads, split junctions and audited connectors");
            var components=(System.Collections.Generic.List<int>[])typeof(RoadRouter).GetField("componentNodes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
            int total=0,largest=0;
            foreach(var component in components) { total+=component.Count; largest=Math.Max(largest,component.Count); }
            Check(largest>total*.94,"Main drivable network connects over 94 percent of compiled road nodes");
            RoadRoute demo=router.FindRoute(P(.90049494f,.48457418f),P(.93406296f,.46411991f));
            Check(demo.Success && demo.RoadDistanceMeters>2000 && demo.RoadDistanceMeters<2200,
                  "Saved 39-segment server-test route remains connected at approximately 2.1 km");
            Check(demo.EntryDistanceMeters<1 && demo.ExitDistanceMeters<1,"Demo uses actual captured road endpoints");
            var water=new RoutingWaterMask(); water.Load();
            Check(water.IsLoaded,"Water coverage loads");
            PointF[] northB2={P(.40960548f,.39800242f),P(.50006073f,.40556735f),P(.57332342f,.39900682f)};
            PointF[] zIslands={P(.10288965f,.89523792f),P(.30588488f,.90629353f),P(.50756363f,.91399520f),P(.69947680f,.90420452f),P(.89309654f,.90364377f)};
            for(int start=0;start<northB2.Length;start++) for(int island=0;island<zIslands.Length;island++) {
                RoadRoute trip=router.FindRoute(northB2[start],zIslands[island]);
                Check(trip.Success && trip.Polyline!=null && trip.Polyline.Length>2,
                      "Northern B2 approach "+(start+1)+" reaches the actual Z"+(4-island)+" island road network");
                Check(trip.EntryDistanceMeters<3 && trip.ExitDistanceMeters<3,
                      "B2-to-Z routing reaches the island road itself without an off-road substitution");
                Check(trip.TotalDistanceMeters<25000,"B2-to-Z island journey avoids an excessive map-wide detour");
                if(island<2)Check(Array.Exists(trip.Polyline,point=>point.X>.28f && point.X<.31f && point.Y>.76f && point.Y<.82f),
                      "B2-to-western-Z routing uses the Rogoznica road bridge rather than circling the island");
            }
            Check(!router.FindRoute(P(.418f,.487f),P(.4181f,.487f)).Success,"Short lake crossing is rejected");
            Check(!router.FindRoute(P(.35f,.45f),P(.02f,.5f)).Success,"Offshore route is rejected");
            RoadRoute rail=router.FindRoute(P(.46625f,.74648f),P(.49277f,.77264f));
            Check(!rail.Success || (rail.RoadDistanceMeters>1 && water.AllowsConnector(rail.PlayerPoint,rail.RoadEntryPoint) && water.AllowsConnector(rail.RoadExitPoint,rail.TargetPoint)),
                  "Rail-adjacent targets require classified road travel and dry approaches");
            foreach(var trip in new[]{
                new[]{P(.62767f,.63618f),P(.67270f,.03878f)},
                new[]{P(.67542f,.47654f),P(.27907f,.30848f)},
                new[]{P(.28624f,.75685f),P(.29397f,.82816f)},
                new[]{P(.64612f,.72455f),P(.62606f,.79659f)},
                new[]{P(.35f,.45f),P(.295f,.835f)},
                new[]{P(.55f,.35f),P(.60f,.50f)}
            }) {
                RoadRoute route=router.FindRoute(trip[0],trip[1]);
                Console.WriteLine("Trip distance: "+route.TotalDistanceMeters.ToString("F0")+" m (direct "+RoadRouter.DistanceMeters(trip[0],trip[1]).ToString("F0")+" m)");
                if(!route.Success) {
                    var edgeData=(RoadRouter.RoadEdge[])typeof(RoadRouter).GetField("edges",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
                    var nodeComponents=(int[])typeof(RoadRouter).GetField("nodeComponent",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
                    foreach(var point in trip) foreach(var snap in router.SnapToRoadCandidates(point,1000,8)) {
                        int component=nodeComponents[edgeData[snap.EdgeIndex].U];
                        Console.WriteLine("Unreachable snap: "+point+" distance="+snap.DistanceMeters+" componentSize="+components[component].Count+" edge="+snap.EdgeIndex);
                    }
                }
                Check(route.Success,"Normal mainland/island journey has a connected road route: "+trip[0]+" -> "+trip[1]);
                Check(route.EntryDistanceMeters<=1000 && route.ExitDistanceMeters<=1000,
                      "Road approaches stay inside the bounded search radius");
                Check(route.TotalDistanceMeters<RoadRouter.DistanceMeters(trip[0],trip[1])*6,
                      "Normal journey avoids the disconnected-junction detour regression");
                Check(water.AllowsConnector(trip[0],route.RoadEntryPoint) && water.AllowsConnector(route.RoadExitPoint,trip[1]),
                      "Normal journey approaches do not cross mapped water");
            }
            // Exercise spatially distributed road destinations, not just the one
            // short saved route that allowed the disconnected graph to pass.
            var roadNodes=(RoadRouter.RoadNode[])typeof(RoadRouter).GetField("nodes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
            int main=(int)typeof(RoadRouter).GetField("mainComponentId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(router);
            var cells=new System.Collections.Generic.SortedDictionary<int,PointF>();
            foreach(int index in components[main]) {
                var node=roadNodes[index]; PointF point=P(node.U,node.V);
                int key=(int)(node.U*8)+8*(int)(node.V*8);
                if(!cells.ContainsKey(key) && water.AllowsConnector(point,point))cells[key]=point;
            }
            var samples=new System.Collections.Generic.List<PointF>(cells.Values);
            int sweep=0;
            for(int i=0;i<samples.Count;i++) {
                PointF from=samples[i],to=samples[(i+samples.Count/2)%samples.Count];
                Check(router.FindRoute(from,to).Success,"Distributed map route "+(++sweep)+" across cells: "+from+" -> "+to);
            }
            Check(sweep>=40,"Coverage sweep spans at least 40 occupied map cells");
            // Find a genuinely dry strip for isolated graph tests using the real mask.
            PointF origin=default(PointF); bool found=false;
            for(float y=.1f;y<.7f && !found;y+=.01f)
                for(float x=.1f;x<.9f && !found;x+=.01f)
                    if(water.AllowsConnector(P(x,y),P(x+.03f,y))) { origin=P(x,y); found=true; }
            Check(found,"Dry fixture strip is verified against bundled water coverage");
            var a=origin; var b=P(a.X+.006f,a.Y); var c=P(b.X+.0007f,a.Y); var d=P(c.X+.006f,a.Y);
            LoadFixture(router,new[]{a,b,c,d},new[]{new[]{0,1},new[]{2,3}});
            Check(!router.FindRoute(a,d).Success,"Ten-metre disconnected road gap is not healed or bypassed by a farther snap");
            Check(!router.FindRoute(a,P(a.X+.0005f,a.Y+.0005f)).Success ||
                  router.FindRoute(a,P(a.X+.0005f,a.Y+.0005f)).RoadDistanceMeters>0,
                  "Short trips do not report an unverified direct line as road routing");
            LoadFixture(router,new[]{a,b,d},new[]{new[]{0,1},new[]{1,2}});
            Check(router.FindRoute(a,d).Success && router.FindRoute(d,a).Success,
                  "Explicit captured junction works in both directions");
            // An explicit road edge may be a bridge over mapped water. Its endpoints
            // must still have dry approaches; the water mask must not erase the edge.
            PointF shoreA=default(PointF),shoreB=default(PointF); bool shores=false;
            for(float y=.3f;y<.8f && !shores;y+=.002f)
                for(float x=.2f;x<.8f && !shores;x+=.002f) {
                    var left=P(x,y); var right=P(x+.01f,y);
                    if(water.AllowsConnector(left,left) && water.AllowsConnector(right,right) && !water.AllowsConnector(left,right)) {
                        shoreA=left; shoreB=right; shores=true;
                    }
                }
            Check(shores,"Bridge fixture spans mapped water between dry banks");
            LoadFixture(router,new[]{shoreA,shoreB},new[]{new[]{0,1}},true);
            Check(router.FindRoute(shoreA,shoreB).Success,"Explicit classified bridge geometry is preserved across water");
            PointF deck=default(PointF);bool deckFound=false;
            for(int i=1;i<100;i++) {
                PointF point=P(shoreA.X+(shoreB.X-shoreA.X)*i/100,shoreA.Y+(shoreB.Y-shoreA.Y)*i/100);
                if(!water.AllowsConnector(point,point)) { deck=point;deckFound=true;break; }
            }
            Check(deckFound && router.FindRoute(deck,shoreB).Success,"Navigation starts on a bridge above a mapped water pixel");
            Check(!router.FindRoute(P(deck.X,deck.Y+.002f),shoreB).Success,"Bridge deck exemption cannot snap a distant swimmer onto a bridge");
            Check(!new RoutingWaterMask().AllowsConnector(a,a),"Missing water coverage fails closed");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
