# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04 active; P1-05 deferred dormant closeout
- Last reconciled: 2026-09-09
- Current policy: [execution strategy](execution-strategy.md)

P0 history and unrelated P1 work are opt-in archive material. The current
production recovery topology, schedules, models, posting, credentials and copy
behavior remain unchanged.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | ADR-0081 published and exact-head green at `f37c9e549e952004c5443f25aab363fda6e2811c` (workflow `34319614680`, all 12 jobs); C3 is correction-limited at local unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8` | Fresh bounded C3 diagnosis/reslicing, correction and review; E1 remains blocked until corrected C3 is accepted, published, and exact-head green; no source activation |
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

`C2 reusable common evidence -> ADR-0081 published/green at
f37c9e549e952004c5443f25aab363fda6e2811c (workflow 34319614680, all 12 jobs)
-> C3 correction-limited at local unintegrated
d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8 -> fresh bounded diagnosis/reslicing,
correction and review -> E1 only after corrected C3 is accepted, published, and
exact-head green.` R1 is deferred under ADR-0082.
Only accepted E1 may release W1. A1 is outside this objective: it later owns E1
attribution only, while roster attribution waits for a future accepted R1.
C1/S1/S3 are historical sequencing, not future work.
Preserve ADR-0074's seven-milestone upper bound. C2 and each source milestone
use incremental review, a fresh final review, one serialized heavy family, and
exact-head CI.

Flags stay false. Owner approval remains separately required for live
acquisition, development persistence, unattended HTML reuse, GitHub artifacts
or issues, production writes/activation, rollback delegation, restoration, and
completion evidence. P1-05 performs no sidecar/artifact probe and no provider,
observation, receipt, artifact, health, publication, or API action; seed/LKG
remains the truthful original-date/provenance fallback and automatic freshness
is unavailable.

## Current artifacts

- [P1 status snapshot](p1-status-snapshot.md)
- [P1-04/P1-05 packet](p1-04-05-execution-packet.md)
- [context-refresh design](designs/p1-04-05-context-refresh.md)
- [decision index](decisions/README.md)
