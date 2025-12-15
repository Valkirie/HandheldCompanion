[Setup]
; -------------
; SETUP
; -------------

; requires netcorecheck.exe and netcorecheck_x64.exe (see download link below)
#define UseNetCoreCheck
#ifdef UseNetCoreCheck
  #define UseDotNet10
#endif

#define UseDirectX
;Install ViGem first
#define UseViGem
#define UseHideHide
#define UseRTSS
#define UsePawnIO

#define InstallerVersion        "0.2"
#define MyAppSetupName         "Handheld Companion"
#define MyBuildId              "HandheldCompanion"
#define MyAppVersion           "0.28.2.3"
#define MyAppPublisher         "BenjaminLSR"
#define MyAppCopyright         "Copyright © BenjaminLSR"
#define MyAppURL               "https://github.com/Valkirie/HandheldCompanion"
#define MyAppExeName           "HandheldCompanion.exe"
#define MyConfiguration        "Release"

#define RtssExe                "RTSS.exe"
#define EncoderServer64Exe     "EncoderServer64.exe"
#define RTSSHooksLoader64Exe   "RTSSHooksLoader64.exe"
#define EncoderServerExe       "EncoderServer.exe"
#define RTSSHooksLoaderExe     "RTSSHooksLoader.exe"

#define DotNetName             ".NET Desktop Runtime"
#define DirectXName            "DirectX Runtime"
#define ViGemName              "ViGEmBus Setup"
#define HidHideName            "HidHide Drivers"
#define RtssName               "RTSS Setup"
#define PawnIOName             "PawnIO"

#define NewDotNetVersion       "10.0.0"
#define NewDirectXVersion      "9.29.1974"
#define NewViGemVersion        "1.22.0.0"
#define NewHidHideVersion      "1.5.230"
#define NewRtssVersion         "7.3.5.28010"
#define NewPawnIOVersion       "2.0.1.0"

#define DirectXDownloadLink    "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe"
#define HidHideDownloadLink    "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"
#define ViGemDownloadLink      "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
#define RtssDownloadLink       "https://github.com/Valkirie/HandheldCompanion/raw/main/redist/RTSSSetup736.exe"
#define PawnIODownloadLink     "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe"

; Registry  
#define RegAppsPath            "SOFTWARE\" + MyAppSetupName + "\"
#define SoftwareUninstallKey    "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"

#ifdef UseDotNet10
  #define MyConfigurationExt   "net10.0"
  #define DotNetX64DownloadLink "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.0/windowsdesktop-runtime-10.0.0-win-x64.exe"
  #define DotNetX86DownloadLink "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.0/windowsdesktop-runtime-10.0.0-win-x86.exe"
#endif

; Windows 10
#define WindowsVersion         "10.0.22621"

