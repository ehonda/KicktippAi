# Multi-Community Automated Predictions Workflows

This directory contains GitHub Actions workflows that automate the process of generating and posting matchday and bonus predictions for multiple Kicktipp communities in the KicktippAi project.

## Current activation status

As of 2026-08-22, Bundesliga 2025/26 and WM26 community entrypoints are
historical and inert: they retain `workflow_call` only, with no active
`workflow_dispatch` or `schedule` trigger. Former schedule and model
descriptions later in this document are historical evidence, not activation
evidence.

The only Bundesliga 2026/27 Actions entrypoints are the exact manual-only
`ehonda-ai-arena` Luna validation triad documented below. P0-17 records the
[authoritative community matrix](../../docs/onboarding-bundesliga-2026-27/community-onboarding.md),
P0-18 established the reusable workflow contract, and P0-21 alone enables
final production schedules. The separately authorized Luna/none arena
validation schedule was governed by ADR-0047 and is no longer present in the
checked-in workflows.

## Architecture Overview

The workflow system is built on a **reusable workflow architecture** that supports multiple communities with individual configurations and schedules:

### Base Workflows (Reusable Components)

- **`base-matchday-predictions.yml`**: Core logic for matchday predictions
- **`base-bonus-predictions.yml`**: Core logic for bonus predictions
- **`base-context-collection.yml`**: Core logic for context collection and storage

### Community-Specific Workflows

Each community gets its own set of workflows that call the base workflows with specific configurations:

- **`{community}-matchday.yml`**: Matchday predictions for a specific community
- **`{community}-bonus.yml`**: Bonus predictions for a specific community

### Bundesliga 2026/27 manual arena validation triad

- **`buli2627-ehonda-ai-arena-context-collection.yml`**: Manual profile-driven
  context collection for `ehonda-ai-arena` and `bundesliga-2026-27`.
- **`buli2627-ehonda-ai-arena-gpt-5-6-luna-none-matchday.yml`**: Manual
  self-contained matchday validation with `gpt-5.6-luna`, reasoning `none`,
  output cap `10000`, and the production-labelled hosted match prompt pinned to
  version `2`.
- **`buli2627-ehonda-ai-arena-gpt-5-6-luna-none-bonus.yml`**: Manual
  self-contained bonus validation with the same model identity, hosted bonus
  prompt version `1`, and context budgets `20` documents / `32000` estimated
  tokens.

All three files expose `workflow_dispatch` only and have no schedule or
`workflow_call`. The prediction workflows use the reserved Luna arena Kicktipp
credential pair; context and predictions use the shared Firebase configuration,
predictions additionally use OpenAI and Langfuse secrets, and the reusable
workflow reads `LANGFUSE_PUBLIC_KEY` from the repository variable. P0-20 must
record a successful context dispatch before either prediction dispatch. Arena
traces use the Langfuse `production` environment because the posting target is a
production community; this does not promote Luna/none to the production model.

### Context Collection Workflows (historical entrypoints)

- **`pes-squad-context-collection.yml`**: Inert retained context caller for `pes-squad`
  - Retains `workflow_call` only; its former 12-hour cadence and manual trigger are inactive
- **`schadensfresse-context-collection.yml`**: Inert retained context caller for `schadensfresse`
  - Retains `workflow_call` only; its former 12-hour cadence and manual trigger are inactive
- **`rabetrabauken2026-context-collection.yml`**: Historical WM26 reference context collection (`workflow_call` only)
  - Runs Kicktipp collection, guarded recent-history date-map application, FIFA ranking, and lineup context collection for `fifa-world-cup-2026`
  - Formerly used the WM26 context cadence: 23:47, 06:47, and 11:47 UTC
  - Feeds the selected `o3 high` primary and secondary production workflows
- **`wm26-ehonda-ai-arena-context-collection.yml`**: Historical WM26 self-contained context collection (`workflow_call` only)
  - Runs Kicktipp collection, guarded recent-history date-map application, FIFA ranking, and lineup context collection for `ehonda-ai-arena`
  - Formerly used the WM26 context cadence: 23:47, 06:47, and 11:47 UTC
  - Feeds the self-contained `ehonda-ai-arena` WM26 workflows such as `gpt-5-nano minimal`, `gpt-5.5 none`, `gpt-5.5 xhigh`, `gpt-5.4-nano none`, and `o3 medium`

### WM26 Prediction Workflows (historical entrypoints)

