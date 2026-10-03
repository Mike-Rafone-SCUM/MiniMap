using System;

namespace ScumMiniMap {
    public enum AppControlAction {
        Settings, AddWaypoint, Search, ToggleOverlay, ZoomIn, ZoomOut, DeathMarker,
        ResetZoom, ClearWaypoint, ZoneEditor, StartupGuide, AdminCommands
    }

    // One binding catalogue is shared by settings, setup guides and input dispatch.
    public sealed class AppControlBindings {
        public static readonly AppControlAction[] AllActions = {
            AppControlAction.Settings, AppControlAction.AddWaypoint, AppControlAction.Search,
            AppControlAction.ToggleOverlay, AppControlAction.ZoomIn, AppControlAction.ZoomOut,
            AppControlAction.DeathMarker, AppControlAction.ResetZoom, AppControlAction.ClearWaypoint,
            AppControlAction.ZoneEditor, AppControlAction.StartupGuide, AppControlAction.AdminCommands
        };
        readonly int[] keys = new int[12];
        readonly int[] scans = new int[12];

        public AppControlBindings() {
            Set(AppControlAction.Settings, 0x24, 0);
            Set(AppControlAction.AddWaypoint, 0x2D, 0);
            Set(AppControlAction.Search, 0x2E, 0);
            Set(AppControlAction.ToggleOverlay, 0x23, 0);
            Set(AppControlAction.ZoomIn, 0x21, 0);
            Set(AppControlAction.ZoomOut, 0x22, 0);
            Set(AppControlAction.DeathMarker, 0x28, 0);
        }

        public AppControlBindings Clone() {
            var copy = new AppControlBindings();
            Array.Copy(keys, copy.keys, keys.Length);
            Array.Copy(scans, copy.scans, scans.Length);
            return copy;
        }

        public void Set(AppControlAction action, int key, int scan) {
            int index = Index(action);
            keys[index] = key;
            scans[index] = key != 0 && PhysicalKeyCapture.Valid(scan) ? scan : 0;
        }
        public int Key(AppControlAction action) { return keys[Index(action)]; }
        public int ScanCode(AppControlAction action) { return scans[Index(action)]; }

        static int Index(AppControlAction action) {
            int index = (int)action;
            if(index < 0 || index >= 12) throw new ArgumentOutOfRangeException("action");
            return index;
        }

        public static string LabelKey(AppControlAction action) {
            switch(action) {
                case AppControlAction.Settings: return "Settings";
                case AppControlAction.AddWaypoint: return "ShortcutWaypoint";
                case AppControlAction.Search: return "ShortcutSearch";
                case AppControlAction.ToggleOverlay: return "TrayShowHide";
                case AppControlAction.ZoomIn: return "ControlZoomIn";
                case AppControlAction.ZoomOut: return "ControlZoomOut";
                case AppControlAction.DeathMarker: return "ControlDeathMarker";
                case AppControlAction.ResetZoom: return "ControlResetZoom";
                case AppControlAction.ClearWaypoint: return "ClearWaypoint";
                case AppControlAction.ZoneEditor: return "SidebarZoneEditor";
                case AppControlAction.StartupGuide: return "OpenStartupGuide";
                case AppControlAction.AdminCommands: return "ControlAdminCommands";
                default: throw new ArgumentOutOfRangeException("action");
            }
        }

        internal static bool ValidKey(int key) {
            return key >= 0x20 && key < 0xFF && !PhysicalKeyCapture.IsModifierKey(key)
                && key != 0x5D && key != 0xBF && key != 0x6F;
        }

        internal string BindingError(int mapKey, int mapScan, int chatKey, int chatScan, int copyKey, int copyScan) {
            for(int i = 0; i < AllActions.Length; i++) {
                AppControlAction action = AllActions[i];
                int key = Key(action), scan = ScanCode(action);
                if(key == 0) continue;
                if(!ValidKey(key)) return Localization.Get("ShortcutReserved");
                if(GameConflict(key, scan, mapKey, mapScan, chatKey, chatScan, copyKey, copyScan))
                    return Localization.Get("ShortcutScumConflict");
                for(int j = 0; j < i; j++) {
                    AppControlAction previous = AllActions[j];
                    if(Key(previous) != 0 && PhysicalKeyCapture.SameBinding(key, scan, Key(previous), ScanCode(previous)))
                        return Localization.Get("ShortcutConflict");
                }
            }
            return null;
        }

        internal void Sanitize(int mapKey, int mapScan, int chatKey, int chatScan, int copyKey, int copyScan) {
            for(int i = 0; i < AllActions.Length; i++) {
                AppControlAction action = AllActions[i];
                int key = Key(action), scan = ScanCode(action);
                bool invalid = key != 0 && (!ValidKey(key) || GameConflict(key, scan, mapKey, mapScan, chatKey, chatScan, copyKey, copyScan));
                for(int j = 0; !invalid && key != 0 && j < i; j++) {
                    AppControlAction previous = AllActions[j];
                    invalid = Key(previous) != 0 && PhysicalKeyCapture.SameBinding(key, scan, Key(previous), ScanCode(previous));
                }
                if(invalid) Set(action, 0, 0);
            }
        }

        static bool GameConflict(int key, int scan, int mapKey, int mapScan, int chatKey, int chatScan, int copyKey, int copyScan) {
            return (mapKey != 0 && PhysicalKeyCapture.SameBinding(key, scan, mapKey, mapScan))
                || (chatKey != 0 && PhysicalKeyCapture.SameBinding(key, scan, chatKey, chatScan))
                || (copyKey != 0 && PhysicalKeyCapture.SameBinding(key, scan, copyKey, copyScan));
        }
    }
}
