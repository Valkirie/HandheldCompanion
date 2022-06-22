@echo off
echo Handheld Companion Redistributable Installer
echo.

echo Installing DirectX Runtime
dxwebsetup.exe /q

echo Installing ViGEmBus Runtime
ViGEmBusSetup_x64.msi /quiet /qn /norestart

echo Installing HidHide Runtime
HidHideMSI.msi /quiet /qn /norestart

echo Installing Visual C++ 2015-2019 Redistributable
vcredist2019_x64.EXE /passive /norestart

echo Installing Visual C++ 2013 Update 5 Redistributable
vcredist2013_x64.EXE /passive /norestart

echo Installing Visual C++ 2012 Update 4 Redistributable
vcredist2012_x64.EXE /passive /norestart

echo Installing Visual C++ 2010 Service Pack 1 Redistributable
vcredist2010_x64.EXE /passive /norestart

echo Installing Visual C++ 2008 Service Pack 1 Redistributable
vcredist2008_x64.EXE /passive /norestart

echo Installing Visual C++ 2005 Service Pack 1 Redistributable
vcredist2005_x64.EXE /Q

echo Installing .NET Desktop Runtime 6.0.6
windowsdesktop-runtime-6.0.6-win-x64.exe /passive /norestart

echo Installing .NET Runtime 6.0.6
dotnet-runtime-6.0.6-win-x64 /passive /norestart