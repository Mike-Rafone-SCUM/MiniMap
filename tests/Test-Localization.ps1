$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
Add-Type -Path @(('Localization.cs','Localization.MapObjects.cs','Localization.MapObjects.DeNlPl.cs','Localization.Polish.cs','Localization.SpawnItems.cs','Localization.Extended.cs','Localization.Additional.cs') | ForEach-Object { Join-Path $miniRoot "src\$_" })
[ScumMiniMap.Localization]::SelfTest()
$english = [ScumMiniMap.Localization]::StringsEn
$references = 0
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $miniRoot 'src') -Filter '*.cs') {
    $text = [IO.File]::ReadAllText($source.FullName)
    foreach ($match in [regex]::Matches($text, 'Localization\.(?:Get|T)\("([^"\r\n]+)"')) {
        $key = $match.Groups[1].Value
        if (-not $english.ContainsKey($key)) { throw "Unknown localization key '$key' in $($source.Name)" }
        $references++
    }
}
# Prove the check does not silently accept English fallback or broken formatting.
$spanishField = [ScumMiniMap.Localization].GetField('StringsEsAr', [Reflection.BindingFlags]'Public,NonPublic,Static')
$spanish = $spanishField.GetValue($null)
$original = $spanish['HistorySecondsAgo']
try {
    $spanish.Remove('HistorySecondsAgo') | Out-Null
    $rejected = $false
    try { [ScumMiniMap.Localization]::SelfTest() } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing translation was not rejected.' }
    $spanish['HistorySecondsAgo'] = 'Sin argumento'
    $rejected = $false
    try { [ScumMiniMap.Localization]::SelfTest() } catch { $rejected = $true }
    if (-not $rejected) { throw 'Mismatched format arguments were not rejected.' }
} finally { $spanish['HistorySecondsAgo'] = $original }
[ScumMiniMap.Localization]::SelfTest()
$previousLanguage = [ScumMiniMap.Localization]::Current
try {
    [ScumMiniMap.Localization]::SetLanguage('ko-KR')
    if ([ScumMiniMap.Localization]::CurrentCode -ne 'ko') { throw 'Korean culture was not recognized.' }
    if ([ScumMiniMap.Localization]::Get('Settings') -ne '설정') { throw 'Korean UI fell back to English.' }
    if ([ScumMiniMap.Localization]::GetSectionName('Buildings') -ne '건물') { throw 'Korean section translation missing.' }
    if ([ScumMiniMap.Localization]::GetCategoryName('Gas stations') -ne '주유소') { throw 'Korean category translation missing.' }
    if ([ScumMiniMap.Localization]::GetZoneName('Airport') -ne '공항') { throw 'Korean map label missing.' }
    if ([ScumMiniMap.Localization]::GetZoneName('Gas Station - Airport') -ne '주유소 - 공항') { throw 'Korean compound map label missing.' }
    if ([ScumMiniMap.Localization]::GetZoneName('My custom waypoint') -ne 'My custom waypoint') { throw 'Custom waypoint name was changed.' }
    $korean = [ScumMiniMap.Localization]::StringsKo
    $originalKorean = $korean['HistorySecondsAgo']
    try {
        $korean.Remove('HistorySecondsAgo') | Out-Null
        $rejected = $false
        try { [ScumMiniMap.Localization]::SelfTest() } catch { $rejected = $true }
        if (-not $rejected) { throw 'Missing Korean translation was not rejected.' }
    } finally { $korean['HistorySecondsAgo'] = $originalKorean }
} finally { [ScumMiniMap.Localization]::Current = $previousLanguage }
[ScumMiniMap.Localization]::SelfTest()
$addedLanguageExpectations=@{
    'th-TH'=@('th','Th','การตั้งค่า','อาคาร')
    'zh-TW'=@('zh-TW','ZhTw','設定','建築物')
    'ja-JP'=@('ja','Ja','設定','建物')
    'cs-CZ'=@('cs','Cs','Nastavení','Budovy')
    'it-IT'=@('it','It','Impostazioni','Edifici')
    'uk-UA'=@('uk','Uk','Налаштування','Будівлі')
}
try {
    foreach($culture in $addedLanguageExpectations.Keys) {
        $expected=$addedLanguageExpectations[$culture]
        [ScumMiniMap.Localization]::SetLanguage($culture)
        if([ScumMiniMap.Localization]::CurrentCode -cne $expected[0]){throw "Culture selected the wrong language: $culture"}
        if([ScumMiniMap.Localization]::Get('Settings') -cne $expected[2]){throw "New language UI fell back: $culture"}
        if([ScumMiniMap.Localization]::GetSectionName('Buildings') -cne $expected[3]){throw "New section fell back: $culture"}
        $currentEnum=[ScumMiniMap.Localization]::Current
        if([ScumMiniMap.Localization]::LanguageAt([int]$currentEnum) -ne $currentEnum){throw 'Selector index changed language identity'}
        [ScumMiniMap.Localization]::SetLanguage([ScumMiniMap.Localization]::CurrentCode)
        if([ScumMiniMap.Localization]::Current -ne $currentEnum){throw 'Saved language code did not round-trip'}
        $catalogue=[ScumMiniMap.Localization].GetField(('Strings'+$expected[1]),[Reflection.BindingFlags]'Public,Static').GetValue($null)
        $savedText=$catalogue['HistorySecondsAgo']
        try {
            $catalogue.Remove('HistorySecondsAgo') | Out-Null
            $rejected=$false
            try{[ScumMiniMap.Localization]::SelfTest()}catch{$rejected=$true}
            if(!$rejected){throw 'Missing new-language translation silently used English'}
        }finally{$catalogue['HistorySecondsAgo']=$savedText}
    }
    foreach($culture in @('zh-Hant','zh-Hant-TW','zh-HK','zh-MO')) {
        [ScumMiniMap.Localization]::SetLanguage($culture)
        if([ScumMiniMap.Localization]::CurrentCode -cne 'zh-TW'){throw "Traditional Chinese culture collapsed to Simplified: $culture"}
    }
    foreach($culture in @('zh-CN','zh-Hans','zh-SG')) {
        [ScumMiniMap.Localization]::SetLanguage($culture)
        if([ScumMiniMap.Localization]::CurrentCode -cne 'zh-CN'){throw "Simplified Chinese culture changed: $culture"}
    }
}finally{[ScumMiniMap.Localization]::Current=$previousLanguage}
[ScumMiniMap.Localization]::SelfTest()
Write-Output "Validated $($english.Count) strings in all $([ScumMiniMap.Localization]::LanguageDisplayNames.Length) languages, map names, format arguments, and $references source references."
