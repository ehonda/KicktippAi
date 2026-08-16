# P0-14 — Make collection profile-driven

- Status: Not started
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md), [P0-12](p0-12-match-context-and-transfer-retirement.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md)

## Outcome

Development and reusable collection orchestration select Kicktipp, Club Elo, and roster collectors from Bundesliga metadata instead of hard-coded WM26 calls.

## Work items

- [ ] Define a competition profile that declares collectors, required match/KPI documents, expected team/match counts, season dates, prompt route, and validation commands.
- [ ] Add the Bundesliga profile: Kicktipp + Club Elo + rosters; home/away/head-to-head enabled; no FIFA, WM26 date map, national lineups, knockout behavior, or transfers.
- [ ] Express the existing WM26 behavior as a separate profile without changing its output contracts.
- [ ] Refactor `CollectContextDevCommand` to execute the resolved profile's collectors in order and report each disposition.
- [ ] Add the accepted `ehonda-dev-buli-2627` community to the supported development configuration from ADR-0005.
- [ ] Ensure dry-run reaches every selected collector without writing.
- [ ] Add profile-resolution, collector-order, skip, failure-short-circuit, and dry-run tests.

## Validation

- Run `CollectContextDevCommandTests` plus all collector command tests.
- Dry-run one Bundesliga and one WM26 profile and compare the collector lists.

## Complete when

- Bundesliga development collection makes no FIFA/WM26 calls.
- Profile output names all required documents and the exact competition before work starts.
