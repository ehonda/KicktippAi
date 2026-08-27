# P0-19 — Add the self-contained arena Luna/medium workflow triad

- Status: Complete — the leaf triad remains manual-only and schedule-free while ADR-0053's outer lane owns the active recurring schedule
- Priority: P0
- Matrix row: `arena-luna-medium-self-contained`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md)

## Outcome and evidence

The three `buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-*` callers use posting
and context community `ehonda-ai-arena`, `gpt-5.6-luna` / `medium`, cap `10000`,
match v3 / bonus v1, Flex-first / Standard-fallback, and bonus budgets `20` /
`32000`. They expose only `workflow_dispatch` and contain no schedule or
`workflow_call`. Local use selects
`.env.ehonda-ai-arena.gpt-5-6-luna-medium`. The Owner confirmed the exact
`EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_USERNAME` and
`EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_PASSWORD` pair is provisioned.

## Remaining P0-21 gates

- [x] Authenticate, verify competition readiness and working posting behavior,
      and complete one context-before-prediction manual cycle: context
      [`33052882246`](https://github.com/ehonda/KicktippAi/actions/runs/33052882246),
      matchday [`33053095243`](https://github.com/ehonda/KicktippAi/actions/runs/33053095243),
      and bonus [`33053423396`](https://github.com/ehonda/KicktippAi/actions/runs/33053423396)
      all passed final verification on exact head
      `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`.
- [x] Complete payload-safe prompt/context/model/cost/error inspection. The
      aggregate P0-21 audit found exactly 9 match and 5 bonus index-0
      generations for this exact Luna/`medium` / cap-`10000` row, with no
      index `1+`, fallback, or errors.
- [x] Record the community-scoped deadline audit: zero minutes lead time; first
      match and bonus cutoff 2026-08-28 20:30 CEST / 18:30 UTC, subject to a
      fresh read after rescheduling or an administrator rule change.
- [x] Keep this leaf triad schedule-free. Accepted ADR-0053's active outer
      ready-row lane owns recurring scheduling; its first natural observation
      remains an open P0-21 runtime gate.
