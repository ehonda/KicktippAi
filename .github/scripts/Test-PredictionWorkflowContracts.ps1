[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch] $SkipHostileShellSimulation
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

    $match = [regex]::Match(
        $Content,
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
$matchBasePath = Join-Path $workflowDirectory 'base-matchday-predictions.yml'
$bonusBasePath = Join-Path $workflowDirectory 'base-bonus-predictions.yml'
$matchBase = Get-Content -Raw -LiteralPath $matchBasePath
$bonusBase = Get-Content -Raw -LiteralPath $bonusBasePath

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
        $content.Contains('uses: ./.github/workflows/base-matchday-predictions.yml', [StringComparison]::Ordinal) -or
        $content.Contains('uses: ./.github/workflows/base-bonus-predictions.yml', [StringComparison]::Ordinal)
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
    $triggerBlock = [regex]::Match($content, '(?ms)^on:\r?\n(?<body>.*?)^jobs:').Groups['body'].Value
    Assert-True ([regex]::IsMatch($triggerBlock, '(?m)^  workflow_call:\s*$')) "$($caller.Name) must remain workflow_call-only."
    Assert-True (-not [regex]::IsMatch($triggerBlock, '(?m)^  (schedule|workflow_dispatch):')) "$($caller.Name) must not activate schedule or workflow_dispatch."

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

Write-Output "Prediction workflow contract validation passed: 2 bases, $wm26Count callable WM26 callers, $retiredBundesligaCount explicitly retired Bundesliga callers, $currentBundesligaCount current Bundesliga callers."
