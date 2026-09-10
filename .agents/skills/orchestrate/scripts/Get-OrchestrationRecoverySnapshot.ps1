[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RunId,
    [string] $RepositoryRoot,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$capsuleHelper = Join-Path $PSScriptRoot 'Invoke-OrchestrationCapsuleHook.ps1'
$resourceHelper = Join-Path $PSScriptRoot 'Get-OrchestrationResourceSnapshot.ps1'
$validation = (& $capsuleHelper -Mode Validate -RunId $RunId -RepositoryRoot $RepositoryRoot) | ConvertFrom-Json

$branch = (& git -C $RepositoryRoot branch --show-current 2>$null | Select-Object -First 1)
$head = (& git -C $RepositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1)
$status = @(& git -C $RepositoryRoot status --short 2>$null)
$gitAvailable = $LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace([string] $head)

$worktreeOutput = @(& git -C $RepositoryRoot worktree list --porcelain 2>$null)
$worktreeAvailable = $LASTEXITCODE -eq 0
$worktrees = [System.Collections.Generic.List[object]]::new()
$current = $null
foreach ($line in $worktreeOutput) {
    if ($line -like 'worktree *') {
        if ($null -ne $current) { $worktrees.Add([pscustomobject] $current) }
        $current = [ordered] @{ path = $line.Substring(9); head = $null; branch = $null; bare = $false; detached = $false }
    }
    elseif ($null -ne $current -and $line -like 'HEAD *') { $current.head = $line.Substring(5) }
    elseif ($null -ne $current -and $line -like 'branch *') { $current.branch = $line.Substring(7) }
    elseif ($null -ne $current -and $line -eq 'bare') { $current.bare = $true }
    elseif ($null -ne $current -and $line -eq 'detached') { $current.detached = $true }
}
if ($null -ne $current) { $worktrees.Add([pscustomobject] $current) }

$reservations = if ($validation.valid) { @($validation.capsule.resource_state.worktree_reservations) } else { @() }
$outstandingReservationGiB = if ($validation.valid) {
    [double] (($reservations | Measure-Object -Property growth_reservation_gib -Sum).Sum)
}
else {
    0.0
}
$heavyReservations = if ($validation.valid) {
    @($validation.capsule.resource_state.active_heavy_reservations)
}
else { @() }
$activeHeavyProfiles = @($heavyReservations).Count
$activeMemoryReservationsGiB = [double] (
    ($heavyReservations | Measure-Object -Property memory_reservation_gib -Sum).Sum)
$activeWorkerFanout = [int] (
    ($heavyReservations | Measure-Object -Property worker_cap -Sum).Sum)
$activeExclusive = @($heavyReservations | Where-Object {
    $_.PSObject.Properties.Name -contains 'exclusive' -and [bool] $_.exclusive
}).Count -gt 0
$resourceArguments = @{
    Admission = 'Snapshot'
    RepositoryRoot = $RepositoryRoot
    RunId = $RunId
    OutstandingWorktreeReservationsGiB = $outstandingReservationGiB
    ActiveHeavyProfiles = $activeHeavyProfiles
    ActiveMemoryReservationsGiB = $activeMemoryReservationsGiB
    ActiveWorkerFanout = $activeWorkerFanout
    ActiveExclusiveOperation = $activeExclusive
}
if ($validation.valid -and $worktreeAvailable) {
    $resourceArguments.WorktreeInventoryConfirmed = $true
}
$resource = & $resourceHelper @resourceArguments

$snapshot = [pscustomobject] [ordered] @{
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    run_id = $RunId
    recovery = [pscustomobject] [ordered] @{
        mode = [string] $validation.recovery_mode
        code = [string] $validation.code
        message = [string] $validation.message
    }
    git = [pscustomobject] [ordered] @{
        available = $gitAvailable
        branch = [string] $branch
        head = [string] $head
        status = @($status)
    }
    worktrees = [pscustomobject] [ordered] @{
        available = $worktreeAvailable
        inventory = @($worktrees)
        reservations = @($reservations)
        outstanding_growth_reservation_gib = $outstandingReservationGiB
    }
    resource = $resource
    durable_heavy_reservations = @($heavyReservations)
}

if ($AsJson) {
    $snapshot | ConvertTo-Json -Depth 10
}
else {
    $snapshot
}
