param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PackageDirectory)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output) { throw 'Choose a fresh showcase directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$exe=Join-Path $package 'SkynettMiniMap.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$source=@'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using ScumMiniMap;
class Showcase {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static string output;
    static void Set(object o,string name,object value) { o.GetType().GetField(name,Hidden).SetValue(o,value); }
    static object Get(object o,string name) { return o.GetType().GetField(name,Hidden).GetValue(o); }
    static object Call(object o,string name,params object[] values) { return o.GetType().GetMethod(name,Hidden).Invoke(o,values); }
    static void Pump() { Application.DoEvents(); }
    static void Save(Form form,string name) {
        form.Show(); form.PerformLayout(); Pump();
        using(var bitmap=new Bitmap(form.Width,form.Height)) {
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.Combine(output,name+".png"),ImageFormat.Png);
        }
        form.Hide();
    }
    static void Map(MapWindow map,string name) {
        Set(map,"terrainKey",null); Set(map,"lastFrameKey",null);
        map.SaveOverlayPreview(Path.Combine(output,name+".png"));
    }
    [STAThread] static void Main(string[] args) {
        output=args[0]; Application.EnableVisualStyles();
        Localization.Current=AppLanguage.English;
        using(var guide=new StartupGuideDialog()) {
            guide.ClientSize=new Size(1000,1040);
            for(int slide=0;slide<5;slide++) {
                Set(guide,"currentSlide",slide); Call(guide,"UpdateSlide");
                Save(guide,new[]{"01-setup-overview","02-controls","03-key-binding-setup","04-input-safety","05-features"}[slide]);
            }
        }
        var reminderType=typeof(MapWindow).Assembly.GetType("ScumMiniMap.CopyKeyReminderDialog");
        using(var reminder=(Form)Activator.CreateInstance(reminderType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[0],null)) Save(reminder,"06-copy-key-reminder");
        string profile=Path.Combine(output,"isolated-profile"); Directory.CreateDirectory(profile);
        using(var map=new MapWindow(profile,true)) {
            ((System.Windows.Forms.Timer)Get(map,"timer")).Stop();
            RoadRouter.Instance.InitializeFromResource();
            Set(map,"saveAfter",DateTime.MaxValue);
            Call(map,"Accept","{X=-336602.375 Y=-270302.625 Z=18853.264|P=-3.814117 Y=13.952554 R=0.000000}");
            var overlay=(Form)Get(map,"overlay"); overlay.Size=new Size(640,668);
            Set(map,"mapOpacity",100); Set(map,"autoZoom",false); Set(map,"zoom",4f);
            Map(map,"07-circular-minimap");
            Set(map,"overlayShape","Square"); Map(map,"08-square-minimap");
            map.SaveSettingsPreview(Path.Combine(output,"09-settings.png"));
            map.CheckSearchPreview(Path.Combine(output,"10-waypoint-search.png"));
            var deadline=DateTime.UtcNow.AddSeconds(20);
            while((bool)Get(map,"routeCalculating") && DateTime.UtcNow<deadline) { Pump(); System.Threading.Thread.Sleep(20); }
            var route=(RoadRoute)Get(map,"activeRoute");
            if(route==null || !route.Success) throw new Exception("Demonstration road route failed.");
            // Use the real renderer with full-map geometry, without gameplay focus or hotkeys.
            Set(map,"fullMapActive",true); overlay.GetType().GetProperty("FullMapMode").SetValue(overlay,true,null); overlay.Size=new Size(1400,1080);
            Map(map,"11-full-island-map");
            Set(map,"fullMapZoom",4f); Set(map,"fullMapPan",new PointF(.35f,.55f));
            Map(map,"12-full-map-detail");
            Set(map,"fullMapActive",false); overlay.GetType().GetProperty("FullMapMode").SetValue(overlay,false,null); overlay.Size=new Size(640,668);
            Map(map,"13-destination-navigation");
            map.CheckZoneEditor(Path.Combine(output,"14-zone-editor.png"));
        }
        File.WriteAllText(Path.Combine(output,"README.md"),"# SCUM MiniMap showcase\n\nCaptured directly from the packaged executable using its Windows Forms controls and map renderer. Coordinates and the Grid D4 destination are demonstration data in an isolated profile; these are app captures, not live SCUM gameplay screenshots.\n\nThe original community keyboard image is included without alteration.\n");
    }
}
'@
$sourcePath=Join-Path $output 'Showcase.cs'
[IO.File]::WriteAllText($sourcePath,$source)
$harness=Join-Path $output 'Showcase.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$harness" "/reference:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sourcePath
if($LASTEXITCODE -ne 0) { throw 'Showcase harness compilation failed.' }
Copy-Item -LiteralPath $exe -Destination $output
& $harness $output
if($LASTEXITCODE -ne 0) { throw 'Showcase capture failed.' }
Remove-Item -LiteralPath (Join-Path $output 'SkynettMiniMap.exe'),$harness,$sourcePath
Copy-Item -LiteralPath (Join-Path $package 'keyboard-setup.png') -Destination $output
Compress-Archive -Path (Join-Path $output '*.png'),(Join-Path $output 'README.md') -DestinationPath (Join-Path $output 'SCUM-MiniMap-Showcase.zip')
Write-Output "Showcase suite ready: $output"
