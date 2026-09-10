[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$retirementHelper = Join-Path $PSScriptRoot 'Remove-OrchestrationWorktree.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-worktree-$([Guid]::NewGuid().ToString('N'))"
$worktreePath = Join-Path $testRoot '.tmp/worktrees/retire-me'
$outsidePath = Join-Path $testRoot 'outside'
$branch = 'codex/retirement-test'

function Assert-True {
    param([Parameter(Mandatory)] $Condition, [Parameter(Mandatory)][string] $Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Invoke-Retirement {
    param(
        [string] $Path = $worktreePath,
        [string] $ExpectedBranch = $branch,
        [string] $ExpectedTip = $tip,
        [switch] $WhatIf
    )
    $arguments = @{
        WorktreePath = $Path
        ExpectedBranch = $ExpectedBranch
        ExpectedTip = $ExpectedTip
        RepositoryRoot = $testRoot
        AgentTerminal = $true
        MailboxClean = $true
        NoActiveLease = $true
        NoActiveProcessOwnership = $true
        AsJson = $true
    }
    if ($WhatIf) { $arguments.WhatIf = $true }
    return (& $retirementHelper @arguments | ConvertFrom-Json)
}

function Assert-RejectedAndRetained {
    param([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $Pattern)
    $rejected = $false
    try { & $Action | Out-Null }
    catch { $rejected = $_.Exception.Message -match $Pattern }
    Assert-True $rejected "retirement must reject: $Pattern"
    Assert-True (Test-Path -LiteralPath $worktreePath -PathType Container) 'a failed precondition must retain the worktree for parking/remediation'
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    & git -C $testRoot init --quiet
    & git -C $testRoot config user.email 'orchestration-test@example.invalid'
    & git -C $testRoot config user.name 'Orchestration Test'
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'tracked.txt'),
        "base$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    & git -C $testRoot add tracked.txt
    & git -C $testRoot commit --quiet -m 'test base'
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the retirement test commit.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $worktreePath) -Force | Out-Null
    & git -C $testRoot worktree add --quiet -b $branch $worktreePath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the retirement test worktree.' }
    $tip = (& git -C $worktreePath rev-parse HEAD).Trim()

    Assert-RejectedAndRetained -Pattern 'outside the managed worktree root' -Action {
        Invoke-Retirement -Path $outsidePath
    }
    Assert-RejectedAndRetained -Pattern 'branch mismatch' -Action {
        Invoke-Retirement -ExpectedBranch 'codex/wrong'
    }
    Assert-RejectedAndRetained -Pattern 'tip mismatch' -Action {
        Invoke-Retirement -ExpectedTip ('0' * 40)
    }

    [System.IO.File]::WriteAllText(
        (Join-Path $worktreePath 'untracked.txt'),
        "dirty$([Environment]::NewLine)",
        [System.Text.UTF8Encoding]::new($false))
    Assert-RejectedAndRetained -Pattern 'dirty' -Action { Invoke-Retirement }
    Remove-Item -LiteralPath (Join-Path $worktreePath 'untracked.txt') -Force

    $whatIfResult = Invoke-Retirement -WhatIf
    Assert-True (-not $whatIfResult.removed) 'WhatIf must report no removal'
    Assert-True (Test-Path -LiteralPath $worktreePath -PathType Container) 'WhatIf must retain the checkout'

    $result = Invoke-Retirement
    Assert-True ($result.removed -and $result.retained_branch) 'clean exact retirement must remove only the checkout'
    Assert-True (-not (Test-Path -LiteralPath $worktreePath)) 'the retired checkout must be absent'
    $retainedTip = (& git -C $testRoot rev-parse "refs/heads/$branch").Trim()
    Assert-True ($retainedTip -eq $tip) 'retirement must preserve the exact branch and tip'

    Write-Output 'Orchestration worktree-retirement tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
        $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTestRoot.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a test directory outside the system temp root.'
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
