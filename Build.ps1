param(
    [ValidateSet('Release','Test')][string]$Configuration = 'Release',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts\Invoke-MiniMapBuild.ps1') -Configuration $Configuration -OutputDirectory $OutputDirectory
