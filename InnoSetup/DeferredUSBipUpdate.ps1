param(
	[Parameter(Mandatory = $true)]
	[ValidateSet('Update', 'Continue')]
	[string]$Mode,

	[Parameter(Mandatory = $true)]
	[string]$StageDirectory,

	[string]$UpdateTaskName
)

$ErrorActionPreference = 'Stop'
$completionPath = Join-Path $StageDirectory 'completed.status'
$logPath = Join-Path $StageDirectory 'update.log'
$setupPath = Join-Path $StageDirectory 'HandheldCompanion-Setup.exe'
$setupArgumentsPath = Join-Path $StageDirectory 'setup.arguments.txt'

function Write-UpdateLog([string]$Message) {
	Add-Content -LiteralPath $logPath -Value "$(Get-Date -Format o) $Message"
}

function Invoke-USBipUpdate {
	$usbipInstallerPath = Join-Path $StageDirectory 'USBip-Update.exe'
	Write-UpdateLog 'Installing the staged USBIP update.'
	$process = Start-Process -FilePath $usbipInstallerPath -ArgumentList '/VERYSILENT /COMPONENTS=main,client /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
	if ($process.ExitCode -notin 0, 3010) {
		throw "USBIP installation failed with exit code $($process.ExitCode)."
	}
}

if ($Mode -eq 'Continue') {
	$deadline = (Get-Date).AddMinutes(15)
	while (-not (Test-Path -LiteralPath $completionPath) -and (Get-Date) -lt $deadline) {
		Start-Sleep -Seconds 2
	}

	$arguments = if (Test-Path -LiteralPath $setupArgumentsPath) {
		Get-Content -LiteralPath $setupArgumentsPath -Raw
	} else {
		'/usbip-resume=1'
	}
	$setupProcess = Start-Process -FilePath $setupPath -ArgumentList $arguments -PassThru
	$setupProcess.WaitForExit()
	if ((Get-Content -LiteralPath $completionPath -Raw -ErrorAction SilentlyContinue).Trim() -eq 'Success') {
		Start-Sleep -Seconds 2
		Remove-Item -LiteralPath $StageDirectory -Recurse -Force -ErrorAction SilentlyContinue
	}
	exit 0
}

$updateSucceeded = $false
try {
	Write-UpdateLog 'Starting deferred USBIP update.'
	Invoke-USBipUpdate
	$updateSucceeded = $true
	Write-UpdateLog 'Deferred USBIP update completed successfully.'
}
catch {
	Write-UpdateLog "Deferred USBIP update failed: $($_.Exception.Message)"
}
finally {
	Set-Content -LiteralPath $completionPath -Value $(if ($updateSucceeded) { 'Success' } else { 'Failed' }) -Encoding ASCII
	if ($UpdateTaskName) {
		& "$env:SystemRoot\System32\schtasks.exe" /Delete /TN $UpdateTaskName /F | Out-Null
	}
}
