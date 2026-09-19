param([string]$OutputDirectory = 'input-gating-test')
$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
$miniOut = Join-Path (Join-Path $miniRoot 'release') $OutputDirectory
New-Item -ItemType Directory -Force -Path $miniOut | Out-Null
$srcDir = Join-Path $miniRoot 'src'
$resDir = Join-Path $miniRoot 'resources'
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:winexe /platform:x64 "/out:$miniOut\SkynettMiniMap.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$resDir\map.png,map.png" "/resource:$resDir\zones.tsv,zones.tsv" "/resource:$resDir\roads.bin,roads.bin" "/resource:$resDir\scummap.bin,scummap.bin" "/resource:$miniRoot\packaging\detect_zones.py,detect_zones.py" @miniSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
$miniProc = Start-Process -FilePath "$miniOut\SkynettMiniMap.exe" -ArgumentList '-Check' -WindowStyle Hidden -PassThru
try {
    if (-not $miniProc.WaitForExit(30000)) { $miniProc.Kill(); throw 'Check timed out' }
    if ($miniProc.ExitCode -ne 0) { throw 'Packaged self-test failed' }
} finally { $miniProc.Dispose() }
Write-Output "Test executable compiled; packaged self-tests passed: $miniOut\SkynettMiniMap.exe"
