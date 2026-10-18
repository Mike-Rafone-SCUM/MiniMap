$ErrorActionPreference = 'Stop'

Write-Host "Compiling standalone bridge and water avoidance test harness..."

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$miniRoot = Split-Path $PSScriptRoot -Parent
$testBin = Join-Path $PSScriptRoot 'Test-BridgeAndWaterRouter.exe'
$routerCs = Join-Path $miniRoot 'src\RoadRouter.cs'
$roadsBin = Join-Path $miniRoot 'resources\roads.bin'
$waterBin = Join-Path $miniRoot 'resources\water-mask.bin'

$harnessPath = Join-Path $PSScriptRoot 'CapturedRoadTests.cs'

try {
    & $compiler /nologo /codepage:65001 /optimize+ /target:exe "/out:$testBin" /reference:System.Drawing.dll /reference:System.dll "/resource:$roadsBin,roads.bin" "/resource:$waterBin,water-mask.bin" $routerCs (Join-Path $miniRoot 'src\InstallationAssets.cs') $harnessPath
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }

    Write-Host "Running Test-BridgeAndWaterRouter.exe..."
    & $testBin
    if ($LASTEXITCODE -ne 0) { throw "Bridge unit tests failed with code $LASTEXITCODE" }
} finally {
    if (Test-Path -LiteralPath $testBin) { Remove-Item -LiteralPath $testBin -Force }
}
