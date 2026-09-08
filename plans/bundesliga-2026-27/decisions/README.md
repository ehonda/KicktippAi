# Bundesliga 2026/27 decision index

Use the [current plan index](../README.md) to find the decisions required by an
active task. Use this complete index for specialist lookup and historical
traceability. Accepted ADRs stay here while operative; their age or P0 origin
does not make them archive material. An ADR's own status and successor links
remain authoritative.

| ADR | Decision | Status |
|---|---|---|
| [0000](0000-template.md) | ADR template | Proposed template |
| [0001](0001-current-bundesliga-season-only.md) | Support only the current Bundesliga season | Accepted |
| [0002](0002-supersede-transfer-documents.md) | Supersede transfer documents with Elo and roster context | Superseded |
| [0003](0003-duckdb-primary-rosters-with-fallback.md) | Use DuckDB-primary rosters with per-club fallback | Accepted |
| [0004](0004-hosted-prompts-with-local-fallback.md) | Use hosted prompts with local fallback | Accepted |
| [0005](0005-launch-community-and-prediction-topology.md) | Launch selected communities with reference prediction reuse | Superseded by ADR-0052 |
| [0006](0006-stage-validation-with-a-cheap-test-model.md) | Stage validation with a cheap test model | Accepted |
| [0007](0007-require-context-hygiene-before-launch.md) | Require context hygiene before launch | Accepted |
| [0008](0008-launch-club-elo-from-a-dated-seed.md) | Launch Club Elo from a dated seed when necessary | Accepted |
| [0009](0009-bounded-orchestration-and-hybrid-git.md) | Bounded orchestration and hybrid Git integration | Superseded by ADR-0061 |
| [0010](0010-season-scoped-team-identity-manifest.md) | Season-scoped strict team identity manifest | Accepted |
| [0011](0011-roster-snapshot-and-publication-contract.md) | Roster snapshots and atomic publication | Accepted |
| [0012](0012-competition-aware-matchday-completion.md) | Competition-aware matchday completion | Accepted |
| [0013](0013-club-elo-snapshot-and-freshness-contract.md) | Club Elo snapshot and freshness contract | Accepted |
| [0014](0014-share-atomic-context-kpi-publication.md) | Shared atomic context and KPI publication | Accepted |
| [0015](0015-club-elo-prompt-publication-contract.md) | Strict Club Elo prompt and provenance contract | Accepted |
| [0016](0016-validate-club-elo-publication-metadata.md) | Semantic Club Elo publication validation | Accepted |
| [0017](0017-roster-collector-duckdb-and-reconstruction-contract.md) | Roster DuckDB and reconstruction contract | Accepted |
| [0018](0018-validate-roster-publication-metadata-semantically.md) | Semantic roster publication validation | Accepted |
| [0019](0019-roster-publication-truth-boundary.md) | Shared roster-publication truth boundary | Accepted |
| [0020](0020-record-immutable-match-context-manifests.md) | Immutable match-context manifests | Accepted |
| [0021](0021-bind-ordinary-context-content-and-prepare-provenance.md) | Bind ordinary context and prepare provenance | Accepted |
| [0022](0022-transactional-bundesliga-reprediction-allocation.md) | Transactional Bundesliga reprediction allocation | Accepted |
| [0023](0023-use-orchestrator-created-cli-worktrees.md) | Orchestrator-created CLI worktrees | Superseded by ADR-0061 |
| [0024](0024-select-bonus-context-by-competition-and-question.md) | Competition- and question-aware bonus selection | Accepted |
| [0025](0025-reconstruct-bundesliga-history-played-dates.md) | Fixed-source Bundesliga history dates | Accepted |
| [0026](0026-exclude-incomplete-history-rows.md) | Exclude incomplete selected-history rows | Accepted |
| [0027](0027-add-openfootball-for-second-bundesliga-history.md) | CC0 second-division history source | Accepted |
| [0028](0028-capture-openligadb-second-bundesliga-history.md) | OpenLigaDB second-division history capture | Accepted |
| [0029](0029-capture-openligadb-dfb-pokal-final.md) | OpenLigaDB DFB-Pokal final capture | Accepted |
| [0030](0030-use-uefa-match-record-for-europa-league-final.md) | UEFA Europa League final match record | Accepted |
| [0031](0031-correct-dfb-pokal-final-inventory-coverage.md) | Correct DFB-Pokal final inventory coverage | Accepted |
| [0032](0032-freeze-complete-history-set-and-publish-atomically.md) | Freeze and atomically publish preseason history | Accepted |
| [0033](0033-pin-validation-model-ledger-and-reserve-production-selection.md) | Validation model ledger and reserved production selection | Accepted |
| [0034](0034-drive-context-collection-from-competition-profiles.md) | Profile-driven development context collection | Accepted |
| [0035](0035-freeze-first-live-dfb-history-completion.md) | First live DFB history completion | Accepted |
| [0036](0036-retire-legacy-team-manager-context.md) | Retire legacy team and manager context | Accepted |
| [0037](0037-record-immutable-bonus-context-manifests.md) | Immutable bonus-context manifests | Accepted |
| [0038](0038-bound-bonus-context-by-question-policy.md) | Question-policy bonus bounds | Accepted |
| [0039](0039-record-bundesliga-community-and-credential-topology.md) | Community and credential topology | Accepted |
| [0040](0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md) | Hash-bound 2025/26 experiment context | Accepted |
| [0041](0041-freeze-completed-dfb-first-round-history-transition.md) | Completed DFB first-round history transition | Accepted |
| [0042](0042-publish-complete-preseason-context-atomically.md) | Atomically publish preseason Kicktipp context | Superseded |
| [0043](0043-freeze-historical-experiment-aliases-and-eligible-pool.md) | Historical aliases and context-eligible pool | Accepted |
| [0044](0044-select-canonical-preseason-history-sources.md) | Canonical preseason history sources | Accepted |
| [0045](0045-verify-versioned-prompt-promotion-before-validation.md) | Versioned prompt promotion gate | Accepted |
| [0046](0046-bind-cost-usage-to-langfuse-dataset-runs.md) | Cost usage bound to Langfuse dataset runs | Accepted |
| [0047](0047-observe-one-temporary-arena-luna-scheduled-cycle.md) | Temporary arena Luna scheduled-cycle observation | Accepted |
| [0048](0048-verify-bonus-compatibility-before-reference-copy.md) | Bonus compatibility before reference copying | Accepted |
| [0049](0049-preregister-gpt-5-6-candidate-evidence.md) | GPT-5.6 candidate preregistration | Accepted |
| [0050](0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md) | Enriched launch rosters and team subtotals | Accepted |
| [0051](0051-require-explicit-launch-roster-enrichment-overlay.md) | Explicit launch-roster enrichment overlay | Accepted |
| [0052](0052-select-production-model-community-matrix-and-match-prompt-v3.md) | Production model, community matrix, and match prompt v3 | Accepted |
| [0053](0053-schedule-the-production-live-matchday-lane.md) | Production-live matchday schedule | Accepted |
| [0054](0054-copy-schadensfresse-bundesliga-from-pes-squad.md) | Copy schadensfresse Bundesliga from pes-squad | Accepted |
| [0055](0055-add-schadensfresse-to-production-live-lane.md) | Schadensfresse in the production-live lane | Accepted |
| [0056](0056-reconcile-current-open-fixtures-with-outcomes.md) | Reconcile open fixtures with outcomes | Accepted |
| [0057](0057-exempt-standings-from-reprediction-staleness.md) | Standings reprediction-staleness exemption | Accepted |
| [0058](0058-make-schadensfresse-a-competition-typed-primary.md) | Competition-typed schadensfresse primary | Accepted |
| [0059](0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md) | Structured Schadensfresse semantic rules | Accepted |
| [0060](0060-separate-generation-manifest-from-current-rules-attestation.md) | Separate generation provenance from rules attestation | Accepted |
| [0061](0061-preview-and-milestone-orchestration.md) | Preview and milestone orchestration | Accepted |
| [0062](0062-temporarily-restore-schadensfresse-copy.md) | Temporary schadensfresse copy recovery | Accepted |
| [0066](0066-refresh-history-source-checkpoint.md) | Rolling history-source checkpoint | Accepted |
| [0067](0067-tolerate-unresolved-external-history-dates.md) | Collection-date proxy for unresolved dates | Accepted |
| [0068](0068-replace-copy-sunset-with-reviewed-replacement-condition.md) | Reviewed replacement condition for copy recovery | Accepted |
| [0069](0069-deliver-frozen-schadensfresse-champions-league-bonus.md) | Frozen Schadensfresse Champions-League bonus | Accepted |
| [0070](0070-isolate-the-strict-cl-bonus-mutation-transport.md) | Isolated strict CL bonus mutation transport | Accepted |
| [0071](0071-bind-strict-cl-post-to-stable-advertised-action.md) | Stable advertised strict CL POST action | Accepted |
| [0072](0072-operate-history-date-maintenance-manually.md) | Manual history-date maintenance | Accepted |
| [0073](0073-refresh-strength-and-rosters-during-context-collection.md) | Refresh strength and rosters during collection | Accepted |
| [0074](0074-freeze-context-source-cycle-handoff-and-provenance.md) | Context source-cycle handoff and provenance | Accepted |
| [0075](0075-refine-orchestration-recovery-and-resource-admission.md) | Orchestration recovery and resource admission | Accepted; hook-readiness gate refined by ADR-0076 |
| [0076](0076-pause-orchestration-hook-readiness-gate.md) | Pause the orchestration hook-readiness gate | Accepted |
| [0077](0077-refresh-club-elo-from-official-html.md) | Official-HTML Club Elo refresh contract | Accepted |
| [0078](0078-refine-context-source-pre-artifact-and-publication-fence.md) | Context-source pre-artifact and publication fence | Accepted |
| [0079](0079-pin-roster-refresh-endpoints-and-close-c2-validation.md) | Pinned roster endpoints and C2 validation closure | Accepted |
| [0080](0080-bound-transitional-context-publication-recovery.md) | Bound transitional context-publication recovery | Accepted; narrowly supersedes ADR-0078's transitional inference paragraph |
