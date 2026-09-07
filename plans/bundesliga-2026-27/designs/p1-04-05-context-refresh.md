# P1-04 / P1-05 context-refresh design

- Status: Accepted successor contracts; dormant implementation
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)

## Seam and graph

One immutable enabled-source observation is reused by eight lanes inside an
existing context cycle; community heads remain independent and atomic. Disabled
sources resolve no services and do no source-cycle, artifact, persistence, issue
or API work. The only valid sequence is C1 cumulative review and content
integration, C2 common matrix/fence, then independently R1 and C3→E1; W1 waits
for C2 and one accepted source. This preserves the seven-milestone upper bound.

## Source contracts

P1-04 uses descriptor-selected official HTML only. The selected payload is
`club-elo/source.html`; historical CSV evidence remains history. ADR-0077 owns
transport/header/DOM/lexer/fragment/date/retry/mapping/null/integer constraints,
displayed-date health, nested Firestore reconstruction, and self-contained
publication-v2 provenance. P1-05 remains HTML-independent. ADR-0078 owns its
metadata-before-artifact evaluation matrix, truthful nulls/revisions, retained
membership/enrichment provenance, and rejection-only real artifact handling.

## Publication fence and recovery

For source-backed Published/Unchanged/Reactivated operations, the non-null
guard validates typed cycle/scope/source/lane/community/publication-set identity,
enabled source, `HandoffReady`/bundle digest, `Finalized`/observation digest/
strict receipt prefix, watermark, and absent exact receipt in the same
transaction before head CAS. It wins over head equality; failures are
fatal/mutation-free. A null guard is the legacy contract. Replay following a
head-before-receipt crash may create only an exact eligible receipt; it never
fabricates historical source metadata.

## Verification and activation

C2 proves null round trips, supersession, guard/CAS precedence, crash retry and
legacy compatibility. C3/E1 prove the literal HTML fixtures/reconstruction/
integer cases; R1 proves rejection and synthetic takeover. Use incremental then
fresh final review, one heavy family, exact-head CI. Flags remain false. The
owner separately controls live acquisition, development persistence, unattended
HTML reuse, GitHub artifacts/issues, production activation/writes, rollback,
restoration and completion evidence. Synthetic trusted-date fixtures prove
mechanics only.
