# Schadensfresse Live Rules

Schema version: `schadensfresse-live-rules-v1`

- Tips are visible before the deadline: `false`
- Prediction mode: `exact-score`
- Tie break: `matchday-wins-unless-otherwise-agreed`
- Lead time minutes: `0`

## Result bases

1. `bundesliga` | `1. Bundesliga 2026/27` | `regularTime90Minutes`
2. `dfb-pokal` | `DFB-Pokal 2026/27` | `finalScoreIncludingExtraTimeAndPenaltyShootout`
3. `uefa-champions-league` | `Champions League 2026/27` | `finalScoreIncludingExtraTimeAndPenaltyShootout`

## Match scoring

| result | tendencyPoints | goalDifferencePoints | exactResultPoints |
| --- | ---: | ---: | ---: |
| win | 2 | 3 | 5 |
| draw | 3 | null | 5 |

## Bonus scoring

- Points per correct answer: `9`
- Answer order matters: `false`
