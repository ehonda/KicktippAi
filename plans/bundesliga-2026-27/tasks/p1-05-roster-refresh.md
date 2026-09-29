# P1-05 — Roster refresh

- Status: Deferred — dormant under ADR-0082
- Last reconciled: 2026-09-16
- Depends on: exact [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md) dcaribou sidecar and separate future owner gates
- Decision: [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Current contract

P1-05/R1 is excluded from the P1-04 operational activation graph. It is
`MetadataUnavailable` until the exact ADR-0079 sidecar exists. The dormant
closeout is historical scope evidence only; it is not R1 implementation,
provider adoption or activation.

Until a future accepted decision and owner gate release R1, P1-05 performs no
sidecar or artifact probe; resolves no provider; and creates no observation,
receipt, artifact, health, publication, issue or API action. Seed/LKG preserves
the truthful original dates and provenance, and automatic freshness is
unavailable. Existing v1/v2 remain unchanged; no v3 relabel, migration or
backfill is authorized.

## Future gate

A future R1 requires the exact sidecar contract, a new accepted implementation
scope, independent owner authorization and its own validation. P1-04's
parser-v2, transport, source-only validation, scheduled activation and rollback
authority do not grant any P1-05 work.
