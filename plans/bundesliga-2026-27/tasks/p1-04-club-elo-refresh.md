# P1-04 — Refresh Club Elo during context collection

- Status: Accepted official-HTML contract; dormant implementation
- Depends on: C1 review/integration, C2, C3, then E1
- Decisions: [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)

## Outcome

Official `https://clubelo.com/GER` HTML is the sole future P1-04 candidate,
inside the existing context cycle. Descriptor dispatch permits exactly one
payload (`club-elo/source.html`) and preserves historical CSV evidence without
requiring any future CSV source work. Valid observations require the frozen
HTML transport/header/DOM/lexer/fragment/date/retry/mapping/integer contract;
rejected, not-newer, or displayedDate/ratedAt candidates older than ADR-0013's
seven-calendar-day network-candidate limit retain seed/LKG.

## Work and verification

- [ ] C3 applies ADR-0077's nine shared descriptor/health/Firebase/handoff
  amendments only after C2.
- [ ] E1 implements only the literal HTML source, direct AngleSharp dependency,
  mapping and fixtures frozen by ADR-0077; no fixture/dependency is inferred.
- [ ] Prove descriptor-selected payload isolation, strict Firestore integer
  reconstruction, displayed-date health, hostile fixtures, retries, mapping,
  LKG/no-change, disabled-source zero calls, and publication-v2 reconstruction.
- [ ] Complete fresh incremental/final reviews and exact-head CI.

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
