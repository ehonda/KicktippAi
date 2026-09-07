# P1-05 roster refresh in-flight handoff

- Status: C1 then C2 required; R1 dormant and HTML-independent
- Date reconciled: 2026-09-07
- Task: [P1-05](../tasks/p1-05-roster-refresh.md)
- Authority: ADR-0074 and [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)

## Resume boundary

Commission fresh C1 cumulative review of `c99e163..852d179`; only accepted
exact 20-path content may be integrated onto current main. C2 then implements
the pre-artifact matrix and optional transactional publication fence. R1 may
implement acquisition/selection/diff/carry/v3 after C2 and its acceptance.

The real 210776064-byte DuckDB artifact, revision
`e44f186d6f06dd8452aaf54c7921ba66c961f637`, is historical safe-rejection
evidence (`NO_ELIGIBLE_2026_MEMBERSHIP`, `UNKNOWN_SOURCE_DATE`), not a blocker
or accepting result. It retains seed/LKG. Synthetic trusted-date fixtures prove
future takeover mechanics only.

## Frozen invariants

Apply ADR-0078's exact evaluation precedence and field/null matrix; do not
synthesize advertised revision or `NewRevision`. A rejected receipt may have a
null revision only when its observation does and never advances accepted/pending
revision state. The guard is optional for legacy callers but normative for
source-backed Published/Unchanged/Reactivated outcomes: it runs before head CAS,
uses the same transaction, and is fatal/mutation-free on failure. Replay after a
head-before-receipt crash cannot invent metadata.

Flags stay false. Development persistence, production acquisition/writes,
GitHub artifact/issues, rollback, restoration and completion remain owner gates.
