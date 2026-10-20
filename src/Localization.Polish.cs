using System;
using System.Collections.Generic;

namespace ScumMiniMap {
    public static partial class Localization {
        public static readonly Dictionary<string,string> StringsPl = ReadPolishStrings();
        static Dictionary<string,string> ReadPolishStrings() {
            var result = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (string row in PolishUi.Split('\n')) {
                string line = row.TrimEnd('\r');
                int separator = line.IndexOf('|');
                if (separator < 1) throw new FormatException("Invalid Polish UI translation.");
                result.Add(line.Substring(0,separator), line.Substring(separator+1).Replace(@"\n", "\n"));
            }
            return result;
        }
        const string PolishUi = @"ScumAutoLaunchLabel|Uruchamiaj MiniMap wraz ze SCUM i zamykaj po zakończeniu gry
ScumAutoLaunchHelp|Monitor w tle uruchamia się przy logowaniu do Windows. MiniMap zapisuje dane i zamyka się 10 sekund po SCUM. Po ręcznym zamknięciu czeka na następną sesję gry. Wyłącz przed przeniesieniem lub usunięciem folderu aplikacji.
RouteModeQuickest|Najszybsza
RouteModeShortest|Najkrótsza
RouteModePavedOnly|Tylko drogi asfaltowe
RouteModeHelp|Szacunek: 60 km/h na asfalcie, 30 na drogach gruntowych i 5 na dojazdach. Najkrótsza minimalizuje odległość. Tylko asfalt wyklucza drogi gruntowe; suche dojazdy do 1 km. Tory są wykluczone.
AdminMapTeleportHere|Teleportuj tutaj
AdminMapTeleportHelp|Wyślij {0}. Wysokość mapy jest niedostępna; Z wynosi 0.
SetupMoreOptions|Więcej opcji
SetupAppearanceHelp|Wybierz kształt, rozmiar i widoczność mapy. Sprawdź podgląd. Możesz zachować obecne ustawienia.
SetupMapHelp|Wybierz, co ma być na mapie. Dodatkowe kolory i etykiety znajdziesz w Więcej opcji.
SetupTrackingHelp|Wybierz, czy mapa ma automatycznie zmieniać przybliżenie podczas ruchu. Możesz zachować obecne ustawienia.
SetupSharingHelp|Grasz sam? Wybierz Gotowe. Udostępnianie jest opcjonalne; połącz się przez Prywatne pozycje.
GuideSwitchApps|Użyj Alt+Tab, aby przejść do SCUM i wrócić. Zaznaczenia w tym oknie pozostaną.
GuideChecklistIntro|Zaznacz każdy wiersz po przeczytaniu i wykonaniu. Potem wybierz Dalej.
GuideHaveNumpad|Mam klawiaturę numeryczną
GuideNoNumpad|Nie mam klawiatury numerycznej lub używam innego klawisza
GuideExtracted|Wybrałem Wyodrębnij wszystkie. Uruchamiam EXE z rozpakowanego folderu, z assets obok.
GuideWindowed|SCUM działa w oknie lub bez ramek. Moja postać jest wczytana.
GuideScumNumpad|W SCUM Copy Location to Num /, czyli dzielenie na klawiaturze numerycznej.
GuideMiniNumpad|MiniMap już używa Num /. Nie muszę tutaj zmieniać ani przechwytywać klawisza.
GuideScumOther|W SCUM wybrałem wolny klawisz dla Copy Location i kliknąłem Zastosuj.
GuideMiniOther|Teraz muszę ustawić TEN SAM KLAWISZ w MiniMap. Używam przycisku poniżej i naciskam wybrany klawisz bez Ctrl, Shift ani Alt.
GuideCloseSettings|Po zamknięciu instrukcji wrócę do postaci SCUM i zamknę czat oraz ekwipunek.
GuideTrackingTest|Potem nacisnę Copy Location, sprawdzę kopiowanie współrzędnych i ruchem postaci sprawdzę ruch znacznika.
GuideDontShow|Nie pokazuj tej instrukcji automatycznie, także po aktualizacjach. Mogę otworzyć ją w Ustawieniach.
GuideUseNumpad|Użyj Num / w MiniMap
FirstTimeGuideTitle|Instrukcja pierwszego uruchomienia
FirstTimeGuideBody|1. Pobierz pełny ZIP z kanału pobierania. Wybierz Wyodrębnij wszystkie i uruchom SkynettMiniMap.exe z rozpakowanego folderu. Pozostaw assets obok EXE. Nie uruchamiaj wewnątrz ZIP. Wymagane są Windows 64-bit i .NET Framework 4.8.\n\n2. Ustaw SCUM w oknie lub oknie bez ramek i wczytaj postać.\n\n3. SCUM: OPTIONS → MOUSE & KEYBOARD → CONTROLS → UI → COPY LOCATION → Num / → APPLY. Num / to DZIELENIE na klawiaturze numerycznej, a nie / na głównej.\n\n4. MiniMap: NumPad / jako pojedynczy klawisz bez Ctrl, Shift ani Alt. Klawisze mapy i czatu też muszą odpowiadać SCUM. Brak klawiatury numerycznej? Wybierz wolny klawisz, zastosuj go w SCUM i ustaw TEN SAM KLAWISZ W SCUM I MINIMAP. Zmiana tylko jednej aplikacji nie wystarczy.\n\n5. Na każdej stronie przytrzymaj Dalej przez sekundę i puść. Ustaw rozmiar, położenie i wygląd oraz ukończ ostatnią stronę. Grupy są opcjonalne.\n\n6. Zamknij ustawienia, czat i ekwipunek. W SCUM naciśnij Copy Location: współrzędne muszą się skopiować. Rusz się i sprawdź ruch znacznika. Domyślnie Home otwiera ustawienia, a ? kreator. Jeśli nadal nie działa, otwórz zgłoszenie z wersją i zdjęciami obu przypisań.
SoloRoute|Trasa solo
OwnRoute|Twoja trasa
RouteList|Trasy
RouteAutoHelp|Trasy przeliczają się po zmianie przystanków. Bursztynowy oznacza solo, turkusowy wspólną trasę. Linie przerywane to bezpośrednie wskazanie bez trasy drogowej. Twoją trasę solo widzą połączone grupy. Podążanie wspólną trasą nie rozsyła jej ponownie.
PrivateGroupNameLabel|Nazwa grupy (opcjonalna przy tworzeniu)
PrivateGroupDelete|Usuń grupę
PrivateGroupDeleteConfirm|Usunąć grupę „{0}” dla wszystkich? Wszyscy członkowie zostaną rozłączeni, a kod przestanie działać.
PrivateGroupDeleted|Grupa usunięta.
MapGpsDestination|Nawiguj tutaj (GPS)
MapSaveLocation|Zapisz miejsce
MapGpsSavedHelp|GPS to cel tymczasowy. Zapisane miejsca pozostają do usunięcia. Przystanki tworzą uporządkowaną trasę.
PrivateGroupRename|Zapisz nazwę grupy
SquadTools|Narzędzia drużyny
PingDanger|Niebezpieczeństwo
PingLoot|Łupy
PingPickup|Odbierzcie mnie
SquadUpdated|Ostatnia aktualizacja
SquadOffline|Rozłączono
SquadFollow|Podążaj za graczem
SquadStopNav|Zatrzymaj nawigację
SquadRoute|Wspólna trasa
SquadAddStop|Dodaj przystanek
SquadUndoStop|Usuń ostatni przystanek
SquadStartRoute|Rozpocznij trasę
SquadPresets|Profile mapy
SquadSavePreset|Zapisz bieżącą mapę
SquadLooting|Zbieranie łupów
SquadDriving|Jazda
SquadExploring|Eksploracja
SquadDelete|Usuń
SquadPingHelp|Sygnały wygasają po 60 sekundach. Przypisz skróty w ustawieniach. Udostępnianie działa tylko podczas połączenia.
SquadRouteHelp|Kliknij mapę prawym przyciskiem, aby dodać przystanki, lub dodaj bieżący cel poniżej. Połączone grupy otrzymają trasę. Kolejny przystanek włącza się w promieniu 40 m.
SetupSharingVisibility|Panel mapy ma osobne przełączniki pozycji, celu GPS, zapisanych miejsc i znaczników śmierci każdego gracza. Zmieniają tylko twój widok; udostępnianie trwa.
SetupDefaultColours|Przywróć domyślne kolory
PlayerWaiting|Oczekiwanie na graczy
PlayerNoLocation|Brak współrzędnych
PlayerDeaths|Znaczniki śmierci
PlayerSaved|Zapisane miejsca
PlayerGps|Cel GPS
PlayerLiveLocation|Bieżąca pozycja
PlayerConnected|Połączono
SharedPlayers|Gracze
PrivatePositionGpsHelp|Aktywny cel GPS jest udostępniany bez zapisywania. Przeniesienie lub wyczyszczenie celu aktualizuje go w grupie.
PoiClustering|Grupuj pobliskie ikony
PrivatePositionMarkersHelp|Udostępnia też grupie do 128 zapisanych punktów i znaczników śmierci. Znaczniki znajomych pokazują właściciela, służą do nawigacji i pozostają tu tymczasowe, tylko do odczytu.
PrivatePositionRouterManual|Automatyczne przekierowanie niedostępne lub wygasło. Przekieruj port TCP na ten komputer ręcznie. Wykrycie IP i konfiguracja zapory nie potwierdzają dostępu z internetu.
PrivatePositionFirewallFailed|Nie można dodać lub zweryfikować reguły Windows. Zezwól aplikacji i portowi TCP ręcznie; zarządzane zasady mogą blokować zmiany.
PrivatePositionFirewallReady|Reguła zapory Windows dla tej aplikacji i portu TCP zweryfikowana.
PrivatePositionLocalAddresses|Karty LAN / VPN / wirtualne
PrivatePositionPublicIpFailed|Nie wykryto publicznego IP. Włącz NAT-PMP lub UPnP w routerze, ponów lub wpisz publiczny WAN IPv4. Przy CGNAT poproś dostawcę o publiczny IP.
PrivatePositionPublicIpHelp|Połączenie równorzędne bez kont i zewnętrznych usług. Wykrywa publiczny IPv4 lokalnego routera przez NAT-PMP lub UPnP. Host żąda tymczasowego mapowania NAT-PMP i reguły Windows dla aplikacji i portu TCP.
PrivatePositionDetectPublicIp|Wykryj publiczny IP
PrivatePositionShareAddress|Adres do udostępnienia (publiczny IP lub VPN)
PrivatePositionConnected|Połączono / nasłuchiwanie
PrivatePositionConnecting|Łączenie…
PrivatePositionPort|Port TCP
PrivatePositionAddress|Adres / IP hosta
PrivatePositionRelay|Prywatna grupa przez przekaźnik
PrivatePositionRelayHelp|Przekierowanie portów nie jest potrzebne. Udostępnij znajomym jeden klucz grupy prywatnie. Przekaźnik nie może odczytać pozycji. Bezpłatna przepustowość jest ograniczona.
PrivateGroupCreate|Utwórz grupę i skopiuj kod
PrivateGroupConnect|Połącz
PrivateGroupLeave|Opuść grupę
PrivateGroupCopy|Kopiuj kod
PrivateGroupCode|Kod grupy
PrivateGroupRotate|Odnów kod
PrivateGroupLabel|Grupa
PrivateGroupOwner|Właściciel
PrivateGroupSurvivor|Ocalały
WizardHoldNext|Przytrzymaj przez 1 sekundę, aby kontynuować
PrivateGroupHelp|Utwórz grupę i udostępnij kod prywatnie albo wklej kod i połącz się. Kod działa, dopóki właściciel go nie odnowi. Możesz należeć do kilku grup. Zapisane kody są chronione na tym koncie Windows; udostępnianie zaczyna się wyłączone. Odnowienie rozłącza grupę i unieważnia stary kod. Twórca musi zachować zapisany profil, aby zachować własność.
PrivatePositionJoin|Dołącz do grupy
PrivatePositionHost|Utwórz grupę
RotateMinimap|Obracaj mapę wraz ze mną
PrivatePositions|Prywatne pozycje
PrivatePositionName|Twoja nazwa
PrivatePositionFolder|Wybierz prywatny folder współdzielony
PrivatePositionKey|Klucz grupy
PrivatePositionNewKey|Utwórz i skopiuj klucz
PrivatePositionInvalid|Połączenie nieudane. Sprawdź adres, port, nazwę, klucz i zezwolenie TCP w zaporze.
PrivatePositionHelp|Host przekazuje znajomym IP, port i klucz. Dołączający wpisują te dane. Wymaga LAN, VPN lub przekierowania portu TCP. Tylko ta sesja; pozycje wygasają po 30 sekundach.
ZoneAssociationInfo|MiniMap jest dostępny w menu Otwórz za pomocą dla plików TSV. Kliknij plik stref TSV prawym przyciskiem, wybierz Otwórz za pomocą → SCUM MiniMap i Zawsze, aby otwierać dwuklikiem. Inne pliki TSV mogą używać innej aplikacji.
EnableZoneTsv|Włącz otwieranie stref TSV
ZoneOpenError|Nie można otworzyć pliku stref: {0}
NameThisMap|Nazwij tę mapę
ZeTypeUnknown|Nieznana
ZeTypeCustom|Własna
ZeTypeGasStation|Stacja paliw
ZeTypeBunker|Bunkier
ZeTypeMilitary|Wojskowa
ZeTypeFaction|Frakcja
ZeTypeTrader|Handlarz
ZeTypeFarm|Farma
ZeTypeTown|Miasteczko
ZeTypeCity|Miasto
ZeMultiHelp|Ctrl+klik zaznacza kilka stref; Shift+klik zaznacza zakres. Kolor i wypełnienie dotyczą wszystkich zaznaczonych stref.
ZeSelectedCount|Zaznaczone strefy: {0}
ZeSelectSameColour|Zaznacz ten sam kolor
ZeAllTypes|Wszystkie typy stref
WpfCollapseSidebar|Zwiń pasek boczny
WpfExpandSidebar|Rozwiń pasek boczny
WpfFollowPlayer|Śledź gracza
WpfMoveControls|Przeciągnij, aby przesunąć elementy sterowania
WpfResizeControls|Przeciągnij, aby zmienić rozmiar elementów sterowania
WpfResetControls|Przywróć rozmiar i położenie elementów sterowania
WpfMoveMinimap|Przesuń minimapę
WpfDragMinimap|Przeciągnij, aby przesunąć minimapę
WpfGettingStarted|MINIMAPA · PIERWSZE KROKI
WpfClearRoute|Usuń trasę
WpfTutorialFilters|Wszystkie kategorie punktów zainteresowania, w tym łowiectwo i wędkarstwo, są początkowo włączone. Wybierz wyświetlane elementy na pasku bocznym.
WpfTestBuild|TEST
FieldGeneral|Ogólne
FieldShortcuts|Skróty
FieldAppearance|Wygląd
FieldTarget|CEL
FieldCalculating|Obliczanie trasy…
FieldNoRoad|Trasa drogowa niedostępna
FieldDirect|Odległość w linii prostej
NavigationNext|Następny przystanek
NavigationRemaining|Pozostała trasa
NavigationEta|Szacowany czas
FieldMinutes|około {0} min
FieldMatchKeys|Dopasuj klawisze do SCUM
FieldToggle|Przełącz
FieldPaused|Automatyczne śledzenie wstrzymane
InstallationAssetsMissing|Brakuje plików instalacji MiniMap lub są uszkodzone. Rozpakuj całe oficjalne archiwum ZIP, w tym folder assets, i uruchom EXE z rozpakowanego folderu.
ZeMoveHelp|Wybierz strefę i przeciągnij jej wnętrze. Cofnij przywraca poprzedni kształt lub położenie.
ZeMoveZone|Przesuń strefę
ZeChangeShape|Zmień kształt
ZeCircle|Koło
ZeSquare|Kwadrat
ZeRectangle|Prostokąt
ZeCircleHelp|Przeciągnij od środka, aby ustawić promień, następnie Dodaj strefę. Kółko: przybliżenie. Środkowy przycisk: przesuwanie.
ZeDrawCircle|Rysuj koło
ZeSelectZone|Wybierz strefę
ZeDrawingHelp|Rysuj i edytuj na mapie docelowej. Kółko: przybliżenie. Środkowy przycisk: przesuwanie. Wielokąt: klikaj narożniki, zakończ dwuklikiem lub Dodaj strefę. Edycja: przeciągaj wierzchołki; Shift+klik na krawędzi dodaje; prawy przycisk usuwa. Prostokąt: przeciągnij.
ZeEditOutline|Edytuj obrys
ZeDrawRectangle|Rysuj prostokąt
ZeDrawPolygon|Rysuj wielokąt
TutorialWelcome|Witaj w SCUM MiniMap
TutorialFilters|Wszystkie warstwy mapy są domyślnie włączone poza polowaniem i wędkarstwem. Wybierz widoczne elementy na pasku bocznym.
TutorialSettings|Naciśnij {0}, aby otworzyć ustawienia. Zmienisz tam język, rozmiar mapy, przezroczystość i przypisania klawiszy.
TutorialMap|Naciśnij {0}, aby otworzyć pełną mapę. Kliknij miejsce lewym przyciskiem, aby ustawić punkt trasy. Użyj kółka myszy do przybliżania i przeciągnij mapę, aby ją przesunąć.
TutorialSearch|Naciśnij {0} i wpisz nazwę miejscowości lub miejsca, na przykład bunkra albo stacji paliw. Wybierz wynik, aby rozpocząć nawigację.
TutorialDeath|Kliknij znacznik śmierci prawym przyciskiem na pełnej mapie, aby usunąć tylko ten znacznik.
TutorialUnbound|Ten skrót nie ma przypisanego klawisza. Otwórz kartę przypisania klawiszy i wybierz klawisz.
ProfileTitle|Profil gracza — {0}
ProfileButton|Profil gracza / zasoby
ProfileOverview|Przegląd
ProfileVehicles|Pojazdy
ProfileFlags|Flagi
ProfileSquad|Drużyna
ProfileView|Zobacz {0}
ProfileTableView|Widok tabeli
ProfileRefreshingAllSquads|Odświeżanie wszystkich drużyn…
ProfileRefreshingFlags|Odświeżanie flag: strona {0}/{1}…
ProfileReportTimeout|SCUM nie skopiował odpowiedniego raportu. Zachowano poprzednie dane.
ProfileUnknownStatus|Status niepodany
ProfileFlagPageImported|Zaimportowano stronę flag {0}/{1}. Użyj Odśwież wszystkie informacje, aby zastąpić pełną listę flag.
AdminCopyId|Kopiuj ID
AdminCopyName|Kopiuj nazwę
AdminCopyCoordinates|Kopiuj współrzędne
AdminCopyDetails|Kopiuj pełne szczegóły
AdminCopyOwner|Kopiuj ID właściciela
AdminCopyCommand|Kopiuj polecenie
AdminCopied|Skopiowano do schowka.
PlayersSort|Sortuj
PlayersSquadSize|Wielkość drużyny
ProfileTileView|Widok kafelków
ProfileImportedMember|Zaimportowany członek drużyny
ProfileObservation|Ostatnia obserwacja: {0}
ProfileAssetCount|{0} ({1})
ProfileNoPopulation|Nie zaimportowano jeszcze zestawienia graczy.
ProfileNoSquad|Brak zaimportowanego raportu drużyny tego gracza.
ProfileInvalid|Schowek nie zawiera pełnej obsługiwanej listy pojazdów ani raportu drużyny lub flag.
ProfileDatabaseVersion|Nieobsługiwana wersja bazy profili administratora.
ProfileRefreshVehicles|Odśwież listę pojazdów
ProfileRefreshSquad|Odśwież drużynę i flagi
ProfileRefreshMissing|Odśwież zapisane dostępne raporty. Brakujące raporty drużyn: {0}. Zachowano poprzednie informacje.
AdminToolsEnabled|Narzędzia administratora
ProfileTeleportVehicle|Teleportuj do pojazdu
ProfileTeleportFlag|Teleportuj do flagi
ProfileRefreshAll|Odśwież wszystkie informacje
ProfileRefreshProgress|Drużyna i flagi {0}/{1}: {2}
ProfileRefreshingSquad|Odświeżanie drużyny i flag z SCUM…
ProfileSquadTimeout|Nie otrzymano nowego raportu drużyny tego gracza. Uruchom SquadInfo i zaimportuj skopiowany raport.
ProfileRefreshingVehicles|Pobieranie skopiowanej listy pojazdów z SCUM…
ProfileVehicleTimeout|SCUM nie skopiował nowej listy pojazdów. Uruchom #ListSpawnedVehicles true i zaimportuj skopiowany wynik w profilu gracza.
ProfileVehiclesSaved|Zapisano listę pojazdów dla {0}. Wybierz gracza i otwórz jego profil, aby zobaczyć posiadane pojazdy.
ProfileHelp|Serwer: {0} · {1}\nZaimportuj skopiowany wynik #ListSpawnedVehicles true lub raport #SquadInfo gracza. Pozycje są zapisanymi odczytami, a nie śledzeniem na żywo.
ProfileIdentity|{0}\nPlatforma: {1}\nID platformy: {2}\n{3}
ProfilePopulation|Zestawienie graczy: {0} · {1}\nSława: {2} · Saldo: {3} · Złoto: {4}
ProfileSummary|Posiadane pojazdy: {0} · posiadane flagi: {1}\n{2}
ProfileExplanation|Raporty są zapisywane lokalnie dla każdego serwera. Odśwież wszystkie informacje, aby zaktualizować pojazdy, członków drużyny i flagi. Alias pojazdu nie potwierdza własności; właściciel offline wymaga zgodnego raportu właściciela flagi. Pozycje pochodzą z datowanych odczytów.
ProfileOwnerAlias|Właściciel / alias
ProfileType|Typ
ProfileCoordinates|Współrzędne
ProfileImported|Zaimportowano
ProfileAllAssets|Pokaż wszystkie zasoby serwera
ProfileShowSelected|Pokaż wybrane na mapie
ProfileClearMarkers|Usuń znaczniki zasobów
ProfileCopySquad|Kopiuj polecenie DumpAllSquadsInfoList
ProfileCopyVehicles|Kopiuj polecenie listy pojazdów
ProfileOwnershipHelp|Tylko jawne ID właściciela wiążą zasoby z graczem. ID bazy danych graczy offline i aliasy STEAMID pozostają oddzielne.
ProfileSquadHeader|Drużyna: {0} ({1}) · zaimportowano {2}
ProfileSquadMember|{0} · ranga {1} · {2}
ProfileMemberDetails|{0}\n{1}\nRanga {2} · {3} w zaimportowanym raporcie
ProfileOpenPlayer|Otwórz profil gracza
ProfileUnresolvedOwner|Nierozpoznane ID bazy {0}
ProfileNoOwner|Brak właściciela
ProfileAlias|{0} / alias {1}
ProfileImportDone|Zaimportowano dane profilu dla {0}. Wybierz pozycję, aby pokazać ją na mapie.
ProfileCopySquadDone|Skopiowano polecenie DumpAllSquadsInfoList. Uruchom je w SCUM i zaimportuj skopiowany raport.
ProfileCopyVehiclesDone|Skopiowano polecenie listy pojazdów. Uruchom je w SCUM i zaimportuj skopiowany raport.
ProfileSelectLocation|Najpierw otwórz Pojazdy lub Flagi i wybierz co najmniej jedną pozycję.
ProfileMarkersCleared|Usunięto znaczniki zasobów.
ProfileAssetMarker|{0} #{1} · {2} · odczyt {3}
ProfileOnline|Online
ProfileOffline|Offline
ProfileVehicle|Pojazd
ProfileFlag|Flaga
PlayersTitle|Gracze — administrator
PlayersHelp|Przed importem wybierz serwer. Odświeżanie wysyła #ListPlayers true. Pozycje są zapisanymi odczytami: po 60 sekundach szare, po 5 minutach ukryte. Przy zmianie gry sprawdź serwer.
PlayersInvalid|Schowek nie zawiera pełnego obsługiwanego wyniku #ListPlayers true.
PlayersChooseServer|Przed importem wpisz i wybierz nazwę serwera.
PlayersDatabaseVersion|Ta wersja bazy graczy nie jest obsługiwana.
PlayersServer|Wybierz serwer
PlayersImport|Importuj ze schowka
PlayersRefresh|Odśwież z gry
PlayersShow|Pokaż graczy na mapie
PlayersName|Postać
PlayersPlatformName|Nazwa na platformie
PlayersState|Status odczytu
PlayersSeen|Ostatnio widziany
PlayersGo|Teleportuj do gracza
PlayersHere|Przenieś gracza tutaj
PlayersDetails|Sława: {0} · Bank: {1} · Złoto: {2} (skopiowane wartości)
PlayersSnapshot|W ostatnim zestawieniu
PlayersHistory|Zapisana historia
PlayersScope|Wybrany serwer: {0}. Sprawdź, czy odpowiada serwerowi, z którym jesteś połączony.
PlayersImported|Zaimportowano {0} graczy do {1}.
PlayersRefreshing|Pobieranie listy graczy…
PlayersNoResponse|Nie otrzymano prawidłowej listy graczy. Sprawdź uprawnienia administratora lub uruchom #ListPlayers true i użyj Importuj ze schowka.
PlayersTeleportSent|Wysłano polecenie teleportacji. Sprawdź wynik w SCUM.
PlayersTeleportStopped|Zatrzymano polecenie teleportacji.
PlayersSearch|Szukaj według nazwy lub ID gracza
SpawnCartModifiers|Modyfikatory
SpawnCartEditModifiers|Edytuj modyfikatory…
SpawnCartModifierTitle|Modyfikatory tworzenia — {0}
SpawnCartModifierSelection|Wybrane przedmioty: {0}
SpawnCartModifierBulkHelp|Zmiany obejmą wszystkie wybrane przedmioty: {0}. Różne wartości pozostaną bez zmian, dopóki nie wybierzesz wartości lub nie odznaczysz modyfikatora.
SpawnCartModifierMixed|Różne — zachowaj wartość każdego przedmiotu
SpawnCartModifierApplied|Zaktualizowano modyfikatory przedmiotów: {0}.
SpawnCartModifierHelp|Zaznacz opcjonalne modyfikatory i wybierz wartości. Odznacz modyfikator, aby użyć domyślnej wartości SCUM. Dozwolone są też własne wartości; modyfikatory dotyczą każdego tworzonego przedmiotu.
SpawnCartModifierValues|Health i Dirtiness: 0–100%. Uses: co najmniej 0. StackCount: co najmniej 1. Weight: kg. Limity Uses i StackCount zależą od przedmiotu w SCUM.
SpawnCartModifierLocation|Location: X=… Y=… Z=…, trzy współrzędne, skopiowany tekst pozycji SCUM lub 17-cyfrowe ID Steam.
SpawnCartModifierApply|Zastosuj
SpawnCartModifierClear|Przywróć domyślne
SpawnCartModifierPairs|Wpisz pary modyfikator/wartość, np. Health 100 Weight 0.5.
SpawnCartModifierUnknown|Nieznany modyfikator tworzenia: {0}.
SpawnCartModifierDuplicate|Każdy modyfikator może wystąpić tylko raz: {0}.
SpawnCartModifierValue|Wpisz prawidłową wartość dla {0}.
SpawnCartModifierLength|Całe polecenie tworzenia musi zmieścić się w 512 znakach.
SpawnCartChatBinding|Klawisz czatu SCUM: {0}. Musi odpowiadać ustawieniom sterowania SCUM.
SpawnCartNotSent|Nie wysłano: {0}
SpawnCartMonitorUnavailable|Monitorowanie klawiatury jest niedostępne. Uruchom MiniMap ponownie przed wysyłaniem poleceń.
SpawnCartCloseChatInventory|Zamknij czat i ekwipunek SCUM przed wysłaniem polecenia administratora.
SpawnCartGameUnavailable|Nie znaleziono okna SCUM. Otwórz grę w oknie lub bez ramek i spróbuj ponownie.
SpawnCartForegroundBlocked|Wróć do SCUM lub MiniMap przed wysłaniem koszyka.
SpawnCartFocusUnavailable|Nie wysłano polecenia. Pozostaw SCUM dostępny i zwolnij klawisze przed ponowną próbą.
SpawnCartNativeInputRejected|Windows nie mógł wysłać wejścia klawiatury: {0}
SpawnCartGroupWeapons|Broń
SpawnCartGroupClothing|Odzież i pancerze
SpawnCartGroupSurvival|Żywność, zdrowie i przetrwanie
SpawnCartGroupCrafting|Wyposażenie i wytwarzanie
SpawnCartGroupVehicles|Części pojazdów
SpawnCartGroupOther|Inne przedmioty
SpawnCartRangedWeapons|Broń dystansowa
SpawnCartMeleeWeapons|Broń do walki wręcz
SpawnCartSaveStopped|Nie udało się zapisać koszyka. Wysyłanie zatrzymano. Zapisany koszyk może nadal zawierać wysłane przedmioty; sprawdź go przed ponownym użyciem.
SpawnCartInvalidCommand|Koszyk zawiera nieprawidłowe polecenie tworzenia przedmiotu.
SpawnCartBusy|Trwa inne polecenie administratora lub MiniMap jest zamykany.
SpawnCartFocusInputChanged|Wysyłanie zatrzymano, ponieważ zmienił się fokus SCUM lub fizyczne wejście.
SpawnCartClipboardChanged|Wysyłanie zatrzymano, ponieważ zmieniła się zawartość schowka.
SpawnCartCleanupStopped|Polecenie wysłano, ale zamykanie czatu zostało przerwane. Zamknij czat SCUM przed kontynuacją.
SpawnCartBeforeSubmitStopped|Polecenie zatrzymano przed wysłaniem. Sprawdź i zamknij czat SCUM przed kontynuacją.
SpawnCartException|Wysyłanie polecenia zatrzymano: {0}
SpawnCartSubmittedStatus|Koszyk: wysłano {0}/{1} poleceń. Sprawdź wynik w SCUM.
SpawnCartSendingHelp|Pozostaw SCUM na pierwszym planie i zwolnij wszystkie klawisze. Dowolny klawisz zatrzymuje wysyłanie.
SpawnCartTitle|Tworzenie przedmiotów
SpawnCartOpen|Tworzenie przedmiotów / koszyk…
SpawnCartHelp|Wyszukaj przedmioty SCUM Database, wybierz ilości i wyślij koszyk do czatu SCUM. Przedmioty pojawiają się obok twojej postaci administratora.
SpawnCartSearch|Szukaj nazwy lub ID przedmiotu
SpawnCartCompatibility|Powiązane z
SpawnCartExpandGroups|Rozwiń wszystko
SpawnCartCollapseGroups|Zwiń wszystko
SpawnCartClearSearch|Wyczyść wyszukiwanie
SpawnCartSearchHint|Spróbuj AK47 lub AK47 celownik. Strzałkami sekcji rozwijaj lub zwijaj wyniki.
SpawnCartSelectedItems|Wybrano: {0}
SpawnCartEmptyResults|Brak pasujących przedmiotów. Zmień wyszukiwanie lub kategorię.
SpawnCartCompatibilityFor|Do {0}
SpawnCartCompatibilityAdapter|Do {0} (może wymagać adaptera)
SpawnCartCategory|Kategoria
SpawnCartAll|Wszystkie kategorie
SpawnCartName|Przedmiot
SpawnCartId|ID tworzenia
SpawnCartQuantity|Ilość
SpawnCartAdd|Dodaj wybrane
SpawnCartContents|Koszyk przedmiotów
SpawnCartRemove|Usuń wybrane
SpawnCartClear|Wyczyść koszyk
SpawnCartSave|Zapisz zestaw…
SpawnCartLoad|Wczytaj zestaw…
SpawnCartSend|Wyślij koszyk
SpawnCartStop|Zatrzymaj wysyłanie
SpawnCartSource|ID przedmiotów: SCUM Database
SpawnCartMatches|{0} z {1} przedmiotów
SpawnCartSummary|Typy przedmiotów: {0} / przedmioty: {1}
SpawnCartEmpty|Najpierw dodaj przedmioty do koszyka.
SpawnCartLimits|Maksymalnie 100 typów, po 100 sztuk każdego przedmiotu i 1000 sztuk w koszyku.
SpawnCartInvalid|Koszyk zawiera nieznane ID przedmiotu lub nieprawidłową ilość.
SpawnCartLimitExceeded|Przekroczono by limity koszyka.
SpawnCartFileError|Nie udało się wczytać lub zapisać koszyka: {0}
SpawnCartCatalogError|Nie udało się wczytać katalogu przedmiotów: {0}
SpawnCartSending|Wysyłanie polecenia {0} z {1}…
SpawnCartSubmitted|Wysłano {0} poleceń. Sprawdź wynik w SCUM.
SpawnCartInterrupted|Wysłano {0} z {1} poleceń. Niewysłane przedmioty pozostają w koszyku. {2}
SpawnCartTarget|Miejsce tworzenia
SpawnCartTargetSelf|Obok twojej postaci administratora
SpawnCartPreview|Podgląd polecenia
SpawnCartSelect|Wybierz co najmniej jeden przedmiot. Kliknij dwukrotnie, aby go dodać.
SpawnCartUnconfirmed|Wysłanie polecenia nie potwierdza utworzenia przedmiotów przez serwer.
SpawnCartKitFilter|Zestawy przedmiotów SCUM (*.tsv)|*.tsv
AdminPanelSettings|Panel poleceń administratora
UpdateInstallTitle|Zainstaluj aktualizację SCUM MiniMap
UpdateInstallPrompt|Znaleziono uruchomiony SCUM MiniMap v{0}:\n{1}\n\nZainstalować tam v{2}? MiniMap zamknie się, zaktualizuje i uruchomi ponownie. SCUM pozostanie uruchomiony.
UpdateInstallChoose|Wybierz zainstalowany plik wykonywalny SCUM MiniMap
UpdateInstallAmbiguous|Uruchomiono więcej niż jedną instalację MiniMap. Wybierz plik wykonywalny do aktualizacji.
AdminRailTeleport|Teleportacja
ClearDeathMarkers|Usuń znaczniki śmierci
NoteDeathMarkersRemoved|Usunięto znaczniki śmierci: {0}.
AdminOpenFromSettings|Otwórz panel administratora ponownie w Ustawieniach.
SpawnCartKitName|Nazwa zestawu
SpawnCartSavedKits|Zapisane zestawy
SpawnCartKitStorageHint|Zapisane zestawy pozostają dostępne w tym profilu MiniMap.
SpawnCartNoSavedKits|Brak zapisanych zestawów. Zapisz koszyk lub zaimportuj poprzedni zestaw.
SpawnCartKitDetails|Polecenia: {0} · przedmioty: {1} · aktualizacja: {2}
SpawnCartKitNameInvalid|Wpisz nazwę zestawu o długości 1–80 znaków, bez znaków sterujących.
SpawnCartKitExists|Zestaw „{0}” już istnieje.
SpawnCartKitReplace|Zastąpić zestaw „{0}” bieżącym koszykiem?
SpawnCartKitSaved|Zapisano zestaw „{0}”.
SpawnCartKitLoaded|Wczytano zestaw „{0}”.
SpawnCartKitImport|Importuj stary zestaw
SpawnCartKitImported|Zaimportowano zestaw „{0}”.
SpawnCartKitMissing|Ten zapisany zestaw nie jest już dostępny.
SpawnCartDatabaseVersion|Baza zestawów została utworzona przez nowszą wersję MiniMap.
SpawnCartCancelClose|Zatrzymaj wysyłanie przed zamknięciem tego okna.
SpawnCartClearConfirm|Usunąć wszystkie przedmioty z koszyka?
PoiSectionSummary|{0} (kategorie: {1}, miejsca: {2})
PoiSummary|{0}/{1} | {2:N0} POI | siedliska: {3}
GuideFishingSource|Źródło danych o łowieniu
GuideHuntingSource|Źródło danych o polowaniu
GuideRetrieved|Pobrano: {0}
UpdateReadyRestarting|Pobrane dane zweryfikowano. MiniMap zamknie się, zastosuje aktualizację i uruchomi ponownie.
CustomMapRecovery|Nie udało się wczytać własnej mapy. Używana jest mapa domyślna; zachowano twój plik.
WizardKeybindsSkipNotice|Pomiń ten krok tylko wtedy, gdy pokazane klawisze już odpowiadają SCUM. Nowe instalacje używają Num / (dzielenie na klawiaturze numerycznej) do kopiowania pozycji, bez modyfikatora. Bez klawiatury numerycznej wybierz tutaj własny klawisz.
WizardKeybindsDesc|Dopasuj Mapę, Czat i Kopiowanie pozycji SCUM do sterowania w grze. Następnie wybierz pojedynczy fizyczny klawisz dla każdej czynności MiniMap lub pozostaw ją bez przypisania.
WizardKeybindsTitle|KALIBRACJA FIZYCZNYCH KLAWISZY
WizardKeyCopy|Klawisz kopiowania współrzędnych
WizardKeyModifier|Modyfikator kopiowania
WizardKeyChat|Klawisz czatu SCUM
WizardKeyMap|Przełączanie mapy SCUM
WizardPressKeyPrompt|NACIŚNIJ TERAZ FIZYCZNY KLAWISZ…
WizardBtnResetDefaults|PRZYWRÓĆ DOMYŚLNE
WizardBtnCapture|PRZECHWYĆ
WizardBtnSkip|POMIŃ KROK >>
WizardTabKeybinds|03. KLAWISZE
WizardTitle|Skynett MiniMap — przewodnik terenowy
WizardBranding|SKYNETT — ROZPOZNANIE TAKTYCZNE // ASYSTENT HUD
WizardTagline|Telemetria wyspy SCUM, nawigacja i radar taktyczny
WizardTabOverview|01. PRZEGLĄD
WizardTabControls|02. STEROWANIE
WizardTabSafety|04. BEZPIECZEŃSTWO
WizardTabFeatures|05. STREFY I POI
WizardStepIndicator|Krok {0} z {1}
WizardBtnPrev|< POPRZEDNI
WizardBtnNext|NASTĘPNY >
WizardBtnFinish|URUCHOM HUD
OpenStartupGuide|Pierwsze kroki…
WzOvTitle1|Bez wstrzykiwania DLL
WzOvDesc1|Skynett MiniMap działa całkowicie poza procesem SCUM, bez modyfikacji plików gry, przechwytywania pamięci ani wstrzykiwania kodu.
WzOvTitle2|Zalecany tryb wyświetlania
WzOvDesc2|Ustaw SCUM w trybie okna bez ramek lub pełnego ekranu bez ramek, aby poprawnie wyświetlać nakładkę i swobodnie używać myszy.
WzOvTitle4|Nawigacja drogowa
WzOvDesc4|Wbudowany algorytm A* wyznacza trasy po sieci dróg SCUM i oblicza pozostałą odległość w milisekundach.
WzKeyMTitle|Klawisz mapy SCUM
WzKeyMDesc|Otwórz pełną mapę skonfigurowanym klawiszem. Kółko przybliża, przeciąganie przesuwa, a prawy przycisk otwiera opcje punktu trasy. Zamknij klawiszem mapy lub Esc.
WzKeyHomeTitle|Ustawienia
WzKeyHomeDesc|Otwórz panel konfiguracji, aby zmienić przezroczystość, rozmiar, siatkę, filtry kategorii POI i przypisania klawiszy gry.
WzKeyInsertTitle|Dodaj punkt trasy
WzKeyInsertDesc|Zapisz bieżącą pozycję jako nazwany punkt trasy na tej mapie.
WzKeyDeleteTitle|Wyszukiwanie
WzKeyDeleteDesc|Otwórz katalog miejsc, aby szybko znaleźć stacje benzynowe, bunkry i miasta oraz wyznaczyć trasę drogową.
WzKeyEndTitle|Pokaż / ukryj mapę
WzKeyEndDesc|Natychmiast włącz lub wyłącz minimapę bez przerywania synchronizacji współrzędnych w tle.
WzKeyCopyTitle|Ręczna synchronizacja współrzędnych
WzKeyCopyDesc|Ręcznie pobierz współrzędne ze schowka, gdy automatyczny odczyt jest opóźniony lub wstrzymany.
WzSafeTitle1|Klawisz kopiowania współrzędnych
WzSafeDesc1|MiniMap pobiera współrzędne przez wbudowane polecenie kopiowania pozycji SCUM i odczytuje je ze schowka.
WzSafeTitle2|Otwarty czat: automatyczne kopiowanie wstrzymane.
WzSafeDesc2|Kopiowanie współrzędnych jest wstrzymywane podczas aktywnego czatu, wykrytego ekwipunku i utraty fokusu SCUM.
WzSafeTitle3|Kreator przypisywania klawiszy SCUM
WzSafeDesc3|Przypisania Mapy, Czatu i Kopiowania pozycji w MiniMap muszą odpowiadać ustawieniom SCUM.
WzFeatTitle1|Filtry POI ScumMap
WzFeatDesc1|Wybierz widoczne kategorie POI, w tym posterunki policji, bunkry, lotniska i handlarzy. Filtry łowienia i polowania obejmują też granice siedlisk.
WzFeatTitle2|Kreator stref
WzFeatDesc2|Grasz na prywatnym serwerze z własnymi strefami bezpiecznymi lub sektorami PvP? Użyj zrzutu mapy, aby wykryć i skalibrować strefy serwera.
WzFeatTitle3|Zapisane punkty trasy
WzFeatDesc3|Strefy i punkty trasy są zapisywane oddzielnie dla każdej mapy i zachowywane po ponownym uruchomieniu.
AddCustomWaypointHere|Dodaj własny punkt trasy tutaj
AllZonesDeletedDone|Usunięto wszystkie własne strefy i punkty trasy.
ApplyClose|Zastosuj i zamknij
AutoZoomMax|Maksymalne automatyczne przybliżenie (postój)
AutoZoomMin|Minimalne automatyczne przybliżenie (duża prędkość)
AutoZoomSpeed|Automatyczne przybliżenie według prędkości
AwaitingPosition|Oczekiwanie na pozycję…
Bear|Niedźwiedź
BiomeForest|Las
BiomeMeadow|Łąka
BiomeMediterranean|Biom śródziemnomorski
BiomeMountain|Góry
Boar|Dzik
Cancel|Anuluj
CheckUpdatesGitHub|Sprawdź aktualizacje (GitHub)…
Chicken|Kura
ClearAllCustomZones|Usuń wszystkie własne strefy i punkty trasy…
ClearAllZonesConfirm|Trwale usunąć wszystkie własne strefy i punkty trasy ({0})?
ClearWaypoint|Usuń punkt trasy
Close|Zamknij
ConfigurePoiFilters|Wybierz ikony mapy
CustomWaypointsDescription|Osobiste znaczniki zapisane dla bieżącej mapy.
CustomZonesDescription|Pokaż zapisane kontury własnych stref. Twórz lub importuj strefy za pomocą kreatora.
DataFolderOpenError|Nie udało się otworzyć folderu danych: {0}
Deer|Jeleń
DeleteWaypoint|Usuń własny punkt trasy
DeleteWaypointNamed|Usuń punkt trasy „{0}”
DeselectAll|Odznacz wszystko
DiagCopyActive|Automatyczne kopiowanie aktywne.
Done|Gotowe
Donkey|Osioł
FactionPOI| / Punkt frakcji
FadeEdges|Wygaszaj krawędzie
FullMap|Pełna mapa
Goat|Koza
GpsMarker|Znacznik GPS
GridBorders|Linie siatki
GridLabels|Numery siatki / etykiety sektorów (D4, C3 itd.)
GridOpacity|Przezroczystość siatki: {0}%
HabitatGuide|Przewodnik po siedliskach
HabitatHelp|Wybierz gatunki w Łowieniu lub Polowaniu, aby pokazać granice. Kontury ryb odpowiadają opublikowanym obszarom pojawiania się, a nie liniom brzegowym. Obszary łowieckie opierają się na przewodniku biomów ScumMap; przypisania gatunków różnią się między źródłami i nie gwarantują pojawiania się. Aktywność, głębokość, pora, przynęta i populacja serwera wpływają na dostępność. Dotychczasowe POI łowienia i polowania pozostają oddzielne.
HeaderMapZones|SKYNETT / STREFY MAPY
HeaderSettings|SKYNETT / USTAWIENIA v{0}
HeaderWaypoint|SKYNETT / PUNKT TRASY
Horse|Koń
HuntingLegendNote|* Zmutowane bestie rzadko pojawiają się w tych biomach
JoinDiscord|Dołącz do społeczności Discord…
KeyRebindingWizard|Kreator klawiszy SCUM i MiniMap…
KeyWizardCancel|Anuluj
KeyWizardCaptureCancelled|Anulowano przechwytywanie klawisza.
KeyWizardCaptured|Przechwycono {0}.
KeyWizardChatKey|Klawisz czatu SCUM
KeyWizardCopyKey|Klawisz kopiowania współrzędnych
KeyWizardCopyModifier|Modyfikator kopiowania współrzędnych
KeyWizardCopyPairInvalid|Modyfikator i klawisz kopiowania współrzędnych muszą być prawidłowe i różne.
KeyWizardDifferentKeys|Mapa, czat, kopiowanie i każdy skrót MiniMap muszą używać różnych klawiszy.
KeyWizardIntro|Skonfiguruj wszystkie przypisania SCUM i MiniMap. Mapa, Czat i Kopiowanie pozycji SCUM muszą odpowiadać sterowaniu w grze. Czynności MiniMap używają pojedynczego fizycznego klawisza; Usuń przypisanie wyłącza skrót.
KeyWizardMapKey|Klawisz mapy SCUM
KeyWizardModifierRequired|Naciśnij klawisz modyfikatora (Ctrl, Alt, Shift lub Windows).
KeyWizardNonModifier|Naciśnij klawisz niebędący modyfikatorem.
KeyWizardPressChat|Naciśnij teraz klawisz czatu SCUM. Escape anuluje przechwytywanie.
KeyWizardPressCopyKey|Naciśnij teraz klawisz kopiowania współrzędnych SCUM. Escape anuluje przechwytywanie.
KeyWizardPressCopyModifier|Naciśnij teraz modyfikator kopiowania współrzędnych SCUM. Escape anuluje przechwytywanie.
KeyWizardPressKey|Naciśnij klawisz…
KeyWizardPressMap|Naciśnij teraz klawisz mapy SCUM. Escape anuluje przechwytywanie.
KeyWizardReady|Wybierz pole i naciśnij odpowiedni klawisz.
KeyWizardSave|Zapisz
KeyWizardSaved|Zapisano przypisania klawiszy SCUM i MiniMap.
KeyWizardTitle|Kreator klawiszy SCUM i MiniMap
KeyNone|Brak
KeyWizardNoModifier|Pojedynczy klawisz bez modyfikatora
Language|Język
LocApproaching|{0} — Zbliżasz się do {1}
LocBetween|{0} — Między {1} a {2}
LocLeaving|{0} — Opuszczasz {1}
LocLeavingApproaching|{0} — Opuszczasz {1}, zbliżasz się do {2}
LocNear|{0} — W pobliżu {1}
LocationBarPosition|Położenie paska informacji
MapHeight|Wysokość mapy (piksele)
MapOpacity|Przezroczystość (%)
MapWidth|Szerokość mapy (piksele)
MapZonesScreenshot|Kreator stref (import zrzutu)…
MaxZoom|Maksymalne ręczne przybliżenie
NoCustomZonesToDelete|Brak zapisanych własnych stref i punktów trasy.
NoMatches|Brak wyników. Spróbuj „faction”, nazwy miejsca lub sektora od D4 do Z0.
NoteAdsActive|Celowanie (ADS): automatyczne kopiowanie wstrzymane.
NoteAutoCopyActive|Automatyczne kopiowanie aktywne.
NoteAutoWaiting|Automatyczne śledzenie oczekuje na SCUM.
NoteChatClosed|Czat zamknięty: automatyczne kopiowanie może zostać wznowione.
NoteChatMonitorUnavailable|Monitorowanie klawisza czatu niedostępne; automatyczne kopiowanie wyłączone.
NoteChatOpen|Czat otwarty: automatyczne kopiowanie wstrzymane.
NoteClipboardBusy|Schowek zajęty; ponawianie.
NoteCoordReceived|Otrzymano automatyczną odpowiedź ze współrzędnymi.
NoteCopyCancelled|Kopiowanie anulowane przez zmianę fokusu lub wejście użytkownika; wznowienie po zwykłym interwale.
NoteCopyErrorRetry|{0} Automatyczne śledzenie spróbuje ponownie za 5 sekund.
NoteFullMapOff|Przywrócono widok minimapy
NoteFullMapOn|Widok pełnej mapy aktywny
NoteNoCoordsAttempt|Nie otrzymano nowych współrzędnych (próba {0}).
NoteNoCoordsRetry|Brak odpowiedzi ze współrzędnymi. Automatyczne śledzenie spróbuje ponownie za {0} sekund.
NoteCaptureCopyKey|Brak odpowiedzi ze współrzędnymi. Przechwyć fizyczny klawisz kopiowania pozycji w Ustawieniach MiniMap lub przypisz F11 w SCUM i MiniMap.
NoteSettingsSaveFail|Nie udało się zapisać ustawień wyglądu.
NoteWaitingForeground|Oczekiwanie na SCUM na pierwszym planie.
NoteWaitingUserKeys|Oczekiwanie na zakończenie wejścia użytkownika lub zwolnienie klawiszy.
NoteWaypointArrived|Dotarto do {0}. Usunięto punkt trasy.
NoteWaypointCleared|Usunięto wyszukany punkt trasy.
NoteWaypointSet|Ustawiono punkt trasy: {0}. Naciśnij {1}, aby wyszukać lub usunąć.
NoteZonesLoadFail|Nie udało się wczytać zapisanych stref.
OpacityLabel|PRZEZROCZYSTOŚĆ
OpenDataFolder|Otwórz folder danych
Place| / Miejsce
PoiFilterTitle|Kategorie i filtry POI ScumMap
PosAbove|Nad
PosBelow|Pod
PositionInterval|Interwał aktualizacji pozycji (ms)
Rabbit|Królik
RemoveWaypoint|Usuń punkt trasy
ResetDefaultMap|Przywróć domyślną mapę
ResetDefaults|Domyślne
RoadSuffix| (droga)
PlayerMarkerColor|Kolor stożka widzenia i znacznika…
RouteGuidanceColor|Kolor prowadzenia po trasie…
RouteHistoryColor|Kolor historii trasy…
ZoneFillColour|Kolor wypełnienia
ZoneFillOpacity|Krycie wypełnienia
SearchFilterPrompt|Filtruj kategorie (nazwa, sekcja lub typ):
SearchHint|Spróbuj: faction, lotnisko, bunkier C3, grid D4. Literówki są obsługiwane.
SearchPlaceOrGrid|Szukaj miejsca lub sektora…
SearchResults|Wyniki: {0}. Enter rozpoczyna nawigację; Esc wraca.
SearchTitle|Szukaj miejsca lub sektora
SecAppearance|Wygląd
SecGeneral|Ogólne i przewodniki
SecMapZones|Mapa i strefy
SecToolsShortcuts|Narzędzia i skróty
SecTrackingZoom|Śledzenie i przybliżenie
SecondsAgo|{0} s temu
SectorCentre| / Środek sektora
SelectAll|Zaznacz wszystko
SetWaypoint|Ustaw punkt trasy
Settings|Ustawienia
ShowCompass|Pokaż kierunki kompasu (N, S, E, W)
ShowCustomWaypoints|Pokaż własne punkty trasy
ShowHeading|Pokaż stożek / strzałkę kierunku gracza
ShowSavedZones|Pokaż własne strefy mapy
ShowScumMap|Pokaż ikony mapy
ShowStatus|Pokaż pasek informacji o pozycji
ShowZoneLabels|Pokaż nazwy POI
SidebarLayerGrid|Linie siatki
SidebarLayerLabels|Etykiety stref
SidebarLayerWaypoints|Własne punkty trasy
SidebarLayerZones|Własne strefy mapy
SidebarNoCustomLayers|Nie wczytano własnych warstw
SidebarLayers|WARSTWY MAPY
SidebarSearchBtn|Szukaj ({0})
SidebarSettings|Ustawienia ({0})
SidebarTitle|MAPA TAKTYCZNA
SidebarTools|SZYBKIE NARZĘDZIA
SidebarZoneEditor|Kreator stref…
SmartLabelLod|Ukryj szczegółowe etykiety po oddaleniu
StatusSummary|Automatyczne żądania: {0} | Odpowiedzi: {1}
TrayCheckUpdates|Sprawdź aktualizacje…
TrayExit|Zakończ
TrayShowHide|Pokaż / ukryj mapę
UpdateAvailableDialogMsg|Dostępna jest nowsza wersja SCUM MiniMap.\n\nBieżąca wersja: v{0}\nNajnowsza na GitHub: v{1}\nPobieranie: {2}\n\nPobrać aktualizację teraz?
UpdateAvailableDialogTitle|Dostępna aktualizacja — SkynettMiniMap
UpdateAvailableNote|Dostępna aktualizacja: v{0}! Naciśnij {1}, aby poznać szczegóły.
UpdateCheckTitle|Sprawdzanie aktualizacji
UpdateConnectError|Nie udało się pobrać informacji o wydaniu GitHub. Sprawdź połączenie lub spróbuj później.
UpdateDownloadComplete|Aktualizację pobrano i zweryfikowano. MiniMap uruchamia się ponownie.
UpdateDownloading|Pobieranie aktualizacji z GitHub…
UpdateFailedMsg|Sprawdzanie aktualizacji nie powiodło się: {0}
UpdateRateLimited|GitHub tymczasowo ogranicza sprawdzanie aktualizacji lub jest niedostępny. Spróbuj ponownie po {0}.
UpdateUpToDateMsg|Brak nowszego stabilnego wydania.\n\nZainstalowano: v{0}\nNajnowsze na GitHub: v{1}
UpdateUpToDateTitle|Wersja aktualna
UpdateVersionUnknown|GitHub nie podał prawidłowych informacji o wydaniu. Spróbuj ponownie później.
WaitingForCoordinates|Oczekiwanie na współrzędne
WaypointDeleteConfirm|Usunąć punkt trasy „{0}”?
WaypointDeleted|Usunięto punkt trasy: {0}
WaypointNamePrompt|Wpisz nazwę tego punktu trasy:
WaypointNameTitle|Zapisz punkt trasy
WaypointSaved|Zapisano punkt trasy: {0}
Wolf|Wilk
ZeAddZone|Dodaj strefę
ZeAligningLocally|Lokalne wyrównywanie mapy i wykrywanie kolorowych konturów stref…
ZeApplyName|Zastosuj nazwę
ZeAutoFailedKept|Automatyczne wykrywanie nie powiodło się; zachowano istniejące strefy.
ZeClearDraft|Wyczyść szkic
ZeCouldNotImportAuto|Nie udało się automatycznie zaimportować: {0}
ZeDeleteAll|Usuń wszystko
ZeDeleteAllConfirm|Czy na pewno usunąć wszystkie strefy? Po zapisaniu nie można tego cofnąć.
ZeDeleteAllTitle|Usuń wszystkie strefy
ZeDeleteSelected|Usuń wybrane
ZeDeselect|Odznacz
ZeDetectedOutlinesPrompt|Wykryte kontury: {0}. Kliknij strefę, wpisz jej nazwę i wybierz Zastosuj nazwę (lub Nazwij ten kolor). Zapisz strefy zastępuje bieżącą listę.
ZeDraftWarning|Dodaj bieżący kontur jako strefę lub cofnij jego punkty przed zapisaniem.
ZeDuplicate|Duplikuj
ZeExport|Eksportuj…
ZeExportSuccess|Wyeksportowano {0} stref do {1}.
ZeExportTitle|Eksportuj strefy mapy
ZeFillZones|Wypełnij strefy
ZeFilterZones|Filtr:
ZeImportFile|Importuj plik…
ZeImportSuccess|Zaimportowano {0} stref z {1}.
ZeImportTitle|Importuj plik stref mapy
ZeInitialPrompt|Otwórz zrzut ekranu, aby automatycznie wyrównać mapę i wykryć kolorowe strefy. Następnie nazwij każdą strefę lub grupę kolorów.
ZeLabelLengthWarning|Użyj etykiety o długości 1–80 znaków.
ZeManualFallback|Tryb ręczny
ZeAllLayers|Wszystkie warstwy
ZeDeleteLayer|Usuń warstwę
ZeDeleteLayerConfirm|Trwale usunąć warstwę „{0}” i wszystkie jej strefy ({1})?
ZeImportActionTitle|Import stref — tryb warstwy
ZeImportAppend|Dodaj do warstwy „{0}”
ZeImportModePrompt|Wybierz sposób importu stref z „{0}”:
ZeImportNewLayer|Utwórz jako nową warstwę
ZeImportReplace|Zastąp warstwę „{0}”
ZeLayer|Warstwa:
ZeManualStep0|Kliknij punkt orientacyjny 1 na zrzucie (po lewej).
ZeManualStep1|Kliknij TEN SAM punkt na mapie odniesienia (po prawej).
ZeManualStep2|Kliknij punkt orientacyjny 2 na zrzucie, daleko w obu kierunkach.
ZeManualStep3|Kliknij punkt orientacyjny 2 na mapie odniesienia.
ZeManualStep4|Wyrównano. Kliknij kontur strefy na zrzucie, wybierz nazwę i kolor, a następnie Dodaj strefę. Sprawdź kontur po prawej.
ZeMapScreenshotsFilter|Zrzuty mapy|*.png;*.jpg;*.jpeg;*.bmp
ZeMoveDown|W dół
ZeMoveUp|W górę
ZeNameColour|Nazwij ten kolor
ZeNewLayer|+ Warstwa
ZeNewLayerPrompt|Wpisz nazwę nowej warstwy stref:
ZeNewLayerTitle|Nowa warstwa stref
ZeNewZone|Nowa strefa
ZeNoUsableZonesFound|W wybranym pliku nie znaleziono użytecznych konturów stref.
ZeNoZonesToExport|Brak stref do eksportu.
ZeOpenScreenshot|Otwórz zrzut
ZeOutsideMapWarning|Punkt wypada poza obrazem wyspy. Sprawdź wyrównanie.
ZePointsRequirementWarning|Najpierw wyrównaj zrzut i kliknij co najmniej trzy punkty konturu.
ZeRecolorGroup|Zmień kolor grupy
ZeSaveZones|Zapisz strefy
ZeSaveZonesError|Nie udało się zapisać stref: {0}
ZeSelectScreenshotTitle|Wybierz zrzut mapy gry z północą u góry
ZeUndoPoint|Cofnij punkt
ZeZoneColour|Kolor strefy
ZeZonesMaxWarning|Użyj nazwy o długości 1–80 znaków. Obsługiwanych jest maksymalnie 500 stref.
ZoneChime|Dźwięk wejścia do strefy
ZoneEditorTitle|Kreator stref
ZoneLabelSize|Rozmiar etykiet stref
ZoomStep|Krok przybliżenia (%)
ResetDefaultMapConfirm|Przywrócić wbudowany obraz mapy? Własna mapa zostanie usunięta.
ResetDefaultMapDone|Przywrócono domyślną mapę. Uruchom aplikację ponownie, aby zastosować zmianę.
MapResetFailed|Nie udało się przywrócić mapy: {0}
WizardLayoutTitle|Zacznij od kompaktowej minimapy
WizardLayoutDescription|Nowa instalacja zaczyna od 240 × 240 w prawym górnym rogu. Przejdź na pulpit, aby przesunąć mapę lub zmienić jej rozmiar. Ustawienia > Wygląd określają dokładne wymiary. Zmiany zapisują się automatycznie.
WizardAdjustLayout|Przesuń i zmień rozmiar minimapy…
ShortcutTitle|Ustaw skrót: {0}
ShortcutPrompt|Naciśnij pojedynczy fizyczny klawisz bez Ctrl, Alt, Shift lub Windows. Escape anuluje.
ShortcutSingleKey|Użyj pojedynczego klawisza bez Ctrl, Alt, Shift lub Windows.
ShortcutRetry|Naciśnij klawisz ponownie, aby przechwycić jego fizyczną pozycję.
ShortcutCancel|Escape anuluje.
ShortcutHeading|Skróty MiniMap (pojedyncze klawisze)
ControlZoomIn|Przybliż
ControlZoomOut|Oddal
ControlDeathMarker|Dodaj znacznik śmierci
ControlResetZoom|Przywróć przybliżenie
ControlAdminCommands|Polecenia administratora
AdminRelativeTeleportTitle|Teleportacja z ostatnich współrzędnych
AdminFloatingTitle|Sterowanie administratora
AdminFloatingHelp|Wybierz polecenie lub krok teleportacji. Stany pokazują ostatnie żądania; sprawdź wynik w SCUM. Przed wysłaniem zamknij czat i ekwipunek.
AdminFloatingHint|Rozwiń panel, aby zarządzać poleceniami lub używać teleportacji kierunkowej.
AdminExpand|Rozwiń sterowanie administratora
AdminCollapse|Zwiń sterowanie administratora
AdminCommandsSection|Polecenia
AdminTeleportSection|Teleportacja kierunkowa
AdminAddCommand|Dodaj polecenie
AdminEditCommand|Edytuj
AdminRemoveCommand|Usuń
AdminRailTitle|Administrator
AdminRelativeTeleportHelp|Lewo/prawo/przód/tył korzystają z ostatniego zapisanego kierunku patrzenia. Góra/dół zmieniają wysokość.
AdminTeleportDistance|Odległość (metry)
AdminTeleportUp|W górę
AdminTeleportDown|W dół
AdminTeleportLeft|W lewo
AdminTeleportRight|W prawo
AdminTeleportForward|Do przodu
AdminTeleportBack|Do tyłu
AdminTeleportSend|Wyślij teleportację
AdminTeleportPreview|Podgląd polecenia
AdminTeleportSource|Ostatnie współrzędne: X {0}  Y {1}  Z {2}  |  {3}
AdminTeleportMissing|Oczekiwanie na współrzędne z SCUM.
AdminTeleportWaiting|Oczekiwanie na nowe współrzędne po ostatniej teleportacji.
AdminTeleportInvalid|Wybierz prawidłowy kierunek i odległość dla ostatnich współrzędnych.
AdminTeleportBusy|Trwa wysyłanie polecenia administratora.
AdminTeleportMenu|Teleportacja kierunkowa…
ControlUnbind|Usuń przypisanie
ControlResetDefaults|Przywróć domyślne
ControlAppHeading|Sterowanie MiniMap
ControlGameHeading|Sterowanie SCUM
ControlUnbound|Nieprzypisany
ShortcutPhysicalHelp|Każdą czynność MiniMap można przypisać ponownie lub wyłączyć. Skróty używają fizycznych klawiszy; zachowaj ten sam stan Num Lock dla klawiatury numerycznej.
ShortcutReserved|Wybierz klawisz niezarezerwowany dla gry lub okien dialogowych.
ShortcutScumConflict|Ten klawisz jest używany przez Mapę, Czat lub Kopiowanie pozycji SCUM.
ShortcutConflict|Ten klawisz jest przypisany do innego skrótu MiniMap.
HistoryDuration|Czas historii (minuty)
HistoryClear|Wyczyść historię trasy
HistorySecondsAgo|{0} s temu
HistoryMinutesAgo|{0} min temu
HistoryHoursAgo|{0} godz. temu
HistoryDaysAgo|{0} dni temu
CopyReminderTitle|Sprawdź klawisz kopiowania współrzędnych
CopyReminderBody|Domyślnie: Num / (dzielenie na klawiaturze numerycznej). Ustaw ten sam klawisz kopiowania pozycji w SCUM. Jeśli używasz innego klawisza lub nie masz klawiatury numerycznej, przechwyć go w Ustawieniach MiniMap.
CopyReminderHide|Nie pokazuj ponownie
CopyReminderOkay|OK
VoiceHeading|Nawigacja głosowa (w trakcie prac)
VoiceEnable|Włącz nawigację głosową
VoiceName|Głos
VoiceVolume|Głośność głosu (%)
VoicePreview|Odsłuchaj głos
VoiceHelp|Dodaj foldery głosów z tymi samymi 12 nazwami plików MP3 do folderu voice-navigation w danych aplikacji i ponownie otwórz Ustawienia.
VoiceRecalculating|Przeliczanie trasy
VoiceWrongWay|Zły kierunek
VoiceUnavailable|Trasa drogowa niedostępna
CopyBindingHelp|Domyślnie: Num / (dzielenie numeryczne), bez modyfikatora. Przypisz ten sam klawisz kopiowania pozycji w SCUM. Obecne przypisania są zachowywane. Bez klawiatury numerycznej przechwyć własny klawisz w Ustawieniach. Śledzenie jednym klawiszem obsługuje 250 ms; Ctrl+C wymaga co najmniej 1 sekundy.
DeathLocation|Miejsce śmierci
DeathName|Śmierć
AutomaticDeathMarkers|Automatyczne znaczniki śmierci
ShortcutWaypoint|Dodaj punkt trasy
ShortcutSearch|Szukaj
UpdateResetTitle|Aktualizacja MiniMap
UpdateResetBody|Zresetować zapisane ustawienia i ponownie uruchomić pierwszą konfigurację?
ZoneEditorOpenError|Nie udało się otworzyć edytora własnych stref: {0}
SidebarTarget|Cel: {0}
SidebarCategories|KATEGORIE POI
SidebarPanHint|Przeciągnij lewym przyciskiem: przesuwanie
SidebarContextHint|Prawy przycisk: opcje punktu trasy
SidebarScrollHint|Panel boczny: kółko lub pasek przewijania
SidebarExitHint|{0} / Esc: zamknij pełną mapę
HistoryEnabled|Historia pozycji
HistoryHover|Czas historii po wskazaniu";

