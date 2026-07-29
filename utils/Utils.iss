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


function UninstallUSBip(): Boolean;
var
  uninstallString: string;
  resultCode: Integer;
begin
  Result := False;
  uninstallString := regGetUninstallValue('{199505b0-b93d-4521-a8c7-897818e0205a}_is1', 'UninstallString');
  if uninstallString = '' then
  begin
    Log('USBip UninstallString is empty or missing.');
    Exit;
  end;

  uninstallString := RemoveQuotes(uninstallString);
  Log('Running USBip uninstaller: ' + uninstallString);
  if ShellExec('', uninstallString, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-', '', SW_SHOWNORMAL, ewWaitUntilTerminated, resultCode) then
  begin
    Log('USBip uninstaller finished with exit code ' + IntToStr(resultCode));
    Result := resultCode = 0;
  end
  else
    Log('Unable to launch USBip uninstaller.');
end;


function isViGemInstalled():boolean;
begin
  result:= false;
  if(FileExists(ExpandConstant('{commonpf}') + '\Nefarius Software Solutions\ViGEm Bus Driver\vigembus.cat')) then
  begin
    log('ViGem is already installed.');
    result:= true;                       
  end;
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


function uninstallViGem():boolean;
begin
  Result := UninstallMsiByDisplayName('ViGEm Bus Driver');
end;