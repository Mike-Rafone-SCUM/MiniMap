$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$miniSources = @('UpdateService.cs','Localization.cs','RoadRouter.cs','MiniMap.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing
[ScumMiniMap.MapWindow]::SelfTest()
$miniForm = [ScumMiniMap.MapWindow]::new($PSScriptRoot, $true)
$miniFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$miniTargetField = $miniForm.GetType().GetField('searchTarget', $miniFlags)
$miniPinField = $miniForm.GetType().GetField('pin', $miniFlags)
$miniAccept = $miniForm.GetType().GetMethod('Accept', $miniFlags)
try {
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
    Write-Output 'Input regression, arrival, invalid coordinates, departure and persistent pin checks passed.'
} finally { $miniForm.Dispose() }
$miniForm.Dispose()
Write-Output 'Repeated disposal passed.'
