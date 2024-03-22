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


function isHidHideInstalled():boolean;
begin
  result:= false;
  if(FileExists(ExpandConstant('{commonpf}') + '\Nefarius Software Solutions\HidHide\HidHide_Updater.exe')) then
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
  filePath:= ExpandConstant('{commonpf}') + '\Nefarius Software Solutions\HidHide\HidHide_Updater.exe';

  if(FileExists(filePath)) then
  begin 
    if(GetVersionNumbersString(filePath, versionNumber)) then   
      log('Found installed HidHide version: ' + versionNumber);
    result:= versionNumber;
  end;  
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


function uninstallHidHide():boolean;
var
  uninstallCommand:string;
  splittedCommand:TArrayOfString;
  resultCode:integer;
begin
  result:= false;
  uninstallCommand:= regGetAppUninstallStringByDisplayName('HidHide');
  splittedCommand:= splitString(uninstallCommand, ' ');

  if((getArrayLength(splittedCommand) > 1) and not(splittedCommand[1] = '')) then
  begin 
    if(ShellExec('', 'msiexec.exe', splittedCommand[1] + ' /qn /norestart' , '', SW_SHOW, ewWaitUntilTerminated, resultCode)) then  
    begin
      log('Successfully executed Hidhide uninstaller');
      if(resultCode = 0) then
      begin
        log('Hidhide uninstaller finished successfully');
        result:= true;
      end
      else
        log('Hidhide uninstaller failed with exit code ' +intToStr(resultCode));
    end 
  end;  
end;


function uninstallViGem():boolean;
var
  uninstallCommand:string;
  splittedCommand:TArrayOfString;
  resultCode:integer;
begin
  result:= false;
  uninstallCommand:= regGetAppUninstallStringByDisplayName('ViGEm Bus Driver');
  splittedCommand:= splitString(uninstallCommand, ' ');

  if((getArrayLength(splittedCommand) > 1) and not(splittedCommand[1] = '')) then
  begin 
    if(ShellExec('', 'msiexec.exe', splittedCommand[1] + ' /qn /norestart' , '', SW_SHOW, ewWaitUntilTerminated, resultCode)) then  
    begin
      log('Successfully executed ViGem uninstaller');
      if(resultCode = 0) then
      begin
        log('ViGem uninstaller finished successfully');
        result:= true;
      end
      else
        log('ViGem uninstaller failed with exit code ' +intToStr(resultCode));
    end 
  end;  
end;