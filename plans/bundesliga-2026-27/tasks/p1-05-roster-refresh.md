# P1-05 — Refresh quality-gated DuckDB roster membership and enrichment

- Status: Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082.
- Depends on: C2 is reusable common evidence; future R1 additionally requires the exact ADR-0079 conforming dcaribou sidecar and separate owner permission; not HTML
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](../decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), and [ADR-0082](../decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)

## Outcome

This run closes only the documented dormant scope under ADR-0082; roster
functionality remains deferred and incomplete. Dcaribou is the sole metadata
authority. The exact ADR-0079 sidecar must exist before any future artifact
request. Its absence/404 is `MetadataUnavailable`: no probe, provider
resolution, observation, receipt, artifact, health, publication, or API action;
seed/LKG is retained and accepted/pending revision state is unchanged. The
current DuckDB is rejection-only evidence and cannot displace seed/LKG. Retained
membership, enrichment provenance, and original dates remain truthful;
automatic freshness is unavailable. Synthetic trusted-date fixtures prove
mechanics, not current-source acceptance.

## Work and verification

- [x] C2 is reusable common evidence, not R1 implementation credit.
- [x] Dormant closeout is satisfied by ADR-0082; rejected R1
  `b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected closeout `80b7c6c`
  remain unintegrated evidence only.
- [ ] R1 implements bounded acquisition/provider/consumer work, selection/diff/
  carry, canonical v3/reconstruction, source tests, enrichment automation, and
  valid takeover criteria only after the conforming ADR-0079 sidecar and
  separate owner permission.
- [ ] Cover metadata unavailable/malformed/revision, hostile sidecar/URL,
  null/type/range/canonical/date rejection, identity failure, schema rejection,
  existing real rejection, synthetic takeover, replay/supersession, guard/CAS
  precedence, crash replay, legacy null guard, no source-disabled
  resolution/writes/API calls, and exact-head CI under one heavy family.
- [ ] Bound changed/pending acquisition to one metadata check/cycle, then only
  after a valid sidecar a temporary stream, five minutes/300 MiB, one in-budget
  transient retry, hash, embedded revision verification and remote-drift check;
  no alternate provider, redirect, or effective-URL variance.
- [ ] Preserve deterministic per-club additions/departures/team/coach/source/
  enrichment-only diff, stable-ID carry, new-player `N/A`, sourced decreases,
  candidate-only conflict rejection, final-set fatal contradiction, complete-18
  and atomic-publication gates. Roster attribution waits for future accepted R1.
- [ ] R1's source/test surface is bounded to `BundesligaRosterRefresh.cs`,
  `BundesligaRosterModels.cs`, `BundesligaRosterPolicy.cs`,
  `BundesligaRosterPublication.cs`, `BundesligaRosterPublicationContract.cs`,
  `BundesligaRosterCsv.cs`, `BundesligaRosterSeed.cs`,
  `BundesligaRosterArtifactAcquirer.cs`, `BundesligaRosterSource.cs`,
  `CollectContextRostersCommand.cs`, `roster-refresh-policy-v1.json`, the
  roster source document, and the literal R1 tests listed in the execution
  packet; it does not edit shared descriptor/receipt/health/fence paths.

No alternate provider, provider-adoption claim, source activation, persistence,
schedule, model, post, credential, or copy behavior is implied. Existing v1/v2
remain unchanged; no v3 relabel, migration, or backfill occurs. R1 is neither
live nor current-source acceptable.

## Owner gates and deferred future work

Development persistence, production acquisition/writes/activation, GitHub
artifact/issues, rollback delegation, restoration and future evidence remain
owner decisions. A future valid-membership takeover requires trustworthy
enrichment automation and separately authorized evidence.
