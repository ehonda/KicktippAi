# P0-22 — Reconstruct exact played dates in history context

- Status: Complete
- Priority: P0
- Depends on: [P0-02](p0-02-competition-scoped-storage.md), [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0025](../decisions/0025-reconstruct-bundesliga-history-played-dates.md), [ADR-0026](../decisions/0026-exclude-incomplete-history-rows.md), [ADR-0027](../decisions/0027-add-openfootball-for-second-bundesliga-history.md), [ADR-0028](../decisions/0028-capture-openligadb-second-bundesliga-history.md), [ADR-0029](../decisions/0029-capture-openligadb-dfb-pokal-final.md), [ADR-0030](../decisions/0030-use-uefa-match-record-for-europa-league-final.md), [ADR-0031](../decisions/0031-correct-dfb-pokal-final-inventory-coverage.md), [ADR-0032](../decisions/0032-freeze-complete-history-set-and-publish-atomically.md), [ADR-0035](../decisions/0035-freeze-first-live-dfb-history-completion.md), [ADR-0041](../decisions/0041-freeze-completed-dfb-first-round-history-transition.md), and [ADR-0044](../decisions/0044-select-canonical-preseason-history-sources.md)

## Outcome

Every row selected from Bundesliga `recent-history-*`, `home-history-*`, and `away-history-*` context has the exact played date and auditable provenance. Bundesliga fixtures use the competition's Kicktipp schedule/results; DFB-Pokal, UEFA, friendly, and other intervening fixtures use an accepted external source rather than an inferred Bundesliga date. Existing dated head-to-head documents remain unchanged.

## Work items

- [x] Inventory the exact live history schemas and every place that creates, stores, reconstructs, or selects recent, home, away, and head-to-head history.
- [x] Record an ADR that fixes the played-date source hierarchy, source/license requirements, canonical match identity, duplicate handling, ambiguity behavior, and last-known-good semantics. Evaluate official competition sources and the existing source-revision-pinned DuckDB data before adding a new dependency.
- [x] Generalize the proven WM26 date-map/parser/application seams without changing WM26 output contracts or treating its competition-specific map as Bundesliga data.
- [x] Check in a deterministic, source-attributed preseason map for all selected historical Bundesliga club rows that do not already carry an exact played date.
- [x] For completed 2026/27 Bundesliga fixtures, resolve the date from the exact competition-scoped Kicktipp match schedule/result stored by collection, keyed by canonical manifest identities and match identity rather than fuzzy text matching.
- [x] Resolve intervening DFB-Pokal, UEFA, friendly, and other non-Bundesliga fixtures from the accepted external source, retaining source name, URL or immutable dataset revision, verification time, and competition identity.
- [x] Preserve a valid existing `Played_At`; never substitute `Data_Collected_At`, context collection time, matchday order, or a guessed date.
- [x] Add dry-run inventory/export, apply, and strict audit modes. Ambiguous, conflicting, or unresolved selected rows must retain the last complete documents and fail the production collection gate with actionable diagnostics.
- [x] Apply the step only to `recent-history-*`, `home-history-*`, and `away-history-*`; prove `head-to-head-*` bytes and dates are not rewritten.
- [x] Expose a typed, competition-scoped collector step for P0-14 to run immediately after Kicktipp history collection; P0-18 then carries that profile step into reusable workflows.
- [x] Emit trace/console evidence for exact dates by source class, preserved dates, unresolved rows, conflicts, affected documents, and the map/source revision.
- [x] Add parser, identity, idempotency, duplicate, conflict, missing-source, last-known-good, competition-isolation, CRLF, and WM26 regression tests.

## Validation

- Reconstruct representative preseason recent/home/away documents and prove every row has an exact source-attributed `Played_At`.
- Reconstruct one completed 2026/27 Bundesliga fixture from the Kicktipp schedule and one intervening cup/UEFA fixture from the accepted external source.
- Prove ambiguous same-team/score cases fail rather than receiving a guessed date, repeated application is byte-stable, and head-to-head content is unchanged.
- Run the affected Core, context-provider, Firebase adapter, Orchestrator, and WM26 history-date test trees.
- During P0-20, run the strict audit against every live selected history document and record a zero-unresolved result before prediction validation.

## Evidence

