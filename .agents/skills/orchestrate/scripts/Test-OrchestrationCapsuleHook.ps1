[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRepository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-checkpoint-$([Guid]::NewGuid().ToString('N'))"
$runId = 'checkpoint-test'

function Assert-True {
    param([Parameter(Mandatory)] $Condition, [Parameter(Mandatory)][string] $Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Invoke-Checkpoint {
    param([Parameter(Mandatory)][hashtable] $Arguments)
    $Arguments.RepositoryRoot = $testRoot
    $Arguments.AsJson = $true
    return (& $checkpoint @Arguments | ConvertFrom-Json)
}

function Invoke-Validation {
    return (& $hook -Mode Validate -RunId $runId -RepositoryRoot $testRoot | ConvertFrom-Json)
}

function New-HookInput {
    param([Parameter(Mandatory)][string] $EventName)
    return [pscustomobject] @{
        session_id = $runId
        hook_event_name = $EventName
        source = if ($EventName -eq 'SessionStart') { 'compact' } else { $null }
        trigger = if ($EventName -eq 'PreCompact') { 'auto' } else { $null }
    } | ConvertTo-Json -Compress
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    foreach ($relativeDirectory in @('.agents/skills', '.codex')) {
        New-Item -ItemType Directory -Path (Join-Path $testRoot $relativeDirectory) -Force | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $sourceRepository '.agents/skills/orchestrate') -Destination (Join-Path $testRoot '.agents/skills') -Recurse
    Copy-Item -LiteralPath (Join-Path $sourceRepository '.codex/hooks.json') -Destination (Join-Path $testRoot '.codex/hooks.json')
    foreach ($relativeFile in @('New-AgentWorktree.ps1', 'KicktippAi.slnx')) {
        Copy-Item -LiteralPath (Join-Path $sourceRepository $relativeFile) -Destination (Join-Path $testRoot $relativeFile)
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
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize checkpoint test repository.' }

    $checkpoint = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1'
    $hook = Join-Path $testRoot '.agents/skills/orchestrate/scripts/Invoke-OrchestrationCapsuleHook.ps1'
    $runDirectory = Join-Path $testRoot ".tmp/orchestration/$runId"

    $plain = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput -EventName 'SessionStart')
    Assert-True ([string]::IsNullOrWhiteSpace([string] $plain)) 'ordinary sessions must remain silent'

    $initial = Invoke-Checkpoint @{
        Action = 'Initialize'
        RunId = $runId
        ExpectedRevision = 0
        Objective = 'Exercise transactional recovery state.'
        StopCondition = 'All assertions pass.'
        InitialSha = ('1' * 40)
        AllowedBranchPrefix = 'codex/checkpoint-test-'
        NextRootAction = 'Freeze the first lane.'
    }
    Assert-True ($initial.revision -eq 1 -and $initial.changed) 'initialization must commit revision 1'
    foreach ($name in @(
        'control-state.json', 'preview.md', 'capsule.json', 'capsule.sha256',
        'instruction-manifest.json', 'hook-manifest.json',
        'active-contract-manifest.json', 'active')) {
        Assert-True (Test-Path -LiteralPath (Join-Path $runDirectory $name) -PathType Leaf) "initialization must create $name"
    }

    $validation = Invoke-Validation
    Assert-True ($validation.valid -and $validation.recovery_mode -eq 'hot') 'fresh state must validate hot'
    Assert-True ($validation.control_state.revision -eq 1) 'validation must expose control revision'
    Assert-True ($validation.capsule.state_revision -eq 1) 'capsule revision must match control state'

    $instructionManifest = Get-Content -LiteralPath (Join-Path $runDirectory 'instruction-manifest.json') -Raw | ConvertFrom-Json
    foreach ($path in @(
        '.agents/skills/orchestrate/SKILL.md',
        '.agents/skills/orchestrate/references/operations-protocol.md',
        '.agents/skills/orchestrate/references/roles-and-handoffs.md',
        '.agents/skills/orchestrate/references/validation-and-integration.md')) {
        Assert-True (@($instructionManifest.entries.path) -ccontains $path) "instruction graph must include $path"
    }
    Assert-True (@($instructionManifest.entries.path | Where-Object { $_ -like 'docs/codex/*' }).Count -eq 0) 'supplemental docs must not be runtime inputs'

    $hookManifest = Get-Content -LiteralPath (Join-Path $runDirectory 'hook-manifest.json') -Raw | ConvertFrom-Json
    foreach ($path in @(
        '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1',
        '.agents/skills/orchestrate/scripts/Remove-OrchestrationWorktree.ps1',
        '.agents/skills/orchestrate/resources/control-state-template.json',
        '.agents/skills/orchestrate/resources/resource-policy.json')) {
        Assert-True (@($hookManifest.entries.path) -ccontains $path) "hook packet must include $path"
    }

    $noOp = Invoke-Checkpoint @{
        Action = 'Transition'
        RunId = $runId
        ExpectedRevision = 1
        Status = 'preview'
        TransitionName = 'run:preview-no-op'
    }
    Assert-True ($noOp.no_op -and $noOp.revision -eq 1) 'semantic duplicates must not create revisions'

    $laneGate = [ordered] @{
        id = 'lane-memory'
        category = 'resource'
        scope = 'lane-a'
        evidence = 'Memory measurement is stale.'
        prohibited_transitions = @('lane-a:build')
        remediation_owner = 'root-orchestrator'
        remediation_action = 'Refresh resource evidence.'
        continue_actions = @('lane-b:review')
        escalation_condition = 'Evidence remains unavailable after one retry.'
        retry_budget = 1
        retry_attempts = 0
    } | ConvertTo-Json -Compress
    $setLaneGate = Invoke-Checkpoint @{
        Action = 'SetGate'
        RunId = $runId
        ExpectedRevision = 1
        GateJson = $laneGate
    }
    Assert-True ($setLaneGate.revision -eq 2) 'lane gate must create one revision'
    Assert-True ((Invoke-Validation).valid) 'a lane-scoped gate must preserve hot recovery'
    $gatedTransitionRejected = $false
    try {
        Invoke-Checkpoint @{
            Action = 'Transition'
            RunId = $runId
            ExpectedRevision = 2
            Status = 'preview'
            TransitionName = 'lane-a:build'
        } | Out-Null
    }
    catch { $gatedTransitionRejected = $_.Exception.Message -match 'prohibited by active gate' }
    Assert-True $gatedTransitionRejected 'a gate must deny only its exact prohibited transition'

    $invalidLifecycleRejected = $false
    try {
        Invoke-Checkpoint @{
            Action = 'Transition'
            RunId = $runId
            ExpectedRevision = 2
            Status = 'active'
            TransitionName = 'run:activate'
        } | Out-Null
    }
    catch { $invalidLifecycleRejected = $_.Exception.Message -match 'not allowed' }
    Assert-True $invalidLifecycleRejected 'preview must not skip the ready lifecycle state'

    $ownerGate = [ordered] @{
        id = 'owner-route'
        category = 'owner'
        scope = 'run'
        evidence = 'Publication target requires an owner choice.'
        prohibited_transitions = @('run:publish')
        remediation_owner = 'root-orchestrator'
        remediation_action = 'Ask the owner for the target.'
        continue_actions = @()
        escalation_condition = 'No safe frontier remains.'
        retry_budget = 0
        retry_attempts = 0
    } | ConvertTo-Json -Compress
    $setOwnerGate = Invoke-Checkpoint @{
        Action = 'SetGate'
        RunId = $runId
        ExpectedRevision = 2
        GateJson = $ownerGate
    }
    Assert-True ($setOwnerGate.revision -eq 3) 'run owner gate must create one revision'
    $gatedValidation = Invoke-Validation
    Assert-True (-not $gatedValidation.valid -and $gatedValidation.code -eq 'owner-gate') 'run owner gates must require cold owner reconciliation'

    $clearOwnerGate = Invoke-Checkpoint @{
        Action = 'ClearGate'
        RunId = $runId
        ExpectedRevision = 3
        GateId = 'owner-route'
        ClearanceEvidence = 'The owner selected the recorded publication target.'
    }
    Assert-True ($clearOwnerGate.revision -eq 4 -and (Invoke-Validation).valid) 'clearing run gate must restore hot recovery'

    $statePath = Join-Path $runDirectory 'control-state.json'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $state.lanes = @([pscustomobject] @{
        id = 'lane-a'
        status = 'correction'
        owner = '/root/writer'
        dependencies = @()
        worktree_path = '.tmp/worktrees/lane-a'
        next_action = 'Apply bounded correction.'
    })
    $state.ownership_reservations = @([pscustomobject] @{
        assignment_id = 'lane-a-writer-initial'
        lane_id = 'lane-a'
        agent_path = '/root/writer'
        role = 'milestone-writer'
        model = 'gpt-5.6-terra'
        reasoning_effort = 'medium'
        owned_paths = @('src/lane-a')
        next_action = 'Apply bounded correction.'
    })
    $replace = Invoke-Checkpoint @{
        Action = 'Update'
        RunId = $runId
        ExpectedRevision = 4
        StateJson = ($state | ConvertTo-Json -Depth 20 -Compress)
        CheckpointKind = @('Freeze', 'OwnershipReservation')
        TransitionName = 'run:freeze-lane-a'
    }
    Assert-True ($replace.revision -eq 5) 'replace must commit one validated state revision'

    $currentForReorder = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $reorderedState = [ordered] @{}
    foreach ($propertyName in @($currentForReorder.PSObject.Properties.Name)[-1..-(
        $currentForReorder.PSObject.Properties.Name.Count)]) {
        $reorderedState[$propertyName] = $currentForReorder.$propertyName
    }
    $reorderedNoOp = Invoke-Checkpoint @{
        Action = 'Update'
        RunId = $runId
        ExpectedRevision = 5
        StateJson = ($reorderedState | ConvertTo-Json -Depth 20 -Compress)
        CheckpointKind = @('Freeze')
        TransitionName = 'run:semantic-no-op'
    }
    Assert-True ($reorderedNoOp.no_op -and $reorderedNoOp.revision -eq 5) 'JSON property order must not create a checkpoint revision'

    $ambiguousOwnership = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $ambiguousOwnership.ownership_reservations = @(
        @($ambiguousOwnership.ownership_reservations) +
        [pscustomobject] @{
            assignment_id = 'lane-a-writer-duplicate'
            lane_id = 'lane-a'
            agent_path = '/root/writer'
            role = 'milestone-writer'
            model = 'gpt-5.6-terra'
            reasoning_effort = 'medium'
            owned_paths = @('src/lane-a')
            next_action = 'Duplicate ownership must be rejected.'
        })
    $ambiguousOwnershipRejected = $false
    try {
        Invoke-Checkpoint @{
            Action = 'Update'
            RunId = $runId
            ExpectedRevision = 5
            StateJson = ($ambiguousOwnership | ConvertTo-Json -Depth 20 -Compress)
            CheckpointKind = @('OwnershipReservation')
            TransitionName = 'lane-a:duplicate-owner'
        } | Out-Null
    }
    catch { $ambiguousOwnershipRejected = $_.Exception.Message -match 'Ownership reservation is invalid' }
    Assert-True $ambiguousOwnershipRejected 'one lane must never have ambiguous current ownership'
    Assert-True ((Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json).revision -eq 5) 'rejected ownership must preserve the committed revision'

    $revision = 5
    foreach ($suffix in @('one', 'two', 'three')) {
        $reserve = Invoke-Checkpoint @{
            Action = 'ReserveCorrection'
            RunId = $runId
            ExpectedRevision = $revision
            MilestoneId = 'lane-a'
            CorrectionRole = 'milestone-writer'
            AssignmentId = "fix-$suffix"
            TransitionName = 'lane-a:correction-dispatch'
        }
        $revision++
        Assert-True ($reserve.correction_allowed -and $reserve.revision -eq $revision) 'writer correction must reserve before dispatch'
        $started = Invoke-Checkpoint @{
            Action = 'MarkCorrectionStarted'
            RunId = $runId
            ExpectedRevision = $revision
            MilestoneId = 'lane-a'
            CorrectionRole = 'milestone-writer'
            AssignmentId = "fix-$suffix"
        }
        $revision++
        Assert-True ($started.revision -eq $revision) 'started correction must consume the reservation'
    }
    $exhausted = Invoke-Checkpoint @{
        Action = 'ReserveCorrection'
        RunId = $runId
        ExpectedRevision = $revision
        MilestoneId = 'lane-a'
        CorrectionRole = 'milestone-writer'
        AssignmentId = 'fix-four'
        TransitionName = 'lane-a:correction-dispatch'
    }
    $revision++
    Assert-True (-not $exhausted.correction_allowed) 'fourth writer correction must be rejected'
    $exhaustedState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($exhaustedState.lanes[0].status -eq 'needs-diagnosis') 'exhaustion must reroute only the affected lane'
    Assert-True ([string]::IsNullOrWhiteSpace([string] $exhaustedState.lanes[0].owner)) 'exhaustion must release the current specialist'
    Assert-True (@($exhaustedState.correction_counters[0].issued_assignment_ids).Count -eq 3) 'consumed correction assignment IDs must remain recorded'
    $repeatedExhaustion = Invoke-Checkpoint @{
        Action = 'ReserveCorrection'
        RunId = $runId
        ExpectedRevision = $revision
        MilestoneId = 'lane-a'
        CorrectionRole = 'milestone-writer'
        AssignmentId = 'fix-five'
        TransitionName = 'lane-a:correction-dispatch'
    }
    Assert-True ($repeatedExhaustion.no_op -and -not $repeatedExhaustion.correction_allowed) 'repeated over-budget dispatch must remain explicitly rejected without another revision'

    $missingGate = Invoke-Checkpoint @{
        Action = 'ClearGate'
        RunId = $runId
        ExpectedRevision = $revision
        GateId = 'absent'
    }
    Assert-True ($missingGate.no_op -and $missingGate.revision -eq $revision) 'clearing an absent gate must be a no-op'

    $conflict = $false
    try {
        Invoke-Checkpoint @{
            Action = 'Transition'
            RunId = $runId
            ExpectedRevision = ($revision - 1)
            Status = 'active'
            TransitionName = 'run:activate'
        } | Out-Null
    }
    catch { $conflict = $_.Exception.Message -match 'Revision conflict' }
    Assert-True $conflict 'stale expected revisions must fail'

    $committedPreviewHash = (Get-FileHash -LiteralPath (Join-Path $runDirectory 'preview.md') -Algorithm SHA256).Hash
    $invalidCandidate = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $invalidCandidate.durable_decisions = @(('x' * 13000))
    $candidateRejected = $false
    try {
        Invoke-Checkpoint @{
            Action = 'Update'
            RunId = $runId
            ExpectedRevision = $revision
            StateJson = ($invalidCandidate | ConvertTo-Json -Depth 20 -Compress)
            CheckpointKind = @('Freeze')
            TransitionName = 'run:oversized-freeze'
        } | Out-Null
    }
    catch { $candidateRejected = $_.Exception.Message -match 'oversized' }
    Assert-True $candidateRejected 'an oversized staged projection must be rejected before commit'
    $stateAfterRejection = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($stateAfterRejection.revision -eq $revision) 'a rejected candidate must preserve committed control state'
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $runDirectory 'preview.md') -Algorithm SHA256).Hash -eq $committedPreviewHash) 'a rejected candidate must preserve committed projections'
    Assert-True (@(Get-ChildItem -LiteralPath $runDirectory -Directory -Filter '.checkpoint-stage-*').Count -eq 0) 'a rejected candidate must clean staging files'

    $lockPath = Join-Path $runDirectory '.checkpoint.lock'
    $lockStream = [System.IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    try { $lockedValidation = Invoke-Validation }
    finally { $lockStream.Dispose() }
    Assert-True (-not $lockedValidation.valid -and $lockedValidation.code -eq 'checkpoint-in-progress') 'hooks must detect an uncleared checkpoint lock'

    $previewPath = Join-Path $runDirectory 'preview.md'
    $previewBytes = [System.IO.File]::ReadAllBytes($previewPath)
    Add-Content -LiteralPath $previewPath -Value 'drift'
    $driftValidation = Invoke-Validation
    Assert-True (-not $driftValidation.valid -and $driftValidation.code -eq 'packet-digest-mismatch') 'projection drift must force cold reconstruction'
    [System.IO.File]::WriteAllBytes($previewPath, $previewBytes)
    Assert-True ((Invoke-Validation).valid) 'restored projection bytes must validate'

    $stateBytes = [System.IO.File]::ReadAllBytes($statePath)
    Add-Content -LiteralPath $statePath -Value ' '
    $stateDrift = Invoke-Validation
    Assert-True (-not $stateDrift.valid -and $stateDrift.code -eq 'control-state-digest-mismatch') 'control-state drift must force cold reconstruction'
    [System.IO.File]::WriteAllBytes($statePath, $stateBytes)
    Assert-True ((Invoke-Validation).valid) 'restored control-state bytes must validate'

    $activePath = Join-Path $runDirectory 'active'
    $activeMarker = [System.IO.File]::ReadAllBytes($activePath)
    Set-Content -LiteralPath $activePath -Value "kicktippai.orchestrate/v4 revision=$($revision - 1)"
    $markerOutput = & $hook -RepositoryRoot $testRoot -InputJson (
        New-HookInput -EventName 'SessionStart') | ConvertFrom-Json
    Assert-True ($markerOutput.hookSpecificOutput.additionalContext -match 'activation-revision-mismatch') 'activation marker drift must select cold exact-session recovery'
    [System.IO.File]::WriteAllBytes($activePath, $activeMarker)

    $preCompact = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput -EventName 'PreCompact')
    Assert-True ([string]::IsNullOrWhiteSpace([string] $preCompact)) 'valid pre-compaction checks must remain silent'
    $sessionStart = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput -EventName 'SessionStart') | ConvertFrom-Json
    Assert-True ($sessionStart.hookSpecificOutput.additionalContext -match 'HOT \$orchestrate RECOVERY') 'valid session recovery must inject the hot capsule'

    $integratedState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $integratedState.lanes[0].status = 'integrated'
    $integratedState.lanes[0].next_action = 'Retire the completed lane.'
    $integratedLane = Invoke-Checkpoint @{
        Action = 'Update'
        RunId = $runId
        ExpectedRevision = $revision
        StateJson = ($integratedState | ConvertTo-Json -Depth 20 -Compress)
        CheckpointKind = @('Integration')
        TransitionName = 'lane-a:integrate'
    }
    $revision++
    Assert-True ($integratedLane.revision -eq $revision) 'lane integration must be checkpointed'

    $ready = Invoke-Checkpoint @{
        Action = 'Transition'
        RunId = $runId
        ExpectedRevision = $revision
        Status = 'ready'
        TransitionName = 'run:ready'
        NextRootAction = 'Start the admitted wave.'
    }
    $revision++
    Assert-True ($ready.revision -eq $revision) 'preview must transition to ready'
    $activeRun = Invoke-Checkpoint @{
        Action = 'Transition'
        RunId = $runId
        ExpectedRevision = $revision
        Status = 'active'
        TransitionName = 'run:activate'
        NextRootAction = 'Finish accepted lanes.'
    }
    $revision++
    Assert-True ($activeRun.revision -eq $revision) 'ready must transition to active'

    $complete = Invoke-Checkpoint @{
        Action = 'Complete'
        RunId = $runId
        ExpectedRevision = $revision
        TransitionName = 'run:complete'
    }
    Assert-True ($complete.status -eq 'complete') 'completion must be checkpointed'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $runDirectory 'active'))) 'completion must remove the active marker'
    Assert-True ((Invoke-Validation).valid) 'terminal projections must remain internally valid'

    $rootInstructions = Get-Content -LiteralPath (Join-Path $sourceRepository 'AGENTS.md') -Raw
    $autoReview = Get-Content -LiteralPath (Join-Path $sourceRepository 'AUTO-REVIEW.md') -Raw
    Assert-True ($rootInstructions -notmatch 'Explicit Orchestration Workflow') 'root AGENTS.md must not own orchestration procedure'
    Assert-True ($autoReview -notmatch 'Explicit.*orchestrate') 'always-loaded review instructions must not duplicate orchestration authority'

    Write-Output 'Orchestration checkpoint and capsule-hook tests passed.'
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
