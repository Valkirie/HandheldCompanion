[Code]
function ScheduledTaskExists(const TaskName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Query /TN "' + TaskName + '"',
	'', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function CreateSystemStartupTask(const TaskName, CommandPath: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Create /TN "' + TaskName + '" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR "' +
	  CommandPath + '" /F',
	'', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);

  if not Result then
	Log('Failed to create scheduled task "' + TaskName + '". ExitCode=' + IntToStr(ResultCode));
end;

function DisableScheduledTaskIfExists(const TaskName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not ScheduledTaskExists(TaskName) then
  begin
	Log('Scheduled task "' + TaskName + '" does not exist; no startup suppression is required.');
	Exit;
  end;

  Result := Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Change /TN "' + TaskName + '" /DISABLE',
	'', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);

  if Result then
	Log('Disabled scheduled task "' + TaskName + '".')
  else
	Log('Failed to disable scheduled task "' + TaskName + '". ExitCode=' + IntToStr(ResultCode));
end;

procedure DeleteScheduledTask(const TaskName: String);
var
  ResultCode: Integer;
begin
  if not ScheduledTaskExists(TaskName) then
	Exit;

  if not Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Delete /TN "' + TaskName + '" /F',
	'', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
	Log('Failed to delete scheduled task "' + TaskName + '". ExitCode=' + IntToStr(ResultCode));
end;
