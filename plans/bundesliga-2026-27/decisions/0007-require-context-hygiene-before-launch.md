# ADR-0007: Require context hygiene before launch

- Status: Accepted
- Date: 2026-08-16

## Context

Real production predictions begin when P0 goes live. Stale or duplicated `team-data` and `manager-data` can contradict the new Club Elo, roster, and squad-summary documents. Bonus predictions happen only before the season, so question-aware bonus context work has no useful post-launch window.

## Decision

Context-document hygiene and question-aware bonus budgeting are P0 launch requirements.

The 2026/27 match and bonus catalogs use explicit competition-owned allowlists. Stale team/manager artifacts, transfer documents, WM26 documents, 2025/26 documents, and deprecated upload paths cannot enter a live prompt. Facts already represented by Elo, rosters, or `team-squad-summary` are not duplicated. Any remaining team/manager field requires an explicit source, freshness contract, and focused document.

Bonus questions are deterministically categorized before production. Each category has an explicit document set and budget, uses only targeted rosters, records selections/exclusions in traces, and gives unknown questions a safe bounded baseline. A representative fixed bonus-question set is validated before any production bonus run.

Historical competition partitions are preserved. Remote deletion is not a launch requirement; any proposed current-scope deletion first produces an explicit dry-run inventory.

## Alternatives considered

- **Leave both tasks in P1:** Rejected because production and one-time bonus predictions would already have consumed the stale or oversized context.
- **Load every current document and let the model reconcile conflicts:** Rejected because it increases cost and can degrade correctness.
- **Delete all historical documents:** Rejected because exclusion through competition-scoped allowlists is safer and preserves evidence.

## Consequences

- The former P1-01 and P1-02 tasks move into P0 ahead of community workflows and validation.
- P0 grows, but the launch definition now protects the quality and cost of the only pre-season bonus run.
- Trace inspection must prove both required-document presence and deprecated-document absence.

## Affected tasks

- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-16](../tasks/p0-16-question-aware-bonus-context.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

The original post-launch priority and dependencies of P1-01 and P1-02.
