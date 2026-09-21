param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory) { $OutputDirectory=Join-Path $root ('release\tests\responsiveness-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output) { throw 'Choose a fresh test output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
foreach($test in @('Test-UpdateChecker.ps1','Check-Stability.ps1','Test-InputVoice.ps1')) {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
    if($LASTEXITCODE -ne 0) { throw "$test failed" }
}
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-AuditRegressions.ps1')
if($LASTEXITCODE -ne 0) { throw 'Audit checks failed' }
$voiceResources=@(& (Join-Path $PSScriptRoot 'Get-VoiceResources.ps1'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@(& (Join-Path $PSScriptRoot 'Get-MiniMapSources.ps1'))
$renderTest=Join-Path $output 'RenderingChecks.exe'
& $compiler /nologo /optimize+ /target:exe /platform:x64 /define:MINIMAP_TEST /main:MapRenderingTests "/out:$renderTest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources (Join-Path $root 'tests\MapRenderingTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Rendering test compilation failed' }
& $renderTest (Join-Path $root 'resources')
if($LASTEXITCODE -ne 0) { throw 'Rendering checks failed' }
Remove-Item -LiteralPath $renderTest
$testSource=Join-Path $output 'MiniMap.test.cs'
$mainSource=Join-Path $root 'src\MiniMap.cs'
$text=[IO.File]::ReadAllText($mainSource)
$text=$text -replace 'AssemblyInformationalVersion\("[^"]+"\)', 'AssemblyInformationalVersion("1.4.8-input-voice-test.7")'
[IO.File]::WriteAllText($testSource,$text,[Text.UTF8Encoding]::new($false))
$sources=@($sources | ForEach-Object { if($_ -eq $mainSource) { $testSource } else { $_ } })
$res=Join-Path $root 'resources'
$pack=Join-Path $root 'packaging'
$exe=Join-Path $output 'SkynettMiniMap-Test.exe'
try {
    & $compiler /nologo /optimize+ /target:winexe /platform:x64 @voiceResources /define:MINIMAP_TEST "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$res\map.png,map.png" "/resource:$res\zones.tsv,zones.tsv" "/resource:$res\roads.bin,roads.bin" "/resource:$res\scummap.bin,scummap.bin" "/resource:$pack\detect_zones.py,detect_zones.py" "/win32icon:$res\App-Icon.ico" @sources
    if($LASTEXITCODE -ne 0) { throw 'Test package compilation failed' }
} finally { Remove-Item -LiteralPath $testSource }
$process=Start-Process -FilePath $exe -ArgumentList '-Check' -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Self-test timed out' }
if($process.ExitCode -ne 0) { throw 'Self-test failed' }
$process.Dispose()
Copy-Item -LiteralPath (Join-Path $root 'TESTING-RESPONSIVENESS.md') -Destination (Join-Path $output 'TESTING.md')
Copy-Item -LiteralPath (Join-Path $root 'ATTRIBUTION.md') -Destination $output
$files=@('SkynettMiniMap-Test.exe','TESTING.md','ATTRIBUTION.md') | ForEach-Object { Join-Path $output $_ }
Compress-Archive -LiteralPath $files -DestinationPath (Join-Path $output 'SCUM-MiniMap-Input-Voice-Test.zip')
Write-Output "Test package ready: $output"
