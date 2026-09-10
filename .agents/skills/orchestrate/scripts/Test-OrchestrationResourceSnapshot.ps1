[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRepository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
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
        MemoryCircuitBreakerMode = if ($CircuitBreakerActive) { 'degraded' } else { 'normal' }
        MemoryCircuitBreakerReason = if ($CircuitBreakerActive) { 'synthetic pressure failure' } else { $null }
        DegradedProfile = if ($CircuitBreakerActive) { 'bounded-project-gate' } else { $null }
        DegradedFingerprint = if ($CircuitBreakerActive) { 'project-gate-v1' } else { $null }
        DegradedRetriesUsed = 0
        DegradedRetryLimit = if ($CircuitBreakerActive) { 1 } else { 0 }
    }
}

$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../resources/resource-policy.json') -Raw | ConvertFrom-Json
Assert-True ($policy.schemaVersion -eq 4) 'resource policy schema must be version 4'
Assert-True ($policy.heavyOperation.PSObject.Properties.Name -notcontains 'concurrentLimit') 'fixed heavy-operation count must be absent'
Assert-True ($policy.heavyOperation.preferredAvailableMemoryFloorGiB -eq 1.0) 'preferred floor must be 1.0 GiB'
Assert-True ($policy.heavyOperation.absoluteAvailableMemoryFloorGiB -eq 0.5) 'absolute floor must be 0.5 GiB'
Assert-True ($policy.heavyOperation.provisionalProjectGateReservationGiB -eq 0.85) 'project gate reservation must be 0.85 GiB'
Assert-True (@($policy.heavyOperation.operationProfile.requiredRequestFields).Count -ge 7) 'policy must define the operation-profile request schema'
Assert-True (@($policy.heavyOperation.operationProfile.requiredOutcomeEvidence) -ccontains 'pagingHealth') 'policy must define outcome evidence'

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
    -RequestedWorkerFanout 4 -OperationProfile bounded-project-gate `
    -OperationFingerprint project-gate-v1 -FanoutControllable -Sample (New-Sample)
Assert-True $twoPreferred.HeavyOperationAdmission.Allowed 'two project gates must fit above the preferred floor'
Assert-True ($twoPreferred.HeavyOperationAdmission.EffectiveFloorGiB -eq 1.0) 'ordinary admission must keep preferred floor'
$threePreferred = & $helper -Admission Heavy -RequestedMemoryReservationGiB 2.55 `
    -RequestedWorkerFanout 6 -OperationProfile bounded-project-gate `
    -OperationFingerprint project-gate-v1 -FanoutControllable -Sample (New-Sample)
Assert-True (-not $threePreferred.HeavyOperationAdmission.Allowed) 'three project gates must not silently cross preferred floor'
$threeExperimental = & $helper -Admission Heavy -Mandatory -Recoverable `
    -FanoutControllable -CommitHealthHealthy -PagingHealthy `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -RequestedMemoryReservationGiB 2.55 -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True $threeExperimental.HeavyOperationAdmission.Allowed '3.06 GiB must admit three 0.85 GiB mandatory recoverable gates'
Assert-True $threeExperimental.HeavyOperationAdmission.UsesExperimentalFloor 'three-gate case must identify experimental use'
Assert-True ($threeExperimental.HeavyOperationAdmission.PostAdmissionMemoryGiB -eq 0.01) 'three-gate case must leave measured 0.01 GiB above floor'
$missingHealth = & $helper -Admission Heavy -Mandatory -Recoverable `
    -FanoutControllable -OperationProfile bounded-project-gate `
    -OperationFingerprint project-gate-v1 -RequestedMemoryReservationGiB 2.55 `
    -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True (-not $missingHealth.HeavyOperationAdmission.Allowed) 'experimental floor must require affirmative commit and paging health'
$external = & $helper -Admission Heavy -Mandatory -Recoverable `
    -FanoutControllable -CommitHealthHealthy -PagingHealthy `
    -EffectClass external-or-live -OperationProfile bounded-project-gate `
    -OperationFingerprint project-gate-v1 -RequestedMemoryReservationGiB 2.55 `
    -RequestedWorkerFanout 6 -Sample (New-Sample)
