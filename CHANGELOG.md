# Changelog

All notable changes to **SkynettMiniMap** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

## [1.7.1] - 2026-10-09

- Keep the admin player directory responsive by loading server data and clipboard imports off the UI thread, caching asset counts between refreshes, debouncing search, and building player cards in short batches only in tile view.
- Restore the originating admin controls after command dispatch, including when dispatch closed the full map, so another command can be sent without reopening the map.
- Check for updates hourly while the app is running. Periodic checks use the existing cache and rate-limit handling, defer during command/copy/chat activity, and notify once per new version without interrupting gameplay with an install dialog. Click the tray notification or check manually to install.

## [1.7.0] - 2026-10-09

- Enable every map category by default except Hunting and Fishing, including saved zone display; preserve saved filter preferences.
- Replace the first-launch guide's opening page with a localized quick start using the actual settings, full-map and search keys, waypoint clicks and individual death-marker removal.

- Use verified, finalized clipboard publication for individual ASCII admin commands as well as spawn batches, avoiding false clipboard-change failures caused by Windows text-format synthesis.

- Right-click a death marker on the full map to remove that marker only, preserving other markers and unrelated navigation.

- Reduce language-switch UI work by disposing settings controls as complete trees and caching built-in map-name translations separately for each language.

- Fix a native dropdown crash when switching app languages by rebuilding settings after the selection notification completes.

- Add European Portuguese as a separate app language with independent UI text, system-language detection and saved preferences. Preserve Brazilian Portuguese and existing language values.
- Add offline translation drafts for all 2,328 spawn items and 136 catalogue headings in the remaining app languages. Expand French, Simplified Chinese, Turkish, Arabic, Brazilian Portuguese and Korean marker coverage, including generated labels and all 130 map categories. Native-speaker terminology approval remains pending; see `docs/LOCALIZATION-REVIEW.md`.
- Improve Arabic item-selector text direction and mixed ID presentation while preserving raw commands and selections during language changes. Measure modifier-label widths and extend catalogue, search and language-switching regressions across all thirteen languages.

## [1.6.24] - 2026-10-09

- Translate all 130 bundled map categories in German, Dutch, Russian, Argentine Spanish and Polish, including animals, fish, crops, traders and loot containers. Add German, Dutch, Russian and Polish coverage for the 120 existing named map locations and translate generated object labels while retaining settlement names and sector codes. Search accepts translated and English map object names; selected map objects refresh their display names when the language changes. Custom waypoint text remains unchanged.

- Add Polish to first-time setup, Settings, system-language detection and saved preferences. Translate all 604 UI strings, 16 map sections, map categories and named locations. Include Polish display names for all 2,328 bundled spawn items, translated item categories, subtypes, results and cart names. Search accepts Polish with or without accents, English names and canonical IDs. Open item selectors refresh on language changes while preserving selections, cart quantities, modifiers and command IDs. Weapon model codes, brands and player-authored names remain intact.

- Add Korean to first-time setup and Settings, system-language detection and saved preferences. Translate all 604 UI strings, 16 map sections, 48 categories and 120 named map locations with matching format arguments.

- Keep player and squad refreshes responsive by validating reports and saving/loading local snapshots in background tasks. Keep clipboard access and game input on the UI thread, and recheck clipboard ownership after validation.
- Build replacement player cards in short batches while the previous directory stays visible, then swap the completed view. Batch squad-card layout and asset-table redraws, restore layout in failure paths, and double-buffer player/profile windows to reduce incomplete painting.
- Verify UI message processing during refresh alongside the existing report, pagination, profile and map-marker regressions.

## [1.6.23] - 2026-10-08

- Replace per-player squad refresh loops with `#DumpAllSquadsInfoList true` and paginated `#ListFlags <page> true`. Full directory/profile refresh captures players, vehicles, all squads and every reported flag page; two flag pages need five commands regardless of population size.
- Parse bulk squad membership, Unicode names and ranks, paged flags and unowned flags. Derive online status from a fresh population report during full refresh; standalone bulk imports show unreported status instead of inventing it.
- Replace squad membership per server and commit a complete flag snapshot only after every page validates. Wrong, missing, duplicate or inconsistent pages preserve the previous complete flags. Manual flag-page imports merge safely, and older individual SquadInfo reports remain supported.
- Refresh profile identity, statistics and title after population capture; both clipboard import buttons accept the supported report formats. Add copy commands for bulk squads and flag page 1.
- Passed 161 admin-player checks, including supplied reports, a five-command two-page refresh, interrupted capture and complete flag transactions. Added five strings in all ten languages.

## [1.6.22] - 2026-10-08

- Stop timer-driven sidebar visibility and TopMost updates during the pending command focus handoff. The dispatcher explicitly hides the expanded admin palette after requesting SCUM focus, preserving expanded/collapsed state.
- Explicitly close the full-map context menu and release mouse capture before queued admin commands and map teleports.
- Map-originated commands now display the real failure popup even without a tool-window owner, and retain the actual failure reason in map status. Previously this path silently skipped the popup.

## [1.6.21] - 2026-10-08

- Request SCUM focus before hiding the initiating admin windows. Allow a bounded transient empty foreground during handoff and one pre-dispatch focus retry while MiniMap or that transient state remains foreground. Another application, held keys, chat/inventory and target validation still stop dispatch. Submitted commands are never retried.
- Display the actual localized command failure and diagnostic stop reason instead of replacing every failure with a generic held-key instruction.

## [1.6.20] - 2026-10-08

- Restore admin player/profile/cart windows through one foreground-aware path after command dispatch, including normal window state, topmost ordering, capture release and activation. Preserve the active child above its directory and do not steal focus after switching to another application.
- Hide the visible admin tool stack during dispatch instead of leaving a parent above SCUM. Do not restore the map while a command is running. Apply the shared restoration path to refresh, teleport and cart completion, including interrupted commands.

## [1.6.19] - 2026-10-08

- Sort player tiles and table rows by A–Z / Z–A, squad size, flag count or vehicle count, with ascending and descending count order. Search remains available; no numeric range filters are added. Counts use saved reports for the selected server, including shared squad flags.
- Add teleport, Steam profile and copy-information context menus across player cards, squad members, profile identity, vehicle/flag rows and admin map markers. Copy IDs, names, coordinates, details and commands; unavailable data stays disabled.
- Add right-click copy IDs, names and spawn commands to item results and cart entries, with add/edit/remove actions and preserved multi-selection. Clipboard writes are blocked while sending commands.
- Verified 132 player checks, 892 cart checks and all 599 strings in ten languages; inspected the compact player directory.

## [1.6.18] - 2026-10-08

### Changed

- Compact player directory and profile menus with smaller title bars, dark page navigation, styled scrolling and tighter spacing. Squad pages show one squad header and member cards instead of repeating every member in a text box. Profile actions follow the selected page; duplicate footer controls are hidden in tile view.
- Player cards show a clear name, identity, copied fame/bank/gold values and visible Teleport to player / Bring player here actions.
- Add the supplied Steam logo beside player IDs on directory cards, profile headers and squad cards. Clicking opens the validated Steam Community profile in the default browser.
- Retrieve public Steam usernames asynchronously with two concurrent requests, a timeout and per-session caching. Verify returned Steam IDs, reject unsafe XML, and retain SCUM's reported platform name when a lookup is unavailable. Character names and server ownership are unchanged.

### Validation

- Passed 124 player checks, including quick teleport targets, Steam link validation and public-name parsing. Inspected directory and squad previews, including the minimum profile window size. Diagnostic forms do not launch browsers or query Steam.

