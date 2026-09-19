# MiniMap release announcements

- Public bot wording names the app **SCUM MiniMap**, made **for the Skynett community**. Skynett is the community, not the app brand. Preserve real executable filenames, archive filenames and existing download URLs when writing installation instructions.
- Public channel exception: `join-the-skynett-community-discord` (`1548673867902488671`) under INFORMATION is intentionally visible to @everyone alongside welcome-and-rules. Keep it read-only for non-administrators. Its community invitation is `https://discord.gg/SkynettGaming`; this differs from the MiniMap community invitation used in release announcements.

- Whenever the user authorizes pushing a MiniMap release announcement, update both the announcement and downloads channels as part of that same operation. Do not report completion until both have been read back and verified.
- Post as ScumBot in the ScumMinimap server (`1548445162210861117`), announcements channel `1548459996449345657`.
- Include the community invitation footer from the previous release, linking to `https://discord.gg/MYzcGaFDMn`. Preserve the user's requested announcement style and wording.
- Update the existing bot-authored downloads message `1548463975513989133` in channel `1548450028417187860` to the announced version. Preserve its download link and other content unless the release requires changes. Edit it rather than creating duplicate download posts.
- Read current messages before editing. If these IDs no longer resolve, discover their replacements before posting. Never expose bot credentials in output or commit them to files.

## Discord maintenance after Onboarding migration

- Native Discord Onboarding and Channels & Roles own language selection. Do not restore verification, reaction-role, or language-toggle panels. Public language channels are discoverable through Discord; private tickets and their archive retain explicit access restrictions.
- Maintain equivalent English and Argentine Spanish information, permissions, forum guides, and styling. Keep official information read-only and public discussions writable. Preserve member posts and ticket history.
- As verified on 2026-09-13, the old downloads message was replaced. Current English downloads header: `1548762984216789124` in `1548450028417187860`; Spanish: `1548763008421863435` in `1548694496110182400`. Current announcement posts: `1548778247628525659` in `1548459996449345657`, and `1548778251768434851` in `1548694492679249921`. Read and rediscover if needed; do not create duplicates.
- Future release publishing requires reviewed `release/github/vX.Y.Z/discord-en.md` and `discord-es.md` before publication. Both announcements and both downloads headers must be updated and read back. Use the community invitation above and suppress mass mentions during maintenance.
- The public Git repository contains documentation and release assets only. Keep source, deployment tooling, Discord snapshots, credentials, and operator notes outside public commits. Documentation-only updates do not require a MiniMap version bump.
- Building or packaging alone does not authorize a Discord post. These instructions apply when publishing an announcement is authorized; they do not create a scheduled monitor or authorize uploading a release to itch.io.
