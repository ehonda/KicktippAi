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

$statePath = Join-Path $RepositoryRoot '.tmp/orchestration/resource-policy-state.json'
$stateDirectory = Split-Path -Parent $statePath

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
    $state = [ordered] @{
        schema_version = 1
        status = 'active'
        effective_floor_gib = 1.1
        updated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        run_id = $RunId
        operation = $Operation
        reason = $Reason
        reviewed_by = $null
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($ReviewedBy) -or [string]::IsNullOrWhiteSpace($Reason)) {
        throw 'ReviewedBy and Reason are required to clear the circuit breaker after owner-reviewed analysis.'
    }
    $state = [ordered] @{
        schema_version = 1
        status = 'cleared'
        effective_floor_gib = 1.0
        updated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        run_id = $RunId
        operation = $Operation
        reason = $Reason
        reviewed_by = $ReviewedBy
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
