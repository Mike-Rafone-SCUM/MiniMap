$ErrorActionPreference = 'Stop'
# Use the captured-geometry regressions; old cross-map tests relied on invented links.
& (Join-Path $PSScriptRoot 'Test-BridgeAndWaterRouting.ps1')
