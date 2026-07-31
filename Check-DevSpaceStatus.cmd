@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DevSpaceStatus.ps1" -Once
 echo.
pause
endlocal
