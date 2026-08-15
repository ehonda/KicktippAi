# ADR-0005: Launch all selected communities with reference prediction reuse

- Status: Accepted
- Date: 2026-08-16

## Context

Bonus predictions can only be submitted before the season and the project does not want to miss a matchday. The development community and all three production communities therefore need explicit identities, context ownership, credential owners, and prediction-reuse behavior before workflows are created.

## Decision

The safe development community is `ehonda-dev-buli-2627`. The required production scope is `pes-squad`, `schadensfresse`, and `ehonda-ai-arena`.

`pes-squad` is the reference production community. `schadensfresse` uses its own community context and independently generates with the selected production configuration. The matching production-model arena participant posts the stored `pes-squad` prediction by targeting `ehonda-ai-arena` while using `community_context: "pes-squad"`, following the proven WM26 secondary copy-posting pattern. Arena challenger configurations use `community_context: "ehonda-ai-arena"` and generate independently.

Match prediction reuse requires fixture compatibility. Bonus reuse requires exact normalized question and option compatibility. Any incompatible arena bonus question fails closed for copy-posting and is generated independently.

The project owner has configured the development community and arena with their Luna/none validation participants. The respective community administrators configure `pes-squad` and `schadensfresse` later, before production validation. Final model and arena-challenger choices remain a late owner-controlled decision.

## Alternatives considered

- **Launch one canary community:** Rejected because all three communities must participate before the opening cutoff.
- **Generate the production model again in the arena:** Rejected because stored-prediction reuse saves API spend without losing the reference entry.
- **Reuse one context for every arena challenger:** Rejected because challengers need a self-contained, comparable arena context.

## Consequences

- Community workflows must distinguish posting target from community context.
- Bonus compatibility becomes an explicit activation gate.
- The complete matrix is recorded before final workflow generation, while exact production models may be filled in at the later model gate.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-17](../tasks/p0-17-community-scope.md)
- [P0-19](../tasks/p0-19-community-workflow-triad.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

The provisional one-canary recommendation in the draft execution strategy.
