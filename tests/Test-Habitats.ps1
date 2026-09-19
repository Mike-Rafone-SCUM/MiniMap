$ErrorActionPreference='Stop'
$miniRoot=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$srcDir = Join-Path $miniRoot 'src'
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing
$resDir = Join-Path $miniRoot 'resources'
$miniStore=[ScumMiniMap.ScumMapStore]::Load($resDir)
if($miniStore.Habitats.Count -ne 191) { throw 'Habitat record loss.' }
if(@($miniStore.Categories | Where-Object Id -lt 0).Count -ne 24) { throw 'Species category loss.' }
$miniStore.ApplyDisabledCategoriesString('')
if(@($miniStore.Habitats | Where-Object { $miniStore.IsHabitatEnabled($_) }).Count -ne 0) { throw 'Legacy settings unexpectedly enabled habitat layers.' }
$miniStore.SetAllEnabled($false)
$miniTuna=$miniStore.Categories | Where-Object Name -eq 'Tuna'
$miniPike=$miniStore.Categories | Where-Object Name -eq 'Pike'
$miniStore.SetCategoryEnabled($miniTuna.Id,$true)
$miniVolumes=Get-Content (Join-Path $miniRoot 'tests\fixtures\habitat-origins.json') -Raw | ConvertFrom-Json
for($miniIndex=0;$miniIndex -lt 187;$miniIndex++) {
    $miniVolume=$miniVolumes[$miniIndex]
    $miniShape=$miniStore.Habitats[$miniIndex]
    if([Math]::Abs($miniShape.Rings[0][0].X-$miniVolume.x) -gt 0.000001 -or [Math]::Abs($miniShape.Rings[0][0].Y-$miniVolume.y) -gt 0.000001) { throw 'Aquatic geometry differs from the audited fixture.' }
}
$miniOnlyTuna=$miniStore.Habitats | Where-Object Name -eq 'River Sea Open Tuna Activity Spot' | Select-Object -First 1
if($miniOnlyTuna.CategoryIds.Count -ne 1 -or $miniOnlyTuna.CategoryIds[0] -ne $miniTuna.Id) { throw 'Tuna-specific volume contaminated.' }
$miniCenter=[Drawing.PointF]::new(($miniOnlyTuna.Rings[0][0].X+$miniOnlyTuna.Rings[0][2].X)/2,($miniOnlyTuna.Rings[0][0].Y+$miniOnlyTuna.Rings[0][2].Y)/2)
if($null -eq $miniStore.HabitatAt($miniCenter)) { throw 'Tuna habitat not selectable.' }
$miniStore.SetCategoryEnabled($miniTuna.Id,$false)
if($null -ne $miniStore.HabitatAt($miniCenter)) { throw 'Disabled habitats remained selectable.' }
$miniStore.SetCategoryEnabled($miniPike.Id,$true)
if(@($miniStore.Habitats | Where-Object { $_.Name -eq 'Lake Big' -and $_.CategoryIds.Contains($miniPike.Id) }).Count -ne 0) { throw 'Pike incorrectly assigned to lake preset.' }
$miniSaved=$miniStore.GetDisabledCategoriesString()
$miniReload=[ScumMiniMap.ScumMapStore]::Load($resDir)
$miniReload.ApplyDisabledCategoriesString($miniSaved)
if(-not $miniReload.IsCategoryEnabled($miniPike.Id) -or $miniReload.IsCategoryEnabled($miniTuna.Id)) { throw 'Species selection did not persist.' }

# Biome hole coverage: ensure holes in multi-ring biomes return false while solid interiors return true
$miniMountain=$miniStore.Habitats | Where-Object Name -eq 'Mountain'
$miniMed=$miniStore.Habitats | Where-Object Name -eq 'Mediterranean'
$miniForest=$miniStore.Habitats | Where-Object Name -eq 'Continental Forest'
$miniMeadow=$miniStore.Habitats | Where-Object Name -eq 'Continental Meadow'

if($miniMountain.Contains([Drawing.PointF]::new(0.535,0.0171))) { throw 'Mountain hole point falsely inside biome.' }
if(-not $miniMountain.Contains([Drawing.PointF]::new(0.535,0.025))) { throw 'Mountain solid point falsely outside biome.' }
if($miniMed.Contains([Drawing.PointF]::new(0.08,0.0263))) { throw 'Mediterranean hole point falsely inside biome.' }
if(-not $miniMed.Contains([Drawing.PointF]::new(0.08,0.035))) { throw 'Mediterranean solid point falsely outside biome.' }
if($miniForest.Contains([Drawing.PointF]::new(0.228,0.022))) { throw 'Continental Forest hole point falsely inside biome.' }
if(-not $miniForest.Contains([Drawing.PointF]::new(0.1002,0.0388))) { throw 'Continental Forest solid point falsely outside biome.' }
if($miniMeadow.Contains([Drawing.PointF]::new(0.7065,0.2896))) { throw 'Continental Meadow hole point falsely inside biome.' }
if(-not $miniMeadow.Contains([Drawing.PointF]::new(0.7065,0.280))) { throw 'Continental Meadow solid point falsely outside biome.' }

