# P0-01 — Make 2026/27 the current Bundesliga competition

- Status: Not started
- Priority: P0
- Depends on: None
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

Every normal non-WM26 runtime path resolves Bundesliga to `bundesliga-2026-27`, and current-season observability metadata agrees.

## Work items

- [ ] Add `CompetitionIds.Bundesliga2026_27` and make it the live Bundesliga identifier.
- [ ] Advance `CompetitionResolver`'s non-WM26 default and `KicktippSeasonMetadata.Current` to the new ID.
- [ ] Update CLI option descriptions and user-facing output that describes the default competition.
- [ ] Remove active fallback logic whose only purpose is to keep 2025/26 current; do not add a parallel legacy runtime route.
- [ ] Classify remaining `bundesliga-2025-26` literals as historical fixtures/artifacts or stale live defaults. Move stale live defaults into the relevant follow-up task rather than mechanically rewriting historical evidence.
- [ ] Update resolver and season-metadata tests for explicit WM26, explicit 2026/27, and omitted-competition behavior.

## Validation

- Run the `CompetitionResolverTests` tree in `tests/Orchestrator.Tests`.
- Search live source and workflow code for current-season references to `bundesliga-2025-26` and account for every remaining result.

## Complete when

- An omitted competition resolves to `bundesliga-2026-27` for Bundesliga communities.
- Explicit WM26 resolution is unchanged.
- No production default reports or selects 2025/26.