All WM26 entrypoint workflows are now `workflow_call`-only. The former
production selections and cadences below are retained as historical design
evidence and must not be copied into Bundesliga 2026/27.

- **`wm26-rabetrabauken2026-o3-high-matchday.yml`**: Historical WM26 primary production matchday predictions
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"`
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-rabetrabauken2026-o3-high-bonus.yml`**: Deactivated WM26 primary production bonus predictions
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"`
  - Keeps the old WM26 bonus cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-o3-high-matchday.yml`**: Historical WM26 secondary production matchday copy-posting
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"` so it reuses the stored primary prediction
  - Formerly used the slower secondary cadence: 01:47, 08:47, and 13:47 UTC
- **`wm26-ehonda-ai-arena-o3-high-bonus.yml`**: Deactivated WM26 secondary production bonus copy-posting
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"` so it reuses the stored primary prediction
  - Keeps the slower secondary cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml`**: Historical WM26 self-contained matchday predictions
  - Uses `gpt-5-nano` with `reasoning_effort: "minimal"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml`**: Deactivated WM26 self-contained bonus predictions
  - Uses `gpt-5-nano` with `reasoning_effort: "minimal"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Keeps the WM26 bonus cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-o3-medium-matchday.yml`**: Historical WM26 self-contained matchday comparison
  - Uses `o3` with `reasoning_effort: "medium"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained comparison path
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-o3-medium-bonus.yml`**: Deactivated WM26 self-contained bonus comparison
  - Uses `o3` with `reasoning_effort: "medium"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained comparison path
  - Keeps the WM26 bonus cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml`**: Historical WM26 onboarding matchday test
  - Uses `gpt-5.5` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml`**: Deactivated WM26 onboarding bonus test
  - Uses `gpt-5.5` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Keeps the WM26 bonus cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml`**: Historical WM26 onboarding matchday test
  - Uses `gpt-5.5` with `reasoning_effort: "xhigh"`
  - Pins `max_output_tokens: 40000` to match the documented xhigh estimate assumptions
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml`**: Deactivated WM26 onboarding bonus test
  - Uses `gpt-5.5` with `reasoning_effort: "xhigh"`
  - Pins `max_output_tokens: 40000` to match the documented xhigh estimate assumptions
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Keeps the WM26 bonus cadence commented out for future reuse
- **`wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml`**: Historical WM26 onboarding matchday test
  - Uses `gpt-5.4-nano` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Formerly used the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml`**: Deactivated WM26 onboarding bonus test
  - Uses `gpt-5.4-nano` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Keeps the WM26 bonus cadence commented out for future reuse

WM26 workflow display names should include `🏆` so they are easy to distinguish
from Bundesliga workflows in the GitHub Actions UI. New WM26 workflow filenames
should use a `wm26-` prefix instead of reusing Bundesliga-era community/model
filenames.

The formerly scheduled self-contained `ehonda-ai-arena` workflows and
secondary `o3 high` workflows coexisted because they used different model
configurations, model-specific posting credentials, and `community_context`
values. Every retained WM26 entrypoint is now inert.

### Cost Analysis Workflow

- **`cost-analysis.yml`**: Manual cost analysis for all prediction activities
  - No longer scheduled because it performs many Firestore reads
  - Analyzes costs for all community configurations using a matrix strategy
  - Configurations analyzed: `all.json`, `ehonda-ai-arena.json`, `pes-squad.json`, `schadensfresse.json`
  - Can be manually triggered
  - Shows a `⚠️` warning before execution because of the Firestore read cost
  - Provides detailed cost breakdown and observability into prediction expenses

## How It Works

### Prediction Workflows

Each prediction workflow implements the core prediction loop:

1. **Configuration Parsing**: Extract community-specific settings from inputs
2. **Verification**: Check if predictions are needed for the community with `verify MODEL --community COMMUNITY --init-matchday --agent`
3. **Prediction**: Generate and post predictions if verification fails or force is enabled
4. **Final Check**: Verify that predictions were successfully posted with `verify MODEL --community COMMUNITY --agent`

### Context Collection Process

Context collection workflows gather and store contextual data for multiple communities:

1. **Environment Setup**: Configure Kicktipp and Firebase credentials
2. **Profile Resolution**: Resolve the exact competition profile and its ordered collectors
3. **Context Gathering**: Run `collect-context profile` for the exact competition and community context. Bundesliga runs Kicktipp with included played-date reconstruction, Club Elo, and rosters; WM26 retains its Kicktipp, date-map, FIFA, and lineup profile.
4. **Database Storage**: Store context documents in Firebase with version control
5. **Duplicate Detection**: Skip unchanged context to avoid redundant storage

## Community Configuration

Each community workflow is configured with direct parameters:

- **`community`**: Kicktipp community name
- **`model`**: Required, pinned OpenAI model identity for predictions
- **`reasoning_effort`**: Required, pinned OpenAI reasoning effort
- **`max_output_tokens`**: Required, positive output-token cap
- **`community_context`**: Community context when generating predictions (or using stored ones from the database)
- **`competition`**: Required competition identifier in context, matchday, and bonus workflows
- **`prompt_source` / prompt name, label, and version**: Required prompt route and exact hosted identity when `langfuse` is selected

### Bundesliga 2026/27 sequencing contract

The reusable context and prediction workflows are separate callable units, so
their caller owns sequencing. A Bundesliga matchday or bonus dispatch is not
valid until context collection has completed successfully for that exact
`competition` and `community_context`; a successful run for another community,
competition, or an old WM26/unscoped partition does not satisfy this dependency.

P0-19 entrypoints remain `workflow_dispatch`-only. During P0-20, record the
successful context run ID and completion before dispatching matchday or bonus
validation. P0-21 must preserve context-before-prediction spacing when enabling
production schedules and must observe the first scheduled sequence. A failed or
cancelled context run blocks the corresponding prediction dispatch.

### Bundesliga 2026/27 topology

The exact P0-17 matrix has six stable rows: `dev-luna`,
`arena-luna-self-contained`, `pes-production-reference`,
`schadensfresse-production-independent`, `arena-production-copy`, and the
nondeployable `arena-challenger-slot` template. Do not replace an unresolved
production or challenger field with a historical model or the Luna validation
identity.

The arena validation workflow reserves
`EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` and
`EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD`. The production reference
and independent rows use `PES_SQUAD_KICKTIPP_USERNAME` /
`PES_SQUAD_KICKTIPP_PASSWORD` and `SCHADENSFRESSE_KICKTIPP_USERNAME` /
`SCHADENSFRESSE_KICKTIPP_PASSWORD`, respectively. Exact model-specific names
for `arena-production-copy` and admitted challengers remain an owner gate; do
not create placeholder secrets.

Shared prediction workflows use `FIREBASE_PROJECT_ID`,
`FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, and
`LANGFUSE_SECRET_KEY`. `LANGFUSE_PUBLIC_KEY` is a repository variable. Secret
names are safe to document; values must never be printed or committed.

