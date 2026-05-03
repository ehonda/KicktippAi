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

Shared methods that apply across several experiments can live in support documents such as [token-usage-methodology.md](token-usage-methodology.md). Each numbered experiment should still include its own methodology section with the run-specific design, parameters, and links to any shared method it uses.

## Experiment Log

| Sequence | Experiment | Status | Purpose |
| --- | --- | --- | --- |
| 001 | [Slice vs Single Repeated-Match Token Usage](001-slice-vs-repeated-token-usage.md) | Completed | Compared `o3` medium token usage between a 10-item random slice and 10 measured repetitions from one random fixture after the `2025-12-01` cutoff. |
| 002 | [Slice vs Multi-Fixture Repeated-Match Token Usage](002-slice-vs-multi-fixture-repeated-token-usage.md) | Completed | Compared `o3` medium token usage between a 20-item random slice and 20 repeated-match observations from 5 random repeated fixtures of size 4 after the `2025-12-01` cutoff. |
| 003 | [Slice vs Multi-Fixture Token Usage Across Model Configs](003-slice-vs-multi-fixture-token-usage-model-configs.md) | Completed | Repeated the 20-item slice vs 5-by-4 repeated-fixture token comparison for `gpt-5.4-nano` xhigh and `gpt-5.5` none with hosted prompt `langfuse-o3-poc`. |

## Current Estimator Shape

The estimator is still provisional and should be revised as each user-specified experiment adds evidence.

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
- Langfuse traces, observations, dataset runs, and scores; batched v2 observation queries expose `predict-match` `usageDetails` and `costDetails`
- Orchestrator command output and generated manifests
- official model pricing data, recorded with source and date

Expected output:

- estimated total cost
- cost range or confidence note
- per-item and per-run assumptions
- observed comparable runs used as evidence
- recommended cheapest next validation step

Current finding from experiments 001 and 002:

- A single repeated fixture is not enough to estimate slice input-token usage because total input tokens are fixture/context specific.
- In experiment 001 (`o3`, medium effort, `N = 10` measured per group), output tokens and total tokens did not differ significantly between the random slice and repeated-match sample, while total input tokens did differ because the repeated fixture had a shorter prompt than the slice average.
- In experiment 002 (`o3`, medium effort, `N = 20` per group), a 20-item random slice was compared with 20 repeated-match observations from 5 random repeated fixtures of size 4. Neither total input tokens (`p = 0.100263`) nor total output tokens (`p = 0.524548`) differed significantly.
- In experiment 003 (`gpt-5.4-nano` xhigh and `gpt-5.5` none, hosted prompt `langfuse-o3-poc`, `N = 20` per group), the same 5-by-4 repeated-fixture design again showed no significant slice-vs-repeated differences for input, output, reasoning, or total token counts.
- For total input/output token-count estimates, prefer a reference design of `M` random repeated-match fixtures of size `S`, with `N = M * S`, rather than one repeated fixture. Keep cached-input and uncached-input fields separate only when the research question includes cost or cache behavior.
- Use the reproducible [analyze_token_usage.py](analyze_token_usage.py) script for usage pulls and analysis. It uses batched Langfuse v2 observation queries by default, matching the Orchestrator exporter's per-run batching pattern.
- For reasoning-heavy configs, preflight the intended prompt and reasoning effort before the full run and set an explicit output-token cap when needed. In experiment 003, `gpt-5.4-nano` xhigh needed `--max-output-tokens 40000`; lower caps produced `OpenAI response did not contain output text`.

## Open Questions

- Can exported experiment analysis bundles be extended to include normalized usage data so agents do not need a separate usage extraction step?
- How much variance exists between repeated predictions for the same fixture and prompt?
- How large should `M` and `S` be before repeated-match usage is representative enough for each model and reasoning effort?
- Does reasoning effort materially change token usage in a predictable way for our prompts?
- Can prompt reconstruction estimate input tokens accurately enough without calling a model?
- How should the skill treat cached input tokens, retries, and failed item runs?
