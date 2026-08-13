# P0-13 — Establish competition-aware bonus context

- Status: Not started
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md), [P0-12](p0-12-match-context-and-transfer-retirement.md)

## Outcome

Bundesliga bonus predictions use the new aggregate documents and targeted rosters without inheriting WM26 names or loading every available KPI document.

## Work items

- [ ] Propagate competition into `IKpiContextProvider` calls and repository queries.
- [ ] Define the P0 baseline selection policy: `club-elo-rankings` and `team-squad-summary` for placement/team-strength questions, targeted `roster-*` documents for top-scorer/coach questions, and no transfer documents.
- [ ] Remove broad `fifa-rankings`, `lineups`, and WM26 wording from Bundesliga branches while leaving WM26 behavior competition-scoped.
- [ ] Avoid injecting the full `team-rosters` aggregate into every bonus prompt.
- [ ] Fail with an actionable message when a required aggregate or targeted roster is absent.
- [ ] Add tests by competition and representative German question types.

## Validation

- Run `FirebaseKpiContextProviderTests` and affected bonus-command/prompt tests.
- Inspect reconstructed champion, relegation, top-scorer, and coach prompts for selected document names and size.

## Complete when

- Bundesliga and WM26 context policies cannot leak into one another.
- Every supported P0 question receives useful aggregate/targeted context without transfer documents or all-roster overfetch.