For the copy row, `community` is `ehonda-ai-arena` and `community_context` is
`pes-squad`. Posting-target arena credentials are mandatory. Match fixtures
must be compatible, and bonus questions plus options must normalize exactly
before a stored reference prediction is posted.

The retained self-contained WM26 workflow evidence keeps `community` and
`community_context` aligned. Those inert `ehonda-ai-arena` callers use
`community_context: "ehonda-ai-arena"`; their former context, matchday, and
bonus schedules are no longer active. The historical `gpt-5.5 none`,
`gpt-5.5 xhigh`, `gpt-5.4-nano none`, and `o3 medium` paths keep that same
self-contained context alignment.

For the retained WM26 secondary-community copy-posting evidence, `o3 high` was
the selected configuration. In that historical case, keep `community` as the posting target, set
`community_context` to `rabetrabauken2026`, and run the workflow after the
matching primary `rabetrabauken2026` prediction path so the secondary workflow
can post the stored reference prediction rather than create a separate model
run. Do not use this pattern for the self-contained `gpt-5-nano minimal`
workflows, `o3 medium`, dev shortcuts, or other WM26 model experiments.

For model-specific posting identities, include the reasoning effort in the
secret name whenever the workflow pins one. The preliminary
`ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows use
`EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME` and
`EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`.
The selected WM26 `o3 high` production workflows use
`RABETRABAUKEN2026_KICKTIPP_USERNAME` /
`RABETRABAUKEN2026_KICKTIPP_PASSWORD` for the primary community and
`EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_PASSWORD` for the secondary copy-posting
community.
The additional historical self-contained WM26 workflows retain
`EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_PASSWORD`,
`EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD`,
`EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD`, and
`EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD`.

## Example Communities

### Test Community

- **Matchday**: Runs twice daily (midnight and noon Europe/Berlin)
- **Bonus**: Runs daily at 6 PM Europe/Berlin
- **Configured Model**: o4-mini (testing/development)

### Production Community

- **Matchday**: Runs twice daily (6:30 AM and 6:30 PM Europe/Berlin)
- **Bonus**: Runs weekly on Sunday evening
- **Configured Model**: o1 (production quality)

## Required Secrets

Each community requires its own set of secrets configured in the GitHub repository:

### Per-Community Secrets

For each community (replace `{COMMUNITY}` with uppercase community name with dashes replaced by underscores):

- `{COMMUNITY}_KICKTIPP_USERNAME`: Kicktipp account username for this community
- `{COMMUNITY}_KICKTIPP_PASSWORD`: Kicktipp account password for this community

Examples:

- `TEST_COMMUNITY_KICKTIPP_USERNAME`
- `TEST_COMMUNITY_KICKTIPP_PASSWORD`
- `PROD_COMMUNITY_KICKTIPP_USERNAME`
- `PROD_COMMUNITY_KICKTIPP_PASSWORD`

### Global Secrets (Shared Across Communities)

- `FIREBASE_PROJECT_ID`: Your Firebase project ID
- `FIREBASE_SERVICE_ACCOUNT_JSON`: Firebase service account JSON key
- `OPENAI_API_KEY`: OpenAI API key for prediction generation
- `LANGFUSE_SECRET_KEY`: Langfuse ingestion key for traced prediction steps

`LANGFUSE_PUBLIC_KEY` is configured as a repository variable, not a secret.

### Bundesliga 2026/27 Model-Specific Prediction Secrets

- `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME`: username for the authorized self-contained Luna/none arena validation participant
- `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD`: password for the authorized self-contained Luna/none arena validation participant
- `PES_SQUAD_KICKTIPP_USERNAME` / `PES_SQUAD_KICKTIPP_PASSWORD`: reference production community names; use remains gated by P0-21
- `SCHADENSFRESSE_KICKTIPP_USERNAME` / `SCHADENSFRESSE_KICKTIPP_PASSWORD`: independent production community names; use remains gated by P0-21

Arena production-copy and challenger secret names are deliberately absent
until the owner selects the exact model participants.

### WM26 Model-Specific Prediction Secrets

- `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 o3/high secondary copy-posting workflows
- `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 o3/high secondary copy-posting workflows
- `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5-nano/minimal self-contained posting workflows
- `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5-nano/minimal self-contained posting workflows
- `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 o3/medium comparison workflows
- `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 o3/medium comparison workflows
- `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.5/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.5/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.5/xhigh posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.5/xhigh posting workflows
- `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.4-nano/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD`: retained credential name for the historical ehonda-ai-arena WM26 gpt-5.4-nano/none posting workflows

