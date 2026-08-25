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

    $match = [regex]::Match($Content, '(?ms)^on:\r?\n(?<body>.*?)^jobs:')
    Assert-True $match.Success "$FileName does not contain a readable top-level trigger block."
    return $match.Groups['body'].Value
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
        @{ FileName = $matchFileName; Content = $match; Base = 'base-matchday-predictions.yml'; PromptName = 'kicktippai/bundesliga-2026-27/predict-one-match'; PromptVersion = '2' },
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

function Get-TopLevelBlock {
    param(
        [string] $Content,
        [string] $Name,
        [string] $FileName
    )

    $match = [regex]::Match(
        $Content,
        "(?ms)^$([regex]::Escape($Name)):\r?\n(?<body>.*?)(?=^\S|\z)")
    Assert-True $match.Success "$FileName does not contain a readable top-level '$Name' block."
    return $match.Groups['body'].Value
}

function Get-NamedJobBlock {
    param(
        [string] $JobsBlock,
        [string] $Name,
        [string] $FileName
    )

    $match = [regex]::Match(
        $JobsBlock,
        "(?ms)^  $([regex]::Escape($Name)):\r?\n(?<body>.*?)(?=^  \S|\z)")
    Assert-True $match.Success "$FileName does not contain the expected '$Name' job."
    return $match.Groups['body'].Value
}

function Assert-ExactJobSecretBlock {
    param(
        [string] $JobContent,
        [string[]] $ExpectedMappings,
        [string] $JobName,
        [string] $FileName
    )

    $secretBlock = [regex]::Match(
        $JobContent,
        '(?ms)^    secrets:\r?\n(?<body>.*?)(?=^    \S|\z)')
    Assert-True $secretBlock.Success "$FileName $JobName does not contain a readable secrets block."

    $expected = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($mapping in $ExpectedMappings) {
        $parts = $mapping.Split('=', 2)
        Assert-True ($parts.Count -eq 2 -and $expected.TryAdd($parts[0], $parts[1])) "$FileName $JobName has an invalid expected secret contract."
    }

    $actual = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $secretLines = @($secretBlock.Groups['body'].Value -split '\r?\n' |
        Where-Object { $_.Trim().Length -gt 0 -and $_ -notmatch '^\s*#' })
    foreach ($line in $secretLines) {
        $entry = [regex]::Match($line, '^      (?<key>[a-z][a-z0-9_]*):\s*(?<value>\S.*?)\s*$')
        Assert-True $entry.Success "$FileName $JobName contains a malformed or nested secret entry: '$line'."
        Assert-True $actual.TryAdd($entry.Groups['key'].Value, $entry.Groups['value'].Value) "$FileName $JobName repeats secret key '$($entry.Groups['key'].Value)'."
    }

    Assert-True ($actual.Count -eq $expected.Count) "$FileName $JobName must contain exactly $($expected.Count) secret keys; found $($actual.Count)."
    foreach ($key in $expected.Keys) {
        $expectedExpression = '${{ secrets.' + $expected[$key] + ' }}'
        Assert-True ($actual.ContainsKey($key) -and $actual[$key] -ceq $expectedExpression) "$FileName $JobName must map $key exactly from secrets.$($expected[$key])."
    }
}

function Assert-ExactJobWithBlock {
    param(
        [string] $JobContent,
        [string[]] $ExpectedMappings,
        [string] $JobName,
        [string] $FileName
    )

    $withBlock = [regex]::Match(
        $JobContent,
        '(?ms)^    with:\r?\n(?<body>.*?)(?=^    \S|\z)')
    Assert-True $withBlock.Success "$FileName $JobName does not contain a readable with block."

    $expected = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($mapping in $ExpectedMappings) {
        $parts = $mapping.Split('=', 2)
        Assert-True ($parts.Count -eq 2 -and $expected.TryAdd($parts[0], $parts[1])) "$FileName $JobName has an invalid expected input contract."
    }

    $actual = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $withLines = @($withBlock.Groups['body'].Value -split '\r?\n' |
        Where-Object { $_.Trim().Length -gt 0 -and $_ -notmatch '^\s*#' })
    foreach ($line in $withLines) {
        $entry = [regex]::Match($line, '^      (?<key>[a-z][a-z0-9_]*):\s*(?<value>\S.*?)\s*$')
        Assert-True $entry.Success "$FileName $JobName contains a malformed or nested input entry: '$line'."
        $rawValue = $entry.Groups['value'].Value.Trim()
        Assert-True $actual.TryAdd($entry.Groups['key'].Value, $rawValue) "$FileName $JobName repeats input key '$($entry.Groups['key'].Value)'."
    }

    Assert-True ($actual.Count -eq $expected.Count) "$FileName $JobName must contain exactly $($expected.Count) input keys; found $($actual.Count)."
    foreach ($key in $expected.Keys) {
        Assert-True ($actual.ContainsKey($key) -and $actual[$key] -ceq $expected[$key]) "$FileName $JobName must pass $key exactly as '$($expected[$key])'."
    }
}

