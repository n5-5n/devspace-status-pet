@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-DevSpaceStatus.ps1" -Interactive
if errorlevel 1 pause
endlocal
