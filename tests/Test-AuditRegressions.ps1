$ErrorActionPreference='Stop'
$miniRoot=Split-Path $PSScriptRoot -Parent
$testDir=Join-Path ([IO.Path]::GetTempPath()) ('MiniMap-audit-build-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
try {
    $sources=& (Join-Path $miniRoot 'scripts\Get-MiniMapSources.ps1')
    $testExe=Join-Path $testDir 'AuditTests.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:AuditRegressionTests "/out:$testExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$miniRoot\resources\roads.bin,roads.bin" @sources (Join-Path $PSScriptRoot 'AuditRegressionTests.cs')
    if($LASTEXITCODE -ne 0) { throw 'Audit regression compilation failed.' }
    $fixture=Join-Path $testDir 'LaunchFixture.cs'
    [IO.File]::WriteAllText($fixture,'class LaunchFixture { static void Main() { System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"started.txt"),"started"); } }')
    $fixtureExe=Join-Path $testDir 'LaunchFixture.exe'
    & $compiler /nologo /target:winexe "/out:$fixtureExe" $fixture
    if($LASTEXITCODE -ne 0) { throw 'Launch fixture compilation failed.' }
    & $testExe $fixtureExe
    if($LASTEXITCODE -ne 0) { throw 'Audit regression checks failed.' }
} finally {
    $resolved=[IO.Path]::GetFullPath($testDir)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'MiniMap-audit-build-*') { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
}
