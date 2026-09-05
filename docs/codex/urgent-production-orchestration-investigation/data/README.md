# Frozen report data

The files in this directory are the reproducible, privacy-bounded evidence for orchestration run `01a06e72-0c2f-76a3-8d2b-d48f9131a1d5`.

- `analysis.json`: normalized root, task-agent, guardian, turn, token, task, concurrency, and Git evidence.
- `derived-metrics.json`: cost counterfactuals and workflow measurements derived by `enrich.py`.
- `session-facts.json`: reviewed qualitative scorecard, production outcomes, timeline, and limitations.
- `model-usage.csv`: per-thread model, effort, tokens, responses, and API list-price equivalent.
- `agents.csv` and `agent-turns.csv`: task-agent lifecycles and turns.
- `tool-timings.csv`: orchestration-visible tool-call intervals and classifications.
- `resource-samples.csv`: parsed resource admission evidence.
- `review-turns.csv`: independent review outcomes.
- `commits.csv` and `commit-stats.csv`: commits in the frozen source-session range.
- `ci-runs.csv`: authenticated GitHub Actions observations for source-session commits.
- `task-files.csv`, `task-groups.csv`, and `user-messages.csv`: planning and intervention evidence.

Prompt bodies and raw transcripts are not published. Result excerpts in `analysis.json` and `review-turns.csv` are bounded by the shared extractor.
