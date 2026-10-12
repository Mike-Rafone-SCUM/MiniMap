$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$miniRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $miniRoot 'src'
$resDir = Join-Path $miniRoot 'resources'
$sources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Xml,System.IO.Compression

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

    # Simulate music presence without starting MusicalScumbag or sending game input.
    $flags = [Reflection.BindingFlags]'NonPublic,Instance'
    $type = $miniWindow.GetType()
    $diagnostic = $type.GetField('diagnosticMode', $flags)
    $music = $type.GetField('musicalScumbagOpen', $flags)
    $type.GetField('nextMusicProcessCheck', $flags).SetValue($miniWindow, [DateTime]::MaxValue)
    $music.SetValue($miniWindow, $true)
    $diagnostic.SetValue($miniWindow, $false)
    try {
        $type.GetField('pending', $flags).SetValue($miniWindow, $true)
        $tickMethod.Invoke($miniWindow, @($null, [EventArgs]::Empty))
        if (-not $type.GetField('intensiveWorkWasPaused', $flags).GetValue($miniWindow)) { throw 'Music did not pause intensive work.' }
        if ($type.GetField('pending', $flags).GetValue($miniWindow)) { throw 'Pending copy survived music pause.' }
        if ($type.GetField('timer', $flags).GetValue($miniWindow).Interval -ne 250) { throw 'Music pause retained the intensive timer cadence.' }
        $copyInProgressField.SetValue($miniWindow, $false)
        $type.GetMethod('PerformCopyAsync', $flags).Invoke($miniWindow, @())
        if ($copyInProgressField.GetValue($miniWindow)) { throw 'Music pause allowed a new copy request.' }
        $frames = $miniWindow.PresentedFrames
        $type.GetMethod('RenderOverlay', $flags).Invoke($miniWindow, @())
        if ($miniWindow.PresentedFrames -ne $frames) { throw 'Music pause presented an overlay frame.' }
        $music.SetValue($miniWindow, $false)
        $type.GetField('adminCommandBusy', $flags).SetValue($miniWindow, $true)
        $tickMethod.Invoke($miniWindow, @($null, [EventArgs]::Empty))
        if ($type.GetField('intensiveWorkWasPaused', $flags).GetValue($miniWindow)) { throw 'Music close did not resume work.' }
        if ($type.GetField('timer', $flags).GetValue($miniWindow).Interval -eq 250) { throw 'Music close retained the paused cadence.' }
    } finally {
        $type.GetField('adminCommandBusy', $flags).SetValue($miniWindow, $false)
        $diagnostic.SetValue($miniWindow, $true)
        $music.SetValue($miniWindow, $false)
    }

    # Test MapMotion smooth interpolation during active sampling
    $motion = [ScumMiniMap.MapMotion]::new()
    $motion.Sample([System.Drawing.PointF]::new(0.5, 0.5), 0, 0, $false)
    $motion.Sample([System.Drawing.PointF]::new(0.501, 0.5), 90, 1.0, $false)

    if ($motion.Yaw -ne 90) { throw "Heading must update immediately when a sample arrives" }
    $motionSteps = 0
    $previousX = $motion.Point.X
    for ($f = 1; $f -le 30; $f++) {
        $advTime = 1.0 + ($f * 0.033)
        $changed = $motion.Advance($advTime)
        if ($changed) { $motionSteps++ }
        if ($motion.Point.X -lt $previousX -or $motion.Point.X -gt [single]0.501) {
            throw "Motion reversed or extrapolated beyond the latest sample"
        }
        if ($f -eq 1 -and ($motion.Point.X -le [single]0.5 -or $motion.Point.X -ge [single]0.501)) {
            throw "Motion must interpolate before settling"
        }
        if ($advTime -ge 1.18 -and $motion.Point.X -ne [single]0.501) {
            throw "Motion did not settle within the 180 ms responsiveness budget"
        }
        if ($f -gt 6 -and $changed) { throw "Settled motion requested an idle redraw" }
        $previousX = $motion.Point.X
    }
    Write-Output ("MapMotion advanced smoothly across {0}/30 frames (Point.X={1:N4}, Yaw={2:N1}°)" -f $motionSteps, $motion.Point.X, $motion.Yaw)
    if ($motionSteps -lt 2) { throw "MapMotion stalled or failed to interpolate" }

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
