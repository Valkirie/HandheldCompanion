@echo off
@setlocal enableextensions
@cd /d "%~dp0"

echo.
echo Controller Service Deployement Script
echo.

mkdir Logs

echo Installing HidHide
msiexec /i Resources\HidHideMSI.msi /quiet /qn /norestart /log "Logs\HidHideSetup.log"

echo Installing ViGEm
msiexec /i Resources\ViGEmBusSetup_x64.msi /quiet /qn /norestart /log "Logs\ViGEmBusSetup.log"

echo Installing Windows Desktop Runtime 5.0.12
Resources\windowsdesktop-runtime-5.0.12-win-x64.exe /install /quiet /norestart

echo Installing DirectX
Resources\dxwebsetup.exe

echo Creating LocalDumps registry key
REG ADD "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ControllerService.exe" /f >> "Logs\ControllerServiceSetup.log"
REG ADD "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ControllerService.exe" /v "DumpFolder" /t REG_EXPAND_SZ /d "%cd%" /f >> "Logs\ControllerServiceSetup.log"

echo Starting Controller Helper
start ControllerHelper.exe
timeout /t 2 /nobreak > nul

echo.
echo Please restart your device to complete the installation process.
echo.
pause