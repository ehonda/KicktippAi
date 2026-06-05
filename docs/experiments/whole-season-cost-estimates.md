# Whole-Season Cost Estimates

Last estimate refresh: 2026-06-06

Coverage note updated: 2026-06-06

This estimate projects match prediction cost for two full competitions, all
`gpt-5.5` reasoning efforts, four comparison configurations, and the current
WM26 onboarding configurations for `gpt-5-nano` / `minimal`, `gpt-5.5` /
`none`, `gpt-5.5` / `xhigh`, and `gpt-5.4-nano` / `none`. Bonus question cost
is excluded because it is negligible relative to full-season match prediction
cost.

Important pricing assumption: these estimates assume every match prediction is
billed at OpenAI `flex` pricing. Production is expected to use flex from here
on out. Actual spend can be higher if flex requests occasionally fall back to
standard processing after flex-capacity failures, but that share is expected to
stay low.

The `gpt-5-nano minimal`, `gpt-5.5 none`, `gpt-5.5 xhigh`,
`gpt-5.4-nano none`, `o3 medium`, and `o3 high` values were rerun during the
2026-06-06 refresh with `experiment_cost_estimator.py estimate`. The other
values remain from the 2026-05-11 and 2026-06-02 refreshes. No Firebase
`cost` query was rerun for this estimate; the Bundesliga reprediction evidence
reuses the already collected `pes-squad` / `o3` counts.

