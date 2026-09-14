[CmdletBinding()]
param(
    [ValidateSet('Hook', 'Seal', 'Validate')]
    [string] $Mode = 'Hook',
    [string] $RunId,
    [string] $RepositoryRoot,
    [string] $InputJson,
    [string] $ControlStatePath,
    [string] $ProjectionDirectory,
    [switch] $StageOnly
)

$ErrorActionPreference = 'Stop'
$maximumCapsuleBytes = 8192
$maximumPreviewBytes = 12288
$previewWarningBytes = 8192
$activationMarkerPrefix = 'kicktippai.orchestrate/v4'
$canonicalPushUrl = 'https://github.com/ehonda/KicktippAi.git'
$allowedStatuses = @('preview', 'awaiting-owner', 'ready', 'active', 'complete', 'stopped')
$allowedGateCategories = @(
    'owner', 'authority', 'production-continuity', 'dependency', 'validation',
    'resource', 'integration', 'publication', 'recovery')

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$orchestrationRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $RepositoryRoot '.tmp/orchestration'))

function Assert-SafeRunId {
    param([Parameter(Mandatory)][string] $Identifier)

    if (
        $Identifier.Length -gt 128 -or
        $Identifier -in @('.', '..') -or
        [System.IO.Path]::GetFileName($Identifier) -ne $Identifier -or
        $Identifier -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
        throw 'The orchestration run ID is not a safe path segment.'
    }
}

function Get-RunPaths {
    param(
        [Parameter(Mandatory)][string] $Identifier,
        [string] $CandidateDirectory
    )

    Assert-SafeRunId -Identifier $Identifier
    $runDirectory = [System.IO.Path]::GetFullPath(
        (Join-Path $orchestrationRoot $Identifier))
    $expectedPrefix = $orchestrationRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar

    if (-not $runDirectory.StartsWith(
        $expectedPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The orchestration run directory escaped the repository orchestration root.'
    }

    $materialDirectory = $runDirectory
    if (-not [string]::IsNullOrWhiteSpace($CandidateDirectory)) {
        $materialDirectory = [System.IO.Path]::GetFullPath($CandidateDirectory)
        $candidateParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $materialDirectory))
        $candidateName = Split-Path -Leaf $materialDirectory
        if (-not $candidateParent.Equals($runDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
            $candidateName -notmatch '^\.checkpoint-stage-[a-f0-9]{32}$') {
            throw 'A projection staging directory must be an exact checkpoint child of the run directory.'
        }
    }

    [pscustomobject] @{
        Directory = $materialDirectory
        CanonicalDirectory = $runDirectory
        Capsule = Join-Path $materialDirectory 'capsule.json'
        Checksum = Join-Path $materialDirectory 'capsule.sha256'
        Active = Join-Path $runDirectory 'active'
        Preview = Join-Path $materialDirectory 'preview.md'
        InstructionManifest = Join-Path $materialDirectory 'instruction-manifest.json'
        HookManifest = Join-Path $materialDirectory 'hook-manifest.json'
        ActiveContractManifest = Join-Path $materialDirectory 'active-contract-manifest.json'
        ControlState = Join-Path $materialDirectory 'control-state.json'
        PendingControlState = Join-Path $runDirectory 'control-state.pending.json'
        Lock = Join-Path $runDirectory '.checkpoint.lock'
    }
}

function New-ValidationResult {
    param(
        [Parameter(Mandatory)][bool] $Valid,
        [Parameter(Mandatory)][string] $Code,
        [Parameter(Mandatory)][string] $Message,
        $Capsule,
        $ControlState = $null
    )

    [pscustomobject] @{
        Valid = $Valid
        Code = $Code
        Message = $Message
        Capsule = $Capsule
        ControlState = $ControlState
    }
}

function Wait-CheckpointLock {
    param([Parameter(Mandatory)][string] $LockPath)

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $stream = $null
        try {
            $stream = [System.IO.File]::Open(
                $LockPath, [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            return $true
        }
        catch [System.IO.IOException] {
            Start-Sleep -Milliseconds 100
        }
        finally {
            if ($null -ne $stream) { $stream.Dispose() }
        }
    }
    return $false
}

function Test-ControlState {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $ExpectedRunId
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return New-ValidationResult $false 'missing-control-state' 'control-state.json is missing.' $null
    }
    try {
        $state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        return New-ValidationResult $false 'malformed-control-state' 'control-state.json is not valid JSON.' $null
    }
    $required = @(
        'schema_version', 'session_id', 'run_id', 'revision', 'status',
        'objective', 'wave', 'gates', 'resource_state')
    foreach ($field in $required) {
        if ($state.PSObject.Properties.Name -notcontains $field) {
            return New-ValidationResult $false 'invalid-control-state' "control-state.json is missing '$field'." $null
        }
    }
    if ([int] $state.schema_version -ne 1) {
        return New-ValidationResult $false 'unsupported-control-schema' 'control-state.json has an unsupported schema.' $null
    }
    if (
        [string] $state.session_id -cne $ExpectedRunId -or
        [string] $state.run_id -cne $ExpectedRunId) {
        return New-ValidationResult $false 'control-identity-mismatch' 'control-state.json does not match the exact hook session ID.' $null
    }
    if ([long] $state.revision -lt 1 -or [string] $state.status -notin $allowedStatuses) {
        return New-ValidationResult $false 'invalid-control-state' 'control-state.json has an invalid revision or status.' $null
    }
    if (-not ($state.gates -is [System.Array])) {
        return New-ValidationResult $false 'invalid-control-state' 'control-state gates must be an array.' $null
    }
    return New-ValidationResult $true 'valid' 'control-state.json is structurally valid.' $null $state
}

