; -- CodeDependencies.iss --
;
; This script shows how to download and install any dependency such as .NET,
; Visual C++ or SQL Server during your application's installation process.
;
; contribute: https://github.com/DomGries/InnoDependencyInstaller


; -----------
; SHARED CODE
; -----------
[Code]
// types and variables
type
  TDependency_Entry = record
    Filename: String;
    Parameters: String;
    Title: String;
    URL: String;
    Checksum: String;
    ForceSuccess: Boolean;
    RestartAfter: Boolean;
  end;

var
  Dependency_Memo: String;
  Dependency_List: array of TDependency_Entry;
  Dependency_NeedRestart, Dependency_ForceX86: Boolean;
  Dependency_DownloadPage: TDownloadWizardPage;

procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess, RestartAfter: Boolean);
var
  Dependency: TDependency_Entry;
  DependencyCount: Integer;
begin
  Dependency_Memo := Dependency_Memo + #13#10 + '%1' + Title;

  Dependency.Filename := Filename;
  Dependency.Parameters := Parameters;
  Dependency.Title := Title;

  if FileExists(ExpandConstant('{tmp}{\}') + Filename) then begin
    Dependency.URL := '';
  end else begin
    Dependency.URL := URL;
  end;

  Dependency.Checksum := Checksum;
  Dependency.ForceSuccess := ForceSuccess;
  Dependency.RestartAfter := RestartAfter;

  DependencyCount := GetArrayLength(Dependency_List);
  SetArrayLength(Dependency_List, DependencyCount + 1);
  Dependency_List[DependencyCount] := Dependency;
end;

procedure Dependency_InitializeWizard;
begin
  Dependency_DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function Dependency_PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DependencyCount, DependencyIndex, ResultCode: Integer;
  Retry: Boolean;
  TempValue: String;
