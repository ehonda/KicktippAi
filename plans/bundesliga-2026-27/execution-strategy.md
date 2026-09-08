# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy
- Last updated: 2026-09-08

P1-04/P1-05 closeout is limited to the accepted contracts and their dormant
implementation; other P1 work stays excluded. Current production continuity is
unchanged: source flags false; eight-pair recovery topology, cron,
non-cancelling serial/default-success behavior, manual-only leaves, no bonus,
models, prompts, credentials, posting and copy compatibility remain fixed.

## Sequencing

C1 review and exact 20-path content integration are historical, satisfied work
at `f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`; no future C1 review or
integration is sequenced. S1 is accepted, then S3 accepts ADR-0080's bounded
transitional contract before C2. C2 corrects the five local fence/recovery
areas and direct guarded Firebase matrix. C3 then E1 follow accepted C2 and
ADR-0077; R1 follows C2 independently; W1 requires C2 and an accepted source.
Keep ADR-0074's seven-milestone upper bound. No old run branch, resource cap,
writer count, or per-lane push rule survives this freeze.

## Quality and authority

Every milestone uses a scoped local commit, incremental review where applicable,
a fresh final reviewer, serialized heavy validation, then exact-head CI. Publish
only cohesive independently production-safe reviewed commits. The owner alone
may authorize live acquisition, development persistence, unattended Club Elo
reuse, GitHub artifact/issues, production writes/activation, rollback delegate,
restoration, or completion evidence. Rollback remains flag-off plus reviewed
revert. Synthetic trusted-date fixtures demonstrate mechanics, not real
accepting evidence.
