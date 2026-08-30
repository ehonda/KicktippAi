# WM26 Operational Safety

Read this file and the [WM26 model ledger](../../../../docs/onboarding-wm26/model-config-onboarding.md) before a WM26 onboarding operation. Read the matching sections of [the WM26 onboarding record](../../../../docs/onboarding-wm26/README.md) before changing a workflow, model, context source, schedule, or snapshot process. Those two records are authoritative for active community rows, workflow files, cadence, telemetry, and historical run evidence.

## Safe preflight

Use this full-profile dry-run for the exact WM26 community context being onboarded or changed. It executes the resolved WM26 profile without Firestore, outcome, or prediction writes; it can still make authenticated/read-only provider requests.

```powershell
dotnet run --project src/Orchestrator -- collect-context profile --community-context <community-context> --competition fifa-world-cup-2026 --dry-run --verbose
```

The expected order is `Kicktipp -> Wm26HistoryPlayedDates -> FifaRankings -> NationalLineups`. Do not substitute Bundesliga collectors. `collect-context-dev -c ehonda-dev-wm26 --competition fifa-world-cup-2026 --dry-run --verbose` is a shortcut only for the supported development community; never use it to preflight a non-dev target. If the exact-target dry-run does not reach all direct phases or reports a missing required document, stop before any write or prediction workflow.

Use this as the only explicit date-map command in this profile. It is a strict, no-write repair/preflight; it must precede any separately authorized write:

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context <community-context> --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run
```

Strict mode requires exact `Played_At` coverage. The guarded workflow form (`--apply-known-only --preserve-collected-on-or-after 2026-06-11`) preserves existing exact timestamps, replaces post-cutoff collection markers from the matching stored prediction, and fails before saving if a post-cutoff row has no stored prediction. Before relying on that guarded write path in a new environment, run the read-only composite-index probe documented under **Recent History Played Dates** in the WM26 onboarding record. Do not run the unflagged date-map write until a separate, explicit authorization follows this dry-run and probe.

## Context and source invariants

- Use the official FIFA full 26-player squad membership published 2026-06-03 after the 2026-06-02 submission deadline. The checked-in seed and every `lineup-*`/`lineups` refresh contain full squads, not starters. FIFA is the membership source; the permitted DuckDB source is supplemental only.
- Require the match documents and KPI documents listed under **Context Documents** in the WM26 onboarding record before validation. WM26 has knockout rules and deliberately no home/away or head-to-head history.
- FIFA rankings use `Rank,Team,ELO,Published_At`, two decimal places for points, and the stable FIFA publication timestamp. Lineups use `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`, include coaches, use `N/A` (not `0`) for unavailable player market values, and leave coach values empty.
- Keep prompt source `langfuse`, routes `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`, and local `wm26` fallback as recorded. Hosted prompt checks require `langfusePromptFallback=false`; fallback warnings and `true` trace metadata are failure/investigation evidence, not silent equivalence.
- Do not commit raw `kicktipp-snapshots` HTML. The only commit-eligible snapshots are encrypted files under `tests/KicktippIntegration.Tests/Fixtures/Html/Real/ehonda-dev-wm26/*.html.enc`.

## Configuration, costs, and communities

- Treat `gpt-5-nano` / `minimal` as the development shortcut only. The selected production row is `o3` / `high` / cap `40000`, with `rabetrabauken2026` as reference context and the `o3 high` arena path as its guarded copy. Self-contained arena rows keep their own `community_context`; never select credentials from a copied context.
- Runtime policy is `flex-first-standard-fallback`: `PredictionServiceCommandSupport` creates `PredictionServiceOptions.FlexProcessingWithStandardFallback`, requests `flex`, and `PredictionService.IsFlexFallbackFailure` permits exactly one `default`-tier retry for HTTP `408`; a `429` with a Flex resource-unavailable marker or the retryable non-quota rate-limit classification; `TimeoutRejectedException`; `TimeoutException`; or `TaskCanceledException` when the caller cancellation token is not cancelled. Quota `429`s, auth/validation failures, caller cancellation, and all unmatched failures do not trigger this fallback. Read the [runtime source](../../../../src/Orchestrator/Commands/Shared/PredictionServiceCommandSupport.cs) and its [fallback implementation](../../../../src/OpenAiIntegration/PredictionService.cs) before changing this policy; record requested/final tier and fallback use in the model ledger and first-run trace evidence. Every production/scheduled workflow passes model, reasoning effort, and a non-default cap explicitly.
- Estimate 104 match predictions and exclude bonus cost unless asked. Run `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model <model> --reasoning-effort <effort>`. If the matching row is absent, do not calculate manually: obtain required spend approval, use the base-row procedure, `upsert-row`, rerun `estimate`, and record the command, assumptions, row, and output in `docs/experiments/whole-season-cost-estimates.md` and the model ledger.
- Confirm the exact posting identity has joined and can access every target community. Check required per-community Kicktipp secrets plus `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, `LANGFUSE_SECRET_KEY`, and repository variable `LANGFUSE_PUBLIC_KEY` without exposing their values. Telemetry environment, credential pair, posting target, and community context come from the ledger row.

## Validation and activation

Create the context, matchday, and bonus workflow bundle together using `wm26-` filenames and a `🏆` display-name marker. Keep `workflow_dispatch`; use the exact cadence in the WM26 onboarding record only after the context run succeeds. First-run reporting must record the resolved model/prompt/cap/service policy, context document inspection, final Kicktipp/Firestore verification, Langfuse traces, cost, and remaining manual follow-ups.

Do not run `matchday-dev` before both match-team lineups exist or `bonus-dev` before `lineups` exists. When separately authorized to validate a dev write, final verification requires no on-demand fallback, match lineups, `fifa-rankings`, question-aware `lineups`, hosted prompt evidence, and the exact ranking/lineup schemas above. Inspect the resulting traces with the global `langfuse` skill and installed CLI, filtering the expected environment, WM26 community tag, and `matchday`/`bonus`/`predict-match`/`predict-bonus` observations. Automation cannot accept an invite; missing membership, secrets, access, cost coverage, or context success blocks activation.

The project owner controls production selection, schedule activation, monitoring, and rollback. Keep schedules disabled or preserve their recorded state until the ledger records a successful context→matchday→bonus first run and the owner-approved activation/rollback decision. A dry run, context collection, or manual prediction success is not schedule authority.
