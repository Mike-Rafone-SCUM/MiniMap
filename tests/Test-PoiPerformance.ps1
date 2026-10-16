param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $OutputDirectory 'PoiPerformanceHarness.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'PoiPerformanceHarness.cs')
if($LASTEXITCODE -ne 0){throw 'Profiling harness compilation failed.'}
$exe=Join-Path $PackageDirectory 'SkynettMiniMap.exe'
& $harness $exe (Join-Path $OutputDirectory 'isolated-profile')
if($LASTEXITCODE -ne 0){throw 'POI profiling failed.'}
