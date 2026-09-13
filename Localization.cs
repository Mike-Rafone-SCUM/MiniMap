using System;
using System.Collections.Generic;
using System.Globalization;

namespace ScumMiniMap {
    public enum AppLanguage {
        English = 0,
        SpanishArgentina = 1
    }

    public static class Localization {
        public static event Action LanguageChanged;

        private static AppLanguage currentLanguage = AppLanguage.English;

        public static AppLanguage Current {
            get { return currentLanguage; }
            set {
                if (currentLanguage != value) {
                    currentLanguage = value;
                    Action handler = LanguageChanged;
                    if (handler != null) handler();
                }
            }
        }

        public static string CurrentCode {
            get {
                switch (currentLanguage) {
                    case AppLanguage.SpanishArgentina: return "es-AR";
                    default: return "en";
                }
            }
        }

        public static void SetLanguage(string code) {
            if (string.IsNullOrWhiteSpace(code)) return;
            string normalized = code.Trim().ToLowerInvariant();
            if (normalized.StartsWith("es")) {
                Current = AppLanguage.SpanishArgentina;
            } else {
                Current = AppLanguage.English;
            }
        }

        public static void DetectSystemLanguage() {
            try {
                string name = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                if (name.StartsWith("es")) {
                    Current = AppLanguage.SpanishArgentina;
                } else {
                    Current = AppLanguage.English;
                }
            } catch {
                Current = AppLanguage.English;
            }
        }

        public static string Get(string key) {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            Dictionary<string, string> dict = (currentLanguage == AppLanguage.SpanishArgentina) ? StringsEsAr : StringsEn;
            string value;
            if (dict.TryGetValue(key, out value)) {
                return value;
            }

            // Fallback to English
            if (StringsEn.TryGetValue(key, out value)) {
                return value;
            }

            return key;
        }

        public static string T(string key, params object[] args) {
            string text = Get(key);
            if (args == null || args.Length == 0) return text;
            try {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            } catch {
                return text;
            }
        }

        public static string GetZoneName(string englishName) {
            if (string.IsNullOrEmpty(englishName)) return string.Empty;
            if (currentLanguage == AppLanguage.SpanishArgentina) {
                string localized;
                if (ZoneNamesEsAr.TryGetValue(englishName, out localized)) {
                    return localized;
                }
                if (englishName.StartsWith("Gas Station - ", StringComparison.OrdinalIgnoreCase)) {
                    string sub = englishName.Substring("Gas Station - ".Length);
                    string locSub;
                    if (ZoneNamesEsAr.TryGetValue(sub, out locSub)) {
                        return "Estación de Servicio - " + locSub;
                    }
                    return "Estación de Servicio - " + sub;
                }
            }
            return englishName;
        }

