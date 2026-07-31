@echo off
setlocal
start "DevSpace Status Pet Settings" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Open-DevSpaceStatusSettings.ps1"
endlocal
