# Automation

This project includes automated workflows for continuous prediction generation and posting.

## GitHub Actions

### Automated Matchday Predictions

The main automation workflow is located at `.github/workflows/automated-predictions.yml` and handles:

- **Scheduled Execution**: Runs twice daily (midnight and noon Europe/Berlin time)
- **Verification**: Checks if predictions need to be generated or updated
- **Prediction Generation**: Uses configurable OpenAI models to generate predictions
- **Posting**: Automatically posts predictions to Kicktipp
- **Manual Triggers**: Allows on-demand execution with custom parameters

For detailed information about the workflow, configuration, and troubleshooting, see the [workflow documentation](.github/workflows/README.md).

## Required Configuration

The automation requires the following secrets to be configured in the GitHub repository:

- **Kicktipp**: `KICKTIPP_USERNAME`, `KICKTIPP_PASSWORD`
- **Firebase**: `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`
- **OpenAI**: `OPENAI_API_KEY`

## Core Loop

The automation implements this decision logic:

1. Run verification command to check prediction status
2. If verification fails → Generate and post predictions
3. If verification passes → No action needed
4. Perform final verification to confirm success

This ensures predictions are always up-to-date without unnecessary API calls or duplicate work.
