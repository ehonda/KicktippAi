# Preflight Evidence

Updated: 2026-05-11

Use this file for one-item high-reasoning cap and cost probes. These rows are not valid base estimate rows and must not be inserted into `base-estimates.json`; use them to choose the first output-token cap and to estimate the expected spend before running a 5-by-4 base estimate.

| Model | Reasoning effort | Prompt route | Run name | Fixture / evaluation | Max output tokens | Input tokens | Output tokens | Reasoning tokens | Service tier | Observed cost (USD) | Cap outcome | Notes |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |
| `o3` | `high` | local `prompt-v1` | `preflight__pes-squad__o3__prompt-v1__reasoning-high__repeat-1-o3-high-preflight__exact-time__2026-05-04t22-16-09z` | VfB Stuttgart vs RB Leipzig, `2026-03-15T12:00:00 Europe/Berlin (+01)` | 10000 | 3260 | 6325 | 6272 | flex | 0.028560 | succeeded; below cap | Compact usage: `C:\tmp\kicktippai-o3-high-preflight-20260504-usage.json`; one-item point estimate for 20 observations is `$0.571200000000`; prediction `2-1`, scored 3 points. |
| `gpt-5.5` | `high` | Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`) | `preflight__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-high__repeat-1__exact-time__2026-05-10t22-56-09z` | VfB Stuttgart vs RB Leipzig, `2026-03-15T12:00:00 Europe/Berlin (+01)` | 10000 | 3260 | 2089 | 2070 | flex | 0.039485 | succeeded; below cap | Compact usage: `C:\tmp\kicktippai-gpt55-high-preflight-20260510-usage.json`; default cap used for full 20-observation base row `2026-05-10t23-02-30z`; prediction `2-1`, scored 3 points. |
| `gpt-5.5` | `xhigh` | Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`) | `preflight__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-1__exact-time__2026-05-04t21-10-03z` | VfB Stuttgart vs RB Leipzig, `2026-03-15T12:00:00 Europe/Berlin (+01)` | 40000 | 3260 | 5715 | 5696 | flex | 0.093875 | succeeded; no cap pressure | Compact usage: `C:\tmp\kicktippai-gpt55-xhigh-preflight-20260504-usage.json`; 20-observation data-gathering point estimate from this one item is `$1.877500000000`. |
