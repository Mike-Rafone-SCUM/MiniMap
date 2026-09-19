$ErrorActionPreference='Stop'
$miniRoot=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$miniSources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies System.Windows.Forms,System.Drawing
[ScumMiniMap.ScumMapStore]::SelfTest()
$miniStore=[ScumMiniMap.ScumMapStore]::Load((Join-Path $miniRoot 'resources'))
foreach($miniExpected in @(@(858,135),@(869,83),@(24,161),@(17,4),@(81,4),@(859,7),@(776,4))) {
    if($miniStore.CategoryById[$miniExpected[0]].MarkerCount -ne $miniExpected[1]) { throw 'Source activity marker count mismatch.' }
}
$miniStore.SetAllEnabled($false)
$miniStore.SetSectionEnabled('Fishing',$true)
$miniStore.SetSectionEnabled('Hunting',$true)
$miniSaved=$miniStore.GetDisabledCategoriesString()
$miniReload=[ScumMiniMap.ScumMapStore]::Load((Join-Path $miniRoot 'resources'))
$miniReload.ApplyDisabledCategoriesString($miniSaved)
if(-not $miniReload.IsSectionEnabled('Fishing') -or -not $miniReload.IsSectionEnabled('Hunting') -or $miniReload.IsCategoryEnabled(8)) { throw 'Activity filter persistence failed.' }
$miniMarker=$miniStore.Markers | Where-Object CategoryId -eq 858 | Select-Object -First 1
$miniBounds=[Drawing.RectangleF]::new(0,0,1000,1000)
$miniPoint=[Drawing.PointF]::new($miniMarker.X*1000,$miniMarker.Y*1000)
if($null -eq $miniStore.HitTest($miniPoint,$miniBounds,1)) { throw 'Fishing marker hit test failed.' }
$miniStore.SetAllEnabled($false)
if($null -ne $miniStore.HitTest($miniPoint,$miniBounds,1)) { throw 'Disabled marker remained clickable.' }
$miniOut=Join-Path $miniRoot 'release\activity-marker-test'
New-Item -ItemType Directory -Path $miniOut -Force | Out-Null
$miniPrompt=[ScumMiniMap.MapWindow].GetMethod('CreateWaypointRemovalPrompt',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@('Fishing waypoint'))
$miniFilter=[ScumMiniMap.PoiFilterDialog]::new($miniReload,$null)
try {
    if($miniPrompt.AcceptButton.DialogResult -ne [Windows.Forms.DialogResult]::Cancel) { throw 'Unexpected destructive default button.' }
    foreach($miniView in @(@($miniPrompt,'removal-prompt.png'),@($miniFilter,'activity-filters.png'))) {
        $miniForm=$miniView[0]
        $miniForm.Show()
        [Windows.Forms.Application]::DoEvents()
        if([Object]::ReferenceEquals($miniForm,$miniPrompt)) {
            if(-not [Object]::ReferenceEquals($miniPrompt.ActiveControl,$miniPrompt.CancelButton)) { throw 'Removal prompt did not focus Cancel.' }
            $miniButton=$miniPrompt.CancelButton
            if(-not [Windows.Forms.Cursor]::Position.IsEmpty -and -not $miniButton.RectangleToScreen($miniButton.ClientRectangle).Contains([Windows.Forms.Cursor]::Position)) { throw 'Removal prompt did not draw the cursor to its controls.' }
        }
        $miniBitmap=[Drawing.Bitmap]::new($miniForm.Width,$miniForm.Height)
        try { $miniForm.DrawToBitmap($miniBitmap,[Drawing.Rectangle]::new(0,0,$miniForm.Width,$miniForm.Height)); $miniBitmap.Save((Join-Path $miniOut $miniView[1])) } finally { $miniBitmap.Dispose(); $miniForm.Hide() }
    }
} finally { $miniPrompt.Dispose(); $miniFilter.Dispose() }
Write-Output 'Activity counts, filter persistence, hit testing, cache invalidation and removal prompt checks passed.'
