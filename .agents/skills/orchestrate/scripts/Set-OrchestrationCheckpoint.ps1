[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet(
        'Initialize', 'Update', 'Transition', 'SetGate', 'ClearGate',
        'ReserveCorrection', 'MarkCorrectionStarted',
        'ReleaseUnstartedCorrection', 'TripMemoryCircuitBreaker',
        'ReserveMemoryRetry', 'ClearMemoryCircuitBreaker', 'ParkWorktree',
        'Complete', 'Stop')]
    [string] $Action,
    [Parameter(Mandatory)][string] $RunId,
    [Parameter(Mandatory)][ValidateRange(0, [long]::MaxValue)]
    [long] $ExpectedRevision,
    [string] $RepositoryRoot,
    [string] $Objective,
    [string] $StopCondition,
    [string] $InitialSha,
    [string] $AllowedBranchPrefix,
    [string] $IntegrationBranch = 'main',
    [string] $NextRootAction,
    [string] $Status,
    [string] $Wave,
    [string] $StateJson,
    [ValidateSet(
        'Freeze', 'MaterialRefreeze', 'Blocker', 'OwnershipReservation',
        'WorktreeReservation', 'HeavyReservation', 'ResourceVerdict',
        'Retention', 'ReviewedCandidate', 'Integration', 'Publication')]
    [string[]] $CheckpointKind = @(),
    [string] $TransitionName,
    [string] $GateJson,
    [string] $GateId,
    [string] $ClearanceEvidence,
    [string] $MilestoneId,
    [ValidateSet('milestone-writer', 'implementation-reviewer')]
    [string] $CorrectionRole,
    [string] $AssignmentId,
    [string] $OperationProfile,
    [string] $OperationFingerprint,
    [string] $Operation,
    [string] $Reason,
    [string] $ReviewedBy,
    [string] $WorktreePath,
    [string[]] $InstructionInput = @(),
    [string[]] $ActiveContract = @(),
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'
$canonicalPushUrl = 'https://github.com/ehonda/KicktippAi.git'
$allowedStatuses = @('preview', 'awaiting-owner', 'ready', 'active', 'complete', 'stopped')
$terminalStatuses = @('complete', 'stopped')
$allowedLaneStatuses = @(
    'needs-interview', 'deferred', 'blocked', 'ready', 'active', 'review',
    'correction', 'needs-diagnosis', 'accepted', 'integrated', 'complete')
$taskRoleIds = @(
    'intake-research', 'architecture-lead', 'specification-reviewer',
    'milestone-writer', 'implementation-reviewer',
    'cumulative-validator-triage', 'deep-diagnosis',
    'architecture-reconciliation', 'final-acceptance-reviewer',
    'ci-status-monitor')
$gateCategories = @(
    'owner', 'authority', 'production-continuity', 'dependency', 'validation',
    'resource', 'integration', 'publication', 'recovery')

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else { $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot) }

function Assert-SafeRunId {
    param([Parameter(Mandatory)][string] $Identifier)
    if (
        $Identifier.Length -gt 128 -or $Identifier -in @('.', '..') -or
        [System.IO.Path]::GetFileName($Identifier) -ne $Identifier -or
        $Identifier -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
        throw 'The orchestration run ID is not a safe path segment.'
    }
}

function Write-AtomicUtf8File {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Content)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($Path)) -Force | Out-Null
    $temporaryPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath, $Content, [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Read-JsonValue {
    param([Parameter(Mandatory)][string] $Value)
    $content = if (Test-Path -LiteralPath $Value -PathType Leaf) {
        Get-Content -LiteralPath $Value -Raw
    }
    else { $Value }
    return $content | ConvertFrom-Json
}

function Copy-JsonObject {
    param([Parameter(Mandatory)] $Value)
    return ($Value | ConvertTo-Json -Depth 30 -Compress) | ConvertFrom-Json
}

function ConvertTo-CanonicalValue {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Array]) {
        return @($Value | ForEach-Object { ConvertTo-CanonicalValue -Value $_ })
    }
    if ($Value -is [pscustomobject] -or $Value -is [System.Collections.IDictionary]) {
        $result = [ordered] @{}
        $names = if ($Value -is [System.Collections.IDictionary]) {
            @($Value.Keys | ForEach-Object { [string] $_ })
        }
        else { @($Value.PSObject.Properties.Name) }
        foreach ($name in @($names | Sort-Object -CaseSensitive)) {
            $child = if ($Value -is [System.Collections.IDictionary]) { $Value[$name] } else { $Value.$name }
            $result[$name] = ConvertTo-CanonicalValue -Value $child
        }
        return [pscustomobject] $result
    }
    return $Value
}

function Get-CanonicalJson {
    param($Value)
    return (ConvertTo-CanonicalValue -Value $Value) | ConvertTo-Json -Depth 30 -Compress
}

function Test-JsonEqual {
    param($Left, $Right)
    return (Get-CanonicalJson -Value $Left) -ceq (Get-CanonicalJson -Value $Right)
}

function Assert-TransitionNotGated {
    param([Parameter(Mandatory)] $State, [Parameter(Mandatory)][string] $Name)
    if ([string]::IsNullOrWhiteSpace($Name)) {
        throw 'This checkpoint action requires a stable TransitionName.'
    }
    $blockingGate = @($State.gates | Where-Object {
        @($_.prohibited_transitions) -ccontains $Name
    }) | Select-Object -First 1
    if ($null -ne $blockingGate) {
        throw "Transition '$Name' is prohibited by active gate '$($blockingGate.id)'."
    }
}

function Assert-LifecycleTarget {
    param(
        [Parameter(Mandatory)][string] $CurrentStatus,
        [Parameter(Mandatory)][string] $TargetStatus
    )
    $allowedTargets = switch ($CurrentStatus) {
        'preview' { @('awaiting-owner', 'ready', 'stopped') }
        'awaiting-owner' { @('preview', 'ready', 'stopped') }
        'ready' { @('preview', 'awaiting-owner', 'active', 'stopped') }
        'active' { @('awaiting-owner', 'complete', 'stopped') }
        default { @() }
    }
    if ($TargetStatus -cne $CurrentStatus -and $TargetStatus -notin $allowedTargets) {
        throw "Lifecycle transition '$CurrentStatus -> $TargetStatus' is not allowed."
    }
}

function Get-ChangedProperties {
    param([Parameter(Mandatory)] $Before, [Parameter(Mandatory)] $After)
    $names = @($Before.PSObject.Properties.Name + $After.PSObject.Properties.Name |
        Sort-Object -CaseSensitive -Unique)
    return @($names | Where-Object { -not (Test-JsonEqual -Left $Before.$_ -Right $After.$_) })
}

function Set-Property {
    param([Parameter(Mandatory)] $Object, [Parameter(Mandatory)][string] $Name, $Value)
    if ($Object.PSObject.Properties.Name -contains $Name) { $Object.$Name = $Value }
    else { $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value }
}

