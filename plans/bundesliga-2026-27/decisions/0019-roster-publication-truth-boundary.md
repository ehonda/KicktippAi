# ADR-0019: Share one roster-publication truth boundary

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0018 made roster metadata canonical but did not require the builder and reconstructing reader to prove the same metadata facts against the selected snapshots. A malformed quality report could therefore be serialized by a trusted writer, while normalizing metadata during reconstruction could hide an identity corruption. DuckDB membership conversion and enrichment failures also need a precise distinction from per-club gate rejections.

## Decision

This supersedes only ADR-0018's metadata semantic-validation decision. Core uses one shared truth validator for `BundesligaRosterPublication.Build` and `ReconstructLastKnownGood`. It requires exact manifest coverage; canonical normalized member names and ordering without repair; exact snapshot source/date/member and coverage counts; ordinal-sorted unique references, diagnostics, and members; the source/gate selection-reason matrix; DuckDB membership date equal to its DuckDB snapshot date; lower-case SHA-256 LKG identities; and valid source/revision/snapshot/LKG provenance combinations. Builders reject false facts before emitting JSON.

Gate-representable raw membership problems remain per-club policy rejections. Membership query or lossless-conversion failures are global source failures: retain a headed LKG exactly, otherwise retain the complete seed with `ENRICHMENT_UNAVAILABLE`. Enrichment requires at most one player row for each selected stable ID; zero rows are non-fatal unmatched identities and emit deterministic `UNMATCHED_STABLE_PLAYER_IDS` diagnostics. DuckDB paths are passed through a safe connection-string builder as literal data-source values.

## Consequences

- Published roster metadata is an auditable truth claim, not only a canonical JSON shape.
- Corrupt or ambiguous enrichment cannot advance a roster head.
- Per-club membership diagnostics remain useful without weakening global failure safety.

## Affected tasks

- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-08](../tasks/p0-08-roster-membership-seed.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

Only the metadata semantic-validation decision in [ADR-0018](0018-validate-roster-publication-metadata-semantically.md).
