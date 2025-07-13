# Use Case "Predict One Match" - Price Estimate

## Goal

We want to estimate the price for a single run of the use case.

## Pricing Information (per 1M tokens)

| Model | Input Price | Cached Input Price | Output Price |
|-------|-------------|-------------------|--------------|
| gpt-4.1 | $2.00 | $0.50 | $8.00 |
| gpt-4.1-mini | $0.40 | $0.10 | $1.60 |
| gpt-4.1-nano | $0.10 | $0.025 | $0.40 |
| gpt-4.5-preview | $75.00 | $37.50 | $150.00 |
| gpt-4o | $2.50 | $1.25 | $10.00 |
| gpt-4o-mini | $0.15 | $0.075 | $0.60 |
| o1 | $15.00 | $7.50 | $60.00 |
| o1-pro | $150.00 | - | $600.00 |
| o3 | $2.00 | $0.50 | $8.00 |
| o4-mini | $1.10 | $0.275 | $4.40 |
| o3-mini | $1.10 | $0.55 | $4.40 |
| o1-mini | $1.10 | $0.55 | $4.40 |

## Price Calculation Formula

For a single use case run, the total cost is calculated as:

```text
Total Cost = (Context Tokens × Input Price) + (System Message Tokens × Input Price) + (Match Input Tokens × Input Price) + (Output Tokens × Output Price)
```

Where:

- **Context Tokens**: Retrieved context from Context Service (historical data, team stats, etc.)
- **System Message Tokens**: Fixed prompt instructions for the AI predictor
- **Match Input Tokens**: Tokens for the specific match information being predicted
- **Output Tokens**: Generated prediction response tokens

All prices are per 1M tokens, so the formula becomes:

```text
Total Cost = ((Context + System Message + Match Input) / 1,000,000 × Input Price) + (Output / 1,000,000 × Output Price)
```

💡 **Note:** This is a conservative estimate. In typical usage, multiple matches are predicted in sequence on a match day. System message tokens and a proportion of context tokens would be cached across predictions, reducing costs by utilizing the lower cached input prices.

## Context Token Estimation

To create more realistic price estimates, we need to understand the different types of context and their typical token counts.

### Kicktipp Context

This category includes context specific to the prediction game and current state:

| Context Type | Estimated Tokens | Caching Behavior | Notes |
|--------------|------------------|------------------|-------|
| **Community Rules** | 150 | ✅ Cached | Point system rules (2-4 points for tendency/goal difference/exact result), tie-breaking rules. Optimized with examples for "Tendenz", "Tordifferenz", "Ergebnis" |
| **Current Community Standings** | 400 | ✅ Cached | Player rankings and total points only (excluding individual predictions). Typically only relevant for strategic decisions on final matchdays - should be provided via tool call when needed |
| **Bundesliga Standings** | 450 | ✅ Cached | League table in CSV format: Position, Team, Games, Points, Goal_Ratio, Goals_For, Goals_Against, Wins, Draws, Losses (18 teams) |
| **Recent History (Last 10 Games)** | 280 | ❌ Never Cached | Team-specific match results for both teams in CSV format: League, Home_Team, Away_Team, Score (~140 tokens per team) |
| **Metadata** | 35 | ✅ Cached | Document headers: prediction-game-rules.md, bundesliga-standings.csv, recent-history-home-team.csv, recent-history-away-team.csv |

**Total Kicktipp Context:** ~915 tokens (with ~635 cacheable, ~280 unique)

💡 **Note:** Current Community Standings (400 tokens) are excluded from this total as they are only included situationally via tool calls on final matchdays.

💡 **Future Enhancement Hint:** Additional strategic context (provided to the model via tools, because it will only situationally be useful) could include prediction history, bonus predictions, future match day pairings, and Bundesliga rules. These would provide situational data for strategic decisions aimed at winning the community competition.

### Match Input Tokens

The match input consists of a serialized `Match` object in minimized JSON format:

**Example Match Input:**

```json
{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2025-01-18T15:30:00Z"}
```

