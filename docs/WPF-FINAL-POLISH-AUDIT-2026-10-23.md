# WPF final polish and development handover audit

**Review date:** 23 October 2026. **Baseline:** released v2.3.23. **Status:** local audit and proposed work; application fixes have not been implemented by this review.

**Publication hold:** no GitHub push, tag, release, asset replacement, Discord update or notification is authorized by this audit. The user requires explicit confirmation before any GitHub push. The forthcoming major-release version is undecided. Track subsequent work in [the major-release worklog](WPF-MAJOR-RELEASE-WORKLOG.md).

## Assessment

The WPF application has a substantial tested core. The best final development pass is to make existing features easier to understand, harder to misuse and easier to recover, then leave clear maintenance instructions. A broad redesign or another feature expansion would consume the remaining time and create more acceptance work.

The most consequential findings are recoverable local data being overwritten after a preset load error, saved groups that cannot be removed locally, disruptive code rotation without confirmation, window sizes that do not accommodate smaller/high-DPI work areas, and help/error text that describes superseded behaviour. These findings can coexist with passing regressions: most concern failure paths and usability contracts that those checks do not currently assert.

There is no evidence from this audit of a universal failure of navigation, map input, encryption or the updater. There is also no basis to certify all live gameplay, hardware, accessibility or prolonged-use scenarios. This is a comprehensive review across the WPF user journeys below, not a line-by-line certification of every backend implementation.

## Scope and evidence

The scope is **`ScumMiniMap-Dev/WPF-Test-Version`**, including its WPF presentation, the controller behaviour exposed through it, release installation/update flows, and dependencies that affect continued WPF use. The original frontend is not the product being assessed. Native specialist dialogs invoked by WPF are included as handoffs; unrelated companion economy, dashboard and native-server features are excluded. No live services or public content were changed or contacted for acceptance testing.

Evidence used:

- Source review of `WpfFieldConsole.cs`, `WpfEngineAdapter.cs`, `SquadTools.cs`, `SquadState.cs`, `RelayGroups.cs`, relevant settings/zone/localization code, package/test scripts, and current/historical WPF documentation.
- Visual inspection of all ten English v2.3.23 showcase images: tactical map, player visibility, compact HUD, placement, setup, tracking, shortcuts, groups, admin controls and Routes. These are existing exact-release captures with synthetic data, not new live gameplay observations.
- Fresh exact-release UI, broader WPF, opacity/setup and package self-check runs, plus a focused isolated audit probe. See the verification section.
- The local release verification record, `deploy/v2.3.23-verification.json`, as **historical release evidence**, not a new GitHub or production health check.
- SHA-256 of the audited executable: `6f5ef5507c7bd3e51c89794d53cd2a880ad2c16e07911be045d4155ba9886804`, matching the recorded release identity. All 58 files directly under current `src/` match the retained v2.3.23 `src/` snapshot by SHA-256. `src/WpfFieldConsole.cs`: `e379e1e3e47931086516ea7075e7fea50c098fc0b73677b85798ea74011124b7`.

Evidence labels used below:

- **Reproduced:** demonstrated against the released executable in an isolated synthetic profile.
- **Confirmed in source:** control/state/error path exists in the inspected source; not necessarily reproduced through a live game.
- **Visual finding:** visible in inspected release captures; impact is a usability judgment.
- **Validation gap / recommendation:** further acceptance or a design decision is needed; not a confirmed runtime defect.

Priorities: **P1** = address first in the final release preparation; **P2** = valuable polish or recovery work; **P3** = defer unless measurements or remaining time justify it. These are planning priorities, not security severity ratings. Estimates below include implementation effort only; translations and final release validation add time.

## Prioritized findings

### F01 — Preserve a damaged preset file before permitting replacement

**P1 · Reproduced · approximately 1–3 hours plus tests**

`src/SquadTools.cs:131–136` catches `MapPresetFile.Load` failure, displays the generic connection error and continues with an empty editable preset list. Save remains enabled and writes to the same path. A user trying to recover can replace a potentially recoverable `map-presets.dat` with a new list without a recovery decision.

The isolated audit probe wrote deliberately invalid bytes, opened the actual release preset page, invoked its Save button and confirmed that the original bytes were replaced. No real profile was used.

**Action:** retain the original, present a preset-specific error and require an explicit recovery/reset choice before replacing unreadable data. Keep the ordinary atomic save path. Keep in-memory changes consistent with disk when a save fails.

