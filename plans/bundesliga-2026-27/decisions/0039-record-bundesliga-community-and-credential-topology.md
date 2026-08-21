# ADR-0039: Record Bundesliga community and credential topology

- Status: Accepted
- Date: 2026-08-21

## Context

Bundesliga 2026/27 needs one safe development target, three production posting targets, a reproducible plumbing configuration, and late-bound production configurations. The posting target and the context owner are not always the same: the selected production participant in `ehonda-ai-arena` reuses predictions stored for `pes-squad`, while arena challengers remain self-contained. Credentials therefore cannot be selected from `community_context` without risking a write through the wrong Kicktipp participant.

The repository already pins the Luna/none validation identity in ADR-0033 and reserves final production and challenger choices for the owner. Existing Bundesliga 2025/26 and WM26 entrypoint files are inert historical material; their model values and schedules must not become 2026/27 defaults.

## Decision

The authoritative six-row community matrix is tracked in [the Bundesliga community onboarding document](../../../docs/onboarding-bundesliga-2026-27/community-onboarding.md). Its stable rows are:

1. `dev-luna` — self-contained validation in `ehonda-dev-buli-2627`;
2. `arena-luna-self-contained` — self-contained Luna/none validation in `ehonda-ai-arena`;
3. `pes-production-reference` — independently generated reference production predictions and context in `pes-squad`;
4. `schadensfresse-production-independent` — independently generated production predictions and context in `schadensfresse`;
5. `arena-production-copy` — an `ehonda-ai-arena` posting target that reuses the selected `pes-squad` production identity and stored predictions; and
6. `arena-challenger-slot` — a nondeployable template for zero or more future self-contained arena challengers.

All rows use competition `bundesliga-2026-27`. The two validation rows use only the complete ADR-0033 identity: `gpt-5.6-luna`, reasoning `none`, output cap `10000`, match prompt `kicktippai/bundesliga-2026-27/predict-one-match` version `2`, and bonus prompt `kicktippai/bundesliga-2026-27/predict-bonus` version `1`. This identity is not a production default or an admitted production challenger.

The three production rows share one `production-primary` configuration slot. The arena challenger template uses separately admitted `arena-challenger-<n>` slots. Model, reasoning effort, output cap, numbered prompt versions, service-tier/fallback policy, whole-season cost ceiling, and participant credentials remain explicit owner gates. An unresolved slot cannot produce a workflow.

The `arena-production-copy` row uses posting target `ehonda-ai-arena` and `community_context: "pes-squad"`. Match reuse requires fixture compatibility. Bonus reuse requires exact normalized question and option compatibility; an incompatibility fails closed and cannot silently invoke a model under the copy row.

For ordinary local `matchday`, `bonus`, `verify`, and `verify-bonus` execution, Kicktipp credentials resolve from the posting target `community`, never from `community_context`. Startup first loads the base environment. A present sibling `.env.<posting-community>` may then override only `KICKTIPP_USERNAME` and `KICKTIPP_PASSWORD`; when it is absent, the already loaded environment remains in effect. Credential values are never printed. The confirmed `.env.ehonda-ai-arena` belongs to the Luna validation participant and is not authority to post through a future production or challenger participant.

Langfuse environment classification remains posting-target based. `ehonda-dev-buli-2627` is `development`; `pes-squad`, `schadensfresse`, and `ehonda-ai-arena` are `production`. An arena Luna validation trace being in the `production` environment records its production-community target; it does not promote Luna/none to a production model.

The exact Actions names for the arena validation participant are `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD`. Existing `pes-squad` and `schadensfresse` names remain as recorded in the matrix. Names for the arena production and challenger participants are not invented before the owner selects those participants.

P0-17 creates no workflow and activates no trigger or schedule. P0-18 updates reusable workflows, P0-19 creates explicit manual entrypoints from deployable matrix rows, P0-20 validates the authorized Luna ladder, and P0-21 alone controls production dispatch and schedule activation.

## Alternatives considered

- **Select credentials from `community_context`:** Rejected because arena copy-posting reads `pes-squad` context while writing through an arena participant.
- **Use the Luna arena sibling environment for every arena participant:** Rejected because one local filename identifies the confirmed validation participant, not future production or challenger accounts.
- **Fill production rows from the Luna validation row or historical workflow models:** Rejected because ADR-0006 and ADR-0033 reserve the production decision for the owner.
- **Give challengers the reference community context:** Rejected because arena challengers must remain self-contained and comparable.
- **Reactivate or edit the old season entrypoints during topology work:** Rejected because reusable workflow support and explicit 2026/27 entrypoints belong to P0-18 and P0-19.

## Consequences

- Workflow writers have exact target/context, credential, environment, and reuse semantics without guessing a model.
- Local arena validation can replace only the Kicktipp credentials while retaining shared Firebase, OpenAI, and Langfuse configuration.
- Local execution for multiple arena participants would need a separately accepted credential-profile selector; P0-17 does not overload the Luna sibling file.
- Secret connectivity and community behavior remain P0-20/P0-21 runtime gates; tracked names and owner confirmation do not substitute for those checks.
- Every unresolved production or challenger slot stays visibly nondeployable.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-17](../tasks/p0-17-community-scope.md)
- [P0-18](../tasks/p0-18-base-workflow-support.md)
- [P0-19](../tasks/p0-19-community-workflow-triad.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None.
