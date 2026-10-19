# MiniMap development workflow

`ScumMiniMap-Dev` is the canonical local project. The sibling server, companion bot and native bridge are independent projects.

## Captured road routing (local WPF test)

The WPF network uses the original road allowlist plus explicitly listed asphalt
barrier variants and bridge decks in `resources/routing-road-meshes.json`: 12,720
captured road segments. Rivers, railway and standalone wall/fence splines remain
excluded. Captured endpoint identities are preserved. Degree-one endpoints may
join nearby road curves within five metres, including T-junctions at spline
interiors, after height/grade and water checks. True mid-spline intersections
connect only at matching heights (within 30 cm) on dry mapped ground. Cycles are
retained so valid junctions cannot be lost merely because a longer route exists.
Every inferred seam and intersection is recorded.
Vertically separated crossings remain separate; runtime gap healing is disabled.

Three existing surveyed highway bridge curves are retained in
`resources/routing-bridges.json`, attached to stable source-segment identities
with bounded approaches. These static spans were not exported as landscape
splines. They are explicitly distinguished from captured bridge geometry; no
railway bridge or generic swimming/boat links are included.

Owner-provided map sections registered on 18 October add 59 reviewed road curves
in `resources/routing-map-links.json`. Image hashes, registration transforms and
source-road anchors are retained with each survey. These close visible gaps,
including the connection from the Rogoznica bridge approaches into the wider
Z3/Z4 road network and junction continuity on both approaches. Each survey is bounded to 500 metres, must attach within
three metres of its specified captured road and must pass the entire water mask.
The compiler never uses a map survey to add a new water crossing.

Regenerate from the SCUM workspace root:

```powershell
python ScumMiniMap-Dev/WPF-Test-Version/scripts/Build-RoadNetwork.py research/autonomous-driving/road-graph.json research/autonomous-driving/road-surface-allowlist.json
python ScumMiniMap-Dev/WPF-Test-Version/tests/test_road_network.py
```

The deterministic version-3 `resources/roads.bin` includes bridge surface flags
and a companion provenance JSON with input/output hashes, source identities and
all 1,976 inferred seams and 48 at-grade intersections. Its largest component
contains 98.33% of compiled nodes after the 59 map-surveyed gaps are included.
Ordinary builds consume this asset without needing the server capture or Python.
Road approaches are limited to 1,000 metres and checked against the water mask.
On-road starts stay on their nearest road; open-ground positions may consider
other nearby roads and a dry approach to the main network. Bridge snapping over
a water pixel is limited to eight metres from a flagged deck. Failed routes
remain unverified, and long two-feeder paths with no road travel are rejected.
Regressions cover the original approximately 2.1 km server-test route, ordinary
mainland/island journeys, 63 distributed map cells, all 15 combinations of three
northern-B2 approaches and captured Z0-Z4 island road destinations, shoreline safety, overpasses,
T-junctions, actual bridge-deck starts and rejected lake/swimmer shortcuts.
Coordinates use the existing map calibration; the snapshot does not prove current
collision clearance or distinguish stacked roads when the caller supplies only XY.

## Layout

The complete five-page WPF configuration opens whenever `OnboardingVersion`
does not equal `VersionString`, including existing users updating and older
profiles without the marker. Legacy `HideOnboardingGuide` values are ignored;
the UI has no setup opt-out. Six checklist acknowledgements enable Next, then
Appearance, Map options, Tracking/zoom and optional Sharing follow. Only final
Done records the completed version. Ordinary saves and early closure do not
complete setup. Existing settings and bindings are preserved unless changed.

The checklist uses large text and separate number-pad/no-number-pad paths.
Default Num / users need no MiniMap rebinding. Other users acknowledge changing
SCUM first, then use the capture button in the guide to set MiniMap's matching
key. The final lines explain what to test after closing the guide, since tracking
pauses while setup has focus. The wizard uses ordinary Next buttons instead of
timed holds, short page introductions and collapsed More options for detail.
It has no arrow overlay or walkthrough coach. Manual access remains in Settings.

