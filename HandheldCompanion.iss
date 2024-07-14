[Setup]
; -------------
; SETUP
; -------------
#ifndef Dependency_NoExampleSetup

; requires netcorecheck.exe and netcorecheck_x64.exe (see download link below)
#define UseNetCoreCheck
#ifdef UseNetCoreCheck
  #define UseDotNet80
#endif

;#define UseVC2005
;#define UseVC2008
;#define UseVC2010
;#define UseVC2012
;#define UseVC2013
;#define UseVC2015To2019

#define UseDirectX
;Install ViGem first
#define UseViGem
#define UseHideHide
#define UseRTSS

#define InstallerVersion '0.2'
#define MyAppSetupName 'Handheld Companion'
#define MyBuildId 'HandheldCompanion'
#define MyAppVersion '0.21.5.1'
#define MyAppPublisher 'BenjaminLSR'
#define MyAppCopyright 'Copyright @ BenjaminLSR'
#define MyAppURL 'https://github.com/Valkirie/HandheldCompanion'
#define MyAppExeName "HandheldCompanion.exe"
#define MyConfiguration "Release" 

#define RtssExe "RTSS.exe"
#define EncoderServer64Exe "EncoderServer64.exe"
#define RTSSHooksLoader64Exe "RTSSHooksLoader64.exe"  
#define EncoderServerExe "EncoderServer.exe"
#define RTSSHooksLoaderExe "RTSSHooksLoader.exe"

#define DotNetName ".NET Desktop Runtime"
#define DirectXName "DirectX Runtime"
#define ViGemName "ViGEmBus Setup"
#define HidHideName "HidHide Drivers"
#define RtssName "RTSS Setup"

#define NewDotNetVersion "8.0.1"
#define NewDirectXVersion "9.29.1974"
#define NewViGemVersion "1.22.0.0"
#define NewHidHideVersion "1.5.212"
; RTSS 7.3.6
#define NewRtssVersion "7.3.5.28010"

//#define DotNetX64DownloadLink "https://download.visualstudio.microsoft.com/download/pr/b280d97f-25a9-4ab7-8a12-8291aa3af117/a37ed0e68f51fcd973e9f6cb4f40b1a7/windowsdesktop-runtime-8.0.0-win-x64.exe"
//#define DotNetX86DownloadLink "https://download.visualstudio.microsoft.com/download/pr/f9e3b581-059d-429f-9f0d-1d1167ff7e32/bd7661030cd5d66cd3eee0fd20b24540/windowsdesktop-runtime-8.0.0-win-x86.exe"   
                 
#define DotNetX64DownloadLink "https://download.visualstudio.microsoft.com/download/pr/f18288f6-1732-415b-b577-7fb46510479a/a98239f751a7aed31bc4aa12f348a9bf/windowsdesktop-runtime-8.0.1-win-x64.exe" 
#define DotNetX86DownloadLink "https://download.visualstudio.microsoft.com/download/pr/ca725693-6de7-4a4d-b8a4-4390b0387c66/ce13f2f016152d9b5f2d3c6537cc415b/windowsdesktop-runtime-8.0.1-win-x86.exe"

#define DirectXDownloadLink "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe"
#define HidHideDownloadLink "https://github.com/nefarius/HidHide/releases/download/v1.5.212.0/HidHide_1.5.212_x64.exe"
#define ViGemDownloadLink "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
#define RtssDownloadLink "https://github.com/Valkirie/HandheldCompanion/raw/main/redist/RTSSSetup736.exe"

//Registry  
#define RegAppsPath "SOFTWARE\" +MyAppSetupName+ "\" 
#define SoftwareUninstallKey "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"

#ifdef UseDotNet80
	#define MyConfigurationExt "net8.0"
#endif 

