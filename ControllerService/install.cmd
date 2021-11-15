@echo off
@setlocal enableextensions
@cd /d "%~dp0"

echo.
echo Controller Service Deployement Script
echo.

echo Installing HidHide
msiexec /i dependencies\HidHideMSI.msi /quiet /qn /norestart /log HidHideSetup.log

echo Installing ViGEm
msiexec /i dependencies\ViGEmBusSetup_x64.msi /quiet /qn /norestart /log ViGEmBusSetup.log

echo Installing DirectX
dependencies\dxwebsetup.exe /q

echo Installing Windows Desktop Runtime 5.0.12
dependencies\windowsdesktop-runtime-5.0.12-win-x64.exe /install /quiet /norestart

echo Creating LocalDumps registry key
REG ADD "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ControllerService.exe" /f >> ControllerServiceSetup.log
REG ADD "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ControllerService.exe" /v "DumpFolder" /t REG_EXPAND_SZ /d "%cd%" /f >> ControllerServiceSetup.log

echo Uninstalling previous installation
sc.exe stop "ControllerService" >> ControllerServiceSetup.log
timeout /t 3 /nobreak > nul
sc.exe delete "ControllerService" >> ControllerServiceSetup.log
timeout /t 3 /nobreak > nul

echo Installing Controller Service
sc.exe create "ControllerService" binpath= "%cd%\ControllerService.exe" start= "auto" DisplayName= "Controller Service" >> ControllerServiceSetup.log
timeout /t 2 /nobreak > nul
sc.exe description "ControllerService" "Provides gyroscope and accelerometer support to the AYA NEO 2020, 2021 models through a virtual DualShock 4 controller. If the service is enabled, embedded controller will be cloaked to applications outside the whitelist. If the service is disabled, embedded controller will be uncloaked and virtual DualShock 4 controller disabled." >> ControllerServiceSetup.log
timeout /t 2 /nobreak > nul

echo Starting Controller Service
sc.exe start "ControllerService" >> ControllerServiceSetup.log
timeout /t 2 /nobreak > nul

echo.
echo Please restart your device to complete the installation process.
echo.
pause