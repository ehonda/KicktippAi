# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy under [ADR-0083](decisions/0083-activate-official-club-elo-context-refresh.md)
- Last updated: 2026-09-16

## Current activation strategy

P1-04 is an operational activation lane, not a dormant-complete task. Merge of
PR #111 is prior C3/E1 implementation input. The current graph is S0 → E2/W2
in parallel → V2 → source-only live validation → A2 → closeout/post-merge.
E2 proves the exact paired parser/table v2 and historical v1 compatibility; W2
proves immutable artifact/issue transport; V2 adds explicit CLI/DI/workflow
wiring while normal flags stay false. A2 is admitted only after real
development and production source-only evidence.

## Owner and authority gates

Production continuity stays fixed: existing cron/topology, models, prompts,
credentials, posting and copy behavior do not change. The bounded owner
authority covers Club Elo reads/context writes/GitHub handoff and issues/
existing-schedule source activation after evidence/Pages and a ready PR merge.
It excludes model calls, prediction/posting changes, roster, new schedules,
credential-routing and unrelated P1 work. Rollback is reviewed all-eight
flag-off; restoration needs corrected reviewed code and fresh accepting then
retention evidence. The owner is recovery owner.

Validate offline first, then local development dry run/persistence/distinct
cycle, branch development source-only dispatch, branch production source-only
dispatch with artifact/eight receipts/four heads, later distinct retention and
same-run replay. A source-only pass never certifies the whole schedule. Final
merge requires fresh independent acceptance and exact-head CI; post-merge
checks main, Pages, a source-only dispatch and the next scheduled run.

P1-05/R1 remains dormant under ADR-0082 and has no sidecar probe, provider or
adoption action. At or above 20 GiB effective free space there is no disk
restriction.

## Historical dormant strategy

The retained strategy below describes the prior dormant closeout only. ADR-0083
and the preceding current strategy supersede it for activation sequencing.

# Historical Bundesliga 2026/27 execution strategy

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