### Context Collection Secrets

- `PES_SQUAD_KICKTIPP_USERNAME`: Kicktipp username for pes-squad context collection
- `PES_SQUAD_KICKTIPP_PASSWORD`: Kicktipp password for pes-squad context collection
- `SCHADENSFRESSE_KICKTIPP_USERNAME`: Kicktipp username for schadensfresse context collection
- `SCHADENSFRESSE_KICKTIPP_PASSWORD`: Kicktipp password for schadensfresse context collection
- `RABETRABAUKEN2026_KICKTIPP_USERNAME`: Kicktipp username for rabetrabauken2026 WM26 context collection and o3/high primary production workflows
- `RABETRABAUKEN2026_KICKTIPP_PASSWORD`: Kicktipp password for rabetrabauken2026 WM26 context collection and o3/high primary production workflows
- `FIREBASE_PROJECT_ID`: Same Firebase project ID (shared with predictions)
- `FIREBASE_SERVICE_ACCOUNT_JSON`: Same Firebase service account (shared with predictions)

## Adding a New Community

To add support for a new community:

1. **Create a reviewed community triad**:
   - `{community-name}-context-collection.yml`
   - `{community-name}-matchday.yml`
   - `{community-name}-bonus.yml`

2. **Configure secrets in GitHub**:
   - Add `{COMMUNITY}_KICKTIPP_USERNAME` secret
   - Add `{COMMUNITY}_KICKTIPP_PASSWORD` secret

