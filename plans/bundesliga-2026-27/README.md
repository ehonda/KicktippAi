# Bundesliga 2026/27 implementation plan

This directory decomposes the P0 and P1 proposals in [the readiness research](../../docs/research/bundesliga-2026-27-onboarding-readiness.md) into implementation-sized tasks.

P0 ends with successful manual production runs, the opening bonus and match predictions, deliberately enabled schedules, and observation of the first scheduled sequence. P1 then improves maintainability, freshness, experiment tooling, and live cost evidence.

The accepted [execution strategy](execution-strategy.md) defines gated orchestration, bounded worktree parallelism, hybrid Git integration, Codex usage discipline, and the remaining owner-controlled launch decisions.

## Accepted direction

- Bundesliga 2026/27 is the only live Bundesliga runtime target in scope. We are not preserving 2025/26 workflows, prompt routes, defaults, or implicit storage behavior.
- Transfer documents are retired. Club Elo provides current strength context; current rosters and squad summaries provide membership and squad context.
- Recent, home, and away history must carry exact played dates: current-season Bundesliga rows come from the competition-scoped Kicktipp schedule/results, while intervening cup, UEFA, friendly, and other fixtures use an accepted source with explicit provenance. Head-to-head already carries dates and is not rewritten.
- DuckDB is the primary roster-membership source per club once it explicitly represents 2026/27 and passes strict gates. A complete, source-dated 18-club seed and last-known-good snapshots cover missing, stale, partial, or suspicious data without ongoing manual maintenance.
- Langfuse-hosted prompts are primary. Checked-in mirrors are the outage or first-fetch fallback.
- The required production scope is `pes-squad`, `schadensfresse`,
  `relaxdays-tippt`, and `ehonda-ai-arena`; `ehonda-dev-buli-2627` is the safe
  development target. ADR-0052 fixes the exact primary/copy/challenger matrix.
- Historical data is not deleted. A future historical experiment must opt into an explicit competition, prompt, and context setup.

## P0 tasks

