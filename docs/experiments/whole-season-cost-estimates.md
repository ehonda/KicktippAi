# Whole-Season Cost Estimates

Updated: 2026-05-11

This estimate projects match prediction cost for two full competitions and three
model configurations. Bonus question cost is excluded because it is negligible
relative to full-season match prediction cost.

Important pricing assumption: these estimates assume every match prediction is
billed at OpenAI `flex` pricing. Production is expected to use flex from here
on out. Actual spend can be higher if flex requests occasionally fall back to
standard processing after flex-capacity failures, but that share is expected to
stay low.

The dollar values below were regenerated immediately before this document was
written with `experiment_cost_estimator.py estimate`. No Firebase `cost` query
was rerun for this estimate; the Bundesliga reprediction evidence reuses the
already collected `pes-squad` / `o3` counts.

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
| Bundesliga 2025/26 | `gpt-5.5 xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$28.968255000000` | `$46.671077500000` |
| Bundesliga 2025/26 | `gpt-5.4-nano xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$3.360748275000` | `$5.414538887500` |
| Bundesliga 2025/26 | `o3 medium` | local `prompt-v1` | 10000 | `$3.193599600000` | `$5.145243800000` |
| FIFA World Cup 2026 | `gpt-5.5 xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$9.845420000000` | `$9.845420000000` |
| FIFA World Cup 2026 | `gpt-5.4-nano xhigh` | Langfuse `langfuse-o3-poc` | 40000 | `$1.142215100000` | `$1.142215100000` |
| FIFA World Cup 2026 | `o3 medium` | local `prompt-v1` | 10000 | `$1.085406400000` | `$1.085406400000` |

All base rows use service tier `flex`, treat input tokens as uncached for the
estimate, and use model knowledge cutoff `2025-11-29` with sampling cutoff
`2025-12-01T00:00:00 Europe/Berlin (+01)`. If a future run observes non-flex
fallbacks, estimate the standard-tier share separately instead of silently
mixing it into these all-flex totals.

## Estimator Commands

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
