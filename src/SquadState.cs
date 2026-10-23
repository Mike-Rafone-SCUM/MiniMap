using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScumMiniMap {
    // Optional marker identities keep the encrypted v3 transport interoperable.
    // Older clients show ordinary named markers; newer clients also understand
    // ping expiry and route ordering. No new relay messages or services are used.
    internal sealed class SquadState {
        internal const int PingSeconds=60,MaximumStops=16;
        readonly Func<DateTime> clock;
        readonly Dictionary<string,DateTime> firstPings=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        readonly List<SharedMapMarker> pings=new List<SharedMapMarker>();
        readonly List<SharedMapMarker> stops=new List<SharedMapMarker>();
        DateTime lastPing=DateTime.MinValue;
        string route=Guid.NewGuid().ToString("N");
        internal bool Shared { get;private set; }
        internal SquadState(Func<DateTime> clock=null) { this.clock=clock??(()=>DateTime.UtcNow); }
        internal static bool IsPing(SharedMapMarker m) { return m!=null&&m.Id!=null&&m.Id.StartsWith("50494E47",StringComparison.Ordinal)&&m.Id.Length==64; }
        internal static bool IsStop(SharedMapMarker m) { return m!=null&&m.Id!=null&&m.Id.Length==64&&(m.Id.StartsWith("524F5554",StringComparison.Ordinal)||m.Id.StartsWith("534F4C4F",StringComparison.Ordinal)||m.Id.StartsWith("53485244",StringComparison.Ordinal))&&StopIndex(m)<MaximumStops; }
        internal static bool IsShared(SharedMapMarker m) { return IsStop(m)&&!m.Id.StartsWith("534F4C4F",StringComparison.Ordinal); }
        string StopId(int index,string identity=null) { return (Shared?"53485244":"534F4C4F")+route+index.ToString("X2")+(identity??Guid.NewGuid().ToString("N").Substring(0,22)); }
        internal void SetShared(bool shared) { Shared=shared;for(int i=0;i<stops.Count;i++)stops[i].Id=StopId(i,stops[i].Id.Substring(42)); }
        internal static int StopIndex(SharedMapMarker m) { int value;return m!=null&&m.Id!=null&&m.Id.Length==64&&int.TryParse(m.Id.Substring(40,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)?value:int.MaxValue; }
        internal static string RouteKey(SharedMapMarker m) { return (m.OwnerId??"")+":"+m.Id.Substring(8,32); }
        internal bool Ping(int kind,string name,double x,double y) {
            if(kind<0||kind>2||clock()-lastPing<TimeSpan.FromSeconds(2))return false;
            var m=new SharedMapMarker { Id="50494E47"+kind.ToString("X2")+Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N").Substring(0,22),Name=name,X=x,Y=y };
            if(!SharedMapMarker.Valid(m))return false;lastPing=clock();pings.RemoveAll(p=>!Alive(p,"local"));
            if(pings.Count>=8)pings.RemoveAt(0);pings.Add(m);firstPings["local:"+m.Id]=clock();return true;
        }
        bool Alive(SharedMapMarker m,string owner) {
            string key=owner+":"+m.Id;DateTime first;
            if(!firstPings.TryGetValue(key,out first)) { if(firstPings.Count>=2048)return false;firstPings[key]=first=clock(); }
            return clock()-first<TimeSpan.FromSeconds(PingSeconds)&&clock()>=first;
        }
        internal SharedMapMarker[] Receive(IEnumerable<SharedMapMarker> markers) {
            foreach(var key in firstPings.Where(p=>clock()-p.Value>TimeSpan.FromMinutes(5)).Select(p=>p.Key).ToArray())firstPings.Remove(key);
            return markers.Where(SharedMapMarker.Valid).Where(m=>!IsPing(m)||Alive(m,m.OwnerId??"")).Select(m=>m.Copy()).ToArray();
        }
        internal SharedMapMarker[] Local() {
            pings.RemoveAll(p=>!Alive(p,"local"));return pings.Concat(stops).Select(m=>m.Copy()).ToArray();
        }
        internal SharedMapMarker[] Stops { get { return stops.Select(m=>m.Copy()).ToArray(); } }
        internal bool AddStop(string name,double x,double y) {
            if(stops.Count>=MaximumStops)return false;
            var m=new SharedMapMarker { Id=StopId(stops.Count),Name=name,X=x,Y=y };
            if(!SharedMapMarker.Valid(m))return false;stops.Add(m);return true;
        }
        internal void ClearRoute() { stops.Clear();route=Guid.NewGuid().ToString("N"); }
        internal void RemoveLast() { if(stops.Count>0)stops.RemoveAt(stops.Count-1); }
        internal bool RemoveStop(string id) {
            int index=stops.FindIndex(m=>m.Id==id);if(index<0)return false;stops.RemoveAt(index);
            for(int i=0;i<stops.Count;i++)stops[i].Id=StopId(i,stops[i].Id.Substring(42));return true;
        }
        internal static SharedMapMarker[][] Routes(IEnumerable<SharedMapMarker> markers) {
            return markers.Where(IsStop).GroupBy(RouteKey).Select(g=>g.OrderBy(StopIndex).Take(MaximumStops).ToArray()).Where(g=>g.Select(StopIndex).SequenceEqual(Enumerable.Range(0,g.Length))).ToArray();
        }
    }
    internal sealed class MapPreset {
        internal string Name,Map,Categories,ZoneLayers,Zones;
        internal bool Grid,Waypoints,History,Labels,ShowZones,Poi,Rotate,Cluster;
        internal int Opacity;
        internal float Zoom;
        internal int TacticalOpacity;
        internal float TacticalZoom=1;
    }
    internal static class MapPresetFile {
        internal static string Recover(string file) {
            // Copy first. If either copy or atomic reset fails, the original remains recoverable.
            string backup=file+".recovery-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".bak";
            File.Copy(file,backup,false);
            Save(file,new MapPreset[0]);return backup;
        }
        internal static MapPreset[] Load(string file) {
            if(!File.Exists(file))return new MapPreset[0];if(new FileInfo(file).Length>262144)throw new InvalidDataException();
            using(var r=new BinaryReader(File.OpenRead(file),Encoding.UTF8)) {
                int version=r.ReadInt32();if(version!=1&&version!=2)throw new InvalidDataException();int count=r.ReadInt32();if(count<0||count>24)throw new InvalidDataException();var list=new List<MapPreset>();
                for(int i=0;i<count;i++) {
                    var p=new MapPreset { Name=r.ReadString(),Map=r.ReadString(),Categories=r.ReadString(),ZoneLayers=r.ReadString(),Zones=r.ReadString(),Grid=r.ReadBoolean(),Waypoints=r.ReadBoolean(),History=r.ReadBoolean(),Labels=r.ReadBoolean(),ShowZones=r.ReadBoolean(),Poi=r.ReadBoolean(),Rotate=r.ReadBoolean(),Cluster=r.ReadBoolean(),Opacity=r.ReadInt32(),Zoom=r.ReadSingle() };
                    if(version>=2) { p.TacticalOpacity=r.ReadInt32();p.TacticalZoom=r.ReadSingle(); }
                    if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>64||p.Opacity<30||p.Opacity>100||float.IsNaN(p.Zoom)||p.Zoom<1||p.Zoom>100)throw new InvalidDataException();list.Add(p);
                    if(p.TacticalOpacity<0||p.TacticalOpacity>100||float.IsNaN(p.TacticalZoom)||p.TacticalZoom<1||p.TacticalZoom>100)throw new InvalidDataException();
                }
                if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException();return list.ToArray();
            }
        }
        internal static void Save(string file,IEnumerable<MapPreset> presets) {
            var values=presets.ToArray();if(values.Length>24)throw new InvalidOperationException();Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                using(var w=new BinaryWriter(stream,Encoding.UTF8,true)) { w.Write(2);w.Write(values.Length);foreach(var p in values) { w.Write(p.Name);w.Write(p.Map);w.Write(p.Categories);w.Write(p.ZoneLayers);w.Write(p.Zones);w.Write(p.Grid);w.Write(p.Waypoints);w.Write(p.History);w.Write(p.Labels);w.Write(p.ShowZones);w.Write(p.Poi);w.Write(p.Rotate);w.Write(p.Cluster);w.Write(p.Opacity);w.Write(p.Zoom);w.Write(p.TacticalOpacity);w.Write(p.TacticalZoom); }w.Flush(); }stream.Flush(true);
            }
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
    }
}
