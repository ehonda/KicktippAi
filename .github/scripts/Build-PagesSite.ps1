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
      $items = @($reportFiles | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($ExperimentRoot, $_.FullName).Replace("\", "/")
        $label = $relativePath -replace "\.report\.html$", "" -replace "\.html$", ""
            "<li><a href='$relativePath'>$label</a></li>"
      })

        "<ul class='link-list'>{0}</ul>" -f ($items -join "`n")
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

    .link-list {
      margin: 18px 0 0;
      padding-left: 20px;
    }

    .link-list li + li {
      margin-top: 10px;
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
