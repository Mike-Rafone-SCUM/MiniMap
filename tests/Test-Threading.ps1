$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $env:TEMP ('MiniMap-threading-tests-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /codepage:65001 /target:exe "/out:$output" (Join-Path $root 'src/PrivatePositions.cs') (Join-Path $root 'src/RelayGroups.cs') (Join-Path $root 'src/PeerNetworkSetup.cs') (Join-Path $root 'src/Position.cs') (Join-Path $PSScriptRoot 'ThreadingTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Threading regressions did not compile'}
    & $output
    if($LASTEXITCODE -ne 0){throw 'Threading regressions failed'}
}finally { if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output} }
