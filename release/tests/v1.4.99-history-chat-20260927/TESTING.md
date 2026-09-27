# SCUM MiniMap 1.4.99-test: route history and chat input

Published test build: 27 September 2026.

Exit the running MiniMap, then download and run SkynettMiniMap-Test.exe from this folder. This test build disables automatic updates and uses the separate %LocalAppData%\ScumMiniMap-ResponsivenessTest profile.

## Changes to test

- Settings now includes Clear route history. Clearing removes the trail immediately; subsequent location samples start a fresh trail.
- History duration (minutes) is adjustable from 1 to 240, with a 30-minute default. Older points expire even while stationary. Shortening the duration removes older points immediately; increasing it retains future history longer and cannot restore expired points.
- History visibility, hover timestamps and duration are saved per user. Trail points remain session-only.
- Chat owns its shortcut keys before queued UI handling, including Tab channel changes, typed M and /, and Enter/Escape closure. The competing Escape map-close polling path has been removed.

## In-game checks

1. Travel to create a trail, clear it from Settings, then verify only new travel appears.
2. Set duration to 1 minute and remain stationary; old trail points should disappear. Increase the duration and restart the test app to confirm the preference is retained.
3. Open SCUM chat, type text containing M and /, repeatedly switch channels using Tab, and close using Escape or Enter. MiniMap should not open or close from those chat inputs. Verify M works normally after chat closes.

Automated regression, input/voice, rendering, and packaged executable/resource checks passed. The intermittent input issue still needs live SCUM confirmation.