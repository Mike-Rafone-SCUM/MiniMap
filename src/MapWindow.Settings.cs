using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    public sealed partial class MapWindow {

        void AddCheck(FlowLayoutPanel panel,string text,bool value,Action<bool> changed) {



            TacticalCheckBox box = new TacticalCheckBox { Text = text, Checked = value, Width = 365, Margin = new Padding(0, 2, 0, 2) };



            box.CheckedChanged+=(s,e)=> { changed(box.Checked); SettingsChanged(); }; panel.Controls.Add(box);



        }



        NumericUpDown AddNumber(FlowLayoutPanel panel,string text,int min,int max,int value,Action<int> changed) {



            FlowLayoutPanel row=new FlowLayoutPanel { Width=365,Height=31 };



            row.Controls.Add(new Label { Text=text,Width=220,Padding=new Padding(0,5,0,0) });



            NumericUpDown number=new NumericUpDown { Minimum=min,Maximum=max,Value=value,Width=100 };



            number.ValueChanged+=(s,e)=> { changed((int)number.Value); SettingsChanged(); }; row.Controls.Add(number); panel.Controls.Add(row); return number;



        }



        void InvalidateFullMapSidebar() { fullMapSidebarRevision++; }



        void SettingsChanged() { terrainKey=null; lastFrameKey=null; cachedLocationAnchors=null; locationDescriptionRevision++; InvalidateFullMapSidebar(); saveAfter=DateTime.UtcNow.AddMilliseconds(700); }



        static bool IsCustomWaypointZone(MapZone zone) {



            // Saved custom zones are polygons; saved waypoints are the cyan, single-point



            // entries created by Insert or the full-map context menu.



            return zone!=null && zone.Points!=null && zone.Points.Length==1;



        }



        HashSet<ZoneCategory> BuildHiddenCategories() {



            var hidden = new HashSet<ZoneCategory>();



            if(!showCities) hidden.Add(ZoneCategory.City);



            if(!showTowns) hidden.Add(ZoneCategory.Town);



            if(!showFarms) hidden.Add(ZoneCategory.Farm);



            if(!showTraders) hidden.Add(ZoneCategory.Trader);



            if(!showFactions) hidden.Add(ZoneCategory.Faction);



            if(!showMilitary) hidden.Add(ZoneCategory.Military);



            if(!showBunkers) hidden.Add(ZoneCategory.Bunker);



            return hidden.Count==0 ? null : hidden;



        }



        static int ReadCopyInterval(int value,bool legacy) {



            // Limit synthetic shortcuts to one per second; preserve slower custom values.



            if(legacy) return value<=1?1000:Math.Min(10,value)*1000;



            return Math.Max(1000,Math.Min(10000,value));



        }



        void LoadSettings() {



            try {



                if(!File.Exists(settingsPath)) {



                    isFirstLaunch = true;



                    Localization.DetectSystemLanguage();



                    SaveSettings();



                    return;



                }



                string[] lines;



                try { lines=File.ReadAllLines(settingsPath); }



                catch(IOException) { return; }



                catch(UnauthorizedAccessException) { return; }



                bool foundWelcomed=false, foundLanguage=false;



                int width=savedWidth,height=savedHeight,left=overlay!=null?overlay.Left:savedLeft,top=overlay!=null?overlay.Top:savedTop;



                foreach(string line in lines) {



                    try {



                        if(string.IsNullOrEmpty(line)) continue;



                        int eqIdx=line.IndexOf('=');



                        if(eqIdx<1 || eqIdx>=line.Length-1) continue;



                        string key=line.Substring(0,eqIdx).Trim();



                        string val=line.Substring(eqIdx+1).Trim();



                        if(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;



                        int n; bool b; float z;



                        if(key=="Welcomed") { foundWelcomed=true; continue; }



                        if(key=="Language") { Localization.SetLanguage(val); foundLanguage=true; continue; }



                        if(bool.TryParse(val,out b)) {



                            switch(key) { case "GridLabels":gridLabels=b;break;case "GridBorders":gridBorders=b;break;case "EdgeFade":edgeFade=b;break;case "ShowStatus":showStatus=b;break;case "ShowZones":showZones=b;break;case "ShowGasStations":showGasStations=b;break;case "AutoZoom":autoZoom=b;break;case "ShowHeading":showHeading=b;break;case "ShowCompass":showCompass=b;break;case "ShowElevation":showElevation=b;break;case "ZoneChime":zoneChime=b;break;case "ShowCities":showCities=b;break;case "ShowTowns":showTowns=b;break;case "ShowFarms":showFarms=b;break;case "ShowTraders":showTraders=b;break;case "ShowFactions":showFactions=b;break;case "ShowMilitary":showMilitary=b;break;case "ShowBunkers":showBunkers=b;break;case "ShowCustomWaypoints":showCustomWaypoints=b;break;case "ShowScumMap":showScumMap=b;break;case "ShowZoneLabels":showZoneLabels=b;break;case "SmartLabelLod":smartLabelLod=b;break;case "ShowHuntingLegend":showHuntingLegend=b;break;case "SidebarWildlifeExpanded":sidebarWildlifeExpanded=b;break;case "SidebarZonesExpanded":sidebarZonesExpanded=b;break; }
                        }
                        if(key=="DisabledZoneLayers") {
                            disabledZoneLayers.Clear();
                            if(!string.IsNullOrWhiteSpace(val)) {
                                foreach(string dl in val.Split(new[]{',',';'}, StringSplitOptions.RemoveEmptyEntries)) {
                                    string trimmed = dl.Trim();
                                    if(!string.IsNullOrEmpty(trimmed)) disabledZoneLayers.Add(trimmed);
                                }
                            }
                        }
                        if(key=="ScumMapDisabledCats") {
                            if(scumMap!=null) scumMap.ApplyDisabledCategoriesString(val);
                            else pendingDisabledCats=val;
                        }



                        if(key=="StatusPos") { statusPos=val=="Above"?"Above":"Below"; }



                        if(key=="Shape") { overlayShape=val=="Circle"?"Circle":"Square"; }
                        if(key=="PlayerColor" || key=="PlayerConeColor") {
                            try {
                                if(val.StartsWith("#")) playerConeColor = ColorTranslator.FromHtml(val);
                                else if(int.TryParse(val, out n)) playerConeColor = Color.FromArgb(n);
                            } catch {}
                            continue;
                        }
                        if(key=="RouteColor") {
                            try {
                                if(val.StartsWith("#")) routeGuidanceColor = ColorTranslator.FromHtml(val);
                                else if(int.TryParse(val, out n)) routeGuidanceColor = Color.FromArgb(n);
                            } catch {}
                            continue;
                        }



                        if(int.TryParse(val,out n)) {



                            switch(key) { case "Width":width=Math.Max(240,Math.Min(800,n));break;case "Height":height=Math.Max(240,Math.Min(800,n));break;case "Left":left=n;break;case "Top":top=n;break;case "Opacity":mapOpacity=Math.Max(30,Math.Min(100,n));break;case "FullMapOpacity":fullMapOpacity=Math.Max(20,Math.Min(100,n));break;case "GridOpacity":gridOpacity=Math.Max(0,Math.Min(100,n));break;case "LabelSize":labelSize=Math.Max(6,Math.Min(24,n));break;case "MaxZoom":maxZoom=Math.Max(4,Math.Min(32,n));break;case "AutoZoomMin":autoZoomMin=Math.Max(1,Math.Min(32,n));break;case "AutoZoomMax":autoZoomMax=Math.Max(1,Math.Min(32,n));break;case "ZoomStep":zoomStepPercent=Math.Max(10,Math.Min(100,n));break;case "CopyInterval":copyIntervalMs=ReadCopyInterval(n,true);break;case "CopyIntervalMs":copyIntervalMs=ReadCopyInterval(n,false);break;case "ScumMapKey":if(n>0&&n<256)scumMapKey=n;break;case "ScumChatKey":if(n>0&&n<256)scumChatKey=n;break;case "ScumCopyModifierKey":if(n==0||IsModifierKey(n))scumCopyModifierKey=n;break;case "ScumCopyKey":if(n>0&&n<256&&!IsModifierKey(n))scumCopyKey=n;break; }



                        }



                        if(key=="Zoom" && float.TryParse(val,NumberStyles.Float,CultureInfo.InvariantCulture,out z) && !float.IsNaN(z) && !float.IsInfinity(z)) { zoom=Math.Max(1,Math.Min(maxZoom,z)); targetZoom=zoom; }



                    } catch { /* skip malformed line */ }



                }



                if(!foundWelcomed) isFirstLaunch=true;



                if(!foundLanguage) Localization.DetectSystemLanguage();



                if(scumMapKey==scumChatKey) {



                    // A malformed or hand-edited config must not make the map key permanently



                    // indistinguishable from the chat key.



                    scumMapKey=0x4D;



                    scumChatKey=0x54;



                }



                if(scumCopyModifierKey!=0 && !IsModifierKey(scumCopyModifierKey)) scumCopyModifierKey=0xA2;



                if(scumCopyKey<=0 || scumCopyKey>=256 || IsModifierKey(scumCopyKey)) scumCopyKey=0x43;



                savedWidth=width; savedHeight=height; savedLeft=left; savedTop=top;



                if(overlay!=null) {



                    overlay.Size=new Size(width,height);



                    try {



                        Rectangle area=Screen.FromPoint(new Point(left,top)).WorkingArea;



                        overlay.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-width,left)),Math.Max(area.Top,Math.Min(area.Bottom-height,top)));



                    } catch { /* default position if screen detection fails */ }



                }



            } catch { /* catch-all: proceed with defaults on any unexpected error */ }



        }



        void SaveSettings() {



            if(diagnosticMode)return;



            saveAfter=DateTime.MaxValue;



            try { File.WriteAllLines(settingsPath+".tmp",new string[]{"Welcomed=True","Language="+Localization.CurrentCode,"GridLabels="+gridLabels,"GridBorders="+gridBorders,"GridOpacity="+gridOpacity,"ShowZones="+showZones,"ShowGasStations="+showGasStations,"LabelSize="+labelSize,"EdgeFade="+edgeFade,"Shape="+overlayShape,"ShowHeading="+showHeading,"ShowCompass="+showCompass,"ShowElevation="+showElevation,"ZoneChime="+zoneChime,"CopyIntervalMs="+copyIntervalMs,"AutoZoom="+autoZoom,"AutoZoomMin="+autoZoomMin,"AutoZoomMax="+autoZoomMax,"ShowStatus="+showStatus,"StatusPos="+statusPos,"Opacity="+mapOpacity,"FullMapOpacity="+fullMapOpacity,"Width="+(overlay!=null?overlay.Width:savedWidth),"Height="+(overlay!=null?overlay.Height:savedHeight),"Left="+(overlay!=null?overlay.Left:savedLeft),"Top="+(overlay!=null?overlay.Top:savedTop),"Zoom="+zoom.ToString(CultureInfo.InvariantCulture),"MaxZoom="+maxZoom,"ZoomStep="+zoomStepPercent,"ScumMapKey="+scumMapKey,"ScumChatKey="+scumChatKey,"ScumCopyModifierKey="+scumCopyModifierKey,"ScumCopyKey="+scumCopyKey,"ShowCities="+showCities,"ShowTowns="+showTowns,"ShowFarms="+showFarms,"ShowTraders="+showTraders,"ShowFactions="+showFactions,"ShowMilitary="+showMilitary,"ShowBunkers="+showBunkers,"ShowCustomWaypoints="+showCustomWaypoints,"ShowScumMap="+showScumMap,"ScumMapDisabledCats="+(scumMap!=null?scumMap.GetDisabledCategoriesString():""),"ShowZoneLabels="+showZoneLabels,"SmartLabelLod="+smartLabelLod,"ShowHuntingLegend="+showHuntingLegend,"SidebarWildlifeExpanded="+sidebarWildlifeExpanded,"SidebarZonesExpanded="+sidebarZonesExpanded,"DisabledZoneLayers="+string.Join(";",disabledZoneLayers),"RouteColor="+ColorTranslator.ToHtml(routeGuidanceColor),"PlayerColor="+ColorTranslator.ToHtml(playerConeColor)});



                if(File.Exists(settingsPath)) File.Replace(settingsPath+".tmp",settingsPath,settingsPath+".bak"); else File.Move(settingsPath+".tmp",settingsPath); }



            catch(IOException) { note=Localization.Get("NoteSettingsSaveFail"); } catch(UnauthorizedAccessException) { note=Localization.Get("NoteSettingsSaveFail"); }



        }



        readonly GameFocusReturn settingsFocus=new GameFocusReturn();



        bool panelOpening;



        void DismissSettings() {



            SaveSettings(); settingsFocus.Restore(); Hide();



            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



        }



        async void ShowSettings() {
            if(panelOpening || searchOpen) return;
            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;
            if(Visible) {



                Height=Math.Min(640,Screen.FromControl(overlay).WorkingArea.Height - 40);



                OverlayTheme.Anchor(this,overlay);



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);



                return;



            }



            panelOpening=true;



            lastHomeAction=DateTime.UtcNow;



            settingsFocus.Capture();



            try {



                while(Native.CopyInProgress) await Task.Delay(5);



                if(closing || IsDisposed) return;



                Height=Math.Min(640,Screen.FromControl(overlay).WorkingArea.Height - 40);



                OverlayTheme.Anchor(this,overlay);



                Show();



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);



            } finally { panelOpening=false; }



        }

    }

}
