using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace ScumMiniMap {
    internal sealed class ScumSessionLifetime {
        bool seenGame;
        DateTime? missingSince;
        internal ScumSessionLifetime(bool autoStarted=false) { seenGame=autoStarted; }
        internal bool Observe(bool enabled,bool? running,DateTime now) {
            if(!enabled) { seenGame=false;missingSince=null;return false; }
            if(!running.HasValue) { missingSince=null;return false; }
            if(running.Value) { seenGame=true;missingSince=null;return false; }
            if(!seenGame)return false;
            if(!missingSince.HasValue)missingSince=now;
            return now-missingSince.Value>=TimeSpan.FromSeconds(10);
        }
    }
    internal sealed class ScumAutoLaunch:IDisposable {
        internal static string Label { get { return Localization.Get("ScumAutoLaunchLabel"); } }
        internal static string Help { get { return Localization.Get("ScumAutoLaunchHelp"); } }
        static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Program.DataFolderName,"auto-launch"); } }
        static string EnabledFile { get { return Path.Combine(Folder,"enabled.txt"); } }
        static string TaskName { get { return "SCUM MiniMap Auto Launch"+(Program.IsTestBuild?" Test":"")+" "+WindowsIdentity.GetCurrent().User.Value; } }
        internal static bool Enabled { get { return File.Exists(EnabledFile); } }
        readonly Timer timer;
        readonly ScumSessionLifetime lifetime=new ScumSessionLifetime(Array.IndexOf(Environment.GetCommandLineArgs(),"--auto-scum")>=0);
        int busy,disposed;
        internal ScumAutoLaunch(Action exit) {
            timer=new Timer(_=> {
                if(Interlocked.Exchange(ref busy,1)!=0)return;
                try {
                    bool enabled=Enabled;
                    if(Volatile.Read(ref disposed)==0&&lifetime.Observe(enabled,enabled?GameRunning():null,DateTime.UtcNow)) {
                        if(Interlocked.Exchange(ref disposed,1)==0)exit();
                    }
                }catch(Exception error) { Program.LogException("ScumAutoClose",error); }
                finally { Volatile.Write(ref busy,0); }
            },null,2000,2000);
        }
        internal static bool? GameRunning() {
            try {
                int session;using(var own=Process.GetCurrentProcess())session=own.SessionId;
                foreach(string name in new[]{"SCUM","SCUM-Win64-Shipping"}) {
                    var processes=Process.GetProcessesByName(name);
                    bool found=false,unknown=false;
                    foreach(var process in processes)using(process) {
                        try { if(!process.HasExited&&process.SessionId==session)found=true; }
                        catch(InvalidOperationException) { }
                        catch(System.ComponentModel.Win32Exception) { unknown=true; }
                    }
                    if(found)return true;
                    if(unknown)return null;
                }
                return false;
            }catch { return null; }
        }
        static void Scheduler(string arguments) {
            using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"schtasks.exe"),arguments) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true
            })) {
                var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(15000)) { process.Kill();throw new IOException("Windows Task Scheduler did not respond."); }
                if(process.ExitCode!=0)throw new IOException("Windows Task Scheduler: "+output.Result+error.Result);
            }
        }
        internal static string TaskXml(string sid,string script) {
            Func<string,string> escape=value=>SecurityElement.Escape(value);
            string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
            return "<?xml version=\"1.0\" encoding=\"utf-16\"?><Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+escape(sid)+"</UserId></LogonTrigger></Triggers><Principals><Principal id=\"User\"><UserId>"+escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings><Actions Context=\"User\"><Exec><Command>"+escape(powershell)+"</Command><Arguments>"+escape("-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \""+script+"\"")+"</Arguments></Exec></Actions></Task>";
        }
        internal static void SetEnabled(bool enabled) {
            if(!enabled) {
                if(File.Exists(EnabledFile))File.Delete(EnabledFile);
                Scheduler("/Delete /TN \""+TaskName+"\" /F");return;
            }
            string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
            InstallationAssets.Validate();Directory.CreateDirectory(Folder);
            string script=Path.Combine(Folder,"watch-scum.ps1"),xml=Path.Combine(Folder,"task.xml");
            File.WriteAllText(script,WatcherScript,new UTF8Encoding(true));
            File.WriteAllText(xml,TaskXml(WindowsIdentity.GetCurrent().User.Value,script),Encoding.Unicode);
            Scheduler("/Create /TN \""+TaskName+"\" /XML \""+xml+"\" /F");
            try {
                File.WriteAllText(EnabledFile,exe,new UTF8Encoding(false));
                Scheduler("/Run /TN \""+TaskName+"\"");
            }catch {
                if(File.Exists(EnabledFile))File.Delete(EnabledFile);
                try { Scheduler("/Delete /TN \""+TaskName+"\" /F"); }catch { }
                throw;
            }
        }
        public void Dispose() { Interlocked.Exchange(ref disposed,1);timer.Dispose(); }
        // No mapped app executable: the background watcher cannot block updates.
        internal const string WatcherScript=@"param([switch]$LibraryOnly)
$ErrorActionPreference='Stop'
function Get-ScumRunning {
    try {
        $session=[Diagnostics.Process]::GetCurrentProcess().SessionId
        $found=$false
        foreach($name in @('SCUM','SCUM-Win64-Shipping')) {
            foreach($p in [Diagnostics.Process]::GetProcessesByName($name)) {
                try { if(-not $p.HasExited -and $p.SessionId -eq $session){$found=$true} }
                finally { $p.Dispose() }
            }
        }
        return $found
    }catch { return $null }
}
function Test-MiniMapRunning {
    try { $m=[Threading.Mutex]::OpenExisting('Local\ScumMiniMapOverlay');$m.Dispose();return $true }
    catch [Threading.WaitHandleCannotBeOpenedException] { return $false }
}
function Invoke-ScumWatchCycle($state,$running,$now,$exe) {
    if($null -eq $running){$state.Missing=$null;return}
    if($running) {
        $state.Missing=$null
        if(-not $state.Launched) {
            if(-not (Test-MiniMapRunning)) {
                if(-not [IO.File]::Exists($exe)){throw 'MiniMap was moved or deleted. Enable automatic launch again from its new folder.'}
                Start-Process -FilePath $exe -ArgumentList '--auto-scum' -WorkingDirectory ([IO.Path]::GetDirectoryName($exe))
            }
            $state.Launched=$true
        }
    }elseif($state.Launched) {
        if($null -eq $state.Missing){$state.Missing=$now}
        if(($now-$state.Missing).TotalSeconds -ge 10){$state.Launched=$false}
    }
}
if($LibraryOnly){return}
$enabled=Join-Path $PSScriptRoot 'enabled.txt'
$id=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$watcherProfile=[IO.Path]::GetFileName([IO.Path]::GetDirectoryName($PSScriptRoot))
$created=$false
$mutex=New-Object Threading.Mutex($true,('Local\ScumMiniMapWatcher-'+$id+'-'+$watcherProfile),[ref]$created)
if(-not $created){$mutex.Dispose();return}
try {
    $state=@{Launched=$false;Missing=$null}
    while([IO.File]::Exists($enabled)) {
        try {
            $exe=[IO.File]::ReadAllText($enabled).Trim()
            Invoke-ScumWatchCycle $state (Get-ScumRunning) ([DateTime]::UtcNow) $exe
        }catch { Add-Content -LiteralPath (Join-Path $PSScriptRoot 'watcher.log') -Value ($_.Exception.Message);break }
        Start-Sleep -Seconds 2
    }
}finally {$mutex.ReleaseMutex();$mutex.Dispose()}
";
    }
}
