[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string] $WorktreePath,
    [Parameter(Mandatory)][string] $ExpectedBranch,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')]
    [string] $ExpectedTip,
    [Parameter(Mandatory)][string] $RunId,
    [Parameter(Mandatory)][ValidateRange(1, [long]::MaxValue)]
    [long] $ExpectedRevision,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()]
    [string] $TransitionName,
    [string] $RepositoryRoot,
    [Parameter(Mandatory)][switch] $AgentTerminal,
    [Parameter(Mandatory)][switch] $MailboxClean,
    [Parameter(Mandatory)][switch] $NoActiveLease,
    [Parameter(Mandatory)][switch] $NoActiveProcessOwnership,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else { $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot) }
$WorktreePath = [System.IO.Path]::GetFullPath($WorktreePath)
if (
    $RunId.Length -gt 128 -or
    [System.IO.Path]::GetFileName($RunId) -ne $RunId -or
    $RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    throw 'The orchestration run ID is not a safe path segment.'
}

$primaryGitDirectory = Join-Path $RepositoryRoot '.git'
if (-not (Test-Path -LiteralPath $primaryGitDirectory -PathType Container)) {
    throw 'Worktree retirement must run against the primary checkout.'
}
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot '.tmp/worktrees'))
$allowedPrefix = $allowedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $WorktreePath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The worktree is outside the managed worktree root: $allowedRoot"
}
$statePath = [System.IO.Path]::GetFullPath(
    (Join-Path $RepositoryRoot ".tmp/orchestration/$RunId/control-state.json"))
