# P0-02 — Require competition-scoped persistence

- Status: Not started
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

All 2026/27 context, KPI, match-outcome, and prediction operations carry the resolved competition and use competition-scoped document identities.

## Work items

- [ ] Trace competition construction through `FirebaseServiceFactory`, Firebase repositories, and `KicktippContextProvider`.
- [ ] Remove the live `null`/2025/26 implicit path, including the `ToRepositoryCompetitionArgument` behavior that converts the old season to an unscoped argument.
- [ ] Make production composition pass `bundesliga-2026-27` explicitly into every repository/provider.
- [ ] Ensure document ID builders always include the new competition and the required community identity.
- [ ] Remove 2025/26 default field initializers from the live write path and treat missing competition identity as invalid for current operations; historical record compatibility is out of scope.
- [ ] Add isolation tests proving 2026/27 reads cannot return unscoped or WM26 documents and that writes cannot collide across communities.
- [ ] Update Firebase adapter documentation to describe current competition scoping.

## Validation

- Run the competition-isolation, context-repository, KPI-repository, prediction-repository, and match-outcome repository test trees in `tests/FirebaseAdapter.Tests`.
- Inspect generated IDs in tests for the literal `bundesliga-2026-27` prefix and community partition.

## Complete when

- No normal 2026/27 repository is constructed with an absent competition.
- A test fixture containing old unscoped data cannot satisfy a 2026/27 query.
- No historical data migration or deletion is required.
