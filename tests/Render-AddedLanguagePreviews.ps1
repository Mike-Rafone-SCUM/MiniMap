param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Render-LocalizationPreviews.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $OutputDirectory -Languages @('th','zh-TW','ja','cs','it','uk')
