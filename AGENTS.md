# MiniMap release announcements

## Canonical workspace and build flow (2026-09-23)

- `ScumMiniMap-Dev` is the active MiniMap checkout. The old `ScumMiniMap` checkout was archived outside the SCUM workspace. See `DEVELOPMENT.md` for commands and layout.
- Use `Build.ps1 -Configuration Release` or `Build.ps1 -Configuration Test`. Both delegate to `scripts/Invoke-MiniMapBuild.ps1`; do not add alternative compiler/resource lists. `Test.ps1` runs the shared regressions.
- `src/MiniMap.cs` `VersionString` is authoritative. Write the changelog first, then use `scripts/Set-ReleaseVersion.ps1`. Build scripts stamp temporary source copies and must not invent release notes, install the app, or publish.
- Published artifacts under `release/github/vX.Y.Z` are immutable. Use a fresh output path for rebuild verification. `Clean.ps1` removes only known generated development outputs.
- The current SCUMMinimap server (`1548445162210861117`) has one English official-information layout. MAIN contains welcome (`1548450023224905851`), announcements (`1550649057582645328`), downloads (`1550649063911985242`), FAQ (`1550649067615551508`), other servers (`1552747796015353927`), other projects (`1552742868689621193`) and donations (`1552789456946470978`). COMMUNITY contains the existing English general chat (`1550649076733972592`), ten localized general chats, and two voice channels. SUPPORT contains make-a-ticket (`1550649070555758673`); closed tickets live in TICKET ARCHIVES. There are no separate Spanish announcement or download channels.
- Current bot-authored release posts: announcements message `1552987547167105095`; downloads message `1550649098573582466`. Read and verify bot authorship before editing. Preserve the translation buttons on both posts: they use Google Cloud Translation to provide a private translation on demand, including Brazilian and European Portuguese.

- Public bot wording names the app **SCUM MiniMap**, made **for the Skynett community**. Skynett is the community, not the app brand. Preserve real executable filenames, archive filenames and existing download URLs when writing installation instructions.
- Public channel exception: `join-the-skynett-community-discord` (`1548673867902488671`) under INFORMATION is intentionally visible to @everyone alongside welcome-and-rules. Keep it read-only for non-administrators. Its community invitation is `https://discord.gg/SkynettGaming`; this differs from the MiniMap community invitation used in release announcements.

- Whenever the user authorizes pushing a MiniMap release announcement, update both the announcement and downloads posts as part of that same operation. Do not report completion until both have been read back and verified.
- Post as the existing bot in the SCUMMinimap server (`1548445162210861117`).
- Include the community invitation footer from the previous release, linking to `https://discord.gg/MYzcGaFDMn`. Preserve the user's requested announcement style and wording.
- Update the existing bot-authored downloads message in channel `1550649063911985242` to the announced version. Preserve its download links and translation buttons. Edit existing posts rather than creating duplicates. If the saved IDs no longer resolve, discover their replacements before posting. Never expose bot credentials in output or commit them to files.

## Discord maintenance after English-only layout migration

- Keep the official English information read-only and general chat writable. Preserve member posts and ticket history. Do not recreate old language categories or channels; the bot's buttons translate English posts privately with Google Cloud Translation.
- Future release publishing requires reviewed `release/github/vX.Y.Z/discord-en.md` and `discord-download-en.md` before publication. Update the English announcement and downloads posts and read both back. Preserve their translation buttons, use the community invitation above and suppress mass mentions during maintenance.
- The public Git repository contains documentation and release assets only. Keep source, deployment tooling, Discord snapshots, credentials, and operator notes outside public commits. Documentation-only updates do not require a MiniMap version bump.
- Building or packaging alone does not authorize a Discord post. These instructions apply when publishing an announcement is authorized; they do not create a scheduled monitor or authorize uploading a release to itch.io.