- Read-only authenticated Kicktipp export for `ehonda-dev-buli-2627`, exact requested matchdays 1 and 2: all 54 manifest prefix documents were nonempty; 432 raw rows yielded 398 completed rows after 34 incomplete scheduled rows were excluded before ordinal assignment.
- Preseason deterministic map: 398/398 inventory rows and 196 unique matches. Transfermarkt covered 326 rows / 152 matches, captured OpenLigaDB snapshots covered 70 rows / 43 matches, and the official UEFA match record covered 2 rows / 1 match. No new identity, source gap, or source conflict occurred during the 18-document expansion.
- The read-only preseason inventory scratch artifact SHA-256 was `ECC9A7FE9F0EE92BF119066A8A24C6345F8FC1C906A2D60EB636433B1DC5DB2E`; the preseason canonical map SHA-256 was `FD97CE0DBD218C1BB4DAA9B60D5132C11C3E00CE1CB0C121D15BEA92AF9DDD8E`.
- Live fail-closed evidence on 2026-08-21: an authenticated read-only `ehonda-dev-buli-2627` dry-run performed no writes and stopped after Kicktipp prepended completed DFB-Pokal match `81832` to `away-history-sge.csv` and `recent-history-sge.csv`; the 17:37 preseason map had correctly excluded that fixture before kickoff.
- Frozen live source: the 30,666-byte `dfb/2026` OpenLigaDB checkpoint has SHA-256 `B60D4C1EF214FFA2680EFB27CACE33CC7B47BF9700B4F57E7043736919A8EEAB`, 32 unique fixtures, exactly two completed IDs, and exact match `81832` identity/date/halftime/full-time evidence. Completed match `81848` is outside the 18-team inventory and contributes no map row.
- ADR-0035 deterministic map checkpoint: 400 rows / 197 unique matches. Transfermarkt remains 326 / 152, OpenLigaDB is 72 / 44, and UEFA remains 2 / 1. Match `81832` occurs exactly once at frozen ordinal 1 in each accepted SGE document; their seven prior identities shift intact to 2-8. The map SHA-256 is `9EDA9A437B54286A20CCBBE89A6B8701FD77478B69450E7528EB07320A87F221`.
- Rolling-window resolution uses exact per-document row identity while retaining frozen ordinals as audit provenance. Focused regressions prove a prepended current `1.BL` outcome plus shifted fixed suffix, an off-window ledger row, uncaptured non-league failure, duplicate live and fixed identity failure, and outcome/map date conflict behavior.
- Artifact audit: exact header/order/counts, CRLF with final terminator, source revisions and hashes, source split, DFB identities, attribution/license boundaries, verification timestamps, constants, and secret scan all passed.
- Git checkout-filter audit: the history-map attribute resolves to `text eol=crlf`; index reconstruction produces 401 CRLF-only lines, no bare LF, the header as the first byte, and a final CRLF.
- Focused tests: Core history 20/20 in 1.304s; Orchestrator history/expected-set/apply 24/24 in 3.933s; corrected WM26 history/verbose regressions 15/15 in 3.423s; invalid-matchday pre-write safety 5/5 in 3.230s; Firebase atomic publication 6/6 in 19.736s; real Firestore canonical-set integration 1/1 in 1m04.919s.
- Full affected suites: Core 155/155 in 2.896s; ContextProviders.Kicktipp 47/47 in 1.527s; KicktippIntegration 194/194 in 13.131s; FirebaseAdapter 258/258 in 52.760s; Orchestrator 903/903 in 1m51.138s; Integration 4/4 in 27.439s.
- `dotnet build --no-restore KicktippAi.slnx`: succeeded in 14.32s with 0 errors and 10 existing dependency-advisory/obsolete-API warnings.
- Live-completion follow-up tests: final focused Core history/source `31/31` in `0.926s`; full Core `173/173` in `2.564s`; affected Bundesliga-history and Kicktipp collection commands `49/49` in `4.448s`.
- Live-completion follow-up `dotnet build KicktippAi.slnx --no-restore`: succeeded in `26.30s` with `0` errors; the fresh full build reported `192` existing dependency-advisory, obsolete-API, and nullability warnings.
- P0-20 first-round fail-closed evidence on 2026-08-25: the non-dry current-matchday collector saved no context after reporting 20 unresolved occurrences / 13 DFB fixtures, then skipped history, Club Elo, and rosters. A fresh authenticated read-only matchdays 1+2 export established the complete authority: all 54 documents, 430 completed rows, two excluded incomplete rows, and 30 new occurrences / 15 fixtures. The 31,580-byte inventory SHA-256 is `77324DA1AF184B7F566A743783A3B51B1B00E7D6929E6A85E298F9648CB080B1`.
- ADR-0041 frozen source: the 74,988-byte `dfb/2026` response captured at `2026-08-25T01:45:04+02:00` has SHA-256 `92CA6F8C7175970DB15BBDCEA15CB79F3F2E83CB52A59300CFCF9591760AFFA2`, 32 unique fixtures, exactly 30 completed IDs, one full-time result for every completed match, and exact selected identity checks including match `81843`'s `2:5` after-extra-time result.
- ADR-0041 deterministic map: 430 rows / 212 unique matches; source split 326 / 152 Transfermarkt, 102 / 59 OpenLigaDB, and 2 / 1 UEFA. All prior identities remain, 210 suffix rows shift intact, and the 30 new occurrences are restricted to the exact current inventory. The map SHA-256 is `E341010B4BB0F95FF31009BF904616C825212118D4E3F80D7D2DBCB0F8732492`.
- ADR-0041 validation: exact inventory/map identity diff `0`; 54-document/430-row map bytes are UTF-8 without BOM, CRLF-only with a final terminator, and all 400 prior identities remain. Focused Orchestrator history `4/4`, Kicktipp history-processing `9/9`, and WM26 history `15/15` passed; full Core `253/253` and Orchestrator `1047/1047` passed. After the fresh worktree restored its missing test-project assets, the final `dotnet build KicktippAi.slnx --no-restore` succeeded in `6.64s` with `0` errors and nine existing dependency-advisory warnings.
- ADR-0044 binds these exact 54 documents to deterministic canonical fixture sources and requires all 430 completed resolutions plus the two accepted incomplete exclusions. Its 306 exact-matchday H2H documents remain outside played-date rewriting.
- The original P0-22 validation performed no live Firestore or Kicktipp write; its Firestore checks used the local emulator. The later P0-20 attempt created nine competition-scoped pending matchday-1 outcome rows before the played-date gate stopped all context publication. No prediction or Kicktipp placement ran. The P0-20 activation gate retains the required strict live zero-unresolved audit before prediction validation.

## Complete when

- Every selected recent/home/away history row has an exact played date, source identity, and deterministic match join.
- No collection timestamp or inferred league order is presented as a played date.
- Bundesliga and intervening non-league fixtures follow separate accepted source paths, with ambiguity failing closed and the last complete context remaining visible.
- The typed collector is ready for automatic P0-14/P0-18 profile execution, while WM26 and head-to-head behavior remain unchanged.
