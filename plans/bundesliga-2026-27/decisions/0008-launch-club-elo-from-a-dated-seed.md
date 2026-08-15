# ADR-0008: Launch Club Elo from a dated seed when necessary

- Status: Accepted
- Date: 2026-08-16

## Context

Club Elo currently covers all 18 Bundesliga clubs, but explicit official reuse and unattended API terms have not yet been established. That decision can happen late in onboarding, while implementation and the first matchdays can safely use a source-dated complete snapshot.

## Decision

P0 implements the Club Elo source contract, parser/provider boundary, 18-club mapping, validation gates, cache, and last-known-good behavior. The accepted launch source may be a complete, source-dated seed.

Unattended network fetching remains disabled until a late pre-go-live decision records acceptable reuse terms or selects a permitted alternative. If terms remain unresolved at launch, predictions use the dated seed and expose its age; P1-04 owns later scheduled refresh activation. Partial or stale network data can never replace the complete seed or last-known-good snapshot.

## Alternatives considered

- **Block all implementation until terms are resolved:** Rejected because provider and validation work is independent and the seed is sufficient initially.
- **Enable scheduled fetching without a terms decision:** Rejected because source/reuse acceptance is an explicit owner gate.
- **Drop Club Elo context:** Rejected because it is the accepted independent strength signal replacing transfer documents.

## Consequences

- P0-10 can accept a lawful dated seed as the launch source while keeping network activation gated.
- The late source decision is visible and cannot be mistaken for routine implementation authority.
- Initial predictions may use an older but complete rating date, with trace-visible age.

## Affected tasks

- [P0-10](../tasks/p0-10-club-elo-source.md)
- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)
- [P1-04](../tasks/p1-04-club-elo-refresh.md)

## Supersedes

The requirement to resolve unattended Club Elo fetching before provider implementation can begin.
