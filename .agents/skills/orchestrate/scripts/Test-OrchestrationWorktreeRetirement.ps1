[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRepository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$retirementHelper = Join-Path $PSScriptRoot 'Remove-OrchestrationWorktree.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-worktree-$([Guid]::NewGuid().ToString('N'))"
$worktreePath = Join-Path $testRoot '.tmp/worktrees/retire-me'
$outsidePath = Join-Path $testRoot 'outside'
$branch = 'codex/retirement-test'

function Assert-True {
    param([Parameter(Mandatory)] $Condition, [Parameter(Mandatory)][string] $Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Invoke-Retirement {
    param(
        [string] $Path = $worktreePath,
        [string] $ExpectedBranch = $branch,
        [string] $ExpectedTip = $tip,
        [bool] $AgentTerminal = $true,
        [bool] $MailboxClean = $true,
        [bool] $NoActiveLease = $true,
        [bool] $NoActiveProcessOwnership = $true,
        [switch] $WhatIf
    )
    $arguments = @{
        WorktreePath = $Path
        ExpectedBranch = $ExpectedBranch
        ExpectedTip = $ExpectedTip
        RunId = 'test-run'
        ExpectedRevision = [long] ((Get-Content -LiteralPath (
            Join-Path $testRoot '.tmp/orchestration/test-run/control-state.json') `
            -Raw | ConvertFrom-Json).revision)
        TransitionName = 'worktree:retire-me:retire'
        RepositoryRoot = $testRoot
        AgentTerminal = $AgentTerminal
        MailboxClean = $MailboxClean
        NoActiveLease = $NoActiveLease
        NoActiveProcessOwnership = $NoActiveProcessOwnership
        AsJson = $true
    }
    if ($WhatIf) { $arguments.WhatIf = $true }
    return (& $retirementHelper @arguments | ConvertFrom-Json)
}

function Reset-RemovalReady {
    $statePath = Join-Path $testRoot '.tmp/orchestration/test-run/control-state.json'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $state.resource_state.worktree_reservations[0].class = 'removal-ready'
    $state.resource_state.worktree_reservations[0].growth_reservation_gib = 0.0
    $state.resource_state.worktree_reservations[0].owner = ''
    $state.resource_state.worktree_reservations[0].remediation = ''
    $state.resource_state.worktree_admission = 'unknown'
    & $testCheckpoint -Action Update -RunId test-run `
        -ExpectedRevision ([long] $state.revision) -RepositoryRoot $testRoot `
        -StateJson ($state | ConvertTo-Json -Depth 20 -Compress) `
        -CheckpointKind WorktreeReservation -TransitionName 'worktree:test-rearm' | Out-Null
}

function Assert-Parked {
    $state = Get-Content -LiteralPath (
        Join-Path $testRoot '.tmp/orchestration/test-run/control-state.json') `
        -Raw | ConvertFrom-Json
    Assert-True ($state.resource_state.worktree_reservations[0].class -eq 'parked-recovery-only') 'a failed retirement precondition must checkpoint the worktree as parked'
    Assert-True (-not [string]::IsNullOrWhiteSpace(
        [string] $state.resource_state.worktree_reservations[0].remediation)) 'a parked worktree must record remediation'
}

function Assert-RejectedAndRetained {
    param([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $Pattern)
    $rejected = $false
    try { & $Action | Out-Null }
    catch { $rejected = $_.Exception.Message -match $Pattern }
    Assert-True $rejected "retirement must reject: $Pattern"
    Assert-True (Test-Path -LiteralPath $worktreePath -PathType Container) 'a failed precondition must retain the worktree for parking/remediation'
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
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
    & git -C $testRoot config user.email 'orchestration-test@example.invalid'
    & git -C $testRoot config user.name 'Orchestration Test'
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'tracked.txt'),
        "base$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    & git -C $testRoot add tracked.txt
    & git -C $testRoot commit --quiet -m 'test base'
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the retirement test commit.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $worktreePath) -Force | Out-Null
    & git -C $testRoot worktree add --quiet -b $branch $worktreePath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the retirement test worktree.' }
    $tip = (& git -C $worktreePath rev-parse HEAD).Trim()
    $testCheckpoint = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1'
    & $testCheckpoint -Action Initialize -RunId test-run -ExpectedRevision 0 `
        -RepositoryRoot $testRoot -Objective 'Test worktree retirement.' `
        -StopCondition 'Assertions pass.' -InitialSha $tip `
        -AllowedBranchPrefix 'codex/test-run-' `
        -NextRootAction 'Record the removal-ready worktree.' | Out-Null
    $statePath = Join-Path $testRoot '.tmp/orchestration/test-run/control-state.json'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $state.resource_state.worktree_reservations = @([pscustomobject] [ordered] @{
        path = $worktreePath
        class = 'removal-ready'
        growth_reservation_gib = 0.0
        owner = ''
        branch = $branch
        tip = $tip
        remediation = ''
    })
    & $testCheckpoint -Action Update -RunId test-run -ExpectedRevision 1 `
        -RepositoryRoot $testRoot -StateJson ($state | ConvertTo-Json -Depth 20 -Compress) `
        -CheckpointKind WorktreeReservation -TransitionName 'worktree:test-record' | Out-Null

    Assert-RejectedAndRetained -Pattern 'outside the managed worktree root' -Action {
        Invoke-Retirement -Path $outsidePath
    }
    Assert-RejectedAndRetained -Pattern 'removal-ready' -Action {
        Invoke-Retirement -ExpectedBranch 'codex/wrong'
    }
    Assert-RejectedAndRetained -Pattern 'removal-ready' -Action {
        Invoke-Retirement -ExpectedTip ('0' * 40)
    }
    Assert-RejectedAndRetained -Pattern 'terminal-agent' -Action {
        Invoke-Retirement -AgentTerminal $false
    }
    Assert-Parked
    Reset-RemovalReady
    Assert-RejectedAndRetained -Pattern 'mailbox' -Action {
        Invoke-Retirement -MailboxClean $false
    }
    Assert-Parked
    Reset-RemovalReady
    Assert-RejectedAndRetained -Pattern 'lease' -Action {
        Invoke-Retirement -NoActiveLease $false
    }
    Assert-Parked
    Reset-RemovalReady
    Assert-RejectedAndRetained -Pattern 'process-ownership' -Action {
        Invoke-Retirement -NoActiveProcessOwnership $false
    }
    Assert-Parked
    Reset-RemovalReady

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $state.resource_state.worktree_reservations[0].owner = '/root/still-working'
    & $testCheckpoint -Action Update -RunId test-run `
        -ExpectedRevision ([long] $state.revision) -RepositoryRoot $testRoot `
        -StateJson ($state | ConvertTo-Json -Depth 20 -Compress) `
        -CheckpointKind WorktreeReservation `
        -TransitionName 'worktree:test-record-owner' | Out-Null
    Assert-RejectedAndRetained -Pattern 'still records worktree ownership' -Action {
        Invoke-Retirement
    }
    Assert-Parked
    Reset-RemovalReady

    $gate = [ordered] @{
        id = 'worktree-retirement-gate'
        category = 'resource'
        scope = 'worktree:retire-me'
        evidence = 'Synthetic retirement gate.'
        prohibited_transitions = @('worktree:retire-me:retire')
        remediation_owner = 'root-orchestrator'
        remediation_action = 'Clear the synthetic gate.'
        continue_actions = @('unrelated work')
        escalation_condition = 'Synthetic evidence is resolved.'
        retry_budget = 1
        retry_attempts = 0
    }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    & $testCheckpoint -Action SetGate -RunId test-run `
        -ExpectedRevision ([long] $state.revision) -RepositoryRoot $testRoot `
        -GateJson ($gate | ConvertTo-Json -Depth 10 -Compress) | Out-Null
    Assert-RejectedAndRetained -Pattern 'prohibited by active gate' -Action {
        Invoke-Retirement
    }
    Assert-Parked
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    & $testCheckpoint -Action ClearGate -RunId test-run `
        -ExpectedRevision ([long] $state.revision) -RepositoryRoot $testRoot `
        -GateId 'worktree-retirement-gate' `
        -ClearanceEvidence 'Synthetic gate test completed.' | Out-Null
    Reset-RemovalReady

    [System.IO.File]::WriteAllText(
        (Join-Path $worktreePath 'untracked.txt'),
        "dirty$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    Assert-RejectedAndRetained -Pattern 'dirty' -Action { Invoke-Retirement }
    Assert-Parked
    Remove-Item -LiteralPath (Join-Path $worktreePath 'untracked.txt') -Force
    Reset-RemovalReady

    $whatIfResult = Invoke-Retirement -WhatIf
    Assert-True (-not $whatIfResult.removed) 'WhatIf must report no removal'
    Assert-True (Test-Path -LiteralPath $worktreePath -PathType Container) 'WhatIf must retain the checkout'

    $result = Invoke-Retirement
    Assert-True ($result.removed -and $result.retained_branch -and $result.checkpoint_required) 'clean exact retirement must remove only the checkout and require reservation release'
    Assert-True (-not (Test-Path -LiteralPath $worktreePath)) 'the retired checkout must be absent'
    $retainedTip = (& git -C $testRoot rev-parse "refs/heads/$branch").Trim()
    Assert-True ($retainedTip -eq $tip) 'retirement must preserve the exact branch and tip'

    Write-Output 'Orchestration worktree-retirement tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
        $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTestRoot.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a test directory outside the system temp root.'
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