AllowNoIcons=yes
AppName={#MyAppSetupName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppSetupName}
AppCopyright={#MyAppCopyright}
// remove next line if you only deploy 32-bit binaries and dependencies
ArchitecturesInstallIn64BitMode=x64
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL} 
CloseApplications = yes
Compression=lzma
DefaultGroupName={#MyAppSetupName}
DefaultDirName={autopf}\{#MyAppSetupName}
OutputBaseFilename={#MyBuildId}-{#MyAppVersion}  
SetupIconFile="{#SourcePath}\HandheldCompanion\Resources\icon.ico"
SetupLogging=yes 
MinVersion=6.0
OutputDir={#SourcePath}\install 
PrivilegesRequired=admin
SolidCompression=yes 
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: en; MessagesFile: "compiler:Default.isl"

[Files]
#ifdef UseNetCoreCheck
// download netcorecheck.exe: https://go.microsoft.com/fwlink/?linkid=2135256
// download netcorecheck_x64.exe: https://go.microsoft.com/fwlink/?linkid=2135504
Source: "{#SourcePath}\redist\netcorecheck.exe"; Flags: dontcopy noencryption
Source: "{#SourcePath}\redist\netcorecheck_x64.exe"; Flags: dontcopy noencryption
#endif                   
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\WinRing0x64.dll"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\WinRing0x64.sys"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows10.0.19041.0\*"; Excludes: "*WinRing0x64.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#SourcePath}\redist\SegoeIcons.ttf"; DestDir: "{autofonts}"; FontInstall: "Segoe Fluent Icons (TrueType)"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SourcePath}\redist\PromptFont.otf"; DestDir: "{autofonts}"; FontInstall: "PromptFont"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppSetupName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"

[Run]
Filename: "{app}\HandheldCompanion.exe"; Description:"Start Handheld Companion"; Flags: postinstall nowait shellexec skipifsilent;  

[InstallDelete]
Type: files; Name: "{userdesktop}\HidHide Configuration Client.lnk"
Type: files; Name: "{commondesktop}\HidHide Configuration Client.lnk"

[UninstallRun]
Filename: "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe"; Parameters: "--cloak-off" ; RunOnceId: "CloakOff"; Flags: runascurrentuser runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps"; Flags: uninsdeletekeyifempty
Root: HKA; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\HandheldCompanion.exe"; ValueType: string; ValueName: "DumpFolder"; ValueData: "{userdocs}\HandheldCompanion\dumps"; Flags: uninsdeletekey

[Code]  
// types and variables
type
  TIntegerArray = array of integer;

  TDependency_Entry = record
    Filename: String;
    NewVersion: String;
    InstalledVersion: String;
    Parameters: String;
    Title: String;
    URL: String;
    Checksum: String;
    ForceSuccess: Boolean;
    RestartNeeded: Boolean;
  end;

var
  Dependency_Memo: String;
  Dependency_List: array of TDependency_Entry;
  Dependency_NeedRestart, Dependency_ForceX86: Boolean;
  Dependency_DownloadPage: TDownloadWizardPage;

//Prototypes
procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess: Boolean); forward;
procedure Dependency_Add_With_Version(const Filename, NewVersion, InstalledVersion, Parameters, Title, URL, Checksum: String; const ForceSuccess, RestartNeeded: Boolean); forward;
function Dependency_PrepareToInstall(var NeedsRestart: Boolean): String; forward;
function Dependency_UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String; forward;
procedure Dependency_AddDotNet80Desktop; forward;
procedure Dependency_AddDirectX; forward;
procedure Dependency_AddHideHide; forward;
procedure Dependency_AddViGem; forward;
procedure Dependency_AddRTSS; forward; 
function BoolToStr(Value: Boolean): String; forward;


#include "./utils/CompareVersions.iss"
#include "./utils/ApiUtils.iss"
#include "./utils/RegUtils.iss"                              
#include "./utils/UpdateUninstallWizard.iss" 
#include "./utils/Utils.iss"   
       

procedure InitializeWizard;
begin
  Dependency_DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);      
end;    


procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
     //TODO -  Add firewall entry
  end;
end;


procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin  
    if(Dependency_NeedRestart) then 
      WizardForm.RunList.Visible := False;
  end;
end;

function NeedRestart: Boolean;
begin
  log('***Enter NeedRestart()***');
  log('NeedRestart: ' +boolToStr(Dependency_NeedRestart));
  Result := Dependency_NeedRestart;
  log('!!!Leave NeedRestart()!!!');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PrepareToInstallResult:String;
begin
  log('***Enter PrepareToInstall()***');
  log('Restart needed: ' +boolToStr(NeedsRestart));
  PrepareToInstallResult:= Dependency_PrepareToInstall(NeedsRestart);
  log('Result: ' +PrepareToInstallResult);
  result:= PrepareToInstallResult;
  log('!!!Leave PrepareToInstall()!!!');
end; 

function UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := Dependency_UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo);
end;
         

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
      end; 
    end;   
                   
    if not(keepHidhideCheckbox.Checked) then
    begin 
      uninstallHidHide();
    end; 
           
    if not(keepVigemCheckbox.Checked) then
    begin 
      if(ShellExec('', 'msiexec.exe', '/X{966606F3-2745-49E9-BF15-5C3EAA4E9077}', '', SW_SHOW, ewWaitUntilTerminated, resultCode)) then   
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


function InitializeSetup: Boolean;
var
  installedVersion:string;
begin

#ifdef UseDotNet80   
  installedVersion:= regGetInstalledVersion('{#DotNetName}');
  if(compareVersions('{#NewDotNetVersion}', installedVersion, '.', '-') > 0) then
  begin
    log('{#DotNetName} {#NewDotNetVersion} needs update.');
    Dependency_AddDotNet80Desktop; 
  end;  
#endif

#ifdef UseVC2005
  Dependency_AddVC2005;
#endif
#ifdef UseVC2008
  Dependency_AddVC2008;
#endif
#ifdef UseVC2010
  Dependency_AddVC2010;
#endif
#ifdef UseVC2012
  Dependency_AddVC2012;
#endif
#ifdef UseVC2013
  Dependency_AddVC2013;
#endif
#ifdef UseVC2015To2019
  Dependency_AddVC2015To2019;
#endif

#ifdef UseDirectX
  installedVersion:= regGetInstalledVersion('{#DirectXName}');
  if(compareVersions('{#NewDirectXVersion}', installedVersion, '.', '-') > 0) then
  begin
    log('{#DirectXName} {#NewDirectXVersion} needs update.');
    Dependency_AddDirectX; 
  end;      
#endif

#ifdef UseHideHide
  if not(isHidHideInstalled()) then
  begin
    Dependency_AddHideHide;
    uninstallHidHide();
  end
  else
  begin
    installedVersion:= getInstalledHidHideVersion();
    if(compareVersions('{#NewHidHideVersion}', installedVersion, '.', '-') > 0) then
    begin
      log('{#HidHideName} {#NewHidHideVersion} needs update.');
      Dependency_AddHideHide; 
      uninstallHidHide();
    end; 
  end;  
#endif

#ifdef UseViGem
  if not(isViGemInstalled()) then
  begin
    Dependency_AddViGem; 
    uninstallViGem();
  end
  else
  begin
    installedVersion:= regGetInstalledVersion('{#ViGemName}');
    if(compareVersions('{#NewViGemVersion}', installedVersion, '.', '-') > 0) then
    begin
      log('{#ViGemName} {#NewViGemVersion} needs update.');
      Dependency_AddViGem; 
      uninstallViGem();
    end;  
  end;
#endif

#ifdef UseRTSS
  if(not isRtssInstalled()) then
    Dependency_AddRTSS
  else
  begin
    installedVersion:= getInstalledRtssVersion();
    if(compareVersions('{#NewRtssVersion}', installedVersion, '.', '-') > 0) then
    begin
      log('{#RtssName} {#NewRtssVersion} needs update.');
      Dependency_AddRTSS; 
    end;
  end;  
#endif

  Result := True;
end; 
#endif  

procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess: Boolean);
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

  DependencyCount := GetArrayLength(Dependency_List);
  SetArrayLength(Dependency_List, DependencyCount + 1);
  Dependency_List[DependencyCount] := Dependency;
end;


procedure Dependency_Add_With_Version(const Filename, NewVersion, InstalledVersion, Parameters, Title, URL, Checksum: String; const ForceSuccess, RestartNeeded: Boolean);
var
  Dependency: TDependency_Entry;
  DependencyCount: Integer;
begin
  Dependency_Memo := Dependency_Memo + #13#10 + '%1' + Title;

  Dependency.Filename := Filename;
  Dependency.NewVersion:= NewVersion;
  Dependency.InstalledVersion:= InstalledVersion;
  Dependency.Parameters := Parameters;
  Dependency.Title := Title;

  if FileExists(ExpandConstant('{tmp}{\}') + Filename) then begin
    Dependency.URL := '';
  end else begin
    Dependency.URL := URL;
  end;

  Dependency.Checksum := Checksum;
  Dependency.ForceSuccess := ForceSuccess;
  Dependency.RestartNeeded:= RestartNeeded;

  DependencyCount := GetArrayLength(Dependency_List);
  SetArrayLength(Dependency_List, DependencyCount + 1);
  Dependency_List[DependencyCount] := Dependency;
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
        Dependency_DownloadPage.SetText(Dependency_List[DependencyIndex].Title + ' ' +Dependency_List[DependencyIndex].NewVersion, '');
        Dependency_DownloadPage.SetProgress(DependencyIndex + 1, DependencyCount + 1);

        while True do begin
          ResultCode := 0;
          if ShellExec('', ExpandConstant('{tmp}{\}') + Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then 
          begin
            log('Successfully executed ' +Dependency_List[DependencyIndex].Filename+ ' with result code: ' +intToStr(ResultCode));
  
            if (ResultCode = 0) or Dependency_List[DependencyIndex].ForceSuccess then begin // ERROR_SUCCESS (0)
            begin   
              if(Dependency_List[DependencyIndex].RestartNeeded) then
              begin
                log('Restart is needed by ' +Dependency_List[DependencyIndex].Title);
                Dependency_NeedRestart := True;   
              end;
              regSetVersion(Dependency_List[DependencyIndex].Title, Dependency_List[DependencyIndex].NewVersion);
              break;
            end
            end else if ResultCode = 1641 then begin // ERROR_SUCCESS_REBOOT_INITIATED (1641)
              NeedsRestart := True;
              log(Dependency_List[DependencyIndex].Title + ' needs restart with result code ' +intToStr(ResultCode));
              Result := Dependency_List[DependencyIndex].Title;
              break;
            end else if ResultCode = 3010 then begin // ERROR_SUCCESS_REBOOT_REQUIRED (3010)
              Dependency_NeedRestart := True;
              log(Dependency_List[DependencyIndex].Title + ' needs restart with result code ' +intToStr(ResultCode));
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
  log('Ready MemoResult: ' +result);
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

function Dependency_IsNetCoreInstalled(const Version: String): Boolean;
var
  ResultCode: Integer;
begin
  // source code: https://github.com/dotnet/deployment-tools/tree/master/src/clickonce/native/projects/NetCoreCheck
  if not FileExists(ExpandConstant('{tmp}{\}') + 'netcorecheck' + Dependency_ArchSuffix + '.exe') then begin
    ExtractTemporaryFile('netcorecheck' + Dependency_ArchSuffix + '.exe');
  end;
  Result := ShellExec('', ExpandConstant('{tmp}{\}') + 'netcorecheck' + Dependency_ArchSuffix + '.exe', Version, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure Dependency_AddDotNet80Desktop;
begin
  // https://dotnet.microsoft.com/en-us/download/dotnet/8.0
  if not Dependency_IsNetCoreInstalled('Microsoft.WindowsDesktop.App 8.0.0') then begin
    Dependency_Add_With_Version('dotNet80desktop' + Dependency_ArchSuffix + '.exe', '{#NewDotNetVersion}', regGetInstalledVersion('{#DotNetName}'),
      '/lcid ' + IntToStr(GetUILanguage) + ' /passive /norestart',
      '{#DotNetName}', Dependency_String('{#DotNetX86DownloadLink}', '{#DotNetX64DownloadLink}'), '', False, False);
  end;
end;  


procedure Dependency_AddVC2005;
begin
  // https://www.microsoft.com/en-US/download/details.aspx?id=26347
  if not IsMsiProductInstalled(Dependency_String('{86C9D5AA-F00C-4921-B3F2-C60AF92E2844}', '{A8D19029-8E5C-4E22-8011-48070F9E796E}'), PackVersionComponents(8, 0, 61000, 0)) then begin
    Dependency_Add('vcredist2005' + Dependency_ArchSuffix + '.exe',
      '/q',
      'Visual C++ 2005 Service Pack 1 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x86.EXE', 'https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x64.EXE'),
      '', False);
  end;
end;

procedure Dependency_AddVC2008;
begin
  // https://www.microsoft.com/en-US/download/details.aspx?id=26368
  if not IsMsiProductInstalled(Dependency_String('{DE2C306F-A067-38EF-B86C-03DE4B0312F9}', '{FDA45DDF-8E17-336F-A3ED-356B7B7C688A}'), PackVersionComponents(9, 0, 30729, 6161)) then begin
    Dependency_Add('vcredist2008' + Dependency_ArchSuffix + '.exe',
      '/q',
      'Visual C++ 2008 Service Pack 1 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe', 'https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe'),
      '', False);
  end;
end;

procedure Dependency_AddVC2010;
begin
  // https://www.microsoft.com/en-US/download/details.aspx?id=26999
  if not IsMsiProductInstalled(Dependency_String('{1F4F1D2A-D9DA-32CF-9909-48485DA06DD5}', '{5B75F761-BAC8-33BC-A381-464DDDD813A3}'), PackVersionComponents(10, 0, 40219, 0)) then begin
    Dependency_Add('vcredist2010' + Dependency_ArchSuffix + '.exe',
      '/passive /norestart',
      'Visual C++ 2010 Service Pack 1 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe', 'https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe'),
      '', False);
  end;
