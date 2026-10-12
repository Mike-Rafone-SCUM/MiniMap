# External installation assets and package updates

SCUM MiniMap remains a Windows EXE. Starting with the prepared v1.7.15 build, its official installation includes `assets/map-tiles.bin` and `assets/roads.bin`. Neither large binary is embedded in the application. A small embedded `installation-assets.txt` pins both SHA-256 hashes to that exact executable. Assets are loaded relative to the assembly location, never the current working directory, and remain open with write/delete sharing denied while used.

The canonical build copies those assets into both Release and Test ZIPs. Developer regression hosts without the small identity resource can still use their explicitly embedded test fixtures. A packaged build never falls back to alternate working-directory assets after validation fails.

## Update format

The public `update.txt` uses schema 2 with exactly `schema`, `version`, `sha256` (EXE) and `package-sha256` (ZIP). Old schema-1 clients reject schema 2 without installing anything. The new client continues reading schema 1 for legacy test and release compatibility, and uses the ZIP endpoint for schema 2.

Starting with the prepared v1.7.16 build, the downloader rechecks the official version-specific manifest before trusting identities from the local notification cache. Every HTTP redirect is validated before connecting against approved HTTPS GitHub release hosts. The internal package manifest is bounded by actual decompressed bytes, independently of ZIP metadata. Elevated updater-helper directories use a protected Administrators/SYSTEM ACL. See `SECURITY-REVIEW-2026-10-11.md` for test evidence and trust boundaries.

The ZIP contains `package.txt`: `version`, `SkynettMiniMap.exe`, `assets/map-tiles.bin` and `assets/roads.bin`, with each file's SHA-256. It does not contain `update.txt`: the ZIP digest is computed after packaging, avoiding self-reference. Extraction accepts only known distribution entries, rejects duplicates and traversal paths, limits per-entry and total expanded sizes, and verifies all required file checksums. No extracted scripts are executed by the updater.

The updater stages assets beside its uniquely named replacement executable. The existing helper request additionally pins their combined digest. Before closing MiniMap, the helper checks executable, installed-file and staged-asset identities. It replaces only the two managed asset files, preserving unrelated files in the assets directory. Successful replacement keeps backup files until executable launch succeeds. Replacement/launch exceptions restore old assets and the old executable. This is exception rollback, not a guarantee of atomic recovery after sudden power loss or process termination; leftover `.previous` files must be preserved for recovery.

## First upgrade

Extract the complete ZIP and run `Update-MiniMap.cmd`. The new EXE identifies the old installation and copies verified assets into staging before invoking its helper. The ZIP bootstrap can validate local `package.txt` and the executable's embedded asset identities without downloading maps at startup. AppData profiles are not modified by package installation. Running the EXE without its assets produces an installation error instead of downloading files silently.

## Validation and publication

Use the existing `Build.ps1` flow with a fresh output directory. Packaging validates hashes, native UAC metadata, resource inclusion/exclusion, ZIP entries, and the packaged asset loader. Updater fixtures cover checksum rejection, traversal/unknown entries, extraction cleanup, rollback and unrelated-file preservation. Published releases remain immutable.

The first package release needs reviewed Discord/download wording making the ZIP requirement explicit. Do not reuse standalone-EXE installation wording or publish automatically. Removing compressed assets from the PE addresses the exact resource-entropy finding; it does not confirm that vendor detections have been cleared.
