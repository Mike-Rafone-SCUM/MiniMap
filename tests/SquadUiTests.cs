using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;
class SquadUiTests {
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static object Get(object o,string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,F).SetValue(o,v); }
    static object Call(object o,string n,params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static void Check(bool v,string n) { if(!v)throw new Exception(n);Console.WriteLine("PASS: "+n); }
    static void Pump() { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(20); }
    static void Await(Func<bool> test) { var end=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<end) { if(test())return;Pump(); }throw new Exception("UI fixture timed out"); }
    static IEnumerable<DependencyObject> Children(DependencyObject p) { foreach(object c in LogicalTreeHelper.GetChildren(p))if(c is DependencyObject) { yield return (DependencyObject)c;foreach(var d in Children((DependencyObject)c))yield return d; } }
    [STAThread]static void Main(string[] args) {
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));string folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"settings.ini"),"Welcomed=True\nLanguage=en\nWpfDefaultsRevision=1\n");
        var type=asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime");object runtime=Activator.CreateInstance(type,F,null,new object[]{folder,true},null);type.GetField("Active",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,runtime);
        IDisposable host=null,friend=null;
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");
            var hud=(Window)Get(runtime,"hud");hud.Left=-32000;hud.Top=-32000;
            var surface=asm.GetType("ScumMiniMap.WpfMapSurface");var arrow=surface.GetMethod("DrawPlayer",BindingFlags.NonPublic|BindingFlags.Static);var pixels=new List<byte[]>();
            foreach(int yaw in new[]{0,90}) { var visual=new DrawingVisual();using(var dc=visual.RenderOpen())arrow.Invoke(null,new object[]{dc,new Point(32,32),(double)yaw,Brushes.Cyan,true});var image=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);image.Render(visual);var bytes=new byte[64*64*4];image.CopyPixels(bytes,64*4,0);pixels.Add(bytes);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(folder,"player-arrow-"+yaw+".png")))png.Save(stream); }
            Check(!pixels[0].SequenceEqual(pixels[1]),"Shared self/teammate arrow renderer turns with player heading");
            var arrowState=Call(engine,"WpfSnapshot",true);var arrowPos=Activator.CreateInstance(Get(engine,"position").GetType());var arrowPeer=Activator.CreateInstance(asm.GetType("ScumMiniMap.PrivatePositions+Peer"),true);Set(arrowPeer,"Name","Arrow fixture");Set(arrowPeer,"Position",arrowPos);var arrowPeers=Array.CreateInstance(arrowPeer.GetType(),1);arrowPeers.SetValue(arrowPeer,0);Set(arrowState,"Peers",arrowPeers);var identity=type.GetMethod("VisualIdentity",BindingFlags.NonPublic|BindingFlags.Static);string previousIdentity=(string)identity.Invoke(null,new[]{arrowState});Set(arrowPos,"Yaw",90d);Check(previousIdentity!=(string)identity.Invoke(null,new[]{arrowState}),"Turning in place invalidates teammate map rendering");
            var groups=(Window)Call(runtime,"BuildRelayGroups");try { Check(Children(groups).OfType<TextBox>().Any(box=>(string)box.Tag=="new-group-label"&&box.MaxLength==48),"Group creation exposes a separate group-name field"); }finally { groups.Close(); }
            var tactical=(Window)Get(runtime,"tactical");Call(runtime,"ToggleTactical",false);Check(tactical.IsVisible&&(bool)Get(engine,"fullMapActive"),"Opening map synchronizes visible window and engine flag");
            Call(runtime,"SyncTacticalFocus",true,false);Check(tactical.IsVisible,"Gameplay focus keeps full map visible");Call(runtime,"SyncTacticalFocus",false,true);Check(tactical.IsVisible,"MiniMap controls remain usable");
            Call(runtime,"SyncTacticalFocus",false,false);Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive")&&!(bool)Get(runtime,"tacticalMouseShieldActive"),"Alt-tab hides full map, clears controller flag and releases mouse shield");
            Call(runtime,"SyncTacticalFocus",true,false);Check(!tactical.IsVisible,"Returning to SCUM does not reopen a dismissed map");Call(runtime,"ToggleTactical",false);Call(engine,"SetFullMap",false);Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive"),"Legacy chat and close paths hide the actual WPF window");
            for(int i=0;i<4;i++) { Call(runtime,"ToggleTactical",false);Call(runtime,"CloseTactical",false); }Check(!tactical.IsVisible&&!(bool)Get(engine,"fullMapActive"),"Repeated open and close requests do not leave a stuck map");
            Call(runtime,"ToggleTactical",false);Set(engine,"fullMapActive",false);Check((bool)Call(runtime,"TryDismissTactical",0x1B,0,true)&&!tactical.IsVisible,"Escape dismisses the visible map even with a stale flag and paused tracking");
            var direct=asm.GetType("ScumMiniMap.DirectPositions");var privateType=asm.GetType("ScumMiniMap.PrivatePositions");string key=(string)privateType.GetMethod("NewKey",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
            host=(IDisposable)Activator.CreateInstance(direct,F,null,new object[]{true,"",port,key,"Lead",false,null},null);friend=(IDisposable)Activator.CreateInstance(direct,F,null,new object[]{false,"127.0.0.1",port,key,"Brother",false,null},null);Set(runtime,"privatePositions",host);
            var pos=Get(engine,"position");Call(friend,"Update",pos,DateTime.UtcNow);Await(()=>((Array)Call(host,"Roster")).Length==1);Call(runtime,"Refresh");var roster=(Array)Get(runtime,"playerRoster");var peer=roster.GetValue(0);string id=(string)Get(peer,"Id");Check(((string)Call(runtime,"PeerStatus",peer)).Contains("Last updated"),"Teammate status shows capture age");
            Set(runtime,"followingPeer",id);Call(runtime,"UpdateSquad");var target=Get(engine,"searchTarget");Check(target!=null&&((string)Get(target,"Name")).Contains("Brother"),"Following targets the selected teammate");
            Check(!((Array)Call(engine,"WpfSharedMarkers")).Cast<object>().Any(m=>(string)Get(m,"Id")==new string('F',64)),"Following cannot bridge a teammate location into other groups as a GPS marker");
            var moved=Activator.CreateInstance(pos.GetType());foreach(string n in new[]{"X","Y","Z","Yaw"})Set(moved,n,Get(pos,n));Set(moved,"X",Convert.ToDouble(Get(pos,"X"))+20000);Call(friend,"Update",moved,DateTime.UtcNow);Set(runtime,"nextSquadRoute",DateTime.MinValue);Await(()=> { var p=((Array)Call(host,"Snapshot")).GetValue(0);return Convert.ToDouble(Get(Get(p,"Position"),"X"))==Convert.ToDouble(Get(moved,"X")); });Call(runtime,"UpdateSquad");Check(!object.ReferenceEquals(target,Get(engine,"searchTarget")),"Moving teammate updates the navigation destination");
            Call(friend,"Update",null,DateTime.UtcNow);Await(()=>Get(((Array)Call(host,"Roster")).GetValue(0),"Position")==null);Call(runtime,"UpdateSquad");Check(Get(engine,"searchTarget")==null&&(string)Get(runtime,"followingPeer")==id,"Stale or paused teammate position removes navigation until a fresh position arrives");
            Call(friend,"Update",moved,DateTime.UtcNow);Await(()=>Get(((Array)Call(host,"Roster")).GetValue(0),"Position")!=null);Call(runtime,"UpdateSquad");Check(Get(engine,"searchTarget")!=null,"Following resumes when the teammate publishes a fresh location");
            Call(runtime,"StopSquadNavigation");Check(Get(runtime,"followingPeer")==null,"Explicit stop cancels follow even while the teammate is stale");
            var squad=Get(runtime,"squad");var toMap=engine.GetType().GetMethod("ToMap",BindingFlags.Public|BindingFlags.Static);var firstPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{pos});var secondPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{moved});var finalPos=Activator.CreateInstance(pos.GetType());foreach(string n in new[]{"X","Y","Z","Yaw"})Set(finalPos,n,Get(pos,n));Set(finalPos,"X",Convert.ToDouble(Get(pos,"X"))+40000);var finalPoint=(System.Drawing.PointF)toMap.Invoke(null,new[]{finalPos});
            Call(squad,"AddStop","Bunker",(double)firstPoint.X,(double)firstPoint.Y);Call(squad,"AddStop","Fuel",(double)secondPoint.X,(double)secondPoint.Y);Call(squad,"AddStop","Trader",(double)finalPoint.X,(double)finalPoint.Y);
            Set(engine,"diagnosticMode",false);
            try { var stops=squad.GetType().GetProperty("Stops",F).GetValue(squad,null);Call(runtime,"StartSquadRoute",stops);Check((int)Get(runtime,"activeStop")==1,"Starting at the first stop advances to the second stop");Set(engine,"position",moved);Set(engine,"updated",DateTime.UtcNow);Call(runtime,"UpdateSquad");Check((int)Get(runtime,"activeStop")==2&&((string)Get(Get(engine,"searchTarget"),"Name")).StartsWith("3/3"),"Arrival advances ordered route navigation to the next stop");Set(engine,"position",finalPos);Call(runtime,"UpdateSquad");Check(Get(engine,"searchTarget")==null&&((Array)Get(runtime,"activeStops")).Length==0,"Completing the final route stop clears navigation"); }
            finally { Set(engine,"diagnosticMode",true); }
            var preset=Call(engine,"WpfCapturePreset","Driving");Call(engine,"WpfLayer","grid",!(bool)Get(preset,"Grid"));Call(engine,"WpfApplyPreset",preset);Check((bool)Get(Call(engine,"WpfSnapshot",false),"Grid")== (bool)Get(preset,"Grid"),"Applying preset restores actual map layers");
            bool inspected=false;var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) };timer.Tick+=(s,e)=> {
                timer.Stop();try { var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Squad tools");dialog.Left=-32000;dialog.Top=-32000;dialog.UpdateLayout();var labels=Children(dialog).OfType<Button>().Select(b=>b.Content as string).ToArray();Check(labels.Contains("Danger")&&labels.Contains("Loot")&&labels.Contains("Pick me up")&&labels.Contains("Save current map preset"),"Squad panel exposes pings, route controls and presets");var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(dialog);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"squad-tools.png")))encoder.Save(file);inspected=true;dialog.Close(); }catch { foreach(var w in Application.Current.Windows.OfType<Window>().ToArray())if(w!=tactical)w.Close();throw; }
            };timer.Start();Call(runtime,"ShowSquadTools");Check(inspected,"Actual squad panel rendered and closed successfully");
        }finally { type.GetField("Active",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);Set(runtime,"privatePositions",null);if(friend!=null)friend.Dispose();if(host!=null)host.Dispose();((IDisposable)runtime).Dispose(); }
    }
}
