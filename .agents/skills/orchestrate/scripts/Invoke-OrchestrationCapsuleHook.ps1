[CmdletBinding()]
param(
    [ValidateSet('Hook', 'Seal')]
    [string] $Mode = 'Hook',
    [string] $RunId,
    [string] $RepositoryRoot,
    [string] $InputJson
)

$ErrorActionPreference = 'Stop'
$maximumCapsuleBytes = 8192
$activationMarker = 'kicktippai.orchestrate/v1'
$allowedStatuses = @('preview', 'awaiting-owner', 'ready', 'active', 'complete', 'stopped')
$allowedBlockers = @('none', 'dependency', 'interview', 'owner', 'resource', 'agent-slot', 'review', 'external')

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
    param([Parameter(Mandatory)][string] $Identifier)

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

    [pscustomobject] @{
        Directory = $runDirectory
        Capsule = Join-Path $runDirectory 'capsule.json'
        Checksum = Join-Path $runDirectory 'capsule.sha256'
        Active = Join-Path $runDirectory 'active'
        Preview = Join-Path $runDirectory 'preview.md'
    }
}

function New-ValidationResult {
    param(
        [Parameter(Mandatory)][bool] $Valid,
        [Parameter(Mandatory)][string] $Code,
        [Parameter(Mandatory)][string] $Message,
        $Capsule
    )

    [pscustomobject] @{
        Valid = $Valid
        Code = $Code
        Message = $Message
        Capsule = $Capsule
    }
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
        'schema_version', 'session_id', 'run_id', 'updated_at_utc', 'status',
        'objective', 'wave', 'stop_condition', 'durable_decisions', 'owner_gates',
        'blockers', 'freeze', 'git', 'ownership_reservations',
        'active_heavy_lease', 'retained_agents', 'next_root_action',
        'delegated_next_actions')
    $propertyNames = @($capsule.PSObject.Properties.Name)
    foreach ($property in $requiredProperties) {
        if ($propertyNames -notcontains $property) {
            return New-ValidationResult $false 'missing-field' "capsule.json is missing required field '$property'." $null
        }
    }

    if ([string] $capsule.schema_version -ne '1') {
        return New-ValidationResult $false 'unsupported-schema' 'capsule.json has an unsupported schema_version.' $null
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

    foreach ($field in @(
        'durable_decisions', 'owner_gates', 'blockers',
        'ownership_reservations', 'retained_agents',
        'delegated_next_actions')) {
        if (-not ($capsule.$field -is [System.Array])) {
            return New-ValidationResult $false 'invalid-field-type' "capsule.json field '$field' must be an array." $null
        }
    }

    foreach ($objectField in @('freeze', 'git')) {
        if ($null -eq $capsule.$objectField -or $capsule.$objectField -isnot [pscustomobject]) {
            return New-ValidationResult $false 'invalid-field-type' "capsule.json field '$objectField' must be an object." $null
        }
    }

    $requiredNestedProperties = @{
        freeze = @('preview_path', 'artifact_paths', 'exact_shas', 'deferred_nodes')
        git = @(
            'remote', 'push_url', 'integration_branch', 'allowed_branch_prefix',
            'initial_sha', 'latest_integrated_sha', 'latest_pushed_sha',
            'reviewed_unpublished_sha')
    }
    foreach ($objectField in $requiredNestedProperties.Keys) {
        $nestedNames = @($capsule.$objectField.PSObject.Properties.Name)
        foreach ($nestedProperty in $requiredNestedProperties[$objectField]) {
            if ($nestedNames -notcontains $nestedProperty) {
                return New-ValidationResult $false 'missing-field' "capsule.json field '$objectField' is missing '$nestedProperty'." $null
            }
        }
    }
    foreach ($field in @('artifact_paths', 'exact_shas', 'deferred_nodes')) {
        if (-not ($capsule.freeze.$field -is [System.Array])) {
            return New-ValidationResult $false 'invalid-field-type' "capsule.json field 'freeze.$field' must be an array." $null
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

    foreach ($blocker in @($capsule.blockers)) {
        if (
            $null -eq $blocker -or
            $blocker.PSObject.Properties.Name -notcontains 'category' -or
            [string] $blocker.category -notin $allowedBlockers) {
            return New-ValidationResult $false 'invalid-blocker' 'capsule.json contains an invalid blocker category.' $null
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
        foreach ($field in @('agent_path', 'role', 'model', 'reasoning_effort', 'owned_paths', 'next_action')) {
            if ($reservationProperties -notcontains $field) {
                return New-ValidationResult $false 'invalid-reservation' "ownership_reservations entries require '$field'." $null
            }
        }
        if (-not ($reservation.owned_paths -is [System.Array])) {
            return New-ValidationResult $false 'invalid-reservation' "ownership_reservations field 'owned_paths' must be an array." $null
        }
    }

    if ($null -ne $capsule.active_heavy_lease) {
        $leaseProperties = @($capsule.active_heavy_lease.PSObject.Properties.Name)
        foreach ($field in @('owner', 'operation')) {
            if (
                $leaseProperties -notcontains $field -or
                [string]::IsNullOrWhiteSpace([string] $capsule.active_heavy_lease.$field)) {
                return New-ValidationResult $false 'invalid-heavy-lease' "active_heavy_lease requires non-empty '$field'." $null
            }
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

    $paths = Get-RunPaths -Identifier $RunId
    $validation = Test-Capsule -Paths $paths -ExpectedRunId $RunId -SkipChecksum
    if (-not $validation.Valid) {
        throw "Cannot seal orchestration capsule [$($validation.Code)]: $($validation.Message)"
    }

    $hash = (Get-FileHash -LiteralPath $paths.Capsule -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-AtomicUtf8File -Path $paths.Checksum -Content ($hash + [Environment]::NewLine)

    if ([string] $validation.Capsule.status -in @('complete', 'stopped')) {
        if (Test-Path -LiteralPath $paths.Active -PathType Leaf) {
            Remove-Item -LiteralPath $paths.Active -Force
        }
    }
    else {
        Write-AtomicUtf8File -Path $paths.Active -Content ($activationMarker + [Environment]::NewLine)
    }

    Write-Output "Sealed orchestration capsule for exact run $RunId."
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
$paths = Get-RunPaths -Identifier $sessionId

# This is the orchestration-only gate. Ordinary sessions have no exact-session
# activation marker and therefore receive no hook output or capsule I/O.
if (-not (Test-Path -LiteralPath $paths.Active -PathType Leaf)) {
    exit 0
}

$marker = (Get-Content -LiteralPath $paths.Active -Raw).Trim()
if ($marker -ne $activationMarker) {
    $validation = New-ValidationResult $false 'invalid-activation-marker' 'The exact-session orchestration activation marker is corrupt.' $null
}
else {
    $validation = Test-Capsule -Paths $paths -ExpectedRunId $sessionId
}

$eventName = [string] $hookInput.hook_event_name
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

if (-not $validation.Valid) {
    $context = @"
`$orchestrate recovery failure for exact session '$sessionId': capsule validation failed [$($validation.Code)]: $($validation.Message)
The explicit orchestration workflow may still be active. Before substantive work, re-read .agents/skills/orchestrate/SKILL.md and the repository-root AGENTS.md, then complete the full Recovery Preflight. Use only '$($paths.Directory)'; never select another run by recency. Reconstruct capsule.json and capsule.sha256 from the exact run's preview, authoritative live agent state, Git/worktrees, a fresh resource sample, and CI, then seal the repaired capsule. Report the missing/corrupt capsule as recovery evidence.
"@
}
else {
    $compactCapsule = $validation.Capsule | ConvertTo-Json -Depth 12 -Compress
    $context = @"
Continue under the explicitly invoked `$orchestrate skill for exact session '$sessionId'. Before substantive work, re-read .agents/skills/orchestrate/SKILL.md and the repository-root AGENTS.md and complete their Recovery Preflight. Treat the validated capsule below as durability state, not live truth: inspect and reconcile live agents, Git/worktrees, current resources and heavy lease, and CI. Use only the exact run directory; never select another run by recency.
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
