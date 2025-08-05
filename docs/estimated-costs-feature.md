# Estimated Costs Feature

## Overview

The estimated costs feature allows you to see what the costs would be if you had used a different OpenAI model for your predictions, assuming the same token counts. This is useful for cost analysis and budget planning when considering switching between different models.

## Usage

Add the `--estimated-costs` option to any prediction command:

```bash
# Matchday predictions with estimated costs for o1 model
dotnet run --project src/Orchestrator/Orchestrator.csproj -- matchday o4-mini --community your-community --estimated-costs o1

# Bonus predictions with estimated costs for gpt-4o model  
dotnet run --project src/Orchestrator/Orchestrator.csproj -- bonus o4-mini --community your-community --estimated-costs gpt-4o
```

## Example Output

### Individual Prediction (with --verbose)

```text
Token usage: 1,492 / 0 / 3,008 / 40 / $0.0151 (est o1: $0.2053)
```

### Final Summary

```text
Token usage (uncached/cached/reasoning/output/$cost): 15,234 / 2,156 / 8,932 / 456 / $0.1234 (est o1: $1.6789)
```

## Format Explanation

- **Format**: `uncached / cached / reasoning / output / $actualCost (est modelName: $estimatedCost)`
- **uncached**: Uncached input tokens
- **cached**: Cached input tokens (if supported by model)
- **reasoning**: Reasoning output tokens (for reasoning models like o1)
- **output**: Regular output tokens
- **$actualCost**: Cost using the actual model specified in the command
- **$estimatedCost**: Cost if using the estimated model with same token counts

## Supported Models

The feature supports any model that has pricing information configured in the system:

- `gpt-4.1`, `gpt-4.1-mini`, `gpt-4.1-nano`
- `gpt-4.5-preview`
- `gpt-4o`, `gpt-4o-mini`
- `o1`, `o1-pro`, `o1-mini`
- `o3`, `o3-mini`
- `o4-mini`

## Technical Details

### How It Works

1. The actual prediction is made using the model specified in the command
2. Token usage is tracked during the API calls
3. The estimated costs are calculated by applying the alternative model's pricing to the same token counts
4. Both actual and estimated costs are displayed in the output

### Limitations

- Estimated costs assume identical token usage patterns between models
- Different models may actually use different amounts of tokens for the same task
- Cached token pricing is estimated based on the proportion from the actual usage
- The feature is for estimation purposes only and may not reflect actual costs when switching models

### Implementation

The feature is implemented through:

- `--estimated-costs` option in `BaseSettings.cs`
- Enhanced `ICostCalculationService` with `CalculateCost` method
- Updated `ITokenUsageTracker` with estimated cost summary methods
- Modified command outputs in `MatchdayCommand` and `BonusCommand`

## Use Cases

1. **Cost Analysis**: Compare costs between different models for the same workload
2. **Budget Planning**: Estimate budget impact when considering model upgrades
3. **Cost Optimization**: Identify potential savings when switching to more cost-effective models
4. **Reporting**: Generate cost estimates for different scenarios
