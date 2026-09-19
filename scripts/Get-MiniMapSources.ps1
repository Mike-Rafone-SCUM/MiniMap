$miniRoot = Split-Path $PSScriptRoot -Parent
@('UpdateService.cs','Localization.cs','RoadRouter.cs','ScumMap.cs','PoiFilterDialog.cs','StartupGuideDialog.cs','MiniMap.cs','Native.cs','Program.cs','Overlay.cs','Zones.cs','Search.cs') | ForEach-Object { Join-Path (Join-Path $miniRoot 'src') $_ }