| Task | Outcome | Depends on |
|---|---|---|
| [P0-01](tasks/p0-01-current-competition.md) | Make 2026/27 the current Bundesliga competition | — |
| [P0-02](tasks/p0-02-competition-scoped-storage.md) | Require explicit competition-scoped persistence | P0-01 |
| [P0-03](tasks/p0-03-matchday-completion.md) | Require nine completed matches per Bundesliga matchday | P0-01 |
| [P0-04](tasks/p0-04-team-manifest.md) | Check in the exact 18-team join manifest | — |
| [P0-05](tasks/p0-05-prompt-route.md) | Implement hosted 2026/27 prompts with local fallback | P0-01 |
| [P0-06](tasks/p0-06-model-ledger-and-cost-baseline.md) | Complete: production Sol/xhigh and the arena matrix are pinned with a non-enforced USD 35 planning orientation | P0-05, P0-23 |
| [P0-07](tasks/p0-07-roster-contract.md) | Define quality-gated DuckDB and fallback roster contracts | P0-04 |
| [P0-08](tasks/p0-08-roster-membership-seed.md) | Author and audit fallback membership for all clubs | P0-07 |
| [P0-09](tasks/p0-09-roster-collector.md) | Select, enrich, and publish complete roster documents | P0-07, P0-08 |
| [P0-10](tasks/p0-10-club-elo-source.md) | Accept a dated Club Elo launch source and gate network use | P0-04 |
| [P0-11](tasks/p0-11-club-elo-collector.md) | Publish complete per-team and aggregate Elo snapshots | P0-04, P0-10 |
| [P0-12](tasks/p0-12-match-context-and-transfer-retirement.md) | Replace transfer context with required Elo and roster context | P0-09, P0-11 |
| [P0-13](tasks/p0-13-bonus-context-baseline.md) | Route safe aggregate and targeted bonus context | P0-09, P0-11, P0-12 |
| [P0-14](tasks/p0-14-profile-driven-collection.md) | Select collectors from a competition profile | P0-09, P0-11, P0-12, P0-22 |
| [P0-15](tasks/p0-15-context-document-hygiene.md) | Remove stale, duplicate, and deprecated live context | P0-12, P0-13, P0-14, P0-22 |
| [P0-16](tasks/p0-16-question-aware-bonus-context.md) | Bound bonus context by question before the one-time bonus run | P0-13, P0-15 |
| [P0-17](tasks/p0-17-community-scope.md) | Record community, context, model-slot, and credential topology | P0-05, P0-16 |
| [P0-18](tasks/p0-18-base-workflow-support.md) | Teach reusable workflows the Bundesliga profile | P0-14, P0-17 |
| [P0-19 template](tasks/p0-19-community-workflow-triad.md) | Copy an explicit workflow-triad task per deployable matrix row | P0-17, P0-18 |
| [P0-19 arena Luna/none](tasks/p0-19-arena-luna-self-contained-workflow-triad.md) | Complete: manual-only self-contained challenger/validation triad on match v3 | P0-17, P0-18 |
| [P0-19 pes-squad production reference](tasks/p0-19-pes-squad-production-reference-workflow-triad.md) | Complete: manual-only Sol/xhigh primary triad; P0-21 owns runtime evidence | P0-06, P0-17, P0-18 |
| [P0-19 schadensfresse independent production](tasks/p0-19-schadensfresse-production-independent-workflow-triad.md) | Complete: manual-only Sol/xhigh primary triad; external setup and runtime remain P0-21 | P0-06, P0-17, P0-18 |
| [P0-19 relaxdays production copy](tasks/p0-19-relaxdays-production-copy-workflow-triad.md) | Complete: manual-only Sol/xhigh copy triad sourced from `pes-squad` | P0-06, P0-17, P0-18, P0-24 |
| [P0-19 arena production copy](tasks/p0-19-arena-production-copy-workflow-triad.md) | Complete: manual-only Sol/xhigh production-copy triad sourced from `pes-squad` | P0-06, P0-17, P0-18, P0-24 |
| [P0-19 arena Sol/high](tasks/p0-19-arena-sol-high-self-contained-workflow-triad.md) | Complete: manual-only self-contained challenger triad | P0-06, P0-17, P0-18 |
| [P0-19 arena Luna/medium](tasks/p0-19-arena-luna-medium-self-contained-workflow-triad.md) | Complete: manual-only self-contained challenger triad | P0-06, P0-17, P0-18 |
| [P0-19 arena Terra/xhigh](tasks/p0-19-arena-terra-xhigh-self-contained-workflow-triad.md) | Complete: manual-only self-contained challenger triad | P0-06, P0-17, P0-18 |
| [P0-20](tasks/p0-20-seed-and-development-validation.md) | Seed context and validate dev plus arena plumbing | P0-02 through P0-18, P0-22, local dev path, Luna/none arena P0-19 entrypoints |
| [P0-21](tasks/p0-21-production-activation.md) | Validate production, submit opening predictions, and enable schedules | P0-06, P0-20, P0-24, P0-25, production P0-19 entrypoints |
| [P0-22](tasks/p0-22-history-played-dates.md) | Reconstruct exact played dates for recent, home, and away history | P0-02, P0-04 |
| [P0-23](tasks/p0-23-gpt-5-6-production-candidate-evidence.md) | Complete: publish cutoff-safe GPT-5.6 cost/quality evidence with Luna/`max` incomplete; post-hoc Sol/`xhigh` and the later Sol/`max` extension remain explicitly exploratory | P0-05, P0-12, P0-20 |
| [P0-24](tasks/p0-24-bonus-copy-post-compatibility.md) | Complete: exact bonus question and complete-option-set copy compatibility is implemented and integrated | P0-16, P0-17, P0-18 |
| [P0-25](tasks/p0-25-roster-enrichment-and-team-total.md) | Complete: pinned enriched v2 arena rosters, deterministic team known-value subtotals, and one exact Luna/none replacement trace round are validated | P0-09, P0-20 |