Automatic and manual setup windows are modeless, non-topmost, unowned Windows
windows with a native draggable title bar, taskbar entry and minimize controls.
They keep the same checklist instance while switching to SCUM and back. Input
protection is enabled only while setup has focus (or a genuine child dialog is
open); losing setup focus releases tracking/input protection. Settings closes
before opening the guide so a modal parent cannot trap the user behind it.

Admin controls open independently of the retired map window. They dock just
inside the WPF map's left edge until the user drags the Admin Controls header.
The dragged position is retained for reopen/expand operations during that app
session and clamped to the monitor working area.

- `src/`: application source. `MiniMap.cs` contains the authoritative `VersionString`.
- `resources/`: required map, road, water-mask, icon and voice assets.
- User map data lives in `maps/<map-id>/` beneath the active data folder: `map.png`, `zones.tsv` and `customwaypoints.tsv`. Successful imports are retained in separate `imports/<import-id>/` folders with the original `source` image/file, `zones.tsv`, and (for screenshot detection) `source-zones.tsv` and `detection.json`. Import IDs prevent repeated filenames or layer names from overwriting earlier imports. The main `zones.tsv` contains the edited annotations saved from the zone editor; import TSVs preserve the initial detection.
- `resources/map-tiles.bin`: generated 512-pixel tile pyramid used for the bundled map. Regenerate with `python scripts/Build-MapTiles.py resources/map.png resources/map-tiles.bin` after changing the map image (requires Pillow). Commit the resulting tile pack with the map; builds require the checked-in pack and do not need Pillow.
- `packaging/`: files shipped alongside the executable.
- `scripts/`: shared build, verification and explicit publication commands.
- `tests/`: regression tests and focused diagnostic tools.
- `release/github/vX.Y.Z/`: completed release packages and reviewed release notes. Published packages are immutable.
- `release/tests/`, `release/checks/`, `release/.staging/`: generated development output; removable with `Clean.ps1`.

## Test and build

Run commands from this directory in Windows PowerShell 5.1 or PowerShell 7. Scripts resolve paths relative to the project, so invoking them from another directory also works. Windows, .NET Framework 4.x and its x64 C# compiler are required. Checks run in fresh Windows PowerShell STA processes.

```powershell
.\Test.ps1
.\Build.ps1 -Configuration Test
.\Build.ps1 -Configuration Release
```

The build flow is: validate inputs and output path, run regressions, run rendering checks, stamp a temporary source copy, compile all resources, run packaged self-tests, create the package, then move the finished package to its output directory. Failure cleans the staging directory. Builds do not install, start the interactive app, publish, or change the source version.

Release output defaults to `release/github/vX.Y.Z`. An existing directory is rejected before tests run. To verify an already-published version without changing it:

```powershell
.\Build.ps1 -Configuration Release -OutputDirectory release/checks/rebuild
```

Test builds use `SkynettMiniMap-Test.exe`, a `X.Y.Z-test` product version, disabled updates and a separate `%LocalAppData%\ScumMiniMap-ResponsivenessTest` profile. Release and test builds share the same compiler/resource list, including voices and the water mask. Both embed `packaging/MiniMap.manifest` with `requireAdministrator` and `uiAccess=false`. Packaging verifies the native manifest and invokes the packaged `-Check` entry point inside an isolated STA reflection runner, avoiding UAC prompts or interactive app startup during builds. `scripts/Build-GitHubRelease.ps1` and `scripts/Build-TestPackage.ps1` remain compatibility wrappers.

v1.7.15 separates map tiles and roads into the assets folder with identities pinned by a small embedded manifest. Both configurations include these files in their ZIP. Package self-checks validate assets as well as the EXE. The first upgrade from an EXE-only release requires extracting the complete ZIP and running `Update-MiniMap.cmd`; older updaters reject schema 2 rather than install an incomplete executable. Later automatic updates verify and extract the complete ZIP and roll back managed assets and EXE on replacement/launch failure. See `docs/PACKAGE-UPDATES.md` for formats and recovery limits. Updater helpers retain exact process/path/start-time validation. Regression tests use fake process operations and owned temporary files, never a user's running app.

