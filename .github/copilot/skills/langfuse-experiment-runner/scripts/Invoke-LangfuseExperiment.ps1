[CmdletBinding(DefaultParameterSetName = "single")]
<#
.SYNOPSIS
    Runs the Task 5 experiment flow for either a single match or a sampled canonical dataset slice.

.DESCRIPTION
    Preserves the original single-match export + legacy runner path and adds a sampled slice mode that
    exports the canonical dataset, optionally refreshes the hosted dataset, selects a deterministic sample,
    reconstructs experiment items using a relative evaluation policy, runs a single dataset run per model,
    and verifies ingestion via the Langfuse API skill.
#>
param(
    [Parameter(ParameterSetName = "single", Mandatory)]
    [string]$HomeTeam,

    [Parameter(ParameterSetName = "single", Mandatory)]
    [string]$AwayTeam,

    [Parameter(ParameterSetName = "single", Mandatory)]
    [int]$Matchday,

    [Parameter(ParameterSetName = "single", Mandatory)]
    [string]$EvaluationTime,

    [Parameter(ParameterSetName = "slice", Mandatory)]
    [switch]$SampleCanonicalDataset,

    [Parameter(ParameterSetName = "slice")]
    [int]$SampleSize = 10,

    [Parameter(ParameterSetName = "slice")]
    [Nullable[int]]$SampleSeed,

    [Parameter(ParameterSetName = "slice")]
    [string]$EvaluationPolicyKind = "relative",

    [Parameter(ParameterSetName = "slice")]
    [string]$EvaluationPolicyOffset = "-12:00:00",

    [Parameter(ParameterSetName = "slice")]
    [string]$PromptKey = "prompt-v1",

    [Parameter(ParameterSetName = "slice")]
    [string]$SliceKind = "random-sample",

    [Parameter(ParameterSetName = "slice")]
    [string]$SampleMethod = "random-sample",

    [Parameter(ParameterSetName = "slice")]
    [string]$Matchdays,

    [string]$CommunityContext = "pes-squad",

    [string[]]$Models = @("o3", "gpt-5-nano"),

    [Nullable[int]]$BatchSize,

    [int]$Repetitions = 5,

    [string]$DatasetName = "match-predictions/bundesliga-2025-26/pes-squad",

    [string]$RunNamePrefix = "task-5",

    [string]$OutputDirectory = "artifacts/langfuse-runner-spike/runs",

    [switch]$WithJustification,

    [switch]$ReplaceRun,

    [switch]$SkipDatasetRefresh,

    [switch]$SkipLangfuseVerification
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotenvx -ErrorAction SilentlyContinue)) {
    Write-Error "dotenvx is not installed or not on PATH. Install via: winget install dotenvx"
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "../../../../..")
$envFilePath = Join-Path $solutionRoot "../KicktippAi.Secrets/src/Orchestrator/.env"
$runnerDirectory = Join-Path $solutionRoot "tools/langfuse-runner-spike"
$langfuseApiScriptPath = Join-Path $solutionRoot ".github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1"
$outputDirectoryPath = Join-Path $solutionRoot $OutputDirectory

if (-not (Test-Path $envFilePath)) {
    Write-Error "Secrets .env file not found. Expected at: $envFilePath"
}

if (-not (Test-Path $langfuseApiScriptPath)) {
    Write-Error "Langfuse API skill script not found. Expected at: $langfuseApiScriptPath"
}

$envFilePath = (Resolve-Path $envFilePath).Path
New-Item -ItemType Directory -Force -Path $outputDirectoryPath | Out-Null

if ($Models.Count -eq 0) {
    Write-Error "At least one model must be provided."
}

