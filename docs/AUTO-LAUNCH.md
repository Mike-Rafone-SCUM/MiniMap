# Automatic launch

Enable **Settings > General > Start MiniMap with SCUM and close it when SCUM exits**. The default is off. Extract the full application ZIP into a folder you intend to retain and launch `SkynettMiniMap.exe`.

Enabling registers a per-user Windows logon task, running a hidden PowerShell watcher with the highest rights available to the current account. It starts immediately, checks the same Windows session every two seconds, and uses the existing MiniMap single-instance mutex. The watcher holds no MiniMap executable open, so app updates can replace it normally. It does not read game memory or send input.

MiniMap saves and exits normally after SCUM has been absent for ten seconds. Unknown process observations reset exit confirmation. A short game restart cancels shutdown; manually opening MiniMap before SCUM keeps it open until a game session is observed. A manual MiniMap exit is respected until the watcher observes a complete game exit and a later launch.

Disabling deletes the enabled marker and removes the scheduled task. The watcher exits on its next poll and the current MiniMap remains open. Disable before moving/deleting the application folder; enable again from the new location. Watcher configuration and errors live in the active profile's `auto-launch` folder. Test and Release profiles/tasks are separate.

## Validation

- `tests/Test-ScumAutoLaunch.ps1`: 17 session lifetime/task XML checks; nine watcher checks, including the actual hidden worker's enabled/disabled lifetime with an explicitly absent synthetic game. No scheduled tasks or SCUM/MiniMap sessions are started.
- `tests/Test-Localization.ps1`: option and help text cover all 19 application languages.
- Canonical Test build includes the new policy suite, existing regressions and package integrity checks.
- `tests/Test-SquadUi.ps1`: inspect the actual packaged setting, accessible label, wrapped text and disabled diagnostic controls; retain the existing UI suite.

Manual acceptance remains necessary on the target Windows account: enable the setting, launch SCUM, check one MiniMap opens, exit SCUM, check MiniMap saves and exits, relaunch SCUM, then verify manual MiniMap exit and disable. Finally sign out/in to verify the logon task. Automated checks do not register a real elevated task or exercise a live game.

Included in v2.3.16 after the user's successful Test-build confirmation. Windows sign-in remains a target-account acceptance check; automated tests do not establish that it was exercised.
