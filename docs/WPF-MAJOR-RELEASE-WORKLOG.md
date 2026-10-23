# WPF major-release preparation worklog

## Publication authorization and baseline

The user explicitly authorized publishing on 23 October 2026 with a full changelog, no pings and release DMs. Release v2.3.24 includes the implemented items below; deferred manual acceptance remains documented. Prior preparation and hold entries below are historical. Published assets remain immutable.

Baseline: WPF v2.3.23, source release commit `3f1689c70d89037ca2a66ac4ecc409f3ad9d60bf`. Active application: `ScumMiniMap-Dev/WPF-Test-Version`. Published artifacts remain immutable.

## Review and change ledger

| Date | Change | State | Validation |
|---|---|---|---|
| 2026-10-23 | [WPF final polish audit](WPF-FINAL-POLISH-AUDIT-2026-10-23.md): 24 prioritized findings, documentation disposition, two-day selection plan and acceptance checklist. | Local documentation complete. Product recommendations are not implemented. | Source review, ten existing release captures, fresh exact-release UI/WPF/setup/package checks and isolated focused probe; limits/failure retained in the report. |
| 2026-10-23 | This major-release preparation ledger and publication hold. | Local documentation complete. | Reviewed for separation of proposed and delivered work. |
| 2026-10-23 | Private synthetic audit probe and logs under `SCUM/deploy/wpf-final-audit-20261023/`. | Local evidence only; not a product feature or distribution asset. | Six observations reproduced against the v2.3.23 executable; real profiles/services untouched. |

For every subsequent implementation, add the finding ID, concrete before/after behaviour, affected files, persistence/compatibility implications, translations, exact-candidate tests and any unresolved limitations. Record failed checks and their resolution. Do not mark an item complete because its code was edited.

## Original proposed scope (historical)

- Recovery: damaged preset preservation; explicit settings/group save outcomes.
- Group housekeeping: safe local forgetting; confirmed code replacement; accurate states, copied-code feedback and display-name saving.
- Usability: work-area sizing; coherent errors; deliberate preset/automatic-zoom behaviour; clear route ownership/sharing guidance.
- Documentation: current WPF user/developer guides; clearly dated historical instructions; private operational continuity plan.
- Optional polish after priorities pass: label density, roster organization, window consistency, accessible group fields and targeted contrast/motion fixes.

Use the audit IDs to select actual work. These bullets are a proposal, not a changelog of delivered features.

## Local polish implementation — 23 October 2026

Candidate: `release/tests/audit-polish-final-20261023/SCUM-MiniMap-Test.zip`, using the canonical Test build with unchanged base version `2.3.23-test`. This is a local review package; no new major version has been selected and no published v2.3.23 asset was replaced. The original audit remains a dated record of the pre-change product.

Seven application source files changed: `Localization.cs`, `MapWindow.Settings.cs`, `RelayGroups.cs`, `SquadState.cs`, `SquadTools.cs`, `WpfEngineAdapter.cs`, `WpfFieldConsole.cs`. Formats remain compatible: preset format 2 and protected membership format 3 are retained; no profile migration or reset was added. New interface copy is authored in all 20 catalogues; native-language approval is still pending.

