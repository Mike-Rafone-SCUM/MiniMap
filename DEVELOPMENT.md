# Build the current source

This tree contains the current application source, required resource snapshots, packaging inputs and regression tests. It excludes old builds, credentials, operator tools and captured player data.

On Windows 10/11 with .NET Framework 4.x, use Windows PowerShell:

```powershell
.\Test.ps1
.\Build.ps1 -Configuration Release
```

The canonical build runs regressions, compiles all resources, verifies startup and creates the complete EXE/assets ZIP plus update manifest. No credentials or network access are needed to build. Outputs go beneath release/ and are ignored by Git.

Use a fresh -OutputDirectory to rebuild an existing version. Test builds use -Configuration Test and an isolated user profile. Do not remove map or voice resources: they are required by the application.

Optional map-image detection uses packaging/detect_zones.py and its NumPy/OpenCV requirements. Python dependencies are not needed for the C# application build. scripts/Build-MapTiles.py additionally needs Pillow only when regenerating the already supplied tile pack. Catalogue refresh scripts access the public SCUM Database API only when invoked explicitly.

Application text is supplied in 19 languages; native-speaker terminology review is ongoing. Four recorded navigation voice packs are supplied. See ATTRIBUTION.md for third-party data and recordings.
