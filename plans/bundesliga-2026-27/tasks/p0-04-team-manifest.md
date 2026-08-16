# P0-04 — Create the 18-team join manifest

- Status: Complete
- Priority: P0
- Depends on: None
- Decision: [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md)

## Outcome

One checked-in manifest is the join boundary among Kicktipp names, document slugs, official roster sources, Club Elo aliases, and Transfermarkt enrichment IDs.

## Work items

- [x] Capture the exact team names returned by the selected 2026/27 Kicktipp community.
- [x] Define and document a CSV schema under `data/bundesliga-2026-27/` with Kicktipp name, canonical slug, official name/source, Club Elo name, and optional Transfermarkt club ID.
- [x] Populate all 18 clubs, including Elversberg, Schalke, and Paderborn.
- [x] Replace fallback slugging for this competition with manifest lookup and actionable errors for unknown teams.
- [x] Add parser/validation tests for row count, unique Kicktipp names, unique slugs, required source fields, and non-empty Club Elo mappings.
- [x] Generate CSV with deterministic row order, CRLF line endings, no leading blank line, and a final terminator.

## Validation

- Run the new manifest tests and `MatchContextDocumentCatalogTests` in `tests/Core.Tests`.
- Compare the 18 manifest names with a fresh Kicktipp collection result from the chosen development community.

## Validation evidence

- On 2026-08-16, `dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --matchdays 1 --dry-run` authenticated with the base development credential file and returned nine fixtures containing 18 unique team names. The exact set matched the checked-in manifest. The dry run made no remote writes; missing community-rules warnings did not affect fixture or standings collection.
- The official [Bundesliga club overview](https://www.bundesliga.com/de/bundesliga/clubs?firsttab=kader) and every linked club page were checked on 2026-08-16 for the 18 official names and roster-source URLs.
- The [Club Elo Germany ranking](https://clubelo.com/GER), source-dated 2026-08-14, and all 18 linked club routes were checked on 2026-08-16 for the manifest aliases. The dated CSV endpoint timed out in two bounded read-only attempts, so the captured-response assertion remains explicitly owned by P0-10; no network refresh was enabled.
- `dotnet run --project tests/Core.Tests -- --treenode-filter '/*/*/BundesligaTeamManifestTests/*'`: 10 passed.
- `dotnet run --project tests/Core.Tests -- --treenode-filter '/*/*/MatchContextDocumentCatalogTests/*'`: 7 passed.
- `dotnet run --project tests/ContextProviders.Kicktipp.Tests`: 44 passed.
- `dotnet run --project tests/Orchestrator.Tests -- --treenode-filter '/*/*/MatchdayCommand_ContextRetrieval_Tests/*'`: 23 passed.
- `dotnet run --project tests/Orchestrator.Tests -- --treenode-filter '/*/*/AnalyzeMatch*Command_Output_Tests/*'`: 33 passed after its duplicate mapping was removed.

## Complete when

- Every Kicktipp club resolves one-to-one without slug fallback.
- Both roster and Elo work can consume the same typed manifest.
- Source provenance is present for every club.
