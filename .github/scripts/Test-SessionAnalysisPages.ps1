[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".tmp"))
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $scratchRoot "pages-discovery-$([guid]::NewGuid().ToString('N'))"))
if (-not $testRoot.StartsWith($scratchRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Unsafe Pages discovery test path: $testRoot"
}

$manifestDir = Join-Path $testRoot "manifests"
$sessionDir = Join-Path $testRoot "session-analysis"
$outputDir = Join-Path $testRoot "site"
$duplicateOutputDir = Join-Path $testRoot "duplicate-site"

try
{
    New-Item -ItemType Directory -Path $manifestDir, $sessionDir -Force | Out-Null
    Copy-Item -Path (Join-Path $repoRoot "docs\codex\session-analysis\reports\*.report.json") -Destination $manifestDir
    Copy-Item -Path (Join-Path $repoRoot "session-analysis\*") -Destination $sessionDir -Recurse -Force

    $newReportDir = Join-Path $sessionDir "manifest-discovery-test"
    New-Item -ItemType Directory -Path $newReportDir -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $newReportDir "index.html") -Encoding utf8 -Value "<!doctype html><html><body>discovered</body></html>"

    $manifest = [ordered]@{
        schema_version = 1
        id = "manifest-discovery-test"
        title = "Manifest discovery test"
        summary = "A fixture report discovered without editing the Pages builder."
        eyebrow = "Discovery fixture"
        published_at = "2099-01-01"
        session_kind = "orchestration"
        source_path = "docs/codex/session-analysis"
        site_path = "session-analysis/manifest-discovery-test"
        html_file = "index.html"
        focus_ids = @()
        analysis = $null
    }
    $manifestPath = Join-Path $manifestDir "manifest-discovery-test.report.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
        -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
        -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
        -SessionAnalysisDir $sessionDir `
        -SessionAnalysisManifestDir $manifestDir `
        -OutputDir $outputDir

    $index = Get-Content -LiteralPath (Join-Path $outputDir "index.html") -Raw
    if ($index -notmatch "href='session-analysis/manifest-discovery-test/index.html'" -or
        $index -notmatch "Manifest discovery test")
    {
        throw "A valid added manifest was not discovered in the Pages index"
    }
    $expectedCards = @(Get-ChildItem -LiteralPath $manifestDir -File -Filter "*.report.json").Count
    $actualCards = [regex]::Matches($index, "class='card' href='session-analysis/").Count
    if ($actualCards -ne $expectedCards)
    {
        throw "Expected $expectedCards session-analysis cards, found $actualCards"
    }

    $duplicate = [ordered]@{} + $manifest
    $duplicate | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $manifestDir "duplicate.report.json") -Encoding utf8
    $duplicateRejected = $false
    try
    {
        & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
            -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
            -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
            -SessionAnalysisDir $sessionDir `
            -SessionAnalysisManifestDir $manifestDir `
            -OutputDir $duplicateOutputDir
    }
    catch
    {
        if ($_.Exception.Message -notmatch "Duplicate session-analysis report ID")
        {
            throw
        }
        $duplicateRejected = $true
    }
    if (-not $duplicateRejected)
    {
        throw "Duplicate session-analysis report ID was accepted"
    }

    Write-Host "Session-analysis Pages discovery tests passed"
}
finally
{
    if (Test-Path -LiteralPath $testRoot)
    {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
