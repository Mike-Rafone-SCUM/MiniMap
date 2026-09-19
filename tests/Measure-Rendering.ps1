$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$miniRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $miniRoot 'src'
$resDir = Join-Path $miniRoot 'resources'
$sources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing
$miniWindow = [ScumMiniMap.MapWindow]::new($resDir, $true)
try {
    $miniRender = $miniWindow.GetType().GetMethod('OverlayBitmap', [Reflection.BindingFlags]'NonPublic,Instance')
    $miniTimer = [Diagnostics.Stopwatch]::StartNew()
    for ($miniIndex = 0; $miniIndex -lt 100; $miniIndex++) {
        $miniFrame = $miniRender.Invoke($miniWindow, @())
        if ($null -eq $miniWindow.GetType().GetField('overlayFrame', [Reflection.BindingFlags]'NonPublic,Instance')) { $miniFrame.Dispose() }
    }
    $miniTimer.Stop()
    Write-Output ('100 frame renders: {0:N1} ms ({1:N2} ms/frame)' -f $miniTimer.Elapsed.TotalMilliseconds, ($miniTimer.Elapsed.TotalMilliseconds / 100))
    $miniWindow.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $miniPresent = $miniWindow.GetType().GetMethod('RenderOverlay', [Reflection.BindingFlags]'NonPublic,Instance')
    $miniCount = $miniWindow.GetType().GetField('PresentedFrames', [Reflection.BindingFlags]'NonPublic,Instance')
    $miniBefore = $miniCount.GetValue($miniWindow)
    for ($miniIndex = 0; $miniIndex -lt 100; $miniIndex++) { $miniPresent.Invoke($miniWindow, @()) }
    $miniDifference = $miniCount.GetValue($miniWindow) - $miniBefore
    if ($miniDifference -ne 0) { throw "Unchanged frames were unexpectedly presented: $miniDifference" }
    Write-Output '100 unchanged overlay checks: 0 redraws.'
} finally {
    $miniWindow.Close()
}
