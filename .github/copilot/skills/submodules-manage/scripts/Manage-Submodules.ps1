<#
.SYNOPSIS
    Manage external git submodules under the external/ directory.

.DESCRIPTION
    Add, remove, list, or manage sparse checkout for git submodules that provide
    local access to external repositories referenced by this project. Submodules
    are placed at external/<owner>/<repo>.

.PARAMETER Command
    The command to run: add, remove, list, sparse

.PARAMETER Arguments
    Remaining arguments passed to the command handler.

.EXAMPLE
    .\Manage-Submodules.ps1 add thomhurst/TUnit --info-only

.EXAMPLE
    .\Manage-Submodules.ps1 add thomhurst/TUnit --sparse-paths "docs/docs"

.EXAMPLE
    .\Manage-Submodules.ps1 remove thomhurst/TUnit --confirm

.EXAMPLE
    .\Manage-Submodules.ps1 list

.EXAMPLE
    .\Manage-Submodules.ps1 sparse add dotnet/dotnet-api-docs --paths "xml/System.Net"

.EXAMPLE
    .\Manage-Submodules.ps1 sparse list dotnet/dotnet-api-docs
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$Arguments
)

$ErrorActionPreference = "Stop"

$SUBMODULE_ROOT = "external"

# ── Usage ────────────────────────────────────────────────────────────────────

function Show-Usage {
    @"
Usage:
  Manage-Submodules.ps1 add <owner/repo|url> [options]
  Manage-Submodules.ps1 remove <owner/repo> --confirm
  Manage-Submodules.ps1 list
  Manage-Submodules.ps1 sparse <add|remove|list> <owner/repo> [--paths <paths>]
  Manage-Submodules.ps1 --help

Commands:
  add       Add a git submodule under external/<owner>/<repo>
  remove    Remove a git submodule (requires --confirm)
  list      List all submodules as JSON
  sparse    Manage sparse checkout paths for an existing submodule

Add options:
  --depth N           Clone depth (default: 1)
  --full              Clone with full history (overrides --depth)
  --sparse-paths P    Comma-separated top-level paths for sparse checkout
  --info-only         Show repo size and top-level contents without adding

Remove options:
  --confirm           Required flag to confirm removal

Sparse sub-commands:
  sparse add <owner/repo> --paths <csv>     Add paths to sparse checkout
  sparse remove <owner/repo> --paths <csv>  Remove paths from sparse checkout
  sparse list <owner/repo>                  List current sparse checkout paths
"@
}

# ── Helpers ──────────────────────────────────────────────────────────────────

function Resolve-OwnerRepo {
    param([string]$Input_)

    $val = $Input_
    # Strip trailing .git
    if ($val.EndsWith(".git")) { $val = $val.Substring(0, $val.Length - 4) }
    # Strip https://github.com/ prefix
    if ($val.StartsWith("https://github.com/")) { $val = $val.Substring("https://github.com/".Length) }
    # Strip git@github.com: prefix
    if ($val.StartsWith("git@github.com:")) { $val = $val.Substring("git@github.com:".Length) }

    if ($val -notmatch '^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$') {
        Write-Error "Invalid owner/repo format: $Input_. Expected 'owner/repo' or a GitHub URL."
        exit 2
    }
    return $val
}

function ConvertTo-GitHubUrl {
    param([string]$OwnerRepo)
    return "https://github.com/$OwnerRepo.git"
}

function Parse-Arguments {
    param(
        [string[]]$Args_,
        [hashtable]$Schema  # key => "flag"|"value", defines expected options
    )

    $result = @{ Positional = @() }
    # Initialize defaults
    foreach ($key in $Schema.Keys) {
        if ($Schema[$key] -eq "flag") {
            $result[$key] = $false
        } else {
            $result[$key] = ""
        }
    }

    $i = 0
    while ($i -lt $Args_.Count) {
        $arg = $Args_[$i]
        if ($arg.StartsWith("--")) {
            $optName = $arg.Substring(2)
            if (-not $Schema.ContainsKey($optName)) {
                Write-Error "Unknown option: $arg"
                exit 2
            }
            if ($Schema[$optName] -eq "flag") {
                $result[$optName] = $true
                $i++
            } else {
                if ($i + 1 -ge $Args_.Count) {
                    Write-Error "$arg requires a value"
                    exit 2
                }
                $result[$optName] = $Args_[$i + 1]
                $i += 2
            }
        } else {
            $result.Positional += $arg
            $i++
        }
    }
    return $result
}

