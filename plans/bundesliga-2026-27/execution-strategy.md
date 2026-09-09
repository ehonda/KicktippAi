# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy
- Last updated: 2026-09-09

P1-04 and the P1-05 dormant closeout are limited to accepted contracts; R1 and
all other P1 work stay excluded. Current production continuity is
unchanged: source flags false; eight-pair recovery topology, cron,
non-cancelling serial/default-success behavior, manual-only leaves, no bonus,
models, prompts, credentials, posting and copy compatibility remain fixed.

## Sequencing

C1/S1/S3 are historical and C2 is reusable common evidence. ADR-0081 is
published and exact-head green at `f37c9e549e952004c5443f25aab363fda6e2811c`
(workflow `34319614680`, all 12 jobs); C3 is correction-limited at local
unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
diagnosis/reslicing and subsequent correction/review. E1 remains blocked until
corrected C3 is accepted, published, and exact-head green. R1 is deferred under
ADR-0082 until the exact dcaribou sidecar and separate owner gates exist. Only
accepted E1 may release W1; W1/A1 are outside this objective and not released.
A1 later owns E1 attribution only, while roster attribution waits for future
accepted R1. Keep ADR-0074's seven-milestone upper bound. No old run branch,
resource cap, writer count, or per-lane push rule survives this freeze.

## Quality and authority

Every milestone uses a scoped local commit, incremental review where applicable,
a fresh final reviewer, serialized heavy validation, then exact-head CI. Publish
only cohesive independently production-safe reviewed commits. The owner alone
may authorize live acquisition, development persistence, unattended Club Elo
reuse, GitHub artifact/issues, production writes/activation, rollback delegate,
restoration, or completion evidence. Rollback remains flag-off plus reviewed
revert. P1-05 makes no sidecar/artifact probe, provider, observation, receipt,
artifact, health, publication, or API action; seed/LKG remains and v1/v2 are
unchanged with no v3. Synthetic trusted-date fixtures demonstrate mechanics,
not real accepting evidence.
