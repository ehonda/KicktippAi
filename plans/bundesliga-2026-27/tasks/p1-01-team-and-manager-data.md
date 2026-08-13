# P1-01 — Replace stale team and manager artifacts

- Status: Not started
- Priority: P1
- Depends on: [P0-19](p0-19-production-activation.md)

## Outcome

Bonus context no longer depends on manually stale `team-data` or `manager-data` artifacts whose facts overlap the roster and squad-summary pipeline.

## Work items

- [ ] Inventory every live `team-data` and `manager-data` consumer, field, uploader, and prompt reference.
- [ ] Map fields already supplied by `team-squad-summary` and roster coach rows; remove duplicate context rather than refreshing it twice.
- [ ] Define the remaining team/manager fields, accepted sources, provenance, freshness, and ownership in an ADR.
- [ ] Implement a focused derived document or collector only for remaining required fields.
- [ ] Replace broad name-substring routing with explicit document names from the Bundesliga profile.
- [ ] Remove superseded upload utilities, files, and tests once no live consumer remains.
- [ ] Add freshness and missing-data tests.

## Validation

- Reconstruct coach, relegation, and placement bonus prompts and compare facts/document sizes before and after.
- Run KPI provider and bonus-command test trees.

## Complete when

- Each live team/manager fact has one authoritative document and freshness date.
- No prompt receives duplicate stale/manual and roster-derived versions of the same fact.
