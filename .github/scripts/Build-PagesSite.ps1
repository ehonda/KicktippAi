[CmdletBinding()]
param(
    [string]$CoverageReportDir = "coverage-report",
    [string]$ExperimentAnalysisDir = "experiment-analysis",
    [string]$SessionAnalysisDir = "session-analysis",
    [string]$SessionAnalysisManifestDir = "docs/codex/session-analysis/reports",
    [string]$RepositoryRoot = ".",
    [string]$OutputDir = "pages-site"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:PathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$script:PathComparer = if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }

function Initialize-Directory {
    param([string]$Path)

    if (Test-Path -Path $Path)
    {
        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Copy-DirectoryContents {
    param(
        [string]$SourceDir,
        [string]$DestinationDir
    )

    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null

    if (-not (Test-Path -Path $SourceDir))
    {
        return
    }

    foreach ($item in Get-ChildItem -Path $SourceDir -Force)
    {
        Copy-Item -Path $item.FullName -Destination $DestinationDir -Recurse -Force
    }
}

function Escape-Html {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value)
    {
        return ""
    }

    return [System.Net.WebUtility]::HtmlEncode([string]$Value)
}

function ConvertTo-TitleLabel {
    param([string]$Value)

    $clean = ($Value -replace "[-_]+", " ").Trim()
    if ([string]::IsNullOrWhiteSpace($clean))
    {
        return ""
    }

    $textInfo = [System.Globalization.CultureInfo]::InvariantCulture.TextInfo
    return $textInfo.ToTitleCase($clean.ToLowerInvariant())
}

function Get-NormalizedTextSha256 {
    param([string]$Path)

    $content = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($content)
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($sha.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally
    {
        $sha.Dispose()
    }
}

function Resolve-PhysicalPath {
    param([string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $pathRoot = [IO.Path]::GetPathRoot($fullPath)
    $current = $pathRoot
    $relative = [IO.Path]::GetRelativePath($pathRoot, $fullPath)
    foreach ($component in @($relative -split "[\\/]" | Where-Object { $_ }))
    {
        $next = Join-Path $current $component
        if (Test-Path -LiteralPath $next)
        {
            $item = Get-Item -LiteralPath $next -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                $target = $item.ResolveLinkTarget($true)
                if ($null -eq $target)
                {
                    throw "Unable to resolve symbolic-link component: $next"
                }
                $current = $target.FullName
            }
            else
            {
                $current = $item.FullName
            }
        }
        else
        {
            $current = $next
        }
    }
    return [IO.Path]::GetFullPath($current)
}

function Resolve-ContainedPath {
    param(
        [string]$Root,
        [string]$RelativePath,
        [string]$Field
    )

    if ($RelativePath -notmatch "^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$" -or
        @($RelativePath -split "/" | Where-Object { $_ -in @("", ".", "..") }).Count -ne 0)
    {
        throw "$Field must be a safe normalized relative path"
    }
    $resolvedRoot = (Resolve-PhysicalPath -Path $Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $resolvedRoot ($RelativePath -replace "/", [IO.Path]::DirectorySeparatorChar)))
    $candidate = Resolve-PhysicalPath -Path $candidate
    $prefix = $resolvedRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, $script:PathComparison))
    {
        throw "$Field resolves outside its allowed root"
    }
    return $candidate
}

