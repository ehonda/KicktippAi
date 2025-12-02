<#
.SYNOPSIS
    Applies mobile-friendly CSS overrides to ReportGenerator HTML reports.

.DESCRIPTION
    This script:
    1. Copies the mobile override CSS to the report directory
    2. Injects a link to the CSS in all HTML files

.PARAMETER ReportDir
    The directory containing the generated HTML coverage report.

.PARAMETER CssSourcePath
    Optional path to the mobile override CSS file.
    Defaults to .github/styles/coverage-mobile-override.css relative to the repo root.

.EXAMPLE
    ./Apply-CoverageMobileStyles.ps1 -ReportDir ./coverage-report

.EXAMPLE
    ./Apply-CoverageMobileStyles.ps1 -ReportDir ./coverage-report -CssSourcePath ./custom-mobile.css
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ReportDir,
    
    [string]$CssSourcePath
)

$ErrorActionPreference = "Stop"

# Determine repo root (script is in .github/scripts/)
$ScriptDir = $PSScriptRoot
$RepoRoot = (Get-Item $ScriptDir).Parent.Parent.FullName

# Default CSS source path if not specified
if (-not $CssSourcePath) {
    $CssSourcePath = Join-Path $RepoRoot ".github/styles/coverage-mobile-override.css"
}

# Validate paths
if (-not (Test-Path $ReportDir)) {
    Write-Error "Report directory not found: $ReportDir"
    exit 1
}

if (-not (Test-Path $CssSourcePath)) {
    Write-Error "CSS source file not found: $CssSourcePath"
    exit 1
}

Write-Host "Applying mobile-friendly CSS to coverage report..." -ForegroundColor Cyan
Write-Host "  Report directory: $ReportDir"
Write-Host "  CSS source: $CssSourcePath"

# Copy CSS file to report directory
$DestinationCss = Join-Path $ReportDir "mobile-override.css"
Copy-Item $CssSourcePath $DestinationCss
Write-Host "  Copied CSS to: $DestinationCss" -ForegroundColor Gray

# Inject CSS link into all HTML files
$CssLink = '<link rel="stylesheet" type="text/css" href="mobile-override.css" />'
$HtmlFiles = Get-ChildItem -Path $ReportDir -Filter "*.html"
$ModifiedCount = 0

foreach ($HtmlFile in $HtmlFiles) {
    $Content = Get-Content $HtmlFile.FullName -Raw
    
    # Only modify if the link isn't already present
    if ($Content -notmatch 'mobile-override\.css') {
        $Content = $Content -replace '(<link rel="stylesheet" type="text/css" href="report.css" />)', "`$1`n$CssLink"
        Set-Content $HtmlFile.FullName $Content -NoNewline
        $ModifiedCount++
    }
}

Write-Host "  Modified $ModifiedCount HTML file(s)" -ForegroundColor Gray
Write-Host "Mobile-friendly CSS applied successfully." -ForegroundColor Green
