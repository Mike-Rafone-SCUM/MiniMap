# Changelog

All notable changes to **SkynettMiniMap** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.3.9] - 2026-09-14

### Added
- **Interactive Habitat Boundaries & Spawning Extents**: Full integration of 187 aquatic volume rectangles and 4 regional hunting biomes (Mountain, Mediterranean, Continental Forest, Continental Meadow) across 24 species categories (15 fish types including radioactive carp, 9 animal species).
- **Multi-Ring Winding Hole Geometry**: Advanced winding fill rendering and geometric point-in-polygon containment that properly excludes inner water bodies, clearings, and internal terrain holes from surrounding biome shapes.
- **Species-Specific Presets & Exclusivity**: Dedicated species presets and filtering (e.g. Tuna, Dentex, Pike, Sardine, Radioactive Carp) with accurate habitat assignment and click-to-identify waypoints.
- **Granular Custom Map Importer Controls**: Upgraded the Map Zones screenshot importer with comprehensive zone management controls:
  - **Delete All Zones**: Added a safety-confirmed action to immediately clear all custom map zones from memory and disk.
  - **Direct Zone Search & Filtering**: Real-time filter box in the zone list for finding specific zones by name or tag.
  - **Reordering & Layer Management**: Added Up/Down controls to adjust drawing and rendering priority among overlapping zones.
  - **Zone Duplication**: Quick one-click duplication of existing zone shapes and coordinates for fast variant creation.
  - **Batch Recolor by Group**: Color selection can now be applied across all zones sharing the selected zone's original color.
  - **Append vs Replace Import Mode**: Checkbox to either append newly detected or imported zones onto existing collections or replace all.
  - **Direct File Import & Export**: Load and save custom zones to `.json` or `.tsv` files for seamless backup and sharing.
  - **Wireframe vs Filled Polygon Mode**: Toggle translucent polygon fills or clean wireframe outlines with vertex points.
  - **Zone Inspector & Details Status Bar**: Live display of vertex counts, center sector (e.g. B2), color hex, and total zone counts.
  - **Quick Clear in Settings**: Added a direct "Clear all custom zones..." shortcut in Settings -> Map and zones for quick maintenance.
- **Dedicated Full-Map Right-Side Sidebar & Clean Map Canvas**: Integrated a high-contrast dark control panel docked immediately to the right of the square map, completely removing all floating cards, buttons, or sliders from the map canvas for an unobstructed view of the island:
  - **Pinned Tactical Header**: Real-time zoom readout with `[-]`, `[+]`, and `[1x]` reset shortcuts, smooth horizontal map opacity slider, and search button with active destination readout and one-click clear button.
  - **General Map Layer Switches**: Instant sleek toggle switches for Custom Zones, Custom Waypoints, Gas Stations, Grid Lines, and Zone Labels.
  - **Full POI Category System with Collapsible Accordions**:
    - Complete accordion hierarchy for all major sections (`Bunkers`, `Vehicles`, `Hunting`, `Crops`, `Outposts`, `Buildings`, `Fishing`, `Radiation`, etc.) with `▼` / `►` foldout toggles.
    - Master toggle switch per section supporting enabled, disabled, and partially active states, plus an active count badge (e.g. `3/7`, `6/6`).
    - Instant **All** / **Off** master controls to toggle all POI categories across the entire map with a single click.
    - Individual category toggle switches with custom color swatch dots (`ColorBackground`), localized titles, and live marker count badges (e.g. `WW2 bunkers (53)`, `Hunting Towers (161)`).
  - **Embedded Wildlife Biomes Reference**: Seamlessly integrated directly into the expanded `Hunting` section, detailing the 4 regional biomes (Mediterranean `#49B7CB`, Meadow `#FFDB2B`, Forest `#41B549`, Mountain `#DDDDDD`), their resident animal fauna, and the mutant beast roaming footnote without obstructing map terrain.
  - **Smooth Scrolling & Draggable Scrollbar**: Full vertical navigation via mouse wheel (48px per notch) and a draggable high-contrast scrollbar thumb on the right edge with clipped hit-testing.
  - **Quick Tool Shortcuts**: Instant modals for Custom Zone Editor and Settings, along with in-game hotkey tips.

### Changed
- **Streamlined Default Map Markers**: Reduced default initial startup markers from 758 down to 154 essential landmark markers (safezone trader outposts, key underground bunkers, gas stations, police stations, vehicle repair, pharmacies, gun shops, hospitals, and lighthouses), eliminating map clutter on fresh launches.
- **Merged Fish & Hunting Habitats**: Unified 15 fish species and 9 wildlife habitat categories directly into the primary "Fishing" and "Hunting" sections in the POI filter dialog, rather than displaying redundant separate habitat sections.
- **Habitats Disabled by Default**: All dense habitat boundary layers now start turned off by default for clean initial exploration, while remaining easily toggled via the quick activity buttons in the POI filter dialog.

### Fixed
- **Settings Reopening & Persistence**: Resolved an early initialization issue where pre-existing settings files could fail to load saved filter configurations on startup.
- **Small Zone Selection Priority**: Ensured smaller specific fishing spots and local waypoints take precedence over large surrounding biome regions during hit testing and map clicks.

## [1.3.8] - 2026-09-13

### Added
- **Instant POI Search & Discovery**: Destination search (`Delete` key) now instantly lists nearby ScumMap POIs and custom zones sorted by distance when opened without typing. Search queries match marker titles, categories (e.g., "police", "bunker", "well", "pharmacy"), and sectors (e.g., "bunker d4").
- **Clear Category Context in Search**: Search results now display the specific POI category and sector alongside the distance (e.g. `B2 / Police station / 450 m`).

### Changed
- **Streamlined 3-Layer Map Architecture**: Reorganized Map Layers in Settings into three clear, distinct layers:
  1. `Custom Map Zones (using the PNG)` (with screenshot importer)
  2. `ScumMap Pois` (with POI filter dialog)
  3. `User Added Pois` (custom waypoints placed by player)
  Removed redundant legacy sub-category checkboxes from the Settings interface.

### Removed
- **Legacy Pin Diamond Marker**: Removed obsolete yellow diamond marker and literal "PIN" label drawn on the map when placing waypoints or clicking destinations. All points and destinations now render through clean category dots and GPS navigation routing.

## [1.3.7] - 2026-09-13

