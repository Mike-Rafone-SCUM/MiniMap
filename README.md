# SCUM MiniMap v2.3.12

SCUM MiniMap is an external map overlay and road-navigation utility for SCUM, made for the Skynett community. Version 2 uses WPF for the tactical map, compact minimap, Settings, setup guide and Zone Creator. The existing tracking and routing engine remains behind the new interface.

## Install or update

1. Extract the **complete release ZIP**. Keep `assets` beside `SkynettMiniMap.exe`; the executable alone is not a complete installation.
2. Run `SkynettMiniMap.exe` and approve the administrator prompt. Both Test and Release executables request administrator rights so their input can reach an elevated SCUM process.
3. Run SCUM in **Windowed** or **Borderless Window** mode. Exclusive fullscreen suppresses external desktop overlays.
4. In SCUM Controls, set **Copy Location** to **NumPad /** without Ctrl, Shift or Alt, or configure the same alternative binding in both SCUM and MiniMap. Existing saved bindings are retained.
5. Complete the getting-started checklist and match Copy Location in both apps. Number-pad users keep MiniMap's default Num /; other users change the same single key in SCUM and MiniMap. Continue through Appearance, Map options, Tracking/zoom and optional Sharing, then select Done.

The five-page setup is required for every user on first install and after each new version, including users with an old saved guide opt-out. Existing settings and keys are kept. Use ordinary Next clicks; only final Done records completion. Closing early makes setup return at the next launch. Short instructions and a live Appearance preview show the main choices; extra controls are under More options. Setup can be moved, minimized or left open while Alt-Tabbing to SCUM. Future pages stay locked until reached, and visited pages remain available in the left rail. Reopen the guide using the map's **?** button. Private positions opens the same group-code screen used in Settings: create and copy a code, or paste a code and Connect. Save and join multiple groups independently; only the creator can refresh each group's code or delete the group for everyone after confirmation. Codes and ownership are saved with Windows account protection; sharing starts disabled. The encrypted relay avoids home port forwarding, and the direct-IP Host/Join button has been removed. See [private sharing setup](docs/PRIVATE-POSITIONS.md). Appearance preferences are saved; active connections and per-player visibility choices last for the session.

The first upgrade from an EXE-only release requires extracting the new complete ZIP and running `Update-MiniMap.cmd`. Later automatic updates download and validate the complete package. Installation assets are checksum-verified; missing or modified assets stop normal startup. Re-extract the complete package to recover.

Release settings, zones and kits are kept in `%LOCALAPPDATA%\ScumMiniMap`. Test builds use the separate `%LOCALAPPDATA%\ScumMiniMap-ResponsivenessTest` profile. Back up your profile before a major upgrade. A post-update result is shown after restarting and its validated temporary helper is cleaned up; acknowledging the result prevents repeated notifications.

MusicalScumbag users should use its corrected **F10** auto-walk build. Its older NumPad / shortcut conflicts with MiniMap's default Copy Location key.

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

Use the wheel or bottom controls to zoom. Drag the map to pan and choose **Follow player** to resume following. Initially, the full map has 0% terrain opacity at 1× and becomes opaque when zoomed in. The opacity slider works at every zoom level; once you adjust it, your chosen transparency is retained across zoom changes and restarts. The compact minimap has its own opacity setting.

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

Give a group a name when creating it, or edit its name in Private positions and choose **Save group name**. Any member with the group code can update its shared name; only the creator retains code-refresh controls. Names travel encrypted between connected members and are saved with each Windows-protected membership. A new member learns the name when another member is online. Renaming leaves invite codes unchanged. Names distinguish players and markers across multiple groups. Teammates use the same heading-arrow shape as your own marker and point in their own direction; the heading-display setting applies to both.

Open **Squad tools** in the full-map sidebar for group connections, teammate visibility/follow controls and **Danger**, **Loot** or **Pick me up** pings. Its linked **Routes** and **Map presets** pages are also available directly from the sidebar and Settings. Pings require a connected group and a fresh captured position; the dialog keeps game input protected while sending an explicit ping. Right-click the map to place a ping at a selected location. Assign the three optional hotkeys in Settings; they start unbound to preserve your existing controls. A two-second cooldown prevents repeated presses from flooding the group. Pings expire after 60 seconds from first receipt; heartbeat updates do not extend them. Older clients display them as ordinary markers until the sender withdraws them.

Teammate cards show connection status and seconds since the last captured location. Disconnected cards remain for up to five minutes, with their location hidden. **Follow teammate** updates your destination when they move at least 25 m, at most once every two seconds. Navigation pauses when their position is unavailable and resumes on a fresh update. **Stop navigation**, Clear, or choosing another destination cancels follow.

Right-click to **Add route stop**, select a search result and choose **Add route stop**, or add a manual GPS destination from its map card or the **Routes** page. Named search results and shared markers retain their names when added. Open **Routes** from a GPS/route card to inspect the ordered stops and remove individual stops. A route holds up to 16 ordered stops and is shared with your connected groups through the encrypted marker transport. Choose **Start route** for your route or another member's route. **Stop navigation** keeps the route for resumption; **Clear route** removes your stops. Stops advance within 40 m; you can navigate independently of other members. Route drafts and pings are session-local. Only your own route and pings are published; received routes are not automatically forwarded into other groups. New clients understand stop ordering; older clients see individual markers.

Routes calculate and start navigation automatically as stops are placed or edited. After stopping, **Start route** resumes navigation. Right-click → **Routes** to remove the clicked stop, undo, start or stop navigation, or open the route editor. **Your route** selects **Solo route** or **Shared route**. Right-click a teammate's arrow for **Follow teammate**. Choosing an ordinary GPS destination immediately replaces route/follow navigation while retaining your editable route.

**Solo routes are amber** and represent a player's own plan, automatically visible to their connected groups. **Shared squad routes are cyan** and appear under their original owner. Following a shared route updates with the owner's new stages without rebroadcasting that route into your other groups; your duplicate personal route publication is suppressed while following it. Other players' routes are calculated locally from their encrypted stops and available positions. No route geometry is sent to the server. Each player has a separate **Routes** visibility pill.

Solid route legs follow calculated roads. Dashed legs provide direct guidance when a road route is unavailable; they are not a verified drivable road. Calculations run off the UI thread, coalesce rapid edits and discard outdated results. Removing a stop recalculates the remaining ordered route. Without a fresh player position, the inter-stop sections can still be calculated.

Under **Map presets**, choose a suggested name or type your own, then **Save current map preset**. Saving the same name on the same map replaces that preset. Click a saved name to restore categories, custom-zone visibility, map layers, zoom, rotation, clustering and opacity. Up to 24 presets are stored in the current Windows profile; each applies only to its original map. Group memberships, sharing and hotkeys are not changed by presets.

The full map closes when you switch to an unrelated application and stays closed when you return. Escape, Tab and the map toggle can dismiss the visible map even if tracking is paused. Dialogs and text entry retain their own keyboard behavior.

## Death markers

Death locations display a red tombstone/cross with an age label. Right-click to delete a specific marker; the floating tombstone removes death markers through the existing removal action. Ordinary-waypoint visibility does not hide death markers.

Recovery cleanup first observes departure of at least 50 m, then a return within 25 m, and removes the marker only after leaving at least 100 m away. The marker stays while you recover items. Failed saves retain it. Recovery progress is session-local, so restarting safely retains markers.

## Specialist tools and compatibility

Existing admin, player-directory, profile and spawn tools remain available through Settings, tray controls and configured shortcuts. Some use native dialogs. They require the relevant SCUM permissions; submitted command values are not proof of the game's current state.

Launch `SkynettMiniMap.exe --legacy-ui` to use the original frontend. This remains useful for features whose WPF presentation is incomplete: arbitrary imported terrain images, full directional vision cone, detailed compass, history timestamps on hover and admin-player map overlays.

## Performance and current limits

Version 2 caches zone identities, visibility lists, trail snapshots and world geometry. It avoids repeated formatting/hashing of all vertices during idle refreshes, culls off-screen geometry and preserves sidebar controls during camera movement.

Automated checks cover input ownership, rendering, routing, persistence, package integrity, all 19 language catalogues and a detailed-zone idle stress case. They do not certify every GPU, monitor, DPI, OBS setup or long-duration workload. Very large expanded custom-zone lists are not virtualized. Death-marker age text may remain stale while stationary. Toolbar layout and death-recovery progress are session-local.

SCUM MiniMap does not read game memory or inject DLLs. This statement is not an official anticheat certification. SCUM MiniMap is an independent community tool, not an official SCUM product.

## Release history, source and credits

See [CHANGELOG.md](CHANGELOG.md) for the complete v2.0.0 release notes and previous releases. The source checkout uses `Build.ps1 -Configuration Release` or `Build.ps1 -Configuration Test`, both through the canonical build scripts; published output directories are immutable.

Community map datasets, wildlife regions, bundled fonts and other third-party resources retain their existing credits and licenses. See [ATTRIBUTION.md](ATTRIBUTION.md) for full details. SCUM artwork, game definitions and trademarks belong to their respective owners.
