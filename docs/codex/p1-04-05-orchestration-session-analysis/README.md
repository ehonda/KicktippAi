# P1-04 / P1-05 orchestration session analysis

This directory contains the privacy-bounded source data and presentation for
the orchestration run that began on 2026-09-07 and stopped at draft PR #111 on
2026-09-09.

## Frozen boundary

- Root thread: `01a07d0b-35b7-73b3-8ea4-dd57b89cbd81`
- UTC event cutoff: `2026-09-09T19:29:13.555Z`
- Git range: `fa170211b74e71c23d149be4f867752f86836f64` through
  `d64701668668584ef306dfbaf45972fcada45290`
- Analysis generation: `2026-09-09T20:25:47.4071191Z`, reported in
  `Europe/Berlin`
- Transcript privacy: complete message bodies and bounded excerpts are both
  excluded from published artifacts.

The manifest and `data/snapshot-lock.json` bind the exact root log and recursive
subagent family by cutoff-bounded byte count and SHA-256. Transcript files stay
local and are never copied into this directory.

## Reproduce

From the repository root, with the owner's local Codex session family present:

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/run_analysis.py `
  --manifest docs/codex/session-analysis/reports/p1-04-05-orchestration.report.json `
  --sessions-dir <local-codex-sessions-directory> `
  --repo . `
  --output-dir docs/codex/p1-04-05-orchestration-session-analysis/data `
  --quiet

uv --cache-dir .uv-cache run python -B `
  docs/codex/p1-04-05-orchestration-session-analysis/derive_metrics.py `
  --manifest docs/codex/session-analysis/reports/p1-04-05-orchestration.report.json `
  --analysis docs/codex/p1-04-05-orchestration-session-analysis/data/analysis.json `
  --sessions-dir <local-codex-sessions-directory> `
  --repo . `
  --output docs/codex/p1-04-05-orchestration-session-analysis/data/derived-metrics.json
```

Do not pass `--create-snapshot-lock` on reruns. Drift in the transcript family,
included bytes, record counts, or hashes must fail rather than widen the report.

## Interpretation limits

- Token costs are API list-price equivalents dated 2026-09-09, not Codex
  subscription charges. Tool-call fees, if any, are excluded.
- Agent active seconds overlap and are not additive wall time. One interrupted
  root turn has no completion event, so the normalized `root_active_seconds`
  understates the observed engaged span by about 15 hours 58 minutes.
- Git line counts measure repository change, not value or correctness. The
  integrated-main and draft-PR ranges are not strictly additive because the
  latter modifies files already changed in the former.
- Poll-response token attribution uses cumulative usage deltas for responses
  containing `wait_agent`; the broad measure includes one mixed-action response.
- Resource conclusions use the run's sealed capsule, preview, and heavy-operation
  evidence. They establish observed headroom and outcomes, not a universal safe
  concurrency factor.