### Added
- **Full ScumMap Location Filter Alignment**: Comprehensive alignment of POI categories matching the official [scum-map.com](https://scum-map.com) Island catalog. Integrated all 106 categories across 12 sections covering 8,056 POI locations.
- **Dedicated POI Filter Dialog**: Added a searchable filter management window accessible via Settings -> Map and zones -> "Configure POI Filters (106 categories)...". Supports real-time text filtering, section-wide batch toggles, individual category selection with location counts, and default state reset.
- **Master & Group Settings Controls**: Added a master switch `Show ScumMap POIs` and granular category persistence across sessions in `settings.ini`.
- **POI Click-to-Waypoint & Search**: Clicking any visible ScumMap POI locks onto that landmark with its exact name and coordinates. Integrated POI matching into the Destination Search dialog (`Delete` key).

### Changed & Performance
- **High-Performance Spatial Indexing & Viewport Culling**: Embedded `scummap.bin` (241 KB) with a 32x32 spatial index grid. Only visible markers in the player's viewport are evaluated (< 0.05 ms query time), maintaining steady 60+ FPS overlay rendering with zero idle redraw overhead.

### Fixed
- **Faction Bunker Classification**: Fixed an issue in `Zones.cs` where faction bases containing "WWII Bunker" in their name were misclassified as generic bunkers due to substring evaluation order.

## [1.3.6] - 2026-09-13

### Fixed
- **Coordinate Telemetry Sampling Chord Fix**: Fixed an issue where coordinate sampling aborted during the key chord due to the synthetic control key triggering a busy keys false positive.
- **Mouse Input Busy Check Refinement**: Removed left mouse button from persistent busy key suppression to prevent desktop or game click conflicts.

## [1.3.5] - 2026-09-13

### Changed
- **Aim Down Sights (ADS) Tracking Suppression**: While holding the right mouse button to aim down sights (ADS) in SCUM, automatic coordinate sampling and background copy macros are paused.
- **Post-ADS Cooldown Guard**: Releasing ADS introduces a 600ms safety buffer before telemetry sampling resumes, preventing synthetic keystrokes from interrupting active gunplay, recoil control, or weapon transitions.

## [1.3.4] - 2026-09-13

### Added
- **Click-to-Place GPS Marker**: Left-clicking anywhere on the full-screen map now instantly sets a GPS waypoint and navigation route directly to that point. Snaps to named POIs when clicked near one, or creates a sector-labeled GPS marker (e.g., `GPS Marker (C3)`). Re-clicking the active marker or right-clicking at 1.0x zoom clears the waypoint.

### Fixed
- **In-Game Chat Guard for M Key**: Gated `M` key handling behind active chat state across both the low-level keyboard hook and physical polling loop. Pressing `T` or `/` pauses hotkey triggers, ensuring typing words containing the letter 'm' in in-game chat will never accidentally trigger or toggle Full Map mode until `Enter` or `Escape` closes the chat box.

## [1.3.3] - 2026-09-13

### Added
- **Full-Map Mouse Wheel Zoom & Drag Pan**: Full Map Mode (`M` key) now supports interactive mouse wheel zooming (1.0x to 16.0x) and left-click drag panning across the island without stealing keyboard focus from SCUM. Right-click resets to default 1.0x view.
- **Floating Opacity Slider**: Added a sleek glass pill control in the upper right of the full-screen map with real-time opacity adjustment (20% to 100%), allowing players to see through the map overlay directly into the game world.
- **Independent Bunker POI Filter**: Separated military bunkers and abandoned bunkers from the general military category, providing an independent toggle in Map & Zone settings.

### Fixed
- **Full-Map Display Height**: Removed WinForms form maximum size constraints (`MaximumSize = Size.Empty`), allowing the full map mode overlay to expand to the full display height (up to 1152p / 1440p / 4K) directly covering SCUM's native in-game map.

## [1.3.2] - 2026-09-13

### Fixed
- **Full Map Centering**: Calibrated Full Map Mode (M key) to detect the SCUM game monitor and center the 1:1 square overlay horizontally (`(ScreenWidth - ScreenHeight) / 2`), matching the in-game map position and covering it at 100% opacity without obscuring peripheral HUD elements.

## [1.3.1] - 2026-09-13

### Added
- **Right-Click Deletion of Custom Waypoints**: Added context menu and Delete key shortcut in the waypoint search list to delete custom markers directly without walking back to them.
- **Smart POI Label LOD**: Automatically suppresses cluttered local town, farm, military, and faction text labels when zoomed out (< 2.5x), displaying only major Cities, Traders, and Custom Waypoints to keep roads and terrain crystal clear.
- **Independent Zone Label Toggle**: Added `ShowZoneLabels` setting to toggle POI text labels on/off independently from zone borders.
- **Smart Label LOD Toggle**: Added `SmartLabelLod` setting in Map and zones to toggle automatic zoom-based label decluttering.

### Fixed
- **Full Map Mode (M Key)**: Added 0x4D to the low-level keyboard hook filter and resolved timing conflict with user activity detection, enabling the M key to transition the minimap into a full-screen rectangle at 100% opacity covering SCUM's in-game map. Added auto-restore on Esc, cursor disappearance, or game focus loss.
- **Waypoint Name Input Focus**: Configured custom marker prompt dialog with tab index 0, active control assignment, and foreground window activation so the text input box immediately draws keyboard focus and ready cursor.
- **Live Settings Update & Invalidation**: Wired up `SettingsChanged()` and expanded `terrainKey` caching so toggling any POI category or label filter immediately refreshes the map and persists to `settings.ini`.

## [1.3.0] - 2026-09-13

### Added
- **POI Category Filtering**: Added customizable filter options in Settings to selectively show or hide POI categories on the minimap:
  - Cities (Samobor, Klenovnik Hospital, Novigrad)
  - Towns, villages, and local settlements
  - Farms, vineyards, and agricultural locations
  - Trader outposts (C2, B4, A0, Z3, CC)
  - Faction War POIs
  - Military installations, bunkers, airfields, barracks, and radar sites
  - Fuel / Gas stations
  - Custom user waypoints
- **Category Tagging Engine**: Tagged all 144 map POIs with explicit category designations in zones.tsv, with backward-compatible 4th-column format and dynamic runtime heuristic fallback.
- **Custom Waypoint Saving**: Pressing Insert now prompts for a custom waypoint name and permanently saves the location to zones.tsv. Pressing Insert while near an existing custom waypoint prompts for confirmation to delete it. Custom waypoints are rendered with a distinct cyan badge and label.
- **Full-Size In-Game Map Mode**: Pressing M while in SCUM expands the minimap into a full-size rectangle covering the screen at 100% opacity, matching the in-game map view. Pressing M again or losing focus smoothly restores standard minimap dimensions and opacity.
- **Custom Map Polish**: Added image dimension validation (minimum 1024x1024) and confirmation preview dialog prior to importing custom maps, along with a 'Reset to default map' button to restore the embedded map image.

### Fixed
- **Settings Panel Auto-Scrolling**: Fixed issue where the settings viewport snapped upwards every second during live coordinate updates by overriding ScrollToControl with AutoScrollPosition and suspending container layout during status text refreshes.
- **Import Zones Unhandled Exception**: Fixed unhandled UriFormatException during TSV loading by safely unescaping names, guarded against null points in zone serialization, and ensured reliable recursive cleanup of temporary screenshot analysis directories.
- **Map Alignment Calibration**: Calibrated ToMap() coordinate projection offsets (+200 cm X/Y) so that road navigation GPS lines up directly on roads rather than adjacent to them.

## [1.2.5] - 2026-09-13

### Changed
- GitHub now hosts first downloads, updates and the MiniMap source project.
- Added a complete download ZIP with the optional screenshot-import files and corrected Windows launcher.
- Removed the obsolete itch.io address from the application source and current installation instructions.

## [1.2.4] - 2026-09-13

### Changed
- Application updates now use Mike-Rafone-SCUM/MiniMap on GitHub. itch.io remains the initial download location.
- Checks compare stable release versions from a strict manifest instead of scraping page text.
- Updates download on request and are verified against the release SHA-256 checksum. Users close the app and replace the executable manually; settings and zones are preserved.
- Cached checks reduce requests, and retry delays survive app restarts. Missing releases, invalid metadata and connection failures never report that the app is up to date.

### Added
- Isolated GitHub release build, manifest generation, publishing script and updater regression tests.

## [1.2.3] - 2026-09-13

### Added
- **Itch.io Live Executable Name Matching**: Update checker now queries and parses uploaded binary filenames directly from the itch.io store page (`v<version> - SkynettMiniMap.exe`), displaying the exact live itch.io release file and validating up-to-date status.
- **Version-Named Release Binaries**: Automated builder now generates version-stamped standalone executables in `release/` (e.g. `v1.2.3 - SkynettMiniMap.exe`) formatted for direct drag-and-drop upload to itch.io.

### Changed
- **Single Root Binary Packaging**: Cleaned up all legacy and intermediate executables. Root directory now hosts a single dedicated standalone binary (`SkynettMiniMap.exe`).
- **Streamlined Builder Pipeline**: `Build.ps1 -Install` refreshes the single root executable without generating duplicate alias binaries (`ScumMiniMap.exe`, `*.previous.exe`).

## [1.2.2] - 2026-09-13

### Added
- **Road-Aware GPS Navigation System**: Embedded full topological road network (`roads.bin`) with fast native A* pathfinding in `RoadRouter.cs`. Calculates true driving distance and renders a curved cyan route polyline along the road network.
- **Fuzzy Search for Outposts & Traders**: Expanded search engine (`Search.cs`) with trader synonyms (`trader`, `comerciante`, `outpost`, `vendor`, `safezone`, `puesto`) and edit-distance tolerance ($\le 2$), enabling instant matching for queries like `trader`, `comerciante`, `traider B4`, or `outpost`.
- **Full Zone Name Localization (`es-AR`)**: Added 121 comprehensive Argentine Spanish translations for all non-proper POIs, settlements, military installations, bunkers, and gas stations across radar labels, HUD status lines, and search items.

### Changed
- **Thicker Off-Road Dashed Line Guidance**: Increased the line weight and opacity of off-road feeder segments (from player/target to the road network) and straight fallback navigation lines to 3.0px for clear visibility against all terrain backgrounds.
- **Real-Time Language Cache Invalidation**: Map labels, HUD text, and route suffixes now rebuild dynamically upon changing language in Settings without requiring an application restart.

## [1.2.1] - 2026-09-13

### Added
- **All 25 In-Game Gas Stations Added**: Integrated exact fuel pump locations across all sectors (A1 to Z3) extracted directly from the authoritative server database (`SCUM.db`).
- **Dedicated Fuel Icon Badges**: Replaced heavy zone polygons with compact, anti-aliased orange fuel badges (`#FF8800`) featuring a crisp white fuel pump silhouette on the radar.
- **Waypointable Fuel Stations**: Full navigation support for gas stations via the Search dialog (Delete key). Search `gas`, `fuel`, `petrol`, `nearest gas`, or by sector/town (e.g. `gas samobor`, `fuel D3`), press Enter to lock waypoint, and auto-clear upon arrival within 30 metres.
- **Independent Gas Station Visibility Toggle**: Added "Show gas stations (fuel icons)" in Settings under Map & Zones so fuel markers can be toggled on/off independently from territory zones.

## [1.2.0] - 2026-09-13

### Added
- **Full English & Argentine Spanish Localization (`es-AR`)**: Complete bilingual support across all interface panels, system tray menus, HUD radar overlays, notifications, and dialogs.
- **Dynamic In-Game Language Selector**: Added an interactive Language dropdown in the Settings panel under "Language / Idioma" that switches between English and Español (Argentina) instantly in real time without restarting.
- **Authentic Argentine Survival Conventions**: Applied authentic Argentine Spanish phrasing with *voseo* (*Probá*, *Elegí*, *Guardá*, *Hacé clic*, *Ingresá*, *Abrí*, *Alineá*, *Revisá*, *Presioná*) and survival terminology (*nafta*, *punto de ruta*, *marcador*, *cuadrícula*).
- **Localized POI & Sector Search**: Accent-insensitive fuzzy search aliases (`aeropuerto`, `aerodromo`, `nafta`, `combustible`, `estacion`, `puerto`, `facción`, `faccion`, `cuadrícula`, `cuadricula`, `cerca`).
- **Automatic System Culture Detection**: Unconfigured installations automatically detect Argentine / Uruguayan / Spanish OS cultures and default to Español (Argentina).

### Changed
- **UI Layout & Typography Fitting**: Expanded button and label bounds across the Search dialog, Zone Editor, and Settings sliders to ensure clean presentation of localized text without truncation.

## [1.1.7] - 2026-09-13

### Fixed
- **Hotkey & Menu Opening Reliability**: Resolved critical issues where hotkeys (`Home`, `Delete`, `End`, `Insert`, `PgUp`, `PgDn`) intermittently failed to open the menus or respond:
  - Re-enabled physical polling via `Native.GetAsyncKeyState()` in `CheckHotkeysAsync()`, coordinating down-state flags with the low-level keyboard hook (`GameKeys`) to eliminate race conditions without silencing the polling loop.
  - Ensured hotkeys are checked unconditionally every 40 ms at the start of `Tick()`, eliminating polling stalls caused by `tickBusy` during automatic coordinate copying.
  - Self-healing `held` key state tracking in `GameKeys` using real-time physical key state to recover automatically from lost or dropped `WM_KEYUP` messages.
  - Allowed injected keystrokes outside of internal copy chords so software-remapped gaming keyboard and mouse buttons trigger hotkeys reliably.
  - Improved `ToggleSettings()` to bring an already-open background Settings window to the foreground and focus it rather than hiding it when pressing `Home` from within the game.
  - Added `Native.ForceForeground()` using `SetWindowPos` (`HWND_TOPMOST`) to guarantee menu activation over borderless fullscreen game windows.

## [1.1.6] - 2026-09-13

### Fixed
- **Update Check Reliability**: Added 10-second connection and read timeouts to update checks, and prevented completed checks from opening dialogs after the minimap closes.
- **Shutdown Stability**: Prevented new tracking ticks once shutdown begins, and disabled online update checks in diagnostic mode.

### Changed
- **Stability Validation**: Expanded regression checks for failed key presses, failed key releases and interrupted copy sequences. Verified settings, search, waypoint arrival, persistent pins and rendering cache behaviour.

## [1.1.5] - 2026-09-13

### Fixed
- **Intermittent Tracking Pauses**: Distinguished coordinate requests cancelled by focus or user input from Windows input failures. Cancelled requests now resume at the normal sampling interval instead of triggering a five-second retry delay, while input safety checks and failure backoff remain in place.

## [1.1.4] - 2026-09-13

### Changed
- **Player-Placed Pins**: Renamed player-placed markers to "Pins" throughout hotkey help, status messages and map labels. Pins placed with `Insert` remain until manually cleared.
- **Position Sampling Frequency**: Limited automatic coordinate requests to once per second. Faster saved intervals now migrate to 1000 ms, while slower custom intervals are retained.

### Fixed
- **Waypoint Arrival Clearing**: Navigation waypoints now clear automatically when a fresh coordinate update places the player within 30 metres of the destination centre.
- **Unintended Crouch Input**: Improved `Ctrl+C` timing by holding Control before and after the C press. Added cancellation before C is sent when focus or physical input changes, plus retries for failed key releases, to address unintended crouching during automatic tracking.
- **Physical Key Monitoring**: Prevented injected shortcut events from updating physical held-key and chat monitoring state.

## [1.1.3] - 2026-09-12

### Added
- **Discord Community Integration**: Added direct "Join Discord community..." actions to both the system tray context menu and the in-game Settings panel (`Home`) linking directly to the official community server (`https://discord.gg/MYzcGaFDMn`).

### Changed
- Performance refinements and maintenance release.

## [1.1.2] - 2026-09-12

### Added
- **Automated Version Incrementing**: Build script (`Build.ps1`) automatically increments semantic patch version with each build/package run.
- **Enhanced Process Management**: Robust process closure and reliable working-directory launch on build install.

## [1.1.1] - 2026-09-12

### Fixed
- **Settings Menu Flickering**: Resolved race condition between `GameKeys` keyboard hook and 40 ms timer polling loop in `CheckHotkeysAsync()`.
- **Settings Viewport Snapping**: Removed `MouseEnter` accordion header triggers and implemented `SettingsPanel` with `ScrollToControl` override to prevent scroll position resetting to top.

## [1.1.0] - 2026-09-12

### Added
- **Live Update Checker**: Automated version check querying the official itch.io store page in the background. Notifies players on the HUD when updates are available and provides a manual Check for updates... action in Settings and the system tray context menu.
- **Custom High-Def Map Import**: Added an in-app Import custom high-def map... file picker in Settings to import custom map.png textures into %LocalAppData%\ScumMiniMap without manual file copying.
- **Taskbar Registration**: Enabled ShowInTaskbar for Settings and Search windows so they remain directly accessible and interactable when alt-tabbing or running alongside borderless fullscreen games.
- **Escape Key Dismissal**: Added Escape key handling to Settings and dialogs to quickly return focus to SCUM.

### Changed
- **Settings Panel Navigation**:
  - Removed hover-based (MouseEnter) accordion switching that previously collapsed sections and violently snapped the scroll position back to the top during mouse wheel scrolling.
  - All settings categories (Navigation, Map and zones, Appearance, Tracking and zoom, Layout, Tools and shortcuts) are now expanded and visible by default.
  - Clicking any section header folds or unfolds only that section (▼ / ►) without resetting the viewport or collapsing other sections.
  - Implemented SettingsPanel overriding internal Windows Forms ScrollToControl focus handling, preventing viewport snapping whenever sliders, number inputs, or buttons gain focus.
- **README Documentation**: Updated shortcut documentation and control references to reflect v1.1.0 layout and behavior.

### Fixed
- **Dynamic Player Centering**: Resolved critical bug where the map remained anchored at fixed coordinates during zoom (zoom > 1), causing the player blip to walk out of the circular radar mask and disappear. The map now continuously centers on the player's live position, smoothly translating terrain, grid lines, and safe zones underneath.
- **Settings and Search Window Flickering**: Resolved race condition between the low-level keyboard hook (GameKeys) and the 40 ms timer polling loop in CheckHotkeysAsync() where pressing Home or Delete caused the window to open and immediately close within 10–40 ms.
- **Hotkey Debouncing**: Implemented global 450 ms debounce tracking across all hotkey actions (Home, Delete, End, Insert, Page Up, Page Down) to eliminate duplicate key-down dispatches.
- **Modal Parent Association**: Fixed ShowZoneSearch modal parent handling to avoid activation loss when opening over hidden parent windows.

---

## [1.0.0] - 2026-09-12

### Added
- External, non-invasive real-time tactical radar HUD overlay for SCUM.
- Automated position tracking via clipboard coordinate sampling when SCUM is focused.
- Intelligent POI and sector fuzzy search (e.g. airfield, bunker C3, grid D4).
- Circular and square overlay shapes with configurable edge fade and opacity.
- Dynamic speed-based auto-zoom and manual zoom controls.
- Distance-based safe zone and POI proximity detection with directional heading arrows and zone entry chimes.
- In-game chat and modifier key detection to automatically pause coordinate polling during typing.
- Custom map and zone support (%LocalAppData%\ScumMiniMap\map.png, zones.tsv).
- Optional Python-based computer vision script (detect_zones.py) for automated zone boundary extraction from map screenshots.
