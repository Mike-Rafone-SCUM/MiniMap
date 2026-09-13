$ErrorActionPreference = 'Stop'
# Build a versioned, isolated release without stopping the running app or modifying its installation.
$source = Get-Content (Join-Path $PSScriptRoot 'MiniMap.cs') -Raw
if ($source -notmatch 'public const string VersionString = "(\d+\.\d+\.\d+)";') { throw 'Cannot read application version.' }
$version = $matches[1]
$updateSource = Get-Content (Join-Path $PSScriptRoot 'UpdateService.cs') -Raw
if ($updateSource -notmatch 'public const string Repository = "[A-Za-z0-9-]+/[A-Za-z0-9._-]+";') { throw 'Configure the update repository first.' }
foreach ($script in @('Test-UpdateChecker.ps1','Check-Stability.ps1')) {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script)
    if ($LASTEXITCODE -ne 0) { throw "$script failed." }
}
$output = Join-Path $PSScriptRoot "release\github\v$version"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'SkynettMiniMap.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('UpdateService.cs','Localization.cs','RoadRouter.cs','MiniMap.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /optimize+ /target:winexe "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$PSScriptRoot\map.png,map.png" "/resource:$PSScriptRoot\zones.tsv,zones.tsv" "/resource:$PSScriptRoot\roads.bin,roads.bin" @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
$process = Start-Process -FilePath $exe -ArgumentList '-Check' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Executable self-test timed out.' }
if ($process.ExitCode -ne 0) { throw 'Executable self-test failed.' }
$process.Dispose()
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -Executable $exe -OutputPath (Join-Path $output 'update.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $output 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination (Join-Path $output 'CHANGELOG.md') -Force
foreach ($asset in @('detect_zones.py','requirements.txt','Start-MiniMap.cmd')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $asset) -Destination (Join-Path $output $asset) -Force
}
$packageFiles = @('SkynettMiniMap.exe','README.md','CHANGELOG.md','detect_zones.py','requirements.txt','Start-MiniMap.cmd') | ForEach-Object { Join-Path $output $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath (Join-Path $output 'SkynettMiniMap.zip') -Force
$changelog = Get-Content (Join-Path $PSScriptRoot 'CHANGELOG.md') -Raw
$section = [regex]::Match($changelog, '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
if (-not $section.Success) { throw 'Add a changelog entry for this release before packaging.' }
$notes = "SCUM MiniMap, made for the Skynett community.`n`n" + $section.Groups[1].Value.Trim() + "`n`nDownload SkynettMiniMap.exe for the standalone app, or SkynettMiniMap.zip for the complete package. Exit the previous app before replacing it. Saved settings and zones are retained.`n"
[IO.File]::WriteAllText((Join-Path $output 'release-notes.md'), $notes, [Text.UTF8Encoding]::new($false))
Write-Output "GitHub release files are ready: $output"