**Acceptance:** damaged, unreadable and unsupported-format fixtures retain their bytes; merely opening/closing the page does not modify them; a successful explicit recovery preserves a backup; failed saves remain clearly unsaved.

### F02 — Add a safe local “Forget group” action

**P1 · Confirmed in source · approximately 2–4 hours plus migration/regression checks**

`src/RelayGroups.cs:99–106` caps saved groups at 16. `Leave` preserves the saved code, while the WPF group cards offer no local removal action (`src/WpfFieldConsole.cs:1655–1683`). The removal path in the store is owner deletion of the entire group. A non-owner can collect obsolete groups until new joins are rejected, without an in-app cleanup route.

**Action:** distinguish **Leave/disconnect and retain** from **Forget on this PC** and **Delete for everyone**. A local forget must not delete a relay group. Owned groups require a prominent ownership-loss warning; do not offer a casual destructive owner reset.

**Acceptance:** forgetting one joined group frees a slot, persists across restart and leaves other memberships unchanged; no relay-delete request is sent; failure to persist preserves the previous membership. Test owner warnings and pending operations separately. Do not suggest deleting the entire profile as a workaround.

### F03 — Confirm disruptive group-code refresh

**P1 · Confirmed in source · approximately 0.5–1 hour plus translated copy**

`src/WpfFieldConsole.cs:1675` invokes rotation immediately. Adjacent Delete has an explicit confirmation. Refresh invalidates the old code and disconnects members, so an ordinary utility-looking button performs a disruptive group action.

**Action:** confirm with the group name and consequences: old code stops working, members disconnect, and the new code must be shared. Default to Cancel. Consider wording such as **Replace invite code…** if it translates clearly.

**Acceptance:** Cancel performs no operation; confirm affects only the chosen group; failed rotation retains a meaningful retry state; clipboard failure does not conceal a successful rotation.

### F04 — Fit ordinary WPF windows to the available work area

**P1 · Source-confirmed sizing limitation; real-monitor acceptance pending · approximately 2–4 hours**

`BuildOnboarding` requires 960 × 620 WPF logical units and initially requests 1100 × 720 (`src/WpfFieldConsole.cs:2086`). Zone Creator initially requests 1450 × 920 with a minimum of 1100 × 740 (`:2024`). The shared window factory (`:1266`) centers windows without applying a work-area bound. At 125% scaling, setup's minimum height alone corresponds to 775 physical pixels, already exceeding a 768-pixel display before taskbar and window chrome. Zone Creator's minimum exceeds a 720-pixel display even at 100% scaling.

Main-map recovery already has dedicated monitor handling; this finding concerns setup and tools. A ScrollViewer inside an oversized window does not ensure its footer or title bar remains reachable.

**Action:** size and position dialogs against the selected monitor's work area in DIPs; use adaptive columns or scrolling where necessary. Keep all save/cancel controls reachable and preserve monitor-coordinate handling.

**Acceptance:** 1280 × 720 and 1366 × 768 at supported scaling; 1920 × 1080 at 150%/200%; taskbar at different edges; mixed-DPI monitor movement; disconnected monitor; setup and Zone Creator reachable using keyboard alone. These combinations need actual desktop checks after implementation.

### F05 — Replace obsolete and unrelated error messages

**P1 · Confirmed in source; preset example reproduced · approximately 2–4 hours including a bounded translation pass**

`Localization.cs:1126` tells users to check address, port and a TCP firewall rule. The current group-code screen exposes neither address nor port. `BuildRelayGroups` uses that text for invalid names, codes, storage failures and operation errors. Presets use the same connection error for local file failures (`SquadTools.cs:131,134`). This sends users toward the wrong remedy.

**Action:** use a small set of actionable categories: invalid code/name, saved-group limit, service unavailable/retrying, storage access failure, preset read/write failure, and unavailable Windows-protected credentials. Preserve technical detail privately in logs; show users the next safe action. Never advise disabling the firewall globally.

**Acceptance:** each representative failure reaches its intended message in all 20 app languages; no local preset/storage failure is described as a network failure; limit and retry states identify what the user can do.

### F06 — Establish one current WPF guide and label historical documents

**P1 · Confirmed documentation contradictions · approximately 1.5–3 hours**

