# P1-05 roster refresh in-flight handoff

- Status: C1 satisfied/integrated; S1 acceptance then C2; R1 sidecar-gated, dormant and HTML-independent
- Date reconciled: 2026-09-08
- Task: [P1-05](../tasks/p1-05-roster-refresh.md)
- Authority: ADR-0074, [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), and [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md)

## Resume boundary

C1 review/integration is satisfied on current `main` at exact
`f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`; the `c99e163..852d179` review and
20-path integration are historical. Resume with S1 acceptance, then C2's
pre-artifact matrix and optional transactional publication fence. R1 may
implement acquisition/selection/diff/carry/v3 only after C2 acceptance, a
conforming dcaribou sidecar at the exact pinned URL, and separate owner
permission.

The real 210776064-byte DuckDB artifact, revision
`e44f186d6f06dd8452aaf54c7921ba66c961f637`, is historical safe-rejection
evidence (`NO_ELIGIBLE_2026_MEMBERSHIP`, `UNKNOWN_SOURCE_DATE`), not a blocker
or accepting result. The future sidecar is currently absent/404, so this is
`MetadataUnavailable`: no artifact request and no accepted/pending revision
mutation. It retains seed/LKG. Synthetic trusted-date fixtures prove future
takeover mechanics only.

## Frozen invariants

Apply ADR-0078's exact evaluation precedence and field/null matrix plus
ADR-0079's strict provider-sidecar envelope/order/URLs; do not synthesize
advertised revision or `NewRevision`. A rejected receipt may have a
null revision only when its observation does and never advances accepted/pending
revision state. The guard is optional for legacy callers but normative for
source-backed Published/Unchanged/Reactivated outcomes: it runs before head CAS,
uses the same transaction, and is fatal/mutation-free on failure. Replay after a
head-before-receipt crash cannot invent metadata.

Flags stay false. Development persistence, production acquisition/writes,
GitHub artifact/issues, rollback, restoration and completion remain owner gates.
