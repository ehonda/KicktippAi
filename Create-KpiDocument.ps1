param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("tsv", "firebase")]
    [string]$Mode,
    
    [Parameter(Mandatory = $false)]
    [string]$DocumentName
)

# Function to prompt for mode if not provided
function Get-Mode {
    if ([string]::IsNullOrWhiteSpace($Mode)) {
        Write-Host "Available modes:" -ForegroundColor Cyan
        Write-Host "  tsv      - Create empty input TSV template" -ForegroundColor White
        Write-Host "  firebase - Create Firebase-ready output document" -ForegroundColor White
        Write-Host ""
        
        do {
            $Mode = Read-Host "Please enter the mode (tsv/firebase)"
        } while ($Mode -notin @("tsv", "firebase"))
    }
    return $Mode
}

# Function to prompt for document name if not provided
function Get-DocumentName {
    if ([string]::IsNullOrWhiteSpace($DocumentName)) {
        do {
            $DocumentName = Read-Host "Please enter the document name"
        } while ([string]::IsNullOrWhiteSpace($DocumentName))
    }
    return $DocumentName
}

# Function to create the timestamp suffix
function Get-TimestampSuffix {
    return (Get-Date -AsUTC).ToString("yyyy-MM-dd-HH-mm-ss")
}

# Function to create the directory structure
function New-KpiDocumentDirectory {
    param(
        [string]$Mode
    )
    
    # Create the base directory path
    $baseDirectory = "kpi-documents"
    $subDirectory = if ($Mode -eq "tsv") { "input" } else { "output" }
    $directoryPath = Join-Path $baseDirectory $subDirectory
    
    # Create directory if it doesn't exist
    if (-not (Test-Path $directoryPath)) {
        New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
        Write-Host "Created directory: $directoryPath" -ForegroundColor Green
    }
    
    return $directoryPath
}

# Function to get full header (including X columns) from template file
function Get-FullHeader {
    $templatePath = "kpi-documents\templates\header-template.tsv"
    
    if (-not (Test-Path $templatePath)) {
        throw "Header template file not found: $templatePath"
    }
    
    return Get-Content $templatePath -First 1
}

# Function to get filtered header (without X columns) from template file  
function Get-FilteredHeader {
    $templatePath = "kpi-documents\templates\header-template.tsv"
    
    if (-not (Test-Path $templatePath)) {
        throw "Header template file not found: $templatePath"
    }
    
    $fullHeader = Get-Content $templatePath -First 1
    $columns = $fullHeader -split "`t"
    $filteredColumns = $columns | Where-Object { $_ -ne "X" }
    return $filteredColumns -join "`t"
}

# Function to create TSV template file
function New-TsvTemplate {
    param(
        [string]$DirectoryPath,
        [string]$DocumentName
    )
    
    # Create the file name (no timestamp suffix)
    $fileName = "$DocumentName.tsv"
    $filePath = Join-Path $DirectoryPath $fileName
    
    # Get full header (including X columns for TSV input)
    $fullHeader = Get-FullHeader
    
    # Create TSV file with full header
    Set-Content -Path $filePath -Value $fullHeader -Encoding UTF8
    
    Write-Host "Created TSV template: $filePath" -ForegroundColor Green
    return $filePath
}

# Function to create Firebase document template
function New-FirebaseDocument {
    param(
        [string]$DirectoryPath,
        [string]$DocumentName
    )
    
    # Create the file name (no timestamp suffix)
    $fileName = "$DocumentName.json"
    $filePath = Join-Path $DirectoryPath $fileName
    
    # Try to read content from matching TSV file
    $tsvFilePath = "kpi-documents\input\$DocumentName.tsv"
    $content = ""
    
    if (Test-Path $tsvFilePath) {
        # Read the TSV file and filter out X columns
        $tsvLines = Get-Content $tsvFilePath
        $filteredLines = @()
        
        foreach ($line in $tsvLines) {
            $columns = $line -split "`t"
            $filteredColumns = @()
            
            for ($i = 0; $i -lt $columns.Length; $i++) {
                # Skip columns that contain "X" in the header (first line)
                if ($filteredLines.Count -eq 0) {
                    # For header line, check if column value is "X"
                    if ($columns[$i] -ne "X") {
                        $filteredColumns += $columns[$i]
                    }
                } else {
                    # For data lines, use the same column positions as header
                    $headerColumns = $tsvLines[0] -split "`t"
                    if ($headerColumns[$i] -ne "X") {
                        $filteredColumns += $columns[$i]
                    }
                }
            }
            
            $filteredLines += ($filteredColumns -join "`t")
        }
        
        $content = $filteredLines -join "`n"
        Write-Host "Loaded content from: $tsvFilePath" -ForegroundColor Green
    } else {
        Write-Host "TSV file not found: $tsvFilePath - content will be empty" -ForegroundColor Yellow
    }
    
    # Create Firebase document template
    $firebaseContent = @{
        "documentId" = $DocumentName
        "name" = "$DocumentName.tsv"
        "content" = $content
        "createdAt" = (Get-Date -AsUTC -Format 'o')
        "competition" = "bundesliga-2025-26"
        "tags" = @("kpi", "bonus-predictions", "team-data")
        "documentType" = "kpi-context"
        "description" = "KPI context document containing team performance data for bonus predictions"
    } | ConvertTo-Json -Depth 10
    
    # Create the JSON file
    Set-Content -Path $filePath -Value $firebaseContent -Encoding UTF8
    
    Write-Host "Created Firebase document: $filePath" -ForegroundColor Green
    return $filePath
}

# Main script execution
try {
    Write-Host "KPI Document Creator" -ForegroundColor Cyan
    Write-Host "===================" -ForegroundColor Cyan
    Write-Host ""
    
    # Get the mode (either from parameter or prompt)
    $mode = Get-Mode
    Write-Host "Mode: $mode" -ForegroundColor Yellow
    
    # Get the document name (either from parameter or prompt)
    $documentName = Get-DocumentName
    Write-Host "Document: $documentName" -ForegroundColor Yellow
    
    # Generate timestamp suffix
    $suffix = Get-TimestampSuffix
    Write-Host "Timestamp: $suffix" -ForegroundColor Yellow
    Write-Host ""
    
    # Create the appropriate directory
    $directoryPath = New-KpiDocumentDirectory -Mode $mode
    
    # Create the document based on mode
    $createdFile = switch ($mode) {
        "tsv" {
            New-TsvTemplate -DirectoryPath $directoryPath -DocumentName $documentName
        }
        "firebase" {
            New-FirebaseDocument -DirectoryPath $directoryPath -DocumentName $documentName
        }
    }
    
    Write-Host ""
    Write-Host "Successfully created KPI document:" -ForegroundColor Cyan
    Write-Host $createdFile -ForegroundColor White
    
    # Open the file in VS Code only for TSV mode
    if ($mode -eq "tsv") {
        code $createdFile
    }
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
