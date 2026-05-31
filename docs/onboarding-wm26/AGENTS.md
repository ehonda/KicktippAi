# WM26 Onboarding Agent Instructions

## Mandatory Lineup Context

Always use `collect-context lineups` when creating, enriching, uploading, or validating WM26 lineup context. WM26 communities require:

- per-team Firestore context documents named `lineup-*.csv`
- aggregate Firestore KPI document `lineups`

Do not run `matchday-dev` until the required `lineup-*` context documents exist for the match teams. Do not run `bonus-dev` until the `lineups` KPI document exists.

## Data Sources

Use official FIFA lineup or squad material for official membership once available. Use `dcaribou/transfermarkt-datasets` as the only supplemental source for coach, age, position, and market-value data. `collect-context lineups` refreshes the upstream CC0 DuckDB database into an ignored cache by default; do not commit the database, generated payloads, or private source notes.

Lineup CSV payloads must use `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`. Provisional vs official source state is documented, not passed as a command flag, and must not be written into prompt context as a CSV column.

FIFA final squad lists are expected on 2 June 2026, when FIFA announces the submitted final 26-player lists. Treat earlier squad announcements as provisional. Once the final FIFA squad lists are available, update `data/wm26/lineups/lineups-seed.csv` to official full-squad membership and refresh `lineup-*` context documents plus the `lineups` KPI document with `collect-context lineups`. This workflow keeps full squads in context, not only match starters.

Do not scrape websites for supplemental lineup values in this context.

## Workflow Activation

For testing-only onboarding, leave GitHub Actions schedules inactive and record
that status in `model-config-onboarding.md`.

For every new WM26 community, activate its scheduled context collection
workflow before enabling scheduled prediction workflows. That context workflow
must run Kicktipp context collection, `collect-context fifa`, and
`collect-context lineups` for the community with
`--competition fifa-world-cup-2026`.

## Model Configuration Ledger

When onboarding or changing a WM26 model configuration, update
`model-config-onboarding.md` with the community, competition, model, reasoning
effort, prompt route, where the configuration is wired, workflow activation
status, and full-competition estimate status.

Do not describe `gpt-5-nano` / `minimal` as the production default. It is only
the guarded `matchday-dev` and `bonus-dev` dev/test default. Production
workflows must pass an explicit model and reasoning effort after the production
configuration is selected. Use the reusable prediction workflow
`reasoning_effort` input for that wiring.

If the exact model and reasoning effort are not documented in
`../experiments/whole-season-cost-estimates.md`, use the project
`estimate-experiment-cost-skill` workflow before scheduled activation. Do not
hand-calculate missing dollar estimates.

## Manual Follow-Up Reporting

When autonomous onboarding finishes, include a manual follow-up section in the
final response. Call out GitHub Actions secrets and variables, scheduled
workflow activation status, production model decision status, cost estimate
coverage, explicit model/reasoning workflow wiring, Kicktipp/Firebase access
validation, Langfuse key and prompt setup, and the first manual workflow trigger
sequence.

## Verification Workflow

Always execute this verification workflow when changing WM26 context behavior, lineup behavior, onboarding docs, or prompt-context routing:

```powershell
dotnet run --project src/Orchestrator -- matchday-dev -c ehonda-dev-wm26 --verbose
dotnet run --project src/Orchestrator -- bonus-dev -c ehonda-dev-wm26 --verbose
```

Run `dotnet` and `git` outside the sandbox in this repository.

Use `$langfuse` and the installed `langfuse` CLI to inspect the resulting development traces. Record trace or observation IDs and verify:

- matchday traces include only the two participating teams' `lineup-*` docs
- bonus traces include `lineups` only for `Welche Mannschaft stellt den Spieler mit den meisten Toren?`
- hosted prompt fallback is false
- WM26 dev/test default reasoning effort is present for dev shortcut traces

If verification cannot be completed, record the exact skipped command and blocker in the final response.
