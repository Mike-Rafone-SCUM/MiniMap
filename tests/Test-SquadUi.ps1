param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$FastTac)
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output) { throw 'Choose a fresh UI test output folder' }
New-Item -ItemType Directory -Path $output | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $output 'SquadUiTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/reference:$framework/WPF/UIAutomationProvider.dll" "/reference:$framework/WPF/UIAutomationTypes.dll" "/out:$harness" (Join-Path $PSScriptRoot 'SquadUiTests.cs')
if($LASTEXITCODE -ne 0) { throw 'UI test harness failed to compile' }
$harnessArgs=@([IO.Path]::GetFullPath($Executable),(Join-Path $output 'isolated-profile'))
if($FastTac){$harnessArgs+='--fast-tac-test'}
& $harness @harnessArgs
if($LASTEXITCODE -ne 0) { throw 'Squad UI tests failed' }
