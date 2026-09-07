# Bundesliga 2026/27 P0 archive

P0 delivered the Bundesliga 2026/27 onboarding, context, validation, and
production launch. These records are historical evidence, not required startup
context for current P1 work.

## Closeout evidence

The first natural production-live cycle completed on `main` revision
`50f3ed148891977b5909659f9986c9c9958d7875` in GitHub Actions run
`33143114280`:

- all eight ordered context→match pairs (16 jobs) completed successfully;
- every context collector preserved its published snapshot;
- match jobs observed all 9/9 Bundesliga fixtures with no prediction writes,
  generations, repredictions, tokens, or model cost;
- the run lasted 38 minutes 46 seconds and did not overlap another scheduled
  cycle; and
- GitHub delivery started 2 hours 46 minutes 22 seconds after the nominal cron,
  a platform-delivery observation rather than an application failure.

The detailed evidence and rollback contract remain in
[P0-21](tasks/p0-21-production-activation.md). Current production continuity is
summarized by the parent [execution strategy](../../execution-strategy.md).

## Historical delivery sequence

1. P0-01–P0-11 established competition identity, storage, prompts, model/cost
   gates, roster contracts, and Club Elo foundations.
2. P0-12–P0-16 and P0-22 established ordinary/bonus context, profile-driven
   collection, hygiene, question-aware selection, and history dates.
3. P0-17–P0-19 established community scope and reusable workflow triads.
4. P0-20 completed development and arena validation.
5. P0-23 and P0-24 completed production-candidate and bonus-copy evidence.
6. P0-25 published the enriched launch roster.
7. P0-21 recorded owner selection, activation, rollback, and natural-run
   evidence.

## Task index

| Record | Historical outcome |
|---|---|
| [P0-01](tasks/p0-01-current-competition.md) | Current Bundesliga competition established |
| [P0-02](tasks/p0-02-competition-scoped-storage.md) | Competition-scoped persistence required |
| [P0-03](tasks/p0-03-matchday-completion.md) | Matchday completion corrected |
| [P0-04](tasks/p0-04-team-manifest.md) | Strict 18-team join manifest established |
| [P0-05](tasks/p0-05-prompt-route.md) | 2026/27 prompt route established |
| [P0-06](tasks/p0-06-model-ledger-and-cost-baseline.md) | Model ledger and launch cost baseline pinned |
| [P0-07](tasks/p0-07-roster-contract.md) | Roster seed and document contracts defined |
| [P0-08](tasks/p0-08-roster-membership-seed.md) | Roster membership seed authored |
| [P0-09](tasks/p0-09-roster-collector.md) | Roster enrichment and collection implemented |
| [P0-10](tasks/p0-10-club-elo-source.md) | Operational Club Elo source accepted |
| [P0-11](tasks/p0-11-club-elo-collector.md) | Club Elo collection implemented |
| [P0-12](tasks/p0-12-match-context-and-transfer-retirement.md) | Transfer context replaced in the match contract |
| [P0-13](tasks/p0-13-bonus-context-baseline.md) | Competition-aware bonus context established |
| [P0-14](tasks/p0-14-profile-driven-collection.md) | Collection made profile-driven |
| [P0-15](tasks/p0-15-context-document-hygiene.md) | Live context-document contract cleaned |
| [P0-16](tasks/p0-16-question-aware-bonus-context.md) | Question-aware bonus budgeting added |
| [P0-17](tasks/p0-17-community-scope.md) | Community and environment scope recorded |
| [P0-18](tasks/p0-18-base-workflow-support.md) | Reusable workflows gained Bundesliga support |
| [P0-19 Luna/medium](tasks/p0-19-arena-luna-medium-self-contained-workflow-triad.md) | Arena Luna/medium triad completed |
| [P0-19 Luna](tasks/p0-19-arena-luna-self-contained-workflow-triad.md) | Luna arena triad completed |
| [P0-19 arena copy](tasks/p0-19-arena-production-copy-workflow-triad.md) | Arena production-copy triad completed |
| [P0-19 Sol/high](tasks/p0-19-arena-sol-high-self-contained-workflow-triad.md) | Arena Sol/high triad completed |
| [P0-19 Terra/xhigh](tasks/p0-19-arena-terra-xhigh-self-contained-workflow-triad.md) | Arena Terra/xhigh triad completed |
| [P0-19 template](tasks/p0-19-community-workflow-triad.md) | Historical per-community template retained |
| [P0-19 pes-squad](tasks/p0-19-pes-squad-production-reference-workflow-triad.md) | Production-reference triad completed |
| [P0-19 relaxdays](tasks/p0-19-relaxdays-production-copy-workflow-triad.md) | Production-copy triad completed |
| [P0-19 schadensfresse](tasks/p0-19-schadensfresse-production-copy-workflow-triad.md) | Production-copy triad completed |
| [P0-20](tasks/p0-20-seed-and-development-validation.md) | Development and arena validation completed |
| [P0-21](tasks/p0-21-production-activation.md) | Production validation and schedule activation completed |
| [P0-22](tasks/p0-22-history-played-dates.md) | Exact history dates reconstructed |
| [P0-23](tasks/p0-23-gpt-5-6-production-candidate-evidence.md) | GPT-5.6 candidate evidence completed |
| [P0-24](tasks/p0-24-bonus-copy-post-compatibility.md) | Bonus copy-post compatibility proven |
| [P0-25](tasks/p0-25-roster-enrichment-and-team-total.md) | Enriched rosters and team totals published |

## Handoff index

- [Foundations green — 2026-08-16](handoffs/buli-2627-p0-foundations-green-2026-08-16.md)
- [P0-12 open review — 2026-08-19](handoffs/buli-2627-p0-12-open-review-2026-08-19.md)
- [P0 closeout ready — 2026-08-25](handoffs/buli-2627-p0-closeout-ready-2026-08-25.md)
