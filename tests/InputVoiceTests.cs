using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScumMiniMap;

static class InputVoiceTests {
    static int count;
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); count++; Console.WriteLine("PASS: "+message); }
    static PointF P(double x,double y) { return new PointF((float)(.5+x/15216.18),(float)(.5+y/15236.18)); }
    static IEnumerable<Control> Descendants(Control root) { foreach(Control child in root.Controls) { yield return child; foreach(Control nested in Descendants(child)) yield return nested; } }
    static RoadRoute Route(params PointF[] points) { return new RoadRoute { Success=true,Polyline=points,JunctionIndices=Enumerable.Range(1,Math.Max(0,points.Length-2)).ToArray() }; }
    static void Pump(VoicePlayer player) {
        DateTime until=DateTime.UtcNow.AddSeconds(35);
        while(player.Busy && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(10); }
        Check(!player.Busy && player.LastError==null,"MP3 sequence completed without decoder errors");
    }
    [STAThread] static void Main() {
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
        using(var hooks=new GameKeys(key=>{},key=>true,chat,()=>0x54)) {
            typeof(GameKeys).GetMethod("RefreshHooks",Hidden).Invoke(hooks,null);
            Check(hooks.Available && chat.Paused,"Hook renewal restores monitoring without clearing chat protection");
        }
        Check(Program.TrackingIntervalMs(250,0xA2)==1000 && Program.TrackingIntervalMs(250,0)==250 && Program.TrackingIntervalMs(3000,0xA2)==3000,"Modifier shortcuts use at most one copy per second; single-key tracking retains 250 ms");
        Check(Program.CopyResponseTimeoutMs(0)==350 && Program.CopyResponseTimeoutMs(0xA2)==1000 && Program.CopyRetryDelayMs(0)==1000,"Single-key misses no longer introduce a one-second response wait or five-second retry pause");
        var singleKeyDelays=new List<int>();
        Native.CopyChord((key,up)=>true,ms=>{ singleKeyDelays.Add(ms); return Task.FromResult(0); },()=>true,0,0xDC).GetAwaiter().GetResult();
        Check(singleKeyDelays.SequenceEqual(new[]{60}),"Backslash copy spans game frames without the redundant post-release delay");
        Check(!GameKeys.MouseActionBlocksCopy(0x20A,0) && !GameKeys.MouseActionBlocksCopy(0x20E,0) && GameKeys.MouseActionBlocksCopy(0x20A,0xA2),"Single-key tracking continues through scrolling; modifier-based copying remains guarded");
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
        left.HasWaterTransit=true;
        Check(navigator.NextCue(left,P(500,0))==null,"Water transit does not receive road-turn instructions");
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
        left.HasWaterTransit=false;
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
                    var message=reminder.Controls.OfType<Label>().Single();
                    var checkbox=reminder.Controls.OfType<CheckBox>().Single();
                    var okay=reminder.Controls.OfType<Button>().Single();
                    Check(message.Text.Contains("\\") && checkbox.Text.Length>0 && okay.Text.Length>0
                        && okay.DialogResult==DialogResult.OK && !reminder.DoNotShowAgain,
                        "Copy-key reminder is localized and confirmable: "+language);
                    checkbox.Checked=true;
                    Check(reminder.DoNotShowAgain,"Copy-key reminder offers persistent opt-out: "+language);
                }
            }
            Localization.Current=originalLanguage;
            using(var bitmap=new Bitmap(32,32)) bitmap.Save(Path.Combine(root,"map.png"));
            using(var window=new MapWindow(root,true)) {
                Check((int)typeof(MapWindow).GetField("scumCopyModifierKey",Hidden).GetValue(window)==0 && (int)typeof(MapWindow).GetField("scumCopyKey",Hidden).GetValue(window)==0xDC,"New installations default to backslash without a modifier");
                Check(!(bool)typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).GetValue(window),"Copy-key reminder appears by default");
                using(var guide=new StartupGuideDialog()) {
                    Check((int)typeof(StartupGuideDialog).GetField("copyModKey",Hidden).GetValue(guide)==0 && (int)typeof(StartupGuideDialog).GetField("copyKey",Hidden).GetValue(guide)==0xDC,"First-run guide matches the single-key default");
                    typeof(StartupGuideDialog).GetMethod("RenderSlideKeybinds",Hidden).Invoke(guide,null);
                    var reset=Descendants(guide).OfType<Button>().First(button=>button.Text==Localization.Get("WizardBtnResetDefaults"));
                    typeof(Button).GetMethod("OnClick",Hidden).Invoke(reset,new object[]{EventArgs.Empty});
                    Check((int)typeof(StartupGuideDialog).GetField("copyModKey",Hidden).GetValue(guide)==0 && (int)typeof(StartupGuideDialog).GetField("copyKey",Hidden).GetValue(guide)==0xDC,"Reset button retains the single-key default without checkbox events re-enabling Ctrl");
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
                typeof(MapWindow).GetMethod("LoadSettings",Hidden).Invoke(window,null);
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
                typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).SetValue(persistWindow,true);
                typeof(MapWindow).GetMethod("SaveSettings",Hidden).Invoke(persistWindow,null);
            }
            using(var reopened=new MapWindow(root,true)) {
                Check((bool)typeof(MapWindow).GetField("suppressCopyKeyReminder",Hidden).GetValue(reopened)
                    && File.ReadAllText(Path.Combine(root,"settings.ini")).Contains("SuppressCopyKeyReminder=True"),"Copy-key reminder opt-out persists across restarts");
            }
        } finally {
            if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("MiniMap-voice-test-")) Directory.Delete(root,true);
        }
        Console.WriteLine("Passed "+count+" input and voice checks.");
    }
}
