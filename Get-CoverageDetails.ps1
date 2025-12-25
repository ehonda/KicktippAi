<#
.SYNOPSIS
    Extracts and displays code coverage details from cobertura XML reports.

.DESCRIPTION
    This script parses cobertura XML coverage reports and displays coverage
    details in a terminal-friendly format. It supports:
    - Filtering by assembly name
    - Overview of line/branch coverage
    - Detailed breakdown by class
    - Showing uncovered line numbers

.PARAMETER Assembly
    Filter results to a specific assembly name (supports wildcards).
    If not specified, shows all assemblies.

.PARAMETER Detailed
    Show detailed breakdown by class within each assembly.

.PARAMETER ShowUncovered
    Display uncovered line numbers grouped by class.

.PARAMETER ReportPath
    Path to a cobertura XML report file. If not specified, auto-detects:
    1. coverage/merged.cobertura.xml (if exists)
    2. First *.cobertura.xml file in coverage/ directory

.EXAMPLE
    .\Get-CoverageDetails.ps1
    Shows coverage summary for all assemblies.

.EXAMPLE
    .\Get-CoverageDetails.ps1 -Assembly FirebaseAdapter
    Shows coverage summary for the FirebaseAdapter assembly.

.EXAMPLE
    .\Get-CoverageDetails.ps1 -Assembly Core -Detailed
    Shows detailed class-by-class breakdown for assemblies matching "Core".

.EXAMPLE
    .\Get-CoverageDetails.ps1 -Assembly FirebaseAdapter -ShowUncovered
    Shows uncovered line numbers for FirebaseAdapter classes.

.EXAMPLE
    .\Get-CoverageDetails.ps1 -ReportPath coverage/FirebaseAdapter.Tests.cobertura.xml
    Uses a specific coverage report file.
#>

param(
    [string]$Assembly,
    [switch]$Detailed,
    [switch]$ShowUncovered,
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = $PSScriptRoot
$CoverageDir = Join-Path $RepoRoot "coverage"

# Auto-detect report path if not specified
if (-not $ReportPath) {
    $MergedReport = Join-Path $CoverageDir "merged.cobertura.xml"
    if (Test-Path $MergedReport) {
        $ReportPath = $MergedReport
    } else {
        $FirstReport = Get-ChildItem -Path $CoverageDir -Filter "*.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($FirstReport) {
            $ReportPath = $FirstReport.FullName
        } else {
            Write-Error "No coverage report found in '$CoverageDir'. Run Generate-CoverageReport.ps1 first."
            exit 1
        }
    }
}

if (-not (Test-Path $ReportPath)) {
    Write-Error "Coverage report not found: $ReportPath"
    exit 1
}

Write-Host "Reading coverage report: $ReportPath" -ForegroundColor Cyan
Write-Host ""

# Load XML
[xml]$CoverageXml = Get-Content -Path $ReportPath

# Helper function to format percentage (invariant culture for consistent decimal dots)
function Format-Percentage {
    param([double]$Rate)
    return ($Rate * 100).ToString("N2", [System.Globalization.CultureInfo]::InvariantCulture) + "%"
}

