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
- The required production scope is `pes-squad`, `schadensfresse`, and `ehonda-ai-arena`; `ehonda-dev-buli-2627` is the safe development target.
- Historical data is not deleted. A future historical experiment must opt into an explicit competition, prompt, and context setup.

## P0 tasks

| Task | Outcome | Depends on |
|---|---|---|
| [P0-01](tasks/p0-01-current-competition.md) | Make 2026/27 the current Bundesliga competition | — |
| [P0-02](tasks/p0-02-competition-scoped-storage.md) | Require explicit competition-scoped persistence | P0-01 |
| [P0-03](tasks/p0-03-matchday-completion.md) | Require nine completed matches per Bundesliga matchday | P0-01 |
| [P0-04](tasks/p0-04-team-manifest.md) | Check in the exact 18-team join manifest | — |
| [P0-05](tasks/p0-05-prompt-route.md) | Implement hosted 2026/27 prompts with local fallback | P0-01 |
| [P0-06](tasks/p0-06-model-ledger-and-cost-baseline.md) | Pin test identity and later approve the launch model/cost baseline | P0-05 |
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
| [P0-19](tasks/p0-19-community-workflow-triad.md) | Add an explicit workflow triad per community | P0-17, P0-18 |
| [P0-20](tasks/p0-20-seed-and-development-validation.md) | Seed context and validate dev plus arena plumbing | P0-02 through P0-18, P0-22, Luna/none P0-19 entrypoints |
| [P0-21](tasks/p0-21-production-activation.md) | Validate production, submit opening predictions, and enable schedules | P0-06, P0-20, production P0-19 entrypoints |
| [P0-22](tasks/p0-22-history-played-dates.md) | Reconstruct exact played dates for recent, home, and away history | P0-02, P0-04 |

P0-01 and P0-04 can begin independently. After P0-01, storage, completion, and prompt work can proceed in dependency-safe lanes; after P0-04, roster, Club Elo, and history played-date work can proceed independently. Context hygiene joins those lanes before any community workflow can generate a real prediction. P0-21 is the only task authorized to enable final production schedules.

For P0-19, copy the template once per community matrix row that needs an entrypoint so each workflow triad can be implemented and reviewed independently.

## Handoffs

- [`buli-2627-p0-foundations-green-2026-08-16`](handoffs/buli-2627-p0-foundations-green-2026-08-16.md) — clean, green pause after the foundation contracts, Club Elo seed, roster membership seed, and P0-22 planning; intended resume after the 2026-08-20 allowance reset.

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
- autonomous Luna/none validation passes in development and through the arena local, `workflow_dispatch`, and schedule ladder;
- manual production runs and opening writes succeed for `pes-squad`, `schadensfresse`, and `ehonda-ai-arena`, including compatible prediction copy-posting;
- final schedules remain disabled until P0-21 records the launch decision and successful manual evidence, then the first scheduled sequence is observed.

## Decision index

- [ADR-0001: Support only the current Bundesliga season](decisions/0001-current-bundesliga-season-only.md)
- [ADR-0002: Supersede transfer documents with Elo and roster context](decisions/0002-supersede-transfer-documents.md) — superseded by ADR-0003
- [ADR-0003: Use DuckDB-primary rosters with per-club fallback](decisions/0003-duckdb-primary-rosters-with-fallback.md)
- [ADR-0004: Use hosted prompts with local fallback](decisions/0004-hosted-prompts-with-local-fallback.md)
- [ADR-0005: Launch all selected communities with reference prediction reuse](decisions/0005-launch-community-and-prediction-topology.md)
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
