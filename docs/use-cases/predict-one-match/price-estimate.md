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

**Total Kicktipp Context:** ~1,315 tokens (with ~1,035 cacheable, ~280 unique)

💡 **Future Enhancement Hint:** Additional strategic context (provided to the model via tools, because it will only situationally be useful) could include prediction history, bonus predictions, future match day pairings, and Bundesliga rules. These would provide situational data for strategic decisions aimed at winning the community competition.

### Match Input Tokens

The match input consists of a serialized `Match` object in minimized JSON format:

**Example Match Input:**

```json
{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2025-01-18T15:30:00Z"}
```

**Estimated Tokens:** ~35 tokens (this one is 32 according to the [openai tokenizer](https://platform.openai.com/tokenizer))

## Scenarios

### Simple Baseline

⚠️ **Known Limitations:** This scenario significantly underestimates costs for reasoning models (o1, o1-pro, o3, o4-mini, o3-mini, o1-mini) because it assumes only 150 output tokens. Reasoning models generate substantial reasoning tokens that count as output tokens, potentially increasing output token usage by 5-10x or more.

#### Simple Baseline Assumptions

- **Context Tokens**: 5,000 (Low), 15,000 (Medium), 30,000 (High)
- **System Message Tokens**: 500 (consistent across scenarios)
- **Match Input Tokens**: 200 (consistent across scenarios)
- **Output Tokens**: 150 (consistent across scenarios)

#### Cost Estimates by Model and Token Usage

| Model | Low Context (5.7K total) | Medium Context (15.7K total) | High Context (30.7K total) |
|-------|---------------------------|-------------------------------|----------------------------|
| gpt-4.1 | $0.0126 | $0.0335 | $0.0634 |
| gpt-4.1-mini | $0.0025 | $0.0065 | $0.0125 |
| gpt-4.1-nano | $0.0006 | $0.0016 | $0.0031 |
| gpt-4.5-preview | $0.4503 | $1.2003 | $2.3253 |
| gpt-4o | $0.0159 | $0.0418 | $0.0792 |
| gpt-4o-mini | $0.0010 | $0.0025 | $0.0047 |
| o1 | $0.0945 | $0.2445 | $0.4695 |
| o1-pro | $0.9450 | $2.4450 | $4.6950 |
| o3 | $0.0126 | $0.0335 | $0.0634 |
| o4-mini | $0.0069 | $0.0179 | $0.0344 |
| o3-mini | $0.0069 | $0.0179 | $0.0344 |
| o1-mini | $0.0069 | $0.0179 | $0.0344 |

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