The current README says users can adjust tactical terrain opacity and retain it across zoom changes (`README.md:54`), contradicting the tested automatic 0%/100% lock. It also describes five-minute disconnected cards (`:90`) without explaining persistent rosters and fixed amber/cyan route colours (`:96`) despite assigned member colours. Other documents describe direct Host/Join, sharing disabled after every restart, timed setup holds and outdated page counts.

**Action:** use README as the current user guide, DEVELOPMENT as the build/maintainer entry point, and dated review files as history. Add conspicuous historical notices rather than rewriting evidence of past tests. A detailed document disposition table appears below.

**Acceptance:** a new user following only current docs can install, finish five-page setup, connect/reconnect/leave, understand sharing, recover assets, and find help without meeting a retired control or false instruction. Current docs explicitly retain pending live/accessibility/native-language acceptance limits.

### F07 — Leave a concise operating and recovery handover

**P1 · Recommendation based on development stopping · approximately 2–3 hours plus an operator walkthrough**

Relay README contains substantial deployment detail and the workspace retains release evidence, but neither is a short end-to-end continuity checklist. WPF solo use and private sharing have different dependencies. The hosting account, certificate pin, durable registry, traffic ledger and companion state require deliberate preservation if development stops.

**Action:** prepare a private handover with a named operator/contact, service and account ownership, renewal/expiry checks, restart and restore steps, known-good release hashes, backup locations, certificate-pin replacement implications, budget cutoff behaviour and a shutdown/support plan. Reference secrets by storage location; do not put values in public docs. Preserve notifier opt-outs/history and disabled automatic reminders.

**Acceptance:** someone other than the author can identify the active release, read private health, diagnose loss of sharing, locate backups and perform an approved recovery without resetting the registry, traffic ledger or notification history. No operator has been assigned or live recovery exercised by this audit.

### F08 — Use status wording instead of an action as group state

**P2 · Source and visual finding · approximately 1 hour plus translations**

Disconnected cards display **Leave group** as their state because `Session == null` maps to `PrivateGroupLeave` (`WpfFieldConsole.cs:1662,1685`). The inspected release group capture shows **Leave group** immediately above **Connect**. Retrying connections also collapse into broad invalid/connecting states.

**Action:** explicitly show Disconnected, Connecting, Connected, Reconnecting or Action required. Distinguish saved auto-rejoin preference from current connectivity. Do not imply that a displayed Connect button has disabled background retry.

**Acceptance:** clean launch, manual Leave, network failure, host Disconnect and restart each show accurate state/actions, without changing their established reconnect policy.

### F09 — Acknowledge clipboard success and failure

**P2 · Confirmed in source · approximately 1 hour plus translated copy**

Create-and-copy and Copy code swallow clipboard exceptions (`WpfFieldConsole.cs:1646,1672`). The user receives no explicit copy result. For a newly created group, repeating Create after an apparent failure can produce needless groups.

**Action:** show **Code copied** only on success. On clipboard contention, preserve the created group and offer Retry copy with a useful message. Keep secret codes masked by default.

**Acceptance:** a locked clipboard produces a copy-specific error without another create/rotate action; retry copies the same valid code; no code enters logs or general diagnostic exports.

### F10 — Save “Your name” explicitly

**P2 · Reproduced · approximately 1 hour**

Editing the group dialog's **Your name**, then pressing Done, discards that isolated edit. `SetName` runs only as part of another operation (`WpfFieldConsole.cs:1642`); Done merely closes (`:1683`). The focused release probe confirmed that the store still contains its previous name after Done.

**Action:** provide an explicit Save name or commit a validated name on Done. Define and explain whether name changes affect existing sessions immediately or after reconnect.

**Acceptance:** an isolated name edit survives Done/reopen/restart; invalid or failed persistence retains the edit and a correction path; unrelated group operations do not have to be used to save a name.

### F11 — Make preset treatment of automatic zoom deliberate

**P2 · Reproduced · approximately 1–2 hours**

`WpfApplyPreset` unconditionally sets `autoZoom=false` (`SquadTools.cs:21`), but `MapPreset` does not store the automatic-zoom preference. The release probe confirmed that applying a preset switches an enabled automatic-zoom setting off.

**Action:** decide whether a view preset preserves auto-zoom or stores/restores it. Document the decision, version the format if needed, and preserve older presets. Avoid silently changing a preference the preset cannot faithfully represent.

**Acceptance:** preset application follows the documented choice, old presets remain readable, and unrelated sharing/keys remain unchanged.

### F12 — Keep settings and setup save failures actionable

