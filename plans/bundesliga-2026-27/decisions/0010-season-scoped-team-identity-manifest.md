# ADR-0010: Use a season-scoped strict team identity manifest

- Status: Accepted
- Date: 2026-08-16

## Context

Kicktipp, official Bundesliga roster pages, Club Elo, Transfermarkt data, and context-document names identify clubs differently. The previous runtime contained a Bundesliga name-to-slug dictionary in one path, duplicated a second dictionary in AnalyzeMatch, and silently generated a slug for any missing name. That behavior could publish or query plausible-looking documents under the wrong identity, especially for the promoted 2026/27 clubs Elversberg, Schalke, and Paderborn.

Roster and Elo work need one typed, source-provenanced join boundary before their collectors can be implemented. The manifest also needs stable repository document slugs; external display codes are not sufficiently stable or consistent with established KicktippAi document IDs.

## Decision

`data/bundesliga-2026-27/team-manifest.csv` is the authoritative identity join for exactly 18 Bundesliga 2026/27 clubs. It contains, per row:

- the exact case-sensitive Kicktipp name captured from `ehonda-dev-buli-2627`;
- one unique stable KicktippAi document slug;
- the official club name and an HTTPS Bundesliga club page containing the roster section;
- one unique non-empty Club Elo club/API route alias; and
- an optional positive Transfermarkt club ID used only for enrichment.

The rows are ordered by document slug using ordinal comparison. The checked-in CSV is UTF-8 without a byte-order mark, uses CRLF line endings, starts directly with its header, and ends with a final CRLF. Core embeds and strictly parses this same file, validating its exact schema, count, required fields, URLs, IDs, uniqueness, and ordering.

The stable promoted-club slugs are `sve` for SV Elversberg, `s04` for FC Schalke 04, and `scp` for SC Paderborn 07. These are repository document identities, not a promise to mirror the Bundesliga table's display codes. Existing retained slugs remain unchanged.

All Bundesliga 2026/27 name-to-document resolution uses the manifest's exact Kicktipp name. An unknown or differently cased name fails with an actionable error; automatic slug fallback is disabled for this competition. Generic slugging remains available for other explicit competition contracts such as FIFA World Cup 2026. Runtime and tooling must not maintain a second live Bundesliga mapping.

Official Bundesliga pages establish current club names and roster-source URLs. Club Elo's source-dated Germany ranking and linked club routes establish aliases; P0-10 must still validate them against its captured launch response because the dated CSV endpoint did not respond during bounded P0-04 verification. This does not authorize unattended network access; ADR-0008 continues to govern that separate launch gate. Transfermarkt IDs do not establish season membership.

## Alternatives considered

- **Continue fallback slug generation:** Rejected because a misspelled or newly promoted team could silently select a new document namespace.
- **Use official league display codes as document slugs:** Rejected because existing repository document IDs intentionally use stable project conventions such as `fck`, `sge`, and `b04`, not a uniform external code system.
- **Keep mappings beside each consumer:** Rejected because independent roster, Elo, context, and observability maps drift and cannot enforce a one-to-one join.
- **Use Club Elo or Transfermarkt as the membership authority:** Rejected because those providers serve strength or enrichment roles and do not replace the official current-season roster contract.

## Consequences

- Roster and Club Elo implementations can join against the same typed, validated records.
- A new or changed Kicktipp name requires a reviewed manifest update instead of producing an implicit document slug.
- The Core assembly embeds a small checked-in data resource so all runtime entrypoints share the contract.
- Source and alias changes remain data reviews; unattended Club Elo fetching remains disabled until its separate owner gate passes.

## Affected tasks

- [P0-04](../tasks/p0-04-team-manifest.md)
- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-08](../tasks/p0-08-roster-membership-seed.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-10](../tasks/p0-10-club-elo-source.md)
- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-14](../tasks/p0-14-profile-driven-collection.md)

## Supersedes

The implicit Bundesliga team dictionaries and fallback-slug behavior.
