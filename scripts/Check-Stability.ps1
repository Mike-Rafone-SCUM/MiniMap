$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $repoRoot 'src'
$resDir = Join-Path $repoRoot 'resources'
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing
[ScumMiniMap.MapWindow]::SelfTest()
$miniForm = [ScumMiniMap.MapWindow]::new($resDir, $true)
$miniFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$miniTargetField = $miniForm.GetType().GetField('searchTarget', $miniFlags)
$miniPinField = $miniForm.GetType().GetField('pin', $miniFlags)
$miniAccept = $miniForm.GetType().GetMethod('Accept', $miniFlags)
try {
    $miniForm.GetType().GetField('locationHistory', $miniFlags).SetValue($miniForm, $true)
    $historyRecord = $miniForm.GetType().GetMethod('RecordLocationHistory', $miniFlags)
    $historyPoints = $miniForm.GetType().GetField('locationHistoryPoints', $miniFlags).GetValue($miniForm)
    $historyStart = [DateTime]::UtcNow.AddMinutes(-2)
    foreach ($historySecond in @(0, 10, 29, 30, 59, 60)) {
        $historyRecord.Invoke($miniForm, @([Drawing.PointF]::new([single](0.1 + $historySecond * 0.001), 0.1), $historyStart.AddSeconds($historySecond)))
    }
    $timestampMarkCount = 0
    foreach ($historyPoint in $historyPoints) {
        $showTimestamp = $historyPoint.GetType().GetField('ShowTimestamp', [Reflection.BindingFlags]'NonPublic,Instance').GetValue($historyPoint)
        if ($showTimestamp) { $timestampMarkCount++ }
    }
    if ($timestampMarkCount -ne 3) { throw "Expected three 30-second history timestamps, got $timestampMarkCount." }
    $historyAge = $miniForm.GetType().GetMethod('FormatHistoryAge', [Reflection.BindingFlags]'NonPublic,Static')
    if ($historyAge.Invoke($null, @([TimeSpan]::FromSeconds(29))) -ne '0s ago' -or
        $historyAge.Invoke($null, @([TimeSpan]::FromSeconds(30))) -ne '30s ago') {
        throw 'History age labels did not advance in 30-second steps.'
    }
    $miniMapState = $miniForm.GetType().GetField('fullMapActive', $miniFlags)
    $miniSetMap = $miniForm.GetType().GetMethod('SetFullMap', $miniFlags)
    $miniKey = $miniForm.GetType().GetMethod('OnGameKey', $miniFlags)
    $miniSetMap.Invoke($miniForm, @($true))
    $miniSetMap.Invoke($miniForm, @($true))
    if (-not $miniMapState.GetValue($miniForm)) { throw 'Repeated map-open request toggled closed.' }
    foreach ($miniShortcut in @(0x4D,0x1B,0x23,0x24,0x2D,0x2E,0x21,0x22)) {
        $miniKey.Invoke($miniForm, @($miniShortcut))
    }
    [System.Windows.Forms.Application]::DoEvents()
    if (-not $miniMapState.GetValue($miniForm)) { throw 'Blocked shortcut changed map state.' }
    $miniSetMap.Invoke($miniForm, @($false))
    $miniSetMap.Invoke($miniForm, @($false))
    if ($miniMapState.GetValue($miniForm)) { throw 'Repeated map-close request reopened map.' }
    $miniChat = $miniForm.GetType().GetField('chat', $miniFlags).GetValue($miniForm)
    $miniSetMap.Invoke($miniForm, @($true))
    $miniChat.Key(0x54)
    if (-not $miniChat.Paused) { throw 'Chat key failed to pause.' }
    $miniSetMap.Invoke($miniForm, @($false))
    if (-not $miniChat.Paused) { throw 'Closing map while chat open destroyed chat pause.' }
    $miniChat.Key(0x09)
    if (-not $miniChat.Paused) { throw 'Tab while chat open unlatched chat pause.' }
    $miniChat.Key(0x4D)
    if (-not $miniChat.Paused) { throw 'Typing M after Tab unlatched chat pause.' }
    $miniChat.Key(0x0D)
    if ($miniChat.Paused) { throw 'Enter did not resume chat.' }
    $miniChat.Key(0x09)
    if ($miniChat.Paused) { throw 'Tab while chat closed opened chat.' }
    $miniMotion = [ScumMiniMap.MapMotion]::new()
    $miniMotion.Sample([Drawing.PointF]::new(0.5, 0.5), 90.0, 1.0, $false)
    $miniMotion.Sample([Drawing.PointF]::new(0.500005, 0.5), 90.5, 2.0, $false)
    if ($miniMotion.Point.X -ne 0.5 -or $miniMotion.Yaw -ne 90.0) { throw 'Stationary sub-threshold jitter was not deadbanded.' }
    $miniMotion.Sample([Drawing.PointF]::new(0.501, 0.5), 90.0, 3.0, $false)
    $null = $miniMotion.Advance(3.18)
    if ([Math]::Abs($miniMotion.Point.X - 0.501) -gt 0.0001) { throw 'Latest observed position did not settle within the responsiveness budget.' }
    $miniTarget = [ScumMiniMap.MapZone]::new()
    $miniTarget.Name = 'Arrival test'
    $miniTarget.Points = [Drawing.PointF[]]@([Drawing.PointF]::new(0.5, 0.5))
    $miniPin = [ScumMiniMap.Position]::new()
    $miniPin.X = 123
    $miniTargetField.SetValue($miniForm, $miniTarget)
    $miniPinField.SetValue($miniForm, $miniPin)
    $miniAccept.Invoke($miniForm, @('invalid coordinates'))
    if ($null -eq $miniTargetField.GetValue($miniForm)) { throw 'Invalid sample cleared destination.' }
    $miniAccept.Invoke($miniForm, @('{X=0 Y=0 Z=0|P=0 Y=0 R=0}'))
    if ($null -eq $miniTargetField.GetValue($miniForm)) { throw 'Distant sample cleared destination.' }
    $miniAccept.Invoke($miniForm, @('{X=-143991 Y=-142991 Z=0|P=0 Y=0 R=0}'))
    if ($null -ne $miniTargetField.GetValue($miniForm)) { throw 'Arrival did not clear destination.' }
    if (-not [Object]::ReferenceEquals($miniPin, $miniPinField.GetValue($miniForm))) { throw 'Arrival removed pin.' }
    $miniAccept.Invoke($miniForm, @('{X=0 Y=0 Z=0|P=0 Y=0 R=0}'))
    if ($null -ne $miniTargetField.GetValue($miniForm)) { throw 'Departure restored destination.' }
    $scrollKey1 = [ScumMiniMap.MapWindow]::GetScrollBaseKey('B2 - Airport  |  -> Safe Zone: 1420m (~2m)')
    $scrollKey2 = [ScumMiniMap.MapWindow]::GetScrollBaseKey('B2 - Airport  |  -> Safe Zone: 1416m (~2m)')
    $scrollKey3 = [ScumMiniMap.MapWindow]::GetScrollBaseKey('B2 - Airport  |  -> Bunker: 1420m (~2m)')
    if ($scrollKey1 -ne $scrollKey2) { throw 'Dynamic distance change modified ticker base scroll key.' }
    if ($scrollKey1 -eq $scrollKey3) { throw 'Target destination change did not update ticker base scroll key.' }
    Write-Output 'Input regression, arrival, invalid coordinates, departure, persistent pin, and ticker stability checks passed.'
} finally { $miniForm.Dispose() }
$miniForm.Dispose()
Write-Output 'Repeated disposal passed.'
