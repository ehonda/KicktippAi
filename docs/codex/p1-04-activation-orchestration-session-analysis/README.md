# P1-04 activation orchestration session analysis

This directory contains the privacy-bounded source data and report-specific
enrichment for the ongoing orchestration run that closed dormant P1-04/P1-05,
merged PR #111, and then began the owner-requested operational P1-04 activation.

## Frozen boundary

- Root thread: `01a0a211-c4bc-7941-86f9-37dfba1da3fa`
- Root log: `rollout-2026-09-15T00-37-50-01a0a211-c4bc-7941-86f9-37dfba1da3fa.jsonl`
- UTC event cutoff: `2026-09-15T22:22:27.6493516Z`
- Git range: `8600f734587a2e0933b3cbeb5061c98b9881c715` through
  `a846bcbd0c23f22cd646c48ccc113ca25642ef2e`
- Analysis generation: `2026-09-15T22:22:27.6493516Z`, reported in
  `Europe/Berlin`
- Transcript privacy: complete message bodies and bounded excerpts are both
  excluded from published artifacts.

The manifest and `data/snapshot-lock.json` bind the exact root log and recursive
subagent family by cutoff-bounded byte count and SHA-256. The root session
continued after the cutoff. Later activation work is intentionally excluded.
Transcript files remain local and are never copied into this directory.

## Reproduce

From the repository root, with the owner's local Codex session family present:

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/run_analysis.py `
  --manifest docs/codex/session-analysis/reports/p1-04-activation-orchestration.report.json `
  --sessions-dir <local-codex-sessions-directory> `
  --repo . `
  --output-dir docs/codex/p1-04-activation-orchestration-session-analysis/data `
  --quiet

uv --cache-dir .uv-cache run python -B `
  docs/codex/p1-04-activation-orchestration-session-analysis/derive_metrics.py `
  --manifest docs/codex/session-analysis/reports/p1-04-activation-orchestration.report.json `
  --analysis docs/codex/p1-04-activation-orchestration-session-analysis/data/analysis.json `
  --sessions-dir <local-codex-sessions-directory> `
  --repo . `
  --output docs/codex/p1-04-activation-orchestration-session-analysis/data/derived-metrics.json

uv --cache-dir .uv-cache run python -B `
  docs/codex/p1-04-activation-orchestration-session-analysis/build_html.py
```

Do not pass `--create-snapshot-lock` on reruns. Drift in the transcript family,
included bytes, record counts, or hashes must fail instead of widening the
snapshot.

## Interpretation limits

- Token costs are API list-price equivalents dated 2026-09-16, not Codex
  subscription charges. Tool-call fees, if any, are excluded.
- The 23.7-hour wall span includes a 14.3-hour interval between the original
  closeout and the owner's review-brief request. It is not continuous agent work.
- Agent active seconds do not overlap in this run, but they remain observed turn
  intervals rather than timesheets.
- Outcome classes are report-specific labels checked against the locked final
  subagent messages; no final message text is published.
- Resource samples are discrete observations. They support conclusions about
  this run's one-family policy, not a universal concurrency factor.
- The PowerShell probe records the analysis host's version and culture. It
  demonstrates the reported compatibility mechanism but is not a claim about
  every PowerShell/culture combination.
