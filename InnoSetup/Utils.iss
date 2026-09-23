[Code]
function isRtssInstalled():boolean;
begin
  result:= false;
  if(FileExists(ExpandConstant('{commonpf32}') + '\RivaTuner Statistics Server\RTSS.exe')) then
  begin          
    log('RTSS is already installed.');
    result:= true;
  end;
end;


function getInstalledRtssVersion():string;
var
  versionNumber, filePath:string;
begin
  result:= '';
  filePath:= ExpandConstant('{commonpf32}') + '\RivaTuner Statistics Server\RTSS.exe';

  if(FileExists(filePath)) then
  begin 
    if(GetVersionNumbersString(filePath, versionNumber)) then   
      log('Found installed RTSS version: ' + versionNumber);
    result:= versionNumber;
  end;  
end;


function isPawnIOInstalled(): boolean;
var
  installLocation, driverPath: string;
begin
  Result := False;

  if not regUninstallKeyExists('PawnIO') then
  begin
    Log('PawnIO uninstall key not found.');
    Exit;
  end;

  installLocation := regGetUninstallValue('PawnIO', 'InstallLocation');
  if installLocation = '' then
  begin
    Log('PawnIO InstallLocation is empty or missing.');
    Exit;
  end;

  // Normalize and build full path to driver
  installLocation := RemoveQuotes(installLocation);
  driverPath := AddBackslash(installLocation) + 'PawnIOLib.dll';

  if FileExists(driverPath) then
  begin
    Log('PawnIOLib.dll found at: ' + driverPath);
    Result := True;
  end
  else
    Log('PawnIOLib.dll NOT found at: ' + driverPath);
end;


function GetInstalledPawnIOVersion(): string;
begin
  Result := '';
  if not regUninstallKeyExists('PawnIO') then
  begin
    Log('PawnIO uninstall key not found (version unavailable).');
    Exit;
  end;

  Result := regGetUninstallValue('PawnIO', 'DisplayVersion');
  if Result = '' then
    Log('PawnIO DisplayVersion is empty or missing.');
end;


function IsUSBipInstalled(): boolean;
var
  uninstallString: string;
begin
  Result := False;

  if not regUninstallKeyExists('{199505b0-b93d-4521-a8c7-897818e0205a}_is1') then
  begin
    Log('USBip uninstall key not found.');
    Exit;
  end;

  uninstallString := regGetUninstallValue('{199505b0-b93d-4521-a8c7-897818e0205a}_is1', 'UninstallString');
  if uninstallString = '' then
  begin
    Log('USBip UninstallString is empty or missing.');
    Exit;
  end;

  Log('USBip uninstall key found.');
  Result := True;
end;


function GetInstalledUSBipVersion(): string;
begin
  Result := '';
  if not regUninstallKeyExists('{199505b0-b93d-4521-a8c7-897818e0205a}_is1') then
  begin
    Log('USBip uninstall key not found (version unavailable).');
    Exit;
  end;

  Result := regGetUninstallValue('{199505b0-b93d-4521-a8c7-897818e0205a}_is1', 'DisplayVersion');
  if Result = '' then
    Log('USBip DisplayVersion is empty or missing.');
end;


function PathContainsDirectory(const PathValue, Directory: string): Boolean;
var
  Entry, Remaining: string;
  Separator: Integer;
begin
  Result := False;
  Remaining := PathValue;

  while Remaining <> '' do
  begin
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Entry := Remaining;
      Remaining := '';
    end
    else
    begin
      Entry := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;

    Entry := RemoveBackslashUnlessRoot(Trim(RemoveQuotes(Entry)));
    if CompareText(Entry, RemoveBackslashUnlessRoot(Directory)) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;


procedure EnsureUSBipSystemPath;
var
  EnvironmentKey, SystemPath, USBipDirectory: string;
begin
  EnvironmentKey := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  USBipDirectory := ExpandConstant('{commonpf}\USBip');

  if not DirExists(USBipDirectory) then
  begin
    Log('USBip directory not found; creating: ' + USBipDirectory);
    if not ForceDirectories(USBipDirectory) then
    begin
      Log('Failed to create USBip directory: ' + USBipDirectory);
      Exit;
    end;
  end;

  if RegValueExists(HKLM, EnvironmentKey, 'Path') then
  begin
    if not RegQueryStringValue(HKLM, EnvironmentKey, 'Path', SystemPath) then
    begin
      Log('Unable to read the system Path environment variable.');
      Exit;
    end;
  end
  else
  begin
    Log('System Path environment variable not found; creating it.');
    SystemPath := '';
  end;

  if PathContainsDirectory(SystemPath, USBipDirectory) then
  begin
    Log('USBip directory already exists in the system Path: ' + USBipDirectory);
    Exit;
  end;

  Log('USBip directory not found in the system Path; adding: ' + USBipDirectory);
  if SystemPath <> '' then
  begin
    if SystemPath[Length(SystemPath)] <> ';' then
      SystemPath := SystemPath + ';';
  end;

  if not RegWriteExpandStringValue(HKLM, EnvironmentKey, 'Path', SystemPath + USBipDirectory) then
    Log('Failed to add the USBip directory to the system Path.');
