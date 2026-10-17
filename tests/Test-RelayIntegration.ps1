param([string]$Address='92.5.169.22',[int]$Port=47832)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Test-PersistentGroups.ps1') -Address $Address -Port $Port
