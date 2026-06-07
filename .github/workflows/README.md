# Multi-Community Automated Predictions Workflows

This directory contains GitHub Actions workflows that automate the process of generating and posting matchday and bonus predictions for multiple Kicktipp communities in the KicktippAi project.

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

### Context Collection Workflows

- **`pes-squad-context-collection.yml`**: Automated context collection for pes-squad community
  - Runs every 12 hours (00:00 and 12:00 UTC)
  - Can be manually triggered
- **`schadensfresse-context-collection.yml`**: Automated context collection for schadensfresse community
  - Runs every 12 hours (00:00 and 12:00 UTC)
  - Can be manually triggered
- **`rabetrabauken2026-context-collection.yml`**: Scheduled WM26 reference production context collection
  - Runs Kicktipp collection, guarded recent-history date-map application, FIFA ranking, and lineup context collection for `fifa-world-cup-2026`
  - Uses the WM26 context cadence: 23:47, 06:47, and 11:47 UTC
  - Feeds the selected `o3 high` primary and secondary production workflows
- **`wm26-ehonda-ai-arena-context-collection.yml`**: Scheduled WM26 self-contained context collection
  - Runs Kicktipp collection, guarded recent-history date-map application, FIFA ranking, and lineup context collection for `ehonda-ai-arena`
  - Uses the WM26 context cadence: 23:47, 06:47, and 11:47 UTC
  - Feeds the self-contained `ehonda-ai-arena` WM26 workflows such as `gpt-5-nano minimal`, `gpt-5.5 none`, `gpt-5.5 xhigh`, `gpt-5.4-nano none`, and `o3 medium`

### WM26 Prediction Workflows

- **`wm26-rabetrabauken2026-o3-high-matchday.yml`**: Scheduled WM26 primary production matchday predictions
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"`
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-rabetrabauken2026-o3-high-bonus.yml`**: Scheduled WM26 primary production bonus predictions
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"`
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC
- **`wm26-ehonda-ai-arena-o3-high-matchday.yml`**: Scheduled WM26 secondary production matchday copy-posting
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"` so it reuses the stored primary prediction
  - Uses the slower secondary cadence: 01:07, 08:07, and 13:07 UTC
- **`wm26-ehonda-ai-arena-o3-high-bonus.yml`**: Scheduled WM26 secondary production bonus copy-posting
  - Uses `o3` with `reasoning_effort: "high"`
  - Pins `max_output_tokens: 40000`
  - Uses `community_context: "rabetrabauken2026"` so it reuses the stored primary prediction
  - Uses the slower secondary cadence: 01:07, 08:07, and 13:07 UTC
- **`wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml`**: Scheduled WM26 self-contained matchday predictions
  - Uses `gpt-5-nano` with `reasoning_effort: "minimal"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml`**: Scheduled WM26 self-contained bonus predictions
  - Uses `gpt-5-nano` with `reasoning_effort: "minimal"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC
- **`wm26-ehonda-ai-arena-o3-medium-matchday.yml`**: Scheduled WM26 self-contained matchday comparison
  - Uses `o3` with `reasoning_effort: "medium"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained comparison path
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-o3-medium-bonus.yml`**: Scheduled WM26 self-contained bonus comparison
  - Uses `o3` with `reasoning_effort: "medium"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained comparison path
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml`**: Scheduled WM26 onboarding matchday test
  - Uses `gpt-5.5` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml`**: Scheduled WM26 onboarding bonus test
  - Uses `gpt-5.5` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml`**: Scheduled WM26 onboarding matchday test
  - Uses `gpt-5.5` with `reasoning_effort: "xhigh"`
  - Pins `max_output_tokens: 40000` to match the documented xhigh estimate assumptions
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml`**: Scheduled WM26 onboarding bonus test
  - Uses `gpt-5.5` with `reasoning_effort: "xhigh"`
  - Pins `max_output_tokens: 40000` to match the documented xhigh estimate assumptions
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC
- **`wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml`**: Scheduled WM26 onboarding matchday test
  - Uses `gpt-5.4-nano` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 main matchday cadence: 00:37, 07:37, and 12:37 UTC
- **`wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml`**: Scheduled WM26 onboarding bonus test
  - Uses `gpt-5.4-nano` with `reasoning_effort: "none"`
  - Pins `max_output_tokens: 10000`
  - Uses `community_context: "ehonda-ai-arena"` for the self-contained onboarding path
  - Uses the WM26 bonus cadence: 00:47, 07:47, and 12:47 UTC

WM26 workflow display names should include `🏆` so they are easy to distinguish
from Bundesliga workflows in the GitHub Actions UI. New WM26 workflow filenames
should use a `wm26-` prefix instead of reusing Bundesliga-era community/model
filenames.

