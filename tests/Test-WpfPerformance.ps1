param([string]$PackageDirectory=(Join-Path (Split-Path $PSScriptRoot -Parent) 'build-desktop-safe-final'),[string]$OutputDirectory=(Join-Path $env:TEMP ('MiniMap-WpfChecks-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$harness=Join-Path $OutputDirectory 'WpfPerformanceHarness.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Windows.Forms.dll /reference:System.Xaml.dll /reference:System.Core.dll "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\PresentationFramework.dll" "/reference:$framework\WPF\WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'WpfPerformanceHarness.cs')
if($LASTEXITCODE -ne 0){throw 'Harness compilation failed'}
$assemblyPath=Join-Path $PackageDirectory 'SkynettMiniMap-Test.exe'
if(-not (Test-Path -LiteralPath $assemblyPath)) { $assemblyPath=Join-Path $PackageDirectory 'SkynettMiniMap.exe' }
if(-not (Test-Path -LiteralPath $assemblyPath)) { throw 'Package executable is missing' }
& $harness $assemblyPath $OutputDirectory
if($LASTEXITCODE -ne 0){throw 'WPF performance and behavior checks failed'}
