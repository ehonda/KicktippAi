# P1-16 — Automatic history-date updates

- Status: Deferred / low urgency — needs interview
- Outcome: later decide whether automatic exact-date updates can preserve reviewed source authority, last-known-good behavior, and publication safety.
- Depends on: [ADR-0072](../decisions/0072-operate-history-date-maintenance-manually.md)

## Boundary

This identifier is intentionally unused by the completed manual-maintenance slice. Any future implementation must first interview source authority, cadence, rollback, and PR/publication topology; it must not infer authority from the weekly reminder or collection-date proxy reporting.