function Assert-StringArray {
    param($Value, [Parameter(Mandatory)][string] $Label)
    if (-not ($Value -is [System.Array]) -or
        @($Value | Where-Object { [string]::IsNullOrWhiteSpace([string] $_) }).Count -gt 0) {
        throw "$Label must be an array of non-empty strings."
    }
}

function Assert-ControlState {
    param([Parameter(Mandatory)] $State)
    $required = @(
        'schema_version', 'session_id', 'run_id', 'revision', 'updated_at_utc',
        'status', 'objective', 'wave', 'stop_condition', 'durable_decisions',
        'lanes', 'gates', 'instruction_inputs', 'active_contracts', 'git',
        'evidence_references',
        'ownership_reservations', 'correction_counters', 'resource_state',
        'retained_agents', 'next_root_action', 'delegated_next_actions')
    foreach ($field in $required) {
        if ($State.PSObject.Properties.Name -notcontains $field) {
            throw "control-state.json is missing '$field'."
        }
    }
    if ([int] $State.schema_version -ne 1) { throw 'Unsupported control-state schema.' }
    if ([string] $State.session_id -cne $RunId -or [string] $State.run_id -cne $RunId) {
        throw 'Control-state identity does not match the exact run ID.'
    }
    if ([long] $State.revision -lt 1) { throw 'Control-state revision must be positive.' }
    if ([string] $State.status -notin $allowedStatuses) { throw 'Invalid lifecycle status.' }
    foreach ($field in @('updated_at_utc', 'objective', 'wave', 'stop_condition')) {
        if ([string]::IsNullOrWhiteSpace([string] $State.$field)) {
            throw "Control-state field '$field' must not be empty."
        }
    }
    if ([string] $State.status -notin $terminalStatuses -and
        [string]::IsNullOrWhiteSpace([string] $State.next_root_action)) {
        throw 'An active run requires next_root_action.'
    }
    foreach ($field in @(
        'durable_decisions', 'lanes', 'gates', 'instruction_inputs',
        'active_contracts', 'evidence_references', 'ownership_reservations', 'correction_counters',
        'retained_agents', 'delegated_next_actions')) {
        if (-not ($State.$field -is [System.Array])) {
            throw "Control-state field '$field' must be an array."
        }
    }
    foreach ($field in @(
        'durable_decisions', 'instruction_inputs', 'active_contracts', 'evidence_references',
        'delegated_next_actions')) {
        Assert-StringArray -Value $State.$field -Label $field
    }
    $parsed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string] $State.updated_at_utc, [ref] $parsed)) {
        throw 'Control-state timestamp is invalid.'
    }
    if (
        [string] $State.git.remote -cne 'origin' -or
        [string] $State.git.push_url -cne $canonicalPushUrl -or
        [string] $State.git.allowed_branch_prefix -notmatch '^codex/[A-Za-z0-9][A-Za-z0-9._-]*-$' -or
        [string] $State.git.initial_sha -notmatch '^[A-Fa-f0-9]{40}$') {
        throw 'Control-state Git target does not match the canonical allowlist.'
    }
    if (
        [string] $State.git.integration_branch -cne 'main' -and
        -not ([string] $State.git.integration_branch).StartsWith(
            [string] $State.git.allowed_branch_prefix,
            [System.StringComparison]::Ordinal)) {
        throw 'The integration branch is outside the control-state allowlist.'
    }
    foreach ($field in @('latest_integrated_sha', 'latest_pushed_sha', 'reviewed_unpublished_sha')) {
        $sha = [string] $State.git.$field
        if (-not [string]::IsNullOrWhiteSpace($sha) -and $sha -notmatch '^[A-Fa-f0-9]{40}$') {
            throw "Control-state Git field '$field' must be empty or an exact commit SHA."
        }
    }
    $laneIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($lane in @($State.lanes)) {
        foreach ($field in @('id', 'status', 'dependencies', 'owner', 'worktree_path', 'next_action')) {
            if ($lane.PSObject.Properties.Name -notcontains $field) {
                throw "Lane entry requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $lane.id) -or
            -not $laneIds.Add([string] $lane.id) -or
            [string] $lane.status -notin $allowedLaneStatuses -or
            [string]::IsNullOrWhiteSpace([string] $lane.next_action) -or
            -not ($lane.dependencies -is [System.Array]) -or
            @($lane.dependencies | Where-Object { [string]::IsNullOrWhiteSpace([string] $_) }).Count -gt 0) {
            throw 'Lane entry is invalid.'
        }
    }
    if ([string] $State.status -eq 'awaiting-owner') {
        $runDecisionGates = @($State.gates | Where-Object {
            [string] $_.scope -eq 'run' -and
            [string] $_.category -in @('owner', 'authority')
        })
        $continuingLane = @($State.lanes | Where-Object {
            [string] $_.status -in @('ready', 'active', 'review', 'correction', 'accepted')
        }) | Select-Object -First 1
        if ($runDecisionGates.Count -eq 0 -or
            $null -ne $continuingLane -or
            @($State.delegated_next_actions).Count -gt 0 -or
            @($runDecisionGates.continue_actions).Count -gt 0) {
            throw 'awaiting-owner is valid only when a run-wide owner/authority gate blocks every remaining ready path.'
        }
    }
    foreach ($lane in @($State.lanes)) {
        foreach ($dependency in @($lane.dependencies)) {
            if (-not $laneIds.Contains([string] $dependency) -or [string] $dependency -ceq [string] $lane.id) {
                throw "Lane '$($lane.id)' has an invalid dependency '$dependency'."
            }
        }
    }
    $resolvedLanes = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    while ($resolvedLanes.Count -lt $laneIds.Count) {
        $progress = $false
        foreach ($lane in @($State.lanes | Where-Object {
            -not $resolvedLanes.Contains([string] $_.id)
        })) {
            if (@($lane.dependencies | Where-Object {
                -not $resolvedLanes.Contains([string] $_)
            }).Count -eq 0) {
                [void] $resolvedLanes.Add([string] $lane.id)
                $progress = $true
            }
        }
        if (-not $progress) { throw 'The lane dependency graph contains a cycle.' }
    }
    $gateIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($gate in @($State.gates)) {
        foreach ($field in @(
            'id', 'category', 'scope', 'evidence', 'prohibited_transitions',
            'remediation_owner', 'remediation_action', 'continue_actions',
            'escalation_condition', 'retry_budget', 'retry_attempts')) {
            if ($gate.PSObject.Properties.Name -notcontains $field) {
                throw "Gate entry requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $gate.id) -or
            -not $gateIds.Add([string] $gate.id) -or
            [string] $gate.category -notin $gateCategories -or
            [string]::IsNullOrWhiteSpace([string] $gate.scope) -or
            [string]::IsNullOrWhiteSpace([string] $gate.evidence) -or
            [string]::IsNullOrWhiteSpace([string] $gate.remediation_owner) -or
            [string]::IsNullOrWhiteSpace([string] $gate.remediation_action) -or
            [string]::IsNullOrWhiteSpace([string] $gate.escalation_condition) -or
            [int] $gate.retry_budget -lt 0 -or
            [int] $gate.retry_attempts -lt 0 -or
            [int] $gate.retry_attempts -gt [int] $gate.retry_budget) {
            throw 'Gate entry is invalid.'
        }
        Assert-StringArray -Value $gate.prohibited_transitions -Label 'gate.prohibited_transitions'
        Assert-StringArray -Value $gate.continue_actions -Label 'gate.continue_actions'
    }
    $counterKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($counter in @($State.correction_counters)) {
        foreach ($field in @(
            'milestone_id', 'role', 'used', 'issued_assignment_ids',
            'pending_assignment_id')) {
            if ($counter.PSObject.Properties.Name -notcontains $field) {
                throw "Correction counter requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $counter.milestone_id) -or
            -not $laneIds.Contains([string] $counter.milestone_id) -or
            [string] $counter.role -notin @('milestone-writer', 'implementation-reviewer') -or
            [int] $counter.used -lt 0 -or
            -not $counterKeys.Add("$($counter.milestone_id)`0$($counter.role)") -or
            -not ($counter.issued_assignment_ids -is [System.Array]) -or
            @($counter.issued_assignment_ids).Count -ne [int] $counter.used -or
            @($counter.issued_assignment_ids | Where-Object {
                [string]::IsNullOrWhiteSpace([string] $_)
            }).Count -gt 0 -or
            @($counter.issued_assignment_ids | Sort-Object -Unique).Count -ne
                @($counter.issued_assignment_ids).Count -or
            ($null -ne $counter.pending_assignment_id -and
             ([string]::IsNullOrWhiteSpace([string] $counter.pending_assignment_id) -or
              @($counter.issued_assignment_ids) -cnotcontains [string] $counter.pending_assignment_id))) {
            throw 'Correction counter is invalid.'
        }
        $maximum = if ([string] $counter.role -eq 'milestone-writer') { 3 } else { 2 }
        if ([int] $counter.used -gt $maximum) { throw 'Correction counter exceeds its hard limit.' }
    }
    foreach ($field in @('worktree_admission', 'heavy_admission')) {
        if ([string] $State.resource_state.$field -notin @('unknown', 'allowed', 'denied')) {
            throw "Invalid resource verdict '$field'."
        }
    }
    foreach ($field in @(
        'disk_warning_band', 'memory_warning_band', 'owner_override',
        'worktree_reservations', 'active_heavy_reservations',
        'circuit_breaker_mode', 'circuit_breaker_trigger', 'degraded_profiles')) {
        if ($State.resource_state.PSObject.Properties.Name -notcontains $field) {
            throw "resource_state is missing '$field'."
        }
    }
    foreach ($field in @('disk_warning_band', 'memory_warning_band')) {
        if ([string] $State.resource_state.$field -notin @('unknown', 'normal', 'warning')) {
            throw "Invalid resource warning band '$field'."
        }
    }
    if ([string] $State.resource_state.circuit_breaker_mode -notin @('normal', 'degraded')) {
        throw 'Invalid circuit-breaker mode.'
    }
    foreach ($field in @(
        'worktree_reservations', 'active_heavy_reservations', 'degraded_profiles')) {
        if (-not ($State.resource_state.$field -is [System.Array])) {
            throw "resource_state.$field must be an array."
        }
    }
    if ([string] $State.resource_state.circuit_breaker_mode -eq 'normal') {
        if ($null -ne $State.resource_state.circuit_breaker_trigger -or
            @($State.resource_state.degraded_profiles).Count -ne 0) {
            throw 'Normal circuit-breaker mode cannot retain a trigger or degraded profile.'
        }
    }
    else {
        $trigger = $State.resource_state.circuit_breaker_trigger
        if ($null -eq $trigger) { throw 'Degraded circuit-breaker mode requires a trigger.' }
        foreach ($field in @('run_id', 'profile', 'fingerprint', 'operation', 'at_utc', 'reason')) {
            if ($trigger.PSObject.Properties.Name -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $trigger.$field)) {
                throw "Circuit-breaker trigger requires '$field'."
            }
        }
        if ([string] $trigger.run_id -cne $RunId) {
            throw 'Circuit-breaker trigger belongs to another run.'
        }
        $triggerTime = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse([string] $trigger.at_utc, [ref] $triggerTime)) {
            throw 'Circuit-breaker trigger timestamp is invalid.'
        }
        if (@($State.resource_state.degraded_profiles).Count -ne 1) {
            throw 'Degraded mode requires exactly one offending profile override.'
        }
        $degraded = @($State.resource_state.degraded_profiles)[0]
        foreach ($field in @(
            'profile', 'fingerprint', 'exclusive', 'maximum_worker_fanout',
            'recoverable_retry_limit', 'recoverable_retries_used')) {
            if ($degraded.PSObject.Properties.Name -notcontains $field) {
                throw "Degraded profile requires '$field'."
            }
        }
        if (
            [string] $degraded.profile -cne [string] $trigger.profile -or
            [string] $degraded.fingerprint -cne [string] $trigger.fingerprint -or
            -not [bool] $degraded.exclusive -or
            [int] $degraded.maximum_worker_fanout -lt 1 -or
            [int] $degraded.recoverable_retry_limit -lt 1 -or
            [int] $degraded.recoverable_retries_used -lt 0 -or
            [int] $degraded.recoverable_retries_used -gt [int] $degraded.recoverable_retry_limit) {
            throw 'The degraded profile override is invalid.'
        }
    }
    $worktreePaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($reservation in @($State.resource_state.worktree_reservations)) {
        foreach ($field in @(
            'path', 'class', 'growth_reservation_gib', 'owner', 'branch', 'tip',
            'remediation')) {
            if ($reservation.PSObject.Properties.Name -notcontains $field) {
                throw "Worktree reservation requires '$field'."
            }
        }
        $expectedGrowth = if ([string] $reservation.class -in @(
            'active-build-capable', 'uncertain')) { 1.25 } else { 0.0 }
        if (
            [string]::IsNullOrWhiteSpace([string] $reservation.path) -or
            -not $worktreePaths.Add([string] $reservation.path) -or
            [string] $reservation.class -notin @(
                'active-build-capable', 'parked-recovery-only',
                'removal-ready', 'uncertain') -or
            [double] $reservation.growth_reservation_gib -ne $expectedGrowth -or
            [string]::IsNullOrWhiteSpace([string] $reservation.branch) -or
            [string] $reservation.tip -notmatch '^[A-Fa-f0-9]{40}$' -or
            ([string] $reservation.class -eq 'parked-recovery-only' -and
             [string]::IsNullOrWhiteSpace([string] $reservation.remediation))) {
            throw 'Worktree reservation is invalid.'
        }
    }
    $heavyIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($reservation in @($State.resource_state.active_heavy_reservations)) {
        foreach ($field in @(
            'id', 'owner', 'profile', 'fingerprint', 'memory_reservation_gib',
            'recoverable', 'effect_class', 'worker_cap', 'fanout_controllable',
            'exclusive')) {
            if ($reservation.PSObject.Properties.Name -notcontains $field) {
                throw "Heavy reservation requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $reservation.id) -or
            -not $heavyIds.Add([string] $reservation.id) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.owner) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.profile) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.fingerprint) -or
            [double] $reservation.memory_reservation_gib -le 0 -or
            [string] $reservation.effect_class -notin @('local-only', 'external-or-live') -or
            [int] $reservation.worker_cap -lt 1 -or
            (-not [bool] $reservation.fanout_controllable -and -not [bool] $reservation.exclusive)) {
            throw 'Heavy reservation is invalid.'
        }
    }
    $assignmentIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $reservedLaneIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($reservation in @($State.ownership_reservations)) {
        foreach ($field in @(
            'assignment_id', 'lane_id', 'agent_path', 'role', 'model',
            'reasoning_effort', 'owned_paths', 'next_action')) {
            if ($reservation.PSObject.Properties.Name -notcontains $field) {
                throw "Ownership reservation requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $reservation.assignment_id) -or
            -not $assignmentIds.Add([string] $reservation.assignment_id) -or
            -not $laneIds.Contains([string] $reservation.lane_id) -or
            -not $reservedLaneIds.Add([string] $reservation.lane_id) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.agent_path) -or
            [string] $reservation.role -notin $taskRoleIds -or
            [string]::IsNullOrWhiteSpace([string] $reservation.model) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.reasoning_effort) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.next_action) -or
            -not ($reservation.owned_paths -is [System.Array]) -or
            @($reservation.owned_paths).Count -eq 0 -or
            @($reservation.owned_paths | Where-Object {
                [string]::IsNullOrWhiteSpace([string] $_)
            }).Count -gt 0) {
            throw 'Ownership reservation is invalid.'
        }
        $ownedLane = @($State.lanes | Where-Object {
            [string] $_.id -ceq [string] $reservation.lane_id
        }) | Select-Object -First 1
        if ([string] $ownedLane.owner -cne [string] $reservation.agent_path) {
            throw "Ownership reservation '$($reservation.assignment_id)' does not match its lane owner."
        }
    }
    foreach ($lane in @($State.lanes | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string] $_.owner)
    })) {
        if (@($State.ownership_reservations | Where-Object {
            [string] $_.lane_id -ceq [string] $lane.id -and
            [string] $_.agent_path -ceq [string] $lane.owner
        }).Count -ne 1) {
            throw "Lane '$($lane.id)' owner does not match one current ownership reservation."
        }
    }
    foreach ($retained in @($State.retained_agents)) {
        foreach ($field in @('agent_path', 'role', 'reason', 'release_trigger', 'context_cost_limit')) {
            if ($retained.PSObject.Properties.Name -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $retained.$field)) {
                throw "Retained agent requires non-empty '$field'."
            }
        }
    }
    if ($null -ne $State.resource_state.owner_override) {
        foreach ($field in @('scope', 'reason', 'reserved_capacity')) {
            if ($State.resource_state.owner_override.PSObject.Properties.Name -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $State.resource_state.owner_override.$field)) {
                throw "Owner resource override requires non-empty '$field'."
            }
        }
    }
}

