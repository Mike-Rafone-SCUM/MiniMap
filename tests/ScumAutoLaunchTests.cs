using System;
using System.IO;
using System.Xml;
using ScumMiniMap;
class ScumAutoLaunchTests {
    static int checks;
    static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++; }
    static int Main(string[] args) {
        var now=new DateTime(2026,10,20,0,0,0,DateTimeKind.Utc);
        var state=new ScumSessionLifetime();
        Check(!state.Observe(true,false,now),"manual launch without game stays open");
        Check(!state.Observe(true,true,now),"game detected");
        Check(!state.Observe(true,false,now),"exit starts grace period");
        Check(!state.Observe(true,false,now.AddSeconds(9)),"grace period");
        Check(state.Observe(true,false,now.AddSeconds(10)),"confirmed exit");
        state=new ScumSessionLifetime();state.Observe(true,true,now);state.Observe(true,false,now);
        Check(!state.Observe(true,true,now.AddSeconds(5)),"rapid game restart cancels shutdown");
        Check(!state.Observe(true,false,now.AddSeconds(10)),"new grace period after restart");
        Check(!state.Observe(true,null,now.AddSeconds(21)),"unknown observation does not close");
        Check(!state.Observe(true,false,now.AddSeconds(22)),"unknown resets exit confirmation");
        Check(!state.Observe(false,false,now.AddSeconds(40)),"disable cancels shutdown");
        Check(!state.Observe(true,false,now.AddSeconds(80)),"reenable needs game observation");
        state=new ScumSessionLifetime(true);
        Check(!state.Observe(true,false,now),"game ended during startup");
        Check(state.Observe(true,false,now.AddSeconds(10)),"auto launch still closes if game ended during startup");
        var xml=new XmlDocument();xml.LoadXml(ScumAutoLaunch.TaskXml("S-1-5-21-test",@"C:\Map & Test\watch.ps1"));
        var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("t",xml.DocumentElement.NamespaceURI);
        Check(xml.SelectSingleNode("//t:LogonTrigger/t:UserId",ns).InnerText=="S-1-5-21-test","per-user trigger");
        Check(xml.SelectSingleNode("//t:RunLevel",ns).InnerText=="HighestAvailable","app elevation");
        Check(xml.SelectSingleNode("//t:ExecutionTimeLimit",ns).InnerText=="PT0S","watcher survives long sessions");
        Check(xml.SelectSingleNode("//t:Arguments",ns).InnerText.Contains("\"C:\\Map & Test\\watch.ps1\""),"quoted path roundtrip");
        File.WriteAllText(args[0],ScumAutoLaunch.WatcherScript);
        Console.WriteLine(checks+" auto-launch lifetime/task checks passed.");return 0;
    }
}
namespace ScumMiniMap {
    public static class Program {
        internal static string DataFolderName="SyntheticAutoLaunchTest";
        internal static bool IsTestBuild=true;
        internal static void LogException(string name,Exception error) { throw error; }
    }
    internal static class InstallationAssets { internal static void Validate() { } }
    internal static class Localization { internal static string Get(string key) { return key; } }
}