## [1.6.17] - 2026-10-08

### Fixed

- Automatically capture and import a fresh SCUM report after command submission even if Escape cleanup was interrupted or focus returned to MiniMap. Physical input still cancels further key sending; read-only capture retains clipboard freshness, SCUM ownership and report validation. Interrupted cleanup stops subsequent commands rather than resending.
- Keep player profiles above the player directory through explicit window ownership. Opening a profile no longer immediately rebuilds the directory underneath it.
- Allow up to fifteen seconds for a squad clipboard report to arrive, without resending the command. Population and vehicle refresh retain their existing timeouts; focus, physical-input and clipboard ownership checks remain active.
- Record command dispatch stages locally without command text or clipboard contents, so remaining game-specific failures can be diagnosed.

### Validation

- Passed 118 admin player checks, including owned profile windows, delayed clipboard replies, interrupted Escape and automatic squad import. Live SCUM verification remains pending.

## [1.6.16] - 2026-10-08

### Fixed

- Keep player profiles alive while SCUM command sending temporarily hides them. The previous modal dialog could finish and dispose the profile before its asynchronous squad reply was imported. Reopening a player reuses the profile and focuses the requested page.
- Include profile windows in map/popup protection and admin-disable handling. Hide profiles when changing servers and preserve the active server if a previous refresh finishes later.
- Show flags owned by known squad members in player profiles and tile counts, retaining the reported owner rather than assigning ownership to every member.

### Validation

- Reproduce the real squad-refresh button hiding its profile and receiving a delayed reply without sending live game input. Verify window survival, database import, rendered squad flags, profile reuse and the supplied Saints report's Steam IDs and spacing.

## [1.6.15] - 2026-10-08

### Added

- Refresh the player directory's population, vehicles and squad/flag information together. Squad reports are shared across their members to avoid duplicate queries. Individual profiles include a full refresh and separate vehicle/squad refresh controls.
- Add teleport context menus to player tiles, directory rows, profile identities, squad members, vehicles and flags. Right-click shown map markers for the corresponding teleport actions.
- Add an Admin tools pill toggle in the full-map navigation bar and Settings. Admin tools are disabled by default and the preference is saved.

### Fixed

- Capture and validate fresh game-owned SquadInfo clipboard replies for the requested player before saving profile information. Keep existing information when reports are unavailable and show incomplete refresh status.
- Resolve offline vehicle database-owner IDs only through matching, unambiguous flag-owner observations from the same server; aliases remain separate.
- Align profile page backgrounds, identity cards, summaries and context menus with the map theme. Asset teleports preserve reported XYZ coordinates.

### Validation

- Validate report capture, requested-player matching, offline ownership links, teleport coordinates, map hit testing and disabled-by-default admin controls with regressions. No live SCUM commands were submitted during checks.

## [1.6.14] - 2026-10-08

### Added

- Add Mila (German) and Isabel (Spanish Argentine) as selectable voice navigators, using navigation prompts extracted from the supplied recordings.
- Compose German distance cues as distance followed by action, and Spanish cues with the isolated “En” prefix. North-specific and location-placeholder examples are excluded from reusable guidance.

### Validation

- Check discovery and cue composition for all four bundled voices, rejection of missing turn clips, and decoding of the new audio clips.

## [1.6.13] - 2026-10-08

### Added

- Bundle Mira (Russian) as a selectable navigator, with distance, straight/left/right/keep/U-turn and arrival prompts extracted from the supplied ElevenLabs recording. Russian distance cues speak the distance followed by the action; the English voice retains its existing phrasing.
- Include the tile-based player profile system from the verified, unpublished v1.6.12 build: overview links, squad membership and linked member profiles, vehicle/flag imports, scoped SQLite storage, fresh game-owned vehicle refreshes and selected asset map markers.
- Translate the profile controls, statuses and explanations into all ten app languages.

### Fixed

- Include the bounded map-close settling fix for quick commands before chat opens, without automatically repeating command submissions.

### Validation

- Verify both bundled voice packs are discoverable and complete, Russian/English cue composition, and decoded MP3 durations. Run the complete build checks including profile parser/database/UI regressions without live game input.

## [1.6.12] - 2026-10-08

### Added

- Replace the default player table with a searchable tile directory and quick Overview, Vehicles, Flags and Squad links. Keep a table-view option for administration.
- Add full profile pages with copied player statistics, squad membership/ranks/status, links to other squad members' profiles, owned vehicle and flag locations, and multi-selection of asset snapshot markers.
- Import the current SCUM vehicle and SquadInfo/flag report formats into a separate per-server AppData SQLite profile database. Preserve vehicle aliases separately from ownership and leave offline database-only owners unresolved. Keep imported squad members available as profiles even before a population-list import.
- Refresh vehicle lists through one ListSpawnedVehicles command with a fresh SCUM-owned clipboard response; also support manual command copying and validated clipboard imports. Replace scoped snapshots atomically and retain unrelated server data.

### Fixed

- Give quick commands the same bounded, owned map-close settling check as cart commands before opening chat. Clear the map state only after the map key is accepted; never retry a submitted command automatically.
- Protect profile dialogs through the shared modal-input path and keep dynamic tile/squad buttons consistent with the map's dark theme. Clear asset markers on server changes.

### Validation

- Extend player checks with vehicle/flag/squad parsing, alias-versus-owner handling, SQLite persistence/isolation/atomicity, fresh clipboard capture, tile links, profile navigation, map selections and server changes. Use synthetic fixtures without live SCUM input.

## [1.6.11] - 2026-10-08

### Fixed

- Change the admin sidebar's topmost state only when visibility changes. Repeated timer updates must not raise the panel over its popup menus.
- Suspend the map window while player or cart tools are visible, block automatic map restoration over those tools, and restore the previous map only after the last tool hides or closes. Leave sidebar window positioning untouched while tools are open.
- Verify native window order across sidebar timer synchronization and map suspension/restoration through player-window Close, alongside server-selection checks.

## [1.6.10] - 2026-10-08

### Fixed

- Show the attached admin sidebar only while the full map is open. Hide it on returning to the corner minimap, including collapsed tabs and explicit settings requests; preserve the same controls and choices for reopening the full map.
- Allow full-map sidebar buttons to respond during coordinate-copy cleanup instead of discarding the first click. Cancel pending copying before opening settings, and allow Done and framed Close buttons to bypass unfinished field validation.
- Activate the player directory for normal user opens while keeping diagnostic windows inactive. Commit saved-server choices immediately, accept Enter after typing a server name, and retain explicit per-server separation before importing lists.

## [1.6.9] - 2026-10-08

### Fixed

- Apply shared modal input protection to settings, shortcut capture, colours, search, waypoint prompts, zone editors, admin command editors, update dialogs and error/confirmation messages. Release mouse confinement, pause tracking and shortcuts, hide map overlays and preserve nested dialog state until closing.
- Protect the cart's modifier, kit, replacement, clear-cart and legacy-import dialogs through the same interaction path. Restore the previously active tool above the map after a child dialog closes.
- Release mouse capture when opening the cart, player directory and map context menus. Keep attached admin controls hidden while either tool is visible and keep the player directory above the topmost map. Avoid using minimized settings as the owner of map-menu prompts.


## [1.6.8] - 2026-10-08

### Fixed

