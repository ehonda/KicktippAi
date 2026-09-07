[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string[]] $Path
)

$ErrorActionPreference = 'Stop'

function Get-MarkdownFiles {
    param([string[]] $InputPaths)

    $excludedSegments = @('.git', '.tmp', 'bin', 'external', 'node_modules', 'obj')
    $files = foreach ($inputPath in $InputPaths) {
        $resolved = Resolve-Path -LiteralPath $inputPath -ErrorAction Stop
        foreach ($item in $resolved) {
            if (Test-Path -LiteralPath $item.Path -PathType Leaf) {
                if ([IO.Path]::GetExtension($item.Path) -eq '.md') {
                    Get-Item -LiteralPath $item.Path
                }
                continue
            }

            Get-ChildItem -LiteralPath $item.Path -Recurse -File -Filter '*.md' |
                Where-Object {
                    $relativeSegments = [IO.Path]::GetRelativePath($item.Path, $_.FullName) -split '[\\/]'
                    -not ($relativeSegments | Where-Object { $_ -in $excludedSegments })
                }
        }
    }

    return @($files | Sort-Object FullName -Unique)
}

function Get-LocalDestinations {
    param([string] $Line)

    $destinations = [Collections.Generic.List[string]]::new()
    $inlinePattern = '!?(?:\[[^\]]*\])\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
    foreach ($match in [regex]::Matches($Line, $inlinePattern)) {
        $destinations.Add($match.Groups['target'].Value)
    }

    $referenceMatch = [regex]::Match($Line, '^\s*\[[^\]]+\]:\s*(?<target><[^>]+>|\S+)')
    if ($referenceMatch.Success) {
        $destinations.Add($referenceMatch.Groups['target'].Value)
    }

    return $destinations
}

function ConvertTo-MarkdownAnchor {
    param([string] $Heading)

    $anchor = [Net.WebUtility]::HtmlDecode($Heading)
    $anchor = [regex]::Replace($anchor, '!\[([^\]]*)\]\([^)]*\)', '$1')
    $anchor = [regex]::Replace($anchor, '\[([^\]]+)\]\([^)]*\)', '$1')
    $anchor = [regex]::Replace($anchor, '<[^>]+>', '')
    $anchor = $anchor.Replace('`', '').Replace('*', '').Replace('~', '')
    $anchor = $anchor.ToLowerInvariant()
    $anchor = [regex]::Replace($anchor, '[^\p{L}\p{M}\p{Nd}\s_-]', '')
    return [regex]::Replace($anchor, '\s', '-')
}

$anchorCache = @{}

function Get-MarkdownAnchors {
    param([string] $MarkdownPath)

    if ($anchorCache.ContainsKey($MarkdownPath)) {
        return $anchorCache[$MarkdownPath]
    }

    $anchors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $slugCounts = @{}
    $lines = [IO.File]::ReadAllLines($MarkdownPath)
    $fenceCharacter = $null
    $fenceLength = 0

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $fenceMatch = [regex]::Match($line, '^\s{0,3}(?<fence>`{3,}|~{3,})')
        if ($fenceMatch.Success) {
            $candidate = $fenceMatch.Groups['fence'].Value
            if ($null -eq $fenceCharacter) {
                $fenceCharacter = $candidate[0]
                $fenceLength = $candidate.Length
            }
            elseif ($candidate[0] -eq $fenceCharacter -and $candidate.Length -ge $fenceLength) {
                $fenceCharacter = $null
                $fenceLength = 0
            }
            continue
        }
        if ($null -ne $fenceCharacter) {
            continue
        }

        foreach ($explicitMatch in [regex]::Matches($line, '(?i)<[^>]+\s(?:id|name)=["''](?<id>[^"'']+)["''][^>]*>')) {
            [void] $anchors.Add($explicitMatch.Groups['id'].Value)
        }

        $heading = $null
        $atxMatch = [regex]::Match($line, '^\s{0,3}#{1,6}\s+(?<heading>.*?)(?:\s+#+\s*)?$')
        if ($atxMatch.Success) {
            $heading = $atxMatch.Groups['heading'].Value
        }
        elseif (
            $index -gt 0 -and
            $line -match '^\s{0,3}(?:=+|-+)\s*$' -and
            -not [string]::IsNullOrWhiteSpace($lines[$index - 1])
        ) {
            $heading = $lines[$index - 1].Trim()
        }

        if ($null -ne $heading) {
            $baseSlug = ConvertTo-MarkdownAnchor -Heading $heading
            if (-not [string]::IsNullOrWhiteSpace($baseSlug)) {
                $slug = $baseSlug
                if ($slugCounts.ContainsKey($baseSlug)) {
                    $slugCounts[$baseSlug]++
                    $slug = "$baseSlug-$($slugCounts[$baseSlug])"
                }
                else {
                    $slugCounts[$baseSlug] = 0
                }
                [void] $anchors.Add($slug)
            }
        }
    }

    $anchorCache[$MarkdownPath] = $anchors
    return $anchors
}

