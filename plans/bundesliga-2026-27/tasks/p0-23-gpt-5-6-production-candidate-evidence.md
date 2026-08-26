# P0-23 — Collect GPT-5.6 production-candidate evidence

- Status: In progress — live cost execution is paused after the reviewed Luna/`max` cap-`20000` one-item remediation succeeded with healthy headroom on Flex; cap `20000` is selected for a future five-by-four row, but no row was upserted and no further paid call is admitted until a new reviewed checkpoint, integration, explicit push, exact-head green CI, ledger reconciliation, and fresh Decimal admission
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md), [P0-12](p0-12-match-context-and-transfer-retirement.md), and [P0-20](p0-20-seed-and-development-validation.md)
- Reuses: the completed cost/provenance foundation recorded in [P0-06](p0-06-model-ledger-and-cost-baseline.md)
- Gates: the final owner-selection item in [P0-06](p0-06-model-ledger-and-cost-baseline.md)
- Decisions: [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0040](../decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md), [ADR-0043](../decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md), [ADR-0046](../decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md), [ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)

## Outcome

The owner can compare an explicitly authorized set of GPT-5.6 production candidates using reproducible cost evidence and a separate cutoff-safe Bundesliga 2025/26 quality comparison, or can explicitly waive missing comparative evidence before selecting the launch configuration.

The already accepted hosted prompt identities remain match version 2 and bonus version 1. This task does not reopen or silently move either prompt.

The current no-spend design is
[the GPT-5.6 Bundesliga 2025/26 production-candidate preregistration](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-preregistration.md).
It replaces the superseded Terra/`medium`, Sol/`medium`, cap-`10000`, and
fixed-`15 × 20` example with the owner's exact matrix: Sol `high` / `medium` /
`none`, Terra `xhigh` / `medium` / `none`, and Luna `max` / `medium` / `none`.
Output caps are derived by the estimate-row preflight process, never guessed.
The quality topology is selected adaptively from exact rows while preserving a
meaningful paired minimum. The exact Luna/`none` row remains reusable without a
rerun.

## Owner and spend gate

- [x] Record the owner's exact matrix: `gpt-5.6-sol` at `high`, `medium`, and `none`; `gpt-5.6-terra` at `xhigh`, `medium`, and `none`; and `gpt-5.6-luna` at `max`, `medium`, and `none`. Caps follow the estimate-row preflight policy, and quality topology is chosen adaptively only after exact rows exist.
- [x] Record explicit authorization for cost-row and quality runs under one cumulative USD 30 ceiling. This owner amendment supersedes the handoff's earlier separate fixed phase-budget template. USD 30 is a stop ceiling, not a target; cheaper completion remains preferred.
- [x] Record that comparative quality evidence is required for the production and arena decisions. If the full matrix cannot support the preregistered meaningful minimum inside the remaining ceiling, run the preregistered subset first and return to the owner before expanding it.
- [x] Pass independent review of the no-spend checkpoint and integrate the machine-readable Decimal cumulative-budget gate with its exact validated aggregate command before preparing or syncing a dataset, resolving or mutating a hosted prompt, calling a model, or incurring new spend. The reviewed gate is integrated at exact main commit `0b86b11564b9cc7500b7bfaf94301e4e83263f73`; its focused deterministic suite passes all 24 tests, and exact-commit `Build and Test` run `32910669112` completed successfully. The current per-row estimator alone remains insufficient as the program-total gate.

## Cutoff and provenance contract

- [x] Reverify each candidate's published model knowledge cutoff and current pricing from primary sources immediately before preparing evidence. The 2026-08-26 execution-date check reconfirmed cutoff `2026-02-16` for every exact model ID.
- [x] Derive the sampling boundary as the exact Europe/Berlin local midnight two calendar days after the candidate's published cutoff. The common boundary remains `2026-02-18T00:00:00 Europe/Berlin (+01)`.
- [x] For Luna, preserve the already-proven contract exactly: official cutoff `2026-02-16`, sampling boundary `2026-02-18T00:00:00 Europe/Berlin (+01)`, and only fixtures strictly after that instant.
- [x] Fail before spend if the cutoff-safe, completed, exact-context pool cannot support the declared sample. The frozen `109`-fixture pool and hash passed unchanged.
- [x] Use only the explicit `bundesliga-2025-26-legacy-id-hash-v1` read-only compatibility route and hosted `kicktippai/bundesliga-2026-27/predict-one-match` version 2 with required `production` membership.
- [ ] Bind the official and sampling cutoffs, eligibility policy/count/hash, seed, selected fixture IDs/hash, completed scores, evaluation time, all seven exact context-document versions/content hashes, prompt identity, model identity, reasoning, cap, and dataset/run/trace linkage in the prepared and reported provenance.

