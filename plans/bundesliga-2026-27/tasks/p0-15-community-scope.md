# P0-15 — Record community and environment scope

- Status: Not started
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md), [P0-06](p0-06-model-ledger-and-cost-baseline.md)

## Outcome

The exact development and production communities, context-sharing relationships, model configurations, and credential owners are decided before workflows are created.

## Work items

- [ ] Decide which of `pes-squad`, `schadensfresse`, and `ehonda-ai-arena` participate, plus any new community.
- [ ] Select one development community that is safe for overwrite and prediction validation.
- [ ] Decide whether any community shares context or copies predictions; default to self-contained behavior unless explicitly accepted.
- [ ] Map each community to community context, model-ledger entry, prompt route, secret names, and Langfuse environment.
- [ ] Record the complete matrix in an ADR and a small tracked onboarding table.
- [ ] Verify Kicktipp membership, competition configuration, and credential availability without enabling schedules.

## Validation

- Cross-check the matrix against repository secret names and `ProductionCommunities` behavior in matchday/bonus commands.
- Confirm every selected community has an owner and a safe manual validation path.

## Complete when

- P0-17 can be copied once per selected community with no unresolved placeholders.
- Unselected 2025/26 entrypoints are explicitly retired or left inert, not accidentally reactivated.
