[Code]
//Prototypes
function regGetAppValue(rootKey:integer; appValue: string):string; forward;
      
      
function createOrUpdateStringKey(rootKey:integer; subKey:string; valueName:string; valueData:string; overwrite: boolean): integer;
var
  resultString: string;
begin
  if (not regQueryStringValue(rootKey, subKey, valueName, resultString) or (resultString = '') or (overwrite = true)) then
  begin
    log('Creating registry subkey ' +subKey + ' with value ' +valueName);
    if (regWriteStringValue(rootKey, subKey, valueName, valueData)) then
    begin
      if (overwrite) then
      begin
        log('SubKey "' +subKey + '" with value ' + '"' +valueName + '" was successfully updated.');
      end
      else
      begin
        log('SubKey "' +subKey + '" with value ' + '"' +valueName + '" was successfully created.');
      end;
      result:= 1;
    end
    else
    begin
      log('Failed to create subkey "' +subKey+ '" with value "' +valueName+ '"');
      result:= -1;
    end;
  end
  else
  begin
    result:= 0;
    log('SubKey "' +subKey + '" with value ' + '"' +valueName + '" already exists.');
  end;
end;


procedure regSetVersion(appName, version:string);
begin
  createOrUpdateStringKey(HKA, '{#RegAppsPath}', appName + ' Version', version, true); 
end;
     

function regGetInstalledVersion(appName: string):string;
begin
  result:= '';    
  result:= regGetAppValue(HKA, appName + ' Version');
	if((result = '') and isWin64) then
	begin
		result:= regGetAppValue(HKA32, appName + ' Version');
	end;
end;           
      

function regGetAppValue(rootKey:integer; appValue: string):string;
var regAppValue:string;
begin
  log('***Enter regGetAppValue()***');
  if(regQueryStringValue(rootKey, '{#RegAppsPath}\', appValue, regAppValue)) then
  begin
    log('Successfully read key ' + appValue + ' with value ' +regAppValue + ' from registry.');
    result:= regAppValue;
  end
  else
  begin
    log('Unable to read {#RegAppsPath}\' +appValue + ' from registry.');
    result:= '';
  end;
  log('!!*Leave regGetAppValue()*!!');
end;


function getStringValueFromReg(rootKey:integer; keyName, valueName:string):string;
var regResult, regPath:string;
begin
  log('***Enter getStringValueFromReg()***');
  regPath:= 'HKA\' + keyName;
  if(regQueryStringValue(rootKey, keyName, valueName, regResult)) then
  begin		
    log('Successfully read ' +valueName+ ' from registry path "' +regPath+ '".');
    result:= regResult;
  end
  else
  begin
    log('Unable to read the value ' +valueName+ ' from registry path "' +regPath+ '".');
    result:= '';
  end;
  log('!!*Leave getStringValueFromReg()*!!');
end;       


function regGetSubkeys(keyPath:string):TArrayOfString;
var
  subkeyNames: TArrayOfString;
begin
  result:= subkeyNames;
  if RegGetSubkeyNames(HKLM, keyPath, subkeyNames) then
  begin
    result:= subkeyNames;
  end 
  else
  begin
    log('Failed to get subkeys from HKLM\' +keyPath);
  end;
end;


function regGetAppUninstallStringByDisplayName(appId:string):string;
var
  subkeyNames: TArrayOfString;
  currentSubkey, uninstallString: string;
  displayName: string;
  i: integer;
begin   
  subkeyNames:= regGetSubkeys('{#SoftwareUninstallKey}');   
  currentSubkey := '';  

  for i:= 0 to GetArrayLength(subkeyNames)-1 do
  begin
    currentSubkey:= subkeyNames[i];
    displayName:= getStringValueFromReg(HKLM, '{#SoftwareUninstallKey}' + '\' + currentSubkey, 'DisplayName');
    log('Current display name: ' +displayName);

    if(displayName = appid) then
    begin
      log('Successfully found AppId ' +displayName);    
      uninstallString:= getStringValueFromReg(HKLM, '{#SoftwareUninstallKey}' + '\' + currentSubkey, 'UninstallString');
      log('Successfully found uninstall path: ' +uninstallString);
      result:= uninstallString;
      break;
    end; 
  end;  
end;
     