        public static readonly Dictionary<string, string> StringsEn = new Dictionary<string, string>(StringComparer.Ordinal) {
            // General & Common
            { "Close", "Close" },
            { "Done", "Done" },
            { "Cancel", "Cancel" },
            { "Settings", "Settings" },
            { "Language", "Language" },
            { "AppTitleSettings", "SkynettMiniMap Settings v{0}" },
            { "HeaderSettings", "SKYNETT / SETTINGS v{0}" },
            { "HeaderWaypoint", "SKYNETT / WAYPOINT" },
            { "HeaderMapZones", "SKYNETT / MAP ZONES" },

            // Settings Sections
            { "SecLanguage", "Language / Idioma" },
            { "SecNavigation", "Navigation" },
            { "SecMapZones", "Map and zones" },
            { "SecAppearance", "Appearance" },
            { "SecTrackingZoom", "Tracking and zoom" },
            { "SecLayout", "Layout" },
            { "SecToolsShortcuts", "Tools and shortcuts" },

            // Navigation Section
            { "AutoTrackingActive", "Automatic tracking / active during gameplay" },
            { "SearchPlaceOrGrid", "Search place or grid..." },

            // Map and zones Section
            { "GridLabels", "Grid numbers / sector labels (D4, C3, etc.)" },
            { "GridBorders", "Grid lines" },
            { "GridOpacity", "Grid opacity: {0}%" },
            { "ShowSavedZones", "Show saved zones" },
            { "ShowGasStations", "Show gas stations (fuel icons)" },
            { "ZoneLabelSize", "Zone label size" },
            { "MapZonesScreenshot", "Map zones from screenshot..." },

            // Appearance Section
            { "FadeEdges", "Fade edges" },
            { "ShowHeading", "Show player heading cone / arrow" },
            { "ShowCompass", "Show cardinal compass indicators (N, S, E, W)" },
            { "ShowElevation", "Show player elevation (Z)" },
            { "ZoneChime", "Zone entry chime sound" },

            // Tracking and zoom Section
            { "AutoZoomSpeed", "Auto-zoom based on travel speed" },
            { "AutoZoomMax", "Auto-zoom max (stationary)" },
            { "AutoZoomMin", "Auto-zoom min (high speed)" },
            { "PositionInterval", "Position update interval (ms)" },

            // Layout Section
            { "ShowStatus", "Show location info bar" },
            { "LocationBarPosition", "Location bar position" },
            { "PosBelow", "Below" },
            { "PosAbove", "Above" },
            { "MapWidth", "Map width (pixels)" },
            { "MapHeight", "Map height (pixels)" },
            { "MapOpacity", "Opacity (%)" },
            { "MaxZoom", "Max manual zoom level" },
            { "ZoomStep", "Zoom step size (%)" },
            { "ZoomOut", "Zoom out (PgDn)" },
            { "ZoomIn", "Zoom in (PgUp)" },
            { "FullMap", "Full map" },
            { "CentreMap", "Centre map on player" },

            // Tools and shortcuts Section
            { "ImportCustomMap", "Import custom high-def map..." },
            { "CheckUpdatesGitHub", "Check for updates (GitHub)..." },
            { "JoinDiscord", "Join Discord community..." },
            { "HotkeysHint", "HOTKEYS IN SCUM:\n* PgUp / PgDn: Zoom in / Zoom out\n* Home: Open Settings\n* End: Show / Hide Minimap\n* Insert: Set / Clear Pin\n* Delete: Search place / grid waypoint\n* T: pause chat | Enter/Esc: resume chat" },
            { "OpenDataFolder", "Open Data Folder" },

            // Tray Menu
            { "TrayCheckUpdates", "Check for updates..." },
            { "TrayShowHide", "Show / hide map" },
            { "TrayExit", "Exit" },

            // Search Dialog
            { "SearchTitle", "Search place or grid" },
            { "SearchHint", "Try: faction, airfield, bunker C3, grid D4. Typos are supported." },
            { "SetWaypoint", "Set waypoint" },
            { "ClearWaypoint", "Clear waypoint" },
            { "NoMatches", "No matches. Try 'faction', a place name, or a grid from D4 to Z0." },
            { "SearchResults", "{0} results. Enter to navigate; Esc to return." },
            { "SectorCentre", " / Sector centre" },
            { "FactionPOI", " / Faction POI" },
            { "Place", " / Place" },

            // Overlay HUD & Location descriptions
            { "AwaitingPosition", "Awaiting position..." },
            { "AwaitingPositionShort", "Awaiting position" },
            { "WaitingForCoordinates", "Waiting for coordinates" },
            { "Live", "LIVE" },
            { "SecondsAgo", "{0}s ago" },
            { "ElevationMeters", " | Alt: {0}m" },
            { "PinDistance", " | Pin: {0}" },
            { "ToDestination", " | To: {0}" },
            { "RoadSuffix", " (road)" },
            { "LocLeavingApproaching", "{0} - Leaving {1}, approaching {2}" },
            { "LocLeaving", "{0} - Leaving {1}" },
            { "LocApproaching", "{0} - Approaching {1}" },
            { "LocNear", "{0} - Near {1}" },
            { "LocBetween", "{0} - Between {1} and {2}" },

            // Status Notes & Notifications
            { "NoteAutoWaiting", "Automatic tracking is waiting for SCUM." },
            { "NoteZonesLoadFail", "Could not load saved zones." },
            { "NoteSettingsSaveFail", "Unable to save appearance settings." },
            { "NotePinSet", "Pin set at current location." },
            { "NotePinCleared", "Pin cleared." },
            { "NoteWaypointSet", "Waypoint set: {0}. Press Delete to search or clear." },
            { "NoteWaypointCleared", "Search waypoint cleared." },
            { "NoteWaypointArrived", "Arrived at {0}. Waypoint cleared." },
            { "NoteChatOpen", "Chat open: automatic copying paused." },
            { "NoteChatClosed", "Chat closed: automatic copying can resume." },
            { "NoteCoordReceived", "Automatic coordinate response received." },
            { "NoteNoCoordsAttempt", "No fresh coordinates received (attempt {0})." },
            { "NoteNoCoordsRetry", "No coordinate response. Automatic tracking will retry in 5 seconds." },
            { "NoteChatMonitorUnavailable", "Chat key monitoring unavailable; automatic copying disabled." },
            { "NoteWaitingForeground", "Waiting for SCUM to be the foreground window." },
            { "NoteWaitingUserKeys", "Waiting for user input or busy keys to be released." },
            { "NoteAutoCopyActive", "Auto-copy active." },
            { "NoteCopyRejected", "Windows did not accept the copy shortcut." },
            { "NoteCopyCancelled", "Copy cancelled by focus or user input; resuming at the normal interval." },
            { "NoteCopyErrorRetry", "{0} Automatic tracking will retry in 5 seconds." },
            { "NoteClipboardBusy", "Clipboard busy; retrying." },
            { "StatusSummary", "Automatic requests: {0} | Responses: {1}" },
            { "DiagCopyActive", "Automatic copying active." },

            // Zone Editor
            { "ZoneEditorTitle", "Map zones from screenshot" },
            { "ZeOpenScreenshot", "Open screenshot" },
            { "ZeManualFallback", "Manual fallback" },
            { "ZeNewZone", "New zone" },
            { "ZeZoneColour", "Zone colour" },
            { "ZeUndoPoint", "Undo point" },
            { "ZeAddZone", "Add zone" },
            { "ZeApplyName", "Apply name" },
            { "ZeNameColour", "Name this colour" },
            { "ZeSaveZones", "Save zones" },
            { "ZeDeleteSelected", "Delete selected" },
            { "ZeAligningLocally", "Aligning map and detecting coloured zone outlines locally..." },
            { "ZeAutoFailedKept", "Automatic detection failed; existing zones were kept." },
            { "ZeCouldNotImportAuto", "Could not import automatically: {0}" },
            { "ZeDetectedOutlinesPrompt", "{0} outlines detected. Click a zone, enter what it is, then Apply name (or Name this colour). Save zones replaces the current saved list." },
            { "ZeManualStep0", "Click landmark 1 on the screenshot (left)." },
            { "ZeManualStep1", "Click the SAME landmark on the reference map (right)." },
            { "ZeManualStep2", "Click landmark 2 on the screenshot, far away in both directions." },
            { "ZeManualStep3", "Click landmark 2 on the reference map." },
            { "ZeManualStep4", "Aligned. Click around a zone on the screenshot, choose a name/colour, then Add zone. Verify the outline on the right." },
            { "ZeInitialPrompt", "Open a screenshot to automatically align it and detect coloured zones. Then name each zone or colour group." },
            { "ZeDraftWarning", "Add the current outline as a zone, or undo its points, before saving." },
            { "ZeLabelLengthWarning", "Use a label of 1–80 characters." },
            { "ZeOutsideMapWarning", "That point maps outside the island image. Check alignment." },
            { "ZePointsRequirementWarning", "Align the screenshot and click at least three outline points first." },
            { "ZeZonesMaxWarning", "Use a name of 1–80 characters. Up to 500 zones are supported." },
            { "ZeSaveZonesError", "Could not save zones: {0}" },
            { "ZeSelectScreenshotTitle", "Select a north-up screenshot of your in-game map" },
            { "ZeMapScreenshotsFilter", "Map screenshots|*.png;*.jpg;*.jpeg;*.bmp" },

            // Dialogs & Alerts
            { "WelcomeTitle", "SkynettMiniMap v{0} - Quick Start Guide" },
            { "WelcomeMsg", "Welcome to SkynettMiniMap v{0}!\n\nHOW IT WORKS:\nWhen SCUM is in focus, coordinates are automatically sampled and tracked live on your circular radar HUD.\n\nHOTKEYS (IN-GAME):\n* PgUp / PgDn : Zoom In / Zoom Out\n* Home : Open Settings & Customization\n* End : Show / Hide Minimap\n* Insert : Set / Clear Pin\n* Delete : Search place / grid waypoint\n* T / Enter / Esc : Automatic chat detection\n\nEnjoy the game!" },
            { "CustomMapSelectTitle", "Select High-Definition Map Image" },
            { "CustomMapSuccessMsg", "High-definition map imported successfully!\nPlease restart the app to reload the new map texture." },
            { "CustomMapSuccessTitle", "Map Imported" },
            { "CustomMapErrorMsg", "Could not import map: {0}" },
            { "DiscordOpenError", "Could not open Discord: {0}" },
            { "DataFolderOpenError", "Could not open data folder: {0}" },

            // Updates
            { "UpdateVersionUnknown", "GitHub did not provide valid release information. Please try again later." },
            { "UpdateRateLimited", "GitHub is temporarily limiting update checks or is unavailable. Try again after {0}." },
            { "UpdateSaveTitle", "Save SCUM MiniMap update" },
            { "UpdateChooseNewFile", "Choose a new filename. Existing files cannot be replaced while downloading an update." },
            { "UpdateDownloading", "Downloading update from GitHub..." },
            { "UpdateDownloadComplete", "Update downloaded and verified." },
            { "UpdateDownloadedMsg", "Update downloaded and verified:\n\n{0}\n\nExit SCUM MiniMap using the tray menu, then replace your old executable with this file and run it. Your saved settings and zones will be retained." },
            { "UpdateCheckTitle", "Update Check" },
            { "UpdateConnectError", "Could not retrieve the GitHub release. Check your connection or try again later." },
            { "UpdateAvailableNote", "Update available: v{0}! Press Home for details." },
            { "UpdateAvailableDialogTitle", "Update Available - SkynettMiniMap" },
            { "UpdateAvailableDialogMsg", "A newer version of SCUM MiniMap is available.\n\nCurrent version: v{0}\nLatest on GitHub: v{1}\nDownload: {2}\n\nDownload this update now?" },
            { "UpdateUpToDateTitle", "Up to Date" },
            { "UpdateUpToDateMsg", "No newer stable release is available.\n\nInstalled: v{0}\nLatest on GitHub: v{1}" },
            { "UpdateFailedMsg", "Update check failed: {0}" }
        };

