# P0-16 — Add question-aware bonus context budgeting

- Status: Complete
- Priority: P0
- Depends on: [P0-13](p0-13-bonus-context-baseline.md), [P0-15](p0-15-context-document-hygiene.md)
- Decisions: [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0024](../decisions/0024-select-bonus-context-by-competition-and-question.md), [ADR-0037](../decisions/0037-record-immutable-bonus-context-manifests.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md)

## Outcome

Before the one-time pre-season bonus predictions are generated, bonus context selection is explicit by question category, explainable in traces, and bounded so full roster data is loaded only when it adds signal.

## Work items

- [x] Define supported question categories and their document sets in an ADR-backed policy table.
- [x] Separate deterministic question matching from document retrieval and give unknown questions a safe, documented baseline.
- [x] Select only referenced/team-relevant `roster-*` documents; never use `team-rosters` as an unconditional fallback.
- [x] Add configurable document/token budgets and log exclusions with reasons.
- [x] Attach category, selected documents, and estimated context size to trace metadata.
- [x] Add multilingual/wording variants and false-positive tests for each category.
- [x] Measure prediction/context behavior on a fixed representative bonus-question set before rollout.

## Validation

- Run KPI provider, bonus prompt, and telemetry tests.
- Compare fixed-set token counts and document selections against the P0 baseline.

## Complete when

- Every supported category has deterministic tests and trace-visible routing.
- Unknown questions remain useful without loading all rosters.
- The fixed-set context footprint is no larger than P0 without an accepted quality reason.
- This task is complete before any production bonus workflow can run.

## Evidence

- 2026-08-21: [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md) accepts the explicit `Champion`, `Relegation`, `TopScorer`, `Coach`, and `Unknown` policy, fail-closed multi-category ambiguity, exact option/member identity routing, and the immutable whole-selection budget. German and English phrase variants use Unicode-scalar letter/digit boundaries; supplementary-plane letters/digits stay inside tokens while punctuation and emoji remain boundaries. Longer-token false positives and UEFA-specific `Champions-League-Meister`/`Champions League champion` phrases remain `Unknown`. Options cannot classify a question.
- 2026-08-21: The Bundesliga provider keeps ADR-0024's exact headed order: `club-elo-rankings`, `team-squad-summary`, then only exact roster slugs sorted ordinally. `team-rosters` is always reported as `ProhibitedAggregate`; nonselected roster documents are reported deterministically as `CategoryDoesNotUseRoster` or `NoExactIdentity`. Storage presence never expands selection, and roster-relevant questions without an exact current team/member identity remain fail closed.
- 2026-08-21: The deterministic estimate measures the exact rendered context section bytes—`---\n{name}\n\n{content}\n` per document plus one closing `---`—and calculates `ceiling(UTF-8 bytes / 4)`. Accepted defaults are 20 documents and 32,000 estimated tokens; CLI values below two documents or 256 estimated tokens fail before Kicktipp/client/provider access. A valid but exceeded budget fails before model prediction or placement and never truncates required documents.
- 2026-08-21: The fixed headed-publication regression records unchanged P0 selections and exact footprints: champion `2/2,250/563`, relegation `2/2,250/563`, top scorer `3/4,441/1,111`, coach `3/4,441/1,111`, and unknown `2/2,250/563` for documents/UTF-8 bytes/estimated tokens. Maximum observed is 3 documents, 4,441 bytes, and 1,111 estimated tokens, below both defaults without a quality exception.
- 2026-08-21: The resolved result validates exact manifest document names/order/hashes, selected-name equality, the complete canonical exclusion ledger (`team-rosters` first, then every unselected manifest roster in slug order with exact kind/reason), recomputed size, and the effective budget. Missing, extra, reordered, undefined, wrong-kind, wrong-reason, and selected-roster exclusion entries fail closed. Prediction observations receive category, selected/excluded documents, bytes/tokens, and both budgets. A two-question command regression proves each metadata object is rebuilt so a top-scorer roster/reason cannot leak into the following unknown question. The ADR-0037 persisted manifest stays byte/content focused and unchanged.
- 2026-08-21: Focused Release validation passed: Core policy/result/budget `52/52` (`0.776s` final-review rerun), Firebase provider `32/32` (`1.322s`), Bonus command family `94/94` (`10.621s`), and OpenAI telemetry/composer/bonus prediction `34/34` (`1.235s`). The fixed-set provider test independently binds the estimator to the exact prompt renderer.
- 2026-08-21: Four affected full Release assemblies ran in parallel from one compiled output and passed: Core `242/242` (`4.576s` test time), Firebase `278/278` (`1m44.982s`), OpenAI `220/220` (`5.361s`), and Orchestrator `990/990` (`2m01.159s`). The final `dotnet build KicktippAi.slnx --configuration Release --no-restore` passed with `0` errors in `43.37s`; existing analyzer, obsolete-API, nullability, and `SSH.NET` advisory warnings remain outside P0-16.
- 2026-08-21: No live calls, paid predictions, credentials, workflows, WM26 contact, or production choices were used. WM26 retains its existing offline regression path and legacy selector. P0-18 must add the accepted budget inputs to reusable Bundesliga workflow composition; P0-16 intentionally does not edit workflow files.
- 2026-08-21: The independently approved lane range through `65d384ca63a6d28c9f4c8768cc1632fd41e80f40` was applied to main as the single commit `fe48c8270320efd1d1c27d042cbea5bfa3ca708d`, so accepted ADR-0038 enters main once in final form. Both commits have the exact tree `0f92e3a63932075e19e43bd5e929cce5c63165c3`.
