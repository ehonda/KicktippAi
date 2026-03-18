# Task 3 — Runner Spike

## Status

Completed

## Objective

Run a narrow implementation spike to choose the experiment-runner language for the first milestone: Python first, JS/TS fallback.

## Why This Comes Third

The runner decision should be made after the data contract and prompt reconstruction are stable, but before hosted dataset synchronization and experiment execution are implemented.

## Required Outputs

- A language decision for the first milestone
- A written reason for the decision
- A minimal working proof that the chosen runner can consume exported items and deliver traces to Langfuse

## Planned Work

1. Prepare one or a few exported experiment items from the .NET side
2. Test Python against the exported data and Langfuse setup
3. If Python setup is disproportionately painful, test a JS/TS fallback path
4. Record the decision and the reasons

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](done/03-task-02-prompt-reconstruction.md)
- [src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs](src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs)

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-3--runner-spike) during implementation.

## Decision Criteria

- Ease of local setup in this repo
- Ease of consuming exported experiment items
- Ease of running Langfuse experiments and attaching scores
- Reliability of trace delivery and flushing
- Overall complexity cost for the first milestone

## Completion Criteria

- Python or JS/TS is selected explicitly
- The selection is backed by a successful minimal spike
- Later tasks can assume a concrete runner environment

## Decision

- Select JS/TS for the first milestone runner
- Keep Python as the preferred longer-term option once the local machine has a modern Python installation, but do not block Phase 2 on that machine-level upgrade

## Reason

- Local Python readiness was not acceptable for the first milestone in this repo session
- `py -0p` showed only Python `3.6` installations on this machine
- Current Langfuse Python guidance targets the modern OpenTelemetry-based SDK and current integrations, which makes a Python-first spike here dependent on upgrading the local interpreter before repo work can continue
- Node.js, npm, and `dotenvx` were already available locally with no extra machine setup, so the JS/TS fallback had materially lower setup cost for this repository session
- The JS/TS SDK requires explicit OpenTelemetry bootstrap, but that complexity is bounded and repo-local, while the Python blocker was environmental and outside the repo

## Spike Outcome

- Added a .NET export command: `export-experiment-item`
- Added a minimal JS runner workspace under `tools/langfuse-runner-spike`
- Exported one reconstructed historical item for `VfB Stuttgart` vs `RB Leipzig` on matchday `26`
- Executed a one-item local Langfuse experiment from the exported JSON
- Verified the resulting trace in Langfuse via the existing API helper script

## Exported Item Shape Used In The Spike

- `datasetItem` contains the future hosted-dataset-aligned fields: `id`, `input`, `expectedOutput`, and `metadata`
- `runnerPayload` contains the reconstructed `systemPrompt` and serialized `matchJson` needed by the runner spike
- The stable item ID is preserved in the export artifact, but the local JS spike intentionally strips that ID before calling `experiment.run(...)` so Langfuse does not try to link against a hosted dataset item that does not exist yet

## Evidence

- Local Python check: only Python `3.6` was available from the Windows launcher during this task
- Export command output file: `artifacts/langfuse-runner-spike/26-vfb-stuttgart-vs-rb-leipzig-o4-mini.json`
- Verified trace ID: `17878f3ce43fcfe4be107a9b27919afb`
- Verified observations on that trace included:
	- `experiment-item-run`
	- `predict-match-runner-spike`
- Verified trace payload in Langfuse:
	- input: `{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2026-03-15T19:30:00 UTC+01 (+01)"}`
	- output: `{"homeGoals":1,"awayGoals":0,"note":"runner-spike echo output"}`

## Commands Used

1. Export one item from .NET

```powershell
dotnet run --project src/Orchestrator -- export-experiment-item o4-mini --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26
```

2. Run the JS spike with existing Langfuse credentials

```powershell
dotenvx run -f ..\KicktippAi.Secrets\src\Orchestrator\.env -- npm run --prefix tools/langfuse-runner-spike run -- --input "C:\Users\dennis\source\repos\ehonda\KicktippAi\artifacts\langfuse-runner-spike\26-vfb-stuttgart-vs-rb-leipzig-o4-mini.json"
```

3. Verify the trace through the existing API helper

```powershell
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces/17878f3ce43fcfe4be107a9b27919afb"
```

## Handoff Notes

- Task 4 should use the .NET exporter seam as the source for dataset-item materialization and should target the JS/TS Langfuse client for hosted dataset sync unless local Python tooling changes first
- Task 5 should assume the first runnable experiment flow is JS/TS-based and reuse the runner workspace or its patterns for trace flushing and Langfuse experiment execution
