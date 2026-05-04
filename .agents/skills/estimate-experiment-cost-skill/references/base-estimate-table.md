# Base Estimate Table

Updated: 2026-05-04

Use this table as the first lookup source for KicktippAi `slice` and `repeated-match` experiment cost estimates. Rows are keyed primarily by model name and reasoning effort. Prompt route, max output token count, and sampling cutoff are qualifiers that should be called out when the planned run differs from the observed row.

Base rows use five random repeated-match fixtures with four predictions each, for `N = 20` observations, and flex pricing. Values are emitted by `scripts/experiment_cost_estimator.py` from total input tokens and total output tokens. The calculation treats all input as uncached even when the observed calls had cache hits.

The initial rows use `2025-11-29` as the stored model knowledge cutoff date because their sampling cutoff was `2025-12-01T00:00:00 Europe/Berlin (+01)`, which is the cutoff date plus the required two-day safety margin. See [seed-evidence.md](seed-evidence.md) for run-family evidence.

| Model | Reasoning effort | Prompt route | Model knowledge cutoff date | Sampling cutoff used | Max output tokens | Base sample observations | Total input tokens | Estimated uncached input cost (USD) | Total output tokens | Estimated output cost (USD) | Estimated total cost (USD) | Average cost per match prediction (USD) | Source |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `o3` | `medium` | local `prompt-v1` | `2025-11-29` | `2025-12-01T00:00:00 Europe/Berlin (+01)` | 10000 | 20 | 67580 | 0.067580000000 | 35288 | 0.141152000000 | 0.208732000000 | 0.010436600000 | Initial seed evidence: `o3-medium-2026-05-01t22-14-04z`. |
| `gpt-5.4-nano` | `xhigh` | Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`) | `2025-11-29` | `2025-12-01T00:00:00 Europe/Berlin (+01)` | 40000 | 20 | 67580 | 0.006758000000 | 340638 | 0.212898750000 | 0.219656750000 | 0.010982837500 | Initial seed evidence: `gpt-5.4-nano-xhigh-2026-05-03t22-42-35z`; default 10000 and 20000 caps were insufficient before the completed `maxout-40000` run. |
| `gpt-5.5` | `none` | Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`) | `2025-11-29` | `2025-12-01T00:00:00 Europe/Berlin (+01)` | 10000 | 20 | 67580 | 0.168950000000 | 340 | 0.005100000000 | 0.174050000000 | 0.008702500000 | Initial seed evidence: `gpt-5.5-none-2026-05-03t22-42-35z`. |
| `gpt-5.5` | `xhigh` | Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`) | `2025-11-29` | `2025-12-01T00:00:00 Europe/Berlin (+01)` | 40000 | 20 | 66560 | 0.166400000000 | 115130 | 1.726950000000 | 1.893350000000 | 0.094667500000 | Base estimate evidence: [gpt-5.5-xhigh-base-estimate-2026-05-04.md](gpt-5.5-xhigh-base-estimate-2026-05-04.md). |

## Maintenance

When adding a row:

- Store the original model knowledge cutoff date, not the two-day safety-margin sampling cutoff.
- Store the actual max output token count used. Use `10000` when no explicit override was needed.
- Use `scripts/experiment_cost_estimator.py base-row` to emit the cost columns. Do not hand-calculate or use observed cached Langfuse `totalCost` for estimates.
- Record only completed base estimates with exactly 20 successful flex observations and no output-cap hits.
- Preserve enough source detail to regenerate the usage pull: run names, run-family labels, compact usage artifact path, or Langfuse query notes.
