# Urgent production orchestration investigation

This report analyzes orchestration run `01a06e72-0c2f-76a3-8d2b-d48f9131a1d5`, frozen at commit `538c30c53870faa608cf0d6e6a9dbf20f8d833d3` and root event `2026-09-05T14:44:05.022Z`.

The main findings are:

- PR #98 improved runtime resilience. Eleven task-agent threads completed without a thread-limit error, the new memory policy admitted 20 heavy samples that the old 1.50 GiB floor would have rejected, and dotnet command families did not overlap.
- Specialist reuse was too aggressive. The long-lived Sol/high completion thread crossed several roles and consumed 134.0 million tokens, about $69.05 at API list-price equivalents.
- Ledger pressure regressed to 207 patches, or 18.51 per effective hour.
- The Astra/medium root cost $219.249 at API list prices. Repricing its exact token mix at Sol rates gives $87.700, so the direct price premium was $131.549 and 2.5×. Compared with the prior observed Sol/xhigh root, it was 2.57× as expensive while using 6.24% more tokens.

The published, self-contained report is generated at `session-analysis/urgent-production-orchestration/index.html`.

## Reproduce

From the repository root:

```powershell
uv --cache-dir .uv-cache run --no-project --with tzdata python docs/codex/urgent-production-orchestration-investigation/analyze.py --output-dir docs/codex/urgent-production-orchestration-investigation/data --repo . --quiet
uv --cache-dir .uv-cache run --no-project python docs/codex/urgent-production-orchestration-investigation/enrich.py --analysis docs/codex/urgent-production-orchestration-investigation/data/analysis.json --output-dir docs/codex/urgent-production-orchestration-investigation/data --repo .
uv --cache-dir .uv-cache run --no-project python docs/codex/urgent-production-orchestration-investigation/build_html.py
uv --cache-dir .uv-cache run --no-project python docs/codex/urgent-production-orchestration-investigation/verify_snapshot.py
```

The extractor reads local Codex session logs under `%USERPROFILE%/.codex/sessions`. The checked-in CSV and JSON outputs preserve the frozen evidence so the report remains reviewable without those private logs.
