# WM26 Model Configuration Onboarding

Updated: 2026-05-31

This ledger tracks which FIFA World Cup 2026 model configurations are
onboarded, where they are wired, and whether their full-competition match
prediction costs are documented. Scheduled activation is intentionally pending
while testing starts.

The checked-in `gpt-5-nano` / `minimal` fallback is for dev work and low-cost
manual testing only. It is not the WM26 production model configuration.
Production is still TBD and must be added here explicitly before scheduled
prediction workflows are activated.

Use one row per effective model configuration: model, reasoning effort, prompt
route, prompt label/version policy, community, and workflow status. Before
activating a scheduled prediction workflow, make sure the matching cost estimate
is present in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md).

## Current WM26 Configurations

| Community / use | Competition | Model config | Prompt route | Where onboarded | Workflow status | Full-competition estimate |
| --- | --- | --- | --- | --- | --- | --- |
| `ehonda-dev-wm26` dev/testing fallback | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Omitted-model fallback in `src/Orchestrator/Commands/Shared/PredictionServiceCommandSupport.cs` and prompt defaults in `src/Orchestrator/Infrastructure/CompetitionResolver.cs`; docs in `docs/onboarding-wm26/README.md` | Manual dev commands only; no GitHub Actions schedule activated; not a production config | Missing. `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model gpt-5-nano --reasoning-effort minimal` reported no matching base estimate row on 2026-05-31. |
| WM26 production predictions | `fifa-world-cup-2026` | TBD; must be more capable than the dev/testing fallback | TBD | Not onboarded yet | Not activated | Missing until production model and reasoning effort are selected. |

No WM26 production prediction workflow is currently activated. When adding bulk
activation workflows, list every community/model pair here before enabling
schedules.

## Activation Checklist

- Confirm the community resolves to `fifa-world-cup-2026`.
- Record the exact model, reasoning effort, prompt source, prompt name, prompt
  label or version, max output token cap, and service tier policy.
- Document where the configuration is wired: code defaults, workflow files,
  manual command docs, or community-specific configuration.
- Verify scheduled prediction workflows pass both the selected model and
  reasoning effort explicitly. Add reusable workflow support for
  reasoning-effort input before production activation if needed.
- Check [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md)
  for the same model and reasoning effort. If it is absent, run the
  `estimate-experiment-cost-skill` workflow before activation.
- Keep context collection scheduled before prediction workflows. During
  testing-only onboarding, leave schedules inactive and record that status here.
- Once schedules are activated, update this ledger with the workflow file paths
  and activation date.
