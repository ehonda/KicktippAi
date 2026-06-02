# Workflows — Agent Context

## Workflow Activation Status

As of 2026-06-02, most season-specific community entrypoint workflows in this
directory are deactivated because the most recent active competition,
Bundesliga 2025 / 2026, has concluded. The files remain in place for future
reuse, so their presence alone should not be treated as evidence that the
corresponding automations are currently active.

`ehonda-ai-arena-gpt-5-nano-matchday.yml` and
`ehonda-ai-arena-gpt-5-nano-bonus.yml` are preliminary manual-only WM26 testing
entrypoints. Their cron schedules remain disabled. For this preliminary
self-contained test path, they use `community_context: "ehonda-ai-arena"`.

`ehonda-ai-arena-context-collection.yml` is the matching manual-only WM26
context workflow for that preliminary self-contained test path.

`rabetrabauken2026-context-collection.yml` is a manual-only WM26 reference
context workflow. It does not invoke `matchday` or `bonus`, so it does not make
`rabetrabauken2026` a production prediction community for Langfuse environment
tagging.

## Production Communities and Langfuse Environments

Each command (`matchday`, `bonus`) determines its Langfuse trace environment (`production` vs `development`) based on whether the `community` parameter matches a **production community**. A community is a production community for a given command if there is a workflow in `.github/workflows/` that targets that community and invokes that command.

### Current Production Communities

#### Matchday Command

Derived from workflows: `pes-squad-matchday.yml`, `schadensfresse-matchday.yml`, `ehonda-ai-arena-*-matchday.yml`

- `pes-squad`
- `schadensfresse`
- `ehonda-ai-arena`

#### Bonus Command

Derived from workflows: `pes-squad-bonus.yml`, `schadensfresse-bonus.yml`, `ehonda-ai-arena-*-bonus.yml`

- `pes-squad`
- `schadensfresse`
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