WM26 model onboarding coverage is tracked in
[model-config-onboarding.md](../onboarding-wm26/model-config-onboarding.md).
As of 2026-06-06, the manual WM26 dev/testing shortcut configuration and the
preliminary scheduled `ehonda-ai-arena` workflow configuration still use
`gpt-5-nano` / `minimal`. That configuration has a preliminary estimate based
on the hosted WM match prompt `kicktippai/wm26/predict-one-match`, label
`latest`, version `2`. A Langfuse lookup on 2026-06-02 found no `production`
label for that prompt, so `latest` remains the configured WM hosted route for
this estimate. Additional self-contained manual-only `ehonda-ai-arena` workflow
tests are now onboarded for `gpt-5.5 none`, `gpt-5.5 xhigh`, and
`gpt-5.4-nano none`. Their exact model/reasoning rows currently reuse the
generic `langfuse-o3-poc` base prompt route rather than WM-hosted base samples,
so treat them as provisional WM26 onboarding estimates for testing-only
workflows rather than a final production-model decision. The hosted WM26
production model is still TBD. The `o3 medium` and `o3 high` rows below are
generic comparison estimates from local `prompt-v1` base rows rather than
WM-hosted base samples, so treat them as model-tradeoff planning numbers, not
as WM26 onboarding coverage. The `gpt-5-nano minimal` estimator command was:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model gpt-5-nano --reasoning-effort minimal
```

It reported `N=104: $0.008894080000`. The `gpt-5.5 none`,
`gpt-5.5 xhigh`, `gpt-5.4-nano none`, `o3 medium`, and `o3 high` values were
refreshed with the exact commands recorded below. Create and persist WM-hosted
base rows through the `estimate-experiment-cost-skill` workflow before
treating any additional scheduled WM26 model configuration as production-grade
estimate coverage.

## Match Counts And Repredictions

| Competition | Official match count | Reprediction basis | Projected calls without repredictions | Projected calls with repredictions |
| --- | ---: | --- | ---: | ---: |
| Bundesliga 2025/26 | 306 | Reused `pes-squad` / `o3` evidence from Firebase cost analysis | 306 | 493 |
| FIFA World Cup 2026 | 104 | Assumes no repredictions | 104 | 104 |

Sources:

- Bundesliga: the official Bundesliga fixture explainer states that Bundesliga
  and Bundesliga 2 each run across 34 matchdays and together comprise 612
  games, with each club playing 17 home and 17 away games. That yields 306
  Bundesliga matches. Source:
  <https://www.bundesliga.com/en/bundesliga/news/how-the-bundesliga-fixture-list-is-made-bayern-munich-borussia-dortmund-20316>
- FIFA World Cup 2026: FIFA's official schedule release names 104 matches.
  Source:
  <https://inside.fifa.com/organisation/media-releases/updated-world-cup-2026-match-schedule-venues-kick-off-times-104-matches>

Bundesliga reprediction evidence:

- Observed `pes-squad` / `o3` counts: index `0` = `313`, index `1` = `123`,
  index `2+` = `68`.
- Extra repredictions: `123 + 68 = 191`.
- Projected Bundesliga calls:
  `306 + round(306 * 191 / 313) = 493`.
- This basis fits Bundesliga best because the observed data came from
  Bundesliga 2025/26 and `o3` is the current production model.

The World Cup estimate assumes no repredictions. Unlike a club season, national
teams should not have Champions League, Europa League, domestic cup, or league
matches between tournament matchdays that would trigger comparable context
changes.

## Cost Estimates

These are flex-pricing estimates for all match predictions. They are not
historical production spend totals from the Firebase `cost` command, especially
for predictions that were generated before production moved to flex.

| Competition | Model config | Base row prompt route | Max output tokens | Cost without repredictions | Cost with repredictions |
| --- | --- | --- | ---: | ---: | ---: |
| Bundesliga 2025/26 | `gpt-5.5 none` | Langfuse `langfuse-o3-poc` | 10000 | `$2.662965000000` | `$4.290332500000` |
| Bundesliga 2025/26 | `gpt-5.5 low` | Langfuse `langfuse-o3-poc` | 10000 | `$3.572932500000` | `$5.756391250000` |
| Bundesliga 2025/26 | `gpt-5.5 medium` | Langfuse `langfuse-o3-poc` | 10000 | `$5.194120500000` | `$8.368305250000` |
| Bundesliga 2025/26 | `gpt-5.5 high` | Langfuse `langfuse-o3-poc` | 10000 | `$16.355623500000` | `$26.350726750000` |
| Bundesliga 2025/26 | `gpt-5.5 xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$28.968255000000` | `$46.671077500000` |
| Bundesliga 2025/26 | `gpt-5.4-nano none` | Langfuse `langfuse-o3-poc` | 10000 | `$0.109794330000` | `$0.176890865000` |
| Bundesliga 2025/26 | `gpt-5.4-nano xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$3.360748275000` | `$5.414538887500` |
| Bundesliga 2025/26 | `o3 medium` | local `prompt-v1` | 10000 | `$3.193599600000` | `$5.145243800000` |
| Bundesliga 2025/26 | `o3 high` | local `prompt-v1` | 10000 | `$7.353822600000` | `$11.847825300000` |
| FIFA World Cup 2026 | `gpt-5-nano minimal` | Langfuse `wm26-hosted-latest` | 10000 | `$0.008894080000` | `$0.008894080000` |
| FIFA World Cup 2026 | `gpt-5.5 none` | Langfuse `langfuse-o3-poc` | 10000 | `$0.905060000000` | `$0.905060000000` |
| FIFA World Cup 2026 | `gpt-5.5 low` | Langfuse `langfuse-o3-poc` | 10000 | `$1.214330000000` | `$1.214330000000` |
| FIFA World Cup 2026 | `gpt-5.5 medium` | Langfuse `langfuse-o3-poc` | 10000 | `$1.765322000000` | `$1.765322000000` |
| FIFA World Cup 2026 | `gpt-5.5 high` | Langfuse `langfuse-o3-poc` | 10000 | `$5.558774000000` | `$5.558774000000` |
| FIFA World Cup 2026 | `gpt-5.5 xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$9.845420000000` | `$9.845420000000` |
| FIFA World Cup 2026 | `gpt-5.4-nano none` | Langfuse `langfuse-o3-poc` | 10000 | `$0.037315720000` | `$0.037315720000` |
| FIFA World Cup 2026 | `gpt-5.4-nano xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$1.142215100000` | `$1.142215100000` |
| FIFA World Cup 2026 | `o3 medium` | local `prompt-v1` | 10000 | `$1.085406400000` | `$1.085406400000` |
| FIFA World Cup 2026 | `o3 high` | local `prompt-v1` | 10000 | `$2.499338400000` | `$2.499338400000` |

All base rows use service tier `flex` and treat input tokens as uncached for the
estimate. The existing `gpt-5.5`, `gpt-5.4-nano`, and `o3` rows use model
knowledge cutoff `2025-11-29` with sampling cutoff
`2025-12-01T00:00:00 Europe/Berlin (+01)`. The new `gpt-5-nano minimal` row
uses model knowledge cutoff `2024-05-31` with sampling cutoff
`2024-06-02T00:00:00 Europe/Berlin (+02)`. If a future run observes non-flex
fallbacks, estimate the standard-tier share separately instead of silently
mixing it into these all-flex totals.

Base estimate rows noted in this document:

- `gpt-5-nano minimal`: 20 observations from repeated-match-slice run family
  `2026-06-02t22-02-53z`, hosted WM prompt
  `kicktippai/wm26/predict-one-match` label `latest` version `2`, average
  `$0.000085520000` per match prediction, observed service tiers
  `{'flex': 20}`, no non-flex fallback. The sample uses historical `pes-squad`
  fixtures for scored estimates, so only the prompt route matches the WM26
  runtime configuration.

- `gpt-5.5 none`: 20 observations from seed evidence
  `gpt-5.5-none-2026-05-03t22-42-35z`, average
  `$0.008702500000` per match prediction, service tier `flex`, prompt route
  `langfuse-o3-poc`, max output tokens `10000`.
- `gpt-5.5 xhigh`: 20 observations from base estimate evidence
  `gpt-5.5-xhigh-base-estimate-2026-05-04.md`, average
  `$0.094667500000` per match prediction, service tier `flex`, prompt route
  `langfuse-o3-poc`, max output tokens `40000`.
- `gpt-5.4-nano none`: 20 observations from repeated-match-slice run
  `2026-05-17`, average `$0.000358805000` per match prediction, observed
  service tiers `{'flex': 20}`, no non-flex fallback, prompt route
  `langfuse-o3-poc`, max output tokens `10000`.
- `o3 medium`: 20 observations from seed evidence
  `o3-medium-2026-05-01t22-14-04z`, average `$0.010436600000` per match
  prediction, service tier `flex`, prompt route local `prompt-v1`, max output
  tokens `10000`.
- `o3 high`: preflight
  `preflight__pes-squad__o3__prompt-v1__reasoning-high__repeat-1-o3-high-preflight__exact-time__2026-05-04t22-16-09z`
  succeeded at the default `10000` output-token cap; 20-observation base
  estimate evidence `o3-high-base-estimate-2026-05-05.md` averaged
  `$0.024032100000` per match prediction, service tier `flex`, prompt route
  local `prompt-v1`, max output tokens `10000`.
- `gpt-5.5 medium`: 20 observations from run family
  `2026-05-10t22-57-59z`, average `$0.016974250000` per match prediction,
  observed service tiers `{'flex': 20}`, no non-flex fallback.
- `gpt-5.5 high`: preflight
  `preflight__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-high__repeat-1__exact-time__2026-05-10t22-56-09z`
  succeeded at the default `10000` output-token cap; 20-observation run family
  `2026-05-10t23-02-30z` averaged `$0.053449750000` per match prediction,
  observed service tiers `{'flex': 20}`, no non-flex fallback.

## Estimator Commands

`gpt-5-nano minimal`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model gpt-5-nano --reasoning-effort minimal
```

