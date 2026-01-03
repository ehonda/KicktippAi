<#
.SYNOPSIS
    Generates code coverage report for all test projects.

.DESCRIPTION
    This script:
    1. Runs all test projects with coverage collection using dotnet-coverage
    2. Merges coverage reports from all test projects
    3. Generates an HTML report using ReportGenerator
    4. Optionally opens the report in the default browser

.PARAMETER NoOpen
    If specified, the HTML report will not be automatically opened in the browser.

.PARAMETER Projects
    Run coverage for specific test projects only (e.g., "OpenAiIntegration.Tests", "Core.Tests").
    If not specified, all test projects under tests/ are run.

.EXAMPLE
    .\Generate-CoverageReport.ps1
    Runs all tests with coverage and opens the HTML report.

.EXAMPLE
    .\Generate-CoverageReport.ps1 -NoOpen
    Runs all tests with coverage without opening the report.

.EXAMPLE
    .\Generate-CoverageReport.ps1 -Projects OpenAiIntegration.Tests
    Runs only the specified test project with coverage.

.EXAMPLE
    .\Generate-CoverageReport.ps1 -Projects OpenAiIntegration.Tests,Core.Tests
    Runs the specified test projects with coverage.
#>

param(
    [switch]$NoOpen,
    [string[]]$Projects
)

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = $PSScriptRoot
$CoverageDir = Join-Path $RepoRoot "coverage"
$ReportDir = Join-Path $RepoRoot "coverage-report"
$MergedReport = Join-Path $CoverageDir "merged.cobertura.xml"

# Ensure tools are restored
Write-Host "Restoring dotnet tools..." -ForegroundColor Cyan
dotnet tool restore

# Clean previous coverage data and reports
if (Test-Path $CoverageDir) {
    Remove-Item $CoverageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $CoverageDir | Out-Null

if (Test-Path $ReportDir) {
    Remove-Item $ReportDir -Recurse -Force
}

# Discover test projects
if ($Projects) {
    $TestProjects = $Projects
    Write-Host "Running coverage for: $($Projects -join ', ')" -ForegroundColor Cyan
} else {
    $TestProjects = Get-ChildItem -Path (Join-Path $RepoRoot "tests") -Directory | 
        Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } |
        Select-Object -ExpandProperty Name
    Write-Host "Discovered test projects: $($TestProjects -join ', ')" -ForegroundColor Cyan
}

# Run tests with coverage for each project
$CoverageFiles = @()
foreach ($TestProject in $TestProjects) {
    Write-Host "`nRunning tests for $TestProject..." -ForegroundColor Yellow
    
    $CoverageOutput = Join-Path $CoverageDir "$TestProject.cobertura.xml"
    $CoverageFiles += $CoverageOutput
    
    dotnet dotnet-coverage collect `
        -s coverage.settings.json `
        -f cobertura `
        -o $CoverageOutput `
        "dotnet run --project tests/$TestProject --configuration Release"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed for $TestProject"
        exit 1
    }
}

# Merge coverage reports if multiple projects
if ($CoverageFiles.Count -gt 1) {
    Write-Host "`nMerging coverage reports..." -ForegroundColor Cyan
    
    # Pass files as separate arguments using array splatting
    $MergeArgs = @("merge") + $CoverageFiles + @("-f", "cobertura", "-o", $MergedReport)
    & dotnet dotnet-coverage @MergeArgs
    
    $ReportSource = $MergedReport
} else {
    $ReportSource = $CoverageFiles[0]
}

# Generate HTML report
Write-Host "`nGenerating HTML report..." -ForegroundColor Cyan

dotnet reportgenerator `
    "-reports:$ReportSource" `
    "-targetdir:$ReportDir" `
    "-reporttypes:Html"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate HTML report"
    exit 1
}

# Inject mobile-friendly CSS override using shared script
$ApplyMobileStylesScript = Join-Path $RepoRoot ".github/scripts/Apply-CoverageMobileStyles.ps1"
& $ApplyMobileStylesScript -ReportDir $ReportDir

$IndexPath = Join-Path $ReportDir "index.html"
Write-Host "`nCoverage report generated at: $IndexPath" -ForegroundColor Green

# Open report in browser
if (-not $NoOpen) {
    Write-Host "Opening report in browser..." -ForegroundColor Cyan
    Start-Process $IndexPath
}
