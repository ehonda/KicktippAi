# Automated Matchday Predictions Workflow

This GitHub Actions workflow automates the process of generating and posting matchday predictions for the KicktippAi project.

## How It Works

The workflow implements the core prediction loop:

1. **Verification**: Runs `dotnet run -- verify --init-matchday --agent` to check if predictions are needed
2. **Prediction**: If verification fails (indicating missing/mismatched predictions), runs `dotnet run -- matchday $MODEL --override-kicktipp --verbose --agent`
3. **Final Check**: Verifies that predictions were successfully posted

## Schedule

The workflow runs automatically twice daily:

- **~Midnight Europe/Berlin time** (23:00 UTC = 00:00 CET / 01:00 CEST)
- **~Noon Europe/Berlin time** (11:00 UTC = 12:00 CET / 13:00 CEST)

> **Note**: GitHub Actions cron only supports UTC time. During Daylight Saving Time transitions, the actual local time may be off by 1 hour. The times above are approximations that work reasonably well year-round.

## Manual Triggering

You can manually trigger the workflow from the GitHub Actions tab with the following options:

- **Model Selection**: Choose from available OpenAI models (`o4-mini`, `o1`)
- **Environment**: Select staging, production, or auto-detection
- **Force Prediction**: Override the verification check and generate predictions regardless

## Environment Configuration

The workflow supports **staging** and **production** environments that can be easily configured via repository variables:

- **Staging** (default: enabled, o4-mini): For testing and development
- **Production** (default: disabled, o1): For final predictions

For detailed configuration instructions, see [Environment Configuration Guide](ENVIRONMENT-CONFIG.md).

## Required Secrets

The following repository secrets must be configured for the workflow to function:

### Kicktipp Integration

- `KICKTIPP_USERNAME`: Your Kicktipp account username
- `KICKTIPP_PASSWORD`: Your Kicktipp account password

### Firebase Database

- `FIREBASE_PROJECT_ID`: Your Firebase project ID
- `FIREBASE_SERVICE_ACCOUNT_JSON`: Service account JSON key for Firebase access

### OpenAI API

- `OPENAI_API_KEY`: Your OpenAI API key for generating predictions

## Workflow Behavior

### When Predictions Are Needed

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

```bash
# Test verification
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify --init-matchday --agent

# Test prediction generation
dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday o4-mini --override-kicktipp --verbose --agent

# Test final verification
dotnet run --project src/Orchestrator/Orchestrator.csproj -- verify --agent
```

Make sure to set the required environment variables locally for testing.
