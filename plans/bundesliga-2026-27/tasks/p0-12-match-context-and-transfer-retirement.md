# P0-12 — Replace transfer context in the match contract

- Status: Not started
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md)
- Decision: [ADR-0002](../decisions/0002-supersede-transfer-documents.md)

## Outcome

Every 2026/27 match requires the two roster and two Club Elo documents and no live path selects or uploads transfer documents.

## Work items

- [ ] Extend `MatchContextDocumentCatalog` with manifest-backed `roster-{slug}.csv` and `club-elo-{slug}.csv` names for both teams.
- [ ] Keep standings, community rules, recent history, home/away history, and head-to-head history required for Bundesliga.
- [ ] Remove `IncludeTransfers`, transfer optional-document selection, and transfer-specific retrieval branches from matchday, random-match, prompt reconstruction, experiment export/execution, and match-analysis helpers.
- [ ] Remove the `upload-transfers` command registration, implementation, tests, and current documentation because it no longer serves a live context contract.
- [ ] Simplify `MatchContextDocumentSelection` if optional documents no longer have another supported use.
- [ ] Replace transfer-focused tests with assertions that both Elo and roster documents are mandatory and missing required context fails clearly.
- [ ] Leave historical Firestore documents untouched; do not add a deletion migration.

## Validation

- Run `MatchContextDocumentCatalogTests`, matchday/random-match context retrieval tests, prompt reconstruction tests, and affected experiment/analyze-match tests.
- Search live `src`, `tests`, `.github`, and non-archive docs for transfer-document names and account for every remaining result.

## Complete when

- A match trace has standings, rules, histories, two Elo rows, and two roster documents.
- No live command asks for `*-transfers.csv` and no upload-transfers command is exposed.
- Future historical experiments remain possible only by explicitly assembling their own context.
