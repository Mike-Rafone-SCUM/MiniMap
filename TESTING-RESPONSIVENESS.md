# SCUM MiniMap input and voice navigation test

## Main map position and size (22 October 2026)

This candidate also tests smoother transparent-map dragging. Pending camera input now presents on the next Windows composition frame, with the previous 33 ms timer retained only as a fallback. Multiple input updates still coalesce into one pending frame; hiding the map cancels both presentation paths. Terrain/annotation snapshots and the existing locked zoom-dependent opacity remain unchanged. Local 4K measurements are synthetic and do not establish the frame rate on another PC.

For comparison, use **Start-Transparent-Tac-Test.cmd** at the same game resolution, map position/size, zoom and visible layers as the prior positioning candidate. Try dragging just above minimum zoom (for example 1.25x), both continuously and while briefly holding the pointer still. Compare the feel of the map with SCUM running. Check Escape/Alt-Tab and close/reopen behavior too. Keep using the normal transparent launcher for this comparison; the separate Fast Tac launcher changes the composition path.

Extract the whole test ZIP, close your other MiniMap instance and run **Start-Transparent-Tac-Test.cmd**. Open **Settings > Main map position and size**, or use the same button in the main map sidebar.

- **Fit to SCUM window** is the default. In SCUM's Windowed or Borderless Window mode, the main overlay fits the playable client area, excluding window borders. Move or resize the game and reopen the map; with the map visible it also follows changes automatically. If SCUM cannot be found, it falls back to the monitor.
- **Manual position and size** enables X, Y, width and height in physical screen pixels. Negative X/Y values support monitors to the left of or above the primary monitor. Minimum size is 640 × 420. Choose **Done** to save.
- **Move and resize preview** opens a separate placement preview. Drag its header and resize its bottom-right corner, then choose **Done**. Escape cancels that preview. The preview's Done saves immediately; the surrounding pixel fields then show the saved result.
- **Fill monitor** fills the fields with the current game monitor's bounds. Choose **Done** to save, or switch back to **Fit to SCUM window** to reset to automatic fitting.

Manual placement is retained after restart. If a display is disconnected, the map is fitted within a connected monitor. Compact minimap placement remains separate. This is a local test candidate with updates disabled; live SCUM alignment on the reporting 5120 × 1440 setup still needs player verification.

For the automatic-launch test, extract the complete ZIP to a folder you intend to keep, close other MiniMap instances and run **Start-Transparent-Tac-Test.cmd**. Enable **Settings > General > Start MiniMap with SCUM and close it when SCUM exits**. This defaults to off and installs a per-user Windows logon task for a small hidden watcher. Launch SCUM and check that only one MiniMap opens. Exit SCUM and check that MiniMap saves and closes after ten seconds. Launch SCUM again, then close MiniMap manually: it should remain closed until the next game session. Sign out/in to verify automatic startup of the watcher. Disable the option before moving or deleting this test folder; disabling removes the task, stops the watcher and leaves the current MiniMap open. Test and Release settings are separate. Automated checks use synthetic observations and do not verify a live game or register an elevated task.

The tactical map and compact HUD remain see-through. While dragging or zooming, the tactical map moves a cached static image and keeps navigation/player markers live. Newly exposed areas use the coarse overview; full terrain, marker detail and current-zoom labels return after the drag is released and camera input pauses for 200 ms. A held drag stays in preview even if the pointer briefly stops moving. Opacity and filter changes remain usable.

**Start-Fast-Tac-Test.cmd** retains the earlier solid-window comparison. Both launchers use the same test profile and settings; exit before switching. Fast Tac submits the retained terrain directly to WPF when a graphics rendering tier is available and software-only rendering is not forced; otherwise it uses the bounded raster worker. Its opacity slider fades terrain over a dark backdrop rather than revealing the game. Neither launcher changes the release installation or its settings.

Build the current source with `Build.ps1 -Configuration Test`. Exit the current MiniMap, run the resulting `SkynettMiniMap-Test.exe` and approve Windows' administrator prompt. The embedded Windows manifest requests elevation on every normal launch. Its product version follows the source version with a `-test` suffix. Updates are disabled in this build.

Keep the packaged `assets` folder beside the test EXE. If moving the build to another PC, extract the entire test ZIP rather than copying only the executable. Map tiles and road data are loaded locally and verified before normal startup.

Squad tools checks: connect two test clients to the same group. Assign optional Danger/Loot/Pick me up shortcuts, send pings and confirm that each disappears within 60 seconds of first receipt without repeat-key flooding. Right-click to add several route stops, then start the shared route from the other client and visit each stop in order. Follow a moving teammate, pause their capture, and verify that navigation clears until they send a fresh position. In a second group, confirm that following does not publish that teammate's location as your shared GPS marker. Save and reload differently named map presets, including after an app restart.

