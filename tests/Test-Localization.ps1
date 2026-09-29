$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $miniRoot 'src\Localization.cs')
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
Write-Output "Validated $($english.Count) strings in all 10 languages, format arguments, and $references source references."
