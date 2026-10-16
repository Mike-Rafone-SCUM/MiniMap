using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Captures the actual packaged WPF interface using synthetic, loopback-only peers.
class WpfShowcaseHarness {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object value,string name) { return value.GetType().GetField(name,Flags).GetValue(value); }
    static void Set(object value,string name,object data) { value.GetType().GetField(name,Flags).SetValue(value,data); }
    static object Call(object value,string name,params object[] args) { return value.GetType().GetMethod(name,Flags).Invoke(value,args); }
    static IEnumerable<DependencyObject> Children(DependencyObject parent) { foreach(object item in LogicalTreeHelper.GetChildren(parent)) { var child=item as DependencyObject;if(child==null)continue;yield return child;foreach(var nested in Children(child))yield return nested; } }
    static void Pump() { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(20); }
    static string Json(string value) { return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\""; }
    static void Shot(Window window,string file,double scale=1) {
        window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000;window.Show();window.UpdateLayout();for(int i=0;i<5;i++)Pump();window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*scale),(int)(window.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))encoder.Save(stream);window.Hide();Console.WriteLine(Path.GetFileName(file));
    }
    [STAThread]static void Main(string[] args) {
        string output=Path.GetFullPath(args[1]),profile=Path.Combine(output,"isolated-profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True\nLanguage="+args[2]+"\nWpfDefaultsRevision=1\n");
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var runtimeType=assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime");object runtime=Activator.CreateInstance(runtimeType,Flags,null,new object[]{profile,true},null);var peers=new List<IDisposable>();
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");Set(engine,"fullMapOpacity",100);Set(engine,"fullMapZoom",2f);Set(engine,"gridOpacity",20);Call(engine,"WpfRotate",true);Call(engine,"WpfAppearance","Circle",90);
            var position=Get(engine,"position");var point=(System.Drawing.PointF)engine.GetType().GetMethod("ToMap",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new[]{position});
            var direct=assembly.GetType("ScumMiniMap.DirectPositions");var markerType=assembly.GetType("ScumMiniMap.SharedMapMarker");var positionType=assembly.GetType("ScumMiniMap.Position");var privateType=assembly.GetType("ScumMiniMap.PrivatePositions");string key=(string)privateType.GetMethod("NewKey",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
            object host=Activator.CreateInstance(direct,Flags,null,new object[]{true,"",port,key,"Team lead",false,null},null);peers.Add((IDisposable)host);Set(runtime,"privatePositions",host);
            for(int peerIndex=0;peerIndex<2;peerIndex++) {
                object friend=Activator.CreateInstance(direct,Flags,null,new object[]{false,"127.0.0.1",port,key,peerIndex==0?"Raven":"Scout",false,null},null);peers.Add((IDisposable)friend);
                var friendPosition=Activator.CreateInstance(positionType);foreach(string coordinate in new[]{"X","Y","Z","Yaw"})Set(friendPosition,coordinate,Get(position,coordinate));Set(friendPosition,"X",Convert.ToDouble(Get(position,"X"))+(peerIndex==0?30000d:-30000d));Set(friendPosition,"Y",Convert.ToDouble(Get(position,"Y"))+(peerIndex==0?50000d:-50000d));Call(friend,"Update",friendPosition,DateTime.UtcNow);
                var markers=Array.CreateInstance(markerType,3);for(int index=0;index<3;index++) {var marker=Activator.CreateInstance(markerType);Set(marker,"Id",index==0?new string('F',64):new string((char)('A'+index),64));Set(marker,"Name",new[]{"Fuel stop","Rally point","Death"}[index]);Set(marker,"Death",index==2);Set(marker,"X",Math.Max(.01,Math.Min(.99,point.X+(peerIndex==0?.025:-.05)*(index+1))));Set(marker,"Y",Math.Max(.01,Math.Min(.99,point.Y+(peerIndex==0?.045:-.03)*(index+1))));markers.SetValue(marker,index);}Call(friend,"UpdateMarkers",markers);
            }
            DateTime deadline=DateTime.UtcNow.AddSeconds(6);while(((Array)Call(host,"Roster")).Length<2&&DateTime.UtcNow<deadline)Pump();if(((Array)Call(host,"Roster")).Length!=2)throw new Exception("Synthetic peers did not connect");
            Call(runtime,"Refresh");var tactical=(Window)Get(runtime,"tactical");tactical.Width=1500;tactical.Height=900;Shot(tactical,Path.Combine(output,"01-private-player-map.png"));
            var saved=Children((DependencyObject)Get(runtime,"sidebar")).OfType<System.Windows.Controls.Primitives.ToggleButton>().First(button=>((string)button.Tag).EndsWith(":4"));saved.IsChecked=false;saved.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Shot(tactical,Path.Combine(output,"02-player-visibility.png"));
            Shot((Window)Get(runtime,"hud"),Path.Combine(output,"03-rotating-minimap.png"),2);
            foreach(var screen in new[]{Tuple.Create("BuildOnboarding",5,"04-setup-appearance.png"),Tuple.Create("BuildOnboarding",6,"05-setup-colours-layers.png"),Tuple.Create("BuildOnboarding",7,"06-setup-tracking-voice.png"),Tuple.Create("BuildOnboarding",2,"07-setup-keybindings.png")}) {var window=(Window)Call(runtime,screen.Item1,screen.Item2);try {Shot(window,Path.Combine(output,screen.Item3));}finally {window.Close();}}
            Set(runtime,"privatePositions",null);Window connection=null;try {connection=(Window)Call(runtime,"BuildPrivatePositions");connection.Height=900;var name=Children(connection).OfType<TextBox>().First();name.Text="Team lead";Children(connection).OfType<TextBox>().Single(input=>(input.Tag as string)=="private-position-address").Text="203.0.113.10";Children(connection).OfType<PasswordBox>().Single().Password=Convert.ToBase64String(new byte[32]);Shot(connection,Path.Combine(output,"08-private-host-join.png"));}finally {if(connection!=null)connection.Close();Set(runtime,"privatePositions",host);Call(runtime,"Refresh");}
            var filters=(Window)Call(runtime,"BuildPoiFilters");try {Shot(filters,Path.Combine(output,"09-poi-filters.png"));}finally {filters.Close();}
            Call(engine,"WpfWaypoint","Rally point",(System.Drawing.PointF?)new System.Drawing.PointF(point.X+.01f,point.Y+.01f));Call(engine,"SaveDeathMarker",position,false);Call(engine,"WpfTarget",((System.Collections.IList)Call(engine,"WpfSearch","grid D4"))[0]);deadline=DateTime.UtcNow.AddSeconds(5);while((bool)Get(Call(engine,"WpfSnapshot",true),"Calculating")&&DateTime.UtcNow<deadline)Pump();Call(runtime,"Refresh");Shot(tactical,Path.Combine(output,"10-shared-navigation.png"));
            string[] captionKeys={"PrivatePositions","SharedPlayers","RotateMinimap","FieldAppearance","SecMapZones","SecTrackingZoom","FieldMatchKeys","PrivatePositions","ConfigurePoiFilters","SetWaypoint"};var images=Directory.GetFiles(output,"*.png").OrderBy(file=>file,StringComparer.Ordinal).ToArray();var getText=assembly.GetType("ScumMiniMap.Localization").GetMethod("Get",BindingFlags.Static|BindingFlags.Public);string version=(string)engine.GetType().GetField("VersionString",BindingFlags.Public|BindingFlags.Static).GetRawConstantValue();
            File.WriteAllText(Path.Combine(output,"gallery.json"),"{\"version\":"+Json(version)+",\"language\":"+Json(args[2])+",\"syntheticPlayers\":true,\"images\":["+string.Join(",",images.Select((file,index)=>"{\"filename\":"+Json(Path.GetFileName(file))+",\"description\":"+Json((string)getText.Invoke(null,new object[]{captionKeys[index]}))+"}"))+"]}");
        }finally {Set(runtime,"privatePositions",null);foreach(var peer in peers)peer.Dispose();((IDisposable)runtime).Dispose();}
    }
}
