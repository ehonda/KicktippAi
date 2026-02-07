# KicktippAi 🤖⚽

AI-powered football prediction system for [Kicktipp.de](https://www.kicktipp.de) using OpenAI's GPT models.

## Overview

KicktippAi automatically generates intelligent match predictions and places bets on the German football prediction platform Kicktipp.de. The system uses advanced AI models, historical data, and real-time context to make informed predictions, running fully automated via GitHub Actions.

### Key Features

- 🤖 **AI-Powered Predictions** - Uses OpenAI GPT models (gpt-4o, o3, gpt-5-nano) for intelligent score predictions
- 📊 **Context-Aware** - Analyzes team standings, head-to-head records, and historical performance
- 🔄 **Fully Automated** - GitHub Actions workflows run twice daily (midnight & noon Berlin time)
- 💾 **Database Integration** - Firebase Firestore for prediction history and analytics
- 💰 **Cost Optimized** - Configurable models with cost tracking and estimation
- 🎯 **Multi-Community Support** - Manages predictions for multiple Kicktipp communities
- 🔒 **Secure** - Environment-based credential management

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    GitHub Actions                            │
│                   (Automated Workflows)                      │
└────────────┬────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│                     Orchestrator                             │
│           (CLI - Coordinates all components)                 │
└─┬─────────┬──────────┬────────────┬────────────┬───────────┘
  │         │          │            │            │
  ▼         ▼          ▼            ▼            ▼
┌──────┐ ┌─────┐ ┌──────────┐ ┌──────────┐ ┌─────────┐
│OpenAI│ │Core │ │Kicktipp  │ │Firebase  │ │Context  │
│ API  │ │Logic│ │Integration│ │Adapter   │ │Providers│
└──────┘ └─────┘ └──────────┘ └──────────┘ └─────────┘
```

### Components

- **Orchestrator** - Main CLI application coordinating prediction generation and placement
- **OpenAI Integration** - Service layer for AI-powered prediction generation
- **Kicktipp Integration** - Web automation (login, bet placement) using HttpClient & AngleSharp; inspired by [schwalle/kicktipp-betbot](https://github.com/schwalle/kicktipp-betbot)
- **Firebase Adapter** - Firestore-based prediction persistence and analytics
- **Context Providers** - Supply match data, team standings, and historical records to the AI
- **Core** - Shared domain models (Match, Prediction, etc.)

## Technologies

- **.NET 10.0** - Modern C# runtime
- **OpenAI API** - AI prediction generation
- **Firebase Firestore** - Database and analytics
- **AngleSharp** - HTML parsing for web automation
- **GitHub Actions** - Automated workflows
- **TUnit** - Testing framework

## Quick Start

### Prerequisites

- .NET 10.0 SDK
- OpenAI API key
- Kicktipp.de account
- Firebase project (for database)

### Local Testing

```bash
# Predict a matchday using a fast model
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli

# Get help on available commands
dotnet run --project src/Orchestrator -- --help
dotnet run --project src/Orchestrator -- matchday --help
```

### Configuration

The system requires the following secrets:
- **Kicktipp**: `KICKTIPP_USERNAME`, `KICKTIPP_PASSWORD`
- **Firebase**: `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`
- **OpenAI**: `OPENAI_API_KEY`

For local development, see [manual testing guidelines](.github/instructions/manual-testing.instructions.md).

## Automated Workflows

The system runs automated predictions via GitHub Actions:

- **Schedule**: Twice daily (00:00 and 12:00 Berlin time)
- **Communities**: Multiple communities with individual configurations
- **Models**: Configurable OpenAI models (production uses o3)
- **Cost Analysis**: Automated cost tracking and reporting

For details, see [automation documentation](docs/automation.md) and [workflow README](.github/workflows/README.md).

## Development

### Project Structure

```
src/
├── Orchestrator/           # Main CLI application
├── OpenAiIntegration/      # AI prediction service
├── KicktippIntegration/    # Web automation
├── FirebaseAdapter/        # Database layer
├── ContextProviders.Kicktipp/ # Match context data
├── Core/                   # Domain models
└── TestUtilities/          # Test helpers

tests/                      # TUnit test suites
docs/                       # Documentation
.github/workflows/          # GitHub Actions
```

### Running Tests

```bash
# Generate coverage report (focused on specific projects)
./Generate-CoverageReport.ps1 -Projects OpenAiIntegration.Tests,Core.Tests

# Get coverage details for specific classes
./Get-CoverageDetails.ps1 -Filter "ClassName" -ShowUncovered
```

### Contributing

1. Follow the [project style guide](src/project_style_guide.md)
2. Write tests using TUnit (see [test instructions](.github/instructions/tests.instructions.md))
3. Run linters and tests before submitting
4. Check the [troubleshooting guide](docs/troubleshooting.md) if you encounter issues

## Cost Optimization

The system includes several cost-saving features:
- Uses gpt-5-nano for development/testing
- Caches predictions to avoid regeneration
- Estimates costs before running production models
- Tracks actual costs via automated analysis

Example cost estimate command:
```bash
dotnet run --project src/Orchestrator -- matchday o3 --community ehonda-test-buli --verbose --estimated-costs o3
```

## License

See [LICENSE](LICENSE) file for details.