begin
  DependencyCount := GetArrayLength(Dependency_List);

  if DependencyCount > 0 then begin
    Dependency_DownloadPage.Show;

    for DependencyIndex := 0 to DependencyCount - 1 do begin
      if Dependency_List[DependencyIndex].URL <> '' then begin
        Dependency_DownloadPage.Clear;
        Dependency_DownloadPage.Add(Dependency_List[DependencyIndex].URL, Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Checksum);

        Retry := True;
        while Retry do begin
          Retry := False;

          try
            Dependency_DownloadPage.Download;
          except
            if Dependency_DownloadPage.AbortedByUser then begin
              Result := Dependency_List[DependencyIndex].Title;
              DependencyIndex := DependencyCount;
            end else begin
              case SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
                IDABORT: begin
                  Result := Dependency_List[DependencyIndex].Title;
                  DependencyIndex := DependencyCount;
                end;
                IDRETRY: begin
                  Retry := True;
                end;
              end;
            end;
          end;
        end;
      end;
    end;

    if Result = '' then begin
      for DependencyIndex := 0 to DependencyCount - 1 do begin
        Dependency_DownloadPage.SetText(Dependency_List[DependencyIndex].Title, '');
        Dependency_DownloadPage.SetProgress(DependencyIndex + 1, DependencyCount + 1);

        while True do begin
          ResultCode := 0;
          if ShellExec('', ExpandConstant('{tmp}{\}') + Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then begin
            if Dependency_List[DependencyIndex].RestartAfter then begin
              if DependencyIndex = DependencyCount - 1 then begin
                Dependency_NeedRestart := True;
              end else begin
                NeedsRestart := True;
                Result := Dependency_List[DependencyIndex].Title;
              end;
              break;
            end else if (ResultCode = 0) or Dependency_List[DependencyIndex].ForceSuccess then begin // ERROR_SUCCESS (0)
              break;
            end else if ResultCode = 1641 then begin // ERROR_SUCCESS_REBOOT_INITIATED (1641)
              NeedsRestart := True;
              Result := Dependency_List[DependencyIndex].Title;
              break;
            end else if ResultCode = 3010 then begin // ERROR_SUCCESS_REBOOT_REQUIRED (3010)
              Dependency_NeedRestart := True;
              break;
            end;
          end;

          case SuppressibleMsgBox(FmtMessage(SetupMessage(msgErrorFunctionFailed), [Dependency_List[DependencyIndex].Title, IntToStr(ResultCode)]), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
            IDABORT: begin
              Result := Dependency_List[DependencyIndex].Title;
              break;
            end;
            IDIGNORE: begin
              break;
            end;
          end;
        end;

        if Result <> '' then begin
          break;
        end;
      end;

      if NeedsRestart then begin
        TempValue := '"' + ExpandConstant('{srcexe}') + '" /restart=1 /LANG="' + ExpandConstant('{language}') + '" /DIR="' + WizardDirValue + '" /GROUP="' + WizardGroupValue + '" /TYPE="' + WizardSetupType(False) + '" /COMPONENTS="' + WizardSelectedComponents(False) + '" /TASKS="' + WizardSelectedTasks(False) + '"';
        if WizardNoIcons then begin
          TempValue := TempValue + ' /NOICONS';
        end;
        RegWriteStringValue(HKA, 'SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce', '{#SetupSetting("AppName")}', TempValue);
      end;
    end;

    Dependency_DownloadPage.Hide;
  end;
end;

function Dependency_UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '';
  if MemoUserInfoInfo <> '' then begin
    Result := Result + MemoUserInfoInfo + Newline + NewLine;
  end;
  if MemoDirInfo <> '' then begin
    Result := Result + MemoDirInfo + Newline + NewLine;
  end;
  if MemoTypeInfo <> '' then begin
    Result := Result + MemoTypeInfo + Newline + NewLine;
  end;
  if MemoComponentsInfo <> '' then begin
    Result := Result + MemoComponentsInfo + Newline + NewLine;
  end;
  if MemoGroupInfo <> '' then begin
    Result := Result + MemoGroupInfo + Newline + NewLine;
  end;
  if MemoTasksInfo <> '' then begin
    Result := Result + MemoTasksInfo;
  end;

  if Dependency_Memo <> '' then begin
    if MemoTasksInfo = '' then begin
      Result := Result + SetupMessage(msgReadyMemoTasks);
    end;
    Result := Result + FmtMessage(Dependency_Memo, [Space]);
  end;
end;

function Dependency_IsX64: Boolean;
begin
  Result := not Dependency_ForceX86 and Is64BitInstallMode;
end;

function Dependency_String(const x86, x64: String): String;
begin
  if Dependency_IsX64 then begin
    Result := x64;
  end else begin
    Result := x86;
  end;
end;

function Dependency_ArchSuffix: String;
begin
  Result := Dependency_String('', '_x64');
end;

function Dependency_ArchTitle: String;
begin
  Result := Dependency_String(' (x86)', ' (x64)');
end;

[Setup]
; -------------
; EXAMPLE SETUP
; -------------
#ifndef Dependency_NoExampleSetup

#define UseOfflineInstaller

; requires netcorecheck.exe and netcorecheck_x64.exe (see download link below)
; #define UseNetCoreCheck
#ifdef UseNetCoreCheck
#endif

#define UseDotNet70

#define UseVC2005
#define UseVC2008
#define UseVC2010
#define UseVC2012
#define UseVC2013
#define UseVC2015To2019

#define UseDirectX
#define UseHideHide
#define UseViGem
#define UseRTSS
#define UseHWiNFO

#define MyAppSetupName 'Handheld Companion'
#define MyBuildId 'HandheldCompanion'
#define MyAppVersion '0.16.2.6'
#define MyAppPublisher 'BenjaminLSR'
#define MyAppCopyright 'Copyright © BenjaminLSR'
#define MyAppURL 'https://github.com/Valkirie/HandheldCompanion'
#define MyAppExeName "HandheldCompanion.exe"
#define MySerExeName "ControllerService.exe"
#define MyConfiguration "Release"

#ifdef UseDotNet70
	#define MyConfigurationExt "net7.0"
#endif

; #define ClearProfiles
; #define ClearHotkeys

AppName={#MyAppSetupName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppSetupName}
AppCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
OutputBaseFilename={#MyBuildId}-{#MyAppVersion}-offline
DefaultGroupName={#MyAppSetupName}
DefaultDirName={autopf}\{#MyAppSetupName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile="{#SourcePath}\HandheldCompanion\Resources\icon.ico"
SourceDir=redist
OutputDir={#SourcePath}\install
AllowNoIcons=yes

MinVersion=6.0
PrivilegesRequired=admin

// remove next line if you only deploy 32-bit binaries and dependencies
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: en; MessagesFile: "compiler:Default.isl"

[Setup]
AlwaysRestart = yes
CloseApplications = yes

[Files]
#ifdef UseNetCoreCheck
// download netcorecheck.exe: https://go.microsoft.com/fwlink/?linkid=2135256
// download netcorecheck_x64.exe: https://go.microsoft.com/fwlink/?linkid=2135504
Source: "netcorecheck.exe"; Flags: dontcopy noencryption
Source: "netcorecheck_x64.exe"; Flags: dontcopy noencryption
#endif

#ifdef UseOfflineInstaller
Source: "dxwebsetup.exe"; Flags: dontcopy noencryption
Source: "vcredist2005_x64.exe"; Flags: dontcopy noencryption
Source: "vcredist2008_x64.exe"; Flags: dontcopy noencryption
Source: "vcredist2010_x64.exe"; Flags: dontcopy noencryption
Source: "vcredist2012_x64.exe"; Flags: dontcopy noencryption
Source: "vcredist2013_x64.exe"; Flags: dontcopy noencryption
Source: "vcredist2019_x64.exe"; Flags: dontcopy noencryption
	
	#ifdef UseDotNet70
		Source: "dotnet-runtime-7.0.0-win-x64.exe"; Flags: dontcopy noencryption
		Source: "windowsdesktop-runtime-7.0.0-win-x64.exe"; Flags: dontcopy noencryption
	#endif
#endif

Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\WinRing0x64.dll"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\WinRing0x64.sys"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\*"; Excludes: "*WinRing0x64.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#SourcePath}\redist\SegoeIcons.ttf"; DestDir: "{autofonts}"; FontInstall: "Segoe Fluent Icons (TrueType)"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SourcePath}\redist\PromptFont.otf"; DestDir: "{autofonts}"; FontInstall: "PromptFont"; Flags: uninsneveruninstall

Source: "{#SourcePath}\redist\ViGEmBus_1.21.442_x64_x86_arm64.exe"; DestDir: "{app}\redist\"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\redist\HidHide_1.2.98_x64.exe"; DestDir: "{app}\redist\"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\redist\RTSSSetup734.exe"; DestDir: "{app}\redist\"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\redist\hwi_746.exe"; DestDir: "{app}\redist\"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppSetupName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"

[Run]
Filename: "{tmp}\dxwebsetup.exe"; StatusMsg: "Installing DirectX Runtime"; Parameters: "/q"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2019_x64.exe"; StatusMsg: "Installing Visual C++ 2015-2019 Redistributable"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2013_x64.exe"; StatusMsg: "Installing Visual C++ 2013 Redistributable"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2012_x64.exe"; StatusMsg: "Installing Visual C++ 2012 Redistributable"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2010_x64.exe"; StatusMsg: "Installing Visual C++ 2010 Redistributable"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2008_x64.exe"; StatusMsg: "Installing Visual C++ 2008 Redistributable"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\vcredist2005_x64.exe"; StatusMsg: "Installing Visual C++ 2005 Redistributable"; Parameters: "/Q"; Flags: waituntilterminated

#ifdef UseDotNet70
Filename: "{tmp}\windowsdesktop-runtime-7.0.0-win-x64"; StatusMsg: ".NET Desktop Runtime 7.0.0"; Parameters: "/passive /norestart"; Flags: waituntilterminated
Filename: "{tmp}\dotnet-runtime-7.0.0-win-x64"; StatusMsg: "Installing .NET Runtime 7.0.0"; Parameters: "/passive /norestart"; Flags: waituntilterminated
#endif

Filename: "{app}\redist\ViGEmBus_1.21.442_x64_x86_arm64.exe"; StatusMsg: "Installing ViGEmBus"; Parameters: "/quiet /norestart"; Flags: runascurrentuser
Filename: "{app}\redist\HidHide_1.2.98_x64.exe"; StatusMsg: "Installing HidHide"; Parameters: "/quiet /norestart"; Flags: runascurrentuser
Filename: "{app}\redist\RTSSSetup734.exe"; StatusMsg: "Installing RTSS"; Parameters: "/S"; Flags: runascurrentuser
Filename: "{app}\redist\hwi_746.exe"; StatusMsg: "Installing HWiNFO"; Parameters: "/silent"; Flags: runascurrentuser

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ControllerService" ; RunOnceId: "StopService"; Flags: runascurrentuser runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete ControllerService" ; RunOnceId: "DeleteService"; Flags: runascurrentuser runhidden
Filename: "C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe"; Parameters: "--cloak-off" ; RunOnceId: "CloakOff"; Flags: runascurrentuser runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[InstallDelete]
#ifdef ClearHotkeys
Type: filesandordirs; Name: "{userdocs}\{#MyBuildId}\hotkeys"
#endif

#ifdef ClearProfiles
Type: filesandordirs; Name: "{userdocs}\{#MyBuildId}\profiles"
#endif

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\ControllerService.exe"; ValueType: string; ValueName: "DumpFolder"; ValueData: "{userdocs}\HandheldCompanion\dumps"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\HandheldCompanion.exe"; ValueType: string; ValueName: "DumpFolder"; ValueData: "{userdocs}\HandheldCompanion\dumps"; Flags: uninsdeletekey

[Code]
#include "./UpdateUninstallWizard.iss"

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  resultCode:integer;
begin
  if CurUninstallStep = usUninstall then
  begin     
    if not(checkListBox.checked[keepAllCheck]) then
    begin
      if DirExists(ExpandConstant('{userdocs}\{#MyBuildId}\profiles'))  then  
        DelTree(ExpandConstant('{userdocs}\{#MyBuildId}\profiles'), True, True, True);
      if DirExists(ExpandConstant('{userdocs}\{#MyBuildId}\hotkeys'))  then
        DelTree(ExpandConstant('{userdocs}\{#MyBuildId}\hotkeys'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\HandheldCompanion'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\ControllerService'), True, True, True);
      exit;
    end
    else
    begin
      if not(checkListBox.checked[profilesCheck]) then
      begin
        if DirExists(ExpandConstant('{userdocs}\{#MyBuildId}\profiles'))  then  
          DelTree(ExpandConstant('{userdocs}\{#MyBuildId}\profiles'), True, True, True);
      end;

      if not(checkListBox.checked[hotkeysCheck]) then
      begin
        if DirExists(ExpandConstant('{userdocs}\{#MyBuildId}\hotkeys'))  then
          DelTree(ExpandConstant('{userdocs}\{#MyBuildId}\hotkeys'), True, True, True);
      end;
      
      if not(checkListBox.checked[applicationSettingsCheck]) then
      begin 
        DelTree(ExpandConstant('{localappdata}\HandheldCompanion'), True, True, True);
        DelTree(ExpandConstant('{localappdata}\ControllerService'), True, True, True);
      end; 
    end;   
                   
    if not(keepHidhideCheckbox.Checked) then
    begin 
      if(ShellExec('', 'msiexec.exe', '/X{27AF679E-48DB-4B49-A689-1D6A3A52C472} /qn /norestart', '', SW_SHOW, ewWaitUntilTerminated, resultCode)) then  
      begin
        log('Successfully executed Hidhide uninstaller');
        if(resultCode = 0) then
          log('Hidhide uninstaller finished successfully')
        else
          log('Hidhide uninstaller failed with exit code ' +intToStr(resultCode));
      end
      else
      begin
        log('Failed to execute Hidhide uninstaller');
      end;
    end; 
           
    if not(keepVigemCheckbox.Checked) then
    begin 
      if(ShellExec('', 'msiexec.exe', '/X{9C581C76-2D68-40F8-AA6F-94D3C5215C05} /qn /norestart', '', SW_SHOW, ewWaitUntilTerminated, resultCode)) then   
      begin
        log('Successfully executed Vigem uninstaller');
        if(resultCode = 0) then
          log('Vigem uninstaller finished successfully')
        else
          log('Vigem uninstaller failed with exit code ' +intToStr(resultCode));
      end
      else
      begin
        log('Failed to execute Vigem uninstaller');
      end;
    end;
  end;
end;

procedure InitializeWizard;
begin
  Dependency_InitializeWizard;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := Dependency_PrepareToInstall(NeedsRestart);
end;

function NeedRestart: Boolean;
begin
  Result := Dependency_NeedRestart;
end;

function UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := Dependency_UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo);
end;

function InitializeSetup: Boolean;
begin
#ifdef UseDotNet70
  ExtractTemporaryFile('dotnet-runtime-7.0.0-win-x64.exe');
  ExtractTemporaryFile('windowsdesktop-runtime-7.0.0-win-x64.exe');
#endif

#ifdef UseVC2005
  ExtractTemporaryFile('vcredist2005_x64.exe');
#endif
#ifdef UseVC2008
  ExtractTemporaryFile('vcredist2008_x64.exe');
#endif
#ifdef UseVC2010
  ExtractTemporaryFile('vcredist2010_x64.exe');
#endif
#ifdef UseVC2012
  ExtractTemporaryFile('vcredist2012_x64.exe');
#endif
#ifdef UseVC2013
  ExtractTemporaryFile('vcredist2013_x64.exe');
#endif
#ifdef UseVC2015To2019
  ExtractTemporaryFile('vcredist2019_x64.exe');
#endif

#ifdef UseDirectX
  ExtractTemporaryFile('dxwebsetup.exe');
#endif

#ifdef UseHideHide
  ExtractTemporaryFile('HidHide_1.2.98_x64.exe');
#endif

#ifdef UseViGem
  ExtractTemporaryFile('ViGEmBus_1.21.442_x64_x86_arm64.exe');
#endif

#ifdef UseRTSS
  ExtractTemporaryFile('RTSSSetup734.exe');
#endif

#ifdef UseHWiNFO
  ExtractTemporaryFile('hwi_746.exe');
#endif

  Result := True;
end;

#endif