function Get-PublishableText {
    param([string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xff -and $bytes[1] -eq 0xfe)
    {
        return [Text.Encoding]::Unicode.GetString($bytes)
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xfe -and $bytes[1] -eq 0xff)
    {
        return [Text.Encoding]::BigEndianUnicode.GetString($bytes)
    }
    try
    {
        return ([Text.UTF8Encoding]::new($false, $true)).GetString($bytes)
    }
    catch [Text.DecoderFallbackException]
    {
        if ($bytes -contains 0)
        {
            return $null
        }
        $sampleLength = [Math]::Min(8192, $bytes.Length)
        $controls = 0
        for ($index = 0; $index -lt $sampleLength; $index++)
        {
            if ($bytes[$index] -lt 32 -and $bytes[$index] -notin @(9, 10, 13))
            {
                $controls++
            }
        }
        if ($sampleLength -gt 0 -and ($controls / $sampleLength) -gt 0.05)
        {
            return $null
        }
        return [Text.Encoding]::GetEncoding(1252).GetString($bytes)
    }
}

function Assert-PublishableTextPrivacy {
    param([string[]]$Paths)

    $patterns = @(
        '(?i)(?:[A-Z]:[\\/]+(?:Users|Documents and Settings)[\\/]+[^\\/\s]+|/(?:home|Users)/[^/\s]+|/root/(?:[A-Za-z0-9._-]+/)*(?:\.[A-Za-z0-9._-]+|private(?=[/\s"''<>]|$)|[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)+(?=[/\s"''<>]|$))|/var/root(?=[/\s"''<>]|$))',
        '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
        '\b(?:sk-(?:proj-)?|ghp_|github_pat_|glpat-)[A-Za-z0-9_-]{16,}',
        '\bAKIA[0-9A-Z]{16}\b',
        '(?i)\bBearer\s+[A-Za-z0-9._~+/=-]{20,}'
    )
    $seen = [Collections.Generic.HashSet[string]]::new($script:PathComparer)
    foreach ($path in $Paths)
    {
        $files = if (Test-Path -LiteralPath $path -PathType Container)
        {
            @(Get-ChildItem -LiteralPath $path -File -Recurse)
        }
        elseif (Test-Path -LiteralPath $path -PathType Leaf)
        {
            @((Get-Item -LiteralPath $path))
        }
        else
        {
            @()
        }
        foreach ($file in $files)
        {
            if (-not $seen.Add($file.FullName))
            {
                continue
            }
            $content = Get-PublishableText -Path $file.FullName
            if ($null -eq $content)
            {
                continue
            }
            foreach ($pattern in $patterns)
            {
                if ($content -match $pattern)
                {
                    throw "Private path or possible credential remains in publishable output: $($file.FullName)"
                }
            }
        }
    }
}

function Test-SessionAnalysisArtifact {
    param(
        [object]$Manifest,
        [string]$ManifestPath,
        [string]$RepoRoot,
        [string]$SourcePath
    )

    $analysisRelative = ([string]$Manifest.analysis_file).Substring(([string]$Manifest.source_path).Length + 1)
    $analysisPath = Resolve-ContainedPath -Root $SourcePath -RelativePath $analysisRelative -Field "analysis_file"
    if (-not (Test-Path -LiteralPath $analysisPath -PathType Leaf))
    {
        throw "Normalized session-analysis artifact is missing for $($Manifest.id): $analysisPath"
    }
    $analysis = Get-Content -LiteralPath $analysisPath -Raw | ConvertFrom-Json
    if ($null -eq $analysis.source)
    {
        throw "Normalized session-analysis artifact has no source object: $analysisPath"
    }
    $source = $analysis.source
    if ($source.complete_message_bodies_included -ne $false)
    {
        throw "Normalized session-analysis artifact does not exclude complete messages for $($Manifest.id)"
    }
    $relativeManifest = [IO.Path]::GetRelativePath($RepoRoot, $ManifestPath).Replace("\", "/")
    $manifestHash = Get-NormalizedTextSha256 -Path $ManifestPath
    if ([string]$source.report_manifest -cne $relativeManifest -or
        [string]$source.report_manifest_sha256 -cne $manifestHash)
    {
        throw "Normalized session-analysis manifest binding drifted for $($Manifest.id)"
    }
    if ((@($source.focus_ids) | ConvertTo-Json -Compress) -cne (@($Manifest.focus_ids) | ConvertTo-Json -Compress))
    {
        throw "Normalized session-analysis focus IDs drifted for $($Manifest.id)"
    }
    $expected = $Manifest.analysis
    $rootLog = ([string]$source.root_log).Replace("\", "/").Split("/")[-1]
    if ([string]$analysis.generated_at -cne [string]$expected.generated_at -or
        [string]$source.root_thread_id -cne [string]$expected.root_thread_id -or
        $rootLog -cne [string]$expected.root_log_name -or
        [string]$source.event_cutoff_at -cne [string]$expected.event_cutoff_at -or
        [string]$source.repository.base_commit -cne [string]$expected.repository.base_commit -or
        [string]$source.repository.final_commit -cne [string]$expected.repository.final_commit -or
        [bool]$source.bounded_excerpts_included -ne [bool]$expected.privacy.include_bounded_excerpts)
    {
        throw "Normalized session-analysis snapshot contract drifted for $($Manifest.id)"
    }
    if (-not ([string]$expected.snapshot_lock).StartsWith(([string]$Manifest.source_path) + "/", [StringComparison]::Ordinal))
    {
        throw "snapshot_lock must be under source_path for $($Manifest.id)"
    }
    $lockRelative = ([string]$expected.snapshot_lock).Substring(([string]$Manifest.source_path).Length + 1)
    $lockPath = Resolve-ContainedPath -Root $SourcePath -RelativePath $lockRelative -Field "snapshot_lock"
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf))
    {
        throw "Session-analysis snapshot lock is missing for $($Manifest.id): $lockPath"
    }
    $lockHash = Get-NormalizedTextSha256 -Path $lockPath
    if ([string]$source.snapshot_lock -cne [string]$expected.snapshot_lock -or
        [string]$source.snapshot_lock_sha256 -cne $lockHash)
    {
        throw "Normalized session-analysis snapshot-lock binding drifted for $($Manifest.id)"
    }
}

function Get-SessionAnalysisReports {
    param(
        [string]$ManifestDir,
        [string]$SessionSourceRoot,
        [string]$RepoRoot
    )

    if (-not (Test-Path -LiteralPath $ManifestDir -PathType Container))
    {
        throw "Session-analysis manifest directory does not exist: $ManifestDir"
    }

    $manifestFiles = @(Get-ChildItem -LiteralPath $ManifestDir -File -Filter "*.report.json" | Sort-Object Name)
    if ($manifestFiles.Count -eq 0)
    {
        throw "No session-analysis report manifests found in $ManifestDir"
    }

    $requiredFields = @(
        "analysis", "analysis_file", "eyebrow", "focus_ids", "html_file", "id",
        "legacy", "published_at", "schema_version", "session_kind", "site_path",
        "source_path", "summary", "title"
    )
    $legacyIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    @(
        "p0-closeout", "p1-context-refresh", "p1-orchestration-follow-up",
        "p1-orchestration-interim", "urgent-production-orchestration"
    ) | ForEach-Object { [void]$legacyIds.Add($_) }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $sitePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $reports = foreach ($manifestFile in $manifestFiles)
    {
        try
        {
            $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
        }
        catch
        {
            throw "Invalid session-analysis manifest $($manifestFile.FullName): $($_.Exception.Message)"
        }

        $actualFields = @($manifest.PSObject.Properties.Name | Sort-Object)
        if (($actualFields -join "`n") -ne ($requiredFields -join "`n"))
        {
            throw "Unexpected fields in session-analysis manifest $($manifestFile.Name)"
        }
        if ($manifest.schema_version -ne 1)
        {
            throw "Unsupported schema_version in $($manifestFile.Name)"
        }
        if ([string]$manifest.id -notmatch "^[a-z0-9]+(?:-[a-z0-9]+)*$")
        {
            throw "Invalid report ID in $($manifestFile.Name)"
        }
        if (-not $ids.Add([string]$manifest.id))
        {
            throw "Duplicate session-analysis report ID: $($manifest.id)"
        }
        $expectedLegacy = $legacyIds.Contains([string]$manifest.id)
        if ($manifest.legacy -isnot [bool] -or [bool]$manifest.legacy -ne $expectedLegacy)
        {
            throw "legacy must match the fixed report allowlist in $($manifestFile.Name)"
        }
        if ($expectedLegacy)
        {
            if ($null -ne $manifest.analysis -or $null -ne $manifest.analysis_file -or @($manifest.focus_ids).Count -ne 0)
            {
                throw "Legacy report fields are invalid in $($manifestFile.Name)"
            }
        }
        else
        {
            if ($null -eq $manifest.analysis -or [string]::IsNullOrWhiteSpace([string]$manifest.analysis_file))
            {
                throw "Future report requires analysis and analysis_file in $($manifestFile.Name)"
            }
            if ([string]$manifest.analysis_file -notmatch "^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$" -or
                @(([string]$manifest.analysis_file) -split "/" | Where-Object { $_ -in @("", ".", "..") }).Count -ne 0 -or
                -not ([string]$manifest.analysis_file).StartsWith(([string]$manifest.source_path) + "/"))
            {
                throw "Unsafe analysis_file in $($manifestFile.Name)"
            }
        }
        foreach ($field in @("title", "summary", "eyebrow", "published_at", "session_kind", "source_path", "site_path", "html_file"))
        {
            if ([string]::IsNullOrWhiteSpace([string]$manifest.$field))
            {
                throw "Missing $field in $($manifestFile.Name)"
            }
        }
        foreach ($field in @("source_path", "site_path", "html_file"))
        {
            if ([string]$manifest.$field -notmatch "^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$" -or
                @(([string]$manifest.$field) -split "/" | Where-Object { $_ -in @("", ".", "..") }).Count -ne 0)
            {
                throw "Unsafe $field in $($manifestFile.Name)"
            }
        }
        if ([string]$manifest.site_path -notmatch "^session-analysis/")
        {
            throw "site_path must be under session-analysis/ in $($manifestFile.Name)"
        }
        if (-not $sitePaths.Add([string]$manifest.site_path))
        {
            throw "Duplicate session-analysis site_path: $($manifest.site_path)"
        }
        try
        {
            $publishedAt = [DateTime]::ParseExact(
                [string]$manifest.published_at,
                "yyyy-MM-dd",
                [Globalization.CultureInfo]::InvariantCulture
            )
        }
        catch
        {
            throw "published_at must use YYYY-MM-DD in $($manifestFile.Name)"
        }

        $relativeSitePath = ([string]$manifest.site_path).Substring("session-analysis/".Length)
        $reportDirectory = Resolve-ContainedPath -Root $SessionSourceRoot -RelativePath $relativeSitePath -Field "site_path"
        $publishedHtml = Resolve-ContainedPath -Root $reportDirectory -RelativePath ([string]$manifest.html_file) -Field "html_file"
        if (-not (Test-Path -LiteralPath $publishedHtml -PathType Leaf))
        {
            throw "Published session-analysis HTML is missing for $($manifest.id): $publishedHtml"
        }
        $sourcePath = Resolve-ContainedPath -Root $RepoRoot -RelativePath ([string]$manifest.source_path) -Field "source_path"
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Container))
        {
            throw "Session-analysis source path is missing for $($manifest.id): $sourcePath"
        }
        Assert-PublishableTextPrivacy -Paths @($manifestFile.FullName, $reportDirectory)
        if (-not $expectedLegacy)
        {
            Test-SessionAnalysisArtifact -Manifest $manifest -ManifestPath $manifestFile.FullName -RepoRoot $RepoRoot -SourcePath $sourcePath
            Assert-PublishableTextPrivacy -Paths @($sourcePath)
        }

        [pscustomobject]@{
            Id = [string]$manifest.id
            Title = [string]$manifest.title
            Summary = [string]$manifest.summary
            Eyebrow = [string]$manifest.eyebrow
            PublishedAt = $publishedAt
            Href = "$($manifest.site_path)/$($manifest.html_file)"
            SitePath = [string]$manifest.site_path
            HtmlFile = [string]$manifest.html_file
            SourceHtml = $publishedHtml
        }
    }

    return @($reports | Sort-Object @{ Expression = "PublishedAt"; Descending = $true }, Title)
}

