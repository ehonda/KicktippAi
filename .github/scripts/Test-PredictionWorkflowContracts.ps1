[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch] $SkipHostileShellSimulation,
    [switch] $AllowMissingArenaLunaTriad
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-WithValue {
    param(
        [string] $Content,
        [string] $Name,
        [string] $FileName,
        [bool] $Required = $true
    )

    $withBlock = [regex]::Match(
        $Content,
        '(?ms)^    with:\r?\n(?<body>.*?)(?=^    \S)')
    Assert-True $withBlock.Success "$FileName does not contain a reusable-workflow with block."

    $match = [regex]::Match(
        $withBlock.Groups['body'].Value,
        "(?m)^      $([regex]::Escape($Name)):[ \t]*(?<value>[^\r\n]*)\r?$")
    if (-not $match.Success) {
        if ($Required) {
            throw "$FileName does not pass '$Name' to the reusable prediction workflow."
        }

        return $null
    }

    return $match.Groups['value'].Value.Trim().Trim('"', "'")
}

function Assert-RequiredInput {
    param(
        [string] $Content,
        [string] $Name,
        [string] $FileName
    )

    $pattern = "(?ms)^      $([regex]::Escape($Name)):\r?\n(?:(?!^      \S).)*?^        required: true\s*$"
    Assert-True ([regex]::IsMatch($Content, $pattern)) "$FileName must declare '$Name' as a required workflow_call input."
}

function Get-TriggerBlock {
    param(
        [string] $Content,
        [string] $FileName
    )

    $match = [regex]::Match($Content, '(?ms)^on:\r?\n(?<body>.*?)(?=^\S[^:\r\n]*:\s*\r?$)')
    Assert-True $match.Success "$FileName does not contain a readable top-level trigger block."
    return $match.Groups['body'].Value
}

function Get-WorkflowJobBlock {
    param(
        [string] $Content,
        [string] $JobId,
        [string] $FileName
    )

    $jobs = [regex]::Match($Content, '(?ms)^jobs:\r?\n(?<body>.*)\z')
    Assert-True $jobs.Success "$FileName does not contain a readable jobs block."

    $job = [regex]::Match(
        $jobs.Groups['body'].Value,
        "(?ms)^  $([regex]::Escape($JobId)):\r?\n(?<body>.*?)(?=^  [a-z0-9][a-z0-9-]*:\s*\r?$|\z)")
    Assert-True $job.Success "$FileName does not contain job '$JobId'."
    return $job.Groups['body'].Value
}

function Assert-ManualDispatchOnly {
    param(
        [string] $Content,
        [string] $FileName,
        [bool] $RequiresPredictionInputs
    )

    $triggerBlock = Get-TriggerBlock $Content $FileName
    $topLevelEntries = @($triggerBlock -split '\r?\n' |
        Where-Object { $_ -match '^  \S' -and $_ -notmatch '^  #' })
    $hasExactManualEntry = $topLevelEntries.Count -eq 1 -and
        $topLevelEntries[0] -match '^  workflow_dispatch:\s*$'
    Assert-True $hasExactManualEntry "$FileName trigger block must contain exactly the unquoted workflow_dispatch entry and no other top-level entry; found: $($topLevelEntries -join ' | ')."

    if (-not $RequiresPredictionInputs) {
        Assert-True (-not [regex]::IsMatch($triggerBlock, '(?m)^    inputs:\s*$')) "$FileName context dispatch must not expose prediction inputs."
        return
    }

    foreach ($inputContract in @(
        @{ Name = 'force_prediction'; Default = 'false'; Type = 'boolean' },
        @{ Name = 'max_repredictions'; Default = '2'; Type = 'number' }
    )) {
        $pattern = "(?ms)^      $([regex]::Escape($inputContract.Name)):\r?\n(?:(?!^      \S).)*?^        default: $([regex]::Escape($inputContract.Default))\s*\r?\n(?:(?!^      \S).)*?^        type: $([regex]::Escape($inputContract.Type))\s*$"
        Assert-True ([regex]::IsMatch($triggerBlock, $pattern)) "$FileName must expose $($inputContract.Name) with default $($inputContract.Default) and type $($inputContract.Type)."
    }
}

