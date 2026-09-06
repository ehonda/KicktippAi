[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Get-OrchestrationResourceSnapshot.ps1'
$circuitBreakerHelper = Join-Path $PSScriptRoot 'Set-OrchestrationMemoryCircuitBreaker.ps1'

function Assert-True {
    param(
        [Parameter(Mandatory)] $Condition,
        [Parameter(Mandatory)][string] $Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

$baseline = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 24.25
    TotalDiskGiB = 237.7
    AvailableMemoryGiB = 2.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
    WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 0
}
Assert-True $baseline.WorktreeAdmission.Allowed 'the current-like disk sample should admit one worktree'
Assert-True ($baseline.Warnings.Count -eq 1) 'effective disk percentage below 15% should produce a warning'
Assert-True ($baseline.HeavyOperationAdmission.CurrentLimit -eq 1) 'a four-core host should retain one heavy-operation lease'

$fullPool = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 40
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 12
    WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 10
}
Assert-True $fullPool.WorktreeAdmission.Allowed 'linked-worktree count must remain inventory rather than an admission cap'
Assert-True ($fullPool.WorktreeAdmission.ExistingLinkedTaskWorktrees -eq 12) 'the inventory count must remain visible'

$lowDisk = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 16
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 0
    WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 1
}
Assert-True (-not $lowDisk.WorktreeAdmission.Allowed) 'the 14 GiB effective post-reservation disk floor must be enforced'

$missingInventory = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 40
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 2
    WorktreeInventoryConfirmed = $false
    OutstandingWorktreeReservationsGiB = 0
}
Assert-True (-not $missingInventory.WorktreeAdmission.Allowed) 'unreconciled worktree inventory must fail closed'

$parked = & $helper -Admission Worktree -ProposedWorktreeClass ParkedRecoveryOnly -Sample @{
    FreeDiskGiB = 15
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 3
    WorktreeInventoryConfirmed = $true
    OutstandingWorktreeReservationsGiB = 1
}
Assert-True $parked.WorktreeAdmission.Allowed 'a parked worktree must not reserve future growth'
Assert-True ($parked.WorktreeAdmission.ProposedReservedGiB -eq 0) 'parked worktrees must have zero proposed growth reservation'

$lowMemory = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 0.99
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True (-not $lowMemory.HeavyOperationAdmission.Allowed) 'low memory must deny a heavy operation'

$hardFloor = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 1.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True $hardFloor.HeavyOperationAdmission.Allowed 'the 1.00 GiB hard floor must admit one heavy operation'
Assert-True ($hardFloor.Warnings.Count -eq 1) 'memory below 1.50 GiB should produce a warning'
Assert-True $hardFloor.HeavyOperationAdmission.InExperimentalBand 'the 1.00-1.10 GiB band must be identified as experimental'

$trippedFloor = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 1.05
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
    MemoryCircuitBreakerActive = $true
    MemoryCircuitBreakerReason = 'test failure'
}
Assert-True (-not $trippedFloor.HeavyOperationAdmission.Allowed) 'an active circuit breaker must restore the 1.10 GiB floor'
Assert-True ($trippedFloor.HeavyOperationAdmission.EffectiveHardFloorGiB -eq 1.1) 'the restored floor must be visible'

$formerCliff = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 1.48
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True $formerCliff.HeavyOperationAdmission.Allowed '1.48 GiB must no longer fail heavy admission'
Assert-True ($formerCliff.Warnings.Count -eq 1) '1.48 GiB should retain the low-memory warning'

$warningBoundary = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 1.5
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True $warningBoundary.HeavyOperationAdmission.Allowed 'the 1.50 GiB warning boundary must admit one heavy operation'
Assert-True ($warningBoundary.Warnings.Count -eq 0) 'the exact 1.50 GiB warning boundary must not warn'

$singleLease = & $helper -Admission Heavy -ActiveHeavyOperations 1 -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 3.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True (-not $singleLease.HeavyOperationAdmission.Allowed) 'one active operation must fill the default lease'

