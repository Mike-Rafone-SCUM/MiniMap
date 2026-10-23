using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

class GroupMemberUiTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static object Prop(object o,string n) { return o.GetType().GetProperty(n,F).GetValue(o,null); }
    static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n,F).Invoke(o,args); }
    static void Set(object o,string n,object value) { o.GetType().GetField(n,F).SetValue(o,value); }
    static void Check(bool value,string message) { if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message); }
    static void Pump() { var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>frame.Continue=false),DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame); }
    static void Await(Func<bool> value) { for(int i=0;i<300;i++) { Pump();if(value())return;Thread.Sleep(25); }throw new Exception("Timed out waiting for member UI"); }
    static System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject root) { for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var item in Children(child))yield return item; } }
    [STAThread]static void Main(string[] args) {
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=asm.GetType("ScumMiniMap.RelayGroupStore");string folder=Path.GetFullPath(args[1]);
        var runtime=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{folder,true},null);object member=null;
        try {
            string address=args.Length>4?args[4]:"127.0.0.1";
            var owner=Activator.CreateInstance(type,F,null,new object[]{Path.Combine(folder,"host"),address,int.Parse(args[2]),args[3],null},null);
            Set(runtime,"relayGroups",owner);Call(owner,"SetName","Synthetic host");
            member=Activator.CreateInstance(type,F,null,new object[]{Path.Combine(folder,"member"),address,int.Parse(args[2]),args[3],null},null);Call(member,"SetName","Synthetic member");
            var group=Call(owner,"Create","UI member fixture");string code=(string)Prop(group,"Code");var joined=Call(member,"Join",code);
            Await(()=>(bool)Prop(Get(joined,"Session"),"Ready")&&(bool)Prop(Get(group,"Session"),"Ready"));
            var tools=(Window)Call(runtime,"BuildRelayGroups");tools.Left=tools.Top=-32000;tools.Show();Pump();
            var manage=Children(tools).OfType<Button>().Single(b=>b.Tag as string=="group-members:"+(string)Get(group,"Id"));Check(manage.IsEnabled,"The host has an enabled Manage members action");
            Exception failure=null;
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=> {
                Window dialog=null;
                try {
                    dialog=Application.Current.Windows.Cast<Window>().Single(w=>w.Tag as string=="group-members");dialog.Left=dialog.Top=-32000;
                    Await(()=>Children(dialog).OfType<Button>().Any(b=>(b.Tag as string??"").StartsWith("group-disconnect:")));
                    Check(Children(dialog).OfType<TextBlock>().Any(t=>t.Text.Contains("Synthetic member")),"Member management displays the decrypted member name");
                    Check(Children(dialog).OfType<Button>().Count(b=>(b.Tag as string??"").StartsWith("group-disconnect:"))==1,"Only the non-owner member has a Disconnect control");
                    if(args.Length>5&&int.Parse(args[5])>0) {
                        var clock=System.Diagnostics.Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<int.Parse(args[5])) { Pump();Thread.Sleep(100); }
                        Await(()=>Children(dialog).OfType<Button>().Any(b=>(b.Tag as string??"").StartsWith("group-disconnect:")&&b.IsEnabled));
                        string error=(string)asm.GetType("ScumMiniMap.Localization").GetMethod("Get").Invoke(null,new object[]{"GroupMembersFailed"});
                        Check(!Children(dialog).OfType<TextBlock>().Any(t=>t.Text==error),"Sustained member polling stays responsive within the live firewall connection limit");
                    }
                    var button=Children(dialog).OfType<Button>().Single(b=>(b.Tag as string??"").StartsWith("group-disconnect:"));button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Await(()=>!(bool)Prop(Get(joined,"Session"),"Ready"));
                    Check((bool)Prop(Get(group,"Session"),"Ready"),"Clicking Disconnect removes the member and keeps the host connected");
                    Await(()=>!Children(dialog).OfType<Button>().Any(b=>(b.Tag as string??"").StartsWith("group-disconnect:")));
                    Check(true,"The member list refreshes after the disconnect completes");
                    string offline=(string)asm.GetType("ScumMiniMap.Localization").GetMethod("Get").Invoke(null,new object[]{"SquadOffline"});
                    Check(Children(dialog).OfType<TextBlock>().Any(t=>t.Text.Contains("Synthetic member")&&t.Text.Contains(offline)),"The disconnected member remains visibly labelled offline");
                    var offlineImage=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);offlineImage.Render(dialog);var offlinePng=new System.Windows.Media.Imaging.PngBitmapEncoder();offlinePng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(offlineImage));using(var stream=File.Create(Path.Combine(folder,"offline-member.png")))offlinePng.Save(stream);
                    Call(member,"Connect",joined);Await(()=>(bool)Prop(Get(joined,"Session"),"Ready"));
                    Await(()=>Children(dialog).OfType<Button>().Any(b=>(b.Tag as string??"").StartsWith("group-disconnect:")));
                    Check((string)Prop(group,"Code")==code,"Rejoining uses the unchanged invite code and returns to the live member list");
                    var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(dialog);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(folder,"host-member-management.png")))png.Save(stream);
                }catch(Exception error) { failure=error; }finally { if(dialog!=null)dialog.Close(); }
            }),DispatcherPriority.ApplicationIdle);
            manage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));tools.Close();if(failure!=null)throw failure;Call(owner,"Delete",group);
        }finally { if(member!=null)((IDisposable)member).Dispose();((IDisposable)runtime).Dispose(); }
    }
}
