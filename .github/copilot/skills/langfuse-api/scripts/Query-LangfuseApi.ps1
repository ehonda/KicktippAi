<#
.SYNOPSIS
    Queries the Langfuse REST API.

.DESCRIPTION
    Loads Langfuse credentials (LANGFUSE_PUBLIC_KEY, LANGFUSE_SECRET_KEY) via dotenvx
    from the project secrets .env file and queries the Langfuse public API using curl
    with Basic Auth.

.PARAMETER Endpoint
    The API endpoint path, e.g. "traces", "traces/<id>", "observations", "observations/<id>".

.PARAMETER QueryParams
    Optional hashtable of query parameters to append to the URL.

.EXAMPLE
    .\.github\copilot\skills\langfuse-api\scripts\Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=5}

.EXAMPLE
    .\.github\copilot\skills\langfuse-api\scripts\Query-LangfuseApi.ps1 -Endpoint "traces/abc-123"

.EXAMPLE
    .\.github\copilot\skills\langfuse-api\scripts\Query-LangfuseApi.ps1 -Endpoint "observations" -QueryParams @{traceId="abc-123"; type="GENERATION"}
#>
param(
    [Parameter(Mandatory)]
    [string]$Endpoint,

    [hashtable]$QueryParams
)

$ErrorActionPreference = "Stop"

# Verify dotenvx is available
if (-not (Get-Command dotenvx -ErrorAction SilentlyContinue)) {
    Write-Error "dotenvx is not installed or not on PATH. Install via: winget install dotenvx"
}

# Resolve .env file path
# Script is at .github/copilot/skills/langfuse-api/scripts/, so five levels up is the solution root
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "../../../../..")
$envFilePath = Join-Path $solutionRoot "../KicktippAi.Secrets/src/Orchestrator/.env"

if (-not (Test-Path $envFilePath)) {
    Write-Error "Secrets .env file not found. Expected at: $envFilePath"
}

$envFilePath = (Resolve-Path $envFilePath).Path

# Load credentials via dotenvx
$envJson = dotenvx get -f $envFilePath 2>$null
$envVars = $envJson | ConvertFrom-Json
$publicKey = $envVars.LANGFUSE_PUBLIC_KEY
$secretKey = $envVars.LANGFUSE_SECRET_KEY

if ([string]::IsNullOrEmpty($publicKey) -or [string]::IsNullOrEmpty($secretKey)) {
    Write-Error "LANGFUSE_PUBLIC_KEY or LANGFUSE_SECRET_KEY not found in: $envFilePath"
}

# Build URL
$baseUrl = "https://cloud.langfuse.com"
$url = "$baseUrl/api/public/$Endpoint"

if ($QueryParams -and $QueryParams.Count -gt 0) {
    $queryParts = $QueryParams.GetEnumerator() | ForEach-Object {
        "$([uri]::EscapeDataString("$($_.Key)"))=$([uri]::EscapeDataString("$($_.Value)"))"
    }
    $url = "$url?$($queryParts -join '&')"
}

# Execute API call with Basic Auth
$response = curl.exe -s -u "${publicKey}:${secretKey}" $url

# Pretty-print JSON output, with clear indication when results are empty
if ([string]::IsNullOrWhiteSpace($response)) {
    Write-Warning "Langfuse API returned an EMPTY response for: $url"
    return
}

try {
    $parsed = $response | ConvertFrom-Json

    # Check for list endpoints that return a data array
    if ($null -ne $parsed.data -and $parsed.data.Count -eq 0) {
        Write-Warning "Langfuse API returned ZERO results for: $url"
    }

    $parsed | ConvertTo-Json -Depth 20
}
catch {
    $response
}
