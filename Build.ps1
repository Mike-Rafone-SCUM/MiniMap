param([switch]$Install, [switch]$NoIncrement, [string]$Version)
$ErrorActionPreference = 'Stop'

# Automated Version Increment
$miniCsPath = Join-Path $PSScriptRoot 'MiniMap.cs'
$miniCsText = [System.IO.File]::ReadAllText($miniCsPath, [System.Text.Encoding]::UTF8)
$activeVer = '1.1.0'

if (-not $NoIncrement -or $Version) {
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
        $readmeText = $readmeText -replace '^#\s+SkynettMiniMap\s+v\d+\.\d+\.\d+', "# SkynettMiniMap v$activeVer"
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

$miniRelease = Join-Path $PSScriptRoot 'release'
$miniPackage = Join-Path $miniRelease 'SkynettMiniMap'
$miniOutput = Join-Path $miniPackage 'SkynettMiniMap.exe'
Get-Process SkynettMiniMap,ScumMiniMap -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $miniOutput -or $_.Path -eq (Join-Path $PSScriptRoot 'SkynettMiniMap.exe') -or $_.Path -eq (Join-Path $PSScriptRoot 'ScumMiniMap.exe') } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 200
if (Test-Path -LiteralPath $miniPackage) { Remove-Item -LiteralPath $miniPackage -Recurse -Force }
New-Item -ItemType Directory -Path $miniPackage -Force | Out-Null
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-MiniMap.ps1') -Check
if ($LASTEXITCODE -ne 0) { throw 'Source checks failed.' }
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Check-Stability.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Stability checks failed.' }
$miniCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$miniSources = @('UpdateService.cs','Localization.cs','RoadRouter.cs','MiniMap.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $miniCompiler /nologo /optimize+ /target:winexe "/out:$miniOutput" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$PSScriptRoot\map.png,map.png" "/resource:$PSScriptRoot\zones.tsv,zones.tsv" "/resource:$PSScriptRoot\roads.bin,roads.bin" @miniSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
foreach ($miniAsset in @('detect_zones.py','requirements.txt','README.md','CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $miniAsset) -Destination $miniPackage -Force
}
$miniCheck = Start-Process -FilePath $miniOutput -ArgumentList '-Check' -WindowStyle Hidden -PassThru
if (-not $miniCheck.WaitForExit(30000)) { throw 'Packaged checks did not finish within 30 seconds.' }
$miniCheckExit = $miniCheck.ExitCode
$miniCheck.Dispose()
if ($miniCheckExit -ne 0) { throw 'Packaged executable checks failed.' }
foreach ($miniPreview in @('preview.png','settings-preview.png','search-preview.png','zones-preview.png','zones.tsv')) {
    $miniPreviewPath = Join-Path $miniPackage $miniPreview
    if (Test-Path -LiteralPath $miniPreviewPath) { Remove-Item -LiteralPath $miniPreviewPath }
}
# Produce version-named executable for direct itch.io upload
$versionedExeName = "v$activeVer - SkynettMiniMap.exe"
$versionedExePath = Join-Path $miniRelease $versionedExeName
Copy-Item -LiteralPath $miniOutput -Destination $versionedExePath -Force
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-UpdateChecker.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Update checker tests failed.' }
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -Executable $miniOutput -OutputPath (Join-Path $miniRelease 'update.txt')

Get-ChildItem -LiteralPath $miniPackage -File | Get-FileHash -Algorithm SHA256 | Select-Object @{n='File';e={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $miniRelease 'checksums.json')
$zipTarget = Join-Path $miniRelease 'SkynettMiniMap.zip'
for ($zipRetry = 0; $zipRetry -lt 5; $zipRetry++) {
    try {
        Compress-Archive -LiteralPath $miniPackage -DestinationPath $zipTarget -Force
        break
    } catch {
        if ($zipRetry -eq 4) { throw }
        Start-Sleep -Milliseconds 400
    }
}
if ($Install) {
    Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class MiniMapReleaseShutdown {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 public static void Close(uint process) {
  EnumWindows((h,p)=> { uint id; GetWindowThreadProcessId(h,out id); if(id==process) {
   var text=new StringBuilder(256); GetWindowText(h,text,256);
    if(text.ToString().StartsWith("Search place or grid") || text.ToString().StartsWith("Buscar lugar o cuadrícula")) PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero);
    if(text.ToString().StartsWith("SkynettMiniMap Settings") || text.ToString().StartsWith("SkynettMiniMap Ajustes")) { PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero); PostMessage(h,0x10,IntPtr.Zero,IntPtr.Zero); }
  } return true; },IntPtr.Zero);
 }
}
"@
    $miniInstalled = Join-Path $PSScriptRoot 'SkynettMiniMap.exe'
    $miniLegacy = Join-Path $PSScriptRoot 'ScumMiniMap.exe'
    if (Test-Path -LiteralPath $miniLegacy) { Remove-Item -LiteralPath $miniLegacy -Force -ErrorAction SilentlyContinue }
    $miniRunning = @(Get-Process SkynettMiniMap,ScumMiniMap -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $miniInstalled -or $_.Path -eq $miniLegacy })
    foreach ($miniProcess in $miniRunning) {
        [MiniMapReleaseShutdown]::Close($miniProcess.Id)
        if (-not $miniProcess.WaitForExit(3000)) {
            Stop-Process -Id $miniProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Copy-Item -LiteralPath $miniOutput -Destination $miniInstalled -Force
    Start-Sleep -Milliseconds 250
    Start-Process -FilePath $miniInstalled -WorkingDirectory $PSScriptRoot
}
Write-Output "Build v$activeVer checks passed. Package: $miniRelease\SkynettMiniMap.zip"