**P2 · Confirmed control flow; fault-injected end-user acceptance pending · approximately 2–3 hours**

`SaveSettings` reports write/access failures through controller `note` (`MapWindow.Settings.cs:564`). `WpfSave` returns void (`WpfEngineAdapter.cs:250`). Settings Done closes, and setup completion closes after a void save (`:224`; `WpfFieldConsole.cs:2113`). The controller status can carry the error, but the closing dialog does not wait for durable success or offer a retry in place.

**Action:** expose save outcome at the dialog boundary; keep the user's values and show Retry/Close without saving where appropriate. Setup completion should distinguish an in-memory result from durable completion. Reuse the stronger Save location failure behaviour already present.

**Acceptance:** denied writes/full disk do not claim saved settings or completed setup; retry works; closing preserves the previous on-disk configuration. Verify the actual note presentation before changing general status behaviour.

### F13 — Explain invalid numeric input before dismissing Settings

**P2 · Confirmed in source · approximately 1–2 hours**

`ConvertSetting` marks invalid numeric input/ranges orange-red, retaining the old backing value (`WpfFieldConsole.cs:1853–1861`). There is no specific explanation of invalid precision or format, and ordinary Done can still close the dialog. Colour alone is a weak explanation, particularly for keyboard and accessibility users.

**Action:** show the expected range/precision in text and an accessible validation message. On Done, either block invalid fields and focus the first error or explicitly revert with a visible explanation. Use the selected locale consistently.

**Acceptance:** malformed, out-of-range and excess-decimal values behave predictably via Enter, Tab and Done; valid localized numbers persist; an error is understandable without colour.

### F14 — Align route wording, colour guidance and privacy expectations

**P2 · Source and visual finding · approximately 1–2 hours plus translations**

`RouteAutoHelp` says **Amber means solo; cyan means shared** (`Localization.cs:1052`), while current member-colour rendering and captures show differently coloured routes. **Solo route** can also imply privacy even though the help correctly states it is visible to connected groups. The detailed explanation is easy to miss among several small paragraphs.

**Action:** describe routes by owner and purpose; avoid a fixed colour promise that no longer applies. Place **Visible to connected groups** beside the Solo/Shared choice. Preserve current sharing behaviour unless a separate product decision changes it.

**Acceptance:** players can tell who owns a route and who receives it without inferring from colour. Current client, older-client and multiple-group explanations remain accurate.

### F15 — Reduce simultaneous tactical-map labels

**P2 · Visual finding; no measured gameplay defect · approximately 2–4 hours for a modest change**

The tactical-map captures contain overlapping POI, route-stage, player, saved-place and destination text around clustered activity. The compact view also shows clipped labels near its crop. Existing category switches, smart labels and player pills help, but require users to tune the view.

**Action:** prioritize active navigation and nearby player labels; shorten repeated owner/route prefixes; reveal secondary detail on hover or selection. Start with text priority/collision rules before introducing a new settings system. Keep route geometry and marker identities unchanged.

**Acceptance:** compare identical dense synthetic scenes before/after at several zooms; active stop/player remain identifiable; hidden detail remains accessible; performance does not regress. Get a brief gameplay read from a player if possible.

### F16 — Make persistent rosters manageable in the sidebar

**P2 · Source/visual design risk; large-roster measurement pending · approximately 2–4 hours**

Each player card has five visibility controls and a follow action. These precede layers/filters in the same scrolling sidebar (`WpfFieldConsole.cs:1378`). Persistent groups can remember far more members than their nine live connections. Multiple expanded groups/offline cards could bury frequent map controls.

**Action:** show online members first, collapse offline members by default, retain an offline count and allow expansion. Consider compact cards and group headings. Preserve each player's session-only visibility choices.

**Acceptance:** synthetic 9-online and 64-remembered rosters, plus multiple groups, remain navigable without long UI stalls; duplicate names remain distinguishable; offline location data stays hidden.

### F17 — Give ordinary dialogs consistent move/close affordances

**P2 · Confirmed in source · approximately 1–2 hours**

The shared factory uses `WindowStyle.None` (`WpfFieldConsole.cs:1267`). Some consumers restore native chrome or wire a drag header; Search and Private positions do not provide the same title-bar/move treatment. Groups visually starts with help prose rather than a visible title. Keyboard Escape works, but mouse users face inconsistent window conventions.

