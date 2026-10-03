# MiniMap development workflow

`ScumMiniMap-Dev` is the canonical local project. The sibling server, companion bot and native bridge are independent projects.

## Layout

- `src/`: application source. `MiniMap.cs` contains the authoritative `VersionString`.
- `resources/`: required map, road, water-mask, icon and voice assets.
- User map data lives in `maps/<map-id>/` beneath the active data folder: `map.png`, `zones.tsv` and `customwaypoints.tsv`. Successful imports are retained in separate `imports/<import-id>/` folders with the original `source` image/file, `zones.tsv`, and (for screenshot detection) `source-zones.tsv` and `detection.json`. Import IDs prevent repeated filenames or layer names from overwriting earlier imports. The main `zones.tsv` contains the edited annotations saved from the zone editor; import TSVs preserve the initial detection.
- `resources/map-tiles.bin`: generated 512-pixel tile pyramid used for the bundled map. Regenerate with `python scripts/Build-MapTiles.py resources/map.png resources/map-tiles.bin` after changing the map image (requires Pillow). Commit the resulting tile pack with the map; builds require the checked-in pack and do not need Pillow.
- `packaging/`: files shipped alongside the executable.
- `scripts/`: shared build, verification and explicit publication commands.
- `tests/`: regression tests and focused diagnostic tools.
- `release/github/vX.Y.Z/`: completed release packages and reviewed release notes. Published packages are immutable.
- `release/tests/`, `release/checks/`, `release/.staging/`: generated development output; removable with `Clean.ps1`.

## Test and build

Run commands from this directory in Windows PowerShell 5.1 or PowerShell 7. Scripts resolve paths relative to the project, so invoking them from another directory also works. Windows, .NET Framework 4.x and its x64 C# compiler are required. Checks run in fresh Windows PowerShell STA processes.

```powershell
.\Test.ps1
.\Build.ps1 -Configuration Test
.\Build.ps1 -Configuration Release
```

The build flow is: validate inputs and output path, run regressions, run rendering checks, stamp a temporary source copy, compile all resources, run packaged self-tests, create the package, then move the finished package to its output directory. Failure cleans the staging directory. Builds do not install, start the interactive app, publish, or change the source version.

Release output defaults to `release/github/vX.Y.Z`. An existing directory is rejected before tests run. To verify an already-published version without changing it:

```powershell
.\Build.ps1 -Configuration Release -OutputDirectory release/checks/rebuild
```

Test builds use `SkynettMiniMap-Test.exe`, a `X.Y.Z-test` product version, disabled updates and a separate `%LocalAppData%\ScumMiniMap-ResponsivenessTest` profile. Release and test builds share the same compiler/resource list, including voices and the water mask. `scripts/Build-GitHubRelease.ps1` and `scripts/Build-TestPackage.ps1` remain compatibility wrappers.

Localised UI text belongs in `src/Localization.cs`, including reminder and voice text. Add each key to all ten language catalogues and preserve its format arguments. `tests/Test-Localization.ps1` checks catalogue parity, formatting and literal source references; it runs through `Test.ps1` and both build configurations. Shortcut hints must use configured bindings rather than fixed Home/Insert/Delete labels. Bridge and water-avoidance checks also run in the shared check flow.

## Prepare and publish a release

1. Write the real change list in `CHANGELOG.md` under the next version.
2. Run `scripts/Set-ReleaseVersion.ps1 -Version X.Y.Z` to update the authoritative version and README heading. Assembly metadata is stamped during compilation.
3. Run `Build.ps1 -Configuration Release` and inspect the resulting EXE, ZIP, manifest and release notes.
4. When publication is authorized, run `scripts/Publish-GitHubRelease.ps1 -Version X.Y.Z`. It uploads only the three named assets and release notes, verifies the draft, publishes, then verifies the live updater download. It never pushes application source.
5. Prepare and review the requested announcement and download messages before an authorized Discord update. Preserve download URLs and the community footer, and verify the posted messages. Keep operator snapshots local.

Building does not authorize GitHub or Discord publication. CI validates/builds packages and does not publish automatically. Do not commit credentials, deployment tooling or operator records.

## Cleanup

```powershell
.\Clean.ps1 -WhatIf
.\Clean.ps1
```

Run cleanup after builds have finished. Only known generated test, check, staging and benchmark directories are removed. The current published release, source, resources and local application profile are retained. Do not store hand-written code in generated directories.