Localised UI text belongs in the `src/Localization*.cs` partial classes, including reminder and voice text. Add each key to all nineteen language catalogues and preserve its format arguments. `tests/Test-Localization.ps1` checks catalogue parity, formatting and literal source references; it runs through `Test.ps1` and both build configurations. Shortcut hints must use configured bindings rather than fixed Home/Insert/Delete labels. Bridge and water-avoidance checks also run in the shared check flow. Item catalogues use embedded TSV resources listed by `scripts/Get-LocalizationResources.ps1`; see `docs/LOCALIZATION-REVIEW.md` for draft provenance, coverage and outstanding native-speaker review.

Spawn kits and the working cart use `spawn-kits.db` in the active user profile. `SpawnKitDatabase` calls the system SQLite library on supported Windows 10/11 installations using prepared statements and transactional replacements. Kit IDs, quantities, modifier strings and row order are validated before changes. Legacy TSV cart migration runs once and preserves its source; TSV kits can be imported explicitly. Regression fixtures use isolated temporary profiles and never modify the player's AppData database.

## Shortcut support capture

For keyboard failures that `automatic.log` cannot explain, build with the normal `Build.ps1 -Configuration Test` command. Add `scripts/Collect-ShortcutDiagnostics.ps1`, `packaging/Collect-ShortcutDiagnostics.cmd` and `packaging/SHORTCUT-DIAGNOSTICS.md` beside the generated `SkynettMiniMap-Test.exe`, then package those files together. The player exits their installed MiniMap and runs the CMD launcher as administrator with SCUM already running. The launcher checks its privileges before creating a diagnostic profile so it can start the elevated test executable with its isolated environment.

The launcher copies their active `settings.ini` into a new GUID-named profile under LocalAppData and enables input tracing only in the child process. The override requires a test build, `SCUM_MINIMAP_INPUT_TRACE=1` and a valid N-format `SCUM_MINIMAP_DIAGNOSTIC_SESSION` GUID. Normal releases and the existing responsiveness-test profile keep their usual locations. Startup guides, reset prompts and executable-folder migration are skipped in this capture session; updates remain disabled as in every test build.

Tracing ends after two minutes or 4,096 lines. It records configured control events, hook installation, input gates, dispatch, process integrity levels and clipboard parse success/failure, without clipboard contents or screenshots. On normal exit the launcher creates a ZIP containing only the new session's `automatic.log`, `input-trace.log` and settings files, then removes the temporary profile. Failed collection preserves that profile. This package diagnoses live input delivery; passing the ordinary `-Check` self-test does not establish that shortcuts reach SCUM on another player's PC.

## Prepare and publish a release

1. Write the real change list in `CHANGELOG.md` under the next version.
2. Run `scripts/Set-ReleaseVersion.ps1 -Version X.Y.Z` to update the authoritative version and README heading. Assembly metadata is stamped during compilation.
3. Run `Build.ps1 -Configuration Release` and inspect the resulting EXE, ZIP, manifest and release notes.
4. When publication is authorized, run `scripts/Publish-GitHubRelease.ps1 -Version X.Y.Z -SourceCommit AUDITED_COMMIT_SHA`. It uploads the EXE, application ZIP, audited current source ZIP, update manifest and release notes, verifies the draft, publishes, then verifies the live updater download. Push the sanitized core source separately before publication; exclude captured player reports, credentials and operator tooling.
5. Prepare and review the English announcement (`discord-en.md`), shared downloads (`discord-download-en.md`) and translated `discord-<code>.md` announcement for every language in the companion's `src/data/minimap_community_languages.json` registry. Translate these drafts before publication; announcements do not use Google or on-click translation. Preserve versioned release links, download URLs and the community footer. `Publish-DiscordRelease.ps1` validates every draft before writes, posts the translations to matching channels and verifies the English/download/localized results. Keep operator snapshots local.

Building does not authorize GitHub or Discord publication. CI validates/builds packages and does not publish automatically. Do not commit credentials, deployment tooling or operator records.

## Cleanup

```powershell
.\Clean.ps1 -WhatIf
.\Clean.ps1
```

Run cleanup after builds have finished. Only known generated test, check, staging and benchmark directories are removed. The current published release, source, resources and local application profile are retained. Do not store hand-written code in generated directories.