function Render-Preview {
    param([Parameter(Mandatory)] $State)
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in @(
        "# Orchestration preview - $RunId", '',
        "- State revision: $($State.revision)",
        "- Objective: $($State.objective)",
        "- Lifecycle: $($State.status)",
        "- Wave: $($State.wave)", '', '## Current graph', '')) { $lines.Add($line) }
    if (@($State.lanes).Count -eq 0) { $lines.Add('- No lanes frozen yet.') }
    foreach ($lane in @($State.lanes)) {
        $owner = if (-not [string]::IsNullOrWhiteSpace([string] $lane.owner)) {
            [string] $lane.owner
        }
        else { 'unassigned' }
        $next = if ($lane.PSObject.Properties.Name -contains 'next_action') { [string] $lane.next_action } else { '' }
        $lines.Add("- $($lane.id) [$($lane.status)]; owner: $owner; next: $next")
    }
    $lines.AddRange([string[]] @('', '## Gates and reservations', ''))
    if (@($State.gates).Count -eq 0) { $lines.Add('- No active gates.') }
    foreach ($gate in @($State.gates)) {
        $lines.Add("- $($gate.id) [$($gate.category)/$($gate.scope)]: $($gate.evidence)")
    }
    $lines.Add("- Worktree reservations: $(@($State.resource_state.worktree_reservations).Count)")
    $lines.Add("- Heavy reservations: $(@($State.resource_state.active_heavy_reservations).Count)")
    $lines.AddRange([string[]] @('', '## Durable decisions', ''))
    if (@($State.durable_decisions).Count -eq 0) { $lines.Add('- None recorded.') }
    foreach ($decision in @($State.durable_decisions)) { $lines.Add("- $decision") }
    $lines.AddRange([string[]] @('', '## Evidence references', ''))
    if (@($State.evidence_references).Count -eq 0) { $lines.Add('- None recorded.') }
    foreach ($evidence in @($State.evidence_references)) { $lines.Add("- $evidence") }
    $lines.AddRange([string[]] @('', '## Integration and next actions', ''))
    $lines.Add("- Integration branch: $($State.git.integration_branch)")
    $lines.Add("- Allowed branch prefix: $($State.git.allowed_branch_prefix)")
    $lines.Add("- Root: $($State.next_root_action)")
    foreach ($next in @($State.delegated_next_actions)) { $lines.Add("- Delegated: $next") }
    $lines.AddRange([string[]] @('', '<!-- orchestration-instruction-inputs:start -->'))
    foreach ($path in @($State.instruction_inputs | Sort-Object -Unique)) { $lines.Add([string] $path) }
    $lines.Add('<!-- orchestration-instruction-inputs:end -->')
    $lines.AddRange([string[]] @('', '<!-- orchestration-active-contract:start -->'))
    foreach ($path in @($State.active_contracts | Sort-Object -Unique)) { $lines.Add([string] $path) }
    $lines.Add('<!-- orchestration-active-contract:end -->')
    return ($lines -join [Environment]::NewLine) + [Environment]::NewLine
}

