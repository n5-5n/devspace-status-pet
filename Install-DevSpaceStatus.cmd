@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-DevSpaceStatus.ps1"
if errorlevel 1 (
  echo.
  echo インストールに失敗しました。
  pause
  exit /b 1
)
echo.
timeout /t 3 >nul
endlocal
