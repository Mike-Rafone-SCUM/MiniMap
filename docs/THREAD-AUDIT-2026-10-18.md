# MiniMap threading and responsiveness audit — 18 October 2026

Scope: the canonical WPF client, its native controller, and their background services. This audit follows the user's reported tactical-map lag, group/menu delays and request to check thread ownership. It includes source inspection of task/thread/timer creation, dispatch, synchronous waits and locks; targeted fault injection; the canonical Test build; and actual executable UI tests. It is not a claim that every possible hardware, driver or live-game timing has been reproduced.

## Confirmed findings and changes

| Finding | Change |
| --- | --- |
| Group reads shared a lock with DPAPI and durable storage writes. The UI could wait for disk activity. | Separate serialized operations from short model locking. Snapshot protected data before writing. Coalesce incoming-name saves, expose errors and retry failed saves. |
| Group menus repeatedly read a membership's live Session while a worker could replace or clear it. | Publish immutable membership views, with a captured session and identity for actions. |
| Router renewal held a lock across network exchanges. Disconnect and lease discovery could wait behind it. | Track renewal completion separately. Perform renewal and ordered lease withdrawal outside UI-facing locks, keeping replacement-lease ownership checks. |
| Hook modifier/activity values crossed threads without explicit memory publication. | Volatile key-state publication and Interlocked UTC activity ticks. |
| Worker misuse could reach map snapshot caches or navigation state before failing. | Fail at the adapter entry point when the caller is not the owning UI thread. |
| Dialog completion and shutdown could leave late failures unobserved or queue work after disposal. | Observe failures on workers, post through a disposal-aware helper, verify dispatcher ownership during disposal, and observe pending zone-open failures without waiting. |
| Uncached terrain PNGs were read and decoded inside drawing. | A single bounded loader publishes frozen tile images. Draw the overview immediately, then add detail; shutdown does not wait for reads. |
| At high DPI, the visible map exceeded the previous 48-tile cache, causing continual eviction and re-decoding. The asynchronous prototype exposed this as repeated redraws. | Retain up to 128 decoded tiles, cap a view at 64 detail tiles using pyramid selection, and verify that repeated settled high-DPI draws enqueue no further reads. The request queue remains bounded to 64. |
| Fully transparent tactical terrain still entered drawing and tile loading. | Skip terrain commands and requests at 0% opacity, retaining the mouse hit surface and map overlays. Any zoom above 1× selects 100% terrain opacity and draws the overview immediately. |
| Dense map redraws repeated label selection and POI toggles rebuilt sidebar controls. | Retain label layouts; synchronize existing POI controls and counts. Preserve their focus and scroll state. |
| Some input actions entered the timer refresh throttle. | Click, wheel, pan and toolbar interaction updates bypass it. |
| The local MCI decoder reported "playing" indefinitely after reaching the end of an MP3, causing the audio worker's 20-second timeout. | Also recognize completion from playback position reaching the clip length, using an explicit millisecond time format. The local probe reproduced a 1,443 ms clip remaining "playing" at position 1,443 ms. |
| Settings opened/appended their temporary file repeatedly. | Build the same settings snapshot in memory and write it once before replacement. POI toggles use the existing 700 ms delayed save. |
| Edge masks reduced visibility even when users selected 100% opacity. | Remove edge fading, its option and persistence. Keep shape clipping and the opacity slider. Use readable 16-point defaults, migrating the former six-point label default once while preserving other saved label sizes. Zoomed tactical terrain remains 100% opaque with a visible overview while details load. |

## Ownership and reviewed paths

