<#
.SYNOPSIS
    Display a directory tree in multiple formats (tree, indented, flat).

.DESCRIPTION
    Renders the external/ submodule directory tree in one of three formats:
    flat (brace-expansion), indented (space-indented), or tree (box-drawing).

.PARAMETER Directory
    Directory to display. Default: external

.PARAMETER Format
    Output format: tree, indented, or flat. Default: flat

.PARAMETER Depth
    Maximum traversal depth. 0 means unlimited. Default: 0

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\Display-Tree.ps1
    # Flat format (default) of external/

.EXAMPLE
    .\Display-Tree.ps1 -Format indented

.EXAMPLE
    .\Display-Tree.ps1 -Format tree -Depth 2
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Directory = "external",

    [ValidateSet("tree", "indented", "flat")]
    [string]$Format = "flat",

    [int]$Depth = 0,

    [switch]$Help
)

$ErrorActionPreference = "Stop"

# ── Usage ────────────────────────────────────────────────────────────────────

if ($Help) {
    @"
Usage:
  Display-Tree.ps1 [Directory] [-Format tree|indented|flat] [-Depth N]
  Display-Tree.ps1 -Help

Arguments:
  Directory             Directory to display (default: external)

Options:
  -Format FORMAT        Output format: tree, indented, flat (default: flat)
  -Depth N              Maximum traversal depth (default: 0 = unlimited)

Formats:
  flat       Brace-expansion notation:  root/{dir1/{a,b},dir2/c}
  indented   Space-indented list with trailing / on dirs
  tree       Classic tree with box-drawing characters
"@
    exit 0
}

# ── Validation ───────────────────────────────────────────────────────────────

# Strip trailing slash/backslash
$Directory = $Directory.TrimEnd('\', '/')

if (-not (Test-Path $Directory -PathType Container)) {
    Write-Error "Directory not found: $Directory"
    exit 1
}

# ── Gather entries ───────────────────────────────────────────────────────────

function Get-Entries {
    param([string]$Root, [int]$MaxDepth)

    $rootFull = (Resolve-Path $Root).Path

    $items = Get-ChildItem -Path $Root -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            # Exclude .git directories and their children
            $rel = $_.FullName.Substring($rootFull.Length).TrimStart('\', '/')
            $parts = $rel -split '[/\\]'
            -not ($parts -contains '.git')
        } |
        Where-Object {
            # Only include directories (we display directory structure)
            $_.PSIsContainer
        } |
        Where-Object {
            if ($MaxDepth -gt 0) {
                $rel = $_.FullName.Substring($rootFull.Length).TrimStart('\', '/')
                $d = ($rel -split '[/\\]').Count
                $d -le $MaxDepth
            } else {
                $true
            }
        } |
        Sort-Object { $_.FullName.Replace('\', '/') }

    return $items
}

$rootFull = (Resolve-Path $Directory).Path
$entries = @(Get-Entries -Root $Directory -MaxDepth $Depth)

if ($entries.Count -eq 0) {
    Write-Output "$Directory/"
    exit 0
}

# ── Format: indented ────────────────────────────────────────────────────────

function Format-IndentedTree {
    Write-Output "$Directory/"
    foreach ($entry in $entries) {
        $rel = $entry.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
        $depthLevel = ($rel -split '/').Count
        $indent = ' ' * $depthLevel
        $name = $entry.Name
        Write-Output "$indent$name/"
    }
}

# ── Format: tree ─────────────────────────────────────────────────────────────

function Format-BoxTree {
    Write-Output "$Directory/"

    for ($idx = 0; $idx -lt $entries.Count; $idx++) {
        $entry = $entries[$idx]
        $rel = $entry.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
        $parts = $rel -split '/'
        $currentDepth = $parts.Count - 1
        $name = $entry.Name

        # Determine parent prefix for sibling check
        if ($currentDepth -gt 0) {
            $parentPrefix = ($parts[0..($parts.Count - 2)]) -join '/'
        } else {
            $parentPrefix = ""
        }

        # Check if this is the last sibling
        $isLast = $true
        for ($j = $idx + 1; $j -lt $entries.Count; $j++) {
            $otherRel = $entries[$j].FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
            $otherParts = $otherRel -split '/'
            if ($otherParts.Count - 1 -lt $currentDepth) {
                break
            }
            if ($otherParts.Count - 1 -eq $currentDepth) {
                $otherParent = if ($currentDepth -gt 0) { ($otherParts[0..($otherParts.Count - 2)]) -join '/' } else { "" }
                if ($otherParent -eq $parentPrefix) {
                    $isLast = $false
                    break
                }
            }
        }

        # Build prefix from ancestors
        $prefix = ""
        for ($d = 0; $d -lt $currentDepth; $d++) {
            $ancestorPath = ($parts[0..$d]) -join '/'
            $ancestorParent = if ($d -gt 0) { ($parts[0..($d - 1)]) -join '/' } else { "" }

            # Check if ancestor is last in its parent
            $ancestorIsLast = $true
            $ancestorFull = "$rootFull/$ancestorPath".Replace('/', '\')
            foreach ($other in $entries) {
                $otherRel2 = $other.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
                $otherParts2 = $otherRel2 -split '/'
                $otherParent2 = if ($otherParts2.Count -gt 1) { ($otherParts2[0..($otherParts2.Count - 2)]) -join '/' } else { "" }
                if ($otherParent2 -eq $ancestorParent -and ($other.FullName.Replace('\', '/') -gt $ancestorFull.Replace('\', '/'))) {
                    $ancestorIsLast = $false
                    break
                }
            }

            if ($ancestorIsLast) {
                $prefix += "    "
            } else {
                $prefix += [char]0x2502 + "   "  # │
            }
        }

        if ($isLast) {
            $connector = [char]0x2514 + [string]([char]0x2500) + [string]([char]0x2500) + " "  # └──
        } else {
            $connector = [char]0x251C + [string]([char]0x2500) + [string]([char]0x2500) + " "  # ├──
        }

        Write-Output "$prefix$connector$name/"
    }
}

# ── Format: flat ─────────────────────────────────────────────────────────────

function Build-Flat {
    param(
        [string]$Dir,
        [int]$EffectiveDepth = 0,
        [int]$MaxD = 0
    )

    # If depth limited and we've reached it, return empty
    if ($MaxD -gt 0 -and $EffectiveDepth -ge $MaxD) {
        return ""
    }

    $dirFull = (Resolve-Path $Dir).Path
    $children = @(Get-ChildItem -Path $Dir -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne '.git' -and $_.PSIsContainer } |
        Sort-Object Name)

    if ($children.Count -eq 0) {
        return ""
    }

    $parts = @()
    foreach ($child in $children) {
        $cname = $child.Name
        $sub = Build-Flat -Dir $child.FullName -EffectiveDepth ($EffectiveDepth + 1) -MaxD $MaxD
        if ($sub) {
            $parts += "$cname/$sub"
        } else {
            $parts += $cname
        }
    }

    if ($parts.Count -eq 1) {
        return $parts[0]
    } else {
        $joined = $parts -join ','
        return "{$joined}"
    }
}

function Format-FlatTree {
    $content = Build-Flat -Dir $Directory -EffectiveDepth 0 -MaxD $Depth
    if ($content) {
        Write-Output "$Directory/$content"
    } else {
        Write-Output "$Directory/"
    }
}

# ── Dispatch ─────────────────────────────────────────────────────────────────

switch ($Format) {
    "indented" { Format-IndentedTree }
    "tree"     { Format-BoxTree }
    "flat"     { Format-FlatTree }
}
