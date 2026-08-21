# P0-19 — Add the self-contained Luna arena workflow triad

- Status: Implementation complete — integration review pending
- Priority: P0
- Depends on: [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

The `arena-luna-self-contained` matrix row has context, matchday, and bonus entrypoints pinned to its accepted Bundesliga 2026/27 plumbing configuration, with manual dispatch only.

## Work items

- [x] Create the `ehonda-ai-arena` context entrypoint with explicit `competition: bundesliga-2026-27` and the profile-driven reusable collector.
- [x] Create the self-contained matchday entrypoint with `community` and `community_context` set to `ehonda-ai-arena` and the exact Luna/none validation ledger identity.
- [x] Create the self-contained bonus entrypoint with the same explicit identity and the accepted `20`-document / `32000`-token context budgets.
- [x] Pin the production-labelled hosted match prompt to `kicktippai/bundesliga-2026-27/predict-one-match` version `2` and the bonus prompt to `kicktippai/bundesliga-2026-27/predict-bonus` version `1`.
- [x] Expose only `workflow_dispatch`, including typed `force_prediction` and `max_repredictions` passthrough on prediction entrypoints; do not add `workflow_call` or `schedule`.
- [x] Wire only the P0-17 Luna arena credential pair and the shared Firebase, OpenAI, and Langfuse contracts. Keep `LANGFUSE_PUBLIC_KEY` as the repository variable consumed by the reusable prediction workflows.
- [x] Leave `MatchdayCommand.ProductionCommunities` and `BonusCommand.ProductionCommunities` unchanged because `ehonda-ai-arena` is already classified as production.
- [x] Leave all live dispatch, write, trace inspection, temporary scheduling, and schedule removal to P0-20.

## Validation

- Parse all three YAML files and compare every caller input against the corresponding reusable workflow declaration.
- Confirm the triad contains neither `workflow_call` nor `schedule`, and that no historical Bundesliga or WM26 competition/prompt identity appears.
- Run the repository prediction-workflow contract after the shared P0-19 contract changes join at integration, and run `git diff --check` in this lane.
- Obtain independent review before integration.

## Evidence — 2026-08-22

- The three entrypoints are manual-only and self-contained. No workflow was dispatched, no schedule was created, and no external state was changed.
- PyYAML parsed all three files. A declaration-aware check confirmed context maps `3` supported inputs and `4` required secrets, matchday maps `13` supported inputs and `6` secrets, and bonus maps `15` supported inputs and `6` secrets, with every required reusable input and secret present.
- Deterministic identity/trigger assertions and `git diff --check` passed. `actionlint` is not installed in this environment; the integrated shared workflow contract provides the repository-specific gate.
- Independent review and the integrated shared contract remain pre-integration gates owned by the orchestrating lane.

## Complete when

- The triad is manually callable, schedule-free, and fully explicit, without deploying an unresolved production model slot.
- P0-20 owns the first context-before-prediction dispatch sequence and its runtime evidence.