Assert-True (-not $external.HeavyOperationAdmission.Allowed) 'external/live work must not use experimental floor'

$cpuDenied = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -ActiveWorkerFanout 7 -RequestedWorkerFanout 2 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -Sample (New-Sample)
Assert-True (-not $cpuDenied.HeavyOperationAdmission.Allowed) 'worker fanout above logical processors must deny'
$unknownAlone = & $helper -Admission Heavy -UnmeasuredOperation `
    -OperationProfile unknown-tool -OperationFingerprint unknown-v1 -Sample (New-Sample)
Assert-True $unknownAlone.HeavyOperationAdmission.Allowed 'unmeasured operation must use the full pool when alone'
Assert-True $unknownAlone.HeavyOperationAdmission.ExclusiveRequest 'unmeasured operation must be exclusive'
Assert-True ($unknownAlone.HeavyOperationAdmission.RequestedWorkerFanout -eq 8) 'unmeasured operation may use all controlled CPUs'
$unknownOverlap = & $helper -Admission Heavy -UnmeasuredOperation `
    -ActiveHeavyProfiles 1 -ActiveMemoryReservationsGiB 0.5 -ActiveWorkerFanout 1 `
    -OperationProfile unknown-tool -OperationFingerprint unknown-v1 -Sample (New-Sample)
Assert-True (-not $unknownOverlap.HeavyOperationAdmission.Allowed) 'unmeasured operation must not overlap an active profile'
$exclusiveBackfill = & $helper -Admission Heavy -Backfill -ActiveExclusiveOperation `
    -RequestedMemoryReservationGiB 0.85 -RequestedWorkerFanout 1 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -Sample (New-Sample)
Assert-True (-not $exclusiveBackfill.HeavyOperationAdmission.Allowed) 'active exclusive operation must block backfill'
$safeBackfill = & $helper -Admission Heavy -Backfill -ActiveHeavyProfiles 1 `
    -ActiveMemoryReservationsGiB 0.85 -ActiveWorkerFanout 2 `
    -RequestedMemoryReservationGiB 0.85 -RequestedWorkerFanout 2 `
    -FanoutControllable -OperationProfile bounded-project-gate `
    -OperationFingerprint project-gate-v1 `
    -Sample (New-Sample -AvailableMemoryGiB 4)
Assert-True $safeBackfill.HeavyOperationAdmission.Allowed 'fresh capacity must permit conservative backfill'
$missingMemory = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 1 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 -Sample @{
    FreeDiskGiB = 50; TotalDiskGiB = 100; AvailableMemoryGiB = $null
    LogicalProcessors = 8; LinkedTaskWorktrees = 0; WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 0; MemoryCircuitBreakerMode = 'normal'
}
Assert-True (-not $missingMemory.HeavyOperationAdmission.Allowed) 'missing memory must gate heavy admission'

$degraded = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 2 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -Sample (New-Sample -CircuitBreakerActive $true)
Assert-True (-not $degraded.HeavyOperationAdmission.Allowed) 'degraded mode must reject fanout above one'
$degradedAllowed = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 1 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -Sample (New-Sample -CircuitBreakerActive $true)
Assert-True $degradedAllowed.HeavyOperationAdmission.Allowed 'degraded mode must continue one recoverable low-fanout gate'
Assert-True ($degradedAllowed.HeavyOperationAdmission.EffectiveFloorGiB -eq 1.0) 'degraded mode must restore preferred floor'
$exhaustedSample = New-Sample -CircuitBreakerActive $true
$exhaustedSample.DegradedRetriesUsed = 1
$degradedExhausted = & $helper -Admission Heavy -RequestedMemoryReservationGiB 0.85 `
    -RequestedWorkerFanout 1 -FanoutControllable `
    -OperationProfile bounded-project-gate -OperationFingerprint project-gate-v1 `
    -Sample $exhaustedSample