## Cost evidence — phase 1

- [ ] Use the integrated Decimal gate to machine-project/admit each one-item preflight, serialize every candidate row, and separately admit each retry only after preceding usage/cost settles. Produce each preflight-to-20 projection through tooling from the exact one-item `base-row --expect-count 1` report; never multiply it manually.
- [ ] For every owner-authorized candidate without an authoritative matching row, follow the repository estimate-row process: verify pricing, run the prescribed one-item preflight, inspect cap/tier/fallback/cost behavior, then run the exact five-fixture by four-repetition base sample only when the preflight is healthy.
- [ ] Collect compact usage by immutable Langfuse dataset-run binding, upsert the authoritative row, and run the repository estimator for 306 and 493 match-prediction calls. Do not retain prompt, context, or prediction payloads.
- [ ] Label every historical seven-document result as a preseason cost proxy that may understate the live eleven-document Bundesliga 2026/27 input. Cost evidence is not prediction-quality evidence.

## Quality evidence — phase 2

- [x] Pre-register the comparison metrics, aggregation, paired/common-manifest rule, repetition policy, adaptive topology, and failure handling before any quality run. Full-matrix designs retain 20 paired repetition totals. If those cannot fit, exactly one quality-first preliminary block is allowed; it targets 20 paired totals and may use the owner-authorized exploratory `10 × 15` then `10 × 10` fallback only after machine estimates prove stronger options unaffordable. Apply the hardest candidate cutoff to a shared paired sample when candidates are compared directly.
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

## Evidence — 2026-08-26 no-spend checkpoint

- Official OpenAI model pages were rechecked for `gpt-5.6-sol`,
  `gpt-5.6-terra`, and `gpt-5.6-luna`; all currently publish knowledge cutoff
  `2026-02-16`, support every effort used by the owner matrix, and name `medium`
  as their default reasoning effort.
- Official Standard and Flex short-context prices were rechecked. The repository
  cost calculator already recognizes all three exact IDs at the current Standard
  rates, and its Flex multiplier produces the current official Flex rates. The
  experiment CLI and core identity accept `none`, `medium`, `high`, `xhigh`, and
  `max`; no source/test change is required.
- The strict `2026-02-18T00:00:00 Europe/Berlin (+01)` boundary, eligible pool
  count `109`, and hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`
  remain authoritative for the completed Luna row. They are not inherited by an
  unspecified future surface; every selected model must re-derive its cutoff.
- The earlier fixed `15 × 20` topology and separate-phase budget template remain
  superseded. The accepted design uses a single USD 30 cumulative ledger and an
  exact-row-driven topology. Full-matrix evidence preserves 20 paired
  repetition totals; only the one quality-first preliminary block may fall back
  to 15 or 10, with weaker precision and effective sample size stated plainly.
- [ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
  is the durable experiment contract. Independent review approved the
  preregistration, and exact main commit
  `0b86b11564b9cc7500b7bfaf94301e4e83263f73` integrates the Decimal
  cumulative-budget gate and executable aggregate contract. All 24 focused
  deterministic gate tests and exact-commit `Build and Test` run `32910669112`
  passed. These no-spend checks do not satisfy any unchecked execution-date or
  live-action gate.
- The three owner-supplied rationale screenshots are preserved under
  `docs/experiments/assets/gpt-5-6-production-candidate-selection/` and embedded
  in the preregistration.
- The exact one-item and five-by-four datasets and manifests were reproduced as
  ignored local artifacts through the pre-sync boundary. Both bind eligible
  pool count `109` and hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`.
  The one-item selection is `ts1423757341`, selected-set hash
  `4a293d4bac8f6406cb88770332a5b85f9084f01d2f2e0227f7d52d63e93c4e16`,
  raw dataset/manifest hashes
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717` /
  `b396ffd599c8c79569db656d66e68ebe9169caf9a7e274d1aa0e7a0c8f8017c1`,
  and canonical historical-artifact hash
  `a03c31c174e0e0be1723b5214453a3992c2b5d023d125eb75fa658a7503c2946`.
  The five-by-four selection is `ts1423757259`, `ts1423757286`,
  `ts1423757328`, `ts1423757333`, and `ts1423757341`, selected-set hash
  `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`,
  raw dataset/manifest hashes
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18` /
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`,
  and canonical historical-artifact hash
  `22dfcab23f063e2fbb7a7fa96df4f2fb5dca384bb1329adc0c33157f5419a105`.
  Manifest sample/item counts are `1` / `1` and `20` / `20`.
- The owner subsequently authorized the audited public dataset sync and all
  experiment-required Langfuse changes. The exact one-item sync returned
  `created=0`, `updated=0`, `unchanged=1`; no local context manifest, prompt,
  prediction payload, credential, or secret was uploaded.

## Evidence — 2026-08-26 Luna/max live stop

- Exact preflight dataset run `195f2348-bac2-4900-9764-bd35618bd4a3`
  completed on Flex without fallback at cap `10000`: `2463` input, `535`
  output, `516` reasoning tokens, observed cost `$0.000567300000`.
- The machine-admitted five-by-four attempt used frozen manifest SHA-256
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`
  and parallelism `5`. It exited `1` on `ts1423757259`, Hamburger SV vs RB
  Leipzig, with `OpenAI response did not contain output text`; a different
  visible call used `8970 / 10000` output tokens. No accepted dataset run or
  authoritative estimate row exists.
