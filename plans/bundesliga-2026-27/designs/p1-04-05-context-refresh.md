# P1-04 / P1-05 context-refresh design

- Status: Accepted P1-04 successor contracts; P1-05 dormant closeout with R1 deferred
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Seam and graph

One immutable enabled-source observation is reused by eight lanes inside an
existing context cycle; community heads remain independent and atomic. Disabled
sources resolve no services and do no source-cycle, artifact, persistence, issue
or API work. C2 is reusable common evidence. ADR-0081 is published/green at
`f37c9e549e952004c5443f25aab363fda6e2811c` with workflow `34319614680` and
all 12 jobs green. The current frontier is C3 correction-limited at local
unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
diagnosis/reslicing and subsequent correction/review. E1 remains blocked until
corrected C3 is accepted, published, and exact-head green. R1 is deferred under
ADR-0082 until the exact ADR-0079 sidecar and separate owner gates exist. Only
accepted E1 may release W1; A1 later links E1 attribution only, while roster
attribution waits for future accepted R1. W1/A1 do not start here. This
preserves the seven-milestone upper bound.

## Source contracts

P1-04 uses descriptor-selected official HTML only. The selected payload is
`club-elo/source.html`; historical CSV evidence remains history. ADR-0077 owns
detailed acquisition/parser constraints; ADR-0081 closes descriptor evidence,
shared selection, receipt completion, publication reconstruction and
family-specific numeric rules. P1-05 remains HTML-independent. ADR-0078 owns its
metadata-before-artifact evaluation matrix, truthful nulls/revisions, retained
membership/enrichment provenance, and rejection-only real artifact handling.
ADR-0079 pins dcaribou's only future sidecar/artifact URLs and strict envelope;
without that sidecar P1-05 is `MetadataUnavailable`, has no probe, resolves no
provider, and produces no observation, receipt, artifact, health, publication,
or API action. It retains seed/LKG with truthful original dates/provenance;
automatic freshness is unavailable. Existing v1/v2 remain unchanged with no v3
relabel, migration, or backfill.

## Publication fence and recovery

For source-backed Published/Unchanged/Reactivated operations, the non-null
guard validates typed cycle/scope/source/lane/community/publication-set identity,
enabled source, `HandoffReady`/bundle digest, `Finalized`/observation digest/
strict receipt prefix, watermark, and prior-cycle selection before head CAS. It
wins over head equality; failures are fatal/mutation-free. A null guard is the
legacy contract. ADR-0080 makes replay receipt-first and limits an absent
receipt to a proved `expected == current == target` `Unchanged` reduction with
no head/document rewrite. A mismatch is fatal even when current equals target;
ordinary atomic C2 `Published`/`Reactivated` remains available only when
expected equals a different current head. Prior selection is verified against
the prior completed cycle's observation, receipt, and
`stalenessReferenceAtUtc`; current freshness is calculated separately.

## Verification and activation

C2 proves exact receipt replay, null round trips, supersession, guard/CAS precedence, crash retry,
legacy compatibility, reason precedence, canonical lane IDs, and guarded
metadata-unchanged prior receipt/health validation, including the direct
Firebase mismatch/current-target, no-rewrite `Unchanged`, ordinary atomic
transition, and prior/current staleness matrix. R1's future test surface
also proves hostile sidecars/URLs, null/type/range/canonical/date defects, and
mixed-generation rejection. C3/E1 prove ADR-0081's literal HTML fixtures,
reconstruction/integer, receipt-completion and family-specific numeric cases;
R1 proves rejection and synthetic takeover. Use incremental then
fresh final review, one heavy family, exact-head CI. Flags remain false. The
owner separately controls live acquisition, development persistence, unattended
HTML reuse, GitHub artifacts/issues, production activation/writes, rollback,
restoration and completion evidence. Synthetic trusted-date fixtures prove
mechanics only. The rejected R1 `b4c9041b323cd55534194fa894b2f3975ac6526a`
and rejected closeout `80b7c6c` are unintegrated evidence only, with no
implementation credit.
