# P1-04 / P1-05 context-refresh design

- Status: Accepted successor contracts; dormant implementation
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md)

## Seam and graph

One immutable enabled-source observation is reused by eight lanes inside an
existing context cycle; community heads remain independent and atomic. Disabled
sources resolve no services and do no source-cycle, artifact, persistence, issue
or API work. C1 review/content integration is satisfied on current `main` at
`f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`; the current frontier is S1
acceptance, then C2 common matrix/fence, then independently R1 and C3→E1; W1
waits for C2 and one accepted source. This preserves the seven-milestone upper
bound.

## Source contracts

P1-04 uses descriptor-selected official HTML only. The selected payload is
`club-elo/source.html`; historical CSV evidence remains history. ADR-0077 owns
transport/header/DOM/lexer/fragment/date/retry/mapping/null/integer constraints,
displayed-date health, nested Firestore reconstruction, and self-contained
publication-v2 provenance. P1-05 remains HTML-independent. ADR-0078 owns its
metadata-before-artifact evaluation matrix, truthful nulls/revisions, retained
membership/enrichment provenance, and rejection-only real artifact handling.
ADR-0079 pins dcaribou's only future sidecar/artifact URLs and strict envelope;
without that sidecar P1-05 is `MetadataUnavailable`, does not request an
artifact, and retains seed/LKG.

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

C2 proves null round trips, supersession, guard/CAS precedence, crash retry,
legacy compatibility, reason precedence, canonical lane IDs, and guarded
metadata-unchanged prior receipt/health validation. R1's future test surface
also proves hostile sidecars/URLs, null/type/range/canonical/date defects, and
mixed-generation rejection. C3/E1 prove the literal HTML fixtures/reconstruction/
integer cases; R1 proves rejection and synthetic takeover. Use incremental then
fresh final review, one heavy family, exact-head CI. Flags remain false. The
owner separately controls live acquisition, development persistence, unattended
HTML reuse, GitHub artifacts/issues, production activation/writes, rollback,
restoration and completion evidence. Synthetic trusted-date fixtures prove
mechanics only.
