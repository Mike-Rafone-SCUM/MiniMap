# Main Settings structure audit — 23 October 2026

Baseline: accepted local `member-colours-nudges-final-20261023` Test package, following the user's request to audit and polish the main Settings menu. Scope is WPF Settings and its existing entry points, not a redesign of map rendering or the five-page setup policy.

Evidence: isolated runtime inventory of every native setting descriptor and baseline captures of General, Shortcuts and Appearance in `SCUM/deploy/settings-structure-audit/`. No real profile, live service or game command changed during this audit.

## Findings

1. General is a catch-all: setup, groups, Routes, presets, startup, language, admin, voice, tracking/zoom, update checks, Discord and map recovery. Its first viewport is dominated by unrelated full-width launch buttons.
2. The fixed footer contains Move minimap, main-map placement and Open Data Folder beside an oversized Done button. They consume about 130 logical pixels on every page, including Shortcuts, and obscure the page's task hierarchy.
3. Appearance combines compact-HUD dimensions, map visibility, labels, history, colours, zone imports, file associations and destructive zone cleanup. Useful choices and specialist actions have the same visual priority.
4. Shortcut editing already has meaningful SCUM versus MiniMap identities, but a rebinding-wizard button on General opens another Settings window with those same controls. Guide entry points also duplicate the same five-page WPF flow.
5. Numeric errors must survive category changes; moving a setting cannot become a way to dismiss invalid input silently. Native callbacks, exact setting ranges, live preview, disabled Test updates, modal ownership and admin permissions must remain intact.

## Selected polish

- Use a left category list with General, Map appearance, Tracking and voice, Shortcuts, Groups and routes, and Tools and data. Keep the existing internal entry indices for General/Shortcuts/Appearance so established callers still reach the correct page.
- Keep only Done in the footer. Put map placement and compact positioning with appearance; put Data Folder with tools.
- General contains language, startup and one setup entry. Groups contains group/member-colour and route entry points. Tracking contains voice and zoom/capture controls. Tools contains admin, maintenance and recovery.
- Partition Appearance into positioning/compact display, map layers, markers/history and zone tools, with secondary sections collapsible. Keep every existing native setting reachable once, with redundant guide/wizard buttons explicitly replaced by their direct category/guide entry points.
- Show the selected category, reset scrolling when switching categories, support keyboard category navigation and Ctrl+Tab, preserve in-window expansion choices, validate before leaving a category, and retain focus/save-failure behaviour.

## Acceptance

Inventory each descriptor's destination rather than relying only on screenshots. Test category isolation, every setting/entry point, validation during category changes, live preview, persistence, nested group/admin handoffs and translated layout. Compare the same initial tasks and captures before/after. Check English, a long-text language, Arabic, both Chinese variants and Finnish; all 20 catalogue structures must pass. Real Narrator, high contrast, mixed-DPI and game acceptance remain separate manual work. No GitHub push/publication is authorized by this audit or local polish.

## Implemented disposition

Implemented locally in `settings-structure-final-20261023`. The canonical build and seven exact-package suites passed, with 173 focused Settings structure assertions and 1,048 desktop assertions overall. Native settings are inventoried across all categories; only duplicate guide/wizard entry points are explicitly replaced by the direct guide and shortcut category. Category validation, collapsed errors, expansion memory, scroll reset, translated names, modal handoffs and live settings regressions passed. See the major-release worklog and private `deploy/settings-structure-verification.json` for final hashes, evidence and retained failed build. This closes the selected menu-structure work; manual hardware/accessibility/game/language acceptance is still pending.
