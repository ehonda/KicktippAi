[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Get-OrchestrationResourceSnapshot.ps1'

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
}
Assert-True $baseline.WorktreeAdmission.Allowed 'the current-like disk sample should admit one worktree'
Assert-True ($baseline.Warnings.Count -eq 1) 'low disk percentage should produce a warning'
Assert-True ($baseline.HeavyOperationAdmission.CurrentLimit -eq 1) 'a four-core host should retain one heavy-operation lease'

$fullPool = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 40
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 2
}
Assert-True (-not $fullPool.WorktreeAdmission.Allowed) 'a full worktree pool must fail closed'

$lowDisk = & $helper -Admission Worktree -Sample @{
    FreeDiskGiB = 10.5
    TotalDiskGiB = 200
    AvailableMemoryGiB = 4
    LogicalProcessors = 8
    LinkedTaskWorktrees = 0
}
Assert-True (-not $lowDisk.WorktreeAdmission.Allowed) 'the post-reservation disk floor must be enforced'

$lowMemory = & $helper -Admission Heavy -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 1.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True (-not $lowMemory.HeavyOperationAdmission.Allowed) 'low memory must deny a heavy operation'

$singleLease = & $helper -Admission Heavy -ActiveHeavyOperations 1 -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 3.0
    LogicalProcessors = 4
    LinkedTaskWorktrees = 0
}
Assert-True (-not $singleLease.HeavyOperationAdmission.Allowed) 'one active operation must fill the default lease'

$expandedLease = & $helper -Admission Heavy -ActiveHeavyOperations 1 -Sample @{
    FreeDiskGiB = 30
    TotalDiskGiB = 200
    AvailableMemoryGiB = 8.0
    LogicalProcessors = 8
    LinkedTaskWorktrees = 0
}
Assert-True $expandedLease.HeavyOperationAdmission.Allowed 'a well-provisioned sample should admit the second lease'
Assert-True ($expandedLease.HeavyOperationAdmission.CurrentLimit -eq 2) 'the expanded lease limit should be two'

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

Write-Output 'Orchestration resource snapshot tests passed.'