Full-map focus checks: open the map, Alt-Tab to another app, then return to SCUM. The full map should stay closed. Reopen it and dismiss it using Escape, Tab and the map key. Repeat while tracking is paused and after opening/closing Settings. These actions must release map mouse interception. Text entry and key-binding capture must still keep their own keyboard handling.

Automated state/transport regressions run in `tests/Test-SquadState.ps1`. Against a built package, run `tests/Test-SquadUi.ps1 -Executable <test-exe> -OutputDirectory <fresh-folder>` from Windows PowerShell STA. This exercises the actual WPF visibility, controller state, teammate navigation and preset application with synthetic loopback peers; it also renders the squad panel.

To test private groups, open Settings > General > Private positions. Click
**Create group and copy code**, then share that code privately with friends.
They paste it into **Group code** and click **Connect**. Names are prefilled and
can be edited. You can save and join multiple groups at once. **Leave group**
disconnects just that group and retains its code for later. Only its creator sees
**Refresh code**, which disconnects that group and invalidates the old code.
Saved codes and owner credentials are protected by this Windows account. Keep
the creator's saved profile to retain ownership. Sharing starts disabled after
each app restart. Close Settings to resume game position capture. The relay uses
no home port forwarding and cannot read encrypted positions or markers. It has a
50 GB monthly traffic cutoff and no paid fallback. Direct Host/Join remains available.

First-time setup describes the same group flow. Hold the forward button for one
second to advance, then release before advancing again. Short clicks, key repeats,
forward sidebar clicks and instant Skip cannot bypass the hold requirement.

Home now restores Settings from the taskbar and brings it to the foreground. Repeated presses keep Settings open. Use Escape, Done or the close button to minimize Settings and return to gameplay.

## Destination indicator and full-map tracking

- Hold movement or vehicle controls while opening the main map. The map must retain SCUM's foreground keyboard ownership, including after map clicks. Search/Settings text-entry dialogs may take focus; dismissing them should return to the game while keeping the map visible. Verify walking, driving and flying in SCUM; the automated checks cover window styles, mouse activation and retained foreground focus without sending gameplay input.
- With POI categories, the POI master switch and map zones disabled, use Delete location search. Hidden destinations should still appear and keep their exact coordinates without enabling those layers.
- Click a known town, POI or named zone with its icons/layers disabled. The destination should still use its name and retain the clicked coordinates; unrecognised terrain still uses the grid sector.
- With all POIs enabled and clustering disabled, scroll in and out repeatedly with labels on. Individual marker membership should be reused across zoom; identical appearances share frozen icon drawings in a bounded cache. `tests/Measure-PoiZoom.ps1 -AssemblyPath <exe> -OutputDirectory <fresh-folder> -RequireReuse` measures the same nine zoom steps using the bundled catalogue, without opening a window or sending gameplay input.

- Select a destination: a glowing arrow on the minimap edge points toward the final endpoint, using your route colour. Circular maps also show a short glowing arc. The indicator works independently of voice navigation and disappears when the destination is cleared or reached.
- Check different bearings, zoom levels, circular/square shapes and status-bar positions. The arrow indicates the direct destination bearing, not the next road turn.
- Open the full map while travelling. Position and heading should continue updating even though SCUM shows its map cursor. Chat, other applications, held keys and active mouse interactions still retain their normal input protections.
- Test normal cursor-visible menus with the full map closed: automatic copying should remain paused there.

Settings, zones, logs and voice packs use `%LocalAppData%\ScumMiniMap-ResponsivenessTest`. The installed app and its normal data folder are unchanged.

The app now keeps its icon in the taskbar while minimized. Click it to open Settings; closing Settings minimizes the app while tracking continues. Use Exit from the tray menu to quit.

## Gameplay input checks

Ctrl+C remains supported but now uses at least 1 second between automatic copies. It still injects gameplay keys and cannot guarantee freedom from control conflicts. For the crouching test, bind Copy location inside SCUM to an unused single key (such as F8), then select that same key and Single key without modifier in the MiniMap key setup. The game binding must be changed first; this build does not change it for you. Single-key mode retains the 250 ms cadence.

All held shortcuts, including M, recover from missed key-up events. Keyboard and mouse monitoring each run on their own message thread so map rendering cannot delay input delivery. The keyboard hook renews on its input thread every 30 seconds and on game focus gain without clearing chat protection. Physical key events invalidate coordinate-copy delivery immediately; owned-key release and UI work are queued to the UI thread. T/Enter/Esc also have physical-state polling to recover missed chat transitions.

For the keyboard fix, hold W while steering with A/D, then check Shift sprint, Space jump, releasing each key and Alt-Tab. Repeat with tracking paused and active. Check configured map/settings shortcuts and open chat: letters typed there must not trigger map actions or coordinate copying. Callback regressions exercise movement down/repeat/up events while the UI thread is blocked; they do not inject physical gameplay input.

