# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04 dormant implementation complete; P1-05 dormant closeout complete with R1 deferred
- Last reconciled: 2026-09-15
- Current policy: [execution strategy](execution-strategy.md)

P0 history and unrelated P1 work are opt-in archive material. The current
production recovery topology, schedules, models, posting, credentials and copy
behavior remain unchanged.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Dormant implementation complete: C3 and E1 accepted, pushed to draft PR #111, and exact-head CI green | Separate owner gates for operational/live validation and activation; source flags remain false |
| [P1-05](tasks/p1-05-roster-refresh.md) | Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082. | Future R1 remains deferred until a conforming sidecar and separate owner gates |
| P1-10 and every other P1 task | Excluded | Not released by this closeout |

The operative decisions are [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md),
[ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md),
[ADR-0081](decisions/0081-close-club-elo-html-publication-and-selection-seams.md),
[ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md),
[ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), and
[ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md), and
[ADR-0082](decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md).
ADR-0077 preserves CSV-era evidence as history and selects official HTML only
for the dormant P1-04 source. ADR-0078 refines roster pre-artifact evaluation
and source-backed publication fencing without enabling either source; ADR-0080
narrowly replaces only its transitional C1 inference with receipt-first,
provable-`Unchanged` recovery. ADR-0079
pins the future dcaribou sidecar/artifact contract; until that sidecar exists,
P1-05 is `MetadataUnavailable`, makes no artifact request, and retains seed/LKG.

## Required order and gates

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

C1/S1/S3 sequencing is satisfied and C2 remains reusable common evidence.
The C3 → E1 dormant implementation sequence is complete. R1 remains deferred
under ADR-0082 until the exact ADR-0079 dcaribou sidecar and separate owner
gates exist. Accepted E1 satisfies W1's implementation prerequisite, but
W1/A1 are outside this objective and remain unreleased. Future A1 owns E1
attribution only; roster attribution waits for a future accepted R1.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.

P1-05 performs no sidecar/artifact probe and no provider, observation, receipt,
artifact, health, publication, issue, or API action. Seed/LKG remains the
truthful original-date/provenance fallback; automatic freshness is unavailable.
Rejected R1 and old closeout work receive no implementation credit.

## Current artifacts

- [P1 status snapshot](p1-status-snapshot.md)
- [P1-04/P1-05 packet](p1-04-05-execution-packet.md)
- [context-refresh design](designs/p1-04-05-context-refresh.md)
- [decision index](decisions/README.md)
