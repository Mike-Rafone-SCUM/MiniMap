param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [ValidateSet('Release','Test')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$artifactRoot = [IO.Path]::GetFullPath($PackageDirectory)
$exeName = if ($Configuration -eq 'Release') { 'SkynettMiniMap.exe' } else { 'SkynettMiniMap-Test.exe' }
$zipName = if ($Configuration -eq 'Release') { 'SkynettMiniMap.zip' } else { 'SCUM-MiniMap-Test.zip' }
$exe = Join-Path $artifactRoot $exeName
# Read the native RT_MANIFEST resource as data; this never starts the executable.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class MiniMapExecutionManifest {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", EntryPoint="FindResourceW", SetLastError=true)]
    static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")]
    static extern bool FreeLibrary(IntPtr module);
    public static byte[] Read(string path) {
        // LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE | LOAD_LIBRARY_AS_IMAGE_RESOURCE:
        // resource mapping only; no executable entry point or imports are run.
        IntPtr module=LoadLibraryEx(path,IntPtr.Zero,0x40|0x20);
        if(module==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot read executable manifest.");
        try {
            IntPtr resource=FindResource(module,new IntPtr(1),new IntPtr(24));
            if(resource==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Embedded Windows manifest is missing.");
            uint size=SizeofResource(module,resource);
            if(size==0 || size>1048576) throw new InvalidOperationException("Invalid embedded Windows manifest size.");
            IntPtr loaded=LoadResource(module,resource);
            if(loaded==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot load embedded Windows manifest.");
            IntPtr data=LockResource(loaded);
            if(data==IntPtr.Zero) throw new InvalidOperationException("Cannot read embedded Windows manifest bytes.");
            byte[] bytes=new byte[(int)size];
            Marshal.Copy(data,bytes,0,bytes.Length);
            return bytes;
        } finally { FreeLibrary(module); }
    }
}
'@
$manifestBytes = [MiniMapExecutionManifest]::Read($exe)
$manifestStream = [IO.MemoryStream]::new([byte[]]$manifestBytes)
$xmlSettings = [Xml.XmlReaderSettings]::new()
$xmlSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$xmlSettings.XmlResolver = $null
$xmlReader = [Xml.XmlReader]::Create($manifestStream,$xmlSettings)
try {
    $executionManifest = [Xml.XmlDocument]::new()
    $executionManifest.XmlResolver = $null
    $executionManifest.Load($xmlReader)
} finally { $xmlReader.Dispose(); $manifestStream.Dispose() }
$manifestNamespaces = [Xml.XmlNamespaceManager]::new($executionManifest.NameTable)
$manifestNamespaces.AddNamespace('asm1','urn:schemas-microsoft-com:asm.v1')
$manifestNamespaces.AddNamespace('asm3','urn:schemas-microsoft-com:asm.v3')
$executionLevels = $executionManifest.SelectNodes('/asm1:assembly/asm3:trustInfo/asm3:security/asm3:requestedPrivileges/asm3:requestedExecutionLevel',$manifestNamespaces)
if ($executionLevels.Count -ne 1 -or $executionLevels[0].GetAttribute('level') -cne 'requireAdministrator' -or
    $executionLevels[0].GetAttribute('uiAccess') -cne 'false') {
    throw 'Packaged executable must request requireAdministrator with uiAccess=false.'
}
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
if ($Configuration -eq 'Release') {
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release product version.' }
    $expected = @($exeName,'README.md','CHANGELOG.md','ATTRIBUTION.md','detect_zones.py','requirements.txt','Start-MiniMap.cmd','Update-MiniMap.cmd','package.txt','keyboard-setup.png','assets/map-tiles.bin','assets/roads.bin')
} else {
    if ($version -notmatch '^\d+\.\d+\.\d+-test$') { throw 'Invalid test product version.' }
    if (Test-Path -LiteralPath (Join-Path $artifactRoot 'update.txt')) { throw 'Test packages must not include an updater manifest.' }
    $expected = @($exeName,'TESTING.md','ATTRIBUTION.md','assets/map-tiles.bin','assets/roads.bin')
}
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Configuration -eq 'Release') {
    $manifest = [IO.File]::ReadAllText((Join-Path $artifactRoot 'update.txt')).Trim()
    $packageHash=(Get-FileHash -LiteralPath (Join-Path $artifactRoot $zipName) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifest -ne "schema=2`nversion=$version`nsha256=$hash`npackage-sha256=$packageHash") { throw 'Manifest/package mismatch.' }
}
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $artifactRoot $zipName))
try {
    $entryDifferences=@(Compare-Object ($expected | Sort-Object) ($zip.Entries.FullName | Where-Object {$_ -ne 'assets/'} | Sort-Object))
    if ($entryDifferences.Count) { throw ('Unexpected ZIP contents: '+(($entryDifferences | ForEach-Object {$_.InputObject+' '+$_.SideIndicator}) -join ', ')) }
    $stream = $zip.GetEntry($exeName).Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $zipHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose(); $stream.Dispose() }
    if ($hash -ne $zipHash) { throw 'ZIP executable differs from standalone executable.' }
    if ($Configuration -eq 'Release') {
        $zipManifestReader = [IO.StreamReader]::new($zip.GetEntry('package.txt').Open())
        try { $zipManifest = $zipManifestReader.ReadToEnd().Trim() } finally { $zipManifestReader.Dispose() }
        if ($zipManifest -ne [IO.File]::ReadAllText((Join-Path $artifactRoot 'package.txt')).Trim()) { throw 'ZIP package manifest differs from local manifest.' }
    }
    foreach($assetName in @('map-tiles.bin','roads.bin')) {
        $assetStream=$zip.GetEntry('assets/'+$assetName).Open();$assetSha=[Security.Cryptography.SHA256]::Create()
        try {$assetHash=[BitConverter]::ToString($assetSha.ComputeHash($assetStream)).Replace('-','').ToLowerInvariant()}finally{$assetSha.Dispose();$assetStream.Dispose()}
        if($assetHash -ne (Get-FileHash -LiteralPath (Join-Path $artifactRoot ('assets/'+$assetName)) -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'ZIP asset mismatch.'}
    }
} finally { $zip.Dispose() }
$assembly = [Reflection.Assembly]::LoadFile($exe)
$resources = @($assembly.GetManifestResourceNames())
$localizationResources = @(& (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Get-LocalizationResources.ps1'))
foreach ($argument in $localizationResources) {
    $name = $argument.Substring($argument.LastIndexOf(',') + 1)
    if ($resources -notcontains $name) { throw "Missing embedded localized catalogue: $name" }
}
foreach ($name in @('installation-assets.txt','zones.tsv','water-mask.bin','scummap.bin','spawn-items.tsv','spawn-compatibility.tsv','spawn-item-names-pl.tsv','detect_zones.py','keyboard-setup.png','steam-logo.png')) {
    if ($resources -notcontains $name) { throw "Missing embedded resource: $name" }
}
foreach($name in @('map-tiles.bin','roads.bin')) {if($resources -contains $name){throw "Large asset must not be embedded: $name"}}
$assetReader=[IO.StreamReader]::new($assembly.GetManifestResourceStream('installation-assets.txt'))
try{$assetManifestText=$assetReader.ReadToEnd()}finally{$assetReader.Dispose()}
foreach($name in @('map-tiles.bin','roads.bin')) {
    $assetHash=(Get-FileHash -LiteralPath (Join-Path $artifactRoot ('assets/'+$name)) -Algorithm SHA256).Hash.ToLowerInvariant()
    if(($assetManifestText -split "`n") -notcontains ($name+'='+$assetHash)){throw 'Embedded asset identity mismatch.'}
    if($Configuration -eq 'Release' -and ($zipManifest -split "`r?`n") -notcontains ('assets/'+$name+'='+$assetHash)){throw 'Package asset identity mismatch.'}
}
if ($resources -contains 'map.png') { throw 'Redundant full-resolution map is embedded.' }
if ($resources -contains 'copy-location-key.jpg') { throw 'Obsolete copy-key screenshot is embedded.' }
if (@($resources | Where-Object { $_ -like 'voice-navigation/*' }).Count -lt 12) { throw 'Bundled voice resources missing.' }
Write-Output "Verified $Configuration package ${version}: administrator manifest, ZIP contents, embedded resources and executable hashes."