- Release mouse capture and cursor confinement while the saved-settings Yes/No prompt, first-time setup guide and copy-key reminder are open. Hide the map and attached admin controls, pause tracking and suppress shortcuts until the prompt closes, restoring those states even if opening fails.
- Open the saved-settings prompt without the minimized settings window as its owner, keeping the startup choice accessible.

## [1.6.7] - 2026-10-08

### Fixed

- Keep Add command, Item spawner / cart and Admin players buttons within matching ten-pixel side margins. Initialize their container at the correct width before anchoring controls, eliminating the negative margin that let buttons extend past the panel edge.
- Fit command cards to their actual available width, align edit/remove actions to the right, and preserve button and preview bounds after command refreshes, panel resizing and horizontal scrolling on narrow displays.
- Reflow command actions and teleport directions on narrow panels, wrap help and coordinate previews, and use horizontal scrolling only when the viewport is too small for the supported compact layout.

## [1.6.6] - 2026-10-08

### Added

- Add an admin player directory using SCUM's `#ListPlayers true` clipboard result, with character/platform names, stable platform IDs, coordinates, capture times and read-only fame/bank/gold information. Import an already copied list or request a fresh list through the configured chat controls; no server bridge is required.
- Save players and their last reported positions in `%LocalAppData%\ScumMiniMap\admin-players.db`. Select a named server before importing, isolate each server's population, update returning players by ID and retain missing players as history. Reject incomplete, malformed or duplicate results before changing saved data.
- Search the directory by either name or ID, teleport to a player or bring them to you, and optionally draw players from the latest snapshot on both map modes. Markers show coordinate age, turn gray after 60 seconds and disappear after five minutes. History entries remain searchable; positions are refreshed snapshots, not a continuous feed.
- Translate the player directory into all ten supported app languages. Require a fresh clipboard result from the original SCUM process for automatic refresh, stop on input/focus changes, and restore the previous clipboard only while the captured command/result still owns it.

### Changed

- Attach admin controls to both the compact minimap and expanded map. Collapse to a 28-pixel Admin tab; use its arrow or label to expand. Close, Escape and native window closing collapse the same controls instead of dismissing them. Preserve command sections and teleport choices between map modes.
- Follow map moves, resizes and monitor edges immediately, use available space on either side and preserve the map's size and position. Hide the attached controls with the map, input suppression or command transport, then restore the same instance without activation.
- Ignore the previous Settings-only persistent dismissal preference so older profiles retain an accessible attached tab. Settings and menu shortcuts expand that same panel.

## [1.6.5] - 2026-10-08

### Added

- Remove the current map's death markers directly from the map sidebar without clearing ordinary waypoints or zones.
- Browse a bundled SCUM Database item catalogue in a visible category tree with six groups and source categories/subcategories. Search by display name, spawn ID or subtype, including individual item variants. Build a quantity-based cart, review its commands and save or load reusable spawn kits.
- Include source-listed magazines, ammunition, attachments and adapters when searching for a weapon. Normalize ID punctuation and spacing, support combined searches such as `AK47 scope`, deduplicate shared parts and identify matching guns and possible adapter requirements. Bundle 3,882 explicit compatibility links from SCUM Database for offline searching.
- Send the spawn cart through the existing configured SCUM chat controls in one action. Pace commands, stop on input/focus/clipboard interference and retain only unsent entries after an interruption. Translate the selector and result messages in all ten languages.
- Select per-item Health, Uses, Dirtiness, Location, KeyCardSector, StackCount, Radiation and Weight overrides using checkboxes and value dropdowns. Offer numeric presets and all sixteen keycard sectors, retain custom numeric values, include validated modifiers in command previews and saved kits, and import legacy kits. Location accepts coordinates, copied SCUM location text or a Steam ID and preserves the required quoting.
- Add an opt-in, two-minute shortcut support capture with configured key events, hook installation errors, input gates, hook/polling dispatch, process integrity information and coordinate-response outcomes. It records no clipboard contents or screenshots.
- Run the support package in a unique test profile seeded with the player's current settings, preserving the installed app and any existing test profile. Collect the session logs into a ZIP for diagnosis.
- Offer installation from a newly downloaded standalone EXE when an older MiniMap is running, with an alternative `Update-MiniMap.cmd` launcher in the ZIP. Verify the release checksum, close only the identified MiniMap, replace its executable and restart it while preserving AppData.

### Changed

- Match the admin panel to the map sidebar and retain an explicitly opened panel in the background across map and focus changes. Restore and focus the existing panel when requested. Keep an explicitly closed panel dismissed until reopened from Settings.
- Store named spawn kits and the working cart together in an AppData SQLite database. Replace kit file pickers with a saved-kit list, retain ordered quantities and modifiers, migrate the previous cart once and support importing legacy TSV kits without removing their source files.
- Organize item search results into collapsible sections using source categories and subtypes, including ammunition calibres, scopes, sights, rails and suppressors. Show section counts, keep collapse state while filtering, offer expand/collapse-all controls and clear hidden selections before adding. Preserve multi-selection across open sections and full compatibility details.
- Refine the spawn cart layout with a wider browser, readable name/compatibility columns, selection counts, search reset and empty-result feedback, Ctrl+F search access, compact cart actions and command preview, and consistent dark styling. Translate the new controls in all ten languages.
- Request administrator rights when launching either the test or release executable. Verify the embedded elevation manifest during packaging and run the packaged self-check through an isolated STA reflection runner without launching the interactive app or requesting elevation during builds.
- Reduce the pause between spawn-cart commands from 250 ms to 60 ms while preserving chat-open, paste and key-release timing and interruption checks.
- Edit modifiers for multiple selected cart items at once. Show mixed values, apply only changed properties, preserve individual untouched modifiers and unselected rows, and validate the complete selection before committing it.
- Apply accepted updates automatically after verification, request elevation when needed, and allow a bounded force-close fallback for the exact verified MiniMap process if it does not exit normally. Cancelled elevation or failed verification leaves the previous app intact; failed replacement or restart restores the previous executable.

### Fixed

- Persist manual death-marker removal immediately and invalidate the map display. Ignore death-screen samples captured before a manual clear so an outstanding probe cannot recreate the removed markers.
- Allow a bounded wait for the original SCUM window to receive focus and for held keys to release before admin input. Display the configured chat key, show the reason when no cart command was sent, and preserve native input rejection details. Log admin gates and submission counts without recording commands or clipboard contents.
- Queue cart submission after the button's click event finishes. Validate active cart editors explicitly so automatic focus validation cannot suppress the Send handler, and log the click and any preparation step that blocks it.
- Stop timer-based inventory detection from cancelling an admin send during its own map/chat transitions. Check inventory once after map closure and before opening chat, retain physical-input cancellation, and preserve the specific reason when a send stops before pasting.
- Publish and verify cart command text eagerly under a clipboard lock, then verify ownership and all text formats after Windows' close-time synthesis to retain the final stable sequence. Allow a short wait for clipboard acquisition. Keep the expanded-map state until its closing key is accepted, and wait for the closing menu to disappear before opening chat. This prevents an early clipboard failure from leaving the game map open while MiniMap believes it has closed. Never repeat a map toggle or submitted command automatically.

### Upgrading to this release

Windows now requests administrator access when MiniMap starts so its input can reach an elevated SCUM process. For the first upgrade from an older unelevated version, download and run the new `SkynettMiniMap.exe` while the old MiniMap is running. Approve Windows' administrator prompt, then accept the offer to update the existing installation. The app verifies the release, closes the old MiniMap, replaces it and restarts it automatically. ZIP users can extract the package and run `Update-MiniMap.cmd`. Future accepted updates use the app's updater. SCUM stays running. Keep the existing AppData folder; settings, zones and waypoints are retained, and legacy spawn carts migrate to the kit database.

