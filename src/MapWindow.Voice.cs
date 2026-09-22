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
            string[] recalculating={"Recalculating route","Recalculando ruta","Recalcul de l’itinéraire","Route wird neu berechnet","Route wordt opnieuw berekend","Пересчёт маршрута","正在重新规划路线","Rota yeniden hesaplanıyor","جارٍ إعادة حساب المسار","Recalculando rota"};
            string[] wrong={"Wrong way","Dirección incorrecta","Mauvaise direction","Falsche Richtung","Verkeerde richting","Неверное направление","行驶方向错误","Yanlış yön","اتجاه خاطئ","Direção errada"};
            int language=(int)Localization.Current;
            return (wrongWay?wrong[language]+" — ":"")+recalculating[language];
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
        static string RouteUnavailableText {
            get {
                string[] errors={"Road route unavailable","Ruta por carretera no disponible","Itinéraire routier indisponible","Straßenroute nicht verfügbar","Wegroute niet beschikbaar","Автомобильный маршрут недоступен","道路路线不可用","Karayolu rotası bulunamadı","مسار الطريق غير متاح","Rota rodoviária indisponível"};
                return errors[(int)Localization.Current];
            }
        }
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
            OverlayTheme.Section(panel,VoiceText(0));
            AddCheck(panel,VoiceText(1),voiceEnabled,value=>{ voiceEnabled=value; StopVoice(); voiceNavigator.Reset(); });
            var row=new FlowLayoutPanel { Width=390,Height=34 };
            row.Controls.Add(new Label { Text=VoiceText(2),Width=110,Padding=new Padding(0,5,0,0) });
            var voices=new TacticalComboBox { Width=240 };
            string[] available=VoicePacks.Discover(VoiceRoot);
            voices.Items.AddRange(available);
            voices.SelectedItem=voiceName;
            if(voices.SelectedIndex<0 && available.Length>0) { voices.SelectedIndex=0; voiceName=available[0]; }
            voices.SelectedIndexChanged+=(s,e)=>{ voiceName=(string)voices.SelectedItem; StopVoice(); voiceNavigator.Reset(); SettingsChanged(); };
            row.Controls.Add(voices); panel.Controls.Add(row);
            AddNumber(panel,VoiceText(3),0,100,voiceVolume,value=>{ voiceVolume=value; StopVoice(); }).Increment=5;
            var preview=new Button { Text=VoiceText(4),Width=280,Enabled=available.Length>0 };
            preview.Click+=(s,e)=>{ voicePreview=true; SpeakVoice(new[]{VoicePacks.Clips[10],VoicePacks.Clips[2],VoicePacks.Clips[5]}); };
            panel.Controls.Add(preview);
            var help=new Label { Text=VoiceText(5),Width=365,Height=48 };
            panel.Controls.Add(help);
        }
        static string VoiceText(int index) {
            string[][] labels={
                new[]{"Voice navigation (work in progress)","Enable voice navigation","Voice","Voice volume (%)","Preview voice","Add voice folders with the same 12 MP3 filenames under your app data's voice-navigation folder, then reopen Settings."},
                new[]{"Navegación por voz (en desarrollo)","Activar navegación por voz","Voz","Volumen de voz (%)","Probar voz","Agregá carpetas de voces con los mismos 12 nombres MP3 en voice-navigation dentro de los datos de la app y volvé a abrir Ajustes."},
                new[]{"Navigation vocale (en développement)","Activer la navigation vocale","Voix","Volume vocal (%)","Écouter la voix","Ajoutez des dossiers avec les 12 mêmes noms MP3 dans voice-navigation des données de l’application, puis rouvrez les paramètres."},
                new[]{"Sprachnavigation (in Entwicklung)","Sprachnavigation aktivieren","Stimme","Sprachlautstärke (%)","Stimme anhören","Weitere Stimmen: Ordner mit denselben 12 MP3-Dateinamen unter voice-navigation in den App-Daten ablegen. Einstellungen erneut öffnen."},
                new[]{"Spraaknavigatie (in ontwikkeling)","Spraaknavigatie inschakelen","Stem","Stemvolume (%)","Stem beluisteren","Voeg stemmappen met dezelfde 12 MP3-bestandsnamen toe aan voice-navigation in de appgegevens en open Instellingen opnieuw."},
                new[]{"Голосовая навигация (в разработке)","Включить голосовую навигацию","Голос","Громкость голоса (%)","Прослушать голос","Добавьте папки голосов с теми же 12 именами MP3 в voice-navigation в данных приложения и снова откройте настройки."},
                new[]{"语音导航（开发中）","启用语音导航","语音","语音音量 (%)","试听语音","在应用数据的 voice-navigation 文件夹中添加包含相同12个MP3文件名的语音文件夹，然后重新打开设置。"},
                new[]{"Sesli navigasyon (geliştirme aşamasında)","Sesli navigasyonu etkinleştir","Ses","Ses düzeyi (%)","Sesi önizle","Uygulama verilerindeki voice-navigation klasörüne aynı 12 MP3 adıyla ses klasörleri ekleyin ve Ayarlar'ı yeniden açın."},
                new[]{"الملاحة الصوتية (قيد التطوير)","تفعيل الملاحة الصوتية","الصوت","مستوى الصوت (%)","معاينة الصوت","أضف مجلدات أصوات بأسماء ملفات MP3 الاثني عشر نفسها في voice-navigation ضمن بيانات التطبيق، ثم أعد فتح الإعدادات."},
                new[]{"Navegação por voz (em desenvolvimento)","Ativar navegação por voz","Voz","Volume da voz (%)","Ouvir prévia da voz","Adicione pastas de vozes com os mesmos 12 nomes de MP3 em voice-navigation na pasta de dados do aplicativo e reabra as Configurações."}
            };
            return labels[(int)Localization.Current][index];
        }
    }
}
