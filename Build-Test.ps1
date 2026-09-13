$ErrorActionPreference = 'Stop'

Write-Host "Building Road Navigation Test Package (Isolated Build)..."

$testRelease = Join-Path $PSScriptRoot 'release\test-road-nav'
$testPackage = Join-Path $testRelease 'SkynettMiniMap-RoadTest'
$testOutput = Join-Path $testPackage 'SkynettMiniMap-RoadTest.exe'
$zipTarget = Join-Path $testRelease 'SkynettMiniMap-RoadTest.zip'

# Clean previous test package directory if present
if (Test-Path -LiteralPath $testPackage) {
    Remove-Item -LiteralPath $testPackage -Recurse -Force
}
New-Item -ItemType Directory -Path $testPackage -Force | Out-Null

# Step 1: Pre-flight checks on source scripts
Write-Host "Running pre-flight checks on sources..."
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-MiniMap.ps1') -Check
if ($LASTEXITCODE -ne 0) { throw 'Source checks failed.' }

& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Check-Stability.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Stability checks failed.' }

# Step 2: Ensure roads.bin exists
$roadsBin = Join-Path $PSScriptRoot 'roads.bin'
if (-not (Test-Path -LiteralPath $roadsBin)) {
    Write-Host "Compiling roads.bin from road graph..."
    $repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
    & python (Join-Path $repoRoot 'tools\compile_road_network.py')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $roadsBin)) {
        throw 'Failed to generate roads.bin'
    }
}

# Step 3: Compile standalone test executable with embedded road network
Write-Host "Compiling $testOutput with embedded roads.bin..."
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('UpdateService.cs','Localization.cs','RoadRouter.cs','MiniMap.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }

& $compiler /nologo /optimize+ /target:winexe "/out:$testOutput" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$PSScriptRoot\map.png,map.png" "/resource:$PSScriptRoot\zones.tsv,zones.tsv" "/resource:$PSScriptRoot\roads.bin,roads.bin" @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

# Copy companion assets
foreach ($asset in @('detect_zones.py','requirements.txt','README.md','CHANGELOG.md')) {
    $assetPath = Join-Path $PSScriptRoot $asset
    if (Test-Path -LiteralPath $assetPath) {
        Copy-Item -LiteralPath $assetPath -Destination $testPackage -Force
    }
}

# Step 4: Run packaged self-tests on the output binary
Write-Host "Validating packaged test executable..."
$proc = Start-Process -FilePath $testOutput -ArgumentList '-Check' -WindowStyle Hidden -PassThru
if (-not $proc.WaitForExit(30000)) {
    $proc.Kill()
    throw 'Packaged checks timed out after 30 seconds.'
}
$exitCode = $proc.ExitCode
$proc.Dispose()
if ($exitCode -ne 0) { throw "Packaged executable self-test failed with code $exitCode." }

# Step 5: Generate checksums and zip package
Get-ChildItem -LiteralPath $testPackage -File | Get-FileHash -Algorithm SHA256 | Select-Object @{n='File';e={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRelease 'checksums.json')

Write-Host "Packaging $zipTarget..."
Compress-Archive -LiteralPath $testPackage -DestinationPath $zipTarget -Force

$exeSize = (Get-Item $testOutput).Length / 1MB
$zipSize = (Get-Item $zipTarget).Length / 1MB

Write-Host "`nSUCCESS! Test package built without modifying production release:"
Write-Host "  Test Binary : $testOutput ($("{0:F2}" -f $exeSize) MB)"
Write-Host "  Test Archive: $zipTarget ($("{0:F2}" -f $zipSize) MB)"