## [1.6.4] - 2026-10-05

### Added

- Show a floating Admin tab beside the expanded map's left edge. It starts horizontally collapsed on each map opening and expands to the left within the available space, with a gap between the panel and map. Commands and Directional teleport have separate expandable sections; Commands retains Add, Edit and Remove.
- Teleport up, down, left, right, forward or back by a distance in metres from the latest received coordinates. Horizontal directions follow the recorded facing direction; up/down change elevation. Review the clean `#Teleport X Y Z` preview before sending, and wait for a new coordinate sample after each submission.

### Changed

- Match the admin panel to the full-map sidebar with blue-black surfaces, compact text, rounded buttons, orange selections, flat command rows and a thin blue scrollbar. The panel hides with map closure, focus loss and Ctrl map interaction; Settings and the tray retain explicit access.

### Fixed

- Preserve the expanded map's original full-screen size and position when opening, expanding, collapsing or closing the admin panel. Fit the panel into the available left margin and hide the attached tab when there is insufficient space.

## [1.6.3] - 2026-10-04

### Changed

- Expand Settings, the SCUM/MiniMap rebinding wizard and first-launch setup to configure all twelve app actions: Settings, Add waypoint, Search, Show/hide map, Zoom in, Zoom out, Add death marker, Reset zoom, Clear waypoint, Zone Creator Wizard, Getting Started Guide and Admin commands.
- Use one control catalogue for physical-key capture, collision checks, persistence, default reset and shortcut dispatch. Game bindings must match SCUM; app actions use a single physical key and can be unbound. Preserve existing saved shortcuts and default the five additional tool actions to unbound.
- Keep wizard changes pending until Save or setup completion, validating and applying SCUM and app bindings together. Cancelling leaves the previous controls intact.
- Apply zoom shortcuts to the current compact or expanded map, and route the additional tool shortcuts through the same actions as their buttons and menus. Update shortcut hints to follow configured bindings, including overlay visibility and death markers.
- Translate the complete controls UI and guidance in all ten supported languages, and document every app action, default, unbinding and reset behavior.

### Fixed

- Stop watching old fixed shortcuts after rebinding, prevent hook/polling duplicate presses, and discard queued shortcuts when bindings change. Keep capture feedback visible while scrolling the complete wizard.
- Keep expanded-map zoom responsive while a cancelled coordinate-copy operation finishes releasing its keys. Clearing navigation immediately stops its voice guidance.

## [1.6.2] - 2026-10-03

### Fixed

- Dispatch admin dropdown commands after the native menu closes so it releases focus and mouse capture before sending. Block direct location-copy preparation and shortcut dispatch while an admin command is active.
- Record an admin request when Enter is dispatched, including when input or key-release failure interrupts the following cleanup. This prevents the next toggle from repeating the value SCUM may already have received.
- Show UNKNOWN for admin commands on each app launch and provide explicit ON/OFF actions in the dropdown and panel. Panel toggles use the current last-submitted request across both interfaces; cancelled sends preserve that value. These values do not confirm SCUM's live state.
- Normalize saved boolean commands containing a trailing true/false value before assembling a new request, preventing duplicate values from keeping a command enabled. Preserve optional arguments and non-boolean command text.

## [1.6.1] - 2026-10-03

### Added

- Death markers display relative age, such as Death 12m ago, including markers saved by earlier versions. Returning within 25 metres removes the marker and its active navigation after tracking has first seen the player at least 50 metres away.

- Full-map right-click menu offers Admin commands > Teleport here. It converts the clicked map location into SCUM X/Y coordinates and submits #Teleport through the existing chat sender, using Z=0 because map terrain height is unavailable.

- Automatic death markers recognise the red heading and three stacked respawn buttons without reading text or requiring calibration. A complete layout match immediately saves the last known coordinates. Fourteen consecutive misses (about ten seconds) are required to rearm detection. The detector supports changed text and excludes unrelated right-side map content from the button checks.
- Down arrow saves the last received location as a persistent tombstone waypoint. Place it before respawning; navigate to or delete it through the waypoint list. Chat, inventory, focus and binding conflicts retain priority for the manual shortcut.

### Fixed

- Death detection retains the last known location when death or menus pause coordinate tracking for more than 30 seconds. Missing coordinates remain missing rather than adopting a later respawn location.
- Added bounded death-detection diagnostics for blocked probes, unavailable captures and saved/skipped/failed markers, including location age without coordinates or screenshots.

## [1.6.0] - 2026-10-02

### Fixed
- Reduced admin-command focus and chat delays, with a held-key check in place of the location-copy mouse cooldown. Commands submit with Enter, then send Escape to dismiss chat; interrupted cleanup retains chat protection and distinguishes an already submitted command.
- Chat/map keys immediately after game focus returns now validate the foreground window at the physical event. Chat-session changes invalidate queued map shortcuts, preventing an old M press from firing after chat opens and closes.
- Admin-injected chat keys no longer enter the polling fallback's physical chat state.

### Added
- Command editor fields for Label, Command, Is boolean?, optional Quantity and optional Extra strings, with an assembled-command preview. Boolean entries toggle true/false; other entries run once. Optional arguments and boolean settings persist per command, and earlier saved toggles remain compatible.
- Full-map Admin dropdown in the right-click menu and sidebar Tools section, with single On/Off toggles sending true/false and an Edit commands entry. Existing paired command buttons migrate to single toggles; displayed values are last submitted requests, not confirmed game state.
- Admin command panel accessible from Settings and the tray menu, with boolean presets for God mode, player information and nameplates. Sends the displayed command through SCUM's configured chat key, clipboard paste, Enter and Escape; SCUM permissions and command results remain authoritative.
- Admin panel appears above the game on the overlay's monitor and restores from minimization when reopened.
- Add, edit and remove named command buttons, saved per user in `admin-commands.tsv`. Commands are limited to one line starting with `#`.
- Admin sending pauses location copying and map shortcuts, closes the expanded map before opening chat, cancels further key presses after physical input or focus changes, and restores the clipboard only while it still owns it. Interrupted chat remains protected until explicitly closed.

## [1.5.5] - 2026-10-01

### Added
- For admin players, hold either Ctrl key while the expanded map is open to temporarily hide MiniMap and interact directly with SCUM's map, so admins can select players and use SCUM teleport actions. Release Ctrl to restore the overlay when no map click was completed.
- Complete a Ctrl-left-click on the exposed map to collapse MiniMap to its compact view and clear stale chat, inventory and pending-copy locks. Cleanup follows the click gesture; MiniMap does not receive a confirmed teleport event from SCUM. Newly opened chat remains protected.
- Retain each map image beside its own `zones.tsv` and `customwaypoints.tsv` in `maps/<map-id>/`. Keep successful imports in separate subfolders containing the original image/file, map-coordinate TSV, source-coordinate TSV and detection report where applicable.

### Fixed
- Preserve measured screenshot zone boundaries instead of forcing imperfect circular detections into oversized enclosing rectangles. Smooth confidently circular outlines using fitted boundary pixels, retain other measured outlines and reduce aggressive green-mask merging.
- Offer layer naming on every screenshot import, including the first import into an empty zone editor. Focus and select the proposed name for editing; disable append/replace when no zones exist.
- Keep repeated import filenames and layer names from overwriting earlier source images or TSVs; failed archives leave no partial import folder.
- Ignore injected Ctrl from automatic location copying when entering the admin map interaction mode.
- Cancel overlay dragging and context menus during direct game-map interaction, preserve intentional overlay hiding, and restore visibility correctly after focus changes.
- Isolate history regression fixtures from live coordinates already present on the clipboard.

