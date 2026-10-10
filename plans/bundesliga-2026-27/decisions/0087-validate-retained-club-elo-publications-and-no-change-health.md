# ADR-0087: Validate retained Club Elo publications and no-change health

- Status: Accepted
- Date: 2026-10-10

## Context

Production recovery published October 10 ratings to four heads, then failed in
the second arena consumer. Five lanes share the arena head, but receipt
validation still required the previous completed cycle's snapshot. Recovery
must also retain valid publications committed by a superseded incomplete cycle.
Canonical HTML `NotNewer` passes acquisition, date, mapping, coverage and
freshness; its existing failure classification raises alerts for normal retention.

## Decision

A retained receipt may use completed-cycle authority or a committed publication
newer than that completed baseline. The latter reconstructs the full headed
publication within the receipt transaction and binds its source cycle, attempt,
descriptor, observation time, community, snapshot and date to a persisted
successful publication receipt. A superseded incomplete cycle retains its valid
committed evidence. Exact head identity, current-cycle bundle/observation
identity and ordered receipt consumption remain required.

Canonical HTML `NotNewer` is successful acquisition with no publication. New
receipts omit `ACQUISITION_FAILED` and `CLUB_ELO_SOURCE_REJECTED`, and completed
health resets acquisition failures. Freshness is checked separately. Other
rejections retain their existing failure semantics.

Only authoritative prior-evidence validation recognizes historical HTML
`NotNewer` receipts carrying both old failure conditions. It validates a
normalized copy and compares the historical selection with the exact old
condition reduction. Stored receipts, metadata and payloads remain immutable;
new receipts reject the obsolete failure conditions.

## Alternatives considered

- Reset heads or edit completed health: erases valid publication provenance.
- Compare only source dates: admits unrelated or corrupt snapshots.
- Rewrite old receipts: breaks immutable replay and audit evidence.
- Keep no-change failure counts: preserves alerts during successful refresh.

## Consequences

Shared arena consumers and later recovery cycles complete without republishing
data. Successful no-change refresh closes recovered health alerts. Alternate
authority reads payloads within the Firestore transaction. Storage, acquisition,
workflow topology, model, prompt and credential contracts remain operative.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md).

## Supersedes

The Club Elo rejection health rule in [ADR-0074](0074-freeze-context-source-cycle-handoff-and-provenance.md)
and its HTML continuation in [ADR-0081](0081-close-club-elo-html-publication-and-selection-seams.md)
are partially superseded for canonical `NotNewer`. Their other rules remain
operative. Retained-publication authority is refined for shared heads and
committed publications from superseded incomplete cycles.
