# P0-20 — Seed and validate in development and the arena

- Status: Not started
- Priority: P0
- Depends on: P0-02 through P0-18 and the Luna/none development/arena entrypoints copied from P0-19; final production P0-19 copies may remain gated on P0-06
- Decisions: [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0012](../decisions/0012-competition-aware-matchday-completion.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md)

## Outcome

The new Firestore partition and prompt paths complete representative matchday and bonus cycles in the safe development community, then pass the authorized local, `workflow_dispatch`, and scheduled arena ladder with trace evidence.

## Work items

- [ ] Run the full profile in dry-run and resolve every manifest, source, and quality warning.
- [ ] Collect Kicktipp rules, standings, histories, outcomes, Club Elo, rosters, and squad summaries into `bundesliga-2026-27`.
- [ ] Query/inspect stored identities and prove no old unscoped Bundesliga or WM26 document satisfied the run.
- [ ] Verify all CSVs render header-first, deterministic, CRLF-terminated content with a final terminator.
- [ ] Run a development prediction cycle covering a complete nine-match matchday and representative champion/relegation/top-scorer/coach bonus questions.
- [ ] Use `gpt-5.6-luna` with `none` reasoning for autonomous dev and arena plumbing validation; do not judge or promote its prediction quality.
- [ ] Using the owner-confirmed arena setup, validate the same cheap configuration through local CLI, `workflow_dispatch`, and an arena-only schedule.
- [ ] Verify the owner-confirmed Firebase, OpenAI, Langfuse, Kicktipp, and workflow credentials by connectivity and behavior without displaying secret values.
- [ ] Inspect Langfuse traces for selected document names, prompt version, model identity, reasoning, token usage, and cost anomalies.
- [ ] Test missing roster, partial Elo, and eight-of-nine outcomes to confirm launch gates fail safely.
- [ ] Record commands, trace IDs, document versions, timestamps, and results in the task's validation evidence section.

## Validation evidence

Not run yet.

## Complete when

- Every P0 launch gate except final production-model execution/scheduling has direct evidence.
- Fail-closed scenarios preserve the last complete context and prevent prediction.
- No final production-model schedule has been enabled; only the explicitly authorized Luna/none arena validation schedule may have run.
