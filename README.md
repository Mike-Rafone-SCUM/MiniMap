# SCUM MiniMap v2.3.24

SCUM MiniMap is an external map overlay and road-navigation utility for SCUM, made for the Skynett community. Version 2 uses WPF for the tactical map, compact minimap, Settings, setup guide and Zone Creator. The existing tracking and routing engine remains behind the new interface.

## Settings and group polish (v2.3.24)

Version 2.3.24 implements selected findings from [the final audit](docs/WPF-FINAL-POLISH-AUDIT-2026-10-23.md); [the worklog](docs/WPF-MAJOR-RELEASE-WORKLOG.md) separates implemented checks from remaining acceptance. The five-page setup is still required on each application version update. Extract the complete ZIP with its assets folder before launching.

In Private positions, **Done** saves an isolated Your name edit; an explicit name-save button also keeps the dialog open. Existing connections use the new name after reconnecting. **Leave group** keeps the saved code but disables automatic rejoining. **Forget on this PC** removes only this PC's saved membership and does not delete the relay group. Forgetting an owned group permanently loses its owner controls on this PC; preserve the invitation code first. **Delete group** remains the owner's separate action for everyone. Pending operations must be resolved before forgetting. Code replacement now asks for confirmation and copy failure offers a retry through Copy code without creating another group.

## Main map position and size

Manual position fields and the main-map move/resize preview offer four arrow buttons for one-screen-pixel adjustments. The compact minimap's Move minimap preview has the same controls. Preview movement uses physical pixels rather than one WPF logical unit. Done saves; Escape cancels a preview.

Open **Settings > Appearance > Positioning > Main map position and size**, or the same control in the main-map sidebar. **Fit to SCUM window** follows the game's playable window area, including window moves and resizes. **Manual position and size** saves screen-pixel bounds; use the exact fields or **Move and resize preview**, then Done. Negative monitor coordinates are supported. Escape cancels the preview; its Done saves immediately. **Fill monitor** fills the fields with the game monitor's bounds. Saved off-screen placements recover to a connected monitor. Compact minimap placement is separate.

## Finding settings

Settings now uses six categories. General contains language, startup and the setup guide. Appearance contains main-map/minimap placement and display controls, with collapsible map-layer, marker/history and zone-tool sections. Tracking and voice contains automatic zoom, capture timing and audio. Shortcuts contains the three SCUM bindings and all MiniMap actions. Groups and routes opens private groups/member colours and route editing. Tools and data contains admin controls, updates, support links, Data Folder and collapsed recovery actions.

The footer contains Done. Use the category list with the mouse or keyboard; Ctrl+Tab and Ctrl+Shift+Tab cycle categories. Invalid numeric edits must be corrected before leaving their category. Collapsed sections retain their expansion state while the Settings window stays open. Changes use the existing settings/profile format; the mandatory five-page setup policy remains.

Transparent-map dragging now coalesces input until the next Windows composition frame, with a timer fallback. Local synthetic 4K tests showed steadier drag updates; results vary by PC and this is not a guaranteed game-FPS improvement. Existing settings and keys are preserved. Complete all five setup pages after updating.

## Install or update