| Area | Ownership / publication |
| --- | --- |
| Windows, controls, map caches, menus, settings and navigation | Application UI dispatcher. Native controller and WPF share this thread. Worker render requests marshal before entering WPF. |
| Keyboard and mouse hooks | Dedicated message pumps. Hooks use published handles, focus and physical input state; they do not inspect WPF controls or perform route/disk/render work. Clipboard delivery uses an input-safe predicate. |
| GPS and multi-stop routes | Background searches; bounded/debounced squad requests. Road graph initialization publishes through a volatile loaded flag. A* scratch state uses searchLock. Revision and destination checks reject obsolete UI results. |
| Dense POIs and terrain | Frozen drawing stamps/images cross to the UI. POI generation checks discard obsolete work; a shared slot bounds construction. Tile cache mutation stays on the dispatcher and detailed reads belong to one worker. |
| Relay/direct/private-folder positions | Timers and network workers publish bounded snapshots. Socket IO remains outside state locks. Session disposal clears visibility immediately. Group operation ordering is separate from UI reads. |
| Clipboard/admin reports/database views | Clipboard capture uses a separate STA worker; asynchronous continuations do not execute inline on it. UI workflows await background parsing/database loading and apply results on the UI. Database connections are scoped and serialized by their stores. |
| Audio | Dedicated playback worker, generation cancellation and no UI join on shutdown. |
| Process checks, death-marker lookup, diagnostics and updates | Background work uses captured inputs, bounded busy flags or generation checks. UI application is dispatched; update checks/downloads are awaited rather than synchronously waited on the dispatcher. |
| Zone-folder polling/import | Background parsing produces replacement models; the UI consumes completed results. No wait on an incomplete poll from refresh. |

Lock order: credential mutation takes operations before gate. Reads take only gate. Save copies under gate and releases it before encryption/IO. Neither worker completion nor router lifecycle cleanup waits for the UI. Router lifecycle serialization queues work while holding its lock briefly, then releases it before awaiting completion on a worker.

## Verification

All new group/slow-storage/router fixtures use synthetic state and local networking. The persistent group protocol suite uses a temporary loopback TLS relay and a test certificate, not production Oracle rooms. Existing private reports and databases are not fixtures.

| Verification | Result |
| --- | --- |
| Canonical `Build.ps1 -Configuration Test` | Passed all required checks and package/hash/resource integrity validation. Includes 632 input/voice, 212 admin-player, 178 updater and 100 audit regressions, plus the other required suites. |
| Dedicated threading regressions | 20 passed: blocked protected writes, stable group views, coalescing/retry, router renewal/disposal ordering and replacement lease ownership. |
| Persistent groups against local pinned TLS relay | 33 passed: multi-group positions/markers/names, ownership, code rotation, deletion/retry, protected persistence, restart and legacy migration. |
| Actual executable squad/map UI | 104 passed, including menu lifetime, worker misuse, navigation, clearing, remote routes, POI completion and wheel-zoom visibility. |
| Broader actual executable WPF UI | 384 passed, including settings/setup, font migration, opaque edge/centre pixels, no fade option, bounded high-DPI loading, blocked tile IO/disposal and tile-boundary alignment. |

Measured on the final local executable, with the bundled map and synthetic profile:

- Dense-map drawing: **8.7 ms average** over 12 redraws. The initial audit fixture reproduced 162.4 ms in the previous implementation.
- POI toggle: **13 ms** to record its first updated frame; **103 ms** through publication of the completed retained scene. A separate sidebar interaction completed in 4 ms and preserved its existing controls.
- Bitmap export: 280.1 ms for the full scene versus 276.1 ms for the same surface blank, giving **4.0 ms incremental scene cost**. The suite independently measures blank readback overhead rather than attributing it to application drawing; it also retains separate drawing and interaction bounds.
- Detailed-zone cache: 5 ms for 1,000 snapshots of 300 synthetic zones with 512 vertices each; 10 ms for 100 idle refreshes.

The final package is `SCUM/deploy/thread-audit-final-package/SCUM-MiniMap-Test.zip`; the verification receipt is `SCUM/deploy/thread-audit-verification.json`. It is an unreleased 2.3.6-test package, not a replacement for the published release. The receipt records hashes and logs. Fullscreen UI fixtures explicitly move their own windows off-screen, wait for layout before checking menu anchors, and retain bounds-aware placement assertions.

## Practical limits

- Explicit settings commits and startup loading still perform local IO. The settings writer now uses one temporary-file write; POI clicks defer persistence. This audit does not promise that a failing drive cannot delay an explicit durability operation.
- A third-party clipboard provider or audio driver can stall its own worker. Clipboard capture remains bounded by the existing in-progress guard; it is not forcibly aborted inside an external provider.
- Timings are measured on this machine using the bundled map and synthetic profiles. Live SCUM focus/GPU contention still requires gameplay verification.
- The audit first validated a local 2.3.6-test package. The user subsequently requested release as v2.3.7; its release build, source archive, actual executable UI checks and updater verification are recorded separately in the operator release receipts. Published v2.3.6 assets remain unchanged.
