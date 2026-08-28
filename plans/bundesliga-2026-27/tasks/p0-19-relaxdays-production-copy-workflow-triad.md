# P0-19 — Add the `relaxdays-tippt` production-copy workflow triad

- Status: Complete — the leaf triad remains manual-only and schedule-free while ADR-0053's outer lane owns the active recurring schedule
- Priority: P0
- Matrix row: `relaxdays-production-copy`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-18](p0-18-base-workflow-support.md), and the [`pes-squad` reference triad](p0-19-pes-squad-production-reference-workflow-triad.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md)

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
- [x] `community-rules/relaxdays-tippt.md` supplies the exact
      `community-rules-relaxdays-tippt.md` context-document identity and is
      textually identical to the accepted default-rule source
      `pes-squad.md`. A fresh checkout reconstructs the same CRLF SHA-256
      `e52945f0d63e9a332ee225d4a9fd60677b761771dac0ac6cc8d7957143252292`.
      Missing selected rule sources still fail closed.
- [x] Every caller exposes only `workflow_dispatch`; none has a schedule or
      `workflow_call`.
- [x] The workflow contract fixes exact inputs, context, credential names, and
      schedule absence.

## Completed P0-21 gates

- [x] Authenticate, establish current competition/read readiness and working
      posting behavior, and complete one context-before-prediction manual cycle.
- [x] Complete payload-safe copy/no-extra-generation inspection.
- [x] Record the timestamped community deadline audit: zero minutes lead time;
      first match and bonus cutoff 2026-08-28 20:30 CEST / 18:30 UTC, subject
      to a fresh read after rescheduling or an administrator rule change.
- [x] Keep this leaf triad schedule-free. Accepted ADR-0053's active outer
      ready-row lane owns recurring scheduling; P0-21 natural run `33143114280`
      later completed its first observation.

The first authorized context attempt, Actions run
[`33047564359`](https://github.com/ehonda/KicktippAi/actions/runs/33047564359),
authenticated, found the current season and matches, and published the pinned
roster overlay. Normal profile collection then failed closed because the
required checked-in `relaxdays-tippt.md` rules source was missing. The tracked
source and deterministic production-community coverage remediate that
repository defect. Exact repair commit
`eedf33052591beb5bbdc51c9e0ebe9869d5ab64d` passed exact-head CI run
[`33049482431`](https://github.com/ehonda/KicktippAi/actions/runs/33049482431).
The authorized context retry
[`33049949393`](https://github.com/ehonda/KicktippAi/actions/runs/33049949393),
match copy
[`33050188533`](https://github.com/ehonda/KicktippAi/actions/runs/33050188533),
and bonus copy
[`33050549422`](https://github.com/ehonda/KicktippAi/actions/runs/33050549422)
then completed successfully with final verification. The original failed run is
preserved as failure evidence. The payload-safe audit proves this compatible
path copied 9/9 matches and 5/5 bonus answers, generated zero model calls, and
used no independent fallback.
