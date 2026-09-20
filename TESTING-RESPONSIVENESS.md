# SCUM MiniMap responsiveness test 2

This is an unpublished test package based on v1.4.7. Run `SkynettMiniMap-Test.exe` after exiting the installed MiniMap from its tray menu. The two versions cannot run together because they would compete for keyboard and clipboard tracking.

The test uses `%LocalAppData%\ScumMiniMap-ResponsivenessTest` for its settings, zones and logs. It does not replace the installed executable or modify the normal MiniMap data folder. Update checks and installation of updates are disabled in this build. Close the test and start your usual executable to return to v1.4.7.

## Changes to try

- A padded terrain cache follows small camera movements without rebuilding terrain and POI every frame.
- The renderer crops the visible texture before scaling, reducing the cost of zoomed views.
- The cone applies newly received headings immediately, independently of position smoothing. Position still catches up within 180 ms. No movement or heading is predicted beyond the latest received sample.
- The test requests coordinates and heading every 250 ms by default, including while stationary. The old test's 1,000 ms default is upgraded once; other saved intervals are retained. Change the interval in Settings if needed.
- The UI timer requests frames every 16 ms instead of 33 ms. Actual frame rate depends on Windows and rendering load.

The cone can only show heading supplied by SCUM's clipboard command. The 250 ms interval is a request cadence, not a guaranteed response time; chat, aiming, input conflicts, focus changes and pending copy requests can delay it. All those protections remain active. More frequent sampling sends more coordinate-copy shortcuts: include any effect on game controls in your feedback, and use a slower interval if needed.

## Comparison checklist

1. Match size, zoom, layer filters and tracking interval when comparing against v1.4.7. Test both 240-pixel and larger minimap sizes.
2. Turn while standing still, then walk and drive. The cone should use each new heading immediately instead of rotating slowly toward it. Check whether the more frequent coordinate-copy shortcuts affect your controls.
3. Pan and zoom the full map. Check that roads, POI, waypoints and the player stay aligned, with no blank strips at the viewport edges.
4. Toggle layers, resize the minimap, Alt-Tab, aim and type in chat. Confirm that the existing input behaviour remains intact.
5. Report your map dimensions, display resolution, zoom, enabled layers and what felt faster or worse. Logs are in the separate test data folder.

The accompanying development benchmarks are synthetic CPU rendering measurements, not an in-game frame-rate guarantee. No GitHub release or Discord announcement is created by the test build script.

## Initial renderer benchmark (test 1)

180 moving frames per scenario, fixed 16x zoom, same map, same machine. These times cover bitmap composition, excluding Windows presentation and game coordinate acquisition.

| Minimap size | v1.4.7 median / p95 | Test median / p95 | Terrain rebuilds, original / test |
| --- | --- | --- | --- |
| 240 x 240 | 16.83 / 20.96 ms | 0.90 / 1.38 ms | 179 / 0 |
| 600 x 600 | 137.76 / 158.48 ms | 4.17 / 5.64 ms | 179 / 1 |

Occasional cache rebuilds still occur, especially during zooming, large jumps and filter changes. Heading-only rendering was broadly unchanged (around 1 ms at 240 pixels and 4 ms at 600 pixels). These figures do not establish live gameplay latency or frame rate.
