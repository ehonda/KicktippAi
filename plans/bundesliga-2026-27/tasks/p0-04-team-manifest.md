# P0-04 — Create the 18-team join manifest

- Status: Not started
- Priority: P0
- Depends on: None

## Outcome

One checked-in manifest is the join boundary among Kicktipp names, document slugs, official roster sources, Club Elo aliases, and Transfermarkt enrichment IDs.

## Work items

- [ ] Capture the exact team names returned by the selected 2026/27 Kicktipp community.
- [ ] Define and document a CSV schema under `data/bundesliga-2026-27/` with Kicktipp name, canonical slug, official name/source, Club Elo name, and optional Transfermarkt club ID.
- [ ] Populate all 18 clubs, including Elversberg, Schalke, and Paderborn.
- [ ] Replace fallback slugging for this competition with manifest lookup and actionable errors for unknown teams.
- [ ] Add parser/validation tests for row count, unique Kicktipp names, unique slugs, required source fields, and non-empty Club Elo mappings.
- [ ] Generate CSV with deterministic row order, CRLF line endings, no leading blank line, and a final terminator.

## Validation

- Run the new manifest tests and `MatchContextDocumentCatalogTests` in `tests/Core.Tests`.
- Compare the 18 manifest names with a fresh Kicktipp collection result from the chosen development community.

## Complete when

- Every Kicktipp club resolves one-to-one without slug fallback.
- Both roster and Elo work can consume the same typed manifest.
- Source provenance is present for every club.
