param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputDirectory,[ValidateSet('all','opacity','setup')][string]$Mode='all')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $OutputDirectory 'OpacitySetupTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'OpacitySetupTests.cs')
if($LASTEXITCODE -ne 0) {throw 'Opacity/setup harness compilation failed'}
& $harness ([IO.Path]::GetFullPath($Executable)) (Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'isolated-profile') $Mode
if($LASTEXITCODE -ne 0) {throw 'Opacity/setup regression checks failed'}
