param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$exe = Get-Item -LiteralPath $Executable
$version = $exe.VersionInfo.ProductVersion
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'The executable must have a stable X.Y.Z product version.' }
$hash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), "schema=1`nversion=$version`nsha256=$hash`n", [Text.UTF8Encoding]::new($false))
Write-Output "Generated update manifest for v$version"
