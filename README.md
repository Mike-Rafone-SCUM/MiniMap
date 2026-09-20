# SCUM MiniMap v1.4.7

SCUM MiniMap is an external map overlay and navigation utility for SCUM, made for the Skynett community. It displays player coordinates, heading, elevation, road routes, points of interest, and custom waypoints. It does not read game memory or inject DLLs; this is not an official anticheat certification.

Run `SkynettMiniMap.exe` from the extracted release directory. High-resolution map artwork, road networks, and over 8,000 categorized ScumMap points of interest across 106 categories are embedded directly into the executable.

> [!IMPORTANT]
> SCUM must run in **Borderless Window** or **Windowed** mode. Windows exclusive fullscreen mode takes dedicated hardware control of the display and suppresses external desktop overlays.

---

## Official Distribution & Automatic Updates

Official binaries, update manifests, and cryptographic checksums are distributed exclusively via GitHub Releases:

* **Official Repository:** [Mike-Rafone-SCUM/MiniMap](https://github.com/Mike-Rafone-SCUM/MiniMap)
* **Latest Release:** [GitHub Releases Latest](https://github.com/Mike-Rafone-SCUM/MiniMap/releases/latest)
* **Standalone Executable:** [SkynettMiniMap.exe](https://github.com/Mike-Rafone-SCUM/MiniMap/releases/latest/download/SkynettMiniMap.exe)
* **Complete Archive:** [SkynettMiniMap.zip](https://github.com/Mike-Rafone-SCUM/MiniMap/releases/latest/download/SkynettMiniMap.zip)

### Integrated Auto-Updater
SCUM MiniMap checks the version in the official `update.txt` manifest at startup. Startup results are cached for up to an hour; manual checks through **Check for updates (GitHub)** in the tray menu or Settings reuse results for up to one minute. A newer release triggers a notification. Use a manual check to choose and save the download; its SHA-256 checksum is verified before it is saved.

Exit the old app from its tray menu, replace the executable with the downloaded file, then launch it. Installation is manual. Saved settings and zones are retained.

### Discord support

Join the [SCUM MiniMap Discord](https://discord.gg/MYzcGaFDMn) for downloads, release notices, guides, and support in English, Spanish (Argentina), French, German, Dutch, Russian, Chinese, Turkish, and Arabic. Select your preferred languages in **Channels & Roles** or through the Welcome gateway buttons. The app's language setting is separate from Discord's language selection.

---

## Key Features

### In-Game Full Map Mode (M Key)
Pressing <kbd>M</kbd> during gameplay seamlessly transitions the minimap into a full-scale, 1:1 square tactical map centered directly over SCUM's built-in in-game map.
* **Full Monitor Height & Centering:** Unconstrained display geometry detection expands the overlay to the full display height (1080p, 1152p, 1440p, 4K) and horizontally centers it (`(ScreenWidth - ScreenHeight) / 2`), leaving peripheral HUD elements (health, stamina, speedometer) visible.
* **Mouse Wheel Zoom & Pan:** Roll the mouse wheel while viewing the full map to zoom smoothly from 1.0x up to 16.0x towards the cursor. Click and drag with the left mouse button to pan across the island. Right-click opens map actions, including resetting the view and adding a custom waypoint at the clicked location.
* **Floating Opacity Slider:** An interactive glass pill at the top-right corner allows adjusting overlay opacity in real time from 20% to 100%, letting you see through the overlay directly into the game environment.
* **Non-Activating Window Hit-Testing:** Interacting with the map (zooming, panning, sliding opacity) uses non-activating hit testing so SCUM never loses keyboard focus or gameplay responsiveness.
* **Smart Auto-Restore:** Restores normal minimap dimensions and position when pressing <kbd>M</kbd> again, pressing <kbd>Esc</kbd>, closing the map in SCUM (detected via cursor hiding), or switching away from the game.

### Custom Waypoints & Search List Management
* **Save at Current Location (Insert Key):** Press <kbd>Insert</kbd> while playing to capture your current GPS coordinates. An input dialog immediately captures keyboard focus to let you name the location, saving it persistently to `zones.tsv` with a cyan marker.
* **List Management & Right-Click Deletion:** Open the waypoint list (<kbd>Delete</kbd> key) to search, navigate to, or manage locations. Right-clicking any custom waypoint displays a context menu to delete it, or highlight it and press <kbd>Delete</kbd>.
* **Proximity Clearing:** Pressing <kbd>Insert</kbd> within 50 meters of an existing custom waypoint prompts you to delete it directly in the field.
* **SCUM Key Rebinding Wizard:** Settings can capture every SCUM binding used by MiniMap: Map, Chat, and the coordinate-copy modifier/key. This prevents custom bindings such as Ctrl for free look from moving the camera during tracking.

### Road-Aware GPS Navigation
* **A* Driving Route Pathfinding:** Real-time pathfinding across SCUM's road network calculates driving trajectories, turns, and remaining road distance in milliseconds.
* **Calibrated Alignment:** Calibrated coordinate transform offsets (+200 cm X/Y) ensure vehicle markers and navigation trajectories align directly along road centrelines.
* **High-Contrast Feeder Vectors:** Off-road dashed guidance lines connect your vehicle to the nearest road entry point.

### POI Filtering & Smart Label LOD
* **POI Filters:** Use Configure POI filters or the full-map category list for locations such as gas stations and bunkers. Custom waypoints and imported zone layers have separate visibility controls.
* **Independent Bunker Filtering:** Bunkers and abandoned underground facilities can be shown or hidden independently from surface military sites.
* **Smart Level of Detail (LOD):** When zoomed out (< 2.5x), minor text labels (towns, farms, military sites, faction tags) are automatically suppressed to keep terrain and roadways clean and legible. Only major Cities, Traders, and Custom Waypoints display text labels. Detailed local tags smoothly reappear as you zoom in closer.
* **Independent Border & Label Controls:** Toggle POI text names (`ShowZoneLabels`) independently from colored zone borders (`ShowZones`).

### Interface Languages
Choose English, Argentine Spanish, French, German, Dutch, Russian, Simplified Chinese, Turkish, or Arabic in Settings without restarting. The initial language follows your system language; untranslated labels fall back to English.

### Custom Map Textures & Zone Import
* Supports custom `map.png` textures and custom `zones.tsv` files in `%LocalAppData%\ScumMiniMap`.
* Includes a **Reset to default map** option to revert to embedded high-resolution assets without manual file deletion.

---

## First Launch: Size and Position

New installations start with a compact 240 × 240 minimap in the upper-right corner. The setup guide's **Move and resize minimap** button opens the Appearance settings. Switch to the desktop to drag the minimap into position or drag its edges to resize it; use **Home → Appearance** for exact width and height. Changes save automatically. Reopen the setup guide from Settings whenever needed.

Existing saved layouts are retained. Opening the full map does not replace your saved minimap size or position.

## Controls & Keybinds

| Input | Mode | Action |
|---|---|---|
| <kbd>M</kbd> | In-Game | Toggle Full Map Mode (centered full-height overlay; ignored while typing in chat) |
| <kbd>Left Click</kbd> | Full Map | Click anywhere to place/route GPS marker (re-click marker to clear) |
| <kbd>Mouse Wheel</kbd> | Full Map | Zoom in and out smoothly (1.0x to 16.0x toward cursor) |
| <kbd>Left Click + Drag</kbd> | Full Map | Pan across the map (when zoomed in) or drag opacity slider |
| <kbd>Right Click</kbd> | Full Map | Open map actions: add custom waypoint here, reset zoom, or clear active GPS waypoint |
| <kbd>Home</kbd> | Any | Open / Close Settings panel |
| <kbd>Delete</kbd> | Any | Open Waypoint Search & Destination Navigation |
| <kbd>End</kbd> | Any | Show / Hide Minimap Overlay |
| <kbd>Insert</kbd> | In-Game | Save custom waypoint at current GPS position |
| <kbd>Page Up</kbd> | Minimap | Manual zoom in |
| <kbd>Page Down</kbd> | Minimap | Manual zoom out |
| <kbd>Escape</kbd> | Any | Close active search/settings dialog, or exit full map mode |

---

## Intelligent Search & Navigation

* **Fuzzy & Phonetic Matching:** Accent-insensitive, typo-tolerant search handles aliases and abbreviations (e.g., `mil air`, `airfeild`, `nafta`, `bunker C3`, `nearest trader`).
* **Sector Search:** Enter any sector code (`D4`, `C2`, `B0`) to highlight and navigate to the sector center.
* **Auto-Clear on Arrival:** Active navigation targets clear automatically once your character arrives within 30 meters of the destination.

---

## Architecture & Anticheat Safety

SCUM MiniMap runs as an external Windows application:

* **Zero DLL Injection:** Operates entirely outside the SCUM process space as a standard layered Windows desktop utility.
* **No Game Memory Access:** Does not attach debuggers, inspect process RAM, hook DirectX/Vulkan APIs, or modify game files.
* **Clipboard-Based Coordinate Acquisition:** Samples coordinates strictly through Windows standard clipboard copy commands triggered while SCUM is focused.
* **Safe Input Suspension:** Automatically pauses coordinate polling while aiming down sights (holding right mouse button in ADS), while typing in chat (after pressing the configured SCUM Chat key or <kbd>/</kbd>), while modifier keys are held, or when SCUM loses window focus. Includes a 600ms cooldown after releasing ADS to prevent macro interference during gunplay.

---

## System Requirements

* **Operating System:** Windows 10 / 11 (64-bit)
* **Display Mode:** Borderless Window or Windowed Mode
* **Framework:** Microsoft .NET Framework 4.0 or higher
* **Optional:** Python 3.8+ (only required if executing `detect_zones.py` for automated CV zone extraction)

---

## Data & Configuration Directory

All user settings, logs, and custom assets are stored in:
```
%LocalAppData%\ScumMiniMap
```
* `settings.ini`: Persistent user preferences, layout, filters, and language settings.
* `zones.tsv`: Categorized point-of-interest database and saved custom waypoints.
* `automatic.log`: Diagnostic communication and updater activity log.
* `map.png`: Optional custom map override texture.

---

## Repository contents

This public repository contains documentation and downloadable releases. Application source, development scripts, map assets, and Discord administration files are maintained separately and are not included in a public clone.

Each GitHub release provides `SkynettMiniMap.exe`, the complete `SkynettMiniMap.zip` package, and the `update.txt` version and SHA-256 manifest. Use the download links above to install the app.

---

## Attributions & Third-Party Credits

SCUM MiniMap incorporates public community data, open-source libraries, and game references with sincere appreciation:

* **[Scum-Map.com](https://scum-map.com)**: POI coordinate datasets (8,000+ points across 106 categories), vector hunting biome regions (`hunting_biomes.svg`), and Island Wildlife Guide species behavioral data. Maintained by Jazi and the Scum-Map community.
* **[Davo's SCUM Interactive Map](https://davoonline.com/scummap/)**: Aquatic life spawning volume extents (`EMBEDDED_AQUATIC_LIFE`, 187 volumes) and fish species preset groupings (`FISH_SPECIES_BY_PRESET`). Created by Davo / DavoOnline.
* **[scummymap.com](https://scummymap.com)**: Map legend definitions and reference cross-checking.
* **Game Intellectual Property**: SCUM is developed by **Gamepires** and published by **Jagex**. All game artwork, terrain textures, sector names, item definitions, and trademarks belong to Gamepires d.o.o. and Jagex Ltd. SCUM MiniMap is an independent, non-commercial community tool made for the Skynett Gaming community.
* **Open Source Software**: [OpenCV](https://opencv.org/) (Apache 2.0), [NumPy](https://numpy.org/) (BSD 3-Clause), [RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) (MIT), and [Tabler Icons](https://tabler.io/icons) (MIT).

See [ATTRIBUTION.md](ATTRIBUTION.md) for full license details, source citations, and copyright notices.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the complete history of updates, feature additions, and bug fixes.
