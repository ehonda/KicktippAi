<#
.SYNOPSIS
    Gets the latest published version of a NuGet package from nuget.org.

.DESCRIPTION
    Runs `dotnet package search` with --exact-match and --format json, then pipes
    the result through jq to extract the highest version available on nuget.org.

    This script MUST be run before adding or updating any .NET package reference.
    Never assume or guess a package version.

.PARAMETER PackageName
    The exact NuGet package ID to look up (e.g. "Newtonsoft.Json").

.EXAMPLE
    .\Get-LatestPackageVersion.ps1 -PackageName "Newtonsoft.Json"

.EXAMPLE
    .\Get-LatestPackageVersion.ps1 -PackageName "Microsoft.Extensions.Logging"
#>
param(
    [Parameter(Mandatory)]
    [string]$PackageName
)

$ErrorActionPreference = "Stop"

# Verify jq is available
if (-not (Get-Command jq -ErrorAction SilentlyContinue)) {
    Write-Error "jq is not installed or not on PATH. Install via: winget install jqlang.jq"
}

# Run dotnet package search and capture JSON output
$searchJson = dotnet package search $PackageName --exact-match --format json 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet package search failed with exit code $LASTEXITCODE"
}

# Extract the latest version from nuget.org using jq
$jqFilter = '.searchResult[] | select(.sourceName == "nuget.org") | .packages | sort_by(.version | split(".") | map(tonumber)) | reverse | .[0]'
$result = $searchJson | jq $jqFilter

if ($LASTEXITCODE -ne 0) {
    Write-Error "jq failed to parse the dotnet package search output"
}

if ([string]::IsNullOrWhiteSpace($result) -or $result -eq "null") {
    Write-Error "No package found on nuget.org with exact name: $PackageName"
}

$result
