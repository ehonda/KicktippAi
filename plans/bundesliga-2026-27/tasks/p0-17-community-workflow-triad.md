# P0-17 — Add one community workflow triad

- Status: Template — copy once per selected community
- Priority: P0
- Depends on: [P0-15](p0-15-community-scope.md), [P0-16](p0-16-base-workflow-support.md)
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

One selected community has context, matchday, and bonus entrypoints pinned to its accepted 2026/27 configuration, with manual dispatch only.

## Before implementation

- Copy this file to `p0-17-<community>-workflow-triad.md`.
- Replace every reference to “the community” with the exact P0-15 matrix row.
- Add the copied task to the P0 table in `README.md`; leave this template uncompleted.

## Work items

- [ ] Create/update the context entrypoint with explicit `competition: bundesliga-2026-27` and the accepted context profile.
- [ ] Create/update the matchday entrypoint with exact community, community context, model, reasoning, output cap, and prompt identity.
- [ ] Create/update the bonus entrypoint with the same explicit identity.
- [ ] Expose `workflow_dispatch` only; leave every `schedule` commented out until P0-19.
- [ ] Wire only the secrets assigned in P0-15 and update `MatchdayCommand.ProductionCommunities` / `BonusCommand.ProductionCommunities` when appropriate.
- [ ] Remove or clearly retire the superseded 2025/26 entrypoint for this community so two similarly named live files cannot diverge.
- [ ] Add/update telemetry environment tests for the community.

## Validation

- Validate the three YAML files and inspect each reusable-workflow input.
- Manually dispatch only after P0-18 authorizes the development/production validation step.

## Complete when

- The triad is manually callable, schedule-free, and fully explicit.
- No workflow input can resolve to 2025/26 or request transfer documents.