function Test-CapsuleStructure {
    param(
        [Parameter(Mandatory)][string] $CapsulePath,
        [Parameter(Mandatory)][string] $ExpectedRunId
    )

    if (-not (Test-Path -LiteralPath $CapsulePath -PathType Leaf)) {
        return New-ValidationResult $false 'missing-capsule' 'capsule.json is missing.' $null
    }

    $capsuleFile = Get-Item -LiteralPath $CapsulePath
    if ($capsuleFile.Length -gt $maximumCapsuleBytes) {
        return New-ValidationResult $false 'oversized-capsule' "capsule.json exceeds the $maximumCapsuleBytes-byte limit." $null
    }

    try {
        $capsule = Get-Content -LiteralPath $CapsulePath -Raw | ConvertFrom-Json
    }
    catch {
        return New-ValidationResult $false 'malformed-json' 'capsule.json is not valid JSON.' $null
    }

    $requiredProperties = @(
        'schema_version', 'state_revision', 'control_state_sha256', 'session_id', 'run_id',
        'updated_at_utc', 'status', 'objective', 'wave', 'stop_condition',
        'durable_decisions', 'evidence_references', 'gates', 'freeze', 'git',
        'ownership_reservations', 'correction_counters', 'resource_state',
        'retained_agents', 'next_root_action',
        'delegated_next_actions')
    $propertyNames = @($capsule.PSObject.Properties.Name)
    foreach ($property in $requiredProperties) {
        if ($propertyNames -notcontains $property) {
            return New-ValidationResult $false 'missing-field' "capsule.json is missing required field '$property'." $null
        }
    }

    if ([string] $capsule.schema_version -ne '4') {
        return New-ValidationResult $false 'unsupported-schema' 'capsule.json has an unsupported schema_version.' $null
    }
    if ([long] $capsule.state_revision -lt 1) {
        return New-ValidationResult $false 'invalid-state-revision' 'capsule.json has an invalid state_revision.' $null
    }
    if ([string] $capsule.control_state_sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        return New-ValidationResult $false 'invalid-control-state-digest' 'capsule.json has an invalid control-state digest.' $null
    }
    if (
        [string] $capsule.session_id -ne $ExpectedRunId -or
        [string] $capsule.run_id -ne $ExpectedRunId) {
        return New-ValidationResult $false 'identity-mismatch' 'capsule.json does not match the exact hook session ID.' $null
    }
    if ([string] $capsule.status -notin $allowedStatuses) {
        return New-ValidationResult $false 'invalid-status' 'capsule.json has an invalid lifecycle status.' $null
    }
    foreach ($field in @('updated_at_utc', 'objective', 'wave', 'stop_condition')) {
        if ([string]::IsNullOrWhiteSpace([string] $capsule.$field)) {
            return New-ValidationResult $false 'empty-field' "capsule.json field '$field' must not be empty." $null
        }
    }
    if (
        [string] $capsule.status -notin @('complete', 'stopped') -and
        [string]::IsNullOrWhiteSpace([string] $capsule.next_root_action)) {
        return New-ValidationResult $false 'empty-field' "capsule.json field 'next_root_action' must not be empty for an active run." $null
    }

    foreach ($field in @(
        'durable_decisions', 'evidence_references', 'gates', 'ownership_reservations',
        'correction_counters', 'retained_agents',
        'delegated_next_actions')) {
        if (-not ($capsule.$field -is [System.Array])) {
            return New-ValidationResult $false 'invalid-field-type' "capsule.json field '$field' must be an array." $null
        }
    }

    foreach ($objectField in @('freeze', 'git', 'resource_state')) {
        if ($null -eq $capsule.$objectField -or $capsule.$objectField -isnot [pscustomobject]) {
            return New-ValidationResult $false 'invalid-field-type' "capsule.json field '$objectField' must be an object." $null
        }
    }

    $requiredNestedProperties = @{
        freeze = @(
            'preview_path', 'instruction_manifest', 'hook_manifest',
            'active_contract_manifest')
        git = @(
            'remote', 'push_url', 'integration_branch', 'allowed_branch_prefix',
            'initial_sha', 'latest_integrated_sha', 'latest_pushed_sha',
            'reviewed_unpublished_sha')
        resource_state = @(
            'worktree_admission', 'heavy_admission', 'disk_warning_band',
            'memory_warning_band', 'owner_override', 'worktree_reservations',
            'active_heavy_reservations', 'circuit_breaker_mode',
            'circuit_breaker_trigger', 'degraded_profiles')
    }
    foreach ($objectField in $requiredNestedProperties.Keys) {
        $nestedNames = @($capsule.$objectField.PSObject.Properties.Name)
        foreach ($nestedProperty in $requiredNestedProperties[$objectField]) {
            if ($nestedNames -notcontains $nestedProperty) {
                return New-ValidationResult $false 'missing-field' "capsule.json field '$objectField' is missing '$nestedProperty'." $null
            }
        }
    }
    $expectedPreviewPath = ".tmp/orchestration/$ExpectedRunId/preview.md"
    if ([string] $capsule.freeze.preview_path -cne $expectedPreviewPath) {
        return New-ValidationResult $false 'invalid-preview-path' 'freeze.preview_path does not point to the exact run preview.' $null
    }
    $manifestNames = @{
        instruction_manifest = 'instruction-manifest.json'
        hook_manifest = 'hook-manifest.json'
        active_contract_manifest = 'active-contract-manifest.json'
    }
    foreach ($field in $manifestNames.Keys) {
        $reference = $capsule.freeze.$field
        if ($null -eq $reference -or $reference -isnot [pscustomobject]) {
            return New-ValidationResult $false 'invalid-manifest-reference' "freeze.$field must be an object." $null
        }
        $names = @($reference.PSObject.Properties.Name)
        if ($names -notcontains 'path' -or $names -notcontains 'sha256') {
            return New-ValidationResult $false 'invalid-manifest-reference' "freeze.$field requires path and sha256." $null
        }
        $expectedPath = ".tmp/orchestration/$ExpectedRunId/$($manifestNames[$field])"
        if ([string] $reference.path -cne $expectedPath -or [string] $reference.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
            return New-ValidationResult $false 'invalid-manifest-reference' "freeze.$field is not an exact run-scoped manifest reference." $null
        }
    }

    if (
        [string] $capsule.git.remote -cne 'origin' -or
        [string] $capsule.git.push_url -cne $canonicalPushUrl -or
        [string] $capsule.git.allowed_branch_prefix -notmatch '^codex/[A-Za-z0-9][A-Za-z0-9._-]*-$') {
        return New-ValidationResult $false 'invalid-git-target' 'git target fields do not match the canonical repository allowlist.' $null
    }
    if ([string] $capsule.git.initial_sha -notmatch '^[A-Fa-f0-9]{40}$') {
        return New-ValidationResult $false 'invalid-git-sha' 'git.initial_sha must be an exact 40-character commit SHA.' $null
    }
    foreach ($field in @('latest_integrated_sha', 'latest_pushed_sha', 'reviewed_unpublished_sha')) {
        $sha = [string] $capsule.git.$field
        if (-not [string]::IsNullOrWhiteSpace($sha) -and $sha -notmatch '^[A-Fa-f0-9]{40}$') {
            return New-ValidationResult $false 'invalid-git-sha' "git.$field must be empty or an exact 40-character commit SHA." $null
        }
    }

    foreach ($field in @('worktree_admission', 'heavy_admission')) {
        if ([string] $capsule.resource_state.$field -notin @('unknown', 'allowed', 'denied')) {
            return New-ValidationResult $false 'invalid-resource-state' "resource_state.$field has an invalid verdict." $null
        }
    }
    foreach ($field in @('disk_warning_band', 'memory_warning_band')) {
        if ([string] $capsule.resource_state.$field -notin @('unknown', 'normal', 'warning')) {
            return New-ValidationResult $false 'invalid-resource-state' "resource_state.$field has an invalid warning band." $null
        }
    }
    if (-not ($capsule.resource_state.worktree_reservations -is [System.Array])) {
        return New-ValidationResult $false 'invalid-resource-state' 'resource_state.worktree_reservations must be an array.' $null
    }
    if (
        [string] $capsule.git.integration_branch -cne 'main' -and
        -not ([string] $capsule.git.integration_branch).StartsWith(
            [string] $capsule.git.allowed_branch_prefix,
            [System.StringComparison]::Ordinal)) {
        return New-ValidationResult $false 'invalid-git-target' 'git.integration_branch is outside the run branch allowlist.' $null
    }
    if (-not ($capsule.resource_state.active_heavy_reservations -is [System.Array]) -or
        -not ($capsule.resource_state.degraded_profiles -is [System.Array]) -or
        [string] $capsule.resource_state.circuit_breaker_mode -notin @('normal', 'degraded')) {
        return New-ValidationResult $false 'invalid-resource-state' 'resource_state heavy reservations or circuit-breaker mode is invalid.' $null
    }
    if ([string] $capsule.resource_state.circuit_breaker_mode -eq 'normal') {
        if ($null -ne $capsule.resource_state.circuit_breaker_trigger -or
            @($capsule.resource_state.degraded_profiles).Count -ne 0) {
            return New-ValidationResult $false 'invalid-resource-state' 'Normal circuit-breaker mode retains degraded state.' $null
        }
    }
    else {
        $trigger = $capsule.resource_state.circuit_breaker_trigger
        $profile = @($capsule.resource_state.degraded_profiles)[0]
        if (
            $null -eq $trigger -or
            @($capsule.resource_state.degraded_profiles).Count -ne 1 -or
            [string]::IsNullOrWhiteSpace([string] $trigger.profile) -or
            [string]::IsNullOrWhiteSpace([string] $trigger.fingerprint) -or
            [string] $profile.profile -cne [string] $trigger.profile -or
            [string] $profile.fingerprint -cne [string] $trigger.fingerprint -or
            -not [bool] $profile.exclusive -or
            [int] $profile.maximum_worker_fanout -lt 1 -or
            [int] $profile.recoverable_retry_limit -lt 1 -or
            [int] $profile.recoverable_retries_used -lt 0 -or
            [int] $profile.recoverable_retries_used -gt
                [int] $profile.recoverable_retry_limit) {
            return New-ValidationResult $false 'invalid-resource-state' 'Degraded circuit-breaker state is invalid.' $null
        }
    }
    foreach ($worktree in @($capsule.resource_state.worktree_reservations)) {
        $worktreeProperties = @($worktree.PSObject.Properties.Name)
        foreach ($field in @(
            'path', 'class', 'growth_reservation_gib', 'owner', 'branch', 'tip',
            'remediation')) {
            if ($worktreeProperties -notcontains $field) {
                return New-ValidationResult $false 'invalid-resource-state' "worktree_reservations entries require '$field'." $null
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $worktree.path) -or
            [string] $worktree.class -notin @(
                'active-build-capable', 'parked-recovery-only',
                'removal-ready', 'uncertain') -or
            [double] $worktree.growth_reservation_gib -lt 0 -or
            [string]::IsNullOrWhiteSpace([string] $worktree.branch) -or
            [string] $worktree.tip -notmatch '^[A-Fa-f0-9]{40}$' -or
            ([string] $worktree.class -eq 'parked-recovery-only' -and
             [string]::IsNullOrWhiteSpace([string] $worktree.remediation))) {
            return New-ValidationResult $false 'invalid-resource-state' 'resource_state contains an invalid worktree reservation.' $null
        }
        $expectedReservation = if ([string] $worktree.class -in @('active-build-capable', 'uncertain')) { 1.25 } else { 0.0 }
        if ([double] $worktree.growth_reservation_gib -ne $expectedReservation) {
            return New-ValidationResult $false 'invalid-resource-state' "worktree class '$($worktree.class)' requires a $expectedReservation GiB growth reservation." $null
        }
    }
    if ($null -ne $capsule.resource_state.owner_override) {
        $overrideProperties = @($capsule.resource_state.owner_override.PSObject.Properties.Name)
        foreach ($field in @('scope', 'reason', 'reserved_capacity')) {
            if (
                $overrideProperties -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $capsule.resource_state.owner_override.$field)) {
                return New-ValidationResult $false 'invalid-resource-state' "resource_state.owner_override requires non-empty '$field'." $null
            }
        }
    }

    $parsedTimestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse(
        [string] $capsule.updated_at_utc,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal,
        [ref] $parsedTimestamp)) {
        return New-ValidationResult $false 'invalid-timestamp' 'capsule.json updated_at_utc is not a valid timestamp.' $null
    }

    foreach ($gate in @($capsule.gates)) {
        foreach ($field in @(
            'id', 'category', 'scope', 'evidence', 'prohibited_transitions',
            'remediation_owner', 'remediation_action', 'continue_actions',
            'escalation_condition', 'retry_budget', 'retry_attempts')) {
            if ($gate.PSObject.Properties.Name -notcontains $field) {
                return New-ValidationResult $false 'invalid-gate' "capsule gate requires '$field'." $null
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $gate.id) -or
            [string] $gate.category -notin $allowedGateCategories -or
            [string]::IsNullOrWhiteSpace([string] $gate.scope) -or
            [string]::IsNullOrWhiteSpace([string] $gate.evidence) -or
            [string]::IsNullOrWhiteSpace([string] $gate.remediation_owner) -or
            [string]::IsNullOrWhiteSpace([string] $gate.remediation_action) -or
            [string]::IsNullOrWhiteSpace([string] $gate.escalation_condition) -or
            -not ($gate.prohibited_transitions -is [System.Array]) -or
            -not ($gate.continue_actions -is [System.Array]) -or
            [int] $gate.retry_budget -lt 0 -or
            [int] $gate.retry_attempts -lt 0 -or
            [int] $gate.retry_attempts -gt [int] $gate.retry_budget) {
            return New-ValidationResult $false 'invalid-gate' 'capsule.json contains an invalid gate.' $null
        }
    }

    $counterKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($counter in @($capsule.correction_counters)) {
        foreach ($field in @(
            'milestone_id', 'role', 'used', 'issued_assignment_ids',
            'pending_assignment_id')) {
            if ($counter.PSObject.Properties.Name -notcontains $field) {
                return New-ValidationResult $false 'invalid-correction-counter' "capsule correction counter requires '$field'." $null
            }
        }
        $maximum = if ([string] $counter.role -eq 'milestone-writer') { 3 } else { 2 }
        if (
            [string]::IsNullOrWhiteSpace([string] $counter.milestone_id) -or
            [string] $counter.role -notin @('milestone-writer', 'implementation-reviewer') -or
            [int] $counter.used -lt 0 -or [int] $counter.used -gt $maximum -or
            -not $counterKeys.Add("$($counter.milestone_id)`0$($counter.role)") -or
            -not ($counter.issued_assignment_ids -is [System.Array]) -or
            @($counter.issued_assignment_ids).Count -ne [int] $counter.used -or
            @($counter.issued_assignment_ids | Sort-Object -Unique).Count -ne
                @($counter.issued_assignment_ids).Count -or
            ($null -ne $counter.pending_assignment_id -and
             ([string]::IsNullOrWhiteSpace([string] $counter.pending_assignment_id) -or
              @($counter.issued_assignment_ids) -cnotcontains [string] $counter.pending_assignment_id))) {
            return New-ValidationResult $false 'invalid-correction-counter' 'capsule.json contains an invalid correction counter.' $null
        }
    }

    foreach ($retained in @($capsule.retained_agents)) {
        $retainedProperties = @($retained.PSObject.Properties.Name)
        foreach ($field in @('agent_path', 'role', 'reason', 'release_trigger', 'context_cost_limit')) {
            if (
                $retainedProperties -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $retained.$field)) {
                return New-ValidationResult $false 'invalid-retention' "retained_agents entries require non-empty '$field'." $null
            }
        }
    }

    foreach ($reservation in @($capsule.ownership_reservations)) {
        $reservationProperties = @($reservation.PSObject.Properties.Name)
        foreach ($field in @(
            'assignment_id', 'lane_id', 'agent_path', 'role', 'model',
            'reasoning_effort', 'owned_paths', 'next_action')) {
            if (
                $reservationProperties -notcontains $field -or
                ($field -ne 'owned_paths' -and
                    [string]::IsNullOrWhiteSpace([string] $reservation.$field))) {
                return New-ValidationResult $false 'invalid-reservation' "ownership_reservations entries require non-empty '$field'." $null
            }
        }
        if (
            -not ($reservation.owned_paths -is [System.Array]) -or
            @($reservation.owned_paths).Count -eq 0 -or
            @($reservation.owned_paths | Where-Object { [string]::IsNullOrWhiteSpace([string] $_) }).Count -gt 0) {
            return New-ValidationResult $false 'invalid-reservation' "ownership_reservations field 'owned_paths' must be a non-empty string array." $null
        }
    }

    foreach ($reservation in @($capsule.resource_state.active_heavy_reservations)) {
        foreach ($field in @(
            'id', 'owner', 'profile', 'fingerprint', 'memory_reservation_gib',
            'recoverable', 'effect_class', 'worker_cap', 'fanout_controllable',
            'exclusive')) {
            if ($reservation.PSObject.Properties.Name -notcontains $field) {
                return New-ValidationResult $false 'invalid-heavy-reservation' "active heavy reservation requires '$field'." $null
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $reservation.id) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.owner) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.profile) -or
            [string]::IsNullOrWhiteSpace([string] $reservation.fingerprint) -or
            [double] $reservation.memory_reservation_gib -le 0 -or
            [string] $reservation.effect_class -notin @('local-only', 'external-or-live') -or
            [int] $reservation.worker_cap -lt 1 -or
            (-not [bool] $reservation.fanout_controllable -and
             -not [bool] $reservation.exclusive)) {
            return New-ValidationResult $false 'invalid-heavy-reservation' 'capsule.json contains an invalid active heavy reservation.' $null
        }
    }

    return New-ValidationResult $true 'valid' 'capsule.json is structurally valid.' $capsule
}

