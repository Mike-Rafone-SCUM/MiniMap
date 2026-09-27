# SCUM MiniMap v1.4.100-test: input locking

Exit the current MiniMap, then download and run SkynettMiniMap-Test.exe. The executable identifies itself as version 1.4.100-test. Updates are disabled; settings use the separate ScumMiniMap-ResponsivenessTest data folder.

## Input changes

- MiniMap-owned shortcuts are swallowed by the keyboard hook while SCUM is the foreground window. Both key-down and key-up are swallowed, preventing M, Insert, Delete and other configured MiniMap shortcuts from also triggering SCUM actions.
- Chat controls and typed input pass through. Suppression is released after physical key-up and recovers if a key-up is missed. Injected copy input is ignored by physical shortcut suppression.
- Settings, Add waypoint and Search remain configurable from Settings → MiniMap shortcuts.
- Copy polling avoids mistaking its own injected key for a physical chat transition.

## Verification

The supplied report showed “Waiting for SCUM to be the foreground window.” Automatic coordinate copying remains gated on SCUM being the focused foreground application. Check while SCUM is active. If that message persists, send the bottom Settings status line and whether the player uses local SCUM or streaming.

Automated audit, input/voice, chat/focus, rendering, and packaged resource/hash checks passed for 1.4.100-test. Live SCUM behavior still needs player confirmation.