function New-Capsule {
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $ControlStateSha256,
        [Parameter(Mandatory)] $InstructionManifest,
        [Parameter(Mandatory)] $HookManifest,
        [Parameter(Mandatory)] $ActiveContractManifest
    )
    return [ordered] @{
        schema_version = 4
        state_revision = [long] $State.revision
        control_state_sha256 = $ControlStateSha256
        session_id = $RunId
        run_id = $RunId
        updated_at_utc = [string] $State.updated_at_utc
        status = [string] $State.status
        objective = [string] $State.objective
        wave = [string] $State.wave
        stop_condition = [string] $State.stop_condition
        durable_decisions = @($State.durable_decisions)
        evidence_references = @($State.evidence_references)
        gates = @($State.gates)
        freeze = [ordered] @{
            preview_path = ".tmp/orchestration/$RunId/preview.md"
            instruction_manifest = [ordered] @{
                path = [string] $InstructionManifest.path
                sha256 = [string] $InstructionManifest.sha256
            }
            hook_manifest = [ordered] @{
                path = [string] $HookManifest.path
                sha256 = [string] $HookManifest.sha256
            }
            active_contract_manifest = [ordered] @{
                path = [string] $ActiveContractManifest.path
                sha256 = [string] $ActiveContractManifest.sha256
            }
        }
        git = $State.git
        ownership_reservations = @($State.ownership_reservations)
        correction_counters = @($State.correction_counters)
        resource_state = $State.resource_state
        retained_agents = @($State.retained_agents)
        next_root_action = [string] $State.next_root_action
        delegated_next_actions = @($State.delegated_next_actions)
    }
}