### Compatibility
- Existing saved key bindings and annotations are retained. Previously imported zones need reimporting to use the corrected outline detection.
- Includes the v1.5.4 community keyboard illustration, setup cleanup and all earlier map-key, focus, dragging and tracking fixes.

## [1.5.4] - 2026-10-01

### Added
- Display the community-supplied keyboard illustration in the key binding setup guide and Copy Location reminder, showing the NumPad divide key highlighted in green and the main keyboard slash crossed out.
- Include the original keyboard illustration in the complete release archive and setup documentation.

### Changed
- Dispose setup slide controls and their image resources when navigating between pages.
- Retain all v1.5.3 map-key, focus, dragging and tracking fixes, alongside existing saved key bindings.

## [1.5.3] - 2026-09-29

### Fixed
- Let the configured map key reach SCUM so its own map opens underneath the overlay. Keep game keyboard focus while the expanded overlay accepts mouse input, and retain map state across Alt-Tab.
- Drag the compact map with explicit mouse capture while Settings is open. Hovering no longer opens a menu over the drag target; use right-click for the desktop context menu.
- Includes the v1.5.2 fix for false inventory detection blocking player tracking and the map key, plus desktop dragging/resizing and gameplay click-through.

### Setup and compatibility
- **NumPad / (number-pad divide), without a modifier, is the default Copy Location key for new installations. SCUM's Copy Location binding MUST match MiniMap.** Existing saved bindings are retained; configure both applications when changing keys.
- Fixed the interaction with MusicalScumbag by moving that application's auto-walk shortcut from NumPad / to F10. MusicalScumbag users need its corrected build as well; MiniMap alone cannot update the other application's hotkeys.
- Includes the earlier localisation, key-capture/reset consistency, and obsolete-code/settings cleanup from v1.5.1.

## [1.5.2] - 2026-09-29

### Fixed
- Restore compact minimap dragging and resizing when SCUM is not focused, while retaining click-through during gameplay.
- Require a visible cursor before detecting inventory navigation highlights, preventing bright gameplay scenery from blocking position tracking and the map shortcut.

## [1.5.1] - 2026-09-28

### Changed
- Removed the nonfunctional Show player elevation setting and its unused persistence/cache state. Localised the full-map history toggles using the same strings as Settings.
- Removed obsolete water-transit rendering and route fields, unused input wrappers, redundant key-wizard state and the unused copy-key screenshot resource.
- Consolidated physical-key names, modifier validation, conflict detection and copy-key invocation across setup, Settings and regression checks.
- Moved reminder, voice and copy-binding translations into the main localisation catalogue. Completed zone-layer, shortcut, history and update-reset translations in all ten languages.
- Updated help and sidebar hints for configured shortcuts and per-map annotation storage. Added translation completeness and placeholder checks to the shared build checks, alongside bridge and water-avoidance regressions.

### Fixed
- Clear stale physical scan codes when reloading older settings or resetting setup bindings; retain the previous binding when a capture attempt fails.
- Reject conflicting setup bindings before saving, while allowing navigation back to correct them.
- Use one mouse-passthrough policy during both overlay handle creation and subsequent updates.

## [1.5.0] - 2026-09-28

This release consolidates the complete 1.4.x development line, from 1.4.0 through 1.4.103. The detailed version-by-version history remains below.

### Added
- Configurable SCUM map, chat and coordinate-copy bindings, plus MiniMap Settings, Add waypoint and Search shortcuts. Captured physical scan codes make bindings work across keyboard layouts; older virtual-key-only settings remain supported.
- A layout-independent NumPad Divide default for coordinate copying, with setup guidance for keyboards without a number pad.
- Optional voice navigation with selectable voice packs, volume and preview; route-based distance and turn cues, wrong-way detection and recalculation guidance.
- Custom waypoint and zone tools, faction/custom zone layers, map import and per-custom-map `zones.tsv` and `customwaypoints.tsv` sidecars.
- Route-history controls to clear the trail and set its visible duration from 1 to 240 minutes.
- Destination-bearing indicators, player marker and heading-cone color controls, and expanded multilingual support for ten application languages.

### Changed
- Rebuilt map rendering around on-demand tiled assets, cached terrain, reusable drawing buffers and visible-area culling to reduce memory use and stutter.
- Expanded routing across bridges and islands, added water-aware routing and improved road graph healing, snapping and route progress tracking.
- Improved location sampling responsiveness, position smoothing, auto-zoom, and voice playback scheduling.
- Refreshed the Settings, onboarding and key-rebinding interfaces, with persistent taskbar access and update/reset guidance.
- Hardened updater version checks, downloaded-file integrity checks, installation rollback and release-package verification.

### Fixed
- Protect chat typing, Tab channel changes, inventory entry fields and other game UI input from MiniMap shortcuts and coordinate-copy injection; keep the NumPad Divide copy binding from being mistaken for slash-to-open-chat.
- Match keyboard shortcuts by physical key position across localized layouts, and recover from missed key-up events or dropped keyboard hooks.
- Keep full-map mouse input usable while returning focus to SCUM when the map closes; keep the compact minimap mouse-transparent.
- Correct map rendering seams and clipping, route progress near crossing/parallel roads, waypoint dialog focus, zone editing and settings persistence.
- Prevent false update prompts, startup reset loops, stale UI state, repeated route announcements and several rendering/audio/resource leaks.

## [1.4.103] - 2026-09-27

### Fixed
- Keep the compact minimap mouse-transparent so moving the cursor over it does not move or aim the player in SCUM; the expanded interactive map still accepts mouse input.
- Apply the inventory UI input lock to the M/full-map shortcut as well as coordinate sampling, preventing accidental map toggles while typing in inventory search fields.

## [1.4.102] - 2026-09-27

### Changed
- Store zone polygons and custom waypoints in separate per-map sidecars, keyed to each custom map image so switching custom textures keeps annotations isolated.

### Fixed
- Migrate existing `zones.tsv` annotations to the active map once while retaining the original file as a backup.

## [1.4.101] - 2026-09-27

### Fixed
- Detect the selected SCUM navigation tab by its visual highlight instead of its translated label, pausing coordinate sampling and MiniMap shortcut handling while game UI input is active.
- Preserve SCUM text-entry input while preventing MiniMap shortcuts from firing over the inventory interface.

## [1.4.100] - 2026-09-27

### Added
- Clear route history from Settings and choose how long the trail remains visible (1–240 minutes, default 30). History preferences are saved per user.
- Configurable Settings, Add waypoint and Search shortcuts with direct key capture for compact and number-pad keyboards. Home, Insert and Delete remain the defaults.

### Fixed
- Prevent typing after Alt-Tab from opening a false chat gate that disables MiniMap shortcuts.
- Preserve chat ownership through typing, Tab channel changes, and Escape/Enter closure. Suppress MiniMap-owned key-down and key-up events so SCUM does not also act on shortcut keys.
- Ignore injected copy keys in chat-state polling, and let UI shortcuts work when the mouse hook is unavailable.

## [1.4.99] - 2026-09-25

### Fixed
- Copy Location now resolves the active SCUM keyboard layout and supports capturing the physical key, including Brazilian ABNT2 layouts.
- Improved guidance when SCUM does not return coordinates after a copy-key attempt.

