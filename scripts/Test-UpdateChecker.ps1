$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testBin = Join-Path ([IO.Path]::GetTempPath()) ('MiniMap-UpdateTests-' + [guid]::NewGuid().ToString('N') + '.exe')
try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    & $compiler /nologo /target:exe "/out:$testBin" (Join-Path $repoRoot 'src\UpdateService.cs') (Join-Path $repoRoot 'tests\UpdateTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Update tests failed to compile.' }
    & $testBin
    if ($LASTEXITCODE -ne 0) { throw 'Update tests failed.' }
} finally { if (Test-Path -LiteralPath $testBin) { Remove-Item -LiteralPath $testBin } }
