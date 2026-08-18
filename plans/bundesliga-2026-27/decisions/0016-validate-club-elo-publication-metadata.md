# ADR-0016: Validate Club Elo publication metadata semantically

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0015 records the Club Elo publication provenance fields, but reconstruction must also reject plausible-looking metadata that contradicts source selection or loses diagnostic meaning. A headed snapshot is last-known-good only when both its exact payloads and provenance are trustworthy.

## Decision

Club Elo metadata is an immutable JSON object with exactly the nine ADR-0015 properties: schema version, rated date, UTC collection timestamp, absolute HTTPS source URL, selected origin, selection disposition, diagnostics, expected manifest count, and rank-policy identifier. Enum values use exact declared names only; numeric and case-variant values are invalid. Diagnostics are nonblank trimmed strings, unique, and ordinal-sorted.

`NetworkAccepted` requires `NetworkCandidate` origin and no diagnostics. Every other disposition requires `LaunchSeed` or `LastKnownGood` origin and at least one diagnostic. `NetworkDisabled` has exactly `UNATTENDED_NETWORK_USE_NOT_APPROVED`; rejected candidates retain a nonempty canonical diagnostic list; stale and not-newer candidates each have one diagnostic with their accepted policy prefix. Source URL, dates, and selected origin are parsed into an immutable metadata model and used to reconstruct the snapshot provenance; any malformed type, unknown property, contradiction, or noncanonical value fails closed.

## Consequences

- Metadata cannot be used to disguise disabled network collection as an accepted refresh.
- LKG reconstruction detects both lexical CSV tampering and semantic provenance tampering.
- Collect-context traces can expose the same nonsecret selection facts deterministically.

## Affected tasks

- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)

## Supersedes

The metadata/provenance-validation portion of [ADR-0015](0015-club-elo-prompt-publication-contract.md); ADR-0015's CSV schema and rendering contract remain accepted.
