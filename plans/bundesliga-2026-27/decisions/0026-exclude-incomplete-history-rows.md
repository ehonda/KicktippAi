# ADR-0026: Exclude incomplete rows from selected match history

- Status: Accepted
- Date: 2026-08-21

## Context

The Kicktipp recent, home, and away tables returned before Bundesliga matchday 1 include upcoming DFB-Pokal fixtures. Those rows have teams and a competition but no completed score. They are scheduled matches, not historical results, and cannot satisfy the exact completed-match identity used by ADR-0025. Assigning them a `Played_At` would misrepresent a future schedule as played history.

Incomplete rows can appear at the start of a raw table. If map ordinals counted them, removing the rows after reconstruction would shift every completed row and make a repeated strict audit fail against the unchanged map.

## Decision

Bundesliga history selection requires a completed, parseable non-negative `home:away` score before played-date resolution. The collector excludes incomplete rows from the prompt-facing recent, home, and away documents before applying the source hierarchy. It reports the exact excluded count and the completed-result reason. It never creates a date-map entry or date for an incomplete row.

`Row_Ordinal` in the deterministic map counts only completed selected rows after exclusion, preserving their existing order. This makes the transformation idempotent when an incomplete leading row is removed. A later collection may select that match only after Kicktipp presents a completed score; it must then resolve through the ADR-0025 source hierarchy like any other completed row.

All source, license, exact identity, ambiguity, provenance, competition-isolation, last-known-good, and head-to-head decisions in ADR-0025 remain unchanged.

## Alternatives considered

- **Assign the scheduled date:** Rejected because the row is not a completed historical result and the final played date could change.
- **Keep the incomplete row undated:** Rejected because every prompt-facing selected history row must carry exact played-date evidence.
- **Count raw-table ordinals:** Rejected because exclusion would shift ordinals on the next audit and break byte-stable idempotency.

## Consequences

- Prompt-facing history contains results, not upcoming schedule entries.
- Export, audit, apply, and typed collection evidence includes an incomplete-row exclusion count.
- Tests must cover leading incomplete rows, completed-row ordinals, and repeated application.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0025 only where its canonical row ordinal did not explicitly state that incomplete rows are excluded before ordinal assignment.
