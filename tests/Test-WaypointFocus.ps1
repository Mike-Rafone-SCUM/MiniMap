$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing

# Native is intentionally internal to the MiniMap assembly. PowerShell cannot
# resolve [ScumMiniMap.Native] directly, so invoke the two helpers by reflection.
$nativeType = [ScumMiniMap.MapWindow].Assembly.GetType('ScumMiniMap.Native', $true)
$nativeFlags = [Reflection.BindingFlags]'NonPublic,Static'
$releaseCapture = $nativeType.GetMethod('ReleaseCapture', $nativeFlags)
$forceForeground = $nativeType.GetMethod('ForceForeground', $nativeFlags)
if ($null -eq $releaseCapture -or $null -eq $forceForeground) {
    throw 'Could not resolve internal ScumMiniMap.Native focus helpers.'
}

Write-Host "Running Waypoint Focus & Dialog Verification..."

# 1. Test OverlayTheme.Frame TabStop property
$testForm = New-Object Windows.Forms.Form
$testForm.Size = New-Object Drawing.Size(300, 200)
$dismissed = $false
[ScumMiniMap.OverlayTheme]::Frame($testForm, "Test Title", [Action]{ $script:dismissed = $true })

# Find the header panel and close button
$header = $null
foreach ($c in $testForm.Controls) {
    if ($c -is [Windows.Forms.Panel]) {
        $header = $c
        break
    }
}
if ($null -eq $header) { throw "Header panel not found on framed Form" }

$closeBtn = $null
foreach ($c in $header.Controls) {
    if ($c -is [Windows.Forms.Button]) {
        $closeBtn = $c
        break
    }
}
if ($null -eq $closeBtn) { throw "Close button not found in header" }
if ($closeBtn.TabStop -ne $false) { throw "Close button TabStop must be false, but was $($closeBtn.TabStop)" }
Write-Host "  [PASS] OverlayTheme.Frame close button TabStop is false."

# 2. Test Add Waypoint dialog focus behavior
$prompt = New-Object Windows.Forms.Form
$prompt.ClientSize = New-Object Drawing.Size(380, 180)
$prompt.Text = [ScumMiniMap.Localization]::Get("WaypointNameTitle")
$prompt.StartPosition = [Windows.Forms.FormStartPosition]::CenterScreen
$prompt.FormBorderStyle = [Windows.Forms.FormBorderStyle]::None
$prompt.MaximizeBox = $false
$prompt.MinimizeBox = $false
$prompt.TopMost = $true
$prompt.ShowInTaskbar = $true

[ScumMiniMap.OverlayTheme]::Frame($prompt, [ScumMiniMap.Localization]::Get("WaypointNameTitle"), [Action]{ $prompt.DialogResult = [Windows.Forms.DialogResult]::Cancel })
$lbl = New-Object Windows.Forms.Label -Property @{ Left=20; Top=64; Width=340; Text=[ScumMiniMap.Localization]::Get("WaypointNamePrompt"); TabIndex=1 }
$txt = New-Object Windows.Forms.TextBox -Property @{ Left=20; Top=92; Width=340; TabIndex=0 }
$ok = New-Object Windows.Forms.Button -Property @{ Text="OK"; Left=190; Top=132; Width=80; Height=30; DialogResult=[Windows.Forms.DialogResult]::OK; TabIndex=2 }
$cn = New-Object Windows.Forms.Button -Property @{ Text="Cancel"; Left=280; Top=132; Width=80; Height=30; DialogResult=[Windows.Forms.DialogResult]::Cancel; TabIndex=3 }
$prompt.Controls.AddRange(@($txt, $lbl, $ok, $cn))
$prompt.ActiveControl = $txt
$prompt.AcceptButton = $ok
$prompt.CancelButton = $cn

$focusHandled = $false
$prompt.add_Shown({
    $prompt.BeginInvoke([Action]{
        if ($prompt.IsDisposed) { return }
        $releaseCapture.Invoke($null, @()) | Out-Null
        [Windows.Forms.Cursor]::Clip = [Drawing.Rectangle]::Empty
        $forceForeground.Invoke($null, @($prompt.Handle)) | Out-Null
        $prompt.Activate()
        $prompt.ActiveControl = $txt
        $txt.Focus() | Out-Null
        $txt.SelectAll()
        [Windows.Forms.Cursor]::Position = $txt.PointToScreen((New-Object Drawing.Point(($txt.ClientSize.Width / 2), ($txt.ClientSize.Height / 2))))
        $txt.Cursor = [Windows.Forms.Cursors]::IBeam
        $script:focusHandled = $true
    })
})

$prompt.Show()
for ($i = 0; $i -lt 20; $i++) {
    [Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 20
    if ($focusHandled) { break }
}

if (-not $focusHandled) { throw "Prompt Shown BeginInvoke did not fire within expected timeout" }
if (-not [Object]::ReferenceEquals($prompt.ActiveControl, $txt)) {
    throw "ActiveControl is not the TextBox! Was: $($prompt.ActiveControl.GetType().Name)"
}
if (-not $txt.Focused) {
    throw "TextBox is not focused!"
}
Write-Host "  [PASS] Add Waypoint dialog focuses TextBox and sets ActiveControl."
$prompt.Close()
$prompt.Dispose()
$testForm.Dispose()

# 3. Test CreateWaypointRemovalPrompt
$removalPrompt = [ScumMiniMap.MapWindow].GetMethod('CreateWaypointRemovalPrompt',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@('Test Waypoint'))
try {
    $removalPrompt.Show()
    for ($i = 0; $i -lt 10; $i++) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 20
    }
    if (-not [Object]::ReferenceEquals($removalPrompt.ActiveControl, $removalPrompt.CancelButton)) {
        throw "Removal prompt did not focus Cancel button! Was: $($removalPrompt.ActiveControl.GetType().Name)"
    }
    Write-Host "  [PASS] Waypoint removal prompt focuses Cancel button."
} finally {
    $removalPrompt.Close()
    $removalPrompt.Dispose()
}

Write-Host "All Waypoint focus & dialog tests passed successfully!"

