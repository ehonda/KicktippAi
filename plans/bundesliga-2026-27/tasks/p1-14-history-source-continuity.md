# P1-14 — History source continuity

- Status: In progress — exact source repair integrated; collection-date proxy continuity pending reviewed release
- Outcome: retain exact selected-history provenance through rolling Kicktipp history changes without widening live collection authority.
- Depends on: [ADR-0066](../decisions/0066-refresh-history-source-checkpoint.md) and [ADR-0067](../decisions/0067-tolerate-unresolved-external-history-dates.md)

## Evidence

- 2026-09-04 OpenLigaDB `dfb/2026` capture: 32 completed fixtures, 77,825 bytes, SHA-256 `728d31be6f928fa83cff7bf56925d0456642686a35bf02fb43e428ebd3ce81eb`.
- Authenticated read-only 3+4+2 export: exact 54 names, 390 completed rows, 42 incomplete exclusions, and the four new BVB/FCB DFB occurrences.
- Accumulated fixed evidence is 434 rows / 214 matches; rolling inventory is intentionally not an equality gate.
- Exact source commits `cd8408c` and `f4fb722` were integrated to `main`; [Build-and-Test run 33930255667](https://github.com/ehonda/KicktippAi/actions/runs/33930255667) passed all 12 jobs. Natural production-run proof remains pending.

## Runtime validation

- The `ehonda-dev-buli-2627` ordinary dry run failed only at the history gate because its community-partitioned current `1.BL` outcomes were stale; it is not production evidence and performed no business, model, or storage write.
- The production-equivalent `pes-squad` ordinary profile dry run passed: 256 completed history rows (`16` outcome-backed, `240` fixed-map), zero incomplete exclusions, and Club Elo plus Rosters dry-run validation. It performed no business, model, prediction, context, or outcome write. The normal diagnostic OTLP trace exported successfully.

## Deferred follow-up

- The closed-input historical full-season collector and CL-specific context remain separate. Do not alter the selector/parser in this work.