# ── Commands ─────────────────────────────────────────────────────────────────

function Invoke-Add {
    param([string[]]$Args_)

    $parsed = Parse-Arguments -Args_ $Args_ -Schema @{
        "depth"        = "value"
        "full"         = "flag"
        "sparse-paths" = "value"
        "info-only"    = "flag"
    }

    if ($parsed.Positional.Count -eq 0) {
        Write-Error "Missing required argument: owner/repo"
        exit 2
    }

    $ownerRepo = Resolve-OwnerRepo $parsed.Positional[0]
    $url = ConvertTo-GitHubUrl $ownerRepo
    $submodulePath = "$SUBMODULE_ROOT/$ownerRepo"
    $depth = if ($parsed["depth"]) { [int]$parsed["depth"] } else { 1 }

    # ── Info-only mode ───────────────────────────────────────────────────
    if ($parsed["info-only"]) {
        Write-Host "Fetching info for $ownerRepo..." -ForegroundColor Cyan

        $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("submodule-info-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null

        try {
            $repoDir = Join-Path $tmpDir "repo"
            git clone --depth 1 --no-checkout $url $repoDir 2>&1 | Write-Host

            Push-Location $repoDir
            try {
                git sparse-checkout init --no-cone 2>&1 | Out-Null
                git sparse-checkout set '/*' 2>&1 | Out-Null
                git checkout HEAD 2>&1 | Write-Host
            } catch {
                # Non-fatal — some repos may have issues with sparse checkout
            }
            Pop-Location

            # Gather info
            $allFiles = Get-ChildItem -Path $repoDir -Recurse -File -Force -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notlike "*\.git\*" -and $_.FullName -notlike "*\.git" }
            $fileCount = $allFiles.Count

            $topItems = Get-ChildItem -Path $repoDir -Force -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -ne ".git" }

            $topLevelContents = @()
            foreach ($item in $topItems) {
                $entryType = if ($item.PSIsContainer) { "dir" } else { "file" }
                $entrySize = if ($item.PSIsContainer) {
                    $dirSize = (Get-ChildItem -Path $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                        Measure-Object -Property Length -Sum).Sum
                    if ($null -eq $dirSize) { "0B" } else { Format-Size $dirSize }
                } else {
                    Format-Size $item.Length
                }
                $topLevelContents += [PSCustomObject]@{
                    name = $item.Name
                    type = $entryType
                    size = $entrySize
                }
            }

            $diskSizeBytes = ($allFiles | Measure-Object -Property Length -Sum).Sum
            $diskSize = if ($null -eq $diskSizeBytes) { "0B" } else { Format-Size $diskSizeBytes }

            $result = [PSCustomObject]@{
                repo               = $ownerRepo
                url                = $url
                file_count         = $fileCount
                disk_size          = $diskSize
                top_level_contents = $topLevelContents
            }

            $result | ConvertTo-Json -Depth 3
        } finally {
            Remove-Item -Path $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        return
    }

    # ── Idempotency check ────────────────────────────────────────────────
    if (Test-Path $submodulePath -PathType Container) {
        Write-Host "Submodule already exists at $submodulePath, skipping."
        return
    }

    # ── Add submodule ────────────────────────────────────────────────────
    $depthArgs = @()
    if (-not $parsed["full"]) {
        $depthArgs = @("--depth", $depth)
    }

    Write-Host "Adding submodule $ownerRepo at $submodulePath..." -ForegroundColor Cyan

    if ($parsed["sparse-paths"]) {
        # When sparse-paths is specified, we must configure sparse checkout BEFORE
        # the initial checkout to avoid checking out files we don't need (and to
        # avoid long-path errors on repos with deeply nested files).
        #
        # Approach:
        #   1. Ensure parent directory exists
        #   2. Clone with --no-checkout into the submodule path
        #   3. Configure sparse checkout in the cloned repo
        #   4. Checkout
        #   5. Register as a submodule via .gitmodules and git add

        $parentDir = Split-Path $submodulePath -Parent
        if (-not (Test-Path $parentDir -PathType Container)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }

        git clone @depthArgs --no-checkout $url $submodulePath
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to clone repository"
            exit 1
        }

        Push-Location $submodulePath
        try {
            $paths = $parsed["sparse-paths"] -split ','
            Write-Host "Configuring sparse checkout for: $($parsed['sparse-paths'])" -ForegroundColor Cyan
            git sparse-checkout init --cone
            git sparse-checkout set @paths
            git checkout
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to checkout"
                exit 1
            }
        } finally {
            Pop-Location
        }

        # Register as submodule: add .gitmodules entry, stage, and absorb git dir
        git config -f .gitmodules "submodule.$submodulePath.path" $submodulePath
        git config -f .gitmodules "submodule.$submodulePath.url" $url
        git add .gitmodules
        # Suppress embedded-repo hint — we absorb the gitdir immediately after
        git -c advice.addEmbeddedRepo=false add $submodulePath
        # Move .git dir from submodule into parent's .git/modules (proper submodule layout)
        git submodule absorbgitdirs $submodulePath
        # Now init so .git/config knows the submodule
        git submodule init $submodulePath 2>$null
        git config "submodule.$submodulePath.url" $url
    } else {
        git submodule add @depthArgs $url $submodulePath
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to add submodule"
            exit 1
        }
    }

    Write-Host "Successfully added submodule: $submodulePath" -ForegroundColor Green
}