        public static readonly Dictionary<string, string> StringsEsAr = new Dictionary<string, string>(StringComparer.Ordinal) {
            // General & Common
            { "Close", "Cerrar" },
            { "Done", "Listo" },
            { "Cancel", "Cancelar" },
            { "Settings", "Ajustes" },
            { "Language", "Idioma" },
            { "AppTitleSettings", "SkynettMiniMap Ajustes v{0}" },
            { "HeaderSettings", "SKYNETT / AJUSTES v{0}" },
            { "HeaderWaypoint", "SKYNETT / PUNTO DE RUTA" },
            { "HeaderMapZones", "SKYNETT / ZONAS DEL MAPA" },

            // Settings Sections
            { "SecLanguage", "Idioma / Language" },
            { "SecNavigation", "Navegación" },
            { "SecMapZones", "Mapa y zonas" },
            { "SecAppearance", "Apariencia" },
            { "SecTrackingZoom", "Rastreo y zoom" },
            { "SecLayout", "Disposición y tamaño" },
            { "SecToolsShortcuts", "Herramientas y atajos" },

            // Navigation Section
            { "AutoTrackingActive", "Rastreo automático / activo durante la partida" },
            { "SearchPlaceOrGrid", "Buscar lugar o cuadrícula..." },

            // Map and zones Section
            { "GridLabels", "Números de cuadrícula / etiquetas de sector (D4, C3, etc.)" },
            { "GridBorders", "Líneas de cuadrícula" },
            { "GridOpacity", "Opacidad de la cuadrícula: {0}%" },
            { "ShowSavedZones", "Mostrar zonas guardadas" },
            { "ShowGasStations", "Mostrar estaciones de servicio (iconos de combustible)" },
            { "ZoneLabelSize", "Tamaño de etiqueta de zona" },
            { "MapZonesScreenshot", "Mapear zonas desde captura de pantalla..." },

            // Appearance Section
            { "FadeEdges", "Desvanecer bordes del radar" },
            { "ShowHeading", "Mostrar cono / flecha de orientación del jugador" },
            { "ShowCompass", "Mostrar puntos cardinales de brújula (N, S, E, O)" },
            { "ShowElevation", "Mostrar elevación del jugador (Z)" },
            { "ZoneChime", "Sonido de campana al entrar a una zona" },

            // Tracking and zoom Section
            { "AutoZoomSpeed", "Zoom automático según velocidad de movimiento" },
            { "AutoZoomMax", "Zoom auto máx. (estacionario)" },
            { "AutoZoomMin", "Zoom auto mín. (alta velocidad)" },
            { "PositionInterval", "Intervalo de actualización de posición (ms)" },

            // Layout Section
            { "ShowStatus", "Mostrar barra de información de ubicación" },
            { "LocationBarPosition", "Posición de la barra de ubicación" },
            { "PosBelow", "Abajo" },
            { "PosAbove", "Arriba" },
            { "MapWidth", "Ancho del mapa (píxeles)" },
            { "MapHeight", "Alto del mapa (píxeles)" },
            { "MapOpacity", "Opacidad (%)" },
            { "MaxZoom", "Nivel máximo de zoom manual" },
            { "ZoomStep", "Paso de zoom (%)" },
            { "ZoomOut", "Alejar (AvPág)" },
            { "ZoomIn", "Acercar (RePág)" },
            { "FullMap", "Mapa completo" },
            { "CentreMap", "Centrar mapa en el jugador" },

            // Tools and shortcuts Section
            { "ImportCustomMap", "Importar mapa HD personalizado..." },
            { "CheckUpdatesGitHub", "Buscar actualizaciones (GitHub)..." },
            { "JoinDiscord", "Unirse a la comunidad de Discord..." },
            { "HotkeysHint", "ATAJOS EN SCUM:\n* RePág / AvPág: Acercar / Alejar\n* Inicio: Abrir Ajustes\n* Fin: Mostrar / Ocultar Minimapa\n* Insert: Fijar / Quitar Marcador\n* Supr: Buscar lugar / punto de ruta\n* T: pausar chat | Enter/Esc: reanudar chat" },
            { "OpenDataFolder", "Abrir carpeta de datos" },

            // Tray Menu
            { "TrayCheckUpdates", "Buscar actualizaciones..." },
            { "TrayShowHide", "Mostrar / ocultar mapa" },
            { "TrayExit", "Salir" },

            // Search Dialog
            { "SearchTitle", "Buscar lugar o cuadrícula" },
            { "SearchHint", "Probá: facción, aeródromo, búnker C3, cuadrícula D4. Admite errores de tipeo." },
            { "SetWaypoint", "Fijar punto de ruta" },
            { "ClearWaypoint", "Borrar punto de ruta" },
            { "NoMatches", "Sin coincidencias. Probá con 'facción', un lugar o una cuadrícula de D4 a Z0." },
            { "SearchResults", "{0} resultados. Enter para navegar; Esc para volver." },
            { "SectorCentre", " / Centro de sector" },
            { "FactionPOI", " / Punto de facción" },
            { "Place", " / Lugar" },

            // Overlay HUD & Location descriptions
            { "AwaitingPosition", "Esperando posición..." },
            { "AwaitingPositionShort", "Esperando posición" },
            { "WaitingForCoordinates", "Esperando coordenadas" },
            { "Live", "EN VIVO" },
            { "SecondsAgo", "hace {0}s" },
            { "ElevationMeters", " | Alt: {0}m" },
            { "PinDistance", " | Marcador: {0}" },
            { "ToDestination", " | Hacia: {0}" },
            { "RoadSuffix", " (ruta)" },
            { "LocLeavingApproaching", "{0} - Dejando {1}, acercándose a {2}" },
            { "LocLeaving", "{0} - Dejando {1}" },
            { "LocApproaching", "{0} - Acercándose a {1}" },
            { "LocNear", "{0} - Cerca de {1}" },
            { "LocBetween", "{0} - Entre {1} y {2}" },

            // Status Notes & Notifications
            { "NoteAutoWaiting", "El rastreo automático está esperando a SCUM." },
            { "NoteZonesLoadFail", "No se pudieron cargar las zonas guardadas." },
            { "NoteSettingsSaveFail", "No se pudieron guardar los ajustes de apariencia." },
            { "NotePinSet", "Marcador fijado en la ubicación actual." },
            { "NotePinCleared", "Marcador quitado." },
            { "NoteWaypointSet", "Punto de ruta fijado: {0}. Presioná Supr para buscar o borrar." },
            { "NoteWaypointCleared", "Punto de ruta eliminado." },
            { "NoteWaypointArrived", "Llegaste a {0}. Punto de ruta eliminado." },
            { "NoteChatOpen", "Chat abierto: copia automática en pausa." },
            { "NoteChatClosed", "Chat cerrado: la copia automática se reanuda." },
            { "NoteCoordReceived", "Respuesta automática de coordenadas recibida." },
            { "NoteNoCoordsAttempt", "No se recibieron coordenadas nuevas (intento {0})." },
            { "NoteNoCoordsRetry", "Sin respuesta de coordenadas. El rastreo automático reintentará en 5 segundos." },
            { "NoteChatMonitorUnavailable", "Monitoreo de teclas de chat no disponible; copia automática desactivada." },
            { "NoteWaitingForeground", "Esperando que SCUM sea la ventana activa." },
            { "NoteWaitingUserKeys", "Esperando que se liberen las teclas ocupadas o entrada de usuario." },
            { "NoteAutoCopyActive", "Auto-copiado activo." },
            { "NoteCopyRejected", "Windows no aceptó el atajo de copiado." },
            { "NoteCopyCancelled", "Copiado cancelado por foco o entrada de usuario; reanudando en el intervalo normal." },
            { "NoteCopyErrorRetry", "{0} El rastreo automático reintentará en 5 segundos." },
            { "NoteClipboardBusy", "Portapapeles ocupado; reintentando." },
            { "StatusSummary", "Solicitudes automáticas: {0} | Respuestas: {1}" },
            { "DiagCopyActive", "Copiado automático activo." },

            // Zone Editor
            { "ZoneEditorTitle", "Mapear zonas desde captura de pantalla" },
            { "ZeOpenScreenshot", "Abrir captura" },
            { "ZeManualFallback", "Modo manual" },
            { "ZeNewZone", "Nueva zona" },
            { "ZeZoneColour", "Color de zona" },
            { "ZeUndoPoint", "Deshacer punto" },
            { "ZeAddZone", "Agregar zona" },
            { "ZeApplyName", "Aplicar nombre" },
            { "ZeNameColour", "Nombrar este color" },
            { "ZeSaveZones", "Guardar zonas" },
            { "ZeDeleteSelected", "Eliminar seleccionada" },
            { "ZeAligningLocally", "Alineando mapa y detectando contornos de zonas en colores localmente..." },
            { "ZeAutoFailedKept", "La detección automática falló; se mantuvieron las zonas existentes." },
            { "ZeCouldNotImportAuto", "No se pudo importar automáticamente: {0}" },
            { "ZeDetectedOutlinesPrompt", "{0} contornos detectados. Hacé clic en una zona, poné qué es, y después Aplicar nombre (o Nombrar este color). Guardar zonas reemplaza la lista guardada actual." },
            { "ZeManualStep0", "Hacé clic en el punto de referencia 1 en la captura (izquierda)." },
            { "ZeManualStep1", "Hacé clic en el MISMO punto de referencia en el mapa (derecha)." },
            { "ZeManualStep2", "Hacé clic en el punto de referencia 2 en la captura, alejado en ambas direcciones." },
            { "ZeManualStep3", "Hacé clic en el punto de referencia 2 en el mapa." },
            { "ZeManualStep4", "Alineado. Hacé clic alrededor de una zona en la captura, elegí nombre/color, y después Agregar zona. Verificá el contorno a la derecha." },
            { "ZeInitialPrompt", "Abrí una captura para alinearla automáticamente y detectar zonas en color. Después nombrá cada zona o grupo de color." },
            { "ZeDraftWarning", "Agregá el contorno actual como una zona, o deshacé sus puntos, antes de guardar." },
            { "ZeLabelLengthWarning", "Usá una etiqueta de 1 a 80 caracteres." },
            { "ZeOutsideMapWarning", "Ese punto queda fuera de la imagen de la isla. Revisá la alineación." },
            { "ZePointsRequirementWarning", "Alineá la captura y hacé clic en al menos tres puntos del contorno primero." },
            { "ZeZonesMaxWarning", "Usá un nombre de 1 a 80 caracteres. Se admiten hasta 500 zonas." },
            { "ZeSaveZonesError", "No se pudieron guardar las zonas: {0}" },
            { "ZeSelectScreenshotTitle", "Seleccioná una captura orientada al norte de tu mapa en el juego" },
            { "ZeMapScreenshotsFilter", "Capturas de mapa|*.png;*.jpg;*.jpeg;*.bmp" },

            // Dialogs & Alerts
            { "WelcomeTitle", "SkynettMiniMap v{0} - Guía de inicio rápido" },
            { "WelcomeMsg", "¡Bienvenido a SkynettMiniMap v{0}!\n\nCÓMO FUNCIONA:\nCuando SCUM está en primer plano, las coordenadas se leen y rastrean automáticamente en vivo en tu radar circular del HUD.\n\nATAJOS (EN EL JUEGO):\n* RePág / AvPág : Acercar / Alejar\n* Inicio : Abrir Ajustes y Personalización\n* Fin : Mostrar / Ocultar Minimapa\n* Insert : Fijar / Quitar Marcador\n* Supr : Buscar lugar / punto de ruta en cuadrícula\n* T / Enter / Esc : Detección automática del chat\n\n¡Que disfrutes del juego!" },
            { "CustomMapSelectTitle", "Seleccionar imagen de mapa de alta definición" },
            { "CustomMapSuccessMsg", "¡Mapa de alta definición importado con éxito!\nPor favor reiniciá la app para recargar la textura del nuevo mapa." },
            { "CustomMapSuccessTitle", "Mapa importado" },
            { "CustomMapErrorMsg", "No se pudo importar el mapa: {0}" },
            { "DiscordOpenError", "No se pudo abrir Discord: {0}" },
            { "DataFolderOpenError", "No se pudo abrir la carpeta de datos: {0}" },

            // Updates
            { "UpdateVersionUnknown", "GitHub no proporcionó información válida de la versión. Intentá de nuevo más tarde." },
            { "UpdateRateLimited", "GitHub está limitando las consultas o no está disponible. Intentá de nuevo después de {0}." },
            { "UpdateSaveTitle", "Guardar actualización de SCUM MiniMap" },
            { "UpdateChooseNewFile", "Elegí un nombre nuevo. No se pueden reemplazar archivos existentes durante la descarga." },
            { "UpdateDownloading", "Descargando actualización de GitHub..." },
            { "UpdateDownloadComplete", "Actualización descargada y verificada." },
            { "UpdateDownloadedMsg", "Actualización descargada y verificada:\n\n{0}\n\nSalí de SCUM MiniMap desde el menú de la bandeja, reemplazá el ejecutable anterior con este archivo y abrilo. Se conservarán tus ajustes y zonas." },
            { "UpdateCheckTitle", "Verificación de actualizaciones" },
            { "UpdateConnectError", "No se pudo obtener la versión de GitHub. Revisá tu conexión o intentá más tarde." },
            { "UpdateAvailableNote", "¡Actualización disponible: v{0}! Presioná Inicio para más detalles." },
            { "UpdateAvailableDialogTitle", "Actualización disponible - SkynettMiniMap" },
            { "UpdateAvailableDialogMsg", "Hay una nueva versión de SCUM MiniMap.\n\nVersión actual: v{0}\nÚltima en GitHub: v{1}\nArchivo: {2}\n\n¿Querés descargar la actualización?" },
            { "UpdateUpToDateTitle", "Actualizado" },
            { "UpdateUpToDateMsg", "No hay una versión estable más reciente.\n\nInstalada: v{0}\nÚltima en GitHub: v{1}" },
            { "UpdateFailedMsg", "Falló la verificación de actualizaciones: {0}" }
        };

