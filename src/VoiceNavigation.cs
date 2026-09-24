using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal static class VoicePacks {
        internal const string DefaultName="Lyan (Female US)";
        internal const string RecalculatingClip="13_recalculating.mp3";
        internal static bool IsClip(string name) { return Clips.Contains(name) || name==RecalculatingClip; }
        internal static readonly string[] Clips={"01_500_meters.mp3","02_250_meters.mp3","03_100_meters.mp3","04_50_meters.mp3",
            "05_continue_straight.mp3","06_turn_left.mp3","07_turn_right.mp3","08_keep_left.mp3","09_keep_right.mp3",
            "10_make_a_u_turn.mp3","11_in.mp3","12_you_have_arrived.mp3"};
        internal static string[] Discover(string root) {
            if(!Directory.Exists(root)) return new string[0];
            return Directory.GetDirectories(root).Where(dir=>Clips.All(file=>File.Exists(Path.Combine(dir,file))))
                .Select(Path.GetFileName).OrderBy(name=>name,StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        internal static void InstallBundled(string root) {
            Assembly assembly=typeof(VoicePacks).Assembly;
            foreach(string resource in assembly.GetManifestResourceNames()) {
                if(!resource.StartsWith("voice-navigation/",StringComparison.Ordinal)) continue;
                string[] parts=resource.Split('/');
                if(parts.Length!=3 || parts[1]!=Path.GetFileName(parts[1]) || !IsClip(parts[2])) continue;
                string directory=Path.Combine(root,parts[1]); Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,parts[2]);
                if(File.Exists(path)) continue;
                using(Stream source=assembly.GetManifestResourceStream(resource))
                using(FileStream destination=File.Create(path)) source.CopyTo(destination);
            }
        }
    }

    // MCI can block while opening an MP3. Keep every audio call off the rendering/input thread.
    internal sealed class VoicePlayer:IDisposable {
        [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern int mciSendString(string command,StringBuilder result,int size,IntPtr callback);
        readonly string alias="scumvoice"+Guid.NewGuid().ToString("N");
        readonly object gate=new object();
        readonly System.Threading.AutoResetEvent wake=new System.Threading.AutoResetEvent(false);
        readonly System.Threading.Thread worker;
        string[] pending;
        int generation,pendingVolume;
        bool disposed;
        volatile bool busy;
        volatile string lastError;
        internal int Volume=80;
        internal string LastError { get { return lastError; } }
        internal bool Busy { get { return busy; } }
        internal bool WaitForExit(int milliseconds) { return worker.Join(milliseconds); }
        internal VoicePlayer() {
            worker=new System.Threading.Thread(Run) { IsBackground=true,Name="MiniMap voice playback" };
            worker.SetApartmentState(System.Threading.ApartmentState.STA);
            worker.Start();
        }
        void Command(string command) {
            int error=mciSendString(command,null,0,IntPtr.Zero);
            if(error!=0) throw new InvalidOperationException("Audio playback error "+error+".");
        }
        bool Cancelled(int version) { lock(gate) return disposed || generation!=version; }
        internal void Speak(string directory,IEnumerable<string> clips) {
            string[] names=clips.ToArray();
            if(names.Any(name=>!VoicePacks.IsClip(name))) return;
            string[] paths=names.Select(name=>Path.Combine(directory,name)).ToArray();
            lock(gate) {
                if(disposed) return;
                generation++; pending=paths; pendingVolume=Volume; lastError=null; busy=paths.Length>0;
                wake.Set();
            }
        }
        void Run() {
            Application.OleRequired();
            try {
                while(true) {
                    wake.WaitOne();
                    string[] files; int version,volume;
                    lock(gate) {
                        if(disposed) break;
                        files=pending; pending=null; version=generation; volume=pendingVolume;
                    }
                    if(files==null) continue;
                    try {
                        foreach(string path in files) {
                            if(Cancelled(version)) break;
                            bool open=false;
                            try {
                                if(!File.Exists(path)) throw new FileNotFoundException("Voice clip missing.",path);
                                Command("open \""+path+"\" type mpegvideo alias "+alias); open=true;
                                if(Cancelled(version)) break;
                                Command("setaudio "+alias+" volume to "+(Math.Max(0,Math.Min(100,volume))*10));
                                Command("play "+alias);
                                DateTime until=DateTime.UtcNow.AddSeconds(20);
                                while(!Cancelled(version)) {
                                    Application.DoEvents(); // Pump only the audio thread's native driver messages.
                                    StringBuilder mode=new StringBuilder(32);
                                    if(mciSendString("status "+alias+" mode",mode,mode.Capacity,IntPtr.Zero)!=0)
                                        throw new InvalidOperationException("Voice playback status failed.");
                                    if(mode.ToString().Equals("stopped",StringComparison.OrdinalIgnoreCase)) break;
                                    if(DateTime.UtcNow>until) throw new TimeoutException("Voice clip timed out.");
                                    // Do not consume the request signal here; the next outer iteration owns it.
                                    System.Threading.Thread.Sleep(20);
                                }
                            } finally { if(open) mciSendString("close "+alias,null,0,IntPtr.Zero); }
                        }
                    } catch(Exception ex) {
                        if(!Cancelled(version)) { lastError=ex.Message; Program.LogException("VoiceNavigation",ex); }
                    } finally { lock(gate) { if(generation==version) busy=false; } }
                }
            } finally { wake.Dispose(); }
        }
        internal void Stop() {
            lock(gate) { if(disposed) return; generation++; pending=null; busy=false; wake.Set(); }
        }
        public void Dispose() {
            lock(gate) { if(disposed) return; disposed=true; generation++; pending=null; busy=false; wake.Set(); }
        }
    }
    internal sealed class VoiceCue {
        internal PointF Point;
        internal double Distance;
        internal string Action;
    }
    internal sealed class VoiceNavigator {
        sealed class Spoken { internal PointF Point; internal string Action; internal int Stage; }
        readonly List<Spoken> spoken=new List<Spoken>();
        DateTime lastSpoken=DateTime.MinValue,lastWrongWarning=DateTime.MinValue;
        RoadRoute route;
        PointF[] path;
        double[] distances;
        int junctionOffset;
        int deviationSamples;
        PointF deviationPoint;
        DateTime deviationSince=DateTime.MinValue;
        bool outsideCorridor;
        PointF previousPlayer;
        bool hasPrevious;
        DateTime previousTime;
        double backwardsMetres;
        VoiceCue previousCue;
        internal VoiceCue CurrentCue { get; private set; }
        internal bool NeedsReroute { get; private set; }
        internal bool WrongWay { get; private set; }
        internal double Speed { get; private set; }
        double nearest,along;
        int nearestSegment;
        double previousAlong;
        PointF projectedPlayer;
        bool hasProjection;
        internal void Reset() {
            spoken.Clear(); route=null; path=null; lastSpoken=lastWrongWarning=DateTime.MinValue;
            hasPrevious=false; backwardsMetres=0; previousCue=CurrentCue=null;
            NeedsReroute=WrongWay=false; Speed=0;
            deviationSamples=0; deviationSince=DateTime.MinValue; outsideCorridor=false;
            hasProjection=false;
        }
        internal void RouteReplaced() {
            route=null; previousCue=CurrentCue=null; spoken.Clear(); lastSpoken=DateTime.MinValue;
            hasPrevious=false; backwardsMetres=0; WrongWay=NeedsReroute=false;
            deviationSamples=0; deviationSince=DateTime.MinValue; outsideCorridor=false;
            hasProjection=false;
        }
        static double SegmentProjection(PointF p,PointF a,PointF b,out double t) {
            double dx=(b.X-a.X)*15216.18,dy=(b.Y-a.Y)*15236.18;
            double px=(p.X-a.X)*15216.18,py=(p.Y-a.Y)*15236.18;
            t=dx*dx+dy*dy<.0001?0:Math.Max(0,Math.Min(1,(px*dx+py*dy)/(dx*dx+dy*dy)));
            return Math.Sqrt((px-dx*t)*(px-dx*t)+(py-dy*t)*(py-dy*t));
        }
        internal static string Turn(double degrees) {
            if(Math.Abs(degrees)<15) return null;
            if(Math.Abs(degrees)>=150) return VoicePacks.Clips[9];
            // A branch decision is a turn. Keep-left/right requires lane/fork metadata we do not have.
            return VoicePacks.Clips[degrees<0?5:6];
        }
        PointF At(double distance) {
            if(distance<=0) return path[0];
            for(int i=1;i<path.Length;i++) if(distances[i]>=distance) {
                double length=distances[i]-distances[i-1];
                double t=length<.001?0:(distance-distances[i-1])/length;
                return new PointF((float)(path[i-1].X+(path[i].X-path[i-1].X)*t),(float)(path[i-1].Y+(path[i].Y-path[i-1].Y)*t));
            }
            return path[path.Length-1];
        }
        internal VoiceCue NextCue(RoadRoute current,PointF player) {
            NeedsReroute=false; CurrentCue=null; outsideCorridor=false;
            if(current==null || !current.Success || current.HasWaterTransit || current.Polyline==null || current.Polyline.Length<2) return null;
            if(!Object.ReferenceEquals(route,current)) {
                route=current;
                hasProjection=false;
                var complete=new List<PointF>(); junctionOffset=0;
                // Polyline contains the road section; the renderer also draws these connectors.
                if(current.EntryDistanceMeters>15 && RoadRouter.DistanceMeters(current.PlayerPoint,current.Polyline[0])>1) {
                    complete.Add(current.PlayerPoint); junctionOffset=1;
                }
                complete.AddRange(current.Polyline);
                if(current.ExitDistanceMeters>15 && RoadRouter.DistanceMeters(current.TargetPoint,current.Polyline[current.Polyline.Length-1])>1)
                    complete.Add(current.TargetPoint);
                path=complete.ToArray();
                distances=new double[path.Length];
                for(int i=1;i<path.Length;i++) distances[i]=distances[i-1]+RoadRouter.DistanceMeters(path[i-1],path[i]);
            }
            nearest=double.MaxValue; along=0; nearestSegment=1;
            double bestContinuity=double.MaxValue;
            bool continuous=hasProjection && RoadRouter.DistanceMeters(projectedPlayer,player)<100;
            double closest=double.MaxValue;
            for(int i=1;i<path.Length;i++) {
                double t,d=SegmentProjection(player,path[i-1],path[i],out t);
                if(d<closest) closest=d;
            }
            for(int i=1;i<path.Length;i++) {
                double t,d=SegmentProjection(player,path[i-1],path[i],out t);
                double candidateAlong=distances[i-1]+t*(distances[i]-distances[i-1]);
                double continuity=continuous?Math.Abs(candidateAlong-previousAlong):0;
                // At crossings and parallel roads, near-equal projections should stay on the
                // branch already being followed instead of jumping to another route section.
                if(d<=closest+8 && (continuity<bestContinuity || (continuity==bestContinuity && d<nearest))) {
                    nearest=d; along=candidateAlong; nearestSegment=i; bestContinuity=continuity;
                }
            }
            // Deviation is geometric: a continuity preference must not turn a valid
            // position near another part of the route into a false off-route sample.
            outsideCorridor=closest>35;
            if(outsideCorridor) return null;
            nearest=closest;
            previousAlong=along; projectedPlayer=player; hasProjection=true;
            foreach(int junction in current.JunctionIndices ?? new int[0]) {
                int i=junction+junctionOffset;
                if(i<=0 || i>=path.Length-1 || distances[i]<=along+3) continue;
                PointF before=At(distances[i]-35),after=At(distances[i]+35);
                double ax=(path[i].X-before.X)*15216.18,ay=(path[i].Y-before.Y)*15236.18;
                double bx=(after.X-path[i].X)*15216.18,by=(after.Y-path[i].Y)*15236.18;
                string action=Turn(Math.Atan2(ax*by-ay*bx,ax*bx+ay*by)*180/Math.PI);
                if(action!=null) return CurrentCue=new VoiceCue { Point=path[i],Distance=distances[i]-along,Action=action };
            }
            return CurrentCue=new VoiceCue { Point=path[path.Length-1],Distance=distances[path.Length-1]-along,Action=VoicePacks.Clips[4] };
        }
        void TrackMovement(PointF player,DateTime now,bool usableRoute) {
            if(!hasPrevious) { previousPlayer=player; previousTime=now; hasPrevious=true; return; }
            double travel=RoadRouter.DistanceMeters(previousPlayer,player),dt=(now-previousTime).TotalSeconds;
            if(dt>5 || travel>150) { previousPlayer=player; previousTime=now; backwardsMetres=0; WrongWay=false; Speed=0; return; }
            if(travel<3 || dt<=0) return;
            Speed=Math.Min(100,travel/dt);
            if(usableRoute && nearest<35) {
                PointF a=path[nearestSegment-1],b=path[nearestSegment];
                double mx=(player.X-previousPlayer.X)*15216.18,my=(player.Y-previousPlayer.Y)*15236.18;
                double dx=(b.X-a.X)*15216.18,dy=(b.Y-a.Y)*15236.18;
                double length=Math.Sqrt(dx*dx+dy*dy);
                double alignment=length<.001?0:(mx*dx+my*dy)/(travel*length);
                backwardsMetres=alignment<-.6?backwardsMetres+travel:0;
                WrongWay=backwardsMetres>=10;
            } else { WrongWay=false; backwardsMetres=0; }
            previousPlayer=player; previousTime=now;
        }
        internal string[] Update(RoadRoute current,PointF player,DateTime now,bool busy=false) {
            VoiceCue cue=NextCue(current,player);
            if(current==null || !current.Success || current.HasWaterTransit || current.Polyline==null || current.Polyline.Length<2) {
                outsideCorridor=false; WrongWay=false; backwardsMetres=0;
            }
            if(!outsideCorridor) { deviationSamples=0; deviationSince=DateTime.MinValue; }
            else if(deviationSamples==0 || RoadRouter.DistanceMeters(deviationPoint,player)>=2) {
                if(deviationSamples==0) deviationSince=now;
                deviationPoint=player; deviationSamples++;
            }
            NeedsReroute=outsideCorridor && deviationSamples>=3 && (now-deviationSince).TotalSeconds>=.5;
            TrackMovement(player,now,current!=null && current.Success && !current.HasWaterTransit && current.Polyline!=null && current.Polyline.Length>1);
            if(WrongWay) {
                NeedsReroute=true;
                if((now-lastWrongWarning).TotalSeconds>=12) {
                    lastWrongWarning=now; lastSpoken=now; spoken.Clear(); previousCue=null;
                    return new[]{VoicePacks.Clips[9]};
                }
                return null;
            }
            if(cue==null) return null;
            double leadDistance=Math.Max(0,cue.Distance-Speed*1.5);
            int stage=-1;
            if(cue.Action!=VoicePacks.Clips[4] && cue.Distance<=Math.Max(25,Speed*3)) stage=0;
            else foreach(int threshold in new[]{50,100,250,500}) {
                bool crossed=previousCue!=null && previousCue.Action==cue.Action && RoadRouter.DistanceMeters(previousCue.Point,cue.Point)<30
                    && previousCue.Distance-Speed*1.5>threshold && leadDistance<=threshold && leadDistance>=threshold*.65;
                if(Math.Abs(leadDistance-threshold)<=Math.Max(12,threshold*.12) || crossed) { stage=threshold; break; }
            }
            if(!busy) previousCue=cue;
            if(cue.Action==VoicePacks.Clips[4] || cue.Distance>600) {
                if(busy || (now-lastSpoken).TotalSeconds<90) return null;
                lastSpoken=now; return new[]{VoicePacks.Clips[4]};
            }
            if(stage<0 || (busy && stage!=0) || (now-lastSpoken).TotalSeconds<(stage==0?.5:1.2)) return null;
            if(spoken.Any(item=>item.Action==cue.Action && item.Stage<=stage && RoadRouter.DistanceMeters(item.Point,cue.Point)<30)) return null;
            spoken.Add(new Spoken { Point=cue.Point,Action=cue.Action,Stage=stage });
            if(spoken.Count>256) spoken.RemoveAt(0);
            lastSpoken=now;
            if(stage==0) return new[]{cue.Action};
            int clip=stage==500?0:stage==250?1:stage==100?2:3;
            return new[]{VoicePacks.Clips[10],VoicePacks.Clips[clip],cue.Action};
        }
    }
}
