# Seed Evidence

This file preserves the initial base estimate evidence used to seed [base-estimates.json](base-estimates.json). The rows are based on the same 5-random-fixture by 4-repetition design used by the skill's base estimate method.

All rows:

- Community context: `pes-squad`
- Sampling cutoff: `2025-12-01T00:00:00 Europe/Berlin (+01)`
- Stored model knowledge cutoff date: `2025-11-29`
- Repeated-match shape: five repeated-match runs, four predictions per run, `N = 20`
- Repeated-match runner setting: `--batch-count 1`
- Pricing basis: flex processing, with all input tokens treated as uncached for the estimate

## `o3-medium-2026-05-01t22-14-04z`

- Model: `o3`
- Reasoning effort: `medium`
- Prompt route: local `prompt-v1`
- Max output tokens: default `10000`
- Total input tokens: `67580`
- Total output tokens: `35288`
- Run names:
  - `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z`
  - `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z`
  - `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z`
  - `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z`
  - `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z`

## `gpt-5.4-nano-xhigh-2026-05-03t22-42-35z`

- Model: `gpt-5.4-nano`
- Reasoning effort: `xhigh`
- Prompt route: Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`)
- Max output tokens: `40000`
- Cap finding: default `10000` and `20000` were insufficient; completed base runs used `maxout-40000`
- Total input tokens: `67580`
- Total output tokens: `340638`
- Run names:
  - `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z`

## `gpt-5.5-none-2026-05-03t22-42-35z`

- Model: `gpt-5.5`
- Reasoning effort: `none`
- Prompt route: Langfuse `langfuse-o3-poc` (`kicktippai/predict-one-match-o3-poc`, label `poc`)
- Max output tokens: default `10000`
- Total input tokens: `67580`
- Total output tokens: `340`
- Run names:
  - `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z`
  - `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z`