function Test-Capsule {
    param(
        [Parameter(Mandatory)] $Paths,
        [Parameter(Mandatory)][string] $ExpectedRunId,
        [switch] $SkipChecksum
    )

    $structure = Test-CapsuleStructure -CapsulePath $Paths.Capsule -ExpectedRunId $ExpectedRunId
    if (-not $structure.Valid -or $SkipChecksum) {
        return $structure
    }

    if (-not (Test-Path -LiteralPath $Paths.Checksum -PathType Leaf)) {
        return New-ValidationResult $false 'missing-checksum' 'capsule.sha256 is missing.' $null
    }
    $expectedHash = (Get-Content -LiteralPath $Paths.Checksum -Raw).Trim().ToLowerInvariant()
    if ($expectedHash -notmatch '^[a-f0-9]{64}$') {
        return New-ValidationResult $false 'malformed-checksum' 'capsule.sha256 is malformed.' $null
    }
    $actualHash = (Get-FileHash -LiteralPath $Paths.Capsule -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        return New-ValidationResult $false 'checksum-mismatch' 'capsule.json does not match capsule.sha256.' $null
    }

    return $structure
}

function Resolve-ManifestEntryPath {
    param([Parameter(Mandatory)][string] $ManifestPath)

    if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
        return [System.IO.Path]::GetFullPath($ManifestPath)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $ManifestPath))
}

