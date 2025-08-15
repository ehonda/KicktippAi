# Cost Command Configuration Files

This directory contains JSON configuration files for the cost command. These files allow you to save and reuse common command configurations.

## Directory Structure

- **`examples/`**: Sample configuration files for common use cases
- **`production/`**: Production configuration files (manually managed)

## Usage

```bash
# Using example configurations
dotnet run --project src/Orchestrator -- cost --file cost-command-configurations/examples/all-breakdown.json

# Using production configurations
dotnet run --project src/Orchestrator -- cost --file cost-command-configurations/production/my-config.json
```

You can also override any configuration values with command line options:

```bash
# Load config but override to be verbose
dotnet run --project src/Orchestrator -- cost --file cost-command-configurations/examples/gpt5nano-quick.json --verbose
```

## Configuration Format

All properties are optional. If not specified, the default behavior applies.

```json
{
  "matchdays": "1,2,3",                    // Comma-separated matchdays or "all"
  "bonus": true,                           // Include bonus predictions
  "models": "gpt-5,o4-mini",               // Comma-separated models or "all"  
  "communityContexts": "ehonda-test-buli", // Comma-separated contexts or "all"
  "all": false,                            // Aggregate over all data
  "verbose": true,                         // Enable verbose output
  "detailedBreakdown": true                // Show detailed breakdown table
}
```

## Available Example Configurations

- **all-breakdown.json**: Comprehensive analysis with detailed breakdown of all data
- **o3-ehonda.json**: Focus on o3 model costs for ehonda-test-buli community
- **gpt5nano-quick.json**: Quick cost check using the cheapest gpt-5-nano model
- **early-season-ehonda.json**: Analysis of early season matchdays for ehonda-test-buli community

## Priority

Command line options always take precedence over file configuration values.