The implementation path through P0-20 and P0-23 through P0-25 is complete.
ADR-0052 closes P0-06 and every schedule-free P0-19 repository row. At the
2026-08-27 live-validation checkpoint, the exact manual context, matchday, and
bonus triads succeeded for `pes-squad`, the `relaxdays-tippt` production copy,
the arena Sol/`xhigh` production copy, and the self-contained arena Sol/`high`
Luna/`medium`, and Terra/`xhigh` challengers. The first `relaxdays-tippt` context attempt
exposed a missing target-owned rules source; exact-head repair `eedf330` and
green CI run `33049482431` preceded the successful retry. P0-21 remains the active P0
closeout gate: the Luna/`none` context and matchday runs succeeded, but bonus
run `33055144574` failed closed on stale immutable provenance at the zero-
reprediction limit before a model call. That row requires a deliberate
remediation decision and a new authorized bonus validation. Payload-safe
inspection is complete for every successful row through the Luna/`none` match:
65 real generations / `$0.5723559`, exact index-0 prompt/model/context
identities, zero index `1+` or document contamination, and zero model calls on
both compatible copy rows. The failed Luna bonus added no generation, cost, or
prediction mutation.
An authenticated 2026-08-27 11:41 CEST GET audit returned HTTP 200 for
`schadensfresse` but found no 2026/27 marker, zero open match/bonus controls,
and only historical rows/rules. It is an explicit NOT READY manual-only
exception pending external setup and its complete readiness/context/prediction/
inspection ladder. Exact schedule cadence/ownership, the activation
ADR, deliberate schedule enablement, and first scheduled observation remain
open.