if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
    throw 'The exact-run control state is missing; retain and park the worktree.'
}
try { $controlState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json }
catch { throw 'The exact-run control state is unreadable; retain and park the worktree.' }
if (
    [int] $controlState.schema_version -ne 1 -or
    [string] $controlState.run_id -cne $RunId -or
    [string] $controlState.session_id -cne $RunId -or
    [long] $controlState.revision -ne $ExpectedRevision) {
    throw 'The exact-run control state identity or revision changed; retain and park the worktree.'
}
$recorded = @($controlState.resource_state.worktree_reservations | Where-Object {
    try {
        $recordedPath = if ([System.IO.Path]::IsPathRooted([string] $_.path)) {
            [System.IO.Path]::GetFullPath([string] $_.path)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ([string] $_.path)))
        }
        $recordedPath.Equals(
            $WorktreePath,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch { $false }
}) | Select-Object -First 1
if (
    $null -eq $recorded -or
    [string] $recorded.class -cne 'removal-ready' -or
    [string] $recorded.branch -cne $ExpectedBranch -or
    -not ([string] $recorded.tip).Equals(
        $ExpectedTip,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Control state does not record this exact branch/tip as removal-ready; retain and park the worktree.'
}
function Stop-RetirementAndPark {
    param([Parameter(Mandatory)][string] $Message)

    $checkpoint = Join-Path $PSScriptRoot 'Set-OrchestrationCheckpoint.ps1'
    try {
        $parkResult = & $checkpoint -Action ParkWorktree -RunId $RunId `
            -ExpectedRevision $ExpectedRevision -RepositoryRoot $RepositoryRoot `
            -WorktreePath $WorktreePath -Reason $Message -AsJson | ConvertFrom-Json
    }
    catch {
        throw "$Message The checkout was retained, but its parking checkpoint failed: $($_.Exception.Message)"
    }
    throw "$Message The checkout was retained and checkpointed as parked-recovery-only at revision $($parkResult.revision)."
}
$blockingGate = @($controlState.gates | Where-Object {
    @($_.prohibited_transitions) -ccontains $TransitionName
}) | Select-Object -First 1
if ($null -ne $blockingGate) {
    Stop-RetirementAndPark -Message (
        "Retirement transition '$TransitionName' is prohibited by active gate '$($blockingGate.id)'.")
}
$worktreeLaneIds = @($controlState.lanes | Where-Object {
    if ([string]::IsNullOrWhiteSpace([string] $_.worktree_path)) { return $false }
    try {
        $lanePath = if ([System.IO.Path]::IsPathRooted([string] $_.worktree_path)) {
            [System.IO.Path]::GetFullPath([string] $_.worktree_path)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ([string] $_.worktree_path)))
        }
        return $lanePath.Equals(
            $WorktreePath,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch { return $false }
} | ForEach-Object { [string] $_.id })
$recordedOwnership = @($controlState.ownership_reservations | Where-Object {
    @($worktreeLaneIds) -ccontains [string] $_.lane_id
})
$pendingCorrections = @($controlState.correction_counters | Where-Object {
    @($worktreeLaneIds) -ccontains [string] $_.milestone_id -and
    $null -ne $_.pending_assignment_id
})
if (
    -not [string]::IsNullOrWhiteSpace([string] $recorded.owner) -or
    @($controlState.lanes | Where-Object {
        @($worktreeLaneIds) -ccontains [string] $_.id -and
        -not [string]::IsNullOrWhiteSpace([string] $_.owner)
    }).Count -gt 0 -or
    $recordedOwnership.Count -gt 0 -or
    $pendingCorrections.Count -gt 0) {
    Stop-RetirementAndPark -Message (
        'Control state still records worktree ownership or a pending assignment.')
}
if (-not ($AgentTerminal -and $MailboxClean -and $NoActiveLease -and $NoActiveProcessOwnership)) {
    Stop-RetirementAndPark -Message 'Retirement requires terminal-agent, mailbox, lease, and process-ownership confirmation.'
}

$entries = [System.Collections.Generic.List[object]]::new()
$current = $null
foreach ($line in @(& git -C $RepositoryRoot worktree list --porcelain)) {
    if ($line -like 'worktree *') {
        if ($null -ne $current) { $entries.Add([pscustomobject] $current) }
        $current = [ordered] @{ path = $line.Substring(9); head = ''; branch = ''; bare = $false }
    }
    elseif ($null -ne $current -and $line -like 'HEAD *') { $current.head = $line.Substring(5) }
    elseif ($null -ne $current -and $line -like 'branch *') { $current.branch = $line.Substring(7) }
    elseif ($null -ne $current -and $line -eq 'bare') { $current.bare = $true }
}
if ($LASTEXITCODE -ne 0) {
    Stop-RetirementAndPark -Message 'Git worktree inventory is unavailable.'
}
if ($null -ne $current) { $entries.Add([pscustomobject] $current) }

$entry = @($entries | Where-Object {
    [System.IO.Path]::GetFullPath([string] $_.path).Equals(
        $WorktreePath, [System.StringComparison]::OrdinalIgnoreCase)
}) | Select-Object -First 1
if ($null -eq $entry -or $entry.bare) {
    Stop-RetirementAndPark -Message 'The exact linked worktree is absent or not removable.'
}
$expectedBranchRef = "refs/heads/$ExpectedBranch"
if ([string] $entry.branch -cne $expectedBranchRef) {
    Stop-RetirementAndPark -Message "Worktree branch mismatch: expected '$expectedBranchRef', found '$($entry.branch)'."
}
if (-not ([string] $entry.head).Equals($ExpectedTip, [System.StringComparison]::OrdinalIgnoreCase)) {
    Stop-RetirementAndPark -Message "Worktree tip mismatch: expected '$ExpectedTip', found '$($entry.head)'."
}
$branchTip = (& git -C $RepositoryRoot rev-parse --verify "$expectedBranchRef^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or
    -not $branchTip.Equals($ExpectedTip, [System.StringComparison]::OrdinalIgnoreCase)) {
    Stop-RetirementAndPark -Message 'The retained branch does not preserve the exact worktree tip.'
}
$status = @(& git -C $WorktreePath status --porcelain=v1)
if ($LASTEXITCODE -ne 0) {
    Stop-RetirementAndPark -Message 'Worktree status is unavailable.'
}
if ($status.Count -gt 0) {
    Stop-RetirementAndPark -Message 'The worktree is dirty and requires remediation.'
}

$removed = $false
if ($PSCmdlet.ShouldProcess($WorktreePath, "retire clean worktree at $ExpectedTip")) {
    & git -C $RepositoryRoot worktree remove -- $WorktreePath
    if ($LASTEXITCODE -ne 0) {
        Stop-RetirementAndPark -Message 'git worktree remove failed.'
    }
    $removed = $true
}

$result = [pscustomobject] [ordered] @{
    worktree = $WorktreePath
    branch = $ExpectedBranch
    tip = $ExpectedTip.ToLowerInvariant()
    retained_branch = $true
    removed = $removed
    checkpoint_required = $removed
}
if ($AsJson) { $result | ConvertTo-Json -Compress } else { $result }
