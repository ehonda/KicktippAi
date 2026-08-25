# P0-23 — Collect GPT-5.6 production-candidate evidence

- Status: In progress — no-spend preregistration drafted; owner matrix and both spend gates pending
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md), [P0-12](p0-12-match-context-and-transfer-retirement.md), and [P0-20](p0-20-seed-and-development-validation.md)
- Reuses: the completed cost/provenance foundation recorded in [P0-06](p0-06-model-ledger-and-cost-baseline.md)
- Gates: the final owner-selection item in [P0-06](p0-06-model-ledger-and-cost-baseline.md)
- Decisions: [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0040](../decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md), [ADR-0043](../decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md), [ADR-0046](../decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md)

## Outcome

The owner can compare an explicitly authorized set of GPT-5.6 production candidates using reproducible cost evidence and a separate cutoff-safe Bundesliga 2025/26 quality comparison, or can explicitly waive missing comparative evidence before selecting the launch configuration.

The already accepted hosted prompt identities remain match version 2 and bonus version 1. This task does not reopen or silently move either prompt.

The current no-spend design is
[the GPT-5.6 Bundesliga 2025/26 production-candidate preregistration](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-preregistration.md).
It proposes Sol and Terra at their documented default `medium` effort and cap
`10000`, reuses the exact Luna/`none` row, and keeps all owner, spend, execution,
and evidence boxes below open.

## Owner and spend gate

- [ ] Obtain the owner's exact candidate matrix: model IDs, reasoning efforts, maximum output-token caps, and any configurations intentionally excluded.
- [ ] Obtain explicit authorization and a maximum spend for the cost phase and, separately, the later quality phase. No prompt, dataset, Langfuse, or model mutation may begin before the applicable phase is authorized.
- [ ] Record whether the owner requires comparative quality evidence before selection or explicitly waives it. A waiver must state its rationale and accepted risk in the final production-selection ADR; silence is not a waiver.

## Cutoff and provenance contract

- [ ] Verify each candidate's published model knowledge cutoff and current pricing from primary sources immediately before preparing evidence. Derive and record a separate exact eligible window for every candidate rather than inheriting Luna's dates.
- [ ] Derive the sampling boundary as the exact Europe/Berlin local midnight two calendar days after the candidate's published cutoff. The historical compatibility contract admits only Bundesliga 2025/26 fixtures starting strictly after that boundary; do not weaken the margin, shift the boundary to gain fixtures, or substitute an earlier manifest.
- [ ] For Luna, preserve the already-proven contract exactly: official cutoff `2026-02-16`, sampling boundary `2026-02-18T00:00:00+01:00`, and only fixtures strictly after that instant. The completed Luna one-item and five-by-four samples already satisfied this rule and do not need another model run.
- [ ] Fail before spend if the cutoff-safe, completed, exact-context pool cannot support the declared sample. Do not retry seeds or silently change the candidate list, fixture count, repetitions, or comparison design.
- [ ] Use only the explicit `bundesliga-2025-26-legacy-id-hash-v1` read-only compatibility route and hosted `kicktippai/bundesliga-2026-27/predict-one-match` version 2 with required `production` membership.
- [ ] Bind the official and sampling cutoffs, eligibility policy/count/hash, seed, selected fixture IDs/hash, completed scores, evaluation time, all seven exact context-document versions/content hashes, prompt identity, model identity, reasoning, cap, and dataset/run/trace linkage in the prepared and reported provenance.

## Cost evidence — phase 1

- [ ] For every owner-authorized candidate without an authoritative matching row, follow the repository estimate-row process: verify pricing, run the prescribed one-item preflight, inspect cap/tier/fallback/cost behavior, then run the exact five-fixture by four-repetition base sample only when the preflight is healthy.
- [ ] Collect compact usage by immutable Langfuse dataset-run binding, upsert the authoritative row, and run the repository estimator for 306 and 493 match-prediction calls. Do not retain prompt, context, or prediction payloads.
- [ ] Label every historical seven-document result as a preseason cost proxy that may understate the live eleven-document Bundesliga 2026/27 input. Cost evidence is not prediction-quality evidence.

## Quality evidence — phase 2

- [ ] Pre-register the comparison metrics, aggregation, paired/common-manifest rule, repetition policy, and failure handling before any quality run. Apply the hardest candidate cutoff to a shared paired sample when candidates are compared directly.
- [ ] Run only the owner-authorized candidates against completed, cutoff-safe Bundesliga 2025/26 outcomes using exact prepared manifests and immutable run binding. Keep model/prompt settings equal to their declared production candidates.
- [ ] Publish complete comparable results, including failures and uncertainty, without selecting the production model. Never infer quality from cost, token count, output length, plumbing success, or the Luna validation ladder.
- [ ] If a cutoff-safe paired comparison is impossible, fail closed and return the exact eligible-pool evidence to the owner; do not manufacture comparability from different or pre-cutoff samples.

## Production-selection boundary

- [ ] Do not fill `production-primary`, admit an arena challenger, create its model-bound production workflow, or activate production based only on the Luna plumbing row.
- [ ] After the comparative evidence is complete, or after the owner records an explicit waiver, hand the exact cost and quality evidence to P0-06 for the owner decision and new Accepted ADR.
- [ ] Carry the already accepted match v2 and bonus v1 prompt identities into the candidate ledger unless a separately reviewed successor ADR deliberately changes them.

## Validation

- Validate prepared manifests, exact dataset-run linkage, estimator rows, and reports with the repository experiment-cost and Langfuse experiment tooling.
- Verify cutoff boundaries directly, including exclusion at the exact cutoff and admission only strictly afterward.
- Reconcile every reported run to its exact manifest, model configuration, prompt version, dataset, dataset-run, traces, usage, scores, and tracked evidence.
- Run focused and affected full automated suites for any implementation required by the evidence workflow, then obtain independent review before live spend and before integration.

## Evidence — 2026-08-25 no-spend preregistration

- Official OpenAI model pages were checked for `gpt-5.6-sol`,
  `gpt-5.6-terra`, and `gpt-5.6-luna`; all currently publish knowledge cutoff
  `2026-02-16` and `medium` as default reasoning effort.
- Official Standard and Flex short-context prices were recorded with a dated
  source link in the preregistration. The repository cost calculator already
  recognizes the explicit Sol and Terra model IDs.
- The provisional hardest-cutoff boundary is therefore the already-audited
  strict `2026-02-18T00:00:00 Europe/Berlin (+01)` boundary with eligible pool
  count `109` and hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`.
- The cost design freezes one-item then exact five-by-four evidence for every
  missing candidate row and reuses Luna without another prediction. The quality
  section remains explicitly DRAFT at 15 fixtures by 20 repetitions on one
  common manifest, pending the owner's exact matrix and separate spend ceiling.
- No dataset was prepared or synced; no Langfuse or prompt mutation, model call,
  or spend occurred while drafting this evidence.

## Complete when

- Every owner-authorized production candidate has comparable, reproducible cost evidence and the required cutoff-safe quality evidence, or the final owner ADR explicitly records the waived evidence and risk.
- Cost and quality claims remain visibly separate and traceable to exact immutable provenance.
- P0-06 can record the production decision without silently inheriting Luna/none or an old prompt/context route.
