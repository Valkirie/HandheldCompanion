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

function DisableHandheldCompanionTask: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not HandheldCompanionTaskExists then
  begin
	Log('HandheldCompanion scheduled task was not found.');
	Exit;
  end;

  ResultCode := -1;
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
  begin
	Result := False;
	Log('Failed to disable the HandheldCompanion scheduled task. ExitCode=' + IntToStr(ResultCode));
  end;
end;
