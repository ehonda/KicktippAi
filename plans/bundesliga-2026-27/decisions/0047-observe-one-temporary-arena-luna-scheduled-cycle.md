# ADR-0047: Observe one temporary arena Luna scheduled cycle

- Status: Accepted
- Date: 2026-08-25

## Context

P0-20 requires the authorized `gpt-5.6-luna` validation identity to pass the
arena local CLI, manual Actions, and temporary-schedule ladder before any final
production activation. The local and manual Actions rungs have passed with
exact hosted prompts and direct Kicktipp, Firestore, and Langfuse evidence.
P0-21 still exclusively owns the final production model and schedules.

The three arena entrypoints from P0-19 are deliberately manual-only. Adding an
independent cron to each would not guarantee that context, matchday, and bonus
operations run in order because scheduled Actions can be delayed. Reusing the
current valid index-0 predictions without forcing would also make the reusable
prediction workflows skip generation and produce no scheduled Langfuse trace.

## Decision

Add one temporary schedule-only workflow named
`buli2627-ehonda-ai-arena-gpt-5-6-luna-none-scheduled-cycle.yml`. It has the
single daily cron `47 8 * * *` (UTC), a fixed
`p0-20-ehonda-ai-arena-luna-scheduled-cycle` concurrency group with
`cancel-in-progress: false`, and exactly three reusable-workflow jobs:

1. collect `ehonda-ai-arena` context for `bundesliga-2026-27`;
2. after context succeeds, force the nine-match arena prediction cycle;
3. after matchday succeeds, force the five-question arena bonus cycle.

The prediction jobs pin `gpt-5.6-luna`, reasoning `none`, output cap `10000`,
Langfuse source, the `production` label, immutable match prompt version 2 and
bonus prompt version 1, and bonus budgets 20 documents / 32000 estimated
tokens. Both pass `force_prediction: true` and `max_repredictions: 0`. The
force flag makes the reusable workflow use its existing database-override
path, overwriting index 0 instead of allocating index 1; the zero remains an
explicit safety pin and documents that no reprediction index is authorized.
Generate and final-verification steps must run, while the normal
already-up-to-date success notification is expected to skip.

Only the reserved arena Luna credential pair and shared Firebase credentials
reach context. Matchday and bonus receive those exact secrets plus the shared
OpenAI and Langfuse secrets. No development, `pes-squad`, `schadensfresse`,
WM26, or final production-model workflow is part of this cycle. The existing
manual arena triad remains unchanged, including its exact numeric
`max_repredictions` conversion and valid manual zero behavior.

The scheduled workflow may reach the default branch only after independent
review and exact-head green CI. While it is present, the serialized P0-20 live
lane permits no manual dispatch or other arena live operation. The first
eligible occurrence is 2026-08-25 at 08:47 UTC. Observe that single scheduled
run through terminal state and inspect its exact event, ref, SHA, run/job IDs,
step order, result, Kicktipp, Firestore, Langfuse configuration/usage/cost,
prediction indices, context inventory, and ordering. Context failure skips
both prediction jobs; matchday failure skips bonus. Do not use an `always()`
override, manual fallback dispatch, or automatic retry.

Whether the run succeeds, fails, or has not appeared by 09:47 UTC, manually
tear the schedule down in a separate reviewed commit. Remove the temporary
workflow, replace its activation contract with a durable absence assertion,
record the observed evidence in P0-20, and integrate the teardown before the
next possible 2026-08-26 08:47 UTC occurrence. Any later attempt requires a
new reviewed decision and activation; this ADR never authorizes a second
cycle.

## Alternatives considered

- **Add schedules to the three manual callers:** Rejected because delayed cron
  events cannot provide deterministic ordering or prevent overlap.
- **Schedule separate callers at offset times:** Rejected because fixed spacing
  is not a completion dependency.
- **Do not force predictions:** Rejected because current valid index-0 results
  would skip model generation and provide no scheduled Langfuse trace.
- **Automatically disable after the run:** Rejected because teardown must be a
  visible, reviewed, manual repository change with its own green evidence.

## Consequences

- One outer run and its `needs` chain serialize all scheduled live operations.
- Forced Luna calls add only the already authorized cheap 9+5 validation
  observations and replace index 0 rather than creating repredictions.
- A failure prevents later jobs and triggers manual teardown without retry.
- The workflow and its schedule are temporary activation evidence, not a
  production selection or reusable production cadence.

## Affected tasks

- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None. This decision instantiates ADR-0006's temporary arena validation rung
without changing the P0-21 production activation gate.