1. Extract the **complete release ZIP**. Keep `assets` beside `SkynettMiniMap.exe`; the executable alone is not a complete installation.
2. Run `SkynettMiniMap.exe` and approve the administrator prompt. Both Test and Release executables request administrator rights so their input can reach an elevated SCUM process.
3. Run SCUM in **Windowed** or **Borderless Window** mode. Exclusive fullscreen suppresses external desktop overlays.
4. In SCUM Controls, set **Copy Location** to **NumPad /** without Ctrl, Shift or Alt, or configure the same alternative binding in both SCUM and MiniMap. Existing saved bindings are retained.
5. Complete the getting-started checklist and match Copy Location in both apps. Number-pad users keep MiniMap's default Num /; other users change the same single key in SCUM and MiniMap. Continue through Appearance, Map options, Tracking/zoom and optional Sharing, then select Done.

The five-page setup is required for every user on first install and after each new version, including users with an old saved guide opt-out. Existing settings and keys are kept. Use ordinary Next clicks; only final Done records completion. Closing early makes setup return at the next launch. Short instructions and a live Appearance preview show the main choices; extra controls are under More options. Setup can be moved, minimized or left open while Alt-Tabbing to SCUM. Future pages stay locked until reached, and visited pages remain available in the left rail. Reopen the guide using the map's **?** button. Private positions opens the same group-code screen used in Settings: create and copy a code, or paste a code and Connect. Save and join multiple groups independently; only the creator can refresh each group's code or delete the group for everyone after confirmation. Codes and ownership are saved with Windows account protection; active groups automatically reconnect when MiniMap opens again. Closing the app leaves you offline without shutting down the group; explicitly leaving a group stops automatic rejoining. The encrypted relay avoids home port forwarding, and the direct-IP Host/Join button has been removed. See [private sharing setup](docs/PRIVATE-POSITIONS.md). Appearance preferences are saved; per-player visibility choices last for the session.

The first upgrade from an EXE-only release requires extracting the new complete ZIP and running `Update-MiniMap.cmd`. Later automatic updates download and validate the complete package. Installation assets are checksum-verified; missing or modified assets stop normal startup. Re-extract the complete package to recover.

Release settings, zones and kits are kept in `%LOCALAPPDATA%\ScumMiniMap`. Test builds use the separate `%LOCALAPPDATA%\ScumMiniMap-ResponsivenessTest` profile. Back up your profile before a major upgrade. A post-update result is shown after restarting and its validated temporary helper is cleaned up; acknowledging the result prevents repeated notifications.

MusicalScumbag users should use its corrected **F10** auto-walk build. Its older NumPad / shortcut conflicts with MiniMap's default Copy Location key.

## Automatic launch with SCUM

In **Settings > General**, enable **Start MiniMap with SCUM and close it when SCUM exits**. The option defaults to off. Enable it from the extracted application folder you intend to keep. Windows Task Scheduler runs a small hidden watcher at sign-in and immediately after enabling; it uses your existing Windows account, without storing a password. The app already requires administrator rights, so the task uses the highest rights available to that account.

The watcher checks the local session's `SCUM.exe` and `SCUM-Win64-Shipping.exe` every two seconds. MiniMap saves and exits normally after ten seconds of confirmed game absence. It stays open through brief process handovers. Closing MiniMap yourself prevents another automatic launch during that game session; launching SCUM again starts it again. Opening MiniMap manually before SCUM is supported.

Disable the option to remove the scheduled task and stop the watcher. This keeps the current MiniMap open. Disable it before moving/deleting the application folder, then enable it again from the new location. Release and Test settings are separate. Diagnostics never register tasks or run the watcher. If setup fails, the setting shows an error; watcher errors are recorded in `auto-launch/watcher.log` inside the active profile.

## Controls

| Default key | Action |
|---|---|
| M, or your configured SCUM Map key | Open/close the full map |
| Home | Open/close Settings |
| Insert | Save a named waypoint |
| Delete | Search for a place or grid sector |
| End | Show/hide the compact minimap |
| PageUp / PageDown | Zoom in/out |
| Down arrow | Add a death marker |
| NumPad + | Open the player directory |

Shortcuts can be changed in Settings. Reset zoom, clear route, Zone Creator, setup guide and admin commands also have configurable bindings; some begin unbound. Coordinate copying pauses while SCUM chat owns typing, during supported editors and when SCUM loses focus. Sending another message with Enter does not release chat protection; physical Escape closes that chat session.

## Tactical map and minimap

The tactical map is transparent around the world map. Its sidebar overlays the viewport, so expanding or collapsing it preserves map centring and scale. The floating rail offers close, collapse/expand, Settings (cog), setup guide (?) and death-marker removal (tombstone), with localized action tooltips.

Tactical terrain opacity is automatic: 0% at 1× zoom, 100% above 1×. The toolbar reports this policy-fixed value. Compact minimap opacity is independent and remains adjustable.

Drag the bottom bar's left handle to move its controls or its right handle to resize them between 70% and 160%. Right-click the bar to reset its size and position. These layout choices last for the current session. To move the compact minimap, open Settings and choose **Move minimap**; Done saves its position and Escape cancels.

The tray's **Show/Hide** and configured minimap shortcut control the same WPF minimap. An unexpectedly hidden or minimized HUD is restored, and saved placement is kept within a connected monitor's work area. Restoring the minimap does not take keyboard focus. An explicit Hide remains respected until you show it again.

Settings controls grid lines/labels and opacity, label sizing and placement, shape, edge fading, status position, history duration and zoom limits/step. Route guidance, history and the player marker have independent colour controls.

## Navigation, POIs and wildlife

Search by place name or sector, or click anywhere within the world map—including empty terrain and water—to navigate to that exact point. This also works at 0% terrain opacity. Right-click offers navigation, saving a named waypoint, clearing the route and deleting a nearby saved marker by name. Clicking the active destination clears its route while preserving a saved waypoint. An off-screen destination shows an arrow at the viewport edge.

The compact status display separates sector/location from route details. Long route text scrolls back and forth; distance changes preserve the ticker's progress.

The first WPF preferences migration enables every POI category, including hunting and fishing. Subsequent launches preserve your selections. Each POI group has a switch, mixed state and independent type switches. Hover a POI or wildlife region to see its details and enabled fish/animal species in the top-left info card. Wildlife regions have subtle coloured fills and thin outlines, with polygon holes preserved.

## Custom zones and Zone Creator

Drop a downloaded `zones` folder beside the executable (for example, `zones/skynett/zones.tsv`). WPF checks for added, changed or removed packs in the background and refreshes the map and custom-zone dropdown within a few seconds, without restarting. Keep each pack in its own subfolder with a `zones.tsv` file. Incomplete or invalid files retain the last working zones until a valid copy is available. Folder packs use the default SCUM map and stay separate from your saved profile annotations; remove a pack folder to unload it, or use the existing layer/zone switches to hide it. Import a pack through Zone Creator if you want an editable profile copy.

Expand **Custom map zones** to select layers and individual zones. Hidden entries remain listed and choices persist. Enabling an individual zone also enables its layer and the master switch. Disabling a layer preserves its individual choices.

Open Zone Creator from Settings or a configured shortcut. It provides import/export, layers, filtering, naming, grouping, ordering, duplication, deletion and polygon, rectangle, circle, move and outline-edit tools. Screenshot import displays source and destination canvases together; use two landmarks for alignment, then trace into map coordinates. Geometry undo and manual fallback are available.

Set outline colour independently from fill colour and opacity. Older zone files retain their default fill behavior; the optional fill field preserves their existing death timestamp field. Ordinary waypoints and death markers retain separate visibility behavior.

In Zone Creator, use Ctrl-click for individual selections or Shift-click for a range. Filter by zone type (such as Bunker or Town), layer and name, then choose **Select All** to edit that visible group. **Select same colour** selects matching visible zones. Outline colour, fill colour and opacity apply to every selected zone; changing opacity preserves each zone's fill colour. Duplicate, Apply name and Delete selected also operate on the selection. Single-zone geometry tools stay separate. Save zones commits your changes; Cancel leaves saved data unchanged.

To open downloaded zone TSVs from Explorer, use **Enable opening zone TSV files** in Settings. Right-click a zone `.tsv`, choose **Open with → SCUM MiniMap**, and choose **Always** if you want double-click opening. Windows keeps control of the default application for this shared file extension. A TSV can also be dropped onto the MiniMap executable or opened with `--import-zones "path/to/zones.tsv"`. MiniMap shows **Name this map**, then opens Zone Creator with the file's zones selected in that named layer. Save to add them to the map; Cancel preserves existing zones. If MiniMap is already running, the request goes to that instance and waits until any current dialog has closed. Zone files are validated before the naming/import flow.

## Squad tools

In **Private groups**, **Your shared member colour** offers 16 colours. Each person chooses their own colour, seen consistently by teammates on their player arrow, routes and markers, including older clients. It applies to all code-based private groups on this Windows profile and survives restart. Members can choose the same colour; coordinate choices if you want every teammate distinct. Changing colour briefly reconnects active groups while preserving Leave, host Disconnect, memberships and ownership. A teammate following the previous identity may need to select Follow teammate again after your colour changes.

Give a group a name when creating it, or edit its name in Private positions and choose **Save group name**. Any member with the group code can update its shared name; only the creator retains code-refresh controls. Names travel encrypted between connected members and are saved with each Windows-protected membership. A new member learns the name when another member is online. Renaming leaves invite codes unchanged. Names distinguish players and markers across multiple groups. Teammates use the same heading-arrow shape as your own marker and point in their own direction; the heading-display setting applies to both.

Open **Squad tools** in the full-map sidebar for group connections, teammate visibility/follow controls and **Danger**, **Loot** or **Pick me up** pings. Its linked **Routes** and **Map presets** pages are also available directly from the sidebar and Settings. Pings require a connected group and a fresh captured position; the dialog keeps game input protected while sending an explicit ping. Right-click the map to place a ping at a selected location. Assign the three optional hotkeys in Settings; they start unbound to preserve your existing controls. A two-second cooldown prevents repeated presses from flooding the group. Pings expire after 60 seconds from first receipt; heartbeat updates do not extend them. Older clients display them as ordinary markers until the sender withdraws them.

Teammate cards show connection status and seconds since the last captured location. Persistent group rosters retain offline members across empty rooms and relay restarts, with live locations hidden. Transient legacy peer cards can remain for up to five minutes. **Follow teammate** updates your destination when they move at least 25 m, at most once every two seconds. Navigation pauses when their position is unavailable and resumes on a fresh update. **Stop navigation**, Clear, or choosing another destination cancels follow.

Right-click to **Add route stop**, select a search result and choose **Add route stop**, or add a manual GPS destination from its map card or the **Routes** page. Named search results and shared markers retain their names when added. Open **Routes** from a GPS/route card to inspect the ordered stops and remove individual stops. A route holds up to 16 ordered stops and is shared with your connected groups through the encrypted marker transport. Choose **Start route** for your route or another member's route. **Stop navigation** keeps the route for resumption; **Clear route** removes your stops. Stops advance within 40 m; you can navigate independently of other members. Route drafts and pings are session-local. Only your own route and pings are published; received routes are not automatically forwarded into other groups. New clients understand stop ordering; older clients see individual markers.

Routes calculate and start navigation automatically as stops are placed or edited. After stopping, **Start route** resumes navigation. Right-click → **Routes** to remove the clicked stop, undo, start or stop navigation, or open the route editor. **Your route** selects **Solo route** or **Shared route**. Right-click a teammate's arrow for **Follow teammate**. Choosing an ordinary GPS destination immediately replaces route/follow navigation while retaining your editable route.

**Solo routes** represent a player's own plan and are visible to their connected groups. **Shared squad routes** remain under their original owner. Routes use their owner's assigned member colour; colour does not imply privacy. Following a shared route updates with the owner's new stages without rebroadcasting that route into your other groups; your duplicate personal route publication is suppressed while following it. Other players' routes are calculated locally from their encrypted stops and available positions. No route geometry is sent to the server. Each player has a separate **Routes** visibility pill.

Solid route legs follow calculated roads. Dashed legs provide direct guidance when a road route is unavailable; they are not a verified drivable road. Calculations run off the UI thread, coalesce rapid edits and discard outdated results. Removing a stop recalculates the remaining ordered route. Without a fresh player position, the inter-stop sections can still be calculated.

Under **Map presets**, choose a suggested name or type your own, then **Save current map preset**. Saving the same name on the same map replaces that preset. Click a saved name to restore categories, custom-zone visibility, map layers, zoom, rotation, clustering and opacity. Up to 24 presets are stored in the current Windows profile; each applies only to its original map. Automatic zoom, group memberships, sharing and hotkeys are not changed by presets. Applying a stored zoom does not disable automatic zoom; subsequent tracking can adjust it. Unreadable presets block Save until you explicitly back up and reset; the recovery backup stays beside `map-presets.dat`. Failed writes preserve the previous saved list.

The full map closes when you switch to an unrelated application and stays closed when you return. Escape, Tab and the map toggle can dismiss the visible map even if tracking is paused. Dialogs and text entry retain their own keyboard behavior.

## Death markers

Death locations display a red tombstone/cross with an age label. Right-click to delete a specific marker; the floating tombstone removes death markers through the existing removal action. Ordinary-waypoint visibility does not hide death markers.

Recovery cleanup first observes departure of at least 50 m, then a return within 25 m, and removes the marker only after leaving at least 100 m away. The marker stays while you recover items. Failed saves retain it. Recovery progress is session-local, so restarting safely retains markers.

## Specialist tools and compatibility

Existing admin, player-directory, profile and spawn tools remain available through Settings, tray controls and configured shortcuts. Some use native dialogs. They require the relevant SCUM permissions; submitted command values are not proof of the game's current state.

Launch `SkynettMiniMap.exe --legacy-ui` to use the original frontend. This remains useful for features whose WPF presentation is incomplete: arbitrary imported terrain images, full directional vision cone, detailed compass, history timestamps on hover and admin-player map overlays.

## Performance and current limits

Version 2 caches zone identities, visibility lists, trail snapshots and world geometry. It avoids repeated formatting/hashing of all vertices during idle refreshes, culls off-screen geometry and preserves sidebar controls during camera movement.

Automated checks cover input ownership, rendering, routing, persistence, package integrity, all 20 language catalogues and a detailed-zone idle stress case. They do not certify every GPU, monitor, DPI, OBS setup or long-duration workload. Very large expanded custom-zone lists are not virtualized. Toolbar layout and death-recovery progress are session-local.

SCUM MiniMap does not read game memory or inject DLLs. This statement is not an official anticheat certification. SCUM MiniMap is an independent community tool, not an official SCUM product.

## Release history, source and credits

See [CHANGELOG.md](CHANGELOG.md) for release notes and previous releases. The source checkout uses `Build.ps1 -Configuration Release` or `Build.ps1 -Configuration Test`, both through the canonical build scripts; published output directories are immutable.

Community map datasets, wildlife regions, bundled fonts and other third-party resources retain their existing credits and licenses. See [ATTRIBUTION.md](ATTRIBUTION.md) for full details. SCUM artwork, game definitions and trademarks belong to their respective owners.

## Troubleshooting and persistence

| Symptom | Next step |
|---|---|
| Missing/wrong assets | Extract the full application ZIP into a new folder; keep the matching assets beside the executable. |
| Presets cannot be read | Keep the original. Use Back up and reset only when ready to start a new list; preserve the adjacent recovery backup for support. |
| Local save failed | Check folder access and free space, then retry. Settings/setup Done stays open on failure; do not assume an unsaved edit survived restart. |
| Group code rejected | Check the complete invite code and ask the owner whether it was replaced. |
| Group limit reached | Forget an unused membership on this PC; this does not delete the group for others. |
| Clipboard busy | Use Copy code again on the existing group. |
| Host disconnected you | Use Connect manually, or restart; automatic retry remains blocked for the current session. |
| Sharing unavailable | Check your connection and retry. Solo map use remains available; relay operations require the service. |

Settings, keys, saved places, presets and retained group memberships persist in the current Windows profile. Route stops, danger pings, per-player visibility and follow/navigation progress are temporary. Group credentials use Windows account protection; copying the encrypted file to another account is not a portable backup of owner access.

| Capability | WPF status / requirement |
|---|---|
| Tactical map, HUD, setup, Settings, Routes and groups | WPF interface; sharing requires the relay. |
| Admin/player/profile/spawn tools | Some native dialogs; relevant SCUM permission required. A submitted command is not proof of execution. |
| Arbitrary terrain images, directional cone, detailed compass, history hover timestamps, admin-player overlays | Original frontend remains required for complete presentation. |

Native-language review, real-game acceptance, Narrator/high contrast, mixed-DPI hardware and current WPF OBS capture remain pending. Small-window fitting is covered locally and does not certify every display combination.
