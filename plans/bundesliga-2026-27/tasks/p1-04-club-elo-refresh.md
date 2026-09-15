# P1-04 — Refresh Club Elo during context collection

- Status: Complete — dormant implementation only; operational/live validation and activation remain separate owner gates
- Last reconciled: 2026-09-15
- Depends on: C3 and E1 accepted, pushed to draft PR #111, and exact-head CI green; C2 remains reusable common evidence
- Decisions: [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Outcome

Official `https://clubelo.com/GER` HTML is the implemented dormant P1-04 candidate,
inside the existing context cycle. Descriptor dispatch permits exactly one
payload (`club-elo/source.html`) and preserves historical CSV evidence without
requiring any future CSV source work. Valid observations require the frozen
HTML transport/header/DOM/lexer/fragment/date/retry/mapping/integer contract;
rejected, not-newer, or displayedDate/ratedAt candidates older than ADR-0013's
seven-calendar-day network-candidate limit retain seed/LKG.

## Work and verification

- [x] C3 shared selection, publication and receipt-completion corrections accepted.
- [x] E1 source, command, dependency, mapping and fixture implementation accepted.
- [x] Descriptor-selected payload isolation, strict Firestore integer
  reconstruction, displayed-date health, hostile fixtures, retries, mapping,
  LKG/no-change, disabled-source zero calls and publication-v2 reconstruction verified.
- [x] Fresh reviews, cumulative local validation and exact-head CI completed.

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

ADR-0081 closes C3/E1 selection, publication and receipt completion without
enabling Club Elo. ADR-0082 defers R1. Accepted E1 satisfies W1's implementation prerequisite; W1/A1
are outside this objective and not released. A1 later owns E1 attribution only,
while roster attribution waits for future accepted R1.
The retained HTML body is historical specification evidence, not a live
acceptance. Synthetic fixtures prove mechanics only; a real accepting-evidence
recipe is unsupported pending separate specification. Flags remain false.

## Separate operational/live validation and activation gates

Live acquisition, development persistence, unattended HTML reuse, GitHub
artifact/issue work, production writes/activation, rollback delegation,
restoration and operational completion evidence require distinct owner approval. Operational completion
requires separately authorized real accepted and later distinct no-change
evidence with all receipts, independent heads, reconciled health/issue state,
copy compatibility, and no model/post change.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.
