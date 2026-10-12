using System;
using System.Drawing;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        // Admin preparation owns the map/chat transitions. Its sender checks inventory
        // once before opening chat; subsequent physical input cancels the input lease.
        internal void UpdateInventoryInputGate(DateTime now,bool gameFocused,Func<bool> selectedTab) {
            if(adminCommandBusy)return;
            if(!gameFocused || fullMapActive) {
                inventoryInputLocked=false;inventoryProbeMisses=0;
            } else if(now>=nextInventoryProbe) {
                nextInventoryProbe=now.AddMilliseconds(120);
                bool inventoryVisible=selectedTab();
                if(inventoryVisible) {
                    inventoryProbeMisses=0;
                    if(!inventoryInputLocked)Native.CancelActiveCopy();
                    inventoryInputLocked=true;
                } else if(inventoryInputLocked && ++inventoryProbeMisses>=2) {
                    inventoryInputLocked=false;inventoryProbeMisses=0;
                }
            }
        }

        AppControlBindings appControls = new AppControlBindings();
        readonly DateTime[] lastAdditionalControlActions=new DateTime[AppControlBindings.AllActions.Length];
        int controlBindingRevision;

        string lastShortcutTraceState;
        void TraceShortcutBindings() {
            if(!Native.InputTraceActive) return;
            Native.TraceInput("startup version="+VersionString+" test="+Program.IsTestBuild+" profile="+Program.DataFolderName);
            Native.TraceFocusSecurity(false);
            Native.TraceInput("binding Map vk="+scumMapKey+" scan="+scumMapScanCode+" Chat vk="+scumChatKey+
                " scan="+scumChatScanCode+" Copy vk="+scumCopyKey+" scan="+scumCopyScanCode+" modifier="+scumCopyModifierKey);
            foreach(var action in AppControlBindings.AllActions)
                Native.TraceInput("binding "+action+" vk="+AppControlKey(action)+" scan="+AppControlScanCode(action));
        }

        string ShortcutTraceState(bool settingsShortcut=false) {
            return "focus="+AppShortcutWindowFocused(settingsShortcut)+" chat="+chat.Paused+" inventory="+inventoryInputLocked+
                " settings="+SettingsVisible+" search="+searchOpen+" panel="+panelOpening+" capture="+shortcutCaptureOpen+
                " admin="+adminCommandBusy+" modifiers="+(keys!=null && keys.ShortcutModifiersDown(true))+
                " handle="+IsHandleCreated+" closing="+closing+" diagnostic="+diagnosticMode+" fallbackArmed="+fallbackKeysArmed;
        }

        void TraceShortcutContext(string stage,int key,int scanCode) {
            if(!Native.InputTraceActive) return;
            bool settingsShortcut=MatchesAppControl(AppControlAction.Settings,key,scanCode);
            Native.TraceInput("shortcut "+stage+" vk="+key+" scan="+scanCode+" "+ShortcutTraceState(settingsShortcut));
        }

        void TraceShortcutState() {
            if(!Native.InputTraceActive) return;
            string state=ShortcutTraceState();
            if(state==lastShortcutTraceState) return;
            lastShortcutTraceState=state;
            Native.TraceInput("shortcut gates "+state);
        }

        internal int AppControlKey(AppControlAction action) {
            switch(action) {
                case AppControlAction.Settings: return settingsShortcutKey;
                case AppControlAction.AddWaypoint: return pinShortcutKey;
                case AppControlAction.Search: return searchShortcutKey;
                default: return appControls.Key(action);
            }
        }

        internal int AppControlScanCode(AppControlAction action) {
            switch(action) {
                case AppControlAction.Settings: return settingsShortcutScanCode;
                case AppControlAction.AddWaypoint: return pinShortcutScanCode;
                case AppControlAction.Search: return searchShortcutScanCode;
                default: return appControls.ScanCode(action);
            }
        }

        internal AppControlBindings CurrentAppControls() {
            var bindings=appControls.Clone();
            foreach(var action in AppControlBindings.AllActions)
                bindings.Set(action,AppControlKey(action),AppControlScanCode(action));
            return bindings;
        }

        void StoreAppControls(AppControlBindings bindings) {
            appControls=bindings.Clone();
            settingsShortcutKey=bindings.Key(AppControlAction.Settings);
            settingsShortcutScanCode=bindings.ScanCode(AppControlAction.Settings);
            pinShortcutKey=bindings.Key(AppControlAction.AddWaypoint);
            pinShortcutScanCode=bindings.ScanCode(AppControlAction.AddWaypoint);
            searchShortcutKey=bindings.Key(AppControlAction.Search);
            searchShortcutScanCode=bindings.ScanCode(AppControlAction.Search);
        }

        internal void ApplyAppControls(AppControlBindings bindings) {
            string error=bindings.BindingError(scumMapKey,scumMapScanCode,scumChatKey,scumChatScanCode,scumCopyKey,scumCopyScanCode);
            if(error!=null) throw new ArgumentException(error,"bindings");
            Native.CancelActiveCopy();
            inputGeneration++;
            controlBindingRevision++;
            fallbackKeysArmed=false;
            StoreAppControls(bindings);
            SettingsChanged();
            SaveSettings();
            BuildTrayMenu();
        }

        internal bool MatchesAppControl(AppControlAction action,int key,int scanCode) {
            int binding=AppControlKey(action);
            return binding!=0 && PhysicalKeyCapture.Matches(key,scanCode,binding,AppControlScanCode(action));
        }

        string AppControlBindingError(AppControlAction action,int key,int scanCode) {
            var bindings=CurrentAppControls();
            bindings.Set(action,key,scanCode);
            return bindings.BindingError(scumMapKey,scumMapScanCode,scumChatKey,scumChatScanCode,scumCopyKey,scumCopyScanCode);
        }

        internal bool TryGetAppControl(int key,int scanCode,out AppControlAction action) {
            foreach(var candidate in AppControlBindings.AllActions) {
                if(MatchesAppControl(candidate,key,scanCode)) { action=candidate; return true; }
            }
            action=default(AppControlAction);
            return false;
        }

        bool AppShortcutWindowFocused(bool settingsShortcut=false) {
            IntPtr foreground=Native.GetForegroundWindow();
            return Native.GameFocused()
                || (fullMapActive && overlay!=null && overlay.IsHandleCreated && foreground==overlay.Handle)
                || (settingsShortcut && IsHandleCreated && foreground==Handle);
        }

        internal void DispatchAppControl(AppControlAction action) {
            if(!HotkeyContextAllowed(action==AppControlAction.Settings)) {
                TraceShortcutContext("rejected action="+action,AppControlKey(action),AppControlScanCode(action));
                return;
            }
            if(Native.InputTraceActive) Native.TraceInput("shortcut action="+action+" accepted location="+(position!=null)+
                " fullMap="+fullMapActive+" zoom="+zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)+
                " maxZoom="+maxZoom.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Native.CancelActiveCopy();
            resumeAfter=DateTime.UtcNow.AddMilliseconds(400);
            switch(action) {
                case AppControlAction.Settings: FocusSettingsShortcut(); return;
                case AppControlAction.AddWaypoint: TriggerPin(); return;
                case AppControlAction.Search: TriggerZoneSearch(); return;
                case AppControlAction.ToggleOverlay: TriggerToggleOverlay(); return;
                case AppControlAction.ZoomIn: TriggerZoomIn(); return;
                case AppControlAction.ZoomOut: TriggerZoomOut(); return;
                case AppControlAction.DeathMarker: TriggerDeathMarker(); return;
            }
            if((DateTime.UtcNow-lastAdditionalControlActions[(int)action]).TotalMilliseconds<450) return;
            lastAdditionalControlActions[(int)action]=DateTime.UtcNow;
            switch(action) {
                case AppControlAction.ResetZoom: ResetMapZoom(); break;
                case AppControlAction.ClearWaypoint: ClearActiveWaypoint(); break;
                case AppControlAction.ZoneEditor: OpenZoneImport(null); break;
                case AppControlAction.StartupGuide: OpenStartupGuide(); break;
                case AppControlAction.AdminCommands: ShowAdminCommands(); break;
                case AppControlAction.AdminPlayers: ShowAdminPlayers(); break;
            }
        }

        internal void ResetMapZoom() {
            if(fullMapActive) { fullMapZoom=1f; fullMapPan=new PointF(.5f,.5f);fullMapZoomOpacityOverride=false; }
            else { autoZoom=false; zoom=1f; targetZoom=zoom; }
            SettingsChanged();
            RenderOverlay();
        }

        // Zoom changes only the rendered view. A cancelled copy can still be
        // releasing its keys asynchronously, so its cleanup must not lose a press.
        void AdjustFullMapZoom(int delta,Point point) {
            int mapDimension=overlay.Height;
            int steps=delta/120;if(steps==0)return;
            float factor=(float)Math.Pow(1.25,Math.Max(-64,Math.Min(64,steps)));
            float newZoom=Math.Max(1f,Math.Min(16f,fullMapZoom*factor));
            if(Math.Abs(newZoom-1f)<.05f) {
                fullMapZoom=1f; fullMapPan=new PointF(.5f,.5f);
            } else {
                if(overlay.Width>0 && mapDimension>0) {
                    float sideBefore=mapDimension*fullMapZoom,sideAfter=mapDimension*newZoom;
                    float offsetX=point.X-mapDimension/2f,offsetY=point.Y-mapDimension/2f;
                    fullMapPan.X=Math.Max(.05f,Math.Min(.95f,fullMapPan.X+offsetX/sideBefore-offsetX/sideAfter));
                    fullMapPan.Y=Math.Max(.05f,Math.Min(.95f,fullMapPan.Y+offsetY/sideBefore-offsetY/sideAfter));
                }
                fullMapZoom=newZoom;
            }
            fullMapZoomOpacityOverride=fullMapZoom>1f;
            InvalidateFullMapSidebar();
            lastFrameKey=null; terrainKey=null;
            RenderOverlay();
        }

        internal void ClearActiveWaypoint() {
            searchTarget=null; pin=null; activeRoute=null; routeGeneration++; lastRouteTarget=null;
            StopVoice(); voiceTarget=null; voiceNavigator.Reset();
            voiceStatusUntil=DateTime.MinValue; lastVoicePosition=null; lastVoiceRoute=null;
            note=Localization.Get("NoteWaypointCleared");
            SettingsChanged();
            RenderOverlay();
        }
    }
}
