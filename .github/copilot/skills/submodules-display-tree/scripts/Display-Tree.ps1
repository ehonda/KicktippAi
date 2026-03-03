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

.PARAMETER DetailView
    One or more submodule root names to show in detail.
    When specified, only matching submodules are displayed and their
    full internal directory structure is included.

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\Display-Tree.ps1
    # Flat format (default) of external/

.EXAMPLE
    .\Display-Tree.ps1 -Format indented

.EXAMPLE
    .\Display-Tree.ps1 -Format tree -Depth 2

.EXAMPLE
    .\Display-Tree.ps1 -DetailView dotnet-api-docs,openai-dotnet -Format tree
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Directory = "external",

    [ValidateSet("tree", "indented", "flat")]
    [string]$Format = "flat",

    [int]$Depth = 0,

    [string[]]$DetailView = @(),

    [switch]$Help
)

$ErrorActionPreference = "Stop"

# ── Usage ────────────────────────────────────────────────────────────────────

if ($Help) {
    @"
Usage:
  Display-Tree.ps1 [Directory] [-Format tree|indented|flat] [-Depth N]
  Display-Tree.ps1 -DetailView name1,name2 [-Format tree|indented|flat] [-Depth N]
  Display-Tree.ps1 -Help

Arguments:
  Directory             Directory to display (default: external)

Options:
  -Format FORMAT        Output format: tree, indented, flat (default: flat)
  -Depth N              Maximum traversal depth (default: 0 = unlimited)
  -DetailView NAMES     Comma-separated submodule root names to show in detail
                        (shows full internal structure for those submodules only)

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

# ── Submodule roots ──────────────────────────────────────────────────────────

$script:SubmoduleRoots = @()
$gitmodulesOutput = git config --file .gitmodules --get-regexp path 2>$null
if ($gitmodulesOutput) {
    foreach ($line in @($gitmodulesOutput)) {
        $subPath = ($line -split '\s+', 2)[1]
        if ($subPath) {
            $fullPath = Join-Path (Get-Location) $subPath
            if (Test-Path $fullPath) {
                $script:SubmoduleRoots += (Resolve-Path $fullPath).Path
            }
        }
    }
}

# Resolve detail-view selections to full paths
$script:DetailViewRoots = @()
$script:IsDetailView = $DetailView.Count -gt 0
if ($script:IsDetailView) {
    foreach ($name in $DetailView) {
        $matched = $script:SubmoduleRoots | Where-Object { (Split-Path $_ -Leaf) -eq $name }
        if (-not $matched) {
            Write-Error "Submodule root not found: $name"
            exit 1
        }
        foreach ($m in @($matched)) {
            $script:DetailViewRoots += $m
        }
    }
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
            $itemFull = $_.FullName

            if ($script:IsDetailView) {
                # In detail-view: only keep items that are ancestors of, equal to,
                # or descendants of a selected submodule root
                $keep = $false
                foreach ($dvRoot in $script:DetailViewRoots) {
                    # Item is the selected root itself
                    if ($itemFull -eq $dvRoot) { $keep = $true; break }
                    # Item is an ancestor of a selected root (on the path to it)
                    if ($dvRoot.StartsWith($itemFull + '\') -or $dvRoot.StartsWith($itemFull + '/')) {
                        $keep = $true; break
                    }
                    # Item is a descendant inside a selected root
                    if ($itemFull.StartsWith($dvRoot + '\') -or $itemFull.StartsWith($dvRoot + '/')) {
                        $keep = $true; break
                    }
                }
                $keep
            } else {
                # Default mode: exclude contents inside submodule roots
                $isInsideSubmodule = $false
                foreach ($smRoot in $script:SubmoduleRoots) {
                    if ($itemFull -ne $smRoot -and
                        ($itemFull.StartsWith($smRoot + '\') -or $itemFull.StartsWith($smRoot + '/'))) {
                        $isInsideSubmodule = $true
                        break
                    }
                }
                -not $isInsideSubmodule
            }
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
        $childFull = (Resolve-Path $child.FullName).Path

        if ($script:IsDetailView) {
            # In detail-view: only follow paths leading to or inside selected roots
            $isSelected = $script:DetailViewRoots -contains $childFull
            $isAncestor = $false
            $isDescendant = $false
            foreach ($dvRoot in $script:DetailViewRoots) {
                if ($dvRoot.StartsWith($childFull + '\') -or $dvRoot.StartsWith($childFull + '/')) {
                    $isAncestor = $true; break
                }
                if ($childFull.StartsWith($dvRoot + '\') -or $childFull.StartsWith($dvRoot + '/')) {
                    $isDescendant = $true; break
                }
            }

            if ($isSelected -or $isDescendant) {
                # Selected submodule root or inside one: recurse fully
                $sub = Build-Flat -Dir $child.FullName -EffectiveDepth ($EffectiveDepth + 1) -MaxD $MaxD
                if ($sub) {
                    $parts += "$cname/$sub"
                } else {
                    $parts += $cname
                }
            } elseif ($isAncestor) {
                # On the path to a selected root: recurse but same detail-view filtering applies
                $sub = Build-Flat -Dir $child.FullName -EffectiveDepth ($EffectiveDepth + 1) -MaxD $MaxD
                if ($sub) {
                    $parts += "$cname/$sub"
                } else {
                    $parts += $cname
                }
            }
            # else: skip this child entirely
        } else {
            # Default mode: stop at submodule roots
            $isSubmoduleRoot = $script:SubmoduleRoots -contains $childFull
            if ($isSubmoduleRoot) {
                $parts += $cname
            } else {
                $sub = Build-Flat -Dir $child.FullName -EffectiveDepth ($EffectiveDepth + 1) -MaxD $MaxD
                if ($sub) {
                    $parts += "$cname/$sub"
                } else {
                    $parts += $cname
                }
            }
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
