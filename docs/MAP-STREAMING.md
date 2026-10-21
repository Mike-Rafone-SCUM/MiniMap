# Viewport streaming

The tactical map prepares POI graphics only for cells intersecting the viewport,
including a 24-DIP border for symbols crossing its edges. A spatial index keeps
lightweight POI records available for search, clustering, filtering and hover.
The compact map uses the same index to select visible POIs and labels.

`WpfMapStreaming.cs` owns the index, tile requests, bounded cache and route chunk
selection. `WpfFieldConsole.cs` composes terrain, history, annotations, shared
waypoints/routes and live guidance as separate retained visuals on both maps.
Player updates refresh the live visual. Changes to the camera, filters,
localization, saved objects or route plans invalidate their affected layers.
Local death-marker ages remain independently refreshable.

POI requests contain copied coordinates and frozen icon assets. One batch of at
most four cells runs at a time, using the existing shared worker semaphore.
After completion, the next request uses the latest viewport; panning does not
accumulate a queue of old viewports. Layout, zoom and density changes cancel the
previous generation, and stale completions cannot replace its successor.
Unloading or disposing the map cancels work and releases cached tiles.

The cache holds at most 48 cells and 16,777,216 raster pixels (64 MiB of pixel
storage). Raster density and cell size adapt to viewport size and DPI so a
complete visible viewport fits the budget. Distant cells are evicted by last use.
High zoom uses frozen vectors in visible cells instead of a world-sized bitmap.
Clustering/layout metadata remains resident; this change streams rendering
assets rather than removing searchable records or changing the sharing protocol.

Routes have cached bounds for consecutive groups of 128 segments. Only groups
intersecting the viewport are projected. Adjacent visible groups stay in one
figure, retaining joins, line width and verified/unverified styling. Personal
GPS retains its road path with other shared graphics; its player connector
updates separately as the player moves. Off-screen players and point markers
are rejected before creating their geometry; existing HUD edge guidance remains.

## Verification

Build with the canonical `Build.ps1 -Configuration Test -OutputDirectory <fresh
directory>`. Run `tests/Test-MapStreaming.ps1 -Executable <package executable>
-OutputDirectory <fresh checks directory>` for spatial selection, cold assets,
exact visible POI/route pixels, cache limits, warm reuse, layer invalidation,
filter cancellation and unloading checks. The test also checks dense square
viewports at 200% DPI against the raster budget.

Set `MINIMAP_STREAM_BENCHMARK=1` for the synthetic 16-player/1,024-waypoint
benchmark. It measures CPU time to record heading updates on a stationary camera,
with POIs enabled. It does not measure live SCUM frame rate or network latency.
Run UI suites sequentially: simultaneous fixtures can interfere with foreground
and native-window assertions.

Local candidate `release/checks/streamed-map-20261021-verified` passed the canonical
Test build/package checks and 842 exact-package checks: 28 streaming, 387 squad/UI,
387 broader WPF, 10 retained-layer, 21 camera-burst and 9 camera/opacity checks.
The synthetic group benchmark measured median tactical update recording at
1.837 ms versus 13.084 ms in v2.3.17, and HUD recording at 3.934 ms versus
7.529 ms. These local CPU measurements do not establish live game frame rates;
live group acceptance remains outstanding.
