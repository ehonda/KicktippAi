<#
.SYNOPSIS
    Exports exact-timestamp experiment items and runs hosted Langfuse dataset experiments.

.DESCRIPTION
    For each requested model, this script exports a single experiment item from the orchestrator,
    then invokes the JS Langfuse runner with serial-first batching and hosted dataset linkage.

.EXAMPLE
    .\.github\copilot\skills\langfuse-experiment-runner\scripts\Invoke-LangfuseExperiment.ps1 `
        -HomeTeam "VfB Stuttgart" `
        -AwayTeam "RB Leipzig" `
        -Matchday 26 `
        -EvaluationTime "2026-03-15T12:00:00 Europe/Berlin (+01)" `
        -ReplaceRun
#>
param(
    [Parameter(Mandatory)]
    [string]$HomeTeam,

    [Parameter(Mandatory)]
    [string]$AwayTeam,

    [Parameter(Mandatory)]
    [int]$Matchday,

    [Parameter(Mandatory)]
    [string]$EvaluationTime,

    [string]$CommunityContext = "pes-squad",

    [string[]]$Models = @("o3", "gpt-5-nano"),

    [int]$Repetitions = 5,

    [int]$BatchSize = 8,

    [string]$DatasetName = "match-predictions/bundesliga-2025-26/pes-squad",

    [string]$RunNamePrefix = "task-5",

    [string]$OutputDirectory = "artifacts/langfuse-runner-spike/runs",

    [switch]$WithJustification,

    [switch]$ReplaceRun
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotenvx -ErrorAction SilentlyContinue)) {
    Write-Error "dotenvx is not installed or not on PATH. Install via: winget install dotenvx"
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "../../../../..")
$envFilePath = Join-Path $solutionRoot "../KicktippAi.Secrets/src/Orchestrator/.env"

if (-not (Test-Path $envFilePath)) {
    Write-Error "Secrets .env file not found. Expected at: $envFilePath"
}

$envFilePath = (Resolve-Path $envFilePath).Path
$runnerDirectory = Join-Path $solutionRoot "tools/langfuse-runner-spike"
$outputDirectoryPath = Join-Path $solutionRoot $OutputDirectory

if ($Repetitions -lt 1) {
    Write-Error "Repetitions must be at least 1."
}

if ($BatchSize -lt 1) {
    Write-Error "BatchSize must be at least 1."
}

if ($Models.Count -eq 0) {
    Write-Error "At least one model must be provided."
}

New-Item -ItemType Directory -Force -Path $outputDirectoryPath | Out-Null

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

    return (($RunnerOutput[$jsonStartIndex..($RunnerOutput.Count - 1)]) -join [Environment]::NewLine) | ConvertFrom-Json
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
        $BatchSize
    )

    if ($ReplaceRun) {
        $runnerArguments += "--replace-run"
    }

    Write-Host "Running hosted experiment for model '$model'..."
    $runnerOutput = Invoke-DotenvCommand -WorkingDirectory $runnerDirectory -CommandArguments $runnerArguments

    $summary = Convert-RunnerOutputToJson -RunnerOutput $runnerOutput
    $summaries += [pscustomobject]@{
        Model = $model
        ArtifactPath = $artifactPath
        RunFamilyName = $summary.runFamilyName
        DatasetRuns = $summary.datasetRuns
        AggregateScores = $summary.aggregateScores
        ExecutionCount = $summary.executionCount
    }
}

$summaries | ConvertTo-Json -Depth 10
