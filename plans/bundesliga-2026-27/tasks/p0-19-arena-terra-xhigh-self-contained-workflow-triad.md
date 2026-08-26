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

- [ ] Authenticate, verify competition readiness and POST permission, record
      deadlines, and inspect one context-before-prediction manual cycle.
- [ ] Enable no schedule until the final Owner activation decision.