Fresh output summary:

```text
Average cost per match prediction: $0.000085520000
N=104: $0.008894080000
```

`gpt-5.5 none`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.5 --reasoning-effort none
```

Fresh output summary:

```text
Average cost per match prediction: $0.008702500000
N=104: $0.905060000000
N=306: $2.662965000000
N=493: $4.290332500000
```

`gpt-5.5 low`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.5 --reasoning-effort low
```

Fresh output summary:

```text
Average cost per match prediction: $0.011676250000
N=104: $1.214330000000
N=306: $3.572932500000
N=493: $5.756391250000
```

`gpt-5.5 medium`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.5 --reasoning-effort medium
```

Fresh output summary:

```text
Average cost per match prediction: $0.016974250000
N=104: $1.765322000000
N=306: $5.194120500000
N=493: $8.368305250000
```

`gpt-5.5 high`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.5 --reasoning-effort high
```

Fresh output summary:

```text
Average cost per match prediction: $0.053449750000
N=104: $5.558774000000
N=306: $16.355623500000
N=493: $26.350726750000
```

`gpt-5.5 xhigh`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.5 --reasoning-effort xhigh
```

Fresh output summary:

```text
Average cost per match prediction: $0.094667500000
N=104: $9.845420000000
N=306: $28.968255000000
N=493: $46.671077500000
```

`gpt-5.4-nano none`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.4-nano --reasoning-effort none
```

Fresh output summary:

```text
Average cost per match prediction: $0.000358805000
N=104: $0.037315720000
N=306: $0.109794330000
N=493: $0.176890865000
```

`gpt-5.4-nano xhigh`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model gpt-5.4-nano --reasoning-effort xhigh
```

Fresh output summary:

```text
Average cost per match prediction: $0.010982837500
N=104: $1.142215100000
N=306: $3.360748275000
N=493: $5.414538887500
```

`o3 medium`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model o3 --reasoning-effort medium
```

Fresh output summary:

```text
Average cost per match prediction: $0.010436600000
N=104: $1.085406400000
N=306: $3.193599600000
N=493: $5.145243800000
```

`o3 high`:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104,306,493 --model o3 --reasoning-effort high
```

Fresh output summary:

```text
Average cost per match prediction: $0.024032100000
N=104: $2.499338400000
N=306: $7.353822600000
N=493: $11.847825300000
```

## Firestore Read Notes

Firebase reads are scarce because the project uses the free plan. For future
whole-season estimates:

- Reuse documented reprediction evidence when it matches the competition,
  community context, and model closely enough.
- Only rerun `cost` when evidence is missing, stale, or explicitly refreshed.
- If a rerun is required, use the narrowest possible filters, for example:

```powershell
dotnet run --project src/Orchestrator -- cost --community-contexts pes-squad --models o3 --detailed-breakdown --output-json C:\tmp\kicktippai-cost-pes-squad-o3.json
```
