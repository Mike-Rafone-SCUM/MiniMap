param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputDirectory,[Parameter(Mandatory=$true)][int]$Port,[Parameter(Mandatory=$true)][string]$Certificate,[string]$Address='127.0.0.1',[int]$SoakSeconds=0)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'GroupMemberUiTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot 'GroupMemberUiTests.cs')
if($LASTEXITCODE -ne 0){throw 'Member UI harness compilation failed'}
& $harness ([IO.Path]::GetFullPath($Executable)) (Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'isolated-profile') $Port $Certificate $Address $SoakSeconds
if($LASTEXITCODE -ne 0){throw 'Member UI checks failed'}
