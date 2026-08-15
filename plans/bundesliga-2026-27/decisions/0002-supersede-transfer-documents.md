# ADR-0002: Supersede transfer documents with Elo and roster context

- Status: Superseded
- Date: 2026-08-13

## Context

The readiness research described per-team transfer documents as optional match context and proposed transfer automation as P1 work. Transfer documents overlap with the current-roster view, require difficult window and loan semantics, and do not provide a reliable independent measure of team strength.

## Decision

Bundesliga 2026/27 match and bonus prediction paths will not collect, upload, select, or require transfer documents.

Club Elo documents will provide current relative-strength context. Authoritative current-roster documents and the derived `team-squad-summary` will provide membership, coach, age, position, and valuation context. The transfer-document selection and upload path will be retired from live code and tests.

Transfermarkt/DuckDB may still be used to enrich authoritative roster membership with stable IDs, age, position, and market values. This is roster enrichment, not a transfer document source.

Existing historical transfer documents in Firestore do not need to be deleted. A future experiment may assemble its own explicit context outside the live Bundesliga catalog.

## Alternatives considered

- **Keep transfer documents optional:** Rejected because optional retrieval preserves dead code and ambiguous context without a launch requirement.
- **Generate complete transfer-window documents:** Rejected because correct loans, exits, future-effective dates, and unknown fees add work already superseded by the roster and Elo contracts.
- **Use aggregate squad value as the ranking:** Rejected because valuation is complementary roster metadata, not a performance rating.

## Consequences

- Match context becomes smaller and has one clear source for current membership and one for current strength.
- The upload-transfers utility and transfer-specific selection tests can be removed.
- Prompts and trace validation must not refer to transfer documents.

## Affected tasks

- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-13](../tasks/p0-13-bonus-context-baseline.md)

## Supersedes

The optional transfer-document and P1 transfer-automation recommendations in the readiness research.

Superseded by [ADR-0003](0003-duckdb-primary-rosters-with-fallback.md), which retains transfer-document retirement while replacing the roster-source policy.
