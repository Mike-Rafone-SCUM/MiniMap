param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$package=[IO.Path]::GetFullPath($PackageDirectory)
$evidence=Join-Path $root 'release/checks/baltic-swahili'
$exe=Join-Path $package 'SkynettMiniMap-Test.exe'
if(-not (Test-Path -LiteralPath $exe)){$exe=Join-Path $package 'SkynettMiniMap.exe'}
$assembly=[Reflection.Assembly]::LoadFrom($exe)
[ScumMiniMap.Localization]::SelfTest()
$reference=Get-Content (Join-Path $evidence 'reference.json') -Raw | ConvertFrom-Json
$counts=@{}
foreach($code in @('lt','sw')) {
    [ScumMiniMap.Localization]::SetLanguage($code)
    $suffix=if($code -eq 'lt'){'Lt'}else{'Sw'}
    $table=[ScumMiniMap.Localization].GetField(('Strings'+$suffix)).GetValue($null)
    foreach($property in $reference.StringsEn.PSObject.Properties) {
        if(-not $table.ContainsKey($property.Name)){throw "Missing packaged string: $code $($property.Name)"}
    }
    $counts[$code]=$table.Count
    foreach($prefix in @('spawn-item-names','spawn-category-names')) {
        $name="$prefix-$code.tsv"
        $stream=$assembly.GetManifestResourceStream($name)
        if($null -eq $stream){throw "Missing packaged catalogue: $name"}
        try {
            $memory=New-Object IO.MemoryStream
            $stream.CopyTo($memory)
            $source=[IO.File]::ReadAllBytes((Join-Path $root "resources/$name"))
            if([Convert]::ToBase64String($source) -cne [Convert]::ToBase64String($memory.ToArray())){throw "Stale packaged catalogue: $name"}
        } finally {$stream.Dispose();if($memory){$memory.Dispose()}}
    }
}
$voiceResources=@($assembly.GetManifestResourceNames() | Where-Object {$_ -like 'voice-navigation/Ona (*)/*' -or $_ -like 'voice-navigation/Zuri (*)/*'})
if($voiceResources.Count -ne 24){throw 'Expected all 24 new voice clips in the executable'}
@{executable=$exe;sha256=(Get-FileHash $exe -Algorithm SHA256).Hash;interfaceStrings=$counts;newVoiceClips=$voiceResources.Count;catalogues='byte-identical to audited sources';nativeSpeakerApproval='pending'} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'package-verification.json') -Encoding UTF8
Write-Output 'Exact package localisation, catalogues and 24 navigation clips verified.'
