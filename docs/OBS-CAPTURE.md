# Capture the live MiniMap in OBS

Prepared v1.7.18 exposes the existing `SCUM Minimap Overlay` window to OBS Window Capture. Earlier builds marked it `WS_EX_TOOLWINDOW`; OBS's window enumeration explicitly rejects that flag. The new build clears it while retaining the hidden owner, no separate taskbar button, layered bitmap rendering, non-activating behaviour and click-through gameplay. v1.7.19 additionally keeps/restores the overlay when OBS has focus: earlier behaviour hid it after switching away from SCUM, preventing selection in OBS's visible-window list. This visibility exception does not authorize game input or coordinate capture while OBS has focus.

1. Extract the complete MiniMap ZIP and run its EXE with `assets` beside it. Exit the previous MiniMap first, or use the included `Update-MiniMap.cmd` to update that installation.
2. Close MiniMap's settings, make the actual map overlay visible, then switch to OBS. A minimized settings window is not the map. Use v1.7.19 or later so focus changes do not hide the map while configuring OBS.
3. In OBS, add **Window Capture** and name it **SCUM MiniMap Overlay**.
4. Select **[SkynettMiniMap.exe]: SCUM Minimap Overlay**.
5. Use **Windows 10 (1903 and up)** as Capture Method, **Window title must match** as match priority, and disable Capture Cursor and Capture Audio for this map source. Exact title matching prevents fallback to a settings or admin window when the map is hidden.
6. Place this source above the SCUM game capture. Resize/position it in the scene as needed. The live overlay must remain visible; capture is not an independent background map feed.

Game Capture targets hooked graphics applications and is not the appropriate source for this GDI layered overlay. Selecting `SKYNETT / SETTINGS` captures the settings window instead of the map.

Discovery is covered by actual HWND style regression checks, alongside layered rendering, non-activation and click-through tests. On 2026-10-12, the user's OBS 32.2.2 source targeting `SCUM Minimap Overlay` rendered black with Automatic. Switching explicitly to **Windows 10 (1903 and up)** made the map visible in the source preview and scene. Capture Cursor was disabled, Window Match Priority set to **Window title must match**, and the properties were saved. No recording or stream was started; fresh gameplay-coordinate updates and other Windows/OBS combinations were not separately verified. If another setup remains blank or has opaque corners, inspect the chosen method and preview before adding masking/filter workarounds.

References: [OBS Window Capture](https://obsproject.com/kb/window-capture-sources), [OBS window enumeration source](https://github.com/obsproject/obs-studio/blob/master/libobs/util/windows/window-helpers.c).
