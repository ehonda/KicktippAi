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

.PARAMETER Project
    Run coverage for a specific test project only (e.g., "OpenAiIntegration.Tests").
    If not specified, all test projects under tests/ are run.

.EXAMPLE
    .\Generate-CoverageReport.ps1
    Runs all tests with coverage and opens the HTML report.

.EXAMPLE
    .\Generate-CoverageReport.ps1 -NoOpen
    Runs all tests with coverage without opening the report.

.EXAMPLE
    .\Generate-CoverageReport.ps1 -Project OpenAiIntegration.Tests
    Runs only the specified test project with coverage.
#>

param(
    [switch]$NoOpen,
    [string]$Project
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
if ($Project) {
    $TestProjects = @($Project)
    Write-Host "Running coverage for: $Project" -ForegroundColor Cyan
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

# Inject mobile-friendly CSS override
Write-Host "Applying mobile-friendly CSS..." -ForegroundColor Cyan
$MobileOverrideCss = Join-Path $RepoRoot ".github\styles\coverage-mobile-override.css"
$ReportCss = Join-Path $ReportDir "mobile-override.css"
Copy-Item $MobileOverrideCss $ReportCss

# Add link to mobile override CSS in index.html
$IndexPath = Join-Path $ReportDir "index.html"
$IndexContent = Get-Content $IndexPath -Raw
$CssLink = '<link rel="stylesheet" type="text/css" href="mobile-override.css" />'
$IndexContent = $IndexContent -replace '(<link rel="stylesheet" type="text/css" href="report.css" />)', "`$1`n$CssLink"
Set-Content $IndexPath $IndexContent -NoNewline

Write-Host "`nCoverage report generated at: $IndexPath" -ForegroundColor Green

# Open report in browser
if (-not $NoOpen) {
    Write-Host "Opening report in browser..." -ForegroundColor Cyan
    Start-Process $IndexPath
}
