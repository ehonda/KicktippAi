# ADR-0001: Support only the current Bundesliga season

- Status: Accepted
- Date: 2026-08-13

## Context

Bundesliga 2025/26 is complete. The readiness research initially proposed preserving its live prompt paths, workflow behavior, and unscoped persistence mapping while adding 2026/27. We do not plan to participate in 2025/26 again or run more experiments against it as part of normal repository operation.

Keeping two live Bundesliga paths would add fallback branches, tests, and workflow ambiguity to every onboarding task without serving a current use case.

## Decision

`bundesliga-2026-27` is the only live Bundesliga season supported by this plan. Runtime defaults, local prompts, community workflows, current-season metadata, and normal persistence composition will advance to 2026/27.

The implementation does not need to preserve 2025/26 workflow execution, prompt routing, implicit/unscoped document identity, or experiment defaults. Existing historical Firestore data and tracked experiment artifacts may remain and will not be migrated or deleted by this plan.

A future historical experiment must explicitly provide its competition, prompt, dataset, and context contract instead of relying on the live application defaults.

## Alternatives considered

- **Maintain parallel 2025/26 compatibility:** Rejected because the season is no longer operated and the compatibility branches would complicate the new live path.
- **Delete all historical data and artifacts:** Rejected because cleanup is unnecessary for readiness and could destroy useful evidence.

## Consequences

- Current code and tests can be simplified around 2026/27 rather than preserving old defaults.
- Historical fixtures may remain where they test generic parsing or analysis, but historical runtime behavior is not an acceptance criterion.
- Firestore isolation still matters: new 2026/27 writes must use explicit competition-scoped identities and must not collide with old unscoped documents.

## Affected tasks

- [P0-01](../tasks/p0-01-current-competition.md)
- [P0-02](../tasks/p0-02-competition-scoped-storage.md)
- [P0-05](../tasks/p0-05-prompt-route.md)
- [P0-17](../tasks/p0-17-community-workflow-triad.md)
- [P1-06](../tasks/p1-06-observability-datasets.md)

## Supersedes

The 2025/26 compatibility recommendation in the readiness research.