function ConvertTo-ManifestIdentifier {
    param([Parameter(Mandatory)][string] $AbsolutePath)

    $fullPath = [System.IO.Path]::GetFullPath($AbsolutePath)
    $repositoryPrefix = $RepositoryRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if ($fullPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return [System.IO.Path]::GetRelativePath($RepositoryRoot, $fullPath).Replace('\', '/')
    }
    return $fullPath.Replace('\', '/')
}

function Read-PreviewPacketPaths {
    param(
        [Parameter(Mandatory)][string] $PreviewPath,
        [Parameter(Mandatory)][string] $Kind
    )

    $start = "<!-- orchestration-${Kind}:start -->"
    $end = "<!-- orchestration-${Kind}:end -->"
    $inside = $false
    $found = $false
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($PreviewPath)) {
        $trimmed = $line.Trim()
        if ($trimmed -ceq $start) {
            if ($found -or $inside) { throw "duplicate $Kind packet markers" }
            $inside = $true
            $found = $true
            continue
        }
        if ($trimmed -ceq $end) {
            if (-not $inside) { throw "unmatched $Kind packet end marker" }
            $inside = $false
            continue
        }
        if ($inside -and -not [string]::IsNullOrWhiteSpace($trimmed)) {
            $result.Add($trimmed)
        }
    }
    if (-not $found -or $inside) { throw "missing or incomplete $Kind packet marker block" }
    return @($result)
}

function Test-RecoveryPackets {
    param(
        [Parameter(Mandatory)] $Paths,
        [Parameter(Mandatory)] $Capsule
    )

    if (-not (Test-Path -LiteralPath $Paths.Preview -PathType Leaf)) {
        return New-ValidationResult $false 'missing-preview' 'preview.md is missing.' $Capsule
    }
    if ((Get-Item -LiteralPath $Paths.Preview).Length -gt $maximumPreviewBytes) {
        return New-ValidationResult $false 'oversized-preview' "preview.md exceeds the $maximumPreviewBytes-byte hard ceiling." $Capsule
    }

    $packets = @(
        [pscustomobject] @{ Name = 'instruction'; Field = 'instruction_manifest'; Path = $Paths.InstructionManifest },
        [pscustomobject] @{ Name = 'hook'; Field = 'hook_manifest'; Path = $Paths.HookManifest },
        [pscustomobject] @{ Name = 'active-contract'; Field = 'active_contract_manifest'; Path = $Paths.ActiveContractManifest }
    )
    $packetEntries = @{}

    foreach ($packet in $packets) {
        $reference = $Capsule.freeze.($packet.Field)
        if (-not (Test-Path -LiteralPath $packet.Path -PathType Leaf)) {
            return New-ValidationResult $false 'missing-manifest' "$($packet.Name) recovery manifest is missing." $Capsule
        }
        $manifestDigest = (Get-FileHash -LiteralPath $packet.Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($manifestDigest -ne ([string] $reference.sha256).ToLowerInvariant()) {
            return New-ValidationResult $false 'manifest-digest-mismatch' "$($packet.Name) recovery manifest does not match its capsule digest." $Capsule
        }
        try {
            $manifest = Get-Content -LiteralPath $packet.Path -Raw | ConvertFrom-Json
        }
        catch {
            return New-ValidationResult $false 'malformed-manifest' "$($packet.Name) recovery manifest is not valid JSON." $Capsule
        }
        if (
            [int] $manifest.schema_version -ne 2 -or
            [long] $manifest.state_revision -ne [long] $Capsule.state_revision -or
            [string] $manifest.packet -cne $packet.Name) {
            return New-ValidationResult $false 'invalid-manifest' "$($packet.Name) recovery manifest has the wrong schema, revision, or packet identity." $Capsule
        }

        $entries = @($manifest.entries)
        if ($entries.Count -eq 0) {
            return New-ValidationResult $false 'empty-manifest' "$($packet.Name) recovery manifest has no entries." $Capsule
        }
        $entryPaths = @($entries | ForEach-Object { [string] $_.path })
        $sortedPaths = @($entryPaths | Sort-Object -CaseSensitive)
        if (($entryPaths -join "`n") -cne ($sortedPaths -join "`n") -or
            @($entryPaths | Select-Object -Unique).Count -ne $entryPaths.Count) {
            return New-ValidationResult $false 'noncanonical-manifest' "$($packet.Name) recovery manifest paths are not uniquely ordered." $Capsule
        }
        $packetEntries[$packet.Name] = $entryPaths

        foreach ($entry in $entries) {
            if (
                [string]::IsNullOrWhiteSpace([string] $entry.id) -or
                [string] $entry.id -cne [string] $entry.path -or
                [string] $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
                return New-ValidationResult $false 'invalid-manifest-entry' "$($packet.Name) recovery manifest contains an invalid entry." $Capsule
            }
            $entryPath = if ([string] $entry.path -ceq [string] $Capsule.freeze.preview_path) {
                $Paths.Preview
            }
            else { Resolve-ManifestEntryPath -ManifestPath ([string] $entry.path) }
            if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf)) {
                return New-ValidationResult $false 'missing-packet-material' "$($packet.Name) packet material is missing: $($entry.id)" $Capsule
            }
            $entryDigest = (Get-FileHash -LiteralPath $entryPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($entryDigest -ne ([string] $entry.sha256).ToLowerInvariant()) {
                return New-ValidationResult $false 'packet-digest-mismatch' "$($packet.Name) packet material changed: $($entry.id)" $Capsule
            }
        }

        $requiredEntries = switch ($packet.Name) {
            'instruction' { @('AGENTS.md', '.agents/skills/orchestrate/SKILL.md') }
            'hook' { @(
                '.codex/hooks.json',
                '.agents/skills/orchestrate/scripts/Invoke-OrchestrationCapsuleHook.ps1',
                '.agents/skills/orchestrate/scripts/Get-OrchestrationRecoverySnapshot.ps1',
                '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1',
                '.agents/skills/orchestrate/scripts/New-OrchestrationRecoveryManifest.ps1',
                '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1',
                '.agents/skills/orchestrate/scripts/Set-OrchestrationMemoryCircuitBreaker.ps1',
                '.agents/skills/orchestrate/scripts/Remove-OrchestrationWorktree.ps1',
                'New-AgentWorktree.ps1',
                '.agents/skills/orchestrate/resources/control-state-template.json',
                '.agents/skills/orchestrate/resources/capsule-template.json',
                '.agents/skills/orchestrate/resources/preview-template.md',
                '.agents/skills/orchestrate/resources/resource-policy.json') }
            'active-contract' { @([string] $Capsule.freeze.preview_path) }
        }
        foreach ($requiredEntry in $requiredEntries) {
            if ($entryPaths -cnotcontains $requiredEntry) {
                return New-ValidationResult $false 'incomplete-manifest' "$($packet.Name) recovery manifest omits required material: $requiredEntry" $Capsule
            }
        }
    }

    try {
        $declaredInstructionInputs = @(Read-PreviewPacketPaths -PreviewPath $Paths.Preview -Kind 'instruction-inputs')
        $declaredActiveContracts = @(Read-PreviewPacketPaths -PreviewPath $Paths.Preview -Kind 'active-contract')
    }
    catch {
        return New-ValidationResult $false 'invalid-preview-packet-index' "preview.md packet index is invalid: $($_.Exception.Message)." $Capsule
    }
    $revisionMarker = "- State revision: $($Capsule.state_revision)"
    if (@([System.IO.File]::ReadAllLines($Paths.Preview) | Where-Object {
        $_.Trim() -ceq $revisionMarker
    }).Count -ne 1) {
        return New-ValidationResult $false 'preview-revision-mismatch' 'preview.md does not carry the committed state revision.' $Capsule
    }

    foreach ($declaredPath in $declaredInstructionInputs) {
        if ($packetEntries.instruction -cnotcontains $declaredPath) {
            return New-ValidationResult $false 'incomplete-manifest' "instruction recovery manifest omits preview-declared material: $declaredPath" $Capsule
        }
    }

    $expectedActiveContracts = @(
        @([string] $Capsule.freeze.preview_path) + $declaredActiveContracts |
            Sort-Object -CaseSensitive -Unique)
    $actualActiveContracts = @($packetEntries.'active-contract' | Sort-Object -CaseSensitive)
    if (($expectedActiveContracts -join "`n") -cne ($actualActiveContracts -join "`n")) {
        return New-ValidationResult $false 'active-contract-index-mismatch' 'active-contract manifest does not exactly match the paths declared by preview.md.' $Capsule
    }

    foreach ($instructionPath in $packetEntries.instruction) {
        $instructionAbsolutePath = Resolve-ManifestEntryPath -ManifestPath $instructionPath
        foreach ($line in [System.IO.File]::ReadAllLines($instructionAbsolutePath)) {
            if ($line -match '^\s*@(?<include>[^\s]+)\s*$') {
                $includedPath = [string] $Matches.include
                $includedAbsolutePath = if ([System.IO.Path]::IsPathRooted($includedPath)) {
                    [System.IO.Path]::GetFullPath($includedPath)
                }
                else {
                    [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $instructionAbsolutePath) $includedPath))
                }
                $includedIdentifier = ConvertTo-ManifestIdentifier -AbsolutePath $includedAbsolutePath
                if ($packetEntries.instruction -cnotcontains $includedIdentifier) {
                    return New-ValidationResult $false 'incomplete-manifest' "instruction recovery manifest omits transitive include: $includedIdentifier" $Capsule
                }
            }
        }
    }

    foreach ($contractPath in $declaredActiveContracts) {
        $contractAbsolutePath = Resolve-ManifestEntryPath -ManifestPath $contractPath
        $repositoryPrefix = $RepositoryRoot.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) +
            [System.IO.Path]::DirectorySeparatorChar
        if (-not $contractAbsolutePath.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        $directory = Split-Path -Parent $contractAbsolutePath
        while ($directory.StartsWith($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $agentsPath = Join-Path $directory 'AGENTS.md'
            if (Test-Path -LiteralPath $agentsPath -PathType Leaf) {
                $agentsIdentifier = ConvertTo-ManifestIdentifier -AbsolutePath $agentsPath
                if ($packetEntries.instruction -cnotcontains $agentsIdentifier) {
                    return New-ValidationResult $false 'incomplete-manifest' "instruction recovery manifest omits applicable nested instructions: $agentsIdentifier" $Capsule
                }
            }
            if ($directory.Equals($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) { break }
            $parent = Split-Path -Parent $directory
            if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $directory) { break }
            $directory = $parent
        }
    }

    $message = if ((Get-Item -LiteralPath $Paths.Preview).Length -gt $previewWarningBytes) {
        "Recovery packets are valid; preview.md exceeds the $previewWarningBytes-byte target."
    }
    else {
        'Recovery packets are valid.'
    }
    return New-ValidationResult $true 'hot' $message $Capsule
}

