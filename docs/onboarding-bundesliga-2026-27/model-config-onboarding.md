# Bundesliga 2026/27 Model Configuration Onboarding

Updated: 2026-08-25

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

## Whole-season cost evidence

Bundesliga has 306 official fixtures. Reusing the documented historical counts
(`313` initial predictions, `123` first repredictions, `68` second-or-later
repredictions) projects `493` calls with repredictions. On 2026-08-25 the owner
authorized the one-item and subsequent 20-item Luna/none cost gates. Both were
completed without cap pressure; this authorization did not select Luna for
production.

The one-item run used source fixture `1423757341`, dataset
`cmt86fx6o0aeuad0dg99ivamv`, and dataset run
`80e17c90-631d-4c89-8640-21fe36fef541`. It observed `2463` uncached input,
`17` output, and zero reasoning tokens on flex with no fallback, at observed
cost `$0.0002565` and `0.17%` output-cap use.

The five-by-four sample uses completed Bundesliga 2025/26 fixtures strictly
after `2026-02-18T00:00:00 Europe/Berlin (+01)` and the exact historical
seven-document compatibility route. Eligibility policy
`bundesliga-2025-26-completed-after-sampling-cutoff-all-7-context-documents-at-or-before-starts-at-minus-12h-v1`
yielded 109 fixtures, hash
`6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`.
Seed `20260821` selected source IDs `1423757259`, `1423757286`, `1423757328`,
`1423757333`, and `1423757341`.

The non-authoritative parallelism-5 attempt completed 20 items with one
flex-429 standard fallback. The prescribed parallelism-3 replacement completed
20/20 entirely on flex and is authoritative: dataset
`cmt86m8gn0awvad0eyx7mn5f6`, dataset run
`6a3c4e70-ebb4-4c07-9a1a-7af19c32d995`, prepared manifest SHA-256
`fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`.
Exact dataset-run recovery admitted 20 distinct linked traces and excluded the
20 retained same-name traces from the earlier attempt.

The base row prices all `48752` input tokens as uncached, despite `48692`
observed cache-read tokens, and records `340` output, zero reasoning tokens,
zero non-flex/fallback requests, total estimated flex cost `$0.005079200000`,
and average `$0.000253960000` per prediction. The exact estimator command was:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 306,493 --model gpt-5.6-luna --reasoning-effort none
```

Exact estimator stdout: `N=306: $0.077711760000`; `N=493: $0.125202280000`.
This is a preseason seven-document cost proxy that may understate the live
eleven-document Bundesliga 2026/27 input. It is not prediction-quality evidence.

See [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md),
[the compact base evidence](../../.agents/skills/estimate-experiment-cost-skill/references/gpt-5.6-luna-none-base-estimate-2026-08-25.md),
and [ADR-0033](../../plans/bundesliga-2026-27/decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md).

## Production owner gate

Before any production workflow or schedule is enabled, record the approved `production-primary` model, reasoning effort, maximum output tokens, match and bonus prompt versions, arena challenger matrix, service-tier/fallback behavior, whole-season cost ceiling, and exact estimator evidence in this ledger and an accepted decision. The Luna/none validation row cannot be copied into `production-primary` or an `arena-challenger-<n>` row without explicit owner approval.
