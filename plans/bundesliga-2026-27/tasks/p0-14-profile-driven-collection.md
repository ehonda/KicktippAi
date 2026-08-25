# P0-14 — Make collection profile-driven

- Status: Complete
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md), [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-22](p0-22-history-played-dates.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0032](../decisions/0032-freeze-complete-history-set-and-publish-atomically.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0034](../decisions/0034-drive-context-collection-from-competition-profiles.md), and [ADR-0044](../decisions/0044-select-canonical-preseason-history-sources.md)

## Outcome

Development and reusable collection orchestration select Kicktipp, Club Elo, and roster collectors from Bundesliga metadata instead of hard-coded WM26 calls.

## Work items

- [x] Define a competition profile that declares collectors, required match/KPI documents, expected team/match counts, season dates, prompt route, and validation commands.
- [x] Add the Bundesliga profile in dependency order: Kicktipp, Bundesliga history played-date reconstruction, Club Elo, and rosters; home/away/head-to-head enabled; no FIFA, WM26 date map, national lineups, knockout behavior, or transfers.
- [x] Express the existing WM26 behavior as a separate profile without changing its output contracts.
- [x] Refactor `CollectContextDevCommand` to execute the resolved profile's collectors in order and report each disposition.
- [x] Add the accepted `ehonda-dev-buli-2627` community to the supported development configuration from ADR-0005.
- [x] Ensure dry-run reaches every selected collector without writing.
- [x] Add profile-resolution, collector-order, skip, failure-short-circuit, and dry-run tests.

## Validation

- [x] `CollectContextDevCommandTests`: `4/4` passed in `1.809s`, including real-executor WM26 preservation and a real-executor Bundesliga composite dry-run whose context/outcome/publication writes throw if reached and whose inactive WM sources throw if resolved.
- [x] Competition-profile/orchestration tests: `18/18` passed in `2.663s`; shared competition resolver tests: `21/21` passed in `0.535s`.
- [x] All affected `CollectContext*` command tests: `81/81` passed in `38.783s`.
- [x] Full `Orchestrator.Tests`: `948/948` passed in `1m 23.565s`.
- [x] `dotnet build KicktippAi.slnx --no-restore`: succeeded in `16.25s` with `0` errors (existing warnings remain).
- [x] Deterministic dry-run regressions compare the Bundesliga collector list (`Kicktipp`, embedded `BundesligaHistoryPlayedDates`, `ClubElo`, `Rosters`) with the preserved WM26 list (`Kicktipp`, `Wm26HistoryPlayedDates`, `FifaRankings`, `NationalLineups`) and prove no writes.
- [x] On `2026-08-21`, an authenticated read-only fetch of [the `ehonda-dev-buli-2627` Kicktipp rules](https://www.kicktipp.de/ehonda-dev-buli-2627/spielregeln) verified win points `2/3/4` (tendency/goal difference/exact), draw points `2/4` (tendency/exact), and `4` points for a correct bonus answer. The normal-match contract is frozen in `community-rules/ehonda-dev-buli-2627.md` with SHA-256 `e52945f0d63e9a332ee225d4a9fd60677b761771dac0ac6cc8d7957143252292`, exactly matching `pes-squad.md` and `ehonda-ai-arena.md`, not `ehonda-test-buli.md`. Its narrow Git attribute stores normalized LF in the index and reconstructs all `43` line terminators as CRLF, including the final terminator; a fresh index checkout reproduced the same SHA-256.
- [x] The first credentialed Bundesliga profile dry-run authenticated and fetched exactly nine fixtures, then failed only because `community-rules/ehonda-dev-buli-2627.md` was not yet tracked. Dry-run prevented writes; this follow-up supplies that missing required profile document.
- [x] Installing the original-repository locator exposed the real base Firebase environment to the full test process, so three environment-helper cases initially short-circuited their synthetic file paths (`945/948`, while the class passed `9/9` alone). The affected tests now explicitly clear inherited project/service-account variables after saving them for restoration; the class passes `9/9` in `0.920s` and the subsequent default full suite passes `948/948`.
- [x] After integration, the only credentialed live profile dry-run targeted `ehonda-dev-buli-2627` and `bundesliga-2026-27`, authenticated successfully, fetched exactly the nine pending matchday-one fixtures and 18 teams, and resolved all 47 required context documents. Its ordered collector dispositions were `Kicktipp` (`DryRunValidated`), embedded `BundesligaHistoryPlayedDates` (`IncludedInPreviousDryRun`), `ClubElo` (`DryRunValidated`, launch seed, network disabled), and `Rosters` (`DryRunValidated`, checked-in fallback seed). The history gate resolved all 265 completed rows from the fixed map and excluded 23 incomplete rows. Every selected collector completed without a database write. WM26 remained deterministic regression coverage only and no live WM26 endpoint was contacted.
- [x] ADR-0044 refines the explicit Bundesliga full-season profile path: validate the complete 34-by-9 schedule first, collect canonical 54 selected histories plus 306 exact-matchday H2Hs, and keep downstream profile collectors behind the complete Kicktipp gate. Ordinary and WM26 profile behavior is unchanged.

No Firestore, context, outcome, or prediction write occurred. Established Langfuse OTLP telemetry for the dry run was exported successfully. The implementation agent did not handle credentials; the authenticated read-only evidence above was supplied by the root orchestrator.

## Complete when

- Bundesliga development collection runs its own strict history played-date step and makes no FIFA/WM26 calls.
- Profile output names all required documents and the exact competition before work starts.
