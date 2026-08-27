# P0-19 — Add the self-contained arena Terra/xhigh workflow triad

- Status: Complete — repository preparation is manual-only and schedule-free
- Priority: P0
- Matrix row: `arena-terra-xhigh-self-contained`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome and evidence

The three `buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-*` callers use posting
and context community `ehonda-ai-arena`, `gpt-5.6-terra` / `xhigh`, cap `10000`,
match v3 / bonus v1, Flex-first / Standard-fallback, and bonus budgets `20` /
`32000`. They expose only `workflow_dispatch` and contain no schedule or
`workflow_call`. Local use selects
`.env.ehonda-ai-arena.gpt-5-6-terra-xhigh`. The Owner confirmed the exact
`EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_USERNAME` and
`EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_PASSWORD` pair is provisioned.

## Remaining P0-21 gates

- [x] Complete the context-before-prediction manual cycle. Context
      [`33053664914`](https://github.com/ehonda/KicktippAi/actions/runs/33053664914)
      and matchday
      [`33053888656`](https://github.com/ehonda/KicktippAi/actions/runs/33053888656)
      plus bonus
      [`33054314209`](https://github.com/ehonda/KicktippAi/actions/runs/33054314209)
      passed with final verification on exact head
      `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`.
- [x] Complete payload-safe prompt/context/model/cost/error inspection. The
      aggregate P0-21 audit found exactly 9 match and 5 bonus index-0
      generations for this exact Terra/`xhigh` / cap-`10000` row, with no
      index `1+` or errors. Two of its 14 successful calls used Standard
      fallback; the other 12 used Flex.
- [x] Record the community-scoped deadline audit: zero minutes lead time; first
      match and bonus cutoff 2026-08-28 20:30 CEST / 18:30 UTC, subject to a
      fresh read after rescheduling or an administrator rule change.
- [ ] Enable no schedule until the final Owner activation decision.