function Test-RecoveryState {
    param(
        [Parameter(Mandatory)] $Paths,
        [Parameter(Mandatory)][string] $ExpectedRunId,
        [string] $StatePath,
        [switch] $SkipChecksum
    )

    if ([string]::IsNullOrWhiteSpace($StatePath)) { $StatePath = $Paths.ControlState }
    $controlValidation = Test-ControlState -Path $StatePath -ExpectedRunId $ExpectedRunId
    if (-not $controlValidation.Valid) { return $controlValidation }
    $capsuleValidation = Test-Capsule -Paths $Paths -ExpectedRunId $ExpectedRunId -SkipChecksum:$SkipChecksum
    if (-not $capsuleValidation.Valid) { return $capsuleValidation }
    $actualControlStateHash = (
        Get-FileHash -LiteralPath $StatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualControlStateHash -cne
        ([string] $capsuleValidation.Capsule.control_state_sha256).ToLowerInvariant()) {
        return New-ValidationResult $false 'control-state-digest-mismatch' 'Control state does not match the capsule commit record.' $capsuleValidation.Capsule $controlValidation.ControlState
    }
    if (
        [long] $capsuleValidation.Capsule.state_revision -ne
            [long] $controlValidation.ControlState.revision -or
        [string] $capsuleValidation.Capsule.status -cne
            [string] $controlValidation.ControlState.status) {
        return New-ValidationResult $false 'state-revision-mismatch' 'Control state and capsule do not identify the same committed revision.' $capsuleValidation.Capsule $controlValidation.ControlState
    }
    $packetValidation = Test-RecoveryPackets -Paths $Paths -Capsule $capsuleValidation.Capsule
    return New-ValidationResult $packetValidation.Valid $packetValidation.Code $packetValidation.Message $packetValidation.Capsule $controlValidation.ControlState
}