        public static readonly Dictionary<string, string> ZoneNamesEsAr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            // Major POIs & Infrastructure
            { "Airport", "Aeropuerto" },
            { "Military Airfield", "Aeródromo Militar" },
            { "Military Barracks", "Barracas Militares" },
            { "Dam", "Represa" },
            { "Lumber Mill", "Aserradero" },
            { "Naval Base", "Base Naval" },
            { "Observatory", "Observatorio" },
            { "Prison", "Prisión" },
            { "Radio Station", "Estación de Radio" },
            { "Boot Camp", "Campo de Entrenamiento" },
            { "Trainyard", "Playa Ferroviaria" },
            { "Train Yard", "Playa Ferroviaria" },
            { "Brick Factory", "Fábrica de Ladrillos" },
            { "Fish Factory", "Fábrica Pesquera" },
            { "Cheese Factory", "Fábrica de Quesos" },
            { "Weapons Factory", "Fábrica de Armas" },
            { "Factory", "Fábrica" },
            { "Junkyard", "Desarmadero" },
            { "Klenovnik Hospital", "Hospital Klenovnik" },
            { "Motorbike Track", "Pista de Motocross" },
            { "Demolition Derby", "Derbi de Demolición" },
            { "Duga Radar", "Radar Duga" },
            { "North Quarry", "Cantera Norte" },
            { "South Quarry", "Cantera Sur" },
            { "Salt Ponds", "Salinas" },
            { "Small Shipyard", "Astillero Pequeño" },
            { "Big Vern monument", "Monumento a Big Vern" },
            { "CC", "Centro de Carga" },

