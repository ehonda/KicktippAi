# P1-05 — Refresh quality-gated DuckDB roster membership and enrichment

- Status: Source-independent dormant lane
- Depends on: C1 review/integration then C2; not HTML
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), and [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)

## Outcome

One observed artifact per cycle is selected only through the exact pre-artifact
matrix, per-club safety gates, and the source-publication fence. The current
artifact remains rejection-only evidence: it cannot displace seed/LKG. Retained
membership, enrichment provenance and dates remain truthful; a synthetic future
artifact with trusted dates proves mechanics, not current-source acceptance.

## Work and verification

- [ ] C2 implements ADR-0078's evaluation precedence, required/null fields,
  nullable receipt revision rule, revision-state protection and optional guard.
- [ ] R1 implements bounded acquisition/selection/diff/carry/v3 only after C2.
- [ ] Cover metadata unavailable/malformed/revision, identity failure, schema
  rejection, existing real rejection, synthetic takeover, guard/CAS precedence,
  crash replay, legacy null guard, no source-disabled resolution/writes/API
  calls, and exact-head CI under one heavy family.
- [ ] Bound changed/pending acquisition to one metadata check/cycle, a temporary
  stream, five minutes/300 MiB, one in-budget transient retry, hash, embedded
  revision verification and remote-drift check; no alternate provider.
- [ ] Preserve deterministic per-club additions/departures/team/coach/source/
  enrichment-only diff, stable-ID carry, new-player `N/A`, sourced decreases,
  candidate-only conflict rejection, final-set fatal contradiction, complete-18
  and atomic-publication gates. Add source attribution outside prompt content.
- [ ] R1's source/test surface is bounded to `BundesligaRosterRefresh.cs`,
  `BundesligaRosterModels.cs`, `BundesligaRosterPolicy.cs`,
  `BundesligaRosterPublication.cs`, `BundesligaRosterPublicationContract.cs`,
  `BundesligaRosterCsv.cs`, `BundesligaRosterSeed.cs`,
  `BundesligaRosterArtifactAcquirer.cs`, `BundesligaRosterSource.cs`,
  `CollectContextRostersCommand.cs`, `roster-refresh-policy-v1.json`, the
  roster source document, and the literal R1 tests listed in the execution
  packet; it does not edit shared descriptor/receipt/health/fence paths.

No alternate provider, source activation, persistence, schedule, model, post,
credential, or copy behavior is implied.

## Owner gates and completion

Development persistence, production acquisition/writes/activation, GitHub
artifact/issues, rollback delegation, restoration and completion evidence remain
owner decisions. Complete only after trustworthy enrichment automation and a
separately authorized future valid-membership takeover proof.
