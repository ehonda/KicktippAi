# P1-14 — History source continuity

- Status: In progress — exact source repair complete pending reviewed release; proxy and full-season recovery deferred
- Outcome: retain exact selected-history dates through rolling Kicktipp history changes without widening live collection authority.
- Depends on: [ADR-0066](../decisions/0066-refresh-history-source-checkpoint.md)

## Evidence

- 2026-09-04 OpenLigaDB `dfb/2026` capture: 32 completed fixtures, 77,825 bytes, SHA-256 `728d31be6f928fa83cff7bf56925d0456642686a35bf02fb43e428ebd3ce81eb`.
- Authenticated read-only 3+4+2 export: exact 54 names, 390 completed rows, 42 incomplete exclusions, and the four new BVB/FCB DFB occurrences.
- Accumulated fixed evidence is 434 rows / 214 matches; rolling inventory is intentionally not an equality gate.

## Deferred follow-up

- Define and review proxy-date continuity and the closed-input historical full-season collector separately. Do not alter the selector/parser in this repair.
