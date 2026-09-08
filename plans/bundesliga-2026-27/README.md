# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04/P1-05 active only
- Last reconciled: 2026-09-08
- Current policy: [execution strategy](execution-strategy.md)

P0 history and unrelated P1 work are opt-in archive material. The current
production recovery topology, schedules, models, posting, credentials and copy
behavior remain unchanged.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Official HTML contract accepted; dormant | S3 tracked-contract acceptance/publication, then C2, C3, then E1; no source activation |
| [P1-05](tasks/p1-05-roster-refresh.md) | dcaribou-sidecar-gated roster lane | S3 tracked-contract acceptance/publication, then C2; R1 remains dormant until a conforming sidecar and separate owner gates |
| P1-10 and every other P1 task | Excluded | Not released by this closeout |

The operative decisions are [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md),
[ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md), and
[ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md),
[ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), and
[ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md).
ADR-0077 preserves CSV-era evidence as history and selects official HTML only
for the dormant P1-04 source. ADR-0078 refines roster pre-artifact evaluation
and source-backed publication fencing without enabling either source; ADR-0080
narrowly replaces only its transitional C1 inference with receipt-first,
provable-`Unchanged` recovery. ADR-0079
pins the future dcaribou sidecar/artifact contract; until that sidecar exists,
P1-05 is `MetadataUnavailable`, makes no artifact request, and retains seed/LKG.

## Required order and gates

`C1 satisfied/integrated at f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07 -> S1
acceptance -> S3 ADR-0080 contract acceptance -> C2 shared amendment -> (C3
HTML common -> E1) and R1; W1 follows C2 and one accepted source.` The C1
review of `c99e163..852d179` and its
20-path integration are satisfied historical sequencing, not future work.
Preserve ADR-0074's seven-milestone upper bound. C2 and each source milestone
use incremental review, a fresh final review, one serialized heavy family, and
exact-head CI.

Flags stay false. Owner approval remains separately required for live
acquisition, development persistence, unattended HTML reuse, GitHub artifacts
or issues, production writes/activation, rollback delegation, restoration, and
completion evidence.

## Current artifacts

- [P1 status snapshot](p1-status-snapshot.md)
- [P1-04/P1-05 packet](p1-04-05-execution-packet.md)
- [context-refresh design](designs/p1-04-05-context-refresh.md)
- [decision index](decisions/README.md)
