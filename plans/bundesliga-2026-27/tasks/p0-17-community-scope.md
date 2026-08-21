# P0-17 — Record community and environment scope

- Status: In progress — community matrix complete; credential-loading lane and join evidence pending
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
- [ ] Add community-specific local credential loading for ordinary `verify`, `matchday`, and `bonus` validation so the base dev `.env` does not need to be swapped for arena runs.
- [ ] Verify Kicktipp membership, competition configuration, sibling `.env` routing, and GitHub secret-name availability without exposing values or enabling final production schedules. Owner-confirmed membership/configuration and the names-only profile inventory are recorded; the credential-loader join and P0-20 runtime connectivity evidence remain pending.
- [x] Reserve `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` for the arena test workflow.

## Confirmed prerequisite state (2026-08-16)

- The owner configured `ehonda-dev-buli-2627` and `ehonda-ai-arena` and registered the Luna/none participant in both.
- The arena sibling `.env` and the two model-specific GitHub Actions secrets are updated.
- Existing local and GitHub Actions Firebase, OpenAI, Langfuse, and other shared credentials remain valid from prior WM26 runs.
- Local implementation still needs to load `.env.<community>` for ordinary arena `verify`, `matchday`, and `bonus` commands; current startup behavior uses only the base development `.env` for those paths.
- The connected GitHub token could not list secret names, so P0-20 must verify connectivity without revealing values.

## Validation

- Cross-check the matrix against repository secret names and `ProductionCommunities` behavior in matchday/bonus commands.
- Confirm every selected community has an owner and a safe manual validation path.

## Evidence — 2026-08-21

- [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md) and the [authoritative onboarding table](../../../docs/onboarding-bundesliga-2026-27/community-onboarding.md) record six stable rows, posting-target credential semantics, exact validation identity, production and challenger gates, reuse compatibility, and activation ownership.
- A names-only local inventory found `.env`, `.env.ehonda-ai-arena`, `.env.pes-squad`, `.env.schadensfresse`, and `firebase.json`; no credential values were read. The base `.env` remains the development source and the arena sibling remains exclusive to the Luna participant.
- Existing tracked workflow references confirm the `PES_SQUAD_*` and `SCHADENSFRESSE_*` names. The owner-confirmed Luna pair is reserved. The available GitHub token cannot enumerate repository secret names, so P0-20 must prove connectivity without revealing values.
- Existing Bundesliga 2025/26 and WM26 entrypoints remain `workflow_call`-only and inert. This lane changes no workflow, trigger, schedule, C# code, or external state.
- Final completion and credential-loader test evidence remain for the P0-17 integration join.

## Complete when

- P0-19 can create the Luna/none validation entrypoints immediately and production entrypoints whose final model slots are filled only after P0-06 owner approval.
- Unselected 2025/26 entrypoints are explicitly retired or left inert, not accidentally reactivated.
