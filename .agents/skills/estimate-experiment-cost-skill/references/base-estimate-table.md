# Base Estimate Table

Updated: 2026-05-05

This Markdown table has been retired as the authoritative estimate store.
Use [base-estimates.json](base-estimates.json) through
`scripts/experiment_cost_estimator.py` instead.

Active lookup:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,60,100 --model o3 --reasoning-effort medium
```

Active row maintenance:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py upsert-row --input C:\tmp\kicktippai-cost-estimate-usage.json --model o3 --reasoning-effort medium --prompt-route "local prompt-v1" --model-knowledge-cutoff 2025-11-29 --sampling-cutoff "2025-12-01T00:00:00 Europe/Berlin (+01)" --max-output-tokens 10000 --source "base-estimate run family 2026-05-04"
```

Do not paste new rows into this Markdown file.
