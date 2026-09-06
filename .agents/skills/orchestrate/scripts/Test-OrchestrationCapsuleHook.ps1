[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$hook = Join-Path $PSScriptRoot 'Invoke-OrchestrationCapsuleHook.ps1'
$manifestHelper = Join-Path $PSScriptRoot 'New-OrchestrationRecoveryManifest.ps1'
$recoverySnapshotHelper = Join-Path $PSScriptRoot 'Get-OrchestrationRecoverySnapshot.ps1'
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
    param(
        [Parameter(Mandatory)][string] $EventName,
        [string] $Prompt = ''
    )

    [pscustomobject] @{
        session_id = $runId
        transcript_path = $null
        cwd = $testRoot
        hook_event_name = $EventName
        model = 'gpt-5.6-sol'
        source = if ($EventName -eq 'SessionStart') { 'compact' } else { $null }
        trigger = if ($EventName -eq 'PreCompact') { 'auto' } else { $null }
        prompt = if ($EventName -eq 'UserPromptSubmit') { $Prompt } else { $null }
    } | ConvertTo-Json -Compress
}

function New-ValidCapsule {
    param([string] $Status = 'active')

    [ordered] @{
        schema_version = 2
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
            instruction_manifest = [ordered] @{
                path = ".tmp/orchestration/$runId/instruction-manifest.json"
                sha256 = (Get-FileHash -LiteralPath (Join-Path $runDirectory 'instruction-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            hook_manifest = [ordered] @{
                path = ".tmp/orchestration/$runId/hook-manifest.json"
                sha256 = (Get-FileHash -LiteralPath (Join-Path $runDirectory 'hook-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            active_contract_manifest = [ordered] @{
                path = ".tmp/orchestration/$runId/active-contract-manifest.json"
                sha256 = (Get-FileHash -LiteralPath (Join-Path $runDirectory 'active-contract-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            deferred_nodes = @()
        }
        hook_trust = [ordered] @{
            schema_version = 1
            repository = 'https://github.com/ehonda/KicktippAi.git'
            session_id = $runId
            hook_definition_sha256 = (Get-FileHash -LiteralPath (Join-Path $testRoot '.codex/hooks.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            script_sha256 = (Get-FileHash -LiteralPath $hook -Algorithm SHA256).Hash.ToLowerInvariant()
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
        resource_state = [ordered] @{
            worktree_admission = 'allowed'
            heavy_admission = 'allowed'
            disk_warning_band = 'normal'
            memory_warning_band = 'warning'
            owner_override = $null
            worktree_reservations = @([ordered] @{
                path = '.tmp/worktrees/test'
                class = 'active-build-capable'
                growth_reservation_gib = 1.25
                owner = '/root/writer'
            })
        }
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
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.codex') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $testRoot '.agents/skills/orchestrate/scripts') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'AGENTS.md') -Value "# Test instructions`n`n@AUTO-REVIEW.md" -Encoding utf8
    Set-Content -LiteralPath (Join-Path $testRoot 'AUTO-REVIEW.md') -Value '# Included review policy' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $testRoot '.agents/skills/orchestrate/SKILL.md') -Value '# Test skill' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $testRoot '.codex/hooks.json') -Value '{"hooks":{}}' -Encoding utf8
    Copy-Item -LiteralPath $hook -Destination (Join-Path $testRoot '.agents/skills/orchestrate/scripts/Invoke-OrchestrationCapsuleHook.ps1')
    Copy-Item -LiteralPath $recoverySnapshotHelper -Destination (Join-Path $testRoot '.agents/skills/orchestrate/scripts/Get-OrchestrationRecoverySnapshot.ps1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Get-OrchestrationResourceSnapshot.ps1') -Destination (Join-Path $testRoot '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1')
    & git -C $testRoot init --quiet
    & git -C $testRoot remote add origin 'https://github.com/ehonda/KicktippAi.git'

    $ordinaryPromptOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'UserPromptSubmit' 'Please inspect this repository.')
    Assert-True ([string]::IsNullOrWhiteSpace($ordinaryPromptOutput)) 'ordinary prompts must receive no trust marker'

    & git -C $testRoot remote set-url origin 'https://github.com/example/other.git'
    $wrongRepositoryOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'UserPromptSubmit' 'Use $orchestrate for P1.')
    Assert-True ([string]::IsNullOrWhiteSpace($wrongRepositoryOutput)) 'the trust marker must fail closed for a non-canonical repository'
    & git -C $testRoot remote set-url origin 'https://github.com/ehonda/KicktippAi.git'

    $trustOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'UserPromptSubmit' 'Use $orchestrate for P1.') | ConvertFrom-Json
    $trustContext = $trustOutput.hookSpecificOutput.additionalContext
    Assert-True ($trustContext -match 'ORCHESTRATION HOOK TRUST EVIDENCE') 'an explicit invocation must emit positive trust evidence'
    Assert-True ($trustContext -match 'does not activate orchestration') 'trust evidence must state that it does not activate orchestration'

    $plainOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart')
    Assert-True ([string]::IsNullOrWhiteSpace($plainOutput)) 'plain sessions must receive no hook output'

    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $runDirectory 'active') -Value 'kicktippai.orchestrate/v2' -NoNewline

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

    New-Item -ItemType Directory -Path (Join-Path $testRoot 'nested') | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'nested/AGENTS.md') -Value '# Nested instructions' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $testRoot 'nested/contract.md') -Value '# Active contract' -Encoding utf8
    $previewContent = @'
# Current frozen graph

<!-- orchestration-instruction-inputs:start -->
<!-- orchestration-instruction-inputs:end -->

<!-- orchestration-active-contract:start -->
nested/contract.md
<!-- orchestration-active-contract:end -->
'@
    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value $previewContent -Encoding utf8
    & $manifestHelper -RunId $runId -Packet instruction -RepositoryRoot $testRoot -Path @(
        'AGENTS.md', '.agents/skills/orchestrate/SKILL.md') | Out-Null
    & $manifestHelper -RunId $runId -Packet hook -RepositoryRoot $testRoot -Path @(
        '.codex/hooks.json', '.agents/skills/orchestrate/scripts/Invoke-OrchestrationCapsuleHook.ps1') | Out-Null
    & $manifestHelper -RunId $runId -Packet active-contract -RepositoryRoot $testRoot -Path @(
        ".tmp/orchestration/$runId/preview.md") | Out-Null

    $instructionManifestPath = Join-Path $runDirectory 'instruction-manifest.json'
    $instructionManifest = Get-Content -LiteralPath $instructionManifestPath -Raw | ConvertFrom-Json
    Assert-True (@($instructionManifest.entries.path) -ccontains 'AUTO-REVIEW.md') 'instruction manifests must expand transitive includes'
    Assert-True (@($instructionManifest.entries.path) -ccontains 'nested/AGENTS.md') 'instruction manifests must discover applicable nested instructions'
    $hookManifest = Get-Content -LiteralPath (Join-Path $runDirectory 'hook-manifest.json') -Raw | ConvertFrom-Json
    Assert-True (@($hookManifest.entries.path) -ccontains '.agents/skills/orchestrate/scripts/Get-OrchestrationRecoverySnapshot.ps1') 'hook manifests must include the hot recovery helper'
    Assert-True (@($hookManifest.entries.path) -ccontains '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1') 'hook manifests must include the recovery resource dependency'
    $activeManifest = Get-Content -LiteralPath (Join-Path $runDirectory 'active-contract-manifest.json') -Raw | ConvertFrom-Json
    Assert-True (@($activeManifest.entries.path) -ccontains 'nested/contract.md') 'active manifests must include preview-declared contracts'

    $completeInstructionManifest = Get-Content -LiteralPath $instructionManifestPath -Raw
    $instructionManifest.entries = @($instructionManifest.entries | Where-Object { [string] $_.path -ne 'AUTO-REVIEW.md' })
    $instructionManifest | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath $instructionManifestPath -Encoding utf8
    New-ValidCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    $incompleteInstructionRejected = $false
    try {
        & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    }
    catch {
        $incompleteInstructionRejected = $_.Exception.Message -match 'incomplete-manifest'
    }
    Assert-True $incompleteInstructionRejected 'sealing must reject a manifest that omits a transitive instruction include'
    [System.IO.File]::WriteAllText($instructionManifestPath, $completeInstructionManifest, [System.Text.UTF8Encoding]::new($false))

    New-ValidCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    Assert-True (Test-Path -LiteralPath (Join-Path $runDirectory 'capsule.sha256')) 'seal mode must create the checksum'

    $validOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    $validContext = $validOutput.hookSpecificOutput.additionalContext
    Assert-True ($validContext -match 'HOT \$orchestrate RECOVERY') 'valid packet recovery must select the hot path'
    Assert-True ($validContext -match 'VALIDATED ORCHESTRATION CAPSULE') 'valid recovery must inject the capsule'
    Assert-True ($validContext -match 'Inspect live state') 'valid recovery must retain the next root action'
    Assert-True ($validContext -match 'Do not reread unchanged policies') 'hot recovery must prohibit broad unchanged rereads'
    $recoverySnapshot = & $recoverySnapshotHelper -RunId $runId -RepositoryRoot $testRoot -AsJson | ConvertFrom-Json
    Assert-True ($recoverySnapshot.recovery.mode -eq 'hot') 'the compact recovery helper must preserve packet validation'
    Assert-True ($recoverySnapshot.worktrees.outstanding_growth_reservation_gib -eq 1.25) 'the compact recovery helper must reconcile capsule worktree reservations'
    $validPreCompactOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'PreCompact')
    Assert-True ([string]::IsNullOrWhiteSpace($validPreCompactOutput)) 'valid pre-compaction checks must stay silent'

    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value '# Drifted graph' -Encoding utf8
    $driftOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($driftOutput.hookSpecificOutput.additionalContext -match 'COLD \$orchestrate RECOVERY') 'packet drift must select the cold path'
    Assert-True ($driftOutput.hookSpecificOutput.additionalContext -match 'packet-digest-mismatch') 'changed packet material must be identified'
    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value $previewContent -Encoding utf8

    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value ($previewContent + "`n" + ('w' * 9000)) -NoNewline
    & $manifestHelper -RunId $runId -Packet active-contract -RepositoryRoot $testRoot -Path @(
        ".tmp/orchestration/$runId/preview.md") | Out-Null
    New-ValidCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    $warningValidation = & $hook -Mode Validate -RunId $runId -RepositoryRoot $testRoot | ConvertFrom-Json
    Assert-True $warningValidation.valid 'a preview between 8 and 12 KiB must remain valid'
    Assert-True ($warningValidation.message -match '8192-byte target') 'a preview above 8 KiB must report the size warning'

    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value ($previewContent + "`n" + ('x' * 12289)) -NoNewline
    $largePreviewOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($largePreviewOutput.hookSpecificOutput.additionalContext -match 'oversized-preview') 'a preview above 12 KiB must force cold recovery'
    Set-Content -LiteralPath (Join-Path $runDirectory 'preview.md') -Value $previewContent -Encoding utf8
    & $manifestHelper -RunId $runId -Packet active-contract -RepositoryRoot $testRoot -Path @(
        ".tmp/orchestration/$runId/preview.md") | Out-Null

    $ownerGateCapsule = New-ValidCapsule -Status awaiting-owner
    $ownerGateCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    $ownerGateOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($ownerGateOutput.hookSpecificOutput.additionalContext -match 'owner-gate') 'an unresolved owner gate must force cold recovery'

    $activeOwnerGateCapsule = New-ValidCapsule
    $activeOwnerGateCapsule.owner_gates = @('Owner must select the production route.')
    $activeOwnerGateCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    $activeOwnerGateOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($activeOwnerGateOutput.hookSpecificOutput.additionalContext -match 'owner-gate') 'nonempty owner_gates must force cold recovery even when lifecycle status is active'

    $ownerBlockerCapsule = New-ValidCapsule
    $ownerBlockerCapsule.blockers = @([ordered] @{ category = 'owner'; detail = 'Owner confirmation remains open.' })
    $ownerBlockerCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    $ownerBlockerOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($ownerBlockerOutput.hookSpecificOutput.additionalContext -match 'owner-gate') 'an owner blocker must force cold recovery even when lifecycle status is active'

    $invalidTargetCapsule = New-ValidCapsule
    $invalidTargetCapsule.git.push_url = 'https://github.com/example/other.git'
    $invalidTargetCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    $invalidTargetOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($invalidTargetOutput.hookSpecificOutput.additionalContext -match 'invalid-git-target') 'wrong Git targets must be forwarded'

    $invalidPreviewCapsule = New-ValidCapsule
    $invalidPreviewCapsule.freeze.preview_path = '.tmp/orchestration/other/preview.md'
    $invalidPreviewCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    $invalidPreviewOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($invalidPreviewOutput.hookSpecificOutput.additionalContext -match 'invalid-preview-path') 'wrong-run preview paths must be forwarded'

    New-ValidCapsule | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    Add-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Value ' '
    $hashOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart') | ConvertFrom-Json
    Assert-True ($hashOutput.hookSpecificOutput.additionalContext -match 'checksum-mismatch') 'checksum mismatch must be forwarded'

    New-ValidCapsule -Status complete | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'capsule.json') -Encoding utf8
    & $hook -Mode Seal -RunId $runId -RepositoryRoot $testRoot | Out-Null
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $runDirectory 'active'))) 'terminal capsules must clear the activation marker'
    $completeOutput = & $hook -RepositoryRoot $testRoot -InputJson (New-HookInput 'SessionStart')
    Assert-True ([string]::IsNullOrWhiteSpace($completeOutput)) 'completed sessions must receive no hook output'

    $hooksConfig = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../../.codex/hooks.json') -Raw | ConvertFrom-Json
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -contains 'UserPromptSubmit') 'hook config must include UserPromptSubmit trust evidence'
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -contains 'PreCompact') 'hook config must include PreCompact'
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -contains 'SessionStart') 'hook config must include SessionStart'
    Assert-True ($hooksConfig.hooks.PSObject.Properties.Name -notcontains 'PostCompact') 'hook config must not include PostCompact'
    $timeouts = @(
        $hooksConfig.hooks.UserPromptSubmit[0].hooks[0].timeout,
        $hooksConfig.hooks.PreCompact[0].hooks[0].timeout,
        $hooksConfig.hooks.SessionStart[0].hooks[0].timeout)
    Assert-True (@($timeouts | Where-Object { $_ -ne 30 }).Count -eq 0) 'all orchestration hooks must allow 30 seconds'
    Assert-True ($hooksConfig.hooks.UserPromptSubmit[0].PSObject.Properties.Name -notcontains 'matcher') 'UserPromptSubmit filtering must occur inside the script'
    Assert-True ($hooksConfig.hooks.UserPromptSubmit[0].hooks[0].additionalContextLimit -eq 0) 'trust evidence must not spill context'
    Assert-True ($hooksConfig.hooks.SessionStart[0].hooks[0].additionalContextLimit -eq 0) 'the strict byte cap must prevent context spilling'
    Assert-True ($hooksConfig.hooks.PreCompact[0].hooks[0].PSObject.Properties.Name -notcontains 'statusMessage') 'plain sessions must not display PreCompact status text'
    Assert-True ($hooksConfig.hooks.SessionStart[0].hooks[0].PSObject.Properties.Name -notcontains 'statusMessage') 'plain sessions must not display SessionStart status text'

    Write-Output 'Orchestration capsule hook tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
