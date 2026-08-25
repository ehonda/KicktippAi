# P0-19 — Add one community workflow triad

- Status: Template — copy once per selected community
- Priority: P0
- Depends on: [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

One selected community has context, matchday, and bonus entrypoints pinned to its accepted 2026/27 configuration, with manual dispatch only.

## Before implementation

- Copy this file to `p0-19-<community>-workflow-triad.md`.
- Replace every reference to “the community” with the exact P0-17 matrix row.
- Add the copied task to the P0 table in `README.md`; leave this template uncompleted.
- For a final production entrypoint, wait for P0-06 owner approval before replacing its model/configuration slot, and require [P0-24](p0-24-bonus-copy-post-compatibility.md) before enabling bonus copy-post behavior. Luna/none validation entrypoints may proceed earlier.

## Work items

- [ ] Create/update the context entrypoint with explicit `competition: bundesliga-2026-27` and the accepted context profile.
- [ ] Create/update the matchday entrypoint with exact community, community context, approved ledger identity, reasoning, output cap, and prompt identity.
- [ ] Create/update the bonus entrypoint with the same explicit identity.
- [ ] Expose `workflow_dispatch` only; leave every final production `schedule` commented out until P0-21. The accepted Luna/none arena validation workflow may use its separately authorized temporary schedule.
- [ ] Wire only the secrets assigned in P0-17 and update `MatchdayCommand.ProductionCommunities` / `BonusCommand.ProductionCommunities` when appropriate.
- [ ] Remove or clearly retire the superseded 2025/26 entrypoint for this community so two similarly named live files cannot diverge.
- [ ] Add/update telemetry environment tests for the community.

## Validation

- Validate the three YAML files and inspect each reusable-workflow input.
- Dispatch Luna/none validation only within P0-20; dispatch final production entrypoints only within P0-21.

## Complete when

- The triad is manually callable, schedule-free, and fully explicit; no unresolved model slot is deployed.
- No workflow input can resolve to 2025/26 or request transfer documents.
