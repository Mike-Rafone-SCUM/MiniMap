using System;
using System.Collections.Generic;

namespace ScumMiniMap {
    public static partial class Localization {
        public static readonly Dictionary<string,string> StringsFi=ReadFinnishTable(FinnishUi);
        private static readonly Dictionary<string,string> SectionNamesFi=ReadFinnishTable(FinnishSections);
        private static readonly Dictionary<string,string> CategoryNamesFi=ReadFinnishTable(FinnishCategories);
        private static readonly Dictionary<string,string> MapObjectsFi=ReadFinnishTable(FinnishMapObjects);
        static Dictionary<string,string> ReadFinnishTable(string text) {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string row in text.Split('\n')) {
                string line=row.TrimEnd('\r');if(line.Length==0)continue;int separator=line.IndexOf('|');
                if(separator<1||separator==line.Length-1)throw new FormatException("Invalid Finnish catalogue row");
                result.Add(line.Substring(0,separator),line.Substring(separator+1).Replace(@"\n","\n"));
            }
            return result;
        }
        const string FinnishUi=@"ScumAutoLaunchLabel|Käynnistä MiniMap SCUMin mukana ja sulje se pelin päättyessä
ScumAutoLaunchHelp|Taustalla toimiva valvonta käynnistyy Windowsiin kirjautuessa. MiniMap tallentaa ja sulkeutuu 10 sekuntia SCUMin sulkemisen jälkeen. Jos suljet MiniMapin itse, se käynnistyy vasta seuraavalla pelikerralla. Poista tämä käytöstä ennen sovelluskansion siirtämistä tai poistamista.
AdminMapTeleportHere|Teleporttaa tähän
AdminMapTeleportHelp|Lähetä {0}. Kartan korkeustietoa ei ole saatavilla, joten Z on 0.
RouteModeQuickest|Nopein
RouteModeShortest|Lyhin
RouteModePavedOnly|Vain päällystetyt tiet
RouteModeHelp|Nopein reitti käyttää nopeusarvioita 60 km/h päällystetyillä teillä, 30 km/h sorateillä ja 5 km/h siirtymillä. Lyhin minimoi matkan. Vain päällystetyt tiet sulkee soratiet pois; kuivan maan siirtymät voivat olla enintään 1 km. Rautateitä ei käytetä.
SetupMoreOptions|Lisäasetukset
SetupAppearanceHelp|Valitse kartan muoto, koko ja näkyvyys. Tarkkaile esikatselua. Voit myös säilyttää nykyiset asetukset.
SetupMapHelp|Valitse kartalla näytettävät kohteet. Lisää väri- ja nimikeasetuksia on kohdassa Lisäasetukset.
SetupTrackingHelp|Valitse, lähentyykö ja loitontuuko kartta automaattisesti liikkuessasi. Voit myös säilyttää nykyiset asetukset.
SetupSharingHelp|Pelaatko yksin? Valitse Valmis. Jakaminen kavereiden kanssa on vapaaehtoista. Muodosta yhteys kohdasta Yksityinen sijainninjako.
GuideSwitchApps|Vaihda SCUMin ja tämän oppaan välillä painamalla Alt+Tab. Valintamerkit säilyvät.
GuideChecklistIntro|Merkitse kukin kohta, kun olet ymmärtänyt sen. Etene ylhäältä alas.
GuideHaveNumpad|Näppäimistössäni on numeronäppäimistö
GuideNoNumpad|Näppäimistössäni ei ole numeronäppäimistöä tai käytän muuta näppäintä
GuideExtracted|Purin ZIP-tiedoston toiminnolla Pura kaikki. Käynnistin EXE-tiedoston puretusta kansiosta, ja assets-kansio on sen vieressä.
GuideWindowed|SCUM on ikkunatilassa tai reunattomassa ikkunatilassa. Pelihahmoni on ladattu.
GuideScumNumpad|SCUMin Copy Location -toiminnon näppäin on Num / eli numeronäppäimistön jakonäppäin.
GuideMiniNumpad|MiniMap käyttää jo näppäintä Num /. Minun ei tarvitse muuttaa tai määrittää näppäintä tässä.
GuideScumOther|Valitsin SCUMin Copy Location -toiminnolle vapaan näppäimen ja painoin Apply.
GuideMiniOther|Nyt minun on määritettävä SAMA NÄPPÄIN MiniMapiin. Käytän alla olevaa painiketta ja painan valitsemaani näppäintä ilman Ctrl-, Shift- tai Alt-näppäintä.
GuideCloseSettings|Tämän oppaan sulkemisen jälkeen palaan SCUM-hahmooni ja suljen keskustelun sekä tavaraluettelon.
GuideTrackingTest|Painan sitten Copy Location -näppäintä ja tarkistan, että koordinaatit kopioituvat. Liikutan hahmoani ja varmistan, että myös karttamerkki liikkuu.
GuideDontShow|Älä näytä tätä opasta uudelleen tässä versiossa. Opas avautuu seuraavan päivityksen jälkeen. Voin avata sen myös asetuksista.
GuideUseNumpad|Käytä MiniMapissa näppäintä Num /
FirstTimeGuideTitle|Ensimmäisen käyttökerran opas
FirstTimeGuideBody|Noudata näitä ohjeita järjestyksessä. Copy Location -näppäimen on oltava sama SCUMissa ja MiniMapissa.\n\n1. Lataa ja pura\nLataa sovelluksen koko ZIP-paketti Discordin Downloads-kanavalta. Napsauta pakettia hiiren kakkospainikkeella ja valitse Pura kaikki. Avaa purettu kansio ja käynnistä SkynettMiniMap.exe. Säilytä assets-kansio sen vieressä. Älä käynnistä sovellusta ZIP-paketin sisältä. Tarvitset 64-bittisen Windowsin ja .NET Framework 4.8:n.\n\n2. Valitse SCUMin näyttötila\nKäytä reunatonta ikkunatilaa (Borderless Window) tai ikkunatilaa (Windowed). Liity peliin ja lataa hahmosi.\n\n3. Määritä SCUMin COPY LOCATION\nOPTIONS → MOUSE & KEYBOARD → CONTROLS → SUBCATEGORY: UI → COPY LOCATION\nValitse Num / ja paina APPLY.\nTämä on NUMERONÄPPÄIMISTÖN JAKONÄPPÄIN, ei kirjainten vieressä oleva /.\nKuvalliset ohjeet: Discord → FAQ → Copy Location.\n\n4. Määritä sama näppäin MiniMapiin\nValitse käyttöönoton pelinäppäinasetuksissa Copy Location -näppäimeksi NumPad / ja yksi näppäin ilman muunnosnäppäintä. Älä käytä Ctrl-, Shift- tai Alt-näppäintä. Tarkista myös, että kartan ja keskustelun näppäimet vastaavat SCUMin asetuksia.\n\n❗ EI NUMERONÄPPÄIMISTÖÄ? VAIHDA NÄPPÄIN SEKÄ SCUMISSA ETTÄ MINIMAPISSA.\nValitse SCUMissa vapaa yksittäinen näppäin ja paina Apply. Määritä sitten MiniMapiin sama fyysinen näppäin. Vain toisen sovelluksen asetuksen muuttaminen ei riitä.\n\n5. Viimeistele käyttöönotto\nNapsauta kunkin sivun Seuraava-painiketta. Määritä kartan koko, sijainti ja ulkoasu sekä viimeistele viimeinen sivu. Ryhmiä ei tarvita yksinpeliin.\n\n6. Tarkista seuranta\nSulje MiniMapin asetukset. Palaa hahmoosi ja sulje keskustelu sekä tavaraluettelo. Paina Copy Location -näppäintä: SCUMin on kopioitava koordinaatit. Liikuta hahmoasi ja varmista, että karttamerkkisi liikkuu.\n\nTarvitsetko apua? Tarkista ensin MOLEMPIEN sovellusten näppäinasetukset. Home avaa oletusarvoisesti asetukset, ja suuren kartan ? avaa käyttöönoton uudelleen. Jos seuranta ei vieläkään toimi, avaa tukipyyntö Discordin Make a ticket -kohdasta. Liitä mukaan sovelluksen versio ja kuvakaappaukset molempien sovellusten näppäinasetuksista.
NavigationNext|Seuraava pysähdys
NavigationRemaining|Reittiä jäljellä
NavigationEta|Arvioitu saapumisaika
MapGpsDestination|Navigoi tähän (GPS)
MapSaveLocation|Tallenna sijainti
MapGpsSavedHelp|GPS-kohde on väliaikainen. Tallennetut sijainnit säilyvät, kunnes poistat ne. Reittipisteet muodostavat järjestetyn matkan.
PrivateGroupDelete|Poista ryhmä
PrivateGroupDeleteConfirm|Poistetaanko ryhmä ”{0}” kaikilta? Kaikkien jäsenten yhteys katkeaa ja ryhmäkoodi lakkaa toimimasta.
PrivateGroupDeleted|Ryhmä poistettu.
SoloRoute|Oma reitti
OwnRoute|Sinun reittisi
RouteList|Reitit
RouteAutoHelp|Reitti lasketaan automaattisesti reittipisteiden muuttuessa. Oranssi tarkoittaa omaa reittiä ja turkoosi jaettua reittiä. Katkoviivalla merkityt osuudet ovat suoraa opastusta ilman tiereittiä. Myös oma reittisi näkyy yhdistetyille ryhmille. Toisen jaetun reitin seuraaminen ei jaa sitä eteenpäin.
PrivateGroupNameLabel|Ryhmän nimi (vapaaehtoinen luotaessa)
PrivateGroupRename|Tallenna ryhmän nimi
SquadTools|Ryhmän työkalut
PingDanger|Vaara
PingLoot|Saalista
PingPickup|Tule hakemaan
SquadUpdated|Viimeksi päivitetty
SquadOffline|Yhteys katkaistu
SquadFollow|Seuraa ryhmäläistä
SquadStopNav|Lopeta navigointi
SquadRoute|Jaettu reitti
SquadAddStop|Lisää reittipiste
SquadUndoStop|Poista viimeinen reittipiste
SquadStartRoute|Aloita reitti
SquadPresets|Kartan esiasetukset
SquadSavePreset|Tallenna nykyiset kartta-asetukset
SquadLooting|Saalistaminen
SquadDriving|Ajaminen
SquadExploring|Tutkiminen
SquadDelete|Poista
SquadPingHelp|Merkit vanhenevat 60 sekunnissa. Määritä niiden pikanäppäimet asetuksissa. Jakaminen toimii vain yhteyden ollessa muodostettuna.
SquadRouteHelp|Lisää reittipisteitä napsauttamalla karttaa hiiren kakkospainikkeella tai lisää nykyinen kohde alta. Yhdistetyt ryhmät näkevät reittisi. Seuraava piste valitaan 40 metrin etäisyydellä.
PrivateGroupCreate|Luo ryhmä ja kopioi koodi
PrivateGroupConnect|Yhdistä
PrivateGroupLeave|Poistu ryhmästä
PrivateGroupCopy|Kopioi koodi
PrivateGroupCode|Ryhmäkoodi
PrivateGroupRotate|Uusi koodi
PrivateGroupLabel|Ryhmä
PrivateGroupOwner|Omistaja
PrivateGroupSurvivor|Selviytyjä
WizardHoldNext|Jatka pitämällä painettuna 1 sekunti
PrivateGroupHelp|Luo ryhmä ja jaa sen koodi yksityisesti tai liitä saamasi koodi ja yhdistä. Koodi on voimassa, kunnes omistaja uusii sen. Voit liittyä useaan ryhmään. Tallennetut koodit suojataan tällä Windows-tilillä, ja jakaminen on aluksi pois käytöstä. Koodin uusiminen katkaisee ryhmän yhteydet ja mitätöi vanhan koodin. Ryhmän luojan on säilytettävä tallennettu profiilinsa omistajuuden säilyttämiseksi.
PrivatePositionRelay|Yksityinen ryhmä välityspalvelimen kautta
PrivatePositionRelayHelp|Porttiohjausta ei tarvita. Jaa ryhmäavain yksityisesti kavereillesi. Välityspalvelin ei voi lukea sijaintejanne. Maksuttoman välityspalvelimen kapasiteetti on rajallinen.
SetupSharingVisibility|Suuren kartan sivupalkissa voit valita kunkin pelaajan reaaliaikaisen sijainnin, GPS-kohteen, tallennettujen sijaintien ja kuolinmerkkien näkyvyyden erikseen. Nämä valinnat vaikuttavat vain omaan näkymääsi; jakaminen jatkuu.
SetupDefaultColours|Palauta oletusvärit
PlayerWaiting|Odotetaan pelaajia
PlayerNoLocation|Ei koordinaatteja
PlayerDeaths|Kuolinmerkit
PlayerSaved|Tallennetut sijainnit
PlayerGps|GPS-kohde
PlayerLiveLocation|Reaaliaikainen sijainti
PlayerConnected|Yhdistetty
SharedPlayers|Pelaajat
PrivatePositionGpsHelp|Aktiivinen GPS-kohteesi jaetaan myös tallentamatta sitä. Kohteen siirtäminen tai tyhjentäminen päivittyy ryhmälle.
PoiClustering|Ryhmittele lähekkäiset karttakuvakkeet
PrivatePositionMarkersHelp|Jakaminen lähettää ryhmälle myös enintään 128 tallennettua sijainti- ja kuolinmerkkiäsi. Kavereiden merkeissä näkyy omistaja, ja voit navigoida niihin. Ne ovat tässä väliaikaisia eikä niitä voi muokata.
PrivatePositionRouterManual|Automaattinen porttiohjaus ei ole käytettävissä tai se on vanhentunut. Ohjaa tämä TCP-portti käsin tähän tietokoneeseen. IP-osoitteen tunnistus ja palomuuriasetus eivät vielä osoita, että yhteys toimii internetistä.
PrivatePositionFirewallFailed|Windowsin palomuurisääntöä ei voitu lisätä tai varmistaa. Salli sovellus ja TCP-portti käsin. Hallittu palomuurikäytäntö voi estää muutokset.
PrivatePositionFirewallReady|Windowsin palomuurisääntö on varmistettu tälle sovellukselle ja TCP-portille.
PrivatePositionLocalAddresses|Lähiverkko-, VPN- ja virtuaalisovittimet
PrivatePositionPublicIpFailed|Julkista IP-osoitetta ei voitu tunnistaa. Ota reitittimen NAT-PMP tai UPnP käyttöön ja yritä uudelleen tai syötä julkinen WAN-IPv4-osoite käsin. CGNAT-yhteys vaatii julkisen IP-osoitteen internetoperaattorilta.
PrivatePositionPublicIpHelp|Suora vertaisyhteys ilman käyttäjätilejä tai ulkopuolisia osoitehakupalveluja. Julkinen IPv4-osoite tunnistetaan omasta reitittimestä NAT-PMP:n tai UPnP:n avulla. Isännöinti pyytää tilapäistä NAT-PMP-porttiohjausta ja Windowsin palomuurisääntöä sovellukselle ja TCP-portille.
PrivatePositionDetectPublicIp|Tunnista julkinen IP
PrivatePositionShareAddress|Jaettava osoite (julkinen IP tai VPN)
PrivatePositionConnected|Yhdistetty / odottaa yhteyksiä
PrivatePositionConnecting|Yhdistetään…
PrivatePositionPort|TCP-portti
PrivatePositionAddress|Isännän osoite / IP
PrivatePositionJoin|Liity ryhmään
PrivatePositionHost|Isännöi ryhmää
RotateMinimap|Kierrä karttaa kääntyessäni
PrivatePositions|Yksityinen sijainninjako
PrivatePositionName|Nimesi
PrivatePositionFolder|Valitse yksityinen jaettu kansio
PrivatePositionKey|Ryhmäavain
PrivatePositionNewKey|Luo ja kopioi avain
PrivatePositionInvalid|Yhteys epäonnistui. Tarkista osoite, portti, nimi ja ryhmäavain sekä salli TCP-yhteys palomuurissa.
PrivatePositionHelp|Isäntä: anna kavereillesi IP-osoitteesi, portti ja ryhmäavain. Liittyjä: syötä nämä tiedot. Yhteys vaatii lähiverkon, VPN:n tai TCP-porttiohjauksen. Jakaminen on voimassa tämän käyttökerran ajan, ja sijainnit vanhenevat 30 sekunnissa.
ZoneAssociationInfo|MiniMap on käytettävissä TSV-tiedostojen Avaa sovelluksessa -valikossa. Napsauta alueiden TSV-tiedostoa hiiren kakkospainikkeella, valitse Avaa sovelluksessa → SCUM MiniMap ja valitse Aina, jos haluat avata tiedostot kaksoisnapsauttamalla. Muut TSV-tiedostot voivat käyttää muuta sovellusta.
EnableZoneTsv|Ota alueiden TSV-tiedostojen avaaminen käyttöön
ZoneOpenError|Aluetiedostoa ei voitu avata: {0}
NameThisMap|Nimeä tämä kartta
ZeTypeUnknown|Tuntematon
ZeTypeCustom|Oma
ZeTypeGasStation|Huoltoasema
ZeTypeBunker|Bunkkeri
ZeTypeMilitary|Sotilaskohde
ZeTypeFaction|Ryhmittymä
ZeTypeTrader|Kauppias
ZeTypeFarm|Maatila
ZeTypeTown|Kylä
ZeTypeCity|Kaupunki
ZeMultiHelp|Valitse useita alueita Ctrl-napsautuksella tai alueväli Shift-napsautuksella. Väri ja täyttö koskevat kaikkia valittuja alueita.
ZeSelectedCount|{0} aluetta valittu
ZeSelectSameColour|Valitse samanväriset
ZeAllTypes|Kaikki aluetyypit
WpfCollapseSidebar|Piilota sivupalkki
WpfExpandSidebar|Näytä sivupalkki
WpfFollowPlayer|Seuraa pelaajaa
WpfMoveControls|Siirrä säätimiä vetämällä
WpfResizeControls|Muuta säätimien kokoa vetämällä
WpfResetControls|Palauta säätimien koko ja sijainti
WpfMoveMinimap|Siirrä pienoiskarttaa
WpfDragMinimap|Siirrä pienoiskarttaa vetämällä
WpfGettingStarted|MINIMAP · ALKUTOIMET
WpfClearRoute|Tyhjennä reitti
WpfTutorialFilters|Kaikki kohdeluokat, myös metsästys ja kalastus, ovat aluksi käytössä. Valitse näytettävät kohteet sivupalkista.
WpfTestBuild|TESTI
ZoneFillColour|Täyttöväri
ZoneFillOpacity|Täytön peittävyys
RouteHistoryColor|Reittihistorian väri…
FieldPaused|Automaattinen seuranta keskeytetty
FieldGeneral|Yleiset
FieldShortcuts|Pikanäppäimet
FieldAppearance|Ulkoasu
FieldTarget|KOHDE
FieldCalculating|Lasketaan reittiä…
FieldNoRoad|Tiereittiä ei ole saatavilla
FieldDirect|Etäisyys suoraan
FieldMinutes|noin {0} min
FieldMatchKeys|Määritä samat näppäimet kuin SCUMissa
FieldToggle|Päälle / pois
InstallationAssetsMissing|MiniMapin asennustiedostoja puuttuu tai ne ovat vioittuneet. Pura koko virallinen ZIP-paketti assets-kansioineen ja käynnistä EXE-tiedosto puretusta kansiosta.
ZeRectangle|Suorakulmio
ZeSquare|Neliö
ZeCircle|Ympyrä
ZeChangeShape|Muuta muotoa
ZeMoveZone|Siirrä aluetta
ZeMoveHelp|Valitse alue ja siirrä sitä vetämällä sen sisäpuolelta. Kumoa palauttaa edellisen muodon tai sijainnin.
ZeDrawCircle|Piirrä ympyrä
ZeCircleHelp|Määritä säde vetämällä keskipisteestä ja valitse sitten Lisää alue. Rulla: lähennä tai loitonna. Keskipainikkeella vetäminen: siirrä näkymää.
ZeSelectZone|Valitse alue
ZeDrawPolygon|Piirrä monikulmio
ZeDrawRectangle|Piirrä suorakulmio
ZeEditOutline|Muokkaa ääriviivaa
ZeDrawingHelp|Piirrä ja muokkaa kohdekartalla. Rulla: lähennä tai loitonna. Keskipainikkeella vetäminen: siirrä näkymää. Monikulmio: napsauta kulmia ja viimeistele kaksoisnapsauttamalla tai valitsemalla Lisää alue. Muokkaus: vedä kärkipisteitä, lisää piste reunalle Shift-napsautuksella ja poista piste hiiren kakkospainikkeella. Suorakulmio: vedä.
TutorialWelcome|Tervetuloa SCUM MiniMapiin
TutorialFilters|Kaikki karttatasot ovat aluksi käytössä paitsi metsästys ja kalastus. Valitse näytettävät tasot sivupalkista.
TutorialSettings|Avaa asetukset painamalla {0}. Voit vaihtaa kielen, kartan koon, peittävyyden ja näppäimet.
TutorialMap|Avaa koko kartta painamalla {0}. Aseta kohde hiiren vasemmalla painikkeella. Lähennä tai loitonna rullalla ja siirrä karttaa vetämällä.
TutorialSearch|Paina {0} ja kirjoita kylän tai kohteen nimi, esimerkiksi bunkkeri tai huoltoasema. Valitse tulos aloittaaksesi navigoinnin.
TutorialDeath|Poista yksittäinen kuolinmerkki napsauttamalla sitä suurella kartalla hiiren kakkospainikkeella.
TutorialUnbound|Tätä pikanäppäintä ei ole määritetty. Valitse näppäin Näppäinasetukset-välilehdellä.
ProfileTitle|Pelaajaprofiili — {0}
ProfileButton|Pelaajaprofiili / omaisuus
ProfileOverview|Yleiskuva
ProfileVehicles|Ajoneuvot
ProfileFlags|Liput
ProfileSquad|Ryhmä
ProfileView|Näytä {0}
ProfileTableView|Taulukkonäkymä
ProfileRefreshingAllSquads|Päivitetään kaikkia ryhmiä…
ProfileRefreshingFlags|Päivitetään lippuja: sivu {0}/{1}…
ProfileReportTimeout|SCUM ei kopioinut vastaavaa raporttia. Aiemmat tiedot säilytettiin.
ProfileUnknownStatus|Tilaa ei ilmoitettu
ProfileFlagPageImported|Lippusivu {0}/{1} tuotiin. Korvaa koko lippuluettelo valitsemalla Päivitä kaikki tiedot.
AdminCopyId|Kopioi tunniste
AdminCopyName|Kopioi nimi
AdminCopyCoordinates|Kopioi koordinaatit
AdminCopyDetails|Kopioi kaikki tiedot
AdminCopyOwner|Kopioi omistajan tunniste
AdminCopyCommand|Kopioi komento
AdminCopied|Kopioitu leikepöydälle.
PlayersSort|Järjestä
PlayersSquadSize|Ryhmän koko
ProfileTileView|Ruudukkonäkymä
ProfileImportedMember|Tuotu ryhmän jäsen
ProfileObservation|Viimeisin havainto: {0}
ProfileAssetCount|{0} ({1})
ProfileNoPopulation|Pelaajatilannekuvaa ei ole vielä tuotu.
ProfileNoSquad|Tälle pelaajalle ei ole tuotu ryhmäraporttia.
ProfileInvalid|Leikepöydällä ei ole kokonaista tuettua ajoneuvoluetteloa tai ryhmä- ja lippuraporttia.
ProfileDatabaseVersion|Ylläpitäjän profiilitietokannan versiota ei tueta.
ProfileRefreshVehicles|Päivitä ajoneuvoluettelo
ProfileRefreshSquad|Päivitä ryhmä ja liput
ProfileRefreshMissing|Päivitä tallennetut käytettävissä olevat raportit. Puuttuvia ryhmäraportteja: {0}. Aiemmat tiedot säilytettiin.
AdminToolsEnabled|Ylläpitotyökalut
ProfileTeleportVehicle|Teleporttaa ajoneuvon luo
ProfileTeleportFlag|Teleporttaa lipulle
ProfileRefreshAll|Päivitä kaikki tiedot
ProfileRefreshProgress|Ryhmä ja liput {0}/{1}: {2}
ProfileRefreshingSquad|Päivitetään ryhmää ja lippuja SCUMista…
ProfileSquadTimeout|Tälle pelaajalle ei saatu uutta ryhmäraporttia. Suorita SquadInfo ja tuo kopioitu raportti.
ProfileRefreshingVehicles|Pyydetään SCUMia kopioimaan ajoneuvoluettelo…
ProfileVehicleTimeout|SCUM ei kopioinut uutta ajoneuvoluetteloa. Suorita #ListSpawnedVehicles true ja tuo kopioitu tulos pelaajaprofiiliin.
ProfileVehiclesSaved|Ajoneuvoluettelo tallennettiin palvelimelle {0}. Valitse pelaaja ja avaa hänen profiilinsa nähdäksesi omistetut ajoneuvot.
ProfileHelp|Palvelin: {0} · {1}\nTuo kopioitu #ListSpawnedVehicles true -tulos tai pelaajan #SquadInfo-raportti. Sijainnit ovat tilannekuvia, eivät reaaliaikaista seurantaa.
ProfileIdentity|{0}\nAlusta: {1}\nAlustan tunniste: {2}\n{3}
ProfilePopulation|Pelaajatilannekuva: {0} · {1}\nMaine: {2} · Saldo: {3} · Kulta: {4}
ProfileSummary|{0} omistettua ajoneuvoa · {1} omistettua lippua\n{2}
ProfileExplanation|Raportit tallennetaan paikallisesti palvelinkohtaisesti. Päivitä ajoneuvot, ryhmän jäsenet ja liput valitsemalla Päivitä kaikki tiedot. Ajoneuvon nimimerkki ei todista omistajuutta. Poissa olevan omistajan tunnistamiseen tarvitaan vastaava lipun omistajaraportti. Sijainnit ovat aikaleimattuja tilannekuvia.
ProfileOwnerAlias|Omistaja / nimimerkki
ProfileType|Tyyppi
ProfileCoordinates|Koordinaatit
ProfileImported|Tuotu
ProfileAllAssets|Näytä palvelimen kaikki omaisuuskohteet
ProfileShowSelected|Näytä valitut kartalla
ProfileClearMarkers|Tyhjennä omaisuusmerkit
ProfileCopySquad|Kopioi DumpAllSquadsInfoList-komento
ProfileCopyVehicles|Kopioi ajoneuvoluettelon komento
ProfileOwnershipHelp|Omaisuus yhdistetään pelaajaan vain nimenomaisen omistajatunnisteen perusteella. Tietokannan offline-tunnisteet ja STEAMID-nimimerkit pidetään erillään.
ProfileSquadHeader|Ryhmä: {0} ({1}) · tuotu {2}
ProfileSquadMember|{0} · arvo {1} · {2}
ProfileMemberDetails|{0}\n{1}\nArvo {2} · {3} tuodussa raportissa
ProfileOpenPlayer|Avaa pelaajaprofiili
ProfileUnresolvedOwner|Tuntematon tietokantatunniste {0}
ProfileNoOwner|Ei omistajaa
ProfileAlias|{0} / nimimerkki {1}
ProfileImportDone|Pelaajan {0} profiilihavainnot tuotiin. Valitse sijainti näyttääksesi sen kartalla.
ProfileCopySquadDone|DumpAllSquadsInfoList-komento kopioitiin. Suorita se SCUMissa ja tuo sitten kopioitu raportti.
ProfileCopyVehiclesDone|Ajoneuvoluettelon komento kopioitiin. Suorita se SCUMissa ja tuo sitten kopioitu raportti.
ProfileSelectLocation|Avaa ensin Ajoneuvot tai Liput ja valitse yksi tai useampi sijainti.
ProfileMarkersCleared|Omaisuusmerkit poistettiin.
ProfileAssetMarker|{0} #{1} · {2} · tilannekuva {3}
ProfileOnline|Paikalla
ProfileOffline|Poissa
ProfileVehicle|Ajoneuvo
ProfileFlag|Lippu
PlayersTitle|Ylläpitäjän pelaajaluettelo
PlayersHelp|Valitse palvelin ennen tuontia. Päivitä lähettää komennon #ListPlayers true. Sijainnit ovat tilannekuvia: ne harmaantuvat 60 sekunnin kuluttua ja piilotetaan 5 minuutin jälkeen. Tarkista palvelin vaihtaessasi peliä.
PlayersInvalid|Leikepöydällä ei ole kokonaista tuettua #ListPlayers true -tulosta.
PlayersChooseServer|Anna ja valitse palvelimen nimi ennen tuontia.
PlayersDatabaseVersion|Tätä pelaajatietokannan versiota ei tueta.
PlayersServer|Valitse palvelin
PlayersImport|Tuo leikepöydältä
PlayersRefresh|Päivitä pelistä
PlayersShow|Näytä pelaajat kartalla
PlayersName|Hahmo
PlayersPlatformName|Alustan nimi
PlayersState|Tilannekuvan tila
PlayersSeen|Viimeksi nähty
PlayersGo|Teleporttaa pelaajan luo
PlayersHere|Tuo pelaaja tähän
PlayersDetails|Maine: {0} · Pankki: {1} · Kulta: {2} (kopioidut arvot)
PlayersSnapshot|Viimeisimmässä tilannekuvassa
PlayersHistory|Tallennettu historia
PlayersScope|Valittu palvelin: {0}. Varmista, että se vastaa palvelinta, jolla pelaat.
PlayersImported|{0} pelaajaa tuotiin palvelimelle {1}.
PlayersRefreshing|Pyydetään pelaajaluetteloa…
PlayersNoResponse|Kelvollista pelaajaluetteloa ei saatu. Tarkista ylläpito-oikeudet tai suorita #ListPlayers true ja valitse Tuo leikepöydältä.
PlayersTeleportSent|Teleporttikomento lähetettiin. Tarkista tulos SCUMissa.
PlayersTeleportStopped|Teleporttikomennon lähetys keskeytettiin.
PlayersSearch|Hae nimellä tai pelaajatunnisteella
SpawnCartModifiers|Lisämääreet
SpawnCartEditModifiers|Muokkaa lisämääreitä…
SpawnCartModifierTitle|Luotavien esineiden lisämääreet — {0}
SpawnCartModifierSelection|{0} valittua esinettä
SpawnCartModifierBulkHelp|Muutokset koskevat kaikkia {0} valittua esinettä. Keskenään poikkeavat arvot säilyvät, kunnes valitset uuden arvon tai poistat lisämääreen valinnan.
SpawnCartModifierMixed|Vaihtelee — säilytä kunkin esineen arvo
SpawnCartModifierApplied|{0} esineen lisämääreet päivitettiin.
SpawnCartModifierHelp|Valitse haluamasi lisämääreet ja esiasetetut arvot. Poista lisämääreen valinta käyttääksesi SCUMin oletusta. Voit myös syöttää oman arvon. Lisämääreet koskevat jokaista luotavaa esinettä.
SpawnCartModifierValues|Health ja Dirtiness: 0–100 %. Uses: vähintään 0. StackCount: vähintään 1. Weight: kg. SCUM rajoittaa Uses- ja StackCount-arvoja esinekohtaisesti.
SpawnCartModifierLocation|Location: X=… Y=… Z=…, kolme koordinaattia, SCUMista kopioitu sijaintiteksti tai 17-numeroinen Steam-tunniste.
SpawnCartModifierApply|Käytä
SpawnCartModifierClear|Palauta oletukset
SpawnCartModifierPairs|Syötä lisämääre ja arvo pareittain, esimerkiksi Health 100 Weight 0.5.
SpawnCartModifierUnknown|Tuntematon esineen lisämääre: {0}.
SpawnCartModifierDuplicate|Kukin lisämääre voi esiintyä vain kerran: {0}.
SpawnCartModifierValue|Anna kelvollinen arvo lisämääreelle {0}.
SpawnCartModifierLength|Koko esineenluontikomennon on mahduttava 512 merkkiin.
SpawnCartChatBinding|SCUMin keskustelunäppäin: {0}. Sen on vastattava SCUMin näppäinasetuksia.
SpawnCartNotSent|Ei lähetetty: {0}
SpawnCartMonitorUnavailable|Näppäimistön valvonta ei ole käytettävissä. Käynnistä MiniMap uudelleen ennen komentojen lähettämistä.
SpawnCartCloseChatInventory|Sulje SCUMin keskustelu ja tavaraluettelo ennen ylläpitokomennon lähettämistä.
SpawnCartGameUnavailable|SCUMin peli-ikkunaa ei löytynyt. Avaa SCUM ikkunatilassa tai reunattomassa ikkunatilassa ja yritä uudelleen.
SpawnCartForegroundBlocked|Palaa SCUMiin tai MiniMapiin ennen luontilistan lähettämistä.
SpawnCartFocusUnavailable|Komentoa ei lähetetty. Pidä SCUM käytettävissä ja vapauta painetut näppäimet ennen uutta yritystä.
SpawnCartNativeInputRejected|Windows ei voinut lähettää näppäinsyötettä: {0}
SpawnCartGroupWeapons|Aseet
SpawnCartGroupClothing|Vaatteet ja suojavarusteet
SpawnCartGroupSurvival|Ruoka, terveys ja selviytyminen
SpawnCartGroupCrafting|Varusteet ja valmistus
SpawnCartGroupVehicles|Ajoneuvojen osat
SpawnCartGroupOther|Muut esineet
SpawnCartRangedWeapons|Ampuma-aseet
SpawnCartMeleeWeapons|Lähitaisteluaseet
SpawnCartSaveStopped|Luontilistan tallennus epäonnistui. Lähetys keskeytettiin. Tallennettu lista voi yhä sisältää jo lähetettyjä esineitä. Tarkista se ennen uudelleenkäyttöä.
SpawnCartInvalidCommand|Lista sisältää virheellisen esineenluontikomennon.
SpawnCartBusy|Toinen ylläpitokomento on käynnissä tai MiniMap sulkeutuu.
SpawnCartFocusInputChanged|Komennon lähetys keskeytettiin, koska SCUMin kohdistus tai fyysinen näppäinsyöte muuttui.
SpawnCartClipboardChanged|Komennon lähetys keskeytettiin, koska leikepöydän sisältö muuttui.
SpawnCartCleanupStopped|Komento lähetettiin, mutta keskustelun sulkeminen keskeytyi. Sulje SCUMin keskustelu ennen jatkamista.
SpawnCartBeforeSubmitStopped|Komento keskeytettiin ennen lähetystä. Tarkista ja sulje SCUMin keskustelu ennen jatkamista.
SpawnCartException|Komennon lähetys keskeytettiin: {0}
SpawnCartSubmittedStatus|Esineiden luontilista: {0}/{1} komentoa lähetetty. Tarkista tulos SCUMissa.
SpawnCartSendingHelp|Pidä SCUM kohdistettuna ja vapauta kaikki näppäimet. Keskeytä lähetys painamalla mitä tahansa näppäintä.
SpawnCartTitle|Esineiden luonti
SpawnCartOpen|Esineiden luonti / lista…
SpawnCartHelp|Hae SCUM Database -esineitä, valitse määrät ja lähetä luontilista SCUMin keskusteluun. Esineet luodaan ylläpitäjähahmosi viereen.
SpawnCartSearch|Hae nimellä tai esinetunnisteella
SpawnCartCompatibility|Liittyy kohteeseen
SpawnCartExpandGroups|Laajenna kaikki
SpawnCartCollapseGroups|Supista kaikki
SpawnCartClearSearch|Tyhjennä haku
SpawnCartSearchHint|Kokeile hakua AK47 tai AK47 kiikaritähtäin. Laajenna tai supista tuloksia osioiden nuolilla.
SpawnCartSelectedItems|{0} valittu
SpawnCartEmptyResults|Vastaavia esineitä ei löytynyt. Kokeile muuta hakua tai luokkaa.
SpawnCartCompatibilityFor|Kohteelle {0}
SpawnCartCompatibilityAdapter|Kohteelle {0} (saattaa vaatia sovittimen)
SpawnCartCategory|Luokka
SpawnCartAll|Kaikki luokat
SpawnCartName|Esine
SpawnCartId|Luontitunniste
SpawnCartQuantity|Määrä
SpawnCartAdd|Lisää valitut
SpawnCartContents|Esineiden luontilista
SpawnCartRemove|Poista valitut
SpawnCartClear|Tyhjennä lista
SpawnCartSave|Tallenna varustepaketti…
SpawnCartLoad|Lataa varustepaketti…
SpawnCartSend|Lähetä lista
SpawnCartStop|Keskeytä lähetys
SpawnCartSource|Esinetunnisteet: SCUM Database
SpawnCartMatches|{0}/{1} esinettä
SpawnCartSummary|{0} esinetyyppiä / {1} esinettä
SpawnCartEmpty|Lisää ensin esineitä luontilistaan.
SpawnCartLimits|Enintään 100 esinetyyppiä, 100 kappaletta kutakin esinettä ja yhteensä 1 000 esinettä listalla.
SpawnCartInvalid|Lista sisältää tuntemattoman esinetunnisteen tai virheellisen määrän.
SpawnCartLimitExceeded|Tämä ylittäisi luontilistan enimmäisrajat.
SpawnCartFileError|Luontilistaa ei voitu ladata tai tallentaa: {0}
SpawnCartCatalogError|Esineluetteloa ei voitu ladata: {0}
SpawnCartSending|Lähetetään komentoa {0}/{1}…
SpawnCartSubmitted|{0} komentoa lähetettiin. Tarkista tulos SCUMissa.
SpawnCartInterrupted|{0}/{1} komentoa lähetettiin. Lähettämättömät esineet säilyvät listalla. {2}
SpawnCartTarget|Luontipaikka
SpawnCartTargetSelf|Ylläpitäjähahmosi viereen
SpawnCartPreview|Komennon esikatselu
SpawnCartSelect|Valitse yksi tai useampi esine. Lisää esine kaksoisnapsauttamalla sitä.
SpawnCartUnconfirmed|Komennon lähettäminen ei varmista, että palvelin loi esineet.
SpawnCartKitFilter|SCUM-varustepaketit (*.tsv)|*.tsv
AdminPanelSettings|Ylläpitokomentopaneeli
UpdateInstallTitle|Asenna SCUM MiniMapin päivitys
UpdateInstallPrompt|Käynnissä oleva SCUM MiniMap v{0} löytyi sijainnista:\n{1}\n\nAsennetaanko v{2} tähän? MiniMap sulkeutuu, päivittyy ja käynnistyy uudelleen. SCUM pysyy käynnissä.
UpdateInstallChoose|Valitse asennetun SCUM MiniMapin suoritettava tiedosto
UpdateInstallAmbiguous|Käynnissä on useampi MiniMap-asennus. Valitse päivitettävä suoritettava tiedosto.
AdminRailTeleport|Teleporttaa
ClearDeathMarkers|Poista kuolinmerkit
NoteDeathMarkersRemoved|{0} kuolinmerkkiä poistettiin.
AdminOpenFromSettings|Avaa ylläpitopaneeli uudelleen asetuksista.
SpawnCartKitName|Varustepaketin nimi
SpawnCartSavedKits|Tallennetut varustepaketit
SpawnCartKitStorageHint|Tallennetut varustepaketit säilyvät käytettävissä tässä MiniMap-profiilissa.
SpawnCartNoSavedKits|Varustepaketteja ei ole vielä tallennettu. Tallenna luontilista tai tuo aiempi paketti.
SpawnCartKitDetails|{0} komentoa · {1} esinettä · päivitetty {2}
SpawnCartKitNameInvalid|Anna 1–80 merkin pituinen nimi ilman ohjausmerkkejä.
SpawnCartKitExists|Varustepaketti ”{0}” on jo olemassa.
SpawnCartKitReplace|Korvataanko varustepaketti ”{0}” nykyisellä luontilistalla?
SpawnCartKitSaved|Varustepaketti ”{0}” tallennettiin.
SpawnCartKitLoaded|Varustepaketti ”{0}” ladattiin.
SpawnCartKitImport|Tuo vanha varustepaketti
SpawnCartKitImported|Varustepaketti ”{0}” tuotiin.
SpawnCartKitMissing|Tämä varustepaketti ei ole enää käytettävissä.
SpawnCartDatabaseVersion|Varustepakettien tietokanta on luotu uudemmalla MiniMap-versiolla.
SpawnCartCancelClose|Keskeytä lähetys ennen tämän ikkunan sulkemista.
SpawnCartClearConfirm|Tyhjennetäänkö luontilistan kaikki esineet?
PoiSectionSummary|{0} ({1} luokkaa, {2} sijaintia)
PoiSummary|{0}/{1} | {2:N0} kohdetta | {3} elinaluetta
GuideFishingSource|Kalastustietojen lähde
GuideHuntingSource|Metsästystietojen lähde
GuideRetrieved|Haettu: {0}
UpdateReadyRestarting|Lataus varmistettiin. MiniMap sulkeutuu, asentaa päivityksen ja käynnistyy automaattisesti uudelleen.
CustomMapRecovery|Omaa karttaasi ei voitu ladata. Käytetään oletuskarttaa; oma karttatiedostosi on säilytetty.
WizardKeybindsSkipNotice|Ohita tämä vaihe vain, jos näytetyt näppäimet vastaavat jo SCUMin asetuksia. Uudessa asennuksessa Copy location käyttää näppäintä Num / eli numeronäppäimistön jakonäppäintä ilman muunnosnäppäintä. Jos numeronäppäimistöä ei ole, määritä valitsemasi näppäin tässä.
WizardKeybindsDesc|Määritä SCUMin kartta-, keskustelu- ja sijainnin kopiointinäppäimet vastaamaan pelin asetuksia. Määritä sitten kullekin MiniMap-toiminnolle yksi fyysinen näppäin tai jätä toiminto määrittämättä.
WizardKeybindsTitle|FYYSISTEN NÄPPÄINTEN MÄÄRITYS
WizardKeyCopy|Koordinaattien kopiointinäppäin
WizardKeyModifier|Kopioinnin muunnosnäppäin
WizardKeyChat|SCUMin keskustelunäppäin
WizardKeyMap|SCUMin karttanäppäin
WizardPressKeyPrompt|PAINA NYT FYYSISTÄ NÄPPÄINTÄ…
WizardBtnResetDefaults|PALAUTA OLETUKSET
WizardBtnCapture|MÄÄRITÄ NÄPPÄIN
WizardBtnSkip|OHITA VAIHE >>
WizardTabKeybinds|03. NÄPPÄIMET
WizardTitle|Skynett MiniMap – käyttöopas
WizardBranding|SKYNETT TAKTINEN TIEDUSTELU // NÄYTTÖAVUSTAJA
WizardTagline|SCUM-saaren sijaintitiedot, navigointi ja taktinen tutka
WizardTabOverview|01. YLEISKUVA
WizardTabControls|02. OHJAUS
WizardTabSafety|04. TURVALLISUUS
WizardTabFeatures|05. ALUEET JA KOHTEET
WizardStepIndicator|Vaihe {0}/{1}
WizardBtnPrev|< EDELLINEN
WizardBtnNext|SEURAAVA >
WizardBtnFinish|KÄYNNISTÄ NÄYTTÖ
OpenStartupGuide|Käyttöönotto-opas…
WzOvTitle1|Ei DLL-injektiota
WzOvDesc1|Skynett MiniMap toimii kokonaan SCUM-prosessin ulkopuolella. Se ei muuta pelitiedostoja, kytkeydy pelin muistiin eikä lisää peliin koodia.
WzOvTitle2|Suositeltu näyttötila
WzOvDesc2|Valitse SCUMin näyttötilaksi reunaton ikkuna tai reunaton koko näyttö, jotta karttanäyttö ja hiiriohjaus toimivat sujuvasti.
WzOvTitle4|Reaaliaikainen tienavigointi
WzOvDesc4|Sisäinen A*-reitinhaku käyttää SCUMin tieverkkoa ja laskee ajoreitit sekä jäljellä olevan matkan millisekunneissa.
WzKeyMTitle|SCUMin karttanäppäin
WzKeyMDesc|Avaa koko kartta määrittämälläsi karttanäppäimellä. Lähennä tai loitonna rullalla, siirrä näkymää vetämällä ja avaa kohdevalinnat hiiren kakkospainikkeella. Sulje karttanäppäimellä tai Esc-näppäimellä.
WzKeyHomeTitle|Asetukset
WzKeyHomeDesc|Avaa asetuspaneeli, jossa voit muuttaa peittävyyttä, kokoa, ruudukkoviivoja, kohdeluokkien suodattimia ja pelin näppäinasetuksia.
WzKeyInsertTitle|Lisää sijaintimerkki
WzKeyInsertDesc|Tallenna nykyinen sijaintisi nimettynä merkkinä tälle kartalle.
WzKeyDeleteTitle|Haku
WzKeyDeleteDesc|Avaa kohdehaku, jonka avulla löydät nopeasti huoltoasemat, bunkkerit ja kaupungit sekä lasket ajoreitit.
WzKeyEndTitle|Näytä / piilota kartta
WzKeyEndDesc|Näytä tai piilota pienoiskartta heti keskeyttämättä taustalla tapahtuvaa koordinaattien päivitystä.
WzKeyCopyTitle|Koordinaattien päivitys käsin
WzKeyCopyDesc|Pyydä koordinaatit leikepöydälle käsin, jos automaattinen päivitys viivästyy tai on keskeytetty.
WzSafeTitle1|Koordinaattien kopiointinäppäin
WzSafeDesc1|MiniMap pyytää koordinaatit SCUMin omalla Copy location -toiminnolla ja lukee ne leikepöydältä.
WzSafeTitle2|Keskustelu avoinna: automaattinen kopiointi keskeytetty.
WzSafeDesc2|Koordinaattien kopiointi keskeytyy keskustelun tai tunnistetun tavaraluettelon käytön ajaksi sekä SCUMin menettäessä kohdistuksen.
WzSafeTitle3|SCUMin näppäinasetusten opas
WzSafeDesc3|Pidä MiniMapin kartta-, keskustelu- ja sijainnin kopiointinäppäimet samoina kuin SCUMin asetuksissa.
WzFeatTitle1|ScumMapin kohdesuodattimet
WzFeatDesc1|Valitse näytettävät kohdeluokat, kuten poliisiasemat, bunkkerit, lentokentät ja kauppiaat. Kalastus- ja metsästyssuodattimet sisältävät myös elinalueiden rajat.
WzFeatTitle2|Alueiden luontiopas
WzFeatDesc2|Pelaatko yksityisellä palvelimella, jolla on omia turva-alueita tai PvP-sektoreita? Ota kartasta kuvakaappaus, jotta voit tunnistaa ja kohdistaa palvelimen omat alueet.
WzFeatTitle3|Pysyvät sijaintimerkit
WzFeatDesc3|Alueet ja sijaintimerkit tallennetaan erikseen kullekin kartalle, ja ne säilyvät uudelleenkäynnistyksen jälkeen.
AddCustomWaypointHere|Lisää oma sijaintimerkki tähän
AllZonesDeletedDone|Kaikki omat alueet ja sijaintimerkit poistettiin.
ApplyClose|Käytä ja sulje
AutoZoomMax|Automaattisen lähennyksen enimmäistaso (paikallaan)
AutoZoomMin|Automaattisen lähennyksen vähimmäistaso (suuri nopeus)
AutoZoomSpeed|Automaattinen lähennys liikkumisnopeuden mukaan
AwaitingPosition|Odotetaan sijaintia…
Bear|Karhu
BiomeForest|Metsä
BiomeMeadow|Niitty
BiomeMediterranean|Välimeren alue
BiomeMountain|Vuoristo
Boar|Villisika
Cancel|Peruuta
CheckUpdatesGitHub|Tarkista päivitykset (GitHub)…
Chicken|Kana
ClearAllCustomZones|Poista kaikki omat alueet ja sijaintimerkit…
ClearAllZonesConfirm|Poistetaanko kaikki {0} omaa aluetta ja sijaintimerkkiä pysyvästi?
ClearWaypoint|Tyhjennä kohde
Close|Sulje
ConfigurePoiFilters|Valitse karttakuvakkeet
CustomWaypointsDescription|Nykyiselle kartalle tallennetut omat sijaintimerkit.
CustomZonesDescription|Näytä tallennettujen omien alueiden rajat. Luo tai tuo alueita alueiden luontioppaalla.
DataFolderOpenError|Datakansiota ei voitu avata: {0}
Deer|Peura
DeleteWaypoint|Poista oma sijaintimerkki
DeleteWaypointNamed|Poista sijaintimerkki ”{0}”
DeselectAll|Poista kaikki valinnat
DiagCopyActive|Automaattinen kopiointi käytössä.
Done|Valmis
Donkey|Aasi
FactionPOI| / Ryhmittymän kohde
FadeEdges|Häivytä reunat
FullMap|Koko kartta
Goat|Vuohi
GpsMarker|GPS-merkki
GridBorders|Ruudukkoviivat
GridLabels|Ruudukon numerot / sektoritunnukset (D4, C3 jne.)
GridOpacity|Ruudukon peittävyys: {0} %
HabitatGuide|Elinalueopas
HabitatHelp|Näytä elinalueiden rajat valitsemalla lajeja Kalastus- tai Metsästys-kohdasta. Kalojen rajat perustuvat julkaistuihin esiintymisalueiden rajoihin, eivät rantaviivoihin. Metsästysrajat seuraavat ScumMapin biomiopasta. Lajikohtaiset tiedot vaihtelevat lähteittäin eivätkä takaa eläimen esiintymistä. Havaintopaikat, syvyys, kellonaika, syötit ja palvelimen eläinkannat vaikuttavat saatavuuteen. Kalastus- ja metsästyskohteet säilyvät erillisinä.
HeaderMapZones|SKYNETT / KARTTA-ALUEET
HeaderSettings|SKYNETT / ASETUKSET v{0}
HeaderWaypoint|SKYNETT / SIJAINTIMERKKI
Horse|Hevonen
HuntingLegendNote|* Muuntuneet pedot liikkuvat näillä biomeilla harvinaisina esiintyminä
JoinDiscord|Liity Discord-yhteisöön…
KeyRebindingWizard|SCUMin ja MiniMapin näppäinasetusten opas…
KeyWizardCancel|Peruuta
KeyWizardCaptureCancelled|Näppäimen määritys peruutettiin.
KeyWizardCaptured|Näppäimeksi määritettiin {0}.
KeyWizardChatKey|SCUMin keskustelunäppäin
KeyWizardCopyKey|Koordinaattien kopiointinäppäin
KeyWizardCopyModifier|Koordinaattien kopioinnin muunnosnäppäin
KeyWizardCopyPairInvalid|Koordinaattien kopiointinäppäimen ja sen muunnosnäppäimen on oltava kelvollisia ja eri näppäimiä.
KeyWizardDifferentKeys|Kartan, keskustelun, kopioinnin ja MiniMapin pikanäppäinten on kaikkien oltava eri näppäimiä.
KeyWizardIntro|Määritä kaikki SCUMin ja MiniMapin näppäimet tässä. SCUMin kartta-, keskustelu- ja sijainnin kopiointinäppäinten on vastattava pelin asetuksia. MiniMapin toiminnot käyttävät yhtä fyysistä näppäintä. Poista määritys poistaa sovelluksen pikanäppäimen käytöstä.
KeyWizardMapKey|SCUMin karttanäppäin
KeyWizardModifierRequired|Paina muunnosnäppäintä (Ctrl, Alt, Shift tai Windows).
KeyWizardNonModifier|Paina näppäintä, joka ei ole muunnosnäppäin.
KeyWizardPressChat|Paina nyt SCUMin keskustelunäppäintä. Escape peruuttaa määrityksen.
KeyWizardPressCopyKey|Paina nyt SCUMin koordinaattien kopiointinäppäintä. Escape peruuttaa määrityksen.
KeyWizardPressCopyModifier|Paina nyt SCUMin koordinaattien kopioinnin muunnosnäppäintä. Escape peruuttaa määrityksen.
KeyWizardPressKey|Paina näppäintä…
KeyWizardPressMap|Paina nyt SCUMin karttanäppäintä. Escape peruuttaa määrityksen.
KeyWizardReady|Valitse kenttä ja paina sitten vastaavaa näppäintä.
KeyWizardSave|Tallenna
KeyWizardSaved|SCUMin ja MiniMapin näppäinasetukset tallennettiin.
KeyWizardTitle|SCUMin ja MiniMapin näppäinasetusten opas
KeyNone|Ei mitään
KeyWizardNoModifier|Yksi näppäin ilman muunnosnäppäintä
Language|Kieli
LocApproaching|{0} – Lähestytään: {1}
LocBetween|{0} – Kohteiden {1} ja {2} välissä
LocLeaving|{0} – Poistutaan: {1}
LocLeavingApproaching|{0} – Poistutaan: {1}, lähestytään: {2}
LocNear|{0} – Lähellä: {1}
LocationBarPosition|Sijaintipalkin paikka
MapHeight|Kartan korkeus (pikseliä)
MapOpacity|Peittävyys (%)
MapWidth|Kartan leveys (pikseliä)
MapZonesScreenshot|Alueiden luontiopas (tuo kuvakaappaus)…
MaxZoom|Suurin manuaalinen lähennystaso
NoCustomZonesToDelete|Omia alueita tai sijaintimerkkejä ei ole tallennettu.
NoMatches|Ei osumia. Kokeile ryhmittymää, paikan nimeä tai ruutua väliltä D4–Z0.
NoteAdsActive|Tähdätään (ADS): automaattinen kopiointi keskeytetty.
NoteAutoCopyActive|Automaattinen kopiointi käytössä.
NoteAutoWaiting|Automaattinen seuranta odottaa SCUMia.
NoteChatClosed|Keskustelu suljettu: automaattinen kopiointi voi jatkua.
NoteChatMonitorUnavailable|Keskustelunäppäimen valvonta ei ole käytettävissä. Automaattinen kopiointi poistettiin käytöstä.
NoteChatOpen|Keskustelu avoinna: automaattinen kopiointi keskeytetty.
NoteClipboardBusy|Leikepöytä on varattu. Yritetään uudelleen.
NoteCoordReceived|Automaattisen koordinaattipyynnön vastaus saatiin.
NoteCopyCancelled|Kopiointi peruutettiin kohdistuksen tai näppäinsyötteen muutoksen vuoksi. Jatketaan normaalin päivitysvälin mukaan.
NoteCopyErrorRetry|{0} Automaattinen seuranta yrittää uudelleen 5 sekunnin kuluttua.
NoteFullMapOff|Pienoiskarttanäkymä palautettiin
NoteFullMapOn|Koko kartan näkymä käytössä
NoteNoCoordsAttempt|Uusia koordinaatteja ei saatu (yritys {0}).
NoteNoCoordsRetry|Koordinaattivastausta ei saatu. Automaattinen seuranta yrittää uudelleen {0} sekunnin kuluttua.
NoteCaptureCopyKey|Koordinaattivastausta ei saatu. Määritä fyysinen Copy Location -näppäin MiniMapin asetuksissa tai määritä F11 sekä SCUMiin että MiniMapiin.
NoteSettingsSaveFail|Ulkoasuasetuksia ei voitu tallentaa.
NoteWaitingForeground|Odotetaan, että SCUM on aktiivinen ikkuna.
NoteWaitingUserKeys|Odotetaan, että käyttäjän syöte päättyy tai painetut näppäimet vapautetaan.
NoteWaypointArrived|Saavuttiin kohteeseen {0}. Kohde tyhjennettiin.
NoteWaypointCleared|Hakukohde tyhjennettiin.
NoteWaypointSet|Kohde asetettu: {0}. Hae tai tyhjennä painamalla {1}.
NoteZonesLoadFail|Tallennettuja alueita ei voitu ladata.
OpacityLabel|PEITTÄVYYS
OpenDataFolder|Avaa datakansio
Place| / Paikka
PoiFilterTitle|ScumMapin kohdeluokat ja suodattimet
PosAbove|Yläpuolella
PosBelow|Alapuolella
PositionInterval|Sijainnin päivitysväli (ms)
Rabbit|Kani
RemoveWaypoint|Poista kohde
ResetDefaultMap|Palauta oletuskartta
ResetDefaults|Oletukset
RoadSuffix| (tietä pitkin)
PlayerMarkerColor|Näkökentän ja karttamerkin väri…
RouteGuidanceColor|Reittiopastuksen väri…
SearchFilterPrompt|Suodata luokkia nimellä, osiolla tai tyypillä:
SearchHint|Kokeile: ryhmittymä, lentokenttä, bunkkeri C3 tai ruutu D4. Myös kirjoitusvirheet huomioidaan.
SearchPlaceOrGrid|Hae paikkaa tai ruutua…
SearchResults|{0} tulosta. Navigoi painamalla Enter tai palaa painamalla Esc.
SearchTitle|Hae paikkaa tai ruutua
SecAppearance|Ulkoasu
SecGeneral|Yleiset ja oppaat
SecMapZones|Kartta ja alueet
SecToolsShortcuts|Työkalut ja pikanäppäimet
SecTrackingZoom|Seuranta ja lähennys
SecondsAgo|{0} s sitten
SectorCentre| / Sektorin keskipiste
SelectAll|Valitse kaikki
SetWaypoint|Aseta kohde
Settings|Asetukset
ShowCompass|Näytä kompassin ilmansuunnat (N, S, E, W)
ShowCustomWaypoints|Näytä omat sijaintimerkit
ShowHeading|Näytä pelaajan suuntakartio / nuoli
ShowSavedZones|Näytä omat kartta-alueet
ShowScumMap|Näytä karttakuvakkeet
ShowStatus|Näytä sijaintitietopalkki
ShowZoneLabels|Näytä kohteiden nimet
SidebarLayerGrid|Ruudukkoviivat
SidebarLayerLabels|Alueiden nimet
SidebarLayerWaypoints|Omat sijaintimerkit
SidebarLayerZones|Omat kartta-alueet
SidebarNoCustomLayers|Omia tasoja ei ole ladattu
SidebarLayers|KARTTATASOT
SidebarSearchBtn|Haku ({0})
SidebarSettings|Asetukset ({0})
SidebarTitle|TAKTINEN KARTTA
SidebarTools|PIKATYÖKALUT
SidebarZoneEditor|Alueiden luontiopas…
SmartLabelLod|Piilota tarkat nimikkeet loitonnettaessa
StatusSummary|Automaattiset pyynnöt: {0} | Vastaukset: {1}
TrayCheckUpdates|Tarkista päivitykset…
TrayExit|Lopeta
TrayShowHide|Näytä / piilota kartta
UpdateAvailableDialogMsg|SCUM MiniMapista on saatavilla uudempi versio.\n\nNykyinen versio: v{0}\nUusin GitHubissa: v{1}\nLataus: {2}\n\nLadataanko päivitys nyt?
UpdateAvailableDialogTitle|Päivitys saatavilla – SkynettMiniMap
UpdateAvailableNote|Päivitys v{0} on saatavilla! Näytä tiedot painamalla {1}.
UpdateCheckTitle|Päivitysten tarkistus
UpdateConnectError|GitHub-julkaisua ei voitu hakea. Tarkista yhteys tai yritä myöhemmin uudelleen.
UpdateDownloadComplete|Päivitys ladattiin ja varmistettiin. MiniMap käynnistyy automaattisesti uudelleen.
UpdateDownloading|Ladataan päivitystä GitHubista…
UpdateFailedMsg|Päivitysten tarkistus epäonnistui: {0}
UpdateRateLimited|GitHub rajoittaa päivitysten tarkistuksia tilapäisesti tai palvelu ei ole käytettävissä. Yritä uudelleen ajankohdan {0} jälkeen.
UpdateUpToDateMsg|Uudempaa vakaata julkaisua ei ole saatavilla.\n\nAsennettu: v{0}\nUusin GitHubissa: v{1}
UpdateUpToDateTitle|Ajan tasalla
UpdateVersionUnknown|GitHub ei palauttanut kelvollisia julkaisutietoja. Yritä myöhemmin uudelleen.
WaitingForCoordinates|Odotetaan koordinaatteja
WaypointDeleteConfirm|Poistetaanko sijaintimerkki ”{0}”?
WaypointDeleted|Sijaintimerkki poistettiin: {0}
WaypointNamePrompt|Anna sijaintimerkille nimi:
WaypointNameTitle|Tallenna sijaintimerkki
WaypointSaved|Sijaintimerkki tallennettiin: {0}
Wolf|Susi
ZeAddZone|Lisää alue
ZeAligningLocally|Kohdistetaan karttaa ja tunnistetaan värillisten alueiden rajoja paikallisesti…
ZeApplyName|Aseta nimi
ZeAutoFailedKept|Automaattinen tunnistus epäonnistui. Aiemmat alueet säilytettiin.
ZeClearDraft|Tyhjennä luonnos
ZeCouldNotImportAuto|Automaattinen tuonti epäonnistui: {0}
ZeDeleteAll|Poista kaikki
ZeDeleteAllConfirm|Poistetaanko varmasti kaikki alueet? Tallennuksen jälkeen tätä ei voi kumota.
ZeDeleteAllTitle|Poista kaikki alueet
ZeDeleteSelected|Poista valitut
ZeDeselect|Poista valinta
ZeDetectedOutlinesPrompt|{0} aluerajaa tunnistettiin. Napsauta aluetta, kirjoita sen nimi ja valitse Aseta nimi tai Nimeä tämänväriset. Tallenna alueet korvaa nykyisen tallennetun luettelon.
ZeDraftWarning|Lisää nykyinen rajaus alueeksi tai kumoa sen pisteet ennen tallentamista.
ZeDuplicate|Monista
ZeExport|Vie…
ZeExportSuccess|{0} aluetta vietiin tiedostoon {1}.
ZeExportTitle|Vie kartta-alueet
ZeFillZones|Täytä alueet
ZeFilterZones|Suodatin:
ZeImportFile|Tuo tiedosto…
ZeImportSuccess|{0} aluetta tuotiin tiedostosta {1}.
ZeImportTitle|Tuo kartta-aluetiedosto
ZeInitialPrompt|Avaa kuvakaappaus, jotta se voidaan kohdistaa ja värilliset alueet tunnistaa automaattisesti. Nimeä sitten kukin alue tai väriryhmä.
ZeLabelLengthWarning|Käytä 1–80 merkin pituista nimeä.
ZeManualFallback|Käsin kohdistus
ZeAllLayers|Kaikki tasot
ZeDeleteLayer|Poista taso
ZeDeleteLayerConfirm|Poistetaanko taso '{0}' ja kaikki sen {1} aluetta pysyvästi?
ZeImportActionTitle|Alueiden tuonti – tasotila
ZeImportAppend|Lisää tasoon '{0}'
ZeImportModePrompt|Valitse tiedoston '{0}' alueiden tuontitapa:
ZeImportNewLayer|Luo uusi taso
ZeImportReplace|Korvaa taso '{0}'
ZeLayer|Taso:
ZeManualStep0|Napsauta maamerkkiä 1 kuvakaappauksessa (vasemmalla).
ZeManualStep1|Napsauta SAMAA maamerkkiä vertailukartalla (oikealla).
ZeManualStep2|Napsauta kuvakaappauksessa maamerkkiä 2, joka on kaukana ensimmäisestä molempiin suuntiin.
ZeManualStep3|Napsauta maamerkkiä 2 vertailukartalla.
ZeManualStep4|Kohdistus valmis. Napsauta kuvakaappauksessa alueen rajapisteitä, valitse nimi ja väri ja paina Lisää alue. Tarkista rajaus oikealta.
ZeMapScreenshotsFilter|Karttakuvakaappaukset|*.png;*.jpg;*.jpeg;*.bmp
ZeMoveDown|Alas
ZeMoveUp|Ylös
ZeNameColour|Nimeä tämänväriset
ZeNewLayer|+ Taso
ZeNewLayerPrompt|Anna uudelle aluetasolle nimi:
ZeNewLayerTitle|Uusi aluetaso
ZeNewZone|Uusi alue
ZeNoUsableZonesFound|Valitusta tiedostosta ei löytynyt käyttökelpoisia aluerajoja.
ZeNoZonesToExport|Vietäviä alueita ei ole.
ZeOpenScreenshot|Avaa kuvakaappaus
ZeOutsideMapWarning|Piste osuu saaren karttakuvan ulkopuolelle. Tarkista kohdistus.
ZePointsRequirementWarning|Kohdista kuvakaappaus ja valitse ensin vähintään kolme rajapistettä.
ZeRecolorGroup|Vaihda ryhmän väri
ZeSaveZones|Tallenna alueet
ZeSaveZonesError|Alueita ei voitu tallentaa: {0}
ZeSelectScreenshotTitle|Valitse pelikartan kuvakaappaus, jossa pohjoinen on ylhäällä
ZeUndoPoint|Kumoa piste
ZeZoneColour|Alueen väri
ZeZonesMaxWarning|Käytä 1–80 merkin pituista nimeä. Enintään 500 aluetta on tuettu.
ZoneChime|Äänimerkki alueelle saavuttaessa
ZoneEditorTitle|Alueiden luontiopas
ZoneLabelSize|Alueiden nimikkeiden koko
ZoomStep|Lähennyksen askelkoko (%)
ResetDefaultMapConfirm|Palautetaanko sisäinen karttakuva? Oma karttasi poistetaan.
ResetDefaultMapDone|Oletuskartta palautettiin. Käynnistä sovellus uudelleen ottaaksesi sen käyttöön.
MapResetFailed|Karttaa ei voitu palauttaa: {0}
WizardLayoutTitle|Aloita pienikokoisella pienoiskartalla
WizardLayoutDescription|Uusi asennus käyttää aluksi 240 × 240 pikselin karttaa oikeassa yläkulmassa. Vaihda työpöydälle siirtääksesi karttaa vetämällä tai muuttaaksesi sen kokoa reunoista. Määritä tarkat mitat kohdassa Asetukset > Ulkoasu. Muutokset tallentuvat automaattisesti.
WizardAdjustLayout|Siirrä pienoiskarttaa ja muuta sen kokoa…
ShortcutTitle|Määritä pikanäppäin: {0}
ShortcutPrompt|Paina yhtä fyysistä näppäintä ilman Ctrl-, Alt-, Shift- tai Windows-näppäintä. Escape peruuttaa.
ShortcutSingleKey|Käytä yhtä näppäintä ilman Ctrl-, Alt-, Shift- tai Windows-näppäintä.
ShortcutRetry|Paina näppäintä uudelleen, jotta sen fyysinen sijainti voidaan tunnistaa.
ShortcutCancel|Escape peruuttaa.
ShortcutHeading|MiniMapin pikanäppäimet (yksittäiset näppäimet)
ControlZoomIn|Lähennä
ControlZoomOut|Loitonna
ControlDeathMarker|Lisää kuolinmerkki
ControlResetZoom|Palauta lähennys
ControlAdminCommands|Ylläpitokomennot
AdminRelativeTeleportTitle|Teleporttaa viimeisimmistä koordinaateista
AdminFloatingTitle|Ylläpidon ohjaimet
AdminFloatingHelp|Valitse komento tai teleportin siirtymä. Tilat näyttävät viimeisimmät pyyntösi; tarkista tulos SCUMissa. Sulje keskustelu ja tavaraluettelo ennen lähetystä.
AdminFloatingHint|Laajenna paneeli hallitaksesi komentoja tai käyttääksesi suunnattua teleporttia.
AdminExpand|Laajenna ylläpidon ohjaimet
AdminCollapse|Supista ylläpidon ohjaimet
AdminCommandsSection|Komennot
AdminTeleportSection|Suunnattu teleportti
AdminAddCommand|Lisää komento
AdminEditCommand|Muokkaa
AdminRemoveCommand|Poista
AdminRailTitle|Ylläpito
AdminRelativeTeleportHelp|Vasen, oikea, eteen ja taakse perustuvat viimeksi tallennettuun katselusuuntaan. Ylös ja alas muuttavat korkeutta.
AdminTeleportDistance|Etäisyys (metriä)
AdminTeleportUp|Ylös
AdminTeleportDown|Alas
AdminTeleportLeft|Vasemmalle
AdminTeleportRight|Oikealle
AdminTeleportForward|Eteenpäin
AdminTeleportBack|Taaksepäin
AdminTeleportSend|Lähetä teleporttikomento
AdminTeleportPreview|Komennon esikatselu
AdminTeleportSource|Viimeisimmät koordinaatit: X {0}  Y {1}  Z {2}  |  {3}
AdminTeleportMissing|Odotetaan koordinaatteja SCUMista.
AdminTeleportWaiting|Odotetaan uusia koordinaatteja edellisen teleporttikomennon jälkeen.
AdminTeleportInvalid|Valitse kelvollinen suunta ja etäisyys viimeisimmille koordinaateille.
AdminTeleportBusy|Ylläpitokomentoa lähetetään.
AdminTeleportMenu|Suunnattu teleportti…
ControlUnbind|Poista määritys
ControlResetDefaults|Palauta oletukset
ControlAppHeading|MiniMapin näppäimet
ControlGameHeading|SCUMin näppäimet
ControlUnbound|Ei määritetty
ShortcutPhysicalHelp|Kaikki MiniMapin näppäimet voidaan määrittää uudelleen tai poistaa käytöstä. Pikanäppäimet käyttävät fyysisiä näppäimiä. Pidä Num Lock samassa tilassa käyttäessäsi numeronäppäimistöä.
ShortcutReserved|Valitse näppäin, jota ei ole varattu pelin tai valintaikkunoiden ohjaukseen.
ShortcutScumConflict|Tämä näppäin on käytössä SCUMin kartalla, keskustelussa tai sijainnin kopioinnissa.
ShortcutConflict|Tämä näppäin on jo määritetty toiselle MiniMapin pikanäppäintoiminnolle.
HistoryDuration|Historian pituus (minuuttia)
HistoryClear|Tyhjennä reittihistoria
HistorySecondsAgo|{0} s sitten
HistoryMinutesAgo|{0} min sitten
HistoryHoursAgo|{0} h sitten
HistoryDaysAgo|{0} vrk sitten
CopyReminderTitle|Tarkista koordinaattien kopiointinäppäin
CopyReminderBody|Oletus on Num / eli numeronäppäimistön jakonäppäin. Määritä SCUMin Copy location käyttämään samaa näppäintä. Jos käytät muuta näppäintä tai numeronäppäimistöä ei ole, määritä näppäin MiniMapin asetuksissa.
CopyReminderHide|Älä näytä uudelleen
CopyReminderOkay|OK
VoiceHeading|Ääninavigointi (kehitteillä)
VoiceEnable|Ota ääninavigointi käyttöön
VoiceName|Ääni
VoiceVolume|Äänenvoimakkuus (%)
VoicePreview|Kuuntele esimerkki
VoiceHelp|Lisää äänikansioita sovelluksen datakansion voice-navigation-kansioon käyttäen samoja 12 MP3-tiedostonimeä. Avaa sitten asetukset uudelleen.
VoiceRecalculating|Lasketaan reitti uudelleen
VoiceWrongWay|Väärä suunta
VoiceUnavailable|Tiereittiä ei ole saatavilla
CopyBindingHelp|Oletus: Num / eli numeronäppäimistön jakonäppäin ilman muunnosnäppäintä. Määritä SCUMin Copy location käyttämään samaa näppäintä. Aiemmat määritykset säilytetään. Jos numeronäppäimistöä ei ole, määritä valitsemasi näppäin asetuksissa. Yksittäisellä näppäimellä seurantaväli voi olla 250 ms. Ctrl+C on yhä tuettu vähintään 1 sekunnin välillä.
DeathLocation|Kuolinpaikka
DeathName|Kuolema
AutomaticDeathMarkers|Automaattiset kuolinmerkit
ShortcutWaypoint|Lisää sijaintimerkki
ShortcutSearch|Haku
UpdateResetTitle|MiniMapin päivitys
UpdateResetBody|Nollataanko tallennetut asetukset ja käynnistetäänkö käyttöönotto uudelleen?
ZoneEditorOpenError|Omien alueiden muokkainta ei voitu avata: {0}
SidebarTarget|Kohde: {0}
SidebarCategories|KOHDELUOKAT
SidebarPanHint|Vasemmalla vetäminen: siirrä näkymää
SidebarContextHint|Kakkospainike: kohdevalinnat
SidebarScrollHint|Sivupalkki: rulla tai vierityspalkki
SidebarExitHint|{0} / Esc: sulje koko kartta
HistoryEnabled|Sijaintihistoria
HistoryHover|Näytä historian aika osoitettaessa
";
        const string FinnishSections=@"Buildings|Rakennukset
Bunkers|Bunkkerit
Construction materials|Rakennusmateriaalit
Crops|Viljelykasvit
Loot containers|Tavarasäiliöt
Other|Muut
Outposts|Etuvartiot
Points of interest|Kohteet
Quests|Tehtävät
Radiation|Säteily
Vehicles|Ajoneuvot
Water sources|Vesilähteet
Fishing|Kalastus
Hunting|Metsästys
Fishing habitats|Kalojen elinalueet
Hunting habitats|Eläinten elinalueet";
        const string FinnishCategories=@"Abandoned bunkers|Hylätyt bunkkerit
Aircraft spawns|Lentokoneiden esiintymispaikat
Animal feeders|Eläinten ruokintapaikat
Boat spawns|Veneiden esiintymispaikat
Bunker Hatch Doors|Bunkkerien luukkuovet
Bunkers|Bunkkerit
Caves|Luolat
Car garages|Autotallit
Decorative fountains|Koristesuihkulähteet
Drill press / Lathe|Pylväsporakone / sorvi
Fishing spots|Kalastuspaikat
Fishing Supplies|Kalastustarvikkeet
Hunting Camps|Metsästysleirit
Hunter's Grotto|Metsästäjän luola
Fountains|Suihkulähteet
Gas stations|Huoltoasemat
Gun Shops|Aseliikkeet
Hand water pumps|Käsikäyttöiset vesipumput
Hunting Shops|Metsästysliikkeet
Hunting Towers|Metsästystornit
Industrial storage silos|Teollisuussiilot
Killboxes|Killbox-tilat
Lighthouses|Majakat
Lockers|Säilytyskaapit
Medical Containers|Lääkintälaatikot
Military Hangars|Sotilaslentokoneiden hallit
Military Warehouses|Sotilasvarastot
Mine entrances|Kaivosten sisäänkäynnit
Motorbike & bicycle spawns|Moottori- ja polkupyörien esiintymispaikat
Natural springs|Luonnonlähteet
Phone Booths|Puhelinkopit
Police stations|Poliisiasemat
Quest Boards|Tehtävätaulut
Quest books|Tehtäväkirjat
Quest Mailboxes|Tehtäväpostilaatikot
Research Facilities|Tutkimuslaitokset
Secret Bunkers|Salaiset bunkkerit
Small Docks|Pienet laiturit
Small Shipwrecks|Pienet laivanhylyt
Underwater Caves|Vedenalaiset luolat
Vehicle repair shops|Ajoneuvokorjaamot
Vehicle spawns|Ajoneuvojen esiintymispaikat
Villages|Kylät
Water dispensers|Vesiautomaatit
Wells|Kaivot
Wheelbarrow spawns|Kottikärryjen esiintymispaikat
Workshops|Pajat
WW2 bunkers|Toisen maailmansodan bunkkerit";
        const string FinnishMapObjects=@"Apple trees|Omenapuut
Armory|Asevarasto
ATM|Pankkiautomaatti
Bars|Baarit
Big Shipwrecks|Suuret laivanhylyt
Bookshelves|Kirjahyllyt
Bricks|Tiilet
Broccoli|Parsakaali
Cabbages|Kaalit
Carrots|Porkkanat
Cement bags|Sementtisäkit
Cherry trees|Kirsikkapuut
Churches|Kirkot
Clubs|Yökerhot
Corn fields|Maissipellot
Cucumbers|Kurkut
Date palms|Taatelipalmut
Depleted Uranium Crates|Köyhdytetyn uraanin laatikot
Fig trees|Viikunapuut
Food Shop|Ruokakauppa
General Goods|Sekatavarat
Grapevines|Viiniköynnökset
Graphite|Grafiitti
Gravel bags|Sorasäkit
Hairdresser|Kampaamo
Hazmat Suit Lockers|Kemikaalisuojapukukaapit
Hospital|Sairaala
Krsko POIs|Krskon kohteet
Log Cabin|Hirsimökki
Logs|Tukit
Mechanic|Mekaanikko
Novigrad POIs|Novigradin kohteet
Olive trees|Oliivipuut
Onions|Sipulit
Outposts|Etuvartiot
Paddle spawns|Melojen esiintymispaikat
Parsnip|Palsternakka
Pear trees|Päärynäpuut
Pharmacies|Apteekit
Pile of planks|Lautapino
Points of interest|Kohteet
Potatoes|Perunat
Propane Tank|Propaanisäiliö
Pubs|Pubit
Radiation Equipment Lockers|Säteilysuojavarustekaapit
Red Toolboxes|Punaiset työkalulaatikot
Restaurants|Ravintolat
RIB spawns|RIB-veneiden esiintymispaikat
Rose hip / dog rose|Ruusunmarja / koiranruusu
Saloon|Saluuna
Samobor POIs|Samoborin kohteet
Sardines|Sardiinit
Schools|Koulut
Storage shed|Varastovaja
Strawberries|Mansikat
Sunflower fields|Auringonkukkapellot
Warehouses|Varastot
Watermelons|Vesimelonit
Amur|Ruohokarppi
Bass|Isobassi
Bleak|Salakka
Carp|Karppi
Carp Radioactive|Radioaktiivinen karppi
Catfish|Monni
Chub|Turpa
Crucian Carp|Ruutana
Dentex|Hammasahven
Orata|Kultaotsa-ahven
Pike|Hauki
Prussian Carp|Hopearuutana
Ruffe|Kiiski
Sardine|Sardiini
Tuna|Tonnikala
Bear|Karhu
Boar|Villisika
Chicken|Kana
Deer|Peura
Donkey|Aasi
Goat|Vuohi
Horse|Hevonen
Rabbit|Kani
Wolf|Susi
Police Station|Poliisiasema
Gas Station|Huoltoasema
Pharmacy|Apteekki
Bar|Baari
Pub|Pubi
Club|Yökerho
Restaurant|Ravintola
Gun Shop|Aseliike
Hunting Shop|Metsästysliike
Vehicle Repair Shop|Ajoneuvokorjaamo
Car Garage|Autotalli
Workshop|Paja
School|Koulu
Church|Kirkko
Lighthouse|Majakka
Military Warehouse|Sotilasvarasto
Military Hangar|Sotilaslentokonehalli
Bunker|Bunkkeri
Abandoned Bunker|Hylätty bunkkeri
Secret Bunker|Salainen bunkkeri
WW2 Bunker|Toisen maailmansodan bunkkeri
WWII Bunker|Toisen maailmansodan bunkkeri
Bunker Hatch|Bunkkeriluukku
Research Facility|Tutkimuslaitos
General Goods Store|Sekatavarakauppa
Killbox|Killbox-tila
Mine Entrance|Kaivoksen sisäänkäynti
Storage Silo|Varastosiilo
Warehouse|Varasto
Dock|Laituri
Quest Board|Tehtävätaulu
Hunting Camp|Metsästysleiri
Hunting Tower|Metsästystorni
Workshop Lathe|Pajan sorvi
Toolbox|Työkalulaatikko
Medical Container|Lääkintälaatikko
Locker|Säilytyskaappi
Hazmat Locker|Kemikaalisuojavarustekaappi
Radiation Locker|Säteilysuojavarustekaappi
Uranium Crate|Uraanilaatikko
Airport|Lentoasema
Airfield|Lentokenttä
Outpost Airfield|Etuvartion lentokenttä
Outpost|Etuvartio
Castle|Linna
Big Vern monument|Big Vernin muistomerkki
Boot Camp|Koulutusleiri
Brick Factory|Tiilitehdas
Camping Site|Leirintäalue
Cheese Factory|Juustotehdas
Chinese shop|Kiinalainen kauppa
Church in Novigrad|Novigradin kirkko
Small Church in Novigrad|Novigradin pieni kirkko
Clinic|Klinikka
Clothing Shop|Vaatekauppa
Coal Mine|Hiilikaivos
DPinchy monument|DPinchyn muistomerkki
Dam|Pato
Duga Radar|Dugan tutka-asema
Elementary School|Alakoulu
Elementary School Yard|Alakoulun piha
Factory|Tehdas
Fire Station|Paloasema
Fish Factory|Kalatehdas
Fishing Spot|Kalastuspaikka
Gun Range|Ampumarata
Halloween House|Halloween-talo
Hardware Store|Rautakauppa
Hotel|Hotelli
Hotel Marina|Marina-hotelli
Junkyard|Romuttamo
Klenovnik Hospital|Klenovnikin sairaala
Liquor Store|Alkoholimyymälä
Lumber Mill|Saha
MMA Cage|Vapaaotteluhäkki
Mailbox|Postilaatikko
Market|Tori
Marketplace|Kauppapaikka
Military Airfield|Sotilaslentokenttä
Military Barracks|Sotilaskasarmi
Military Compound|Sotilasalue
Military Radar & Trenches|Sotilastutka ja juoksuhaudat
Military Stash|Sotilaskätkö
Monolith|Monoliitti
Motorbike Track|Moottoripyörärata
Naval Base|Laivastotukikohta
North Quarry|Pohjoinen louhos
Novigrad Hospital|Novigradin sairaala
Nuclear Power Plant|Ydinvoimala
Observatory|Observatorio
Outdoor restaurant|Ulkoilmaravintola
Park Pond|Puistolampi
Prison|Vankila
Radio Station|Radioasema
Running Track|Juoksurata
Salt Ponds|Suola-altaat
Sanitarium|Parantola
School Gym|Koulun liikuntasali
Shoe Shop|Kenkäkauppa
Small Shipyard|Pieni telakka
South Quarry|Eteläinen louhos
Sports Hall|Urheiluhalli
Stadium|Stadion
Supermarket|Supermarketti
Train Yard|Ratapiha
Trainyard|Ratapiha
Waterfall|Vesiputous
Weapons Factory|Asetehdas
Windmills|Tuulimyllyt
Ramsons Hill|Karhunlaukkakukkula
Rosemary Thyme|Rosmariini ja timjami
City|Kaupunki
POI|Kohde
Camping|Leirintä
Offices|Toimistot
Stables|Tallit
Store with ATM|Kauppa ja pankkiautomaatti
Farm|Maatila
Island WWII bunker|Saaren toisen maailmansodan bunkkeri
House ruins|Talon rauniot
Street between 2 houses|Kahden talon välinen katu
Barn|Lato
Little Farm|Pieni maatila
Little cabin|Pieni mökki
Wine farm|Viinitila
CC|CC
Food store|Ruokakauppa
Graveyard|Hautausmaa
Little bridge|Pieni silta
Lumbermill cabin|Sahan mökki
- General Store|- Sekatavarakauppa
City Warehouse|Kaupungin varasto
Little town - House|Pieni kylä – talo
City - Pharmacy|Kaupunki – apteekki
City - Police Station|Kaupunki – poliisiasema
Clock house|Kellotalo
Stable ruins|Tallin rauniot
Demolition Derby|Romuralli
Airfield East|Lentokentän itäosa
Airfield North|Lentokentän pohjoisosa
Boat Yard|Venetelakka
Boatyard|Venetelakka
D3 Town|D3-kylä
East Coast|Itärannikko
Northeast|Koillinen
Northwest|Luode
Tisno East|Tisnon itäosa
Tisno South|Tisnon eteläosa
WWII Bunker South|Toisen maailmansodan bunkkerin eteläosa
light house|Majakka
Island Lighthouse|Saaren majakka
Abandoned City Quest Board|Hylätyn kaupungin tehtävätaulu
Hosiptal Klenovnik Quest Board|Klenovnikin sairaalan tehtävätaulu
Mountain|Vuoristo
Mediterranean|Välimeren alue
Continental Forest|Sisämaan metsä
Continental Meadow|Sisämaan niitty
Lake Big|Suuri järvi
River Big Radioactive Activity Spot|Suuren joen radioaktiivinen kalapaikka
Sea Dentex Activity Spot|Meren hammasahvenpaikka";
    }
}