end;

procedure Dependency_AddVC2012;
begin
  // https://www.microsoft.com/en-US/download/details.aspx?id=30679
  if not IsMsiProductInstalled(Dependency_String('{4121ED58-4BD9-3E7B-A8B5-9F8BAAE045B7}', '{EFA6AFA1-738E-3E00-8101-FD03B86B29D1}'), PackVersionComponents(11, 0, 61030, 0)) then begin
    Dependency_Add('vcredist2012' + Dependency_ArchSuffix + '.exe',
      '/passive /norestart',
      'Visual C++ 2012 Update 4 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe', 'https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe'),
      '', False);
  end;
end;

procedure Dependency_AddVC2013;
begin
  // https://support.microsoft.com/en-US/help/4032938
  if not IsMsiProductInstalled(Dependency_String('{B59F5BF1-67C8-3802-8E59-2CE551A39FC5}', '{20400CF0-DE7C-327E-9AE4-F0F38D9085F8}'), PackVersionComponents(12, 0, 40664, 0)) then begin
    Dependency_Add('vcredist2013' + Dependency_ArchSuffix + '.exe',
      '/passive /norestart',
      'Visual C++ 2013 Update 5 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://download.visualstudio.microsoft.com/download/pr/10912113/5da66ddebb0ad32ebd4b922fd82e8e25/vcredist_x86.exe', 'https://download.visualstudio.microsoft.com/download/pr/10912041/cee5d6bca2ddbcd039da727bf4acb48a/vcredist_x64.exe'),
      '', False);
  end;
