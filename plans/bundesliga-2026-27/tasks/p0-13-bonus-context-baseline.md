# P0-13 — Establish competition-aware bonus context

- Status: Complete
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md), [P0-12](p0-12-match-context-and-transfer-retirement.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0014](../decisions/0014-share-atomic-context-kpi-publication.md), [ADR-0015](../decisions/0015-club-elo-prompt-publication-contract.md), [ADR-0024](../decisions/0024-select-bonus-context-by-competition-and-question.md)

## Outcome

Bundesliga bonus predictions use the new aggregate documents and targeted rosters without inheriting WM26 names or loading every available KPI document.

## Work items

- [x] Propagate competition into `IKpiContextProvider` calls and repository queries.
- [x] Define the P0 baseline selection policy: `club-elo-rankings` and `team-squad-summary` for placement/team-strength questions, targeted `roster-*` documents for top-scorer/coach questions, and no transfer documents.
- [x] Remove broad `fifa-rankings`, `lineups`, and WM26 wording from Bundesliga branches while leaving WM26 behavior competition-scoped.
- [x] Avoid injecting the full `team-rosters` aggregate into every bonus prompt.
- [x] Fail with an actionable message when a required aggregate or targeted roster is absent.
- [x] Add tests by competition and representative German question types.

## Validation

- Run `FirebaseKpiContextProviderTests` and affected bonus-command/prompt tests.
- Inspect reconstructed champion, relegation, top-scorer, and coach prompts for selected document names and size.

## Validation evidence

- 2026-08-21: [ADR-0024](../decisions/0024-select-bonus-context-by-competition-and-question.md) fixes the competition split and exact order. The Bundesliga provider resolves `club-elo-rankings` and `team-squad-summary` through their current ADR-0014 publication heads, runs the strict Club Elo and roster semantic reconstruction boundaries, and then appends only exact manifest/member-targeted `roster-{slug}` documents. It never calls generic KPI enumeration for Bundesliga. The existing WM26 `fifa-rankings` plus exact top-scorer-team `lineups` branch remains isolated.
- 2026-08-21: Focused Core `BonusContextSelectionPolicyTests` passed 7/7. The cases cover champion and relegation baseline order, no roster overfetch for placement questions, manifest-targeted top-scorer teams, exact player/coach option mapping, deterministic roster ordering, and fail-closed unmapped roster questions.
- 2026-08-21: Focused `FirebaseKpiContextProviderTests` passed 29/29. Bundesliga cases prove both headed publication reads, no generic latest KPI read, exact baseline/roster names, absence of `team-rosters`, FIFA, lineups, and manager data, actionable missing-publication and unmapped-target failures, rejection of text-only Bundesliga selection, and a competition-bound WM26 regression.
- 2026-08-21: Focused Orchestrator validation passed: `BonusCommand*` 77/77 and `FactoryTests` 5/5. The command passes the complete `BonusQuestion`, including options, into selection; the factory canonicalizes competition, injects a publication repository only for Bundesliga, and leaves WM26 off that boundary.
- 2026-08-21: Broader validation passed: Core 138/138, FirebaseAdapter 259/259 (including Docker-backed emulator cases), Orchestrator 895/895, and OpenAiIntegration 212/212. `Integration.Tests.csproj` also restored and built with zero errors, proving the expanded provider contract compiles through the integration composition.
- 2026-08-21: P0-13 intentionally does not change bonus prediction metadata or Verify/reprediction freshness semantics. Those paths still retain document names and query the generic KPI repository, so P0-15 must include them in its owned freshness, missing-data, trace-backed catalog, and live-exclusion audit before launch. P0-13's live generation selection itself is headed, coherent, and fail closed.

## Complete when

- Bundesliga and WM26 context policies cannot leak into one another.
- Every supported P0 question receives useful aggregate/targeted context without transfer documents or all-roster overfetch.
