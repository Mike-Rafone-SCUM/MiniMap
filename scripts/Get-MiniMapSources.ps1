$miniRoot = Split-Path $PSScriptRoot -Parent
Join-Path (Join-Path $miniRoot 'src') 'AdminCommands.cs'
Join-Path (Join-Path $miniRoot 'src') 'AppControls.cs'
Join-Path (Join-Path $miniRoot 'src') 'MapWindow.Controls.cs'
Join-Path (Join-Path $miniRoot 'src') 'DeathBanner.cs'
@('CopyInputLease.cs','VoiceNavigation.cs','MapWindow.Voice.cs','MapWindow.DestinationIndicator.cs','UpdateService.cs','Localization.cs','RoadRouter.cs','ScumMap.cs','PoiFilterDialog.cs','StartupGuideDialog.cs','CopyKeyReminderDialog.cs','MapTilePyramid.cs','MiniMap.cs','MapWindow.Diagnostics.cs','MapWindow.Settings.cs','MapCanvas.cs','Position.cs','Native.cs','Program.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path (Join-Path $miniRoot 'src') $_ }
