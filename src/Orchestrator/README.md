# Orchestrator

The Orchestrator is a console application that leverages all components to generate predictions, typically for the current match day. It includes Firebase database integration for prediction persistence and analytics.

## Features

- **Match Prediction**: Generate AI-powered predictions using OpenAI models
- **Database Integration**: Store and retrieve predictions using Firebase Firestore
- **Kicktipp Integration**: Automatically place predictions on Kicktipp platform
- **Smart Caching**: Check existing predictions before generating new ones
- **Comprehensive Logging**: Track prediction generation and placement

## Commands

### `matchday`
Generates predictions for the current matchday with database integration.

**Workflow:**
1. Load current matchday matches from Kicktipp
2. Check database for existing predictions
3. Generate new predictions only for matches without existing predictions
4. Save new predictions to database before placing bets
5. Place all predictions (existing + new) on Kicktipp

**Usage:**
```bash
dotnet run -- matchday <model> [options]
```

**Example:**
```bash
dotnet run -- matchday gpt-4o-2024-08-06
dotnet run -- matchday o4-mini --verbose
```

### `bonus`
Generates bonus predictions.

**Usage:**
```bash
dotnet run -- bonus <model> [options]
```

**Example:**
```bash
dotnet run -- bonus gpt-4o-2024-08-06
dotnet run -- bonus o4-mini --verbose
```

## Options

- `<model>`: The OpenAI model to use for prediction (e.g., gpt-4o-2024-08-06, o4-mini)
- `-v, --verbose`: Enable verbose output to show detailed information
- `--with-justification`: Include concise reasoning text for each prediction (incompatible with `--agent` mode)

## Configuration

The application uses environment variables for configuration. Create a `.env` file in the secrets directory or set environment variables directly.

## Environment Variables

The Orchestrator requires the following environment variables to be set:

### Required

- `OPENAI_API_KEY`: Your OpenAI API key
- `KICKTIPP_USERNAME`: Your Kicktipp username  
- `KICKTIPP_PASSWORD`: Your Kicktipp password

### Optional (Firebase Database)

- `FIREBASE_PROJECT_ID`: Your Firebase project ID
- `FIREBASE_SERVICE_ACCOUNT_JSON`: Firebase service account key (JSON content)

**Note:** If Firebase variables are not set, the application will run without database features (predictions won't be saved/retrieved).

### Loading Credentials

The application loads credentials in this order:

1. **Environment variables** (highest priority)
2. **`.env` file** at `../KicktippAi.Secrets/src/Orchestrator/.env`
3. **`firebase.json`** file at `../KicktippAi.Secrets/src/Orchestrator/firebase.json`

**Example `.env` file:**

```env
OPENAI_API_KEY=sk-your-openai-api-key
KICKTIPP_USERNAME=your-username
KICKTIPP_PASSWORD=your-password
FIREBASE_PROJECT_ID=your-firebase-project-id
```

**Example `firebase.json` file:**

```json
{
  "type": "service_account",
  "project_id": "your-firebase-project",
  "private_key_id": "...",
  "private_key": "-----BEGIN PRIVATE KEY-----
...
-----END PRIVATE KEY-----
",
  "client_email": "your-service-account@your-project.iam.gserviceaccount.com",
  "client_id": "...",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token"
}
```

## Architecture

### Dependencies

- Integration with all existing components (Core, KicktippIntegration, OpenAiIntegration, ContextProviders.Kicktipp)
- Dependency injection setup for all services
- Orchestration logic for prediction generation
