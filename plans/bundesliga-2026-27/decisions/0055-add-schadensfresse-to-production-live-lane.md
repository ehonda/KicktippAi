# ADR-0055: Add schadensfresse to the production-live matchday lane

- Status: Accepted
- Date: 2026-08-28
- Accepted by: Project Owner on 2026-08-28

## Context

ADR-0053 activated the strict serial production-live matchday lane without
`schadensfresse` because that community's 2026/27 season was not ready.
ADR-0054 subsequently selected `pes-squad` as the reference source for ordinary
Bundesliga predictions while retaining target-owned `schadensfresse` context.
The administrator has now completed setup, and the Owner authorized the full
manual context, match, and deadline-bounded bonus ladder followed by recurring
matchday inclusion when that evidence was green.

The manual context run established the exact current-matchday publication
scope required by the nine opening fixtures: 86 of the 401 full-season
inventory documents are present. This is not a strict 401-document seasonal
inventory pass. The 315 absent documents are future head-to-head documents and
opposite-role histories that are not required by the current fixtures; the
current nine fixtures each have their complete required context set. The
ordered match copy posted all nine `pes-squad` predictions without a model
generation. The cutoff-bounded bonus copy selected five of eight open questions
and left the three later Champions-League questions outside P0. P1-08 still
owns those questions and the DFB/CL match-final primary routes.

The recurring topology therefore needs one bounded evolution without changing
ADR-0053's cadence, concurrency, failure, or operations contract. Bonus work
must remain outside the recurring lane.

## Decision

Keep the sole outer workflow's existing `workflow_dispatch` trigger and exact
UTC cron unchanged:

```yaml
schedule:
  - cron: "7 2,9 * * *"
```

Extend its strict default-success chain from 14 to 16 jobs by inserting these
two reusable-workflow calls immediately after `pes-squad-matchday` and before
`relaxdays-tippt-context`:

1. `schadensfresse-context` calls `base-context-collection.yml` with
   `community_context: schadensfresse`, competition
   `bundesliga-2026-27`, trigger classification derived from the outer event,
   the `SCHADENSFRESSE_KICKTIPP_*` credentials, and
   `publish_launch_roster_overlay: false`.
2. `schadensfresse-matchday` needs that context job and calls
   `base-matchday-predictions.yml` for target `schadensfresse`, source context
   `pes-squad`, `gpt-5.6-sol` / `xhigh`, cap `10000`, hosted match prompt v3
   labelled `production`, `force_prediction: false`, and
   `max_repredictions: 2`.

`relaxdays-tippt-context` now needs `schadensfresse-matchday`; every later edge
retains its existing order. There is still no `always()` continuation, retry,
matrix, workflow timeout, bonus job, or scheduled leaf workflow. A failure in
either added job blocks all downstream rows.

The shared `bundesliga-2026-27-production-live-lane` concurrency group remains
non-cancelling. The outer workflow is not manually dispatched for validation;
its retained `workflow_dispatch` trigger does not grant an operational
exception to ADR-0053's no-manual-dispatch policy. The first natural scheduled
execution containing `schadensfresse` remains runtime evidence that must be
observed after this change reaches the default branch. This decision does not
claim that observation has occurred.

## Alternatives considered

- **Schedule schadensfresse independently:** Rejected because a separate cron
  cannot enforce `pes-squad` reference completion before the copy and can race
  the shared live lane.
- **Insert only the match copy:** Rejected because current target-owned context
  collection and its successful completion are required before every
  prediction operation.
- **Schedule the bounded bonus copy:** Rejected because the five opening
  Bundesliga questions are one-time preseason state and P1-08 must first add
  competition-correct handling for the later Champions-League questions.
- **Republish the launch roster overlay on every schedule:** Rejected because
  the initial pinned enriched publication is complete; recurring collection
  must use the normal false overlay path and preserve the accepted
  last-known-good snapshot.

## Consequences

- The production-live lane now contains eight context/match pairs in one
  fail-closed serial order.
- Ordinary `schadensfresse` Bundesliga matches reuse the exact production
  reference and should incur no model generation when the copy remains
  compatible.
- A context or copy failure stops `relaxdays-tippt` and all arena rows. This is
  the same intentionally conservative failure behavior selected by ADR-0053.
- The fixed UTC cadence, concurrency, owner/on-call duties, rollback targets,
  leaf manual-only status, and bonus exclusion are unchanged.
- P0-21 remains open until the required natural scheduled sequence is observed
  and recorded; repository topology alone is not runtime evidence.

## Affected tasks

- [P0-21](../tasks/p0-21-production-activation.md)
- [P1-08](../tasks/p1-08-schadensfresse-mixed-competition-routing.md)

## Supersedes

- [ADR-0053](0053-schedule-the-production-live-matchday-lane.md), only its
  exact 14-job/seven-pair topology and exclusion of `schadensfresse`. Its cron,
  concurrency, no-bonus, no-manual-dispatch, monitoring, rollback, and natural
  observation contracts remain in force.
- [ADR-0054](0054-copy-schadensfresse-bundesliga-from-pes-squad.md), only its
  statement that the recurring outer lane remains unchanged pending the manual
  evidence gate. Its copy, rules, bonus cutoff, and P1 mixed-competition
  boundaries remain in force.
