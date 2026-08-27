# ADR-0053: Schedule the production-live matchday lane

- Status: Accepted
- Date: 2026-08-27
- Accepted by: Project Owner on 2026-08-27

## Context

ADR-0052 fixes the Bundesliga 2026/27 production and arena identities, while
P0-21 reserves recurring activation for a separate, late Owner gate. The ready
rows have completed their ordered manual context, matchday, and opening bonus
validation. Luna/`none` required one explicitly Owner-approved forced recovery;
that recovery replaced only the same five index-0 records, passed final 5/5
verification, completed its manual triad, and must not be repeated. The single
production-live outer workflow now represents the same fail-closed, match-only
order: `pes-squad`, its `relaxdays-tippt` and arena Sol/`xhigh` copies, then the
self-contained arena Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`
challengers. `schadensfresse` has not completed community-admin setup and cannot
join an activated lane. Opening bonus predictions remain a completed, separate
one-time operation rather than recurring matchday work.

The completed manual live ladder using the same seven context/prediction pairs
took `51m04`. That observation is capacity evidence, not a maximum or service
level. A cadence therefore needs enough room for ordinary variance, a clear
deadline margin, and one place where all ready rows stop after the first failed
dependency.

The Project Owner explicitly accepted this operating contract on 2026-08-27.
The schedule becomes active only when this reviewed change reaches the default
branch. Exact-head CI must then pass immediately after integration. No manual
pre-activation dispatch of the outer workflow is planned: the leaf-by-leaf
manual ladder already supplies the live validation evidence, while another
outer dispatch could spend a reprediction without proving scheduled delivery.

## Decision

Keep `workflow_dispatch` and add this single schedule to
`.github/workflows/buli2627-production-live-matchday.yml`:

```yaml
schedule:
  - cron: "7 2,9 * * *"
```

GitHub evaluates this fixed UTC expression at `02:07` and `09:07` every day.
That is `04:07` and `11:07` during CEST, and `03:07` and `10:07` during CET.
The one-hour local shift across daylight-saving transitions is intentional;
the repository does not try to emulate a fixed Europe/Berlin wall-clock time
with seasonal cron edits.

The exact activation and operating contract is:

- The outer workflow remains the only scheduled Bundesliga 2026/27 workflow.
  Every production leaf caller remains `workflow_dispatch`-only, including all
  bonus callers and the prepared `schadensfresse` triad.
- The exact 14-job default-success chain, seven context jobs immediately
  followed by their seven matchday jobs, remains unchanged. There is no
  `always()` continuation, retry loop, matrix, bonus job, or `schadensfresse`
  job. A failed job blocks all descendants.
- Every context job keeps `publish_launch_roster_overlay: false`; every
  prediction job keeps `force_prediction: false` and `max_repredictions: 2`.
- The shared `bundesliga-2026-27-production-live-lane` concurrency group keeps
  `cancel-in-progress: false`. A running execution is never cancelled. GitHub
  permits at most one running and one pending execution in the group; if more
  are queued, only the newest pending execution is retained.
- No manual production operation may start while a production-live execution
  is running or pending. Local CLI writes and live recovery are forbidden.
  Manual workflow dispatch is forbidden for the outer workflow and every
  production leaf workflow. Recovery starts only after the lane has neither a
  running nor a pending execution. The shared group protects workflow
  dispatches; it is not permission to create a manual backlog and cannot guard
  local CLI writes.
- `90` minutes is the monitoring and escalation envelope, not a workflow
  `timeout-minutes` value. The observed `51m04` remains the initial planning
  baseline. For the audited later-pass deadline scenario, a `09:07` UTC start
  completing at the edge of that envelope at `10:37` UTC precedes the
  `13:30` UTC kickoff by `2h53`, the approximately three-hour completion
  margin used for activation planning.
- The Project Owner is the activation owner, first-cycle monitor, operational
  on-call contact, and rollback owner. The targets are acknowledgement
  within `30` minutes of an alert and removal of the schedule from the default
  branch within `60` minutes when rollback is required.
- Rollback is required for a failed or cancelled lane, a context/prediction
  ordering violation, an unexpected model/prompt/competition identity, an
  unexpected model call in either accepted copy row, a final Kicktipp or
  Firestore verification mismatch, or a concurrency backlog that threatens the
  next cutoff. Rollback disables the cron; it does not cancel an already
  running execution. Read-only diagnosis may begin immediately, but live
  recovery remains a deliberate follow-up operation and cannot start until the
  lane has neither a running nor a pending execution.
- The first run whose event is `schedule` is the runtime observation gate. The
  Owner inspects every job result, expected copy/generation behavior, final
  Kicktipp and Firestore state, and Langfuse errors/usage before scheduled
  activation is considered complete.
- Bonus work remains excluded from the recurring lane. `schadensfresse` joins
  only through a later reviewed change after administrator setup, manual
  context/match/bonus evidence, and its own readiness gate pass.

The first actual `schedule` event remains an open observation gate. This
decision and its implementation prepare and authorize activation; they do not
fabricate or pre-satisfy runtime evidence that can exist only after the cron is
present on the default branch.

## Alternatives considered

- **Schedule each production leaf independently:** Rejected because separate
  crons cannot encode the strict primary-before-copy and
  context-before-prediction dependency graph, and would make partial failure
  harder to contain.
- **Run only once daily:** Rejected because a later refresh provides a much
  fresher pre-kickoff pass while retaining the audited completion margin.
- **Use a fixed Europe/Berlin wall-clock schedule:** Rejected because GitHub
  cron is UTC; seasonal duplicate schedules and date guards add activation
  complexity without a demonstrated P0 need.
- **Add a 90-minute workflow timeout:** Rejected. Ninety minutes is an
  operational observation boundary; cancelling an in-flight write sequence at
  that point would conflict with the non-cancelling lane contract.
- **Dispatch the outer workflow before activation:** Rejected because the
  ordered leaf validation already exercised the live paths and a redundant
  dispatch may allocate repredictions.
- **Schedule bonuses or `schadensfresse`:** Rejected for this activation.
  Bonuses are one-time preseason state, and `schadensfresse` has not passed its
  external setup and manual evidence gates.

## Consequences

- Integration and push activate one default-branch cron for all currently ready
  matchday rows in a fail-closed serial order;
  exact-head CI is the immediate post-integration gate.
- The later pass has an approximately three-hour monitored completion margin
  before the audited `13:30` UTC kickoff case, while the fixed UTC cadence
  deliberately shifts by one local hour with DST.
- One slow or failed upstream row delays or blocks all later participants. That
  is accepted fail-closed behavior and makes the Project Owner's monitoring and
  rollback responsibilities operationally important.
- This decision does not close P0-21 by itself. Default-branch activation,
  exact-head CI, and successful inspection of the first scheduled run remain
  required. `schadensfresse` remains a later P0 completion item.

## Affected tasks

- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None.
