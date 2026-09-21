using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    public static class Program {
#if MINIMAP_TEST
        internal static readonly bool IsTestBuild=true;
#else
        internal static readonly bool IsTestBuild=false;
#endif
        internal static string DataFolderName { get { return IsTestBuild?"ScumMiniMap-ResponsivenessTest":"ScumMiniMap"; } }
        internal static int MinimumCopyIntervalMs { get { return 250; } }
        internal const int DefaultCopyModifierKey = 0;
        internal const int DefaultCopyKey = 0xDC;
        internal static int TrackingIntervalMs(int configured) { return Math.Max(MinimumCopyIntervalMs,configured); }
        internal static int TrackingIntervalMs(int configured,int modifier) { return Math.Max(modifier>0?1000:MinimumCopyIntervalMs,configured); }
        internal static int CopyResponseTimeoutMs(int modifier) { return modifier>0?1000:350; }
        internal static int CopyRetryDelayMs(int modifier) { return modifier>0?5000:1000; }
        internal static string CopyBindingHelp {
            get {
                string[] text={
                    "New default: backslash (\\), no modifier. Bind Copy location in SCUM to the same key. Existing bindings are kept. Single-key tracking supports 250 ms; Ctrl+C remains supported at 1 second or slower.",
                    "Nuevo valor: barra inversa (\\), sin modificador. Asigná Copiar ubicación en SCUM a esa misma tecla. Se conservan tus teclas anteriores. Una tecla permite 250 ms; Ctrl+C sigue disponible a 1 segundo o más.",
                    "Nouveau défaut : barre oblique inverse (\\), sans modificateur. Affectez Copier la position dans SCUM à cette même touche. Vos touches sont conservées. Touche seule : 250 ms ; Ctrl+C : 1 seconde minimum.",
                    "Neuer Standard: Backslash (\\), ohne Zusatztaste. Position kopieren in SCUM auf dieselbe Taste legen. Bestehende Belegungen bleiben erhalten. Einzelne Taste: 250 ms; Strg+C: mindestens 1 Sekunde.",
                    "Nieuwe standaard: backslash (\\), zonder modifier. Stel Locatie kopiëren in SCUM in op dezelfde toets. Bestaande bindingen blijven behouden. Eén toets: 250 ms; Ctrl+C: minimaal 1 seconde.",
                    "Новый стандарт: обратная косая черта (\\), без модификатора. Назначьте копирование координат в SCUM на ту же клавишу. Старые привязки сохраняются. Одна клавиша: 250 мс; Ctrl+C: от 1 секунды.",
                    "新默认按键：反斜杠（\\），无修饰键。请在SCUM中将复制位置设为相同按键。保留已有绑定。单键支持250毫秒；Ctrl+C最短为1秒。",
                    "Yeni varsayılan: ters eğik çizgi (\\), değiştiricisiz. SCUM Konumu kopyala tuşunu da aynı yapın. Mevcut atamalar korunur. Tek tuş: 250 ms; Ctrl+C: en az 1 saniye.",
                    "الافتراضي الجديد: الشرطة المائلة العكسية (\\)، دون مفتاح تعديل. عيّن نسخ الموقع في SCUM إلى المفتاح نفسه. تُحفظ التعيينات الحالية. مفتاح واحد: 250 مللي ثانية؛ Ctrl+C: ثانية على الأقل."
                };
                return text[(int)Localization.Current];
            }
        }
        internal static int UpgradeCopyInterval(int configured,bool revisionSeen) {
            return !revisionSeen && configured==1000 ? 250 : configured;
        }



        internal static string LocalUpdateSource;



        internal static void LogException(string source, Exception ex) {



            try {



                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName);



                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);



                string logFile = Path.Combine(appData, "automatic.log");



                string msg = string.Format("{0} [{1}] {2}: {3}{4}{5}{4}",



                    DateTime.UtcNow.ToString("o"),



                    source,



                    ex != null ? ex.GetType().FullName : "Unknown",



                    ex != null ? ex.Message : "No message",



                    Environment.NewLine,



                    ex != null ? ex.StackTrace : "");



                File.AppendAllText(logFile, msg);



            } catch {}



        }



        [STAThread]



        public static void Main(string[] args) {
            if(args!=null && args.Length==2 && args[0]=="-ApplyVerifiedUpdate") { UpdateInstaller.Run(args[1]); return; }



            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);



            Application.ThreadException += (s, e) => LogException("ThreadException", e.Exception);



            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException("UnhandledException", e.ExceptionObject as Exception);



            TaskScheduler.UnobservedTaskException += (s, e) => {



                LogException("UnobservedTaskException", e.Exception);



                e.SetObserved();



            };



            Application.EnableVisualStyles();



            string baseFolder=AppDomain.CurrentDomain.BaseDirectory;



            string appData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),DataFolderName);



            try {



                if(!Directory.Exists(appData)) Directory.CreateDirectory(appData);



                // Migrate existing config files from executable folder if present



                string[] toMigrate=new[]{"settings.ini","automatic.log"};



                foreach(string f in toMigrate) {



                    string src=Path.Combine(baseFolder,f);



                    string dst=Path.Combine(appData,f);



                    if(File.Exists(src) && !File.Exists(dst)) {



                        try { File.Copy(src,dst); } catch {}



                    }



                }



            } catch {}



            if(args!=null && args.Length>0) {



                bool check=false,preview=false,checkAuto=false;



                string screenshot=null,localUpdateSource=null;



                for(int i=0;i<args.Length;i++) {



                    string a=args[i];



                    if(a.Equals("-Check",StringComparison.OrdinalIgnoreCase)||a.Equals("/Check",StringComparison.OrdinalIgnoreCase)||a.Equals("--check",StringComparison.OrdinalIgnoreCase)) check=true;



                    else if(a.Equals("-Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("/Preview",StringComparison.OrdinalIgnoreCase)||a.Equals("--preview",StringComparison.OrdinalIgnoreCase)) preview=true;



                    else if(a.Equals("-CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("/CheckAutomatic",StringComparison.OrdinalIgnoreCase)||a.Equals("--check-automatic",StringComparison.OrdinalIgnoreCase)) checkAuto=true;



                    else if((a.Equals("-ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("/ImportScreenshot",StringComparison.OrdinalIgnoreCase)||a.Equals("--import-screenshot",StringComparison.OrdinalIgnoreCase)) && i+1<args.Length) { screenshot=args[++i]; }



                    else if((a.Equals("-TestLocalUpdate",StringComparison.OrdinalIgnoreCase)||a.Equals("/TestLocalUpdate",StringComparison.OrdinalIgnoreCase)||a.Equals("--test-local-update",StringComparison.OrdinalIgnoreCase)) && i+1<args.Length) { localUpdateSource=args[++i]; }



                }



                LocalUpdateSource=localUpdateSource;



                if(check) {



                    MapWindow.SelfTest();



                    Console.WriteLine("Compilation and coordinate checks passed.");



                    return;



                }



                if(checkAuto) {



                    using(MapWindow miniForm=new MapWindow(appData,true)) {



                        miniForm.CheckAutomaticImport(screenshot,Path.Combine(baseFolder,"automatic-editor-preview.png"));



                    }



                    Console.WriteLine("Automatic import and naming checks passed.");



                    return;



                }



                if(preview) {



                    using(MapWindow miniForm=new MapWindow(appData,true)) {



                        miniForm.Accept("{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");



                        miniForm.Show();



                        Application.DoEvents();



                        miniForm.SaveOverlayPreview(Path.Combine(baseFolder,"preview.png"));



                        miniForm.SaveSettingsPreview(Path.Combine(baseFolder,"settings-preview.png"));



                        miniForm.CheckSearchPreview(Path.Combine(baseFolder,"search-preview.png"));



                        miniForm.CheckAppearance();



                        miniForm.CheckStaticTerrain();



                        miniForm.CheckZoneEditor(Path.Combine(baseFolder,"zones-preview.png"));



                    }



                    return;



                }



            }



            bool createdNew;



            using(System.Threading.Mutex mutex=new System.Threading.Mutex(true,"Local\\ScumMiniMapOverlay",out createdNew)) {



                if(!createdNew) {
                    if(IsTestBuild) MessageBox.Show("Exit the running SCUM MiniMap from its tray menu before starting this test package.","SCUM MiniMap responsiveness test");
                    return;
                }



                try {



                    using(MapWindow window=new MapWindow(appData)) {
                        string updateStatus=Path.Combine(appData,"update-result.txt");
                        window.Shown+=(s,e)=>UpdateInstaller.CleanupHelper(updateStatus);
                        if(File.Exists(updateStatus)) {
                            string result=File.ReadAllText(updateStatus); File.Delete(updateStatus);
                            window.Shown+=(s,e)=>MessageBox.Show(result,Localization.Get("UpdateCheckTitle"));
                        }



                        if(!string.IsNullOrEmpty(LocalUpdateSource)) window.Shown+=(s,e)=>window.RunLocalUpdateTest(LocalUpdateSource);



                        Application.Run(window);



                    }



                } catch(Exception ex) {



                    LogException("ApplicationRun", ex);



                }



            }



        }



    }

}
