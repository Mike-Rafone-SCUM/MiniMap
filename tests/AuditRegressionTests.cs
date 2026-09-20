using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScumMiniMap;

static class AuditRegressionTests {
    static int count;
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; Console.WriteLine("PASS: "+name); }
    static void Reject(Action action,string name) {
        try { action(); } catch(InvalidDataException) { Check(true,name); return; }
        throw new Exception("Expected data rejection: "+name);
    }
    static FieldInfo Field(object value,string name) { return value.GetType().GetField(name,Hidden); }
    static object Call(object value,string name,params object[] args) { return value.GetType().GetMethod(name,Hidden).Invoke(value,args); }
    static string Hash(string path) { using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    static byte[] Binary(Action<BinaryWriter> action) {
        using(var stream=new MemoryStream()) { using(var writer=new BinaryWriter(stream)) { action(writer); return stream.ToArray(); } }
    }
    static MapZone Zone(string name) { return new MapZone { Name=name,Category=ZoneCategory.Custom,Argb=Color.Cyan.ToArgb(),Points=new[]{new PointF(.5f,.5f)} }; }
    static Control FindControl(Control parent,string text) {
        foreach(Control child in parent.Controls) {
            if(child.Text==text) return child;
            Control found=FindControl(child,text); if(found!=null) return found;
        }
        return null;
    }
    static NumericUpDown SettingNumber(Control parent,string key) {
        Control label=FindControl(parent,Localization.Get(key));
        foreach(Control child in label.Parent.Controls) if(child is NumericUpDown) return (NumericUpDown)child;
        throw new Exception("Missing setting: "+key);
    }
    static void PumpUntil(Func<bool> done) {
        DateTime timeout=DateTime.UtcNow.AddSeconds(10);
        while(!done() && DateTime.UtcNow<timeout) { Application.DoEvents(); Thread.Sleep(10); }
        if(!done()) throw new Exception("Async test timed out.");
    }
    [STAThread] public static int Main(string[] args) {
        if(args.Length==2 && args[0]=="-ApplyVerifiedUpdate") { UpdateInstaller.Run(args[1]); return 0; }
        if(args.Length==2 && args[0]=="-StartUpdate") {
            using(var reader=new BinaryReader(File.OpenRead(Encoding.UTF8.GetString(Convert.FromBase64String(args[1])))))
                UpdateInstaller.Start(reader.ReadString(),reader.ReadString(),reader.ReadString());
            return 0;
        }
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-audit-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string zoneFile=Path.Combine(root,"zones.tsv");
            var zones=new List<MapZone>();
            for(int i=0;i<501;i++) zones.Add(Zone("Waypoint "+i));
            ZoneStore.Save(zoneFile,zones);
            Check(ZoneStore.Load(zoneFile).Count==501,"501 zones survive save/reload");
            foreach(string name in new[]{"Custom Bunker","Gas Cache","D4 Clock house"}) {
                var sample=new List<MapZone>{Zone(name)}; ZoneStore.Save(zoneFile,sample);
                Check(ZoneStore.Load(zoneFile)[0].Category==ZoneCategory.Custom,"Explicit category survives: "+name);
            }
            byte[] before=File.ReadAllBytes(zoneFile);
            var tooMany=new List<MapZone>(); for(int i=0;i<=ZoneStore.MaxZones;i++) tooMany.Add(Zone("x"));
            Reject(()=>ZoneStore.Save(zoneFile,tooMany),"Oversized save rejected");
            Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(zoneFile)),"Rejected save preserves disk content");
            Reject(()=>ZoneStore.Load(new MemoryStream(Encoding.UTF8.GetBytes("bad\t1\tNaN,0.5"))),"Non-finite zone rejected");
            Reject(()=>ZoneStore.Load(new MemoryStream(Encoding.UTF8.GetBytes(new string('x',ZoneStore.MaxLineLength+1)))),"Long zone line rejected before splitting");
            using(var wizard=new StartupGuideDialog(0x4D,0x54,0,0x43,null,null))
                Check((int)Field(wizard,"copyModKey").GetValue(wizard)==0,"No-modifier survives wizard reopening");

            var eventField=typeof(Localization).GetField("LanguageChanged",BindingFlags.NonPublic|BindingFlags.Static);
            Func<int> subscribers=()=> { var d=(Delegate)eventField.GetValue(null); return d==null?0:d.GetInvocationList().Length; };
            int originalSubscribers=subscribers();
            for(int i=0;i<20;i++) using(var frame=new Form()) OverlayTheme.Frame(frame,"Audit",()=>{});
            Check(subscribers()==originalSubscribers,"Disposed framed dialogs unsubscribe from localization");

            string map=Path.Combine(root,"map.png");
            using(var bitmap=new Bitmap(32,32)) bitmap.Save(map,System.Drawing.Imaging.ImageFormat.Png);
            byte[] validMap=File.ReadAllBytes(map); File.WriteAllText(map,"invalid image"); string warning;
            using(var recovered=SafeMapImage.Load(map,()=>new MemoryStream(validMap),out warning))
                Check(recovered.Width==32 && warning!=null && File.ReadAllText(map)=="invalid image","Bad custom image falls back without deleting user data");
            File.WriteAllBytes(map,validMap);

            // Exercise the real transaction entry point under a locked destination.
            using(var window=new MapWindow(root,true)) {
                var minimap=(OverlayWindow)Field(window,"overlay").GetValue(window);
                Check(minimap.Size==new Size(240,240) && !(bool)Field(window,"fullMapActive").GetValue(window),
                    "Fresh installation starts with compact minimap, not full map");
                Rectangle compactBounds=minimap.MinimapBounds;
                Call(window,"SetFullMap",true);
                Rectangle expandedBounds=minimap.Bounds;
                Check(minimap.MinimapBounds==compactBounds,"Full map retains normal bounds for settings persistence");
                Call(window,"BuildSettingsPanel");
                Check(SettingNumber((Control)Field(window,"bar").GetValue(window),"MapWidth").Value==240 &&
                    minimap.Bounds==expandedBounds,"Settings in full-map mode show compact dimensions without shrinking full map");
                minimap.SetMinimapSize(new Size(280,260));
                Check(minimap.Bounds==expandedBounds && minimap.MinimapBounds.Size==new Size(280,260),
                    "Editing compact dimensions does not resize active full map");
                Call(window,"SetFullMap",false);
                Check(minimap.Size==new Size(280,260) && minimap.Location==compactBounds.Location,
                    "Closing full map restores chosen compact size and original position");
                File.WriteAllLines(Path.Combine(root,"settings.ini"),new[]{
                    "Welcomed=True","Language=en","Width=420","Height=340","ShowHuntingLegend=False","ShowGasStations=True",
                    "ShowCities=True","ShowCustomWaypoints=False","GridBorders=False",
                    "CopyInterval=3","PlayerConeColor=#123456","AutoZoomMin=12","AutoZoomMax=4"});
                Call(window,"LoadSettings");
                Check(minimap.Size==new Size(420,340),"Existing users retain their saved minimap dimensions");
                Check(!(bool)Field(window,"showCustomWaypoints").GetValue(window) &&
                    !(bool)Field(window,"gridBorders").GetValue(window),"Old settings preserve active layer preferences");
                Check((int)Field(window,"copyIntervalMs").GetValue(window)==3000 &&
                    ((Color)Field(window,"playerConeColor").GetValue(window)).ToArgb()==ColorTranslator.FromHtml("#123456").ToArgb(),
                    "Legacy interval and player colour aliases remain compatible");
                Check((int)Field(window,"autoZoomMax").GetValue(window)==12,"Inverted saved zoom bounds are normalised");
                Call(window,"BuildSettingsPanel");
                Control settings=(Control)Field(window,"bar").GetValue(window);
                var minimum=SettingNumber(settings,"AutoZoomMin");
                var maximum=SettingNumber(settings,"AutoZoomMax");
                minimum.Value=20;
                Check(maximum.Value==20 && (int)Field(window,"autoZoomMax").GetValue(window)==20,"Raising minimum updates maximum control and state");
                maximum.Value=5;
                Check(minimum.Value==5 && (int)Field(window,"autoZoomMin").GetValue(window)==5,"Lowering maximum updates minimum control and state");
                Field(window,"gridBorders").SetValue(window,true);
                Call(window,"BuildSettingsPanel");
                Check(((TacticalCheckBox)FindControl(settings,Localization.Get("GridBorders"))).Checked,"Rebuilt settings reflect sidebar layer changes");
                var current=(List<MapZone>)Field(window,"zones").GetValue(window);
                var added=new List<MapZone>(current); added.Add(Zone("Must not commit"));
                var deleted=new List<MapZone>();
                using(var locked=new FileStream(zoneFile,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    Check(!(bool)Call(window,"TryCommitZones",added,null),"Locked-file addition reports failure");
                    Check(Object.ReferenceEquals(current,Field(window,"zones").GetValue(window)),"Failed addition preserves live collection");
                    Check(!(bool)Call(window,"TryCommitZones",deleted,null),"Locked-file deletion reports failure");
                    Check(Object.ReferenceEquals(current,Field(window,"zones").GetValue(window)),"Failed deletion preserves live collection");
                }
                Call(window,"BeginCopyRequest");
                Field(window,"pending").SetValue(window,false); // timer consumed an early response
                Call(window,"CompleteCopyRequest",CopyResult.Sent);
                Check(!(bool)Field(window,"pending").GetValue(window),"Early clipboard response is not rearmed after chord completion");
                Call(window,"BeginCopyRequest"); Call(window,"CompleteCopyRequest",CopyResult.Cancelled);
                Check(!(bool)Field(window,"pending").GetValue(window),"Cancelled copy clears its pending request");

                RoadRouter.Instance.InitializeFromResource();
                var gate=typeof(RoadRouter).GetField("searchLock",Hidden).GetValue(RoadRouter.Instance);
                var target=Zone("A"); target.Points=new[]{new PointF(.6727f,.03878f)};
                Field(window,"searchTarget").SetValue(window,target);
                Monitor.Enter(gate);
                try {
                    Call(window,"UpdateRouteAsync",new PointF(.62767f,.63618f),target);
                    Field(window,"searchTarget").SetValue(window,Zone("B"));
                } finally { Monitor.Exit(gate); }
                PumpUntil(()=>!(bool)Field(window,"routeCalculating").GetValue(window));
                Check(Field(window,"activeRoute").GetValue(window)==null,"Superseded route completion cannot publish");
                Field(window,"searchTarget").SetValue(window,target);
                Monitor.Enter(gate);
                try { Call(window,"UpdateRouteAsync",new PointF(.62767f,.63618f),target); Field(window,"searchTarget").SetValue(window,null); }
                finally { Monitor.Exit(gate); }
                PumpUntil(()=>!(bool)Field(window,"routeCalculating").GetValue(window));
                Check(Field(window,"activeRoute").GetValue(window)==null,"Cleared route completion cannot publish");
            }

            Reject(()=>RoadRouter.Instance.LoadBinary(new BinaryReader(new MemoryStream(new byte[]{1}))),"Truncated road header rejected");
            byte[] badRoad=Binary(w=> { w.Write(Encoding.ASCII.GetBytes("ROAD")); w.Write(1u); w.Write(int.MaxValue); w.Write(1); w.Write(128); });
            Reject(()=>RoadRouter.Instance.LoadBinary(new BinaryReader(new MemoryStream(badRoad))),"Oversized road counts rejected before allocation");
            byte[] badPoi=Binary(w=> { w.Write(Encoding.ASCII.GetBytes("SCMP")); w.Write((ushort)1); w.Write((ushort)0); w.Write(-1); });
            Reject(()=>new ScumMapStore().ReadBinary(new MemoryStream(badPoi)),"Negative POI count rejected");
            byte[] nanPoi=Binary(w=> {
                w.Write(Encoding.ASCII.GetBytes("SCMP")); w.Write((ushort)1); w.Write((ushort)1);
                w.Write(1); w.Write((byte)1); w.Write((byte)'s'); w.Write((byte)1); w.Write((byte)'c');
                w.Write(0); w.Write(0); w.Write((byte)1); w.Write(1);
                w.Write(1); w.Write(1); w.Write(float.NaN); w.Write(.5f); w.Write((ushort)0);
            });
            Reject(()=>new ScumMapStore().ReadBinary(new MemoryStream(nanPoi)),"Non-finite POI coordinate rejected");

            string installDir=Path.Combine(root,"Unicode-é-%PATH%"); Directory.CreateDirectory(installDir);
            string installed=Path.Combine(installDir,"app.exe"), staged=installed+".update";
            File.WriteAllText(installed,"old"); File.WriteAllText(staged,"new");
            Reject(()=>UpdateInstaller.ReplaceAndLaunch(staged,installed,"wrong",path=>{}),"Updater rejects modified staging file");
            Check(File.ReadAllText(installed)=="old","Hash rejection retains installed executable");
            bool failed=false;
            try { UpdateInstaller.ReplaceAndLaunch(staged,installed,Hash(staged),path=> { throw new IOException("Simulated launch failure"); }); } catch(IOException) { failed=true; }
            Check(failed && File.ReadAllText(installed)=="old","Failed restart rolls back installation");
            File.WriteAllText(staged,"new");
            using(var locked=new FileStream(installed,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                failed=false; try { UpdateInstaller.ReplaceAndLaunch(staged,installed,Hash(staged),path=>{}); } catch(IOException) { failed=true; }
                Check(failed,"Locked installation rejects update");
            }
            Check(File.ReadAllText(installed)=="old","Locked installation remains unchanged");
            bool launched=false;
            UpdateInstaller.ReplaceAndLaunch(staged,installed,Hash(staged),path=> { launched=path==installed; });
            Check(launched && File.ReadAllText(installed)=="new","Unicode and percent paths replace successfully without shell expansion");

            // Real helper process and real executable restart, isolated from the installed app.
            File.Copy(args[0],staged); string status=Path.Combine(root,"result.txt");
            string config=Path.Combine(root,"request.bin");
            using(var writer=new BinaryWriter(File.Create(config))) {
                writer.Write(staged); writer.Write(installed); writer.Write(status);
            }
            using(var helper=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName=Assembly.GetExecutingAssembly().Location,Arguments="-StartUpdate "+Convert.ToBase64String(Encoding.UTF8.GetBytes(config)),UseShellExecute=false,CreateNoWindow=true
            })) { if(!helper.WaitForExit(15000)) { helper.Kill(); throw new Exception("Helper timed out"); } Check(helper.ExitCode==0,"Dedicated updater helper exits normally"); }
            PumpUntil(()=>File.Exists(Path.Combine(installDir,"started.txt")));
            Check(File.ReadAllText(status).StartsWith("Update installed"),"Helper records success and starts replacement executable");
            PumpUntil(()=>UpdateInstaller.CleanupHelper(status));
            Check(!File.Exists(status+".helper"),"Completed helper files are safely removed");
            foreach(AppLanguage language in Enum.GetValues(typeof(AppLanguage))) {
                Localization.Current=language;
                Check(Localization.T("PoiSectionSummary","A",2,3)!="PoiSectionSummary","POI section translated: "+language);
                Check(Localization.Get("ResetDefaultMapConfirm")!="ResetDefaultMapConfirm" &&
                    Localization.Get("ResetDefaultMapDone")!="ResetDefaultMapDone" &&
                    Localization.T("MapResetFailed","test error").Contains("test error"),
                    "Reset-map messages resolve and format: "+language);
            }
            Console.WriteLine("Passed "+count+" audit regression checks."); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally {
            // This directory was created by this harness under the system temp directory.
            try { Directory.Delete(root,true); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
    }
}