**Action:** use a shared accessible title/header with drag and Close for ordinary tools, or native chrome consistently. Retain the special non-activating, click-through rules for map/HUD windows.

**Acceptance:** Search, groups, placement and member tools can be moved/dismissed consistently; focus returns correctly; map and game input ownership tests continue passing.

### F18 — Finish accessible naming on newly added group fields

**P2 · Confirmed absence of explicit field associations; Narrator acceptance pending · approximately 1–2 hours**

Private groups creates ordinary visual labels followed by TextBox/PasswordBox controls without the explicit AutomationProperties names used elsewhere (`WpfFieldConsole.cs:1635–1637,1659`). Adjacency of a TextBlock does not establish a reliable programmatic label. Rename fields also need their group context.

**Action:** set localized accessible names/LabeledBy, including the group in repeated rename/copy/manage actions. Check status-change announcements and password semantics without exposing codes.

**Acceptance:** keyboard/Narrator users can distinguish name, invite code and group-name fields, as well as repeated group actions; secrets remain protected.

### F19 — Complete high-contrast, motion and non-colour acceptance

**P2 · Validation gap with source-based risks · time-box 1–2 hours before committing a redesign**

WPF theme templates hard-code colours (`WpfFieldConsole.cs:24–36`), and the overflowing route ticker animates continuously (`:39–61`). Focus outlines and named controls are already tested, but do not establish high-contrast, screen-reader or reduced-motion usability.

**Action:** test Windows high contrast, keyboard-only operation, Narrator and a reduced-animation preference. Fix concrete failures first; provide static/full route detail where ticker motion is unsuitable. Do not announce accessibility certification.

**Acceptance:** focus, selected states, validation and critical route ownership remain distinguishable without colour; all essential tasks can be completed or their remaining limitations are clearly documented.

### F20 — State which work survives closing the app

**P2 · Confirmed intended limitation · approximately 0.5–1 hour for copy; persistence is a separate feature**

Route stops and pings are session-local (`SquadState.cs:17–20`; README explains this), while presets, saved places and group memberships persist. The Routes page does not make that difference prominent. Users may invest time in a route before closing MiniMap.

**Action:** put **Route stops last for this app session** beside the route editor. If durable routes are desired, scope and validate them separately; do not squeeze an unreviewed migration into the final polish pass.

**Acceptance:** users can identify persistent versus temporary data from the relevant screen. This finding does not call for persisting short-lived danger pings.

### F21 — Provide a repeatable WPF release-validation entry point

**P2 · Confirmed workflow gap · approximately 1–2 hours**

`Test.ps1` delegates to `scripts/Invoke-MiniMapChecks.ps1`, which runs shared suites but does not itself run the exact-package WPF UI/performance/opacity suites. Other wrappers default to historical package folders. Strong release evidence currently depends on knowing the separate commands.

**Action:** document or add one explicit package-validation command that accepts an executable/package path and writes a fresh evidence directory. Run focus-sensitive desktop fixtures serially. Separate synthetic local checks from explicitly selected live network acceptance.

**Acceptance:** a maintainer can validate the intended package, see each exit status, preserve full logs and identify skipped/manual checks. No silent fallback to an older executable; no accidental production network tests.

### F22 — Present locked opacity as a status, not an editable-looking control

**P3 · Visual recommendation · approximately 0.5–1 hour**

The tactical toolbar retains a disabled slider and percentage for a policy-fixed value. This can look broken to someone following the old instructions.

**Action:** show a clear automatic-opacity status or retain the control with an explanatory tooltip. Keep the requested 0% at 1× / 100% above 1× policy and compact opacity independence.

**Acceptance:** users understand why the value changes and do not expect a manual override. Existing opacity regressions continue passing.

### F23 — Document specialist-tool handoffs and incomplete WPF parity

**P3 · Visual/documentation finding · approximately 1 hour for a useful support inventory**

WPF still invokes native admin/spawn/player tools (`WpfEngineAdapter.cs:276` and dispatch in `WpfFieldConsole.cs:1492`). The admin capture has visibly different density/chrome and truncates small actions. README also identifies features that require the original frontend. These are relevant WPF exits, not a reason to audit or redesign the old interface.

**Action:** publish a small WPF capability/known-limit table. Review native handoff focus/return, tooltips for shortened labels and confirmation of submitted commands. Preserve the explicit UNKNOWN/requested-state distinction; a sent game command is not proof of execution.

