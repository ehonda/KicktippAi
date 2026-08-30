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

$resourceHelperPath = Join-Path $repositoryRoot '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1'
if (-not (Test-Path -LiteralPath $resourceHelperPath -PathType Leaf)) {
    throw "The orchestration resource admission helper is missing: $resourceHelperPath"
}

$resourceSnapshot = & $resourceHelperPath -Admission Worktree -RepositoryRoot $repositoryRoot
if (-not $resourceSnapshot.WorktreeAdmission.Allowed) {
    throw "Worktree resource admission failed. $($resourceSnapshot.WorktreeAdmission.Reason)"
}
Write-Verbose $resourceSnapshot.WorktreeAdmission.Reason

& git check-ref-format --branch $Branch | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Invalid Git branch name: $Branch"
}

& git -C $repositoryRoot check-ignore --no-index --quiet '.codex-local/original-repository-path'
if ($LASTEXITCODE -ne 0) {
    throw 'The required .codex-local/original-repository-path locator is not ignored by Git.'
}

New-Item -ItemType Directory -Path $worktreesRoot -Force | Out-Null

$startCommitOutput = & git -C $repositoryRoot rev-parse --verify --quiet "$StartPoint^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Start point does not resolve to a commit: $StartPoint"
}
$startCommit = ($startCommitOutput -join '').Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($startCommit)) {
    throw "Failed to resolve the exact start commit: $StartPoint"
}

$branchRef = "refs/heads/$Branch"
& git -C $repositoryRoot show-ref --verify --quiet "refs/heads/$Branch"
$branchExists = $LASTEXITCODE -eq 0

if ($branchExists) {
    $branchCommitOutput = & git -C $repositoryRoot rev-parse --verify "${branchRef}^{commit}"
    $branchCommit = ($branchCommitOutput -join '').Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branchCommit)) {
        throw "Failed to resolve existing branch '$Branch'."
    }

    if (-not $branchCommit.Equals($startCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Existing branch '$Branch' points to $branchCommit, not requested start commit $startCommit."
    }
}

$worktreeAdded = $false
$branchCreatedByInvocation = -not $branchExists

try {
    if ($branchExists) {
        & git -C $repositoryRoot worktree add $worktreePath $Branch
    }
    else {
        & git -C $repositoryRoot worktree add $worktreePath -b $Branch $startCommit
    }

    if ($LASTEXITCODE -ne 0) {
        throw "git worktree add failed for branch '$Branch' at '$worktreePath'."
    }
    $worktreeAdded = $true

    $checkedOutCommitOutput = & git -C $worktreePath rev-parse --verify 'HEAD^{commit}'
    $checkedOutCommitExitCode = $LASTEXITCODE
    $checkedOutCommit = ($checkedOutCommitOutput -join '').Trim()
    if (
        $checkedOutCommitExitCode -ne 0 -or
        [string]::IsNullOrWhiteSpace($checkedOutCommit) -or
        -not $checkedOutCommit.Equals($startCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Worktree commit validation failed. Expected '$startCommit', found '$checkedOutCommit'."
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
    Write-Output "Start commit: $startCommit"
    Write-Output "Original checkout locator: $locatorPath"
}
catch {
    $originalException = $_.Exception
    $rollback = [System.Collections.Generic.List[string]]::new()
    $worktreeRemoved = $false

    if ($worktreeAdded) {
        $removeOutput = & git -C $repositoryRoot worktree remove --force $worktreePath 2>&1
        if ($LASTEXITCODE -eq 0) {
            $worktreeRemoved = $true
            $rollback.Add('removed helper-created worktree')
            & git -C $repositoryRoot worktree prune
            if ($LASTEXITCODE -ne 0) {
                $rollback.Add('worktree prune failed')
            }
        }
        else {
            $renderedRemoveOutput = ($removeOutput | ForEach-Object { $_.ToString() }) -join ' '
            $rollback.Add("worktree removal failed: $renderedRemoveOutput")
        }
    }
    else {
        $rollback.Add('no successfully added worktree to remove')
    }

    if ($branchCreatedByInvocation -and $worktreeRemoved) {
        $createdBranchCommitOutput = & git -C $repositoryRoot rev-parse --verify --quiet "${branchRef}^{commit}" 2>$null
        $createdBranchLookupExitCode = $LASTEXITCODE
        $createdBranchCommit = ($createdBranchCommitOutput -join '').Trim()
        if ($createdBranchLookupExitCode -ne 0) {
            $rollback.Add('helper-created branch was already absent')
        }
        elseif ($createdBranchCommit.Equals($startCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
            & git -C $repositoryRoot update-ref -d $branchRef $startCommit
            if ($LASTEXITCODE -eq 0) {
                $rollback.Add('removed unadvanced helper-created branch')
            }
            else {
                $rollback.Add('helper-created branch deletion failed')
            }
        }
        else {
            $rollback.Add("preserved helper-created branch because it advanced to $createdBranchCommit")
        }
    }
    elseif ($branchCreatedByInvocation) {
        $rollback.Add('preserved helper-created branch because worktree removal did not succeed')
    }

    $rollbackSummary = $rollback -join '; '
    throw [System.InvalidOperationException]::new(
        "$($originalException.Message) Rollback: $rollbackSummary.",
        $originalException)
}
