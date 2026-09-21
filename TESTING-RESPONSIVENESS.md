# SCUM MiniMap input and voice navigation test

This is unpublished settings-shortcut test build 9, based on the public v1.4.8 release and destination-indicator test 8. Exit the current MiniMap and run `SkynettMiniMap-Test.exe`. Updates are disabled in this build.

Home now restores Settings from the taskbar and brings it to the foreground. Repeated presses keep Settings open. Use Escape, Done or the close button to minimize Settings and return to gameplay.

## Destination indicator and full-map tracking

- Select a destination: a glowing arrow on the minimap edge points toward the final endpoint, using your route colour. Circular maps also show a short glowing arc. The indicator works independently of voice navigation and disappears when the destination is cleared or reached.
- Check different bearings, zoom levels, circular/square shapes and status-bar positions. The arrow indicates the direct destination bearing, not the next road turn.
- Open the full map while travelling. Position and heading should continue updating even though SCUM shows its map cursor. Chat, other applications, held keys and active mouse interactions still retain their normal input protections.
- Test normal cursor-visible menus with the full map closed: automatic copying should remain paused there.

Settings, zones, logs and voice packs use `%LocalAppData%\ScumMiniMap-ResponsivenessTest`. The installed app and its normal data folder are unchanged.

The app now keeps its icon in the taskbar while minimized. Click it to open Settings; closing Settings minimizes the app while tracking continues. Use Exit from the tray menu to quit.

## Gameplay input checks

Ctrl+C remains supported but now uses at least 1 second between automatic copies. It still injects gameplay keys and cannot guarantee freedom from control conflicts. For the crouching test, bind Copy location inside SCUM to an unused single key (such as F8), then select that same key and Single key without modifier in the MiniMap key setup. The game binding must be changed first; this build does not change it for you. Single-key mode retains the 250 ms cadence.

All held shortcuts, including M, now recover from missed key-up events. Keyboard and mouse hooks renew every 30 seconds and on game focus gain without clearing chat protection. T/Enter/Esc also have physical-state polling to recover missed chat transitions.

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