Assert-True (-not $degradedExhausted.HeavyOperationAdmission.Allowed) 'offending profile must route to diagnosis after one recoverable retry'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-resource-$([Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.agents/skills') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.codex') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRepository '.agents/skills/orchestrate') `
        -Destination (Join-Path $testRoot '.agents/skills') -Recurse
    Copy-Item -LiteralPath (Join-Path $sourceRepository '.codex/hooks.json') `
        -Destination (Join-Path $testRoot '.codex/hooks.json')
    foreach ($relativeFile in @('New-AgentWorktree.ps1', 'KicktippAi.slnx')) {
        Copy-Item -LiteralPath (Join-Path $sourceRepository $relativeFile) `
            -Destination (Join-Path $testRoot $relativeFile)
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'AGENTS.md'),
        "# Test instructions$([Environment]::NewLine)$([Environment]::NewLine)@AUTO-REVIEW.md$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'AUTO-REVIEW.md'),
        "# Test review instructions$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    & git -C $testRoot init --quiet
    $testCheckpoint = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1'
    $testBreaker = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationMemoryCircuitBreaker.ps1'
    $testResource = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1'
    & $testCheckpoint -Action Initialize -RunId test-run -ExpectedRevision 0 `
        -Objective 'Test breaker state.' -StopCondition 'Assertions pass.' `
        -InitialSha ('1' * 40) -AllowedBranchPrefix 'codex/test-run-' `
        -NextRootAction 'Exercise the breaker.' | Out-Null
    & $testBreaker -RepositoryRoot $testRoot -Action Trip -RunId test-run `
        -ExpectedRevision 1 -OperationProfile bounded-project-gate `
        -OperationFingerprint project-gate-v1 -Operation 'bounded project gate' `
        -Reason 'synthetic OOM' -Confirm:$false | Out-Null
    $statePath = Join-Path $testRoot '.tmp/orchestration/test-run/control-state.json'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($state.revision -eq 2) 'breaker trip must use one checkpoint revision'
    Assert-True ($state.resource_state.circuit_breaker_mode -eq 'degraded') 'trip must select degraded mode in control state'
    Assert-True ($state.resource_state.degraded_profiles[0].maximum_worker_fanout -eq 1) 'trip must reduce offending-profile fanout'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $testRoot '.tmp/orchestration/resource-policy-state.json'))) 'breaker must not create a second independent state file'
    $retry = & $testCheckpoint -Action ReserveMemoryRetry -RunId test-run `
        -ExpectedRevision 2 -OperationProfile bounded-project-gate `
        -OperationFingerprint project-gate-v1 -AsJson | ConvertFrom-Json
    Assert-True ($retry.memory_retry_allowed -and $retry.revision -eq 3) 'breaker must reserve exactly one recoverable retry'
    $secondRetry = & $testCheckpoint -Action ReserveMemoryRetry -RunId test-run `
        -ExpectedRevision 3 -OperationProfile bounded-project-gate `
        -OperationFingerprint project-gate-v1 -AsJson | ConvertFrom-Json
    Assert-True (-not $secondRetry.memory_retry_allowed -and $secondRetry.no_op) 'a second memory retry must be rejected without another revision'
    $observedBreaker = & $testResource -Admission Snapshot -RunId test-run `
        -RepositoryRoot $testRoot
    Assert-True $observedBreaker.HeavyOperationAdmission.CircuitBreakerActive 'resource admission must read the exact-run control state'
    & $testBreaker -RepositoryRoot $testRoot -Action Clear -RunId test-run `
        -ExpectedRevision 3 -ReviewedBy owner -Reason 'reviewed safe calibration' `
        -Confirm:$false | Out-Null
    $cleared = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($cleared.revision -eq 4) 'breaker clear must use one checkpoint revision'
    Assert-True ($cleared.resource_state.circuit_breaker_mode -eq 'normal') 'owner-reviewed clear must restore normal mode'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Output 'Orchestration resource snapshot tests passed.'
