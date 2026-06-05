# Workflows — Agent Context

## Workflow Activation Status

As of 2026-06-06, most season-specific community entrypoint workflows in this
directory are deactivated because the most recent active competition,
Bundesliga 2025 / 2026, has concluded. The files remain in place for future
reuse, so their presence alone should not be treated as evidence that the
corresponding automations are currently active.

`wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml` and
`wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml` are preliminary scheduled
WM26 onboarding entrypoints. For this self-contained path, they use
`community_context: "ehonda-ai-arena"` and display `🏆` in the GitHub Actions
UI.

`wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml`,
`wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml`,
`wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml`, and
`wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml` are additional manual-only
WM26 onboarding test entrypoints. They keep
`community_context: "ehonda-ai-arena"` aligned with the shared self-contained
context workflow, and the `gpt-5.5 xhigh` pair explicitly passes
`max_output_tokens: 40000`.

`wm26-ehonda-ai-arena-context-collection.yml` is the matching scheduled WM26
context workflow for that preliminary self-contained test path.

`rabetrabauken2026-context-collection.yml` is a manual-only WM26 reference
context workflow. It does not invoke `matchday` or `bonus`, so it does not make
`rabetrabauken2026` a production prediction community for Langfuse environment
tagging.

The WM26 secondary copy-from-primary pattern is reserved only for the
yet-undetermined `rabetrabauken2026` production model path: a primary
`rabetrabauken2026` prediction workflow must run first, and the matching
`ehonda-ai-arena` workflow may then post the stored prediction with
`community_context: "rabetrabauken2026"`. Do not apply that pattern to
preliminary `ehonda-ai-arena` tests, dev shortcuts, or unrelated WM26 model
experiments.

## Production Communities and Langfuse Environments

Each command (`matchday`, `bonus`) determines its Langfuse trace environment (`production` vs `development`) based on whether the `community` parameter matches a **production community**. A community is a production community for a given command if there is a workflow in `.github/workflows/` that targets that community and invokes that command.

### Current Production Communities

#### Matchday Command

Derived from workflows: `pes-squad-matchday.yml`, `schadensfresse-matchday.yml`, and the active `ehonda-ai-arena` matchday entrypoints including `wm26-ehonda-ai-arena-*-matchday.yml`

- `pes-squad`
- `schadensfresse`
- `ehonda-ai-arena`

#### Bonus Command

Derived from workflows: `pes-squad-bonus.yml`, `schadensfresse-bonus.yml`, and the active `ehonda-ai-arena` bonus entrypoints including `wm26-ehonda-ai-arena-*-bonus.yml`

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
