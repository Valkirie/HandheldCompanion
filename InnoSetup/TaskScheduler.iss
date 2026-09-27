[Code]
function HandheldCompanionTaskExists: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Query /TN "HandheldCompanion"',
	'',
	SW_HIDE,
	ewWaitUntilTerminated,
	ResultCode
  ) and (ResultCode = 0);
end;

procedure DisableHandheldCompanionTask;
var
  ResultCode: Integer;
begin
  if not HandheldCompanionTaskExists then
  begin
	Log('HandheldCompanion scheduled task was not found.');
	Exit;
  end;

  if Exec(
	ExpandConstant('{sys}\schtasks.exe'),
	'/Change /TN "HandheldCompanion" /DISABLE',
	'',
	SW_HIDE,
	ewWaitUntilTerminated,
	ResultCode
  ) and (ResultCode = 0) then
	Log('HandheldCompanion scheduled task disabled until the update completes.')
  else
	Log('Failed to disable the HandheldCompanion scheduled task. ExitCode=' + IntToStr(ResultCode));
end;