For the mouse jitter fix, compare camera movement and aiming with the compact HUD visible and hidden, then open and close the tactical map and Settings. Check clicks and wheel input after returning to gameplay and after Alt-Tab. The regression suite checks that the mouse thread pumps while the UI thread is blocked; live SCUM testing is still required to confirm the symptom is resolved.

- Scroll normally during gameplay while position sampling is active. Physical wheel events cancel modifier-based shortcuts and pause their sampling for 400 ms; single-key copying continues through wheel events. Button-event protections remain active. The copy key is released before its modifier.
- Press T, leave chat idle for over 25 seconds, use Tab to switch channels, and type text containing M. Chat remains protected until Enter sends the message or Esc closes chat.
- Map opening now uses the physical keyboard hook only. The competing map-key polling path is removed. Polling of other shortcuts is suspended during chat.
- Test Alt-Tab, aiming, holding modifiers and the custom coordinate-copy binding. Sampling fails closed if either keyboard or mouse monitoring is unavailable.

These input changes have deterministic regression coverage, but require live SCUM testing to establish whether the reported crouching and chat behaviour is resolved. Synthetic coordinate-copy shortcuts still depend on the game's configured binding.

## Voice navigation

1. Open Settings, find **Voice navigation**, choose **Lyan (Female US)** and press **Preview voice**. It plays “In 100 metres, turn left.”
2. Turn **Enable voice navigation** on and choose a volume. It is off by default. Selection, volume and enabled state persist across restarts.
3. Select a destination with a road route and return to SCUM. Actual road-graph junctions supply left, right and U-turn instructions. Keep-left/right announcements are disabled because lane/fork-choice metadata is unavailable. Approaching a turn plays distance cues at approximately 500, 250, 100 and 50 metres. Timing accounts for movement speed and phrase duration; an urgent turn can interrupt a distance phrase. Straight sections can announce “Continue straight”. Arrival within the app's existing 30 metre radius plays “You have arrived”.
4. Disable voice navigation to stop both the current clip and queued clips. Changing destination, opening chat, leaving the game or stale coordinates also stops route speech. Preview works while guidance is disabled.

Distance phrases are sequenced MP3 clips, not text-to-speech. Audio opening, playback and closing now run on a dedicated background worker. Repeated navigation checks are skipped when position, route and playback state have not changed; voice-pack lookup is cached. No overlapping voice phrases are played. Routine route refreshes and single uncertain samples no longer interrupt a phrase after “In”. Repeated coordinate samples and routine route refreshes do not repeat the same turn cue. Large jumps can skip distance prompts; junction coverage depends on the road graph. Water transit suppresses road instructions. The route check includes the displayed road-entry and destination connectors. Recalculation requires at least three distinct positions outside a 35-metre corridor over at least half a second. The status appears only when the router accepts the request and clears on completion; failure is reported separately. Sustained backward travel of 10 metres against the route triggers the U-turn clip and a recalculation; camera rotation alone does not. This is a first implementation for in-game testing, not a verified road-navigation dataset.

## Adding another voice

Create a folder under `%LocalAppData%\ScumMiniMap-ResponsivenessTest\voice-navigation` with a descriptive voice name. Include all 12 filenames below, then reopen Settings. Incomplete packs are omitted from the selector. The production app will use its own `ScumMiniMap\voice-navigation` folder.

```
01_500_meters.mp3
02_250_meters.mp3
03_100_meters.mp3
04_50_meters.mp3
05_continue_straight.mp3
06_turn_left.mp3
07_turn_right.mp3
08_keep_left.mp3
09_keep_right.mp3
10_make_a_u_turn.mp3
11_in.mp3
12_you_have_arrived.mp3
```

An optional `13_recalculating.mp3` can be added to any pack for spoken recalculation. It is not included among the supplied clips, so recalculation is visual until that recording is provided.

The bundled voice is embedded in the executable and installed into the test data folder. Keep clip wording compatible with the sequence `11_in.mp3` + distance + action.

## Retained responsiveness changes

The cone applies received headings immediately, position smoothing catches up within 180 ms, the overlay timer runs at 16 ms, and a padded terrain cache reduces redraw work. The 250 ms single-key tracking setting is a request cadence; input protection and SCUM clipboard response time can delay updates.
## Test 7 sampling changes

Single-key requests time out after 350 ms from keypress start, rather than over one second; repeated misses back off for one second rather than five. Single-key presses are held for 60 ms to cover game frames, without an extra post-release delay. Clipboard coordinate reads do not retry on contention, restoration disables the default retry loop, and the original clipboard snapshot is cached until external content changes. Snapshot capture and diagnostic writes run off the UI thread.

The test records bounded timing summaries (no coordinates or clipboard content) in tracking-timing.log in the test data folder, to distinguish UI stalls, missing responses and input pauses if symptoms remain.