# Species-specific presets: verify species exclusivity and biome assignments
$miniDentex=$miniStore.Categories | Where-Object Name -eq 'Dentex'
$miniOnlyDentex=$miniStore.Habitats | Where-Object Name -eq 'Sea Dentex Activity Spot' | Select-Object -First 1
if($miniOnlyDentex.CategoryIds.Count -ne 1 -or $miniOnlyDentex.CategoryIds[0] -ne $miniDentex.Id) { throw 'Dentex-specific volume contaminated.' }

$miniRadCarp=$miniStore.Categories | Where-Object Name -eq 'Carp Radioactive'
$miniRadVolume=$miniStore.Habitats | Where-Object Name -eq 'River Big Radioactive Activity Spot' | Select-Object -First 1
if($miniRadVolume.CategoryIds.Count -ne 1 -or $miniRadVolume.CategoryIds[0] -ne $miniRadCarp.Id) { throw 'Radioactive carp volume contaminated.' }

$miniSardine=$miniStore.Categories | Where-Object Name -eq 'Sardine'
$miniFactory=$miniStore.Habitats | Where-Object Name -eq 'Fish Factory' | Select-Object -First 1
if($miniFactory.CategoryIds.Count -ne 1 -or $miniFactory.CategoryIds[0] -ne $miniSardine.Id) { throw 'Fish factory volume contaminated.' }

$miniDonkey=$miniStore.Categories | Where-Object Name -eq 'Donkey'
$miniChicken=$miniStore.Categories | Where-Object Name -eq 'Chicken'
$miniHorse=$miniStore.Categories | Where-Object Name -eq 'Horse'
$miniGoat=$miniStore.Categories | Where-Object Name -eq 'Goat'
$miniBear=$miniStore.Categories | Where-Object Name -eq 'Bear'

if(-not $miniMed.CategoryIds.Contains($miniDonkey.Id) -or $miniMountain.CategoryIds.Contains($miniDonkey.Id)) { throw 'Donkey biome assignment mismatch.' }
if(-not $miniMed.CategoryIds.Contains($miniChicken.Id) -or $miniForest.CategoryIds.Contains($miniChicken.Id)) { throw 'Chicken biome assignment mismatch.' }
if(-not $miniMeadow.CategoryIds.Contains($miniHorse.Id) -or $miniMed.CategoryIds.Contains($miniHorse.Id)) { throw 'Horse biome assignment mismatch.' }
if(-not $miniMed.CategoryIds.Contains($miniGoat.Id) -or -not $miniMountain.CategoryIds.Contains($miniGoat.Id) -or $miniForest.CategoryIds.Contains($miniGoat.Id)) { throw 'Goat biome assignment mismatch.' }
if(-not $miniMountain.CategoryIds.Contains($miniBear.Id) -or $miniMed.CategoryIds.Contains($miniBear.Id)) { throw 'Bear biome assignment mismatch.' }

# Reopening with saved selections in MapWindow
$miniTempDir=Join-Path $miniRoot 'release\test-settings'
New-Item -ItemType Directory -Force -Path $miniTempDir | Out-Null
try {
    Copy-Item (Join-Path $resDir 'map.png') $miniTempDir -Force
    Copy-Item (Join-Path $resDir 'scummap.bin') $miniTempDir -Force
    $miniIniPath=Join-Path $miniTempDir 'settings.ini'
    Set-Content -Path $miniIniPath -Value @('ShowScumMap=True',"ScumMapDisabledCats=$miniSaved")
    $miniWindow=[ScumMiniMap.MapWindow]::new($miniTempDir,$true)
    try {
        $miniScumField=$miniWindow.GetType().GetField('scumMap',[Reflection.BindingFlags]'NonPublic,Instance')
        $miniWinStore=$miniScumField.GetValue($miniWindow)
        if(-not $miniWinStore.IsCategoryEnabled($miniPike.Id) -or $miniWinStore.IsCategoryEnabled($miniTuna.Id)) {
            throw 'MapWindow failed to reopen with saved category selections.'
        }
    } finally { $miniWindow.Dispose() }
} finally {
    $resolved=[IO.Path]::GetFullPath($miniTempDir)
    $expected=[IO.Path]::GetFullPath((Join-Path $miniRoot 'release\test-settings'))
    if($resolved -ne $expected) { throw 'Unexpected test cleanup directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
}

$miniOut=Join-Path $miniRoot 'release\habitat-test'
New-Item -ItemType Directory -Force -Path $miniOut | Out-Null
$miniBase=[Drawing.Image]::FromFile((Join-Path $resDir 'map.png'))
try {
    foreach($miniSection in @('Fishing','Hunting')) {
        $miniStore.SetAllEnabled($false); $miniStore.SetSectionEnabled($miniSection,$true)
        $miniBitmap=[Drawing.Bitmap]::new(1000,1000)
        $miniGraphics=[Drawing.Graphics]::FromImage($miniBitmap)
        try {
            $miniGraphics.DrawImage($miniBase,[Drawing.Rectangle]::new(0,0,1000,1000))
            $miniStore.Draw($miniGraphics,[Drawing.RectangleF]::new(0,0,1000,1000),1,$false,9,$true)
            $miniBitmap.Save((Join-Path $miniOut ($miniSection.Split(' ')[0]+'.png')))
        } finally { $miniGraphics.Dispose(); $miniBitmap.Dispose() }
    }
} finally { $miniBase.Dispose() }
Write-Output '191 habitat records, 24 species, golden geometry, biome holes, species specificity, hit testing, legacy settings, and reopening with saved selections passed.'