The scheduled self-contained `ehonda-ai-arena` workflows and the scheduled
secondary `o3 high` workflows can coexist in `ehonda-ai-arena` because they
use different model configurations, different model-specific posting
credentials, and different `community_context` values.

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
2. **Context Gathering**: Collect match context from all current matchday matches
3. **Competition Extras**: WM26 collection applies known pre-tournament recent-history dates, then optional WM26 collection runs `collect-context fifa` and `collect-context lineups`
4. **Database Storage**: Store context documents in Firebase with version control
5. **Duplicate Detection**: Skip unchanged context to avoid redundant storage

## Community Configuration

Each community workflow is configured with direct parameters:

- **`community`**: Kicktipp community name
- **`model`**: OpenAI model to use for predictions (o4-mini, o1)
- **`reasoning_effort`**: Optional OpenAI reasoning effort for prediction generation. Leave empty for the model default, or set `none`, `minimal`, `low`, `medium`, `high`, or `xhigh`.
- **`max_output_tokens`**: Optional maximum output token cap for prediction generation. Use `0` to keep the command default (`10000`).
- **`community_context`**: Community context when generating predictions (or using stored ones from the database)
- **`competition`**: Optional competition identifier for context collection
- **`include_fifa_rankings` / `include_lineups`**: Enable WM26 ranking and lineup context extras for World Cup communities. WM26 recent-history date-map application is automatic when `competition` is `fifa-world-cup-2026`.

For self-contained WM26 workflow tests and comparisons, keep `community` and
`community_context` aligned. The scheduled self-contained `ehonda-ai-arena`
WM26 workflows use `community_context: "ehonda-ai-arena"` plus the WM26
context, matchday, and bonus schedules. The `gpt-5.5 none`, `gpt-5.5 xhigh`,
`gpt-5.4-nano none`, and `o3 medium` paths keep that same self-contained
context alignment even though they are comparison paths rather than the
selected WM26 production model.

For WM26, secondary-community copy posting is currently selected only for
`o3 high`. In that specific case, keep `community` as the posting target, set
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
The additional scheduled self-contained WM26 workflows use
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

### WM26 Model-Specific Prediction Secrets

- `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 o3/high secondary copy-posting workflows
- `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 o3/high secondary copy-posting workflows
- `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 gpt-5-nano/minimal self-contained posting workflows
- `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 gpt-5-nano/minimal self-contained posting workflows
- `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 o3/medium comparison workflows
- `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 o3/medium comparison workflows
- `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 gpt-5.5/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 gpt-5.5/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 gpt-5.5/xhigh posting workflows
- `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 gpt-5.5/xhigh posting workflows
- `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME`: Kicktipp username for the scheduled ehonda-ai-arena WM26 gpt-5.4-nano/none posting workflows
- `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD`: Kicktipp password for the scheduled ehonda-ai-arena WM26 gpt-5.4-nano/none posting workflows

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

1. **Create community-specific workflow files**:
   - `{community-name}-matchday.yml`
   - `{community-name}-bonus.yml`

2. **Configure secrets in GitHub**:
   - Add `{COMMUNITY}_KICKTIPP_USERNAME` secret
   - Add `{COMMUNITY}_KICKTIPP_PASSWORD` secret

3. **Customize configuration**:
   - Set appropriate schedule (cron expressions)
   - Configure community name and context
   - Choose model and reasoning effort
   - Set manual trigger defaults

### Example Community Workflow Template

```yaml
name: My Community - Matchday Predictions

on:
  schedule:
    - cron: '0 23 * * *'  # Customize schedule
  workflow_dispatch:
    inputs:
      force_prediction:
        description: 'Force prediction even if verify passes'
        required: false
        default: false
        type: boolean

jobs:
  call-base-workflow:
    name: Run Matchday Predictions
    uses: ./.github/workflows/base-matchday-predictions.yml
    with:
      community: "my-kicktipp-community"
      model: "o4-mini"
      reasoning_effort: ""
      community_context: "my-context"
      trigger_type: ${{ github.event_name == 'schedule' && 'scheduled' || 'manual' }}
      force_prediction: ${{ github.event.inputs.force_prediction == 'true' }}
    secrets:
      kicktipp_username: ${{ secrets.MY_COMMUNITY_KICKTIPP_USERNAME }}
      kicktipp_password: ${{ secrets.MY_COMMUNITY_KICKTIPP_PASSWORD }}
      firebase_project_id: ${{ secrets.FIREBASE_PROJECT_ID }}
      firebase_service_account_json: ${{ secrets.FIREBASE_SERVICE_ACCOUNT_JSON }}
      openai_api_key: ${{ secrets.OPENAI_API_KEY }}
```

## Manual Triggering

Each community workflow can be manually triggered from the GitHub Actions tab with:

- **Force Prediction**: Override the verification check and generate predictions regardless

The model and reasoning effort used for predictions are fixed per community workflow (no override option).

## Migration from Old System

The previous staging/production environment system has been replaced with this multi-community approach. Key changes:

- **Environment Variables**: Removed `STAGING_ENABLED`, `PRODUCTION_ENABLED`, etc.
- **Community-Specific**: Each community now has its own workflow with individual scheduling
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
