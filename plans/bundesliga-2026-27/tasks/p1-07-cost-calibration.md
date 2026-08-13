# P1-07 — Calibrate whole-season cost from live evidence

- Status: Not started
- Priority: P1
- Depends on: [P1-02](p1-02-question-aware-context.md), [P1-04](p1-04-club-elo-refresh.md), [P1-05](p1-05-roster-refresh.md)

## Outcome

The whole-season estimate uses measured 2026/27 prompt tokens, output tokens, cache behavior, and reprediction rates rather than only pre-launch assumptions.

## Work items

- [ ] Select a representative completed-match sample and evidence window.
- [ ] Collect compact Langfuse usage for the exact deployed model/prompt configuration.
- [ ] Measure match and bonus context sizes by category, including roster-targeting behavior.
- [ ] Calculate observed reprediction rates and separate context-refresh-triggered runs from other causes.
- [ ] Update stored base estimates when the project estimator requires a new model/configuration row.
- [ ] Recompute 306-match season scenarios with documented low/base/high assumptions.
- [ ] Update `docs/experiments/whole-season-cost-estimates.md` and link the underlying evidence/run IDs.
- [ ] Record any configuration change proposed from the result in a new ADR; do not silently change the launch model.

## Validation

- Follow the repository `whole-season-estimates`, `estimate-experiment-cost-skill`, and Langfuse instructions.
- Independently check arithmetic and ensure current price dates are cited.

## Complete when

- The estimate is reproducible from tracked assumptions and trace evidence.
- Pre-launch versus observed deltas and their causes are explicit.
