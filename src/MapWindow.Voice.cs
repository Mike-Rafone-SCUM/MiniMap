using System;
using System.IO;
using System.Windows.Forms;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        bool voiceEnabled;
        string voiceName=VoicePacks.DefaultName;
        int voiceVolume=80;
        VoicePlayer voicePlayer;
        readonly VoiceNavigator voiceNavigator=new VoiceNavigator();
        MapZone voiceTarget;
        bool voiceArrivalPlaying,voicePreview;
        bool voicesInstalled;
        DateTime nextVoiceUpdate=DateTime.MinValue;
        DateTime nextVoiceReroute=DateTime.MinValue,voiceStatusUntil=DateTime.MinValue;
        string voiceStatus="",playingAction;
        string cachedVoiceName,cachedVoiceDirectory;
        Position lastVoicePosition;
        RoadRoute lastVoiceRoute;
        bool lastVoiceBusy;
        string VoiceStatus { get { return DateTime.UtcNow<voiceStatusUntil?voiceStatus:""; } }
        static string RerouteText(bool wrongWay) {
            return (wrongWay?Localization.Get("VoiceWrongWay")+" — ":"")+Localization.Get("VoiceRecalculating");
        }
        string VoiceRoot { get { return Path.Combine(dataFolder,"voice-navigation"); } }
        void EnsureVoices() {
            if(voicesInstalled) return;
            try { VoicePacks.InstallBundled(VoiceRoot); voicesInstalled=true; }
            catch(Exception ex) { Program.LogException("VoiceInstall",ex); }
        }
        string SelectedVoiceDirectory() {
            if(cachedVoiceName==voiceName) return cachedVoiceDirectory;
            EnsureVoices();
            cachedVoiceName=voiceName; cachedVoiceDirectory=null;
            foreach(string name in VoicePacks.Discover(VoiceRoot)) if(name==voiceName) { cachedVoiceDirectory=Path.Combine(VoiceRoot,name); break; }
            return cachedVoiceDirectory;
        }
        void SpeakVoice(string[] clips) {
            string directory=SelectedVoiceDirectory(); if(directory==null) return;
            if(voicePlayer==null) voicePlayer=new VoicePlayer();
            voicePlayer.Volume=voiceVolume; voicePlayer.Speak(directory,clips);
            playingAction=clips.Length>0?clips[clips.Length-1]:null;
        }
        void StopVoice() { if(voicePlayer!=null)voicePlayer.Stop(); voicePreview=false; voiceArrivalPlaying=false; playingAction=null; }
        static string RouteUnavailableText { get { return Localization.Get("VoiceUnavailable"); } }
        void FinishVoiceReroute(bool success) {
            if(success) {
                voiceNavigator.RouteReplaced(); voiceStatusUntil=DateTime.MinValue;
            } else {
                voiceStatus=RouteUnavailableText; voiceStatusUntil=DateTime.UtcNow.AddSeconds(5);
            }
            lastFrameKey=null;
        }
        void VoiceArrived() {
            if(!voiceEnabled || diagnosticMode || !Native.GameFocused() || chat.Paused) return;
            voiceTarget=null; voiceNavigator.Reset(); voiceArrivalPlaying=true;
            SpeakVoice(new[]{VoicePacks.Clips[11]});
        }
        void TickVoice(DateTime now,bool gameFocused) {
            if(voicePreview && SettingsVisible && voicePlayer!=null && voicePlayer.Busy) return;
            voicePreview=false;
            if(!voiceEnabled || !gameFocused || chat.Paused || SettingsVisible || position==null || (now-updated).TotalSeconds>5) { StopVoice(); return; }
            if(!Object.ReferenceEquals(voiceTarget,searchTarget)) {
                StopVoice(); voiceNavigator.Reset(); voiceTarget=searchTarget; voiceStatusUntil=DateTime.MinValue; lastVoicePosition=null;
            }
            if(searchTarget==null) {
                if(!voiceArrivalPlaying) StopVoice();
                return;
            }
            if(now<nextVoiceUpdate) return;
            bool audioBusy=voicePlayer!=null && voicePlayer.Busy;
            if(Object.ReferenceEquals(lastVoicePosition,position) && Object.ReferenceEquals(lastVoiceRoute,activeRoute)
                && lastVoiceBusy==audioBusy && !voiceNavigator.NeedsReroute) return;
            lastVoicePosition=position; lastVoiceRoute=activeRoute; lastVoiceBusy=audioBusy;
            nextVoiceUpdate=now.AddMilliseconds(100);
            string[] clips=voiceNavigator.Update(activeRoute,ToMap(position),now,audioBusy);
            if(voiceNavigator.NeedsReroute) {
                if(playingAction!=VoicePacks.Clips[9] && playingAction!=VoicePacks.RecalculatingClip) StopVoice();
                if(now>=nextVoiceReroute && RequestRouteAsync(ToMap(position),searchTarget,true)) {
                    nextVoiceReroute=now.AddSeconds(3);
                    voiceStatus=RerouteText(voiceNavigator.WrongWay);
                    voiceStatusUntil=DateTime.MaxValue;
                    string directory=SelectedVoiceDirectory();
                    if(clips==null && directory!=null && File.Exists(Path.Combine(directory,VoicePacks.RecalculatingClip)))
                        SpeakVoice(new[]{VoicePacks.RecalculatingClip});
                }
            }
            // Routine route refreshes and single uncertain samples must not truncate a phrase.
            if(clips!=null) SpeakVoice(clips);
        }
        void BuildVoiceSettings(FlowLayoutPanel panel) {
            cachedVoiceName=null;
            EnsureVoices();
            OverlayTheme.Section(panel,Localization.Get("VoiceHeading"));
            AddCheck(panel,Localization.Get("VoiceEnable"),voiceEnabled,value=>{ voiceEnabled=value; StopVoice(); voiceNavigator.Reset(); });
            var row=new FlowLayoutPanel { Width=390,Height=34 };
            row.Controls.Add(new Label { Text=Localization.Get("VoiceName"),Width=110,Padding=new Padding(0,5,0,0) });
            var voices=new TacticalComboBox { Width=240 };
            string[] available=VoicePacks.Discover(VoiceRoot);
            voices.Items.AddRange(available);
            voices.SelectedItem=voiceName;
            if(voices.SelectedIndex<0 && available.Length>0) { voices.SelectedIndex=0; voiceName=available[0]; }
            voices.SelectedIndexChanged+=(s,e)=>{ voiceName=(string)voices.SelectedItem; StopVoice(); voiceNavigator.Reset(); SettingsChanged(); };
            row.Controls.Add(voices); panel.Controls.Add(row);
            AddNumber(panel,Localization.Get("VoiceVolume"),0,100,voiceVolume,value=>{ voiceVolume=value; StopVoice(); }).Increment=5;
            var preview=new Button { Text=Localization.Get("VoicePreview"),Width=280,Enabled=available.Length>0 };
            preview.Click+=(s,e)=>{ voicePreview=true; SpeakVoice(new[]{VoicePacks.Clips[10],VoicePacks.Clips[2],VoicePacks.Clips[5]}); };
            panel.Controls.Add(preview);
            var help=new Label { Text=Localization.Get("VoiceHelp"),Width=365,Height=48 };
            panel.Controls.Add(help);
        }

    }
}
