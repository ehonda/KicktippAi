# P1-05 — Refresh quality-gated DuckDB roster membership

- Status: Not started
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md)

## Outcome

Roster changes are detected and published from current-season DuckDB membership per club when strict quality gates pass, while fallback or last-known-good membership remains active for incomplete clubs.

## Work items

- [ ] Choose transfer-window and in-season refresh cadence, source process, alert ownership, and emergency-change behavior; record them in an ADR.
- [ ] Build a deterministic per-club diff between fallback/last-known-good membership and proposed DuckDB membership.
- [ ] Classify additions, departures, team changes, coach changes, source changes, and enrichment-only changes.
- [ ] Automatically accept a club only when DuckDB explicitly represents 2026/27 and passes identity, plausible-count, duplicate, coach, and completeness gates.
- [ ] Reject suspicious or partial club changes, retain last-known-good membership, and emit an actionable alert rather than silently publishing.
- [ ] Re-run all 18-team quality gates and publish the complete document set atomically after per-club source selection.
- [ ] Preserve ADR-0050's v2 derived-row semantics and make automated refresh reject any age/position/valuation coverage regression against the headed last-known-good snapshot unless a later accepted policy explicitly permits it. P0-25's one-time pinned launch command does not supply artifact acquisition, refresh cadence, or CI wiring for this task.
- [ ] Add tests for loans, duplicate membership, renamed players, unmatched IDs, coach changes, and rejected diffs.

## Validation

- Exercise one synthetic membership addition, departure, automatic valid takeover, rejected partial update, and enrichment-only update through dry-run/publish.
- Confirm rejected changes leave the last-known-good documents active.

## Complete when

- Every membership change has a deterministic diff and updated provenance.
- A valid current-season DuckDB club can take over automatically; an invalid club cannot displace fallback or last-known-good membership.
