param([ValidateSet('start','status','download','visual','cancel')][string]$Action='status',[long]$RunId=0)
$ErrorActionPreference='Stop'
$credential="protocol=https`nhost=github.com`nusername=ampere112coulomb`n`n" | git -c credential.interactive=never credential fill 2>$null
$passwordLine=@($credential | Where-Object { $_ -like 'password=*' })
if(!$passwordLine.Count){throw 'GitHub credential unavailable'}
$headers=@{Authorization='Bearer '+$passwordLine[0].Substring(9);Accept='application/vnd.github+json';'User-Agent'='HyperLane-iOS-editor'}
$base='https://api.github.com/repos/ampere112coulomb/hyper-lane'
if($Action -eq 'start'){
 Invoke-RestMethod -Method Post "$base/actions/workflows/build-ios-editor.yml/dispatches" -Headers $headers -ContentType 'application/json' -Body '{"ref":"main"}' | Out-Null
 Write-Output 'Dispatched iOS editor build';exit
}
if($Action -eq 'cancel' -and !$RunId){throw 'Explicit run ID required'}
if(!$RunId){$runs=Invoke-RestMethod "$base/actions/workflows/build-ios-editor.yml/runs?per_page=3" -Headers $headers;$RunId=$runs.workflow_runs[0].id}
$run=Invoke-RestMethod "$base/actions/runs/$RunId" -Headers $headers
Write-Output "Run $RunId $($run.status) $($run.conclusion) $($run.head_sha)"
if($Action -eq 'cancel'){
 if($run.name -ne 'Build iOS editor'){throw 'Not an editor build'}
 if($run.status -eq 'completed'){exit}
 Invoke-RestMethod -Method Post "$base/actions/runs/$RunId/cancel" -Headers $headers | Out-Null
 Write-Output 'Cancelled superseded editor build';exit
}
if($Action -eq 'status'){
 $jobs=Invoke-RestMethod "$base/actions/runs/$RunId/jobs" -Headers $headers
 $jobs.jobs | ForEach-Object {$_.steps | Select-Object name,status,conclusion} | Format-Table -AutoSize
 if($run.conclusion -eq 'failure'){
  $zip=Join-Path $PSScriptRoot "../.local-build/run-$RunId-logs.zip"
  New-Item -ItemType Directory -Force (Split-Path $zip) | Out-Null
  Invoke-WebRequest "$base/actions/runs/$RunId/logs" -Headers $headers -OutFile $zip
  $out=Join-Path $PSScriptRoot "../.local-build/run-$RunId-logs"
  Expand-Archive $zip $out -Force
  Get-ChildItem $out -Recurse -Filter '*.txt' | Select-String 'error CS|error :|Build FAILED' | ForEach-Object {$_.Line}
 }
}
if($Action -in @('download','visual')){
 if($Action -eq 'download' -and $run.conclusion -ne 'success'){throw 'Build not successful'}
 $artifacts=Invoke-RestMethod "$base/actions/runs/$RunId/artifacts" -Headers $headers
 $name=if($Action -eq 'visual'){'iOS-editor-visual-check'}else{'HyperLane-Editor-iOS-unsigned'}
 $artifact=$artifacts.artifacts | Where-Object name -eq $name
 if(!$artifact){throw 'Build artifact not found'}
 $out=Join-Path $PSScriptRoot "../.local-build/$RunId-$Action"
 New-Item -ItemType Directory -Force $out | Out-Null
 $zip=Join-Path $out 'editor-artifact.zip'
 Invoke-WebRequest $artifact.archive_download_url -Headers $headers -OutFile $zip
 Expand-Archive $zip $out -Force
 Get-ChildItem $out | Select-Object FullName,Length
 if($Action -eq 'visual'){Get-ChildItem $out -Filter '*.json' | ForEach-Object {Get-Content $_.FullName}}
}
