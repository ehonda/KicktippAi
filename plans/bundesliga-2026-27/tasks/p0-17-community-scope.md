# P0-17 — Record community and environment scope

- Status: Complete
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md), [P0-16](p0-16-question-aware-bonus-context.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

The exact development and production communities, context-sharing relationships, fixed plumbing configuration, late production-model slots, and credential owners are recorded before workflows are created.

## Work items

- [x] Record `ehonda-dev-buli-2627` as the safe overwrite-capable development community and `pes-squad`, `schadensfresse`, and `ehonda-ai-arena` as required production communities.
- [x] Record `pes-squad` as the reference production context, `schadensfresse` as independently generated, and the selected production configuration as copy-posted from `pes-squad` into `ehonda-ai-arena`.
- [x] Keep arena challenger configurations self-contained and require exact question/option compatibility before copy-posting bonus predictions.
- [x] Map each community to community context, model-ledger entry, prompt route, secret names, and Langfuse environment.
- [x] Mark final production and challenger model values as the explicit P0-06 owner gate; do not fill them from agent preference or inherit Luna/none.
- [x] Record the complete matrix in an ADR and a small tracked onboarding table.
- [x] Add community-specific local credential loading for ordinary `verify`, `matchday`, and `bonus` validation so the base dev `.env` does not need to be swapped for arena runs.
- [x] Verify Kicktipp membership, competition configuration, sibling `.env` routing, and GitHub secret-name availability without exposing values or enabling final production schedules. Owner-confirmed membership/configuration and the names-only profile inventory are recorded; P0-20 retains the runtime connectivity proof.
- [x] Reserve `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` for the arena test workflow.

## Confirmed prerequisite state (2026-08-16)

- The owner configured `ehonda-dev-buli-2627` and `ehonda-ai-arena` and registered the Luna/none participant in both.
- The arena sibling `.env` and the two model-specific GitHub Actions secrets are updated.
- Existing local and GitHub Actions Firebase, OpenAI, Langfuse, and other shared credentials remain valid from prior WM26 runs.
- Ordinary `verify`, `matchday`, and `bonus` startup loads credentials for the posting target after settings validation and before client construction. Development keeps the base `.env`; arena and production targets use their exact sibling `.env.<community>` files.
- The connected GitHub token could not list repository secret names. The owner confirmed the required names/configuration; P0-20 must still prove runtime connectivity without revealing values.

## Validation

- Cross-check the matrix against repository secret names and `ProductionCommunities` behavior in matchday/bonus commands.
- Confirm every selected community has an owner and a safe manual validation path.

## Evidence — 2026-08-21

- [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md) and the [authoritative onboarding table](../../../docs/onboarding-bundesliga-2026-27/community-onboarding.md) record six stable rows, posting-target credential semantics, exact validation identity, production and challenger gates, reuse compatibility, and activation ownership.
- A names-only local inventory found `.env`, `.env.ehonda-ai-arena`, `.env.pes-squad`, `.env.schadensfresse`, and `firebase.json`; no credential values were read. The base `.env` remains the development source and the arena sibling remains exclusive to the Luna participant.
- Existing tracked workflow references confirm the `PES_SQUAD_*` and `SCHADENSFRESSE_*` names. The owner-confirmed Luna pair is reserved. The available GitHub token cannot enumerate repository secret names, so P0-20 must prove connectivity without revealing values.
- Existing Bundesliga 2025/26 and WM26 entrypoints remain `workflow_call`-only and inert. This lane changes no workflow, trigger, schedule, C# code, or external state.
- The community-matrix lane was independently approved through `584005981eba790bcaab07d4cf2a50c2daa3af6d` and integrated as `99d0cea` plus `9c467d5`. The credential-loading lane was independently approved through `4672a916ee5a21cef197d48629407152dccc1826` and integrated first as `7050202` plus `b457897`, keeping code and policy joins conflict-free.
- The credential loader is singleton-scoped, selects the posting community rather than a reused context community, validates the community slug before any path access, parses sibling files without mutating process-global environment during parsing, applies the username/password pair atomically, and fails closed on missing, incomplete, duplicate, or interpolated values. Tests cover literal dollar handling and prove development wrappers do not load twice.
- Final lane validation passed: sibling parser/path `25/25`, credential command suites `16/16`, full Orchestrator `1022/1022`, and Release build with `0` errors. The integrated-main gate is recorded in the following evidence entry.
- Integrated main at `9c467d52e65beb561b87c6753f5b5b9ddf6c4cb6` passed `dotnet build KicktippAi.slnx --configuration Release --no-restore` with `0` errors and the full Release Orchestrator suite `1022/1022` in `1m33.707s`. Existing warnings, including the tracked `SSH.NET` advisory, remain outside P0-17.
- No live credential values were read or logged, no prediction was made, and no workflow or schedule was enabled. Runtime connectivity remains P0-20; final production model choices and activation remain the P0-06/P0-21 owner gates.

## Complete when

- P0-19 can create the Luna/none validation entrypoints immediately and production entrypoints whose final model slots are filled only after P0-06 owner approval.
- Unselected 2025/26 entrypoints are explicitly retired or left inert, not accidentally reactivated.
