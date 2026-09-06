[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$hook = Join-Path $PSScriptRoot 'Invoke-OrchestrationCapsuleHook.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "orchestration-capsule-$([Guid]::NewGuid().ToString('N'))"
$runId = 'test-session-123'
$runDirectory = Join-Path $testRoot ".tmp/orchestration/$runId"

function Assert-True {
    param(
        [Parameter(Mandatory)] $Condition,
        [Parameter(Mandatory)][string] $Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function New-HookInput {
    param([Parameter(Mandatory)][string] $EventName)

    [pscustomobject] @{
        session_id = $runId
        transcript_path = $null
        cwd = $testRoot
        hook_event_name = $EventName
        model = 'gpt-5.6-sol'
        source = if ($EventName -eq 'SessionStart') { 'compact' } else { $null }
        trigger = if ($EventName -eq 'PreCompact') { 'auto' } else { $null }
    } | ConvertTo-Json -Compress
}

function New-ValidCapsule {
    param([string] $Status = 'active')

    [ordered] @{
        schema_version = 1
        session_id = $runId
        run_id = $runId
        updated_at_utc = '2026-09-06T12:00:00Z'
        status = $Status
        objective = 'Verify recovery capsule behavior.'
        wave = 'test'
        stop_condition = 'All assertions pass.'
        durable_decisions = @('Use exact-session recovery.')
        owner_gates = @()
        blockers = @()
        freeze = [ordered] @{
            preview_path = ".tmp/orchestration/$runId/preview.md"
            artifact_paths = @()
            exact_shas = @()
            deferred_nodes = @()
        }
        git = [ordered] @{
            remote = 'origin'
            push_url = 'https://github.com/ehonda/KicktippAi.git'
            integration_branch = 'main'
            allowed_branch_prefix = 'codex/test-'
            initial_sha = '0123456789012345678901234567890123456789'
            latest_integrated_sha = ''
            latest_pushed_sha = ''
            reviewed_unpublished_sha = ''
        }
        ownership_reservations = @()
        active_heavy_lease = $null
        retained_agents = @([ordered] @{
            agent_path = '/root/reviewer'
            role = 'review'
            reason = 'One near-term review follow-up.'
            release_trigger = 'Acceptance, role change, or context-cost limit.'
            context_cost_limit = 'maximum 2 completed follow-up turns'
        })
        next_root_action = 'Inspect live state.'
        delegated_next_actions = @('Keep implementation delegated.')
    }
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null

    $plainOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart')
    Assert-True ([string]::IsNullOrWhiteSpace($plainOutput)) 'plain sessions must receive no hook output'

    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $runDirectory 'active') -Value 'kicktippai.orchestrate/v1' -NoNewline

    $missingOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    $missingContext = $missingOutput.hookSpecificOutput.additionalContext
    Assert-True ($missingContext -match 'missing-capsule') 'missing capsules must be forwarded after compaction'
    Assert-True ($missingContext -match '\$orchestrate') 'recovery must explicitly continue under the orchestration skill'
    Assert-True ($missingContext -match 'never select another run by recency') 'recovery must reject recency fallback'

    $preCompactOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'PreCompact') | ConvertFrom-Json
    Assert-True ($preCompactOutput.continue) 'a missing capsule must not strand automatic compaction'
    Assert-True ($preCompactOutput.systemMessage -match 'missing-capsule') 'pre-compaction validation must surface the failure'

    Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Value '{not-json' -NoNewline
    Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.sha256') -Value ('0' * 64) -NoNewline
    $corruptOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($corruptOutput.hookSpecificOutput.additionalContext -match 'malformed-json') 'malformed JSON must be forwarded'

    Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Value ('x' * 8193) -NoNewline
    $oversizedOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($oversizedOutput.hookSpecificOutput.additionalContext -match 'oversized-capsule') 'oversized capsules must be forwarded'

    New-ValidCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    Assert-True (Test-Path -LiteralPath (Join-Path $runDirectory 'capsule.sha256')) 'seal mode must create the checksum'

    $validOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    $validContext = $validOutput.hookSpecificOutput.additionalContext
    Assert-True ($validContext -match 'Continue under the explicitly invoked \$orchestrate skill') 'valid recovery must re-activate the skill contract'
    Assert-True ($validContext -match 'VALIDATED ORCHESTRATION CAPSULE') 'valid recovery must inject the capsule'
    Assert-True ($validContext -match 'Inspect live state') 'valid recovery must retain the next root action'
    $validPreCompactOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'PreCompact')
    Assert-True ([string]::IsNullOrWhiteSpace($validPreCompactOutput)) 'valid pre-compaction checks must stay silent'

    Add-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Value ' '
    $hashOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($hashOutput.hookSpecificOutput.additionalContext -match 'checksum-mismatch') 'checksum mismatch must be forwarded'

    New-ValidCapsule -Status complete | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $runDirectory 'active'))) 'terminal capsules must clear the activation marker'
    $completeOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart')
    Assert-True ([string]::IsNullOrWhiteSpace($completeOutput)) 'completed sessions must receive no hook output'

    $hooksConfig = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../../.codex/hooks.json') -Raw | ConvertFrom-Json
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -contains 'PreCompact') 'hook config must include PreCompact'
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -contains 'SessionStart') 'hook config must include SessionStart'
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -notcontains 'PostCompact') 'hook config must not include PostCompact'
    $timeouts = @(
        $hooksConfig.hooks.PreCompact[0].hooks[0].timeout,
        $hooksConfig.hooks.SessionStart[0].hooks[0].timeout)
    Assert-True (@($timeouts | Where-Object { $_ -ne 30 }).Count -eq 0) 'both compaction hooks must allow 30 seconds'

    Write-Output 'Orchestration capsule hook tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
