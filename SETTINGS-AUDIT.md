# Settings audit — 2026-09-28

Reviewed the controls created by `BuildSettingsPanel`, `AddShortcutSettings` and `BuildVoiceSettings`, their persistence in `LoadSettings`/`SaveSettings`, and their runtime consumers. Also checked the full-map sidebar and repeated the source reference audit.

## Removed

- **Show player elevation (Z)**: changing this flag only changed cache keys. `BuildLocationDescription` and the renderer never displayed elevation. Removed the control, field, cache bookkeeping, saved key and all ten unused translations. Existing settings files containing `ShowElevation` are still readable; unknown settings keys are ignored.
- The previous cleanup removed water-transit route/rendering remnants, test-only input wrappers, duplicated key helpers and translation arrays, redundant key-wizard state and the unused copy-key screenshot.

## Retained controls and their consumers

| Controls | Runtime purpose |
| --- | --- |
| Language, startup guide, SCUM key wizard, three MiniMap shortcuts | UI translation, onboarding and physical keyboard dispatch. The guide and key wizard serve different workflows. |
| Grid labels, borders and opacity | `DrawGrid` and the terrain cache. |
| Location history, hover time, duration and clear | Recording/pruning the trail, drawing it and displaying hover timestamps. Sidebar toggles now share the Settings translations. |
| Custom waypoints, saved zones, zone labels, label detail and size | Zone/waypoint drawing and visibility culling. |
| Route and player colours | Route lines, destination indicators and player/heading drawing. |
| SCUM map layers and POI filters | `ScumMapStore` visibility/category filters and map rendering. |
| Zone editor and clear custom annotations | Per-map zone/waypoint editing and persistence. |
| Width, height, opacity, status-bar position/visibility and edge fading | Overlay sizing, layout and compositing. |
| Heading, compass and zone chime | Direction indicators and zone-entry sound. |
| Voice enable, pack, volume and preview | Voice player and route navigator. |
| Auto-zoom and its minimum/maximum | Speed-dependent zoom targets. |
| Maximum zoom and zoom step | Manual zoom controls; these are distinct from auto-zoom limits. |
| Coordinate-copy interval | Sampling cadence, with the modifier-binding minimum enforced. |
| Restore bundled map | Removes an installed custom texture; still needed for custom-map users. |
| Updates, community link, data folder and Done | Updater, external community link, profile access and dismissing Settings. |

No other nonfunctional or obsolete Settings controls were found in this audit. Some options only apply when their parent feature is enabled; that does not make them unused.

Older settings conversions, virtual-key fallback and annotation migrations remain necessary for existing users. Diagnostic entry points, framework callbacks and documented build wrappers are intentional, not dead application code. Historical published releases remain unchanged. This is a source and regression audit, not a claim that every live SCUM interaction has been exercised.
