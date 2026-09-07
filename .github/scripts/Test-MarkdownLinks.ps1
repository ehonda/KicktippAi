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
                $target.StartsWith('#') -or
                $target.StartsWith('/') -or
                $target.StartsWith('//') -or
                $target -match '^[A-Za-z][A-Za-z0-9+.-]*:'
            ) {
                continue
            }

            $localPart = ($target -split '[#?]', 2)[0]
            if ([string]::IsNullOrWhiteSpace($localPart)) {
                continue
            }

            try {
                $localPart = [Uri]::UnescapeDataString($localPart)
                $resolvedTarget = [IO.Path]::GetFullPath(
                    [IO.Path]::Combine($file.DirectoryName, $localPart)
                )
                $checked++
                if (-not (Test-Path -LiteralPath $resolvedTarget)) {
                    $failures.Add([pscustomobject]@{
                        File = [IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName)
                        Line = $lineNumber
                        Target = $rawTarget
                        Resolved = $resolvedTarget
                    })
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
