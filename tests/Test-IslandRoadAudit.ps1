param(
    [Parameter(Mandatory=$true)][string]$BeforeRoads,
    [Parameter(Mandatory=$true)][string]$AfterRoads,
    [Parameter(Mandatory=$true)][string]$OutputFile
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $env:TEMP ('MiniMap-island-audit-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "/out:$exe" "/resource:$root/resources/water-mask.bin,water-mask.bin" (Join-Path $root 'src/RoadRouter.cs') (Join-Path $root 'src/InstallationAssets.cs') (Join-Path $PSScriptRoot 'IslandRoadAudit.cs')
    if($LASTEXITCODE -ne 0) { throw 'Island audit compilation failed' }
    & $exe ([IO.Path]::GetFullPath($BeforeRoads)) ([IO.Path]::GetFullPath($AfterRoads)) ([IO.Path]::GetFullPath($OutputFile))
    if($LASTEXITCODE -ne 0) { throw 'Northern B2 cannot reach every surveyed Z-island destination' }
}finally { if(Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe } }
