# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy
- Last updated: 2026-09-15

P1-04 dormant implementation and the P1-05 dormant closeout are complete; R1 and
all other P1 work stay excluded. Current production continuity is
unchanged: source flags false; eight-pair recovery topology, cron,
non-cancelling serial/default-success behavior, manual-only leaves, no bonus,
models, prompts, credentials, posting and copy compatibility remain fixed.

## Sequencing

C1/S1/S3 are satisfied and C2 remains reusable common evidence. The accepted
C3 → E1 dormant implementation sequence is complete.

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

R1 remains deferred under ADR-0082 until the exact ADR-0079 dcaribou sidecar
and separate owner gates exist. Accepted E1 satisfies W1's implementation
prerequisite, but W1/A1 remain outside this objective and unreleased. Future A1
owns E1 attribution only; roster attribution waits for future accepted R1.
ADR-0074's seven-milestone upper bound remains the contract for future work.

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

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.
