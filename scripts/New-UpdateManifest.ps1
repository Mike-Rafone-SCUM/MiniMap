param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [string]$Package,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$exe = Get-Item -LiteralPath $Executable
$version = $exe.VersionInfo.ProductVersion
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'The executable must have a stable X.Y.Z product version.' }
$hash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$content="schema=1`nversion=$version`nsha256=$hash`n"
if($Package) { $packageHash=(Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant();$content="schema=2`nversion=$version`nsha256=$hash`npackage-sha256=$packageHash`n" }
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $content, [Text.UTF8Encoding]::new($false))
Write-Output "Generated update manifest for v$version"
