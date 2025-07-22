# Orchestrator

The Orchestrator is a console application that leverages all components to generate predictions, typically for the current match day.

## Commands

### `matchday`
Generates predictions for the current matchday.

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

## Configuration

The application uses environment variables for configuration. Create a `.env` file in the secrets directory or set environment variables directly.

## Development

The commands are currently empty placeholders and will be implemented with the following functionality:
- Integration with all existing components (Core, KicktippIntegration, OpenAiIntegration, ContextProviders.Kicktipp)
- Dependency injection setup for all services
- Orchestration logic for prediction generation
