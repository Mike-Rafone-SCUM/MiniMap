param(
    [ValidateSet('Release','Test')][string]$Configuration = 'Test',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts\Invoke-MiniMapBuild.ps1') -Configuration $Configuration -OutputDirectory $OutputDirectory
