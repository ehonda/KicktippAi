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

## Cost Scenarios by Model and Token Usage

### Scenario Assumptions

- **Context Tokens**: 5,000 (Low), 15,000 (Medium), 30,000 (High)
- **System Message Tokens**: 500 (consistent across scenarios)
- **Match Input Tokens**: 200 (consistent across scenarios)
- **Output Tokens**: 150 (consistent across scenarios)

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

### Cost Analysis Summary

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
