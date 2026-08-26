# P0-25 — Publish enriched rosters and derived team totals

- Status: Complete
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-20](p0-20-seed-and-development-validation.md)
- Gates: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md)

## Outcome

Initial production predictions consume headed v2 roster documents enriched by
the exact audited DuckDB artifact. Every team roster also ends with one derived
known-player-value subtotal, while v1 historical snapshots remain strictly
reconstructable and later no-DuckDB profile runs preserve the enriched
last-known-good data.

## Work items

- [x] Record the v2, subtotal, historical-v1, artifact-pin, coverage-floor, and
  P1-05 boundary in ADR-0050.
- [x] Append exactly one `Team Accumulated` row after every team's players in
  per-team and aggregate roster CSVs without adding a membership enum role.
- [x] Use the sum of positive known player values; render `N/A` when none are
  known and keep partial coverage visible in `team-squad-summary`.
- [x] Emit v2 for new publications and reconstruct exact headed v1 and v2
  snapshots under separate canonical byte contracts.
- [x] Reject missing, duplicate, misplaced, malformed, and incorrect v2 derived
  rows, including non-`N/A` irrelevant fields.
- [x] Add an opt-in local artifact SHA-256 check and the audited 18-team launch
  floors of 464 ages, 464 positions, and 450 valuations before publication.
- [x] Correct the launch path with an explicit paired enrichment-overlay mode:
  preserve authoritative seed/LKG membership, overlay only exact stable-ID
  supplemental fields, never evaluate/adopt historical DuckDB membership, and
  gate the strictly reconstructed final v2 bytes before any write.
- [x] Prove later collection without DuckDB selects the enriched same-date
  last-known-good snapshot without losing supplemental values.
- [x] Keep generic competition-profile CI free of a machine-local DuckDB path;
  leave acquisition, refresh, diffing, and automatic adoption to P1-05.
- [x] After integration and exact-head CI green, publish the pinned enriched v2
  roster snapshot to `ehonda-ai-arena`, then execute the owner-authorized single
  overriding Luna/`none` matchday validation round and inspect payload-safe
  evidence. This is plumbing validation, not P0-23 quality evidence or final
  production selection.
- [x] Carry the fail-closed precondition into P0-21: before any initial
  production prediction, publish the same hash- and coverage-gated enriched v2
  snapshot to that production community and inspect its headed
  snapshot/summary. This records a future P0-21 gate; it does not claim the
  production publication happened or grant authority to post a prediction or
  activate a schedule.

## Implementation evidence — 2026-08-26

- `BundesligaRosterCsv` emits CRLF, a final terminator, deterministic member
  order, and one final derived row. The same known-value function drives the
  row and squad-summary total; unknown remains `N/A`.
- `BundesligaRosterPublication` writes v2 and dispatches strict reconstruction
  between immutable v1 bytes and v2 semantics. Derived rows never enter member
  metadata or quality counts.
- `collect-context rosters` accepts `--duckdb-sha256`,
  `--require-launch-coverage`, and `--launch-enrichment-overlay`. The two launch
  flags are required together; the exact file hash is checked before source
  collection, and strict v2 reconstruction plus 18-team/derived-row/coverage
  validation occurs before a Firestore request is constructed.
- The first live arena command on exact main
  `517db42ce66cb9554848230e176e104ddc87bb64` published snapshot
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
  A payload-safe read-only reconstruction reproduced that hash and proved the
  selected LastKnownGood membership was enriched to 464/464/450. Firestore and
  one collector OTLP trace were the only side effects; no prediction dispatch,
  model call, Kicktipp prediction, or schedule action occurred. ADR-0051 records
  why the corrected explicit launch mode is still required before live retry.
- The final corrective focused validation passes 15/15 Core publication tests,
  36/36 roster-source tests, and 9/9 roster-command tests. It covers exact
  fixture bytes, CRLF/final terminator, order, partial and wholly unknown
  subtotals, aggregate reuse/summary, historical v1, current v2, six derived-row
  corruption classes, explicit overlay provenance, exact 18-team membership
  preservation, no DuckDB membership evaluation, 464/464/450 enrichment,
  paired launch flags, strict final reconstruction, and negative no-write
  behavior.
