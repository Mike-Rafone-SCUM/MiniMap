# Current source release — v1.7.22

The published tree contains the current C# application, map/road and localisation resources, four supplied voice packs, packaging files and regression tests. Build instructions are in DEVELOPMENT.md. There are no historical executable folders, deployment scripts, Discord tooling, environment files, user databases, captured player reports, translation caches or generated previews in this tree.

Player-report regressions use fabricated names, account IDs, balances and coordinates. Existing image/geometry fixtures are retained because rendering and habitat regressions require them. Third-party attribution remains in ATTRIBUTION.md; publishing source does not change the ownership of third-party assets.

The source was rebuilt through Build.ps1, including shared regressions, rendering, packaged startup, administrator-manifest and archive/hash verification. Credential-pattern and private-path scans returned no matches. These checks do not certify that a program is vulnerability-free or guarantee antivirus clearance.

Runtime network requests are for public GitHub release manifests/packages and public Steam profile XML. Browser links open community/source/profile pages when selected. The updater checks official version-specific manifests, checksums, HTTPS redirects, archive entry names and size limits; elevated updater helpers have restricted permissions. Hashes from the release repository do not provide an independent publisher signature. The application remains unsigned and requests administrator rights for compatibility with elevated SCUM installations.

Optional screenshot-zone detection uses the packaged Python helper and the user's local NumPy/OpenCV installation; the application does not install those packages. The current requirements use version ranges and are not a certified or hash-locked dependency inventory.

The queued Num / delivery regression passes. A live SCUM retest remains necessary, and native-speaker wording review of the 19 text languages is ongoing.