**Estimated Tokens:** ~35 tokens (this one is 32 according to the [openai tokenizer](https://platform.openai.com/tokenizer))

## Total Cost Limitations

⚠️ **Important Limitations for Matchday and Season Cost Estimates:**

The "Per Matchday" and "Per Season" cost columns in the following scenarios have known limitations:

- **Matchday costs are underestimated** for the final few matchdays of the season, where strategic decision-making may require additional context such as current community standings (adding ~400 tokens per prediction) or bonus prediction context for strategic considerations
- **Season costs exclude bonus predictions**, which are placed once before the season starts and are separate from regular matchday predictions
- **Caching benefits are not reflected** in these estimates, though in practice, system messages and most context would be cached across multiple predictions on the same matchday, reducing actual costs

These estimates should be considered conservative baselines that may underestimate actual usage costs by an estimated 10-20% (rough estimate based on strategic context usage frequency), depending on how often additional strategic context is utilized.

## Scenarios

### Simple Baseline

⚠️ **Known Limitations:** This scenario significantly underestimates costs for reasoning models (o1, o1-pro, o3, o4-mini, o3-mini, o1-mini) because it assumes only 150 output tokens. Reasoning models generate substantial reasoning tokens that count as output tokens, potentially increasing output token usage by 5-10x or more.

#### Simple Baseline Assumptions

- **Context Tokens**: 5,000 (Low), 15,000 (Medium), 30,000 (High)
- **System Message Tokens**: 500 (consistent across scenarios)
- **Match Input Tokens**: 200 (consistent across scenarios)
- **Output Tokens**: 150 (consistent across scenarios)

#### Cost Estimates by Model and Token Usage

| Model | Low Context (5.7K total) | Medium Context (15.7K total) | High Context (30.7K total) | Per Matchday (9 matches) | Per Season (34 matchdays) |
|-------|---------------------------|-------------------------------|----------------------------|---------------------------|----------------------------|
| gpt-4.1 | $0.0126 | $0.0335 | $0.0634 | $0.11 - $0.57 | $3.88 - $19.48 |
| gpt-4.1-mini | $0.0025 | $0.0065 | $0.0125 | $0.02 - $0.11 | $0.77 - $3.83 |
| gpt-4.1-nano | $0.0006 | $0.0016 | $0.0031 | $0.01 - $0.03 | $0.18 - $0.95 |
| gpt-4.5-preview | $0.4503 | $1.2003 | $2.3253 | $4.05 - $20.93 | $137.69 - $711.64 |
| gpt-4o | $0.0159 | $0.0418 | $0.0792 | $0.14 - $0.71 | $4.86 - $24.24 |
| gpt-4o-mini | $0.0010 | $0.0025 | $0.0047 | $0.01 - $0.04 | $0.31 - $1.44 |
| o1 | $0.0945 | $0.2445 | $0.4695 | $0.85 - $4.23 | $28.89 - $143.65 |
| o1-pro | $0.9450 | $2.4450 | $4.6950 | $8.51 - $42.26 | $289.13 - $1,436.73 |
| o3 | $0.0126 | $0.0335 | $0.0634 | $0.11 - $0.57 | $3.88 - $19.48 |
| o4-mini | $0.0069 | $0.0179 | $0.0344 | $0.06 - $0.31 | $2.11 - $10.52 |
| o3-mini | $0.0069 | $0.0179 | $0.0344 | $0.06 - $0.31 | $2.11 - $10.52 |
| o1-mini | $0.0069 | $0.0179 | $0.0344 | $0.06 - $0.31 | $2.11 - $10.52 |

#### Cost Analysis Summary

**Most Cost-Effective Options:**

1. **gpt-4.1-nano**: $0.0006 - $0.0031 per prediction
2. **gpt-4o-mini**: $0.0010 - $0.0047 per prediction
3. **gpt-4.1-mini**: $0.0025 - $0.0125 per prediction

**Premium Options:**

- **o1-pro**: $0.9450 - $4.6950 per prediction (highest reasoning capability)
- **gpt-4.5-preview**: $0.4503 - $2.3253 per prediction (latest preview model)

**Balanced Options:**

- **gpt-4o**: $0.0159 - $0.0792 per prediction (good performance/cost ratio)
- **o1**: $0.0945 - $0.4695 per prediction (advanced reasoning)

### Simple Baseline v2

This scenario uses our refined token estimates for context and match input while keeping placeholder estimates for system message and output tokens.

⚠️ **Known Limitations:** This scenario still significantly underestimates costs for reasoning models (o1, o1-pro, o3, o4-mini, o3-mini, o1-mini) because it assumes only 150 output tokens. Reasoning models generate substantial reasoning tokens that count as output tokens, potentially increasing output token usage by 5-10x or more.

#### Simple Baseline v2 Assumptions

- **Context Tokens**: 915 (Kicktipp Context from refined estimates, excluding situational community standings)
- **System Message Tokens**: 500 (placeholder, to be refined)
- **Match Input Tokens**: 35 (refined estimate based on actual JSON)
- **Output Tokens**: 150 (placeholder, to be refined)

#### Cost Estimates by Model and Token Usage v2

| Model | Total Input: 1,450 tokens | Total Cost per Prediction | Per Matchday (9 matches) | Per Season (34 matchdays) |
|-------|---------------------------|---------------------------|---------------------------|----------------------------|
| gpt-4.1 | $0.0029 | $0.0041 | $0.037 | $1.25 |
| gpt-4.1-mini | $0.0006 | $0.0008 | $0.007 | $0.25 |
| gpt-4.1-nano | $0.0001 | $0.0002 | $0.002 | $0.06 |
| gpt-4.5-preview | $0.1088 | $0.1313 | $1.18 | $40.18 |
| gpt-4o | $0.0036 | $0.0051 | $0.046 | $1.56 |
| gpt-4o-mini | $0.0002 | $0.0003 | $0.003 | $0.09 |
| o1 | $0.0218 | $0.0308 | $0.277 | $9.42 |
| o1-pro | $0.2175 | $0.3075 | $2.77 | $94.09 |
| o3 | $0.0029 | $0.0041 | $0.037 | $1.25 |
| o4-mini | $0.0016 | $0.0022 | $0.020 | $0.67 |
| o3-mini | $0.0016 | $0.0022 | $0.020 | $0.67 |
| o1-mini | $0.0016 | $0.0022 | $0.020 | $0.67 |

#### Cost Analysis Summary v2

**Most Cost-Effective Options:**

1. **gpt-4.1-nano**: $0.0002 per prediction
2. **gpt-4o-mini**: $0.0003 per prediction  
3. **gpt-4.1-mini**: $0.0008 per prediction

**Premium Options:**

- **o1-pro**: $0.3075 per prediction (highest reasoning capability)
- **gpt-4.5-preview**: $0.1313 per prediction (latest preview model)

**Balanced Options:**

- **gpt-4o**: $0.0051 per prediction (good performance/cost ratio)
- **o1**: $0.0308 per prediction (advanced reasoning)

### Baseline Reasoning

This scenario provides refined estimates specifically for reasoning models, using actual system message token counts and observed reasoning token patterns.

#### Baseline Reasoning Assumptions

- **Context Tokens**: 915 (Kicktipp Context from refined estimates, excluding situational community standings)
- **System Message Tokens**: 200 (refined estimate based on actual instructions_template.md)
- **Match Input Tokens**: 35 (refined estimate based on actual JSON)
- **Output Tokens**: 1,500 (refined estimate based on observed reasoning token patterns for o1 models)

#### Cost Estimates by Model and Token Usage - Reasoning Models

| Model | Total Input: 1,150 tokens | Total Cost per Prediction | Per Matchday (9 matches) | Per Season (34 matchdays) |
|-------|---------------------------|---------------------------|---------------------------|----------------------------|
| o1 | $0.0173 | $0.0900 | $0.810 | $27.54 |
| o1-pro | $0.1725 | $0.9000 | $8.10 | $275.40 |
| o3 | $0.0023 | $0.0120 | $0.108 | $3.67 |
| o4-mini | $0.0013 | $0.0066 | $0.059 | $2.02 |
| o3-mini | $0.0013 | $0.0066 | $0.059 | $2.02 |
| o1-mini | $0.0013 | $0.0066 | $0.059 | $2.02 |

#### Cost Analysis Summary - Reasoning Models

**Most Cost-Effective Reasoning Options:**

1. **o4-mini**: $0.0066 per prediction
2. **o3-mini**: $0.0066 per prediction  
3. **o1-mini**: $0.0066 per prediction

**Premium Reasoning Options:**

- **o1-pro**: $0.9000 per prediction (highest reasoning capability)
- **o1**: $0.0900 per prediction (advanced reasoning)

**Balanced Reasoning Options:**

- **o3**: $0.0120 per prediction (good reasoning/cost ratio)

💡 **Note:** These estimates are based on observed reasoning token patterns of ~1,500 tokens for o1 models. Actual reasoning token usage may vary significantly based on problem complexity and model behavior.
