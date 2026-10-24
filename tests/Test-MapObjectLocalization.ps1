$ErrorActionPreference = 'Stop'
$miniRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$miniSources = & (Join-Path $miniRoot 'scripts\Get-MiniMapSources.ps1')
Add-Type -Path $miniSources -ReferencedAssemblies WindowsBase,PresentationCore,PresentationFramework,System.Xaml,Accessibility,System.Windows.Forms,System.Drawing,System.Xml,System.IO.Compression
$store = [ScumMiniMap.ScumMapStore]::Load((Join-Path $miniRoot 'resources'))
foreach ($category in $store.Categories) {
    $expected = $category.Section -notin @('Hunting','Fishing')
    if ($category.DefaultEnabled -ne $expected -or $store.IsCategoryEnabled($category.Id) -ne $expected) { throw "Wrong initial map default: $($category.Name)" }
}
$store.SetAllEnabled($false)
$store.ResetToDefaults()
foreach ($category in $store.Categories) {
    if ($store.IsCategoryEnabled($category.Id) -ne ($category.Section -notin @('Hunting','Fishing'))) { throw 'Reset to defaults lost the map layer policy.' }
}
$store.SetAllEnabled($false)
$savedFilters = $store.GetDisabledCategoriesString()
$store.ResetToDefaults()
$store.ApplyDisabledCategoriesString($savedFilters)
if ($store.Categories | Where-Object { $store.IsCategoryEnabled($_.Id) }) { throw 'Saved map filter preferences were overwritten by defaults.' }
$store.ResetToDefaults()
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$originalLanguage = [ScumMiniMap.Localization]::Current
try {
    $expectations = @{
        'es-AR' = @('EsAr', 'B2 Comisaría', 'Estación de servicio - Aeropuerto', 'A0 Aeródromo del puesto comercial', 'Comisaría')
        'ru' = @('Ru', 'B2 Полицейский участок', 'Заправочная станция - Аэропорт', 'A0 Аэродром торгового аванпоста', 'Полицейский участок')
        'de' = @('De', 'B2 Polizeiwache', 'Tankstelle - Flughafen', 'A0 Außenpostenflugplatz', 'Polizeiwache')
        'nl' = @('Nl', 'B2 Politiebureau', 'Tankstation - Luchthaven', 'A0 Vliegveld bij handelspost', 'Politiebureau')
        'pl' = @('Pl', 'B2 Posterunek policji', 'Stacja benzynowa - Lotnisko', 'A0 Lotnisko posterunku handlowego', 'Posterunek policji')
        'fr' = @('Fr', 'B2 Commissariat de police', 'Station-service - Aéroport', "A0 Aérodrome d'avant-poste", 'Commissariat de police')
        'zh-CN' = @('Zh', 'B2 警察局', '加油站 - 机场', 'A0 前哨机场', '警察局')
        'tr' = @('Tr', 'B2 Polis Karakolu', 'Benzin istasyonu - Havaalanı', 'A0 Ticaret karakolu havaalanı', 'Polis Karakolu')
        'ar' = @('Ar', 'B2 مركز الشرطة', 'محطة بنزين - المطار', 'A0 مطار المركز التجاري', 'مركز الشرطة')
        'pt-BR' = @('Pt', 'B2 Delegacia de Polícia', 'Posto de gasolina - Aeroporto', 'A0 Aeródromo do Posto Avançado', 'Delegacia de Polícia')
        'pt-PT' = @('PtPt', 'B2 Esquadra da polícia', 'Posto de combustível - Aeroporto', 'A0 Aeródromo do Posto Avançado', 'Esquadra da polícia')
        'ko' = @('Ko', 'B2 경찰서', '주유소 - 공항', 'A0 교역 전초기지 비행장', '경찰서')
        'th' = @('Th', 'B2 สถานีตำรวจ', 'ปั๊มน้ำมัน - สนามบิน', 'A0 สนามบินของด่านการค้า', 'สถานีตำรวจ')
        'zh-TW' = @('ZhTw', 'B2 警察局', '加油站 - 機場', 'A0 前哨機場', '警察局')
        'ja' = @('Ja', 'B2 警察署', 'ガソリンスタンド - 空港', 'A0 交易拠点の飛行場', '警察署')
        'cs' = @('Cs', 'B2 Policejní stanice', 'Čerpací stanice - Letiště', 'A0 Letiště obchodní základny', 'Policejní stanice')
        'it' = @('It', 'B2 Stazione di polizia', 'Distributore di carburante - Aeroporto', "A0 Aerodromo dell'avamposto commerciale", 'Stazione di polizia')
        'uk' = @('Uk', 'B2 Поліцейська дільниця', 'АЗС - Аеропорт', 'A0 Аеродром торгового форпосту', 'Поліцейська дільниця')
        'fi-FI' = @('Fi', 'B2 Poliisiasema', 'Huoltoasema - Lentoasema', 'A0 Etuvartion lentokenttä', 'Poliisiasema')
        'lt-LT' = @('Lt', 'B2 Policijos nuovada', 'Degalinė - Oro uostas', 'A0 Prekybos posto aerodromas', 'Policijos nuovada')
        'sw-TZ' = @('Sw', 'B2 Kituo cha polisi', 'Kituo cha mafuta - Uwanja wa ndege', 'A0 Uwanja wa ndege wa kituo cha biashara', 'Kituo cha polisi')
    }
    foreach ($language in $expectations.Keys) {
        [ScumMiniMap.Localization]::SetLanguage($language)
        $suffix = $expectations[$language][0]
        $objects = [ScumMiniMap.Localization].GetField("MapObjects$suffix", $flags).GetValue($null)
        $categories = [ScumMiniMap.Localization].GetField("CategoryNames$suffix", $flags).GetValue($null)
        foreach ($category in $store.Categories) {
            if (-not $objects.ContainsKey($category.Name) -and -not $categories.ContainsKey($category.Name)) {
                throw "Missing $language translation for map category $($category.Id): $($category.Name)"
            }
            $expected = if ($objects.ContainsKey($category.Name)) { $objects[$category.Name] } else { $categories[$category.Name] }
            if ([ScumMiniMap.Localization]::GetCategoryName($category.Name) -ne $expected) { throw 'Category presentation bypassed translation.' }
            $type = [ScumMiniMap.ScumMapStore]::Singularize($category.Name)
            if ($objects.ContainsKey($type)) { $expectedType = $objects[$type] }
            elseif ($categories.ContainsKey($type)) { $expectedType = $categories[$type] }
            else { throw "Missing $language generated type for $($category.Name): $type" }
            if ([ScumMiniMap.Localization]::GetMapObjectName("B2 $type") -ne "B2 $expectedType") {
                throw "Generated $language category marker bypassed translation: $type"
            }
        }
        $expectedStation = $expectations[$language][1]
        if ([ScumMiniMap.Localization]::GetMapObjectName('B2 Police Station') -ne $expectedStation) { throw 'Generated marker was not translated.' }
        $expectedGas = $expectations[$language][2]
        if ([ScumMiniMap.Localization]::GetMapObjectName('Gas Station - Airport') -ne $expectedGas) { throw 'Compound gas station translation failed.' }
        $expectedOutpost = $expectations[$language][3]
        if ([ScumMiniMap.Localization]::GetMapObjectName('A0 Outpost Airfield') -ne $expectedOutpost) { throw 'Longest matching marker type was not used.' }
        if ([ScumMiniMap.Localization]::GetMapObjectName('New unknown asset') -ne 'New unknown asset') { throw 'Unknown marker did not retain English fallback.' }
        $custom = New-Object ScumMiniMap.MapZone
        $custom.Name = 'B2 Police Station'
        if ($custom.DisplayName -ne $custom.Name) { throw 'Custom waypoint was translated as a generated map object.' }
        $custom.IsMapObject = $true
        if ($custom.DisplayName -ne $expectedStation -or $custom.Name -ne 'B2 Police Station') { throw 'Marker translation changed its source identity.' }
        $store.SetCategoryEnabled(10, $true)
        $query = $expectations[$language][4]
        $translated = [ScumMiniMap.DestinationSearch]::FindScumMap($store, $query, $null, 1000)
        if ($translated.Count -eq 0) { throw 'Translated search returned no police stations.' }
        $english = [ScumMiniMap.DestinationSearch]::FindScumMap($store, 'Police Station', $null, 1000)
        if ($english.Count -ne $translated.Count) { throw 'English and translated marker search disagree.' }
        foreach ($result in $translated) {
            if (-not $result.IsMapObject -or $result.DisplayName -eq $result.Name) { throw 'Search result bypassed marker translation.' }
        }
        $firstName = $translated[0].Name
        [ScumMiniMap.Localization]::SetLanguage('en')
        if ($translated[0].DisplayName -ne $firstName) { throw 'Existing selection did not refresh to English.' }
        [ScumMiniMap.Localization]::SetLanguage($language)
        if ($translated[0].DisplayName -eq $firstName -or $translated[0].Name -ne $firstName) { throw 'Language switch lost selection identity.' }
    }
    [ScumMiniMap.Localization]::SelfTest()
} finally { [ScumMiniMap.Localization]::Current = $originalLanguage }
Write-Output "Validated $($store.Categories.Count) bundled map categories in all $($expectations.Count) non-English languages, generated names, translated/English search, live selection language switching, unknown fallback and custom waypoint preservation."
