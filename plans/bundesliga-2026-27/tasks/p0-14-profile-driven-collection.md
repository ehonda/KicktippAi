# P0-14 — Make collection profile-driven

- Status: In review
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md), [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-22](p0-22-history-played-dates.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0032](../decisions/0032-freeze-complete-history-set-and-publish-atomically.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), and [ADR-0034](../decisions/0034-drive-context-collection-from-competition-profiles.md)

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
- [x] Competition-profile/orchestration tests: `17/17` passed in `1.398s`; shared competition resolver tests: `21/21` passed in `0.535s`.
- [x] All affected `CollectContext*` command tests: `81/81` passed in `38.783s`.
- [x] Full `Orchestrator.Tests`: `947/947` passed in `1m 35.323s`.
- [x] `dotnet build KicktippAi.slnx`: succeeded in `43.71s` with `0` errors (existing warnings remain).
- [x] Deterministic dry-run regressions compare the Bundesliga collector list (`Kicktipp`, embedded `BundesligaHistoryPlayedDates`, `ClubElo`, `Rosters`) with the preserved WM26 list (`Kicktipp`, `Wm26HistoryPlayedDates`, `FifaRankings`, `NationalLineups`) and prove no writes.
- [ ] After integration, run the only credentialed live profile dry-run against `ehonda-dev-buli-2627` and record its collector list. WM26 is retired and must remain deterministic regression coverage only; do not contact live WM26 Kicktipp.

No credentialed Kicktipp or Firebase run and no external write occurred in this implementation lane.

## Complete when

- Bundesliga development collection runs its own strict history played-date step and makes no FIFA/WM26 calls.
- Profile output names all required documents and the exact competition before work starts.
