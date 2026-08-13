# P1-05 — Review and publish roster membership changes

- Status: Not started
- Priority: P1
- Depends on: [P0-19](p0-19-production-activation.md)

## Outcome

Roster changes are detected, reviewed, and published without treating an enrichment database as membership truth.

## Work items

- [ ] Choose transfer-window and in-season review cadence, source process, approvers, and emergency-change behavior; record them in an ADR.
- [ ] Build a deterministic diff between the checked-in authoritative seed and newly proposed membership.
- [ ] Classify additions, departures, team changes, coach changes, source changes, and enrichment-only changes.
- [ ] Require human approval for membership changes unless an ADR accepts a source for automatic publication.
- [ ] Re-run all 18-team quality gates and publish roster documents atomically after acceptance.
- [ ] Keep DuckDB enrichment refresh separable from membership edits.
- [ ] Add tests for loans, duplicate membership, renamed players, unmatched IDs, coach changes, and rejected diffs.

## Validation

- Exercise one synthetic membership addition, departure, and enrichment-only update through dry-run/review/publish.
- Confirm rejected changes leave the last-known-good documents active.

## Complete when

- Every membership change has a reviewed diff and updated provenance.
- Scheduled or automated enrichment cannot silently move a player between clubs.
