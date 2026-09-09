# P1-04 official Club Elo HTML in-flight handoff

- Status: ADR-0081 published/green; C3 correction-limited at local unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`; E1 blocked
- Date reconciled: 2026-09-09
- Task: [P1-04](../tasks/p1-04-club-elo-refresh.md)
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Resume boundary

The Owner-selected official HTML contract is accepted, but selection is not
unattended-reuse or activation authority. ADR-0081 is published and exact-head
green at `f37c9e549e952004c5443f25aab363fda6e2811c` (workflow `34319614680`,
all 12 jobs). C3 is correction-limited at local unintegrated
`d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
diagnosis/reslicing and subsequent correction/review on its exact 13
shared/publication/receipt paths. E1's exact 17 source/command/dependency/
mapping/fixture paths remain blocked until corrected C3 is accepted, published,
and exact-head green. P1-05 remains independent of HTML and deferred under
[ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md).
Only accepted E1 may release W1; W1/A1 are outside this objective and not
released. A1 later owns E1 attribution only, while roster attribution waits for
future accepted R1.

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

## Gates

Fresh live evidence, development persistence, unattended HTML reuse, GitHub
artifact/issues, production writes/activation, rollback owner, restoration and
completion evidence each require separate owner authorization. Synthetic
fixtures prove mechanics only; the real accepting-evidence recipe is still
unsupported until specified separately.
