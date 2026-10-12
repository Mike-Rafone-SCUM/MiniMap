param([switch]$LocalizationOnly)
$ErrorActionPreference = 'Stop'
$spawnRoot = Split-Path $PSScriptRoot -Parent
$spawnOutput = Join-Path $spawnRoot ('release\checks\spawn-cart-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $spawnOutput -Force | Out-Null
$spawnSources = @(& (Join-Path $spawnRoot 'scripts\Get-MiniMapSources.ps1'))
$spawnExe = Join-Path $spawnOutput 'SpawnCartTests.exe'
$spawnCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$localizedResources = @(& (Join-Path $spawnRoot 'scripts\Get-LocalizationResources.ps1'))
& $spawnCompiler /nologo /target:exe /platform:x64 /define:MINIMAP_TEST /main:SpawnCartTests "/out:$spawnExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll "/resource:$spawnRoot\resources\spawn-items.tsv,spawn-items.tsv" "/resource:$spawnRoot\resources\spawn-compatibility.tsv,spawn-compatibility.tsv" "/resource:$spawnRoot\resources\spawn-item-names-pl.tsv,spawn-item-names-pl.tsv" @localizedResources @spawnSources (Join-Path $PSScriptRoot 'SpawnCartTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Spawn-cart regression compilation failed.' }
if ($LocalizationOnly) { & $spawnExe --localization-only } else { & $spawnExe }
if ($LASTEXITCODE -ne 0) { throw 'Spawn-cart regression tests failed.' }
