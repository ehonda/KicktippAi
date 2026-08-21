# ADR-0035: Freeze the first live DFB-Pokal history completion

- Status: Accepted
- Date: 2026-08-21

## Context

The complete preseason history map in ADR-0032 was verified at 17:37 Europe/Berlin, before the 18:00 DFB-Pokal fixture between SC St. Tönis and Eintracht Frankfurt was played. It correctly excluded that then-incomplete row under ADR-0026. An authenticated, read-only Bundesliga profile dry-run after the match failed closed because Kicktipp had prepended the completed `DFB | SC St. Tönis | Eintracht Frankfurt | 0:11` row to both `away-history-sge.csv` and `recent-history-sge.csv`, while the fixed external-source map still began with the preseason identities.

OpenLigaDB's public `dfb/2026` endpoint now provides stable match ID `81832`, an exact local and UTC timestamp, the two team identities, and halftime plus full-time results. The endpoint is live: the response captured at 20:13 Europe/Berlin contained 32 unique 2026/27 fixtures, two completed matches, and 30 incomplete fixtures. Its other completed match, ID `81848` between Preußen Münster and Karlsruher SC, belongs to neither an exact Bundesliga 2026/27 manifest team nor the selected SGE history update.

## Decision

Freeze the exact 30,666-byte response from `https://api.openligadb.de/getmatchdata/dfb/2026` as `data/bundesliga-2026-27/history/sources/openligadb-dfb-2026.json`. Its immutable source revision is SHA-256 `b60d4c1ef214ffa2680efb27cace33cc7b47bf9700b4f57e7043736919a8eeab`. Runtime never fetches this endpoint.

Snapshot validation requires exactly 32 positive, unique match IDs, `dfb` league shortcut and season `2026` on every record, exact consistent local/UTC datetimes and nonblank teams, and the exact completed-ID set `{81832, 81848}` at capture. Every completed match must have exactly one full-time result. Results attached to an incomplete fixture remain non-evidence and are not eligible for a map row. Match `81832` must additionally be exactly SC St. Tönis at home against Eintracht Frankfurt, played `2026-08-21T18:00:00` Europe/Berlin, with halftime `0:10` and full-time `0:11`.

Only match `81832` contributes map rows. In the updated frozen audit ledger it occurs exactly once at completed-row ordinal 1 in each of `away-history-sge.csv` and `recent-history-sge.csv`, with played date `2026-08-21` and the captured response's exact URL, hash revision, and match ID. Both rows use `Verified_At=2026-08-21T20:14:00+02:00`, after the frozen source's completed-result update at 19:57:23 and after the response capture. The seven prior identities in each document shift intact from audit ordinals 1-7 to 2-8. Match `81848` remains frozen source context but contributes no map row because it is outside the exact 18-team selected-history inventory. Any missing occurrence, additional occurrence, document mismatch, identity mismatch, source mismatch, or disagreement fails closed.

The frozen ordinal remains provenance for the observed inventory but is not a permanent runtime join key. Kicktipp history is a bounded rolling window: newly completed rows prepend and older rows eventually leave. Runtime fixed-map lookup therefore uses the exact, case-sensitive unique tuple `(DocumentName, HistoryCompetition, HomeTeam, AwayTeam, normalized Score, Annotation)`. Current-season `1.BL` rows continue to resolve from exact competition-scoped outcomes; when both an outcome and an exact fixed-map identity exist, their dates must agree. Duplicate live tuples, multiple fixed-map candidates, conflicting dates, and any currently selected completed row without an exact source fail closed. A reviewed ledger entry that has rolled out of the current selected document is not itself an error. The explicit expected 54-document set, complete in-memory audit, no-write failure behavior, and atomic publication gate remain unchanged.

The existing 2025/26 OpenLigaDB DFB-Pokal final contract remains separate and unchanged: match `81581` must still occur exactly once in each of its four accepted documents. The new capture and its two derived rows carry the existing OpenLigaDB ODbL 1.0 attribution boundary; application and validation code remains MIT-licensed.

## Alternatives considered

- **Resolve the new DFB row from Kicktipp outcomes:** Rejected because ADR-0025 permits that source only for exact completed `1.BL` rows; non-league rows require accepted fixed external evidence.
- **Drop the newly completed row or retain the old audit ordinals:** Dropping the row was rejected because live Kicktipp now selects an exact non-league identity that otherwise remains unresolved. Keeping the old audit ordinals was rejected because the canonical ledger would no longer reproduce the observed inventory, even though runtime now joins the unchanged suffix by identity.
- **Fetch OpenLigaDB at runtime:** Rejected because a live endpoint can change during the cup round and would violate the deterministic last-known-good contract.
- **Add match 81848 to the map:** Rejected because neither team belongs to the exact Bundesliga manifest and the match is absent from this selected-history transition.
- **Keep joining permanently by document ordinal:** Rejected because every prepend shifts otherwise unchanged identities and every bounded-window eviction leaves an accepted ledger row unused; ordinal remains useful audit provenance but cannot be stable runtime identity.

## Consequences

- The canonical map advances from 398 to 400 rows and from 196 to 197 unique source matches. OpenLigaDB coverage advances from 70 rows / 43 matches to 72 rows / 44 matches; other source counts remain unchanged.
- The authenticated fail-closed dry-run is preserved as evidence that source drift cannot publish guessed dates or a partial history set.
- Later completed external fixtures require their own observed selected-document inventory and reviewed frozen-source update; presence in this broader snapshot alone is not permission to add a map row.
- Normal rolling-window movement no longer forces accepted suffix rows to be renumbered merely to resolve at runtime, while a genuinely new non-league identity still stops collection until its source is frozen.

## Affected tasks

- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0025 only where its canonical runtime identity includes the current row ordinal, and ADR-0032 where it fixes the live map at the preseason 398-row / 196-match counts or implies that every accepted ledger row must remain present in each rolling provider window. Frozen ordinals remain mandatory audit provenance. ADR-0032's 54-document completeness, contiguous ledger ordinal, deterministic-byte, exact current-row resolution, fail-closed, and atomic-publication decisions remain in force.
