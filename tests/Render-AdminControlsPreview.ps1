param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [ValidateSet('Collapsed','Expanded','Commands','Teleport','Players')][string]$PanelState='Collapsed',
    [ValidateSet('Full','Compact')][string]$MapMode='Full',
    [int]$PanelWidth=0,
    [int]$PanelHeight=0,
    [switch]$CompareSidebar,
    [switch]$ShowAttachment
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this preview helper with powershell.exe -NoProfile -STA -File.'
}

# Use an existing canonical build; this helper does not compile or send commands.
$assemblyFile = (Resolve-Path -LiteralPath $AssemblyPath).ProviderPath
$previewOutput = [IO.Path]::GetFullPath($OutputPath)
$previewOutputDirectory = [IO.Path]::GetDirectoryName($previewOutput)
if (-not (Test-Path -LiteralPath $previewOutputDirectory)) {
    New-Item -ItemType Directory -Path $previewOutputDirectory -Force | Out-Null
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies WindowsBase,PresentationCore,PresentationFramework,System.Xaml,Accessibility,System.Windows.Forms,System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public sealed class AdminPreviewInactiveUi : IDisposable {
    delegate IntPtr ActivationHook(int code, IntPtr first, IntPtr second);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, ActivationHook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr first, IntPtr second);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int WindowStyle(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] static extern int SetWindowStyle(IntPtr window, int index, int value);
    readonly ActivationHook callback;
    readonly IntPtr hook;
    public AdminPreviewInactiveUi() {
        callback=(code, first, second)=>code==5?new IntPtr(1):CallNextHookEx(IntPtr.Zero, code, first, second);
        hook=SetWindowsHookEx(5, callback, IntPtr.Zero, GetCurrentThreadId());
        if(hook==IntPtr.Zero)throw new Exception("Could not prevent admin preview-window activation.");
    }
    public void Show(Form dialog) {
        dialog.TopMost=false;dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;
        dialog.Location=new Point(-32000,-32000);
        SetWindowStyle(dialog.Handle,-20,WindowStyle(dialog.Handle,-20)|0x08000000);
        dialog.Show();dialog.PerformLayout();Application.DoEvents();
    }
    public void Dispose() { UnhookWindowsHookEx(hook);GC.KeepAlive(callback); }
}
'@
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFrom($assemblyFile)
$windowType = $assembly.GetType('ScumMiniMap.MapWindow', $true)
$windowConstructor = $windowType.GetConstructor([Type[]]@([string], [bool]))
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$accept = $windowType.GetMethod('Accept', $instanceFlags)
$setFullMap = $windowType.GetMethod('SetFullMap', $instanceFlags)
$setExpanded = $windowType.GetMethod('SetAdminControlsExpanded', $instanceFlags)
$showSection = $windowType.GetMethod('ShowAdminControlsSection', $instanceFlags)
$syncControls = $windowType.GetMethod('SyncFullMapAdminControls', $instanceFlags)
$renderOverlay = $windowType.GetMethod('OverlayBitmap', $instanceFlags)
$paletteField = $windowType.GetField('adminCommandsPanel', $instanceFlags)
if ($null -eq $windowConstructor -or $null -eq $accept -or $null -eq $setFullMap -or
    $null -eq $setExpanded -or $null -eq $showSection -or $null -eq $syncControls -or $null -eq $paletteField) {
    throw 'This assembly does not contain the floating admin controls. Supply a current canonical Test build.'
}
if ($CompareSidebar -and $null -eq $renderOverlay) {
    throw 'This assembly does not expose the full-map bitmap needed for sidebar comparison.'
}

$previewTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$previewProfileName = 'ScumMiniMap-AdminPreview-' + [Guid]::NewGuid().ToString('N')
$previewProfile = Join-Path $previewTempRoot $previewProfileName
$window = $null
$bitmap = $null
$paletteBitmap = $null
$sidebarBitmap = $null
$inactiveUi = $null
try {
    $inactiveUi = New-Object AdminPreviewInactiveUi
    New-Item -ItemType Directory -Path $previewProfile | Out-Null
    Set-Content -LiteralPath (Join-Path $previewProfile 'settings.ini') -Value 'Welcomed=True' -Encoding ASCII
    $embeddedMap = $assembly.GetManifestResourceStream('map.png')
    if ($null -ne $embeddedMap) {
        $mapFile = [IO.File]::Create((Join-Path $previewProfile 'map.png'))
        try { $embeddedMap.CopyTo($mapFile) } finally { $mapFile.Dispose();$embeddedMap.Dispose() }
    } else {
        $sourceMap = New-Object Drawing.Bitmap 32,32
        try { $sourceMap.Save((Join-Path $previewProfile 'map.png'), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $sourceMap.Dispose() }
    }

    $window = $windowConstructor.Invoke([object[]]@([string]$previewProfile, [bool]$true))
    $windowType.GetField('adminToolsEnabled',$instanceFlags).SetValue($window,$true)
    $null = $window.Handle
    $null = $accept.Invoke($window, [object[]]@('{X=315989.281 Y=-229276.188 Z=16060.481|P=-22.862701 Y=-178.932495 R=0.000000}'))
    if ($MapMode -eq 'Full' -and $PanelState -ne 'Players') { $null = $setFullMap.Invoke($window, [object[]]@($true)) }
    $null = $windowType.GetMethod('ShowAdminControls',$instanceFlags).Invoke($window,[object[]]@($false,$true,$false))
    switch ($PanelState) {
        'Collapsed' { }
        'Expanded' { $null = $setExpanded.Invoke($window, [object[]]@($true)) }
        'Commands' { $null = $showSection.Invoke($window, [object[]]@($false)) }
        'Teleport' { $null = $showSection.Invoke($window, [object[]]@($true)) }
        'Players' {
            $databaseType = $assembly.GetType('ScumMiniMap.PlayerDatabase',$true)
            $database = $databaseType.GetConstructor($instanceFlags,$null,[Type[]]@([string]),$null).Invoke([object[]]@([string](Join-Path $previewProfile 'admin-players.db')))
            $null = $databaseType.GetMethod('SelectServer',$instanceFlags).Invoke($database,[object[]]@('Preview Server'))
            $sample = " 1. Example Admin`nPlatform Name: Example`nPlatform ID: 76561198000000001`nFame: 50`nAccount balance: 0`nGold balance: 0`nLocation: X=428762.750 Y=-164771.844 Z=96.770`n`n 2. Unicode Player`nPlatform Name: ツ 🐻`nPlatform ID: 76561198000000002`nFame: 5`nAccount balance: 0`nGold balance: 0`nLocation: X=-172274.172 Y=-682446.125 Z=1194.180`n"
            $null = $databaseType.GetMethod('Import',$instanceFlags).Invoke($database,[object[]]@('Preview Server',$sample,[DateTime]::UtcNow))
            $null = $windowType.GetMethod('ShowAdminPlayers',$instanceFlags).Invoke($window,[object[]]@())
        }
    }
    $null = $syncControls.Invoke($window, [object[]]@($true, $false))
    $palette = $paletteField.GetValue($window)
    if ($PanelState -eq 'Players') { $palette = $windowType.GetField('adminPlayersPanel',$instanceFlags).GetValue($window) }
    if ($null -eq $palette -or $palette.IsDisposed) { throw 'The floating admin controls were not created.' }
    if ($PanelWidth -gt 0 -or $PanelHeight -gt 0) {
        $palette.ClientSize = New-Object Drawing.Size $(if($PanelWidth -gt 0){$PanelWidth}else{$palette.ClientSize.Width}),$(if($PanelHeight -gt 0){$PanelHeight}else{$palette.ClientSize.Height})
        $palette.PerformLayout()
        if ($PanelState -eq 'Teleport' -or $PanelState -eq 'Commands') {
            $scrollSection = $windowType.GetField('scrollAdminControlsSection',$instanceFlags).GetValue($window)
            $null = $scrollSection.DynamicInvoke([object[]]@([bool]($PanelState -eq 'Teleport')))
        }
    }
    $paletteBounds = $palette.Bounds
    $overlay = $windowType.GetField('overlay',$instanceFlags).GetValue($window)
    $mapBounds = $overlay.Bounds
    $inactiveUi.Show($palette)
    $paletteBitmap = New-Object Drawing.Bitmap $palette.Width,$palette.Height
    $palette.DrawToBitmap($paletteBitmap, (New-Object Drawing.Rectangle 0,0,$palette.Width,$palette.Height))
    if ($ShowAttachment) {
        $cachedFrame = $renderOverlay.Invoke($window,[object[]]@())
        $combinedBounds = [Drawing.Rectangle]::Union($paletteBounds,$mapBounds)
        $bitmap = New-Object Drawing.Bitmap $combinedBounds.Width,$combinedBounds.Height
        $attachmentGraphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $attachmentGraphics.Clear([Drawing.Color]::FromArgb(14,17,20))
            $attachmentGraphics.DrawImageUnscaled($cachedFrame,$mapBounds.Left-$combinedBounds.Left,$mapBounds.Top-$combinedBounds.Top)
            $attachmentGraphics.DrawImageUnscaled($paletteBitmap,$paletteBounds.Left-$combinedBounds.Left,$paletteBounds.Top-$combinedBounds.Top)
        } finally { $attachmentGraphics.Dispose() }
    } elseif ($CompareSidebar) {
        # OverlayBitmap returns the window-owned cached frame. Dispose only our cropped clone.
        $cachedFrame = $renderOverlay.Invoke($window, [object[]]@())
        $sidebarWidth = $cachedFrame.Width - $cachedFrame.Height
        if ($sidebarWidth -le 0) { throw 'The current full-map layout has no sidebar to compare.' }
        $sidebarHeight = [Math]::Min($cachedFrame.Height, $paletteBitmap.Height)
        $sidebarBounds = New-Object Drawing.Rectangle $cachedFrame.Height,0,$sidebarWidth,$sidebarHeight
        $sidebarBitmap = $cachedFrame.Clone($sidebarBounds, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $comparisonGap = 24
        $comparisonWidth = $paletteBitmap.Width + $comparisonGap + $sidebarBitmap.Width
        $comparisonHeight = [Math]::Max($paletteBitmap.Height, $sidebarBitmap.Height)
        $bitmap = New-Object Drawing.Bitmap $comparisonWidth,$comparisonHeight
        $comparisonGraphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $comparisonGraphics.Clear($palette.BackColor)
            $comparisonGraphics.DrawImageUnscaled($paletteBitmap, 0, 0)
            $comparisonGraphics.DrawImageUnscaled($sidebarBitmap, $paletteBitmap.Width + $comparisonGap, 0)
        }
        finally { $comparisonGraphics.Dispose() }
    }
    else { $bitmap = $paletteBitmap.Clone() }
    $bitmap.Save($previewOutput, [Drawing.Imaging.ImageFormat]::Png)
    $previewLabel = if ($CompareSidebar) { 'admin controls and actual full-map sidebar comparison at 1:1 scale' } else { 'admin controls preview' }
    Write-Output ('Saved {0} {1}: {2} ({3}x{4})' -f $PanelState, $previewLabel, $previewOutput, $bitmap.Width, $bitmap.Height)
}
finally {
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    if ($null -ne $paletteBitmap) { $paletteBitmap.Dispose() }
    if ($null -ne $sidebarBitmap) { $sidebarBitmap.Dispose() }
    if ($null -ne $window) { $window.Dispose() }
    if ($null -ne $inactiveUi) { $inactiveUi.Dispose() }
    if (Test-Path -LiteralPath $previewProfile) {
        # Resolve and verify the exact newly-created profile before recursive cleanup.
        $resolvedProfile = (Resolve-Path -LiteralPath $previewProfile).ProviderPath
        $resolvedTempRoot = (Resolve-Path -LiteralPath $previewTempRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
        if (-not [string]::Equals([IO.Path]::GetDirectoryName($resolvedProfile), $resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals([IO.Path]::GetFileName($resolvedProfile), $previewProfileName, [StringComparison]::Ordinal)) {
            throw 'Preview profile cleanup refused because its resolved path is outside the expected temporary directory.'
        }
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
    }
}
