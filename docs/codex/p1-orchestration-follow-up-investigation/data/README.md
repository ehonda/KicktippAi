# P1 orchestration follow-up raw data

This directory contains the normalized evidence used by the self-contained
follow-up report. Source JSONL transcripts remain in the local Codex session
store and are not committed.

## Files

- `analysis.json`: canonical root, descendant, guardian, turn, model, token,
  concurrency, user-message, task, and Git-boundary data.
- `comparison-metrics.json`: pause-adjusted old/new parallelism, efficiency,
  coordination, publication, review, and resource metrics.
- `derived-metrics.json`: successor tool, review, Git, branch, and CI
  aggregates.
- `agents.csv`, `agent-turns.csv`, `model-usage.csv`: flattened task-agent and
  model views.
- `commits.csv`, `commit-stats.csv`: first-parent history for
  `71637cc..5891d48` and per-commit file/line counts.
- `branches.csv`: local and remote `codex/01a054ee-*` refs visible at cutoff.
- `review-turns.csv`: bounded verdict excerpts for design/specification and
  exact-artifact review turns.
- `tool-timings.csv`: completed tool calls, elapsed time, role, nested tool
  class, and patch-target classification.
- `ci-runs.csv`: the five Build-and-Test runs on the two main and three draft-PR
  milestone heads in the successor session.
- `task-files.csv`, `task-groups.csv`, `user-messages.csv`: task and user-event
  context.
- `curated-findings.json`: concise interpretations tied to measured evidence.

## Reproduction

From the repository root:

```powershell
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-follow-up-investigation/analyze.py --output-dir docs/codex/p1-orchestration-follow-up-investigation/data --repo . --quiet
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-follow-up-investigation/enrich.py --analysis docs/codex/p1-orchestration-follow-up-investigation/data/analysis.json --output-dir docs/codex/p1-orchestration-follow-up-investigation/data --repo .
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-follow-up-investigation/build_html.py
```

The authenticated GitHub evidence was collected with read-only `gh run list`
and `gh pr list` calls against `ehonda/KicktippAi` and then restricted to the
recorded successor commits.

## Boundaries and caveats

- Thread-family membership requires recursive
  `thread_spawn.parent_thread_id` ancestry to the successor root.
- Auto-review guardian sessions remain separate from task agents.
- The 10h30m adjustment is the contiguous interval from the last completed
  preview-agent turn at `2026-08-30T23:37:08Z` to the owner's resume message at
  `2026-08-31T10:07:26.245Z`.
- The baseline retains its previously published 5h42m54s authorization pause.
- Complete prompts, reasoning, private payloads, secrets, and complete tool
  output are excluded.
- Public API price is not aggregated because public prices are not established
  for every observed Codex model and subscription quota is not API billing.