function New-SessionAnalysisCards {
    param([object[]]$Reports)

    $cards = foreach ($report in $Reports)
    {
        $href = Escape-Html -Value $report.Href
        $eyebrow = Escape-Html -Value $report.Eyebrow
        $title = Escape-Html -Value $report.Title
        $summary = Escape-Html -Value $report.Summary
        "<a class='card' href='$href'><span class='eyebrow'>$eyebrow</span><strong>$title</strong><p>$summary</p></a>"
    }
    return $cards -join "`n      "
}

function Get-ExperimentReportTypeLabel {
    param([string]$TypeSegment)

    switch ($TypeSegment)
    {
        "community-to-date" { return "Community To Date" }
        "slices" { return "Slices" }
        default { return ConvertTo-TitleLabel -Value $TypeSegment }
    }
}

function Get-ExperimentReportDisplayName {
    param(
        [string]$RelativePath,
        [string]$FullPath = ""
    )

    $htmlDisplayName = Get-ExperimentReportDisplayNameFromHtml -Path $FullPath
    if (-not [string]::IsNullOrWhiteSpace($htmlDisplayName))
    {
        return $htmlDisplayName
    }

    $segments = @($RelativePath -split "/")
    $fileName = $segments[-1]
    $stem = $fileName -replace "\.report\.html$", "" -replace "\.html$", "" -replace "\.analysis$", ""
    $lastDirectory = if ($segments.Count -gt 1) { $segments[$segments.Count - 2] } else { "" }
    $timestampPattern = "\d{4}-\d{2}-\d{2}t\d{2}-\d{2}-\d{2}z"
    $timestampMatch = [regex]::Match($stem, $timestampPattern)

    if ($stem.Contains("__"))
    {
        $parts = @($stem -split "__")
        $prefix = $parts[0]
        $suffix = $parts[-1]

        if ($suffix -match "^$timestampPattern$")
        {
            $isGenericPrefix = $prefix -eq $lastDirectory -or $prefix -match "^(community-to-date-md\d+|comparison)$"
            if (-not [string]::IsNullOrWhiteSpace($prefix) -and -not $isGenericPrefix)
            {
                return ConvertTo-TitleLabel -Value $prefix
            }

            return $suffix
        }
    }

    $comparisonMatch = [regex]::Match($stem, "^comparison[-_]($timestampPattern)$")
    if ($comparisonMatch.Success)
    {
        return $comparisonMatch.Groups[1].Value
    }

    if ($timestampMatch.Success)
    {
        return $timestampMatch.Value
    }

    return ConvertTo-TitleLabel -Value $stem
}

function Get-ExperimentReportDisplayNameFromHtml {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -Path $Path))
    {
        return ""
    }

    $content = Get-Content -Path $Path -Raw -Encoding utf8
    $metaMatch = [regex]::Match(
        $content,
        '<meta\s+name="kicktippai-report-title"\s+content="(?<title>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($metaMatch.Success)
    {
        return [System.Net.WebUtility]::HtmlDecode($metaMatch.Groups["title"].Value).Trim()
    }

    $modelMatches = [regex]::Matches(
        $content,
        '<div\s+class="model-name">(?<name>.*?)</div>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $modelNames = @($modelMatches |
        ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Groups["name"].Value).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique -First 3)

    if ($modelNames.Count -ge 2)
    {
        return $modelNames -join " vs "
    }

    return ""
}

function Get-ExperimentReportContext {
    param([string]$RelativePath)

    $segments = @($RelativePath -split "/")
    if ($segments.Count -le 3)
    {
        return ""
    }

    $contextSegments = @($segments[2..($segments.Count - 2)])
    return $contextSegments -join " / "
}

function Get-ExperimentReportMetadata {
    param(
        [string]$RelativePath,
        [string]$FullPath = ""
    )

    $segments = @($RelativePath -split "/")
    $typeKey = if ($segments.Count -gt 0) { $segments[0] } else { "reports" }
    $community = if ($segments.Count -gt 1) { $segments[1] } else { "general" }

    [pscustomobject]@{
        RelativePath = $RelativePath
        TypeKey = $typeKey
        TypeLabel = Get-ExperimentReportTypeLabel -TypeSegment $typeKey
        Community = $community
        DisplayName = Get-ExperimentReportDisplayName -RelativePath $RelativePath -FullPath $FullPath
        Context = Get-ExperimentReportContext -RelativePath $RelativePath
    }
}

function New-ExperimentReportTree {
    param([object[]]$Reports)

    if ($Reports.Count -eq 0)
    {
        return "<p class='empty'>No published experiment analysis reports yet.</p>"
    }

    $sortedReports = @($Reports | Sort-Object TypeLabel, Community, DisplayName, Context)
    $typeNodes = @($sortedReports | Group-Object TypeLabel | ForEach-Object {
        $typeName = $_.Name
        $typeReports = @($_.Group)
        $communityNodes = @($typeReports | Group-Object Community | ForEach-Object {
            $communityName = $_.Name
            $communityReports = @($_.Group | Sort-Object DisplayName, Context)
            $links = @($communityReports | ForEach-Object {
                $href = Escape-Html -Value $_.RelativePath
                $title = Escape-Html -Value $_.DisplayName
                $context = Escape-Html -Value $_.Context
                $meta = if ([string]::IsNullOrWhiteSpace($_.Context))
                {
                    ""
                }
                else
                {
                    "<span class='report-meta'>$context</span>"
                }

                "<a class='report-link' href='$href'><span class='report-title'>$title</span>$meta</a>"
            })
            $communityLabel = Escape-Html -Value $communityName
            $communityCount = $communityReports.Count

@"
        <details class="tree-group community-group" open>
          <summary><span>$communityLabel</span><span class="count">$communityCount</span></summary>
          <div class="tree-children">
            $($links -join "`n            ")
          </div>
        </details>
"@
        })
        $typeLabel = Escape-Html -Value $typeName
        $typeCount = $typeReports.Count

@"
      <details class="tree-group" open>
        <summary><span>$typeLabel</span><span class="count">$typeCount</span></summary>
        <div class="tree-children">
          $($communityNodes -join "`n          ")
        </div>
      </details>
"@
    })

    "<div class='report-tree'>{0}</div>" -f ($typeNodes -join "`n")
}

