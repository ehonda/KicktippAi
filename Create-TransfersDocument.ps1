param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("csv", "firebase")]
    [string]$Mode,
    
    [Parameter(Mandatory = $false)]
    [string]$TeamAbbreviation,

    [Parameter(Mandatory = $false)]
    [string]$CommunityContext
)

# Helper: Prompt for mode
function Get-Mode {
    if ([string]::IsNullOrWhiteSpace($Mode)) {
        Write-Host "Available modes:" -ForegroundColor Cyan
        Write-Host "  csv       - Create empty transfers CSV template" -ForegroundColor White
        Write-Host "  firebase  - Create Firebase-ready JSON document from existing CSV" -ForegroundColor White
        Write-Host ""
        do { $Mode = Read-Host "Please enter the mode (csv/firebase)" } while ($Mode -notin @("csv", "firebase"))
    }
    return $Mode
}

# Helper: Team abbreviation (3 letters already standardized) e.g. fcb, bvb, b04
function Get-TeamAbbreviation {
    if ([string]::IsNullOrWhiteSpace($TeamAbbreviation)) {
        do { $TeamAbbreviation = Read-Host "Enter team abbreviation (e.g. fcb, bvb, b04)" } while ([string]::IsNullOrWhiteSpace($TeamAbbreviation))
    }
    return $TeamAbbreviation.ToLowerInvariant()
}

# Helper: Community context required for firebase mode
function Get-CommunityContext {
    param([string]$Mode)
    if ($Mode -eq "firebase" -and [string]::IsNullOrWhiteSpace($CommunityContext)) {
        do { $CommunityContext = Read-Host "Enter community context (e.g. ehonda-test-buli)" } while ([string]::IsNullOrWhiteSpace($CommunityContext))
    }
    return $CommunityContext
}

function New-TransfersDirectory {
    param([string]$Mode, [string]$CommunityContext = "")
    $base = "transfers-documents"
    $sub = if ($Mode -eq "csv") { "input" } else { "output" }
    $path = Join-Path $base $sub
    if ($Mode -eq "firebase" -and -not [string]::IsNullOrWhiteSpace($CommunityContext)) {
        $path = Join-Path $path $CommunityContext
    }
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null; Write-Host "Created directory: $path" -ForegroundColor Green }
    return $path
}

function New-TransfersCsvTemplate {
    param([string]$DirectoryPath, [string]$TeamAbbreviation)
    $fileName = "$TeamAbbreviation-transfers.csv"
    $filePath = Join-Path $DirectoryPath $fileName
    if (Test-Path $filePath) { Write-Host "File already exists: $filePath" -ForegroundColor Yellow; return $filePath }
    # Header aligned with provided transfer sample (e.g. b04-transfers.csv)
    # Paste rows under these columns:
    # Date (YYYY-MM-DD), Transfer_Type, Name, Position, From_Team, To_Team, Assessment
    $header = "Date,Transfer_Type,Name,Position,From_Team,To_Team,Assessment"
    # Write with LF line ending explicitly (no BOM)
    [IO.File]::WriteAllText($filePath, $header + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Created transfers CSV template: $filePath (LF line endings)" -ForegroundColor Green
    return $filePath
}

function New-FirebaseTransfersDocument {
    param([string]$DirectoryPath, [string]$TeamAbbreviation, [string]$CommunityContext)
    $csvFile = "transfers-documents\\input\\$TeamAbbreviation-transfers.csv"
    $content = ""
    if (Test-Path $csvFile) {
        $content = (Get-Content $csvFile -Raw)
        # Normalize to LF line endings inside JSON content
        $content = $content -replace "`r`n", "`n"
        Write-Host "Loaded content from: $csvFile" -ForegroundColor Green
    } else {
        Write-Host "CSV file not found: $csvFile - creating empty content" -ForegroundColor Yellow
    }
    $documentName = "$TeamAbbreviation-transfers.csv"
    $json = [ordered]@{
        documentName     = $documentName
        content          = $content
        description      = "Transfers context document containing recent transfer activity for team $TeamAbbreviation"
        communityContext = $CommunityContext
    } | ConvertTo-Json -Depth 5
    # Normalize JSON newlines to LF and write without BOM
    $json = $json -replace "`r`n", "`n"
    $jsonFile = Join-Path $DirectoryPath ("$documentName.json")
    [IO.File]::WriteAllText($jsonFile, $json + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Created Firebase transfers document: $jsonFile (LF line endings)" -ForegroundColor Green
    return $jsonFile
}

try {
    Write-Host "Transfers Document Creator" -ForegroundColor Cyan
    Write-Host "==========================" -ForegroundColor Cyan
    Write-Host ""
    $mode = Get-Mode
    Write-Host "Mode: $mode" -ForegroundColor Yellow
    $abbr = Get-TeamAbbreviation
    Write-Host "Team Abbreviation: $abbr" -ForegroundColor Yellow
    $ctx = Get-CommunityContext -Mode $mode
    if ($mode -eq "firebase") { Write-Host "Community Context: $ctx" -ForegroundColor Yellow }
    $dir = New-TransfersDirectory -Mode $mode -CommunityContext $ctx
    $result = switch ($mode) {
        "csv" { New-TransfersCsvTemplate -DirectoryPath $dir -TeamAbbreviation $abbr }
        "firebase" { New-FirebaseTransfersDocument -DirectoryPath $dir -TeamAbbreviation $abbr -CommunityContext $ctx }
    }
    Write-Host ""; Write-Host "Created file:" -ForegroundColor Cyan; Write-Host $result -ForegroundColor White
    if ($mode -eq "csv") { code $result }
}
catch {
    Write-Error "An error occurred: $_"; exit 1
}