## [1.4.98] - 2026-09-25

### Changed
- Cached location history paths, scaled map tiles and POI filter state to reduce repeated drawing and allocation.
- Reduced full-map drag rendering work while keeping the settled map sharp.
- Reduced duplicate focus checks and slowed idle UI polling when neither SCUM nor MiniMap is focused.
- Reused route drawing buffers and limited terrain drawing to the visible map area.

### Fixed
- Tile joins remain seamless with the cached map rendering path.

## [1.4.97] - 2026-09-25

### Fixed
- Location history age labels appear at roughly 30-second intervals along the trail and update in 30-second steps, reducing clutter and redraw work.

## [1.4.96] - 2026-09-24

### Added
- Update startup prompt that offers to clear saved settings and run the first-time installer again.
- Red default player marker and focus cone.
- Compressed SCUM controls reference image in the coordinate-copy reminder.

### Fixed
- Settings version tracking prevents the update reset prompt from repeating after a user makes a choice.

## [1.4.95] - 2026-09-24

### Added
- A coordinate-copy key reminder in all ten app languages. It appears at startup until the user selects "Do not show again" and confirms.

### Changed
- The bundled map loads visible tiles on demand, reducing map startup memory use. The package no longer embeds a second, full-resolution copy of the map.
- Tile borders overlap during rendering to prevent visible seams on the minimap and full map.

### Fixed
- Voice guidance keeps route progress stable where roads cross or run close together, and avoids false off-route recalculation near parallel route sections.

## [1.4.93] - 2026-09-23

### Fixed
- Hotfix for gameplay stuttering caused by foreground-process lookup work in the synchronous keyboard hook.
- Existing v1.4.92 installations can now detect and install this corrected build through the normal updater.

## [1.4.92] - 2026-09-23

### Fixed
- Hotfix: automatic coordinate copying stays paused while typing in SCUM chat, preventing repeated backslash characters in the chat box.
- Keyboard-hook and fallback-polling chat detection now share key transitions, preventing an older Enter or Escape press from clearing a newly opened chat session.
- Verified with regression tests for default and custom chat bindings, repeated sampling attempts, and reopening chat; the reporting user confirmed the fix in game.

## [1.4.91] - 2026-09-23

### Added
- Water mask routing checks and a localized road-route-unavailable voice announcement.
- Brazilian Portuguese as the tenth supported application language.

### Changed
- Removed misleading straight-line water-crossing fallback routes.

### Fixed
- Backslash key labels in settings and the startup guide.

## [1.4.9] - 2026-09-21

### Added
- Destination-bearing arrow on the minimap edge, with a glowing arc for circular maps. Uses the selected route colour and follows the final endpoint independently of voice navigation.

### Fixed
- Home restores and foregrounds Settings instead of toggling it closed; it also works when Settings is already open behind the game. Escape, Done and the close button still minimize Settings.
- Allow coordinate sampling while the full map's mouse cursor is visible, retaining chat, focus and physical-input protections. Player position and heading continue updating over cached map terrain.
- Align the circular map mask and destination indicator with the map area when the status bar is above or below; retain circular clipping when edge fading is disabled at full opacity.

## [1.4.8] - 2026-09-21

### New copy-key default
- New installations and the setup guide's key reset use **backslash (`\`) without a modifier**. Bind SCUM's **Copy location** action to the same key manually; the app does not change SCUM settings. Keyboard layouts differ: use the key capture controls to match your binding, or choose another unused single key.
- Existing saved bindings are preserved. To switch from Ctrl+C, change both SCUM and MiniMap and select **Single key without modifier**. Ctrl+C remains supported with a minimum one-second interval; a dedicated single key avoids injecting Ctrl during gameplay.

### Optional voice navigation — work in progress
- Disabled by default. Enable it in Settings, choose a voice, preview it and adjust volume. Includes **Lyan (Female US)** and supports additional voice packs.
- Distance and turn instructions use the supplied 12 clips. Junction-based turn prompts, wrong-way detection and rerouting are included, but accuracy and timing still need gameplay testing; use the map and road signs to check directions.
- Removed ambiguous keep-left/right prompts from current guidance. Route connector lines are included in off-route checks; ordinary route refreshes no longer interrupt a phrase after “In”.
- Recalculation is shown on screen. The bundled voice has no spoken recalculating clip; future packs can optionally provide one.

### Responsiveness and input
- Single-key tracking requests updates every 250 ms by default. Existing one-second preferences upgrade once; slower custom intervals remain. Modifier bindings retain their one-second minimum.
- Vision cone and heading apply the latest received sample immediately; shorter position smoothing and cached terrain reduce rendering work.
- Removed blocking clipboard retries and repeated full clipboard snapshots from the update loop. Single-key response waits and retry pauses are shorter, and audio playback runs on its own thread.
- Improved injected-key cleanup and chat protection for T, Tab, Enter and Esc. Single-key copying continues while scrolling; modifier copying retains extra input guards.
- Recover keyboard hooks and stale held-key state to address the full-map shortcut becoming unresponsive during long sessions.
- Keep an interactive taskbar icon; click it to reopen Settings.
- Actual update speed still depends on SCUM providing coordinates. These changes do not guarantee uninterrupted tracking or eliminate all gameplay conflicts with Ctrl+C.

## [1.4.7] - 2026-09-20

### Changed
- New installations start at 240 × 240, with a move-and-resize prompt in the setup guide.
- Preserve compact minimap size and position when opening, saving, or configuring the full map.
- Removed unused hunting-legend and legacy category settings, including the obsolete fuel toggle. POI categories remain controlled by the POI filters.
- Removed 51 unused UI string keys across all nine languages and corrected custom-zone help, POI filter labels, and deletion wording.
- Refresh settings from current sidebar state when reopened; keep automatic zoom bounds and their controls synchronised.
- Retained legacy coordinate-interval and player-colour migration aliases.
- Repaired reset-map messages and refreshed first-launch guidance in all nine languages.

## [1.4.6] - 2026-09-19

### Fixed
- **Auto-Updater Version Normalization**: Normalized the application's runtime System.Version to four components (X.Y.Z.0), preventing the currently installed release from incorrectly treating the same published version as a newer update.
- **Release Verification Consistency**: Kept application, assembly, manifest, and live GitHub updater version comparisons aligned under one release version source.

## [1.4.5] - 2026-09-19

### Changed
- **Application Architecture Refactor**: Split the large MiniMap implementation into dedicated startup, native input, position, map canvas, settings, and diagnostics source files while preserving existing runtime behaviour.
- **MapWindow Partial-Class Structure**: Converted MapWindow into a partial class so major application subsystems can be maintained independently without changing user-facing behaviour.
- **Build & Release Hardening**: Improved repository-relative build output handling and removed obsolete build switches and redundant startup tooling.
- **Source & Project Cleanup**: Removed obsolete scripts, dead helper methods, unused rendering code, stale development artifacts, and redundant theme values while retaining required compatibility and migration paths.
- **Regression Test Organisation**: Consolidated diagnostic and self-test code into a dedicated subsystem while retaining updater, input, routing, rendering, settings, and audit regression coverage.

### Fixed
- **Habitat Rendering Culling**: Corrected vertical viewport culling so habitat geometry is tested against the proper visible screen bounds.
- **Waypoint Focus Regression Test**: Corrected the test path for the internal native input bridge following the source-code restructuring.

## [1.4.4] - 2026-09-18

