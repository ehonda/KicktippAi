# P1-05 — Refresh quality-gated DuckDB roster membership and enrichment

- Status: Interview complete — not implemented
- Priority: Highest remaining P1 priority (second, after P1-04)
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md)
- Design: [P1-04/P1-05 context refresh](../designs/p1-04-05-context-refresh.md)

## Outcome

Roster membership and enrichment are observed only in existing context cycles.
Valid current-season membership may publish per club when its strict gates pass;
rejected candidates retain fallback/last-known-good data without obscuring
membership, enrichment, field-effective, or artifact source dates.

## Work items

- [ ] Independently accept the minimal per-cycle health/deduplication and artifact-handoff recovery seam before workflow edits.
- [ ] Add one metadata check per existing context cycle and bounded changed/pending-revision acquisition: temporary stream, hash, revision verification, remote-drift check, five-minute/300-MiB limit, and one in-budget transient retry.
- [ ] Require explicit 2026/27 membership, existing quality gates, and revision-bound authoritative capture/effective dates. Treat the paused upstream as `UNKNOWN_SOURCE_DATE` until that binding is proven; do not add an alternate provider.
- [ ] Build deterministic per-club membership selection/fallback and preserve the global complete-18, identity, reconstruction, and atomic-publication gates.
- [ ] Select enrichment independently by stable ID: carry prior same-ID fields with provenance/age, use `N/A` for new unknowns, accept genuine sourced decreases, reject candidate-only conflicts, and fail final-set contradictions.
- [ ] Add per-due-cycle warning/summary and reusable-issue reporting without a standalone roster schedule or one issue per community job.
- [ ] Add development-first changed/unchanged/drifting/oversize/timed-out artifact, revision/date rejection, membership takeover/fallback/departure, enrichment carry/`N/A`/conflict, reconstruction, reuse, dry-run, serial, and copy-compatibility tests.
- [ ] Add future README-linked source attribution; do not place source attribution in prompt documents.

## Validation

- Exercise synthetic valid future membership takeover, addition, departure, rejected partial update, and carried/`N/A` enrichment through development dry-run/publish fixtures.
- Confirm rejected candidates retain the appropriate prior data, while valid membership can publish with rejected enrichment; first production activation remains separately reviewed.

## Complete when

- Trustworthy enrichment automation is active and the automatic future valid-membership takeover path is fully proven.
- An invalid club cannot displace fallback/LKG; paused upstream may retain live membership and leave its source issue open.
