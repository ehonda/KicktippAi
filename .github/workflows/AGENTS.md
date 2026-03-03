# Workflows — Agent Context

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
