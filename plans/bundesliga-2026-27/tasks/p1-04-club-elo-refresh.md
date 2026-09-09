# P1-04 — Refresh Club Elo during context collection

- Status: ADR-0081 published/green; C3 correction-limited at local unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`; implementation incomplete
- Depends on: ADR-0081 published and exact-head green at `f37c9e549e952004c5443f25aab363fda6e2811c` (workflow `34319614680`, all 12 jobs); fresh bounded C3 diagnosis/reslicing, correction and review; E1 remains blocked until corrected C3 is accepted, published, and exact-head green; C2 is reusable common evidence
- Decisions: [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Outcome

Official `https://clubelo.com/GER` HTML is the sole future P1-04 candidate,
inside the existing context cycle. Descriptor dispatch permits exactly one
payload (`club-elo/source.html`) and preserves historical CSV evidence without
requiring any future CSV source work. Valid observations require the frozen
HTML transport/header/DOM/lexer/fragment/date/retry/mapping/integer contract;
rejected, not-newer, or displayedDate/ratedAt candidates older than ADR-0013's
seven-calendar-day network-candidate limit retain seed/LKG.

## Work and verification

- [ ] C3 is correction-limited at local unintegrated
  `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
  diagnosis/reslicing and subsequent correction/review on ADR-0081's exact 13
  shared/publication/receipt paths; implementation remains incomplete.
- [ ] E1 remains blocked until corrected C3 is accepted, published, and
  exact-head green; it then follows ADR-0081's exact 17 literal source, command,
  dependency, mapping and fixture paths; no path is inferred.
- [ ] Prove descriptor-selected payload isolation, strict Firestore integer
  reconstruction, displayed-date health, hostile fixtures, retries, mapping,
  LKG/no-change, disabled-source zero calls, and publication-v2 reconstruction.
- [ ] Complete fresh incremental/final reviews and exact-head CI.

ADR-0081 closes C3/E1 selection, publication and receipt completion without
enabling Club Elo. ADR-0082 defers R1; only accepted E1 may release W1. W1/A1
are outside this objective and not released. A1 later owns E1 attribution only,
while roster attribution waits for future accepted R1.
The retained HTML body is historical specification evidence, not a live
acceptance. Synthetic fixtures prove mechanics only; a real accepting-evidence
recipe is unsupported pending separate specification. Flags remain false.

## Owner gates and completion

Live acquisition, development persistence, unattended HTML reuse, GitHub
artifact/issue work, production writes/activation, rollback delegation,
restoration and completion evidence require distinct owner approval. Completion
requires separately authorized real accepted and later distinct no-change
evidence with all receipts, independent heads, reconciled health/issue state,
copy compatibility, and no model/post change.
