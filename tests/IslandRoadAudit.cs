using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using ScumMiniMap;

public static class IslandRoadAudit {
    public static int Main(string[] args) {
        var records=new List<object>();
        var starts=new[]{new PointF(.40960548f,.39800242f),new PointF(.50006073f,.40556735f),new PointF(.57332342f,.39900682f)};
        var targets=new[]{new PointF(.10288965f,.89523792f),new PointF(.30588488f,.90629353f),new PointF(.50756363f,.91399520f),new PointF(.69947680f,.90420452f),new PointF(.89309654f,.90364377f)};
        int after=0;
        for(int dataset=0;dataset<2;dataset++) {
            using(var file=File.OpenRead(args[dataset]))
            using(var zip=new GZipStream(file,CompressionMode.Decompress))
            using(var reader=new BinaryReader(zip))RoadRouter.Instance.LoadBinary(reader);
            int successful=0;
            for(int start=0;start<starts.Length;start++)for(int target=0;target<targets.Length;target++) {
                RoadRoute route=RoadRouter.Instance.FindRoute(starts[start],targets[target]);
                if(route.Success)successful++;
                records.Add(new { dataset=dataset==0?"before":"after",start=start,island=4-target,
                    success=route.Success,distanceMeters=route.TotalDistanceMeters,
                    entryMeters=route.EntryDistanceMeters,exitMeters=route.ExitDistanceMeters,
                    points=route.Polyline==null?new float[0][]:route.Polyline.Select(p=>new[]{p.X,p.Y}).ToArray() });
            }
            Console.WriteLine((dataset==0?"Before":"After")+": "+successful+"/15 northern-B2-to-Z routes");
            if(dataset==1)after=successful;
        }
        var serializer=new JavaScriptSerializer { MaxJsonLength=16000000 };
        File.WriteAllText(args[2],serializer.Serialize(records));
        return after==15?0:1;
    }
}