function Assert-TemporaryArenaLunaScheduledCycleContent {
    param(
        [string] $Content,
        [string] $FileName
    )

    $topLevelLines = @($Content -split '\r?\n' |
        Where-Object { $_ -match '^\S' -and $_ -notmatch '^#' })
    $topLevelKeys = [Collections.Generic.List[string]]::new()
    foreach ($topLevelLine in $topLevelLines) {
        $topLevelKey = [regex]::Match($topLevelLine, '^(?<name>[A-Za-z_][A-Za-z0-9_-]*):(?:\s.*)?$')
        Assert-True $topLevelKey.Success "$FileName contains a malformed top-level key: '$topLevelLine'."
        $topLevelKeys.Add($topLevelKey.Groups['name'].Value)
    }
    Assert-True (($topLevelKeys -join ',') -ceq 'name,on,concurrency,jobs') "$FileName must contain exactly the name, on, concurrency, and jobs top-level keys."

    $triggerBlock = Get-TopLevelBlock $Content 'on' $FileName
    $triggerLines = @($triggerBlock -split '\r?\n' |
        Where-Object { $_.Trim().Length -gt 0 -and $_ -notmatch '^\s*#' })
    $expectedTrigger = "  schedule:`n    - cron: '47 8 * * *'"
    Assert-True (($triggerLines -join "`n") -ceq $expectedTrigger) "$FileName must contain only the single accepted 47 8 * * * UTC schedule row."

    $concurrencyBlock = Get-TopLevelBlock $Content 'concurrency' $FileName
    $concurrencyEntries = @($concurrencyBlock -split '\r?\n' |
        Where-Object { $_ -match '^  \S' -and $_ -notmatch '^  #' })
    Assert-True (($concurrencyEntries -join "`n") -ceq "  group: p0-20-ehonda-ai-arena-luna-scheduled-cycle`n  cancel-in-progress: false") "$FileName must pin the accepted non-cancelling concurrency group."

    $jobsBlock = Get-TopLevelBlock $Content 'jobs' $FileName
    $jobKeyLines = @($jobsBlock -split '\r?\n' |
        Where-Object { $_ -match '^  \S' -and $_ -notmatch '^  #' })
    $jobNames = [Collections.Generic.List[string]]::new()
    foreach ($jobKeyLine in $jobKeyLines) {
        $jobKey = [regex]::Match($jobKeyLine, '^  (?<name>[A-Za-z_][A-Za-z0-9_-]*):\s*$')
        Assert-True $jobKey.Success "$FileName contains a malformed or inline top-level job key: '$jobKeyLine'."
        $jobNames.Add($jobKey.Groups['name'].Value)
    }
    Assert-True (($jobNames -join ',') -ceq 'context,matchday,bonus') "$FileName must contain exactly the ordered context, matchday, and bonus job keys."

    $context = Get-NamedJobBlock $jobsBlock 'context' $FileName
    $matchday = Get-NamedJobBlock $jobsBlock 'matchday' $FileName
    $bonus = Get-NamedJobBlock $jobsBlock 'bonus' $FileName
    foreach ($job in @(
        @{ Name = 'context'; Content = $context; ExpectedKeys = 'name,uses,with,secrets' },
        @{ Name = 'matchday'; Content = $matchday; ExpectedKeys = 'name,needs,uses,with,secrets' },
        @{ Name = 'bonus'; Content = $bonus; ExpectedKeys = 'name,needs,uses,with,secrets' }
    )) {
        $jobKeyLines = @($job.Content -split '\r?\n' |
            Where-Object { $_ -match '^    \S' -and $_ -notmatch '^    #' })
        $jobKeys = [Collections.Generic.List[string]]::new()
        foreach ($jobKeyLine in $jobKeyLines) {
            $jobKey = [regex]::Match($jobKeyLine, '^    (?<name>[A-Za-z_][A-Za-z0-9_-]*):(?:\s.*)?$')
            Assert-True $jobKey.Success "$FileName $($job.Name) contains a malformed job-level key: '$jobKeyLine'."
            $jobKeys.Add($jobKey.Groups['name'].Value)
        }
        Assert-True (($jobKeys -join ',') -ceq $job.ExpectedKeys) "$FileName $($job.Name) must contain exactly the accepted job-level keys."
    }
    Assert-True (-not [regex]::IsMatch($context, '(?m)^    needs:')) "$FileName context must be the only root job."
    Assert-True ([regex]::IsMatch($matchday, '(?m)^    needs: context\s*$')) "$FileName matchday must depend exactly on context."
    Assert-True ([regex]::IsMatch($bonus, '(?m)^    needs: matchday\s*$')) "$FileName bonus must depend exactly on matchday."
    foreach ($job in @(
        @{ Name = 'context'; Content = $context },
        @{ Name = 'matchday'; Content = $matchday },
        @{ Name = 'bonus'; Content = $bonus }
    )) {
        Assert-True (-not [regex]::IsMatch($job.Content, '(?m)^    if\s*:')) "$FileName $($job.Name) must not declare a job-level if condition."
        Assert-True (-not [regex]::IsMatch($job.Content, '(?m)^    continue-on-error\s*:')) "$FileName $($job.Name) must not continue on error."
    }

    foreach ($job in @(
        @{ Name = 'context'; Content = $context; Base = 'base-context-collection.yml' },
        @{ Name = 'matchday'; Content = $matchday; Base = 'base-matchday-predictions.yml' },
        @{ Name = 'bonus'; Content = $bonus; Base = 'base-bonus-predictions.yml' }
    )) {
        $usesPattern = "(?m)^    uses: \./\.github/workflows/$([regex]::Escape($job.Base))\r?$"
        Assert-True ([regex]::Matches($job.Content, $usesPattern).Count -eq 1) "$FileName $($job.Name) must call exactly $($job.Base)."
    }

    Assert-ExactJobWithBlock $context @(
        'community_context="ehonda-ai-arena"',
        'competition="bundesliga-2026-27"',
        'trigger_type="scheduled"'
    ) 'context' $FileName
    Assert-ExactJobWithBlock $matchday @(
        'community="ehonda-ai-arena"',
        'community_context="ehonda-ai-arena"',
        'competition="bundesliga-2026-27"',
        'model="gpt-5.6-luna"',
        'reasoning_effort="none"',
        'max_output_tokens=10000',
        'prompt_source="langfuse"',
        'langfuse_prompt_name="kicktippai/bundesliga-2026-27/predict-one-match"',
        'langfuse_prompt_label="production"',
        'langfuse_prompt_version=2',
        'trigger_type="scheduled"',
        'force_prediction=true',
        'max_repredictions=0'
    ) 'matchday' $FileName
    Assert-ExactJobWithBlock $bonus @(
        'community="ehonda-ai-arena"',
        'community_context="ehonda-ai-arena"',
        'competition="bundesliga-2026-27"',
        'model="gpt-5.6-luna"',
        'reasoning_effort="none"',
        'max_output_tokens=10000',
        'prompt_source="langfuse"',
        'langfuse_prompt_name="kicktippai/bundesliga-2026-27/predict-bonus"',
        'langfuse_prompt_label="production"',
        'langfuse_prompt_version=1',
        'bonus_context_document_budget=20',
        'bonus_context_token_budget=32000',
        'trigger_type="scheduled"',
        'force_prediction=true',
        'max_repredictions=0'
    ) 'bonus' $FileName

    foreach ($expected in @(
        @{ Name = 'community_context'; Value = 'ehonda-ai-arena' },
        @{ Name = 'competition'; Value = 'bundesliga-2026-27' },
        @{ Name = 'trigger_type'; Value = 'scheduled' }
    )) {
        Assert-True ((Get-WithValue $context $expected.Name $FileName) -eq $expected.Value) "$FileName context must pass $($expected.Name)=$($expected.Value)."
    }

    foreach ($prediction in @(
        @{ Name = 'matchday'; Content = $matchday; PromptName = 'kicktippai/bundesliga-2026-27/predict-one-match'; PromptVersion = '2' },
        @{ Name = 'bonus'; Content = $bonus; PromptName = 'kicktippai/bundesliga-2026-27/predict-bonus'; PromptVersion = '1' }
    )) {
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
            @{ Name = 'trigger_type'; Value = 'scheduled' },
            @{ Name = 'force_prediction'; Value = 'true' },
            @{ Name = 'max_repredictions'; Value = '0' }
        )) {
            Assert-True ((Get-WithValue $prediction.Content $expected.Name $FileName) -eq $expected.Value) "$FileName $($prediction.Name) must pass $($expected.Name)=$($expected.Value)."
        }
    }

    Assert-True ((Get-WithValue $bonus 'bonus_context_document_budget' $FileName) -eq '20') "$FileName bonus must pin the 20-document context budget."
    Assert-True ((Get-WithValue $bonus 'bonus_context_token_budget' $FileName) -eq '32000') "$FileName bonus must pin the 32,000-token context budget."

    Assert-ExactJobSecretBlock $context @(
        'kicktipp_username=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME',
        'kicktipp_password=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD',
        'firebase_project_id=FIREBASE_PROJECT_ID',
        'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON'
    ) 'context' $FileName
    foreach ($prediction in @(
        @{ Name = 'matchday'; Content = $matchday },
        @{ Name = 'bonus'; Content = $bonus }
    )) {
        Assert-ExactJobSecretBlock $prediction.Content @(
            'kicktipp_username=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME',
            'kicktipp_password=EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD',
            'firebase_project_id=FIREBASE_PROJECT_ID',
            'firebase_service_account_json=FIREBASE_SERVICE_ACCOUNT_JSON',
            'openai_api_key=OPENAI_API_KEY',
            'langfuse_secret_key=LANGFUSE_SECRET_KEY'
        ) $prediction.Name $FileName
    }

    foreach ($forbidden in @(
        'ehonda-dev-buli-2627',
        'pes-squad',
        'schadensfresse',
        'rabetrabauken2026',
        'bundesliga-2025-26',
        'fifa-world-cup-2026',
        'wm26'
    )) {
        Assert-True (-not $Content.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) "$FileName must not contain out-of-scope identity '$forbidden'."
    }
}

