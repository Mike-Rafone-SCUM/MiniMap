$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $env:TEMP ('MiniMap-route-plan-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe /reference:System.Drawing.dll "/out:$exe" (Join-Path $root 'src/SquadRoutePlanner.cs') (Join-Path $root 'src/RoadRouter.cs') (Join-Path $root 'src/InstallationAssets.cs') (Join-Path $PSScriptRoot 'SquadRoutePlannerTests.cs')
    if($LASTEXITCODE -ne 0) { throw 'Route planning tests failed to compile' }
    & $exe
    if($LASTEXITCODE -ne 0) { throw 'Route planning tests failed' }
}finally { if(Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe } }
