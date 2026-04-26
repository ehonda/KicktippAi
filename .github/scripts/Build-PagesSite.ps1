[CmdletBinding()]
param(
    [string]$CoverageReportDir = "coverage-report",
    [string]$ExperimentAnalysisDir = "experiment-analysis",
    [string]$OutputDir = "pages-site"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

Copy-DirectoryContents -SourceDir $CoverageReportDir -DestinationDir $coverageTarget
Copy-DirectoryContents -SourceDir $ExperimentAnalysisDir -DestinationDir $experimentTarget
New-ExperimentAnalysisIndex -ExperimentRoot $experimentTarget

$hasCoverage = Test-Path -Path (Join-Path $coverageTarget "index.html")
$hasExperimentAnalysis = Test-Path -Path (Join-Path $experimentTarget "index.html")

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
      <p>Coverage and experiment analysis artifacts published from the repository's GitHub Pages workflow.</p>
    </section>
    <section class="grid">
      $coverageCard
      $experimentCard
    </section>
  </main>
</body>
</html>
"@

Set-Content -Path (Join-Path $OutputDir "index.html") -Value $rootIndex -Encoding utf8
Write-Host "Pages site written to $OutputDir"