function Assert-ScheduledCycleMutationRejected {
    param(
        [string] $Content,
        [string] $MutationName
    )

    $rejectionMessage = $null
    try {
        Assert-TemporaryArenaLunaScheduledCycleContent $Content "synthetic-$MutationName.yml"
    }
    catch {
        $rejectionMessage = $_.Exception.Message
    }

    Assert-True ($null -ne $rejectionMessage) "The temporary schedule contract must reject the '$MutationName' mutation."
}

function Assert-TemporaryArenaLunaScheduledCycle {
    param([string] $WorkflowDirectory)

    $fileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-scheduled-cycle.yml'
    $path = Join-Path $WorkflowDirectory $fileName
    Assert-True (Test-Path -LiteralPath $path) "$fileName must remain present until the one-cycle activation is manually torn down."
    $content = Get-Content -Raw -LiteralPath $path

    Assert-TemporaryArenaLunaScheduledCycleContent $content $fileName

    Assert-ScheduledCycleMutationRejected ($content.Replace(
        "    - cron: '47 8 * * *'",
        "    - cron: '47 8 * * *'`n    - cron: '52 8 * * *'")) 'second-cron'
    Assert-ScheduledCycleMutationRejected ($content + "`n  unexpected:`n    uses: ./.github/workflows/base-context-collection.yml`n") 'extra-job'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        'on:',
        "permissions: write-all`non:")) 'top-level-permissions'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '    name: Force arena matchday validation',
        "    name: Force arena matchday validation`n    if: failure()")) 'job-if'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '    name: Force arena bonus validation',
        "    name: Force arena bonus validation`n    continue-on-error: true")) 'job-continue-on-error'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '      firebase_service_account_json: ${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}',
        "      firebase_service_account_json: `${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}`n      extra_secret: `${{ secrets.OPENAI_API_KEY }}")) 'extra-secret'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '      openai_api_key: ${{ secrets.OPENAI_API_KEY }}',
        '      openai_api_key: ${{ vars.OPENAI_API_KEY }}')) 'malformed-secret-expression'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '      max_repredictions: 0',
        "      max_repredictions: 0`n      retired_configuration: true")) 'extra-reusable-input'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '      max_repredictions: 0',
        '      max_repredictions: "0"')) 'quoted-numeric-input'
    Assert-ScheduledCycleMutationRejected ($content.Replace(
        '      force_prediction: true',
        '      force_prediction: "true"')) 'quoted-boolean-input'

    return $fileName
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
$arenaLunaMatchFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml'
$arenaLunaBonusFileName = 'buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml'
$arenaLunaTriadPresent = Assert-ArenaLunaTriad $workflowDirectory -AllowMissing:$AllowMissingArenaLunaTriad
$temporaryArenaLunaScheduledCycleFileName = Assert-TemporaryArenaLunaScheduledCycle $workflowDirectory

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

