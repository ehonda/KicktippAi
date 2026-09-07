[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RunId,
    [Parameter(Mandatory)]
    [ValidateSet('instruction', 'hook', 'active-contract')]
    [string] $Packet,
    [Parameter(Mandatory)]
    [string[]] $Path,
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

if (
    $RunId.Length -gt 128 -or
    $RunId -in @('.', '..') -or
    [System.IO.Path]::GetFileName($RunId) -ne $RunId -or
    $RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    throw 'The orchestration run ID is not a safe path segment.'
}

$runDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $RepositoryRoot ".tmp/orchestration/$RunId"))
$orchestrationRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $RepositoryRoot '.tmp/orchestration'))
$expectedPrefix = $orchestrationRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $runDirectory.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The orchestration run directory escaped the repository orchestration root.'
}

$repositoryPrefix = $RepositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

function Get-PreviewDeclaredPaths {
    param([Parameter(Mandatory)][string] $Kind)

    $previewPath = Join-Path $runDirectory 'preview.md'
    if (-not (Test-Path -LiteralPath $previewPath -PathType Leaf)) {
        return @()
    }
    $start = "<!-- orchestration-${Kind}:start -->"
    $end = "<!-- orchestration-${Kind}:end -->"
    $inside = $false
    $found = $false
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($previewPath)) {
        $trimmed = $line.Trim()
        if ($trimmed -ceq $start) {
            if ($found -or $inside) { throw "preview.md contains duplicate $Kind packet markers." }
            $inside = $true
            $found = $true
            continue
        }
        if ($trimmed -ceq $end) {
            if (-not $inside) { throw "preview.md contains an unmatched $Kind packet end marker." }
            $inside = $false
            continue
        }
        if ($inside -and -not [string]::IsNullOrWhiteSpace($trimmed)) {
            $result.Add($trimmed)
        }
    }
    if (-not $found -or $inside) {
        throw "preview.md must contain one complete $Kind packet marker block."
    }
    return @($result)
}

$candidates = [System.Collections.Generic.List[string]]::new()
foreach ($candidate in $Path) { $candidates.Add($candidate) }
if ($Packet -eq 'instruction') {
    $candidates.Add('AGENTS.md')
    $candidates.Add('.agents/skills/orchestrate/SKILL.md')
    foreach ($candidate in (Get-PreviewDeclaredPaths -Kind 'instruction-inputs')) { $candidates.Add($candidate) }
    foreach ($contractPath in (Get-PreviewDeclaredPaths -Kind 'active-contract')) {
        if ([System.IO.Path]::IsPathRooted($contractPath)) { continue }
        $contractAbsolutePath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $contractPath))
        $directory = Split-Path -Parent $contractAbsolutePath
        while ($directory.StartsWith($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $agentsPath = Join-Path $directory 'AGENTS.md'
            if (Test-Path -LiteralPath $agentsPath -PathType Leaf) { $candidates.Add($agentsPath) }
            if ($directory.Equals($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) { break }
            $parent = Split-Path -Parent $directory
            if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $directory) { break }
            $directory = $parent
        }
    }
}
elseif ($Packet -eq 'hook') {
    foreach ($candidate in @(
        '.codex/hooks.json',
        '.agents/skills/orchestrate/scripts/Invoke-OrchestrationCapsuleHook.ps1',
        '.agents/skills/orchestrate/scripts/Get-OrchestrationRecoverySnapshot.ps1',
        '.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1',
        '.agents/skills/orchestrate/scripts/New-OrchestrationRecoveryManifest.ps1',
        '.agents/skills/orchestrate/scripts/Set-OrchestrationMemoryCircuitBreaker.ps1',
        'New-AgentWorktree.ps1',
        '.agents/skills/orchestrate/resources/resource-policy.json')) {
        $candidates.Add($candidate)
    }
}
else {
    $candidates.Add(".tmp/orchestration/$RunId/preview.md")
    foreach ($candidate in (Get-PreviewDeclaredPaths -Kind 'active-contract')) { $candidates.Add($candidate) }
}

$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$entries = [System.Collections.Generic.List[object]]::new()
for ($candidateIndex = 0; $candidateIndex -lt $candidates.Count; $candidateIndex++) {
    $candidate = $candidates[$candidateIndex]
    $absolutePath = if ([System.IO.Path]::IsPathRooted($candidate)) {
        [System.IO.Path]::GetFullPath($candidate)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $candidate))
    }
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        throw "Recovery packet input does not exist: $candidate"
    }

    $identifier = if ($absolutePath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        [System.IO.Path]::GetRelativePath($RepositoryRoot, $absolutePath).Replace('\', '/')
    }
    else {
        $absolutePath.Replace('\', '/')
    }

    if (-not $seen.Add($identifier)) {
        continue
    }

    $entries.Add([pscustomobject] [ordered] @{
        id = $identifier
        path = $identifier
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
    })

    if ($Packet -eq 'instruction') {
        foreach ($line in [System.IO.File]::ReadAllLines($absolutePath)) {
            if ($line -match '^\s*@(?<include>[^\s]+)\s*$') {
                $includedPath = [string] $Matches.include
                $resolvedInclude = if ([System.IO.Path]::IsPathRooted($includedPath)) {
                    $includedPath
                }
                else {
                    Join-Path (Split-Path -Parent $absolutePath) $includedPath
                }
                $candidates.Add($resolvedInclude)
            }
        }
    }
}

$orderedEntries = @($entries | Sort-Object -Property path -CaseSensitive)
if ($orderedEntries.Count -eq 0) {
    throw 'A recovery packet manifest must contain at least one entry.'
}
if (@($orderedEntries.path | Select-Object -Unique).Count -ne $orderedEntries.Count) {
    throw 'A recovery packet manifest cannot contain duplicate normalized paths.'
}

$manifest = [ordered] @{
    schema_version = 1
    packet = $Packet
    entries = $orderedEntries
}
$manifestPath = Join-Path $runDirectory "${Packet}-manifest.json"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$temporaryPath = "$manifestPath.$([Guid]::NewGuid().ToString('N')).tmp"
try {
    $json = $manifest | ConvertTo-Json -Depth 5 -Compress
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $manifestPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

[pscustomobject] [ordered] @{
    packet = $Packet
    path = ".tmp/orchestration/$RunId/$Packet-manifest.json"
    sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    entries = $orderedEntries.Count
}