- Four visible row calls total `$0.009927300000`. The full 900-second collector
  wait ended `4/5`; failed trace `4dc34c3c55f2425c7da00decc4ddb3e7`
  has no generation usage and its reported zero cost is not accepted as proof
  of zero provider charge.
- The Decimal ledger records observable spend `$0.010494600000` and carries a
  conservative `$0.033200000000` bound for the unobservable failed call,
  yielding bounded exposure `$0.043694600000` under the USD 30 ceiling.
- Ignored payload-safe evidence
  `.tmp/p0-23-budget/gpt-5.6-luna-max-base-row-p5-failure-evidence.json`,
  SHA-256
  `2d129d123765d0674d1edaa9a3686498bfa59b6aa8f47138fff42c1e285f0157`,
  independently binds the exact failed run, trace, error, artifact hashes,
  execution settings, 900-second `4/5` outcome, and supporting output hashes.
- [The preregistration live checkpoint](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-preregistration.md#live-execution-checkpoint--lunamax-cap-stop)
  admitted exactly one cap-`20000` Luna/`max` one-item remediation preflight
  after independent exact-SHA review, integration to `main`, explicit push,
  exact-head green CI, and fresh Decimal admission. Exact dataset run
  `47045b08-91f3-4251-a1fa-fb017f05ecc2`, trace
  `1431f6e783d63396832abeef3612a3b7`, completed on Flex without fallback at
  `2463` input, `1053` output, `1034` reasoning tokens, and
  `$0.000878100000` observed cost. The output used `5.265%` of cap, selecting
  cap `20000` for a future reviewed five-by-four row.
- Exact-head `main` commit `ef9221c4ca694158afa1600c3074c9bc83c94df6`
  passed all 12 jobs in `Build and Test` run `32945456262`. The fresh gate
  artifact
  `.tmp/p0-23-budget/gpt-5.6-luna-max-20k-remediation-green-ef9221c-budget-gate.json`,
  SHA-256
  `4037347433e2baa3efa5bed2cbf4b0202c27de7746ea4b937d3440215fbbfe3a`,
  records `$0.010494600000` settled named attempts, the unresolved
  `$0.033200000000` failed-call reservation, the `$0.039200000000` probe
  bound, and `$0.082894600000` bounded all-in exposure.
- Compact immutable usage is retained only in ignored artifact
  `.tmp/p0-23-budget/gpt-5.6-luna-max-20k-remediation-usage.json`, SHA-256
  `5e28b9c988bfd96539368481ad2084897e31a64804dac7f6751ff2ea9bd4c032`.
  Its exact run-name suffix `2026-08-2608-09-51+0` is unconventional because
  PowerShell interpreted `t` and `z` as format specifiers; the run remains
  uniquely named and immutably bound.
- The immutable one-item provisional report, SHA-256
  `7e69e837e0421011c7c1339c68c3d68713e9a539cc8faa28324c7831a5a42270`,
  machine-projects `$0.017562000000` for 20 calls. The post-probe planning
  gate, SHA-256
  `3780a03d3a84a470889c2a1cf25d6e844a2c8b236e037a9b432710a3949b472c`,
  records all three settled attempts at `$0.011372700000`, keeps the
  `$0.033200000000` unresolved failed-call reservation, and projects bounded
  all-in `$0.062134700000`. This is planning evidence only. This checkpoint
  allows no direct 20-call rerun, cap-`40000` probe, later candidate, or
  quality run.

## Complete when

- Every owner-authorized production candidate has comparable, reproducible cost evidence and the required cutoff-safe quality evidence, or the final owner ADR explicitly records the waived evidence and risk.
- Cost and quality claims remain visibly separate and traceable to exact immutable provenance.
- P0-06 can record the production decision without silently inheriting Luna/none or an old prompt/context route.