function Invoke-Remove {
    param([string[]]$Args_)

    $parsed = Parse-Arguments -Args_ $Args_ -Schema @{
        "confirm" = "flag"
    }

    if ($parsed.Positional.Count -eq 0) {
        Write-Error "Missing required argument: owner/repo"
        exit 2
    }

    if (-not $parsed["confirm"]) {
        Write-Error "Removal requires --confirm flag to prevent accidental deletion."
        exit 1
    }

    $ownerRepo = Resolve-OwnerRepo $parsed.Positional[0]
    $submodulePath = "$SUBMODULE_ROOT/$ownerRepo"

    if (-not (Test-Path $submodulePath -PathType Container)) {
        Write-Error "Submodule not found at $submodulePath"
        exit 1
    }

    Write-Host "Removing submodule at $submodulePath..." -ForegroundColor Cyan
    git submodule deinit -f $submodulePath
    git rm -f $submodulePath
    $gitModulesPath = ".git/modules/$submodulePath"
    if (Test-Path $gitModulesPath) {
        Remove-Item -Path $gitModulesPath -Recurse -Force
    }

    Write-Host "Successfully removed submodule: $submodulePath" -ForegroundColor Green
}

function Invoke-List {
    if (-not (Test-Path ".gitmodules")) {
        Write-Output "[]"
        return
    }

    $entries = @()
    $currentPath = ""
    $currentUrl = ""

    foreach ($line in Get-Content ".gitmodules") {
        if ($line -match 'path\s*=\s*(.+)') {
            $currentPath = $Matches[1].Trim()
        }
        if ($line -match 'url\s*=\s*(.+)') {
            $currentUrl = $Matches[1].Trim()
        }
        if ($currentPath -and $currentUrl) {
            $entries += [PSCustomObject]@{
                path = $currentPath
                url  = $currentUrl
            }
            $currentPath = ""
            $currentUrl = ""
        }
    }

    if ($entries.Count -eq 0) {
        Write-Output "[]"
    } else {
        $entries | ConvertTo-Json -Depth 2 -AsArray
    }
}

# ── Sparse subcommand ────────────────────────────────────────────────────────

function Invoke-Sparse {
    param([string[]]$Args_)

    if ($Args_.Count -eq 0) {
        Write-Error "Missing sparse sub-command. Must be one of: add, remove, list"
        exit 2
    }

    $subAction = $Args_[0]
    $remaining = if ($Args_.Count -gt 1) { $Args_[1..($Args_.Count - 1)] } else { @() }

    switch ($subAction) {
        "add"    { Invoke-SparseAdd $remaining }
        "remove" { Invoke-SparseRemove $remaining }
        "list"   { Invoke-SparseList $remaining }
        default  {
            Write-Error "Unknown sparse sub-command: $subAction. Must be one of: add, remove, list"
            exit 2
        }
    }
}

