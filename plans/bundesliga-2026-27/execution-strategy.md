# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy
- Last updated: 2026-09-09

P1-04/P1-05 closeout is limited to the accepted contracts and their dormant
implementation; other P1 work stays excluded. Current production continuity is
unchanged: source flags false; eight-pair recovery topology, cron,
non-cancelling serial/default-success behavior, manual-only leaves, no bonus,
models, prompts, credentials, posting and copy compatibility remain fixed.

## Sequencing

C1/S1/S3 are historical and C2 is accepted at
`c7cc0712b16834d4013948949f1502514ae46770`. The accepted ADR-0081 tracked
specification receives fresh acceptance/publication before a fresh C3 writer
uses its 13 exact paths; E1's 17 exact paths follow accepted C3. R1 follows C2
independently; W1 requires C2 and one accepted source but cannot partially wire
Club Elo. Keep ADR-0074's seven-milestone upper bound. No old run branch,
resource cap, writer count, or per-lane push rule survives this freeze.

## Quality and authority

Every milestone uses a scoped local commit, incremental review where applicable,
a fresh final reviewer, serialized heavy validation, then exact-head CI. Publish
only cohesive independently production-safe reviewed commits. The owner alone
may authorize live acquisition, development persistence, unattended Club Elo
reuse, GitHub artifact/issues, production writes/activation, rollback delegate,
restoration, or completion evidence. Rollback remains flag-off plus reviewed
revert. Synthetic trusted-date fixtures demonstrate mechanics, not real
accepting evidence.