AllowNoIcons=yes
AppName={#MyAppSetupName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppSetupName} {#MyAppVersion}
AppCopyright={#MyAppCopyright}
ArchitecturesInstallIn64BitMode=x64compatible
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL} 
CloseApplications=yes
Compression=lzma
DefaultGroupName={#MyAppSetupName}
DefaultDirName={autopf}\{#MyAppSetupName}
OutputBaseFilename={#MyBuildId}-{#MyAppVersion}
SetupIconFile="{#SourcePath}\HandheldCompanion\Resources\icon.ico"
SetupLogging=yes 
MinVersion={#WindowsVersion}
OutputDir={#SourcePath}\install 
PrivilegesRequired=admin
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=6
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: en; MessagesFile: "compiler:Default.isl"

[Files]
#ifdef UseNetCoreCheck
; download netcorecheck.exe: https://go.microsoft.com/fwlink/?linkid=2135256
; download netcorecheck_x64.exe: https://go.microsoft.com/fwlink/?linkid=2135504
Source: "{#SourcePath}\redist\netcorecheck.exe"; Flags: dontcopy noencryption
Source: "{#SourcePath}\redist\netcorecheck_x64.exe"; Flags: dontcopy noencryption
#endif
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows{#WindowsVersion}.0\win-x64\WinRing0x64.dll"; DestDir: "{app}"; Flags: onlyifdoesntexist																																				   
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows{#WindowsVersion}.0\win-x64\WinRing0x64.sys"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourcePath}\bin\{#MyConfiguration}\{#MyConfigurationExt}-windows{#WindowsVersion}.0\win-x64\*"; Excludes: "*WinRing0x64.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\Certificate.pfx"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#SourcePath}\Certificate.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#SourcePath}\redist\PromptFont.otf"; DestDir: "{autofonts}"; FontInstall: "PromptFont"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppSetupName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppSetupName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -WindowStyle Hidden -File ""{tmp}\Certificate.ps1"""; Description: "Deploying signature"; Flags: runhidden
Filename: "{app}\HandheldCompanion.exe"; Flags: postinstall nowait shellexec skipifsilent; Description: "Starting Handheld Companion"
  
[InstallDelete]
Type: files; Name: "{userdesktop}\HidHide Configuration Client.lnk"
Type: files; Name: "{commondesktop}\HidHide Configuration Client.lnk"

[UninstallRun]
Filename: "C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe"; Parameters: "--cloak-off" ; RunOnceId: "CloakOff"; Flags: runascurrentuser runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Registry]
; Add LocalDumps keys
Root: HKLM; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\HandheldCompanion.exe"; ValueType: string; ValueName: "DumpFolder"; ValueData: "{localappdata}\CrashDumps"; Flags: uninsdeletekey

; Add the compatibility flag to force HandheldCompanion.exe to run as administrator
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "~ RUNASADMIN"; Flags: uninsdeletevalue

[Code]
// Types and variables
type
  TIntegerArray = array of Integer;

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
  SettingsPage: TInputOptionWizardPage;

// Forward declarations
procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess: Boolean); forward;
procedure Dependency_Add_With_Version(const Filename, NewVersion, InstalledVersion, Parameters, Title, URL, Checksum: String; const ForceSuccess, RestartNeeded: Boolean); forward;
function Dependency_PrepareToInstall(var NeedsRestart: Boolean): String; forward;
function Dependency_UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String; forward;
procedure Dependency_AddDotNet10Desktop; forward;
procedure Dependency_AddDirectX; forward;
procedure Dependency_AddHideHide; forward;
procedure Dependency_AddViGem; forward;
procedure Dependency_AddRTSS; forward;
procedure Dependency_AddPawnIO; forward;
function BoolToStr(Value: Boolean): String; forward;

#include "./utils/CompareVersions.iss"
#include "./utils/ApiUtils.iss"
#include "./utils/RegUtils.iss"
#include "./utils/UpdateUninstallWizard.iss"
#include "./utils/Utils.iss"

function NextButtonClick(CurPageID: Integer): Boolean; forward;
procedure DisableCoreIsolation; forward;

procedure InitializeWizard;
begin
  Dependency_DownloadPage := CreateDownloadPage(
    SetupMessage(msgWizardPreparing),
    SetupMessage(msgPreparingDesc),
    nil
  );

  // --- Generic “Optional Settings” page on Welcome ---
  SettingsPage := CreateInputOptionPage(
    wpWelcome,
    'Installation Options',                             // Caption
    'Optional Features',                                // Description
    'Select any optional settings you wish to apply before continuing:',  // SubCaption
    False,                                              // Exclusive = False → checkboxes
    False                                               // ListBox = False → simple list
  );
  // Core Isolation option
  SettingsPage.Add('Disable Windows Core Isolation (Recommended)');
  SettingsPage.Values[0] := False;  // unchecked by default
end;

procedure AddDefenderExclusions_Simple();
var
  PS1, PSBody: string;
  ExitCode: Integer;
begin
  PS1 := ExpandConstant('{tmp}\HC_AddExclusions.ps1');

  // Minimal PS script: add two file exclusions
  PSBody :=
    'Add-MpPreference -ExclusionPath "' + ExpandConstant('{app}\WinRing0x64.sys') + '"' + #13#10 +
    'Add-MpPreference -ExclusionPath "' + ExpandConstant('{app}\HandheldCompanion.sys') + '"' + #13#10;

  SaveStringToFile(PS1, PSBody, False);

  // Run it exactly like your Certificate.ps1 example
  Exec(
    'powershell.exe',
    '-ExecutionPolicy Bypass -WindowStyle Hidden -File "' + PS1 + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode
  );

  Log('Add-MpPreference exit=' + IntToStr(ExitCode));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    AddDefenderExclusions_Simple();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin  
    if Dependency_NeedRestart then 
      WizardForm.RunList.Visible := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;  // allow wizard to proceed
  if CurPageID = SettingsPage.ID then
    if SettingsPage.Values[0] then
      DisableCoreIsolation();
end;

// Disables core isolation settings and requests a single reboot at end
procedure DisableCoreIsolation;
var
  ResultCode: Integer;
begin
  // Hypervisor Enforced Code Integrity
  Exec('reg.exe',
    'add "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" ' +
    '/v Enabled /t REG_DWORD /d 0 /f',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Vulnerable Driver Blocklist
  Exec('reg.exe',
    'add "HKLM\SYSTEM\CurrentControlSet\Control\CI\Config" ' +
    '/v VulnerableDriverBlocklistEnable /t REG_DWORD /d 0 /f',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Control Flow Guard
  Exec('powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "Set-ProcessMitigation -System -Disable CFG"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // mark for single reboot at end
  Dependency_NeedRestart := True;
end;

function NeedRestart: Boolean;
begin
  Log('***Enter NeedRestart()***');
  Log('NeedRestart: ' + BoolToStr(Dependency_NeedRestart));
  Result := Dependency_NeedRestart;
  Log('!!!Leave NeedRestart()!!!');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PrepareToInstallResult: String;
begin
  Log('***Enter PrepareToInstall()***');
  Log('Restart needed: ' + BoolToStr(NeedsRestart));
  PrepareToInstallResult := Dependency_PrepareToInstall(NeedsRestart);
  Log('Result: ' + PrepareToInstallResult);
  Result := PrepareToInstallResult;
  Log('!!!Leave PrepareToInstall()!!!');
end;

function UpdateReadyMemo(
  const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
        MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String
): String;
begin
  Result := Dependency_UpdateReadyMemo(
    Space, NewLine,
    MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
    MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo
  );
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  resultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not(checkListBox.checked[keepAllCheck]) then
    begin
      if DirExists(ExpandConstant('{localappdata}\{#MyBuildId}\profiles')) then
        DelTree(ExpandConstant('{localappdata}\{#MyBuildId}\profiles'), True, True, True);
      if DirExists(ExpandConstant('{localappdata}\{#MyBuildId}\hotkeys')) then
        DelTree(ExpandConstant('{localappdata}\{#MyBuildId}\hotkeys'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\{#MyBuildId}'), True, True, True);
      Exit;
    end
    else
    begin
      if not(checkListBox.checked[profilesCheck]) then
        if DirExists(ExpandConstant('{localappdata}\{#MyBuildId}\profiles')) then
          DelTree(ExpandConstant('{localappdata}\{#MyBuildId}\profiles'), True, True, True);

      if not(checkListBox.checked[hotkeysCheck]) then
        if DirExists(ExpandConstant('{localappdata}\{#MyBuildId}\hotkeys')) then
          DelTree(ExpandConstant('{localappdata}\{#MyBuildId}\hotkeys'), True, True, True);

      if not(checkListBox.checked[applicationSettingsCheck]) then
        DelTree(ExpandConstant('{localappdata}\{#MyBuildId}'), True, True, True);
    end;

    if not(keepHidhideCheckbox.Checked) then
      uninstallHidHide();

    if not(keepVigemCheckbox.Checked) then
    begin
      if ShellExec('', 'msiexec.exe', '/X{966606F3-2745-49E9-BF15-5C3EAA4E9077}', '', SW_SHOW, ewWaitUntilTerminated, resultCode) then
      begin
        Log('Successfully executed Vigem uninstaller');
        if resultCode = 0 then
          Log('Vigem uninstaller finished successfully')
        else
          Log('Vigem uninstaller failed with exit code ' + IntToStr(resultCode));
      end
      else
        Log('Failed to execute Vigem uninstaller');
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  installedVersion: String;
begin
#ifdef UseDotNet10
  installedVersion := RegGetInstalledVersion('{#DotNetName}');
  if compareVersions('{#NewDotNetVersion}', installedVersion, '.', '-') > 0 then
  begin
    Log('{#DotNetName} {#NewDotNetVersion} needs update.');
    Dependency_AddDotNet10Desktop;
  end;
#endif

#ifdef UseDirectX
  installedVersion := RegGetInstalledVersion('{#DirectXName}');
  if compareVersions('{#NewDirectXVersion}', installedVersion, '.', '-') > 0 then
  begin
    Log('{#DirectXName} {#NewDirectXVersion} needs update.');
    Dependency_AddDirectX;
  end;
#endif

#ifdef UseHideHide
  if not IsHidHideInstalled() then
  begin
    Dependency_AddHideHide;
    uninstallHidHide();
  end
  else
  begin
    installedVersion := GetInstalledHidHideVersion();
    if compareVersions('{#NewHidHideVersion}', installedVersion, '.', '-') > 0 then
    begin
      Log('{#HidHideName} {#NewHidHideVersion} needs update.');
      Dependency_AddHideHide;
      uninstallHidHide();
    end;
  end;
#endif

#ifdef UseViGem
  if not IsViGemInstalled() then
  begin
    Dependency_AddViGem;
    uninstallViGem();
  end
  else
  begin
    installedVersion := RegGetInstalledVersion('{#ViGemName}');
    if compareVersions('{#NewViGemVersion}', installedVersion, '.', '-') > 0 then
    begin
      Log('{#ViGemName} {#NewViGemVersion} needs update.');
      Dependency_AddViGem;
      uninstallViGem();
    end;
  end;
#endif

#ifdef UseRTSS
  if not IsRtssInstalled() then
    Dependency_AddRTSS
  else
  begin
    installedVersion := GetInstalledRtssVersion();
    if compareVersions('{#NewRtssVersion}', installedVersion, '.', '-') > 0 then
    begin
      Log('{#RtssName} {#NewRtssVersion} needs update.');
      Dependency_AddRTSS;
    end;
  end;
#endif

#ifdef UsePawnIO
  if not IsPawnIOInstalled() then
    Dependency_AddPawnIO
  else
  begin
    installedVersion := GetInstalledPawnIOVersion();
    if compareVersions('{#NewPawnIOVersion}', installedVersion, '.', '-') > 0 then
    begin
      Log('{#PawnIOName} {#NewPawnIOVersion} needs update.');
      Dependency_AddPawnIO;
    end;
  end;
#endif

  Result := True;
end;

procedure Dependency_Add(const Filename, Parameters, Title, URL, Checksum: String; const ForceSuccess: Boolean);
var
  Dependency: TDependency_Entry;
  DependencyCount: Integer;
begin
  Dependency_Memo := Dependency_Memo + #13#10 + '%1' + Title;
  Dependency.Filename := Filename;
  Dependency.Parameters := Parameters;
  Dependency.Title := Title;
  if FileExists(ExpandConstant('{tmp}\') + Filename) then
    Dependency.URL := ''
  else
    Dependency.URL := URL;
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
  Dependency.NewVersion := NewVersion;
  Dependency.InstalledVersion := InstalledVersion;
  Dependency.Parameters := Parameters;
  Dependency.Title := Title;
  if FileExists(ExpandConstant('{tmp}\') + Filename) then
    Dependency.URL := ''
  else
    Dependency.URL := URL;
  Dependency.Checksum := Checksum;
  Dependency.ForceSuccess := ForceSuccess;
  Dependency.RestartNeeded := RestartNeeded;
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
  if DependencyCount > 0 then
  begin
    Dependency_DownloadPage.Show;
    for DependencyIndex := 0 to DependencyCount - 1 do
    begin
      if Dependency_List[DependencyIndex].URL <> '' then
      begin
        Dependency_DownloadPage.Clear;
        Dependency_DownloadPage.Add(Dependency_List[DependencyIndex].URL, Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Checksum);
        Retry := True;
        while Retry do
        begin
          Retry := False;
          try
            Dependency_DownloadPage.Download;
          except
            if Dependency_DownloadPage.AbortedByUser then
            begin
              Result := Dependency_List[DependencyIndex].Title;
              DependencyIndex := DependencyCount;
            end
            else
            begin
              case SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
                IDABORT: begin
                  Result := Dependency_List[DependencyIndex].Title;
                  DependencyIndex := DependencyCount;
                end;
                IDRETRY: Retry := True;
              end;
            end;
          end;
        end;
      end;
    end;

    if Result = '' then
    begin
      for DependencyIndex := 0 to DependencyCount - 1 do
      begin
        Dependency_DownloadPage.SetText(Dependency_List[DependencyIndex].Title + ' ' + Dependency_List[DependencyIndex].NewVersion, '');
        Dependency_DownloadPage.SetProgress(DependencyIndex + 1, DependencyCount + 1);
        while True do
        begin
          ResultCode := 0;
          if ShellExec('', ExpandConstant('{tmp}\') + Dependency_List[DependencyIndex].Filename, Dependency_List[DependencyIndex].Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
          begin
            Log('Successfully executed ' + Dependency_List[DependencyIndex].Filename + ' with result code: ' + IntToStr(ResultCode));
            if (ResultCode = 0) or Dependency_List[DependencyIndex].ForceSuccess then
            begin
              if Dependency_List[DependencyIndex].RestartNeeded then
              begin
                Log('Restart is needed by ' + Dependency_List[DependencyIndex].Title);
                Dependency_NeedRestart := True;
              end;
              RegSetVersion(Dependency_List[DependencyIndex].Title, Dependency_List[DependencyIndex].NewVersion);
              Break;
            end
            else if ResultCode = 1641 then
            begin
              NeedsRestart := True;
              Log(Dependency_List[DependencyIndex].Title + ' needs restart with result code ' + IntToStr(ResultCode));
              Result := Dependency_List[DependencyIndex].Title;
              Break;
            end
            else if ResultCode = 3010 then
            begin
              Dependency_NeedRestart := True;
              Log(Dependency_List[DependencyIndex].Title + ' needs restart with result code ' + IntToStr(ResultCode));
              Break;
            end;
          end;
          case SuppressibleMsgBox(FmtMessage(SetupMessage(msgErrorFunctionFailed), [Dependency_List[DependencyIndex].Title, IntToStr(ResultCode)]), mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
            IDABORT:
              begin
                Result := Dependency_List[DependencyIndex].Title;
                Break;
              end;
            IDIGNORE:
              Break;
          end;
        end;
        if Result <> '' then
          Break;
      end;

      if NeedsRestart then
      begin
        TempValue := '"' + ExpandConstant('{srcexe}') + '" /restart=1 /LANG="' + ExpandConstant('{language}') + '" /DIR="' + WizardDirValue + '" /GROUP="' + WizardGroupValue + '" /TYPE="' + WizardSetupType(False) + '" /COMPONENTS="' + WizardSelectedComponents(False) + '" /TASKS="' + WizardSelectedTasks(False) + '"';
        if WizardNoIcons then
          TempValue := TempValue + ' /NOICONS';
        RegWriteStringValue(HKA, 'SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce', '{#SetupSetting("AppName")}', TempValue);
      end;
    end;
    Dependency_DownloadPage.Hide;
  end;
end;

function Dependency_UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '';
  if MemoUserInfoInfo <> '' then
    Result := Result + MemoUserInfoInfo + NewLine + NewLine;
  if MemoDirInfo <> '' then
    Result := Result + MemoDirInfo + NewLine + NewLine;
  if MemoTypeInfo <> '' then
    Result := Result + MemoTypeInfo + NewLine + NewLine;
  if MemoComponentsInfo <> '' then
    Result := Result + MemoComponentsInfo + NewLine + NewLine;
  if MemoGroupInfo <> '' then
    Result := Result + MemoGroupInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then
    Result := Result + MemoTasksInfo;
  if Dependency_Memo <> '' then
  begin
    if MemoTasksInfo = '' then
      Result := Result + SetupMessage(msgReadyMemoTasks);
    Result := Result + FmtMessage(Dependency_Memo, [Space]);
  end;
  Log('Ready MemoResult: ' + Result);
end;

function Dependency_IsX64: Boolean;
begin
  Result := not Dependency_ForceX86 and Is64BitInstallMode;
end;

function Dependency_String(const x86, x64: String): String;
begin
  if Dependency_IsX64 then
    Result := x64
  else
    Result := x86;
end;

function Dependency_ArchSuffix: String;
begin
  Result := Dependency_String('', 'x64');
end;

function Dependency_ArchTitle: String;
begin
  Result := Dependency_String(' (x86)', ' (x64)');
end;

function Dependency_IsNetCoreInstalled(const Version: String): Boolean;
var
  ResultCode: Integer;
begin
  if not FileExists(ExpandConstant('{tmp}\') + 'netcorecheck_' + Dependency_ArchSuffix + '.exe') then
    ExtractTemporaryFile('netcorecheck_' + Dependency_ArchSuffix + '.exe');
  Result := ShellExec('', ExpandConstant('{tmp}\') + 'netcorecheck_' + Dependency_ArchSuffix + '.exe', Version, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure Dependency_AddDotNet10Desktop;
begin
  if not Dependency_IsNetCoreInstalled('Microsoft.WindowsDesktop.App 10.0.0') then
    Dependency_Add_With_Version('windowsdesktop-runtime-10.0.0-win-' + Dependency_ArchSuffix + '.exe', '{#NewDotNetVersion}', RegGetInstalledVersion('{#DotNetName}'),
      '/lcid ' + IntToStr(GetUILanguage) + ' /passive /norestart',
      '{#DotNetName}', Dependency_String('{#DotNetX86DownloadLink}', '{#DotNetX64DownloadLink}'), '', False, False);
end;

procedure Dependency_AddDirectX;
begin
  Dependency_Add_With_Version('dxwebsetup.exe', '{#NewDirectXVersion}', RegGetInstalledVersion('{#DirectXName}'),
    '/q',
    '{#DirectXName}',
    '{#DirectXDownloadLink}',
    '', True, False);
end;

procedure Dependency_AddHideHide;
begin
  Dependency_Add_With_Version('HidHide_1.5.230_' + Dependency_ArchSuffix + '.exe', '{#NewHidHideVersion}', RegGetInstalledVersion('{#HidHideName}'),
    '/quiet /norestart',
    '{#HidHideName}',
    '{#HidHideDownloadLink}',
    '', True, False);
end;

procedure Dependency_AddViGem;
begin
  Dependency_Add_With_Version('ViGEmBus_1.22.0_x64_x86_arm64.exe', '{#NewViGemVersion}', RegGetInstalledVersion('{#ViGemName}'),
    '/quiet /norestart',
    '{#ViGemName}',
    '{#ViGemDownloadLink}',
    '', True, True);
end;

procedure Dependency_AddRTSS;
begin
  Dependency_Add_With_Version('RTSSSetup736.exe', '{#NewRtssVersion}', RegGetInstalledVersion('{#RtssName}'),
    '/S',
    '{#RtssName}',
    '{#RtssDownloadLink}',
    '', True, True);

  StopProcess('{#EncoderServer64Exe}');
  StopProcess('{#RTSSHooksLoader64Exe}');
  StopProcess('{#EncoderServerExe}');
  StopProcess('{#RTSSHooksLoaderExe}');
  if IsProcessRunning('{#RtssExe}') then
    StopProcess('{#RtssExe}');
end;

procedure Dependency_AddPawnIO;
begin
  Dependency_Add_With_Version('PawnIO_setup.exe', '{#NewPawnIOVersion}', RegGetInstalledVersion('{#PawnIOName}'),
    '-install -silent',
    '{#PawnIOName}',
    '{#PawnIODownloadLink}',
    '', True, True);
end;

function BoolToStr(Value: Boolean): String;
begin
  if Value then
    Result := 'Yes'
  else
    Result := 'No';
end;