            // Outposts / Traders
            { "A0 Outpost", "Puesto Comercial A0" },
            { "B4 Outpost", "Puesto Comercial B4" },
            { "C2 Outpost", "Puesto Comercial C2" },
            { "Z3 Outpost", "Puesto Comercial Z3" },

            // Bunkers
            { "D0 Bunker", "Búnker D0" },
            { "D1 Abandoned Bunker", "Búnker Abandonado D1" },
            { "C4 Abandoned Bunker", "Búnker Abandonado C4" },
            { "A1 Abandoned Bunker", "Búnker Abandonado A1" },
            { "A3 Abandoned Bunker", "Búnker Abandonado A3" },
            { "C1 Bunker", "Búnker C1" },
            { "A2 Bunker", "Búnker A2" },
            { "B0 Bunker", "Búnker B0" },
            { "B4 Bunker", "Búnker B4" },
            { "D3 Bunker", "Búnker D3" },
            { "Z0 Bunker", "Búnker Z0" },
            { "Z2 Bunker", "Búnker Z2" },
            { "A3 WWII bunker", "Búnker 2da Guerra A3" },
            { "A3 Island WWII bunker", "Búnker Isla 2da Guerra A3" },
            { "B1 WWII Bunker", "Búnker 2da Guerra B1" },
            { "B2 WWII Bunker", "Búnker 2da Guerra B2" },
            { "B3 WWII Bunker", "Búnker 2da Guerra B3" },
            { "C1 ww2 bunker", "Búnker 2da Guerra C1" },
            { "C4 WWII Bunker", "Búnker 2da Guerra C4" },
            { "Z0 WWII bunker", "Búnker 2da Guerra Z0" },
            { "Z0 WWII Bunker South", "Búnker 2da Guerra Sur Z0" },
            { "Z3 WWII Bunker", "Búnker 2da Guerra Z3" },
            { "Z4 WWII Bunker", "Búnker 2da Guerra Z4" },