# Helper function to convert absolute path to workspace-relative
function Get-RelativePath {
    param([string]$AbsolutePath)
    if ($AbsolutePath.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $AbsolutePath.Substring($RepoRoot.Length).TrimStart('\', '/')
    }
    return $AbsolutePath
}

# Get all packages (assemblies)
$Packages = $CoverageXml.coverage.packages.package

# Filter by assembly name if specified
if ($Assembly) {
    $Packages = $Packages | Where-Object { $_.name -like "*$Assembly*" }
    if (-not $Packages) {
        Write-Host "No assemblies found matching '$Assembly'" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Available assemblies:" -ForegroundColor Cyan
        $CoverageXml.coverage.packages.package | ForEach-Object {
            Write-Host "  - $($_.name)"
        }
        exit 0
    }
}

# Display overall summary from root coverage element
$Coverage = $CoverageXml.coverage
Write-Host "=== Overall Coverage ===" -ForegroundColor Green
Write-Host "Lines:    $(Format-Percentage ([double]$Coverage.'line-rate')) ($($Coverage.'lines-covered')/$($Coverage.'lines-valid'))"
Write-Host "Branches: $(Format-Percentage ([double]$Coverage.'branch-rate')) ($($Coverage.'branches-covered')/$($Coverage.'branches-valid'))"
Write-Host ""

# Assembly summary table
Write-Host "=== Assembly Coverage ===" -ForegroundColor Green
Write-Host ""
Write-Host ("{0,-40} {1,12} {2,14}" -f "Assembly", "Line %", "Branch %") -ForegroundColor Cyan
Write-Host ("{0,-40} {1,12} {2,14}" -f "--------", "------", "--------") -ForegroundColor Cyan

foreach ($Package in $Packages) {
    $LineRate = Format-Percentage ([double]$Package.'line-rate')
    $BranchRate = Format-Percentage ([double]$Package.'branch-rate')
    Write-Host ("{0,-40} {1,12} {2,14}" -f $Package.name, $LineRate, $BranchRate)
}
Write-Host ""

# Detailed class breakdown
if ($Detailed) {
    Write-Host "=== Class Coverage Details ===" -ForegroundColor Green
    Write-Host ""
    
    foreach ($Package in $Packages) {
        Write-Host "[$($Package.name)]" -ForegroundColor Yellow
        Write-Host ""
        Write-Host ("  {0,-60} {1,12} {2,14}" -f "Class", "Line %", "Branch %") -ForegroundColor Cyan
        Write-Host ("  {0,-60} {1,12} {2,14}" -f "-----", "------", "--------") -ForegroundColor Cyan
        
        $Classes = $Package.classes.class | Sort-Object { [double]$_.'line-rate' }
        
        foreach ($Class in $Classes) {
            $ClassName = $Class.name -replace "^$($Package.name)\.", ""
            $LineRate = Format-Percentage ([double]$Class.'line-rate')
            $BranchRate = Format-Percentage ([double]$Class.'branch-rate')
            
            # Color code by coverage level
            $Color = "White"
            $LineRateValue = [double]$Class.'line-rate'
            if ($LineRateValue -ge 0.8) { $Color = "Green" }
            elseif ($LineRateValue -ge 0.5) { $Color = "Yellow" }
            elseif ($LineRateValue -gt 0) { $Color = "Red" }
            else { $Color = "DarkRed" }
            
            Write-Host ("  {0,-60} {1,12} {2,14}" -f $ClassName, $LineRate, $BranchRate) -ForegroundColor $Color
        }
        Write-Host ""
    }
}

# Show uncovered lines
if ($ShowUncovered) {
    Write-Host "=== Uncovered Lines ===" -ForegroundColor Green
    Write-Host ""
    
    foreach ($Package in $Packages) {
        $HasUncovered = $false
        
        foreach ($Class in $Package.classes.class) {
            $UncoveredLines = $Class.lines.line | Where-Object { $_.hits -eq "0" }
            
            if ($UncoveredLines) {
                if (-not $HasUncovered) {
                    Write-Host "[$($Package.name)]" -ForegroundColor Yellow
                    Write-Host ""
                    $HasUncovered = $true
                }
                
                $ClassName = $Class.name -replace "^$($Package.name)\.", ""
                $RelativePath = Get-RelativePath $Class.filename
                
                Write-Host "  $ClassName" -ForegroundColor Cyan
                Write-Host "  File: $RelativePath" -ForegroundColor DarkGray
                
                # Group consecutive lines for more compact display
                $LineNumbers = $UncoveredLines | ForEach-Object { [int]$_.number } | Sort-Object
                $Ranges = @()
                $RangeStart = $null
                $RangeEnd = $null
                
                foreach ($LineNum in $LineNumbers) {
                    if ($null -eq $RangeStart) {
                        $RangeStart = $LineNum
                        $RangeEnd = $LineNum
                    } elseif ($LineNum -eq $RangeEnd + 1) {
                        $RangeEnd = $LineNum
                    } else {
                        if ($RangeStart -eq $RangeEnd) {
                            $Ranges += "$RangeStart"
                        } else {
                            $Ranges += "$RangeStart-$RangeEnd"
                        }
                        $RangeStart = $LineNum
                        $RangeEnd = $LineNum
                    }
                }
                
                # Add final range
                if ($null -ne $RangeStart) {
                    if ($RangeStart -eq $RangeEnd) {
                        $Ranges += "$RangeStart"
                    } else {
                        $Ranges += "$RangeStart-$RangeEnd"
                    }
                }
                
                Write-Host "  Lines: $($Ranges -join ', ')" -ForegroundColor Red
                Write-Host ""
            }
        }
        
        if (-not $HasUncovered) {
            Write-Host "[$($Package.name)] - No uncovered lines!" -ForegroundColor Green
            Write-Host ""
        }
    }
}