function Test-RunOwnerGate {
    param([Parameter(Mandatory)] $Validation)
    return (
        $Validation.Valid -and
        ([string] $Validation.Capsule.status -eq 'awaiting-owner' -or
         @($Validation.Capsule.gates | Where-Object {
            [string] $_.scope -eq 'run' -and
            [string] $_.category -in @('owner', 'authority')
         }).Count -gt 0))
}

function Write-AtomicUtf8File {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Content
    )

    $temporaryPath = "$Path.$([System.Guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            $Content,
            [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

if ($Mode -eq 'Seal') {
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        throw 'RunId is required in Seal mode.'
    }

    $paths = Get-RunPaths -Identifier $RunId -CandidateDirectory $ProjectionDirectory
    $statePath = if ([string]::IsNullOrWhiteSpace($ControlStatePath)) {
        $paths.ControlState
    }
    else { [System.IO.Path]::GetFullPath($ControlStatePath) }
    $statePathAllowed = $statePath.Equals($paths.ControlState, [System.StringComparison]::OrdinalIgnoreCase)
    if ([string]::IsNullOrWhiteSpace($ProjectionDirectory) -and
        $statePath.Equals($paths.PendingControlState, [System.StringComparison]::OrdinalIgnoreCase)) {
        $statePathAllowed = $true
    }
    if (-not $statePathAllowed) {
        throw 'ControlStatePath must match the selected projection state.'
    }
    if ($StageOnly -and [string]::IsNullOrWhiteSpace($ProjectionDirectory)) {
        throw 'StageOnly requires an exact run-local ProjectionDirectory.'
    }
    if (-not $StageOnly -and -not [string]::IsNullOrWhiteSpace($ProjectionDirectory)) {
        throw 'A staged ProjectionDirectory may only be sealed with StageOnly.'
    }
    $validation = Test-RecoveryState -Paths $paths -ExpectedRunId $RunId -StatePath $statePath -SkipChecksum
    if (-not $validation.Valid) {
        throw "Cannot seal orchestration capsule [$($validation.Code)]: $($validation.Message)"
    }

    $hash = (Get-FileHash -LiteralPath $paths.Capsule -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-AtomicUtf8File -Path $paths.Checksum -Content ($hash + [Environment]::NewLine)

    if (-not $StageOnly) {
        if ([string] $validation.Capsule.status -in @('complete', 'stopped')) {
            if (Test-Path -LiteralPath $paths.Active -PathType Leaf) {
                Remove-Item -LiteralPath $paths.Active -Force
            }
        }
        else {
            Write-AtomicUtf8File -Path $paths.Active -Content (
                "$activationMarkerPrefix revision=$($validation.Capsule.state_revision)" +
                [Environment]::NewLine)
        }
    }

    Write-Output "Sealed orchestration capsule for exact run $RunId."
    exit 0
}

if ($Mode -eq 'Validate') {
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        throw 'RunId is required in Validate mode.'
    }
    $paths = Get-RunPaths -Identifier $RunId
    if (-not (Wait-CheckpointLock -LockPath $paths.Lock)) {
        $validation = New-ValidationResult $false 'checkpoint-in-progress' 'The checkpoint lock did not clear.' $null
    }
    else {
        $validation = Test-RecoveryState -Paths $paths -ExpectedRunId $RunId
    }
    if (Test-RunOwnerGate -Validation $validation) {
        $validation = New-ValidationResult $false 'owner-gate' 'The capsule records an unresolved owner or authority gate.' $validation.Capsule
    }
    [pscustomobject] [ordered] @{
        valid = $validation.Valid
        recovery_mode = if ($validation.Valid) { 'hot' } else { 'cold' }
        code = $validation.Code
        message = $validation.Message
        capsule = $validation.Capsule
        control_state = $validation.ControlState
    } | ConvertTo-Json -Depth 14 -Compress
    exit 0
}