**Acceptance:** no WPF button leads to an unexplained unsupported flow; users can return to the map; specialist access requirements are clear. Defer a full native-tool rewrite.

### F24 — Measure large custom-zone lists before restructuring them

**P3 · Confirmed non-virtualized construction; performance impact unmeasured here · approximately 1 hour to measure**

Expanded custom-zone groups add one WPF checkbox per zone (`WpfFieldConsole.cs:1383–1388`). Prior idle/rendering stress results do not establish expansion performance for thousands of controls.

**Action:** measure expansion, scrolling and filter-toggle latency at representative limits. Page or virtualize only if the data supports it; a searchable/collapsed list may be enough.

**Acceptance:** record dataset size and UI latency/memory, with before/after evidence for any change. Do not report existing geometry-cache tests as proof of list responsiveness.

## Journey coverage and what should be retained

| Journey | Review result | Remaining acceptance |
|---|---|---|
| Install and update | Complete ZIP/assets, identity checks, shared post-update handling and exception rollback are documented; package self-check passed. | Interruption/power-loss recovery remains a documented limitation; no new updater download or GitHub verification in this audit. |
| First install/update setup | Five-page sequential flow, retained keys, key-capture path and focus release have regression coverage. Long first page and repeated setup add friction. | Small work area/DPI, target-player task completion and durable completion failure. **Keep mandatory five-page setup unless explicitly changed by the user.** |
| Tracking and input | Fresh tests exercised modal ownership, empty search, map dismissal, pause/focus and restore paths. | Walking/driving/flying, chat Escape, inventory, music conflicts and prolonged play on a real game. |
| Search and saved places | Accessible query/results, keyboard selection, empty search and save failure retention passed UI checks. | Search discoverability and repeated names with actual players; retain the distinction between temporary GPS, saved place and route stop. |
| Navigation and squad routes | Fresh UI suite exercised stop advancement, stop vs clear, remote-route updates and cross-group forwarding protection. | Dense scenes, route choices on actual terrain, expiry and route-label readability. Preserve dashed unverified legs and estimated-ETA language. |
| Private groups | Source reviewed for create/join/leave/rotate/delete/reconnect, storage and UI states; release probe exercised local name handling. | F02/F03/F05/F08–F10; no fresh public-relay integration or host disconnect test in this audit. |
| Filters, zones and presets | Fresh broader WPF suite passed layer/zone/marker paths; preset recovery/auto-zoom reproduced. | Large list expansion, complete drawing accessibility, file-failure recovery and small displays. |
| Minimap/placement | Existing captures and fresh suites cover shapes, assigned colours, placement and restoration behaviour. | Mixed-DPI transitions, display unplug, ultrawide placement and OBS on target hardware. |
| Localization/accessibility | Fresh UI suite exercised translated layouts and existing accessible names; source/captures reviewed. | Native-language approval, Narrator, high contrast, actual DPI and new-error copy. Automated coverage is not linguistic approval. |
| Exit/auto-launch | UI suite passed tray exit with admin window open. Existing target-account auto-launch instructions are usable. | Real sign-in/task lifecycle and moved-installation recovery; no scheduled task was registered by this audit. |

## Documentation review and disposition

