$ErrorActionPreference = 'Stop'

$baseline = '91d3979fa45fcb03abd81a086b90a222da050fc5'
$mainline = 'db93cadd55cb9f199beb1cb1dff016bc0150048e'
$ea = '7aec7c3b4839d1e78b2f8212774ac4f5fe1b5b55'

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
	foreach ($revision in @($baseline, $mainline, $ea)) {
		git cat-file -e "$revision^{commit}"
		if ($LASTEXITCODE -ne 0) {
			throw "Missing revision $revision. Fetch the requested commit before running this check."
		}
	}

	$base = git merge-base $mainline $ea
	if ($LASTEXITCODE -ne 0) { throw 'Cannot find the shared ancestor.' }
	git merge-base --is-ancestor $baseline $base
	if ($LASTEXITCODE -ne 0) { throw 'The shared ancestor predates the requested baseline.' }

	$eaFiles = @(git diff --name-only $base $ea)
	if ($LASTEXITCODE -ne 0) { throw 'Cannot list EA changes.' }
	$mainlineFiles = @(git diff --name-only $base $mainline)
	if ($LASTEXITCODE -ne 0) { throw 'Cannot list mainline changes.' }
	$eaOnly = @($eaFiles | Where-Object { $_ -notin $mainlineFiles })
	$mainlineOnly = @($mainlineFiles | Where-Object { $_ -notin $eaFiles })
	$overlap = @($eaFiles | Where-Object { $_ -in $mainlineFiles })

	if ($eaOnly.Count -gt 0) {
		git diff --exit-code $ea -- @eaOnly
		if ($LASTEXITCODE -ne 0) { throw 'EA-only files differ from the requested release.' }
	}
	if ($mainlineOnly.Count -gt 0) {
		git diff --exit-code $mainline -- @mainlineOnly
		if ($LASTEXITCODE -ne 0) { throw 'Mainline-only fixes were modified.' }
	}

	if ($overlap.Count -gt 0) {
		$patch = [IO.Path]::GetTempFileName()
		try {
			foreach ($revision in @($mainline, $ea)) {
				git diff "--output=$patch" --unified=0 $base $revision -- @overlap
				if ($LASTEXITCODE -ne 0) { throw "Cannot generate the patch for $revision." }
				# Zero context permits nearby changes from the other branch; check never edits files.
				git apply --reverse --check --unidiff-zero --ignore-space-change $patch
				if ($LASTEXITCODE -ne 0) { throw "Overlapping changes from $revision are missing or altered." }
			}
		}
		finally {
			Remove-Item $patch
		}
	}

	git -c core.whitespace=cr-at-eol diff $mainline --check
	if ($LASTEXITCODE -ne 0) { throw 'The backport contains whitespace errors or conflict markers.' }
	Write-Host "PASS: $($eaOnly.Count) EA-only files, $($mainlineOnly.Count) mainline-only files, and $($overlap.Count) overlapping files verified."
}
finally {
	Pop-Location
}
