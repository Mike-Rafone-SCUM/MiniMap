$ErrorActionPreference = 'Stop'
$chatTestRoot = Split-Path $PSScriptRoot -Parent
$chatTestOutput = Join-Path $chatTestRoot ('release\checks\chat-copy-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $chatTestOutput -Force | Out-Null
$chatSources = @(& (Join-Path $chatTestRoot 'scripts\Get-MiniMapSources.ps1'))
$chatTestExe = Join-Path $chatTestOutput 'ChatCopyRegressionTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /main:ChatCopyRegressionTests "/out:$chatTestExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Accessibility.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.IO.Compression.dll @chatSources (Join-Path $PSScriptRoot 'ChatCopyRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Chat-copy regression compilation failed.' }
& $chatTestExe
if ($LASTEXITCODE -ne 0) { throw 'Chat-copy regression tests failed.' }