        static readonly Dictionary<string,string> SectionNamesPl = ReadPolishNames(@"Buildings|Budynki
Bunkers|Bunkry
Construction materials|Materiały budowlane
Crops|Uprawy
Loot containers|Pojemniki z łupem
Other|Inne
Outposts|Posterunki handlowe
Points of interest|Punkty zainteresowania
Quests|Zadania
Radiation|Promieniowanie
Vehicles|Pojazdy
Water sources|Źródła wody
Fishing|Łowienie ryb
Hunting|Polowanie
Fishing habitats|Siedliska ryb
Hunting habitats|Siedliska zwierząt");

        static readonly Dictionary<string,string> CategoryNamesPl = ReadPolishNames(@"Abandoned bunkers|Opuszczone bunkry
Aircraft spawns|Miejsca pojawiania się samolotów
Animal feeders|Karmniki dla zwierząt
Boat spawns|Miejsca pojawiania się łodzi
Bunker Hatch Doors|Włazy bunkrów
Bunkers|Bunkry
Caves|Jaskinie
Car garages|Garaże samochodowe
Decorative fountains|Fontanny ozdobne
Drill press / Lathe|Wiertarka stołowa / tokarka
Fishing spots|Miejsca do łowienia ryb
Fishing Supplies|Sprzęt wędkarski
Hunting Camps|Obozy myśliwskie
Hunter's Grotto|Grota myśliwego
Fountains|Fontanny
Gas stations|Stacje benzynowe
Gun Shops|Sklepy z bronią
Hand water pumps|Ręczne pompy wodne
Hunting Shops|Sklepy myśliwskie
Hunting Towers|Wieże myśliwskie
Industrial storage silos|Silosy przemysłowe
Killboxes|Killboxy
Lighthouses|Latarnie morskie
Lockers|Szafki
Medical Containers|Pojemniki medyczne
Military Hangars|Hangary wojskowe
Military Warehouses|Magazyny wojskowe
Mine entrances|Wejścia do kopalń
Motorbike & bicycle spawns|Miejsca pojawiania się motocykli i rowerów
Natural springs|Naturalne źródła
Phone Booths|Budki telefoniczne
Police stations|Posterunki policji
Quest Boards|Tablice zadań
Quest books|Księgi zadań
Quest Mailboxes|Skrzynki pocztowe zadań
Research Facilities|Ośrodki badawcze
Secret Bunkers|Tajne bunkry
Small Docks|Małe przystanie
Small Shipwrecks|Małe wraki statków
Underwater Caves|Podwodne jaskinie
Vehicle repair shops|Warsztaty samochodowe
Vehicle spawns|Miejsca pojawiania się pojazdów
Villages|Wioski
Water dispensers|Dystrybutory wody
Wells|Studnie
Wheelbarrow spawns|Miejsca pojawiania się taczek
Workshops|Warsztaty
WW2 bunkers|Bunkry z II wojny światowej");

        static Dictionary<string,string> ReadPolishNames(string text) {
            var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (string row in text.Split('\n')) {
                string[] fields = row.TrimEnd('\r').Split('|');
                if (fields.Length != 2) throw new FormatException("Invalid Polish name translation.");
                result.Add(fields[0], fields[1]);
            }
            return result;
        }
    }
}
