$ErrorActionPreference='Stop'
$miniRoot=Split-Path $PSScriptRoot -Parent
$testDir=Join-Path ([IO.Path]::GetTempPath()) ('MiniMap-audit-build-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
try {
    $sources=& (Join-Path $miniRoot 'scripts\Get-MiniMapSources.ps1')
    $testExe=Join-Path $testDir 'AuditTests.exe'
    & $compiler /nologo /codepage:65001 /target:exe /platform:x64 /main:AuditRegressionTests "/out:$testExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Accessibility.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.IO.Compression.dll "/resource:$miniRoot\resources\roads.bin,roads.bin" @sources (Join-Path $PSScriptRoot 'AuditRegressionTests.cs')
    if($LASTEXITCODE -ne 0) { throw 'Audit regression compilation failed.' }
    # Owned, inert older/newer executables satisfy the real updater's stable-file
    # contract. They write one version marker and exit, with no app UI or UAC manifest.
    $fixtureExecutables=@()
    foreach($fixtureSpec in @(@{Name='Old';Version='1.2.4'},@{Name='New';Version='1.2.10'})) {
        $fixture=Join-Path $testDir ('MiniMap-LaunchFixture-'+$fixtureSpec.Name+'.cs')
        $fixtureSource=@'
using System.Reflection;
[assembly:AssemblyCompany("MikeRafone")]
[assembly:AssemblyProduct("SkynettMiniMap")]
[assembly:AssemblyVersion("VERSION.0")]
[assembly:AssemblyFileVersion("VERSION.0")]
[assembly:AssemblyInformationalVersion("VERSION")]
class MiniMapLaunchFixture {
    static void Main() {
        System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"started.txt"),"VERSION\n");
    }
}
'@
        [IO.File]::WriteAllText($fixture,$fixtureSource.Replace('VERSION',$fixtureSpec.Version))
        $fixtureExe=Join-Path $testDir ('MiniMap-LaunchFixture-'+$fixtureSpec.Name+'.exe')
        & $compiler /nologo /codepage:65001 /target:winexe "/out:$fixtureExe" $fixture
        if($LASTEXITCODE -ne 0) { throw 'Launch fixture compilation failed.' }
        $fixtureExecutables += $fixtureExe
    }
    & $testExe @fixtureExecutables
    if($LASTEXITCODE -ne 0) { throw 'Audit regression checks failed.' }
} finally {
    $resolved=[IO.Path]::GetFullPath($testDir)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'MiniMap-audit-build-*') { throw 'Unexpected cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
}
