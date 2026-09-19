param([switch]$Check, [switch]$Preview, [string]$ImportScreenshot, [switch]$CheckAutomatic)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $repoRoot 'src'
$resDir = Join-Path $repoRoot 'resources'
$sources = & (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing
if ($Check) {
    [ScumMiniMap.MapWindow]::SelfTest()
    Write-Output 'Compilation and coordinate checks passed.'
    exit
}
[System.Windows.Forms.Application]::EnableVisualStyles()
if ($CheckAutomatic) {
    $miniForm = [ScumMiniMap.MapWindow]::new($resDir, [bool]($Preview -or $CheckAutomatic))
    $miniForm.CheckAutomaticImport($ImportScreenshot, (Join-Path $PSScriptRoot 'automatic-editor-preview.png'))
    $miniForm.Close()
    Write-Output 'Automatic import and naming checks passed.'
    exit
}
if ($Preview) {
    $miniForm = [ScumMiniMap.MapWindow]::new($resDir, [bool]($Preview -or $CheckAutomatic))
    $miniForm.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $miniForm.SaveOverlayPreview((Join-Path $PSScriptRoot 'preview.png'))
    $miniForm.SaveSettingsPreview((Join-Path $PSScriptRoot 'settings-preview.png'))
    $miniForm.CheckSearchPreview((Join-Path $PSScriptRoot 'search-preview.png'))
    $miniForm.CheckAppearance()
    $miniForm.CheckZoneEditor((Join-Path $PSScriptRoot 'zones-preview.png'))
    $miniForm.Close()
    exit
}
$miniMutex = [System.Threading.Mutex]::new($false, 'Local\ScumMiniMapOverlay')
if (-not $miniMutex.WaitOne(0)) {
    $miniMutex.Dispose()
    exit
}
try {
    $miniMain = [ScumMiniMap.MapWindow]::new($resDir, [bool]($Preview -or $CheckAutomatic))
    if ($ImportScreenshot) { $miniMain.add_Shown({ $miniMain.OpenZoneImport($ImportScreenshot) }) }
    [System.Windows.Forms.Application]::Run($miniMain)
} finally {
    $miniMutex.ReleaseMutex()
    $miniMutex.Dispose()
}