| Finding | Implemented behaviour | Acceptance / remaining limit |
|---|---|---|
| F01 | Unreadable presets disable ordinary Save. Explicit backup/reset copies the original to a unique adjacent recovery file before atomic reset. Failed save/delete restores the previous in-memory list. | Exact-candidate tests cover damaged and unsupported bytes, opening/closing without writes, byte-identical recovery backup and locked-file recovery failure. No full-disk hardware test. |
| F02 | Forget on this PC persists the list without the selected membership before removing/disconnecting it; never calls relay Delete. Owner warning states permanent loss of owner controls; Cancel is default. Pending operations cannot be forgotten. | Failed write preserves encrypted bytes and membership; successful removal survives reload and keeps the other membership. Actual owner-warning interaction and a fresh public-relay acceptance remain pending. |
| F03 | Code replacement names the group and explains invalidated old code, member disconnect and sharing the replacement. Confirmation defaults to Cancel. | Source reviewed; no production rotation or live disruption performed. |
| F04 | Ordinary dialogs initially fit their selected monitor's work area in DIPs; setup and Zone Creator have smaller minimums and narrower columns. Dedicated map/HUD placement stays on its existing path. | Current-monitor setup bounds checked; mixed-DPI, display removal and the full small-display matrix remain manual acceptance. Diagnostic fixtures retain intentional off-screen positioning. |
| F05 | Separate translated messages for invalid group input, local save failure, saved-group limit, service-operation failure, preset read/recovery and clipboard contention. | Local storage/numeric/preset failures exercised. Capacity/service categories inspected; specialized DPAPI-read recovery and every live service rejection still need acceptance. Existing direct Host/Join error copy remains for that older specialist flow. |
| F06 | README is the current guide: automatic opacity, assigned route colours, persistent offline membership, session data, local recovery and specialist limits. Contradictory review/OBS/localisation/test documents are marked historical. | Current docs reviewed. Old evidence retained; no new OBS support claim or guessed catalogue counts. |
| F07 | Private continuity checklist at `SCUM/deploy/wpf-polish-operations-handover.md`. Includes accounts to assign, state to preserve, pinned-TLS implications and approved recovery steps. | Draft only: named primary/backup operators, renewals, backup inventory and a second-operator recovery walkthrough remain outstanding. No remote service action. |
| F08 | Saved disconnected groups now say Disconnected rather than Leave group. Connected counts and Connecting remain; blocked/pending operations have retry guidance. | Exact UI checked. A distinct Reconnecting label and a fuller automatic-rejoin state presentation remain possible follow-up. Reconnect policy is unchanged. |
| F09 | Create/replace/copy reports copied only on clipboard success. Failure directs the user to retry Copy code on the existing saved group. | Success/failure branches inspected; a deliberately locked Windows clipboard still needs targeted acceptance. Codes remain masked and are not logged. |
| F10 | Save name saves without another group operation; Done also validates/saves an isolated edit. Failed persistence restores the old stored name and retains the edit for correction. Help explains that existing sessions use it after reconnecting. | Exact-candidate tests cover Done/reopen/reload and injected storage failure. |
| F11 | Presets preserve the current automatic-zoom preference; applying a stored zoom no longer silently disables it. | Exact tests cover enabled and disabled auto-zoom; no format change. Later tracking can adjust the preset zoom. |
| F12 | Settings/setup receive a save outcome. Their Done action stays open on failure; failed setup completion restores the in-memory required-setup marker. | Denied primary-settings write and required marker tested. Settings plus the separate `.version` file are not a single atomic transaction: a sidecar failure after primary replacement may leave primary settings saved. Power loss/full-disk and recovery of that secondary failure remain open. |
| F13 | Invalid numeric values show translated text, expected range/precision and programmatic error status. Settings Done/setup Next validate and focus the first invalid field. Correcting it clears the error. | Exact malformed/correction checks and existing range/increment regression passed. Full locale/precision keyboard matrix remains manual. |
| F14 | Route help states session lifetime, visibility to connected groups and owner colours; obsolete amber/cyan promises removed from the current guide/help. | Full route behaviour regressions retained; sharing semantics unchanged. |
| F15–F16 | Dense map-label and large-roster redesign deferred. | Need representative measurements/captures and player review before changing prioritisation or roster organization. |
| F17 | Group management now has native movable chrome and a Done footer outside its scrolling content. | Full modal focus/input regression retained. Other specialist-window consistency remains a follow-up. |
| F18 | Group name/code/new-name fields have localized programmatic names. Rename and repeated actions include their group context. | Exact structural UI checks pass; Narrator and live status announcements remain pending. |
| F19 | Route ticker respects Windows client-animation preference, stays static when disabled, and exposes full text as tooltip/programmatic name. | WPF test checks the actual system preference. Both branches are specified; the local system selected reduced motion. High contrast/Narrator acceptance and dynamic preference changes remain unverified. No accessibility certification. |
| F20 | Routes prominently explains session-local stops and group visibility. README contrasts persistent and temporary data. | Exact UI/source copy checked; no route persistence or ping migration introduced. |
| F21 | `scripts/Test-WpfCandidate.ps1` requires an explicit canonical executable and fresh evidence directory; runs package, polish, UI, WPF and setup suites serially with full logs, hashes and exit status. | No fallback to historical packages or production relay selection. Both-variant package ambiguity is rejected. Manual checks listed in verification JSON. |
| F22 | Opacity policy remains locked; toolbar help/programmatic detail contains the 1×/above-1× policy and the current guide explains it. | Existing opacity regressions retained. Visible discovery of help on a disabled slider needs further acceptance. |
| F23 | Current guide includes WPF/native capability and permissions table, preserving the submitted-command versus execution distinction. | Full existing native handoff regressions retained; no native-tool rewrite or live game-command acceptance. |
| F24 | Large expanded zone-list restructuring deferred. | List construction/scroll/filter measurements remain outstanding; geometry tests are not substituted for this evidence. |

### Evidence and corrected failures

