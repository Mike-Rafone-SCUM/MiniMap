$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root ('release\checks\input-voice-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@(& (Join-Path $PSScriptRoot 'Get-MiniMapSources.ps1'))
$resources=@(& (Join-Path $PSScriptRoot 'Get-VoiceResources.ps1'))
$exe=Join-Path $output 'InputVoiceTests.exe'
& $compiler /nologo /target:exe /platform:x64 /main:InputVoiceTests "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$root\resources\roads.bin,roads.bin" @resources @sources (Join-Path $root 'tests\InputVoiceTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Input/voice test compilation failed.' }
& $exe
if($LASTEXITCODE -ne 0) { throw 'Input/voice tests failed.' }
