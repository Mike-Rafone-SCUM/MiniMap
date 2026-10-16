param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$artifactRoot=[IO.Path]::GetFullPath($PackageDirectory)
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $artifactRoot 'SkynettMiniMap.exe'))
$release=[ScumMiniMap.UpdateRelease]::Parse([IO.File]::ReadAllText((Join-Path $artifactRoot 'update.txt')))
if(-not $release.IsPackage){throw 'Expected a package update manifest.'}
$archiveBytes=[IO.File]::ReadAllBytes((Join-Path $artifactRoot 'SkynettMiniMap.zip'))
$manifestBytes=[Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText((Join-Path $artifactRoot 'update.txt')))
$transport=[Func[Uri,int,byte[]]] {param($uri,$limit) if($uri.AbsolutePath.EndsWith('update.txt')){return ,$manifestBytes}; return ,$archiveBytes}
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('MiniMap-PackageArtifact-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$staged=Join-Path $scratch 'verified.update.exe'
try {
    $service=[ScumMiniMap.UpdateService]::new([ScumMiniMap.UpdateService]::Repository,$scratch,$transport)
    $service.Download($release,$staged)
    if((Get-FileHash -LiteralPath $staged).Hash -ne (Get-FileHash -LiteralPath (Join-Path $artifactRoot 'SkynettMiniMap.exe')).Hash){throw 'Extracted EXE mismatch.'}
    foreach($name in @('map-tiles.bin','roads.bin')) {
        if((Get-FileHash -LiteralPath (Join-Path ($staged+'.assets') $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $artifactRoot ('assets/'+$name))).Hash){throw 'Extracted asset mismatch.'}
    }
    Write-Output 'Real built ZIP passed the application package updater: ZIP digest, allowlist, manifest, EXE and both asset hashes.'
}finally {
    # Delete only this probe's explicitly named files; unexpected files are preserved.
    foreach($name in @('map-tiles.bin','roads.bin')){[IO.File]::Delete((Join-Path ($staged+'.assets') $name))}
    if([IO.Directory]::Exists($staged+'.assets')){[IO.Directory]::Delete($staged+'.assets',$false)}
    [IO.File]::Delete($staged)
    [IO.Directory]::Delete($scratch,$false)
}
