# Compatibility entry point. Build.ps1 is the documented build command.
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Invoke-MiniMapBuild.ps1') -Configuration Release -OutputDirectory $OutputDirectory