| Document/surface | Finding | Proposed disposition |
|---|---|---|
| `README.md` | Opacity, route colours and offline-card explanations lag current behaviour; closing release-history sentence still points specifically to v2.0.0. | Correct as the current WPF guide. Add a short troubleshooting table and persistence/limits summary. |
| `WPF-TEST-README.md` | Mixes current streaming candidate with old v2.1.0, nine-page setup, direct Host/Join and disabled-on-restart claims; later sections name other launch targets. | Replace the active instructions with one current entry; move history behind an explicit dated heading. |
| `TESTING-RESPONSIVENESS.md` | Contains obsolete hold-to-advance, Host/Join, restart-sharing and Settings behaviour. | Split current acceptance steps from historical experiments. Check every launcher reference. |
| `Start-WPF-Test.cmd` | Points to a retained 21 October streaming test, while Release launcher points to v2.3.23. | Make the selected test candidate explicit; update only when a newly validated candidate exists. Do not claim the launcher tests the latest source. |
| `docs/PRIVATE-POSITIONS.md` | Good current persistent/reconnect/privacy detail, followed by older fixed route/marker-colour instructions. | Keep current group lifecycle; correct colour/wording and link specific recovery guidance. |
| `docs/LOCALISATION-COVERAGE.md` | Header is v1.7.21, 19 languages/625 strings and four audio packs. Finnish/current coverage is documented elsewhere. | Mark historical; provide a current index generated/checked against current catalogues. Do not guess revised string counts. |
| `docs/LOCALIZATION-REVIEW.md` | Explicit 2026-10-09 draft provenance and 13-language snapshot. | Retain provenance and pending approval; label it historical and link current coverage. |
| `docs/AUTO-LAUNCH.md` | Useful current lifecycle; its validation language count is old. | Correct count/validation scope; retain the explicit target-account sign-in check. |
| `docs/OBS-CAPTURE.md` | Describes the old GDI overlay and its old window title, with historical OBS proof. Current WPF compact window is titled `SCUM MiniMap HUD`. | Label old instructions as historical. Capture and verify a WPF-specific guide on the user's OBS installation before claiming support. |
| `docs/WPF-USER-EXPERIENCE-REVIEW.md` | Valuable dated fixes/evidence; mentions the former adjustable opacity and 19-language pass. | Retain as history, point readers to current behaviour. Do not re-open fixed issues solely because they appear in this file. |
| `docs/redesign/Production-Readiness-2026-10-14.md` | Initial updater gap was fixed in its follow-up. Death-age concern must be re-evaluated against current code. | Keep historical. Current `VisualIdentity` includes death DisplayName, so this audit does **not** reassert the old stationary-age defect. |
| `SETTINGS-AUDIT.md`, redesign/specification notes | Predate multiple releases and describe mixed/legacy feature surfaces. | Retain technical history, remove ambiguity about which statements are current WPF contracts. |
| `DEVELOPMENT.md`, `Build.ps1`, `Test.ps1` | Canonical build path exists; exact-package acceptance commands are scattered. | Add a compact maintained validation index and known-good baseline. Preserve immutable release outputs. |
| `relay/README.md` | Good current GROUP3/GROUP4/recovery appendices coexist with opening GROUP2 memory-only/no-registry claims. | Add a current architecture summary first; label older protocol sections and link the private operations runbook. |
| `CHANGELOG.md` and major-release notes | No product fixes from this audit have been implemented. | Do not list recommendations as delivered. Use the worklog to promote only implemented, validated changes into the eventual changelog. |

Do not delete old build/evidence folders just because they look untidy. First identify the active launch targets, retained rollback packages and unique verification evidence; inventory sizes; archive intentionally; verify the archive. `Clean.ps1` deliberately has a narrow generated-output allowlist. This audit performed no cleanup or profile reset.

## Suggested final two-day work plan

Treat this as a selection menu rather than a promise that every finding fits. Reserve roughly one third of the available time for validation, documentation and recovery preparation.

### Day 1 — prevent loss and avoidable mistakes

- [ ] Preserve damaged preset files and make the recovery decision explicit (F01).
- [ ] Add code-refresh confirmation (F03).
- [ ] Correct disconnected/copy/name feedback and obsolete error guidance (F05, F08–F10), sharing translations where possible.
- [ ] Implement safe local group forgetting only if there is time for ownership/persistence acceptance (F02). Otherwise document the limit clearly as an unresolved issue; never ship an unsafe substitute.
- [ ] Record every implemented change and its tests in the major-release worklog.

### Day 2 — make it usable and maintainable

- [ ] Fix work-area sizing or establish a precise, tested supported display baseline (F04).
- [ ] Correct the current guide and label historical docs (F06).
- [ ] Resolve the preset auto-zoom decision if it can be covered without destabilizing the package (F11).
- [ ] Run one focused real-game acceptance session and targeted accessibility checks.
- [ ] Complete the private continuity/backup walkthrough (F07).
- [ ] Build the final candidate into a fresh directory, rerun exact-package checks and compare targeted before/after captures.
- [ ] Prepare accurate major-release notes, known issues and rollback instructions, then present the local candidate for review. **Wait for explicit user confirmation before any GitHub push.**

Defer broad visual redesign, wholesale WinForms removal, route persistence, custom-map parity, new sharing/permission models and speculative performance rewrites. A short final pass benefits more from trustworthy existing workflows.

## Final acceptance checklist for the candidate

### Functional and recovery