function New-ExperimentAnalysisIndex {
    param([string]$ExperimentRoot)

    $reportFiles = @()
    if (Test-Path -Path $ExperimentRoot)
    {
        $reportFiles = @(Get-ChildItem -Path $ExperimentRoot -Filter *.html -Recurse |
            Where-Object { $_.Name -ne "index.html" } |
            Sort-Object FullName)
    }

    $reportLinks = if ($reportFiles.Count -eq 0)
    {
        "<p class='empty'>No published experiment analysis reports yet.</p>"
    }
    else
    {
        $reportRecords = @($reportFiles | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($ExperimentRoot, $_.FullName).Replace("\", "/")
            Get-ExperimentReportMetadata -RelativePath $relativePath -FullPath $_.FullName
        })

        New-ExperimentReportTree -Reports $reportRecords
    }

    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Experiment Analysis</title>
  <style>
    :root {
      --bg: #f4efe6;
      --panel: rgba(255, 252, 246, 0.94);
      --text: #1e1a16;
      --muted: #6d6258;
      --border: rgba(86, 69, 53, 0.16);
      --accent: #b5532f;
      --accent-soft: rgba(181, 83, 47, 0.12);
    }

    body {
      margin: 0;
      font-family: "Segoe UI", "Trebuchet MS", sans-serif;
      color: var(--text);
      background: linear-gradient(180deg, #f7f1e8 0%, var(--bg) 100%);
    }

    main {
      max-width: 1040px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .panel {
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 24px;
      padding: 24px;
      box-shadow: 0 24px 70px rgba(70, 45, 26, 0.12);
    }

    h1 {
      margin: 0 0 12px;
      font-size: clamp(2rem, 4vw, 3rem);
    }

    p {
      color: var(--muted);
      line-height: 1.6;
    }

    a {
      color: var(--accent);
      text-decoration: none;
      font-weight: 600;
    }

    a:hover {
      text-decoration: underline;
    }

    .report-tree {
      margin-top: 22px;
      display: grid;
      gap: 12px;
    }

    .tree-group {
      border: 1px solid var(--border);
      border-radius: 16px;
      background: rgba(255, 250, 241, 0.72);
      overflow: hidden;
    }

    .community-group {
      background: rgba(255, 252, 246, 0.86);
    }

    summary {
      cursor: pointer;
      display: flex;
      justify-content: space-between;
      gap: 12px;
      align-items: center;
      padding: 12px 14px;
      font-weight: 700;
    }

    summary:hover {
      background: var(--accent-soft);
    }

    .count {
      min-width: 1.9rem;
      border-radius: 999px;
      padding: 3px 8px;
      text-align: center;
      color: var(--accent);
      background: var(--accent-soft);
      font-size: 0.82rem;
    }

    .tree-children {
      display: grid;
      gap: 10px;
      padding: 0 14px 14px;
    }

    .tree-children .tree-children {
      padding: 0 0 10px;
    }

    .report-link {
      display: grid;
      gap: 3px;
      border-left: 3px solid var(--accent);
      padding: 8px 0 8px 12px;
    }

    .report-title {
      font-weight: 700;
    }

    .report-meta {
      color: var(--muted);
      font-size: 0.9rem;
      font-weight: 400;
    }

    .empty {
      margin: 18px 0 0;
    }
  </style>
</head>
<body>
  <main>
    <section class="panel">
      <h1>Experiment Analysis</h1>
      <p>Published browser-friendly experiment reports from the repository.</p>
      $reportLinks
    </section>
  </main>
</body>
</html>
"@

    Set-Content -Path (Join-Path $ExperimentRoot "index.html") -Value $html -Encoding utf8
}

Initialize-Directory -Path $OutputDir

$coverageTarget = Join-Path $OutputDir "coverage"
$experimentTarget = Join-Path $OutputDir "experiment-analysis"
$sessionAnalysisTarget = Join-Path $OutputDir "session-analysis"

Copy-DirectoryContents -SourceDir $CoverageReportDir -DestinationDir $coverageTarget
Copy-DirectoryContents -SourceDir $ExperimentAnalysisDir -DestinationDir $experimentTarget
New-ExperimentAnalysisIndex -ExperimentRoot $experimentTarget

$hasCoverage = Test-Path -Path (Join-Path $coverageTarget "index.html")
$hasExperimentAnalysis = Test-Path -Path (Join-Path $experimentTarget "index.html")
$resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$resolvedSessionAnalysisSource = [IO.Path]::GetFullPath($SessionAnalysisDir)
$sessionAnalysisReports = Get-SessionAnalysisReports -ManifestDir $SessionAnalysisManifestDir -SessionSourceRoot $resolvedSessionAnalysisSource -RepoRoot $resolvedRepositoryRoot
foreach ($report in $sessionAnalysisReports)
{
    $destinationDirectory = Resolve-ContainedPath -Root $OutputDir -RelativePath $report.SitePath -Field "published site_path"
    $destinationHtml = Resolve-ContainedPath -Root $destinationDirectory -RelativePath $report.HtmlFile -Field "published html_file"
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destinationHtml)) -Force | Out-Null
    Copy-Item -LiteralPath $report.SourceHtml -Destination $destinationHtml -Force
}
$sessionAnalysisCards = New-SessionAnalysisCards -Reports $sessionAnalysisReports

