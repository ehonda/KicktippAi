# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04/P1-05 active only
- Last reconciled: 2026-09-07
- Current policy: [execution strategy](execution-strategy.md)

P0 history and unrelated P1 work are opt-in archive material. The current
production recovery topology, schedules, models, posting, credentials and copy
behavior remain unchanged.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Official HTML contract accepted; dormant | C1 integration, C2, C3, then E1; no source activation |
| [P1-05](tasks/p1-05-roster-refresh.md) | Source-independent roster lane | C1 integration then C2, then R1; real artifact is rejection-only evidence |
| P1-10 and every other P1 task | Excluded | Not released by this closeout |

The operative decisions are [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md),
[ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md), and
[ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md).
ADR-0077 preserves CSV-era evidence as history and selects official HTML only
for the dormant P1-04 source. ADR-0078 refines roster pre-artifact evaluation
and source-backed publication fencing without enabling either source.

## Required order and gates

`accepted ADRs/current contract -> C1 fresh cumulative review of
c99e163..852d179 -> content-integrate exact reviewed 20-path bytes onto current
main -> C2 shared amendment -> (C3 HTML common -> E1) and R1; W1 follows C2 and
one accepted source.` Preserve ADR-0074's seven-milestone upper bound. C2 and
each source milestone use incremental review, a fresh final review, one
serialized heavy family, and exact-head CI.

Flags stay false. Owner approval remains separately required for live
acquisition, development persistence, unattended HTML reuse, GitHub artifacts
or issues, production writes/activation, rollback delegation, restoration, and
completion evidence.

## Current artifacts

- [P1 status snapshot](p1-status-snapshot.md)
- [P1-04/P1-05 packet](p1-04-05-execution-packet.md)
- [context-refresh design](designs/p1-04-05-context-refresh.md)
- [decision index](decisions/README.md)
