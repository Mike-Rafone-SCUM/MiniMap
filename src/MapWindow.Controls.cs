using System;
using System.Drawing;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        AppControlBindings appControls = new AppControlBindings();
        readonly DateTime[] lastAdditionalControlActions=new DateTime[12];
        int controlBindingRevision;

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
            if(!HotkeyContextAllowed(action==AppControlAction.Settings)) return;
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
            }
        }

        internal void ResetMapZoom() {
            if(fullMapActive) { fullMapZoom=1f; fullMapPan=new PointF(.5f,.5f); }
            else { autoZoom=false; zoom=1f; targetZoom=zoom; }
            SettingsChanged();
            RenderOverlay();
        }

        // Zoom changes only the rendered view. A cancelled copy can still be
        // releasing its keys asynchronously, so its cleanup must not lose a press.
        void AdjustFullMapZoom(int delta,Point point) {
            int mapDimension=overlay.Height;
            float factor=delta>0?1.25f:1f/1.25f;
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
