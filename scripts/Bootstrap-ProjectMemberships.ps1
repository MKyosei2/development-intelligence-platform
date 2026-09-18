# Purpose: Automates the DIP workflow represented by B oo ts tr ap P ro je ct Me mb er sh ip s.
[CmdletBinding(DefaultParameterSetName='Selected')]
param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)][string]$AdminBearerToken,
    [Parameter(Mandatory=$true)][string]$SubjectId,
    [Parameter(Mandatory=$true)][string]$Login,
    [Parameter(ParameterSetName='Selected',Mandatory=$true)][Guid[]]$ProjectIds,
    [Parameter(ParameterSetName='All',Mandatory=$true)][switch]$AllProjects,
    [string]$Reason='Project membership bootstrap after authorization migration'
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($SubjectId)){throw 'SubjectId is required.'}
if([string]::IsNullOrWhiteSpace($Login)){throw 'Login is required.'}
if([string]::IsNullOrWhiteSpace($Reason)){throw 'Reason is required for the audit trail.'}
$base=$BaseUrl.TrimEnd('/')
$headers=@{Authorization="Bearer $AdminBearerToken"}
if($AllProjects){
    $projects=@(Invoke-RestMethod -Method Get -Uri "$base/api/projects" -Headers $headers)
    $ProjectIds=@($projects | ForEach-Object {[Guid]$_.id})
}
if(-not $ProjectIds -or $ProjectIds.Count -eq 0){throw 'No projects were selected.'}
foreach($projectId in ($ProjectIds | Select-Object -Unique)){
    $escaped=[Uri]::EscapeDataString($SubjectId)
    $body=@{subjectId=$SubjectId;login=$Login;role='Owner';reason=$Reason}|ConvertTo-Json -Compress
    Invoke-RestMethod -Method Put -Uri "$base/api/v1/projects/$projectId/memberships/$escaped" -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
    Write-Host "Bootstrapped Owner membership: project=$projectId subject=$SubjectId login=$Login"
}
Write-Host "Bootstrap completed for $($ProjectIds.Count) project(s). Each change is recorded in the governance audit trail."
