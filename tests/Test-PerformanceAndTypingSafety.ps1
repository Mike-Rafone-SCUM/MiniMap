$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$miniRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $miniRoot 'src'
$resDir = Join-Path $miniRoot 'resources'
$sources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing

Write-Output "--- Testing ChatState and Typing Safety ---"
$chat = [ScumMiniMap.ChatState]::new()
if ($chat.Paused) { throw "ChatState should not be paused initially" }

# Press T to open chat
$chat.Key(0x54, 0x54)
if (-not $chat.Paused) { throw "ChatState should be paused after pressing T" }

# Simulate typing a message with 'M' inside: 'H', 'E', 'L', 'L', 'O', 'M', 'A', 'P'
$textKeys = @(0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x4D, 0x41, 0x50)
foreach ($k in $textKeys) {
    $chat.Key($k, 0x54)
    if (-not $chat.Paused) { throw "Typing key $k should keep chat paused" }
}

# Enter closes chat
$chat.Key(0x0D, 0x54)
if ($chat.Paused) { throw "Enter should close chat" }

# Slash opens chat
$chat.Key(0xBF, 0x54)
if (-not $chat.Paused) { throw "Slash should open chat" }

# Escape closes chat
$chat.Key(0x1B, 0x54)
if ($chat.Paused) { throw "Escape should close chat" }

Write-Output "ChatState and typing safety tests passed."

Write-Output "--- Testing MapZone Bounding Box Fast Rejection ---"
$pts = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(100, 100),
    [System.Drawing.PointF]::new(200, 100),
    [System.Drawing.PointF]::new(200, 200),
    [System.Drawing.PointF]::new(100, 200)
)
$zone = [ScumMiniMap.MapZone]::new()
$zone.Name = "Test Box"
$zone.Points = $pts
$zone.ComputeBounds()

# Outside bounds
if ($zone.Contains([System.Drawing.PointF]::new(50, 150))) { throw "Point outside bounds should return false" }
if ($zone.Contains([System.Drawing.PointF]::new(250, 150))) { throw "Point outside bounds should return false" }
if ($zone.Contains([System.Drawing.PointF]::new(150, 50))) { throw "Point outside bounds should return false" }
if ($zone.Contains([System.Drawing.PointF]::new(150, 250))) { throw "Point outside bounds should return false" }

# Inside bounds and inside polygon
if (-not $zone.Contains([System.Drawing.PointF]::new(150, 150))) { throw "Center point should be inside polygon" }

Write-Output "MapZone bounding box checks passed."

Write-Output "--- Testing ScumMapHabitat Bounding Box Culling ---"
$store = [ScumMiniMap.ScumMapStore]::Load($resDir)
$miniMountain = $store.Habitats | Where-Object Name -eq 'Mountain'
if ($null -eq $miniMountain) { throw "Mountain biome not found" }
$miniMountain.ComputeBounds()

# Test fast bounding box rejection with points far outside
if ($miniMountain.Contains([Drawing.PointF]::new(0.01, 0.99))) { throw "Far point should be rejected by bounds" }
# Test known interior and hole points
if ($miniMountain.Contains([Drawing.PointF]::new(0.535, 0.0171))) { throw "Mountain hole point falsely inside biome" }
if (-not $miniMountain.Contains([Drawing.PointF]::new(0.535, 0.025))) { throw "Mountain solid point falsely outside biome" }

Write-Output "ScumMapHabitat bounding box checks passed."

Write-Output "--- Testing Modifier Key Resync Overhead ---"
$trans = [ScumMiniMap.PhysicalKeyTransitions]::new()
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt 10000; $i++) {
    $trans.Resync({ param($k) return $false })
}
$sw.Stop()
Write-Output ("10,000 modifier Resync operations: {0:N2} ms ({1:N4} ms/op)" -f $sw.Elapsed.TotalMilliseconds, ($sw.Elapsed.TotalMilliseconds / 10000))

Write-Output "--- Testing MapWindow Rendering and OverlayKey ---"
$miniWindow = [ScumMiniMap.MapWindow]::new($resDir, $true)
try {
    $overlayKeyMethod = $miniWindow.GetType().GetMethod('OverlayKey', [Reflection.BindingFlags]'NonPublic,Instance')
    $k1 = $overlayKeyMethod.Invoke($miniWindow, @())
    if ([string]::IsNullOrEmpty($k1)) { throw "OverlayKey returned empty string" }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt 10000; $i++) {
        $k = $overlayKeyMethod.Invoke($miniWindow, @())
    }
    $sw.Stop()
    Write-Output ("10,000 OverlayKey evaluations: {0:N2} ms ({1:N4} ms/eval)" -f $sw.Elapsed.TotalMilliseconds, ($sw.Elapsed.TotalMilliseconds / 10000))
} finally {
    $miniWindow.Close()
}

Write-Output "All performance and typing safety tests passed successfully."
