param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# Build a versioned, isolated release without stopping the running app or modifying its installation.
$repoRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $repoRoot 'src'
$resDir = Join-Path $repoRoot 'resources'
$packDir = Join-Path $repoRoot 'packaging'

$source = Get-Content (Join-Path $srcDir 'MiniMap.cs') -Raw -Encoding UTF8
if ($source -notmatch 'public const string VersionString = "(\d+\.\d+\.\d+)";') { throw 'Cannot read application version.' }
$version = $matches[1]
$updateSource = Get-Content (Join-Path $srcDir 'UpdateService.cs') -Raw -Encoding UTF8
if ($updateSource -notmatch 'public const string Repository = "[A-Za-z0-9-]+/[A-Za-z0-9._-]+";') { throw 'Configure the update repository first.' }
foreach ($script in @('Test-UpdateChecker.ps1','Check-Stability.ps1')) {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script)
    if ($LASTEXITCODE -ne 0) { throw "$script failed." }
}
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $repoRoot 'tests\Test-AuditRegressions.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Audit regression checks failed.' }
$output = if ($OutputDirectory) {
    if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    } else {
        [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
    }
} else {
    Join-Path $repoRoot ("release\builds\v$version-" + [guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath (Join-Path $output 'SkynettMiniMap.exe')) { throw 'Output already contains a binary. Choose a fresh build directory.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'SkynettMiniMap.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(& (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-MiniMapSources.ps1'))

# VersionString is the authoritative application version. Stamp a temporary
# MiniMap source for compilation so PE/assembly metadata cannot drift from it.
$versionFull = "$version.0"
$releaseSourceText = $source
$releaseSourceText = $releaseSourceText -replace '\[assembly:\s*AssemblyVersion\("[^"]+"\)\]', "[assembly: AssemblyVersion(`"$versionFull`")]"
$releaseSourceText = $releaseSourceText -replace '\[assembly:\s*AssemblyFileVersion\("[^"]+"\)\]', "[assembly: AssemblyFileVersion(`"$versionFull`")]"
$releaseSourceText = $releaseSourceText -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]+"\)\]', "[assembly: AssemblyInformationalVersion(`"$version`")]"
$releaseSourceText = $releaseSourceText -replace 'public static readonly Version CurrentVersion\s*=\s*new Version\([^)]+\);', 'public static readonly Version CurrentVersion = new Version(VersionString + ".0");'

$releaseMiniMap = Join-Path $output 'MiniMap.release.cs'
[IO.File]::WriteAllText($releaseMiniMap, $releaseSourceText, [Text.UTF8Encoding]::new($false))

$trackedMiniMap = [IO.Path]::GetFullPath((Join-Path $srcDir 'MiniMap.cs'))
$replacementCount = 0
$sources = @($sources | ForEach-Object {
    $candidate = [IO.Path]::GetFullPath($_)
    if ([string]::Equals($candidate, $trackedMiniMap, [StringComparison]::OrdinalIgnoreCase)) {
        $replacementCount++
        $releaseMiniMap
    } else {
        $_
    }
})

if ($replacementCount -ne 1) {
    Remove-Item -LiteralPath $releaseMiniMap -Force -ErrorAction SilentlyContinue
    throw "Expected exactly one MiniMap.cs compilation source; found $replacementCount."
}

& $compiler /nologo /optimize+ /target:winexe /platform:x64 "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$resDir\map.png,map.png" "/resource:$resDir\zones.tsv,zones.tsv" "/resource:$resDir\roads.bin,roads.bin" "/resource:$resDir\scummap.bin,scummap.bin" "/resource:$packDir\detect_zones.py,detect_zones.py" "/win32icon:$resDir\App-Icon.ico" @sources
$compileExitCode = $LASTEXITCODE
Remove-Item -LiteralPath $releaseMiniMap -Force -ErrorAction SilentlyContinue
if ($compileExitCode -ne 0) { throw 'Compilation failed.' }
$process = Start-Process -FilePath $exe -ArgumentList '-Check' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Executable self-test timed out.' }
if ($process.ExitCode -ne 0) { throw 'Executable self-test failed.' }
$process.Dispose()
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -Executable $exe -OutputPath (Join-Path $output 'update.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $output 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination (Join-Path $output 'CHANGELOG.md') -Force
foreach ($asset in @('detect_zones.py','requirements.txt','Start-MiniMap.cmd')) {
    Copy-Item -LiteralPath (Join-Path $packDir $asset) -Destination (Join-Path $output $asset) -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'ATTRIBUTION.md') -Destination $output
$packageFiles = @('SkynettMiniMap.exe','README.md','CHANGELOG.md','ATTRIBUTION.md','detect_zones.py','requirements.txt','Start-MiniMap.cmd') | ForEach-Object { Join-Path $output $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath (Join-Path $output 'SkynettMiniMap.zip') -Force
$changelog = Get-Content (Join-Path $repoRoot 'CHANGELOG.md') -Raw -Encoding UTF8
$section = [regex]::Match($changelog, '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
if (-not $section.Success) { throw 'Add a changelog entry for this release before packaging.' }
$notes = "SCUM MiniMap, made for the Skynett community.`n`n" + $section.Groups[1].Value.Trim() + "`n`nDownload SkynettMiniMap.exe for the standalone app, or SkynettMiniMap.zip for the complete package. Exit the previous app before replacing it. Saved settings and zones are retained.`n"
[IO.File]::WriteAllText((Join-Path $output 'release-notes.md'), $notes, [Text.UTF8Encoding]::new($false))
Write-Output "GitHub release files are ready: $output"
return $output
