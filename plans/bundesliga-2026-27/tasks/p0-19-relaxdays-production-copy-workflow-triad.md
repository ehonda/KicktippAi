# P0-19 — Add the `relaxdays-tippt` production-copy workflow triad

- Status: Complete — repository preparation is manual-only and schedule-free
- Priority: P0
- Matrix row: `relaxdays-production-copy`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-18](p0-18-base-workflow-support.md), and the [`pes-squad` reference triad](p0-19-pes-squad-production-reference-workflow-triad.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

The manual context, matchday, and bonus callers target `relaxdays-tippt`.
Predictions use `community_context: "pes-squad"` and the exact Sol/`xhigh`, cap
`10000`, match-v3/bonus-v1 production identity so compatible reference values
copy without another model call. The default-rule compatibility assumption is
verified again at runtime; bonus incompatibility follows ADR-0048's independent
target-context fallback.

The local profile is `.env.relaxdays-tippt`; the Owner confirmed
`RELAXDAYS_TIPPT_KICKTIPP_USERNAME` and
`RELAXDAYS_TIPPT_KICKTIPP_PASSWORD` are provisioned. This proves neither
authentication nor POST permission.

## Repository evidence

- [x] `relaxdays-tippt-context-collection.yml` is target-context collection and
      opts into the exact pinned, fail-closed P0-25 launch-roster overlay before
      normal profile collection.
- [x] `buli2627-relaxdays-tippt-gpt-5-6-sol-xhigh-matchday.yml` copies the
      `pes-squad` reference with the exact production identity.
- [x] `buli2627-relaxdays-tippt-gpt-5-6-sol-xhigh-bonus.yml` uses the exact
      production identity and budgets `20` / `32000`.
- [x] Every caller exposes only `workflow_dispatch`; none has a schedule or
      `workflow_call`.
- [x] The workflow contract fixes exact inputs, context, credential names, and
      schedule absence.

## Remaining P0-21 gates

- [ ] Authenticate, establish current competition/read readiness and POST
      permission, record deadlines and rule compatibility, and inspect one
      context-before-prediction manual cycle.
- [ ] Enable no schedule until the final Owner activation decision.
