[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Trip', 'Clear')]
    [string] $Action = 'Trip',
    [Parameter(Mandatory)][string] $RunId,
    [Parameter(Mandatory)][ValidateRange(1, [long]::MaxValue)]
    [long] $ExpectedRevision,
    [string] $RepositoryRoot,
    [string] $OperationProfile,
    [string] $OperationFingerprint,
    [string] $Operation,
    [string] $Reason,
    [string] $ReviewedBy,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else { $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot) }

function Resolve-OrchestrationPrimaryCheckout {
    param([Parameter(Mandatory)][string] $CheckoutRoot)

    $commonDirectoryOutput = @(
        & git -C $CheckoutRoot rev-parse --path-format=absolute --git-common-dir 2>$null)
    $gitExitCode = $LASTEXITCODE
    $commonDirectory = [string] ($commonDirectoryOutput | Select-Object -First 1)
    if ($gitExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($commonDirectory)) {
        throw 'Git common-directory identity is unavailable.'
    }
    $commonDirectory = [System.IO.Path]::GetFullPath($commonDirectory.Trim())
    if (Test-Path -LiteralPath (Join-Path $CheckoutRoot '.git') -PathType Container) {
        $primaryGitDirectory = [System.IO.Path]::GetFullPath((Join-Path $CheckoutRoot '.git'))
        if (-not $commonDirectory.Equals(
            $primaryGitDirectory,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The checkout Git directory does not match its Git common-directory identity.'
        }
        return $CheckoutRoot
    }

    $locatorPath = Join-Path $CheckoutRoot '.codex-local/original-repository-path'
    if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
        throw 'The original-checkout locator is missing.'
    }
    $primaryRoot = [System.IO.Path]::GetFullPath(
        ([System.IO.File]::ReadAllText($locatorPath).Trim()))
    $primaryGitDirectory = [System.IO.Path]::GetFullPath((Join-Path $primaryRoot '.git'))
    if (
        -not (Test-Path -LiteralPath $primaryGitDirectory -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $primaryRoot 'KicktippAi.slnx') -PathType Leaf) -or
        -not $commonDirectory.Equals(
            $primaryGitDirectory,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The original-checkout locator does not match this worktree Git common directory.'
    }
    return $primaryRoot
}

$primaryRoot = Resolve-OrchestrationPrimaryCheckout -CheckoutRoot $RepositoryRoot
$checkpoint = Join-Path $primaryRoot '.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1'
if (-not (Test-Path -LiteralPath $checkpoint -PathType Leaf)) {
    throw 'The canonical checkpoint helper is missing from the primary checkout.'
}
$checkpointArguments = @{
    Action = if ($Action -eq 'Trip') {
        'TripMemoryCircuitBreaker'
    }
    else { 'ClearMemoryCircuitBreaker' }
    RunId = $RunId
    ExpectedRevision = $ExpectedRevision
    RepositoryRoot = $primaryRoot
    Reason = $Reason
    AsJson = $true
}
if ($Action -eq 'Trip') {
    $checkpointArguments.OperationProfile = $OperationProfile
    $checkpointArguments.OperationFingerprint = $OperationFingerprint
    $checkpointArguments.Operation = $Operation
}
else { $checkpointArguments.ReviewedBy = $ReviewedBy }

$target = Join-Path $primaryRoot ".tmp/orchestration/$RunId/control-state.json"
if ($PSCmdlet.ShouldProcess($target, "$Action exact-run memory circuit breaker")) {
    $result = (& $checkpoint @checkpointArguments) | ConvertFrom-Json
}
else {
    $result = [pscustomobject] [ordered] @{
        run_id = $RunId
        revision = $ExpectedRevision
        action = $Action
        changed = $false
        what_if = $true
    }
}
if ($AsJson) { $result | ConvertTo-Json -Depth 5 -Compress } else { $result }
