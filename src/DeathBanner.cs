using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Recognise the death-screen layout, never the words: a prominent red heading
    // followed by three aligned, dark respawn rows with neutral-coloured labels.
    // Pixels are processed locally and discarded; no OCR, saved images or calibration.
    internal static class DeathBannerDetector {
        sealed class Pixels {
            internal readonly int Width,Height;
            internal readonly byte[] Data;
            internal Pixels(Bitmap image) {
                Width=image.Width; Height=image.Height; Data=new byte[Width*Height*4];
                BitmapData bits=image.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try {
                    for(int y=0;y<Height;y++) Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),Data,y*Width*4,Width*4);
                } finally { image.UnlockBits(bits); }
            }
            internal int Gray(int x,int y) { int i=(y*Width+x)*4; return (Data[i]+Data[i+1]+Data[i+2])/3; }
            internal bool Neutral(int x,int y,int min,int max) {
                int i=(y*Width+x)*4,b=Data[i],g=Data[i+1],r=Data[i+2];
                return Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<=10 && (r+g+b)/3>=min && (r+g+b)/3<=max;
            }
            internal bool Red(int x,int y) {
                int i=(y*Width+x)*4,r=Data[i+2];
                return r>=140 && Data[i+1]<r*0.45 && Data[i]<r*0.45;
            }
        }
        internal static bool Matches(Bitmap image) {
            string diagnostic;
            return Matches(image,out diagnostic);
        }
        internal static bool Matches(Bitmap image,out string diagnostic) {
            diagnostic="invalid-image";
            if(image==null || image.Width<480 || image.Height<270) return false;
            int width=Math.Min(960,image.Width),height=(int)Math.Round(image.Height*(double)width/image.Width);
            if(height<180 || height>1200) return false;
            diagnostic="no-red-heading";
            using(var small=new Bitmap(width,height)) {
                using(var g=Graphics.FromImage(small)) {
                    g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                    g.DrawImage(image,new Rectangle(0,0,width,height));
                }
                var pixels=new Pixels(small);
                int left=width/10,right=width*9/10,start=-1,last=-1;
                // A right-side map may contain isolated red marks. Group separate
                // horizontal text clusters rather than treating every red pixel as a heading.
                for(int y=height/25;y<=height*48/100+3;y++) {
                    int count=0;
                    if(y<height*48/100) for(int x=left;x<right;x++) if(pixels.Red(x,y)) count++;
                    if(count>=8) { if(start<0) start=y; last=y; }
                    else if(start>=0 && y-last>2) {
                        if(HeadingHasRespawnRows(pixels,start,last,left,right,ref diagnostic)) { diagnostic="matched-layout";return true; }
                        start=-1;
                    }
                }
            }
            return false;
        }
        static bool HeadingHasRespawnRows(Pixels p,int top,int bottom,int left,int right,ref string diagnostic) {
            int h=bottom-top+1;
            if(diagnostic=="no-red-heading")diagnostic="red-band-size";
            if(h<p.Height*0.025 || h>p.Height*0.12) return false;
            int first=-1,last=-1,ink=0;
            for(int x=left;x<=right+Math.Max(3,h);x++) {
                int count=0;
                if(x<right) for(int y=top;y<=bottom;y++) if(p.Red(x,y)) count++;
                if(count>0) { if(first<0) first=x; last=x; ink+=count; }
                else if(first>=0 && x-last>Math.Max(3,h*3/4)) {
                    int w=last-first+1;
                    double coverage=(double)ink/(w*h),aspect=(double)w/h;
                    if(w>=p.Width*0.08 && w<=p.Width*0.65 && aspect>=1.3 && aspect<=24
                        && coverage>=0.12 && coverage<=0.78) {
                        diagnostic="heading-without-three-respawn-rows";
                        if(HasRespawnRows(p,new Rectangle(first,top,w,h))) return true;
                    }
                    first=-1; ink=0;
                }
            }
            return false;
        }
        static bool HasRespawnRows(Pixels p,Rectangle heading) {
            int center=heading.Left+heading.Width/2;
            // The title may stay centred while respawn choices move left for a map.
            int minX=p.Width/12,maxX=p.Width*11/12;
            int minY=heading.Bottom+heading.Height/2;
            int maxY=Math.Min(p.Height*85/100,heading.Bottom+heading.Height*7);
            var rows=new List<Rectangle>();
            for(int y=minY;y<maxY;y+=2) {
                int x=minX;
                while(x<maxX) {
                    if(!p.Neutral(x,y,7,85)) { x++; continue; }
                    int left=x,gray=p.Gray(x,y);
                    while(x<maxX && p.Neutral(x,y,7,85) && Math.Abs(p.Gray(x,y)-gray)<=8) x++;
                    int width=x-left;
                    if(width<p.Width*0.12 || width>p.Width*0.40 || Math.Abs(left+width/2-center)>p.Width*0.28) continue;
                    int a=left+2,b=x-2,top=y,bottom=y;
                    if(a>=b) continue;
                    gray=(p.Gray(a,y)+p.Gray(b,y))/2;
                    while(top>minY && SameRowEdge(p,a,b,top-1,gray)) top--;
                    while(bottom+1<maxY && SameRowEdge(p,a,b,bottom+1,gray)) bottom++;
                    int height=bottom-top+1;
                    if(height<p.Height*0.022 || height>p.Height*0.09 || height<heading.Height*0.30 || height>heading.Height*1.5) continue;
                    var row=new Rectangle(left,top,width,height);
                    bool duplicate=false;
                    foreach(Rectangle existing in rows) if(Math.Abs(existing.Top-top)<=2 && Math.Abs(existing.Left-left)<=3) { duplicate=true; break; }
                    if(!duplicate && HasLabelInk(p,row,gray)) rows.Add(row);
                    if(rows.Count>24) return false;
                }
            }
            rows.Sort((a,b)=>a.Top.CompareTo(b.Top));
            foreach(Rectangle first in rows) {
                if(first.Top-heading.Bottom>heading.Height*4.5) continue;
                foreach(Rectangle second in rows) if(NextRespawnRow(p,first,second))
                    foreach(Rectangle third in rows) if(NextRespawnRow(p,second,third)) return true;
            }
            return false;
        }
        static bool SameRowEdge(Pixels p,int left,int right,int y,int gray) {
            return p.Neutral(left,y,7,85) && p.Neutral(right,y,7,85)
                && Math.Abs(p.Gray(left,y)-gray)<=8 && Math.Abs(p.Gray(right,y)-gray)<=8;
        }
        static bool HasLabelInk(Pixels p,Rectangle row,int background) {
            int ink=0,area=0;
            for(int y=row.Top+row.Height/5;y<row.Bottom-row.Height/5;y++)
                for(int x=row.Left+row.Width/12;x<row.Right-row.Width/12;x++) {
                    area++;
                    if(p.Neutral(x,y,background+15,255)) ink++;
                }
            return ink>=6 && ink>=area*0.01 && ink<=area*0.45;
        }
        static bool NextRespawnRow(Pixels p,Rectangle first,Rectangle second) {
            int gap=second.Top-first.Bottom;
            return gap>=1 && gap<=Math.Max(6,p.Height*0.02)
                && Math.Abs(first.Left-second.Left)<=Math.Max(3,p.Width*0.006)
                && Math.Abs(first.Right-second.Right)<=Math.Max(3,p.Width*0.006)
                && Math.Abs(first.Height-second.Height)<=Math.Max(3,first.Height*0.20);
        }
        internal static void SelfTest() {
            var state=new DeathBannerState();
            if(!state.Observe(true)) throw new Exception("A complete death layout must save immediately.");
            for(int i=0;i<10;i++) if(state.Observe(true)) throw new Exception("Death screen repeated while visible.");
            state.Observe(false); state.Observe(true);
            if(state.Observe(true)) throw new Exception("Death screen flicker rearmed a marker.");
            for(int i=0;i<13;i++)state.Observe(false);
            if(state.Observe(true)) throw new Exception("A brief detection dropout rearmed a marker.");
            for(int i=0;i<14;i++)state.Observe(false);
            if(!state.Observe(true)) throw new Exception("New death screen failed to rearm.");
            state.Interrupt();
            if(state.Observe(true)) throw new Exception("Focus interruption rearmed a marker.");
        }
    }
    internal sealed class DeathBannerState {
        int matches,misses;
        bool latched;
        internal bool IsLatched { get { return latched; } }
        internal bool HasCandidate { get { return matches>0 && !latched; } }
        internal void Interrupt() { matches=0; misses=0; }
        internal bool Observe(bool visible) {
            if(!visible) { matches=0; if(++misses>=14) latched=false; return false; }
            misses=0;
            if(latched) return false;
            latched=true; matches=0;
            return true;
        }
    }
    internal enum DeathMarkerOutcome { None, Saved, MissingPosition, SaveFailed }
    public sealed partial class MapWindow {
        readonly HashSet<MapZone> departedDeathMarkers=new HashSet<MapZone>();
        DateTime nextDeathLabelRefresh;
        internal void ClearReachedDeathMarkers(PointF currentPoint) {
            departedDeathMarkers.RemoveWhere(z=>!zones.Contains(z));
            var reached=new List<MapZone>();
            foreach(MapZone marker in zones) {
                if(!marker.IsDeathMarker)continue;
                double distance=DestinationSearch.Metres(currentPoint,marker.Centroid);
                if(distance>=50)departedDeathMarkers.Add(marker);
                else if(distance<=25 && departedDeathMarkers.Contains(marker))reached.Add(marker);
            }
            if(reached.Count==0)return;
            var candidate=new List<MapZone>(zones);
            candidate.RemoveAll(z=>reached.Contains(z));
            if(!TryCommitZonesCore(candidate,null,true))return;
            if(reached.Contains(searchTarget)) {
                VoiceArrived(); searchTarget=null; activeRoute=null; routeGeneration++; lastRouteTarget=null;
            }
            foreach(MapZone marker in reached)departedDeathMarkers.Remove(marker);
            SettingsChanged();
        }
        void RefreshDeathLabels(DateTime now) {
            if(now<nextDeathLabelRefresh)return;
            nextDeathLabelRefresh=now.AddSeconds(10);
            foreach(MapZone marker in zones)if(marker.IsDeathMarker) { terrainKey=null;lastFrameKey=null;locationDescriptionRevision++;InvalidateFullMapSidebar();break; }
        }
        bool automaticDeathMarkers=true,deathProbeBusy;
        int deathProbeGeneration;
        DateTime nextDeathProbe,lastDeathProbeCompleted;
        readonly DeathBannerState deathBannerState=new DeathBannerState();
        Position firstDeathBannerPosition;
        DateTime firstDeathBannerSampleTime;
        string lastDeathDiagnostic;
        string lastDeathDetectorDiagnostic;
        readonly object deathDiagnosticSync=new object();
        void LogDeathDiagnostic(string message) {
            if(diagnosticMode || message==lastDeathDiagnostic) return;
            lastDeathDiagnostic=message;
            string line=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" version="+VersionString+" "+message+Environment.NewLine;
            string path=Path.Combine(dataFolder,"death-detection.log");
            Task.Run(()=> {
                try { lock(deathDiagnosticSync) { if(!File.Exists(path) || new FileInfo(path).Length<262144) File.AppendAllText(path,line); } }
                catch(IOException) {} catch(UnauthorizedAccessException) {}
            });
        }
        internal Position CaptureDeathMarkerPosition() {
            // Death/menu transitions can stop Copy Location for longer than 30 seconds.
            // Keep the last known session location; its age affects precision, not availability.
            return position==null?null:new Position { X=position.X,Y=position.Y,Z=position.Z,Yaw=position.Yaw };
        }
        internal DeathMarkerOutcome ApplyDeathScreenSample(bool? visible,Position capturedPosition,DateTime sampleTime) {
            if(!visible.HasValue) {
                deathBannerState.Interrupt(); firstDeathBannerPosition=null;
                LogDeathDiagnostic("capture=unavailable");
                return DeathMarkerOutcome.None;
            }
            if(visible.Value && !deathBannerState.HasCandidate && !deathBannerState.IsLatched) {
                firstDeathBannerPosition=capturedPosition;
                firstDeathBannerSampleTime=sampleTime;
                LogDeathDiagnostic("capture=death-candidate");
            }
            bool died=deathBannerState.Observe(visible.Value);
            if(!died) {
                if(!visible.Value) { firstDeathBannerPosition=null; LogDeathDiagnostic("capture=no-death-screen"); }
                return DeathMarkerOutcome.None;
            }
            Position deathPosition=firstDeathBannerPosition;
            firstDeathBannerPosition=null;
            if(deathPosition==null) {
                LogDeathDiagnostic("marker=skipped reason=no-known-location");
                return DeathMarkerOutcome.MissingPosition;
            }
            bool saved=SaveDeathMarker(deathPosition,true);
            double age=Math.Max(0,(DateTime.UtcNow-firstDeathBannerSampleTime).TotalSeconds);
            LogDeathDiagnostic("marker="+(saved?"saved":"failed")+" location_age_seconds="+age.ToString("F1",CultureInfo.InvariantCulture));
            return saved?DeathMarkerOutcome.Saved:DeathMarkerOutcome.SaveFailed;
        }
        void SetAutomaticDeathMarkers(bool enabled) {
            automaticDeathMarkers=enabled;
            deathProbeGeneration++;
            deathBannerState.Interrupt(); firstDeathBannerPosition=null;
        }
        bool DeathProbeAllowed(bool gameFocused) {
            return automaticDeathMarkers && gameFocused && !fullMapActive && !SettingsVisible && !panelOpening && !searchOpen && !shortcutCaptureOpen && !adminCommandBusy;
        }
        void PollDeathBanner(DateTime now,bool gameFocused) {
            if(!DeathProbeAllowed(gameFocused)) {
                string reason=!automaticDeathMarkers?"disabled":!gameFocused?"game-not-foreground":fullMapActive?"expanded-map":SettingsVisible?"settings":adminCommandBusy?"admin-command":"dialog";
                LogDeathDiagnostic("probe=blocked reason="+reason);
                deathProbeGeneration++;
                deathBannerState.Interrupt(); firstDeathBannerPosition=null;
                return;
            }
            if(deathProbeBusy || now<nextDeathProbe) return;
            if(lastDeathProbeCompleted!=DateTime.MinValue && (now-lastDeathProbeCompleted).TotalSeconds>3) {
                deathBannerState.Interrupt(); firstDeathBannerPosition=null;
            }
            nextDeathProbe=now.AddMilliseconds(750);
            StartDeathBannerProbe();
        }
        void StartDeathBannerProbe() {
            if(deathProbeBusy || closing || IsDisposed || !IsHandleCreated || !DeathProbeAllowed(Native.GameFocused())) return;
            // Freeze the first matched frame's last known position before asynchronous capture.
            Position capturedPosition=CaptureDeathMarkerPosition();
            DateTime capturedSampleTime=updated;
            IntPtr game=Native.GetForegroundWindow();
            int generation=deathProbeGeneration;
            DateTime started=DateTime.UtcNow;
            deathProbeBusy=true;
            Task.Run(()=> {
                bool? visible=null;
                string diagnostic="capture-unavailable";
                Exception failure=null;
                try { visible=Native.CaptureDeathBanner(game,out diagnostic); } catch(Exception ex) { failure=ex; }
                try {
                    if(closing || IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(()=> {
                        deathProbeBusy=false;
                        if(closing || IsDisposed || generation!=deathProbeGeneration || Native.GetForegroundWindow()!=game || !DeathProbeAllowed(Native.GameFocused())) return;
                        lastDeathProbeCompleted=DateTime.UtcNow;
                        if(failure!=null) { Program.LogException("DetectDeathScreen",failure); visible=null; }
                        if(!visible.HasValue || (DateTime.UtcNow-started).TotalSeconds>3) {
                            ApplyDeathScreenSample(null,null,capturedSampleTime); return;
                        }
                        if(diagnostic!=lastDeathDetectorDiagnostic) {
                            lastDeathDetectorDiagnostic=diagnostic;
                            LogDeathDiagnostic("detector="+diagnostic);
                        }
                        ApplyDeathScreenSample(visible,capturedPosition,capturedSampleTime);
                    }));
                } catch(InvalidOperationException) { /* Window disposed during capture. */ }
            });
        }
    }
    static partial class Native {
        internal static bool? CaptureDeathBanner(IntPtr gameWindow) {
            string diagnostic;
            return CaptureDeathBanner(gameWindow,out diagnostic);
        }
        internal static bool? CaptureDeathBanner(IntPtr gameWindow,out string diagnostic) {
            diagnostic="capture-unavailable";
            WindowRect rect;
            if(gameWindow==IntPtr.Zero || GetForegroundWindow()!=gameWindow || !IsGameWindow(gameWindow) || !GetWindowRect(gameWindow,out rect)) return null;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
            if(width<640 || height<360 || width>16384 || height>8640) return null;
            // Include the respawn choices beneath the heading, but exclude screen edges.
            Rectangle region=new Rectangle(rect.Left+width/10,rect.Top,width*8/10,height*8/10);
            if(!SystemInformation.VirtualScreen.Contains(region) || (long)region.Width*region.Height>24L*1024*1024) return null;
            try {
                using(var image=new Bitmap(region.Width,region.Height)) {
                    using(var g=Graphics.FromImage(image)) {
                        if(GetForegroundWindow()!=gameWindow) return null;
                        g.CopyFromScreen(region.Location,Point.Empty,region.Size,CopyPixelOperation.SourceCopy);
                    }
                    if(GetForegroundWindow()!=gameWindow) return null;
                    bool result=DeathBannerDetector.Matches(image,out diagnostic);
                    diagnostic+=" region="+region.Width+"x"+region.Height;
                    return result;
                }
            } catch(System.ComponentModel.Win32Exception) { return null; }
            catch(ExternalException) { return null; }
            catch(ArgumentException) { return null; }
        }
    }
}
