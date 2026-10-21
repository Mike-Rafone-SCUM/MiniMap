using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

class OpacitySetupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    static object Get(object o,string name) {return o.GetType().GetField(name,F).GetValue(o);}
    static void Set(object o,string name,object value) {o.GetType().GetField(name,F).SetValue(o,value);}
    static object Call(object o,string name,params object[] args) {return o.GetType().GetMethod(name,F).Invoke(o,args);}
    static void Check(bool value,string message) {if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Pump() {var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>frame.Continue=false),DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);}
    static bool Above(Window above,Window below) {var target=new WindowInteropHelper(below).Handle;var handle=new WindowInteropHelper(above).Handle;for(int i=0;i<2048&&handle!=IntPtr.Zero;i++) {handle=GetWindow(handle,2);if(handle==target)return true;}return false;}
    static object Snapshot(object engine) {return Call(engine,"WpfSnapshot",true);}
    static void Opacity(object runtime) {
        var engine=Get(runtime,"engine");Set(engine,"fullMapOpacity",73);Set(engine,"fullMapZoom",1f);
        Check((int)Get(Snapshot(engine),"Opacity")==0,"Old manual opacity cannot make the fully zoomed-out tactical terrain visible");
        var file=(string)Get(engine,"settingsPath");File.AppendAllText(file,"\nFullMapOpacity=35\nWpfTacticalOpacityOverride=True\n");Call(engine,"LoadSettings");
        var compact=Call(engine,"WpfSnapshot",false);int compactOpacity=(int)Get(compact,"Opacity");
        foreach(float zoom in new[]{1f,1.0001f,2f,8f,1f}) {Set(engine,"fullMapZoom",zoom);Check((int)Get(Snapshot(engine),"Opacity")== (zoom>1?100:0),"Loaded legacy override cannot bypass automatic opacity at zoom "+zoom);}
        var preset=Call(engine,"WpfCapturePreset","Synthetic legacy opacity");Set(preset,"TacticalOpacity",63);Set(preset,"TacticalZoom",3f);Check((bool)Call(engine,"WpfApplyPreset",preset)&&(int)Get(Snapshot(engine),"Opacity")==100,"A saved map preset cannot override the zoomed opacity lock");
        Set(preset,"TacticalZoom",1f);Call(engine,"WpfApplyPreset",preset);Check((int)Get(Snapshot(engine),"Opacity")==0,"A saved map preset cannot override the overview transparency lock");
        Call(runtime,"Refresh");var slider=(Slider)Get(runtime,"tacticalOpacity");Check(!slider.IsEnabled&&slider.Value==0,"The overview toolbar displays locked 0% opacity");slider.Value=44;Check(slider.Value==0&&(int)Get(Snapshot(engine),"Opacity")==0,"A programmatic slider change is reset to the automatic overview value");
        Set(engine,"fullMapZoom",2f);Call(runtime,"RefreshCamera");Check(!slider.IsEnabled&&slider.Value==100&&((TextBlock)Get(runtime,"tacticalOpacityValue")).Text=="100%","Camera-only updates immediately display locked 100% opacity");
        slider.Value=35;Check(slider.Value==100&&(int)Get(Snapshot(engine),"Opacity")==100,"A programmatic slider change cannot unlock zoomed opacity");
        var map=(FrameworkElement)Activator.CreateInstance(engine.GetType().Assembly.GetType("ScumMiniMap.WpfMapSurface"),true);map.Width=640;map.Height=480;map.Measure(new Size(640,480));map.Arrange(new Rect(0,0,640,480));
        var terrain=new DrawingGroup();using(var dc=terrain.Open())dc.DrawRectangle(Brushes.Blue,null,new Rect(0,0,1024,1024));terrain.Freeze();Set(map,"BaseMap",terrain);
        foreach(float zoom in new[]{2f,1f,3f,1f}) {
            Set(engine,"fullMapZoom",zoom);var state=Snapshot(engine);Set(state,"Store",null);Set(state,"Grid",false);Set(state,"GridLabels",false);Set(state,"Zones",false);Set(state,"Labels",false);Set(state,"History",false);Set(state,"Waypoints",false);Set(state,"Saved",Array.CreateInstance(engine.GetType().Assembly.GetType("ScumMiniMap.MapZone"),0));
            Call(map,"BeginCameraFrames");Call(map,"PublishState",state);Call(map,"UpdateTacticalLayers");map.InvalidateVisual();var bitmap=new RenderTargetBitmap(640,480,96,96,PixelFormats.Pbgra32);bitmap.Render(map);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(320,240,1,1),pixel,4,0);
            Console.WriteLine("Terrain pixel: "+string.Join(",",pixel));Check(zoom>1?pixel[0]==255&&pixel[3]==255:pixel[0]==0&&pixel[3]<=1,"Rendered camera preview switches terrain immediately at zoom "+zoom);Call(map,"EndCameraFrames");
        }
        Check((int)Get(Call(engine,"WpfSnapshot",false),"Opacity")==compactOpacity,"Automatic tactical opacity leaves compact HUD opacity unchanged");
        bool diagnostic=(bool)Get(engine,"diagnosticMode");try {Set(engine,"diagnosticMode",false);Call(engine,"WpfSave");}finally {Set(engine,"diagnosticMode",diagnostic);}
        Check(!File.ReadAllText(file).Contains("WpfTacticalOpacityOverride="),"Saving settings removes the obsolete manual tactical override");Call(engine,"LoadSettings");Set(engine,"fullMapZoom",1f);Check((int)Get(Snapshot(engine),"Opacity")==0,"Overview transparency stays locked after saving and reloading");
    }
    static void Setup(object runtime) {
        var foreground=GetForegroundWindow();var game=new Window {Title="Synthetic existing game window",Topmost=true,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000,Width=900,Height=650};game.Show();Pump();
        try {
            Call(runtime,"ShowSetupWindow",false);Pump();var setup=(Window)Get(runtime,"setupWindow");
            Check(setup!=null&&setup.IsVisible&&setup.Topmost&&Above(setup,game),"Automatic setup is raised above an already-open topmost game fixture");
            Check(setup.Owner==null&&setup.ShowInTaskbar&&setup.WindowStyle==WindowStyle.SingleBorderWindow,"Raised setup keeps normal taskbar, move and minimize controls");
            Call(runtime,"SetSetupInputFocus",true);Check((bool)Get(Get(runtime,"engine"),"panelOpening")&&setup.Topmost,"Focused setup protects game input and stays above the game");
            Call(runtime,"SetSetupInputFocus",false);Check(!setup.Topmost&&!(bool)Get(Get(runtime,"engine"),"panelOpening"),"Alt-Tab releases setup's topmost state and game input protection");
            setup.WindowState=WindowState.Minimized;Call(runtime,"ShowSetupWindow",true);Pump();Check(Object.ReferenceEquals(setup,Get(runtime,"setupWindow"))&&setup.WindowState==WindowState.Normal&&setup.Topmost&&Above(setup,game),"Reopening restores and raises the same setup window above the running game");
            var child=(Window)Call(runtime,"BuildSettings",0);bool childAbove=false;child.ContentRendered+=(s,e)=> {childAbove=child.Topmost&&child.Owner==setup&&Above(child,setup);child.Close();};Call(runtime,"Modal",child);Check(childAbove,"Tools opened from setup stay above their topmost parent");
            setup.Close();Pump();Check(Get(runtime,"setupWindow")==null&&!(bool)Get(Get(runtime,"engine"),"panelOpening"),"Closing setup clears the input and window state");
            Check(GetForegroundWindow()==foreground,"Diagnostic game/setup fixtures do not steal actual desktop foreground");
        }finally {var setup=Get(runtime,"setupWindow") as Window;if(setup!=null)setup.Close();game.Close();}
    }
    [STAThread]static void Main(string[] args) {
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var runtime=Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{Path.GetFullPath(args[1]),true},null);
        try {if(args[2]!="setup")Opacity(runtime);if(args[2]!="opacity")Setup(runtime);}finally {((IDisposable)runtime).Dispose();}
    }
}
