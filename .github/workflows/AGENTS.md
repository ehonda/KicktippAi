# Workflows — Agent Context

## Workflow Activation Status

As of 2026-08-27, ADR-0052's complete Bundesliga 2026/27 caller matrix is
present. `pes-squad` is the independent Sol/`xhigh` primary;
`schadensfresse`, `relaxdays-tippt`, and arena Sol/`xhigh` copy `pes-squad` for
ordinary Bundesliga work; arena
Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none` are self-contained.
All rows pin cap `10000`, match v3 / bonus v1, and Flex-first / Standard-
fallback. Every current caller exposes only `workflow_dispatch`; no current
caller has `schedule` or `workflow_call`. P0-21 owns all dispatch and activation.

The manual `schadensfresse` bonus leaf defaults its inclusive strict-UTC scope
to `2026-08-28T18:30:00Z`. Its exact five-question alias policy is not generic
normalization; the three CL questions due September 9 remain P1-08. Do not add
the row to the recurring outer lane before its manual context/copy ladder is
green, and do not add bonus to that lane.

The reusable context workflow's `publish_launch_roster_overlay` input is false
by default. Set it only for an accepted initial Bundesliga production roster
publication. Current `pes-squad`, `relaxdays-tippt`, and prepared
`schadensfresse` callers set it true; it must download the exact audited R2
artifact, pass SHA/revision/date plus both launch flags to `collect-context
rosters`, and complete before the normal profile. Arena callers must keep it
absent because their shared community context already has the verified enriched
last-known-good head. Do not loosen the pins, reorder profile before overlay,
or turn this initial gate into recurring DuckDB refresh automation.

Participant-specific Actions workflows map their exact posting-participant
secret pair directly. Local commands use `--kicktipp-credential-profile` to
select the equivalent sibling file. Neither path selects credentials from a
copied `community_context`. The exact canonical Actions pairs are
Owner-confirmed provisioned, which is not runtime authentication/readiness or
POST evidence.

As of 2026-06-06, most season-specific community entrypoint workflows in this
directory are deactivated because the most recent active competition,
Bundesliga 2025 / 2026, has concluded. The files remain in place for future
reuse, so their presence alone should not be treated as evidence that the
corresponding automations are currently active.

As of 2026-08-13, all WM26 entrypoint workflows are deactivated: their
`schedule` and `workflow_dispatch` triggers are removed because the tournament
has concluded. The files keep `workflow_call` only so they remain valid for
future reuse.

The following 2026-08-25 subsection is historical prerequisite evidence. It is
superseded by the 2026-08-27 matrix above where it calls prediction callers or
model values unresolved.

As of 2026-08-25, the Bundesliga 2026/27 Actions entrypoints included the
manual-only, model-independent production context callers:

- `pes-squad-context-collection.yml`; and
- `schadensfresse-context-collection.yml`.

They expose `workflow_dispatch` only, have no inputs or schedule, pin their
respective `community_context`, pin competition `bundesliga-2026-27`, and pass
literal trigger type `manual` to the reusable context workflow. Their accepted
community-specific Kicktipp credentials and shared Firebase mappings remain
unchanged. At that date, final production matchday and bonus callers remained
gated on the Owner selection; the old production prediction callers remain
retired.

The current Actions entrypoints also include the manual-only self-contained
arena Luna validation triad:

- `buli2627-ehonda-ai-arena-context-collection.yml`;
- `buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml`; and
- `buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml`.

They expose `workflow_dispatch` only and have no checked-in schedule or
`workflow_call`. They target and source context from `ehonda-ai-arena`, pin
competition `bundesliga-2026-27`, and use the authorized plumbing identity
`gpt-5.6-luna` / `none` / `10000` with production-labelled hosted prompt
versions `3` (match) and `1` (bonus). The bonus caller pins the accepted
`20`-document and `32000`-estimated-token budgets. P0-20 owns dispatch and any
separately authorized temporary arena schedule; the triad itself creates no
schedule. Its traces are classified as Langfuse `production` because the arena
is a production posting target, which does not promote Luna/none to the final
production model.

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
They use
`community_context: "ehonda-ai-arena"` and display `🏆` in the GitHub Actions
UI.

`wm26-rabetrabauken2026-o3-high-matchday.yml` and
`wm26-rabetrabauken2026-o3-high-bonus.yml` are the selected WM26 primary
production workflows. They target
`rabetrabauken2026`, use `community_context: "rabetrabauken2026"`, and pin
`max_output_tokens: 40000`.

`wm26-ehonda-ai-arena-o3-high-matchday.yml` and
`wm26-ehonda-ai-arena-o3-high-bonus.yml` are the selected WM26 secondary
copy-posting workflows. They target
`ehonda-ai-arena`, reuse `community_context: "rabetrabauken2026"`, and pin
`max_output_tokens: 40000`.

The additional self-contained onboarding and comparison entrypoints keep
`community_context: "ehonda-ai-arena"` aligned with the shared self-contained
context workflow; the `gpt-5.5 xhigh` pair explicitly passes
`max_output_tokens: 40000`.

`wm26-ehonda-ai-arena-context-collection.yml` is the matching deactivated WM26
context workflow for the self-contained `ehonda-ai-arena` path.

`rabetrabauken2026-context-collection.yml` is the deactivated WM26 reference
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

These lists define the command's retained production-environment
classification; they do not imply that a workflow trigger is active. The WM26
files cited below are historical, inert `workflow_call`-only entrypoints.

#### Matchday Command

Derived from retained workflows: `pes-squad-matchday.yml`, `schadensfresse-matchday.yml`, `wm26-rabetrabauken2026-o3-high-matchday.yml`, the historical/inert `wm26-ehonda-ai-arena-*-matchday.yml` files, and the manual-only Bundesliga arena Luna matchday entrypoint

- `pes-squad`
- `schadensfresse`
- `relaxdays-tippt`
- `rabetrabauken2026`
- `ehonda-ai-arena`

#### Bonus Command

Derived from retained workflows: `pes-squad-bonus.yml`, `schadensfresse-bonus.yml`, `wm26-rabetrabauken2026-o3-high-bonus.yml`, the historical/inert `wm26-ehonda-ai-arena-*-bonus.yml` files, and the manual-only Bundesliga arena Luna bonus entrypoint

- `pes-squad`
- `schadensfresse`
- `relaxdays-tippt`
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
