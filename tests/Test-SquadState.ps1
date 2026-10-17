$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $env:TEMP ('MiniMap-squad-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
$exe=Join-Path $folder 'SquadStateTests.exe'
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe "/out:$exe" (Join-Path $root 'src/SquadState.cs') (Join-Path $root 'src/PrivatePositions.cs') (Join-Path $root 'src/PeerNetworkSetup.cs') (Join-Path $root 'src/Position.cs') (Join-Path $PSScriptRoot 'SquadStateTests.cs')
    if($LASTEXITCODE -ne 0) { throw 'Squad tests failed to compile' }
    & $exe
    if($LASTEXITCODE -ne 0) { throw 'Squad state tests failed' }
}finally {
    # This fresh, uniquely named directory contains only the compiler output.
    if(Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe }
    Remove-Item -LiteralPath $folder
}
