# Agent Instructions

These instructions apply to `docs/research/estimate-experiment-cost-skill`.

## Keep Documents Synchronized

- Read [README.md](README.md) and all existing `NNN-*.md` experiment documents before adding or updating an experiment.
- Keep the experiment log in [README.md](README.md) synchronized with the actual files in this folder.
- Use explicit sequence numbers in experiment filenames: `NNN-short-slug.md`.
- Never renumber completed experiments. Add the next file using one more than the current maximum sequence number.
- Every experiment document must include `Research Question`, `Methodology`, `Outcome`, and `Further Research Directions`.
- When an experiment changes the research workflow, update this `AGENTS.md` in the same change.
- When an experiment changes the provisional estimator, update the `Current Estimator Shape` section in [README.md](README.md).

## Roles And Autonomy

- Treat experiment design and specification as user-owned. Do not invent or replace the next experiment unless the user explicitly asks for proposals.
- Once the user specifies an experiment, execute it fully autonomously unless the user states otherwise.
- Ask for clarification only when the experiment cannot be executed safely or reproducibly from the provided specification and repository context.
- After each experiment, commit and push the updated research documents and any intentionally produced tracked artifacts.

## Data Gathering Guidance

- Use the global `$langfuse` skill for generic Langfuse API access, current Langfuse documentation, SDK behavior, and prompt-management questions.
- In this repository, follow the root `AGENTS.md` override for Langfuse API work: prefer the installed `langfuse` CLI entrypoint for repository-secret workflows instead of routine `npx langfuse-cli` usage.
- Use `.agents/skills/langfuse-experiments/` for KicktippAi-specific dataset preparation, experiment execution, export, statistical reporting, Pages verification, and commit/push workflows.
- Read `docs/langfuse.md` before relying on Langfuse metadata filters. Some metadata filtering behavior in the live project differs from public documentation.
- Read `docs/langfuse/experiments/` before preparing, syncing, running, exporting, or analyzing experiment runs.
- Run every `dotnet` command outside the sandbox, as required by the root instructions.
- Use `uv` for Python commands.
- When a repeated-match experiment needs a random fixture after a cutoff and `prepare-repeated-match` does not support the random/cutoff selection directly, first prepare a one-item random slice with the required cutoff and seed, then use that selected fixture for `prepare-repeated-match`.
- When comparing total input, output, or total token counts between repeated-match and slice usage, prepare exactly `N` repeated-match items. Do not add or exclude a warmup item unless the user explicitly makes cached-input behavior, uncached-input behavior, or monetary cost part of the research question.

Prefer data sources in this order:

1. Existing tracked docs and generated local artifacts.
2. Existing Langfuse traces, observations, dataset runs, and scores.
3. Cheap one-item or tiny-slice verification runs, preferably with `gpt-5-nano`.
4. Larger experimental runs only when the smaller evidence cannot answer the research question.

## What To Record

For every experiment, record enough detail for another agent to reproduce the reasoning:

- date of execution or inspection
- repository commit used, if commands were run
- exact commands or API queries
- dataset names, manifest paths, and run names
- experiment kind, model, prompt key, prompt source, reasoning effort, evaluation policy, and batch settings
- observed token fields, cost fields, score fields, and their API or artifact source
- pricing source and retrieval date when monetary estimates are calculated
- whether values are observed, inferred, estimated, or assumed
- any failed command, missing field, retry, or rate-limit behavior that affects cost estimation

## Autonomous Experiment Loop

1. Read the current overview, prior experiment docs, and this file.
2. Confirm the next experiment has been specified by the user.
3. Execute the specified experiment using the smallest data-gathering path that answers the question.
4. Gather existing evidence before launching a live prediction run.
5. If a live run is needed, preflight with the smallest viable sample and record the expected spend before starting.
6. Update the experiment document with outcome and next directions.
7. Update [README.md](README.md) and this file when the finding changes the shared workflow.
8. Commit and push the completed experiment changes.

Before spending meaningful API budget, state the expected cost range and get user confirmation unless the user has already authorized that exact run.
