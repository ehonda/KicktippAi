[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Trip', 'Clear')]
    [string] $Action = 'Trip',
    [string] $RepositoryRoot,
    [string] $RunId,
    [string] $Operation,
    [string] $Reason,
    [string] $ReviewedBy
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$stateRepositoryRoot = $RepositoryRoot
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git') -PathType Container)) {
    $locatorPath = Join-Path $RepositoryRoot '.codex-local/original-repository-path'
    if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
        throw 'Cannot resolve the shared memory circuit-breaker path: original-checkout locator is missing.'
    }
    $stateRepositoryRoot = [System.IO.Path]::GetFullPath(
        ([System.IO.File]::ReadAllText($locatorPath).Trim()))
    if (
        -not (Test-Path -LiteralPath (Join-Path $stateRepositoryRoot '.git') -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $stateRepositoryRoot 'KicktippAi.slnx') -PathType Leaf)) {
        throw 'Cannot resolve the shared memory circuit-breaker path: original-checkout locator is invalid.'
    }
}

$statePath = Join-Path $stateRepositoryRoot '.tmp/orchestration/resource-policy-state.json'
$stateDirectory = Split-Path -Parent $statePath

function Test-OrchestrationTimestamp {
    param([string] $Value)

    $parsed = [DateTimeOffset]::MinValue
    return (
        -not [string]::IsNullOrWhiteSpace($Value) -and
        [DateTimeOffset]::TryParse(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal,
            [ref] $parsed))
}

$existingState = $null
if (Test-Path -LiteralPath $statePath -PathType Leaf) {
    try {
        $existingState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    }
    catch {
        throw 'The existing memory circuit-breaker state is unreadable; refuse to overwrite it.'
    }
}

if ($Action -eq 'Trip') {
    foreach ($entry in @{
        RunId = $RunId
        Operation = $Operation
        Reason = $Reason
    }.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([string] $entry.Value)) {
            throw "$($entry.Key) is required when tripping the circuit breaker."
        }
    }
    if ($null -ne $existingState -and [string] $existingState.status -eq 'active') {
        throw 'The memory circuit breaker is already active; preserve its original trigger and record later operation evidence separately.'
    }
    $triggeredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $state = [ordered] @{
        schema_version = 1
        status = 'active'
        effective_floor_gib = 1.1
        updated_at_utc = $triggeredAtUtc
        trigger = [ordered] @{
            run_id = $RunId
            operation = $Operation
            at_utc = $triggeredAtUtc
            reason = $Reason
        }
        clearance = $null
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($ReviewedBy) -or [string]::IsNullOrWhiteSpace($Reason)) {
        throw 'ReviewedBy and Reason are required to clear the circuit breaker after owner-reviewed analysis.'
    }
    if (
        $null -eq $existingState -or
        [int] $existingState.schema_version -ne 1 -or
        [string] $existingState.status -ne 'active' -or
        [double] $existingState.effective_floor_gib -ne 1.1 -or
        -not (Test-OrchestrationTimestamp -Value ([string] $existingState.updated_at_utc)) -or
        $null -eq $existingState.trigger -or
        [string]::IsNullOrWhiteSpace([string] $existingState.trigger.run_id) -or
        [string]::IsNullOrWhiteSpace([string] $existingState.trigger.operation) -or
        -not (Test-OrchestrationTimestamp -Value ([string] $existingState.trigger.at_utc)) -or
        [string]::IsNullOrWhiteSpace([string] $existingState.trigger.reason) -or
        $null -ne $existingState.clearance) {
        throw 'Clearing requires an existing valid active circuit breaker with preserved trigger evidence.'
    }
    $clearedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $state = [ordered] @{
        schema_version = 1
        status = 'cleared'
        effective_floor_gib = 1.0
        updated_at_utc = $clearedAtUtc
        trigger = $existingState.trigger
        clearance = [ordered] @{
            at_utc = $clearedAtUtc
            reviewed_by = $ReviewedBy
            reason = $Reason
        }
    }
}

if ($PSCmdlet.ShouldProcess($statePath, "$Action orchestration memory circuit breaker")) {
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    $temporaryPath = "$statePath.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        $json = $state | ConvertTo-Json -Depth 4
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            $json + [Environment]::NewLine,
            [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporaryPath -Destination $statePath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

[pscustomobject] $state
