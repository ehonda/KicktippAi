# Bundesliga 2026/27 Model Configuration Onboarding

Updated: 2026-08-27

This is the source-of-truth ledger for Bundesliga 2026/27 prediction configurations. A row is not onboarded unless its competition, model, reasoning effort, maximum output cap, and numbered prompt versions are all present. Labels are useful for candidate routing, but they are not a substitute for the numbered version in a production or persisted exact identity. [The community onboarding matrix](community-onboarding.md) maps these configuration slots to posting targets, community contexts, credential names, and Langfuse environments.

## Current ledger

| Use | Competition | Model | Reasoning | Max output tokens | Exact prompts | Runtime policy | Status |
| --- | --- | --- | --- | ---: | --- | --- | --- |
| `validation-luna-none` — `ehonda-dev-buli-2627` self-contained plumbing | `bundesliga-2026-27` | `gpt-5.6-luna` | `none` | `10000` | Match `kicktippai/bundesliga-2026-27/predict-one-match` v3; bonus `kicktippai/bundesliga-2026-27/predict-bonus` v1 | Existing Flex-first/Standard-fallback policy; every invocation passes the complete identity | Authorized for development plumbing validation only; not a production default |
| `production-primary` — `pes-squad` / `schadensfresse` generation and compatible copies | `bundesliga-2026-27` | `gpt-5.6-sol` | `xhigh` | `10000` | Match v3; bonus v1; exact names/hashes below | Existing Flex-first/Standard-fallback policy; USD 35 season orientation is planning-only and not enforced | Owner-selected; manual cycles and payload-safe audit are green for `pes-squad`, `relaxdays-tippt`, and the arena copy, while schedules and `schadensfresse` remain |
| `arena-challenger-sol-high` | `bundesliga-2026-27` | `gpt-5.6-sol` | `high` | `10000` | Match v3; bonus v1 | Self-contained arena context; existing Flex-first/Standard-fallback policy | Owner-admitted; manual triad and payload-safe audit green; schedule pending |
| `arena-challenger-luna-medium` | `bundesliga-2026-27` | `gpt-5.6-luna` | `medium` | `10000` | Match v3; bonus v1 | Self-contained arena context; existing Flex-first/Standard-fallback policy | Owner-admitted; manual triad and payload-safe audit green; schedule pending |
| `arena-challenger-terra-xhigh` | `bundesliga-2026-27` | `gpt-5.6-terra` | `xhigh` | `10000` | Match v3; bonus v1 | Self-contained arena context; existing Flex-first/Standard-fallback policy | Owner-admitted; manual triad and payload-safe audit green; schedule pending |
| `arena-challenger-luna-none` / `validation-luna-none` — `ehonda-ai-arena` | `bundesliga-2026-27` | `gpt-5.6-luna` | `none` | `10000` | Match v3; bonus v1 | Self-contained arena context; existing Flex-first/Standard-fallback policy | Owner-admitted challenger and retained plumbing identity; context/match green and audited, bonus failed closed with zero side effects on stale immutable provenance at the zero-reprediction limit |

The current match v3 mirror/version has normalized SHA-256 `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`. The bonus v1 mirror/version has normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`. Hosted v3 carries `production` and `staging`, with `latest` maintained automatically. Production automation passes `--langfuse-prompt-version` explicitly and requires `production` membership. Historical P0-23 artifacts remain bound to match v2 and its hash `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`.

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
--langfuse-prompt-version 3
```

Bonus and bonus verification additionally pass:

```text
--langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-bonus
--langfuse-prompt-version 1
```

P0-19 owns workflow creation. Every manual entry provides the exact ledger values as explicit inputs and does not rely on command model, reasoning, cap, prompt-label, or prompt-version defaults. Final production schedules remain absent until P0-21.

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

## Production selection evidence

[ADR-0052](../../plans/bundesliga-2026-27/decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
records the Owner decision and its exploratory-evidence caveat. The accepted
P0-23 quality order starts with Sol/`xhigh` at `27.8` average points, followed
by Sol/`high` at `26.4`; their `+1.4` difference has Holm-adjusted
`p = 0.192` and is not statistically significant. The Owner selected
Sol/`xhigh` because its descriptive result follows the observed
higher-reasoning trend and the cost remains acceptable. Sol/`max` is a
separate post-hoc follow-up and did not block this selection.

Exact normalized Flex match estimates from the authoritative rows are:

| Configuration | 306 calls | 493 calls |
| --- | ---: | ---: |
| Sol/`xhigh` | `$2.609782200000` | `$4.204649100000` |
| Sol/`high` | `$2.079331200000` | `$3.350033600000` |
| Luna/`medium` | `$0.105573060000` | `$0.170089930000` |
| Terra/`xhigh` | `$1.266319800000` | `$2.040181900000` |
| Luna/`none` | `$0.077711760000` | `$0.125202280000` |

Two independent 493-call Sol/`xhigh` primaries plus one 493-call stream for
each of the four arena challengers total `$14.094805910000`. Compatible
`relaxdays-tippt` and production-arena copies add no match-model call. This
match-only projection excludes bonus calls, the richer live 11-document input,
Standard fallback premiums, and retry variance. USD `35` is therefore retained
only as an orientation and is not enforced.

## Remaining activation gate

The production model, cap, prompts, arena matrix, service policy, and planning
orientation are settled. P0-21's ordered manual cycles are green through
`pes-squad`, `relaxdays-tippt`, the arena production copy, Sol/`high`,
Luna/`medium`, and Terra/`xhigh`. Luna/`none` context and matchday are green,
but its bonus caller failed closed on stale immutable provenance at the zero-
reprediction limit and requires a deliberate remediation decision. The current
successful/failure boundary is payload-audited; operating ownership,
`schadensfresse`, the activation ADR, deliberate schedules,
and first scheduled observation remain. The repository's manual-only callers
do not themselves grant schedule authority.
