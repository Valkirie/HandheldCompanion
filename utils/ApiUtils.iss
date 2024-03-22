[Code]
const
  //$=hex_value
  SMTO_BLOCK = 1;
  WM_WININICHANGE = $001A;
  WM_SETTINGCHANGE = WM_WININICHANGE;
  NET_FW_SCOPE_ALL = 0;
  NET_FW_IP_VERSION_ANY = 2;
  NET_FW_PROTOCOL_TCP = 6;
  NET_FW_PROTOCOL_UDP = 17;
  NET_FW_PROFILE2_DOMAIN  = 1;
	NET_FW_PROFILE2_PRIVATE = 2;
  NET_FW_PROFILE2_PUBLIC =4;
  NET_FW_ACTION_ALLOW = 1;

type
  TShellExecuteInfo = record
    cbSize: DWORD;  // dword = 32-Bit integer
    fMask: Cardinal; //alias for dword
    Wnd: HWND;
    lpVerb: string;
    lpFile: string;
    lpParameters: string;
    lpDirectory: string;
    nShow: Integer;
    hInstApp: THandle;
    lpIDList: DWORD;
    lpClass: string;
    hkeyClass: THandle;
    dwHotKey: DWORD;
    hMonitor: THandle;
    hProcess: THandle;
  end;

  WPARAM = UINT_PTR;
  LPARAM = INT_PTR;
  LRESULT = INT_PTR;

	DWORDLONG = Int64;
	TMemoryStatusEx = record
    dwLength: DWORD;
    dwMemoryLoad: DWORD;
    ullTotalPhys: DWORDLONG;
    ullAvailPhys: DWORDLONG;
    ullTotalPageFile: DWORDLONG;
    ullAvailPageFile: DWORDLONG;
    ullTotalVirtual: DWORDLONG;
    ullAvailVirtual: DWORDLONG;
    ullAvailExtendedVirtual: DWORDLONG;
  end;

	TSysTime = record
    Year:         WORD;
    Month:        WORD;
    DayOfWeek:    WORD;
    Day:          WORD;
    Hour:         WORD;
    Minute:       WORD;
    Second:       WORD;
    Milliseconds: WORD;
  end;

  TProcessEntry = record
    pid: DWORD;
    Name: string;
    ExecutablePath: string;
  end;
  TProcessList = array of TProcessEntry;



function isAppRunning(const FileName : string): integer;
var
    FSWbemLocator: Variant;
    FWMIService   : Variant;
    FWbemObjectSet: Variant;
