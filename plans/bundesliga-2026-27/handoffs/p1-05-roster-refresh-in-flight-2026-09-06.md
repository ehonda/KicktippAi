# P1-05 roster refresh in-flight handoff

- Status: Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082.
- Date reconciled: 2026-09-09
- Task: [P1-05](../tasks/p1-05-roster-refresh.md)
- Authority: ADR-0074, [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Resume boundary

C2 is reusable common evidence, not R1 implementation credit. This run closes
only the documented dormant scope under ADR-0082. R1 may implement
acquisition/provider/consumer work, selection/diff/carry, canonical
v3/reconstruction, source tests, enrichment automation, and valid takeover
criteria only after a conforming dcaribou sidecar at the exact ADR-0079 pinned
URL and separate owner permission.

The real 210776064-byte DuckDB artifact, revision
`e44f186d6f06dd8452aaf54c7921ba66c961f637`, is historical safe-rejection
evidence (`NO_ELIGIBLE_2026_MEMBERSHIP`, `UNKNOWN_SOURCE_DATE`), not a blocker
or accepting result. Dcaribou is the sole metadata authority. Its future
sidecar is absent/404, so this is `MetadataUnavailable`: no probe, provider
resolution, observation, receipt, artifact, health, publication, or API action,
and no accepted/pending revision mutation. It retains seed/LKG with truthful
original dates/provenance; automatic freshness is unavailable. Synthetic
trusted-date fixtures prove future takeover mechanics only. Existing v1/v2
remain unchanged; no v3 relabel, migration, or backfill occurs.

## Frozen invariants

Apply ADR-0078's exact evaluation precedence and field/null matrix plus
ADR-0079's strict provider-sidecar envelope/order/URLs; do not synthesize
advertised revision or `NewRevision`. A rejected receipt may have a
null revision only when its observation does and never advances accepted/pending
revision state. The guard is optional for legacy callers but normative for
source-backed Published/Unchanged/Reactivated outcomes: it runs before head CAS,
uses the same transaction, and is fatal/mutation-free on failure. Replay after a
head-before-receipt crash cannot invent metadata.

The rejected R1 `b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected
closeout `80b7c6c` are unintegrated evidence only and convey no implementation
credit. Flags stay false. Development persistence, production acquisition/writes,
unattended HTML reuse, GitHub/R2 mutation, rollback delegation, restoration,
completion evidence, and activation remain owner gates. This handoff remains a
current deferred contract, not archival material.
