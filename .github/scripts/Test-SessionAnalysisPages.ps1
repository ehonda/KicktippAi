[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedTextSha256 {
    param([string]$Path)

    $content = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($content)
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($sha.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally
    {
        $sha.Dispose()
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".tmp"))
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $scratchRoot "pages-discovery-$([guid]::NewGuid().ToString('N'))"))
if (-not $testRoot.StartsWith($scratchRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Unsafe Pages discovery test path: $testRoot"
}

$testRepo = Join-Path $testRoot "repo"
$manifestDir = Join-Path $testRepo "docs\codex\session-analysis\reports"
$sessionDir = Join-Path $testRoot "session-analysis"
$outputDir = Join-Path $testRoot "site"
$missingArtifactOutputDir = Join-Path $testRoot "missing-artifact-site"
$duplicateOutputDir = Join-Path $testRoot "duplicate-site"
$traversalOutputDir = Join-Path $testRoot "traversal-site"
$privacyOutputDir = Join-Path $testRoot "privacy-site"

try
{
    New-Item -ItemType Directory -Path $manifestDir, $sessionDir -Force | Out-Null
    Copy-Item -Path (Join-Path $repoRoot "docs\codex\session-analysis\reports\*.report.json") -Destination $manifestDir
    Copy-Item -Path (Join-Path $repoRoot "session-analysis\*") -Destination $sessionDir -Recurse -Force

    foreach ($legacyManifestPath in Get-ChildItem -LiteralPath $manifestDir -File -Filter "*.report.json")
    {
        $legacyManifest = Get-Content -LiteralPath $legacyManifestPath.FullName -Raw | ConvertFrom-Json
        $legacySource = Join-Path $testRepo ([string]$legacyManifest.source_path -replace "/", [IO.Path]::DirectorySeparatorChar)
        New-Item -ItemType Directory -Path $legacySource -Force | Out-Null
    }

    $newReportDir = Join-Path $sessionDir "manifest-discovery-test"
    New-Item -ItemType Directory -Path $newReportDir -Force | Out-Null
    $unmanifestedDir = Join-Path $sessionDir "not-in-manifest"
    New-Item -ItemType Directory -Path $unmanifestedDir -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $unmanifestedDir "index.html") -Encoding utf8 -Value "unmanifested"
    $csp = "default-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'; worker-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; font-src data:; media-src data:"
    Set-Content -LiteralPath (Join-Path $newReportDir "index.html") -Encoding utf8 -Value "<!doctype html><html><head><meta http-equiv=`"Content-Security-Policy`" content=`"$csp`"></head><body>discovered</body></html>"

    $manifest = Get-Content -LiteralPath (Join-Path $repoRoot ".agents\skills\session-analysis\references\p0-closeout-parity.report.json") -Raw | ConvertFrom-Json
    $manifest.id = "manifest-discovery-test"
    $manifest.title = "Manifest discovery test"
    $manifest.summary = "A fixture report discovered without editing the Pages builder."
    $manifest.eyebrow = "Discovery fixture"
    $manifest.published_at = "2099-01-01"
    $manifest.source_path = "docs/codex/manifest-discovery-test"
    $manifest.site_path = "session-analysis/manifest-discovery-test"
    $manifest.focus_ids = @()
    $manifest.legacy = $false
    $manifest.analysis_file = "docs/codex/manifest-discovery-test/data/analysis.json"
    $manifest.analysis.snapshot_lock = "docs/codex/manifest-discovery-test/data/snapshot-lock.json"
    $manifestPath = Join-Path $manifestDir "manifest-discovery-test.report.json"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    $sourceDir = Join-Path $testRepo "docs\codex\manifest-discovery-test\data"
    New-Item -ItemType Directory -Path $sourceDir -Force | Out-Null
    $lockPath = Join-Path $sourceDir "snapshot-lock.json"
    $snapshotLock = @{
        schema_version = 1
        root_thread_id = $manifest.analysis.root_thread_id
        event_cutoff_at = $manifest.analysis.event_cutoff_at
        threads = @(@{
            thread_id = $manifest.analysis.root_thread_id
            log_file = $manifest.analysis.root_log_name
            included_bytes = 0
            included_records = 0
            sha256 = "0" * 64
        })
    }
    $snapshotLock | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $lockPath -Encoding utf8
    $relativeManifest = [IO.Path]::GetRelativePath($testRepo, $manifestPath).Replace("\", "/")
    $artifact = @{
        generated_at = $manifest.analysis.generated_at
        source = @{
            complete_message_bodies_included = $false
            report_manifest = $relativeManifest
            report_manifest_sha256 = Get-NormalizedTextSha256 -Path $manifestPath
            focus_ids = @()
            root_thread_id = $manifest.analysis.root_thread_id
            root_log = $manifest.analysis.root_log_name
            event_cutoff_at = $manifest.analysis.event_cutoff_at
            repository = $manifest.analysis.repository
            bounded_excerpts_included = $manifest.analysis.privacy.include_bounded_excerpts
            snapshot_lock = $manifest.analysis.snapshot_lock
            snapshot_lock_sha256 = Get-NormalizedTextSha256 -Path $lockPath
        }
    }
    $artifact | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $sourceDir "analysis.json") -Encoding utf8

    & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
        -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
        -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
        -SessionAnalysisDir $sessionDir `
        -SessionAnalysisManifestDir $manifestDir `
        -RepositoryRoot $testRepo `
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
    if (Test-Path -LiteralPath (Join-Path $outputDir "session-analysis\not-in-manifest"))
    {
        throw "An unmanifested session-analysis directory was published"
    }

    $originalSitePath = $manifest.site_path
    $manifest.site_path = "session-analysis/../escape"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $traversalRejected = $false
    try
    {
        & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
            -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
            -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
            -SessionAnalysisDir $sessionDir `
            -SessionAnalysisManifestDir $manifestDir `
            -RepositoryRoot $testRepo `
            -OutputDir $traversalOutputDir
    }
    catch
    {
        if ($_.Exception.Message -notmatch "Unsafe site_path|safe normalized relative path")
        {
            throw
        }
        $traversalRejected = $true
    }
    finally
    {
        $manifest.site_path = $originalSitePath
        $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }
    if (-not $traversalRejected)
    {
        throw "A traversal-bearing site_path was accepted"
    }

    $originalSummary = $manifest.summary
    $manifest.summary = "Leaked /var/root"
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $privacyRejected = $false
    try
    {
        & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
            -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
            -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
            -SessionAnalysisDir $sessionDir `
            -SessionAnalysisManifestDir $manifestDir `
            -RepositoryRoot $testRepo `
            -OutputDir $privacyOutputDir
    }
    catch
    {
        if ($_.Exception.Message -notmatch "Private path or possible credential")
        {
            throw
        }
        $privacyRejected = $true
    }
    finally
    {
        $manifest.summary = $originalSummary
        $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }
    if (-not $privacyRejected)
    {
        throw "A manifest containing a private home path was accepted"
    }

    if (-not $IsWindows)
    {
        $caseVariantRoot = Join-Path $testRoot "Session-Analysis"
        $caseVariantTarget = Join-Path $caseVariantRoot "case-link"
        $caseVariantReport = Join-Path $caseVariantTarget "nested"
        $caseLink = Join-Path $sessionDir "case-link"
        New-Item -ItemType Directory -Path $caseVariantReport -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $newReportDir "index.html") -Destination (Join-Path $caseVariantReport "index.html")
        New-Item -ItemType SymbolicLink -Path $caseLink -Target $caseVariantTarget | Out-Null
        $manifest.site_path = "session-analysis/case-link/nested"
        $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
        $caseEscapeRejected = $false
        try
        {
            & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
                -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
                -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
                -SessionAnalysisDir $sessionDir `
                -SessionAnalysisManifestDir $manifestDir `
                -RepositoryRoot $testRepo `
                -OutputDir (Join-Path $testRoot "case-escape-site")
        }
        catch
        {
            if ($_.Exception.Message -notmatch "resolves outside its allowed root")
            {
                throw
            }
            $caseEscapeRejected = $true
        }
        finally
        {
            $manifest.site_path = $originalSitePath
            $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
            Remove-Item -LiteralPath $caseLink -Force
            Remove-Item -LiteralPath $caseVariantRoot -Recurse -Force
        }
        if (-not $caseEscapeRejected)
        {
            throw "A case-variant symlink escape was accepted"
        }
    }

    $analysisPath = Join-Path $sourceDir "analysis.json"
    $analysisBackup = Join-Path $sourceDir "analysis.backup"
    Move-Item -LiteralPath $analysisPath -Destination $analysisBackup
    $missingArtifactRejected = $false
    try
    {
        & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
            -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
            -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
            -SessionAnalysisDir $sessionDir `
            -SessionAnalysisManifestDir $manifestDir `
            -RepositoryRoot $testRepo `
            -OutputDir $missingArtifactOutputDir
    }
    catch
    {
        if ($_.Exception.Message -notmatch "Normalized session-analysis artifact is missing")
        {
            throw
        }
        $missingArtifactRejected = $true
    }
    finally
    {
        Move-Item -LiteralPath $analysisBackup -Destination $analysisPath
    }
    if (-not $missingArtifactRejected)
    {
        throw "A future report without its normalized artifact was accepted"
    }

    $duplicate = $manifest | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $duplicate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $manifestDir "zz-duplicate.report.json") -Encoding utf8
    $duplicateRejected = $false
    try
    {
        & (Join-Path $PSScriptRoot "Build-PagesSite.ps1") `
            -CoverageReportDir (Join-Path $testRoot "missing-coverage") `
            -ExperimentAnalysisDir (Join-Path $testRoot "missing-experiments") `
            -SessionAnalysisDir $sessionDir `
            -SessionAnalysisManifestDir $manifestDir `
            -RepositoryRoot $testRepo `
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