$coverageCard = if ($hasCoverage)
{
    "<a class='card' href='coverage/index.html'><span class='eyebrow'>Coverage</span><strong>Code coverage report</strong><p>Browse the merged HTML coverage output.</p></a>"
}
else
{
    "<section class='card card-disabled'><span class='eyebrow'>Coverage</span><strong>Code coverage report</strong><p>No coverage report is available in this Pages bundle.</p></section>"
}

$experimentCard = if ($hasExperimentAnalysis)
{
    "<a class='card' href='experiment-analysis/index.html'><span class='eyebrow'>Experiment analysis</span><strong>Published experiment reports</strong><p>Open the browser-friendly Langfuse analysis artifacts.</p></a>"
}
else
{
    "<section class='card card-disabled'><span class='eyebrow'>Experiment analysis</span><strong>Published experiment reports</strong><p>No experiment reports have been published yet.</p></section>"
}

$rootIndex = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>KicktippAi reports</title>
  <style>
    :root {
      --bg: #ede6da;
      --panel: rgba(255, 252, 246, 0.94);
      --text: #1e1a16;
      --muted: #6d6258;
      --border: rgba(86, 69, 53, 0.16);
      --accent: #b5532f;
      --accent-soft: rgba(181, 83, 47, 0.12);
      --shadow: 0 24px 70px rgba(70, 45, 26, 0.12);
    }

    body {
      margin: 0;
      font-family: "Segoe UI", "Trebuchet MS", sans-serif;
      color: var(--text);
      background:
        radial-gradient(circle at top left, rgba(181, 83, 47, 0.12), transparent 34%),
        linear-gradient(180deg, #f7f1e8 0%, var(--bg) 100%);
    }

    main {
      max-width: 1100px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .hero {
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 28px;
      padding: 28px;
      box-shadow: var(--shadow);
      margin-bottom: 24px;
    }

    .eyebrow {
      display: block;
      color: var(--accent);
      font-size: 0.78rem;
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      margin-bottom: 8px;
    }

    h1 {
      margin: 0 0 12px;
      font-size: clamp(2.2rem, 4vw, 3.4rem);
      line-height: 1.04;
    }

    p {
      color: var(--muted);
      line-height: 1.6;
      margin: 0;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 16px;
    }

    .card {
      display: block;
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 24px;
      padding: 22px;
      box-shadow: var(--shadow);
      color: inherit;
      text-decoration: none;
    }

    .card:hover {
      transform: translateY(-2px);
      transition: transform 150ms ease;
    }

    .card strong {
      display: block;
      font-size: 1.28rem;
      margin-bottom: 10px;
    }

    .card p {
      margin-top: 0;
    }

    .card-disabled {
      opacity: 0.72;
    }

    @media (max-width: 720px) {
      main {
        padding: 20px 14px 32px;
      }

      .hero,
      .card {
        padding: 18px;
        border-radius: 20px;
      }
    }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <span class="eyebrow">KicktippAi</span>
      <h1>Published Reports</h1>
      <p>Coverage, experiment analysis, and engineering-session investigations published from the repository's GitHub Pages workflow.</p>
    </section>
    <section class="grid">
      $coverageCard
      $experimentCard
      $sessionAnalysisCards
    </section>
  </main>
</body>
</html>
"@

Set-Content -Path (Join-Path $OutputDir "index.html") -Value $rootIndex -Encoding utf8
Assert-PublishableTextPrivacy -Paths @($sessionAnalysisTarget, (Join-Path $OutputDir "index.html"))
Write-Host "Pages site written to $OutputDir"
