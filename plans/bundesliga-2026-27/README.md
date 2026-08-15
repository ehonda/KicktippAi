# Bundesliga 2026/27 implementation plan

This directory decomposes the P0 and P1 proposals in [the readiness research](../../docs/research/bundesliga-2026-27-onboarding-readiness.md) into implementation-sized tasks.

P0 ends with a manually verified production run and deliberately enabled schedules. P1 improves maintainability, freshness, context efficiency, experiment tooling, and cost evidence after launch safety is established.

The draft [execution strategy](execution-strategy.md) describes the proposed gated orchestration, conservative worktree parallelism, Git integration options, ChatGPT Pro usage discipline, and the decision/manual-step register that must be resolved before implementation orchestration begins.

## Accepted direction

- Bundesliga 2026/27 is the only live Bundesliga runtime target in scope. We are not preserving 2025/26 workflows, prompt routes, defaults, or implicit storage behavior.
- Transfer documents are retired. Club Elo provides current strength context; authoritative rosters and squad summaries provide current membership and squad context.
- Historical data is not deleted. A future historical experiment must opt into an explicit competition, prompt, and context setup.

## P0 tasks

| Task | Outcome | Depends on |
|---|---|---|
| [P0-01](tasks/p0-01-current-competition.md) | Make 2026/27 the current Bundesliga competition | — |
| [P0-02](tasks/p0-02-competition-scoped-storage.md) | Require explicit competition-scoped persistence | P0-01 |
| [P0-03](tasks/p0-03-matchday-completion.md) | Require nine completed matches per Bundesliga matchday | P0-01 |
| [P0-04](tasks/p0-04-team-manifest.md) | Check in the exact 18-team join manifest | — |
| [P0-05](tasks/p0-05-prompt-route.md) | Select and implement 2026/27 match and bonus prompts | P0-01 |
| [P0-06](tasks/p0-06-model-ledger-and-cost-baseline.md) | Pin the launch model configuration and baseline cost | P0-05 |
| [P0-07](tasks/p0-07-roster-contract.md) | Define roster seed and output contracts | P0-04 |
| [P0-08](tasks/p0-08-roster-membership-seed.md) | Author and validate current membership for all clubs | P0-07 |
| [P0-09](tasks/p0-09-roster-collector.md) | Enrich and publish roster and squad-summary documents | P0-07, P0-08 |
| [P0-10](tasks/p0-10-club-elo-source.md) | Accept an operational club-strength source | P0-04 |
| [P0-11](tasks/p0-11-club-elo-collector.md) | Publish complete per-team and aggregate Elo snapshots | P0-04, P0-10 |
| [P0-12](tasks/p0-12-match-context-and-transfer-retirement.md) | Replace transfer context with required Elo and roster context | P0-09, P0-11 |
| [P0-13](tasks/p0-13-bonus-context-baseline.md) | Route safe aggregate and targeted bonus context | P0-09, P0-11, P0-12 |
| [P0-14](tasks/p0-14-profile-driven-collection.md) | Select collectors from a competition profile | P0-09, P0-11, P0-12 |
| [P0-15](tasks/p0-15-community-scope.md) | Record production and development communities | P0-05, P0-06 |
| [P0-16](tasks/p0-16-base-workflow-support.md) | Teach reusable workflows the Bundesliga profile | P0-14, P0-15 |
| [P0-17](tasks/p0-17-community-workflow-triad.md) | Add a manual-only workflow triad per community | P0-15, P0-16 |
| [P0-18](tasks/p0-18-seed-and-development-validation.md) | Seed isolated context and validate a full development cycle | P0-02 through P0-17 |
| [P0-19](tasks/p0-19-production-activation.md) | Validate production manually, then enable schedules | P0-18 |

P0-01 and P0-04 can begin independently. After P0-01, the storage, completion, and prompt tasks can proceed in parallel; after P0-04, the roster-contract and Club Elo source tasks can proceed in parallel. Provider implementation can proceed in parallel after the manifest and its respective contract/source decision exist. P0-19 is the only task authorized to activate schedules.

For P0-17, copy the task once per community selected by P0-15 so each workflow triad can be implemented and reviewed independently.

## P1 tasks

| Task | Outcome | Depends on |
|---|---|---|
| [P1-01](tasks/p1-01-team-and-manager-data.md) | Replace stale manual team and manager artifacts | P0-19 |
| [P1-02](tasks/p1-02-question-aware-context.md) | Reduce bonus context by question type and token budget | P0-13, P0-19 |
| [P1-03](tasks/p1-03-generic-onboarding-skill.md) | Extract generic competition onboarding tooling | P0-19 |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Schedule Club Elo refresh with freshness gates | P0-19 |
| [P1-05](tasks/p1-05-roster-refresh.md) | Review and publish roster membership changes | P0-19 |
| [P1-06](tasks/p1-06-observability-datasets.md) | Make experiment preparation explicitly 2026/27-capable | P0-12, P0-19 |
| [P1-07](tasks/p1-07-cost-calibration.md) | Recalculate season cost from live usage evidence | P1-02, P1-04, P1-05 |

There is intentionally no transfer-document automation task in P1.

## Launch gates

P0 is complete only when:

- all 18 teams map one-to-one across Kicktipp, document slugs, the roster seed, and the accepted strength source;
- all production reads and writes use `bundesliga-2026-27` and cannot fall back to the old unscoped Bundesliga identity;
- match context contains standings, rules, histories, both Elo documents, and both roster documents, with no transfer-document lookup;
- bonus context uses `club-elo-rankings`, `team-squad-summary`, and only the targeted rosters a question needs;
- a partial matchday is not complete before all nine matches are complete;
- development and production manual runs succeed, CSV rendering and trace context are inspected, and the deployed model/cost metadata is correct;
- schedules remain disabled until P0-19 records the launch decision and successful manual evidence.

## Decision index

- [ADR-0001: Support only the current Bundesliga season](decisions/0001-current-bundesliga-season-only.md)
- [ADR-0002: Supersede transfer documents with Elo and roster context](decisions/0002-supersede-transfer-documents.md)

Add every subsequent ADR to this list.
