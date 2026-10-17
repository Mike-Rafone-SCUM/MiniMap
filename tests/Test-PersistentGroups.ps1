param([string]$Address='92.5.169.22',[int]$Port=47832)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $env:TEMP ('MiniMap-persistent-tests-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$output" (Join-Path $root 'src\PrivatePositions.cs') (Join-Path $root 'src\RelayGroups.cs') (Join-Path $root 'src\PeerNetworkSetup.cs') (Join-Path $root 'src\Position.cs') (Join-Path $PSScriptRoot 'PersistentGroupTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Persistent group tests did not compile'}
    & $output $Address $Port
    if($LASTEXITCODE -ne 0){throw 'Persistent group tests failed'}
}finally{if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output}}
