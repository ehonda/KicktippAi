# ADR-0003: Use DuckDB-primary rosters with per-club fallback

- Status: Accepted
- Date: 2026-08-16

## Context

Transfer documents remain unsuitable for the live Bundesliga context contract. The audited Transfermarkt DuckDB artifact contains useful player identity, position, age, valuation, transfer, and club data, but its season-membership tables are not yet complete for 2026/27. Requiring a person to maintain rosters throughout the season would make the feature operationally unsustainable.

## Decision

Bundesliga 2026/27 will not collect, upload, select, or require transfer documents.

The repository will start with a complete, source-dated fallback membership seed for all 18 clubs. Agents assemble it from official club squad sources, cross-check league listings, use DuckDB for stable identifiers and supplemental enrichment where safe, and perform one targeted independent audit.

At collection time, membership is selected per club:

- DuckDB is primary when it explicitly represents the 2026/27 season and passes manifest identity, plausible squad-count, duplicate, coach, and completeness gates.
- The checked-in fallback or last-known-good club snapshot remains active when DuckDB is missing, stale, partial, or suspicious.
- A club automatically moves to DuckDB membership once those gates pass; routine human approval is not required.
- A later invalid refresh cannot displace last-known-good membership.
- The complete 18-club document set publishes atomically after per-club source selection succeeds.

DuckDB also supplies safe enrichment. Missing supplemental values are rendered as `N/A` with coverage diagnostics and never cause an otherwise valid member to be dropped.

## Alternatives considered

- **Maintain all memberships manually:** Rejected because the project owner cannot maintain rosters during the season.
- **Treat the current audited DuckDB artifact as complete:** Rejected because its 2026/27 season membership is incomplete.
- **Require manual approval for every DuckDB change:** Rejected because strict per-club gates plus last-known-good behavior provide a maintainable automatic path.
- **Keep transfer documents:** Rejected for the overlap and semantic problems recorded in ADR-0002.

## Consequences

- Launch requires a one-time complete seed and high-value audit.
- Provenance and source selection must be visible per club in reports and traces.
- P1 roster refresh work implements automatic valid takeover rather than a perpetual human review queue.
- Historical transfer documents may remain stored but are excluded from all live 2026/27 selectors.

## Affected tasks

- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-08](../tasks/p0-08-roster-membership-seed.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

[ADR-0002](0002-supersede-transfer-documents.md).
