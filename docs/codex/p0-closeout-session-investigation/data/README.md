# Investigation data

This directory contains normalized, publication-oriented data for the [P0 closeout session investigation](../README.md). It deliberately does not contain raw Codex JSONL transcripts, complete prompts, reasoning, tool outputs, secrets, or full user messages.

## Generated artifacts

| File | Contents |
|---|---|
| `analysis.json` | Canonical nested dataset: source and pricing metadata, summary, user-message metadata, commits, task files/groups, real thread records, and guardian records. |
| `agents.csv` | One row per realized descendant task-agent thread. |
| `agent-turns.csv` | One row per task-agent turn with timing, outcome, model context, result hash, and bounded result excerpt. |
| `model-usage.csv` | Per-thread model/effort usage, including the root and internal guardians. |
| `user-messages.csv` | All root user-message records, including injected/automatic contexts; genuine messages have ordinals and hand-annotated intervention categories. |
| `commits.csv` | First-parent commits in `6d0fca3..2c824c8`. |
| `task-files.csv` | Base/final task status and completion transitions. |
| `task-groups.csv` | Transcript-attributed task timing envelopes and child worker totals. |
| `curated-findings.json` | Report-level phase and autonomous-solution records that require human interpretation. |

`analysis.json` uses schema version 2. Token usage is reconstructed from cumulative counter deltas. When a descendant transcript starts a new counter segment, the new segment is added once; repeated counter emissions add zero. `reasoning_output_tokens` is a subset of `output_tokens`.

## Reproduction

From the repository root:

```powershell
uv --cache-dir .uv-cache run python docs/codex/p0-closeout-session-investigation/analyze.py --quiet
```

The command requires:

- the root transcript `01a02485-f0b3-7241-a6c7-c6f58fe44509` and its descendant/guardian logs under `%USERPROFILE%\.codex\sessions`;
- repository history containing commits `6d0fca3` and `2c824c8`;
- Python 3.11 or later so `zoneinfo` contains `Europe/Berlin`.

The `generated_at` value changes on each run. All source transcript paths are stored relative to the Codex sessions directory. Bounded text excerpts replace the workstation home directory with `%USERPROFILE%`.

## Interpretation rules

- `session_wall_span_seconds` is first-to-last root event, including days with no active turn.
- `active_seconds` sums recorded task-turn durations. It includes tool calls and waits inside a turn and is not human labor time.
- Concurrency uses completed or aborted task-agent turn intervals, not thread creation-to-destruction spans.
- `ledger_completed_at` is the latest non-complete-to-complete Git transition in a task group.
- `observed_finish` includes later task-attributed review or repair evidence.
- Task groups are inferred from agent paths and final summaries. They are useful for investigation, not billing.
- `api_cost_equivalent_usd` applies public API list prices to logged tokens. It is not an actual Codex charge. Internal `codex-auto-review` usage remains unpriced.

The report can be published later by generating HTML from `analysis.json` and `curated-findings.json`; the CSVs exist for spreadsheet inspection and simple chart tooling.