            // Minor Locations & Settlements
            { "A0 Camping", "Camping A0" },
            { "A0 Offices", "Oficinas A0" },
            { "A1 School", "Escuela A1" },
            { "A1 Stables", "Caballerizas A1" },
            { "A2 Stables", "Caballerizas A2" },
            { "A2 Store with ATM", "Comercio con Cajero A2" },
            { "A3 Farm", "Granja A3" },
            { "A4 Airfield", "Aeródromo A4" },
            { "A4 House ruins", "Ruinas A4" },
            { "A4 Lighthouse", "Faro A4" },
            { "A4 Street between 2 houses", "Callejones A4" },
            { "B1 Barn", "Galpón B1" },
            { "B1 Little Farm", "Granja Pequeña B1" },
            { "B2 Farm", "Granja B2" },
            { "B2 Little Farm", "Granja Pequeña B2" },
            { "B3 Castle", "Castillo B3" },
            { "B3 Little farm", "Granja Pequeña B3" },
            { "C1 Little farm", "Granja Pequeña C1" },
            { "C3 Little cabin", "Cabaña C3" },
            { "C4 Wine farm", "Viñedo C4" },
            { "D1 Farm", "Granja D1" },
            { "D1 Food store", "Almacén D1" },
            { "D1 Graveyard", "Cementerio D1" },
            { "D2 Little bridge", "Puente Pequeño D2" },
            { "D2 Lumbermill cabin", "Cabaña del Aserradero D2" },
            { "D3 - General Store", "Almacén General D3" },
            { "D3 City Warehouse", "Depósito D3" },
            { "D3 Little town - House", "Casa de Pueblo D3" },
            { "D4 City - Pharmacy", "Farmacia D4" },
            { "D4 City - Police Station", "Comisaría D4" },
            { "D4 Clock house", "Torre del Reloj D4" },
            { "D4 Stable ruins", "Ruinas de Caballerizas D4" },
            { "Z0 Airfield", "Aeródromo Z0" },
            { "Z0 Lighthouse", "Faro Z0" },
            { "Z2 light house", "Faro Z2" },
            { "Z3 Boat yard", "Varadero Z3" },
            { "Z3 Island Lighthouse", "Faro de la Isla Z3" },
            { "Z4 Boatyard", "Varadero Z4" },
            { "Z4 Graveyard", "Cementerio Z4" },
            { "Z4 Lighthouse", "Faro Z4" },

