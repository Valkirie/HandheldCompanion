@echo off
@setlocal enableextensions
@cd /d "%~dp0"

echo Installing HidHide
echo.
msiexec /i .\dependencies\HidHideMSI.msi /quiet /qn /norestart /log HidHideSetup.log

echo Installing ViGEm
echo.
msiexec /i .\dependencies\ViGEmBusSetup_x64.msi /quiet /qn /norestart /log ViGEmBusSetup.log

sc.exe QUERY "ControllerService" > NUL
IF ERRORLEVEL 1060 GOTO MISSING

echo Uninstalling previous installation
echo.
sc.exe stop "ControllerService"
sc.exe delete "ControllerService"

:MISSING
echo Installing Controller Service
echo.
sc.exe create "ControllerService" binpath= "%cd%\ControllerService.exe" start= auto DisplayName= "Controller Service"
sc.exe description "ControllerService" "Provides gyroscope and accelerometer support to the Aya Neo 2020 and 2021 models through a virtual DualShock 4 controller. If the service is enabled, embedded controller will be cloaked to applications outside the whitelist. If the service is disabled, embedded controller will be uncloaked and virtual DualShock 4 controller disabled."

echo Starting Controller Service
echo.
sc.exe start "ControllerService"

:END
echo Please restart your device to complete the installation process.
pause