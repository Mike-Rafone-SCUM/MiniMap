param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $OutputDirectory 'MapStreamingTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'MapStreamingTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Streaming test compilation failed' }
& $harness ([IO.Path]::GetFullPath($Executable)) (Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'isolated-profile')
if($LASTEXITCODE -ne 0) { throw 'Map streaming checks failed' }
