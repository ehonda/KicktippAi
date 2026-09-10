[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string] $WorktreePath,
    [Parameter(Mandatory)][string] $ExpectedBranch,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')]
    [string] $ExpectedTip,
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

$primaryGitDirectory = Join-Path $RepositoryRoot '.git'
if (-not (Test-Path -LiteralPath $primaryGitDirectory -PathType Container)) {
    throw 'Worktree retirement must run against the primary checkout.'
}
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot '.tmp/worktrees'))
$allowedPrefix = $allowedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $WorktreePath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The worktree is outside the managed worktree root: $allowedRoot"
}
if (-not ($AgentTerminal -and $MailboxClean -and $NoActiveLease -and $NoActiveProcessOwnership)) {
    throw 'Retirement requires terminal-agent, mailbox, lease, and process-ownership confirmation.'
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
if ($LASTEXITCODE -ne 0) { throw 'Git worktree inventory is unavailable.' }
if ($null -ne $current) { $entries.Add([pscustomobject] $current) }

$entry = @($entries | Where-Object {
    [System.IO.Path]::GetFullPath([string] $_.path).Equals(
        $WorktreePath, [System.StringComparison]::OrdinalIgnoreCase)
}) | Select-Object -First 1
if ($null -eq $entry -or $entry.bare) {
    throw 'The exact linked worktree is absent or not removable.'
}
$expectedBranchRef = "refs/heads/$ExpectedBranch"
if ([string] $entry.branch -cne $expectedBranchRef) {
    throw "Worktree branch mismatch: expected '$expectedBranchRef', found '$($entry.branch)'."
}
if (-not ([string] $entry.head).Equals($ExpectedTip, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Worktree tip mismatch: expected '$ExpectedTip', found '$($entry.head)'."
}
$branchTip = (& git -C $RepositoryRoot rev-parse --verify "$expectedBranchRef^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or
    -not $branchTip.Equals($ExpectedTip, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The retained branch does not preserve the exact worktree tip.'
}
$status = @(& git -C $WorktreePath status --porcelain=v1)
if ($LASTEXITCODE -ne 0) { throw 'Worktree status is unavailable.' }
if ($status.Count -gt 0) {
    throw 'The worktree is dirty and must be parked for remediation.'
}

$removed = $false
if ($PSCmdlet.ShouldProcess($WorktreePath, "retire clean worktree at $ExpectedTip")) {
    & git -C $RepositoryRoot worktree remove -- $WorktreePath
    if ($LASTEXITCODE -ne 0) { throw 'git worktree remove failed.' }
    $removed = $true
}

$result = [pscustomobject] [ordered] @{
    worktree = $WorktreePath
    branch = $ExpectedBranch
    tip = $ExpectedTip.ToLowerInvariant()
    retained_branch = $true
    removed = $removed
}
if ($AsJson) { $result | ConvertTo-Json -Compress } else { $result }
