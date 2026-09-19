param([switch]$Install, [switch]$Increment, [string]$Version, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'

# Automated Version Increment
$miniCsPath = Join-Path $PSScriptRoot 'src\MiniMap.cs'
$miniCsText = [System.IO.File]::ReadAllText($miniCsPath, [System.Text.Encoding]::UTF8)
$activeVer = '1.1.0'

if ($Increment -or $Version) {
    if ($miniCsText -match 'public const string VersionString\s*=\s*"(\d+)\.(\d+)\.(\d+)";') {
        $major = [int]$matches[1]
        $minor = [int]$matches[2]
        $patch = [int]$matches[3]
    } else {
        throw 'Could not locate VersionString in MiniMap.cs'
    }

    if ($Version) {
        if ($Version -match '^(\d+)\.(\d+)\.(\d+)$') {
            $major = [int]$matches[1]
            $minor = [int]$matches[2]
            $patch = [int]$matches[3]
            $activeVer = "$major.$minor.$patch"
        } else {
            throw "Invalid version format '$Version'. Expected Semantic Versioning format X.Y.Z (e.g. 1.1.1)"
        }
    } else {
        $patch++
        $activeVer = "$major.$minor.$patch"
    }
    $verFull = "$major.$minor.$patch.0"

    Write-Host "Incrementing version to v$activeVer..."

    # 1. Update MiniMap.cs
    $miniCsText = $miniCsText -replace '\[assembly:\s*AssemblyVersion\("[^"]+"\)]', "[assembly: AssemblyVersion(`"$verFull`")]"
    $miniCsText = $miniCsText -replace '\[assembly:\s*AssemblyFileVersion\("[^"]+"\)]', "[assembly: AssemblyFileVersion(`"$verFull`")]"
    $miniCsText = $miniCsText -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]+"\)]', "[assembly: AssemblyInformationalVersion(`"$activeVer`")]"
    $miniCsText = $miniCsText -replace 'public const string VersionString\s*=\s*"[^"]+";', "public const string VersionString = `"$activeVer`";"
    $miniCsText = $miniCsText -replace 'public static readonly Version CurrentVersion\s*=\s*new Version\([^)]+\);', "public static readonly Version CurrentVersion = new Version($major, $minor, $patch, 0);"
    [System.IO.File]::WriteAllText($miniCsPath, $miniCsText, [System.Text.Encoding]::UTF8)

    # 2. Update README.md
    $readmePath = Join-Path $PSScriptRoot 'README.md'
    if (Test-Path -LiteralPath $readmePath) {
        $readmeText = [System.IO.File]::ReadAllText($readmePath, [System.Text.Encoding]::UTF8)
        $readmeText = $readmeText -replace '^#\s+(?:SCUM MiniMap|SkynettMiniMap)\s+v\d+\.\d+\.\d+', "# SCUM MiniMap v$activeVer"
        [System.IO.File]::WriteAllText($readmePath, $readmeText, [System.Text.Encoding]::UTF8)
    }

    # 3. Update CHANGELOG.md
    $changelogPath = Join-Path $PSScriptRoot 'CHANGELOG.md'
    if (Test-Path -LiteralPath $changelogPath) {
        $changelogText = [System.IO.File]::ReadAllText($changelogPath, [System.Text.Encoding]::UTF8)
        if ($changelogText -notmatch [regex]::Escape("## [$activeVer]")) {
            $today = (Get-Date).ToString('yyyy-MM-dd')
            $newEntry = "`n`n## [$activeVer] - $today`n`n### Changed`n- Automated build packaging update.`n- Performance refinements and maintenance release."
            $pattern = [System.Text.RegularExpressions.Regex]::new('(---)')
            $changelogText = $pattern.Replace($changelogText, "`$1$newEntry", 1)
            [System.IO.File]::WriteAllText($changelogPath, $changelogText, [System.Text.Encoding]::UTF8)
        }
    }
} elseif ($miniCsText -match 'public const string VersionString\s*=\s*"([^"]+)";') {
    $activeVer = $matches[1]
}

# Build into a unique directory. No running process is touched by an ordinary build.
$buildOutput = & (Join-Path $PSScriptRoot 'scripts\Build-GitHubRelease.ps1') -OutputDirectory $OutputDirectory
$packagePath = @($buildOutput)[-1]
$buildOutput | Write-Output
if ($Install) {
    $installed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'SkynettMiniMap.exe'))
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installed })
    foreach ($process in $running) {
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) {
            throw 'Close the installed MiniMap from its tray menu, then rerun the explicit install. No process was forcibly terminated.'
        }
    }
    Copy-Item -LiteralPath (Join-Path $packagePath 'SkynettMiniMap.exe') -Destination $installed -Force
    Start-Process -FilePath $installed -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
}
