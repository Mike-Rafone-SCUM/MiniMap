@echo off
setlocal
set "SCUM_MINIMAP_TAC_OPAQUE=1"
set "SCUM_MINIMAP_TAC_RASTER="
start "" "%~dp0SkynettMiniMap-Test.exe" --fast-tac-test