if ([string]::IsNullOrWhiteSpace($InputJson)) {
    $InputJson = [Console]::In.ReadToEnd()
}
if ([string]::IsNullOrWhiteSpace($InputJson)) {
    throw 'The hook input JSON was empty.'
}

try {
    $hookInput = $InputJson | ConvertFrom-Json
}
catch {
    throw 'The hook input was not valid JSON.'
}

$sessionId = [string] $hookInput.session_id
if ([string]::IsNullOrWhiteSpace($sessionId)) {
    throw 'The hook input did not contain session_id.'
}
$eventName = [string] $hookInput.hook_event_name

if ($eventName -notin @('PreCompact', 'SessionStart')) {
    exit 0
}

$paths = Get-RunPaths -Identifier $sessionId

# This is the orchestration-only gate. Ordinary sessions have no exact-session
# activation marker and therefore receive no hook output or capsule I/O.
if (-not (Test-Path -LiteralPath $paths.Active -PathType Leaf)) {
    exit 0
}

$marker = (Get-Content -LiteralPath $paths.Active -Raw).Trim()
$markerMatch = [regex]::Match(
    $marker,
    '^kicktippai\.orchestrate/v4 revision=(?<revision>[1-9][0-9]*)$')
if (-not $markerMatch.Success) {
    $validation = New-ValidationResult $false 'invalid-activation-marker' 'The exact-session orchestration activation marker is corrupt.' $null
}
else {
    if (-not (Wait-CheckpointLock -LockPath $paths.Lock)) {
        $validation = New-ValidationResult $false 'checkpoint-in-progress' 'The checkpoint lock did not clear.' $null
    }
    else {
        $validation = Test-RecoveryState -Paths $paths -ExpectedRunId $sessionId
        if ($validation.Valid -and
            [long] $markerMatch.Groups['revision'].Value -ne
                [long] $validation.Capsule.state_revision) {
            $validation = New-ValidationResult $false 'activation-revision-mismatch' 'The activation marker does not identify the committed state revision.' $validation.Capsule $validation.ControlState
        }
    }
}

