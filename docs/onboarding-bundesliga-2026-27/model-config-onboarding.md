# Bundesliga 2026/27 Model Configuration Onboarding

Updated: 2026-08-21

This is the source-of-truth ledger for Bundesliga 2026/27 prediction configurations. A row is not onboarded unless its competition, model, reasoning effort, maximum output cap, and numbered prompt versions are all present. Labels are useful for candidate routing, but they are not a substitute for the numbered version in a production or persisted exact identity. [The community onboarding matrix](community-onboarding.md) maps these configuration slots to posting targets, community contexts, credential names, and Langfuse environments.

## Current ledger

| Use | Competition | Model | Reasoning | Max output tokens | Exact prompts | Runtime policy | Status |
| --- | --- | --- | --- | ---: | --- | --- | --- |
| `validation-luna-none` — `ehonda-dev-buli-2627` self-contained plumbing | `bundesliga-2026-27` | `gpt-5.6-luna` | `none` | `10000` | Match `kicktippai/bundesliga-2026-27/predict-one-match` v2; bonus `kicktippai/bundesliga-2026-27/predict-bonus` v1 | Current prediction-service flex request with standard fallback; every invocation must pass the complete identity | Authorized for development plumbing validation only; not a production default |
| `validation-luna-none` — `ehonda-ai-arena` self-contained plumbing | `bundesliga-2026-27` | `gpt-5.6-luna` | `none` | `10000` | Match `kicktippai/bundesliga-2026-27/predict-one-match` v2; bonus `kicktippai/bundesliga-2026-27/predict-bonus` v1 | Same exact validation identity, with arena-owned context and participant credentials | Authorized only for the P0-20 validation ladder; its production-community trace environment does not make it a production model |
| `production-primary` — reference, independent, and arena-copy uses | `bundesliga-2026-27` | **Owner decision required** | **Owner decision required** | **Owner decision required** | Match and bonus numbered versions require owner approval | Service tier/fallback policy and cost ceiling require owner approval | Blocked from onboarding and activation until owner decision evidence is recorded |
| `arena-challenger-<n>` | `bundesliga-2026-27` | **Owner decision required** | **Owner decision required** | **Owner decision required** | Match and bonus numbered versions require owner approval | Every challenger requires an independently approved complete identity and participant | Template only; zero challengers are admitted by this ledger |

The match mirror/version has normalized SHA-256 `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`. The bonus mirror/version has normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`. In Langfuse, `production`, `staging`, and automatic `latest` resolved these versions when P0-05 closed, but production automation must pass `--langfuse-prompt-version` explicitly.

## Exact validation command contract

Matchday prediction, bonus prediction, match verification, and bonus verification must all pass:

```text
MODEL: gpt-5.6-luna
--competition bundesliga-2026-27
--reasoning-effort none
--max-output-tokens 10000
--prompt-source langfuse
```

Matchday and match verification additionally pass:

```text
--langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match
--langfuse-prompt-version 2
```

Bonus and bonus verification additionally pass:

```text
--langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-bonus
--langfuse-prompt-version 1
```

P0-19 owns workflow creation. Its manual entries and the separately authorized Luna validation schedule must provide every value above as explicit input; they may not rely on the command's model, reasoning, cap, prompt-label, or prompt-version defaults. P0-06 does not create or activate those workflows. Final production schedules remain blocked until P0-21.

## Capability and pricing evidence

[Official OpenAI model documentation](https://developers.openai.com/api/docs/models/gpt-5.6-luna) identifies `gpt-5.6-luna` as a reasoning model, lists `none`, `low`, `medium` (default), `high`, `xhigh`, and `max`, and records a 1,050,000-token context window, 128,000 maximum output tokens, and a 2026-02-16 knowledge cutoff. The plumbing cap is deliberately pinned much lower at `10000`. Following the estimator's two-day rule, cost sampling starts strictly after `2026-02-18T00:00:00 Europe/Berlin (+01)`.

[Official OpenAI pricing](https://developers.openai.com/api/docs/pricing) lists short-context standard prices per one million tokens of `$0.20` input, `$0.02` cached input, `$0.25` cache writes, and `$1.20` output. Long-context standard prices are `$0.40`, `$0.04`, `$0.50`, and `$1.80`, respectively. The runtime cost calculator now recognizes the short-context input, cached-input, and output rates; cache-write and long-context accounting are not represented by its current token-usage contract and remain explicit planning caveats.

## Whole-season cost gate

Bundesliga has 306 official fixtures. Reusing the documented historical counts (`313` initial predictions, `123` first repredictions, `68` second-or-later repredictions) projects `493` calls with repredictions. The prescribed estimator lookup is:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 306,493 --model gpt-5.6-luna --reasoning-effort none
```

It fails closed with `No matching base estimate JSON row found for model='gpt-5.6-luna', reasoningEffort='none'.` No dollar total is reported or hand-calculated.

Before producing an actionable dollar estimate, the paid evidence proceeds through two separate gates:

1. authorize one observed request using match prompt version 2, `gpt-5.6-luna`, `none`, flex-first processing with standard fallback, and cap `10000`;
2. use that observation to state the expected 20-item spend and obtain a second confirmation for the 5-fixture-by-4-repetition base run;
3. collect exactly 20 successful observations, persist the row with `upsert-row`, and rerun the exact 306/493 estimator command.

The exact one-item identity prepared for the first gate is slice `random-1x1-seed-20260821-gpt-5-6-luna-none-cost-preflight` and run `p0-06__gpt-5.6-luna__none__match-v2__random-1x1-seed-20260821__cost-preflight`. Its conservative authorization ceiling is `$2.00`. The bound deliberately overcounts the full 1,050,000-token context at both the long-context uncached-input and cache-write rates, adds the full `10000` output cap, allows both the initial flex attempt and the executor's single standard fallback, applies the documented 10% regional uplift, and rounds up from less than `$1.60`. It is a spend ceiling, not an estimator projection. No request has been made by P0-06 without the separate approval.

Prepared commands (do not execute without the first spend approval):

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match-slice --community-context pes-squad --match-count 1 --repetitions 1 --sample-seed 20260821 --starts-after "2026-02-18T00:00:00 Europe/Berlin (+01)" --slice-key random-1x1-seed-20260821-gpt-5-6-luna-none-cost-preflight

dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match-slices/pes-squad/all-matchdays-after-20260217t230000z/random-1x1-seed-20260821-gpt-5-6-luna-none-cost-preflight/slice-dataset.json

dotnet run --project src/Orchestrator -- run-repeated-match-slice gpt-5.6-luna --manifest artifacts/langfuse-experiments/repeated-match-slices/pes-squad/all-matchdays-after-20260217t230000z/random-1x1-seed-20260821-gpt-5-6-luna-none-cost-preflight/slice-manifest.json --run-name "p0-06__gpt-5.6-luna__none__match-v2__random-1x1-seed-20260821__cost-preflight" --prompt-key bundesliga-2026-27-match-v2 --prompt-source langfuse --langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match --langfuse-prompt-label production --langfuse-prompt-version 2 --reasoning-effort none --max-output-tokens 10000 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --parallelism 1 --replace-run
```

See [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md) and [ADR-0033](../../plans/bundesliga-2026-27/decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md).

## Production owner gate

Before any production workflow or schedule is enabled, record the approved `production-primary` model, reasoning effort, maximum output tokens, match and bonus prompt versions, arena challenger matrix, service-tier/fallback behavior, whole-season cost ceiling, and exact estimator evidence in this ledger and an accepted decision. The Luna/none validation row cannot be copied into `production-primary` or an `arena-challenger-<n>` row without explicit owner approval.
