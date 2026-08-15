# P0-17 — Record community and environment scope

- Status: Not started
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md), [P0-16](p0-16-question-aware-bonus-context.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md)

## Outcome

The exact development and production communities, context-sharing relationships, fixed plumbing configuration, late production-model slots, and credential owners are recorded before workflows are created.

## Work items

- [ ] Record `ehonda-dev-buli-2627` as the safe overwrite-capable development community and `pes-squad`, `schadensfresse`, and `ehonda-ai-arena` as required production communities.
- [ ] Record `pes-squad` as the reference production context, `schadensfresse` as independently generated, and the selected production configuration as copy-posted from `pes-squad` into `ehonda-ai-arena`.
- [ ] Keep arena challenger configurations self-contained and require exact question/option compatibility before copy-posting bonus predictions.
- [ ] Map each community to community context, model-ledger entry, prompt route, secret names, and Langfuse environment.
- [ ] Mark final production and challenger model values as the explicit P0-06 owner gate; do not fill them from agent preference or inherit Luna/none.
- [ ] Record the complete matrix in an ADR and a small tracked onboarding table.
- [ ] Add community-specific local credential loading for ordinary `verify`, `matchday`, and `bonus` validation so the base dev `.env` does not need to be swapped for arena runs.
- [ ] Verify Kicktipp membership, competition configuration, sibling `.env` routing, and GitHub secret-name availability without exposing values or enabling final production schedules.
- [ ] Reserve `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` for the arena test workflow.

## Confirmed prerequisite state (2026-08-16)

- The owner configured `ehonda-dev-buli-2627` and `ehonda-ai-arena` and registered the Luna/none participant in both.
- The arena sibling `.env` and the two model-specific GitHub Actions secrets are updated.
- Existing local and GitHub Actions Firebase, OpenAI, Langfuse, and other shared credentials remain valid from prior WM26 runs.
- Local implementation still needs to load `.env.<community>` for ordinary arena `verify`, `matchday`, and `bonus` commands; current startup behavior uses only the base development `.env` for those paths.
- The connected GitHub token could not list secret names, so P0-20 must verify connectivity without revealing values.

## Validation

- Cross-check the matrix against repository secret names and `ProductionCommunities` behavior in matchday/bonus commands.
- Confirm every selected community has an owner and a safe manual validation path.

## Complete when

- P0-19 can create the Luna/none validation entrypoints immediately and production entrypoints whose final model slots are filled only after P0-06 owner approval.
- Unselected 2025/26 entrypoints are explicitly retired or left inert, not accidentally reactivated.