function Get-Slug([string]$Value) {
    return (($Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-"))
}

function Invoke-DotenvCommand([string]$WorkingDirectory, [string[]]$CommandArguments) {
    Push-Location $WorkingDirectory
    try {
        $output = & dotenvx run -f $envFilePath -- @CommandArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code ${LASTEXITCODE}: $($CommandArguments -join ' ')"
        }

        return $output
    }
    finally {
        Pop-Location
    }
}

function Convert-RunnerOutputToJson([object[]]$RunnerOutput) {
    $jsonStartIndex = -1

    for ($index = 0; $index -lt $RunnerOutput.Count; $index++) {
        if ($RunnerOutput[$index] -eq "{") {
            $jsonStartIndex = $index
            break
        }
    }

    if ($jsonStartIndex -lt 0) {
        throw "Runner output did not contain a JSON payload. Output:`n$($RunnerOutput -join [Environment]::NewLine)"
    }

    return (($RunnerOutput[$jsonStartIndex..($RunnerOutput.Count - 1)]) -join [Environment]::NewLine) | ConvertFrom-Json -Depth 30
}

function Get-ResolvedBatchSize([string]$Mode) {
    if ($BatchSize.HasValue) {
        if ($BatchSize.Value -lt 1) {
            throw "BatchSize must be at least 1."
        }

        return $BatchSize.Value
    }

    if ($Mode -eq "slice") {
        return 10
    }

    return 8
}

function Get-RelativeEvaluationPolicyKey([string]$Offset) {
    $timeSpan = [TimeSpan]::Parse($Offset, [Globalization.CultureInfo]::InvariantCulture)
    $sign = if ($timeSpan.Ticks -lt 0) { "-" } else { "+" }
    $absolute = $timeSpan.Duration()
    $parts = @()

    if ($absolute.Days -ne 0) { $parts += "$($absolute.Days)d" }
    if ($absolute.Hours -ne 0) { $parts += "$($absolute.Hours)h" }
    if ($absolute.Minutes -ne 0) { $parts += "$($absolute.Minutes)m" }
    if ($absolute.Seconds -ne 0) { $parts += "$($absolute.Seconds)s" }

    if ($parts.Count -eq 0) {
        $parts += "0s"
    }

    return ("startsAt{0}{1}" -f $sign, ($parts -join "")).ToLowerInvariant()
}

function Get-Task5RunTimestamp() {
    return (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
}

function Get-StartedAtUtc() {
    return (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

function Get-SelectedItemIdsHash([string[]]$ItemIds) {
    $sortedIds = $ItemIds | Sort-Object
    $joined = [string]::Join("`n", $sortedIds)
    $bytes = [Text.Encoding]::UTF8.GetBytes($joined)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([Convert]::ToHexString($sha.ComputeHash($bytes))).ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Select-RandomDatasetItems([object[]]$Items, [int]$Count, [int]$Seed) {
    if ($Count -lt 1) {
        throw "SampleSize must be at least 1."
    }

    if ($Items.Count -lt $Count) {
        throw "Requested sample size $Count exceeds available dataset item count $($Items.Count)."
    }

    $buffer = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $Items) {
        $buffer.Add($item)
    }

    $random = [System.Random]::new($Seed)
    for ($index = $buffer.Count - 1; $index -gt 0; $index--) {
        $swapIndex = $random.Next($index + 1)
        $current = $buffer[$index]
        $buffer[$index] = $buffer[$swapIndex]
        $buffer[$swapIndex] = $current
    }

    return @($buffer | Select-Object -First $Count)
}

function Invoke-LangfuseApiQuery([string]$Endpoint, [hashtable]$QueryParams) {
    $output = & $langfuseApiScriptPath -Endpoint $Endpoint -QueryParams $QueryParams
    if ($LASTEXITCODE -ne 0) {
        throw "Langfuse API query failed for endpoint '$Endpoint'."
    }

    return $output | ConvertFrom-Json -Depth 30
}

function Get-LangfuseVerification([pscustomobject]$Summary, [string]$SliceKey, [string]$StartedAtUtc, [string]$Model) {
    if ($SkipLangfuseVerification) {
        return [pscustomobject]@{
            skipped = $true
            reason = "SkipLangfuseVerification"
        }
    }

    $recentTraces = Invoke-LangfuseApiQuery -Endpoint "traces" -QueryParams @{
        limit = 50
        tags = "slice:$SliceKey"
        fromTimestamp = $StartedAtUtc
    }

    $matchingTraces = @($recentTraces.data | Where-Object {
        ($_.tags -contains "slice:$SliceKey") -and ($_.tags -contains "model:$Model")
    })

    $firstTraceId = $Summary.firstExecution.traceId
    $firstTrace = if ($firstTraceId) {
        Invoke-LangfuseApiQuery -Endpoint "traces/$firstTraceId" -QueryParams @{}
    }
    else {
        $null
    }

    $generationObservations = if ($firstTraceId) {
        Invoke-LangfuseApiQuery -Endpoint "observations" -QueryParams @{
            traceId = $firstTraceId
            type = "GENERATION"
            limit = 20
        }
    }
    else {
        $null
    }

    $generationObservationCount = if ($generationObservations -and $generationObservations.data) {
        @($generationObservations.data).Count
    }
    else {
        0
    }

    return [pscustomobject]@{
        skipped = $false
        recentTraceCount = $matchingTraces.Count
        firstTraceId = $firstTraceId
        firstTraceFound = $null -ne $firstTrace
        firstTraceName = $firstTrace.name
        firstTraceTags = @($firstTrace.tags)
        generationObservationCount = $generationObservationCount
    }
}

function Invoke-LegacySingleMatchFlow {
    $resolvedBatchSize = Get-ResolvedBatchSize -Mode "single"
    if ($Repetitions -lt 1) {
        Write-Error "Repetitions must be at least 1."
    }

    $runTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $matchSlug = "$(Get-Slug $HomeTeam)-vs-$(Get-Slug $AwayTeam)"
    $communitySlug = Get-Slug $CommunityContext
    $summaries = @()

    foreach ($model in $Models) {
        $modelSlug = Get-Slug $model
        $artifactPath = Join-Path $outputDirectoryPath "$matchSlug-$modelSlug.json"
        $runName = "$RunNamePrefix`__$communitySlug`__$modelSlug`__$matchSlug`__md$('{0:00}' -f $Matchday)`__$runTimestamp"
        $runDescription = "Task 5 experiment for $HomeTeam vs $AwayTeam at $EvaluationTime"

        $exportArguments = @(
            "dotnet",
            "run",
            "--project",
            "src/Orchestrator",
            "--",
            "export-experiment-item",
            $model,
            "--community-context",
            $CommunityContext,
            "--home",
            $HomeTeam,
            "--away",
            $AwayTeam,
            "--matchday",
            $Matchday,
            "--evaluation-time",
            $EvaluationTime,
            "--output",
            $artifactPath
        )

        if ($WithJustification) {
            $exportArguments += "--with-justification"
        }

        Write-Host "Exporting experiment item for model '$model'..."
        Invoke-DotenvCommand -WorkingDirectory $solutionRoot -CommandArguments $exportArguments | Out-Null

        $runnerArguments = @(
            "node",
            "run-spike.mjs",
            "--input",
            $artifactPath,
            "--model",
            $model,
            "--dataset-name",
            $DatasetName,
            "--run-name",
            $runName,
            "--run-description",
            $runDescription,
            "--repetitions",
            $Repetitions,
            "--batch-size",
            $resolvedBatchSize
        )

        if ($ReplaceRun) {
            $runnerArguments += "--replace-run"
        }

        Write-Host "Running hosted experiment for model '$model'..."
        $runnerOutput = Invoke-DotenvCommand -WorkingDirectory $runnerDirectory -CommandArguments $runnerArguments
        $summary = Convert-RunnerOutputToJson -RunnerOutput $runnerOutput

        $summaries += [pscustomobject]@{
            mode = "single-match"
            model = $model
            artifactPath = $artifactPath
            runFamilyName = $summary.runFamilyName
            datasetRuns = $summary.datasetRuns
            aggregateScores = $summary.aggregateScores
            executionCount = $summary.executionCount
        }
    }

    return $summaries
}

function Invoke-SampledSliceFlow {
    $resolvedBatchSize = Get-ResolvedBatchSize -Mode "slice"
    $resolvedSampleSeed = if ($SampleSeed.HasValue) { $SampleSeed.Value } else { [int](Get-Date).ToUniversalTime().ToString("yyyyMMdd") }
    $evaluationPolicyKey = Get-RelativeEvaluationPolicyKey -Offset $EvaluationPolicyOffset
    $sliceKey = "random-$SampleSize-seed-$resolvedSampleSeed"
    $runTimestamp = Get-Task5RunTimestamp
    $startedAtUtc = Get-StartedAtUtc

    $datasetArtifactPath = Join-Path $outputDirectoryPath "canonical-$($CommunityContext).json"
    $exportDatasetArguments = @(
        "dotnet",
        "run",
        "--project",
        "src/Orchestrator",
        "--",
        "export-experiment-dataset",
        "--community-context",
        $CommunityContext,
        "--output",
        $datasetArtifactPath
    )

    if ($Matchdays) {
        $exportDatasetArguments += @("--matchdays", $Matchdays)
    }

    Write-Host "Exporting canonical dataset artifact..."
    Invoke-DotenvCommand -WorkingDirectory $solutionRoot -CommandArguments $exportDatasetArguments | Out-Null

    if (-not $SkipDatasetRefresh) {
        Write-Host "Refreshing hosted dataset before sampling..."
        $syncArguments = @(
            "node",
            "sync-dataset.mjs",
            "--input",
            $datasetArtifactPath,
            "--dataset-name",
            $DatasetName
        )
        Invoke-DotenvCommand -WorkingDirectory $runnerDirectory -CommandArguments $syncArguments | Out-Null
    }

    $datasetArtifact = Get-Content -Raw $datasetArtifactPath | ConvertFrom-Json -Depth 30
    $availableItems = @($datasetArtifact.items)
    $selectedItems = Select-RandomDatasetItems -Items $availableItems -Count $SampleSize -Seed $resolvedSampleSeed
    $selectedItems = @($selectedItems | Sort-Object id)
    $selectedItemIds = @($selectedItems.id)
    $selectedItemIdsHash = Get-SelectedItemIdsHash -ItemIds $selectedItemIds

    $commonRunMetadata = [ordered]@{
        runner = "task-5-first-experiment"
        task = "task-5"
        communityContext = $CommunityContext
        competition = $selectedItems[0].metadata.competition
        datasetName = $DatasetName
        promptKey = $PromptKey
        sliceKind = $SliceKind
        sliceKey = $sliceKey
        selectedItemIdsHash = $selectedItemIdsHash
        selectedItemIdsCount = $selectedItemIds.Count
        sampleSize = $selectedItems.Count
        evaluationTimestampPolicyKey = $evaluationPolicyKey
        evaluationTimestampPolicy = [ordered]@{
            kind = $EvaluationPolicyKind
            reference = "startsAt"
            offset = $EvaluationPolicyOffset
        }
        startedAtUtc = $startedAtUtc
        sampleSeed = $resolvedSampleSeed
        sampleMethod = $SampleMethod
        includeJustification = [bool]$WithJustification
        promptVersion = $PromptKey
        sourceDatasetKind = "canonical"
    }

    $summaries = @()
    foreach ($model in $Models) {
        $modelSlug = Get-Slug $model
        $runName = "$RunNamePrefix`__$(Get-Slug $CommunityContext)`__$modelSlug`__$(Get-Slug $PromptKey)`__$sliceKey`__$evaluationPolicyKey`__$runTimestamp"
        $runDescription = "Task 5 slice run for $model on $sliceKey"
        $modelDirectory = Join-Path $outputDirectoryPath "$sliceKey-$modelSlug"
        New-Item -ItemType Directory -Force -Path $modelDirectory | Out-Null

        $inputPaths = @()
        foreach ($selectedItem in $selectedItems) {
            $artifactPath = Join-Path $modelDirectory "$($selectedItem.id).json"
            $inputPaths += $artifactPath

            $exportArguments = @(
                "dotnet",
                "run",
                "--project",
                "src/Orchestrator",
                "--",
                "export-experiment-item",
                $model,
                "--community-context",
                $CommunityContext,
                "--home",
                $selectedItem.metadata.homeTeam,
                "--away",
                $selectedItem.metadata.awayTeam,
                "--matchday",
                $selectedItem.metadata.matchday,
                "--evaluation-policy-kind",
                $EvaluationPolicyKind,
                "--evaluation-policy-offset",
                $EvaluationPolicyOffset,
                "--output",
                $artifactPath
            )

            if ($WithJustification) {
                $exportArguments += "--with-justification"
            }

            Write-Host "Exporting sampled experiment item '$($selectedItem.id)' for model '$model'..."
            Invoke-DotenvCommand -WorkingDirectory $solutionRoot -CommandArguments $exportArguments | Out-Null
        }

        $runMetadata = [ordered]@{}
        foreach ($entry in $commonRunMetadata.GetEnumerator()) {
            $runMetadata[$entry.Key] = $entry.Value
        }
        $runMetadata.model = $model
        $runMetadata.batchSize = $resolvedBatchSize

        $runMetadataPath = Join-Path $modelDirectory "run-metadata.json"
        $runMetadata | ConvertTo-Json -Depth 20 | Set-Content -Path $runMetadataPath -Encoding utf8

        $runnerArguments = @("node", "run-task5-slice.mjs")
        foreach ($inputPath in $inputPaths) {
            $runnerArguments += @("--input", $inputPath)
        }

        $runnerArguments += @(
            "--model",
            $model,
            "--dataset-name",
            $DatasetName,
            "--run-name",
            $runName,
            "--run-description",
            $runDescription,
            "--batch-size",
            $resolvedBatchSize,
            "--run-metadata-file",
            $runMetadataPath
        )

        if ($ReplaceRun) {
            $runnerArguments += "--replace-run"
        }

        Write-Host "Running sampled Task 5 slice for model '$model'..."
        $runnerOutput = Invoke-DotenvCommand -WorkingDirectory $runnerDirectory -CommandArguments $runnerArguments
        $summary = Convert-RunnerOutputToJson -RunnerOutput $runnerOutput
        $verification = Get-LangfuseVerification -Summary $summary -SliceKey $sliceKey -StartedAtUtc $startedAtUtc -Model $model

        $summaries += [pscustomobject]@{
            mode = "sampled-slice"
            model = $model
            runName = $runName
            runMetadataPath = $runMetadataPath
            datasetRuns = $summary.datasetRuns
            aggregateScores = $summary.aggregateScores
            executionCount = $summary.executionCount
            selectedItemIds = $selectedItemIds
            selectedItemIdsHash = $selectedItemIdsHash
            sliceKey = $sliceKey
            sampleSeed = $resolvedSampleSeed
            evaluationTimestampPolicyKey = $evaluationPolicyKey
            verification = $verification
        }
    }

    return [pscustomobject]@{
        mode = "sampled-slice"
        communityContext = $CommunityContext
        datasetName = $DatasetName
        sliceKind = $SliceKind
        sliceKey = $sliceKey
        sampleSize = $selectedItems.Count
        sampleSeed = $resolvedSampleSeed
        batchSize = $resolvedBatchSize
        evaluationTimestampPolicyKey = $evaluationPolicyKey
        evaluationTimestampPolicy = $commonRunMetadata.evaluationTimestampPolicy
        selectedItemIds = $selectedItemIds
        selectedItemIdsHash = $selectedItemIdsHash
        startedAtUtc = $startedAtUtc
        modelRuns = $summaries
    }
}

if ($PSCmdlet.ParameterSetName -eq "slice") {
    Invoke-SampledSliceFlow | ConvertTo-Json -Depth 20
}
else {
    Invoke-LegacySingleMatchFlow | ConvertTo-Json -Depth 20
}