- The exact headed-v2 Firebase bonus-context fixture retains the existing
  2/2/3/3/2 document counts and category-specific content sets. The roster-aware
  TopScorer and Coach selections now measure 4,506 UTF-8 bytes / 1,127 estimated
  tokens after their derived subtotal row, still far below the default
  20-document / 32,000-token budget. The focused regression passes 1/1 and the
  full FirebaseAdapter suite passes 292/292.
- Final full validation passes Core 297/297, FirebaseAdapter 292/292, and
  Orchestrator 1,117/1,117, including all Docker-backed fixtures. The full
  Release solution build succeeds with zero errors. Existing
  NU1903/nullable/obsolete warnings remain unchanged.
- No collector, Firestore, Kicktipp, Langfuse, GitHub dispatch, or model command
  was executed by the implementation lane.

## Live arena validation evidence — 2026-08-26

- The explicit corrected republish ran from exact-green main
  `f1cfddeb6e2f7ba376856c0843a196af104b9a5c`. Its strictly reconstructed final
  gate passed all 18 teams, all 18 final derived rows, 464 known ages, 464 known
  positions, and 450 valued players. The headed last-known-good and rendered
  target were both
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
  and publication disposition was `Unchanged`.
- The first implicit publication had already produced that same enriched
  snapshot and one collector OTLP trace. The explicit corrected republish added
  one collector OTLP trace but made no second Firestore content change. These
  are context-publication effects only.
- The pre-dispatch exact-identity verifier passed 9/9. The payload-safe metadata
  inventory contained exactly one match row for
  `gpt-5.6-luna`/`none`/cap-`10000`/hosted-match-v2-`production`, with nine
  records at prediction index 0, none at index 1 or higher, and no bonus row.
