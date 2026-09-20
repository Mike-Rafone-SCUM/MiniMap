param([Parameter(Mandatory=$true)][string]$Label)
$ErrorActionPreference='Stop'
if($Label -notmatch '^[a-zA-Z0-9-]+$') { throw 'Use a simple benchmark label.' }
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'release\performance-test'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=& (Join-Path $PSScriptRoot 'Get-MiniMapSources.ps1')
$exe=Join-Path $output ($Label+'-probe.exe')
& $compiler /nologo /optimize+ /target:exe /platform:x64 /main:MapPerformanceProbe "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources (Join-Path $root 'tests\MapPerformanceProbe.cs')
if($LASTEXITCODE -ne 0) { throw 'Benchmark compilation failed.' }
& $exe (Join-Path $root 'resources') | Tee-Object -FilePath (Join-Path $output ($Label+'.csv'))
if($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
