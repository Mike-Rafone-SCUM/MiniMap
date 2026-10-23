# SCUM MiniMap source

Use Build.ps1 and Test.ps1. Both use the shared source/resource lists and checks.
VersionString in src/MiniMap.cs is authoritative. Update CHANGELOG.md before changing it.
Keep 20 language catalogues and format placeholders consistent. Bundled navigation voices are separate from text languages.
Keep coordinate-copy delivery tags distinct from admin-command tags. Validate chat/focus at delivery and allow cleanup key-ups.
Never publish secrets, user profiles, captured player reports, deployment tooling or generated release/check output.
Tests use fabricated reports and isolated temporary data. Preserve input ownership and updater verification checks.

Private memberships remember active groups across restarts. Preserve DPAPI legacy-profile compatibility, encrypted offline labels, fresh packet replay identities and stable member colours. Explicit Leave disables automatic rejoining; host Disconnect affects only the current connection. Keep group registry and traffic budget independent.

Registry mutations must await the serialized durable worker before acknowledgement. Fatal storage errors must exit as failure. GROUP4 compact frames are negotiated and must never reach legacy peers; preserve full-refresh and marker-removal behavior. Private health data must contain no player or group identifiers. Keep existing traffic and connection limits.
