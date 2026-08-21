# P0-15 — Clean the live context-document contract

- Status: Complete
- Priority: P0
- Depends on: [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-13](p0-13-bonus-context-baseline.md), [P0-14](p0-14-profile-driven-collection.md), [P0-22](p0-22-history-played-dates.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0015](../decisions/0015-club-elo-prompt-publication-contract.md), [ADR-0016](../decisions/0016-validate-club-elo-publication-metadata.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0036](../decisions/0036-retire-legacy-team-manager-context.md), [ADR-0037](../decisions/0037-record-immutable-bonus-context-manifests.md)

## Outcome

No 2026/27 match or bonus prediction can receive stale, duplicated, deprecated, old-season, or cross-competition context. Manually stale `team-data` and `manager-data` artifacts are superseded by Club Elo, rosters, and squad summaries before any production prediction.

## Work items

- [x] Inventory every live `team-data` and `manager-data` consumer, field, uploader, and prompt reference.
- [x] Map squad size, average age, and market value to `team-squad-summary`, and manager/team identity to roster coach rows; retire the duplicate manual documents.
- [x] Record in ADR-0036 that no launch-required residual team/manager field remains. Subjective preseason assessment and coach age, country, and tenure are outside the accepted contract.
- [x] Confirm that no derived document or collector is required for a remaining field; do not create one speculatively.
- [x] Route match and bonus context through exact competition/profile document names. Storage presence cannot expand either allowlist, and known community/competition conflicts fail closed.
- [x] Remove the superseded generic KPI uploader, its registration/tests, and `Create-KpiDocument.ps1`; guard generic context upload/copy paths from current-profile and wrong-kind shadow mutations.
- [x] Audit the complete seasonal storage catalog and exact match/bonus selections. Transfer, WM26, 2025/26, deprecated team/manager, wrong-kind, and unexpected profile-looking names cannot enter the 2026/27 live selections.
- [x] Carry the P0-22 exact `Played_At` gate into the profile audit: all 265 completed selected rows resolved from the fixed map, 23 incomplete rows were excluded, and dated head-to-head rows were not rewritten.
- [x] Preserve historical partitions. Add a read-only `context-hygiene inventory` that reports expected/observed identity, hashes, heads, source dates, freshness, and headed/generic conflicts without payloads, deletion, apply, or writes.
- [x] Persist and trace the exact immutable bonus selection manifest, preserve the exact match manifest, and prove stale or unheaded storage cannot silently satisfy a current Bundesliga prediction.
- [x] Add missing, corrupt, divergent, stale, wrong-scope, wrong-kind, cache-coherence, and publication-freshness tests. Bundesliga failures abort placement; WM26 retains deterministic legacy behavior.

## Validation

- Exact integrated Release head: Core `197/197`, Firebase `276/276`, Orchestrator `985/985`, and OpenAI `218/218` passed; `dotnet build KicktippAi.slnx --no-restore -c Release` succeeded with `0` errors. The existing SSH.NET advisory warnings remain.
- Exact pushed main SHA `6901bc7c59b67da2aa05c88e5938d6ec679daa79` passed [Build and Test run 32521429356](https://github.com/ehonda/KicktippAi/actions/runs/32521429356). Every job completed successfully: [Build 96894229701](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894229701), [Define Test Matrix 96894229976](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894229976), [Orchestrator.Tests 96894620138](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620138), [Integration.Tests 96894620155](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620155), [ContextProviders.Kicktipp.Tests 96894620162](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620162), [FirebaseAdapter.Tests 96894620201](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620201), [Core.Tests 96894620238](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620238), [KicktippIntegration.Tests 96894620257](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620257), [OpenAiIntegration.Tests 96894620261](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620261), [TestUtilities.Tests 96894620262](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96894620262), [Merge Coverage Reports 96895041305](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96895041305), and [Deploy GitHub Pages 96895185700](https://github.com/ehonda/KicktippAi/actions/runs/32521429356/job/96895185700).
- Match selection remains the exact P0-12 11-document order. Bonus selection begins with headed `club-elo-rankings` and `team-squad-summary`, then only exact question-selected roster slugs in deterministic order; storage presence does not change either selection.
- Canonical bonus manifests store competition, community, ordered document kind/name/version/content SHA-256, and the roster/Elo publication snapshot IDs without full prompt content. Normal, repredict, cached, verify, and trace paths fail closed on missing, stale, incoherent, or semantically invalid provenance.
- The read-only inventory accounts for all 401 seasonal storage names and reports headed/generic divergence explicitly without printing payloads. No remote document was written or deleted, and no live WM26 endpoint was contacted.
- Independent frozen-commit review found wrong-kind mutation shadows, hidden headed/generic divergence, target-scope conflicts, cached value/metadata incoherence, partial-success placement, and the missing storage ADR. Follow-up commits closed every finding and were independently approved before integration.
- P0-18 must add explicit post-generation `verify-bonus --check-outdated` to the reusable Bundesliga workflow, as required by ADR-0037; P0-15 does not mutate workflow files.

## Complete when

- Each live team/manager fact has one authoritative document and freshness date.
- No prompt receives duplicate stale/manual and roster-derived versions of the same fact.
- A trace-backed allowlist audit accounts for every context document that can enter a live 2026/27 match or bonus prompt.
