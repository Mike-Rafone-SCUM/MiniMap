$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $env:TEMP ('MiniMap-private-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
try {
    $exe=Join-Path $output 'PrivatePositionsTests.exe'
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe "/out:$exe" (Join-Path $root 'src/PrivatePositions.cs') (Join-Path $root 'src/PeerNetworkSetup.cs') (Join-Path $root 'src/Position.cs') (Join-Path $PSScriptRoot 'PrivatePositionsTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Private position tests did not compile'}
    & $exe
    if($LASTEXITCODE -ne 0){throw 'Private position tests failed'}
}finally {
    $resolved=(Resolve-Path -LiteralPath $output).Path
    $tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^MiniMap-private-tests-[a-f0-9]{32}$'){throw 'Unsafe test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
