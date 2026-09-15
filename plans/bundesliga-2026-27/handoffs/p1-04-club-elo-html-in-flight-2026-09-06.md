# P1-04 official Club Elo HTML closed handoff

- Status: Closed — superseded by accepted C3/E1 dormant implementation results; no implementation work remains in this handoff
- Date reconciled: 2026-09-15
- Task: [P1-04](../tasks/p1-04-club-elo-refresh.md)
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Recorded closeout

P1-04 dormant implementation is complete.

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

The [task](../tasks/p1-04-club-elo-refresh.md) and
[status snapshot](../p1-status-snapshot.md) supersede this handoff's former
in-flight instructions. The official HTML contract and dormant implementation
provide no unattended-reuse or activation authority. P1-05 remains
HTML-independent and deferred under ADR-0082. Accepted E1 satisfies W1's
implementation prerequisite, but W1/A1 remain outside this objective and
unreleased. Future A1 owns E1 attribution only; roster attribution waits for
future accepted R1.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.

## Historical source evidence

Two official captures eleven minutes apart were byte-identical: displayed date
`2026-09-04`, body length `562,238`, SHA-256
`a342b6f83dadbb49599c0fe6364ea3288f0953381d5b35e923eb87a03296aa59`,
`text/html; charset=utf-8`, no content coding and no BOM. The date is bound by
the exact heading link `/2026-09-04/GER` with text `Germany`; the page contains
the expected 18 manifest clubs with current ranks/Elo. The former proposed
475-byte UTF-8/NFC mapping, SHA-256
`ee14dfc556c03157ca215eb3c3161d2119ea7b59d8666084915376dde5213772`, is now
superseded non-normative history; ADR-0077 freezes the verified 487-byte
no-final-terminator mapping. Ignored local evidence, if present,
is `.tmp/p1-04-evidence-01a07449/REPORT.md` and
`.tmp/p1-04-evidence-01a07449/clubelo-GER.html`.

The direct-CSV investigation remains negative historical evidence: current and
historical HTTP requests returned empty `502` responses, HTTPS timed out, and
public archives/caches produced no date-bound raw CSV. Do not repeat the broad
search unless the provider surface changes. None of this evidence is a live
read, accepting refresh, persistence, or completion result.

## Frozen contract

Use only `club-elo/source.html` for the HTML descriptor; no CSV cross-path or
extra bundle file. Preserve exact response/header/DOM/lexer/fragment/date/retry/
route-name mapping/null/hostile-fixture/integer rules and final `CLUB_ELO_*`
diagnostics in ADR-0077. Health uses displayed date. Firebase reconstructs the
nested response and rejects doubles for every integer. Publication-v2 provenance
is self-contained. Source flags remain false and disabled mode has zero source,
cycle, persistence, issue, or API work.

## Separate future operational gates

Fresh live evidence, development persistence, unattended HTML reuse, GitHub
artifact/issues, production writes/activation, rollback owner, restoration and
completion evidence each require separate owner authorization. Synthetic
fixtures prove mechanics only; the real accepting-evidence recipe is still
unsupported until specified separately.
