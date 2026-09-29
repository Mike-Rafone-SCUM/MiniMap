using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    sealed class ShortcutCaptureDialog:Form {
        readonly Func<int,int,string> validate;
        readonly Label instruction;
        internal int CapturedKey { get; private set; }
        internal int CapturedScanCode { get; private set; }
        internal ShortcutCaptureDialog(string title,Func<int,int,string> validate) {
            this.validate=validate;
            Text=Localization.T("ShortcutTitle",title);
            StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false;
            ClientSize=new Size(460,150);
            RightToLeft=Localization.Current==AppLanguage.Arabic?RightToLeft.Yes:RightToLeft.No;
            instruction=new Label { Text=Localization.Get("ShortcutPrompt"),Dock=DockStyle.Fill,Padding=new Padding(12) };
            Controls.Add(instruction);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            if((keyData&Keys.KeyCode)==Keys.Escape) { DialogResult=DialogResult.Cancel; return true; }
            if((keyData&Keys.Modifiers)!=Keys.None) { instruction.Text=Localization.Get("ShortcutSingleKey")+"\n"+Localization.Get("ShortcutCancel"); return true; }
            int key=(int)(keyData&Keys.KeyCode);
            int scan;
            if(!PhysicalKeyCapture.TryGet(key,out scan)) { instruction.Text=Localization.Get("ShortcutRetry")+"\n"+Localization.Get("ShortcutCancel"); return true; }
            string error=validate(key,scan);
            if(error!=null) { instruction.Text=error+"\n"+Localization.Get("ShortcutCancel"); return true; }
            CapturedKey=key; CapturedScanCode=scan; DialogResult=DialogResult.OK;
            return true;
        }
    }

    public sealed partial class MapWindow {
        // Keep the main window minimized for a persistent taskbar button while the overlay runs.
        bool SettingsVisible { get { return Visible && WindowState!=FormWindowState.Minimized; } }
        bool taskbarMinimized;
        void AddShortcutSettings(FlowLayoutPanel panel) {
            panel.Controls.Add(new Label { Text=Localization.Get("ShortcutHeading"),Width=365,Height=40 });
            AddShortcutButton(panel,Localization.Get("Settings"),()=>settingsShortcutKey,()=>settingsShortcutScanCode,(key,scan)=>{ settingsShortcutKey=key; settingsShortcutScanCode=scan; });
            AddShortcutButton(panel,Localization.Get("ShortcutWaypoint"),()=>pinShortcutKey,()=>pinShortcutScanCode,(key,scan)=>{ pinShortcutKey=key; pinShortcutScanCode=scan; });
            AddShortcutButton(panel,Localization.Get("ShortcutSearch"),()=>searchShortcutKey,()=>searchShortcutScanCode,(key,scan)=>{ searchShortcutKey=key; searchShortcutScanCode=scan; });
            panel.Controls.Add(new Label { Text=Localization.Get("ShortcutPhysicalHelp"),Width=365,Height=64 });
        }

        void AddShortcutButton(FlowLayoutPanel panel,string title,Func<int> read,Func<int> readScan,Action<int,int> write) {
            Button button=new Button { Text=title+": "+PhysicalKeyCapture.KeyName(read()),Width=280 };
            button.Click+=(s,e)=> {
                shortcutCaptureOpen=true;
                Native.CancelActiveCopy();
                try {
                    using(var capture=new ShortcutCaptureDialog(title,(key,scan)=>ShortcutBindingError(key,read(),scan,readScan()))) {
                        if(capture.ShowDialog(this)!=DialogResult.OK) return;
                        write(capture.CapturedKey,capture.CapturedScanCode);
                        button.Text=title+": "+PhysicalKeyCapture.KeyName(read());
                        fallbackKeysArmed=false;
                        SettingsChanged();
                        SaveSettings();
                    }
                } finally { shortcutCaptureOpen=false; }
            };
            panel.Controls.Add(button);
        }

        static bool ValidShortcutKey(int key) {
            return key>=0x20 && key<0xFF && !PhysicalKeyCapture.IsModifierKey(key) && key!=0x5D
                && key!=0xBF && key!=0x6F && key!=0x23 && key!=0x21 && key!=0x22;
        }

        string ShortcutBindingError(int key,int previous,int scan,int previousScan) {
            if(!ValidShortcutKey(key)) return Localization.Get("ShortcutReserved");
            if(PhysicalKeyCapture.SameBinding(key,scan,scumMapKey,scumMapScanCode) || PhysicalKeyCapture.SameBinding(key,scan,scumChatKey,scumChatScanCode) || PhysicalKeyCapture.SameBinding(key,scan,scumCopyKey,scumCopyScanCode)) return Localization.Get("ShortcutScumConflict");
            if(!PhysicalKeyCapture.SameBinding(key,scan,previous,previousScan) &&
                (PhysicalKeyCapture.SameBinding(key,scan,settingsShortcutKey,settingsShortcutScanCode)
                || PhysicalKeyCapture.SameBinding(key,scan,pinShortcutKey,pinShortcutScanCode)
                || PhysicalKeyCapture.SameBinding(key,scan,searchShortcutKey,searchShortcutScanCode))) return Localization.Get("ShortcutConflict");
            return null;
        }
        void HandleTaskbarRestore(object sender,EventArgs args) {
            if(WindowState==FormWindowState.Minimized) { taskbarMinimized=true; return; }
            if(!taskbarMinimized || WindowState!=FormWindowState.Normal) return;
            taskbarMinimized=false;
            if(diagnosticMode || panelOpening || closing || !IsHandleCreated) return;
            BeginInvoke(new Action(()=> {
                if(closing || IsDisposed || WindowState!=FormWindowState.Normal) return;
                BuildSettingsPanel();
                ShowSettings();
            }));
        }

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







        static int ReadCopyInterval(int value,bool legacy) {



            // Legacy settings store seconds; current settings allow 250 ms tracking.



            if(legacy) return value<=1?1000:Math.Min(10,value)*1000;



            return Math.Max(Program.MinimumCopyIntervalMs,Math.Min(10000,value));



        }



        void LoadSettings() {



            try {



                if(!File.Exists(settingsPath)) {



                    isFirstLaunch = true;



                    Localization.DetectSystemLanguage();



                    SaveSettings();



                    return;



                }



                // Legacy settings contain only a virtual key. Never reuse a scan code from a previous load.
                scumCopyScanCode=0;
                scumMapScanCode=0; scumChatScanCode=0;
                settingsShortcutScanCode=0; pinShortcutScanCode=0; searchShortcutScanCode=0;
                string[] lines;



                try { lines=File.ReadAllLines(settingsPath); }



                catch(IOException) { return; }



                catch(UnauthorizedAccessException) { return; }
                try { string versionFile=settingsPath+".version"; updateResetPending=!File.Exists(versionFile) || File.ReadAllText(versionFile).Trim()!=VersionString; } catch { updateResetPending=true; }



                bool foundWelcomed=false, foundLanguage=false, foundTrackingRevision=false;



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



                        if((key=="TrackingRevision" && val=="1") || (Program.IsTestBuild && key=="TestTrackingRevision" && val=="2")) { foundTrackingRevision=true; continue; }
                        if(key=="VoiceEnabled") { bool enabled; if(bool.TryParse(val,out enabled)) voiceEnabled=enabled; continue; }
                        if(key=="VoiceName") { if(val==Path.GetFileName(val)) voiceName=val; continue; }
                        if(key=="VoiceVolume") { int volume; if(int.TryParse(val,out volume)) voiceVolume=Math.Max(0,Math.Min(100,volume)); continue; }
                        if(key=="SuppressCopyKeyReminder") { if(bool.TryParse(val,out b)) suppressCopyKeyReminder=b; continue; }
                        if(key=="Welcomed") { foundWelcomed=true; continue; }



                        if(key=="Language") { Localization.SetLanguage(val); foundLanguage=true; continue; }



                        if(bool.TryParse(val,out b)) {



                            switch(key) { case "LocationHistory":locationHistory=b;break;case "LocationHistoryTimestamps":locationHistoryTimestamps=b;break;case "GridLabels":gridLabels=b;break;case "GridBorders":gridBorders=b;break;case "EdgeFade":edgeFade=b;break;case "ShowStatus":showStatus=b;break;case "ShowZones":showZones=b;break;case "AutoZoom":autoZoom=b;break;case "ShowHeading":showHeading=b;break;case "ShowCompass":showCompass=b;break;case "ZoneChime":zoneChime=b;break;case "ShowCustomWaypoints":showCustomWaypoints=b;break;case "ShowScumMap":showScumMap=b;break;case "ShowZoneLabels":showZoneLabels=b;break;case "SmartLabelLod":smartLabelLod=b;break;case "SidebarWildlifeExpanded":sidebarWildlifeExpanded=b;break;case "SidebarZonesExpanded":sidebarZonesExpanded=b;break; }
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
                            if(key=="SettingsShortcutKey" && ValidShortcutKey(n)) settingsShortcutKey=n;
                            if(key=="PinShortcutKey" && ValidShortcutKey(n)) pinShortcutKey=n;
                            if(key=="SearchShortcutKey" && ValidShortcutKey(n)) searchShortcutKey=n;
                            if(key=="SettingsShortcutScanCode" && PhysicalKeyCapture.Valid(n)) settingsShortcutScanCode=n;
                            if(key=="PinShortcutScanCode" && PhysicalKeyCapture.Valid(n)) pinShortcutScanCode=n;
                            if(key=="SearchShortcutScanCode" && PhysicalKeyCapture.Valid(n)) searchShortcutScanCode=n;
                            if(key=="ScumCopyScanCode" && PhysicalKeyCapture.Valid(n)) scumCopyScanCode=n;
                            if(key=="ScumMapScanCode" && PhysicalKeyCapture.Valid(n)) scumMapScanCode=n;
                            if(key=="ScumChatScanCode" && PhysicalKeyCapture.Valid(n)) scumChatScanCode=n;



                            switch(key) { case "LocationHistoryMinutes":locationHistoryMinutes=Math.Max(1,Math.Min(240,n));break;case "Width":width=Math.Max(240,Math.Min(800,n));break;case "Height":height=Math.Max(240,Math.Min(800,n));break;case "Left":left=n;break;case "Top":top=n;break;case "Opacity":mapOpacity=Math.Max(30,Math.Min(100,n));break;case "FullMapOpacity":fullMapOpacity=Math.Max(20,Math.Min(100,n));break;case "GridOpacity":gridOpacity=Math.Max(0,Math.Min(100,n));break;case "LabelSize":labelSize=Math.Max(6,Math.Min(24,n));break;case "MaxZoom":maxZoom=Math.Max(4,Math.Min(32,n));break;case "AutoZoomMin":autoZoomMin=Math.Max(1,Math.Min(32,n));break;case "AutoZoomMax":autoZoomMax=Math.Max(1,Math.Min(32,n));break;case "ZoomStep":zoomStepPercent=Math.Max(10,Math.Min(100,n));break;case "CopyInterval":copyIntervalMs=ReadCopyInterval(n,true);break;case "CopyIntervalMs":copyIntervalMs=ReadCopyInterval(n,false);break;case "ScumMapKey":if(n>0&&n<256)scumMapKey=n;break;case "ScumChatKey":if(n>0&&n<256)scumChatKey=n;break;case "ScumCopyModifierKey":if(n==0||PhysicalKeyCapture.IsModifierKey(n))scumCopyModifierKey=n;break;case "ScumCopyKey":if(n>0&&n<256&&!PhysicalKeyCapture.IsModifierKey(n))scumCopyKey=n;break; }



                        }



                        if(key=="Zoom" && float.TryParse(val,NumberStyles.Float,CultureInfo.InvariantCulture,out z) && !float.IsNaN(z) && !float.IsInfinity(z)) { zoom=Math.Max(1,Math.Min(maxZoom,z)); targetZoom=zoom; }



                    } catch { /* skip malformed line */ }



                }



                // Upgrade the old one-second default once; retain slower custom intervals.
                copyIntervalMs=Program.UpgradeCopyInterval(copyIntervalMs,foundTrackingRevision);
                if(!foundWelcomed) isFirstLaunch=true;



                if(!foundLanguage) Localization.DetectSystemLanguage();



                if(PhysicalKeyCapture.SameBinding(scumMapKey,scumMapScanCode,scumChatKey,scumChatScanCode)) {



                    // A malformed or hand-edited config must not make the map key permanently



                    // indistinguishable from the chat key.



                    scumMapKey=0x4D;



                    scumChatKey=0x54;
                    scumMapScanCode=0; scumChatScanCode=0;



                }



                if(scumCopyModifierKey!=0 && !PhysicalKeyCapture.IsModifierKey(scumCopyModifierKey)) scumCopyModifierKey=Program.DefaultCopyModifierKey;



                if(scumCopyKey<=0 || scumCopyKey>=256 || PhysicalKeyCapture.IsModifierKey(scumCopyKey)) scumCopyKey=Program.DefaultCopyKey;
                if(!PhysicalKeyCapture.Valid(scumCopyScanCode)) scumCopyScanCode=0;
                if(!PhysicalKeyCapture.Valid(scumMapScanCode)) scumMapScanCode=0;
                if(!PhysicalKeyCapture.Valid(scumChatScanCode)) scumChatScanCode=0;
                if(!PhysicalKeyCapture.Valid(settingsShortcutScanCode)) settingsShortcutScanCode=0;
                if(!PhysicalKeyCapture.Valid(pinShortcutScanCode)) pinShortcutScanCode=0;
                if(!PhysicalKeyCapture.Valid(searchShortcutScanCode)) searchShortcutScanCode=0;
                if(PhysicalKeyCapture.SameBinding(settingsShortcutKey,settingsShortcutScanCode,pinShortcutKey,pinShortcutScanCode)
                    || PhysicalKeyCapture.SameBinding(settingsShortcutKey,settingsShortcutScanCode,searchShortcutKey,searchShortcutScanCode)
                    || PhysicalKeyCapture.SameBinding(pinShortcutKey,pinShortcutScanCode,searchShortcutKey,searchShortcutScanCode)) {
                    settingsShortcutKey=0x24; pinShortcutKey=0x2D; searchShortcutKey=0x2E;
                    settingsShortcutScanCode=0; pinShortcutScanCode=0; searchShortcutScanCode=0;
                }
                autoZoomMax=Math.Max(autoZoomMin,autoZoomMax);
                zoom=Math.Max(1,Math.Min(maxZoom,zoom)); targetZoom=zoom;
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
            Rectangle minimapBounds=overlay!=null?overlay.MinimapBounds:new Rectangle(savedLeft,savedTop,savedWidth,savedHeight);



            try { File.WriteAllLines(settingsPath+".tmp",new string[]{"Welcomed=True","TrackingRevision=1","LocationHistory="+locationHistory,"LocationHistoryTimestamps="+locationHistoryTimestamps,"LocationHistoryMinutes="+locationHistoryMinutes,"SuppressCopyKeyReminder="+suppressCopyKeyReminder,"VoiceEnabled="+voiceEnabled,"VoiceName="+voiceName,"VoiceVolume="+voiceVolume,"Language="+Localization.CurrentCode,"GridLabels="+gridLabels,"GridBorders="+gridBorders,"GridOpacity="+gridOpacity,"ShowZones="+showZones,"LabelSize="+labelSize,"EdgeFade="+edgeFade,"Shape="+overlayShape,"ShowHeading="+showHeading,"ShowCompass="+showCompass,"ZoneChime="+zoneChime,"CopyIntervalMs="+copyIntervalMs,"AutoZoom="+autoZoom,"AutoZoomMin="+autoZoomMin,"AutoZoomMax="+autoZoomMax,"ShowStatus="+showStatus,"StatusPos="+statusPos,"Opacity="+mapOpacity,"FullMapOpacity="+fullMapOpacity,"Width="+minimapBounds.Width,"Height="+minimapBounds.Height,"Left="+minimapBounds.Left,"Top="+minimapBounds.Top,"Zoom="+zoom.ToString(CultureInfo.InvariantCulture),"MaxZoom="+maxZoom,"ZoomStep="+zoomStepPercent,"ScumMapKey="+scumMapKey,"ScumChatKey="+scumChatKey,"ScumCopyModifierKey="+scumCopyModifierKey,"ScumCopyKey="+scumCopyKey,"ShowCustomWaypoints="+showCustomWaypoints,"ShowScumMap="+showScumMap,"ScumMapDisabledCats="+(scumMap!=null?scumMap.GetDisabledCategoriesString():""),"ShowZoneLabels="+showZoneLabels,"SmartLabelLod="+smartLabelLod,"SidebarWildlifeExpanded="+sidebarWildlifeExpanded,"SidebarZonesExpanded="+sidebarZonesExpanded,"DisabledZoneLayers="+string.Join(";",disabledZoneLayers),"RouteColor="+ColorTranslator.ToHtml(routeGuidanceColor),"PlayerColor="+ColorTranslator.ToHtml(playerConeColor)});



                File.AppendAllText(settingsPath+".tmp","ScumCopyScanCode="+scumCopyScanCode+Environment.NewLine+"ScumMapScanCode="+scumMapScanCode+Environment.NewLine+"ScumChatScanCode="+scumChatScanCode+Environment.NewLine);
                File.AppendAllLines(settingsPath+".tmp",new[]{"SettingsShortcutKey="+settingsShortcutKey,"PinShortcutKey="+pinShortcutKey,"SearchShortcutKey="+searchShortcutKey,"SettingsShortcutScanCode="+settingsShortcutScanCode,"PinShortcutScanCode="+pinShortcutScanCode,"SearchShortcutScanCode="+searchShortcutScanCode});
                if(File.Exists(settingsPath)) File.Replace(settingsPath+".tmp",settingsPath,settingsPath+".bak"); else File.Move(settingsPath+".tmp",settingsPath); File.WriteAllText(settingsPath+".version",VersionString); }



            catch(IOException) { note=Localization.Get("NoteSettingsSaveFail"); } catch(UnauthorizedAccessException) { note=Localization.Get("NoteSettingsSaveFail"); }



        }



        readonly GameFocusReturn settingsFocus=new GameFocusReturn();



        bool panelOpening;
        bool layoutSettingsRequested;

        void FocusLayoutSettings() {
            if(!layoutSettingsRequested || widthOption==null) return;
            layoutSettingsRequested=false;
            bar.ScrollControlIntoView(widthOption.Parent);
            widthOption.Focus();
        }



        void DismissSettings() {



            SaveSettings(); WindowState=FormWindowState.Minimized; settingsFocus.Restore();



            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);



        }



        async void ShowSettings() {
            if(panelOpening || searchOpen) return;
            Native.ReleaseCapture();
            Cursor.Clip=Rectangle.Empty;
            if(SettingsVisible) {



                Height=Math.Min(640,Screen.FromControl(overlay).WorkingArea.Height - 40);



                OverlayTheme.Anchor(this,overlay);



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);

                FocusLayoutSettings();



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



                BuildSettingsPanel();
                WindowState=FormWindowState.Normal;
                Show();
                FocusLayoutSettings();



                BringToFront();



                Activate();



                Native.ForceForeground(Handle);



            } finally { panelOpening=false; }



        }

    }

}