end;


function GetUSBipExecutablePath(): string;
var
  installLocation, executablePath: string;
begin
  Result := '';

  installLocation := regGetUninstallValue('{199505b0-b93d-4521-a8c7-897818e0205a}_is1', 'InstallLocation');
  if installLocation <> '' then
  begin
    executablePath := AddBackslash(RemoveQuotes(installLocation)) + 'usbip.exe';
    if FileExists(executablePath) then
    begin
      Result := executablePath;
      Exit;
    end;
  end;

  executablePath := FileSearch('usbip.exe', GetEnv('PATH'));
  if executablePath <> '' then
    Result := executablePath;
end;


function ExecWithTimeout(const Filename, Parameters: string; TimeoutSeconds: Integer;
  var ResultCode: Integer): Boolean;
var
  PowerShellPath, ScriptBody, ScriptPath: string;
begin
  ScriptPath := ExpandConstant('{tmp}\HC_ExecWithTimeout.ps1');
  ScriptBody :=
    'param([string]$Executable, [string]$Arguments, [int]$TimeoutSeconds)' + #13#10 +
    '$ErrorActionPreference = ''Stop''' + #13#10 +
    'try {' + #13#10 +
    '  $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru' + #13#10 +
    '  if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {' + #13#10 +
    '    $killer = Start-Process -FilePath "$env:SystemRoot\System32\taskkill.exe" -ArgumentList "/PID $($process.Id) /T /F" -WindowStyle Hidden -PassThru' + #13#10 +
    '    if (-not $killer.WaitForExit(5000)) { $killer.Kill() }' + #13#10 +
    '    if (-not $process.HasExited) { $process.Kill() }' + #13#10 +
    '    exit 1460' + #13#10 +
    '  }' + #13#10 +
    '  exit $process.ExitCode' + #13#10 +
    '} catch {' + #13#10 +
    '  Write-Error $_' + #13#10 +
    '  exit 1' + #13#10 +
    '}' + #13#10;

  if not SaveStringToFile(ScriptPath, ScriptBody, False) then
  begin
    Log('Unable to create the process timeout helper.');
    ResultCode := 1;
    Result := False;
    Exit;
  end;

  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Result := Exec(
    PowerShellPath,
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath +
      '" -Executable "' + Filename + '" -Arguments "' + Parameters +
      '" -TimeoutSeconds ' + IntToStr(TimeoutSeconds),
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Result and (ResultCode = 0);
end;


function isHidHideInstalled():boolean;
begin
  result:= false;
  if(FileExists(ExpandConstant('{commonpf}') + '\Nefarius Software Solutions\HidHide\x64\HidHideClient.exe')) then
  begin
    log('HidHide is already installed.');
    result:= true; 
  end;
end;
     

function getInstalledHidHideVersion():string;
var
  versionNumber, filePath:string;
begin
  result:= '';
  filePath:= ExpandConstant('{commonpf}') + '\Nefarius Software Solutions\HidHide\x64\HidHideClient.exe';

  if(FileExists(filePath)) then
  begin 
    if(GetVersionNumbersString(filePath, versionNumber)) then   
      log('Found installed HidHide version: ' + versionNumber);
    result:= versionNumber;
  end;  
end;


function splitString(Text: String; Separator: String): TArrayOfString;
var
  i, p: Integer;
  Dest: TArrayOfString; 
begin
  i := 0;
  repeat
    SetArrayLength(Dest, i+1);
    p := Pos(Separator,Text);
    if p > 0 then begin
      Dest[i] := Copy(Text, 1, p-1);
      Text := Copy(Text, p + Length(Separator), Length(Text));
      i := i + 1;
    end else begin
      Dest[i] := Text;
      Text := '';
    end;
  until Length(Text)=0;
  Result := Dest
end;


function UninstallMsiByDisplayName(const DisplayName: String): Boolean;
var
  uninstallCommand: String;
  splittedCommand: TArrayOfString;
  resultCode: Integer;
begin
  Result := False;
  uninstallCommand := regGetAppUninstallStringByDisplayName(DisplayName);
  if uninstallCommand = '' then
  begin
    Log('No uninstall command found for ' + DisplayName);
    Exit;
  end;

  splittedCommand := splitString(uninstallCommand, ' ');

  if (GetArrayLength(splittedCommand) > 1) and not (splittedCommand[1] = '') then
  begin 
    if ShellExec('', 'msiexec.exe', splittedCommand[1] + ' /qn /norestart', '', SW_SHOW, ewWaitUntilTerminated, resultCode) then
    begin
      Log('Successfully executed uninstaller for ' + DisplayName);
      if resultCode = 0 then
      begin
        Log('Uninstaller finished successfully for ' + DisplayName);
        Result := True;
      end
      else
        Log('Uninstaller failed for ' + DisplayName + ' with exit code ' + IntToStr(resultCode));
    end
  end
  else
    Log('Unable to parse uninstall command for ' + DisplayName + ': ' + uninstallCommand);
end;


function uninstallHidHide():boolean;
begin
  Result := UninstallMsiByDisplayName('HidHide');
end;
