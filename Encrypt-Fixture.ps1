<#
.SYNOPSIS
    Encrypts HTML fixture files for KicktippIntegration tests.

.DESCRIPTION
    This script encrypts HTML files using AES-256-GCM so they can be safely
    committed to the repository. The encryption key is read from the
    KICKTIPP_FIXTURE_KEY environment variable.

.PARAMETER InputPath
    Path to the HTML file to encrypt.

.PARAMETER OutputPath
    Path where the encrypted file will be saved. If not specified,
    appends .enc to the input filename.

.PARAMETER GenerateKey
    If specified, generates a new encryption key and outputs it.
    Does not encrypt any file.

.EXAMPLE
    .\Encrypt-Fixture.ps1 -GenerateKey
    Generates and displays a new encryption key.

.EXAMPLE
    .\Encrypt-Fixture.ps1 -InputPath tabellen.html
    Encrypts tabellen.html to tabellen.html.enc using the configured key.

.EXAMPLE
    .\Encrypt-Fixture.ps1 -InputPath tabellen.html -OutputPath tests/KicktippIntegration.Tests/Fixtures/Html/tabellen.html.enc
    Encrypts to a specific output path.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$InputPath,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath,

    [switch]$GenerateKey
)

$ErrorActionPreference = "Stop"

# Generate key mode
if ($GenerateKey) {
    $keyBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $base64Key = [Convert]::ToBase64String($keyBytes)
    
    Write-Host ""
    Write-Host "Generated new AES-256 encryption key:" -ForegroundColor Green
    Write-Host ""
    Write-Host "  $base64Key" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Store this key in one of these locations:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Local development:" -ForegroundColor White
    Write-Host "    File: <repo>/../KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env" -ForegroundColor DarkGray
    Write-Host "    Content: KICKTIPP_FIXTURE_KEY=$base64Key" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  CI/CD (GitHub Actions):" -ForegroundColor White
    Write-Host "    Secret name: KICKTIPP_FIXTURE_KEY" -ForegroundColor DarkGray
    Write-Host "    Secret value: $base64Key" -ForegroundColor DarkGray
    Write-Host ""
    
    exit 0
}

# Validate parameters for encryption mode
if (-not $InputPath) {
    Write-Error "InputPath is required. Use -GenerateKey to generate a new key instead."
    exit 1
}

if (-not (Test-Path $InputPath)) {
    Write-Error "Input file not found: $InputPath"
    exit 1
}

# Load environment from secrets directory if available
$RepoRoot = $PSScriptRoot
$EnvPath = Join-Path $RepoRoot ".." "KicktippAi.Secrets" "tests" "KicktippIntegration.Tests" ".env"

if (Test-Path $EnvPath) {
    Write-Host "Loading environment from: $EnvPath" -ForegroundColor DarkGray
    Get-Content $EnvPath | ForEach-Object {
        if ($_ -match "^([^=]+)=(.*)$") {
            [Environment]::SetEnvironmentVariable($matches[1], $matches[2])
        }
    }
}

# Get encryption key
$key = $env:KICKTIPP_FIXTURE_KEY
if (-not $key) {
    Write-Error @"
KICKTIPP_FIXTURE_KEY environment variable is not set.

To generate a new key, run:
  .\Encrypt-Fixture.ps1 -GenerateKey

Then store the key in:
  <repo>/../KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env
"@
    exit 1
}

# Determine output path
if (-not $OutputPath) {
    $OutputPath = "$InputPath.enc"
}

# Encrypt using .NET cryptography
Add-Type -AssemblyName System.Security

$plaintext = [System.IO.File]::ReadAllText($InputPath, [System.Text.Encoding]::UTF8)
$plaintextBytes = [System.Text.Encoding]::UTF8.GetBytes($plaintext)
$keyBytes = [Convert]::FromBase64String($key)

if ($keyBytes.Length -ne 32) {
    Write-Error "Invalid key length. Key must be 256 bits (32 bytes) base64-encoded."
    exit 1
}

# Generate random nonce
$nonce = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(12)
$ciphertext = [byte[]]::new($plaintextBytes.Length)
$tag = [byte[]]::new(16)

# Encrypt using AES-GCM
$aesGcm = [System.Security.Cryptography.AesGcm]::new($keyBytes, 16)
$aesGcm.Encrypt($nonce, $plaintextBytes, $ciphertext, $tag)
$aesGcm.Dispose()

# Combine: nonce (12) + ciphertext + tag (16)
$result = [byte[]]::new(12 + $ciphertext.Length + 16)
[Array]::Copy($nonce, 0, $result, 0, 12)
[Array]::Copy($ciphertext, 0, $result, 12, $ciphertext.Length)
[Array]::Copy($tag, 0, $result, 12 + $ciphertext.Length, 16)

$base64Result = [Convert]::ToBase64String($result)

# Create output directory if needed
$outputDir = Split-Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $base64Result)

Write-Host ""
Write-Host "Encrypted successfully!" -ForegroundColor Green
Write-Host "  Input:  $InputPath" -ForegroundColor White
Write-Host "  Output: $OutputPath" -ForegroundColor White
Write-Host ""
Write-Host "Remember to:" -ForegroundColor Yellow
Write-Host "  1. Delete the original HTML file (do NOT commit it)" -ForegroundColor DarkGray
Write-Host "  2. Commit the .html.enc file" -ForegroundColor DarkGray
Write-Host ""