P0-19 is instantiated for every ADR-0052 deployable row. The development row
remains a local CLI path; every production/copy/challenger entrypoint is
explicit, manual-only, schedule-free, and handed to P0-21 for live validation.
P0-21's manual-only production outer matchday lane is integrated at exact
commit `992af5a63c788c0cc066dce92dd1319a91e5083d` after independent exact-SHA
approval with no findings. Its contract/actionlint/Release/`1142/1142`
Orchestrator validation passed, and exact-head GitHub run
[`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
succeeded including Pages. The lane has strict context-before-matchday ordering
and shared non-cancelling concurrency, but no bonus, `schadensfresse`, or cron;
its integrated writer/reviewer worktrees are cleaned. This preparation does not
close the remaining P0-21 owner gates.

The read-only activation audit recommends not dispatching the outer lane again:
completed leaf validation plus static/review/CI evidence is sufficient, while a
new run could consume index `1` or `2` repredictions. Its proposed, unaccepted
cron is `7 2,9 * * *`, with fixed UTC times mapped to 04:07/11:07 CEST and
03:07/10:07 CET. The 51m04s observed serialized duration supports a 90-minute
monitoring/escalation envelope—not a timeout—and a three-hour later-pass
completion margin. The minimal Accepted ADR/test/workflow/docs activation
patch, operators/rollback, Luna/`none` forced recovery/schedule treatment, and
first observation remain Owner gates.

## Handoffs

- [`buli-2627-p0-closeout-ready-2026-08-25`](handoffs/buli-2627-p0-closeout-ready-2026-08-25.md) — current closeout handoff; its 2026-08-27 addendum closes P0-06 and schedule-free P0-19, leaving P0-21.
- [`buli-2627-p0-foundations-green-2026-08-16`](handoffs/buli-2627-p0-foundations-green-2026-08-16.md) — historical foundation pause; superseded by the current task ledger and execution strategy.
- [`buli-2627-p0-12-open-review-2026-08-19`](handoffs/buli-2627-p0-12-open-review-2026-08-19.md) — historical interrupted-review checkpoint; P0-12 is now complete, so its resume instructions are no longer active.

## P1 tasks

P1-01 and P1-02 were promoted to P0-15 and P0-16 because both affect predictions that exist only at or before go-live. Numbering of the remaining P1 tasks stays stable.

| Task | Outcome | Depends on |
|---|---|---|
| [P1-03](tasks/p1-03-generic-onboarding-skill.md) | Extract generic competition onboarding tooling | P0-21 |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Schedule Club Elo refresh with freshness gates | P0-21 |
| [P1-05](tasks/p1-05-roster-refresh.md) | Automatically adopt valid current-season DuckDB membership | P0-21 |
| [P1-06](tasks/p1-06-observability-datasets.md) | Make experiment preparation explicitly 2026/27-capable | P0-12, P0-21 |
| [P1-07](tasks/p1-07-cost-calibration.md) | Recalculate season cost from live usage evidence | P0-16, P1-04, P1-05 |

There is intentionally no transfer-document automation task in P1.

## Launch gates

P0 is complete only when:

- all 18 teams map one-to-one across Kicktipp, document slugs, roster sources, and the accepted Club Elo snapshot;
- all production reads and writes use `bundesliga-2026-27` and cannot fall back to the old unscoped Bundesliga identity;
- every selected recent/home/away history row has an exact source-attributed played date; collection timestamps and inferred league order are never presented as played dates, and head-to-head dates remain intact;
- match and bonus context use explicit live allowlists, contain the required Elo/roster/squad documents, and exclude stale team/manager, transfer, old-season, and cross-competition documents;
- bonus context is question-aware and bounded before the only pre-season bonus predictions are generated;
- a partial matchday is not complete before all nine matches are complete;
- hosted prompt versions and local mirrors agree, and the exact production model/configuration has owner approval plus a reproducible cost estimate;
- final production selection uses the P0-23 comparative evidence, or its Accepted ADR explicitly records the owner's evidence waiver and accepted risk;
- before the first production prediction, that community has a headed v2 roster publication from the exact pinned launch artifact, at least 464 known ages, 464 known positions, and 450 valued players, plus exactly one validated final `Team Accumulated` row per club; later no-DuckDB collection must preserve the enriched last-known-good snapshot;
- the prepared `pes-squad`, `relaxdays-tippt`, and `schadensfresse` context
  callers satisfy that non-arena gate through ADR-0052's false-by-default,
  exact-pinned overlay step before normal profile collection; arena callers
  preserve their already verified shared enriched head without redownloading;
- autonomous Luna/none validation passes in development and through the arena local, `workflow_dispatch`, and schedule ladder;
- manual production runs and opening writes succeed for every ready ADR-0052
  row: `pes-squad`, `relaxdays-tippt`, and all selected arena participants,
  including P0-24-compatible copy-posting without an extra model call;
  `schadensfresse` remains a visible manual-only exception until its external
  season setup completes;
- final schedules remain disabled until P0-21 records the launch decision and successful manual evidence, then the first scheduled sequence is observed.

## Decision index

- [ADR-0001: Support only the current Bundesliga season](decisions/0001-current-bundesliga-season-only.md)
- [ADR-0002: Supersede transfer documents with Elo and roster context](decisions/0002-supersede-transfer-documents.md) — superseded by ADR-0003
- [ADR-0003: Use DuckDB-primary rosters with per-club fallback](decisions/0003-duckdb-primary-rosters-with-fallback.md)
- [ADR-0004: Use hosted prompts with local fallback](decisions/0004-hosted-prompts-with-local-fallback.md)
- [ADR-0005: Launch all selected communities with reference prediction reuse](decisions/0005-launch-community-and-prediction-topology.md) — superseded by ADR-0052
- [ADR-0006: Stage validation with a cheap test model](decisions/0006-stage-validation-with-a-cheap-test-model.md)
- [ADR-0007: Require context hygiene before launch](decisions/0007-require-context-hygiene-before-launch.md)
- [ADR-0008: Launch Club Elo from a dated seed when necessary](decisions/0008-launch-club-elo-from-a-dated-seed.md)
- [ADR-0009: Use bounded orchestration and hybrid Git integration](decisions/0009-bounded-orchestration-and-hybrid-git.md)
- [ADR-0010: Use a season-scoped strict team identity manifest](decisions/0010-season-scoped-team-identity-manifest.md)
- [ADR-0011: Fix roster snapshots and atomic publication](decisions/0011-roster-snapshot-and-publication-contract.md)
- [ADR-0012: Make matchday completion competition aware](decisions/0012-competition-aware-matchday-completion.md)
- [ADR-0013: Fix the Club Elo snapshot and freshness contract](decisions/0013-club-elo-snapshot-and-freshness-contract.md)
- [ADR-0014: Share atomic context and KPI publication snapshots](decisions/0014-share-atomic-context-kpi-publication.md)
- [ADR-0015: Use strict Club Elo prompt documents and reconstructable publication provenance](decisions/0015-club-elo-prompt-publication-contract.md)
- [ADR-0016: Validate Club Elo publication metadata semantically](decisions/0016-validate-club-elo-publication-metadata.md)
- [ADR-0017: Fix roster collector DuckDB and reconstruction contract](decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md)
- [ADR-0018: Validate roster publication metadata semantically](decisions/0018-validate-roster-publication-metadata-semantically.md)
- [ADR-0019: Share one roster-publication truth boundary](decisions/0019-roster-publication-truth-boundary.md)
- [ADR-0020: Record immutable match-context manifests](decisions/0020-record-immutable-match-context-manifests.md)
- [ADR-0021: Bind ordinary context content and prepare provenance](decisions/0021-bind-ordinary-context-content-and-prepare-provenance.md) — supersedes only ADR-0020's ordinary-document version/content portions
- [ADR-0022: Allocate Bundesliga repredictions transactionally](decisions/0022-transactional-bundesliga-reprediction-allocation.md) — supersedes only ADR-0020's Bundesliga prediction-save portion
- [ADR-0023: Use orchestrator-created CLI worktrees for parallel writers](decisions/0023-use-orchestrator-created-cli-worktrees.md)
- [ADR-0024: Select bonus context by competition and question](decisions/0024-select-bonus-context-by-competition-and-question.md)
- [ADR-0025: Reconstruct Bundesliga history played dates from fixed sources](decisions/0025-reconstruct-bundesliga-history-played-dates.md)
- [ADR-0026: Exclude incomplete rows from selected match history](decisions/0026-exclude-incomplete-history-rows.md)
- [ADR-0027: Add a fixed CC0 source for second-division history](decisions/0027-add-openfootball-for-second-bundesliga-history.md)
- [ADR-0028: Capture OpenLigaDB for second-division history](decisions/0028-capture-openligadb-second-bundesliga-history.md)
- [ADR-0029: Capture the OpenLigaDB DFB-Pokal final](decisions/0029-capture-openligadb-dfb-pokal-final.md)
- [ADR-0030: Use the UEFA match record for the Europa League final](decisions/0030-use-uefa-match-record-for-europa-league-final.md)
- [ADR-0031: Correct DFB-Pokal final inventory coverage](decisions/0031-correct-dfb-pokal-final-inventory-coverage.md)
- [ADR-0032: Freeze the complete preseason history set and publish it atomically](decisions/0032-freeze-complete-history-set-and-publish-atomically.md)
- [ADR-0033: Pin the validation model ledger and reserve production selection](decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md)
- [ADR-0034: Drive context collection from competition profiles](decisions/0034-drive-context-collection-from-competition-profiles.md)
- [ADR-0035: Freeze the first live DFB-Pokal history completion](decisions/0035-freeze-first-live-dfb-history-completion.md)
- [ADR-0036: Retire legacy team and manager context](decisions/0036-retire-legacy-team-manager-context.md)
- [ADR-0037: Record immutable bonus-context manifests](decisions/0037-record-immutable-bonus-context-manifests.md)
- [ADR-0038: Bound bonus context by question policy](decisions/0038-bound-bonus-context-by-question-policy.md)
- [ADR-0039: Record Bundesliga community and credential topology](decisions/0039-record-bundesliga-community-and-credential-topology.md)
- [ADR-0040: Use hash-bound 2025/26 context for preseason cost experiments](decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md)
- [ADR-0041: Freeze the completed DFB-Pokal first-round history transition](decisions/0041-freeze-completed-dfb-first-round-history-transition.md)
- [ADR-0042: Publish complete preseason Kicktipp context atomically](decisions/0042-publish-complete-preseason-context-atomically.md) — superseded by ADR-0044
- [ADR-0043: Freeze historical experiment aliases and the context-eligible pool](decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md) — refines ADR-0040's document-name and sampling-pool contract
- [ADR-0044: Select canonical preseason history sources](decisions/0044-select-canonical-preseason-history-sources.md)
- [ADR-0045: Verify versioned prompt promotion before validation](decisions/0045-verify-versioned-prompt-promotion-before-validation.md)
- [ADR-0046: Bind cost usage to exact Langfuse dataset runs](decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md)
- [ADR-0047: Observe one temporary arena Luna scheduled cycle](decisions/0047-observe-one-temporary-arena-luna-scheduled-cycle.md)
- [ADR-0048: Verify bonus compatibility before reference copying](decisions/0048-verify-bonus-compatibility-before-reference-copy.md)
- [ADR-0049: Preregister GPT-5.6 candidate evidence under one program ceiling](decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
- [ADR-0050: Publish enriched launch rosters with derived team subtotals](decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md)
- [ADR-0051: Require an explicit launch roster enrichment overlay](decisions/0051-require-explicit-launch-roster-enrichment-overlay.md)
- [ADR-0052: Select the production model, community matrix, and match prompt v3](decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
