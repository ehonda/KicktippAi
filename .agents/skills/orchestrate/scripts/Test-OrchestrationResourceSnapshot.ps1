[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Get-OrchestrationResourceSnapshot.ps1'
$breaker = Join-Path $PSScriptRoot 'Set-OrchestrationMemoryCircuitBreaker.ps1'

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function New-Sample {
    param(
        [double] $AvailableMemoryGiB = 3.06,
        [int] $LogicalProcessors = 8,
        [double] $FreeDiskGiB = 50,
        [double] $TotalDiskGiB = 100,
        [bool] $InventoryConfirmed = $true,
        [double] $OutstandingReservationsGiB = 0,
        [bool] $CircuitBreakerActive = $false
    )
    return @{
        FreeDiskGiB = $FreeDiskGiB
        TotalDiskGiB = $TotalDiskGiB
        AvailableMemoryGiB = $AvailableMemoryGiB
        LogicalProcessors = $LogicalProcessors
        LinkedTaskWorktrees = 2
        WorktreeInventoryConfirmed = $InventoryConfirmed
        OutstandingWorktreeReservationsGiB = $OutstandingReservationsGiB
        MemoryCircuitBreakerActive = $CircuitBreakerActive
        MemoryCircuitBreakerReason = if ($CircuitBreakerActive) { 'synthetic pressure failure' } else { $null }
    }
}

$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../resources/resource-policy.json') -Raw | ConvertFrom-Json
Assert-True ($policy.schemaVersion -eq 4) 'resource policy schema must be version 4'
Assert-True ($policy.heavyOperation.PSObject.Properties.Name -notcontains 'concurrentLimit') 'fixed heavy-operation count must be absent'
Assert-True ($policy.heavyOperation.preferredAvailableMemoryFloorGiB -eq 1.0) 'preferred floor must be 1.0 GiB'
Assert-True ($policy.heavyOperation.absoluteAvailableMemoryFloorGiB -eq 0.5) 'absolute floor must be 0.5 GiB'
Assert-True ($policy.heavyOperation.provisionalProjectGateReservationGiB -eq 0.85) 'project gate reservation must be 0.85 GiB'

$missingInventory = & $helper -Admission Worktree -Sample (New-Sample -InventoryConfirmed $false)
Assert-True (-not $missingInventory.WorktreeAdmission.Allowed) 'missing inventory must gate worktree creation'
$diskDenied = & $helper -Admission Worktree -Sample (
    New-Sample -FreeDiskGiB 15 -OutstandingReservationsGiB 0.5)
Assert-True (-not $diskDenied.WorktreeAdmission.Allowed) 'post-reservation disk below 14 GiB must deny'
$parked = & $helper -Admission Worktree -ProposedWorktreeClass ParkedRecoveryOnly -Sample (
    New-Sample -FreeDiskGiB 14)
Assert-True $parked.WorktreeAdmission.Allowed 'parked worktree must reserve no growth'
$active = & $helper -Admission Worktree -Sample (New-Sample)
Assert-True ($active.WorktreeAdmission.ProposedReservedGiB -eq 1.25) 'active worktree must reserve configured growth'

$twoPreferred = & $helper -Admission Heavy -RequestedMemoryReservationGiB 1.70 `
    -RequestedWorkerFanout 4 -Sample (New-Sample)
Assert-True $twoPreferred.HeavyOperationAdmission.Allowed 'two project gates must fit above the preferred floor'
Assert-True ($twoPreferred.HeavyOperationAdmission.EffectiveFloorGiB -eq 1.0) 'ordinary admission must keep preferred floor'
$threePreferred = & $helper -Admission Heavy -RequestedMemoryReservationGiB 2.55 `
    -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True (-not $threePreferred.HeavyOperationAdmission.Allowed) 'three project gates must not silently cross preferred floor'
$threeExperimental = & $helper -Admission Heavy -MandatoryRecoverableLocal `
    -RequestedMemoryReservationGiB 2.55 -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True $threeExperimental.HeavyOperationAdmission.Allowed '3.06 GiB must admit three 0.85 GiB mandatory recoverable gates'
Assert-True $threeExperimental.HeavyOperationAdmission.UsesExperimentalFloor 'three-gate case must identify experimental use'
Assert-True ($threeExperimental.HeavyOperationAdmission.PostAdmissionMemoryGiB -eq 0.01) 'three-gate case must leave measured 0.01 GiB above floor'
$external = & $helper -Admission Heavy -MandatoryRecoverableLocal `
    -HasExternalOrLiveSideEffects -RequestedMemoryReservationGiB 2.55 `
    -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True (-not $external.HeavyOperationAdmission.Allowed) 'external/live work must not use experimental floor'

$cpuDenied = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -ActiveWorkerFanout 7 -RequestedWorkerFanout 2 -Sample (New-Sample)
Assert-True (-not $cpuDenied.HeavyOperationAdmission.Allowed) 'worker fanout above logical processors must deny'
$unknownAlone = & $helper -Admission Heavy -UnmeasuredOperation -Sample (New-Sample)
Assert-True $unknownAlone.HeavyOperationAdmission.Allowed 'unmeasured operation must use the full pool when alone'
Assert-True $unknownAlone.HeavyOperationAdmission.ExclusiveRequest 'unmeasured operation must be exclusive'
Assert-True ($unknownAlone.HeavyOperationAdmission.RequestedWorkerFanout -eq 8) 'unmeasured operation may use all controlled CPUs'
$unknownOverlap = & $helper -Admission Heavy -UnmeasuredOperation `
    -ActiveHeavyProfiles 1 -ActiveMemoryReservationsGiB 0.5 -ActiveWorkerFanout 1 `
    -Sample (New-Sample)
Assert-True (-not $unknownOverlap.HeavyOperationAdmission.Allowed) 'unmeasured operation must not overlap an active profile'
$exclusiveBackfill = & $helper -Admission Heavy -Backfill -ActiveExclusiveOperation `
    -RequestedMemoryReservationGiB 0.85 -RequestedWorkerFanout 1 -Sample (New-Sample)
Assert-True (-not $exclusiveBackfill.HeavyOperationAdmission.Allowed) 'active exclusive operation must block backfill'
$safeBackfill = & $helper -Admission Heavy -Backfill -ActiveHeavyProfiles 1 `
    -ActiveMemoryReservationsGiB 0.85 -ActiveWorkerFanout 2 `
    -RequestedMemoryReservationGiB 0.85 -RequestedWorkerFanout 2 `
    -Sample (New-Sample -AvailableMemoryGiB 4)
Assert-True $safeBackfill.HeavyOperationAdmission.Allowed 'fresh capacity must permit conservative backfill'
$missingMemory = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 50; TotalDiskGiB = 100; AvailableMemoryGiB = $null
    LogicalProcessors = 8; LinkedTaskWorktrees = 0; WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 0; MemoryCircuitBreakerActive = $false
}
Assert-True (-not $missingMemory.HeavyOperationAdmission.Allowed) 'missing memory must gate heavy admission'

$degraded = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 2 -Sample (New-Sample -CircuitBreakerActive $true)
Assert-True (-not $degraded.HeavyOperationAdmission.Allowed) 'degraded mode must reject fanout above one'
$degradedAllowed = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 1 -Sample (New-Sample -CircuitBreakerActive $true)
Assert-True $degradedAllowed.HeavyOperationAdmission.Allowed 'degraded mode must continue one recoverable low-fanout gate'
Assert-True ($degradedAllowed.HeavyOperationAdmission.EffectiveFloorGiB -eq 1.0) 'degraded mode must restore preferred floor'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-resource-$([Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.agents/skills/orchestrate/scripts') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.agents/skills/orchestrate/resources') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'KicktippAi.slnx') -Value '<Solution />'
    Copy-Item -LiteralPath $helper -Destination (Join-Path $testRoot '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1')
    Copy-Item -LiteralPath $breaker -Destination (Join-Path $testRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationMemoryCircuitBreaker.ps1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../resources/resource-policy.json') -Destination (Join-Path $testRoot '.agents/skills/orchestrate/resources/resource-policy.json')
    & git -C $testRoot init --quiet
    & $breaker -RepositoryRoot $testRoot -Action Trip -RunId test-run `
        -Operation bounded-project-gate -Reason 'synthetic OOM' -Confirm:$false | Out-Null
    $statePath = Join-Path $testRoot '.tmp/orchestration/resource-policy-state.json'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($state.schema_version -eq 2) 'breaker must write only the new schema'
    Assert-True ($state.mode -eq 'degraded') 'trip must select degraded mode'
    Assert-True ($state.effective_floor_gib -eq 1.0) 'trip must restore preferred floor'
    Assert-True ($state.maximum_worker_fanout -eq 1) 'trip must reduce fanout'
    & $breaker -RepositoryRoot $testRoot -Action Clear -ReviewedBy owner `
        -Reason 'reviewed safe calibration' -Confirm:$false | Out-Null
    $cleared = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($cleared.mode -eq 'normal') 'owner-reviewed clear must restore normal mode'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Output 'Orchestration resource snapshot tests passed.'
