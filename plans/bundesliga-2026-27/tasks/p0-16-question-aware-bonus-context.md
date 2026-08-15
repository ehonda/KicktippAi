# P0-16 — Add question-aware bonus context budgeting

- Status: Not started
- Priority: P0
- Depends on: [P0-13](p0-13-bonus-context-baseline.md), [P0-15](p0-15-context-document-hygiene.md)
- Decision: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md)

## Outcome

Before the one-time pre-season bonus predictions are generated, bonus context selection is explicit by question category, explainable in traces, and bounded so full roster data is loaded only when it adds signal.

## Work items

- [ ] Define supported question categories and their document sets in an ADR-backed policy table.
- [ ] Separate deterministic question matching from document retrieval and give unknown questions a safe, documented baseline.
- [ ] Select only referenced/team-relevant `roster-*` documents; never use `team-rosters` as an unconditional fallback.
- [ ] Add configurable document/token budgets and log exclusions with reasons.
- [ ] Attach category, selected documents, and estimated context size to trace metadata.
- [ ] Add multilingual/wording variants and false-positive tests for each category.
- [ ] Measure prediction/context behavior on a fixed representative bonus-question set before rollout.

## Validation

- Run KPI provider, bonus prompt, and telemetry tests.
- Compare fixed-set token counts and document selections against the P0 baseline.

## Complete when

- Every supported category has deterministic tests and trace-visible routing.
- Unknown questions remain useful without loading all rosters.
- The fixed-set context footprint is no larger than P0 without an accepted quality reason.
- This task is complete before any production bonus workflow can run.
