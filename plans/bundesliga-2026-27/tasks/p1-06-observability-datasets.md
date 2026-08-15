# P1-06 — Generalize observability datasets for 2026/27

- Status: Not started
- Priority: P1
- Depends on: [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-21](p0-21-production-activation.md)
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

Dataset preparation, prompt reconstruction, and experiment helpers accept explicit 2026/27 competition/context metadata and do not assume the old season.

## Work items

- [ ] Inventory hard-coded 2025/26 dataset names, item IDs, season labels, and defaults in observability commands and examples.
- [ ] Require competition and season metadata where implicit inference can select the wrong partition.
- [ ] Build dataset names and item IDs from explicit input rather than a fixed old prefix.
- [ ] Reconstruct the 2026/27 required context contract, including Elo/rosters and excluding transfers.
- [ ] Update current CLI examples and tests to 2026/27; retain old literals only where a fixture deliberately tests generic historical parsing.
- [ ] Prepare and sync a low-cost 2026/27 smoke slice after completed outcomes exist.
- [ ] Verify trace linkage, prompt identity, and Kicktipp scoring on the smoke run.

## Validation

- Run all observability command and prompt-reconstruction tests.
- Use the repository Langfuse experiment workflow for the smoke slice and record dataset/run/trace IDs.

## Complete when

- No current observability command silently defaults to 2025/26.
- A 2026/27 item reconstructs the same required document names used by live prediction.
- Supporting historical experiments would require explicit caller configuration, not live compatibility code.
