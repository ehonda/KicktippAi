# Estimate Experiment Cost Skill Research

This research builds the evidence needed for a future Codex skill that estimates the cost of running KicktippAi experiments before the run starts.

The skill should eventually help an agent answer questions such as:

- What will this experiment probably cost?
- Which model, sample size, batch shape, or evaluation policy drives the estimate?
- How confident are we in the estimate, and what cheap preflight would reduce uncertainty?
- Which observed Langfuse usage or cost data supports the estimate?

## Goal

Develop a practical, low-cost estimation method for KicktippAi experiment runs. The method should combine observed data from previous experiments with explicit assumptions about model pricing, token usage, prompt shape, completion length, retries, and any fixed overhead from the runner.

The target output is not just a formula. It is a reusable skill workflow that can inspect the planned experiment, gather comparable historical evidence, produce a cost estimate with confidence notes, and recommend whether a small preflight run is warranted.

## Research Method

Research happens as a sequence of small experiments. Each experiment should answer one concrete question and leave enough evidence for the next step to be chosen from the outcome.

Default experiment order:

1. Prefer existing local artifacts and Langfuse records.
2. Use a tiny preflight run only when existing data cannot answer the question.
3. Scale sample size gradually and only when the extra data resolves a real uncertainty.
4. Update this overview and the folder-local [AGENTS.md](AGENTS.md) whenever a new finding changes how later agents should gather data.

Every experiment document must include:

- Research Question
- Methodology
- Outcome
- Further Research Directions

## Experiment Log

| Sequence | Experiment | Status | Purpose |
| --- | --- | --- | --- |
| 001 | [Cost Data Discovery](001-cost-data-discovery.md) | Placeholder | Temporary placeholder from initial scaffolding. The user will replace it shortly with the intended first experiment. |

## Current Estimator Shape

The estimator is intentionally provisional until the first user-specified experiment is complete.

Expected inputs:

- experiment kind: slice, repeated-match, or community-to-date
- planned item count and batch settings
- model and reasoning effort
- prompt source and prompt key
- evaluation time or relative evaluation policy
- expected prompt context size
- retry and failure behavior
- known pricing source with retrieval date

Expected evidence sources:

- existing `artifacts/langfuse-experiments` analysis bundles when available
- Langfuse traces, observations, dataset runs, and scores
- Orchestrator command output and generated manifests
- official model pricing data, recorded with source and date

Expected output:

- estimated total cost
- cost range or confidence note
- per-item and per-run assumptions
- observed comparable runs used as evidence
- recommended cheapest next validation step

## Open Questions

- Which Langfuse API resources expose the most reliable token and cost fields for this project?
- Do exported experiment analysis bundles currently include enough usage data, or do they need to be extended?
- How much variance exists between repeated predictions for the same fixture and prompt?
- Does reasoning effort materially change token usage in a predictable way for our prompts?
- Can prompt reconstruction estimate input tokens accurately enough without calling a model?
- How should the skill treat cached input tokens, retries, and failed item runs?
