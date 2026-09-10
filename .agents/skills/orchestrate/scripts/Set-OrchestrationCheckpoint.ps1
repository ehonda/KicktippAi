[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet(
        'Initialize', 'Replace', 'Transition', 'SetGate', 'ClearGate',
        'ReserveCorrection', 'MarkCorrectionStarted',
        'ReleaseUnstartedCorrection', 'Complete', 'Stop')]
    [string] $Action,
    [Parameter(Mandatory)][string] $RunId,
    [Parameter(Mandatory)][ValidateRange(0, [long]::MaxValue)]
    [long] $ExpectedRevision,
    [string] $RepositoryRoot,
    [string] $Objective,
    [string] $StopCondition,
    [string] $InitialSha,
    [string] $AllowedBranchPrefix,
    [string] $NextRootAction,
    [string] $Status,
    [string] $Wave,
    [string] $StateJson,
    [string] $GateJson,
    [string] $GateId,
    [string] $MilestoneId,
    [ValidateSet('milestone-writer', 'implementation-reviewer')]
    [string] $CorrectionRole,
    [string] $AssignmentId,
    [string[]] $InstructionInput = @(),
    [string[]] $ActiveContract = @(),
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'
$canonicalPushUrl = 'https://github.com/ehonda/KicktippAi.git'
$allowedStatuses = @('preview', 'awaiting-owner', 'ready', 'active', 'complete', 'stopped')
$terminalStatuses = @('complete', 'stopped')
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
        'active_contracts', 'ownership_reservations', 'correction_counters',
        'retained_agents', 'delegated_next_actions')) {
        if (-not ($State.$field -is [System.Array])) {
            throw "Control-state field '$field' must be an array."
        }
    }
    foreach ($field in @(
        'durable_decisions', 'instruction_inputs', 'active_contracts',
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
        [string] $State.git.integration_branch -cne 'main' -or
        [string] $State.git.allowed_branch_prefix -notmatch '^codex/[A-Za-z0-9][A-Za-z0-9._-]*-$' -or
        [string] $State.git.initial_sha -notmatch '^[A-Fa-f0-9]{40}$') {
        throw 'Control-state Git target does not match the canonical allowlist.'
    }
    $gateIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($gate in @($State.gates)) {
        foreach ($field in @(
            'id', 'category', 'scope', 'evidence', 'prohibited_transitions',
            'remediation', 'continue_actions', 'escalation_condition', 'retry_budget')) {
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
            [string]::IsNullOrWhiteSpace([string] $gate.remediation) -or
            [string]::IsNullOrWhiteSpace([string] $gate.escalation_condition) -or
            [int] $gate.retry_budget -lt 0) {
            throw 'Gate entry is invalid.'
        }
        Assert-StringArray -Value $gate.prohibited_transitions -Label 'gate.prohibited_transitions'
        Assert-StringArray -Value $gate.continue_actions -Label 'gate.continue_actions'
    }
    $counterKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($counter in @($State.correction_counters)) {
        foreach ($field in @('milestone_id', 'role', 'used', 'pending_assignment_id')) {
            if ($counter.PSObject.Properties.Name -notcontains $field) {
                throw "Correction counter requires '$field'."
            }
        }
        if (
            [string]::IsNullOrWhiteSpace([string] $counter.milestone_id) -or
            [string] $counter.role -notin @('milestone-writer', 'implementation-reviewer') -or
            [int] $counter.used -lt 0 -or
            -not $counterKeys.Add("$($counter.milestone_id)`0$($counter.role)") -or
            ($null -ne $counter.pending_assignment_id -and
             [string]::IsNullOrWhiteSpace([string] $counter.pending_assignment_id))) {
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
    if ([string] $State.resource_state.circuit_breaker_mode -notin @('normal', 'degraded')) {
        throw 'Invalid circuit-breaker mode.'
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
        $owner = if ($lane.PSObject.Properties.Name -contains 'owner') { [string] $lane.owner } else { 'unassigned' }
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
        [Parameter(Mandatory)] $InstructionManifest,
        [Parameter(Mandatory)] $HookManifest,
        [Parameter(Mandatory)] $ActiveContractManifest
    )
    return [ordered] @{
        schema_version = 3
        state_revision = [long] $State.revision
        session_id = $RunId
        run_id = $RunId
        updated_at_utc = [string] $State.updated_at_utc
        status = [string] $State.status
        objective = [string] $State.objective
        wave = [string] $State.wave
        stop_condition = [string] $State.stop_condition
        durable_decisions = @($State.durable_decisions)
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
            git = [pscustomobject] [ordered] @{
                remote = 'origin'
                push_url = $canonicalPushUrl
                integration_branch = 'main'
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
        $prospective = if ($Action -eq 'Replace') {
            if ([string]::IsNullOrWhiteSpace($StateJson)) { throw 'Replace requires StateJson.' }
            Read-JsonValue -Value $StateJson
        }
        else { Copy-JsonObject -Value $current }

        switch ($Action) {
            'Transition' {
                if ([string]::IsNullOrWhiteSpace($Status) -or $Status -notin $allowedStatuses) {
                    throw 'Transition requires a valid Status.'
                }
                $prospective.status = $Status
                if (-not [string]::IsNullOrWhiteSpace($Wave)) { $prospective.wave = $Wave }
                if (-not [string]::IsNullOrWhiteSpace($NextRootAction)) {
                    $prospective.next_root_action = $NextRootAction
                }
            }
            'SetGate' {
                if ([string]::IsNullOrWhiteSpace($GateJson)) { throw 'SetGate requires GateJson.' }
                $gate = Read-JsonValue -Value $GateJson
                $prospective.gates = @(
                    @($prospective.gates | Where-Object { $_.id -cne $gate.id }) + $gate)
            }
            'ClearGate' {
                if ([string]::IsNullOrWhiteSpace($GateId)) { throw 'ClearGate requires GateId.' }
                $prospective.gates = @($prospective.gates | Where-Object { $_.id -cne $GateId })
            }
            'ReserveCorrection' {
                foreach ($value in @($MilestoneId, $CorrectionRole, $AssignmentId)) {
                    if ([string]::IsNullOrWhiteSpace($value)) {
                        throw 'ReserveCorrection requires MilestoneId, CorrectionRole, and AssignmentId.'
                    }
                }
                $counter = @($prospective.correction_counters | Where-Object {
                    $_.milestone_id -ceq $MilestoneId -and $_.role -ceq $CorrectionRole
                }) | Select-Object -First 1
                if ($null -eq $counter) {
                    $counter = [pscustomobject] [ordered] @{
                        milestone_id = $MilestoneId
                        role = $CorrectionRole
                        used = 0
                        pending_assignment_id = $null
                    }
                    $prospective.correction_counters = @($prospective.correction_counters) + $counter
                }
                if (-not [string]::IsNullOrWhiteSpace([string] $counter.pending_assignment_id)) {
                    throw 'A correction dispatch is already pending for this milestone and role.'
                }
                $maximum = if ($CorrectionRole -eq 'milestone-writer') { 3 } else { 2 }
                if ([int] $counter.used -ge $maximum) {
                    $lane = @($prospective.lanes | Where-Object {
                        $_.id -ceq $MilestoneId
                    }) | Select-Object -First 1
                    if ($null -ne $lane) {
                        Set-Property -Object $lane -Name status -Value 'needs-diagnosis'
                        Set-Property -Object $lane -Name next_action -Value (
                            'Release the current specialist and select a fresh diagnosis route.')
                    }
                    $correctionAllowed = $false
                }
                else {
                    $counter.used = [int] $counter.used + 1
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
                $counter.pending_assignment_id = $null
            }
            'Complete' {
                $prospective.status = 'complete'
                $prospective.next_root_action = ''
            }
            'Stop' {
                $prospective.status = 'stopped'
                $prospective.next_root_action = ''
            }
        }
        if ($Action -eq 'Replace') {
            $prospective.session_id = $RunId
            $prospective.run_id = $RunId
        }
        $comparison = Copy-JsonObject -Value $prospective
        $comparison.revision = $current.revision
        $comparison.updated_at_utc = $current.updated_at_utc
        if (($comparison | ConvertTo-Json -Depth 30 -Compress) -ceq
            ($current | ConvertTo-Json -Depth 30 -Compress)) {
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

    $capsuleArguments = @{
        State = $prospective
        InstructionManifest = $instruction
        HookManifest = $hook
        ActiveContractManifest = $active
    }
    $capsule = New-Capsule @capsuleArguments
    $capsulePath = Join-Path $stageDirectory 'capsule.json'
    Write-AtomicUtf8File -Path $capsulePath -Content (
        ($capsule | ConvertTo-Json -Depth 30) + [Environment]::NewLine)
    $stagedStatePath = Join-Path $stageDirectory 'control-state.json'
    Write-AtomicUtf8File -Path $stagedStatePath -Content (
        ($prospective | ConvertTo-Json -Depth 30) + [Environment]::NewLine)

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
    Move-Item -LiteralPath $stagedStatePath -Destination $statePath -Force
    $activePath = Join-Path $runDirectory 'active'
    if ([string] $prospective.status -in $terminalStatuses) {
        if (Test-Path -LiteralPath $activePath -PathType Leaf) {
            Remove-Item -LiteralPath $activePath -Force
        }
    }
    else {
        Write-AtomicUtf8File -Path $activePath -Content (
            'kicktippai.orchestrate/v3' + [Environment]::NewLine)
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
