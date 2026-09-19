param([string]$Version = '1.2.5', [switch]$RequireLocalBinaryMatch)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected X.Y.Z.' }
$miniRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $miniRoot "release\github\v$Version\SkynettMiniMap.exe"
[void][Reflection.Assembly]::LoadFrom($exe)
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('MiniMap-LiveUpdate-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$service = [ScumMiniMap.UpdateService]::new([ScumMiniMap.UpdateService]::Repository,$testDirectory,$null)
$release = $service.Check($true)
if ($release.VersionText -ne $Version) { throw "Latest release is $($release.VersionText), expected $Version." }
if ($release.Version -ne [ScumMiniMap.MapWindow]::CurrentVersion) { throw 'Installed-version comparison failed.' }
if ($release.Version -le [version]'1.2.3.0') { throw 'Upgrade from 1.2.3 was not detected.' }
$download = Join-Path $testDirectory 'SkynettMiniMap.exe'
$service.Download($release,$download)
if ($RequireLocalBinaryMatch -and (Get-FileHash -LiteralPath $download).Hash -ne (Get-FileHash -LiteralPath $exe).Hash) { throw 'Published executable differs from tested build.' }
if ((Get-Item -LiteralPath $download).VersionInfo.ProductVersion -ne $Version) { throw 'Downloaded product version is wrong.' }
Write-Output "Live GitHub manifest, version comparison, executable download and SHA-256 verification passed for v$Version."
Write-Output "Verified download: $download"
