param(
    [ValidateSet('Release','Test')][string]$Configuration = 'Release',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$buildRoot = Split-Path $PSScriptRoot -Parent
$res = Join-Path $buildRoot 'resources'
$tilePack = Join-Path $res 'map-tiles.bin'
$mapSource = Join-Path $res 'map.png'
$tileFormat = ''
if (Test-Path -LiteralPath $tilePack) {
    $tileFile = [IO.File]::OpenRead($tilePack)
    try { $header = New-Object byte[] 4; if ($tileFile.Read($header,0,4) -eq 4) { $tileFormat = [Text.Encoding]::ASCII.GetString($header) } }
    finally { $tileFile.Dispose() }
}
if ($tileFormat -ne 'MTL2') { throw 'Missing or outdated map tiles. Run scripts/Build-MapTiles.py before building.' }
$pack = Join-Path $buildRoot 'packaging'
$sourcePath = Join-Path $buildRoot 'src\MiniMap.cs'
$source = [IO.File]::ReadAllText($sourcePath)
if ($source -notmatch 'public const string VersionString\s*=\s*"((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))";') { throw 'Invalid VersionString in src\MiniMap.cs.' }
$version = $matches[1]
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(& (Join-Path $PSScriptRoot 'Get-MiniMapSources.ps1'))
$required = @($compiler,$tilePack) + $sources + @('map.png','zones.tsv','roads.bin','water-mask.bin','scummap.bin','App-Icon.ico' | ForEach-Object { Join-Path $res $_ })
foreach ($file in $required) { if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing build input: $file" } }
$changelog = [IO.File]::ReadAllText((Join-Path $buildRoot 'CHANGELOG.md'))
$section = [regex]::Match($changelog, '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
if ($Configuration -eq 'Release' -and -not $section.Success) { throw "Add a real CHANGELOG.md entry for $version before building a release." }
if (-not $OutputDirectory) {
    $OutputDirectory = if ($Configuration -eq 'Release') { "release\github\v$version" } else { "release\tests\v$version-" + (Get-Date -Format 'yyyyMMdd-HHmmss') }
}
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $buildRoot $OutputDirectory)) }
$exeName = if ($Configuration -eq 'Release') { 'SkynettMiniMap.exe' } else { 'SkynettMiniMap-Test.exe' }
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output. Published releases are immutable; use a new version or a fresh -OutputDirectory." }
$stagingRoot = if ([IO.Path]::GetPathRoot($output) -ne [IO.Path]::GetPathRoot($buildRoot)) {
    [IO.Path]::GetFullPath((Join-Path (Split-Path $output -Parent) '.staging'))
} else { [IO.Path]::GetFullPath((Join-Path $buildRoot 'release\.staging')) }
$stage = Join-Path $stagingRoot ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    & (Join-Path $PSScriptRoot 'Invoke-MiniMapChecks.ps1')
    $defines = @()
    if ($Configuration -eq 'Test') { $defines = @('/define:MINIMAP_TEST') }
    $renderTest = Join-Path $stage 'RenderingChecks.exe'
    & $compiler /nologo /optimize+ /target:exe /platform:x64 @defines /main:MapRenderingTests "/out:$renderTest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$tilePack,map-tiles.bin" @sources (Join-Path $buildRoot 'tests\MapRenderingTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Rendering check compilation failed.' }
    & $renderTest $res
    if ($LASTEXITCODE -ne 0) { throw 'Rendering checks failed.' }
    Remove-Item -LiteralPath $renderTest
    $renderPreview = Join-Path $stage 'DestinationIndicatorPreview.png'
    if (Test-Path -LiteralPath $renderPreview) { Remove-Item -LiteralPath $renderPreview }

    # Stamp one version source and embed the same complete resources in both configurations.
    $productVersion = if ($Configuration -eq 'Release') { $version } else { "$version-test" }
    $stamped = $source -replace '\[assembly:\s*AssemblyVersion\("[^"]+"\)\]', "[assembly: AssemblyVersion(`"$version.0`")]"
    $stamped = $stamped -replace '\[assembly:\s*AssemblyFileVersion\("[^"]+"\)\]', "[assembly: AssemblyFileVersion(`"$version.0`")]"
    $stamped = $stamped -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]+"\)\]', "[assembly: AssemblyInformationalVersion(`"$productVersion`")]"
    $stamped = $stamped -replace 'public static readonly Version CurrentVersion\s*=\s*new Version\([^)]+\);', 'public static readonly Version CurrentVersion = new Version(VersionString + ".0");'
    $stampedPath = Join-Path $stage 'MiniMap.build.cs'
    [IO.File]::WriteAllText($stampedPath, $stamped, [Text.UTF8Encoding]::new($false))
    $sources = @($sources | ForEach-Object { if ([IO.Path]::GetFullPath($_) -eq $sourcePath) { $stampedPath } else { $_ } })
    if (@($sources | Where-Object { $_ -eq $stampedPath }).Count -ne 1) { throw 'Expected exactly one main source file.' }
    $embedded = @('zones.tsv','roads.bin','water-mask.bin','scummap.bin' | ForEach-Object { '/resource:' + (Join-Path $res $_) + ',' + $_ })
    $embedded += '/resource:' + $tilePack + ',map-tiles.bin'
    $embedded += '/resource:' + (Join-Path $pack 'detect_zones.py') + ',detect_zones.py'
    $embedded += @(& (Join-Path $PSScriptRoot 'Get-VoiceResources.ps1'))
    $exe = Join-Path $stage $exeName
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 @defines @embedded "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32icon:$res\App-Icon.ico" @sources
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
    Remove-Item -LiteralPath $stampedPath
    $process = Start-Process -FilePath $exe -ArgumentList '-Check' -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Packaged self-test timed out.' }
        if ($process.ExitCode -ne 0) { throw 'Packaged self-test failed.' }
    } finally { $process.Dispose() }
    if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -ne $productVersion) { throw 'Packaged version mismatch.' }
    Copy-Item -LiteralPath (Join-Path $buildRoot 'ATTRIBUTION.md') -Destination $stage
    if ($Configuration -eq 'Release') {
        & (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -Executable $exe -OutputPath (Join-Path $stage 'update.txt')
        foreach ($doc in @('README.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $buildRoot $doc) -Destination $stage }
        foreach ($asset in @('detect_zones.py','requirements.txt','Start-MiniMap.cmd')) { Copy-Item -LiteralPath (Join-Path $pack $asset) -Destination $stage }
        $packageNames = @($exeName,'README.md','CHANGELOG.md','ATTRIBUTION.md','detect_zones.py','requirements.txt','Start-MiniMap.cmd')
        $zipName = 'SkynettMiniMap.zip'
        $notes = "SCUM MiniMap, made for the Skynett community.`n`n" + $section.Groups[1].Value.Trim() + "`n`nDownload SkynettMiniMap.exe or SkynettMiniMap.zip. Exit the previous app before replacing it. Saved settings and zones are retained.`n"
        [IO.File]::WriteAllText((Join-Path $stage 'release-notes.md'), $notes, [Text.UTF8Encoding]::new($false))
    } else {
        Copy-Item -LiteralPath (Join-Path $buildRoot 'TESTING-RESPONSIVENESS.md') -Destination (Join-Path $stage 'TESTING.md')
        $packageNames = @($exeName,'TESTING.md','ATTRIBUTION.md')
        $zipName = 'SCUM-MiniMap-Test.zip'
    }
    $packageFiles = @($packageNames | ForEach-Object { Join-Path $stage $_ })
    Compress-Archive -LiteralPath $packageFiles -DestinationPath (Join-Path $stage $zipName)
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $buildRoot 'tests\Test-BuildArtifacts.ps1') -PackageDirectory $stage -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Package integrity checks failed.' }
    New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
    if (Test-Path -LiteralPath $output) { throw 'Output appeared during build; refusing to replace it.' }
    # Move only the checked, unique staging directory after every check succeeds.
    if (-not $stage.StartsWith($stagingRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging directory.' }
    Move-Item -LiteralPath $stage -Destination $output
    Write-Output "$Configuration package ready: $output"
    Write-Output $output
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($stagingRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging cleanup path.' }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
