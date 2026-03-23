# Bundesliga 2025 / 2026 Experiments

This is the domain-specific design note for the first Phase 2 milestone: Langfuse experiments for the 2025/2026 Bundesliga season, which is the first kicktipp.ai season we have data for.

For implementation tracking, use [01-phase-2-tracker.md](01-phase-2-tracker.md), [00-common-context.md](00-common-context.md), and the numbered task trackers in [tasks](tasks).

## Dataset for Matches

This is how our dataset should look

- Item scope is essentially "`predict-match` traces", because that is the most basic level we can evaluate at: How does the model perform on a single match prediction?
- `input` should ideally match our `predict-match` observations, i.e. it is the match-to-predict JSON payload
  - This means we need a dedicated system message per input, because the system message (currently, we might change this with the start of the next season) has the context documents for the match directly appended
  - Storing the used system prompt in the `metadata` field is **not an option**. We'll have to reconstruct dataset items from our "historic records" in firestore, as we only introduced tracing recently. The firestore entries don't contain the used system prompt
  - So we'll need a **mechanism to reconstruct the system prompt when we run experiments**. This does seem like the correct way to do it anyway though - If we change how we build our system prompt and e.g. don't directly append context documents into it anymore, we'll want to be able to evaluate v1 vs v2 results on the dataset, and will have to build v1 prompts and v2 prompts in experiments differently anyway
- `expectedOutput` is the actual match result and scoring ground truth
- The experiment task output is the newly generated prediction result for the run
- `metadata` contains
  - **match outcome:** This is crucial so we can score the prediction during experiments
  - **context documents:** Critical to reconstruct the system prompt for experiments, as mentioned above
  - **resolved context versions:** We need these once the export pipeline materializes the final experiment items
  - **historical prediction baseline:** Optional, but useful for later comparisons against already stored predictions
  - `matchday`, `season`, `homeTeam`, `awayTeam`: We might use this to filter during experiments, analze results, etc.
- Use JSON schemas to validate the entries in dataset columns
- We can create most of this dataset from historic prediction records plus versioned context documents in Firestore; actual outcomes should be collected from Kicktipp `tippuebersicht` pages and persisted into Firebase before export materialization runs

## Scope Decisions

- Dataset scope is the Kicktipp `community`
- The first hosted dataset rollout targets only `pes-squad`
- The initial slice is all completed Bundesliga 2025/2026 matches available in `pes-squad`
- Matches without persisted outcomes must be excluded from dataset export until they are completed

## Dataset for Bonus Predictions

As these only matter during the start of the season, we'll postpone it for now. It will be very similar to our [Dataset for Matches](#dataset-for-matches).

## Experiments for Matches

- The Langfuse docs on "Experiments via SDK" only expose first-class experiment runners in the Python and JS/TS SDKs.
  - We currently prefer Python first for this work because the examples and evaluation ecosystem are strongest there
  - JS/TS remains the fallback if local Python setup becomes the main source of friction
  - If during development any issues with our local Python setup arise, suggest improvements so we can optimize it
- The first experiment we want to implement is to take a specifyable random sample of matches from the dataset, then run predictions for them, then score them based on how many Kicktipp points they would have achieved
  - Prefer a **hosted Langfuse dataset** over a purely local dataset so we get dataset runs and comparison views in the UI
  - Use Kicktipp points as the primary score, plus supporting metrics such as exact hit, outcome correctness, and goal error
- We need to reconstruct the system prompt for each prediction
  - Check the used context documents, and resolve the latest version whose creation time is less than or equal to the original prediction creation time
  - Query those versions from Firestore to reconstruct the system prompt deterministically

## Current Implementation Shape

- Shared context and repository findings: [00-common-context.md](00-common-context.md)
- Master tracker and task order: [01-phase-2-tracker.md](01-phase-2-tracker.md)
- Manual steps by task and execution point: [manual-steps.md](tasks/manual-steps.md)

## Current Task 5 Setup

- Runner stack for the first milestone is JS/TS with `@langfuse/client`, `@langfuse/tracing`, `@langfuse/otel`, and the OpenAI JS SDK
- Prompt and context reconstruction stay in `.NET`; the JS runner consumes exported experiment-item JSON files
- Explicit evaluation-time input now uses NodaTime's invariant `ZonedDateTime` `G` pattern, for example `2026-03-15T12:00:00 Europe/Berlin (+01)`
- The Langfuse OTEL span processor is configured with `x-langfuse-ingestion-version: 4` to use the faster ingestion path surfaced in the new UI
- Development runs default to `5` repetitions; the intended fuller milestone run remains `17` repetitions
- The first repetition is kept serial to warm provider-side prompt caches; later repetitions run in parallel batches, default batch size `8`

## Current Repetition Workaround

- A single Langfuse dataset run with multiple run items for the same dataset item did not give reliable repetition visibility in the current UI/API behavior during local validation
- The current workaround therefore creates one dataset run per repetition under a shared run-family naming convention
- This keeps every repetition visible and inspectable from the dataset item runs table and from API responses
- The wrapper aggregates scores across all repetition-runs for a model and prints that summary after execution

## Known Tradeoff To Revisit

- The per-repetition-run workaround makes Langfuse's built-in run-level averages less useful for model-vs-model comparison across all repetitions
- For example, comparing `o3` vs `gpt-5-nano` on total average `kicktipp_points` over all repetitions currently relies on the wrapper's aggregate JSON output, not one native Langfuse comparison row per model
- Future refinement should investigate whether Langfuse supports a better repeated-execution model for one dataset item, or whether we should add a higher-level aggregation layer on our side that writes comparison summaries back into Langfuse in a more queryable way
