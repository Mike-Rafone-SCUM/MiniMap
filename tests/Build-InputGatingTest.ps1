param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Invoke-MiniMapBuild.ps1') -Configuration Test -OutputDirectory $OutputDirectory
