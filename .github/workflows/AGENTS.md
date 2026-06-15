# Workflows — Agent Context

## Workflow Activation Status

As of 2026-06-06, most season-specific community entrypoint workflows in this
directory are deactivated because the most recent active competition,
Bundesliga 2025 / 2026, has concluded. The files remain in place for future
reuse, so their presence alone should not be treated as evidence that the
corresponding automations are currently active.

As of 2026-06-15, all WM26 bonus entrypoint workflows are also deactivated:
their `schedule` and `workflow_dispatch` triggers are commented out because the
tournament has started, bonus predictions are locked in, and further runs are
just no-op runner time plus a few database lookups. The files keep
`workflow_call` only so they remain valid for future reuse.

`wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml`,
`wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml`,
`wm26-ehonda-ai-arena-o3-medium-matchday.yml`, and
`wm26-ehonda-ai-arena-o3-medium-bonus.yml` are WM26 self-contained entrypoints.
Their matchday variants remain scheduled, while their bonus variants are
deactivated after the tournament start. They use
`community_context: "ehonda-ai-arena"` and display `🏆` in the GitHub Actions
UI.

`wm26-rabetrabauken2026-o3-high-matchday.yml` and
`wm26-rabetrabauken2026-o3-high-bonus.yml` are the selected WM26 primary
production workflows. Their matchday variant remains scheduled, while the bonus
variant is deactivated after the tournament start. They target
`rabetrabauken2026`, use `community_context: "rabetrabauken2026"`, and pin
`max_output_tokens: 40000`.

`wm26-ehonda-ai-arena-o3-high-matchday.yml` and
`wm26-ehonda-ai-arena-o3-high-bonus.yml` are the selected WM26 secondary
copy-posting workflows. Their matchday variant remains scheduled, while the
bonus variant is deactivated after the tournament start. They target
`ehonda-ai-arena`, reuse `community_context: "rabetrabauken2026"`, and pin
`max_output_tokens: 40000`.

The additional self-contained onboarding and comparison entrypoints keep
`community_context: "ehonda-ai-arena"` aligned with the shared self-contained
context workflow; the `gpt-5.5 xhigh` pair explicitly passes
`max_output_tokens: 40000`.

`wm26-ehonda-ai-arena-context-collection.yml` is the matching scheduled WM26
context workflow for the self-contained `ehonda-ai-arena` path.

`rabetrabauken2026-context-collection.yml` is the scheduled WM26 reference
context workflow for the selected production path.

WM26 context workflows call the reusable base context workflow, which applies
the recent-history date map in guarded mode after Kicktipp collection. Keep
`--apply-known-only --preserve-collected-on-or-after 2026-06-11` on that step
so newly collected tournament rows are preserved and cannot consume older
pre-WM26 map entries with the same matchup key.

The WM26 secondary copy-from-primary pattern is selected only for `o3 high`: a
primary `rabetrabauken2026` prediction workflow must run first, and the
matching `ehonda-ai-arena` workflow may then post the stored prediction with
`community_context: "rabetrabauken2026"`. Do not apply that pattern to the
self-contained `gpt-5-nano minimal` path, `o3 medium`, dev shortcuts, or
unrelated WM26 model experiments.

## Production Communities and Langfuse Environments

Each command (`matchday`, `bonus`) determines its Langfuse trace environment (`production` vs `development`) based on whether the `community` parameter matches a **production community**. A community is a production community for a given command if there is a workflow in `.github/workflows/` that targets that community and invokes that command.

### Current Production Communities

#### Matchday Command

Derived from workflows: `pes-squad-matchday.yml`, `schadensfresse-matchday.yml`, `wm26-rabetrabauken2026-o3-high-matchday.yml`, and the active `ehonda-ai-arena` matchday entrypoints including `wm26-ehonda-ai-arena-*-matchday.yml`

- `pes-squad`
- `schadensfresse`
- `rabetrabauken2026`
- `ehonda-ai-arena`

#### Bonus Command

Derived from workflows: `pes-squad-bonus.yml`, `schadensfresse-bonus.yml`, `wm26-rabetrabauken2026-o3-high-bonus.yml`, and the active `ehonda-ai-arena` bonus entrypoints including `wm26-ehonda-ai-arena-*-bonus.yml`

- `pes-squad`
- `schadensfresse`
- `rabetrabauken2026`
- `ehonda-ai-arena`

### Keeping Code in Sync

The production community lists are hard-coded in each command class:

- `MatchdayCommand.ProductionCommunities` in `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs`
- `BonusCommand.ProductionCommunities` in `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs`

**When adding or removing a community workflow**, update the corresponding `ProductionCommunities` set in the command class. The `RandomMatchCommand` always uses the `development` environment and does not need updating.

Tests verifying the environment tagging are located in:

- `tests/Orchestrator.Tests/Commands/Operations/Matchday/MatchdayCommand_Telemetry_Tests.cs`
- `tests/Orchestrator.Tests/Commands/Operations/Bonus/BonusCommand_Telemetry_Tests.cs`
- `tests/Orchestrator.Tests/Commands/Operations/RandomMatch/RandomMatchCommand_Telemetry_Tests.cs`
