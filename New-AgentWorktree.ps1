[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $Name,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Branch,

    [ValidateNotNullOrEmpty()]
    [string] $StartPoint = 'HEAD'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$solutionPath = Join-Path $repositoryRoot 'KicktippAi.slnx'
$primaryGitDirectory = Join-Path $repositoryRoot '.git'

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "The script directory is not the KicktippAi repository root: $repositoryRoot"
}

if (-not (Test-Path -LiteralPath $primaryGitDirectory -PathType Container)) {
    throw 'Run this script from the primary checkout. A linked worktree has a .git file and cannot own another agent worktree.'
}

$worktreesRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.tmp/worktrees'))
$worktreePath = [System.IO.Path]::GetFullPath((Join-Path $worktreesRoot $Name))
$expectedPrefix = $worktreesRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $worktreePath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Worktree path must remain below $worktreesRoot"
}

if (Test-Path -LiteralPath $worktreePath) {
    throw "Worktree path already exists: $worktreePath"
}

& git check-ref-format --branch $Branch | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Invalid Git branch name: $Branch"
}

& git -C $repositoryRoot check-ignore --no-index --quiet '.codex-local/original-repository-path'
if ($LASTEXITCODE -ne 0) {
    throw 'The required .codex-local/original-repository-path locator is not ignored by Git.'
}

New-Item -ItemType Directory -Path $worktreesRoot -Force | Out-Null

& git -C $repositoryRoot show-ref --verify --quiet "refs/heads/$Branch"
$branchExists = $LASTEXITCODE -eq 0

if ($branchExists) {
    & git -C $repositoryRoot worktree add $worktreePath $Branch
}
else {
    & git -C $repositoryRoot rev-parse --verify --quiet "$StartPoint^{commit}" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Start point does not resolve to a commit: $StartPoint"
    }

    & git -C $repositoryRoot worktree add $worktreePath -b $Branch $StartPoint
}

if ($LASTEXITCODE -ne 0) {
    throw "git worktree add failed for branch '$Branch' at '$worktreePath'."
}

$locatorDirectory = Join-Path $worktreePath '.codex-local'
$locatorPath = Join-Path $locatorDirectory 'original-repository-path'
New-Item -ItemType Directory -Path $locatorDirectory -Force | Out-Null

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText(
    $locatorPath,
    $repositoryRoot + [System.Environment]::NewLine,
    $utf8NoBom)

$resolvedLocator = [System.IO.Path]::GetFullPath(
    [System.IO.File]::ReadAllText($locatorPath).Trim())
if (-not $resolvedLocator.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Original-repository locator validation failed: $locatorPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $resolvedLocator 'KicktippAi.slnx') -PathType Leaf)) {
    throw "Original-repository locator does not identify a KicktippAi checkout: $resolvedLocator"
}

& git -C $worktreePath check-ignore --quiet '.codex-local/original-repository-path'
if ($LASTEXITCODE -ne 0) {
    throw "Worktree locator is not ignored by Git: $locatorPath"
}

$checkedOutBranch = (& git -C $worktreePath branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or -not $checkedOutBranch.Equals($Branch, [System.StringComparison]::Ordinal)) {
    throw "Worktree branch validation failed. Expected '$Branch', found '$checkedOutBranch'."
}

Write-Output "Created worktree: $worktreePath"
Write-Output "Branch: $checkedOutBranch"
Write-Output "Original checkout locator: $locatorPath"
