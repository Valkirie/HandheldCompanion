@echo off
@setlocal enableextensions
@cd /d "%~dp0"

echo Installing HidHide
echo.
msiexec /i .\dependencies\HidHideMSI.msi /quiet /qn /norestart /log HidHideSetup.log

echo Installing ViGEm
echo.
msiexec /i .\dependencies\ViGEmBusSetup_x64.msi /quiet /qn /norestart /log ViGEmBusSetup.log

echo Installing and starting Controller Service
echo.
ControllerService.exe --uninstall
ControllerService.exe --install

echo Press any key to restart your Aya Neo.
pause
shutdown.exe /r /t 10