            // Gas Stations
            { "Gas Station - Airfield (A4)", "Estación de Servicio - Aeródromo (A4)" },
            { "Gas Station - Airfield East (Z0)", "Estación de Servicio - Aeródromo Este (Z0)" },
            { "Gas Station - Airfield North (Z0)", "Estación de Servicio - Aeródromo Norte (Z0)" },
            { "Gas Station - Boat Yard (Z3)", "Estación de Servicio - Varadero (Z3)" },
            { "Gas Station - Brick Factory (B0)", "Estación de Servicio - Fábrica de Ladrillos (B0)" },
            { "Gas Station - Bunker (Z2)", "Estación de Servicio - Búnker (Z2)" },
            { "Gas Station - D3 Town (D3)", "Estación de Servicio - Pueblo D3 (D3)" },
            { "Gas Station - East Coast (Z3)", "Estación de Servicio - Costa Este (Z3)" },
            { "Gas Station - Fish Factory (A1)", "Estación de Servicio - Fábrica Pesquera (A1)" },
            { "Gas Station - Gorica (D1)", "Estación de Servicio - Gorica (D1)" },
            { "Gas Station - Little Farm (B2)", "Estación de Servicio - Granja Pequeña (B2)" },
            { "Gas Station - Little Farm (B3)", "Estación de Servicio - Granja Pequeña (B3)" },
            { "Gas Station - Marof (C3)", "Estación de Servicio - Marof (C3)" },
            { "Gas Station - Naval Base (A4)", "Estación de Servicio - Base Naval (A4)" },
            { "Gas Station - Northeast (C0)", "Estación de Servicio - Noreste (C0)" },
            { "Gas Station - Northwest (C0)", "Estación de Servicio - Noroeste (C0)" },
            { "Gas Station - Novigrad (Z2)", "Estación de Servicio - Novigrad (Z2)" },
            { "Gas Station - Preko (B4)", "Estación de Servicio - Preko (B4)" },
            { "Gas Station - Prkno (B1)", "Estación de Servicio - Prkno (B1)" },
            { "Gas Station - Samobor (D3)", "Estación de Servicio - Samobor (D3)" },
            { "Gas Station - Ston (D4)", "Estación de Servicio - Ston (D4)" },
            { "Gas Station - Tisno East (A3)", "Estación de Servicio - Tisno Este (A3)" },
            { "Gas Station - Tisno South (A2)", "Estación de Servicio - Tisno Sur (A2)" },
            { "Gas Station - Vrsar (Z0)", "Estación de Servicio - Vrsar (Z0)" },
            { "Gas Station - WW2 Bunker (C1)", "Estación de Servicio - Búnker 2da Guerra (C1)" }
        };

        public static void SelfTest() {
            // Verify that all keys in English exist in Spanish (Argentina) and vice versa
            foreach (string key in StringsEn.Keys) {
                if (!StringsEsAr.ContainsKey(key)) {
                    throw new Exception("Missing Spanish (Argentina) translation for key: " + key);
                }
            }
            foreach (string key in StringsEsAr.Keys) {
                if (!StringsEn.ContainsKey(key)) {
                    throw new Exception("Missing English translation for key: " + key);
                }
            }

            // Verify basic formatting interpolation works in both languages
            AppLanguage orig = Current;
            try {
                Current = AppLanguage.English;
                string enFormatted = T("GridOpacity", 75);
                if (enFormatted != "Grid opacity: 75%") throw new Exception("English formatting failed: " + enFormatted);

                Current = AppLanguage.SpanishArgentina;
                string esFormatted = T("GridOpacity", 75);
                if (esFormatted != "Opacidad de la cuadrícula: 75%") throw new Exception("Spanish formatting failed: " + esFormatted);
            } finally {
                Current = orig;
            }
        }
    }
}
