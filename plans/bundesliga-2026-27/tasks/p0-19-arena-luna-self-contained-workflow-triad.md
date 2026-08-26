# P0-19 — Add the self-contained Luna arena workflow triad

- Status: Complete
- Priority: P0
- Depends on: [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

The `arena-luna-self-contained` matrix row has context, matchday, and bonus entrypoints pinned to its accepted Bundesliga 2026/27 plumbing configuration, with manual dispatch only.

## Work items

- [x] Create the `ehonda-ai-arena` context entrypoint with explicit `competition: bundesliga-2026-27` and the profile-driven reusable collector.
- [x] Create the self-contained matchday entrypoint with `community` and `community_context` set to `ehonda-ai-arena` and the exact Luna/none validation ledger identity.
- [x] Create the self-contained bonus entrypoint with the same explicit identity and the accepted `20`-document / `32000`-token context budgets.
- [x] Pin the production-labelled hosted match prompt to `kicktippai/bundesliga-2026-27/predict-one-match` version `3` and the bonus prompt to `kicktippai/bundesliga-2026-27/predict-bonus` version `1`; retain historical P0-20/P0-25 evidence on immutable v2.
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
- Lane A was committed and pushed as `20ebe78e47f935caee2e178a14408ede9990527e`. Independent review approved its complete diff with no findings: the three callers are manual-only, use exact supported inputs and secrets, contain no shell interpolation surface, and introduce no WM26, Bundesliga 2025/26, or scheduled trigger.
- Lane B was committed and pushed as `ceef1889ffa271421176b8b6103e52e8fb6ea23d` and integrated as `2336cfa`. Its independent review found two fail-open cases in the manual-only trigger regression gate: arbitrary additional events and quoted/spaced YAML event keys. Both were remediated with a raw-shape singleton check plus synthetic mutations; final re-review approved the lane with no findings.
- The corrected focused Release telemetry tests passed `2/2` in `2.605s`, proving the exact arena/Bundesliga Luna identity retains Langfuse environment `production` solely because of the posting target classification.
- Integrated main at `2336cfa2106348366caea7965d32498b20e54a4a` passed the Release solution build with `0` errors, the full Release Orchestrator suite `1033/1033` in `148.225s`, the prediction workflow contract (`2` bases, `14` callable WM26 callers, `12` retired Bundesliga 2025/26 callers, `2` current Bundesliga arena callers), parsing of all `39` workflow YAML files, and `git diff --check`.
- No workflow was dispatched, no schedule was enabled, no credential value was printed, and no prediction or external write occurred during P0-19. P0-20 retains all live Luna validation ownership.

## Complete when

- The triad is manually callable, schedule-free, and fully explicit, without deploying an unresolved production model slot.
- P0-20 owns the first context-before-prediction dispatch sequence and its runtime evidence.

## Challenger continuation — 2026-08-27

ADR-0052 additionally admits this exact Luna/`none` / cap-`10000` row as a
cheap arena challenger. The Owner confirmed its existing Actions pair remains
provisioned. Future dispatch and any schedule remain P0-21; the prior plumbing
ladder does not silently activate it.
