param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [ValidateSet('Release','Test')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$artifactRoot = [IO.Path]::GetFullPath($PackageDirectory)
$exeName = if ($Configuration -eq 'Release') { 'SkynettMiniMap.exe' } else { 'SkynettMiniMap-Test.exe' }
$zipName = if ($Configuration -eq 'Release') { 'SkynettMiniMap.zip' } else { 'SCUM-MiniMap-Test.zip' }
$exe = Join-Path $artifactRoot $exeName
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
if ($Configuration -eq 'Release') {
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release product version.' }
    $expected = @($exeName,'README.md','CHANGELOG.md','ATTRIBUTION.md','detect_zones.py','requirements.txt','Start-MiniMap.cmd')
} else {
    if ($version -notmatch '^\d+\.\d+\.\d+-test$') { throw 'Invalid test product version.' }
    if (Test-Path -LiteralPath (Join-Path $artifactRoot 'update.txt')) { throw 'Test packages must not include an updater manifest.' }
    $expected = @($exeName,'TESTING.md','ATTRIBUTION.md')
}
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Configuration -eq 'Release') {
    $manifest = [IO.File]::ReadAllText((Join-Path $artifactRoot 'update.txt')).Trim()
    if ($manifest -ne "schema=1`nversion=$version`nsha256=$hash") { throw 'Manifest/executable mismatch.' }
}
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $artifactRoot $zipName))
try {
    if (@(Compare-Object ($expected | Sort-Object) ($zip.Entries.FullName | Sort-Object)).Count) { throw 'Unexpected ZIP contents.' }
    $stream = $zip.GetEntry($exeName).Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $zipHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose(); $stream.Dispose() }
    if ($hash -ne $zipHash) { throw 'ZIP executable differs from standalone executable.' }
} finally { $zip.Dispose() }
$assembly = [Reflection.Assembly]::LoadFile($exe)
$resources = @($assembly.GetManifestResourceNames())
foreach ($name in @('map-tiles.bin','zones.tsv','roads.bin','water-mask.bin','scummap.bin','detect_zones.py')) {
    if ($resources -notcontains $name) { throw "Missing embedded resource: $name" }
}
if ($resources -contains 'map.png') { throw 'Redundant full-resolution map is embedded.' }
if (@($resources | Where-Object { $_ -like 'voice-navigation/*' }).Count -lt 12) { throw 'Bundled voice resources missing.' }
Write-Output "Verified $Configuration package ${version}: ZIP contents, embedded resources and executable hashes."
