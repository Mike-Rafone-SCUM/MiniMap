param(
    [Parameter(Mandatory=$true)][string]$Version
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected X.Y.Z.' }
$repo = 'Mike-Rafone-SCUM/MiniMap'
Get-Command gh -ErrorAction Stop | Out-Null
& gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Sign into GitHub CLI with gh auth login first.' }
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot "release\github\v$Version"
$exe = Join-Path $output 'SkynettMiniMap.exe'
$manifest = Join-Path $output 'update.txt'
$zip = Join-Path $output 'SkynettMiniMap.zip'
if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -ne $Version) { throw 'Executable version does not match the release tag.' }
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -Executable $exe -OutputPath $manifest
$notes = Join-Path $output 'release-notes.md'
if (-not (Test-Path -LiteralPath $notes)) { throw "Prepare release notes at $notes first." }
$existingRelease = $null
try { $existingRelease = & gh release view "v$Version" --repo $repo --json tagName 2>$null } catch {}
if ($LASTEXITCODE -eq 0 -and $existingRelease) {
    throw 'This version already exists. Published versions are immutable; choose a new version. Inspect any existing draft manually.'
} else {
    # Creating a draft with both assets prevents clients seeing an incomplete release.
    & gh release create "v$Version" $exe $manifest $zip --repo $repo --verify-tag --draft --title "SCUM MiniMap v$Version" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Draft creation failed. Inspect existing releases before retrying.' }
    $draft = & gh release view "v$Version" --repo $repo --json assets,isDraft | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $draft.isDraft -or @($draft.assets).Count -ne 3) { throw 'Draft asset verification failed.' }
    foreach ($asset in @(@{name='SkynettMiniMap.exe';path=$exe},@{name='update.txt';path=$manifest},@{name='SkynettMiniMap.zip';path=$zip})) {
        $remote = @($draft.assets | Where-Object name -eq $asset.name)
        if ($remote.Count -ne 1 -or $remote[0].size -ne (Get-Item -LiteralPath $asset.path).Length) { throw "Asset verification failed: $($asset.name)" }
    }
    & gh release edit "v$Version" --repo $repo --draft=false --latest
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
}
$verified = $false
for ($attempt=0; $attempt -lt 6; $attempt++) {
    try {
        $downloaded = (Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/$repo/releases/latest/download/update.txt").Content
        if ($downloaded -is [byte[]]) { $downloaded = [Text.Encoding]::UTF8.GetString($downloaded) }
        if ($downloaded.Trim() -eq ([IO.File]::ReadAllText($manifest)).Trim()) { $verified=$true; break }
    } catch { if ($attempt -eq 5) { throw } }
    Start-Sleep -Seconds 5
}
if (-not $verified) { throw 'Published latest manifest did not match. Check the release.' }
Write-Output "Published and verified https://github.com/$repo/releases/tag/v$Version"

