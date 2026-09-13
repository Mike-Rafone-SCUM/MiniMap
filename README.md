# SCUM MiniMap v1.2.5

Run SkynettMiniMap.exe from the extracted release folder. Map artwork and default places are embedded. Use borderless/windowed SCUM; exclusive fullscreen can hide external overlays.

## Application updates

Download [the latest release from GitHub](https://github.com/Mike-Rafone-SCUM/MiniMap/releases/latest) for both initial installation and updates. Choose `SkynettMiniMap.exe` for the standalone app or `SkynettMiniMap.zip` for the app plus optional screenshot-import files. Version 1.2.4 and later check [GitHub Releases](https://github.com/Mike-Rafone-SCUM/MiniMap/releases) at startup and through **Check for updates (GitHub)** in Settings or the tray menu. No Git installation or GitHub account is needed.

When a newer stable version is available, choose to download it and save it as a new file. The app verifies its SHA-256 checksum before making the download available. Exit the app using its tray menu, replace the old executable with the downloaded file, and run it. Settings and imported zones in Local Application Data are preserved. Installation is manual; the updater never replaces a running executable.

Startup checks reuse verified release metadata for up to one hour. Manual checks refresh after one minute. Network errors, invalid metadata and rate limits never report that the installation is current. A server retry delay is saved across restarts. Versions 1.2.3 and earlier need the migration build once before they can use GitHub updates.

Maintainers: update the version constants, assembly versions and changelog, then push the source and a matching `vX.Y.Z` tag. GitHub Actions builds and tests the app, uploads `SkynettMiniMap.exe`, `SkynettMiniMap.zip` and `update.txt` to a draft, then publishes the complete release as latest and verifies a public download. Normal pushes and pull requests run the build checks without publishing. The manifest is generated from the executable's product version and SHA-256 hash. Never replace an already published executable; publish a new version. GitHub is the only download and update host.

For local builds, run `Build-GitHubRelease.ps1`; files appear in `release/github/vX.Y.Z`. `Publish-GitHubRelease.ps1 -Version X.Y.Z` can publish those assets using an authenticated GitHub CLI and an existing remote version tag.

## Controls

| Action | Control |
|---|---|
| Search and navigate | Delete |
| Open settings | Home |
| Show / hide radar | End |
| Zoom | Page Up / Page Down |
| Set / clear a pin at your position | Insert |
| Close a panel | Escape / Close |

The radar never activates or appears in Alt-Tab. Search takes keyboard focus and moves the pointer into its text box. Closing a panel returns to the SCUM window that opened it; deliberate Alt-Tab is respected. Right-click the map outside gameplay or use the tray icon for quick actions. Settings sections are all accessible by default with smooth continuous scrolling, and fold or unfold on click.

## Intelligent search

Examples: `mil air`, `airfield military`, `airfeild`, `airport`, `bunker C3`, `grid D4`, `nearest bunker`. Search ignores case, punctuation and accents. Longer words tolerate one or two edits, including transpositions. Airport/airfield, gas/petrol/fuel and harbor/harbour are synonyms.

Exact and prefix matches rank first. Distance breaks equal-rank ties when a position is available; nearest prioritises distance among matching places. Empty search sorts by proximity when possible, otherwise alphabetically. Results show grid and distance to the saved place centre. Arrow keys select; Enter sets the waypoint; Clear waypoint removes it. Grid-only searches include the sector centre and places in that sector. Search covers the loaded catalogue, not every game object.

Navigation waypoints clear automatically when a fresh position is within 30 metres of the destination centre. Insert places a pin at your current position; pins remain until you clear them with Insert. Waypoints and pins are local overlay markers, not SCUM-native markers. Coordinates may be stale; check the LIVE/age indicator before navigating.

## Tracking and data

Tracking is automatic whenever SCUM is focused. There is no manual tracking mode or enable switch. Settings controls the update interval. T pauses for chat; Enter/Escape resumes. Search/settings pause copying and wait for an in-flight chord to release. Tracking resumes automatically when returning to SCUM. Requests cancelled by focus or user input resume at the normal sampling interval. After three missed responses or a Windows input error it waits five seconds before retrying, without requiring user intervention. The status shows AUTO, CHAT, WAIT or RETRY. Chat/menu state is inferred from keyboard activity rather than read from the game.

Settings, imported zones and logs live in the ScumMiniMap folder under Windows Local Application Data. Settings writes atomically replace the previous file and retain settings.ini.bak. Existing files are migrated only when user data is absent. Custom map.png and zones.tsv in the data folder override embedded defaults. Settings includes an Open Data Folder button.

## Optional screenshot import

The release includes detect_zones.py and requirements.txt. Automatic screenshot import additionally needs Python with the packages in requirements.txt; manual editing does not. A python-path.txt file in the data folder can specify the Python executable. Detection uses the loaded map and times out after 60 seconds. Map-edge outlines slightly outside the image are clipped; grossly invalid coordinates are rejected.

## Build and checks

Clone this repository and run `Build-GitHubRelease.ps1` with Windows PowerShell on Windows. The map, road data and default zones are included, so the checkout builds independently. It compiles, runs regression checks and creates a standalone executable, complete ZIP and update manifest. This build does not stop or replace a running installation. The older `Build.ps1` packaging command remains available; it increments the version by default and `-Install` replaces and restarts the local executable.

Start-MiniMap.ps1 -Check tests search, grids, focus-return policy, coordinates and zone parsing. -Preview renders sample windows. Measure-Rendering.ps1 checks rendering performance and caching. Diagnostic windows neither copy game input nor save settings.

Before wider release, check Home/Delete, typing, selecting/cancelling, Alt-Tab, movement keys, manual pause, restart, display scaling and screenshot import in the intended SCUM setup. Local tests cannot establish every game focus or fullscreen combination. No game memory or server access is used.

Map artwork and bounds: https://github.com/nerdcave-support/nerdmaps-for-scum (retrieved 2026-09-12). Artwork belongs to its owners; redistribution rights have not been established. Bounds and saved place outlines are approximate.

## Update frequency

Position sampling defaults to 1000 ms (at most one request per second). Settings > Tracking and zoom > Position update interval (ms) accepts 1000–10000 ms. Faster saved intervals migrate to 1000 ms; slower custom intervals are retained. Clipboard changes are checked on the 40 ms UI timer. Only one request may be outstanding. Ctrl is held before and after C so the shortcut spans game frames; focus changes and fresh physical input cancel C before it is sent. Synthetic keys do not update the physical-key monitor, and failed key releases are retried before the request finishes. SCUM response time, held modifiers, chat and focus can reduce the actual update rate. automatic.log records status changes, not a position stream. Live gameplay still needs to confirm that the shortcut does not conflict with your bindings.

## Dynamic tracking and auto-zoom

When zoomed in, the minimap dynamically centres on the player in real time, smoothly moving the terrain, grid, and zones beneath your position so your player marker remains centered on the radar. At full map view (zoom 1), the entire island is displayed with your marker navigating across it. Auto-zoom remains available in Tracking and zoom, using filtered travel speed and a small change threshold. Terrain, grid and zones are cached efficiently to minimize CPU use.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a complete history of changes, features, and release notes.
