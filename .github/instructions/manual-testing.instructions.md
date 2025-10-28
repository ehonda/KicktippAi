---
applyTo: '**'
---
# Manual Testing Guidelines

These guidelines help effectively test when implementing changes across the solution's core components. Until we have comprehensive automated tests, these MUST be followed when working with manual tests.

## When to apply

* When testing changes to our core components
* When testing changes to our orchestration (the `src/Orchestrator` project) itself

## How to use the Orchestrator for testing

### Basic Usage

A simple sample test predicting a matchday looks like this:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

To discover information about how to use the orchestrator, use these commands:

```powershell
dotnet run --project src/Orchestrator -- --help
# Likewise for the other subcommands
dotnet run --project src/Orchestrator -- matchday --help
```

### Specific Configurations for testing

* Use `gpt-5-nano` to save costs and execute quickly, we don't care about the quality of the predictions for development
* When the command is generating predictions, i.e. `-override-database` is specified, use `--verbose --estimated-costs o3` because that is our production model and we want to see cost estimates for it
* Use `ehonda-test-buli` as the community for testing
