using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScumMiniMap;

static class InputVoiceTests {
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int WindowStyle(IntPtr window,int index);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    static int count;
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); count++; Console.WriteLine("PASS: "+message); }
    static PointF P(double x,double y) { return new PointF((float)(.5+x/15216.18),(float)(.5+y/15236.18)); }
    static IEnumerable<Control> Descendants(Control root) { foreach(Control child in root.Controls) { yield return child; foreach(Control nested in Descendants(child)) yield return nested; } }
    static RoadRoute Route(params PointF[] points) { return new RoadRoute { Success=true,Polyline=points,JunctionIndices=Enumerable.Range(1,Math.Max(0,points.Length-2)).ToArray() }; }
    static T Field<T>(object instance,string name) { return (T)instance.GetType().GetField(name,Hidden).GetValue(instance); }
    static void SetField(object instance,string name,object value) { instance.GetType().GetField(name,Hidden).SetValue(instance,value); }
    static object Invoke(object instance,string name,params object[] arguments) { return instance.GetType().GetMethod(name,Hidden).Invoke(instance,arguments); }
    static AppControlBindings WindowControls(MapWindow window) { return (AppControlBindings)Invoke(window,"CurrentAppControls"); }

    static void TestAppControlModel() {
        Check(AppControlBindings.AllActions.SequenceEqual(new[]{AppControlAction.Settings,AppControlAction.AddWaypoint,AppControlAction.Search,
            AppControlAction.ToggleOverlay,AppControlAction.ZoomIn,AppControlAction.ZoomOut,AppControlAction.DeathMarker,
            AppControlAction.ResetZoom,AppControlAction.ClearWaypoint,AppControlAction.ZoneEditor,AppControlAction.StartupGuide,AppControlAction.AdminCommands}),
            "Every map and tool button has a configurable app action");
        var defaults=new AppControlBindings();
        Check(defaults.BindingError(0x4D,0,0x54,0,0x6F,0)==null
            && defaults.Key(AppControlAction.ToggleOverlay)==0x23 && defaults.Key(AppControlAction.ZoomIn)==0x21
            && defaults.Key(AppControlAction.ZoomOut)==0x22 && defaults.Key(AppControlAction.DeathMarker)==0x28,
            "Existing show/hide, zoom and death-marker defaults remain valid");
        Check(AppControlBindings.AllActions.Skip(7).All(action=>defaults.Key(action)==0 && defaults.ScanCode(action)==0),
            "Additional app actions start unbound without conflicting with one another");
        var changed=defaults.Clone();changed.Set(AppControlAction.ZoomIn,0x77,0x42);
        Check(defaults.Key(AppControlAction.ZoomIn)==0x21 && changed.ScanCode(AppControlAction.ZoomIn)==0x42,
            "Editing a control draft preserves the active controls");
        changed.Set(AppControlAction.ZoomOut,0x78,0x42);
        Check(changed.BindingError(0x4D,0,0x54,0,0x6F,0)!=null,"Zoom actions cannot share a physical key across keyboard layouts");
        changed.Set(AppControlAction.ZoomOut,0x77,0x43);
        Check(changed.BindingError(0x4D,0,0x54,0,0x6F,0)==null,"Different physical keys can share a layout-specific virtual key");
        foreach(var action in new[]{AppControlAction.ToggleOverlay,AppControlAction.ZoomIn,AppControlAction.ZoomOut,AppControlAction.DeathMarker,AppControlAction.ResetZoom}) {
            var conflict=defaults.Clone();conflict.Set(action,0x51,0x32);
            Check(conflict.BindingError(0x4D,0x32,0x54,0x14,0x6F,0x135)!=null,
                "SCUM map physical binding is protected from "+action);
            conflict.Set(action,0x51,0x14);
            Check(conflict.BindingError(0x4D,0x32,0x54,0x14,0x6F,0x135)!=null,
                "SCUM chat physical binding is protected from "+action);
        }
        var copyConflict=defaults.Clone();copyConflict.Set(AppControlAction.DeathMarker,0x51,0x135);
        Check(copyConflict.BindingError(0x4D,0x32,0x54,0x14,0x6F,0x135)!=null,
            "Manual death-marker binding cannot reuse the physical coordinate-copy key");
        foreach(int key in new[]{0x09,0x0D,0x1B,0x12,0xA5,0x5B,0x5D,0xBF,0x6F}) {
            var reserved=defaults.Clone();reserved.Set(AppControlAction.AdminCommands,key,0);
            Check(reserved.BindingError(0x4D,0,0x54,0,0x70,0)!=null,"App controls reject reserved/chat-control key "+key);
        }
        var unsafeControls=defaults.Clone();
        unsafeControls.Set(AppControlAction.ToggleOverlay,0x54,0);
        unsafeControls.Set(AppControlAction.ZoomIn,0x77,0x42);
        unsafeControls.Set(AppControlAction.ZoomOut,0x78,0x42);
        unsafeControls.Set(AppControlAction.AdminCommands,0x12,0);
        unsafeControls.Sanitize(0x4D,0,0x54,0,0x6F,0);
        Check(unsafeControls.Key(AppControlAction.ToggleOverlay)==0 && unsafeControls.Key(AppControlAction.ZoomOut)==0
            && unsafeControls.Key(AppControlAction.AdminCommands)==0 && unsafeControls.Key(AppControlAction.ZoomIn)==0x77
            && unsafeControls.Key(AppControlAction.AddWaypoint)==0x2D && unsafeControls.BindingError(0x4D,0,0x54,0,0x6F,0)==null,
            "Unsafe saved bindings are disabled while unrelated valid preferences survive");
        changed.Set(AppControlAction.ZoomIn,0,0x42);
        Check(changed.Key(AppControlAction.ZoomIn)==0 && changed.ScanCode(AppControlAction.ZoomIn)==0,
            "Unbinding an action removes its previous physical scan code");
    }

    static void TestAppControlWizard() {
        var original=new AppControlBindings();AppControlBindings committed=null;int saves=0;
        using(var guide=new StartupGuideDialog(0x4D,0x54,0,0x6F,null,appControls:original,onSaveAppControls:bindings=>{committed=bindings;saves++;})) {
            SetField(guide,"currentSlide",2);Invoke(guide,"UpdateSlide");
            foreach(var action in AppControlBindings.AllActions) {
                Check(Descendants(guide).Any(control=>control.Name=="AppControlCapture"+action)
                    && Descendants(guide).Any(control=>control.Name=="AppControlUnbind"+action),
                    "Rebinding wizard exposes capture and unbind for "+action);
            }
            var capture=Descendants(guide).OfType<Button>().Single(button=>button.Name=="AppControlCaptureZoomIn");
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(capture,new object[]{EventArgs.Empty});
            PhysicalKeyCapture.Observe(0x77,0x42,0);
            Invoke(guide,"HandleFormKeyDown",guide,new KeyEventArgs(Keys.Control|Keys.F8));
            Check(Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0x21
                && Field<int>(guide,"captureTarget")!=0,"Wizard rejects modifier chords without changing the previous app binding");
            PhysicalKeyCapture.Observe(0x78,0x43,0);
            Invoke(guide,"HandleFormKeyDown",guide,new KeyEventArgs(Keys.F8));
            Check(Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0x21,
                "Wizard retains the previous app binding when the physical capture does not match");
            PhysicalKeyCapture.Observe(0x77,0x42,0);
            Invoke(guide,"HandleFormKeyDown",guide,new KeyEventArgs(Keys.F8));
            Check(Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0x77
                && Field<AppControlBindings>(guide,"appControls").ScanCode(AppControlAction.ZoomIn)==0x42
                && original.Key(AppControlAction.ZoomIn)==0x21 && saves==0,
                "Wizard captures zoom as a physical draft without prematurely changing active bindings");
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(Field<Button>(guide,"nextButton"),new object[]{EventArgs.Empty});
            Check(saves==0 && Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0x77,
                "Navigating away from controls retains a draft without saving it");
            Invoke(guide,"ShowKeyBindings");
            Check(Field<bool>(guide,"bindingEditorMode") && Field<int>(guide,"currentSlide")==2
                && Field<Button>(guide,"finishButton").Text==Localization.Get("KeyWizardSave")
                && Field<Label>(guide,"pageIndicator").Text=="",
                "Dedicated rebind wizard opens all controls with an immediate Save button");
            SetField(guide,"mapKey",0x77);SetField(guide,"mapScanCode",0x42);
            Check(!(bool)Invoke(guide,"CommitKeybindsIfApplicable",false) && saves==0,
                "Wizard cannot commit a SCUM map key that collides with a rebound zoom action");
            SetField(guide,"mapKey",0x4D);SetField(guide,"mapScanCode",0);
            Check((bool)Invoke(guide,"CommitKeybindsIfApplicable",false) && saves==1
                && committed.Key(AppControlAction.ZoomIn)==0x77 && committed.ScanCode(AppControlAction.ZoomIn)==0x42,
                "Wizard commits the complete app binding catalogue together");
            committed.Set(AppControlAction.ZoomIn,0x78,0x43);
            Check(Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0x77,
                "Saved wizard controls are isolated from later callback edits");
            var unbind=Descendants(guide).OfType<Button>().Single(button=>button.Name=="AppControlUnbindZoomIn");
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(unbind,new object[]{EventArgs.Empty});
            Check(Field<AppControlBindings>(guide,"appControls").Key(AppControlAction.ZoomIn)==0
                && Field<AppControlBindings>(guide,"appControls").ScanCode(AppControlAction.ZoomIn)==0,
                "Wizard unbind button clears the app key and its physical scan");
            var reset=Descendants(guide).OfType<Button>().Single(button=>button.Text==Localization.Get("WizardBtnResetDefaults"));
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(reset,new object[]{EventArgs.Empty});
            var restored=Field<AppControlBindings>(guide,"appControls");
            Check(AppControlBindings.AllActions.All(action=>restored.Key(action)==original.Key(action) && restored.ScanCode(action)==0)
                && Descendants(guide).Single(control=>control.Name=="AppControlValueZoomIn").Text==PhysicalKeyCapture.KeyName(0x21),
                "Wizard reset restores every app default and refreshes the displayed values");
        }
        int gameSaves=0,appSaves=0;AppControlBindings editorSaved=null;
        using(var editor=new StartupGuideDialog(0x4D,0x54,0,0x6F,
            (map,chat,modifier,copy,copyScan,mapScan,chatScan)=>gameSaves++,
            appControls:original,onSaveAppControls:bindings=>{appSaves++;editorSaved=bindings;})) {
            Invoke(editor,"ShowKeyBindings");Field<AppControlBindings>(editor,"appControls").Set(AppControlAction.ZoomIn,0x77,0x42);
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(Field<Button>(editor,"finishButton"),new object[]{EventArgs.Empty});
            Check(editor.DialogResult==DialogResult.OK && gameSaves==1 && appSaves==1
                && editorSaved.Key(AppControlAction.ZoomIn)==0x77 && editorSaved.ScanCode(AppControlAction.ZoomIn)==0x42,
                "Dedicated Save button commits SCUM and app controls before completing the dialog");
        }
        using(var cancelled=new StartupGuideDialog(0x4D,0x54,0,0x6F,
            (map,chat,modifier,copy,copyScan,mapScan,chatScan)=>gameSaves++,
            appControls:original,onSaveAppControls:bindings=>appSaves++)) {
            Invoke(cancelled,"ShowKeyBindings");Field<AppControlBindings>(cancelled,"appControls").Set(AppControlAction.ZoomIn,0x78,0x43);
            cancelled.Close();
            Check(gameSaves==1 && appSaves==1 && original.Key(AppControlAction.ZoomIn)==0x21,
                "Closing the dedicated rebind wizard discards its draft without saving either control set");
        }
    }

    static void TestAppControlProfile(string testRoot) {
        string profile=Path.Combine(testRoot,"controls");Directory.CreateDirectory(profile);
        string settings=Path.Combine(profile,"settings.ini");File.WriteAllText(settings,"Welcomed=True");
        using(var bitmap=new Bitmap(32,32)) bitmap.Save(Path.Combine(profile,"map.png"));
        var configured=new AppControlBindings();int[] scans={0x3B,0x3C,0x3D,0x3E,0x3F,0x40,0x41,0x42,0x43,0x44,0x57,0x58};
        for(int i=0;i<AppControlBindings.AllActions.Length;i++) configured.Set(AppControlBindings.AllActions[i],0x70+i,scans[i]);
        using(var window=new MapWindow(profile)) {
            foreach(var action in AppControlBindings.AllActions) {
                string title=Localization.Get(AppControlBindings.LabelKey(action))+":";
                Check(Descendants(window).OfType<Button>().Any(button=>button.Text.StartsWith(title,StringComparison.Ordinal)),
                    "Settings exposes rebinding for "+action);
            }
            Invoke(window,"ApplyAppControls",configured);
            configured.Set(AppControlAction.ZoomIn,0x21,0);
            Check(WindowControls(window).Key(AppControlAction.ZoomIn)==0x74,
                "Applying bindings takes a copy of the draft");
        }
        configured.Set(AppControlAction.ZoomIn,0x74,0x3F);
        using(var reopened=new MapWindow(profile,true)) {
            var loaded=WindowControls(reopened);
            Check(AppControlBindings.AllActions.All(action=>loaded.Key(action)==configured.Key(action)
                && loaded.ScanCode(action)==configured.ScanCode(action)),"All twelve app keys and physical scan codes survive restart");
            var watched=typeof(MapWindow).GetMethod("IsWatchedPhysicalKey",Hidden);
            Check(AppControlBindings.AllActions.All(action=>(bool)watched.Invoke(reopened,new object[]{0xE2,loaded.ScanCode(action)})),
                "Every rebound action reaches keyboard dispatch through its physical position");
            foreach(var action in AppControlBindings.AllActions) {
                object[] eventArguments={0xE2,loaded.ScanCode(action),default(AppControlAction)};
                Check((bool)Invoke(reopened,"TryGetAppControl",eventArguments) && (AppControlAction)eventArguments[2]==action,
                    "Runtime resolves the rebound physical key to "+action);
            }
            Check(new[]{0x24,0x2D,0x2E,0x23,0x21,0x22,0x28}.All(key=>!(bool)watched.Invoke(reopened,new object[]{key,0})),
                "Rebinding removes the old Home, Insert, Delete, End, zoom and death-marker shortcuts");
            Check((bool)Invoke(reopened,"MatchesAppControl",AppControlAction.ZoomIn,0xE2,0x3F)
                && !(bool)Invoke(reopened,"MatchesAppControl",AppControlAction.ZoomIn,0x74,0x56),
                "Runtime zoom matching follows physical positions across keyboard layouts");
            Check(!(bool)Invoke(reopened,"FallbackBindingPressed",0,0),"Polling an unbound app action is safely ignored");
            string beforeInvalidApply=File.ReadAllText(settings);bool rejected=false;
            var invalid=loaded.Clone();invalid.Set(AppControlAction.DeathMarker,loaded.Key(AppControlAction.ZoomIn),loaded.ScanCode(AppControlAction.ZoomIn));
            try { Invoke(reopened,"ApplyAppControls",invalid); }
            catch(TargetInvocationException exception) { rejected=exception.InnerException is ArgumentException; }
            Check(rejected && WindowControls(reopened).Key(AppControlAction.DeathMarker)==loaded.Key(AppControlAction.DeathMarker)
                && File.ReadAllText(settings)==beforeInvalidApply,"Applying conflicting controls preserves the active and saved configuration");
            float before=Field<float>(reopened,"zoom");
            Invoke(reopened,"DispatchAppControl",AppControlAction.ZoomIn);
            Check(Field<float>(reopened,"zoom")==before,"Queued app actions remain disabled in diagnostic mode");
            foreach(string blocked in new[]{"panelOpening","searchOpen","inventoryInputLocked","shortcutCaptureOpen","adminCommandBusy"}) {
                SetField(reopened,blocked,true);Invoke(reopened,"DispatchAppControl",AppControlAction.ZoomIn);
                Check(Field<float>(reopened,"zoom")==before,"Queued app actions cannot bypass "+blocked);
                SetField(reopened,blocked,false);
            }
            SetField(reopened,"zoom",4f);SetField(reopened,"targetZoom",4f);SetField(reopened,"autoZoom",true);
            SetField(reopened,"zoomStepPercent",25);SetField(reopened,"lastPgUpAction",DateTime.MinValue);
            Invoke(reopened,"TriggerZoomIn");
            Check(Math.Abs(Field<float>(reopened,"zoom")-5f)<.001f && !Field<bool>(reopened,"autoZoom")
                && Field<float>(reopened,"targetZoom")==Field<float>(reopened,"zoom"),
                "Compact zoom uses the configured step and switches to manual zoom");
            SetField(reopened,"fullMapActive",true);SetField(reopened,"fullMapZoom",2f);
            SetField(reopened,"lastPgUpAction",DateTime.MinValue);Invoke(reopened,"TriggerZoomIn");
            Check(Field<float>(reopened,"fullMapZoom")>2f && Math.Abs(Field<float>(reopened,"zoom")-5f)<.001f,
                "Expanded-map zoom changes the active full map while preserving compact zoom");
            bool previousCopyBusy=Native.CopyInProgress;
            try {
                // Cancelling a copy releases its keys immediately, while its async delay
                // keeps CopyInProgress true until the final cleanup continuation runs.
                Native.CopyInProgress=true;SetField(reopened,"diagnosticMode",false);
                SetField(reopened,"fullMapZoom",2f);SetField(reopened,"lastPgUpAction",DateTime.MinValue);
                Invoke(reopened,"TriggerZoomIn");
                Check(Math.Abs(Field<float>(reopened,"fullMapZoom")-2.5f)<.001f
                    && Math.Abs(Field<float>(reopened,"zoom")-5f)<.001f,
                    "Expanded zoom-in survives coordinate-copy cleanup after physical cancellation");
                SetField(reopened,"lastPgDnAction",DateTime.MinValue);Invoke(reopened,"TriggerZoomOut");
                Check(Math.Abs(Field<float>(reopened,"fullMapZoom")-2f)<.001f
                    && Math.Abs(Field<float>(reopened,"zoom")-5f)<.001f,
                    "Expanded zoom-out survives coordinate-copy cleanup after physical cancellation");
            } finally { Native.CopyInProgress=previousCopyBusy;SetField(reopened,"diagnosticMode",true); }
            SetField(reopened,"fullMapPan",new PointF(.2f,.7f));Invoke(reopened,"ResetMapZoom");
            Check(Field<float>(reopened,"fullMapZoom")==1f && Field<PointF>(reopened,"fullMapPan")==new PointF(.5f,.5f),
                "Expanded-map reset restores one-times zoom and the map centre");
            SetField(reopened,"fullMapActive",false);Invoke(reopened,"ResetMapZoom");
            Check(Field<float>(reopened,"zoom")==1f && Field<float>(reopened,"targetZoom")==1f,
                "Compact-map reset restores one-times zoom without leaving a stale animation target");
            SetField(reopened,"searchTarget",new MapZone{Name="Clear control target",Points=new[]{P(100,100)}});
            SetField(reopened,"voiceTarget",Field<MapZone>(reopened,"searchTarget"));
            SetField(reopened,"activeRoute",Route(P(0,0),P(100,100)));Invoke(reopened,"ClearActiveWaypoint");
            Check(Field<MapZone>(reopened,"searchTarget")==null && Field<MapZone>(reopened,"voiceTarget")==null
                && Field<RoadRoute>(reopened,"activeRoute")==null,"Clear waypoint removes navigation and voice guidance together");
        }
        configured.Set(AppControlAction.ZoomIn,0,0);
        using(var writer=new MapWindow(profile)) Invoke(writer,"ApplyAppControls",configured);
        using(var unbound=new MapWindow(profile,true)) {
            Check(WindowControls(unbound).Key(AppControlAction.ZoomIn)==0 && WindowControls(unbound).ScanCode(AppControlAction.ZoomIn)==0
                && !(bool)Invoke(unbound,"IsWatchedPhysicalKey",0x74,0x3F),"An explicitly unbound zoom action remains inactive after restart");
            File.WriteAllText(settings,"Welcomed=True");Invoke(unbound,"LoadSettings");
            Check(WindowControls(unbound).Key(AppControlAction.ZoomIn)==0x21 && WindowControls(unbound).ScanCode(AppControlAction.ZoomIn)==0
                && WindowControls(unbound).Key(AppControlAction.ResetZoom)==0,"Loading an old profile clears stale extended control preferences");
        }
        File.WriteAllLines(settings,new[]{"Welcomed=True","ScumMapKey=35","AppControlToggleOverlayKey=35",
            "AppControlZoomInKey=84","AppControlZoomOutKey=119","AppControlZoomOutScanCode=66",
            "AppControlResetZoomKey=120","AppControlResetZoomScanCode=66","AppControlAdminCommandsKey=191"});
        using(var repaired=new MapWindow(profile,true)) {
            var loaded=WindowControls(repaired);
            Check(loaded.Key(AppControlAction.ToggleOverlay)==0 && loaded.Key(AppControlAction.ZoomIn)==0
                && loaded.Key(AppControlAction.ResetZoom)==0 && loaded.Key(AppControlAction.AdminCommands)==0
                && loaded.Key(AppControlAction.ZoomOut)==119 && loaded.Key(AppControlAction.AddWaypoint)==0x2D
                && loaded.BindingError(0x23,0,0x54,0,0x6F,0)==null,
                "Profile loading fails closed for SCUM collisions, duplicate physical controls and reserved chat aliases");
        }
    }
    static void Pump(VoicePlayer player) {
        DateTime until=DateTime.UtcNow.AddSeconds(35);
        while(player.Busy && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(10); }
        Check(!player.Busy && player.LastError==null,"MP3 sequence completed without decoder errors");
    }
    [STAThread] static void Main() {
        Check(MapWindow.BuildMapTeleportCommand(new PointF(0,0))=="#Teleport 617718 618618 0"
            && MapWindow.BuildMapTeleportCommand(new PointF(1,1))=="#Teleport -903900 -905000 0",
            "Map teleport converts both map corners to world coordinates");
        var teleportPoint=MapWindow.ToMap(new Position { X=12345,Y=-54321 });
        string[] teleportFields=MapWindow.BuildMapTeleportCommand(teleportPoint).Split(' ');
        Check(Math.Abs(double.Parse(teleportFields[1],System.Globalization.CultureInfo.InvariantCulture)-12345)<.1
            && Math.Abs(double.Parse(teleportFields[2],System.Globalization.CultureInfo.InvariantCulture)+54321)<.1,
            "Map teleport round-trips a known world position");
        Check(MapWindow.BuildMapTeleportCommand(new PointF(float.NaN,0))==null
            && MapWindow.BuildMapTeleportCommand(new PointF(1.1f,0))==null,
            "Map teleport rejects invalid and outside-map points");
        var queuedChat=new ChatState();long queuedRevision=queuedChat.Revision;
        Check(queuedChat.AllowsQueuedShortcut(queuedRevision),"A current map shortcut can dispatch outside chat");
        queuedChat.Key(0x54);Check(!queuedChat.AllowsQueuedShortcut(queuedRevision),"Chat opening invalidates a queued M shortcut");
        queuedChat.Key(0x0D);Check(!queuedChat.AllowsQueuedShortcut(queuedRevision),"Closing chat cannot revive an old queued M shortcut");
        Check(queuedChat.AllowsQueuedShortcut(queuedChat.Revision),"A new shortcut works after chat closes");
        Check(GameKeys.ShortcutEventFocused(false,true,()=>true),"First chat/map key after focus return is checked against the actual game window");
        Check(!GameKeys.ShortcutEventFocused(false,true,()=>false),"Desktop chat/map keys do not acquire game focus");
        Check(AdminCommand.Valid("#SetGodMode true") && !AdminCommand.Valid("#SetGodMode true\n#Announce test") && !AdminCommand.Valid("hello"),"Admin buttons require a single command without control characters");
        string adminPath=Path.Combine(Path.GetTempPath(),"minimap-admin-"+Guid.NewGuid().ToString("N"),"buttons.tsv");
        var adminSaved=AdminCommand.Defaults();adminSaved.Add(new AdminCommand("Annonce française","#Announce Café"));
        AdminCommand.Save(adminPath,adminSaved);var loaded=AdminCommand.Load(adminPath);
        Check(loaded.Count==4 && loaded[3].Command==adminSaved[3].Command,"Admin command presets and Unicode custom buttons round-trip");
        var once=new AdminCommand("Spawn items","#SpawnItem") {Quantity="3",ExtraStrings="\"Test item\""};
        Check(once.Build(true)=="#SpawnItem 3 \"Test item\"" && once.Build(false)==once.Build(true),"Non-boolean commands append quantity and extra text without true/false");
        var booleanCommand=new AdminCommand("Boolean","#Example",true) {Quantity="2",ExtraStrings="target"};
        Check(booleanCommand.Build(true)=="#Example true 2 target" && booleanCommand.Build(false)=="#Example false 2 target","Boolean commands compose their value before optional arguments");
        var embeddedBoolean=new AdminCommand("Boolean","#Example TRUE false TrUe",true) {Quantity="2",ExtraStrings="target"};
        Check(embeddedBoolean.Build(true)=="#Example true 2 target" && embeddedBoolean.Build(false)=="#Example false 2 target"
            && embeddedBoolean.StateKey==booleanCommand.StateKey,
            "Boolean command construction removes repeated saved values before adding the requested value");
        var literalBoolean=new AdminCommand("Announcement","#Announce false");
        Check(literalBoolean.Build(true)=="#Announce false" && literalBoolean.Build(false)==literalBoolean.Build(true),
            "Non-boolean command text ending in a boolean literal stays intact");
        AdminCommand.Save(adminPath,new List<AdminCommand>{once,booleanCommand});var configured=AdminCommand.Load(adminPath);
        Check(!configured[0].IsBoolean && configured[0].Quantity=="3" && configured[0].ExtraStrings==once.ExtraStrings && configured[1].IsBoolean,"Boolean, quantity and extra-string fields persist independently");
        Func<string,string> encodeAdminField=value=>Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        File.WriteAllLines(adminPath,new[]{
            encodeAdminField(embeddedBoolean.Name)+"\t"+encodeAdminField(embeddedBoolean.Command)+"\t1\t"+encodeAdminField("2")+"\t"+encodeAdminField("target"),
            encodeAdminField(literalBoolean.Name)+"\t"+encodeAdminField(literalBoolean.Command)+"\t0\t\t"
        });
        var normalizedSaved=AdminCommand.Load(adminPath);
        Check(normalizedSaved[0].Command=="#Example" && normalizedSaved[0].Build(false)=="#Example false 2 target"
            && normalizedSaved[1].Command=="#Announce false" && !normalizedSaved[1].IsBoolean,
            "Five-field saved presets normalize boolean values without altering one-shot commands or optional arguments");
        AdminCommand.Save(adminPath,normalizedSaved);
        Check(AdminCommand.Load(adminPath)[0].Build(false)=="#Example false 2 target",
            "Normalized boolean presets retain an explicit OFF request after saving and reloading");
        once.Quantity="invalid";Check(once.ValidationError()!=null,"Non-numeric quantities are rejected");once.Quantity="";once.ExtraStrings="test\n#Example";Check(once.ValidationError()!=null,"Extra strings cannot inject another chat line");
        using(var editor=new AdminCommandEditor(new AdminCommand("","#"))) {
            Check(!editor.Controls.OfType<CheckBox>().Single().Checked && editor.Controls.OfType<TextBox>().Count()==4,"Add-command editor starts non-boolean and exposes all requested fields");
            ((TextBox)editor.Controls["Label"]).Text="Example";((TextBox)editor.Controls["Command"]).Text="#Example";
            ((TextBox)editor.Controls["Quantity"]).Text="5";((TextBox)editor.Controls["ExtraStrings"]).Text="target";
            ((CheckBox)editor.Controls["IsBoolean"]).Checked=true;
            typeof(Button).GetMethod("OnClick",Hidden).Invoke(editor.Controls["Save"],new object[]{EventArgs.Empty});
            Check(editor.SavedCommand.IsBoolean && editor.SavedCommand.Build(true)=="#Example true 5 target","Editor commits boolean and optional argument fields together");
        }
        var migrated=AdminCommand.Toggles(new List<AdminCommand>{new AdminCommand("God mode ON","#SetGodMode true"),new AdminCommand("God mode OFF","#SetGodMode false")});
        Check(migrated.Count==1 && migrated[0].Name=="God mode" && migrated[0].Command=="#SetGodMode","Legacy true/false command pairs migrate into a single toggle");
        AdminCommand.Save(adminPath,new List<AdminCommand>());Check(AdminCommand.Load(adminPath).Count==0,"An intentionally empty admin button list stays empty");
        File.Delete(adminPath);Directory.Delete(Path.GetDirectoryName(adminPath));
        for(int interrupted=0;interrupted<9;interrupted++) {
            var adminEvents=new List<string>();bool allowed=true;int stage=0;bool opened=false;int totalDelay=0;
            bool result=Native.AdminChatSequence((key,up)=> { adminEvents.Add(key+":"+up);return true; },ms=>{totalDelay+=ms;if(++stage==interrupted)allowed=false;return Task.FromResult(0);},()=>allowed,()=>true,()=>opened=true,0x54,0x4D).GetAwaiter().GetResult();
            var held=new Dictionary<string,int>();foreach(string evt in adminEvents) {var p=evt.Split(':');int balance;held.TryGetValue(p[0],out balance);held[p[0]]=balance+(p[1]=="False"?1:-1);}
            Check(held.Values.All(n=>n==0),"Admin send releases its keys at interruption phase "+interrupted);
            if(interrupted==0) {
                Check(result && opened && adminEvents.Contains("65:False") && adminEvents.IndexOf("27:False")>adminEvents.IndexOf("13:True"),"Admin send submits with Enter before closing chat with Escape");
                Check(totalDelay<750,"Admin chat sequence including map closure uses under 750 ms of delays");
            }
            else Check(!result && !adminEvents.Contains("13:False"),"Admin send stops before Enter at interruption phase "+interrupted);
        }
        var cleanupEvents=new List<uint>();bool cleanupAllowed=true,commandSubmitted=false;int cleanupPhase=0;
        bool cleanupResult=Native.AdminChatSequence((key,up)=>{if(!up)cleanupEvents.Add(key);return true;},ms=>{if(++cleanupPhase==10)cleanupAllowed=false;return Task.FromResult(0);},()=>cleanupAllowed,()=>true,()=>{},0x54,0x4D,()=>commandSubmitted=true).GetAwaiter().GetResult();
        Check(commandSubmitted && !cleanupResult && !cleanupEvents.Contains(0x1B),"Input interruption after Enter preserves submitted status and prevents Escape in another context");
        foreach(int mapKey in new[]{0x4D,0}) {
            int enterDelayPhase=mapKey==0x4D?9:7;
            var enterInterruptedEvents=new List<string>();bool enterAllowed=true,enterSubmitted=false,submittedBeforeInterruption=false;int enterPhase=0;
            bool enterInterruptedResult=Native.AdminChatSequence((key,up)=> {enterInterruptedEvents.Add(key+":"+up);return true;},ms=> {
                if(++enterPhase==enterDelayPhase) {submittedBeforeInterruption=enterSubmitted;enterAllowed=false;}
                return Task.FromResult(0);
            },()=>enterAllowed,()=>true,()=>{},0x54,mapKey,()=>enterSubmitted=true).GetAwaiter().GetResult();
            Check(enterSubmitted && submittedBeforeInterruption && !enterInterruptedResult
                && enterInterruptedEvents.Count(evt=>evt=="13:False")==1 && enterInterruptedEvents.Count(evt=>evt=="13:True")==1
                && !enterInterruptedEvents.Contains("27:False")
                && enterInterruptedEvents.GroupBy(evt=>evt.Split(':')[0]).All(group=>group.Count(evt=>evt.EndsWith(":False"))==group.Count(evt=>evt.EndsWith(":True"))),
                "Enter-down records submission before interruption and releases keys without Escape: map key "+mapKey);
        }
        bool rejectedEnterSubmitted=false;int rejectedEnterDownAttempts=0;var rejectedEnterEvents=new List<string>();
        bool rejectedEnterResult=Native.AdminChatSequence((key,up)=> {
            if(key==0x0D && !up) {rejectedEnterDownAttempts++;return false;}
            rejectedEnterEvents.Add(key+":"+up);return true;
        },ms=>Task.FromResult(0),()=>true,()=>true,()=>{},0x54,0,()=>rejectedEnterSubmitted=true).GetAwaiter().GetResult();
        Check(!rejectedEnterResult && !rejectedEnterSubmitted && rejectedEnterDownAttempts==1
            && !rejectedEnterEvents.Contains("13:False") && !rejectedEnterEvents.Contains("27:False")
            && rejectedEnterEvents.GroupBy(evt=>evt.Split(':')[0]).All(group=>group.Count(evt=>evt.EndsWith(":False"))==group.Count(evt=>evt.EndsWith(":True"))),
            "A rejected Enter-down does not claim submission and releases the previously injected keys");
        bool failedEnterReleaseSubmitted=false;int enterReleaseAttempts=0;var failedEnterReleaseEvents=new List<string>();
        bool failedEnterReleaseResult=Native.AdminChatSequence((key,up)=> {
            if(key==0x0D && up && ++enterReleaseAttempts<=2)return false;
            failedEnterReleaseEvents.Add(key+":"+up);return true;
        },ms=>Task.FromResult(0),()=>true,()=>true,()=>{},0x54,0,()=>failedEnterReleaseSubmitted=true).GetAwaiter().GetResult();
        Check(!failedEnterReleaseResult && failedEnterReleaseSubmitted && enterReleaseAttempts==3
            && failedEnterReleaseEvents.Count(evt=>evt=="13:False")==1 && failedEnterReleaseEvents.Count(evt=>evt=="13:True")==1
            && !failedEnterReleaseEvents.Contains("27:False")
            && failedEnterReleaseEvents.GroupBy(evt=>evt.Split(':')[0]).All(group=>group.Count(evt=>evt.EndsWith(":False"))==group.Count(evt=>evt.EndsWith(":True"))),
            "An accepted Enter-down stays submitted when key-up fails and final cleanup retries its release");
        var replacedEvents=new List<uint>();
        bool replaced=Native.AdminChatSequence((key,up)=>{if(!up)replacedEvents.Add(key);return true;},ms=>Task.FromResult(0),()=>true,()=>false,()=>{},0x54,0).GetAwaiter().GetResult();
        Check(!replaced && !replacedEvents.Contains(0x56) && !replacedEvents.Contains(0x0D),"Clipboard replacement cancels admin paste and submission");
        foreach(int modifier in new[]{0,0xA2,0xA4}) foreach(int interruption in new[]{30,60,80}) {
            var events=new List<string>();
            var lease=new CopyInputLease((key,up)=>{ events.Add(key+":"+up); return true; });
            Native.CopyChord(lease.Send,ms=>{ if(ms==interruption) lease.Cancel(); return Task.FromResult(0); },()=>!lease.Cancelled,modifier,0x43).GetAwaiter().GetResult();
            lease.Cancel();
            Check(!lease.Send(67,false),"Interruption blocks further key-downs: modifier "+modifier+", phase "+interruption);
            var balance=new Dictionary<string,int>();
            foreach(string evt in events) { string[] parts=evt.Split(':'); int value; balance.TryGetValue(parts[0],out value); balance[parts[0]]=value+(parts[1]=="False"?1:-1); }
            Check(balance.Values.All(value=>value==0),"Each injected key released exactly once: modifier "+modifier+", phase "+interruption);
            if(modifier>0 && interruption==30) Check(events[events.Count-2]=="67:True" && events[events.Count-1]==modifier+":True","Copy key released before modifier on wheel interruption");
        }
        var chat=new ChatState(); chat.Key(0x54);
        typeof(ChatState).GetField("openTime",Hidden).SetValue(chat,DateTime.UtcNow.AddMinutes(-5));
        Check(chat.Paused,"Idle chat remains protected beyond 25 seconds");
        chat.Key(0x09); chat.Key(0x4D); Check(chat.Paused,"Tab and typed M leave chat protection active");
        chat.Key(0x0D); Check(!chat.Paused,"Enter ends chat protection");
        chat.Key(0x54); chat.Key(0x1B); Check(!chat.Paused,"Esc ends chat protection");
        var localizedChat=new ChatState();
        var localizedChatMonitor=new ChatKeyMonitor();
        Check(localizedChatMonitor.Observe(0x51,true,true,localizedChat,0x4D,0,0x135,0x135)
            && localizedChat.Paused,
            "Configured chat key is detected from its physical scan code across keyboard layouts");
        var otherLocalizedChat=new ChatState();
        var otherLocalizedChatMonitor=new ChatKeyMonitor();
        Check(otherLocalizedChatMonitor.Observe(0x51,true,true,otherLocalizedChat,0x4D,0,0x35,0x135)
            && !otherLocalizedChat.Paused,
            "A different physical key does not open localized chat protection");
        var physical=new PhysicalKeyTransitions();
        physical.Update(0x4D,true); physical.Resync(key=>false);
        Check(physical.Update(0x4D,true),"Missed M key-up is reconciled so the next press opens the map");
        physical.Resync(key=>key==0x4D);
        Check(!physical.Update(0x4D,true),"A genuinely held M key is not retriggered by reconciliation");
        for(int cycle=0;cycle<10000;cycle++) {
            physical.Resync(key=>false);
            if(!physical.Update(0x4D,true)) throw new Exception("Map shortcut remained stuck during repeated missed releases.");
        }
        Check(true,"Map shortcut rearms across 10,000 missed-release cycles");
        chat.Key(0x54);
        using(var hooks=new GameKeys((key,scan)=>{},(key,scan)=>true,chat,()=>0x54)) {
            typeof(GameKeys).GetMethod("RefreshHooks",Hidden).Invoke(hooks,null);
            Check(hooks.Available && chat.Paused,"Hook renewal restores monitoring without clearing chat protection");
        }
        Check(Program.TrackingIntervalMs(250,0xA2)==1000 && Program.TrackingIntervalMs(250,0)==250 && Program.TrackingIntervalMs(3000,0xA2)==3000,"Modifier shortcuts use at most one copy per second; single-key tracking retains 250 ms");
        Check(Program.CopyResponseTimeoutMs(0)==350 && Program.CopyResponseTimeoutMs(0xA2)==1000 && Program.CopyRetryDelayMs(0)==1000,"Single-key misses no longer introduce a one-second response wait or five-second retry pause");
        var singleKeyDelays=new List<int>();
        Native.CopyChord((key,up)=>true,ms=>{ singleKeyDelays.Add(ms); return Task.FromResult(0); },()=>true,0,Program.DefaultCopyKey).GetAwaiter().GetResult();
        Check(singleKeyDelays.SequenceEqual(new[]{60}),"Single-key copy spans game frames without the redundant post-release delay");
        PhysicalKeyCapture.Observe(0xE2,0x56,0);
        int physicalScan;
        Check(PhysicalKeyCapture.TryGet(0xE2,out physicalScan) && physicalScan==0x56 &&
            !PhysicalKeyCapture.TryGet(0xDC,out physicalScan),"ABNT2 physical copy key is captured independently of the US backslash virtual key");
        PhysicalKeyCapture.Observe(0xE2,0x35,1);
        Check(PhysicalKeyCapture.TryGet(0xE2,out physicalScan) && physicalScan==0x135,
            "Extended physical key status survives capture");
        Check(PhysicalKeyCapture.Matches(0x51,0x135,0x4D,0x135)
            && !PhysicalKeyCapture.Matches(0x51,0x35,0x4D,0x135)
            && PhysicalKeyCapture.Matches(0x51,0,0x51,0x135),
            "Captured map/chat bindings match hardware scan codes across layout VK changes, with a VK fallback when the hook has no scan");
        Check(Program.DefaultCopyKey==0x6F &&
            PhysicalKeyCapture.Valid(0x135),
            "Default copy binding is the layout-independent NumPad Divide key and extended physical scan codes are supported");
        var nativeType=typeof(Native);
        var hiddenStatic=BindingFlags.NonPublic|BindingFlags.Static;
        IntPtr keyboardLayout=(IntPtr)nativeType.GetMethod("GetKeyboardLayout",hiddenStatic).Invoke(null,new object[]{(uint)0});
        uint localScan=(uint)nativeType.GetMethod("MapVirtualKeyEx",hiddenStatic).Invoke(null,new object[]{(uint)Program.DefaultCopyKey,(uint)4,keyboardLayout});
        Check(keyboardLayout!=IntPtr.Zero && (localScan&0xFF)==0x35 && (localScan&0xFF00)!=0,
            "Windows maps NumPad Divide to its extended physical scan code independently of the text layout");
        Check((int)nativeType.GetMethod("ScanCodeToVirtualKey",hiddenStatic).Invoke(null,new object[]{0x135})==0x6F,
            "Hook-loss polling converts the captured NumPad Divide scan code back to its physical virtual key");
        Check(!GameKeys.MouseActionBlocksCopy(0x20A,0) && !GameKeys.MouseActionBlocksCopy(0x20E,0) && GameKeys.MouseActionBlocksCopy(0x20A,0xA2),"Single-key tracking continues through scrolling; modifier-based copying remains guarded");
        using(var overlayInput=new OverlayWindow(()=>{},()=>{},delta=>{},()=>{})) {
            var activation=typeof(OverlayWindow).GetProperty("ShowWithoutActivation",Hidden);
            overlayInput.FullMapMode=true;
            Check((bool)activation.GetValue(overlayInput,null),"Expanded map leaves keyboard focus with SCUM for the underlying map");
            int mouseActions=0,cancelledDrags=0;
            overlayInput.OnFullMapMouseDown=(point,button)=>mouseActions++;
            overlayInput.OnFullMapMouseUp=(point,button)=>mouseActions++;
            overlayInput.OnFullMapMouseMove=(point,button)=>mouseActions++;
            overlayInput.OnFullMapWheel=(delta,point)=>mouseActions++;
            overlayInput.OnFullMapClickThrough=()=>cancelledDrags++;
            overlayInput.UpdateFullMapClickThrough(false,true);
            Check(!overlayInput.FullMapClickThrough && (WindowStyle(overlayInput.Handle,-20)&0x20)==0,
                "Expanded map receives mouse input until Ctrl is held");
            overlayInput.Show();
            Check(IsWindowVisible(overlayInput.Handle),"Expanded overlay has a visible native window before Ctrl");
            overlayInput.UpdateFullMapClickThrough(true,true);
            Check(overlayInput.FullMapClickThrough && (WindowStyle(overlayInput.Handle,-20)&0x20)!=0 &&
                (WindowStyle(overlayInput.Handle,-20)&0x8000000)!=0 && cancelledDrags==1,
                "Ctrl enables native layered-window click-through, cancels drags and keeps game keyboard focus");
            Check(!overlayInput.Visible && !IsWindowVisible(overlayInput.Handle),
                "Ctrl removes the overlay window from native mouse targeting and reveals SCUM's map");
            overlayInput.Show();
            Check(!overlayInput.Visible && !IsWindowVisible(overlayInput.Handle),
                "Focus-return Show cannot obstruct the game while Ctrl remains held");
            foreach(string evt in new[]{"OnMouseDown","OnMouseMove","OnMouseUp"})
                typeof(Control).GetMethod(evt,Hidden).Invoke(overlayInput,new object[]{new MouseEventArgs(MouseButtons.Left,1,30,30,0)});
            typeof(Control).GetMethod("OnMouseWheel",Hidden).Invoke(overlayInput,new object[]{new MouseEventArgs(MouseButtons.None,0,30,30,120)});
            object[] hitArguments={Message.Create(overlayInput.Handle,0x84,IntPtr.Zero,IntPtr.Zero)};
            typeof(OverlayWindow).GetMethod("WndProc",Hidden).Invoke(overlayInput,hitArguments);
            Check(mouseActions==0 && ((Message)hitArguments[0]).Result==new IntPtr(-1),
                "Ctrl passes hit testing through and suppresses MiniMap mouse actions");
            overlayInput.UpdateFullMapClickThrough(false,true);
            typeof(Control).GetMethod("OnMouseDown",Hidden).Invoke(overlayInput,new object[]{new MouseEventArgs(MouseButtons.Left,1,30,30,0)});
            Check(!overlayInput.FullMapClickThrough && (WindowStyle(overlayInput.Handle,-20)&0x20)==0 && mouseActions==1,
                "Releasing Ctrl restores expanded-map mouse interaction");
            Check(overlayInput.Visible && IsWindowVisible(overlayInput.Handle),"Releasing Ctrl restores native overlay visibility");
            overlayInput.UpdateFullMapClickThrough(true,true);
            overlayInput.UpdateFullMapClickThrough(true,false);
            Check(!overlayInput.FullMapClickThrough,"Focus loss clears temporary full-map click-through");
            Check(!IsWindowVisible(overlayInput.Handle),"Focus loss does not restore the hidden overlay over another application");
            overlayInput.UpdateFullMapClickThrough(false,true);
            Check(IsWindowVisible(overlayInput.Handle),"Returning to SCUM restores the overlay after Ctrl was released out of focus");
            overlayInput.UpdateFullMapClickThrough(true,true);
            overlayInput.FullMapMode=false;
            overlayInput.UpdateFullMapClickThrough(true,false);
            Check(!overlayInput.FullMapClickThrough,"Closing the expanded map clears Ctrl mode");
            Check((bool)activation.GetValue(overlayInput,null),"Compact overlay stays non-activating");
            overlayInput.Hide();
            overlayInput.UpdateFullMapClickThrough(false,true);
            Check(!IsWindowVisible(overlayInput.Handle),"An intentionally hidden overlay stays hidden");
        }
        using(var sharedMapKeys=new GameKeys((key,scan)=>{},(key,scan)=>key==0x4D || key==0x24,
            sharedGameBinding:(key,scan)=>PhysicalKeyCapture.Matches(key,scan,0x4D,0))) {
            Check(!sharedMapKeys.ConsumesBinding(0x4D,0),"Map binding reaches SCUM as well as MiniMap");
            Check(sharedMapKeys.ConsumesBinding(0x24,0),"App-only Settings binding stays consumed");
        }
        using(var physicalMapKeys=new GameKeys((key,scan)=>{},(key,scan)=>true,
            sharedGameBinding:(key,scan)=>PhysicalKeyCapture.Matches(key,scan,0x4D,0x32))) {
            Check(!physicalMapKeys.ConsumesBinding(0xBA,0x32),"Captured map key reaches SCUM across keyboard layouts");
            Check(physicalMapKeys.ConsumesBinding(0x4D,0x31),"A different physical key is not mistaken for the shared map binding");
        }
        TestAppControlModel();
        TestAppControlWizard();
        var navigator=new VoiceNavigator();
        var right=Route(P(0,0),P(600,0),P(600,600));
        var left=Route(P(0,0),P(600,0),P(600,-600));
        Check(navigator.NextCue(right,P(500,0)).Action==VoicePacks.Clips[6],"East to south is turn right on map coordinates");
        navigator.Reset();
        Check(navigator.NextCue(left,P(500,0)).Action==VoicePacks.Clips[5],"East to north is turn left on map coordinates");
        Check(VoiceNavigator.Turn(-30)==VoicePacks.Clips[5] && VoiceNavigator.Turn(30)==VoicePacks.Clips[6],"Shallow junction decisions use turn instructions instead of unsupported keep instructions");
        Check(VoiceNavigator.Turn(165)==VoicePacks.Clips[9],"Sharp reversal uses U-turn clip");
        navigator.Reset(); DateTime now=DateTime.UtcNow;
        string[] clips=navigator.Update(left,P(500,0),now);
        Check(string.Join(",",clips)=="11_in.mp3,03_100_meters.mp3,06_turn_left.mp3","100 metre turn composes clips in spoken order");
        Check(navigator.Update(left,P(500,0),now.AddSeconds(10))==null,"Stationary position does not repeat the same turn prompt");
        var recalculated=Route(P(490,0),P(600,0),P(600,-600));
        Check(navigator.Update(recalculated,P(500,0),now.AddSeconds(20))==null,"Routine reroute does not repeat an announced turn");
        Check(navigator.Update(recalculated,P(550,0),now.AddSeconds(30))[1]==VoicePacks.Clips[3],"Approaching turn advances to 50 metre prompt");
        Check(navigator.Update(recalculated,P(585,0),now.AddSeconds(40)).SequenceEqual(new[]{VoicePacks.Clips[5]}),"Immediate turn uses the action clip alone");
        navigator.Reset();
        Check(navigator.Update(left,P(420,0),now)==null,"Do not announce 250 metres when the turn is 180 metres away");
        Check(navigator.NextCue(left,P(500,100))==null,"Off-route location suppresses outdated turn instructions");
        left.Success=false;
        Check(navigator.NextCue(left,P(500,0))==null,"Unavailable routes do not receive road-turn instructions");
        navigator.Reset();
        var bend=Route(P(0,0),P(300,-100),P(600,-500)); bend.JunctionIndices=new int[0];
        Check(navigator.NextCue(bend,P(0,0)).Action==VoicePacks.Clips[4],"A curved road without branch nodes produces no turn or keep prompts");
        var roundedT=Route(P(0,0),P(580,0),P(600,-20),P(600,-600)); roundedT.JunctionIndices=new[]{2};
        Check(navigator.NextCue(roundedT,P(500,0)).Action==VoicePacks.Clips[5],"Rounded T-junction uses the approach and departure to announce turn left");
        navigator.Reset();
        var crossing=Route(P(0,0),P(400,0),P(400,400),P(200,400),P(200,-100));
        navigator.NextCue(crossing,P(150,0));
        VoiceCue crossingCue=navigator.NextCue(crossing,P(200,0));
        Check(crossingCue!=null && Math.Abs(crossingCue.Distance-200)<1,"Crossing route sections keep the current progress and next junction");
        navigator.Reset();
        var parallel=Route(P(0,0),P(400,0),P(400,70),P(0,70));
        navigator.Update(parallel,P(100,0),now);
        navigator.Update(parallel,P(100,36),now.AddSeconds(1));
        navigator.Update(parallel,P(102,36),now.AddSeconds(2));
        navigator.Update(parallel,P(104,36),now.AddSeconds(3));
        Check(!navigator.NeedsReroute && navigator.CurrentCue!=null,"Parallel route sections use the closest segment for deviation checks");
        navigator.Reset();
        var straight=Route(P(0,0),P(1000,0));
        navigator.Update(straight,P(600,0),now);
        navigator.Update(straight,P(594,0),now.AddSeconds(.5),true);
        string[] reverse=navigator.Update(straight,P(588,0),now.AddSeconds(1),true);
        Check(navigator.WrongWay && navigator.NeedsReroute && reverse.SequenceEqual(new[]{VoicePacks.Clips[9]}),"Sustained reverse travel interrupts speech with U-turn and requests recalculation");
        Check(navigator.Update(straight,P(582,0),now.AddSeconds(1.5),true)==null,"Wrong-way warning is not repeated every position sample");
        navigator.Update(straight,P(590,0),now.AddSeconds(2));
        Check(!navigator.WrongWay,"Moving along the route clears wrong-way state");
        navigator.Update(straight,P(590,45),now.AddSeconds(3));
        Check(!navigator.NeedsReroute,"A single off-route sample does not interrupt guidance");
        navigator.Update(straight,P(590,45),now.AddSeconds(4));
        Check(!navigator.NeedsReroute,"Timer ticks do not count a repeated coordinate as new deviation evidence");
        navigator.Update(straight,P(590,50),now.AddSeconds(4.25));
        navigator.Update(straight,P(590,55),now.AddSeconds(4.5));
        Check(navigator.NeedsReroute,"Sustained deviation across distinct positions requests recalculation");
        navigator.Reset();
        var connector=Route(P(100,0),P(300,0),P(300,200));
        connector.PlayerPoint=P(0,0); connector.EntryDistanceMeters=100;
        connector.TargetPoint=P(400,200); connector.ExitDistanceMeters=100;
        navigator.Update(connector,P(10,0),now);
        navigator.Update(connector,P(20,0),now.AddSeconds(1));
        navigator.Update(connector,P(30,0),now.AddSeconds(2));
        Check(!navigator.NeedsReroute && !navigator.WrongWay,"Following the displayed entry connector is on-route");
        Check(Math.Abs(navigator.NextCue(connector,P(30,0)).Distance-270)<1,"Entry connector contributes to distance to the next junction");
        navigator.Reset(); navigator.Update(connector,P(350,200),now);
        navigator.Update(connector,P(360,200),now.AddSeconds(1));
        navigator.Update(connector,P(370,200),now.AddSeconds(2));
        Check(!navigator.NeedsReroute && !navigator.WrongWay,"Following the displayed exit connector does not loop in recalculation");
        navigator.Reset();
        left.Success=true;
        navigator.Update(left,P(400,0),now);
        string[] fast=navigator.Update(left,P(450,0),now.AddSeconds(1));
        Check(fast!=null && fast.SequenceEqual(new[]{VoicePacks.Clips[5]}),"Vehicle-speed turn call starts three seconds before the junction");
        navigator.Reset(); navigator.Update(left,P(500,0),now);
        string[] urgent=navigator.Update(left,P(580,0),now.AddSeconds(2),true);
        Check(urgent!=null && urgent.SequenceEqual(new[]{VoicePacks.Clips[5]}),"Urgent turn interrupts a previous distance phrase without six-second cooldown");
        Check(VoicePacks.IsClip(VoicePacks.RecalculatingClip),"Optional recalculating clip is supported without changing the 12 required clips");
        RoadRouter router=RoadRouter.Instance; router.InitializeFromResource();
        Check(router.IsLoaded,"Real road graph loads for navigation validation");
        var nodes=(RoadRouter.RoadNode[])typeof(RoadRouter).GetField("nodes",Hidden).GetValue(router);
        var components=(List<int>[])typeof(RoadRouter).GetField("componentNodes",Hidden).GetValue(router);
        int main=(int)typeof(RoadRouter).GetField("mainComponentId",Hidden).GetValue(router);
        List<int> connected=components[main]; bool foundJunctions=false;
        for(int sample=1;sample<=4 && !foundJunctions;sample++) {
            RoadRouter.RoadNode a=nodes[connected[connected.Count*sample/10]], b=nodes[connected[connected.Count*(sample+5)/10]];
            RoadRoute actual=router.FindRoute(new PointF(a.U,a.V),new PointF(b.U,b.V));
            if(actual!=null && actual.Success && actual.JunctionIndices.Length>0) {
                foundJunctions=true;
                Check(actual.JunctionIndices.All(index=>index>=0 && index<actual.Polyline.Length),"Real computed route retains valid road-junction indices");
                navigator.Reset(); Check(navigator.NextCue(actual,actual.Polyline[0])!=null,"Voice navigator consumes real computed route metadata");
            }
        }
        Check(foundJunctions,"Real road-network routes expose branch decisions to voice guidance");
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-voice-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string packs=Path.Combine(root,"voice-navigation"); VoicePacks.InstallBundled(packs);
            Check(VoicePacks.Discover(packs).SequenceEqual(new[]{VoicePacks.DefaultName}),"Bundled voice installs all 12 clips and is discovered");
            Directory.CreateDirectory(Path.Combine(packs,"Incomplete"));
            Check(VoicePacks.Discover(packs).Length==1,"Incomplete voice packs are excluded");
            string second=Path.Combine(packs,"Second voice"); Directory.CreateDirectory(second);
            foreach(string file in VoicePacks.Clips) File.Copy(Path.Combine(packs,VoicePacks.DefaultName,file),Path.Combine(second,file));
            Check(VoicePacks.Discover(packs).Length==2,"Additional voice folders are discovered without code changes");
            using(var player=new VoicePlayer { Volume=0 }) {
                // Hosted CI runners have no reliable audio endpoint for Windows MCI playback.
                // Keep decoder playback covered on developer machines and verify queue behavior everywhere.
                if(!String.Equals(Environment.GetEnvironmentVariable("CI"),"true",StringComparison.OrdinalIgnoreCase)) {
                    player.Speak(second,VoicePacks.Clips); Pump(player);
                }
                var audioWatch=System.Diagnostics.Stopwatch.StartNew();
                for(int request=0;request<50;request++) { player.Speak(second,new[]{VoicePacks.Clips[10],VoicePacks.Clips[2],VoicePacks.Clips[5]}); player.Stop(); }
                audioWatch.Stop();
                Check(audioWatch.ElapsedMilliseconds<500,"Rapid audio start/cancel requests do not wait for native MP3 opening on the UI thread");
                player.Speak(second,new[]{VoicePacks.Clips[10],VoicePacks.Clips[2],VoicePacks.Clips[5]});
                Check(player.Busy,"Preview starts asynchronously"); player.Stop();
                Check(!player.Busy,"Disabling playback cancels current clip and remaining queue");
                player.Dispose(); Check(player.WaitForExit(5000),"Audio worker closes files and exits after disposal");
            }
            AppLanguage originalLanguage=Localization.Current;
            foreach(AppLanguage language in Enum.GetValues(typeof(AppLanguage))) {
                Localization.Current=language;
                using(var reminder=new CopyKeyReminderDialog()) {
                    var message=reminder.Controls.OfType<Label>().Single(label=>label.Text!="Num /");
                    var keyLabel=reminder.Controls.OfType<Label>().Single(label=>label.Text=="Num /");
                    var checkbox=reminder.Controls.OfType<CheckBox>().Single();
                    var okay=reminder.Controls.OfType<Button>().Single();
                    Check(message.Text.Contains("Num /") && keyLabel.Text=="Num /" && keyLabel.Width>0
                        && checkbox.Text.Length>0 && okay.Text.Length>0
                        && okay.DialogResult==DialogResult.OK && !reminder.DoNotShowAgain,
                        "Copy-key reminder is localized and confirmable: "+language);
                    checkbox.Checked=true;
                    Check(reminder.DoNotShowAgain,"Copy-key reminder offers persistent opt-out: "+language);
                }
            }
            Localization.Current=originalLanguage;
            using(var bitmap=new Bitmap(32,32)) bitmap.Save(Path.Combine(root,"map.png"));
            using(var window=new MapWindow(root,true)) {
                var defaultAdminCommands=AdminCommand.Defaults();var godCommand=defaultAdminCommands[0];var playerCommand=defaultAdminCommands[1];
                Check(defaultAdminCommands.All(command=>window.LastAdminToggleState(command)==null)
                    && window.AdminCommandLabel(godCommand)=="God mode: UNKNOWN",
                    "A new MiniMap session reports admin command values as unknown");
                typeof(MapWindow).GetField("adminCommandBusy",Hidden).SetValue(window,true);
                int attemptsBeforeAdmin=(int)typeof(MapWindow).GetField("attempts",Hidden).GetValue(window);
                uint sequenceBeforeAdmin=(uint)typeof(MapWindow).GetField("sequence",Hidden).GetValue(window);
                var unchangedClipboardSnapshot=new DataObject();
                typeof(MapWindow).GetField("savedDataObject",Hidden).SetValue(window,unchangedClipboardSnapshot);
                typeof(MapWindow).GetField("clipboardSnapshotReady",Hidden).SetValue(window,false);
                typeof(MapWindow).GetMethod("PerformCopyAsync",Hidden).Invoke(window,null);
                Check(!(bool)typeof(MapWindow).GetField("copyInProgress",Hidden).GetValue(window)
                    && !(bool)typeof(MapWindow).GetField("clipboardSnapshotReady",Hidden).GetValue(window)
                    && !(bool)typeof(MapWindow).GetField("pending",Hidden).GetValue(window)
                    && object.ReferenceEquals(unchangedClipboardSnapshot,typeof(MapWindow).GetField("savedDataObject",Hidden).GetValue(window))
                    && (int)typeof(MapWindow).GetField("attempts",Hidden).GetValue(window)==attemptsBeforeAdmin
                    && (uint)typeof(MapWindow).GetField("sequence",Hidden).GetValue(window)==sequenceBeforeAdmin,
                    "Admin input prevents coordinate copying before clipboard capture or attempt bookkeeping");
                typeof(MapWindow).GetField("savedDataObject",Hidden).SetValue(window,null);
                typeof(MapWindow).GetField("adminCommandBusy",Hidden).SetValue(window,false);
                Check(Descendants(window).OfType<Button>().Any(b=>b.Text=="Admin command panel"),"Settings exposes the admin command panel");
                typeof(MapWindow).GetMethod("ShowAdminCommands",Hidden).Invoke(window,null);
                var adminPanel=(Form)typeof(MapWindow).GetField("adminCommandsPanel",Hidden).GetValue(window);
                Check(adminPanel.TopMost && (WindowStyle(adminPanel.Handle,-20)&8)!=0,"Admin panel uses native topmost style above SCUM");
                Check(Descendants(adminPanel).OfType<Button>().Count(b=>b.Text.EndsWith(": UNKNOWN"))==3
                    && Descendants(adminPanel).OfType<Button>().Count(b=>b.Text=="ON")==3
                    && Descendants(adminPanel).OfType<Button>().Count(b=>b.Text=="OFF")==3,
                    "Fresh admin panel values stay unknown and each toggle offers explicit ON and OFF requests");
                Check(Descendants(adminPanel).OfType<Label>().Any(l=>l.Text=="#ShowOtherPlayerInfo true/false"),"Admin panel shows the exact singular player-info command");
                using(var adminMenu=new ToolStripMenuItem("Admin commands")) {
                    window.PopulateAdminMenu(adminMenu);
                    window.PopulateFullMapAdminMenu(adminMenu);
                    Check(adminMenu.DropDownItems[0].Text=="Teleport here" && !adminMenu.DropDownItems[0].Enabled,
                        "Teleport here is disabled without a full-map click target");
                    var unknownAdminItems=adminMenu.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Text.EndsWith(": UNKNOWN")).ToArray();
                    Check(unknownAdminItems.Length==3 && unknownAdminItems.All(i=>i.DropDownItems.OfType<ToolStripMenuItem>().Select(choice=>choice.Text).SequenceEqual(new[]{"ON","OFF"})
                        && i.DropDownItems.OfType<ToolStripMenuItem>().All(choice=>!choice.Checked))
                        && adminMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Text=="Edit commands..."),
                        "Fresh full-map admin menu exposes both requested values without claiming an initial game state");
                    var submittedAdminCommands=new List<string>();
                    Func<string,Task<bool>> submitAdmin=command=> {submittedAdminCommands.Add(command);return Task.FromResult(true);};
                    Check(window.SubmitAdminCommand(godCommand,false,submitAdmin).GetAwaiter().GetResult()
                        && submittedAdminCommands.SequenceEqual(new[]{"#SetGodMode false"}) && window.LastAdminToggleState(godCommand)==false,
                        "Explicit OFF is available on a fresh session and sends false on its first request");
                    var boundaryCommand=new AdminCommand("Enter boundary","#BoundaryToggle",true);var boundaryRequests=new List<string>();bool boundaryChatClosed=true;
                    Check(window.SubmitAdminCommand(boundaryCommand,null,command=> {
                        boundaryRequests.Add(command);bool nativeSubmitted=false,nativeAllowed=true;int nativePhase=0;
                        boundaryChatClosed=Native.AdminChatSequence((key,up)=>true,ms=> {
                            if(++nativePhase==7)nativeAllowed=false;return Task.FromResult(0);
                        },()=>nativeAllowed,()=>true,()=>{},0x54,0,()=>nativeSubmitted=true).GetAwaiter().GetResult();
                        return Task.FromResult(nativeSubmitted);
                    }).GetAwaiter().GetResult() && !boundaryChatClosed && window.LastAdminToggleState(boundaryCommand)==true,
                        "An Enter-down interruption records the submitted ON request despite incomplete chat cleanup");
                    Check(window.SubmitAdminCommand(boundaryCommand,null,command=> {boundaryRequests.Add(command);return Task.FromResult(true);}).GetAwaiter().GetResult()
                        && boundaryRequests.SequenceEqual(new[]{"#BoundaryToggle true","#BoundaryToggle false"}) && window.LastAdminToggleState(boundaryCommand)==false,
                        "The next toggle sends OFF after Enter-down interruption instead of repeating ON");
                    foreach(bool requestedValue in new[]{true,false,true}) {
                        Check(window.SubmitAdminCommand(godCommand,null,submitAdmin).GetAwaiter().GetResult()
                            && submittedAdminCommands.Last()==godCommand.Build(requestedValue) && window.LastAdminToggleState(godCommand)==requestedValue,
                            "Repeated admin toggles use the current submitted value: "+requestedValue);
                    }
                    Check(!window.SubmitAdminCommand(godCommand,false,command=>Task.FromResult(false)).GetAwaiter().GetResult()
                        && window.LastAdminToggleState(godCommand)==true,
                        "An unsuccessful OFF request preserves the previous submitted ON value");
                    Check(window.LastAdminToggleState(playerCommand)==null
                        && window.SubmitAdminCommand(playerCommand,false,submitAdmin).GetAwaiter().GetResult()
                        && window.LastAdminToggleState(playerCommand)==false && window.LastAdminToggleState(godCommand)==true,
                        "Submitting one admin toggle leaves other command values independent");
                    var equivalentGodCommand=new AdminCommand("Renamed god mode","#SetGodMode TRUE false",true);
                    Check(window.LastAdminToggleState(equivalentGodCommand)==true
                        && window.SubmitAdminCommand(equivalentGodCommand,null,submitAdmin).GetAwaiter().GetResult()
                        && submittedAdminCommands.Last()=="#SetGodMode false" && window.LastAdminToggleState(godCommand)==false,
                        "Equivalent normalized commands share their submitted value instead of sending ON repeatedly");
                    Check(window.SubmitAdminCommand(godCommand,null,submitAdmin).GetAwaiter().GetResult(),
                        "The next shared-state toggle can return the command to ON");
                    var pendingAdminResult=new TaskCompletionSource<bool>();
                    Task<bool> pendingAdminSubmission=window.SubmitAdminCommand(godCommand,false,command=> {
                        Check(command=="#SetGodMode false","An asynchronous submission sends the requested OFF command");
                        return pendingAdminResult.Task;
                    });
                    Check(!pendingAdminSubmission.IsCompleted && window.LastAdminToggleState(godCommand)==true,
                        "An in-progress request does not change the previously submitted admin value");
                    pendingAdminResult.SetResult(true);
                    DateTime submissionDeadline=DateTime.UtcNow.AddSeconds(2);
                    while(!pendingAdminSubmission.IsCompleted && DateTime.UtcNow<submissionDeadline) {Application.DoEvents();Thread.Sleep(1);}
                    Check(pendingAdminSubmission.IsCompleted && pendingAdminSubmission.GetAwaiter().GetResult()
                        && window.LastAdminToggleState(godCommand)==false,
                        "The requested admin value changes only after asynchronous submission succeeds");
                    Check(window.SubmitAdminCommand(godCommand,null,submitAdmin).GetAwaiter().GetResult(),
                        "Toggling after asynchronous completion uses the updated submitted value");
                    window.PopulateAdminMenu(adminMenu);
                    var godMenuItem=adminMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="God mode: ON");
                    var playerMenuItem=adminMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="Player info: OFF");
                    Check(Descendants(adminPanel).OfType<Button>().Any(b=>b.Text=="God mode: ON")
                        && Descendants(adminPanel).OfType<Button>().Any(b=>b.Text=="Player info: OFF")
                        && Descendants(adminPanel).OfType<Button>().Any(b=>b.Text=="Nameplates: UNKNOWN")
                        && godMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="ON").Checked
                        && !godMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="OFF").Checked
                        && playerMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="OFF").Checked
                        && !playerMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="ON").Checked,
                        "Shared submissions refresh the panel and reopened menu with consistent requested values");
                }
                adminPanel.WindowState=FormWindowState.Minimized;
                typeof(MapWindow).GetMethod("ShowAdminCommands",Hidden).Invoke(window,null);
                Check(object.ReferenceEquals(adminPanel,typeof(MapWindow).GetField("adminCommandsPanel",Hidden).GetValue(window)) && adminPanel.WindowState==FormWindowState.Normal && adminPanel.TopMost,"Repeated opening restores the admin panel above SCUM");
                adminPanel.Close();
                Check((int)typeof(MapWindow).GetField("scumCopyModifierKey",Hidden).GetValue(window)==0 && (int)typeof(MapWindow).GetField("scumCopyKey",Hidden).GetValue(window)==0x6F,"New installations default to NumPad Divide without a modifier");
                Check(!(bool)typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).GetValue(window),"Copy-key reminder appears by default");
                Check((bool)typeof(MapWindow).GetField("automaticDeathMarkers",Hidden).GetValue(window),"Automatic death markers are enabled for new profiles");
                using(var guide=new StartupGuideDialog()) {
                    Check((int)typeof(StartupGuideDialog).GetField("copyModKey",Hidden).GetValue(guide)==0 && (int)typeof(StartupGuideDialog).GetField("copyKey",Hidden).GetValue(guide)==0x6F,"First-run guide matches the NumPad Divide single-key default");
                    typeof(StartupGuideDialog).GetMethod("RenderSlideKeybinds",Hidden).Invoke(guide,null);
                    foreach(string field in new[]{"copyScanCode","mapScanCode","chatScanCode"})
                        typeof(StartupGuideDialog).GetField(field,Hidden).SetValue(guide,0x56);
                    var reset=Descendants(guide).OfType<Button>().First(button=>button.Text==Localization.Get("WizardBtnResetDefaults"));
                    typeof(Button).GetMethod("OnClick",Hidden).Invoke(reset,new object[]{EventArgs.Empty});
                    Check((int)typeof(StartupGuideDialog).GetField("copyModKey",Hidden).GetValue(guide)==0 && (int)typeof(StartupGuideDialog).GetField("copyKey",Hidden).GetValue(guide)==0x6F,"Reset button retains the NumPad Divide single-key default without checkbox events re-enabling Ctrl");
                    Check(new[]{"copyScanCode","mapScanCode","chatScanCode"}.All(field=>(int)typeof(StartupGuideDialog).GetField(field,Hidden).GetValue(guide)==0),"Reset clears all previous SCUM physical scan codes");
                    var conflicts=typeof(StartupGuideDialog).GetMethod("BindingsConflict",Hidden);
                    Check(!(bool)conflicts.Invoke(guide,null),"Default setup bindings have no conflicts");
                    typeof(StartupGuideDialog).GetField("mapKey",Hidden).SetValue(guide,0x24);
                    Check((bool)conflicts.Invoke(guide,null),"Setup rejects SCUM map conflicts with MiniMap settings");
                    typeof(StartupGuideDialog).GetField("mapKey",Hidden).SetValue(guide,0x4D);
                    typeof(StartupGuideDialog).GetField("mapScanCode",Hidden).SetValue(guide,0x56);
                    typeof(StartupGuideDialog).GetField("chatScanCode",Hidden).SetValue(guide,0x56);
                    Check((bool)conflicts.Invoke(guide,null),"Setup detects physical key conflicts across different virtual keys");
                    Check(!(bool)typeof(StartupGuideDialog).GetMethod("CommitKeybindsIfApplicable",Hidden).Invoke(guide,new object[]{false}),
                        "Invalid setup bindings cannot be committed while navigating the guide");
                    var captureSetup=typeof(StartupGuideDialog).GetMethod("HandleFormKeyDown",Hidden);
                    foreach(var binding in new[]{new[]{1,0x4D},new[]{2,0x54}}) {
                        string keyField=binding[0]==1?"mapKey":"chatKey";
                        string scanField=binding[0]==1?"mapScanCode":"chatScanCode";
                        typeof(StartupGuideDialog).GetField("captureTarget",Hidden).SetValue(guide,binding[0]);
                        PhysicalKeyCapture.Observe(0x7A,0x57,0);
                        captureSetup.Invoke(guide,new object[]{guide,new KeyEventArgs(Keys.F12)});
                        Check((int)typeof(StartupGuideDialog).GetField(keyField,Hidden).GetValue(guide)==binding[1]
                            && (int)typeof(StartupGuideDialog).GetField(scanField,Hidden).GetValue(guide)==0x56,
                            "Failed physical capture preserves the previous "+keyField+" binding");
                        captureSetup.Invoke(guide,new object[]{guide,new KeyEventArgs(Keys.RMenu)});
                        Check((int)typeof(StartupGuideDialog).GetField(keyField,Hidden).GetValue(guide)==binding[1],
                            "Right Alt cannot become the setup "+keyField+" binding");
                    }
                }
                window.Show(); Application.DoEvents();
                typeof(MapWindow).GetMethod("BeginCopyRequest",Hidden).Invoke(window,null);
                DateTime requestStart=DateTime.UtcNow.AddMilliseconds(-200);
                typeof(MapWindow).GetField("sent",Hidden).SetValue(window,requestStart);
                typeof(MapWindow).GetMethod("CompleteCopyRequest",Hidden).Invoke(window,new object[]{CopyResult.Sent});
                Check((DateTime)typeof(MapWindow).GetField("sent",Hidden).GetValue(window)==requestStart,"Copy completion does not restart the response deadline");
                typeof(MapWindow).GetField("pending",Hidden).SetValue(window,false);
                Check(window.ShowInTaskbar && window.Visible && window.WindowState==FormWindowState.Minimized,"Startup keeps a minimized taskbar window");
                Check(!(bool)typeof(MapWindow).GetProperty("SettingsVisible",Hidden).GetValue(window,null),"Minimized taskbar window does not count as open settings or pause tracking");
                typeof(MapWindow).GetMethod("ShowSettings",Hidden).Invoke(window,null); Application.DoEvents();
                Check(window.WindowState==FormWindowState.Normal && (bool)typeof(MapWindow).GetProperty("SettingsVisible",Hidden).GetValue(window,null),"Opening settings restores the taskbar window");
                typeof(MapWindow).GetMethod("DismissSettings",Hidden).Invoke(window,null); Application.DoEvents();
                Check(window.Visible && window.WindowState==FormWindowState.Minimized,"Dismissing settings retains the taskbar button");
                typeof(MapWindow).GetField("lastHomeAction",Hidden).SetValue(window,DateTime.MinValue);
                typeof(MapWindow).GetMethod("FocusSettingsShortcut",Hidden).Invoke(window,null); Application.DoEvents();
                // Windows may deny foreground ownership to a background test process.
                // Check restoration/topmost state here; ShowSettings also requests activation.
                Check(window.WindowState==FormWindowState.Normal && window.Visible && window.TopMost,"Home restores minimized settings as a visible topmost window");
                typeof(MapWindow).GetField("lastHomeAction",Hidden).SetValue(window,DateTime.MinValue);
                typeof(MapWindow).GetMethod("FocusSettingsShortcut",Hidden).Invoke(window,null); Application.DoEvents();
                Check(window.WindowState==FormWindowState.Normal,"Repeated Home keeps already-open settings visible");
                typeof(MapWindow).GetField("lastHomeAction",Hidden).SetValue(window,DateTime.MinValue);
                typeof(Control).GetMethod("OnKeyDown",Hidden).Invoke(window,new object[]{new KeyEventArgs(Keys.Home)}); Application.DoEvents();
                Check(window.WindowState==FormWindowState.Normal,"Home inside the settings form no longer minimizes it");
                typeof(Control).GetMethod("OnKeyDown",Hidden).Invoke(window,new object[]{new KeyEventArgs(Keys.Escape)}); Application.DoEvents();
                Check(window.WindowState==FormWindowState.Minimized,"Escape still dismisses settings to the taskbar");
                Check(!(bool)typeof(MapWindow).GetField("voiceEnabled",Hidden).GetValue(window),"Voice guidance defaults off");
                File.WriteAllLines(Path.Combine(root,"settings.ini"),new[]{"Welcomed=True","VoiceEnabled=True","VoiceName=Second voice","VoiceVolume=45","CopyIntervalMs=1000","ScumCopyModifierKey=162","ScumCopyKey=67"});
                foreach(string field in new[]{"settingsShortcutScanCode","pinShortcutScanCode","searchShortcutScanCode"})
                    typeof(MapWindow).GetField(field,Hidden).SetValue(window,0x56);
                typeof(MapWindow).GetMethod("LoadSettings",Hidden).Invoke(window,null);
                Check(new[]{"settingsShortcutScanCode","pinShortcutScanCode","searchShortcutScanCode"}.All(field=>(int)typeof(MapWindow).GetField(field,Hidden).GetValue(window)==0),"Loading virtual-key-only settings clears stale shortcut scan codes");
                Check((int)typeof(MapWindow).GetField("scumCopyModifierKey",Hidden).GetValue(window)==162 && (int)typeof(MapWindow).GetField("scumCopyKey",Hidden).GetValue(window)==67,"Existing Ctrl+C bindings survive the new-install default change");
                Check((bool)typeof(MapWindow).GetField("voiceEnabled",Hidden).GetValue(window) && (string)typeof(MapWindow).GetField("voiceName",Hidden).GetValue(window)=="Second voice" && (int)typeof(MapWindow).GetField("voiceVolume",Hidden).GetValue(window)==45,"Voice preference, selected pack and volume load correctly");
                typeof(MapWindow).GetMethod("SaveSettings",Hidden).Invoke(window,null);
                string saved=File.ReadAllText(Path.Combine(root,"settings.ini"));
                Check(saved.Contains("VoiceEnabled=True") && saved.Contains("VoiceName=Second voice") && saved.Contains("VoiceVolume=45"),"Voice choices persist across restarts");
                var target=new MapZone { Name="Connector replay",Points=new[]{P(400,200)} };
                typeof(MapWindow).GetField("searchTarget",Hidden).SetValue(window,target);
                typeof(MapWindow).GetField("voiceTarget",Hidden).SetValue(window,target);
                typeof(MapWindow).GetField("activeRoute",Hidden).SetValue(window,connector);
                typeof(MapWindow).GetField("position",Hidden).SetValue(window,new Position { X=-143991,Y=-142991 });
                typeof(MapWindow).GetField("updated",Hidden).SetValue(window,DateTime.UtcNow);
                typeof(MapWindow).GetField("voiceVolume",Hidden).SetValue(window,0);
                typeof(MapWindow).GetMethod("SpeakVoice",Hidden).Invoke(window,new object[]{new[]{VoicePacks.Clips[10],VoicePacks.Clips[2],VoicePacks.Clips[5]}});
                var windowPlayer=(VoicePlayer)typeof(MapWindow).GetField("voicePlayer",Hidden).GetValue(window);
                typeof(MapWindow).GetMethod("TickVoice",Hidden).Invoke(window,new object[]{DateTime.UtcNow,true});
                Check(windowPlayer.Busy,"On-connector guidance does not truncate the phrase after 'In'");
                if(String.Equals(Environment.GetEnvironmentVariable("CI"),"true",StringComparison.OrdinalIgnoreCase)) windowPlayer.Stop();
                else Pump(windowPlayer);
                typeof(MapWindow).GetField("routeCalculating",Hidden).SetValue(window,true);
                Check(!(bool)typeof(MapWindow).GetMethod("RequestRouteAsync",Hidden).Invoke(window,new object[]{P(0,0),target,true}),"Busy router explicitly rejects a new request instead of reporting it started");
                typeof(MapWindow).GetField("routeCalculating",Hidden).SetValue(window,false);
                typeof(MapWindow).GetField("voiceStatusUntil",Hidden).SetValue(window,DateTime.MaxValue);
                Check((bool)typeof(MapWindow).GetMethod("RequestRouteAsync",Hidden).Invoke(window,new object[]{P(0,0),target,true}),"Forced recalculation reports an accepted request");
                DateTime routeTimeout=DateTime.UtcNow.AddSeconds(15);
                while((bool)typeof(MapWindow).GetField("routeCalculating",Hidden).GetValue(window) && DateTime.UtcNow<routeTimeout) { Application.DoEvents(); Thread.Sleep(10); }
                Check(!(bool)typeof(MapWindow).GetField("routeCalculating",Hidden).GetValue(window) && (DateTime)typeof(MapWindow).GetField("voiceStatusUntil",Hidden).GetValue(window)!=DateTime.MaxValue,"Recalculation completion clears the in-progress status or reports failure");
                windowPlayer.Dispose(); Check(windowPlayer.WaitForExit(5000),"Window audio worker shuts down without leaving MP3 files locked");
            }
            using(var persistWindow=new MapWindow(root)) {
                typeof(MapWindow).GetField("settingsShortcutKey",Hidden).SetValue(persistWindow,0x75);
                typeof(MapWindow).GetField("pinShortcutKey",Hidden).SetValue(persistWindow,0x67);
                typeof(MapWindow).GetField("searchShortcutKey",Hidden).SetValue(persistWindow,0x76);
                typeof(MapWindow).GetField("settingsShortcutScanCode",Hidden).SetValue(persistWindow,0x147);
                typeof(MapWindow).GetField("pinShortcutScanCode",Hidden).SetValue(persistWindow,0x152);
                typeof(MapWindow).GetField("searchShortcutScanCode",Hidden).SetValue(persistWindow,0x153);
                typeof(MapWindow).GetField("locationHistoryMinutes",Hidden).SetValue(persistWindow,75);
                typeof(MapWindow).GetField("automaticDeathMarkers",Hidden).SetValue(persistWindow,false);
                typeof(MapWindow).GetField("locationHistory",Hidden).SetValue(persistWindow,false);
                typeof(MapWindow).GetField("locationHistoryTimestamps",Hidden).SetValue(persistWindow,true);
                typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).SetValue(persistWindow,true);
                typeof(MapWindow).GetField("scumCopyScanCode",Hidden).SetValue(persistWindow,0x56);
                typeof(MapWindow).GetField("scumMapScanCode",Hidden).SetValue(persistWindow,0x135);
                typeof(MapWindow).GetField("scumChatScanCode",Hidden).SetValue(persistWindow,0x23);
                typeof(MapWindow).GetMethod("SaveSettings",Hidden).Invoke(persistWindow,null);
            }
            Check(File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("ScumCopyScanCode=86"),"Physical copy scan code is saved");
            Check(File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("ScumMapScanCode=309")
                && File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("ScumChatScanCode=35"),"Physical map and chat scan codes are saved");
            Check(File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("SettingsShortcutScanCode=327")
                && File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("PinShortcutScanCode=338")
                && File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("SearchShortcutScanCode=339"),"All configurable MiniMap shortcut scan codes are saved");
            using(var reopened=new MapWindow(root,true)) {
                Check(AdminCommand.Defaults().All(command=>reopened.LastAdminToggleState(command)==null)
                    && reopened.AdminCommandLabel(AdminCommand.Defaults()[0])=="God mode: UNKNOWN",
                    "Launching MiniMap again does not reuse submitted ON values as the live SCUM state");
                Check(!(bool)typeof(MapWindow).GetField("automaticDeathMarkers",Hidden).GetValue(reopened),"Automatic death marker opt-out survives restart");
                Check((int)typeof(MapWindow).GetField("settingsShortcutKey",Hidden).GetValue(reopened)==0x75
                    && (int)typeof(MapWindow).GetField("pinShortcutKey",Hidden).GetValue(reopened)==0x67
                    && (int)typeof(MapWindow).GetField("searchShortcutKey",Hidden).GetValue(reopened)==0x76,
                    "Captured function-key and number-pad shortcut preferences survive restart");
                var watched=typeof(MapWindow).GetMethod("IsWatchedPhysicalKey",Hidden);
                foreach(int key in new[]{0x75,0x67,0x76}) Check((bool)watched.Invoke(reopened,new object[]{key,0}),"Rebound shortcut reaches keyboard dispatch: "+key);
                var watchedPhysical=typeof(MapWindow).GetMethod("IsWatchedPhysicalKey",Hidden);
                Check((bool)watchedPhysical.Invoke(reopened,new object[]{0x51,0x147})
                    && !(bool)watchedPhysical.Invoke(reopened,new object[]{0x51,0x34})
                    && (bool)watchedPhysical.Invoke(reopened,new object[]{0x51,0x152})
                    && (bool)watchedPhysical.Invoke(reopened,new object[]{0x51,0x153}),
                    "Settings, waypoint and search bindings are dispatched by physical scan code across layouts");
                var validate=typeof(MapWindow).GetMethod("ShortcutBindingError",Hidden);
                Check(validate.Invoke(reopened,new object[]{0x76,0x75,0,0})!=null && validate.Invoke(reopened,new object[]{0x54,0x75,0,0})!=null,
                    "Shortcut capture rejects duplicate and SCUM chat bindings");
                var validatePhysical=typeof(MapWindow).GetMethod("ShortcutBindingError",Hidden);
                Check(validatePhysical.Invoke(reopened,new object[]{0x51,0x75,0x135,0x35})!=null
                    && validatePhysical.Invoke(reopened,new object[]{0x75,0x75,0x35,0x135})==null,
                    "Shortcut conflict checks use physical position, not matching layout-specific virtual keys");
                PhysicalKeyCapture.Observe(0x78,0x3B,0);
                using(var capture=new ShortcutCaptureDialog("Settings",(key,scan)=>(string)validate.Invoke(reopened,new object[]{key,0x75,scan,0}))) {
                    var captureKey=typeof(ShortcutCaptureDialog).GetMethod("ProcessCmdKey",Hidden);
                    captureKey.Invoke(capture,new object[]{new Message(),Keys.Control|Keys.F9});
                    Check(capture.DialogResult==DialogResult.None,"Shortcut capture rejects modifier combinations");
                    captureKey.Invoke(capture,new object[]{new Message(),Keys.F9});
                    Check(capture.DialogResult==DialogResult.OK && capture.CapturedKey==(int)Keys.F9 && capture.CapturedScanCode==0x3B,"Shortcut dialog captures the virtual key and physical scan code");
                }
                Check((int)typeof(MapWindow).GetField("locationHistoryMinutes",Hidden).GetValue(reopened)==75
                    && !(bool)typeof(MapWindow).GetField("locationHistory",Hidden).GetValue(reopened)
                    && (bool)typeof(MapWindow).GetField("locationHistoryTimestamps",Hidden).GetValue(reopened),
                    "History duration and visibility preferences survive restart");
                Check((bool)typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).GetValue(reopened)
                    && File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("SuppressCopyKeyReminder=True"),"Copy-key reminder opt-out persists across restarts");
                Check((int)typeof(MapWindow).GetField("scumCopyScanCode",Hidden).GetValue(reopened)==0x56,"Physical copy scan code survives restart");
                Check((int)typeof(MapWindow).GetField("scumMapScanCode",Hidden).GetValue(reopened)==0x135
                    && (int)typeof(MapWindow).GetField("scumChatScanCode",Hidden).GetValue(reopened)==0x23,
                    "Physical map and chat scan codes survive restart");
                Check((int)typeof(MapWindow).GetField("settingsShortcutScanCode",Hidden).GetValue(reopened)==0x147
                    && (int)typeof(MapWindow).GetField("pinShortcutScanCode",Hidden).GetValue(reopened)==0x152
                    && (int)typeof(MapWindow).GetField("searchShortcutScanCode",Hidden).GetValue(reopened)==0x153,
                    "All configurable MiniMap shortcut scan codes survive restart");
            }
            TestAppControlProfile(root);
        } finally {
            if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("MiniMap-voice-test-")) Directory.Delete(root,true);
        }
        Console.WriteLine("Passed "+count+" input and voice checks.");
    }
}