- Exactly one authorized workflow dispatch ran:
  [Actions run 32917812259](https://github.com/ehonda/KicktippAi/actions/runs/32917812259),
  exact head `f1cfddeb6e2f7ba376856c0843a196af104b9a5c`, job `98025095214`.
  It completed successfully in 5m06s. The workflow's internal pre-verification
  expectedly exited 1 under `continue-on-error` because the existing index-0
  predictions were outdated against the newly selected roster snapshot;
  generation/posting and final verification succeeded, the success-notification
  step was skipped, and the job and summary remained successful.
- Post-dispatch verification with `--check-outdated` passed 9/9. The post-run
  inventory still contained exactly nine records at index 0 and none at index 1
  or higher: one replacement round, no appended reprediction.
- Payload-safe Langfuse inspection identified exact trace
  `3c2814f7b2b6200f3cf4e4bab94d772e` in environment `production`, session
  `matchday-1-ehonda-ai-arena`. It contains one root span and nine ordered
  generations in the exact match order below. All generations used
  `gpt-5.6-luna`, reasoning `none`, cap `10000`, hosted prompt v2 label
  `production`, prompt hash
  `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`,
  Flex service, index 0, no fallback, no error/status failure, and roster
  snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
  Machine totals were 39,228 input tokens, 153 output tokens, and USD
  0.0040146. This is arena plumbing cost only and is excluded from P0-23's USD
  30 experiment ledger.
- In-memory, no-payload roster inspection found 18 unique roster documents.
  Every document had non-`N/A` age, position, and value coverage and exactly one
  valid final `Team Accumulated` row; aggregate coverage was 464/464/450.
  FC Bayern München had 25/25/25 known age/position/value rows and VfB Stuttgart
  had 33/33/33. `validationErrors` was empty. No prompt text or prediction
  payload was retained.
- This consumed the authorization for one arena-only replacement round. It is
  not a production-community post, production schedule, P0-23 quality result,
  final model selection, or authority to repeat the round.

## Executed post-integration arena validation ladder

The following ladder was executed exactly once from the clean primary checkout
at the exact green `main` head and remains as the audit contract. Its authority
is consumed: do not rerun these historical reproduction commands without new
owner authorization. The ladder first verified that the artifact hash equals
`808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c`
and the sibling `.env.ehonda-ai-arena` exists without printing its values.
Publish the roster first:

```powershell
dotnet run --project src/Orchestrator --configuration Release -- collect-context rosters --competition bundesliga-2026-27 --community-context ehonda-ai-arena --duckdb-path .tmp/buli-2026-27-research/transfermarkt-datasets.duckdb --duckdb-revision 154367dfa6d6eb0b86332e332f9df0a080c7ddce --duckdb-snapshot-date 2026-08-13 --duckdb-sha256 808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c --require-launch-coverage --launch-enrichment-overlay --verbose
```

The command must report `NotEvaluated` DuckDB membership gates and the stable
`LAUNCH_ENRICHMENT_OVERLAY` diagnostic for all 18 teams. Any evaluated/rejected
DuckDB membership result, retained-LKG disposition, or missing overlay
diagnostic is a stop condition. Capture the published snapshot ID, previous
snapshot ID, disposition, and
per-team/aggregate/summary document versions without content payloads. The
strictly reconstructed final publication must be v2, show exactly 18 derived
rows, coverage totals of at least 464/464/450, and the expected partial subtotal
for teams with missing valuations.

Before dispatch, run the existing exact-identity verifier and a read-only cost
inventory. Keep the inventory JSON under ignored `.tmp/`; it contains metadata
and cost/count aggregates, not prediction or prompt payloads:

```powershell
dotnet run --no-build --project src/Orchestrator --configuration Release -- verify gpt-5.6-luna --community ehonda-ai-arena --community-context ehonda-ai-arena --competition bundesliga-2026-27 --reasoning-effort none --max-output-tokens 10000 --prompt-source langfuse --langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match --langfuse-prompt-label production --langfuse-prompt-version 2 --verbose --agent
dotnet run --no-build --project src/Orchestrator --configuration Release -- cost --matchdays 1 --models gpt-5.6-luna --reasoning-efforts none --community-contexts ehonda-ai-arena --detailed-breakdown --output-json .tmp/p0-25-arena-pre-dispatch-cost.json
```

This is a fail-closed pre-dispatch gate. The exact identity verifier must find
the expected nine matchday-one identities in Firestore and Kicktipp: FC Bayern
München–VfB Stuttgart, 1. FC Köln–1899 Hoffenheim, SV Elversberg–Bayer 04
Leverkusen, FSV Mainz 05–SC Paderborn 07, 1. FC Union Berlin–Eintracht
Frankfurt, RB Leipzig–Bor. Mönchengladbach, Borussia Dortmund–Hamburger SV, SC
Freiburg–Werder Bremen, and FC Augsburg–FC Schalke 04. The inventory must
contain exactly one match row for the complete
`gpt-5.6-luna`/`none`/cap-`10000`/hosted-match-v2-`production` configuration,
with count 9 at prediction index 0 and zero records at index 1 or higher. Stop
without dispatching on a missing, extra, differently configured, or differently
indexed record.

Only after that gate passes, dispatch exactly once:

```powershell
gh workflow run buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml --ref main -f force_prediction=true -f max_repredictions=0
```

`force_prediction=true` selects the reusable workflow's
`--override-database` path: it overwrites/replaces prediction index 0 instead
of appending a reprediction. `max_repredictions` is ignored on this forced
path; the explicit `0` documents that no reprediction allocation is intended.
Confirm the dispatched run's `headSha` is the exact green main SHA and watch it
to success. Do not retry or dispatch a second round if it fails; stop and
reconcile the evidence.

After success, rerun the same exact-identity verifier and cost inventory, using
`.tmp/p0-25-arena-post-dispatch-cost.json` for the latter. The post-inventory
must still contain exactly the nine expected match records at index 0 and zero
records at index 1 or higher. Inspect only payload-safe Langfuse
trace/observation fields: environment, community/context, competition, model,
reasoning, cap, prompt name/label/version/hash, fallback, roster snapshot ID,
selected document names, usage/cost, errors, and ordered generation count. The
single dispatched workflow and its single replacement trace round must contain
exactly nine ordered match generations, and the trace's roster snapshot ID must
equal the exact enriched snapshot ID captured after publication. Confirm every
relevant `roster-*` prompt document has non-`N/A` age/position/value coverage
and one final `Team Accumulated` row; do not record prompt text or prediction
payloads. The whole ladder is arena-only plumbing validation: it authorizes no
production community post, production schedule, retry, or P0-23
quality-evidence claim.

## Complete when

- [x] The implementation commit is independently approved, integrated, and exact-head CI is green.
- [x] The arena ladder above records successful enriched v2 publication, the passing pre-dispatch index gate, exactly one authorized index-0 replacement round, the unchanged nine-at-index-0/zero-at-index-1+ post-state, exact enriched roster snapshot identity, and payload-safe trace/document evidence.
- [x] P0-21 carries the enriched-publication precondition for every initial production prediction while P1-05 retains the refresh-automation boundary.
