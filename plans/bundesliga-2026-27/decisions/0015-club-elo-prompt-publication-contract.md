# ADR-0015: Use strict Club Elo prompt documents and reconstructable publication provenance

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0013 fixes the complete source snapshot and ADR-0014 provides an atomic publication boundary, but neither fixes the exact Club Elo text that enters prompts or the provenance needed to safely reconstruct a published last-known-good (LKG) snapshot. A collector must not let CSV formatting, rank ties, or mutable “latest” reads change prompt inputs invisibly.

## Decision

The `club-elo` publication set uses the canonical definition from ADR-0014: eighteen context documents named `club-elo-{slug}.csv` and one KPI document named `club-elo-rankings`.

Every document has the exact CSV schema and header:

`Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At`

Each team context document contains that header and exactly one row for its manifest team. The aggregate KPI contains the same header and exactly eighteen rows. `Team` is the manifest `Club_Elo_Name`; it is never a newly derived display name.

The collector calculates Bundesliga rank by Elo descending. Equal Elo values are deterministically ordered by source `Global_Rank` ascending and then by manifest slug ordinal; ranks are sequential `1` through `18`. The aggregate is ordered by Bundesliga rank and then slug ordinal. Every text document is valid UTF-8 without a BOM, begins with the header, uses CRLF line endings only, and ends with one final CRLF.

Publication metadata is a JSON object that records schema version, rated date, UTC collection timestamp, HTTPS source URL, selected snapshot origin, selection disposition, selection diagnostics, expected manifest count, and the rank/tie policy identifier. This metadata, the headed exact payload versions, and the canonical definition are required to reconstruct an LKG snapshot. Reconstruction fails closed if metadata is malformed or inconsistent, a headed payload is missing/corrupt, a document has a non-canonical name/content/order, a per-team row does not match its key, or the aggregate is not the exact rendered aggregate of the reconstructed entries.

Dry-run and trace diagnostics expose the selected origin, selection disposition, rated date, collection timestamp, source URL, age in calendar days at evaluation time, mapping coverage, LKG snapshot ID when present, and all source-selection diagnostics. Network collection remains disabled under ADR-0008 and ADR-0013; no diagnostic implies permission to use it.

## Alternatives considered

- **Use a richer aggregate-only schema:** Rejected because match prompts need one small stable per-team document and the aggregate should remain directly comparable.
- **Use tied shared Bundesliga positions:** Rejected because stable prompt ordering and exactly one row ordering are more useful than presentation-style competition ranking.
- **Store provenance only in console output:** Rejected because LKG reconstruction and trace review require durable evidence.

## Consequences

- Exact document bytes become a durable prompt contract and participate in ADR-0014 snapshot identity.
- Later source work may add a candidate parser but cannot change the text schema, tie rule, or provenance without a successor ADR.
- P0-11 can reconstruct an LKG solely from the publication head, immutable metadata, and exact payloads.

## Affected tasks

- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)

## Supersedes

None.