Private evidence is under `SCUM/deploy/`. `wpf-polish-final-build.log` records the successful canonical build; `wpf-polish-accepted-candidate/verification.json` and its full logs record final exact-candidate acceptance. `wpf-polish-verification.json` records package hashes and seven changed application-source hashes; `wpf-polish-source-review/` contains a comparison with the release snapshot (including pre-existing audit documentation differences).

Final result: package self-check and all four desktop suites exit 0; **28 polish + 394 UI + 407 WPF + 27 opacity/setup = 856 PASS assertions**, plus canonical build/shared regressions. These are assertions in synthetic fixtures, not independent real-game scenarios. Final executable SHA-256: `34494fc1d88326991457a19385d2723cdb72a35246efaea56f0a26da38720b8f`. Final group captures in English, German, Arabic, Simplified/Traditional Chinese and Finnish and the setup capture were retained; English, Traditional Chinese and Finnish final layouts were visually reviewed, with Arabic/Finnish/setup also inspected in the earlier candidate. No linguistic certification is claimed.

`Start-WPF-Test.cmd` now selects this validated candidate. `Start-WPF-Release.cmd` remains unchanged. The repeatable package-validation command is documented in DEVELOPMENT; no live service test is selected by it.

Earlier failures are preserved: initial build caught an unnecessary change to pending Create recovery and it was reverted; first setup check exposed ordinary fitting of intentionally off-screen diagnostic windows and that path was corrected. New fixture errors were corrected without changing the application: unsupported preset format uses InvalidDataException, numeric validation requires a shown visual tree, and the old ticker test always demanded animation despite Windows reduced motion. Each failed evidence directory remains private.

There was no GitHub push/tag/release, Discord edit, notification, live relay deployment/test, real-profile reset, scheduled-task registration or production game-command acceptance. Mandatory five-page setup, settings/keys, notification history and public artifacts are preserved.

## Pixel positioning and shared member colour follow-up — 23 October 2026

User feedback confirmed the app works and requested finer map placement plus individually chosen group-member colours. The user explicitly selected **each person chooses their own shared colour**, rather than per-viewer local overrides.

- Added four one-physical-screen-pixel arrow controls to manual main-map coordinate fields, the main-map move/resize preview and compact-HUD positioning preview. Automatic-fit mode disables manual field nudges. Done commits; Escape cancels preview edits. Physical HWND movement preserves size, Z order and activation. The dedicated main-map preview now bypasses ordinary dialog work-area fitting so its selected physical bounds are retained.
- Added 16 distinct shared colour choices to Private groups, with readable contrast, programmatic names, hex labels and a non-colour check mark for selection. The existing protected colour-seed file is saved before reconnecting eligible active groups. Existing stable member tokens, keys, ownership and membership files are retained; manually left, host-disconnected and pending-operation groups are not reconnected by this action.
- The palette selects existing deterministic identity hues. Both current and older clients can render the selected colour using the unchanged identity/packet format, without extra metadata markers, a relay deployment or another protocol. Player arrows, routes and shared markers use the same existing owner-colour path. Choices apply to all code-based private groups on this Windows account. Duplicate choices are permitted; colours are not forcibly unique.
- Choosing a different colour changes the transport identity prefix and briefly reconnects active groups. A teammate following the old identity may need to select Follow teammate again. A map visual-identity fix ensures a stationary member's new colour invalidates rendering too.
- Three new interface strings are authored in all 20 languages, including Finnish. Current README and Test-package instructions explain the controls, persistence and reconnect boundary. No new release version, publication, Discord update, DM or live relay change is authorized.

Validation evidence for this follow-up is tracked separately under `SCUM/deploy/member-colours-nudges-*`; it does not replace the earlier audit evidence. Final build, exact-candidate results and launcher selection are recorded after acceptance below. Real-game/mixed-DPI hardware and independent language approval remain pending.

Accepted candidate: `release/tests/member-colours-nudges-final-20261023`, selected by Start-WPF-Test.cmd. Canonical build/shared checks and all six exact-package suites passed: package self-check plus 28 audit-polish, 18 member/pixel, 394 UI, 407 WPF and 27 opacity/setup assertions (**874 total**). EXE SHA-256 `34dc85fbc04a6dcd487edd64925d08910d2de96ef7b46a934b0ab23a1eb5408b`. Final Finnish palette, English manual-coordinate and compact-HUD layouts visually reviewed. All 16 palette hues plus one default identity match the actual released v2.3.21/v2.3.22/v2.3.23 MemberInk renderers in separate isolated processes. Evidence: `deploy/member-colours-nudges-verification.json`, `deploy/member-colours-nudges-acceptance/`, final build log and `member-palette-*.json`. This is rendering/loopback compatibility evidence, not a new public-relay soak.

