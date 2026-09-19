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

    }

}
