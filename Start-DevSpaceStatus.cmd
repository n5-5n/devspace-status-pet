@echo off
setlocal
set "SCRIPT=%~dp0DevSpaceStatus.ps1"
set "PET=%~dp0DevSpacePet.ps1"
start "DevSpace Status" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%SCRIPT%"
start "DevSpace Pet" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%PET%"
endlocal
