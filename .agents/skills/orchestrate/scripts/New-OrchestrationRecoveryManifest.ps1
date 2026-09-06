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
$entries = foreach ($candidate in $Path) {
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

    [pscustomobject] [ordered] @{
        id = $identifier
        path = $identifier
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
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
