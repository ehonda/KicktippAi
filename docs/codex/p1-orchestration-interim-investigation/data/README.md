# P1 orchestration interim raw data

This directory contains the normalized evidence used by the self-contained P1
orchestration report. The source JSONL transcripts remain in the local Codex
session store and are not committed.

## Files

- `analysis.json`: normalized root, descendant, guardian, turn, model, token,
  concurrency, user-message, task, and Git-boundary data.
- `agents.csv`, `agent-turns.csv`, `model-usage.csv`: flattened thread and turn
  views.
- `task-files.csv`, `task-groups.csv`: P1 task state and timing envelopes.
- `commits.csv`, `commit-stats.csv`: first-parent commit history and per-commit
  file/line counts for `c4669aa..04a6d85`.
- `branches.csv`: local and remote `codex/p1-*` refs visible at extraction time.
- `review-turns.csv`: bounded P1-10 independent-review outcomes and hashed
  result excerpts.
- `tool-timings.csv`: completed transcript tool calls, elapsed time, nested tool
  class, and patch-target classification.
- `derived-metrics.json`: aggregates used by the HTML report, with separate
  whole-session and exact P1-10 Git/CI boundaries.
- `curated-findings.json`: concise interpretations tied to measured evidence.
- `ci-runs.csv`: read-only GitHub Build-and-Test run metadata captured for the
  session's main-push boundary.
- `user-messages.csv`: categorized user-originated messages and injected
  context records; complete bodies are not included.

## Reproduction

From the repository root:

```powershell
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-interim-investigation/analyze.py --output-dir docs/codex/p1-orchestration-interim-investigation/data --repo . --quiet
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-interim-investigation/enrich.py --analysis docs/codex/p1-orchestration-interim-investigation/data/analysis.json --output-dir docs/codex/p1-orchestration-interim-investigation/data --repo .
uv --cache-dir .uv-cache run python docs/codex/p1-orchestration-interim-investigation/build_html.py
```

The authenticated CI source command was:

```powershell
gh run list --repo ehonda/KicktippAi --workflow "Build and Test" --limit 60 --json databaseId,headSha,status,conclusion,createdAt,startedAt,updatedAt,url,displayTitle,event
```

Because the analyzed session continued after the snapshot, rerunning the
extractor later produces a newer interim dataset unless the recorded boundary
constants are changed deliberately.

## Data boundaries

- Thread-family membership requires a recursive
  `thread_spawn.parent_thread_id` relationship to the root.
- Auto-review guardian sessions are reported separately from task agents.
- Full message bodies, reasoning, private predictions, secrets, and complete
  tool output are excluded.
- Worker and tool durations overlap across agents.
- `tool-timings.csv` is cut off at the normalized analysis generation time.
- Public API cost is not aggregated because public list prices are not
  established for every observed Codex subagent model.
