# SCUM MiniMap 1.4.99-test: configurable shortcuts

Test build dated 27 September 2026. Exit the existing app before running SkynettMiniMap-Test.exe. Uses the separate ScumMiniMap-ResponsivenessTest profile and disables automatic updates.

## Changes

- Settings, Add waypoint and Search now support user-captured single-key bindings. Home, Insert and Delete remain the defaults.
- Open Settings through the taskbar, locate MiniMap shortcuts (single keys), click an action, and press the desired key. Escape cancels. Conflicting keys are rejected. Keep Num Lock in the same state for number-pad shortcuts.
- Typing just after switching away from SCUM no longer opens a phantom chat gate using a stale foreground sample.
- Automatic-copy polling ignores its own injected key while copying. Real physical chat input remains monitored.
- UI shortcuts no longer require the mouse hook. Automatic copying still requires working keyboard and mouse monitoring.
- Retains configurable route history duration (1-240 minutes) and Clear route history.

## Player verification

1. Capture unused function keys for the three actions and test them in SCUM with chat closed. Add waypoint requires a received position. Restart MiniMap and verify the bindings remain saved.
2. Open chat, type M and /, switch channels with Tab, then close using Enter or Escape. Shortcuts must not interrupt chat editing; normal behaviour should resume afterwards.
3. Alt-Tab to another app, type text containing T or /, return to SCUM and check automatic tracking and shortcuts.
4. If manual backslash updates position but automatic tracking does not, report whether SCUM runs locally or through a streaming service. Include the detailed Settings status (copy attempts/responses and error), not only the map's Waiting for position placeholder. Automatic tracking on the affected player's machine remains unconfirmed.

Validation: 130 input/voice checks, chat/focus regressions, audit and rendering checks, packaged executable resources and hashes passed. An initial history-hover rendering check failed once and passed on the diagnostic rerun.