# Remote keyboard and key rebinding test

This TEST branch includes the v2.3.13 application with two pending fixes:

- Remote and accessibility keyboard input follows the normal shortcut handling path. MiniMap's own generated copy/admin input remains excluded. Existing focus, chat, inventory and modifier protections remain active.
- Settings > Shortcuts exposes SCUM map, chat and copy key capture. The key rebinding button opens this editor.

The local test package passed canonical build/package verification, 642 input/voice checks and 328 packaged UI checks. Actual Sunshine/Moonlight compatibility still needs confirmation on the affected player's PC.

## Download and test

Open this branch's **Build MiniMap TEST package** GitHub Actions run and download **MiniMap-TEST-package** after it succeeds. Unzip the downloaded artifact, then extract the complete `SCUM-MiniMap-Test.zip` inside it. Exit any running MiniMap and launch `SkynettMiniMap-Test.exe`, keeping its `assets` folder beside it.

Test builds use a separate responsiveness-test profile and disable automatic updates. Match the map, chat and copy keys to SCUM in **Settings > Shortcuts**. Check map opening and closing through Sunshine/Moonlight and an on-screen keyboard; check that typing the map key in chat does not toggle the map. Also verify the rebinding button opens the editor and a changed map key is retained after restarting the test app.

The TEST workflow validates and uploads a test ZIP. It does not publish a production release.
