using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ScumMiniMap {
    public static partial class Localization {
        static readonly Dictionary<string,Lazy<Dictionary<string,string>>> ItemNames = CreateSpawnTables("spawn-item-names");
        static readonly Dictionary<string,Lazy<Dictionary<string,string>>> SpawnCategoryNames = CreateSpawnTables("spawn-category-names");
        static Dictionary<string,Lazy<Dictionary<string,string>>> CreateSpawnTables(string prefix) {
            var result = new Dictionary<string,Lazy<Dictionary<string,string>>>(StringComparer.Ordinal);
            foreach (string language in new[]{"es-AR","fr","de","nl","ru","zh-CN","tr","ar","pt-BR","pt-PT","ko","pl","th","zh-TW","ja","cs","it","uk"}) {
                string resource = prefix + "-" + language + ".tsv";
                result.Add(language,new Lazy<Dictionary<string,string>>(() => LoadSpawnNames(resource)));
            }
            return result;
        }
        static Dictionary<string,string> LoadSpawnNames(string resource) {
            var names = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
                if (stream == null) throw new InvalidOperationException("The localized spawn catalogue is missing: " + resource);
                using (var reader = new StreamReader(stream, new UTF8Encoding(false,true))) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.Length == 0 || line.StartsWith("#",StringComparison.Ordinal)) continue;
                        string[] fields = line.Split('\t');
                        if (fields.Length != 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]) || fields[1].Length > 256)
                            throw new FormatException("Invalid localized spawn label: " + resource);
                        foreach(char character in fields[1])
                            if(char.IsControl(character))throw new FormatException("Control character in localized spawn label: " + resource);
                        names.Add(fields[0],fields[1]);
                    }
                }
            }
            return names;
        }
        public static string GetSpawnItemName(string id, string englishName) {
            string translated;
            Lazy<Dictionary<string,string>> names;
            return id != null && ItemNames.TryGetValue(CurrentCode,out names) && names.Value.TryGetValue(id,out translated) ? translated : englishName;
        }
        public static string GetSpawnCategoryName(string id, string englishName) {
            string translated;
            if (id == null) return englishName;
            if (Current == AppLanguage.Polish) return PolishSpawnCategories.TryGetValue(id,out translated) ? translated : englishName;
            Lazy<Dictionary<string,string>> names;
            return SpawnCategoryNames.TryGetValue(CurrentCode,out names) && names.Value.TryGetValue(id,out translated) ? translated : englishName;
        }
        public static string GetSpawnModifierLabel(string property) {
            string translated = GetSpawnCategoryName(property,property);
            if(translated!=property)return translated+" ("+DisplayIdentifier(property)+")";
            return property=="Weight"?"Weight (kg)":property;
        }
        // Presentation-only LTR marks supported by classic Windows controls.
        // Never use this value in search identity,
        // copied commands, persistence or RPC arguments.
        public static string DisplayIdentifier(string identifier) {
            return Current==AppLanguage.Arabic && !string.IsNullOrEmpty(identifier) ? "\u200e"+identifier+"\u200e" : identifier;
        }
        public static string DisplayLocalizedText(string text) {
            if(Current!=AppLanguage.Arabic || string.IsNullOrEmpty(text))return text;
            return System.Text.RegularExpressions.Regex.Replace(text,@"[A-Za-z][A-Za-z0-9._/#:\-]*|\.?\d[A-Za-z0-9._/#:\-]*",
                match=>DisplayIdentifier(match.Value));
        }
        static readonly Dictionary<string,string> PolishSpawnCategories = ReadPolishNames(@"weapons|Broń
clothing|Odzież
survival|Żywność, zdrowie i przetrwanie
crafting|Wyposażenie i wytwarzanie
vehicles|Części pojazdów
other|Inne przedmioty
weapon_ranged|Broń dystansowa
weapon_melee|Broń do walki wręcz
melee_weapon|Broń do walki wręcz
ammunition|Amunicja
armor|Pancerze
attachment|Dodatki do broni
book|Książki
buildable|Rozbudowa bazy
consumable|Żywność, napoje i leki
equipment|Wyposażenie
farming|Rolnictwo
fishing|Wędkarstwo
fishing_attachment|Akcesoria wędkarskie
fortification|Umocnienia
hunting|Łowiectwo
magazine|Magazynki
material|Materiały
player_modification|Modyfikacje postaci
seasonal_event|Przedmioty sezonowe
throwable|Broń miotana i pułapki
vehicle_part|Części pojazdów
weapon_part|Części broni
general|Ogólne
Arrow|Strzały
Crossbow Bolt|Bełty do kuszy
Flare|Flary
12 Gauge|Kaliber 12
accessories|Akcesoria
adhesive|Kleje
alcoholic|Napoje alkoholowe
animal_part|Części zwierząt
assault_rifle|Karabiny szturmowe
axe|Siekiery
backpack|Plecaki
base_upgrades|Ulepszenia bazy
bayonet|Bagnety
beverage|Napoje
blunt|Broń obuchowa
body|Karoserie
body_armor|Kamizelki ochronne
bow|Łuki
bumper|Zderzaki
clip|Łódki nabojowe
cookbook|Książki kucharskie
cosmetic|Kosmetyczne
craft_station|Stanowiska wytwarzania
crafting_material|Materiały do wytwarzania
door|Drzwi
earmuffs|Nauszniki
engine|Silniki
enhancement|Ulepszenia
event|Wydarzenia
eyewear|Okulary
fender|Błotniki
fire_starter|Rozpałki
fishing_catch|Złowione ryby
flash_hider|Tłumiki płomienia
flashlight|Latarki
food|Żywność
footwear|Obuwie
fuel|Paliwo
ghillie|Osłony maskujące
ghillie_jacket|Kurtki maskujące
gloves|Rękawice
hazmat_suit|Kombinezony ochronne
headwear|Nakrycia głowy
heavy|Ciężka broń
holster|Kabury
jacket|Kurtki
knife|Noże
leggings|Legginsy
light|Lekkie
lmg|Lekkie karabiny maszynowe
lockpick|Wytrychy
mask|Maski
medical|Leki
miscellaneous|Różne
organic|Organiczne
paint|Farby
pants|Spodnie
pistol|Pistolety
pistol_parts|Części pistoletów
quiver|Kołczany
rail|Szyny
raincoat|Płaszcze przeciwdeszczowe
ranged_parts|Części broni dystansowej
repair_kit|Zestawy naprawcze
repair_material|Materiały naprawcze
results|Wyniki
revolver|Rewolwery
rifle_parts|Części karabinów
scarf|Szaliki
scope|Lunety
seasonal_craft|Wytwarzanie sezonowe
seat|Fotele
security|Zabezpieczenia
seed|Nasiona
shirt|Koszule
shotgun|Strzelby
shotgun_parts|Części strzelb
sight|Celowniki
single_shot|Karabiny jednostrzałowe
skirt|Spódnice
smg|Pistolety maszynowe
smg_parts|Części pistoletów maszynowych
sniper|Karabiny wyborowe
socks|Skarpety
soft_drink|Napoje bezalkoholowe
spear|Włócznie
stabilizer|Stabilizatory
storage|Przechowywanie
suppressor|Tłumiki
sweater|Swetry
sword|Miecze
tool|Narzędzia
undershirt|Podkoszulki
underwear|Bielizna
unknown_parts|Inne części
utility|Użytkowe
vehicle_attachment|Akcesoria pojazdów
water|Woda
wheel|Koła
Health|Stan
Uses|Pozostałe użycia
Dirtiness|Zabrudzenie
Location|Pozycja
KeyCardSector|Sektor karty
StackCount|Liczba w stosie
Radiation|Promieniowanie
Weight|Masa w kg");
    }
}