begin
  log('***Enter isAppRunning***');
  try
    FSWbemLocator := CreateOleObject('WBEMScripting.SWBEMLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    FWbemObjectSet := FWMIService.ExecQuery(Format('SELECT Name FROM Win32_Process Where Name="%s"',[FileName]));
    if(FWbemObjectSet.Count > 0) then
    begin
      result:= 0;
      log('Successfully found running process ' +fileName);
    end
    else
    begin
      result:= 1;
      log('No process with name ' +fileName+ ' was found');
    end;
    FWbemObjectSet := Unassigned;
    FWMIService := Unassigned;
    FSWbemLocator := Unassigned;
  except
    log('Query "SELECT Name FROM Win32_Process Where ..." failed with exception: ' +addPeriod(GetExceptionMessage));
    result:= -1;
  end;
  log('!!!Leave isAppRunning!!!');
end;


function isProcessRunning(processName:string):boolean;
var
  resultCode, processStatus:integer;
begin
  log('***Enter isProcessRunning()***');
  processStatus:= isAppRunning(processName);
  result:= true;

  if(processStatus = 0) then
  begin
    result:= true;
  end
  else if (processStatus = 1) then
  begin
    result:= false;
  end
  else if(processStatus = -1) then
  begin
    log('The service "Windows-Verwaltungsinstrumentation" is not running properly!');
    //Cut .exe file extension because tasklist is listing wrapped jar file without ending
    stringChangeEx(processName, '.exe', '', true);

    if(exec(expandConstant('{cmd}'), '/C tasklist /v | findstr ' + processName, '', SW_HIDE, ewWaitUntilTerminated, resultCode)) then
      log('Sucessfully executed tasklist query on process ' +processName+ ' with resultCode: ' +intToStr(resultCode));

    if(resultCode = 0) then
    begin
      result:= true;
      Log('Process ' +processName+ ' is running.');
    end
    else if (resultCode = 1) then
    begin
      result:= false;
      Log('Process ' +processName+ ' is not running or cannot be queried.');
    end
    else
    begin
      result:= false;
      Log('Process ' +processName+ ' status cannot be queried.');
    end;
  end;
  log('!!!Leave isProcessRunning()!!!');
end;


//Stops process with name 'nameOfFile'
//Stops parent process of the app and all child processes (e.g. javaw.exe)
function stopProcess(nameOfFile:string):boolean;
var resultCode: integer;
begin
  log('***Enter stopProcess()***');
  //Kill process in any case (/F) with given image name (/IM) and all child process (/T)
  //Allow resultCode 128 - program is not existing
  if (exec(expandConstant('{cmd}'), '/C taskkill /IM ' +nameOfFile+ ' /F /T', '', SW_HIDE, ewWaitUntilTerminated, resultCode) AND ((resultCode=0) or (resultCode=128))) then
  begin
    result:= true;
    log('Successfully stopped "' +nameOfFile+ '". ResultCode: ' +intToStr(resultCode));
  end
  else
  begin
    result:= false;
    log('XXX--Failed to stop "' +nameOfFile+ '". No program with this name could be found. ResultCode: ' +intToStr(resultCode)+  ' --XXX');
  end;

  //Give process some time to stop
  sleep(2000);
  log('!!*Leave stopProcess()*!!');
end;



function stopProcessById(processId:string):boolean;
var resultCode: integer;
begin
  log('***Enter stopProcessById()***');
  //Kill process in any case (/F) and all child process (/T)
  //Allow resultCode 128 - program is not existing
  if (exec(expandConstant('{cmd}'), '/C taskkill /F /T /PID ' +processId , '', SW_HIDE, ewWaitUntilTerminated, resultCode) AND ((resultCode=0) or (resultCode=128))) then
  begin
    result:= true;
    log('Successfully stopped process with id "' +processId+ '". ResultCode: ' +intToStr(resultCode));
  end
  else
  begin
    result:= false;
    log('XXX--Failed to stop process with id "' +processId+ '". No process with this id could be found. ResultCode: ' +intToStr(resultCode)+  ' --XXX');
  end;

  //Give process some time to stop
  sleep(2000);
  log('!!*Leave stopProcessById()*!!');
end;


function getProcessId(const FileName : string; out process: TProcessEntry): integer;
var
    FSWbemLocator: Variant;
    FWMIService   : Variant;
    FWbemObject: Variant;
    FWbemObjectSet: Variant;
begin
    log('***Enter getProcessId***');
    Result := 0;
    try
      FSWbemLocator := CreateOleObject('WBEMScripting.SWBEMLocator');
      FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
      FWbemObjectSet := FWMIService.ExecQuery(Format('SELECT ProcessId, Name, ExecutablePath FROM Win32_Process Where Name="%s"',[FileName]));

      Result := FWbemObjectSet.Count;
      if(result > 0) then
      begin
        FWbemObject := FWbemObjectSet.ItemIndex(0);
        if not VarIsNull(FWbemObject) then
        begin
          process.pid := FWbemObject.ProcessId;
          process.name := FWbemObject.Name;
          process.ExecutablePath := FWbemObject.ExecutablePath;
          log('Found parent process "' +process.name+ '" with id ' +intToStr(process.pid));
        end;
      end;
    except
      log('Query "SELECT ProcessId FROM Win32_Process..." failed with exception: ' +addPeriod(GetExceptionMessage));
    end;
    log('!!*Leave getProcessId()*!!');
end;


function getProcessIdByName(const fileName: string; exactSearch:boolean; out process: TProcessEntry): boolean;
var
    FSWbemLocator: Variant;
    FWMIService   : Variant;
    FWbemObject: Variant;
    FWbemObjectSet: Variant;
		i:integer;
begin
    log('***Enter getProcessIdByExePath***');
    result := false;
    try
      FSWbemLocator := CreateOleObject('WBEMScripting.SWBEMLocator');
      FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
      if(exactSearch) then
				FWbemObjectSet := FWMIService.ExecQuery(Format('SELECT ProcessId, Name, ExecutablePath FROM Win32_Process Where Name="%s"',[fileName]))
			else
				FWbemObjectSet := FWMIService.ExecQuery('SELECT ProcessId, Name, ExecutablePath FROM Win32_Process Where Name like "%' +fileName+ '%"');

      Result := FWbemObjectSet.Count;
      for i:= 0 to FWbemObjectSet.Count - 1 do
      begin
        FWbemObject := FWbemObjectSet.ItemIndex(i);
        if not VarIsNull(FWbemObject) then
        begin
          process.pid := FWbemObject.ProcessId;
          process.name := FWbemObject.Name;
          process.ExecutablePath := FWbemObject.ExecutablePath;
          log('Found process "' +process.executablePath+ '" with id ' +intToStr(process.pid));
					if(process.name = fileName) then
					begin
						log('Found matching executable path "' +process.ExecutablePath+ '" with id ' +intToStr(process.pid));
						result:= true;
						exit;
					end;
        end;
      end;
    except
      log('Query "SELECT ExecutablePath FROM Win32_Process..." failed with exception: ' + addPeriod(GetExceptionMessage));
    end;
		log('Failed to find process with name "' +fileName);
    log('!!*Leave getProcessIdByExePath()*!!');
end;


procedure addPortRuleCmd(ruleName, port, ipProtocol:string);
var
	addRuleCommand:string;
	resultCode: integer;
begin
  log('***Enter addPortRuleCmd***');
	addRuleCommand:= 'netsh advfirewall firewall add rule name="' +ruleName+ '" dir=in action=allow protocol=' +ipProtocol+ ' localport=' +port;
	exec(ExpandConstant('{cmd}'), '/C ' + addRuleCommand, '', SW_SHOW, ewWaitUntilTerminated, ResultCode);

	if(resultCode = 0) then
		log('Successfully created "' + ruleName + '" firewall rule.')
	else
		log('Failed to create firewall rule with command: "' +addRuleCommand+ '"');

  log('!!*Leave addPortRuleCmd()*!!');
end;


procedure addFirewallProgramRuleCmd(ruleName:string; applicationPath:string; ipProtocol:string);
var
	addRuleCommand:string;
	resultCode: integer;
begin
  log('***Enter addFirewallProgramRuleCmd***');

	addRuleCommand:= 'netsh advfirewall firewall add rule name="' +ruleName+ '" dir=in action=allow protocol=' +ipProtocol+ ' program="' +applicationPath+ '" enable=yes';

	exec(ExpandConstant('{cmd}'), '/C ' + addRuleCommand, '', SW_SHOW, ewWaitUntilTerminated, ResultCode);

	if(resultCode = 0) then
		log('Successfully created "' + ruleName + '" firewall rule.')
	else
		log('Failed to create firewall rule with command: "' +addRuleCommand+ '"');

  log('!!*Leave addFirewallProgramRuleCmd()*!!');
end;


procedure removeRuleFromFirewall(ruleName: string);
var
  FirewallPolicy: Variant;
begin
try
  FirewallPolicy := CreateOleObject('HNetCfg.FwPolicy2');
  FirewallPolicy.Rules.Remove(ruleName);
  log('Successfully removed "' + ruleName + '" from incoming rules list.');
except
  log('Failed to remove firewall rule!');
  end;
end;