$largeHostLease = & $helper -Admission Heavy -ActiveHeavyOperations 1 -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 8.0
    LogicalProcessors = 8
    LinkedTaskWorktrees = 0
}
Assert-True (-not $largeHostLease.HeavyOperationAdmission.Allowed) 'a well-provisioned sample must still keep one heavy lease'
Assert-True ($largeHostLease.HeavyOperationAdmission.CurrentLimit -eq 1) 'the heavy-operation limit should remain one'

$missingMemory = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = $null
    LogicalProcessors = 8
    LinkedTaskWorktrees = 0
}
Assert-True (-not $missingMemory.HeavyOperationAdmission.Allowed) 'missing memory evidence must fail closed'

$json = & $helper -Admission Snapshot -AsJson -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 3.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
} | ConvertFrom-Json
Assert-True ($json.AdmissionMode -eq 'Snapshot') 'JSON output must preserve the admission mode'
Assert-True ($null -ne $json.WorktreeAdmission.Allowed) 'JSON output must preserve the admission verdicts'
Assert-True ($json.HeavyOperationAdmission.ConfiguredHardFloorGiB -eq 1.0) 'JSON output must expose the experimental configured floor'
Assert-True ($json.HeavyOperationAdmission.EffectiveHardFloorGiB -eq 1.0) 'JSON output must expose the effective floor'
Assert-True ($json.HeavyOperationAdmission.WarningThresholdGiB -eq 1.5) 'JSON output must expose the warning threshold'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-resource-$([Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.git') | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'KicktippAi.slnx') -Value '' -Encoding utf8
    $linkedRoot = Join-Path $testRoot 'linked-worktree'
    New-Item -ItemType Directory -Path (Join-Path $linkedRoot '.codex-local') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $linkedRoot '.git') -Value 'gitdir: synthetic' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $linkedRoot '.codex-local/original-repository-path') -Value $testRoot -Encoding utf8

    $clearWithoutTripRejected = $false
    try {
        & $circuitBreakerHelper -Action Clear -RepositoryRoot $testRoot -Reason 'no trip' -ReviewedBy owner | Out-Null
    }
    catch {
        $clearWithoutTripRejected = $_.Exception.Message -match 'existing valid active'
    }
    Assert-True $clearWithoutTripRejected 'clearing without an existing valid trip must be rejected'

    & $circuitBreakerHelper -Action Trip -RepositoryRoot $linkedRoot -RunId test-run -Operation build -Reason 'synthetic OOM' | Out-Null
    $trippedState = & $helper -Admission Heavy -RepositoryRoot $testRoot -Sample @{
        FreeDiskGiB = 30
        TotalDiskGiB = 200
        AvailableMemoryGiB = 1.05
        LogicalProcessors = 4
        LinkedTaskWorktrees = 0
    }
    Assert-True (-not $trippedState.HeavyOperationAdmission.Allowed) 'a trip from a linked worktree must affect primary-checkout admission'
    Assert-True ($trippedState.HeavyOperationAdmission.CircuitBreakerStatePath -like "$testRoot*") 'linked worktrees must share the primary-checkout circuit-breaker path'

    & $circuitBreakerHelper -Action Clear -RepositoryRoot $linkedRoot -Reason 'owner-reviewed calibration' -ReviewedBy owner | Out-Null
    $clearedState = & $helper -Admission Heavy -RepositoryRoot $testRoot -Sample @{
        FreeDiskGiB = 30
        TotalDiskGiB = 200
        AvailableMemoryGiB = 1.05
        LogicalProcessors = 4
        LinkedTaskWorktrees = 0
    }
    Assert-True $clearedState.HeavyOperationAdmission.Allowed 'owner-reviewed clearing must restore the configured floor'

    Set-Content -LiteralPath (Join-Path $testRoot '.tmp/orchestration/resource-policy-state.json') -Value '{"schema_version":1,"status":"cleared"}' -Encoding utf8
    $malformedClear = & $helper -Admission Heavy -RepositoryRoot $testRoot -Sample @{
        FreeDiskGiB = 30
        TotalDiskGiB = 200
        AvailableMemoryGiB = 2.0
        LogicalProcessors = 4
        LinkedTaskWorktrees = 0
    }
    Assert-True (-not $malformedClear.HeavyOperationAdmission.Allowed) 'a malformed or unaudited cleared state must fail closed'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Output 'Orchestration resource snapshot tests passed.'
