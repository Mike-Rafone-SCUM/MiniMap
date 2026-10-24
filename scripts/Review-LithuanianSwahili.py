"""Apply explicit terminology and instructional copy corrections to offline drafts."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
reference=json.loads((ROOT/'release/checks/baltic-swahili/reference.json').read_text(encoding='utf-8-sig'))['StringsEn']
path=ROOT/'resources/lithuanian-swahili-review.json'
review=json.loads(path.read_text(encoding='utf8'))
# key | Lithuanian | Swahili
copy='''HabitatHelp|Skiltyje „Žvejyba“ arba „Medžioklė“ pasirinkite rūšis, kurių ribas norite matyti. Žuvų kontūrai žymi paskelbtas jų atsiradimo tūrių ribas, o ne krantus. Medžioklės kontūrai remiasi ScumMap biomų vadovu; šaltiniuose rūšys priskiriamos skirtingai, todėl jų atsiradimas negarantuojamas. Prieinamumui įtakos turi veiklos vietos, gylis, laikas, masalas ir serverio populiacija. Esami žvejybos ir medžioklės objektai rodomi atskirai.|Chagua spishi katika Uvuvi au Uwindaji ili kuonyesha mipaka yake. Mipaka ya samaki inawakilisha maeneo ya kutokea yaliyochapishwa, si ufuo. Mipaka ya uwindaji hufuata mwongozo wa mazingira wa ScumMap; vyanzo hutofautiana kuhusu spishi na havihakikishi kutokea kwake. Maeneo ya shughuli, kina, muda, chambo na idadi ya wanyama kwenye seva huathiri upatikanaji. Sehemu zilizopo za uvuvi na uwindaji hubaki tofauti.
MapBoundsInvalid|Įveskite tinkamas pikselių reikšmes. Mažiausias dydis: 640 × 420.|Ingiza thamani halali za pikseli. Ukubwa wa chini: 640 × 420.
SquadDelete|Ištrinti|Futa
WpfGettingStarted|MINIMAP · DARBO PRADŽIA|MINIMAP · MWANZO
WpfTestBuild|BANDOMOJI VERSIJA|TOLEO LA MAJARIBIO
FieldTarget|TAIKINYS|LENGO
PlayersChooseServer|Prieš importuodami įveskite ir pasirinkite serverio pavadinimą.|Ingiza na uchague jina la seva kabla ya kuingiza data.
WizardKeybindsTitle|FIZINIŲ KLAVIŠŲ NUSTATYMAS|KUSANIDI VIFUNGO VYA KIBODI
WizardPressKeyPrompt|DABAR PASPAUSKITE BET KURĮ FIZINĮ KLAVIŠĄ...|BONYEZA KITUFЕ CHOCHOTE CHA KIBODI SASA...
WizardBtnResetDefaults|ATKURTI NUMATYTUOSIUS|REJESHA MIPANGILIO YA AWALI
WizardBtnCapture|UŽFIKSUOTI|NAKILI KITUFЕ
WizardBtnSkip|PRALEISTI ŽINGSNĮ >>|RUKA HATUA >>
WizardTabKeybinds|03. KLAVIŠAI|03. VIFUNGO
WizardBranding|SKYNETT TAKTINĖ ŽVALGYBA // HUD PAGALBININKAS|UCHUNGUZI WA KIMBINU WA SKYNETT // MSAIDIZI WA HUD
WizardTabOverview|01. APŽVALGA|01. MUHTASARI
WizardTabControls|02. VALDYMAS|02. VIDHIBITI
WizardTabSafety|04. SAUGA|04. USALAMA
WizardTabFeatures|05. ZONOS IR OBJEKTAI|05. MAENEO NA SEHEMU MUHIMU
WizardBtnPrev|< ATGAL|< RUDI
WizardBtnNext|TOLIAU >|ENDELEA >
WizardBtnFinish|PALEISTI HUD|ANZISHA HUD
HeaderMapZones|SKYNETT / ŽEMĖLAPIO ZONOS|SKYNETT / MAENEO YA RAMANI
HeaderSettings|SKYNETT / NUSTATYMAI v{0}|SKYNETT / MIPANGILIO v{0}
HeaderWaypoint|SKYNETT / KELIO TAŠKAS|SKYNETT / KITUO CHA NJIA
OpacityLabel|NEPERMATOMUMAS|KUTOPENYEZA MWANGA
SidebarLayers|ŽEMĖLAPIO SLUOKSNIAI|TABAKA ZA RAMANI
SidebarTitle|TAKTINIS ŽEMĖLAPIS|RAMANI YA KIMBINU
SidebarTools|SPARTIEJI ĮRANKIAI|ZANA ZA HARAKA
SidebarCategories|OBJEKTŲ KATEGORIJOS|AINA ZA SEHEMU MUHIMU
WaypointNamePrompt|Įveskite šio kelio taško pavadinimą:|Ingiza jina la kituo hiki cha njia:
ZeNewLayerPrompt|Įveskite naujo zonų sluoksnio pavadinimą:|Ingiza jina la tabaka jipya la maeneo:
BiomeMeadow|Pieva|Uwanda wa nyasi
ProfileAlias|{0} / kitas pavadinimas {1}|{0} / jina jingine {1}
ProfileAssetMarker|{0} #{1} · {2} · kopija {3}|{0} #{1} · {2} · nakala {3}
AdminRailTeleport|Teleportuoti|Hamisha papo hapo
SpawnCartTitle|Daiktų kūrimas|Kuunda vitu
SpawnCartOpen|Daiktų kūrimas / sąrašas...|Kuunda vitu / orodha...
SpawnCartHelp|Ieškokite SCUM Database daiktų, pasirinkite kiekius ir išsiųskite sąrašą į SCUM pokalbį. Daiktai sukuriami šalia jūsų administratoriaus veikėjo.|Tafuta vitu vya SCUM Database, chagua idadi, kisha tuma orodha kwenye gumzo la SCUM. Vitu huundwa karibu na mhusika wako wa msimamizi.
SpawnCartId|Daikto ID|ID ya kitu
SpawnCartName|Daiktas|Kitu
SpawnCartQuantity|Kiekis|Idadi
SpawnCartContents|Daiktų kūrimo sąrašas|Orodha ya vitu vya kuunda
SpawnCartClear|Išvalyti sąrašą|Futa orodha
SpawnCartSend|Siųsti sąrašą|Tuma orodha
SpawnCartSave|Išsaugoti rinkinį...|Hifadhi seti...
SpawnCartLoad|Įkelti rinkinį...|Pakia seti...
SpawnCartTarget|Kūrimo vieta|Mahali pa kuunda vitu
SpawnCartTargetSelf|Šalia jūsų administratoriaus veikėjo|Karibu na mhusika wako wa msimamizi
SpawnCartModifierTitle|Kuriamų daiktų savybės — {0}|Sifa za vitu vya kuunda — {0}
SpawnCartModifierUnknown|Nežinoma kuriamo daikto savybė: {0}.|Sifa ya kitu cha kuunda haijulikani: {0}.
SpawnCartModifierPairs|Įveskite savybių ir reikšmių poras, pavyzdžiui, Health 100 Weight 0.5.|Ingiza jozi za sifa na thamani, kwa mfano Health 100 Weight 0.5.
SpawnCartModifierValues|Health ir Dirtiness: 0–100 %. Uses: 0 arba daugiau. StackCount: 1 arba daugiau. Weight: kg. Uses ir StackCount ribas nustato daiktas žaidime SCUM.|Health na Dirtiness: 0–100%. Uses: 0 au zaidi. StackCount: 1 au zaidi. Weight: kg. Mipaka ya Uses na StackCount hutegemea kitu ndani ya SCUM.
SpawnCartModifierValue|Įveskite tinkamą {0} reikšmę.|Ingiza thamani halali ya {0}.
SpawnCartModifierLength|Visa daikto kūrimo komanda turi tilpti į 512 ženklų.|Amri nzima ya kuunda kitu isizidi vibambo 512.
SpawnCartModifierLocation|Location: X=… Y=… Z=…, trys koordinatės, nukopijuotas SCUM vietos tekstas arba 17 skaitmenų Steam ID.|Location: X=… Y=… Z=…, viwianishi vitatu, maandishi ya eneo yaliyonakiliwa kutoka SCUM, au Steam ID yenye tarakimu 17.
SpawnCartKitFilter|SCUM daiktų rinkiniai (*.tsv)|Seti za vitu vya SCUM (*.tsv)
SpawnCartKitName|Rinkinio pavadinimas|Jina la seti
SpawnCartKitDetails|{0} komandų · {1} daiktų · atnaujinta {2}|Amri {0} · vitu {1} · imesasishwa {2}
SpawnCartKitNameInvalid|Įveskite 1–80 ženklų rinkinio pavadinimą be valdymo ženklų.|Ingiza jina la seti lenye vibambo 1–80 bila vibambo vya udhibiti.
SpawnCartKitReplace|Pakeisti rinkinį „{0}“ dabartiniu sąrašu?|Ungependa kubadilisha seti “{0}” kwa orodha ya sasa?
SpawnCartMeleeWeapons|Artimosios kovos ginklai|Silaha za karibu
SpawnCartRangedWeapons|Nuotoliniai ginklai|Silaha za mbali
SpawnCartEmpty|Pirmiausia įtraukite daiktų į sąrašą.|Ongeza vitu kwenye orodha kwanza.
SpawnCartSearch|Ieškoti pagal pavadinimą arba daikto ID|Tafuta kwa jina au ID ya kitu
SpawnCartSearchHint|Bandykite AK47 arba AK47 scope. Rezultatams išskleisti ar sutraukti naudokite skyrių rodykles.|Jaribu AK47 au AK47 scope. Tumia vishale vya sehemu kupanua au kukunja matokeo.
SpawnCartClearConfirm|Pašalinti visus daiktus iš šio sąrašo?|Ungependa kuondoa vitu vyote kwenye orodha hii?
SpawnCartUnconfirmed|Komandos išsiuntimas nepatvirtina, kad serveris sukūrė daiktus.|Kutuma amri hakuthibitishi kuwa seva imeunda vitu.
SpawnCartInterrupted|Išsiųsta {0} iš {1} komandų. Neišsiųsti daiktai lieka sąraše. {2}|Amri {0} kati ya {1} zimetumwa. Vitu ambavyo havijatumwa vinabaki kwenye orodha. {2}
SpawnCartLimitExceeded|Būtų viršytos sąrašo ribos.|Hatua hii itazidi mipaka ya orodha.
SpawnCartLimits|Iki 100 daiktų tipų, po 100 kiekvieno daikto ir 1,000 daiktų viename sąraše.|Hadi aina 100 za vitu, vitu 100 vya kila aina na jumla ya vitu 1,000 kwa orodha.
SpawnCartInvalid|Sąraše yra nežinomas daikto ID arba netinkamas kiekis.|Orodha ina ID ya kitu isiyojulikana au idadi isiyo halali.
SpawnCartForegroundBlocked|Prieš siųsdami sąrašą grįžkite į SCUM arba MiniMap.|Rudi kwenye SCUM au MiniMap kabla ya kutuma orodha.
SpawnCartInvalidCommand|Sąraše yra netinkama daikto kūrimo komanda.|Orodha ina amri isiyo halali ya kuunda kitu.
SpawnCartChatBinding|SCUM pokalbio klavišas: {0}. Jis turi atitikti SCUM valdymo nustatymus.|Kitufe cha gumzo la SCUM: {0}. Lazima kilingane na mipangilio ya udhibiti ya SCUM.
SpawnCartKitSaved|Rinkinys „{0}“ išsaugotas.|Seti “{0}” imehifadhiwa.
SpawnCartKitLoaded|Rinkinys „{0}“ įkeltas.|Seti “{0}” imepakiwa.
SpawnCartKitImport|Importuoti seną rinkinį|Ingiza seti ya zamani
SpawnCartKitImported|Rinkinys „{0}“ importuotas.|Seti “{0}” imeingizwa.
SpawnCartNoSavedKits|Dar nėra išsaugotų rinkinių. Išsaugokite sąrašą arba importuokite ankstesnį rinkinį.|Hakuna seti zilizohifadhiwa bado. Hifadhi orodha au ingiza seti ya awali.
SpawnCartSaveStopped|Sąrašo išsaugoti nepavyko. Siuntimas sustabdytas. Išsaugotame sąraše gali likti jau išsiųstų daiktų; patikrinkite jį prieš naudodami dar kartą.|Orodha haikuweza kuhifadhiwa. Utumaji umesimamishwa. Orodha iliyohifadhiwa inaweza kuwa na vitu vilivyotumwa; ikague kabla ya kuitumia tena.'''
copy+='''
FirstTimeGuideTitle|Pradinė sąranka|Usanidi wa kwanza
GuideExtracted|Išskleidžiau ZIP pasirinkęs Extract All. Paleidau EXE iš išskleisto aplanko, o assets aplankas yra šalia jo.|Nilitoa faili za ZIP kwa kutumia Extract All. Ninaendesha EXE katika folda iliyotolewa, huku folda ya assets ikiwa kando yake.
GuideWindowed|SCUM veikia Windowed arba Borderless Window režimu. Mano veikėjas įkeltas.|SCUM iko katika hali ya Windowed au Borderless Window. Mhusika wangu amepakiwa.
GuideScumNumpad|SCUM funkcijai Copy Location priskirtas Num / (skaičių klaviatūros dalybos klavišas).|Katika SCUM, Copy Location ni Num / (kitufe cha kugawanya kwenye sehemu ya nambari).
GuideMiniNumpad|MiniMap jau naudoja Num /. Čia nereikia keisti ar užfiksuoti klavišo.|MiniMap tayari hutumia Num /. Sihitaji kubadilisha au kunakili kitufe hapa.
GuideUseNumpad|MiniMap naudoti Num /|Tumia Num / katika MiniMap
CopyReminderTitle|Patikrinkite koordinačių kopijavimo klavišą|Kagua kitufe cha kunakili viwianishi
CopyReminderBody|Numatytasis klavišas yra Num / (skaičių klaviatūros dalyba). SCUM funkcijai Copy location nustatykite tą patį klavišą. Jei naudojate kitą klavišą arba neturite skaičių klaviatūros, užfiksuokite jį MiniMap nustatymuose.|Kitufe cha awali ni Num / (kugawanya kwenye sehemu ya nambari). Weka Copy location katika SCUM kuwa kitufe hicho hicho. Ukitumia kitufe tofauti au huna sehemu ya nambari, kinakili katika Mipangilio ya MiniMap.
WizardKeybindsSkipNotice|Praleiskite šį žingsnį tik jei čia rodomi klavišai jau sutampa su SCUM. Nauja programa funkcijai Copy location naudoja Num / (skaičių klaviatūros dalybą) be modifikatoriaus. Jei neturite skaičių klaviatūros, čia užfiksuokite pasirinktą klavišą.|Ruka hatua hii tu ikiwa vifungo vinavyoonyeshwa tayari vinafanana na SCUM. Usakinishaji mpya hutumia Num / (kugawanya kwenye sehemu ya nambari) kwa Copy location, bila kitufe cha ziada. Ikiwa huna sehemu ya nambari, nakili kitufe ulichochagua hapa.
CopyBindingHelp|Numatytasis: Num / (skaičių klaviatūros dalyba), be modifikatoriaus. SCUM funkcijai Copy location priskirkite tą patį klavišą. Esami klavišų nustatymai išsaugomi. Jei neturite skaičių klaviatūros, nustatymuose užfiksuokite pasirinktą klavišą. Sekimas vienu klavišu palaiko 250 ms; Ctrl+C palaikomas kas 1 sekundę arba rečiau.|Chaguo la awali: Num / (kugawanya kwenye sehemu ya nambari), bila kitufe cha ziada. Weka Copy location katika SCUM kuwa kitufe hicho hicho. Vifungo vilivyopo huhifadhiwa. Ikiwa kibodi haina sehemu ya nambari, nakili kitufe ulichochagua katika Mipangilio. Ufuatiliaji wa kitufe kimoja unaruhusu ms 250; Ctrl+C bado inaruhusiwa kwa sekunde 1 au zaidi.
GuideSwitchApps|Vykdydami šiuos veiksmus spauskite Alt+Tab, kad pereitumėte į SCUM ir grįžtumėte. Jūsų varnelės išliks.|Tumia Alt+Tab kubadilisha kati ya SCUM na mwongozo huu unapofuata hatua hizi. Alama zako za ukaguzi zitabaki.
GuideMiniOther|Dabar MiniMap turiu nustatyti TĄ PATĮ KLAVIŠĄ. Paspaudžiu žemiau esantį mygtuką ir pasirinktą klavišą be Ctrl, Shift ar Alt.|Sasa lazima niweke KITUFЕ HICHO HICHO kwenye MiniMap. Ninatumia kitufe kilicho hapa chini na kubonyeza kitufe nilichochagua bila Ctrl, Shift au Alt.
PrivateGroupDelete|Ištrinti grupę|Futa kikundi
PrivateGroupDeleteConfirm|Ištrinti grupę „{0}“ visiems? Visi nariai bus atjungti, o grupės kodas nebeveiks.|Ungependa kufuta kikundi “{0}” kwa kila mtu? Wanachama wote watakatwa muunganisho na msimbo wake utaacha kufanya kazi.
DeleteWaypoint|Ištrinti savo kelio tašką|Futa kituo chako cha njia
DeleteWaypointNamed|Ištrinti kelio tašką „{0}“|Futa kituo cha njia “{0}”
ZeDeleteAll|Ištrinti viską|Futa vyote
ZeDeleteAllTitle|Ištrinti visas zonas|Futa maeneo yote
ZeDeleteSelected|Ištrinti pasirinktus|Futa vilivyochaguliwa
ZeDeleteLayer|Ištrinti sluoksnį|Futa tabaka
FieldMatchKeys|Suderinkite klavišus su SCUM|Linganisha vifungo vyako na SCUM
MapPlacementHelp|Automatinis režimas seka SCUM langą. Rankiniu režimu padėtis ir dydis išsaugomi ekrano pikseliais.|Hali ya kiotomatiki hufuata dirisha la SCUM. Hali ya kujiwekea huhifadhi nafasi na ukubwa kwa pikseli za skrini.
MemberColourHelp|Pasirinkite savo maršrutų ir žymeklių spalvą visose grupėse. Aktyvios grupės prisijungs iš naujo. Išėjimo iš grupės ir savininko atjungimo nuostatos išlieka.|Chagua rangi ya njia na vialamisho vyako katika vikundi vyote. Vikundi vinavyotumika vitaunganisha upya. Maamuzi ya kuondoka kwenye kikundi na kukatwa muunganisho na mwenyeji yanaendelea kuheshimiwa.
GroupForgetConfirm|Pamiršti {0} šiame kompiuteryje? Grupė liks prieinama kitiems. Jei esate savininkas, visam laikui prarasite jos valdymo teises. Prieš tęsdami išsaugokite pakvietimo kodo kopiją.|Ungependa kusahau {0} kwenye kompyuta hii? Kikundi kitaendelea kupatikana kwa wengine. Ikiwa wewe ni mmiliki, utapoteza vidhibiti vyake kabisa. Hifadhi nakala ya msimbo wa mwaliko kabla ya kuendelea.'''
for line in copy.splitlines():
    key,lt,sw=line.split('|')
    english=reference[key]
    # The file filter separator and extension must retain their exact syntax.
    if key=='SpawnCartKitFilter':lt+='|*.tsv';sw+='|*.tsv'
    review['lt'][english]=lt
    review['sw'][english]=sw.replace('\u0415','E')
path.write_text(json.dumps(review,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
guide_lt='''Atlikite šiuos veiksmus iš eilės. SCUM ir MiniMap turi būti nustatytas tas pats Copy Location klavišas.

1. Atsisiųskite ir išskleiskite
Atsisiųskite visą programos ZIP iš Discord → Downloads. Spustelėkite jį dešiniuoju pelės mygtuku → Išskleisti viską. Atidarykite išskleistą aplanką ir paleiskite SkynettMiniMap.exe. Šalia jo palikite assets aplanką. Nepaleiskite programos ZIP archyvo viduje. Reikia 64 bitų Windows ir .NET Framework 4.8.

2. Nustatykite SCUM rodymo režimą
Naudokite Borderless Window arba Windowed režimą. Prisijunkite prie žaidimo ir įkelkite veikėją.

3. Nustatykite SCUM COPY LOCATION
OPTIONS → MOUSE & KEYBOARD → CONTROLS → SUBCATEGORY: UI → COPY LOCATION
Pasirinkite Num / ir spustelėkite APPLY.
Tai skaičių klaviatūros DALYBOS klavišas, o ne / šalia raidžių.
Ekrano nuotraukos: Discord → FAQ → Copy Location.

4. Nustatykite tą patį MiniMap klavišą
Sąrankos žaidimo klavišų puslapyje Copy Location nustatykite į NumPad / ir vieną klavišą be modifikatoriaus. Nenaudokite Ctrl, Shift ar Alt. Taip pat patikrinkite, ar žemėlapio ir pokalbio klavišai sutampa su SCUM.

❗ NETURITE SKAIČIŲ KLAVIATŪROS? PAKEISKITE KLAVIŠĄ IR SCUM, IR MINIMAP.
SCUM pasirinkite nenaudojamą vieną klavišą ir spustelėkite Apply. Tada MiniMap užfiksuokite tą patį fizinį klavišą. Pakeisti tik vienoje programoje nepakanka.

5. Užbaikite sąranką
Kiekviename puslapyje vieną sekundę laikykite mygtuką Toliau, tada atleiskite. Nustatykite žemėlapio dydį, padėtį ir išvaizdą bei užbaikite paskutinį puslapį. Žaidžiant vienam grupės nebūtinos.

6. Patikrinkite sekimą
Uždarykite MiniMap nustatymus. Grįžkite prie veikėjo ir uždarykite pokalbį bei inventorių. Paspauskite Copy Location klavišą: SCUM turi nukopijuoti koordinates. Pajudinkite veikėją ir patikrinkite, ar juda žymeklis.

Reikia pagalbos? Pirmiausia patikrinkite ABIEJŲ programų klavišus. Pagal numatytuosius nustatymus Home atidaro nustatymus, o ? didžiajame žemėlapyje iš naujo atidaro sąranką. Jei sekimas vis tiek neveikia, Discord → Make a ticket atidarykite pagalbos užklausą. Pridėkite programos versiją ir abiejų programų klavišų nustatymų ekrano nuotraukas.'''
guide_sw='''Fuata hatua hizi kwa mpangilio. Kitufe cha Copy Location lazima kifanane katika SCUM na MiniMap.

1. Pakua na toa faili
Pakua ZIP kamili ya programu kutoka Discord → Downloads. Bofya ZIP kwa kitufe cha kulia → Extract All. Fungua folda iliyotolewa na uanzishe SkynettMiniMap.exe. Weka folda ya assets karibu nayo. Usiendeshe programu ndani ya ZIP. Inahitaji Windows ya biti 64 na .NET Framework 4.8.

2. Weka hali ya kuonyesha SCUM
Tumia Borderless Window au Windowed. Ingia kwenye mchezo na upakie mhusika wako.

3. Weka COPY LOCATION katika SCUM
OPTIONS → MOUSE & KEYBOARD → CONTROLS → SUBCATEGORY: UI → COPY LOCATION
Chagua Num / na ubofye APPLY.
Hiki ni kitufe cha KUGAWANYA kwenye sehemu ya nambari ya kibodi, si / karibu na herufi.
Picha za maelekezo: Discord → FAQ → Copy Location.

4. Weka kitufe hicho hicho katika MiniMap
Kwenye ukurasa wa vifungo vya mchezo wa usanidi, weka Copy Location kuwa NumPad / na kitufe kimoja bila kitufe cha ziada. Usitumie Ctrl, Shift au Alt. Hakikisha pia vifungo vya ramani na gumzo vinafanana na SCUM.

❗ HUNA SEHEMU YA NAMBARI KWENYE KIBODI? BADILISHA KITUFЕ KATIKA SCUM NA MINIMAP.
Chagua kitufe kimoja kisichotumika katika SCUM, bofya Apply, kisha unakili kitufe hicho hicho cha kibodi kwenye MiniMap. Kubadilisha katika programu moja pekee hakutafanya kazi.

5. Maliza usanidi
Kwenye kila ukurasa, shikilia kitufe cha Endelea kwa sekunde moja, kisha uachilie. Weka ukubwa, nafasi na mwonekano wa ramani, kisha umalize ukurasa wa mwisho. Vikundi ni vya hiari unapocheza peke yako.

6. Kagua ufuatiliaji
Funga Mipangilio ya MiniMap. Rudi kwa mhusika wako na ufunge gumzo na orodha ya mali. Bonyeza kitufe cha Copy Location: SCUM lazima inakili viwianishi. Sogeza mhusika wako na uhakikishe kuwa kialamisho chako kinasogea.

Unahitaji msaada? Kwanza kagua vifungo katika programu ZOTE MBILI. Kwa kawaida Home hufungua Mipangilio; ? kwenye ramani kubwa hufungua tena usanidi. Ikiwa ufuatiliaji bado haufanyi kazi, fungua tiketi katika Discord → Make a ticket. Jumuisha toleo la programu na picha za mipangilio ya vifungo katika programu zote mbili.'''
review['lt'][reference['FirstTimeGuideBody']]=guide_lt
review['sw'][reference['FirstTimeGuideBody']]=guide_sw.replace('\u0415','E')
review['categories']={code:dict(line.split('\t') for line in (ROOT/'resources'/('spawn-category-names-'+code+'.tsv')).read_text(encoding='utf8').splitlines() if line and not line.startswith('#')) for code in ['lt','sw']}
category_copy='''clothing|Drabužiai ir šarvai|Mavazi na vifaa vya kinga
survival|Maistas, sveikata ir išgyvenimas|Chakula, afya na kujinusuru
crafting|Įranga ir gamyba|Vifaa na utengenezaji
armor|Šarvai|Vifaa vya kinga
buildable|Bazės plėtros daiktai|Vitu vya kupanua kambi
magazine|Dėtuvės|Makasha ya risasi
player_modification|Veikėjo modifikacijos|Marekebisho ya mhusika
throwable|Metamieji ginklai ir spąstai|Vitu vya kutupa na mitego
general|Bendra|Jumla
Arrow|Strėlės|Mishale
Crossbow Bolt|Arbaleto strėlės|Mishale ya upinde wa msalaba
Flare|Signalinės raketos|Fataki za ishara
12 Gauge|12 kalibras|Kaliba 12
base_upgrades|Bazės patobulinimai|Maboresho ya kambi
body|Kėbulai|Miili ya magari
body_armor|Apsauginės liemenės|Mavazi ya kinga ya mwili
bumper|Buferiai|Vizuia mgongano
clip|Šovinių apkabos|Vishikio vya risasi
cosmetic|Išvaizdos daiktai|Vitu vya mwonekano
earmuffs|Apsauginės ausinės|Vikinga masikio
fender|Automobilių sparnai|Vikinga matope
fire_starter|Ugnies įžiebimo priemonės|Vifaa vya kuwasha moto
flash_hider|Liepsnos slopintuvai|Vificha mwanga wa mlipuko
ghillie|Kamufliažiniai apvalkalai|Vifuniko vya kuficha
ghillie_jacket|Kamufliažinės striukės|Makoti ya kuficha
hazmat_suit|Apsauginiai kostiumai|Mavazi ya kujikinga na kemikali
holster|Ginklų dėklai|Vibebeo vya bastola
leggings|Antblauzdžiai|Suruali za kubana
light|Apšvietimo priemonės|Taa
lockpick|Visrakčiai|Vifaa vya kufungua kufuli
quiver|Strėlinės|Vifuko vya mishale
rail|Tvirtinimo bėgeliai|Reli za kufungia
ranged_parts|Šaunamųjų ginklų dalys|Sehemu za silaha za mbali
revolver|Revolveriai|Bastola za revolva
scope|Optiniai taikikliai|Darubini za kulenga
shotgun|Lygiavamzdžiai šautuvai|Bunduki zinazotawanya risasi
shotgun_parts|Lygiavamzdžių šautuvų dalys|Sehemu za bunduki zinazotawanya risasi
sight|Taikikliai|Vilengezi
smg|Pistoletai kulkosvaidžiai|Bunduki ndogo za risasi mfululizo
smg_parts|Pistoletų kulkosvaidžių dalys|Sehemu za bunduki ndogo za risasi mfululizo
sniper|Snaiperiniai šautuvai|Bunduki za kudungua
suppressor|Duslintuvai|Vipunguza sauti
Uses|Likę panaudojimai|Matumizi yaliyobaki
StackCount|Kiekis krūvoje|Idadi katika fungu
KeyCardSector|Raktų kortelės sektorius|Sekta ya kadi ya ufunguo'''
for line in category_copy.splitlines():
    key,lt,sw=line.split('|');review['categories']['lt'][key]=lt;review['categories']['sw'][key]=sw
for english,lt,sw in [
    ('Apply','Taikyti','Tumia'),
    ('Continental Meadow','Žemyninė pieva','Uwanda wa nyasi wa bara'),
    ('Magazines','Dėtuvės','Makasha ya risasi'),
    ('General','Bendra','Jumla'),
    ('Scopes','Optiniai taikikliai','Darubini za kulenga'),
    ('Sights','Taikikliai','Vilengezi'),
    ('Quiver','Strėlinė','Kifuko cha mishale'),
    ('Suppressors','Duslintuvai','Vipunguza sauti'),
    ('Crossbow Bolt','Arbaleto strėlė','Mshale wa upinde wa msalaba'),
    ('Animal Skin Quiver','Gyvūnų odos strėlinė','Kifuko cha mishale cha ngozi ya mnyama'),
    ('Improvised Quiver','Savadarbė strėlinė','Kifuko cha mishale kilichotengenezwa kwa mikono'),
    ('Military Quiver','Karinė strėlinė','Kifuko cha mishale cha kijeshi'),
    ('Blackhawk Crossbow','Arbaletas Blackhawk','Upinde wa msalaba wa Blackhawk'),
    ('Improvised Crossbow','Savadarbis arbaletas','Upinde wa msalaba uliotengenezwa kwa mikono'),
    ('Carbon Crossbow Bolt','Anglies pluošto arbaleto strėlė','Mshale wa upinde wa msalaba wa nyuzi za kaboni'),
    ('Dildo Wooden Crossbow Bolt','Medinė arbaleto strėlė su dildu','Mshale wa mbao wa upinde wa msalaba wenye dildo'),
    ('Explosive Wooden Crossbow Bolt','Sprogstamoji medinė arbaleto strėlė','Mshale wa mbao wa upinde wa msalaba wenye mlipuko'),
    ('Metal Crossbow Bolt','Metalinė arbaleto strėlė','Mshale wa chuma wa upinde wa msalaba'),
    ('Metal Tip Crossbow Bolt','Arbaleto strėlė metaliniu antgaliu','Mshale wa upinde wa msalaba wenye ncha ya chuma'),
    ('Stone Tip Crossbow Bolt','Arbaleto strėlė akmeniniu antgaliu','Mshale wa upinde wa msalaba wenye ncha ya jiwe'),
    ('Wooden Crossbow Bolt','Medinė arbaleto strėlė','Mshale wa mbao wa upinde wa msalaba'),
    ('Police Station','Policijos nuovada','Kituo cha polisi'),
    ('Outpost Airfield','Prekybos posto aerodromas','Uwanja wa ndege wa kituo cha biashara'),
    ('Melee weapons','Artimosios kovos ginklai','Silaha za karibu'),
    ('Spawn location','Kūrimo vieta','Mahali pa kuunda vitu'),
    ('Bunker','Bunkeris','Ngome ya chini ya ardhi'),
    ('WW2 Bunker','Antrojo pasaulinio karo bunkeris','Ngome ya chini ya ardhi ya Vita vya Pili vya Dunia'),
    ('Bunker Hatch','Bunkerio liukas','Mlango wa ngome ya chini ya ardhi'),
    ('Boot Camp','Mokymo stovykla','Kambi ya mafunzo'),
    ('Junkyard','Metalo laužynas','Dampo la vyuma chakavu'),
    ('Sanitarium','Sanatorija','Hospitali ya wagonjwa wa muda mrefu'),
    ('Monolith','Monolitas','Jiwe kubwa la monoliti'),
    ('Ramsons Hill','Meškinio česnako kalva','Kilima cha vitunguu pori'),
    ('Paddle spawns','Irklų atsiradimo vietos','Maeneo ya kupata makasia'),
    ('Parsnip','Pastarnokas','Mzizi wa parsnip'),
    ('Chub','Šapalas','Samaki aina ya Chub'),
    ('Ruffe','Pūgžlys','Samaki aina ya Ruffe'),
    ('Dentex','Dantytasis karšis','Samaki aina ya Dentex'),
    ('Orata','Auksaspalvis sparas','Samaki aina ya Orata'),
    ('Amur','Baltasis amūras','Samaki aina ya Amur'),
    ('Bass','Ešerys','Samaki aina ya Bass'),
    ('Carp','Karpis','Samaki aina ya Carp'),
    ('Pike','Lydeka','Samaki aina ya Pike'),
    ('Tuna','Tunas','Jodari'),
    ('Pub','Aludė','Baa'),
    ('Killbox','Mirtinų spąstų bunkeris','Chumba cha mitego hatari'),
    ('POI','Svarbi vieta','Sehemu muhimu'),
    ('Krsko POIs','Krsko svarbios vietos','Sehemu muhimu za Krsko'),
    ('Novigrad POIs','Novigrad svarbios vietos','Sehemu muhimu za Novigrad'),
    ('Samobor POIs','Samobor svarbios vietos','Sehemu muhimu za Samobor'),
    ('Admin','Administratorius','Msimamizi')]:
    review['lt'][english]=lt;review['sw'][english]=sw
path.write_text(json.dumps(review,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