3. **Customize configuration**:
   - Pin the exact posting community, context community, and competition
   - Pin model, reasoning effort, positive output cap, prompt source/name/label/numbered version, and bonus budgets
   - Expose `workflow_dispatch` first; add a schedule only through the accepted activation task
   - Preserve context-before-prediction sequencing and map only approved credential names

For Bundesliga 2026/27, start from the P0-19 task template and the checked-in
arena Luna triad rather than a historical workflow. The deterministic workflow
contract rejects incomplete identities, active historical triggers, unexpected
secret names, or a second unreviewed current-season caller.

## Manual Triggering

The P0-19 arena Luna workflows can be manually triggered from the GitHub Actions tab with:

- **Force Prediction**: Override the verification check and generate predictions regardless

The model and reasoning effort used for predictions are fixed per community workflow (no override option).

## Migration from Old System

The previous staging/production environment system has been replaced with this multi-community approach. Key changes:

- **Environment Variables**: Removed `STAGING_ENABLED`, `PRODUCTION_ENABLED`, etc.
- **Community-Specific**: Each admitted matrix row has explicit entrypoints and an independently gated trigger policy
- **Simplified Configuration**: Direct input parameters instead of JSON configuration
- **Individual Credentials**: Each community uses its own Kicktipp credentials
- **Fixed Models**: Models and reasoning efforts are defined per community workflow (no runtime overrides)

## Timezone Considerations

All workflows use `Europe/Berlin` timezone for logging and reference. GitHub Actions cron runs in UTC only, so:

- During **Central European Time (CET)**: UTC + 1 hour
- During **Central European Summer Time (CEST)**: UTC + 2 hours  
- During DST transitions, actual local time may be off by 1 hour

## Workflow Architecture Benefits

1. **Scalability**: Easy to add new communities without code changes
2. **Flexibility**: Each community can have unique schedules and configurations  
3. **Maintainability**: Core logic centralized in reusable workflows
4. **Security**: Community-specific credentials isolation
5. **Monitoring**: Individual workflow runs for each community
6. **Customization**: Per-community model selection and prediction strategies

The workflow will generate and post predictions when:

- No predictions exist in the database for available matches
- Database and Kicktipp predictions don't match
- Manual trigger with "Force Prediction" enabled

### When No Action Is Taken

The workflow will skip prediction generation when:

- All database predictions match Kicktipp predictions
- No matches are available for prediction

## Monitoring

The workflow provides detailed logging and creates a summary for each run, including:

- Trigger type (scheduled or manual)
- Model used for predictions
- Verification results
- Actions taken

## Troubleshooting

### Common Issues

1. **Authentication Failures**
   - Verify Kicktipp credentials are correct
   - Check that Firebase service account has proper permissions

2. **API Rate Limits**
   - OpenAI API calls are subject to rate limits
   - Consider adjusting the model or frequency if issues occur

3. **Timezone Considerations**
   - Scheduled runs use approximate Europe/Berlin time
   - Manual triggers can be used for precise timing

### Workflow Fails

If the workflow fails:

1. Check the workflow logs in the Actions tab
2. Verify all required secrets are properly configured
3. Ensure the Orchestrator project builds successfully
4. Check for any API service outages

## Local Testing

To test the commands locally before relying on the automated workflow:

### Prediction Testing

```bash
# Test verification
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify o4-mini --init-matchday --agent

# Test prediction generation
dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday o4-mini --override-kicktipp --verbose --agent

# Test final verification
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify o4-mini --agent
```

### Context Collection Testing

```bash
# Test context collection with dry run for different communities
dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context pes-squad --dry-run --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context schadensfresse --dry-run --verbose

# Test actual context collection
dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context pes-squad --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context schadensfresse --verbose

# Test WM26 context extras
dotnet run --project src/Orchestrator/Orchestrator.csproj -- wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --apply-known-only --preserve-collected-on-or-after 2026-06-11 --dry-run --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- wm26-recent-history probe-prediction-lookup --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --home-team Mexiko --away-team Südafrika --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context fifa --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --dry-run --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --dry-run --verbose
```

### Cost Analysis Testing

```bash
# Test cost analysis with different configuration files
dotnet run --project src/Orchestrator/Orchestrator.csproj -- cost --file cost-command-configurations/production/all.json --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- cost --file cost-command-configurations/production/ehonda-ai-arena.json --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- cost --file cost-command-configurations/production/pes-squad.json --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- cost --file cost-command-configurations/production/schadensfresse.json --verbose
```

Make sure to set the required environment variables locally for testing.