function Invoke-SparseAdd {
    param([string[]]$Args_)

    $parsed = Parse-Arguments -Args_ $Args_ -Schema @{
        "paths" = "value"
    }

    if ($parsed.Positional.Count -eq 0) {
        Write-Error "Missing required argument: owner/repo"
        exit 2
    }
    if (-not $parsed["paths"]) {
        Write-Error "Missing required option: --paths"
        exit 2
    }

    $ownerRepo = Resolve-OwnerRepo $parsed.Positional[0]
    $submodulePath = "$SUBMODULE_ROOT/$ownerRepo"

    if (-not (Test-Path $submodulePath -PathType Container)) {
        Write-Error "Submodule not found at $submodulePath"
        exit 1
    }

    Push-Location $submodulePath
    try {
        # Auto-init sparse checkout if not active
        $sparseOutput = git sparse-checkout list 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Initializing sparse checkout..." -ForegroundColor Cyan
            git sparse-checkout init --cone
        }

        $newPaths = $parsed["paths"] -split ','
        Write-Host "Adding sparse checkout paths: $($newPaths -join ', ')" -ForegroundColor Cyan
        git sparse-checkout add @newPaths
    } finally {
        Pop-Location
    }

    Write-Host "Successfully added sparse checkout paths to $submodulePath" -ForegroundColor Green
}

function Invoke-SparseRemove {
    param([string[]]$Args_)

    $parsed = Parse-Arguments -Args_ $Args_ -Schema @{
        "paths" = "value"
    }

    if ($parsed.Positional.Count -eq 0) {
        Write-Error "Missing required argument: owner/repo"
        exit 2
    }
    if (-not $parsed["paths"]) {
        Write-Error "Missing required option: --paths"
        exit 2
    }

    $ownerRepo = Resolve-OwnerRepo $parsed.Positional[0]
    $submodulePath = "$SUBMODULE_ROOT/$ownerRepo"

    if (-not (Test-Path $submodulePath -PathType Container)) {
        Write-Error "Submodule not found at $submodulePath"
        exit 1
    }

    Push-Location $submodulePath
    try {
        $currentPaths = @(git sparse-checkout list 2>&1)
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Sparse checkout is not active on this submodule."
            exit 1
        }

        $removePaths = $parsed["paths"] -split ','

        $remaining = @($currentPaths | Where-Object {
            $path = $_
            -not ($removePaths | Where-Object { $_ -eq $path })
        })

        if ($remaining.Count -eq 0) {
            Write-Error "Cannot remove all sparse checkout paths. Use 'git sparse-checkout disable' to restore full checkout."
            exit 1
        }

        Write-Host "Removing sparse checkout paths: $($removePaths -join ', ')" -ForegroundColor Cyan
        git sparse-checkout set @remaining
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to update sparse checkout paths."
            exit 1
        }
    } finally {
        Pop-Location
    }

    Write-Host "Successfully removed sparse checkout paths from $submodulePath" -ForegroundColor Green
}

function Invoke-SparseList {
    param([string[]]$Args_)

    $parsed = Parse-Arguments -Args_ $Args_ -Schema @{}

    if ($parsed.Positional.Count -eq 0) {
        Write-Error "Missing required argument: owner/repo"
        exit 2
    }

    $ownerRepo = Resolve-OwnerRepo $parsed.Positional[0]
    $submodulePath = "$SUBMODULE_ROOT/$ownerRepo"

    if (-not (Test-Path $submodulePath -PathType Container)) {
        Write-Error "Submodule not found at $submodulePath"
        exit 1
    }

    Push-Location $submodulePath
    try {
        git sparse-checkout list
    } finally {
        Pop-Location
    }
}

# ── Utilities ────────────────────────────────────────────────────────────────

function Format-Size {
    param([long]$Bytes)

    if ($Bytes -ge 1GB) { return "{0:N1}G" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N1}M" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N1}K" -f ($Bytes / 1KB) }
    return "${Bytes}B"
}

# ── Main ─────────────────────────────────────────────────────────────────────

if (-not $Command -or $Command -eq "--help" -or $Command -eq "-h") {
    Show-Usage
    exit 0
}

switch ($Command) {
    "add"    { Invoke-Add $Arguments }
    "remove" { Invoke-Remove $Arguments }
    "list"   { Invoke-List }
    "sparse" { Invoke-Sparse $Arguments }
    default  {
        Write-Error "Unknown command: $Command"
        Show-Usage
        exit 2
    }
}
