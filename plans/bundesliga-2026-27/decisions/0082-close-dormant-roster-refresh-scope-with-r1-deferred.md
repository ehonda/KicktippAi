# ADR-0082: Close the dormant roster-refresh scope with R1 deferred

- Status: Accepted
- Date: 2026-09-09
- Supersedes: None
- Implementation status reconciled: 2026-09-15; the accepted roster-deferral decision is unchanged

## Context

P1-05 remains a future dcaribou-sidecar-gated roster-refresh lane. C2 is
reusable common evidence, not R1 implementation. The rejected R1 range ending
at `b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected closeout
`80b7c6c` remain unintegrated evidence only and convey no implementation credit.

ADR-0073, ADR-0074, ADR-0078, ADR-0079, ADR-0080, and ADR-0081 remain the
technical and future-completion contracts. ADR-0079 remains the sole authority
for the exact dcaribou sidecar, URL, envelope, dates, and owner gates.

## Decision

Close this run's P1-05 portion as documented dormant scope. Its task status is
`Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082.`
Roster functionality remains deferred and incomplete.

Dcaribou is the sole metadata authority. Until the exact ADR-0079 sidecar
exists, P1-05 is `MetadataUnavailable`: there is no probe, provider resolution,
artifact request, observation, receipt, artifact, health, publication, issue,
or API action. Retain seed/LKG with truthful original dates and provenance;
automatic freshness is unavailable. The current DuckDB and synthetic
trusted-date fixtures are rejection/mechanics evidence only.

R1 acquisition/provider/consumer work, selection/diff/carry, canonical
v3/reconstruction, source tests, enrichment automation, and valid takeover
remain deferred. Existing v1/v2 remain unchanged; no v3 relabel, migration, or
backfill occurs. No provider adoption, source activation, persistence,
unattended HTML reuse, GitHub/R2 mutation, production write/activation,
rollback delegation, restoration, or operational completion evidence is authorized.

P1-04 dormant implementation is complete as of 2026-09-15.

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

Accepted E1 satisfies W1's implementation prerequisite. W1/A1 remain outside
this objective and unreleased; future A1 owns E1 attribution only, while roster
attribution waits for future accepted R1. This is implementation-status
reconciliation, with the accepted P1-05 deferral decision unchanged.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.

## Alternatives considered

- Treat the rejected R1 or closeout work as implementation: rejected because it
  is unintegrated and does not satisfy the authoritative sidecar or owner gates.
- Probe the sidecar or revive a provider during closeout: rejected because
  `MetadataUnavailable` allows no probe or provider action.

## Consequences

- P1-05 remains a current deferred execution contract, neither complete nor
  archived.
- A future R1 must satisfy ADR-0079 and obtain separate owner gates.
- No runtime, test, data, workflow, source, provider, or activation surface is
  changed by this documentation milestone.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md)
- [P1-05 roster refresh](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 execution packet](../p1-04-05-execution-packet.md)
- [P1-04/P1-05 context-refresh design](../designs/p1-04-05-context-refresh.md)
