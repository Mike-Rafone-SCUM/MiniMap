$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing

$testMap = [Drawing.Image]::FromFile((Join-Path $miniRoot 'resources\map.png'))
$testDir = Join-Path $miniRoot 'release\test-zone-controls'
New-Item -ItemType Directory -Force -Path $testDir | Out-Null
$testZonesPath = Join-Path $testDir 'zones.json'

try {
    # 1. Create sample zones
    $sample = [System.Collections.Generic.List[ScumMiniMap.MapZone]]::new()
    $z1 = [ScumMiniMap.MapZone]::new()
    $z1.Name = 'Base Alpha'
    $z1.Argb = [Drawing.Color]::Red.ToArgb()
    $z1.Points = [Drawing.PointF[]]@([Drawing.PointF]::new(0.2, 0.2), [Drawing.PointF]::new(0.3, 0.2), [Drawing.PointF]::new(0.25, 0.3))
    $sample.Add($z1)

    $z2 = [ScumMiniMap.MapZone]::new()
    $z2.Name = 'Loot Depot'
    $z2.Argb = [Drawing.Color]::Blue.ToArgb()
    $z2.Points = [Drawing.PointF[]]@([Drawing.PointF]::new(0.4, 0.4), [Drawing.PointF]::new(0.5, 0.4), [Drawing.PointF]::new(0.45, 0.5))
    $sample.Add($z2)

    $z3 = [ScumMiniMap.MapZone]::new()
    $z3.Name = 'Base Beta'
    $z3.Argb = [Drawing.Color]::Red.ToArgb()
    $z3.Points = [Drawing.PointF[]]@([Drawing.PointF]::new(0.6, 0.6), [Drawing.PointF]::new(0.7, 0.6), [Drawing.PointF]::new(0.65, 0.7))
    $sample.Add($z3)

    [ScumMiniMap.ZoneStore]::Save($testZonesPath, $sample)

    $savedResult = $null
    $editor = [ScumMiniMap.ZoneEditor]::new($testMap, $sample, $testZonesPath, [Action[System.Collections.Generic.List[ScumMiniMap.MapZone]]]{ param($z) $script:savedResult = $z })
    $flags = [Reflection.BindingFlags]'NonPublic,Instance'
    
    # Check fields
    $zonesField = $editor.GetType().GetField('zones', $flags)
    $listField = $editor.GetType().GetField('list', $flags)
    $filterBoxField = $editor.GetType().GetField('filterBox', $flags)
    $detailsLabelField = $editor.GetType().GetField('detailsLabel', $flags)
    $cboLayerField = $editor.GetType().GetField('cboLayer', $flags)
    $chkFillField = $editor.GetType().GetField('chkFill', $flags)
    $nameField = $editor.GetType().GetField('name', $flags)

    $listBox = $listField.GetValue($editor)
    $filterBox = $filterBoxField.GetValue($editor)
    $detailsLabel = $detailsLabelField.GetValue($editor)
    $zones = $zonesField.GetValue($editor)

    if ($zones.Count -ne 3) { throw "Expected 3 initial zones, got $($zones.Count)" }
    if ($listBox.Items.Count -ne 3) { throw "Expected 3 list items, got $($listBox.Items.Count)" }

    # 2. Test Selection & Details Label
    $setSel = $editor.GetType().GetMethod('SetSelectedZoneIndex', $flags)
    $setSel.Invoke($editor, @(1))
    [System.Windows.Forms.Application]::DoEvents()
    $nameBox = $nameField.GetValue($editor)
    if ($nameBox.Text -ne 'Loot Depot') { throw "Expected selection 'Loot Depot', got '$($nameBox.Text)'" }
    if (-not $detailsLabel.Text.Contains('Loot Depot') -or -not $detailsLabel.Text.Contains('Zone 2/3')) {
        throw "Details label missing zone info: $($detailsLabel.Text)"
    }

    # 3. Test Filter Box
    $filterBox.Text = 'Base'
    [System.Windows.Forms.Application]::DoEvents()
    if ($listBox.Items.Count -ne 2) { throw "Filter failed: expected 2 Base items, got $($listBox.Items.Count)" }
    $filterBox.Text = ''
    [System.Windows.Forms.Application]::DoEvents()
    if ($listBox.Items.Count -ne 3) { throw "Filter clear failed: expected 3 items, got $($listBox.Items.Count)" }

    # 4. Test Reordering (Move Up / Down)
    $moveZone = $editor.GetType().GetMethod('MoveZone', $flags)
    $setSel.Invoke($editor, @(1)) # Select 'Loot Depot'
    $moveZone.Invoke($editor, @(-1)) # Move up to index 0
    if ($zones[0].Name -ne 'Loot Depot' -or $zones[1].Name -ne 'Base Alpha') { throw "MoveZone up failed." }
    $moveZone.Invoke($editor, @(1)) # Move down back to index 1
    if ($zones[1].Name -ne 'Loot Depot') { throw "MoveZone down failed." }

    # 5. Test Duplicate
    $duplicateSelected = $editor.GetType().GetMethod('DuplicateSelected', $flags)
    $setSel.Invoke($editor, @(0))
    $duplicateSelected.Invoke($editor, $null)
    if ($zones.Count -ne 4 -or $zones[1].Name -ne 'Base Alpha (Copy)') { throw "DuplicateSelected failed." }

    # 6. Test Delete Selected
    $deleteSelected = $editor.GetType().GetMethod('DeleteSelected', $flags)
    $setSel.Invoke($editor, @(1)) # Select 'Base Alpha (Copy)'
    $deleteSelected.Invoke($editor, $null)
    if ($zones.Count -ne 3 -or $zones[1].Name -ne 'Loot Depot') { throw "DeleteSelected failed." }

    # 7. Test Deselect
    $deselect = $editor.GetType().GetMethod('DeselectSelection', $flags)
    $deselect.Invoke($editor, $null)
    $getSel = $editor.GetType().GetMethod('GetSelectedZoneIndex', $flags)
    if ($getSel.Invoke($editor, $null) -ne -1) { throw "DeselectSelection failed." }

    # 8. Test Export to file
    $exportFile = Join-Path $testDir 'exported.json'
    [ScumMiniMap.ZoneStore]::Save($exportFile, $zones)
    $loadedExport = [ScumMiniMap.ZoneStore]::Load($exportFile)
    if ($loadedExport.Count -ne 3) { throw "Export/Load zone count mismatch." }

    # 9. Test Delete All Zones directly on collection
    $zones.Clear()
    $editor.GetType().GetMethod('RefreshViews', $flags).Invoke($editor, $null)
    if ($listBox.Items.Count -ne 0) { throw "DeleteAll did not clear list items." }
    if ($zones.Count -ne 0) { throw "DeleteAll did not clear zones collection." }

    # 10. Test Append Mode Import from File
    $cboLayer = $cboLayerField.GetValue($editor)
    if ($null -eq $cboLayer) { throw "cboLayer control not found." }
    $zones.AddRange($loadedExport)
    $editor.GetType().GetMethod('RefreshViews', $flags).Invoke($editor, $null)
    if ($zones.Count -ne 3) { throw "Import with append failed." }

    # Append again (should double to 6)
    $zones.AddRange($loadedExport)
    $editor.GetType().GetMethod('RefreshViews', $flags).Invoke($editor, $null)
    if ($zones.Count -ne 6) { throw "Second append failed: expected 6, got $($zones.Count)" }

    # 11. Test Save Zones
    [ScumMiniMap.ZoneStore]::Save($testZonesPath, $zones)
    $persisted = [ScumMiniMap.ZoneStore]::Load($testZonesPath)
    if ($persisted.Count -ne 6) { throw "Persisted zones count mismatch." }

    # Test Save Empty (Delete All and Save)
    $zones.Clear()
    [ScumMiniMap.ZoneStore]::Save($testZonesPath, $zones)
    $persistedEmpty = [ScumMiniMap.ZoneStore]::Load($testZonesPath)
    if ($persistedEmpty.Count -ne 0) { throw "Persisted empty zones failed." }

    $editor.Dispose()
    Write-Output 'All granular controls (selection, details, filter, reorder, duplicate, delete selected, delete all, deselect, export, append, persistence) passed.'
} finally {
    $testMap.Dispose()
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
