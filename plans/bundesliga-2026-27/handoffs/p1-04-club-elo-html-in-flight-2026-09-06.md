# P1-04 official Club Elo HTML in-flight handoff

- Status: ADR-0077 accepted; dormant C3/E1 only
- Date reconciled: 2026-09-07
- Task: [P1-04](../tasks/p1-04-club-elo-refresh.md)
- Authority: ADR-0074, [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), and [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)

## Resume boundary

The Owner-selected official HTML contract is accepted, but selection is not
unattended-reuse or activation authority. First complete fresh C1 review of
`c99e163..852d179`, content-integrate accepted 20-path bytes onto current main,
and implement/accept C2. Then C3 owns ADR-0077's exact nine shared surfaces;
only after C3 may E1 own the literal source/dependency/mapping/fixture surface.
P1-05 remains independent of HTML.

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