### Added
- **Multi-Layer Custom Map Zones & Sidebar Management**: Expanded the full-screen map sidebar "Custom Map Zones" section into an interactive accordion container displaying all loaded custom zone layers. Each layer features an independent state switch pill and zone count badge (e.g., `[ON] FactionMap [51]`), enabling players to toggle individual layers on or off while maintaining global master control.
- **Dynamic Live Layer Synchronization**: Integrated write-timestamp polling (`lastZonesFileWriteTimeUtc`) that automatically detects external edits to `zones.tsv` and reloads the map layers and sidebar in real time upon opening the full map without requiring an application restart.
- **High-Contrast Selected Zone Highlighting**: Upgraded ZoneEditor selection outlines with a high-contrast dual-layer border (4.5px backing + 2.5px theme accent), providing instant visual clarity against complex terrain and overlapping circular zones.
- **Full-Map Faction Label Visibility**: Added Faction and Custom zone categories to major landmark LOD filters in `ZoneStore.Draw`, ensuring zone names and points of interest remain clearly legible at high zoom distances.
- **System Tray Manual Map Toggle**: Added an explicit manual full-map toggle option to the system tray context menu for direct access regardless of hardware hook state.

### Changed
- **Configurable Coordinate Copy Key (With / Without Modifier)**: Expanded the Key Rebinding Wizard in Settings with a dedicated "Single key without modifier" mode. Players can now configure coordinate telemetry copying either as a combination chord (e.g., `Ctrl+C`, `Alt+C`, `Shift+Key`) or as a standalone single key without requiring any modifier key (e.g., dedicated function or keypad keys). Automatic conflict detection ensures standalone coordinate keys cannot conflict with configured Map or Chat keys.
- **In-Game Keybinding & Chat Guard Overhaul**: Resolved full-screen map activation (`M` key) race condition where typing-activity heuristics (`Native.UserTypingOrActive`) falsely suppressed map toggling. Upgraded `ChatState` with hook-level chat lifecycle tracking: Enter and Escape close active chat, `/` or configured chat key opens tracking, and keypresses continuously refresh the active session to prevent chat timeout mid-sentence. Re-enabled fallback key polling in `Tick()` to ensure responsive activation even under heavy game load.
- **Automated Update Engine & Deployment Verification**: Hardened GitHub release update checks against the official `update.txt` manifest with SHA-256 pre-download validation. Enforced strict version increment comparison (`release.Version > CurrentVersion`) preventing false downgrade prompts on development builds. Implemented rate-limiting detection and exponential backoff retry handling (`UpdateRetryException`).
- **Performance & Smoothness Sweep**: Eliminated jitter and micro-stutters during active coordinate sampling chords. Stabilized player Cone of Vision redraws and HUD telemetry ticker scrolling. Added visible clip bounds culling for zones, habitats, and markers to eliminate off-screen render overhead. Replaced scanline copying with bulk buffer memory transfers in overlay fade transitions.

### Fixed
- **Faction War Zone Detection Engine Overhaul**: Calibrated circular template matching and non-maximum suppression (`1.45 * min(rad, orad)`) in `detect_zones.py`, achieving 100% detection accuracy on all 51 white circular zones across 1024x1024 and 2048x2048 map imports with zero mountain-snow or urban false positives.
- **Authoritative Canonical POI Mapping**: Embedded canonical landmark POI names across all 19 sectors, eliminating seasonal event naming collisions (e.g., "Halloween House" corrected to "D1 Farm").
- **Sidebar Custom Layer Enumeration**: Removed restrictive `Category == Custom` filters in `MiniMap.cs` layer collection, ensuring zones classified as `Faction`, `Bunker`, or other categories are properly indexed under their assigned layer and counted in the sidebar badge.
- **ZoneEditor Polygon Render Flag**: Resolved a bug in `ZoneEditor.DrawDetected` where `showPolygons` was passed as `false`, restoring solid fills and perimeter borders for all circular and polygonal zones.
- **Windows Alt-Tab Task Switcher Deadlock**: Implemented low-level emergency modifier key release (`Ctrl`, `Alt`, `Tab`, `Win`) inside key hook handlers and added 500ms focus shielding to prevent full-screen overlay interference with the Windows DWM task switcher.
- **Continuous Player Coordinate Sampling**: Restored continuous location tracking by decoupling modifier key state validation in coordinate copy scheduling, preventing tracking halts during complex key combinations.

## [1.4.3] - 2026-09-17

### Added
- **Full Bridge Traversal & Island Routing**: Overhauled the GPS routing engine to navigate seamlessly across bridges. Embedded high-precision physical bridge curves and abutment connections for the East Suspension Bridge (A0 to Z0), West Highway Bridge (A3 to Z3 Rogoznica), Central Diagonal Railway Bridge (A2 to Z2), Central-East Highway Bridge (A1 to Z1), and C2 Dam Crest Crossing.
- **Strict Land/Bridge Routing & Water Avoidance**: Enhanced pathfinding prioritizing continuous road and bridge traversal across the entire mainland and southern islands. Nautical water transit is strictly reserved as an unavoidable fallback for isolated offshore islands lacking physical crossings.
- **Multi-Modal Island Transit Engine**: Added intelligent multi-modal routing for offshore islands without physical bridges, routing along roads to the optimal coastal departure point, indicating nautical water transit across the channel, and continuing along roads from the landing dock to the destination.

### Changed
- **Diagonal Railway Bridge Geometry Correction**: Realigned the central diagonal railway bridge to exact surveyed mainland and southern island abutments with true straight deck curve geometry.
- **Zero-Allocation Epoch-Indexed A* Algorithm**: Replaced heap-allocated dictionary lookups with flat primitive arrays and an epoch-based O(1) state reset mechanism, eliminating garbage-collection overhead and reducing full-map pathfinding query latency to under 1 millisecond.
- **Two-Pass Topological Micro-Gap Healing**: Implemented dual-pass graph analysis (standard <= 35m, cross-component <= 65m) dynamically unifying 96.2% of all road network nodes into a continuous navigable graph.
- **Multi-Candidate Snapping & Direct Walk Optimization**: Upgraded road snapping to evaluate up to 8 nearest candidates with guaranteed main-component representation. Added smart direct-walk fallback when direct walking distance is significantly shorter than an off-road detour.

## [1.4.2-HOTFIX] - 2026-09-17

### Fixed
- **Zone Importer Base Map Preparation**: Resolved `FileNotFoundError: [Errno 2] No such file or directory: '...reference.png'` in automatic zone detection by generating the base map reference image from embedded assembly resources or local map files prior to launching Python `detect_zones.py`.
- **Settings Menu Layout & Usability**: All configuration sections now default to expanded for instant visibility of all minimap overlay options. Added a permanent bottom-docked action footer for immediate access to telemetry, `Open Data Folder`, and `Done` buttons.
- **WinForms Handle & GDI Exhaustion**: Eliminated GDI font handle leaks and USER object accumulation across repeated settings panel rebuilds and tactical section header rendering.
- **Embedded Zone Detection Script Fallback**: Added candidate path search and embedded assembly manifest extraction for `detect_zones.py` when running unbundled or standalone binaries.

## [1.4.2] - 2026-09-17

### Fixed
- **Add Waypoint Dialog Stability & Focus**: Resolved a `Win32Exception: Error creating window handle` crash and focus unresponsiveness when opening the custom waypoint prompt via the `Insert` shortcut or the full-map right-click context menu. Releasing Win32 mouse capture, resetting cursor clipping, marshalling dialog invocation past `WM_TIMER` dispatch, and setting the title bar close button `TabStop = false` ensures the name input field is immediately activated, selected, and focused with an I-beam cursor.
- **Auto-Update Downgrade Prompt**: Fixed false-positive update notifications on local and development builds by removing the same-version SHA-256 mismatch comparison in `CheckForUpdates`, ensuring update alerts only trigger when a strictly newer release version is available on GitHub (`release.Version > CurrentVersion`).

