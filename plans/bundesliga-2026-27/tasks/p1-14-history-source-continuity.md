# P1-14 — History source continuity

- Status: Complete — exact source repair, bounded proxy continuity, and maintenance reporting are integrated; full-season capture redesign remains deferred
- Outcome: retain exact selected-history provenance through rolling Kicktipp history changes without widening live collection authority.
- Depends on: [ADR-0066](../decisions/0066-refresh-history-source-checkpoint.md), [ADR-0067](../decisions/0067-tolerate-unresolved-external-history-dates.md), and [ADR-0072](../decisions/0072-operate-history-date-maintenance-manually.md)

## Evidence

- 2026-09-04 OpenLigaDB `dfb/2026` capture: 32 completed fixtures, 77,825 bytes, SHA-256 `728d31be6f928fa83cff7bf56925d0456642686a35bf02fb43e428ebd3ce81eb`.
- Authenticated read-only 3+4+2 export: exact 54 names, 390 completed rows, 42 incomplete exclusions, and the four new BVB/FCB DFB occurrences.
- Accumulated fixed evidence is 434 rows / 214 matches; rolling inventory is intentionally not an equality gate.
- Exact source commits `cd8408c` and `f4fb722` were integrated to `main`; [Build-and-Test run 33930255667](https://github.com/ehonda/KicktippAi/actions/runs/33930255667) passed all 12 jobs.
- Natural production runs [33939145345](https://github.com/ehonda/KicktippAi/actions/runs/33939145345) and [33957672697](https://github.com/ehonda/KicktippAi/actions/runs/33957672697) both completed all 16 jobs. The first at `1ca8dbc` recorded context history `256` (existing `252`, Kicktipp `0`, fixed-map `4`, proxy `0`, incomplete `0`); the second confirmed the same natural topology.

## Runtime validation

- The `ehonda-dev-buli-2627` ordinary dry run failed only at the history gate because its community-partitioned current `1.BL` outcomes were stale; it is not production evidence and performed no business, model, or storage write.
- The production-equivalent `pes-squad` ordinary profile dry run passed: 256 completed history rows (`16` outcome-backed, `240` fixed-map), zero incomplete exclusions, and Club Elo plus Rosters dry-run validation. It performed no business, model, prediction, context, or outcome write. The normal diagnostic OTLP trace exported successfully.
- ADR-0072 adds typed post-publication coverage reporting and the metadata-only weekly reminder. A proxy is visible as maintenance work, never promoted to an exact date or refreshed at runtime.
- Maintenance-reporting validation: Release `KicktippAi.slnx` build passed; focused profile/normal/history/Core-history suites passed `13/13`, `26/26`, `5/5`, and `32/32`; full Orchestrator and Core suites passed `1227/1227` and `309/309`. The workflow-contract gate, including hostile summary simulation, and local native `actionlint` 1.7.12 for the new and all workflows passed.

## Deferred follow-up

- The closed-input historical full-season collector and CL-specific context remain separate. Do not alter the selector/parser in this work.
