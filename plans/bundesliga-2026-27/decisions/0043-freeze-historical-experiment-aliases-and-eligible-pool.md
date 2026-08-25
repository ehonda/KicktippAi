# ADR-0043: Freeze historical experiment aliases and the context-eligible pool

- Status: Accepted
- Date: 2026-08-25

## Context

ADR-0040 established a read-only, hash-bound Bundesliga 2025/26 route for preseason cost experiments. Its initial implementation reused `MatchContextDocumentCatalog` to derive the seven historical document names. That catalog now intentionally serves current competition routes: for `bundesliga-2025-26` it slugified full team names such as `fc-st-pauli` and `1-fc-koln`. The 2025/26 context producer instead used fixed aliases such as `fcs` and `fck`.

A read-only coverage audit on 2026-08-25 examined every completed `pes-squad` Bundesliga 2025/26 fixture strictly after the Luna sampling cutoff `2026-02-18T00:00:00 Europe/Berlin (+01)`, resolving context at `startsAt -12h`. The current full-name route found zero eligible fixtures: the two common documents existed, but the five fixture-specific names did not. The exact producer-era alias route found all 109 fixtures eligible and all 763 required document references valid. The sorted-newline SHA-256 of the 109 canonical source item IDs is `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`.

Selecting from completed outcomes before checking context also made a valid seed depend on whether its first sampled fixtures happened to have complete reconstruction input. Retrying seeds would bias the declared random sample and obscure the actual eligible population.

## Decision

Add a separate `Bundesliga2025_26HistoricalExperimentDocumentCatalog`. It freezes the exact producer-era mappings:

| Team | Alias | Team | Alias |
|---|---|---|---|
| 1. FC Heidenheim 1846 | `fch` | 1. FC Köln | `fck` |
| 1. FC Union Berlin | `fcu` | 1899 Hoffenheim | `tsg` |
| Bayer 04 Leverkusen | `b04` | Bor. Mönchengladbach | `bmg` |
| Borussia Dortmund | `bvb` | Eintracht Frankfurt | `sge` |
| FC Augsburg | `fca` | FC Bayern München | `fcb` |
| FC St. Pauli | `fcs` | FSV Mainz 05 | `m05` |
| Hamburger SV | `hsv` | RB Leipzig | `rbl` |
| SC Freiburg | `scf` | VfB Stuttgart | `vfb` |
| VfL Wolfsburg | `wob` | Werder Bremen | `svw` |

Only the historical context resolver and historical manifest validation use this catalog. The ordinary/live `MatchContextDocumentCatalog`, live Bundesliga 2026/27 eleven-document route, Firestore schemas, and repositories are unchanged.

Historical repeated-match-slice preparation must:

1. load every completed fixture in the declared scope that starts strictly after the exact sampling cutoff;
2. resolve the exact ordered seven-document producer-era route for every fixture at `startsAt -12h`;
3. classify only a genuinely absent exact document/version as context-ineligible, while malformed identity, scope, timestamp, or content provenance fails closed;
4. fail if the complete context-eligible pool has fewer distinct fixtures than requested; and
5. apply the declared seed to that complete pool exactly once with the existing Fisher-Yates implementation, without retrying or substituting a seed.

The local prepared compatibility contract binds eligibility policy `bundesliga-2025-26-completed-after-sampling-cutoff-all-7-context-documents-at-or-before-starts-at-minus-12h-v1`, the eligible fixture count, and the sorted-newline eligible source-ID hash. These fields participate in the historical aggregate SHA-256 and propagate as compact run metadata. Existing selected fixture IDs and their hash remain bound. The synced Langfuse dataset's public shape is unchanged.

## Alternatives considered

- **Restore the legacy alias dictionary in the live catalog:** Rejected because current competition context identity must remain season-scoped and independently evolvable.
- **Retry seeds until selected fixtures resolve:** Rejected because it hides the eligible population and changes the sampling distribution.
- **Treat malformed historical rows as ineligible:** Rejected because corrupt scope or identity is a provenance failure, not missing coverage.
- **Migrate or rename Firestore rows:** Rejected for the same immutability and no-write reasons recorded by ADR-0040.

## Consequences

- The authorized Luna one-by-one and five-by-four samples draw reproducibly from the complete 109-fixture eligible population.
- Historical aliases cannot drift when the live team manifest or generic slug rules change.
- Preparation performs up to seven read-only context lookups for every completed post-cutoff fixture before sampling; this is deliberate pre-spend provenance validation.
- Missing coverage reduces the explicit eligible count and may fail preparation, but never causes a silent seed retry.
- This decision changes no production selection, schedule, prediction-quality conclusion, or live Firestore data.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P1-07](../tasks/p1-07-cost-calibration.md)

## Refines

- [ADR-0040](0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md)