function Assert-ManualDispatchAndExactProductionSchedule {
    param(
        [string] $Content,
        [string] $FileName
    )

    $triggerBlock = Get-TriggerBlock $Content $FileName
    $actualLines = @($triggerBlock -split '\r?\n' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $expectedLines = @(
        '  workflow_dispatch:',
        '  schedule:',
        '    - cron: "7 2,9 * * *"'
    )
    Assert-True (($actualLines -join "`n") -ceq ($expectedLines -join "`n")) "$FileName must expose exactly workflow_dispatch and cron 7 2,9 * * * in that order."
    Assert-True (-not [regex]::IsMatch($triggerBlock, '(?m)^    inputs:\s*$')) "$FileName dispatch must not expose runtime inputs."
}

function Assert-AdditionalManualTriggersRejected {
    foreach ($mutation in @(
        @{ Name = 'bare-push'; Entry = '  push:' },
        @{ Name = 'single-quoted-push'; Entry = "  'push':" },
        @{ Name = 'double-quoted-pull-request'; Entry = '  "pull_request":' },
        @{ Name = 'spaced-repository-dispatch'; Entry = '  repository_dispatch :' },
        @{ Name = 'quoted-spaced-push'; Entry = "  'push' :" }
    )) {
        $syntheticContent = @"
on:
  workflow_dispatch:
$($mutation.Entry)
jobs:
  test:
    runs-on: ubuntu-latest
"@
        $rejectionMessage = $null
        try {
            Assert-ManualDispatchOnly $syntheticContent "synthetic-$($mutation.Name).yml" $false
        }
        catch {
            $rejectionMessage = $_.Exception.Message
        }

        Assert-True ($null -ne $rejectionMessage -and $rejectionMessage.Contains($mutation.Entry, [StringComparison]::Ordinal)) "Manual-only trigger validation must identify and reject the $($mutation.Name) top-level entry."
    }
}

function Assert-SecretMapping {
    param(
        [string] $Content,
        [string] $InputName,
        [string] $SecretName,
        [string] $FileName
    )

    $pattern = "(?m)^      $([regex]::Escape($InputName)):\s*\$\{\{\s*secrets\.$([regex]::Escape($SecretName))\s*\}\}\s*$"
    Assert-True ([regex]::IsMatch($Content, $pattern)) "$FileName must map $InputName from secrets.$SecretName."
}

function Assert-ExactSecretMappings {
    param(
        [string] $Content,
        [string[]] $ExpectedMappings,
        [string] $FileName
    )

    $actualMappings = @([regex]::Matches(
        $Content,
        '(?m)^      (?<input>[a-z0-9_]+):\s*\$\{\{\s*secrets\.(?<secret>[A-Z0-9_]+)\s*\}\}\s*$') |
        ForEach-Object { "$($_.Groups['input'].Value)=$($_.Groups['secret'].Value)" })
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $actual = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($mapping in $ExpectedMappings) {
        $null = $expected.Add($mapping)
    }
    foreach ($mapping in $actualMappings) {
        $null = $actual.Add($mapping)
    }
    Assert-True ($expected.SetEquals($actual)) "$FileName secret mappings differ. Expected $($ExpectedMappings -join ', '); got $($actualMappings -join ', ')."
}

function Assert-ExactReusableMappingBlock {
    param(
        [string] $Content,
        [string] $BlockName,
        [string[]] $ExpectedMappings,
        [string] $FileName
    )

    $block = [regex]::Match(
        $Content,
        "(?ms)^    $([regex]::Escape($BlockName)):\r?\n(?<body>.*?)(?=^    \S|\z)")
    Assert-True $block.Success "$FileName does not contain a reusable-workflow $BlockName block."

    $actualMappings = @([regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^      (?<key>[a-z0-9_]+):[ \t]*(?<value>[^\r\n]*)\r?$') |
        ForEach-Object { "$($_.Groups['key'].Value)=$($_.Groups['value'].Value.Trim())" })
    $nonEmptyBlockLines = @($block.Groups['body'].Value -split '\r?\n' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-True ($nonEmptyBlockLines.Count -eq $actualMappings.Count) "$FileName $BlockName block must contain only exact unquoted mapping keys with scalar values."
    Assert-True ($actualMappings.Count -eq $ExpectedMappings.Count) "$FileName $BlockName mapping count differs. Expected $($ExpectedMappings.Count); got $($actualMappings.Count)."
    for ($mappingIndex = 0; $mappingIndex -lt $ExpectedMappings.Count; $mappingIndex++) {
        Assert-True ($actualMappings[$mappingIndex] -ceq $ExpectedMappings[$mappingIndex]) "$FileName $BlockName mapping at index $mappingIndex differs. Expected '$($ExpectedMappings[$mappingIndex])'; got '$($actualMappings[$mappingIndex])'."
    }
}

function Assert-LaunchRosterOverlayBaseContract {
    param([string] $WorkflowDirectory)

    $fileName = 'base-context-collection.yml'
    $path = Join-Path $WorkflowDirectory $fileName
    Assert-True (Test-Path -LiteralPath $path) "$fileName must exist."
    $content = Get-Content -Raw -LiteralPath $path

    $inputPattern = '(?ms)^      publish_launch_roster_overlay:\r?\n        description:.*\r?\n        required: false\r?\n        default: false\r?\n        type: boolean\s*$'
    Assert-True ([regex]::IsMatch($content, $inputPattern)) "$fileName must expose an optional false-by-default boolean launch-overlay input."
    foreach ($expected in @(
        'if: ${{ inputs.publish_launch_roster_overlay }}',
        'https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb',
        'collect-context rosters',
        '--duckdb-path "$RUNNER_TEMP/transfermarkt-datasets.duckdb"',
        '--duckdb-revision "154367dfa6d6eb0b86332e332f9df0a080c7ddce"',
        '--duckdb-snapshot-date "2026-08-13"',
        '--duckdb-sha256 "808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c"',
        '--require-launch-coverage',
        '--launch-enrichment-overlay',
        'collect-context profile'
    )) {
        Assert-True $content.Contains($expected, [StringComparison]::Ordinal) "$fileName must contain the exact launch-overlay contract token '$expected'."
    }

    $rosterIndex = $content.IndexOf('collect-context rosters', [StringComparison]::Ordinal)
    $profileIndex = $content.IndexOf('collect-context profile', [StringComparison]::Ordinal)
    Assert-True ($rosterIndex -ge 0 -and $profileIndex -gt $rosterIndex) "$fileName must publish the pinned roster overlay before normal profile collection."
}

function Assert-CommunityRulesSource {
    param(
        [string] $WorkflowDirectory,
        [string] $CommunityContext,
        [string] $ContractName
    )

    $repositoryRoot = Split-Path (Split-Path $WorkflowDirectory -Parent) -Parent
    $rulesPath = Join-Path $repositoryRoot "community-rules\$CommunityContext.md"
    Assert-True (Test-Path -LiteralPath $rulesPath -PathType Leaf) "$ContractName requires tracked community rules source community-rules/$CommunityContext.md."
}

function Assert-ProductionContextEntrypoints {
    param([string] $WorkflowDirectory)

    foreach ($caller in @(
        @{
            FileName = 'pes-squad-context-collection.yml'
            CommunityContext = 'pes-squad'
            KicktippUsername = 'PES_SQUAD_KICKTIPP_USERNAME'
            KicktippPassword = 'PES_SQUAD_KICKTIPP_PASSWORD'
        },
        @{
            FileName = 'schadensfresse-context-collection.yml'
            CommunityContext = 'schadensfresse'
            KicktippUsername = 'SCHADENSFRESSE_KICKTIPP_USERNAME'
            KicktippPassword = 'SCHADENSFRESSE_KICKTIPP_PASSWORD'
        },
        @{
            FileName = 'relaxdays-tippt-context-collection.yml'
            CommunityContext = 'relaxdays-tippt'
            KicktippUsername = 'RELAXDAYS_TIPPT_KICKTIPP_USERNAME'
            KicktippPassword = 'RELAXDAYS_TIPPT_KICKTIPP_PASSWORD'
        }
    )) {
        Assert-CommunityRulesSource $WorkflowDirectory $caller.CommunityContext $caller.FileName
        $path = Join-Path $WorkflowDirectory $caller.FileName
        Assert-True (Test-Path -LiteralPath $path) "$($caller.FileName) must exist."
        $content = Get-Content -Raw -LiteralPath $path

        Assert-ManualDispatchOnly $content $caller.FileName $false
        Assert-True (-not [regex]::IsMatch((Get-TriggerBlock $content $caller.FileName), '(?m)^  (?:schedule|workflow_call):')) "$($caller.FileName) must not expose schedule or workflow_call."
        Assert-True $content.StartsWith("name: $($caller.CommunityContext) ⚽ Context Collection", [StringComparison]::Ordinal) "$($caller.FileName) must retain the exact community context display name."

        $topLevelKeys = @([regex]::Matches($content, '(?m)^(?<key>\S[^:\r\n]*):\s*') |
            ForEach-Object { $_.Groups['key'].Value.Trim() })
        Assert-True (($topLevelKeys -join ',') -ceq 'name,on,concurrency,jobs') "$($caller.FileName) must contain exactly the name, on, concurrency, and jobs top-level keys in order; got $($topLevelKeys -join ', ')."

        $jobKeys = @([regex]::Matches($content, '(?m)^  (?<key>\S[^:\r\n]*):\s*') |
            ForEach-Object { $_.Groups['key'].Value.Trim() })
        Assert-True (($jobKeys -join ',') -ceq 'workflow_dispatch,group,cancel-in-progress,call-base-workflow') "$($caller.FileName) must expose only workflow_dispatch, the exact concurrency fields, and the call-base-workflow job at two-space indentation; got $($jobKeys -join ', ')."

        $jobPropertyKeys = @([regex]::Matches($content, '(?m)^    (?<key>\S[^:\r\n]*):\s*') |
            ForEach-Object { $_.Groups['key'].Value.Trim() })
        Assert-True (($jobPropertyKeys -join ',') -ceq 'name,uses,with,secrets') "$($caller.FileName) call-base-workflow job keys must be exactly name, uses, with, and secrets; got $($jobPropertyKeys -join ', ')."

        Assert-True ([regex]::IsMatch($content, "(?m)^    name: Context Collection - $([regex]::Escape($caller.CommunityContext))\r?$")) "$($caller.FileName) must retain the exact reusable job display name."
        Assert-True ([regex]::IsMatch($content, '(?m)^    uses: \./\.github/workflows/base-context-collection\.yml\r?$')) "$($caller.FileName) must call exactly the reusable context workflow."
        Assert-ExactReusableMappingBlock $content 'with' @(
            "community_context=`"$($caller.CommunityContext)`"",
            'competition="bundesliga-2026-27"',
            'trigger_type="manual"',
            'publish_launch_roster_overlay=true'
        ) $caller.FileName
        Assert-ExactReusableMappingBlock $content 'secrets' @(
            ('kicktipp_username=${{ secrets.' + $caller.KicktippUsername + ' }}'),
            ('kicktipp_password=${{ secrets.' + $caller.KicktippPassword + ' }}'),
            'firebase_project_id=${{ secrets.FIREBASE_PROJECT_ID }}',
            'firebase_service_account_json=${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}'
        ) $caller.FileName
        Assert-ExactSecretMappings $content @(
            "kicktipp_username=$($caller.KicktippUsername)",
            "kicktipp_password=$($caller.KicktippPassword)",
            'firebase_project_id=FIREBASE_PROJECT_ID',
            'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON'
        ) $caller.FileName
    }
}

function Assert-ArenaLunaTriad {
    param(
        [string] $WorkflowDirectory,
        [switch] $AllowMissing
    )

    $contextFileName = 'buli2627-ehonda-ai-arena-context-collection.yml'
    $matchFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml'
    $bonusFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml'
    $fileNames = @($contextFileName, $matchFileName, $bonusFileName)
    $paths = @($fileNames | ForEach-Object { Join-Path $WorkflowDirectory $_ })
    $presentPaths = @($paths | Where-Object { Test-Path -LiteralPath $_ })

    if ($presentPaths.Count -eq 0 -and $AllowMissing) {
        Write-Verbose 'Arena Luna triad not present in this isolated support lane; integrated validation remains required.'
        return $false
    }

    $missingFileNames = @($fileNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $WorkflowDirectory $_)) })
    Assert-True ($missingFileNames.Count -eq 0) "The arena Luna triad is incomplete. Missing: $($missingFileNames -join ', ')."

    $context = Get-Content -Raw -LiteralPath (Join-Path $WorkflowDirectory $contextFileName)
    $match = Get-Content -Raw -LiteralPath (Join-Path $WorkflowDirectory $matchFileName)
    $bonus = Get-Content -Raw -LiteralPath (Join-Path $WorkflowDirectory $bonusFileName)

    Assert-ManualDispatchOnly $context $contextFileName $false
    Assert-ManualDispatchOnly $match $matchFileName $true
    Assert-ManualDispatchOnly $bonus $bonusFileName $true

    Assert-True $context.Contains('uses: ./.github/workflows/base-context-collection.yml', [StringComparison]::Ordinal) "$contextFileName must call the reusable context workflow."
    foreach ($expected in @(
        @{ Name = 'community_context'; Value = 'ehonda-ai-arena' },
        @{ Name = 'competition'; Value = 'bundesliga-2026-27' },
        @{ Name = 'trigger_type'; Value = 'manual' }
    )) {
        Assert-True ((Get-WithValue $context $expected.Name $contextFileName) -eq $expected.Value) "$contextFileName must pass $($expected.Name)=$($expected.Value)."
    }

    foreach ($prediction in @(
        @{ FileName = $matchFileName; Content = $match; Base = 'base-matchday-predictions.yml'; PromptName = 'kicktippai/bundesliga-2026-27/predict-one-match'; PromptVersion = '3' },
        @{ FileName = $bonusFileName; Content = $bonus; Base = 'base-bonus-predictions.yml'; PromptName = 'kicktippai/bundesliga-2026-27/predict-bonus'; PromptVersion = '1' }
    )) {
        Assert-True $prediction.Content.Contains("uses: ./.github/workflows/$($prediction.Base)", [StringComparison]::Ordinal) "$($prediction.FileName) must call $($prediction.Base)."
        foreach ($expected in @(
            @{ Name = 'community'; Value = 'ehonda-ai-arena' },
            @{ Name = 'community_context'; Value = 'ehonda-ai-arena' },
            @{ Name = 'competition'; Value = 'bundesliga-2026-27' },
            @{ Name = 'model'; Value = 'gpt-5.6-luna' },
            @{ Name = 'reasoning_effort'; Value = 'none' },
            @{ Name = 'max_output_tokens'; Value = '10000' },
            @{ Name = 'prompt_source'; Value = 'langfuse' },
            @{ Name = 'langfuse_prompt_name'; Value = $prediction.PromptName },
            @{ Name = 'langfuse_prompt_label'; Value = 'production' },
            @{ Name = 'langfuse_prompt_version'; Value = $prediction.PromptVersion },
            @{ Name = 'trigger_type'; Value = 'manual' }
        )) {
            Assert-True ((Get-WithValue $prediction.Content $expected.Name $prediction.FileName) -eq $expected.Value) "$($prediction.FileName) must pass $($expected.Name)=$($expected.Value)."
        }
        Assert-True ((Get-WithValue $prediction.Content 'force_prediction' $prediction.FileName) -eq '${{ inputs.force_prediction }}') "$($prediction.FileName) must pass through the typed force_prediction input."
        $maxRepredictions = Get-WithValue $prediction.Content 'max_repredictions' $prediction.FileName
        Assert-True ($maxRepredictions -eq '${{ fromJSON(inputs.max_repredictions) }}') "$($prediction.FileName) must convert the dispatch max_repredictions string to the reusable workflow's number input."
        Assert-True ($maxRepredictions -ne '${{ inputs.max_repredictions }}') "$($prediction.FileName) must reject raw string passthrough for the numeric max_repredictions input."
        Assert-True (-not $maxRepredictions.Contains('||', [StringComparison]::Ordinal)) "$($prediction.FileName) must not replace the valid zero max_repredictions value with a fallback."
    }

    Assert-True ((Get-WithValue $bonus 'bonus_context_document_budget' $bonusFileName) -eq '20') "$bonusFileName must pin the accepted 20-document bonus budget."
    Assert-True ((Get-WithValue $bonus 'bonus_context_token_budget' $bonusFileName) -eq '32000') "$bonusFileName must pin the accepted 32,000-token bonus budget."

    foreach ($workflow in @(
        @{ FileName = $contextFileName; Content = $context; Prediction = $false },
        @{ FileName = $matchFileName; Content = $match; Prediction = $true },
        @{ FileName = $bonusFileName; Content = $bonus; Prediction = $true }
    )) {
        Assert-SecretMapping $workflow.Content 'kicktipp_username' 'EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME' $workflow.FileName
        Assert-SecretMapping $workflow.Content 'kicktipp_password' 'EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD' $workflow.FileName
        Assert-SecretMapping $workflow.Content 'firebase_project_id' 'FIREBASE_PROJECT_ID' $workflow.FileName
        Assert-SecretMapping $workflow.Content 'firebase_service_account_json' 'FIREBASE_SERVICE_ACCOUNT_JSON' $workflow.FileName
        if ($workflow.Prediction) {
            Assert-SecretMapping $workflow.Content 'openai_api_key' 'OPENAI_API_KEY' $workflow.FileName
            Assert-SecretMapping $workflow.Content 'langfuse_secret_key' 'LANGFUSE_SECRET_KEY' $workflow.FileName
            Assert-ExactSecretMappings $workflow.Content @(
                'kicktipp_username=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME',
                'kicktipp_password=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD',
                'firebase_project_id=FIREBASE_PROJECT_ID',
                'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON',
                'openai_api_key=OPENAI_API_KEY',
                'langfuse_secret_key=LANGFUSE_SECRET_KEY'
            ) $workflow.FileName
        }
        else {
            Assert-ExactSecretMappings $workflow.Content @(
                'kicktipp_username=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME',
                'kicktipp_password=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD',
                'firebase_project_id=FIREBASE_PROJECT_ID',
                'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON'
            ) $workflow.FileName
        }
    }

    return $true
}

function Assert-ArenaParticipantContextEntrypoints {
    param([string] $WorkflowDirectory)

    Assert-CommunityRulesSource $WorkflowDirectory 'ehonda-ai-arena' 'Bundesliga arena context entrypoints'

    foreach ($caller in @(
        @{
            FileName = 'buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh-context-collection.yml'
            Participant = 'Sol xhigh'
            SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH'
        },
        @{
            FileName = 'buli2627-ehonda-ai-arena-gpt-5-6-sol-high-context-collection.yml'
            Participant = 'Sol high'
            SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH'
        },
        @{
            FileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-context-collection.yml'
            Participant = 'Luna medium'
            SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM'
        },
        @{
            FileName = 'buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-context-collection.yml'
            Participant = 'Terra xhigh'
            SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH'
        }
    )) {
        $path = Join-Path $WorkflowDirectory $caller.FileName
        Assert-True (Test-Path -LiteralPath $path) "$($caller.FileName) must exist."
        $content = Get-Content -Raw -LiteralPath $path

        Assert-ManualDispatchOnly $content $caller.FileName $false
        Assert-NoYamlScheduleKey $content $caller.FileName
        Assert-True $content.Contains('uses: ./.github/workflows/base-context-collection.yml', [StringComparison]::Ordinal) "$($caller.FileName) must call the reusable context workflow."
        foreach ($expected in @(
            @{ Name = 'community_context'; Value = 'ehonda-ai-arena' },
            @{ Name = 'competition'; Value = 'bundesliga-2026-27' },
            @{ Name = 'trigger_type'; Value = 'manual' }
        )) {
            Assert-True ((Get-WithValue $content $expected.Name $caller.FileName) -eq $expected.Value) "$($caller.FileName) must pass $($expected.Name)=$($expected.Value)."
        }

        Assert-ExactSecretMappings $content @(
            "kicktipp_username=$($caller.SecretStem)_KICKTIPP_USERNAME",
            "kicktipp_password=$($caller.SecretStem)_KICKTIPP_PASSWORD",
            'firebase_project_id=FIREBASE_PROJECT_ID',
            'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON'
        ) $caller.FileName
    }
}

function Test-IsWorkflowYamlFileName {
    param([string] $FileName)

    $extension = [IO.Path]::GetExtension($FileName)
    return $extension.Equals('.yml', [StringComparison]::OrdinalIgnoreCase) -or
        $extension.Equals('.yaml', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoYamlScheduleKey {
    param(
        [string] $Content,
        [string] $FileName
    )

    $scheduleKeyPattern = '(?m)(?:^|[\s{,])(?:schedule|''schedule''|"schedule")\s*:'
    Assert-True (-not [regex]::IsMatch($Content, $scheduleKeyPattern)) "$FileName must not contain a quoted or unquoted YAML schedule mapping key."
}

function Assert-ExactProductionLiveConcurrency {
    param(
        [string] $Content,
        [string] $FileName
    )

    $blocks = [regex]::Matches($Content, '(?ms)^concurrency:\r?\n(?<body>.*?)(?=^\S[^:\r\n]*:\s*\r?$)')
    Assert-True ($blocks.Count -eq 1) "$FileName must contain exactly one top-level concurrency block."
    $lines = @($blocks[0].Groups['body'].Value -split '\r?\n' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $expected = @(
        '  group: bundesliga-2026-27-production-live-lane',
        '  cancel-in-progress: false'
    )
    Assert-True (($lines -join "`n") -ceq ($expected -join "`n")) "$FileName must use only the exact non-cancelling production-live concurrency contract."
}

function Assert-BundesligaProductionCallerConcurrency {
    param(
        [string] $WorkflowDirectory,
        [string] $OuterFileName
    )

    $callerFileNames = @(
        'pes-squad-context-collection.yml',
        'schadensfresse-context-collection.yml',
        'relaxdays-tippt-context-collection.yml',
        'buli2627-ehonda-ai-arena-context-collection.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh-context-collection.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-high-context-collection.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-context-collection.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-context-collection.yml',
        'buli2627-pes-squad-gpt-5-6-sol-xhigh-matchday.yml',
        'buli2627-pes-squad-gpt-5-6-sol-xhigh-bonus.yml',
        'buli2627-schadensfresse-gpt-5-6-sol-xhigh-matchday.yml',
        'buli2627-schadensfresse-gpt-5-6-sol-xhigh-bonus.yml',
        'buli2627-relaxdays-tippt-gpt-5-6-sol-xhigh-matchday.yml',
        'buli2627-relaxdays-tippt-gpt-5-6-sol-xhigh-bonus.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh-matchday.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh-bonus.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-high-matchday.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-sol-high-bonus.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-matchday.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-bonus.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-matchday.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-bonus.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml',
        'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml'
    )
    $expectedFileNames = @($callerFileNames + $OuterFileName | Sort-Object)

    foreach ($fileName in $expectedFileNames) {
        $path = Join-Path $WorkflowDirectory $fileName
        Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "$fileName must exist for the production-live concurrency contract."
        $content = Get-Content -Raw -LiteralPath $path
        Assert-ExactProductionLiveConcurrency $content $fileName
        if ($fileName -cne $OuterFileName) {
            Assert-NoYamlScheduleKey $content $fileName
        }
    }

    $actualFileNames = @(Get-ChildItem -LiteralPath $WorkflowDirectory -File |
        Where-Object { Test-IsWorkflowYamlFileName $_.Name } |
        Where-Object {
            (Get-Content -Raw -LiteralPath $_.FullName).Contains(
                'group: bundesliga-2026-27-production-live-lane',
                [StringComparison]::Ordinal)
        } |
        ForEach-Object { $_.Name } |
        Sort-Object)
    Assert-True (($actualFileNames -join ',') -ceq ($expectedFileNames -join ',')) "The production-live concurrency group must appear only in the exact outer and current production caller set. Expected $($expectedFileNames -join ', '); got $($actualFileNames -join ', ')."
}

function Assert-ArenaScheduleScannerSelfTests {
    Assert-True (Test-IsWorkflowYamlFileName 'arena-control.yml') 'The arena schedule scanner must enumerate .yml workflows.'
    Assert-True (Test-IsWorkflowYamlFileName 'arena-control.yaml') 'The arena schedule scanner must enumerate .yaml workflows.'
    Assert-True (-not (Test-IsWorkflowYamlFileName 'arena-control.yml.bak')) 'The arena schedule scanner must ignore non-workflow extensions.'

    $scheduledCases = @(
        @{
            Name = 'two-space-block.yml'
            Content = "on:`n  schedule:`n    - cron: '0 0 * * *'"
        },
        @{
            Name = 'four-space-block.yaml'
            Content = "on:`n    schedule:`n      - cron: '0 0 * * *'"
        },
        @{
            Name = 'quoted-key.yml'
            Content = "on:`n  'schedule':`n    - cron: '0 0 * * *'"
        },
        @{
            Name = 'anchored-key.yaml'
            Content = "on:`n  schedule: &daily`n    - cron: '0 0 * * *'"
        },
        @{
            Name = 'single-line-flow.yaml'
            Content = "on: { schedule: [{ cron: '0 0 * * *' }] }"
        },
        @{
            Name = 'multiline-flow.yml'
            Content = "on: {`n  workflow_dispatch: {},`n  `"schedule`": [`n    { cron: '0 0 * * *' }`n  ]`n}"
        }
    )
    foreach ($scheduledCase in $scheduledCases) {
        $rejection = $null
        try {
            Assert-NoYamlScheduleKey $scheduledCase.Content $scheduledCase.Name
        }
        catch {
            $rejection = $_.Exception.Message
        }
        Assert-True ($null -ne $rejection) "The arena schedule scanner must reject synthetic case '$($scheduledCase.Name)'."
    }

    foreach ($manualControl in @(
        @{ Name = 'manual-control.yml'; Content = "on:`n  workflow_dispatch:" },
        @{ Name = 'manual-control.yaml'; Content = "'on': { workflow_dispatch: {} }" }
    )) {
        Assert-NoYamlScheduleKey $manualControl.Content $manualControl.Name
    }
}

function Assert-NoActiveArenaLunaSchedule {
    param(
        [string] $WorkflowDirectory,
        [string] $ProductionOuterFileName
    )

    $temporaryFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-scheduled-cycle.yml'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $WorkflowDirectory $temporaryFileName))) "$temporaryFileName must be removed after the one authorized scheduled cycle."

    $scheduledArenaFiles = [Collections.Generic.List[string]]::new()
    foreach ($workflowFile in Get-ChildItem -LiteralPath $WorkflowDirectory -File |
        Where-Object { Test-IsWorkflowYamlFileName $_.Name }) {
        if ($workflowFile.Name -ceq $ProductionOuterFileName) {
            continue
        }

        $content = Get-Content -Raw -LiteralPath $workflowFile.FullName
        $isArenaBundesligaScope =
            $workflowFile.Name.StartsWith('buli2627-ehonda-ai-arena', [StringComparison]::Ordinal) -or
            ($content.Contains('ehonda-ai-arena', [StringComparison]::Ordinal) -and
                $content.Contains('bundesliga-2026-27', [StringComparison]::Ordinal))
        if (-not $isArenaBundesligaScope) {
            continue
        }

        try {
            Assert-NoYamlScheduleKey $content $workflowFile.Name
        }
        catch {
            $scheduledArenaFiles.Add($workflowFile.Name)
        }
    }

    Assert-True ($scheduledArenaFiles.Count -eq 0) "No historical Bundesliga arena validation schedule may remain after teardown; found: $($scheduledArenaFiles -join ', ')."
}

function Assert-ProductionLiveMatchdayWorkflow {
    param(
        [string] $WorkflowDirectory,
        [string] $FileName
    )

    $path = Join-Path $WorkflowDirectory $FileName
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "$FileName must exist."
    $content = Get-Content -Raw -LiteralPath $path

    Assert-ManualDispatchAndExactProductionSchedule $content $FileName
    Assert-True (-not $content.Contains('workflow_call:', [StringComparison]::Ordinal)) "$FileName must not expose workflow_call."
    Assert-True ([regex]::Matches($content, '(?m)^concurrency:\s*$').Count -eq 1) "$FileName must contain exactly one top-level concurrency block."
    Assert-True ([regex]::Matches($content, '(?m)^  group: bundesliga-2026-27-production-live-lane\s*$').Count -eq 1) "$FileName must use the exact production-live concurrency group."
    Assert-True ([regex]::Matches($content, '(?m)^  cancel-in-progress: false\s*$').Count -eq 1) "$FileName concurrency must be non-cancelling."

    foreach ($forbidden in @(
        'always(',
        'strategy:',
        'matrix:',
        'retry',
        'bonus',
        'force_prediction: true',
        'publish_launch_roster_overlay: true',
        'timeout-minutes'
    )) {
        Assert-True (-not $content.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) "$FileName must not contain forbidden production-live token '$forbidden'."
    }

    $jobsMatch = [regex]::Match($content, '(?ms)^jobs:\r?\n(?<body>.*)\z')
    Assert-True $jobsMatch.Success "$FileName must contain a jobs block."
    $actualJobIds = @([regex]::Matches($jobsMatch.Groups['body'].Value, '(?m)^  (?<id>[a-z0-9][a-z0-9-]*):\s*\r?$') |
        ForEach-Object { $_.Groups['id'].Value })

    $triggerType = '${{ github.event_name == ''schedule'' && ''scheduled'' || ''manual'' }}'
    $jobs = @(
        @{ Id = 'pes-squad-context'; Needs = $null; Kind = 'context'; Context = 'pes-squad'; SecretStem = 'PES_SQUAD' },
        @{ Id = 'pes-squad-matchday'; Needs = 'pes-squad-context'; Kind = 'match'; Community = 'pes-squad'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'PES_SQUAD' },
        @{ Id = 'schadensfresse-context'; Needs = 'pes-squad-matchday'; Kind = 'context'; Context = 'schadensfresse'; SecretStem = 'SCHADENSFRESSE' },
        @{ Id = 'schadensfresse-matchday'; Needs = 'schadensfresse-context'; Kind = 'match'; Community = 'schadensfresse'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'SCHADENSFRESSE' },
        @{ Id = 'relaxdays-tippt-context'; Needs = 'schadensfresse-matchday'; Kind = 'context'; Context = 'relaxdays-tippt'; SecretStem = 'RELAXDAYS_TIPPT' },
        @{ Id = 'relaxdays-tippt-matchday'; Needs = 'relaxdays-tippt-context'; Kind = 'match'; Community = 'relaxdays-tippt'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'RELAXDAYS_TIPPT' },
        @{ Id = 'arena-sol-xhigh-context'; Needs = 'relaxdays-tippt-matchday'; Kind = 'context'; Context = 'ehonda-ai-arena'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH' },
        @{ Id = 'arena-sol-xhigh-matchday'; Needs = 'arena-sol-xhigh-context'; Kind = 'match'; Community = 'ehonda-ai-arena'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH' },
        @{ Id = 'arena-sol-high-context'; Needs = 'arena-sol-xhigh-matchday'; Kind = 'context'; Context = 'ehonda-ai-arena'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH' },
        @{ Id = 'arena-sol-high-matchday'; Needs = 'arena-sol-high-context'; Kind = 'match'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-sol'; Effort = 'high'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH' },
        @{ Id = 'arena-luna-medium-context'; Needs = 'arena-sol-high-matchday'; Kind = 'context'; Context = 'ehonda-ai-arena'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM' },
        @{ Id = 'arena-luna-medium-matchday'; Needs = 'arena-luna-medium-context'; Kind = 'match'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-luna'; Effort = 'medium'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM' },
        @{ Id = 'arena-terra-xhigh-context'; Needs = 'arena-luna-medium-matchday'; Kind = 'context'; Context = 'ehonda-ai-arena'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH' },
        @{ Id = 'arena-terra-xhigh-matchday'; Needs = 'arena-terra-xhigh-context'; Kind = 'match'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-terra'; Effort = 'xhigh'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH' },
        @{ Id = 'arena-luna-none-context'; Needs = 'arena-terra-xhigh-matchday'; Kind = 'context'; Context = 'ehonda-ai-arena'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE' },
        @{ Id = 'arena-luna-none-matchday'; Needs = 'arena-luna-none-context'; Kind = 'match'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-luna'; Effort = 'none'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE' }
    )

    $expectedJobIds = @($jobs | ForEach-Object { $_.Id })
    Assert-True (($actualJobIds -join ',') -ceq ($expectedJobIds -join ',')) "$FileName job order differs. Expected $($expectedJobIds -join ', '); got $($actualJobIds -join ', ')."
    Assert-True ([regex]::Matches($content, '(?m)^    uses: \./\.github/workflows/base-context-collection\.yml\s*$').Count -eq 8) "$FileName must contain exactly eight context jobs."
    Assert-True ([regex]::Matches($content, '(?m)^    uses: \./\.github/workflows/base-matchday-predictions\.yml\s*$').Count -eq 8) "$FileName must contain exactly eight matchday jobs."

    foreach ($job in $jobs) {
        $block = Get-WorkflowJobBlock $content $job.Id $FileName
        Assert-True (-not [regex]::IsMatch($block, '(?m)^    if:')) "$FileName job $($job.Id) must use default-success dependency semantics without if."

        $propertyKeys = @([regex]::Matches($block, '(?m)^    (?<key>[a-z][a-z_-]*):\s*') |
            ForEach-Object { $_.Groups['key'].Value })
        $expectedPropertyKeys = if ($null -eq $job.Needs) {
            @('name', 'uses', 'with', 'secrets')
        }
        else {
            @('name', 'needs', 'uses', 'with', 'secrets')
        }
        Assert-True (($propertyKeys -join ',') -ceq ($expectedPropertyKeys -join ',')) "$FileName job $($job.Id) has unexpected properties: $($propertyKeys -join ', ')."

        if ($null -eq $job.Needs) {
            Assert-True (-not [regex]::IsMatch($block, '(?m)^    needs:')) "$FileName first job must not have a predecessor."
        }
        else {
            Assert-True ([regex]::IsMatch($block, "(?m)^    needs: $([regex]::Escape($job.Needs))\s*$")) "$FileName job $($job.Id) must need exactly $($job.Needs)."
        }

        if ($job.Kind -eq 'context') {
            Assert-True ([regex]::IsMatch($block, '(?m)^    uses: \./\.github/workflows/base-context-collection\.yml\s*$')) "$FileName job $($job.Id) must call the base context workflow."
            Assert-ExactReusableMappingBlock $block 'with' @(
                "community_context=`"$($job.Context)`"",
                'competition="bundesliga-2026-27"',
                "trigger_type=$triggerType",
                'publish_launch_roster_overlay=false'
            ) "$FileName/$($job.Id)"
            Assert-ExactReusableMappingBlock $block 'secrets' @(
                ('kicktipp_username=${{ secrets.' + $job.SecretStem + '_KICKTIPP_USERNAME }}'),
                ('kicktipp_password=${{ secrets.' + $job.SecretStem + '_KICKTIPP_PASSWORD }}'),
                'firebase_project_id=${{ secrets.FIREBASE_PROJECT_ID }}',
                'firebase_service_account_json=${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}'
            ) "$FileName/$($job.Id)"
        }
        else {
            Assert-True ([regex]::IsMatch($block, '(?m)^    uses: \./\.github/workflows/base-matchday-predictions\.yml\s*$')) "$FileName job $($job.Id) must call the base matchday workflow."
            Assert-ExactReusableMappingBlock $block 'with' @(
                "community=`"$($job.Community)`"",
                "community_context=`"$($job.Context)`"",
                'competition="bundesliga-2026-27"',
                "model=`"$($job.Model)`"",
                "reasoning_effort=`"$($job.Effort)`"",
                'max_output_tokens=10000',
                'prompt_source="langfuse"',
                'langfuse_prompt_name="kicktippai/bundesliga-2026-27/predict-one-match"',
                'langfuse_prompt_label="production"',
                'langfuse_prompt_version=3',
                "trigger_type=$triggerType",
                'force_prediction=false',
                'max_repredictions=2'
            ) "$FileName/$($job.Id)"
            Assert-ExactReusableMappingBlock $block 'secrets' @(
                ('kicktipp_username=${{ secrets.' + $job.SecretStem + '_KICKTIPP_USERNAME }}'),
                ('kicktipp_password=${{ secrets.' + $job.SecretStem + '_KICKTIPP_PASSWORD }}'),
                'firebase_project_id=${{ secrets.FIREBASE_PROJECT_ID }}',
                'firebase_service_account_json=${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}',
                'openai_api_key=${{ secrets.OPENAI_API_KEY }}',
                'langfuse_secret_key=${{ secrets.LANGFUSE_SECRET_KEY }}'
            ) "$FileName/$($job.Id)"
        }
    }
}

function Assert-CommandIdentity {
    param(
        [string] $Content,
        [string] $Command,
        [int] $ExpectedCount,
        [string] $FileName
    )

    $lines = [regex]::Matches(
        $Content,
        "(?m)^\s*dotnet run --project src/Orchestrator --configuration Release -- $([regex]::Escape($Command))\b.*$"
    )
    Assert-True ($lines.Count -eq $ExpectedCount) "$FileName must render exactly $ExpectedCount '$Command' command line(s)."

    foreach ($line in $lines) {
        foreach ($flag in @(
            '--competition "$COMPETITION"',
            '--reasoning-effort "$REASONING_EFFORT"',
            '--max-output-tokens "$MAX_OUTPUT_TOKENS"',
            '--prompt-source "$PROMPT_SOURCE"',
            '--langfuse-prompt-name "$LANGFUSE_PROMPT_NAME"',
            '--langfuse-prompt-version "$LANGFUSE_PROMPT_VERSION"'
        )) {
            Assert-True $line.Value.Contains($flag, [StringComparison]::Ordinal) "$FileName $Command command omits '$flag'."
        }
    }
}

function Get-WorkflowRunScripts {
    param([string] $Content)

    $lines = [regex]::Split($Content, '\r?\n')
    $scripts = [Collections.Generic.List[string]]::new()
    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $runMatch = [regex]::Match($lines[$lineIndex], '^(?<indent>\s*)run:\s*(?<value>.*)$')
        if (-not $runMatch.Success) {
            continue
        }

        $value = $runMatch.Groups['value'].Value.Trim()
        if ($value -notmatch '^[|>][-+]?$') {
            $scripts.Add($value)
            continue
        }

        $runIndent = $runMatch.Groups['indent'].Value.Length
        $body = [Collections.Generic.List[string]]::new()
        for ($bodyIndex = $lineIndex + 1; $bodyIndex -lt $lines.Count; $bodyIndex++) {
            $line = $lines[$bodyIndex]
            if ($line.Length -eq 0) {
                $body.Add($line)
                continue
            }

            $lineIndent = [regex]::Match($line, '^\s*').Value.Length
            if ($lineIndent -le $runIndent) {
                break
            }

            $body.Add($line)
        }

        $scripts.Add(($body -join "`n"))
        $lineIndex = $bodyIndex - 1
    }

    return $scripts.ToArray()
}

function Assert-HostileSummaryValueIsLiteral {
    param([switch] $Skip)

    if ($Skip) {
        return
    }

    $bashPath = $null
    if ($IsWindows) {
        $gitCommand = Get-Command git -ErrorAction SilentlyContinue
        if ($null -ne $gitCommand) {
            $gitRoot = Split-Path (Split-Path $gitCommand.Source -Parent) -Parent
            $gitBashCandidate = Join-Path $gitRoot 'bin\bash.exe'
            if (Test-Path -LiteralPath $gitBashCandidate) {
                $bashPath = $gitBashCandidate
            }
        }
    }
    if ($null -eq $bashPath) {
        $bashCommand = Get-Command bash -ErrorAction SilentlyContinue
        $bashPath = if ($null -ne $bashCommand) { $bashCommand.Source } else { $null }
    }

    Assert-True ($null -ne $bashPath) 'Bash is required for the hostile workflow-summary shell simulation. Use -SkipHostileShellSimulation only when Bash is unavailable.'

    $hostileValue = '$(printf INJECTED)"; echo BROKEN; # $HOME `backticks`' + "`n" + 'second line'
    $previousCommunity = $env:COMMUNITY
    try {
        $env:COMMUNITY = $hostileValue
        $bashScript = 'printf ''%s\n'' "- **Community**: $COMMUNITY"'
        $actual = (& $bashPath -c $bashScript 2>&1) -join "`n"
        Assert-True ($LASTEXITCODE -eq 0) "Hostile workflow-summary shell simulation failed with exit code $LASTEXITCODE."
        $expected = "- **Community**: $hostileValue"
        Assert-True ($actual -ceq $expected) "Workflow-summary environment expansion did not preserve the hostile-looking input literally. Expected $($expected | ConvertTo-Json -Compress); got $($actual | ConvertTo-Json -Compress)."
    }
    finally {
        $env:COMMUNITY = $previousCommunity
    }
}

$workflowDirectory = Join-Path $RepositoryRoot '.github\workflows'
Assert-AdditionalManualTriggersRejected
$matchBasePath = Join-Path $workflowDirectory 'base-matchday-predictions.yml'
$bonusBasePath = Join-Path $workflowDirectory 'base-bonus-predictions.yml'
$matchBase = Get-Content -Raw -LiteralPath $matchBasePath
$bonusBase = Get-Content -Raw -LiteralPath $bonusBasePath
$productionLiveFileName = 'buli2627-production-live-matchday.yml'
$arenaLunaMatchFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml'
$arenaLunaBonusFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml'
$arenaLunaTriadPresent = Assert-ArenaLunaTriad $workflowDirectory -AllowMissing:$AllowMissingArenaLunaTriad
Assert-LaunchRosterOverlayBaseContract $workflowDirectory
Assert-ProductionContextEntrypoints $workflowDirectory
Assert-ArenaParticipantContextEntrypoints $workflowDirectory
Assert-ArenaScheduleScannerSelfTests
Assert-NoActiveArenaLunaSchedule $workflowDirectory $productionLiveFileName
Assert-ProductionLiveMatchdayWorkflow $workflowDirectory $productionLiveFileName
Assert-BundesligaProductionCallerConcurrency $workflowDirectory $productionLiveFileName

foreach ($base in @(
    @{ Path = $matchBasePath; Content = $matchBase },
    @{ Path = $bonusBasePath; Content = $bonusBase }
)) {
    foreach ($inputName in @(
        'community',
        'community_context',
        'competition',
        'model',
        'reasoning_effort',
        'max_output_tokens',
        'prompt_source'
    )) {
        Assert-RequiredInput $base.Content $inputName (Split-Path -Leaf $base.Path)
    }

    $preflightIndex = $base.Content.IndexOf('- name: Validate pinned prediction configuration', [StringComparison]::Ordinal)
    $checkoutIndex = $base.Content.IndexOf('- name: Checkout repository', [StringComparison]::Ordinal)
    Assert-True ($preflightIndex -ge 0 -and $preflightIndex -lt $checkoutIndex) "$(Split-Path -Leaf $base.Path) must fail invalid identity before checkout or prediction work."
    Assert-True $base.Content.Contains('This historical prediction configuration is retired', [StringComparison]::Ordinal) "$(Split-Path -Leaf $base.Path) must explicitly fail retired historical callers."
    Assert-True $base.Content.Contains('LANGFUSE_PUBLIC_KEY: ${{ vars.LANGFUSE_PUBLIC_KEY }}', [StringComparison]::Ordinal) "$(Split-Path -Leaf $base.Path) must source LANGFUSE_PUBLIC_KEY from the repository variable."

    foreach ($environmentRoute in @(
        @{ Name = 'TRIGGER_TYPE'; Input = 'trigger_type' },
        @{ Name = 'FORCE_PREDICTION'; Input = 'force_prediction' },
        @{ Name = 'MAX_REPREDICTIONS'; Input = 'max_repredictions' }
    )) {
        $expectedRoute = "      $($environmentRoute.Name): `${{ inputs.$($environmentRoute.Input) }}"
        Assert-True $base.Content.Contains($expectedRoute, [StringComparison]::Ordinal) "$(Split-Path -Leaf $base.Path) must route $($environmentRoute.Input) through the job environment."
    }

    $runScripts = @(Get-WorkflowRunScripts $base.Content)
    Assert-True ($runScripts.Count -gt 0) "$(Split-Path -Leaf $base.Path) must contain shell run blocks to audit."
    foreach ($runScript in $runScripts) {
        Assert-True (-not $runScript.Contains('${{ inputs.', [StringComparison]::Ordinal)) "$(Split-Path -Leaf $base.Path) must not interpolate workflow inputs directly into shell run blocks."
    }

    $safeSummaryLine = @'
          printf '%s\n' "- **Community**: $COMMUNITY" >> "$GITHUB_STEP_SUMMARY"
'@.Trim()
    Assert-True $base.Content.Contains($safeSummaryLine, [StringComparison]::Ordinal) "$(Split-Path -Leaf $base.Path) must render summary values from quoted environment variables with printf."
}

Assert-HostileSummaryValueIsLiteral -Skip:$SkipHostileShellSimulation

Assert-CommandIdentity $matchBase 'verify' 2 'base-matchday-predictions.yml'
Assert-CommandIdentity $matchBase 'matchday' 1 'base-matchday-predictions.yml'
Assert-True ([regex]::Matches($matchBase, '(?m)^\s*dotnet run .* -- verify\b.*--check-outdated').Count -eq 2) 'Both matchday verification commands must check outdated predictions.'
Assert-True ([regex]::Matches($matchBase, '(?m)^\s*dotnet run .* -- verify\b.*--require-matches').Count -eq 1) 'Only final matchday verification must fail closed when Kicktipp exposes no match table.'
$finalMatchVerificationIndex = $matchBase.IndexOf('- name: Final verification', [StringComparison]::Ordinal)
$requireMatchesIndex = $matchBase.IndexOf('--require-matches', [StringComparison]::Ordinal)
$successNotificationIndex = $matchBase.IndexOf('- name: Success notification', [StringComparison]::Ordinal)
Assert-True ($finalMatchVerificationIndex -ge 0 -and $requireMatchesIndex -gt $finalMatchVerificationIndex -and $requireMatchesIndex -lt $successNotificationIndex) 'The fail-closed empty-table guard must belong to final matchday verification.'

Assert-CommandIdentity $bonusBase 'verify-bonus' 2 'base-bonus-predictions.yml'
Assert-CommandIdentity $bonusBase 'bonus' 1 'base-bonus-predictions.yml'
Assert-True ([regex]::Matches($bonusBase, '(?m)^\s*dotnet run .* -- verify-bonus\b.*--check-outdated').Count -eq 2) 'Both bonus verification commands must restore ADR-0037 outdated checking.'
Assert-True $bonusBase.Contains('default: 20', [StringComparison]::Ordinal) 'The accepted 20-document bonus context budget must be surfaced.'
Assert-True $bonusBase.Contains('default: 32000', [StringComparison]::Ordinal) 'The accepted 32,000-token bonus context budget must be surfaced.'
Assert-True ([regex]::IsMatch($bonusBase, '(?m)^      bonus_deadline_at_or_before:\r?$')) 'The reusable bonus workflow must expose the optional deadline ceiling.'
Assert-True $bonusBase.Contains("        default: ''", [StringComparison]::Ordinal) 'The reusable bonus deadline ceiling must default to the unchanged unfiltered behavior.'
Assert-True $bonusBase.Contains('--bonus-context-document-budget "$BONUS_CONTEXT_DOCUMENT_BUDGET"', [StringComparison]::Ordinal) 'The bonus document budget must reach generation.'
Assert-True $bonusBase.Contains('--bonus-context-token-budget "$BONUS_CONTEXT_TOKEN_BUDGET"', [StringComparison]::Ordinal) 'The bonus token budget must reach generation.'
Assert-True ([regex]::Matches($bonusBase, '--bonus-deadline-at-or-before "\$BONUS_DEADLINE_AT_OR_BEFORE"').Count -eq 3) 'The exact deadline ceiling must reach initial verification, generation, and final verification.'

$currentBundesligaCallers = @{}
foreach ($row in @(
    @{ BaseName = 'buli2627-pes-squad-gpt-5-6-sol-xhigh'; Community = 'pes-squad'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'PES_SQUAD' },
    @{ BaseName = 'buli2627-schadensfresse-gpt-5-6-sol-xhigh'; Community = 'schadensfresse'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'SCHADENSFRESSE' },
    @{ BaseName = 'buli2627-relaxdays-tippt-gpt-5-6-sol-xhigh'; Community = 'relaxdays-tippt'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'RELAXDAYS_TIPPT' },
    @{ BaseName = 'buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh'; Community = 'ehonda-ai-arena'; Context = 'pes-squad'; Model = 'gpt-5.6-sol'; Effort = 'xhigh'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH' },
    @{ BaseName = 'buli2627-ehonda-ai-arena-gpt-5-6-sol-high'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-sol'; Effort = 'high'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH' },
    @{ BaseName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-medium'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-luna'; Effort = 'medium'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM' },
    @{ BaseName = 'buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-terra'; Effort = 'xhigh'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH' },
    @{ BaseName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none'; Community = 'ehonda-ai-arena'; Context = 'ehonda-ai-arena'; Model = 'gpt-5.6-luna'; Effort = 'none'; SecretStem = 'EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE' }
)) {
    foreach ($kind in @('matchday', 'bonus')) {
        $fileName = "$($row.BaseName)-$kind.yml"
        $currentBundesligaCallers[$fileName] = [pscustomobject]@{
            Community = $row.Community
            Context = $row.Context
            Model = $row.Model
            Effort = $row.Effort
            SecretStem = $row.SecretStem
            IsBonus = $kind -eq 'bonus'
        }
    }
}

$callerFiles = Get-ChildItem -LiteralPath $workflowDirectory -Filter '*.yml' |
    Where-Object {
        $content = Get-Content -Raw -LiteralPath $_.FullName
        $_.Name -ne $productionLiveFileName -and (
            $content.Contains('uses: ./.github/workflows/base-matchday-predictions.yml', [StringComparison]::Ordinal) -or
            $content.Contains('uses: ./.github/workflows/base-bonus-predictions.yml', [StringComparison]::Ordinal))
    } |
    Sort-Object Name

$retiredBundesligaFiles = [Collections.Generic.HashSet[string]]::new(
    [string[]] @(
        'ehonda-ai-arena-gpt-5-bonus.yml',
        'ehonda-ai-arena-gpt-5-matchday.yml',
        'ehonda-ai-arena-gpt-5-mini-bonus.yml',
        'ehonda-ai-arena-gpt-5-mini-matchday.yml',
        'ehonda-ai-arena-o3-bonus.yml',
        'ehonda-ai-arena-o3-matchday.yml',
        'ehonda-ai-arena-o4-mini-bonus.yml',
        'ehonda-ai-arena-o4-mini-matchday.yml',
        'pes-squad-bonus.yml',
        'pes-squad-matchday.yml',
        'schadensfresse-bonus.yml',
        'schadensfresse-matchday.yml'
    ),
    [StringComparer]::Ordinal
)
Assert-True ($callerFiles.Count -ge 42) "Expected at least 42 retained prediction callers, found $($callerFiles.Count)."

$wm26Count = 0
$retiredBundesligaCount = 0
$currentBundesligaCount = 0
foreach ($caller in $callerFiles) {
    $content = Get-Content -Raw -LiteralPath $caller.FullName
    $isCurrentBundesliga = $currentBundesligaCallers.ContainsKey($caller.Name)
    if ($isCurrentBundesliga) {
        Assert-ManualDispatchOnly $content $caller.Name $true
        Assert-NoYamlScheduleKey $content $caller.Name
    }
    else {
        $triggerBlock = Get-TriggerBlock $content $caller.Name
        Assert-True ([regex]::IsMatch($triggerBlock, '(?m)^  workflow_call:\s*$')) "$($caller.Name) must remain workflow_call-only."
        Assert-True (-not [regex]::IsMatch($triggerBlock, '(?m)^  (schedule|workflow_dispatch):')) "$($caller.Name) must not activate schedule or workflow_dispatch."
    }

    foreach ($inputName in @(
        'community',
        'community_context',
        'competition',
        'model',
        'reasoning_effort',
        'max_output_tokens',
        'prompt_source'
    )) {
        $null = Get-WithValue $content $inputName $caller.Name
    }

    $competition = Get-WithValue $content 'competition' $caller.Name
    $promptSource = Get-WithValue $content 'prompt_source' $caller.Name
    $maxOutputTokens = Get-WithValue $content 'max_output_tokens' $caller.Name
    Assert-True ($maxOutputTokens -match '^[1-9][0-9]*$') "$($caller.Name) must pin a positive output cap."

    $isWm26 = $caller.Name.StartsWith('wm26-', [StringComparison]::Ordinal)
    if ($isWm26) {
        $wm26Count++
        Assert-True ($competition -eq 'fifa-world-cup-2026') "$($caller.Name) must remain in fifa-world-cup-2026."
        Assert-True ($promptSource -eq 'langfuse') "$($caller.Name) must keep its hosted WM26 prompt route."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_label' $caller.Name) -eq 'latest') "$($caller.Name) must retain the historical WM26 latest label alongside its pin."

        $isBonus = $caller.Name.EndsWith('-bonus.yml', [StringComparison]::Ordinal)
        $expectedName = if ($isBonus) { 'kicktippai/wm26/predict-bonus' } else { 'kicktippai/wm26/predict-one-match' }
        $expectedVersion = if ($isBonus) { '1' } else { '3' }
        Assert-True ((Get-WithValue $content 'langfuse_prompt_name' $caller.Name) -eq $expectedName) "$($caller.Name) has the wrong WM26 prompt name."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_version' $caller.Name) -eq $expectedVersion) "$($caller.Name) has the wrong numbered WM26 prompt version."
        Assert-True ((Get-WithValue $content 'retired_configuration' $caller.Name $false) -ne 'true') "$($caller.Name) must remain callable rather than retired."
    }
    elseif ($retiredBundesligaFiles.Contains($caller.Name)) {
        $retiredBundesligaCount++
        Assert-True ($competition -eq 'bundesliga-2025-26') "$($caller.Name) must retain its historical Bundesliga competition."
        Assert-True ($promptSource -eq 'local') "$($caller.Name) must truthfully retain its unversioned local prompt source."
        Assert-True ((Get-WithValue $content 'retired_configuration' $caller.Name) -eq 'true') "$($caller.Name) must fail explicitly because no truthful numbered local identity exists."
        Assert-True (-not $content.Contains('kicktippai/bundesliga-2026-27/', [StringComparison]::Ordinal)) "$($caller.Name) must not infer the Bundesliga 2026/27 hosted route."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_name' $caller.Name $false) -eq $null) "$($caller.Name) must not invent a hosted prompt name."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_version' $caller.Name $false) -eq $null) "$($caller.Name) must not invent a numbered local prompt version."
    }
    else {
        Assert-True $isCurrentBundesliga "$($caller.Name) is neither an accepted current Bundesliga caller nor an explicitly retained historical caller."
        $currentBundesligaCount++
        $expectedCaller = $currentBundesligaCallers[$caller.Name]
        Assert-True ($competition -eq 'bundesliga-2026-27') "$($caller.Name) must explicitly target bundesliga-2026-27."
        Assert-True ($promptSource -eq 'langfuse') "$($caller.Name) must use the accepted hosted Bundesliga prompt route."
        Assert-True ((Get-WithValue $content 'retired_configuration' $caller.Name $false) -ne 'true') "$($caller.Name) is a current Bundesliga caller and cannot be retired."

        $reasoningEffort = Get-WithValue $content 'reasoning_effort' $caller.Name
        Assert-True ($reasoningEffort -eq $expectedCaller.Effort) "$($caller.Name) must pin reasoning_effort=$($expectedCaller.Effort)."
        Assert-True ((Get-WithValue $content 'community' $caller.Name) -eq $expectedCaller.Community) "$($caller.Name) must pin community=$($expectedCaller.Community)."
        Assert-True ((Get-WithValue $content 'community_context' $caller.Name) -eq $expectedCaller.Context) "$($caller.Name) must pin community_context=$($expectedCaller.Context)."
        Assert-True ((Get-WithValue $content 'model' $caller.Name) -eq $expectedCaller.Model) "$($caller.Name) must pin model=$($expectedCaller.Model)."
        Assert-True ($maxOutputTokens -eq '10000') "$($caller.Name) must pin the accepted 10000 output cap."
        $isBonus = $caller.Name.EndsWith('-bonus.yml', [StringComparison]::Ordinal)
        Assert-True ($isBonus -eq $expectedCaller.IsBonus) "$($caller.Name) kind does not match its accepted matrix row."
        $expectedName = if ($isBonus) { 'kicktippai/bundesliga-2026-27/predict-bonus' } else { 'kicktippai/bundesliga-2026-27/predict-one-match' }
        $expectedVersion = if ($isBonus) { '1' } else { '3' }
        Assert-True ((Get-WithValue $content 'langfuse_prompt_name' $caller.Name) -eq $expectedName) "$($caller.Name) has the wrong Bundesliga prompt name."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_version' $caller.Name) -eq $expectedVersion) "$($caller.Name) has the wrong accepted Bundesliga prompt version."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_label' $caller.Name) -eq 'production') "$($caller.Name) must require production label membership."
        Assert-True ((Get-WithValue $content 'trigger_type' $caller.Name) -eq 'manual') "$($caller.Name) must pass trigger_type=manual."
        Assert-True ((Get-WithValue $content 'force_prediction' $caller.Name) -eq '${{ inputs.force_prediction }}') "$($caller.Name) must pass through force_prediction."
        Assert-True ((Get-WithValue $content 'max_repredictions' $caller.Name) -eq '${{ fromJSON(inputs.max_repredictions) }}') "$($caller.Name) must preserve zero while converting max_repredictions to a number."
        if ($isBonus) {
            Assert-True ((Get-WithValue $content 'bonus_context_document_budget' $caller.Name) -eq '20') "$($caller.Name) must pin the accepted 20-document bonus budget."
            Assert-True ((Get-WithValue $content 'bonus_context_token_budget' $caller.Name) -eq '32000') "$($caller.Name) must pin the accepted 32000-token bonus budget."
            $deadlineCeiling = Get-WithValue $content 'bonus_deadline_at_or_before' $caller.Name $false
            if ($caller.Name -ceq 'buli2627-schadensfresse-gpt-5-6-sol-xhigh-bonus.yml') {
                Assert-True ($deadlineCeiling -eq '${{ inputs.bonus_deadline_at_or_before }}') "$($caller.Name) must pass through its audited initial Bundesliga deadline ceiling."
                Assert-True $content.Contains("        default: '2026-08-28T18:30:00Z'", [StringComparison]::Ordinal) "$($caller.Name) must default to the exact initial Bundesliga bonus cutoff."
            }
            else {
                Assert-True ($null -eq $deadlineCeiling) "$($caller.Name) must retain the reusable workflow's unfiltered default."
            }
        }

        Assert-ExactSecretMappings $content @(
            "kicktipp_username=$($expectedCaller.SecretStem)_KICKTIPP_USERNAME",
            "kicktipp_password=$($expectedCaller.SecretStem)_KICKTIPP_PASSWORD",
            'firebase_project_id=FIREBASE_PROJECT_ID',
            'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON',
            'openai_api_key=OPENAI_API_KEY',
            'langfuse_secret_key=LANGFUSE_SECRET_KEY'
        ) $caller.Name
    }
}

Assert-True ($wm26Count -eq 14) "Expected 14 historical WM26 callers, found $wm26Count."
Assert-True ($retiredBundesligaCount -eq 12) "Expected 12 retired Bundesliga 2025/26 callers, found $retiredBundesligaCount."
$expectedCurrentBundesligaCount = $currentBundesligaCallers.Count
Assert-True ($currentBundesligaCount -eq $expectedCurrentBundesligaCount) "Expected $expectedCurrentBundesligaCount exact current Bundesliga prediction callers, found $currentBundesligaCount."

Write-Output "Prediction workflow contract validation passed: 2 bases, $wm26Count callable WM26 callers, $retiredBundesligaCount explicitly retired Bundesliga callers, $currentBundesligaCount current Bundesliga callers."
