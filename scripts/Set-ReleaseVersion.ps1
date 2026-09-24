param([Parameter(Mandatory=$true)][ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')][string]$Version)
$ErrorActionPreference = 'Stop'
$versionRoot = Split-Path $PSScriptRoot -Parent
$sourcePath = Join-Path $versionRoot 'src\MiniMap.cs'
$source = [IO.File]::ReadAllText($sourcePath)
if ($source -notmatch 'public const string VersionString\s*=\s*"([^"]+)";') { throw 'VersionString missing.' }
$current = $matches[1]
if ([version]$Version -le [version]$current) { throw "Choose a version newer than $current." }
$changelog = [IO.File]::ReadAllText((Join-Path $versionRoot 'CHANGELOG.md'))
if ($changelog -notmatch ('(?m)^## \[' + [regex]::Escape($Version) + '\]')) { throw "Write the CHANGELOG.md entry for $Version first. Release notes are never generated from placeholder claims." }
$source = $source -replace 'public const string VersionString\s*=\s*"[^"]+";', "public const string VersionString = `"$Version`";"
[IO.File]::WriteAllText($sourcePath,$source,[Text.UTF8Encoding]::new($false))
$readmePath = Join-Path $versionRoot 'README.md'
$readme = [IO.File]::ReadAllText($readmePath) -replace '^# SCUM MiniMap v\d+\.\d+\.\d+', "# SCUM MiniMap v$Version"
[IO.File]::WriteAllText($readmePath,$readme,[Text.UTF8Encoding]::new($false))
Write-Output "Prepared version $Version. Run Build.ps1 next."
