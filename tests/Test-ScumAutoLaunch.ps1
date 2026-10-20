$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root ('release/checks/auto-launch-policy-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe=Join-Path $output 'AutoLaunchTests.exe'
$script=Join-Path $output 'watch-scum.ps1'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe /reference:System.Xml.dll "/out:$exe" (Join-Path $root 'src/ScumAutoLaunch.cs') (Join-Path $PSScriptRoot 'ScumAutoLaunchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Auto-launch tests did not compile'}
& $exe $script
if($LASTEXITCODE -ne 0){throw 'Auto-launch lifetime tests failed'}
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $script
if($LASTEXITCODE -ne 0){throw 'Watcher initialization failed with automatic launch disabled'}
. $script -LibraryOnly
$script:launches=0
$script:alreadyRunning=$false
function Test-MiniMapRunning {return $script:alreadyRunning}
function Start-Process {param($FilePath,$ArgumentList,$WorkingDirectory);if($ArgumentList -ne '--auto-scum' -or $WorkingDirectory -ne [IO.Path]::GetDirectoryName($exe)){throw 'Incorrect launch arguments'};$script:launches++}
function Assert($value,$label){if(-not $value){throw $label}}
$state=@{Launched=$false;Missing=$null}
$now=[DateTime]::UtcNow
Invoke-ScumWatchCycle $state $false $now $exe
Assert ($script:launches -eq 0) 'No launch before game'
Invoke-ScumWatchCycle $state $true $now $exe
Invoke-ScumWatchCycle $state $true $now.AddSeconds(2) $exe
Assert ($script:launches -eq 1) 'Only one launch per session, including manual app exit'
Invoke-ScumWatchCycle $state $false $now.AddSeconds(4) $exe
Invoke-ScumWatchCycle $state $true $now.AddSeconds(8) $exe
Assert ($script:launches -eq 1) 'Rapid restart does not duplicate app'
Invoke-ScumWatchCycle $state $false $now.AddSeconds(10) $exe
Invoke-ScumWatchCycle $state $null $now.AddSeconds(22) $exe
Assert $state.Launched 'Unknown process observation retains session'
Invoke-ScumWatchCycle $state $false $now.AddSeconds(24) $exe
Invoke-ScumWatchCycle $state $false $now.AddSeconds(34) $exe
Invoke-ScumWatchCycle $state $true $now.AddSeconds(36) $exe
Assert ($script:launches -eq 2) 'Next session launches app again'
$state=@{Launched=$false;Missing=$null};$script:alreadyRunning=$true
Invoke-ScumWatchCycle $state $true $now $exe
Assert ($script:launches -eq 2 -and $state.Launched) 'Existing manual MiniMap adopted without duplicate'
$script:alreadyRunning=$false;$state=@{Launched=$false;Missing=$null};$rejected=$false
try {Invoke-ScumWatchCycle $state $true $now (Join-Path $output 'missing.exe')}catch {$rejected=$true}
Assert $rejected 'Moved or missing executable is rejected'
$workerFolder=Join-Path $output 'auto-launch'
New-Item -ItemType Directory -Path $workerFolder | Out-Null
$workerScript=Join-Path $workerFolder 'watch-scum.ps1'
$workerEnabled=Join-Path $workerFolder 'enabled.txt'
# Exercise the real watcher loop and mutex with an explicitly absent synthetic game.
$workerSource=[IO.File]::ReadAllText($script).Replace("`$enabled=Join-Path `$PSScriptRoot 'enabled.txt'","function Get-ScumRunning { return `$false }`n`$enabled=Join-Path `$PSScriptRoot 'enabled.txt'")
[IO.File]::WriteAllText($workerScript,$workerSource)
[IO.File]::WriteAllText($workerEnabled,$exe)
$info=New-Object Diagnostics.ProcessStartInfo
$info.FileName='powershell.exe';$info.Arguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$workerScript+'"'
$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle='Hidden'
$worker=[Diagnostics.Process]::Start($info)
try {
    Assert (-not $worker.WaitForExit(1500)) 'Enabled watcher remains running without a game'
    Remove-Item -LiteralPath $workerEnabled
    Assert ($worker.WaitForExit(6000) -and $worker.ExitCode -eq 0) 'Disabling stops the real hidden watcher normally'
}finally {if(-not $worker.HasExited){$worker.Kill()};$worker.Dispose()}
Write-Output '9 watcher lifecycle checks passed. No tasks registered or MiniMap/game applications started.'