## [1.4.1] - 2026-09-17

### Added
- **Global Multilingual Localization Expansion**: Added complete native translations for 7 new languages: **French** (`fr`), **German** (`de`), **Dutch** (`nl`), **Russian** (`ru`), **Chinese (Simplified)** (`zh-CN`), **Turkish** (`tr`), and **Arabic** (`ar`), expanding total language support to 9 languages alongside English and Argentine Spanish.
- **Dynamic Welcome & Startup Guide Language Integration**: Integrated all 9 languages into the `StartupGuideDialog` onboarding welcome screen and Settings panel `TacticalComboBox` with instant, zero-restart live translation switching across all slides, tabs, and hardware dialogs.
- **Full UI & POI Coverage**: Comprehensive localization across all 335 interface strings, hotkey guidance, HUD telemetry notes, keybinding wizard, POI categories, and section titles.

### Fixed
- **Zone Editor Launch Stability**: Resolved a Win32 window handle creation exception when opening the Zone Creator Wizard from the full-map sidebar overlay by safely releasing mouse capture, deferring modal invocation past the mouse-down dispatch cycle, and ensuring screen-centered dialog hierarchy.

## [1.4.0] - 2026-09-17

### Added
- **Customizable Player Heading Cone & Position Dot Color**: Users can now customize the color of their heading vision cone and player dot in Settings or the overlay context menu, with real-time saving and configuration persistence.
- **Tactical Section Headers & Dropdown Controls**: Built-in `TacticalSectionHeader` with interactive amber indicator bars and vector chevrons, paired with custom dark-themed `TacticalComboBox` controls.
- **Contextual Settlement & Sector Intelligence Engine**: Intelligently names POIs based on spatial proximity to 61 settlements, bunker sector codes, and natural singularized landmark labels.

### Changed
- **Purged All Blue Palette Elements**: Completely redesigned `OverlayTheme` to authentic military tactical carbon (`#0C0C0C`), matte graphite plates, milled bronze-steel borders (`#32302C`), and high-contrast survival hazard amber (`#FF9F1C`).
- **Tactical Dialog Redesign**: Converted "Save Waypoint" and "Remove Waypoint" prompts from standard OS windows into frameless tactical hardware interfaces with custom buttons, fields, and accent headers.
- **Settings Menu Visual Overhaul**: Eliminated native white scrollbars and cramped layouts with clean collapsible sections and dark explorer scroll containers.
- **Navigation Pill Geometry & Illumination**: Re-aligned the circular minimap navigation pill 3px lower flush against the bottom bezel and added reactive route-color illumination when actively routing.
- **In-Game Chat Channel Cycling & Map Protection**: Fixed an issue where cycling chat channels with Tab unlatched the chat-paused gate and caused typing to inadvertently open the full map. ChatState now treats Tab and regular keystrokes as active session extensions while open, and closing full-map mode when chat opens preserves the active chat pause state rather than resetting it.
- **Stabilized Map & Player Motion**: Resolved map and player wobble, jitter, and desync across all modes. In minimap mode, the player marker and feeder origin are mathematically locked to the exact geometric center, while the background map moves smoothly without 0.25-pixel staircasing. MapMotion duration now smoothly spans the full 1-second sampling interval instead of stopping dead for 500ms, stationary deadbands suppress idle floating-point noise, and auto-zoom is gated to vehicle speeds (> 18 km/h) to eliminate scale pumping on foot.
- **Smooth Ping-Pong Location Ticker**: Overhauled infobar text scrolling for locations that exceed the banner width. Replaced sudden edge wrapping with continuous sinusoidal ease-in-out bouncing (scrolling to the end, pausing, and smoothly gliding back to the start) paired with subpixel anti-aliased text rendering to eliminate integer pixel snapping.
- **Ticker Perpetual Reset & Render Cadence Fix**: Isolated dynamic distance and ETA counter digits using base semantic keys (`GetScrollBaseKey`) so periodic 1-second GPS coordinate polls do not reset the infobar marquee cycle back to start. Refined render timer pacing to 50ms (20 FPS) and relaxed terrain background caching granularity to 0.125px (`Round(left * 8)`) to eliminate UI-thread message pump starvation during movement.

## [1.3.18] - 2026-09-16

### Changed
- Automated build packaging update.
- Performance refinements and maintenance release.

## [1.3.17] - 2026-09-16

### Changed
- Automated build packaging update.
- Performance refinements and maintenance release.

## [1.3.16] - 2026-09-16

### Changed
- Automated build packaging update.
- Performance refinements and maintenance release.

## [1.3.15] - 2026-09-16

### Changed
- Automated build packaging update.
- Performance refinements and maintenance release.

## [Unreleased]

## [1.7.0] - 2026-10-09

### Added
- **Full-Map Waypoint Action**: Full-map right-click options can now save a named custom waypoint at the clicked map location using the existing persistent cyan-marker workflow.
- **SCUM Key Rebinding Wizard**: Settings can capture non-default SCUM Map and Chat bindings and persist them for full-map toggling and chat-safe tracking.
- **Complete SCUM Binding Capture**: The rebinding wizard now captures Map, Chat, and the coordinate-copy modifier/key, removing the hard-coded Ctrl+C conflict for custom free-look and other bindings.
- **Zone Editor Stability**: Full-map and settings launches now share guarded editor startup, with malformed saved zone entries normalized instead of causing an unhandled exception.

### Changed
- **Infobar Layout**: Removed the `AUTO | Xs ago` status tag and added clipped horizontal scrolling for location strings that exceed the infobar width.

## [1.3.14] - 2026-09-15

### Changed
- **Reliable In-Game Hotkeys**: Routed global hotkey callbacks safely through the application UI thread and added one-shot fallback detection for settings, overlay visibility, search, waypoint, and zoom controls when Windows misses a hook event.
- **Safer Full-Map Input**: The `M` map toggle remains exclusively chat-aware, so typing `m` in SCUM chat cannot open or close the full map.
- **Automatic Update Installation**: Launch checks now prompt immediately when a newer verified release is available. After approval, SCUM MiniMap downloads the update to a staging file, verifies its SHA-256 checksum, closes, replaces the prior executable, and restarts itself.

### Fixed
- **Aim Down Sights Input**: Right mouse button input now always passes through the overlay to SCUM, including while the full map is visible, so the overlay cannot swallow an ADS press.
- **Intermittent Chat Map Opening**: Removed the chat-unaware hardware polling route that could toggle the full map while text was being entered.
- **Unresponsive Overlay Controls**: Prevented cross-thread hotkey handling from leaving menu, settings, or overlay UI in an incomplete state.

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
- **Coordinate Calibration & Road Alignment**: Calibrated world origin $(617718, 618618)$ with recompiled road network graph and sector grid, aligning player in-game position, vehicle road splines, and coastline features accurately with the high-resolution map.
- **Minimap Jitter & Motion Smoothing**: Removed discrete quantized terrain caching steps, restoring smooth 25 Hz floating-point rendering during movement while preserving idle performance caching.
- **Optimized Map Asset Loading**: Integrated the optimized 10.6 MB map texture into application resources and eliminated outdated local AppData asset shadowing.
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
