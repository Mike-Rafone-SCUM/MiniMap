$ErrorActionPreference = 'Stop'
$playerRoot = Split-Path $PSScriptRoot -Parent
$playerOutput = Join-Path $playerRoot ('release\checks\admin-players-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $playerOutput -Force | Out-Null
$playerSources = @(& (Join-Path $playerRoot 'scripts\Get-MiniMapSources.ps1'))
$playerExe = Join-Path $playerOutput 'AdminPlayerTests.exe'
$playerCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $playerCompiler /nologo /target:exe /platform:x64 /define:MINIMAP_TEST /main:AdminPlayerTests "/out:$playerExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Accessibility.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.IO.Compression.dll "/resource:$playerRoot\resources\spawn-items.tsv,spawn-items.tsv" "/resource:$playerRoot\resources\spawn-compatibility.tsv,spawn-compatibility.tsv" "/resource:$playerRoot\resources\steam-logo.png,steam-logo.png" @playerSources (Join-Path $PSScriptRoot 'AdminPlayerTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Admin player regression compilation failed.' }
& $playerExe (Join-Path $playerRoot 'tests/fixtures/admin-reports')
if ($LASTEXITCODE -ne 0) { throw 'Admin player regressions failed.' }
