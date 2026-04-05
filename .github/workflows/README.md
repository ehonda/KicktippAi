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

### Cost Analysis Workflow

- **`cost-analysis.yml`**: Automated cost analysis for all prediction activities
  - Runs twice daily (01:30 and 13:30 UTC) - 30 minutes after the last prediction workflows
  - Analyzes costs for all community configurations using a matrix strategy
  - Configurations analyzed: `all.json`, `ehonda-ai-arena.json`, `pes-squad.json`, `schadensfresse.json`
  - Can be manually triggered
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
3. **Database Storage**: Store context documents in Firebase with version control
4. **Duplicate Detection**: Skip unchanged context to avoid redundant storage

## Community Configuration

Each community workflow is configured with direct parameters:

- **`community`**: Kicktipp community name
- **`model`**: OpenAI model to use for predictions (o4-mini, o1)
- **`community_context`**: Community context when generating predictions (or using stored ones from the database)

## Example Communities

### Test Community

- **Matchday**: Runs twice daily (midnight and noon Europe/Berlin)
- **Bonus**: Runs daily at 6 PM Europe/Berlin
- **Default Model**: o4-mini (testing/development)

### Production Community

- **Matchday**: Runs twice daily (6:30 AM and 6:30 PM Europe/Berlin)
- **Bonus**: Runs weekly on Sunday evening
- **Default Model**: o1 (production quality)

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

### Context Collection Secrets

- `PES_SQUAD_KICKTIPP_USERNAME`: Kicktipp username for pes-squad context collection
- `PES_SQUAD_KICKTIPP_PASSWORD`: Kicktipp password for pes-squad context collection
- `SCHADENSFRESSE_KICKTIPP_USERNAME`: Kicktipp username for schadensfresse context collection
- `SCHADENSFRESSE_KICKTIPP_PASSWORD`: Kicktipp password for schadensfresse context collection
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
   - Choose default models
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

The model used for predictions is fixed per community workflow (no override option).

## Migration from Old System

The previous staging/production environment system has been replaced with this multi-community approach. Key changes:

- **Environment Variables**: Removed `STAGING_ENABLED`, `PRODUCTION_ENABLED`, etc.
- **Community-Specific**: Each community now has its own workflow with individual scheduling
- **Simplified Configuration**: Direct input parameters instead of JSON configuration
- **Individual Credentials**: Each community uses its own Kicktipp credentials
- **Fixed Models**: Models are defined per community workflow (no runtime overrides)

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
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify --init-matchday --agent

# Test prediction generation
dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday o4-mini --override-kicktipp --verbose --agent

# Test final verification
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify --agent
```

### Context Collection Testing

```bash
# Test context collection with dry run for different communities
dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context pes-squad --dry-run --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context schadensfresse --dry-run --verbose

# Test actual context collection
dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context pes-squad --verbose

dotnet run --project src/Orchestrator/Orchestrator.csproj -- collect-context kicktipp --community-context schadensfresse --verbose
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
