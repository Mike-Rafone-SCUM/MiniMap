param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$RequireReuse,[switch]$NoHabitats)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $OutputDirectory 'PoiZoomHarness.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /reference:System.Xaml.dll /reference:System.Drawing.dll /reference:System.Core.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'PoiZoomHarness.cs')
if($LASTEXITCODE -ne 0){throw 'POI zoom harness compilation failed'}
$options=@();if($RequireReuse){$options+='--require-reuse'};if($NoHabitats){$options+='--no-habitats'}
& $harness $AssemblyPath (Join-Path $OutputDirectory 'isolated-profile') @options
if($LASTEXITCODE -ne 0){throw 'POI zoom measurement failed'}