Assert-SafeRunId -Identifier $RunId
$runDirectory = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".tmp/orchestration/$RunId"))
$orchestrationRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot '.tmp/orchestration'))
$rootPrefix = $orchestrationRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $runDirectory.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The run directory escaped the orchestration root.'
}
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$statePath = Join-Path $runDirectory 'control-state.json'
$lockPath = Join-Path $runDirectory '.checkpoint.lock'
$stageDirectory = Join-Path $runDirectory ".checkpoint-stage-$([Guid]::NewGuid().ToString('N'))"
$lockStream = $null
$correctionAllowed = $null
$memoryRetryAllowed = $null

try {
    $lockStream = [System.IO.File]::Open(
        $lockPath, [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $current = $null
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        $current = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    }

    if ($Action -eq 'Initialize') {
        if ($null -ne $current -or $ExpectedRevision -ne 0) {
            throw 'Initialize requires no current state and ExpectedRevision 0.'
        }
        foreach ($pair in @(
            @{ Label = 'Objective'; Value = $Objective },
            @{ Label = 'StopCondition'; Value = $StopCondition },
            @{ Label = 'InitialSha'; Value = $InitialSha },
            @{ Label = 'AllowedBranchPrefix'; Value = $AllowedBranchPrefix },
            @{ Label = 'IntegrationBranch'; Value = $IntegrationBranch },
            @{ Label = 'NextRootAction'; Value = $NextRootAction })) {
            if ([string]::IsNullOrWhiteSpace([string] $pair.Value)) {
                throw "Initialize requires $($pair.Label)."
            }
        }
        $prospective = [pscustomobject] [ordered] @{
            schema_version = 1
            session_id = $RunId
            run_id = $RunId
            revision = 1
            updated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
            status = 'preview'
            objective = $Objective
            wave = 'preview'
            stop_condition = $StopCondition
            durable_decisions = @()
            lanes = @()
            gates = @()
            instruction_inputs = @($InstructionInput | Sort-Object -Unique)
            active_contracts = @($ActiveContract | Sort-Object -Unique)
            evidence_references = @()
            git = [pscustomobject] [ordered] @{
                remote = 'origin'
                push_url = $canonicalPushUrl
                integration_branch = $IntegrationBranch
                allowed_branch_prefix = $AllowedBranchPrefix
                initial_sha = $InitialSha
                latest_integrated_sha = ''
                latest_pushed_sha = ''
                reviewed_unpublished_sha = ''
            }
            ownership_reservations = @()
            correction_counters = @()
            resource_state = [pscustomobject] [ordered] @{
                worktree_admission = 'unknown'
                heavy_admission = 'unknown'
                disk_warning_band = 'unknown'
                memory_warning_band = 'unknown'
                owner_override = $null
                worktree_reservations = @()
                active_heavy_reservations = @()
                circuit_breaker_mode = 'normal'
                circuit_breaker_trigger = $null
                degraded_profiles = @()
            }
            retained_agents = @()
            next_root_action = $NextRootAction
            delegated_next_actions = @()
        }
    }
    else {
        if ($null -eq $current) { throw 'Current control-state.json is missing.' }
        Assert-ControlState -State $current
        if ([long] $current.revision -ne $ExpectedRevision) {
            throw "Revision conflict: expected $ExpectedRevision, current $($current.revision)."
        }
        if ([string] $current.status -in $terminalStatuses -and -not (
            ($Action -eq 'Complete' -and [string] $current.status -eq 'complete') -or
            ($Action -eq 'Stop' -and [string] $current.status -eq 'stopped') -or
            ($Action -eq 'Transition' -and [string] $Status -eq [string] $current.status))) {
            throw 'A terminal orchestration run cannot be mutated.'
        }
        $prospective = if ($Action -eq 'Update') {
            if ([string]::IsNullOrWhiteSpace($StateJson)) { throw 'Update requires StateJson.' }
            Read-JsonValue -Value $StateJson
        }
        else { Copy-JsonObject -Value $current }

        switch ($Action) {
            'Transition' {
                if ([string]::IsNullOrWhiteSpace($Status) -or $Status -notin $allowedStatuses) {
                    throw 'Transition requires a valid Status.'
                }
                Assert-TransitionNotGated -State $current -Name $TransitionName
                Assert-LifecycleTarget -CurrentStatus ([string] $current.status) -TargetStatus $Status
                $prospective.status = $Status
                if (-not [string]::IsNullOrWhiteSpace($Wave)) { $prospective.wave = $Wave }
                if (-not [string]::IsNullOrWhiteSpace($NextRootAction)) {
                    $prospective.next_root_action = $NextRootAction
                }
            }
            'SetGate' {
                if ([string]::IsNullOrWhiteSpace($GateJson)) { throw 'SetGate requires GateJson.' }
                $gate = Read-JsonValue -Value $GateJson
                $existingGate = @($prospective.gates | Where-Object {
                    [string] $_.id -ceq [string] $gate.id
                }) | Select-Object -First 1
                if ($null -ne $existingGate -and (
                    [string] $existingGate.category -cne [string] $gate.category -or
                    [string] $existingGate.scope -cne [string] $gate.scope -or
                    [int] $existingGate.retry_budget -ne [int] $gate.retry_budget -or
                    [int] $gate.retry_attempts -lt [int] $existingGate.retry_attempts)) {
                    throw 'An existing gate cannot change identity/scope/budget or decrement retry attempts.'
                }
                $prospective.gates = @(
                    @($prospective.gates | Where-Object { $_.id -cne $gate.id }) + $gate |
                        Sort-Object -Property id -CaseSensitive)
                if (-not [string]::IsNullOrWhiteSpace($Status)) {
                    Assert-TransitionNotGated -State $prospective -Name $TransitionName
                    Assert-LifecycleTarget -CurrentStatus ([string] $current.status) -TargetStatus $Status
                    $prospective.status = $Status
                    if (-not [string]::IsNullOrWhiteSpace($NextRootAction)) {
                        $prospective.next_root_action = $NextRootAction
                    }
                }
            }
            'ClearGate' {
                if ([string]::IsNullOrWhiteSpace($GateId)) { throw 'ClearGate requires GateId.' }
                $existingGate = @($prospective.gates | Where-Object {
                    [string] $_.id -ceq $GateId
                }) | Select-Object -First 1
                if ($null -ne $existingGate -and
                    [string]::IsNullOrWhiteSpace($ClearanceEvidence)) {
                    throw 'Clearing an active gate requires concise ClearanceEvidence.'
                }
                $prospective.gates = @($prospective.gates | Where-Object { $_.id -cne $GateId })
                if (-not [string]::IsNullOrWhiteSpace($Status)) {
                    Assert-TransitionNotGated -State $prospective -Name $TransitionName
                    Assert-LifecycleTarget -CurrentStatus ([string] $current.status) -TargetStatus $Status
                    $prospective.status = $Status
                    if (-not [string]::IsNullOrWhiteSpace($NextRootAction)) {
                        $prospective.next_root_action = $NextRootAction
                    }
                }
            }
            'ReserveCorrection' {
                foreach ($value in @($MilestoneId, $CorrectionRole, $AssignmentId)) {
                    if ([string]::IsNullOrWhiteSpace($value)) {
                        throw 'ReserveCorrection requires MilestoneId, CorrectionRole, and AssignmentId.'
                    }
                }
                Assert-TransitionNotGated -State $current -Name $TransitionName
                $lane = @($prospective.lanes | Where-Object {
                    $_.id -ceq $MilestoneId
                }) | Select-Object -First 1
                if ($null -eq $lane) {
                    throw "Correction milestone '$MilestoneId' is not a current lane."
                }
                if (@($prospective.correction_counters.issued_assignment_ids) -ccontains $AssignmentId) {
                    throw "Correction assignment ID '$AssignmentId' was already issued."
                }
                $counter = @($prospective.correction_counters | Where-Object {
                    $_.milestone_id -ceq $MilestoneId -and $_.role -ceq $CorrectionRole
                }) | Select-Object -First 1
                if ($null -eq $counter) {
                    $counter = [pscustomobject] [ordered] @{
                        milestone_id = $MilestoneId
                        role = $CorrectionRole
                        used = 0
                        issued_assignment_ids = @()
                        pending_assignment_id = $null
                    }
                    $prospective.correction_counters = @($prospective.correction_counters) + $counter
                }
                if (-not [string]::IsNullOrWhiteSpace([string] $counter.pending_assignment_id)) {
                    throw 'A correction dispatch is already pending for this milestone and role.'
                }
                $maximum = if ($CorrectionRole -eq 'milestone-writer') { 3 } else { 2 }
                if ([int] $counter.used -ge $maximum) {
                    $releasedAgent = [string] $lane.owner
                    Set-Property -Object $lane -Name status -Value 'needs-diagnosis'
                    Set-Property -Object $lane -Name next_action -Value (
                        'Select a fresh diagnosis route; the correction specialist is released.')
                    Set-Property -Object $lane -Name owner -Value ''
                    if (-not [string]::IsNullOrWhiteSpace($releasedAgent)) {
                        $prospective.ownership_reservations = @(
                            $prospective.ownership_reservations | Where-Object {
                                [string] $_.agent_path -cne $releasedAgent })
                        $prospective.retained_agents = @(
                            $prospective.retained_agents | Where-Object {
                                [string] $_.agent_path -cne $releasedAgent })
                    }
                    $correctionAllowed = $false
                }
                else {
                    $counter.used = [int] $counter.used + 1
                    $counter.issued_assignment_ids = @($counter.issued_assignment_ids) + $AssignmentId
                    $counter.pending_assignment_id = $AssignmentId
                    $correctionAllowed = $true
                }
            }
            'MarkCorrectionStarted' {
                $counter = @($prospective.correction_counters | Where-Object {
                    $_.milestone_id -ceq $MilestoneId -and $_.role -ceq $CorrectionRole
                }) | Select-Object -First 1
                if ($null -eq $counter -or
                    [string] $counter.pending_assignment_id -cne $AssignmentId) {
                    throw 'No matching pending correction reservation exists.'
                }
                $counter.pending_assignment_id = $null
            }
            'ReleaseUnstartedCorrection' {
                $counter = @($prospective.correction_counters | Where-Object {
                    $_.milestone_id -ceq $MilestoneId -and $_.role -ceq $CorrectionRole
                }) | Select-Object -First 1
                if ($null -eq $counter -or
                    [string] $counter.pending_assignment_id -cne $AssignmentId) {
                    throw 'No matching unstarted correction reservation exists.'
                }
                $counter.used = [Math]::Max(0, [int] $counter.used - 1)
                $counter.issued_assignment_ids = @(
                    $counter.issued_assignment_ids | Where-Object { [string] $_ -cne $AssignmentId })
                $counter.pending_assignment_id = $null
            }
            'TripMemoryCircuitBreaker' {
                foreach ($entry in @{
                    OperationProfile = $OperationProfile
                    OperationFingerprint = $OperationFingerprint
                    Operation = $Operation
                    Reason = $Reason
                }.GetEnumerator()) {
                    if ([string]::IsNullOrWhiteSpace([string] $entry.Value)) {
                        throw "$($entry.Key) is required when tripping the memory circuit breaker."
                    }
                }
                if ([string] $prospective.resource_state.circuit_breaker_mode -eq 'degraded') {
                    throw 'The memory circuit breaker is already degraded; preserve its original trigger.'
                }
                $policyPath = Join-Path $PSScriptRoot '../resources/resource-policy.json'
                $policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
                $degraded = $policy.heavyOperation.degraded
                $triggeredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
                $prospective.resource_state.circuit_breaker_mode = 'degraded'
                $prospective.resource_state.circuit_breaker_trigger = [pscustomobject] [ordered] @{
                    run_id = $RunId
                    profile = $OperationProfile
                    fingerprint = $OperationFingerprint
                    operation = $Operation
                    at_utc = $triggeredAtUtc
                    reason = $Reason
                }
                $prospective.resource_state.degraded_profiles = @(
                    [pscustomobject] [ordered] @{
                        profile = $OperationProfile
                        fingerprint = $OperationFingerprint
                        exclusive = $true
                        maximum_worker_fanout = [int] $degraded.maximumWorkerFanout
                        recoverable_retry_limit = [int] $degraded.recoverableRetryLimit
                        recoverable_retries_used = 0
                    })
                $prospective.resource_state.heavy_admission = 'denied'
                $prospective.resource_state.memory_warning_band = 'warning'
            }
            'ClearMemoryCircuitBreaker' {
                if ([string]::IsNullOrWhiteSpace($ReviewedBy) -or
                    [string]::IsNullOrWhiteSpace($Reason)) {
                    throw 'ReviewedBy and Reason are required after owner-reviewed breaker analysis.'
                }
                if ([string] $prospective.resource_state.circuit_breaker_mode -ne 'degraded') {
                    throw 'Clearing requires a currently degraded memory circuit breaker.'
                }
                $prospective.resource_state.circuit_breaker_mode = 'normal'
                $prospective.resource_state.circuit_breaker_trigger = $null
                $prospective.resource_state.degraded_profiles = @()
                $prospective.resource_state.heavy_admission = 'unknown'
                $prospective.resource_state.memory_warning_band = 'unknown'
                $prospective.durable_decisions = @($prospective.durable_decisions) +
                    "Memory circuit breaker cleared after owner-reviewed analysis by ${ReviewedBy}: $Reason"
            }
            'ReserveMemoryRetry' {
                if ([string]::IsNullOrWhiteSpace($OperationProfile) -or
                    [string]::IsNullOrWhiteSpace($OperationFingerprint)) {
                    throw 'ReserveMemoryRetry requires OperationProfile and OperationFingerprint.'
                }
                if ([string] $prospective.resource_state.circuit_breaker_mode -ne 'degraded') {
                    throw 'A memory retry requires a degraded circuit breaker.'
                }
                $profile = @($prospective.resource_state.degraded_profiles | Where-Object {
                    [string] $_.profile -ceq $OperationProfile -and
                    [string] $_.fingerprint -ceq $OperationFingerprint
                }) | Select-Object -First 1
                if ($null -eq $profile) {
                    throw 'The requested retry does not match the offending degraded profile.'
                }
                if ([int] $profile.recoverable_retries_used -ge
                    [int] $profile.recoverable_retry_limit) {
                    $memoryRetryAllowed = $false
                }
                else {
                    $profile.recoverable_retries_used =
                        [int] $profile.recoverable_retries_used + 1
                    $memoryRetryAllowed = $true
                }
            }
            'ParkWorktree' {
                if ([string]::IsNullOrWhiteSpace($WorktreePath) -or
                    [string]::IsNullOrWhiteSpace($Reason)) {
                    throw 'ParkWorktree requires WorktreePath and Reason.'
                }
                $resolvedWorktreePath = if ([System.IO.Path]::IsPathRooted($WorktreePath)) {
                    [System.IO.Path]::GetFullPath($WorktreePath)
                }
                else { [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $WorktreePath)) }
                $reservation = @($prospective.resource_state.worktree_reservations | Where-Object {
                    try {
                        $candidatePath = if ([System.IO.Path]::IsPathRooted([string] $_.path)) {
                            [System.IO.Path]::GetFullPath([string] $_.path)
                        }
                        else {
                            [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ([string] $_.path)))
                        }
                        $candidatePath.Equals(
                            $resolvedWorktreePath,
                            [System.StringComparison]::OrdinalIgnoreCase)
                    }
                    catch { $false }
                }) | Select-Object -First 1
                if ($null -eq $reservation) {
                    throw 'ParkWorktree requires an exact current worktree reservation.'
                }
                $reservation.class = 'parked-recovery-only'
                $reservation.growth_reservation_gib = 0.0
                $reservation.remediation = $Reason
                $prospective.resource_state.worktree_admission = 'denied'
            }
            'Complete' {
                Assert-TransitionNotGated -State $current -Name $TransitionName
                if ([string] $current.status -notin @('active', 'complete')) {
                    throw 'Complete requires an active run.'
                }
                if (@($prospective.ownership_reservations).Count -gt 0 -or
                    @($prospective.resource_state.active_heavy_reservations).Count -gt 0 -or
                    @($prospective.retained_agents).Count -gt 0 -or
                    @($prospective.correction_counters | Where-Object {
                        $null -ne $_.pending_assignment_id }).Count -gt 0) {
                    throw 'Complete requires all agents, ownership, heavy work, and pending corrections to be released.'
                }
                if (@($prospective.lanes | Where-Object {
                    [string] $_.status -notin @('integrated', 'complete')
                }).Count -gt 0) {
                    throw 'Complete requires every lane to be integrated or complete.'
                }
                $prospective.status = 'complete'
                $prospective.next_root_action = ''
            }
            'Stop' {
                Assert-LifecycleTarget -CurrentStatus ([string] $current.status) -TargetStatus 'stopped'
                $prospective.status = 'stopped'
                $prospective.next_root_action = ''
            }
        }
        if ($Action -eq 'Update') {
            if (@($CheckpointKind).Count -eq 0) {
                throw 'Update requires at least one CheckpointKind.'
            }
            Assert-TransitionNotGated -State $current -Name $TransitionName
            foreach ($immutable in @(
                'schema_version', 'session_id', 'run_id', 'revision',
                'updated_at_utc', 'status', 'objective', 'stop_condition',
                'gates', 'correction_counters')) {
                if (-not (Test-JsonEqual -Left $current.$immutable -Right $prospective.$immutable)) {
                    throw "Update cannot mutate '$immutable'; use its typed checkpoint action."
                }
            }
            $allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            $allowedByKind = @{
                Freeze = @('durable_decisions', 'lanes', 'instruction_inputs', 'active_contracts', 'next_root_action', 'delegated_next_actions')
                MaterialRefreeze = @('wave', 'durable_decisions', 'lanes', 'instruction_inputs', 'active_contracts', 'next_root_action', 'delegated_next_actions')
                Blocker = @('lanes', 'next_root_action', 'delegated_next_actions')
                OwnershipReservation = @('lanes', 'ownership_reservations', 'next_root_action', 'delegated_next_actions')
                WorktreeReservation = @('lanes', 'resource_state', 'next_root_action', 'delegated_next_actions')
                HeavyReservation = @('lanes', 'resource_state', 'next_root_action', 'delegated_next_actions')
                ResourceVerdict = @('resource_state', 'next_root_action')
                Retention = @('retained_agents', 'ownership_reservations', 'next_root_action')
                ReviewedCandidate = @('git', 'next_root_action')
                Integration = @('git', 'lanes', 'next_root_action', 'delegated_next_actions')
                Publication = @('git', 'next_root_action')
            }
            foreach ($kind in @($CheckpointKind)) {
                foreach ($field in $allowedByKind[$kind]) { [void] $allowed.Add($field) }
            }
            [void] $allowed.Add('evidence_references')
            foreach ($changed in (Get-ChangedProperties -Before $current -After $prospective)) {
                if (-not $allowed.Contains($changed)) {
                    throw "Checkpoint kind(s) '$($CheckpointKind -join ', ')' cannot mutate '$changed'."
                }
            }
            foreach ($field in @('remote', 'push_url', 'integration_branch', 'allowed_branch_prefix', 'initial_sha')) {
                if (-not (Test-JsonEqual -Left $current.git.$field -Right $prospective.git.$field)) {
                    throw "Update cannot mutate immutable Git target '$field'."
                }
            }
            if (-not (Test-JsonEqual -Left $current.git -Right $prospective.git)) {
                $allowedGitFields = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
                if ($CheckpointKind -contains 'ReviewedCandidate') { [void] $allowedGitFields.Add('reviewed_unpublished_sha') }
                if ($CheckpointKind -contains 'Integration') {
                    [void] $allowedGitFields.Add('latest_integrated_sha')
                    [void] $allowedGitFields.Add('reviewed_unpublished_sha')
                }
                if ($CheckpointKind -contains 'Publication') {
                    [void] $allowedGitFields.Add('latest_pushed_sha')
                    [void] $allowedGitFields.Add('reviewed_unpublished_sha')
                }
                foreach ($changed in (Get-ChangedProperties -Before $current.git -After $prospective.git)) {
                    if (-not $allowedGitFields.Contains($changed)) {
                        throw "Checkpoint kind(s) '$($CheckpointKind -join ', ')' cannot mutate git.$changed."
                    }
                }
            }
            if (-not (Test-JsonEqual -Left $current.resource_state -Right $prospective.resource_state)) {
                foreach ($field in @(
                    'circuit_breaker_mode', 'circuit_breaker_trigger', 'degraded_profiles')) {
                    if (-not (Test-JsonEqual -Left $current.resource_state.$field -Right $prospective.resource_state.$field)) {
                        throw "Update cannot mutate resource_state.$field; use the typed circuit-breaker action."
                    }
                }
                $allowedResourceFields = [System.Collections.Generic.HashSet[string]]::new(
                    [System.StringComparer]::Ordinal)
                if ($CheckpointKind -contains 'WorktreeReservation') {
                    foreach ($field in @(
                        'worktree_admission', 'disk_warning_band', 'owner_override',
                        'worktree_reservations')) { [void] $allowedResourceFields.Add($field) }
                }
                if ($CheckpointKind -contains 'HeavyReservation') {
                    foreach ($field in @(
                        'heavy_admission', 'memory_warning_band',
                        'active_heavy_reservations')) { [void] $allowedResourceFields.Add($field) }
                }
                if ($CheckpointKind -contains 'ResourceVerdict') {
                    foreach ($field in @(
                        'worktree_admission', 'heavy_admission', 'disk_warning_band',
                        'memory_warning_band', 'owner_override')) {
                        [void] $allowedResourceFields.Add($field)
                    }
                }
                $changedResourceFields = Get-ChangedProperties `
                    -Before $current.resource_state -After $prospective.resource_state
                foreach ($changed in $changedResourceFields) {
                    if (-not $allowedResourceFields.Contains($changed)) {
                        throw "Checkpoint kind(s) '$($CheckpointKind -join ', ')' cannot mutate resource_state.$changed."
                    }
                }
            }
        }
        $comparison = Copy-JsonObject -Value $prospective
        $comparison.revision = $current.revision
        $comparison.updated_at_utc = $current.updated_at_utc
        if (Test-JsonEqual -Left $comparison -Right $current) {
            $result = [pscustomobject] [ordered] @{
                run_id = $RunId
                revision = [long] $current.revision
                action = $Action
                changed = $false
                no_op = $true
                correction_allowed = if ($Action -eq 'ReserveCorrection') {
                    $correctionAllowed
                }
                else { $null }
                memory_retry_allowed = if ($Action -eq 'ReserveMemoryRetry') {
                    $memoryRetryAllowed
                }
                else { $null }
            }
            if ($AsJson) { $result | ConvertTo-Json -Depth 5 } else { $result }
            return
        }
        $prospective.revision = [long] $current.revision + 1
        $prospective.updated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    }

    Assert-ControlState -State $prospective
    New-Item -ItemType Directory -Path $stageDirectory | Out-Null
    $previewPath = Join-Path $stageDirectory 'preview.md'
    Write-AtomicUtf8File -Path $previewPath -Content (Render-Preview -State $prospective)

    $manifestHelper = Join-Path $PSScriptRoot 'New-OrchestrationRecoveryManifest.ps1'
    $baseManifestArguments = @{
        RunId = $RunId
        StateRevision = [long] $prospective.revision
        RepositoryRoot = $RepositoryRoot
        OutputDirectory = $stageDirectory
        PreviewPath = $previewPath
    }
    $instructionArguments = $baseManifestArguments.Clone()
    $instructionArguments.Packet = 'instruction'
    $instructionArguments.Path = @('AGENTS.md', '.agents/skills/orchestrate/SKILL.md')
    $instruction = & $manifestHelper @instructionArguments
    $hookArguments = $baseManifestArguments.Clone()
    $hookArguments.Packet = 'hook'
    $hookArguments.Path = @('.codex/hooks.json')
    $hook = & $manifestHelper @hookArguments
    $activeArguments = $baseManifestArguments.Clone()
    $activeArguments.Packet = 'active-contract'
    $activeArguments.Path = @(".tmp/orchestration/$RunId/preview.md")
    $active = & $manifestHelper @activeArguments

    $stagedStatePath = Join-Path $stageDirectory 'control-state.json'
    $stateContent = ($prospective | ConvertTo-Json -Depth 30) + [Environment]::NewLine
    Write-AtomicUtf8File -Path $stagedStatePath -Content $stateContent
    $controlStateSha256 = (
        Get-FileHash -LiteralPath $stagedStatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $capsuleArguments = @{
        State = $prospective
        ControlStateSha256 = $controlStateSha256
        InstructionManifest = $instruction
        HookManifest = $hook
        ActiveContractManifest = $active
    }
    $capsule = New-Capsule @capsuleArguments
    $capsulePath = Join-Path $stageDirectory 'capsule.json'
    Write-AtomicUtf8File -Path $capsulePath -Content (
        ($capsule | ConvertTo-Json -Depth 30) + [Environment]::NewLine)

    $sealArguments = @{
        Mode = 'Seal'
        RunId = $RunId
        RepositoryRoot = $RepositoryRoot
        ControlStatePath = $stagedStatePath
        ProjectionDirectory = $stageDirectory
        StageOnly = $true
    }
    & (Join-Path $PSScriptRoot 'Invoke-OrchestrationCapsuleHook.ps1') @sealArguments | Out-Null
    foreach ($projectionName in @(
        'preview.md', 'instruction-manifest.json', 'hook-manifest.json',
        'active-contract-manifest.json', 'capsule.json', 'capsule.sha256')) {
        Move-Item -LiteralPath (Join-Path $stageDirectory $projectionName) -Destination (Join-Path $runDirectory $projectionName) -Force
    }
    $activePath = Join-Path $runDirectory 'active'
    if ([string] $prospective.status -notin $terminalStatuses) {
        Write-AtomicUtf8File -Path $activePath -Content (
            "kicktippai.orchestrate/v4 revision=$($prospective.revision)" +
            [Environment]::NewLine)
    }
    Move-Item -LiteralPath $stagedStatePath -Destination $statePath -Force
    if ([string] $prospective.status -in $terminalStatuses -and
        (Test-Path -LiteralPath $activePath -PathType Leaf)) {
        Remove-Item -LiteralPath $activePath -Force
    }

    $result = [pscustomobject] [ordered] @{
        run_id = $RunId
        revision = [long] $prospective.revision
        action = $Action
        changed = $true
        no_op = $false
        correction_allowed = if ($Action -eq 'ReserveCorrection') {
            $correctionAllowed
        }
        else { $null }
        memory_retry_allowed = if ($Action -eq 'ReserveMemoryRetry') {
            $memoryRetryAllowed
        }
        else { $null }
        status = [string] $prospective.status
    }
    if ($AsJson) { $result | ConvertTo-Json -Depth 5 } else { $result }
}
finally {
    if ($null -ne $lockStream) { $lockStream.Dispose() }
    if (Test-Path -LiteralPath $stageDirectory -PathType Container) {
        $stagePrefix = $runDirectory.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar + '.checkpoint-stage-'
        if (-not $stageDirectory.StartsWith($stagePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to clean a checkpoint staging directory outside the exact run.'
        }
        Remove-Item -LiteralPath $stageDirectory -Recurse -Force
    }
}
