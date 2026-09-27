# SCUM MiniMap 1.4.99-test: shortcut input handling

Test build dated 27 September 2026. Exit the current MiniMap before running SkynettMiniMap-Test.exe. Automatic updates are disabled; this build uses the separate ScumMiniMap-ResponsivenessTest data folder.

MiniMap-owned shortcut keys are now swallowed by the low-level keyboard hook while SCUM is the focused application, on both key-down and key-up. This prevents M, Insert, Delete, and other app shortcuts from also firing their SCUM bindings. Chat controls and typed input pass through. The hook reconciles a missed key-up so the suppression cannot stick. Injected copy keys are not consumed by the physical shortcut handler.

Settings, Add waypoint and Search have configurable single-key bindings. Home, Insert and Delete remain the defaults. Open Settings through the taskbar and find MiniMap shortcuts.

The screenshot supplied for the reported tracking issue showed “Waiting for SCUM to be the foreground window.” This test preserves the focus guard: it does not copy coordinates when SCUM is not the foreground app. Please test while SCUM is the active window. If tracking remains paused, send the detailed Settings status with attempts, responses and copy errors, plus whether SCUM runs locally or through streaming.

Validation for this executable: 130 input/voice checks, chat/focus regressions, audit and rendering checks, and packaged resource/hash checks passed. Live SCUM input and automatic tracking still require player verification.