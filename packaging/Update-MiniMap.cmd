@echo off
start "" "%~dp0SkynettMiniMap.exe" -InstallUpdate %*
if errorlevel 1 pause