Assert-CommandIdentity $bonusBase 'verify-bonus' 2 'base-bonus-predictions.yml'
Assert-CommandIdentity $bonusBase 'bonus' 1 'base-bonus-predictions.yml'
Assert-True ([regex]::Matches($bonusBase, '(?m)^\s*dotnet run .* -- verify-bonus\b.*--check-outdated').Count -eq 2) 'Both bonus verification commands must restore ADR-0037 outdated checking.'
Assert-True $bonusBase.Contains('default: 20', [StringComparison]::Ordinal) 'The accepted 20-document bonus context budget must be surfaced.'
Assert-True $bonusBase.Contains('default: 32000', [StringComparison]::Ordinal) 'The accepted 32,000-token bonus context budget must be surfaced.'
Assert-True $bonusBase.Contains('--bonus-context-document-budget "$BONUS_CONTEXT_DOCUMENT_BUDGET"', [StringComparison]::Ordinal) 'The bonus document budget must reach generation.'
Assert-True $bonusBase.Contains('--bonus-context-token-budget "$BONUS_CONTEXT_TOKEN_BUDGET"', [StringComparison]::Ordinal) 'The bonus token budget must reach generation.'

$callerFiles = Get-ChildItem -LiteralPath $workflowDirectory -Filter '*.yml' |
    Where-Object {
        $content = Get-Content -Raw -LiteralPath $_.FullName
        $_.Name -ne $temporaryArenaLunaScheduledCycleFileName -and (
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
Assert-True ($callerFiles.Count -ge 26) "Expected at least 26 retained prediction callers, found $($callerFiles.Count)."

$wm26Count = 0
$retiredBundesligaCount = 0
$currentBundesligaCount = 0
foreach ($caller in $callerFiles) {
    $content = Get-Content -Raw -LiteralPath $caller.FullName
    $isArenaLunaPrediction = $caller.Name -in @($arenaLunaMatchFileName, $arenaLunaBonusFileName)
    if ($isArenaLunaPrediction) {
        Assert-ManualDispatchOnly $content $caller.Name $true
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
        $currentBundesligaCount++
        Assert-True ($competition -eq 'bundesliga-2026-27') "$($caller.Name) must explicitly target bundesliga-2026-27."
        Assert-True ($promptSource -eq 'langfuse') "$($caller.Name) must use the accepted hosted Bundesliga prompt route."
        Assert-True ((Get-WithValue $content 'retired_configuration' $caller.Name $false) -ne 'true') "$($caller.Name) is a current Bundesliga caller and cannot be retired."

        $reasoningEffort = Get-WithValue $content 'reasoning_effort' $caller.Name
        Assert-True ($reasoningEffort -match '^(none|minimal|low|medium|high|xhigh|max)$') "$($caller.Name) must pin a supported reasoning effort."
        $isBonus = $caller.Name.EndsWith('-bonus.yml', [StringComparison]::Ordinal)
        $expectedName = if ($isBonus) { 'kicktippai/bundesliga-2026-27/predict-bonus' } else { 'kicktippai/bundesliga-2026-27/predict-one-match' }
        $expectedVersion = if ($isBonus) { '1' } else { '2' }
        Assert-True ((Get-WithValue $content 'langfuse_prompt_name' $caller.Name) -eq $expectedName) "$($caller.Name) has the wrong Bundesliga prompt name."
        Assert-True ((Get-WithValue $content 'langfuse_prompt_version' $caller.Name) -eq $expectedVersion) "$($caller.Name) has the wrong accepted Bundesliga prompt version."
    }
}

Assert-True ($wm26Count -eq 14) "Expected 14 historical WM26 callers, found $wm26Count."
Assert-True ($retiredBundesligaCount -eq 12) "Expected 12 retired Bundesliga 2025/26 callers, found $retiredBundesligaCount."
$expectedCurrentBundesligaCount = if ($arenaLunaTriadPresent) { 2 } else { 0 }
Assert-True ($currentBundesligaCount -eq $expectedCurrentBundesligaCount) "Expected $expectedCurrentBundesligaCount current Bundesliga arena Luna prediction callers, found $currentBundesligaCount."

Write-Output "Prediction workflow contract validation passed: 2 bases, $wm26Count callable WM26 callers, $retiredBundesligaCount explicitly retired Bundesliga callers, $currentBundesligaCount current Bundesliga callers."
