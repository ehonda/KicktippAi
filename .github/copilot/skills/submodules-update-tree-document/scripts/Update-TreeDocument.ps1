<#
.SYNOPSIS
    Update agent-files/submodule-tree.txt with the flat tree view of external/.

.DESCRIPTION
    Runs the Display-Tree script with -Format flat on external/ and writes
    the raw output to agent-files/submodule-tree.txt. The file is overwritten
    on each run (idempotent).

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\Update-TreeDocument.ps1
#>

[CmdletBinding()]
param(
    [switch]$Help
)

$ErrorActionPreference = "Stop"

if ($Help) {
    @"
Usage:
  Update-TreeDocument.ps1
  Update-TreeDocument.ps1 -Help

Updates agent-files/submodule-tree.txt with the flat tree view of external/.
The file contains only the raw Display-Tree output — no headers or wrapping.
"@
    exit 0
}

# ── Resolve paths ────────────────────────────────────────────────────────────

$displayTreeScript = Join-Path $PSScriptRoot "..\..\submodules-display-tree\scripts\Display-Tree.ps1"
$displayTreeScript = (Resolve-Path $displayTreeScript).Path

$outputDir = "agent-files"
$outputFile = Join-Path $outputDir "submodule-tree.txt"

# ── Main ─────────────────────────────────────────────────────────────────────

if (-not (Test-Path $displayTreeScript)) {
    Write-Error "Display-Tree script not found at: $displayTreeScript"
    exit 1
}

if (-not (Test-Path $outputDir -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Generating flat tree of external/..." -ForegroundColor Cyan
$treeOutput = & $displayTreeScript external -Format flat

Set-Content -Path $outputFile -Value $treeOutput -NoNewline
# Ensure trailing newline
Add-Content -Path $outputFile -Value ""

Write-Host "Updated $outputFile" -ForegroundColor Green
