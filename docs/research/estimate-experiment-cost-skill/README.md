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
| 001 | [Slice vs Repeated-Match Token Usage](001-slice-vs-repeated-token-usage.md) | Completed | Compared `o3` medium token usage between a 10-item random slice and a 10-item measured repeated-match sample after the `2025-12-01` cutoff. |

## Current Estimator Shape

The estimator is still provisional and should be revised as each user-specified sub experiment adds evidence.

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
- Langfuse traces, observations, dataset runs, and scores; trace details currently expose `predict-match` `usageDetails` and `costDetails`
- Orchestrator command output and generated manifests
- official model pricing data, recorded with source and date

Expected output:

- estimated total cost
- cost range or confidence note
- per-item and per-run assumptions
- observed comparable runs used as evidence
- recommended cheapest next validation step

Current finding from experiment 001:

- Repeated-match runs can expose meaningful input-cache behavior, but a single repeated fixture is not enough to estimate slice input-token usage because input tokens are fixture/context specific.
- In Sub Experiment A (`o3`, medium effort, `N = 10` measured per group), output tokens and total tokens did not differ significantly between the random slice and repeated-match sample, while total input tokens did differ because the repeated fixture had a shorter prompt than the slice average.
- For cost estimates, keep total input tokens, cached input tokens, uncached input tokens, output tokens, reasoning tokens, and service tier separate. Cached input behavior should be estimated from repeated-match batch shape, while total input prompt length should be checked against fixture-level prompt/context size.

## Open Questions

- Can exported experiment analysis bundles be extended to include normalized usage data so agents do not need one trace-detail API call per item?
- How much variance exists between repeated predictions for the same fixture and prompt?
- How many random repeated fixtures are needed before repeated-match usage is representative of slice input-token distributions?
- Does reasoning effort materially change token usage in a predictable way for our prompts?
- Can prompt reconstruction estimate input tokens accurately enough without calling a model?
- How should the skill treat cached input tokens, retries, and failed item runs?