## Main Settings audit and structure polish — 23 October 2026

User requested an audit followed by polish of the main Settings menu. [The focused audit](WPF-SETTINGS-STRUCTURE-AUDIT-2026-10-23.md) records the baseline descriptor inventory and screenshots, clutter findings and selected structure.

- Replaced three broad horizontal tabs with a named, keyboard-operable category list: General, Appearance, Tracking and voice, Shortcuts, Groups and routes, Tools and data. Existing General/Shortcuts/Appearance entry indices remain compatible with their callers.
- General now contains language, startup and one setup entry. Tracking/voice, groups/routes and admin/data/recovery each have a dedicated destination. Positioning and compact-HUD preview/display settings live in Appearance. Secondary map layers, markers/history and zone-tool sections collapse; expansion state survives category changes in the same window.
- Only Done remains in the footer. Open Data Folder moved to Tools, and both positioning entry points moved to Appearance. Redundant guide/rebinding launchers were replaced by the one guide entry and direct Shortcuts category. Every native descriptor is accounted for once or as an explicit direct guide/binding replacement in the focused checks.
- Invalid numeric input blocks category navigation, including Ctrl+Tab; a collapsed invalid section is expanded and its field focused. Category changes reset content scrolling. A final focus correction keeps Ctrl+Tab from overriding the invalid field's focus after rejected navigation.
- Eight category/section labels were authored in all 20 languages. The current guide and packaged Test instructions describe the new paths. Existing native control callbacks, ranges, live preview, save-failure handling, modal ownership, admin gates, Test update disabling, profile format and five-page setup policy are preserved.

First structured candidate: canonical build and all seven package/desktop suites passed, including 172 structure assertions, 395 UI, 407 WPF, 28 recovery, 18 colour/pixel and 27 setup/opacity assertions. Baseline and after captures were reviewed in English/Finnish and Arabic Tools; final candidate acceptance is recorded separately. The initial final build failed an existing native admin-player music-pause immediate-render assertion; its log is retained. No diagnosis is claimed from that single observation; the unchanged suite is rerun through the canonical build.

Real Narrator/high contrast, mixed-DPI hardware, target-game and independent language acceptance remain manual. No public publication or live service work is authorized.

Final acceptance: `release/tests/settings-structure-final-20261023` is now selected by Start-WPF-Test.cmd. Canonical Test build/shared checks and all seven exact-candidate suites pass, including **1,048 assertions**: 173 Settings structure, 395 UI, 407 WPF, 28 recovery, 18 member/pixel and 27 opacity/setup. Package integrity also passes. EXE SHA-256 `c0eb9fd41bafaa84d7b1ded4d793d5928a57083cc0a00cec109ecdfb60ae0a20`. Final English General/Tracking, Finnish General and Arabic Tools captures visually reviewed; additional German and Chinese variants are retained. Eight new labels pass all 20 catalogue checks. The unchanged admin-player suite passed on the canonical build retry; the earlier immediate-render failure remains documented without claiming a proven cause. Evidence: `SCUM/deploy/settings-structure-verification.json`, `settings-structure-final-acceptance/`, `settings-structure-final-build-retry.log` and original audit/probe directories. Production launcher/release, services, Discord and notifications remain unchanged.

## Release-note preparation

**Draft status: local candidate improvements are recorded above. A public version and translated release notes have not been prepared.**

When implementation is complete, write the notes around verified user outcomes. Include:

1. The actual major-release version and candidate identity.
2. Implemented improvements, grouped into recovery, group management and WPF usability as appropriate.
3. Settings/profile preservation and any migration; complete-ZIP installation; five-page setup policy unless separately changed.
4. Known limitations and the boundary between synthetic checks and real-game/hardware acceptance.
5. User-facing recovery/support instructions and the service-continuity position.

Only promote completed, validated changes from the ledger into `CHANGELOG.md` and translated release drafts. Derive language coverage from the current catalogues/registry when preparing the eventual update. Screenshots must come from the final candidate. Preserve notification preferences and history; this work does not authorize release DMs.

## Review gate

- [ ] Selected audit findings implemented and recorded individually.
- [ ] Exact final candidate built and validated into a fresh output directory.
- [ ] Final UI captures and documentation reviewed; known issues retained.
- [ ] Source/package/rollback records prepared locally.
- [ ] User reviews the concrete changes and explicitly confirms that GitHub pushing should proceed.
- [ ] Any separately requested publication/Discord steps use their own current instructions and readback checks.