$failures = [Collections.Generic.List[object]]::new()
$checked = 0

foreach ($file in Get-MarkdownFiles -InputPaths $Path) {
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        foreach ($rawTarget in Get-LocalDestinations -Line $line) {
            $target = $rawTarget.Trim().Trim('<', '>') -replace '\\ ', ' '
            if (
                [string]::IsNullOrWhiteSpace($target) -or
                $target.StartsWith('/') -or
                $target.StartsWith('//') -or
                $target -match '^[A-Za-z][A-Za-z0-9+.-]*:'
            ) {
                continue
            }

            $fragment = $null
            $hashIndex = $target.IndexOf('#')
            if ($hashIndex -ge 0) {
                $fragment = $target.Substring($hashIndex + 1)
                $target = $target.Substring(0, $hashIndex)
            }
            $localPart = ($target -split '\?', 2)[0]

            try {
                $localPart = [Uri]::UnescapeDataString($localPart)
                $resolvedTarget = if ([string]::IsNullOrWhiteSpace($localPart)) {
                    $file.FullName
                }
                else {
                    [IO.Path]::GetFullPath([IO.Path]::Combine($file.DirectoryName, $localPart))
                }
                $checked++
                if (-not (Test-Path -LiteralPath $resolvedTarget)) {
                    $failures.Add([pscustomobject]@{
                        File = [IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName)
                        Line = $lineNumber
                        Target = $rawTarget
                        Resolved = $resolvedTarget
                    })
                }
                elseif (
                    -not [string]::IsNullOrWhiteSpace($fragment) -and
                    [IO.Path]::GetExtension($resolvedTarget) -in @('.md', '.markdown') -and
                    (Test-Path -LiteralPath $resolvedTarget -PathType Leaf)
                ) {
                    $decodedFragment = [Uri]::UnescapeDataString($fragment)
                    $anchors = Get-MarkdownAnchors -MarkdownPath $resolvedTarget
                    if (-not $anchors.Contains($decodedFragment)) {
                        $failures.Add([pscustomobject]@{
                            File = [IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName)
                            Line = $lineNumber
                            Target = $rawTarget
                            Resolved = "missing fragment '#$decodedFragment' in $resolvedTarget"
                        })
                    }
                }
            }
            catch {
                $failures.Add([pscustomobject]@{
                    File = [IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName)
                    Line = $lineNumber
                    Target = $rawTarget
                    Resolved = "invalid path: $($_.Exception.Message)"
                })
            }
        }
    }
}

if ($failures.Count -gt 0) {
    $failures |
        Sort-Object File, Line, Target -Unique |
        ForEach-Object {
            Write-Error "$($_.File):$($_.Line): '$($_.Target)' -> $($_.Resolved)" -ErrorAction Continue
        }
    throw "Markdown link validation failed with $($failures.Count) unresolved local target(s)."
}

Write-Output "Markdown link validation passed: $checked local target(s) checked."
