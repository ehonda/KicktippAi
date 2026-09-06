# Frozen data snapshot

This directory contains a normalized extract of Codex orchestration run
`01a07449-de77-7ae0-ac4a-8f5330c43121`, frozen at
`2026-09-06T22:21:56.196+02:00` (Europe/Berlin).

- `analysis.json` is the normalized session-family extract.
- `agents.csv`, `agent-turns.csv`, and `model-usage.csv` flatten agent usage.
- `compactions.csv` measures the interval from each root compaction to the first
  resumed coordination call.
- `resource-samples.csv` contains parsed orchestration resource snapshots.
- `preview-churn.csv` and `review-cycles.csv` capture the two dominant churn
  surfaces.
- `role-usage.csv` aggregates task agents into stable analytical roles.
- `derived-metrics.json` records exact report denominators.
- `curated-findings.json` separates analytical judgments from transcript facts.

Full prompts, reasoning, secrets, and complete message bodies are not copied.
Token-dollar figures use public API list prices as an equivalence estimate; they
are not Codex subscription charges.