if ($eventName -eq 'PreCompact') {
    if (-not $validation.Valid) {
        [pscustomobject] @{
            continue = $true
            systemMessage = "Active orchestration recovery capsule validation failed [$($validation.Code)]: $($validation.Message) Compaction will continue and exact-session recovery will require reconstruction."
        } | ConvertTo-Json -Compress
    }
    exit 0
}

if ($eventName -ne 'SessionStart') {
    exit 0
}

if ($validation.Valid -and [string] $validation.Capsule.status -in @('complete', 'stopped')) {
    exit 0
}

if (Test-RunOwnerGate -Validation $validation) {
    $validation = New-ValidationResult $false 'owner-gate' 'The capsule records an unresolved owner or authority gate.' $validation.Capsule
}

if (-not $validation.Valid) {
    $context = @"
COLD `$orchestrate RECOVERY REQUIRED for exact session '$sessionId': validation failed [$($validation.Code)]: $($validation.Message)
The explicit orchestration workflow may still be active. Before substantive work, use only '$($paths.Directory)' and complete the cold recovery procedure in the normative orchestrate-skill instruction graph; never select another run by recency. Reconstruct current state from authoritative instructions/contracts, live agents, Git/worktrees, resources, reservations, and required CI. Recreate control-state.json through the checkpoint helper and report this cold-recovery cause as purpose-specific evidence. Legacy state is unsupported.
"@
}
else {
    $compactCapsule = $validation.Capsule | ConvertTo-Json -Depth 12 -Compress
    $context = @"
HOT `$orchestrate RECOVERY for exact session '$sessionId'. Committed state revision $($validation.Capsule.state_revision), capsule checksum/schema, recovery packet digests, and preview ceiling are validated. Do not reread unchanged policies, phase/task history, ADR chains, or unrelated evidence. Run .agents/skills/orchestrate/scripts/Get-OrchestrationRecoverySnapshot.ps1 once for compact local Git/worktree/resource/reservation reconciliation, inspect live agents once, and query remote CI only if the immediate next action depends on it. If ownership does not reconcile, switch to the cold path. Read an unchanged active contract only when the validated preview is insufficient for the immediate control-plane decision. Use only the exact run directory; never select another run by recency.
VALIDATED ORCHESTRATION CAPSULE:
$compactCapsule
"@
}

[pscustomobject] @{
    hookSpecificOutput = [pscustomobject] @{
        hookEventName = 'SessionStart'
        additionalContext = $context.Trim()
    }
} | ConvertTo-Json -Depth 5 -Compress
