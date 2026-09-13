# SCUM MiniMap v1.3.3

SCUM MiniMap is an external, real-time tactical radar HUD overlay and navigation utility engineered specifically for SCUM players. It renders an active minimap displaying player coordinates, heading, elevation, road network navigation, categorized points of interest (POIs), and custom waypoints without modifying game memory, injecting DLLs, or violating Easy Anti-Cheat (EAC) policies.

Run `SkynettMiniMap.exe` from the extracted release directory. High-resolution map artwork, road networks, and 144 categorized points of interest are embedded directly into the executable.

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
SkynettMiniMap features an automated background update service. On every startup (and via **Check for updates (GitHub)** in the tray menu or Settings panel), the application queries `update.txt` from the official repository and compares the semantic version and SHA-256 checksum against the running installation. When a verified build is available, the application notifies you with release details and offers to download the new build.

---

## Key Features

### In-Game Full Map Mode (M Key)
Pressing <kbd>M</kbd> during gameplay seamlessly transitions the minimap into a full-scale, 1:1 square tactical map centered directly over SCUM's built-in in-game map.
* **Full Monitor Height & Centering:** Unconstrained display geometry detection expands the overlay to the full display height (1080p, 1152p, 1440p, 4K) and horizontally centers it (`(ScreenWidth - ScreenHeight) / 2`), leaving peripheral HUD elements (health, stamina, speedometer) visible.
* **Mouse Wheel Zoom & Pan:** Roll the mouse wheel while viewing the full map to zoom smoothly from 1.0x up to 16.0x towards the cursor. Click and drag with the left mouse button to pan across the island. Right-click instantly resets to the centered 1.0x view.
* **Floating Opacity Slider:** An interactive glass pill at the top-right corner allows adjusting overlay opacity in real time from 20% to 100%, letting you see through the overlay directly into the game environment.
* **Non-Activating Window Hit-Testing:** Interacting with the map (zooming, panning, sliding opacity) uses non-activating hit testing so SCUM never loses keyboard focus or gameplay responsiveness.
* **Smart Auto-Restore:** Restores normal minimap dimensions and position when pressing <kbd>M</kbd> again, pressing <kbd>Esc</kbd>, closing the map in SCUM (detected via cursor hiding), or switching away from the game.

### Custom Waypoints & Search List Management
* **Save at Current Location (Insert Key):** Press <kbd>Insert</kbd> while playing to capture your current GPS coordinates. An input dialog immediately captures keyboard focus to let you name the location, saving it persistently to `zones.tsv` with a cyan marker.
* **List Management & Right-Click Deletion:** Open the waypoint list (<kbd>Delete</kbd> key) to search, navigate to, or manage locations. Right-clicking any custom waypoint displays a context menu to delete it, or highlight it and press <kbd>Delete</kbd>.
* **Proximity Clearing:** Pressing <kbd>Insert</kbd> within 50 meters of an existing custom waypoint prompts you to delete it directly in the field.

### Road-Aware GPS Navigation
* **A* Driving Route Pathfinding:** Real-time pathfinding across SCUM's road network calculates driving trajectories, turns, and remaining road distance in milliseconds.
* **Calibrated Alignment:** Calibrated coordinate transform offsets (+200 cm X/Y) ensure vehicle markers and navigation trajectories align directly along road centrelines.
* **High-Contrast Feeder Vectors:** Off-road dashed guidance lines connect your vehicle to the nearest road entry point.

### POI Filtering & Smart Label LOD
* **Granular Category Filters:** Toggle visibility for individual POI classes: Cities, Towns, Farms, Traders, Faction War POIs, Military Sites, Bunkers & Abandoned Bunkers, Custom Waypoints, and Gas Stations.
* **Independent Bunker Filtering:** Bunkers and abandoned underground facilities can be shown or hidden independently from surface military sites.
* **Smart Level of Detail (LOD):** When zoomed out (< 2.5x), minor text labels (towns, farms, military sites, faction tags) are automatically suppressed to keep terrain and roadways clean and legible. Only major Cities, Traders, and Custom Waypoints display text labels. Detailed local tags smoothly reappear as you zoom in closer.
* **Independent Border & Label Controls:** Toggle POI text names (`ShowZoneLabels`) independently from colored zone borders (`ShowSavedZones`).

### Real-Time Bilingual Interface
Complete bilingual support in English and Argentine Spanish (`es-AR`). Switch instantly in the Settings panel under **Language / Idioma** without restarting the application. Automatically defaults to Spanish on systems configured with Argentine, Uruguayan, or Hispanic Windows locales.

### Custom Map Textures & Zone Import
* Supports custom `map.png` textures and custom `zones.tsv` files in `%LocalAppData%\ScumMiniMap`.
* Validates imported map dimensions (minimum 1024x1024) with a preview confirmation dialog.
* Includes a **Reset to default map** option to revert to embedded high-resolution assets without manual file deletion.

---

## Controls & Keybinds

| Input | Mode | Action |
|---|---|---|
| <kbd>M</kbd> | In-Game | Toggle Full Map Mode (centered full-height overlay) |
| <kbd>Mouse Wheel</kbd> | Full Map | Zoom in and out smoothly (1.0x to 16.0x) |
| <kbd>Left Click + Drag</kbd> | Full Map | Pan across the map (when zoomed in) or drag opacity slider |
| <kbd>Right Click</kbd> | Full Map | Reset full-map zoom to 1.0x and center view |
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

SkynettMiniMap is designed from the ground up for strict compatibility with Easy Anti-Cheat (EAC):

* **Zero DLL Injection:** Operates entirely outside the SCUM process space as a standard layered Windows desktop utility.
* **No Game Memory Access:** Does not attach debuggers, inspect process RAM, hook DirectX/Vulkan APIs, or modify game files.
* **Clipboard-Based Coordinate Acquisition:** Samples coordinates strictly through Windows standard clipboard copy commands triggered while SCUM is focused.
* **Safe Input Suspension:** Automatically pauses coordinate polling while typing in chat (after pressing <kbd>T</kbd>), while modifier keys are held, or when SCUM loses window focus.

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

## Building from Source

The repository includes embedded road navigation networks, default zones, and high-resolution map assets:

```powershell
# Clone the repository
git clone https://github.com/Mike-Rafone-SCUM/MiniMap.git
cd MiniMap

# Run the automated build, regression tests, and packaging script
.\Build-GitHubRelease.ps1
```

Compiled binaries and release packages appear in `release/github/vX.Y.Z/`:
* `SkynettMiniMap.exe`: Standalone release executable.
* `SkynettMiniMap.zip`: Complete distribution archive.
* `update.txt`: Manifest containing ProductVersion and SHA-256 checksum for auto-updater verification.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the complete history of updates, feature additions, and bug fixes.