- [ ] Fresh install and existing v2.3.23 profile both reach working WPF; settings/keys and required setup behaviour remain correct.
- [ ] Correct/wrong asset identities produce the expected safe startup result.
- [ ] Save failures for settings, saved places, presets and groups retain recoverable data and explain the next step.
- [ ] Group create/join/leave/forget/rotate/delete affect only the chosen membership/group and survive the intended restart behaviour.
- [ ] Wrong code, capacity, unavailable relay, host Disconnect and clipboard contention have specific useful feedback.
- [ ] GPS, saved place, route stop, follow teammate, Stop navigation and Clear route retain distinct behaviour.
- [ ] No received location/route is automatically forwarded into another group.
- [ ] Pings expire; stale/offline players have no live location; preset restore does not unexpectedly change unrelated preferences.
- [ ] Closing, tray Exit and auto-launch do not leave input protection or unwanted app instances behind.

### Usability and display

- [ ] Minimum supported work area and scaling, mixed-DPI movement, ultrawide and monitor disconnection.
- [ ] Every modal has reachable completion/cancellation, sensible focus and a move/close affordance.
- [ ] Keyboard-only main tasks; Narrator names/order; high contrast; visible text-based errors; motion/full route detail.
- [ ] English, a long-text language, Arabic, Simplified/Traditional Chinese and Finnish receive visual checks on changed screens; all 20 catalogues pass structural checks.
- [ ] Dense tactical map remains legible; large offline roster and custom-zone expansion remain responsive.
- [ ] Real-game walking/driving, chat/inventory, Alt-Tab, setup return, hidden HUD restoration and OBS capture if advertised.

### Release and continuity

- [ ] Fresh canonical Release candidate with matching assets/source identity and complete logs.
- [ ] Exact-candidate UI/WPF/setup suites; targeted new recovery tests; live-relay checks only when deliberately selected.
- [ ] Changelog includes only implemented work; screenshots match the candidate; open issues are explicit.
- [ ] Backups/rollback and private service ownership reviewed; no registry/ledger/notification history reset.
- [ ] No automatic DMs/reminders enabled; any later Discord work requires its appropriate authorization.
- [ ] User explicitly approves the GitHub push before it occurs; publication remains a distinct reviewed step.

## Verification performed during this audit

Local evidence root: `SCUM/deploy/wpf-final-audit-20261023/`.

| Check | Result | Evidence / limit |
|---|---|---|
| `Test-SquadUi.ps1` against v2.3.23 EXE | Passed, process exit 0 | `ui/` contains the compiled harness/isolated profile; output was inspected in the session, not redirected to a complete text log. Covers relevant WPF actions, translated layout, save failure, routes, focus and exit with synthetic fixtures. |
| `Test-WpfPerformance.ps1` against v2.3.23 package | Passed, **408 PASS assertions** | `wpf.log`, `wpf/`; includes shared/native-controller fixtures too. This is not 408 independent live-game scenarios. |
| `Test-OpacitySetup.ps1` | First run failed final foreground-equality assertion; serial retry passed **27 PASS assertions** | Both `opacity-setup.log` and `opacity-setup-retry.log` retained. First run overlapped the broader desktop suite; this is a plausible interference source, not a proven diagnosis. Keep desktop-focus fixtures serial and retain the failure. |
| `Test-PackagedMiniMap.ps1` | Passed, process exit 0 | `package.log`; validates packaged assets/theme/self-check without normal application launch. |
| `AuditProbe.cs` against v2.3.23 EXE | **Six observations reproduced** | `probe.log`: preset disables auto-zoom; local preset failure uses network error; Save replaces damaged preset file; isolated name edit does not reach store; Done discards it; setup minimum dimensions. All changes confined to `probe-profile/`. |
| Release identity/source comparison | Matches recorded baseline | EXE identity and all 58 top-level source files checked; `verification.json` records the results. This is not an exhaustive source-archive comparison or a fresh public asset download. |
| Ten English release showcase images | Visually inspected | `deploy/v2.3.23-showcase/en/01…10`; synthetic, previously generated actual-release captures. |

No new full canonical build, public GitHub query, public relay soak, live game session, real task registration, Narrator test or OBS capture was performed. Historical v2.3.23's 942 package checks, 49 pinned-TLS group checks and public-host evidence remain useful baseline records; they must not be relabelled as newly rerun by this audit.

## Decision record

This pass adds local audit documentation, a major-release worklog and private synthetic verification evidence. It changes no application source, version, launcher, saved real profile, live relay, Discord post or notification preference. No GitHub push was attempted. The findings above are proposals until implemented and entered as such in the worklog.
