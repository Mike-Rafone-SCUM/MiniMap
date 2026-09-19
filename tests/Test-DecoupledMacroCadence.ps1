$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$miniRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $miniRoot 'src'
$resDir = Join-Path $miniRoot 'resources'
$sources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing

Write-Output "--- Testing Decoupled Macro Cadence & Motion Advancement ---"
$miniWindow = [ScumMiniMap.MapWindow]::new($resDir, $true)
try {
    $tickMethod = $miniWindow.GetType().GetMethod('Tick', [Reflection.BindingFlags]'NonPublic,Instance')
    $tickBusyField = $miniWindow.GetType().GetField('tickBusy', [Reflection.BindingFlags]'NonPublic,Instance')
    $copyInProgressField = $miniWindow.GetType().GetField('copyInProgress', [Reflection.BindingFlags]'NonPublic,Instance')
    $scrollFrameMethod = $miniWindow.GetType().GetMethod('InfobarScrollFrame', [Reflection.BindingFlags]'NonPublic,Instance')

    # Initial state
    $isBusy = $tickBusyField.GetValue($miniWindow)
    if ($isBusy) { throw "tickBusy should be false before tick" }

    # Run tick: should execute synchronously, advance motion, and release tickBusy immediately
    $tickMethod.Invoke($miniWindow, @($null, [EventArgs]::Empty))
    $isBusyAfter = $tickBusyField.GetValue($miniWindow)
    if ($isBusyAfter) { throw "tickBusy must be released after Tick completes" }

    # Test MapMotion smooth interpolation during active sampling
    $motion = [ScumMiniMap.MapMotion]::new()
    $motion.Sample([System.Drawing.PointF]::new(0.5, 0.5), 0, 0, $false)
    $motion.Sample([System.Drawing.PointF]::new(0.501, 0.5), 90, 1.0, $false)

    $motionSteps = 0
    for ($f = 1; $f -le 30; $f++) {
        $advTime = 1.0 + ($f * 0.033)
        $changed = $motion.Advance($advTime)
        if ($changed) { $motionSteps++ }
    }
    Write-Output ("MapMotion advanced smoothly across {0}/30 frames (Point.X={1:N4}, Yaw={2:N1}°)" -f $motionSteps, $motion.Point.X, $motion.Yaw)
    if ($motionSteps -lt 28) { throw "MapMotion stalled or failed to interpolate" }

    # Test InfobarScrollFrame progression when active
    $activeField = $miniWindow.GetType().GetField('infobarScrollActive', [Reflection.BindingFlags]'NonPublic,Instance')
    $startField = $miniWindow.GetType().GetField('infobarScrollStart', [Reflection.BindingFlags]'NonPublic,Instance')
    $activeField.SetValue($miniWindow, $true)
    $startField.SetValue($miniWindow, [DateTime]::UtcNow)

    $scrolls = @()
    for ($frame = 0; $frame -lt 15; $frame++) {
        [System.Threading.Thread]::Sleep(33)
        $scrolls += $scrollFrameMethod.Invoke($miniWindow, @())
    }
    $initialScroll = $scrolls[0]
    $finalScroll = $scrolls[-1]
    Write-Output ("Infobar scroll frame advanced from {0} to {1} over 15 frames." -f $initialScroll, $finalScroll)
    if ($finalScroll -le $initialScroll) {
        throw "Infobar scroll failed to advance during timer ticks"
    }

    Write-Output "Decoupled macro cadence verified: Tick remains non-blocking and UI animations advance continuously."
} finally {
    $miniWindow.Close()
}