end;

procedure Dependency_AddVC2015To2019;
begin
  // https://support.microsoft.com/en-US/help/2977003/the-latest-supported-visual-c-downloads
  if not IsMsiProductInstalled(Dependency_String('{65E5BD06-6392-3027-8C26-853107D3CF1A}', '{36F68A90-239C-34DF-B58C-64B30153CE35}'), PackVersionComponents(14, 29, 30037, 0)) then begin
    Dependency_Add('vcredist2019' + Dependency_ArchSuffix + '.exe',
      '/passive /norestart',
      'Visual C++ 2015-2019 Redistributable' + Dependency_ArchTitle,
      Dependency_String('https://aka.ms/vs/16/release/vc_redist.x86.exe', 'https://aka.ms/vs/16/release/vc_redist.x64.exe'),
      '', False);
  end;
end;

procedure Dependency_AddDirectX;
begin
  // https://www.microsoft.com/en-US/download/details.aspx?id=35
  Dependency_Add_With_Version('dxwebsetup.exe', '{#NewDirectXVersion}', regGetInstalledVersion('{#DirectXName}'),
    '/q',
    '{#DirectXName}',
    '{#DirectXDownloadLink}',
    '', True, False);
end;

procedure Dependency_AddHideHide;
begin
  Dependency_Add_With_Version('HidHide_1.5.212_x64.exe', '{#NewHidHideVersion}', regGetInstalledVersion('{#HidHideName}'),
    '/quiet /norestart',
    '{#HidHideName}',
    '{#HidHideDownloadLink}',
    '', True, False);
end;

procedure Dependency_AddViGem;
begin
  Dependency_Add_With_Version('ViGEmBus_1.22.0_x64_x86_arm64.exe', '{#NewViGemVersion}', regGetInstalledVersion('{#ViGemName}'),
    '/quiet /norestart',
    '{#ViGemName}',
    '{#ViGemDownloadLink}',
    '', True, True);
end;

procedure Dependency_AddRTSS;
begin
  Dependency_Add_With_Version('RTSSSetup736.exe', '{#NewRtssVersion}', regGetInstalledVersion('{#RtssName}'),
    '/S',
    '{#RtssName}',
    '{#RtssDownloadLink}',
    '', True, True);   

  stopProcess('{#EncoderServer64Exe}');
  stopProcess('{#RTSSHooksLoader64Exe}');
  stopProcess('{#EncoderServerExe}');
  stopProcess('{#RTSSHooksLoaderExe}');

  if(isProcessRunning('{#RtssExe}')) then
  begin
    stopProcess('{#RtssExe}');
  end;
end;
         
function BoolToStr(Value: Boolean): String; 
begin
  if Value then
    Result := 'Yes'
  else
    Result := 'No';
end;
