$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run these focused checks with powershell.exe -NoProfile -STA -File.'
}
$deathCheckRoot = Split-Path $PSScriptRoot -Parent
# Reuse the canonical source list and the same rendering regressions used by builds.
# These focused fixtures provide their own map images and do not require a resource list.
$deathCheckSources = @(& (Join-Path $deathCheckRoot 'scripts\Get-MiniMapSources.ps1'))
$deathCheckSources += Join-Path $PSScriptRoot 'MapRenderingTests.cs'
$deathCheckTypes = Add-Type -Path $deathCheckSources -ReferencedAssemblies System.Drawing,System.Windows.Forms,System.IO.Compression -PassThru
$deathCheckClass = $deathCheckTypes | Where-Object { $_.FullName -eq 'MapRenderingTests' } | Select-Object -First 1
if ($null -eq $deathCheckClass) { throw 'Could not load canonical death-marker regression fixtures.' }
$deathCheckFlags = [Reflection.BindingFlags]'Static,NonPublic'
$null = $deathCheckClass.GetMethod('CheckDeathRecording', $deathCheckFlags).Invoke($null, [object[]]@([string](Join-Path $deathCheckRoot 'resources')))
$null = $deathCheckClass.GetMethod('CheckManualDeathRemoval', $deathCheckFlags).Invoke($null, [object[]]@())
Write-Output 'Passed focused automatic recording and manual death-marker removal checks.'
