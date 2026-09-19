using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    public sealed partial class MapWindow {

        public void SaveOverlayPreview(string path) { OverlayBitmap().Save(path,ImageFormat.Png); }



        public void CheckSearchPreview(string output) {



            if(!diagnosticMode)throw new InvalidOperationException("Search check requires diagnostic mode.");



            Exception failure=null; bool checkedDialog=false;



            using(System.Windows.Forms.Timer check=new System.Windows.Forms.Timer { Interval=100 }) {



                check.Tick+=(sender,args)=> {



                    Form dialog=null;



                    foreach(Form open in Application.OpenForms) if(open.Text=="Search place or grid" || open.Text==Localization.Get("SearchTitle"))dialog=open;



                    if(dialog==null)return;



                    check.Stop();



                    try {



                        TextBox input=null; ListBox results=null; Button choose=null;



                        foreach(Control control in dialog.Controls) {



                            if(control is TextBox)input=(TextBox)control;



                            if(control is ListBox)results=(ListBox)control;



                            foreach(Control child in control.Controls) if(child is Button && (child.Text=="Set waypoint" || child.Text==Localization.Get("SetWaypoint")))choose=(Button)child;



                        }



                        if(input==null || results==null || choose==null)throw new Exception("Search controls missing.");



                        input.Text="airport";



                        if(results.Items.Count==0)throw new Exception("Default airfield is missing from search.");



                        input.Text="zzzzzzzz";



                        if(results.Items.Count!=0 || choose.Enabled)throw new Exception("No-results state allowed navigation.");



                        input.Text="bunker C3";



                        input.Text="grid D4";



                        if(results.Items.Count==0 || !choose.Enabled)throw new Exception("Grid search has no selectable result.");



                        using(Bitmap bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(output,ImageFormat.Png); }



                        checkedDialog=true; choose.PerformClick();



                    } catch(Exception ex) { failure=ex; dialog.Close(); }



                };



                check.Start(); ShowZoneSearch();



            }



            if(failure!=null)throw failure;



            if(!checkedDialog || searchTarget==null || searchTarget.Name!="Grid D4")throw new Exception("Search did not create the selected waypoint.");



        }
public void SaveSettingsPreview(string path) {



            ShowSettings(); PerformLayout();



            using(Bitmap bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(path,ImageFormat.Png); }



            Hide();



        }



        public void CheckZoneEditor(string path) {



            using(ZoneEditor editor=new ZoneEditor(map,new List<MapZone>(),zonesPath,value=>{}))editor.CheckAndPreview(path);



        }



        public void CheckStaticTerrain() {



            Position original=position; bool originalAuto=autoZoom; float originalZoom=zoom;



            try {



                autoZoom=false;



                zoom=1;



                OverlayBitmap(); int builds=TerrainBuilds;



                Position source=position??new Position();



                position=new Position { X=source.X+1000,Y=source.Y+1000,Z=source.Z,Yaw=source.Yaw+10 };



                OverlayBitmap();



                if(TerrainBuilds!=builds)throw new Exception("Full map view unexpectedly rebuilt or moved the terrain.");



                zoom=4; OverlayBitmap();



                if(TerrainBuilds<=builds)throw new Exception("Zoom did not refresh the terrain cache.");



            } finally { position=original; autoZoom=originalAuto; zoom=originalZoom; terrainKey=null; lastFrameKey=null; }



        }



        public void CheckAppearance() {



            Size previous=overlay.Size; bool oldLabels=gridLabels,oldBorders=gridBorders,oldAutoZoom=autoZoom; float oldZoom=zoom;



            overlay.Size=new Size(480,300); zoom=1; autoZoom=false;



            Bitmap resized=OverlayBitmap();



            if(resized.Width!=480 || resized.Height!=300)throw new Exception("Overlay did not resize.");



            gridLabels=false;gridBorders=false;



            gridBorders=true;



            // Compare whole images: a border switch must change raster content.



            using(Bitmap before=(Bitmap)OverlayBitmap().Clone()) {



                gridBorders=false; Bitmap after=OverlayBitmap(); bool different=false;



                for(int y=30;y<270&&!different;y++)for(int x=90;x<390;x++)if(before.GetPixel(x,y)!=after.GetPixel(x,y)) { different=true;break; }



                if(!different)throw new Exception("Grid border switch did not change the map.");



            }



            overlay.Size=previous;gridLabels=oldLabels;gridBorders=oldBorders;zoom=oldZoom;autoZoom=oldAutoZoom;targetZoom=oldZoom;



            lastFrameKey=null;saveAfter=DateTime.MaxValue;



        }

        public static void SelfTest() {



            var arrivalTarget=new MapZone { Name="Destination",Points=new[]{new PointF(.5f,.5f)} };



            if(!DestinationReached(arrivalTarget,new PointF(.5f,.5f))



                || !DestinationReached(arrivalTarget,new PointF(.501f,.5f))



                || DestinationReached(arrivalTarget,new PointF(.503f,.5f))



                || DestinationReached(null,new PointF(.5f,.5f))) throw new Exception("Waypoint arrival radius failed.");



            if(Native.ClassifyCopy(true,"")!=CopyResult.Sent



                || Native.ClassifyCopy(false,"")!=CopyResult.Cancelled



                || Native.ClassifyCopy(false,"SendInput failed")!=CopyResult.Failed) throw new Exception("Copy cancellation must not trigger failure backoff.");



            var chord=new List<string>();



            Func<uint,bool,bool> record=(key,up)=> { chord.Add(key+":"+up); return true; };



            Func<int,Task> noDelay=ms=>Task.FromResult(0);



            if(!Native.CopyChord(record,noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,162:True") throw new Exception("Copy chord ordering failed.");



            chord.Clear();



            if(Native.CopyChord(record,noDelay,()=>false).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,162:True") throw new Exception("Cancelled copy emitted C.");



            chord.Clear();



            bool failedRelease=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==67 && up && !failedRelease) { failedRelease=true; return false; } return true; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,67:True,162:True") throw new Exception("Failed C release was not retried before Control release.");



            chord.Clear();



            if(Native.CopyChord((key,up)=> { record(key,up); return key!=162 || up; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False") throw new Exception("Failed Control press emitted C.");



            chord.Clear();



            if(Native.CopyChord((key,up)=> { record(key,up); return key!=67 || up; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,162:True") throw new Exception("Failed C press did not release Control.");



            chord.Clear();



            bool ctrlReleaseFailed=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==162 && up && !ctrlReleaseFailed) { ctrlReleaseFailed=true; return false; } return true; },noDelay,()=>true).GetAwaiter().GetResult()



                || string.Join(",",chord)!="162:False,67:False,67:True,162:True,162:True") throw new Exception("Failed Control release was not retried.");



            chord.Clear();



            try {



                Native.CopyChord(record,ms=> { if(ms==30)throw new InvalidOperationException("Simulated interruption"); return Task.FromResult(0); },()=>true).GetAwaiter().GetResult();



                throw new Exception("Simulated input interruption was ignored.");



            } catch(InvalidOperationException) {



            }



            chord.Clear();



            if(!Native.CopyChord(record,noDelay,()=>true,0,67).GetAwaiter().GetResult()



                || string.Join(",",chord)!="67:False,67:True") throw new Exception("Single key copy chord ordering failed.");



            chord.Clear();



            if(Native.CopyChord(record,noDelay,()=>false,0,67).GetAwaiter().GetResult()



                || chord.Count!=0) throw new Exception("Cancelled single key copy emitted key.");



            chord.Clear();



            bool singleReleaseFailed=false;



            if(Native.CopyChord((key,up)=> { record(key,up); if(key==67 && up && !singleReleaseFailed) { singleReleaseFailed=true; return false; } return true; },noDelay,()=>true,0,67).GetAwaiter().GetResult()



                || string.Join(",",chord)!="67:False,67:True,67:True") throw new Exception("Failed single key release was not retried.");



            ZoneStore.SelfTest();



            DestinationSearch.SelfTest();



            Localization.SelfTest();



            MapMotion.SelfTest();



            ScumMapStore.SelfTest();



            if(ReadCopyInterval(1,true)!=1000 || ReadCopyInterval(3,true)!=3000 || ReadCopyInterval(0,false)!=1000 || ReadCopyInterval(500,false)!=1000 || ReadCopyInterval(int.MaxValue,false)!=10000)throw new Exception("Sampling interval migration failed.");



            if(!GameFocusReturn.ShouldRestore(true,10,10,20,20)



                || GameFocusReturn.ShouldRestore(true,10,10,30,20)



                || GameFocusReturn.ShouldRestore(false,10,10,20,20)



                || GameFocusReturn.ShouldRestore(true,11,10,20,20)) throw new Exception("Focus return policy failed.");



            foreach(char row in "DCBAZ") for(int col=0;col<5;col++) {



                MapZone target=GridTarget(row.ToString()+col);



                PointF point=target.Centroid;



                if(point.X<=0 || point.X>=1 || point.Y<=0 || point.Y>=1) throw new Exception("Grid target outside map.");



                if(Math.Abs(point.X-(4-col+0.5)/5)>0.002 || Math.Abs(point.Y-("DCBAZ".IndexOf(row)+0.5)/5)>0.002) throw new Exception("Grid orientation incorrect.");



            }



            if(GridTarget(" c 3 ")==null || GridTarget("E3")!=null || GridTarget("C5")!=null || GridTarget("C33")!=null) throw new Exception("Grid validation failed.");



            MapZone first=new MapZone { Name="Town",Points=new[]{new PointF(.2f,.3f)} };



            MapZone second=new MapZone { Name="Town",Points=new[]{new PointF(.7f,.8f)} };



            List<MapZone> found=FindDestinations(new List<MapZone>{first,second}," tOw ");



            if(found.Count!=2 || !object.ReferenceEquals(found[1],second)) throw new Exception("Place search lost duplicate identity.");



            if(FindDestinations(new List<MapZone>(),"Z0").Count!=1 || FindDestinations(new List<MapZone>{first},"missing").Count!=0) throw new Exception("Empty search handling failed.");



            ChatState state=new ChatState();



            PhysicalKeyTransitions transitions=new PhysicalKeyTransitions();



            bool previousCopy=Native.CopyInProgress;



            try {



                Native.CopyInProgress=true;



                if(transitions.ShortcutModifiersDown(false)) throw new Exception("Sampler blocked physical map input.");



                transitions.Update(0xA0,true);



                if(transitions.ShortcutModifiersDown(false) || !transitions.ShortcutModifiersDown(true))



                    throw new Exception("Sprint modifier policy failed.");



                transitions.Update(0xA2,true);



                if(!transitions.ShortcutModifiersDown(false)) throw new Exception("Physical Ctrl not detected.");



                transitions.Update(0xA2,false);



                transitions.Update(0xA0,false);



                if(transitions.ShortcutModifiersDown(true)) throw new Exception("Released modifiers remained active.");



            } finally { Native.CopyInProgress=previousCopy; }



            if(!transitions.Update(0x4D,true)) throw new Exception("First map press lost.");



            for(int repeat=0;repeat<100;repeat++)



                if(transitions.Update(0x4D,true)) throw new Exception("Held map key repeated.");



            if(transitions.Update(0x4D,false) || !transitions.Update(0x4D,true))



                throw new Exception("Map key did not rearm on release.");



            if(transitions.Update(-1,true) || transitions.Update(256,true)) throw new Exception("Invalid key accepted.");



            state.Key(0x54); state.Key(0x54);



            if(!state.Paused)throw new Exception("T must pause without toggling.");



            state.Key(0x4D); if(!state.Paused)throw new Exception("Map key cleared chat gate.");



            state.Key(0xBF); if(!state.Paused)throw new Exception("Typing must preserve chat pause.");



            state.Key(0x09); if(!state.Paused)throw new Exception("Tab while chat open must preserve chat pause.");



            state.Key(0x4D); if(!state.Paused)throw new Exception("Typing after Tab must preserve chat pause.");



            state.Key(0x0D); if(state.Paused)throw new Exception("Enter must resume.");



            state.Key(0x09); if(state.Paused)throw new Exception("Tab while chat closed must not open chat.");



            state.Key(0x54); state.Key(0x1B); if(state.Paused)throw new Exception("Escape must resume.");



            using(Bitmap fade=new Bitmap(60,60,PixelFormat.Format32bppPArgb)) {



                using(Graphics g=Graphics.FromImage(fade))g.Clear(Color.White);



                OverlayWindow.Fade(fade);



                if(fade.GetPixel(0,30).A!=0 || fade.GetPixel(29,29).A<230 || fade.GetPixel(10,30).A>=fade.GetPixel(29,29).A)throw new Exception("Edge alpha fade failed.");



            }



            Position p=Position.Parse("{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");



            if(p==null || p.Y!=-270302.625 || p.Yaw!=13.952554) throw new Exception("Coordinate/rotation parsing failed.");



            if(Position.Parse("hello")!=null || Position.Parse("prefix {X=1 Y=2 Z=3|P=0 Y=0 R=0}")!=null) throw new Exception("Invalid clipboard text accepted.");



            PointF corner=ToMap(new Position { X=617718,Y=618618 });



            PointF end=ToMap(new Position { X=-903900,Y=-905000 });



            if(corner.X!=0 || corner.Y!=0 || Math.Abs(end.X-1)>.00001 || Math.Abs(end.Y-1)>.00001) throw new Exception("Map projection failed.");



            if(RoadRouter.Instance.IsLoaded) {



                var testRoute=RoadRouter.Instance.FindRoute(new PointF(0.62767f,0.63618f),new PointF(0.67270f,0.03878f));



                if(!testRoute.Success || testRoute.Polyline==null || testRoute.Polyline.Length<10) {



                    throw new Exception("Road route self-test failed.");



                }



            }



        }

    }

}
