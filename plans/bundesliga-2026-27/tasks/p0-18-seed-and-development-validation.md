# P0-18 — Seed and validate in development

- Status: Not started
- Priority: P0
- Depends on: P0-02 through P0-17 and every copied community-triad task

## Outcome

The new Firestore partition and prompt paths complete one representative matchday and bonus cycle in the safe development community with trace evidence.

## Work items

- [ ] Run the full profile in dry-run and resolve every manifest, source, and quality warning.
- [ ] Collect Kicktipp rules, standings, histories, outcomes, Club Elo, rosters, and squad summaries into `bundesliga-2026-27`.
- [ ] Query/inspect stored identities and prove no old unscoped Bundesliga or WM26 document satisfied the run.
- [ ] Verify all CSVs render header-first, deterministic, CRLF-terminated content with a final terminator.
- [ ] Run a development prediction cycle covering a complete nine-match matchday and representative champion/relegation/top-scorer/coach bonus questions.
- [ ] Inspect Langfuse traces for selected document names, prompt version, model identity, reasoning, token usage, and cost anomalies.
- [ ] Test missing roster, partial Elo, and eight-of-nine outcomes to confirm launch gates fail safely.
- [ ] Record commands, trace IDs, document versions, timestamps, and results in the task's validation evidence section.

## Validation evidence

Not run yet.

## Complete when

- Every P0 launch gate except production manual execution/scheduling has direct evidence.
- Fail-closed scenarios preserve the last complete context and prevent prediction.
- No production schedule has been enabled.
