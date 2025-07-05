param(
    [Parameter(Mandatory = $false)]
    [string]$PageName
)

# Function to prompt for page name if not provided
function Get-PageName {
    if ([string]::IsNullOrWhiteSpace($PageName)) {
        do {
            $PageName = Read-Host "Please enter the page name"
        } while ([string]::IsNullOrWhiteSpace($PageName))
    }
    return $PageName
}

# Function to create the timestamp suffix
function Get-TimestampSuffix {
    return (Get-Date -AsUTC).ToString("yyyy-MM-dd-HH-mm-ss")
}

# Function to create the directory structure and file
function New-KicktippSample {
    param(
        [string]$PageName,
        [string]$Suffix
    )
    
    # Create the directory path
    $directoryPath = Join-Path "kicktipp-samples" $PageName
    
    # Create directory if it doesn't exist
    if (-not (Test-Path $directoryPath)) {
        New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
        Write-Host "Created directory: $directoryPath" -ForegroundColor Green
    }
    
    # Create the file name
    $fileName = "$PageName-$Suffix.html"
    $filePath = Join-Path $directoryPath $fileName
    
    # Create the empty HTML file
    New-Item -ItemType File -Path $filePath -Force | Out-Null
    
    Write-Host "Created sample file: $filePath" -ForegroundColor Green
    return $filePath
}

# Main script execution
try {
    # Get the page name (either from parameter or prompt)
    $pageName = Get-PageName
    
    # Generate timestamp suffix
    $suffix = Get-TimestampSuffix
    
    # Create the sample file
    $createdFile = New-KicktippSample -PageName $pageName -Suffix $suffix
    
    Write-Host "`nSuccessfully created Kicktipp sample file:" -ForegroundColor Cyan
    Write-Host $createdFile -ForegroundColor White
    
    # Always open the file in VS Code
    code $createdFile
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
