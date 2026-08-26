# `gpt-5.6-sol max` Base Estimate Evidence

Date: `2026-08-26`

This is cost and plumbing evidence only. It makes no prediction-quality or
production-selection claim. The sample uses the explicit Bundesliga 2025/26
seven-document historical compatibility route as a preseason proxy for the
Bundesliga 2026/27 match-v2 route. It may understate the live eleven-document
input cost.

## Configuration and provenance

- Model / reasoning: `gpt-5.6-sol` / `max`
- Hosted prompt: `kicktippai/bundesliga-2026-27/predict-one-match`, exact
  version `2`, required `production` membership
- Prompt route: `Langfuse Bundesliga match v2; Bundesliga 2025/26 7-document legacy-id-hash-v1 context`
- Knowledge / sampling cutoffs: `2026-02-16` /
  `2026-02-18T00:00:00 Europe/Berlin (+01)`
- Evaluation: `startsAt -12:00:00`
- Output cap / planning tier: `20000` / Flex
- Seed / topology: `20260821`, one-item preflight followed by exact `5 × 4`
- Eligible pool: `109` fixtures, hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`
- Reused hosted datasets: `cmt86fx6o0aeuad0dg99ivamv` (one item) and
  `cmt86m8gn0awvad0eyx7mn5f6` (20 items); deterministic reconstruction matched
  the existing hosted artifacts, so no dataset sync was performed
- One-item dataset / manifest SHA-256:
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717` /
  `b396ffd599c8c79569db656d66e68ebe9169caf9a7e274d1aa0e7a0c8f8017c1`
- Five-by-four dataset / manifest SHA-256:
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18` /
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`

[Official OpenAI model documentation](https://developers.openai.com/api/docs/models/gpt-5.6-sol)
records `max` support, a `2026-02-16` knowledge cutoff, and the short-context
standard prices used by the repository (`$4` input and `$20` output per
million tokens). The estimator applies the repository's Flex multiplier and
prices every input token as uncached.

## Admission and wall-clock boundary

The Owner authorized strictly less than `$5` of new Sol/`max` exposure and a
shared live deadline. The one-item bootstrap used the full `272000`-token
short-context boundary plus the full `20000` output cap. The machine gate
projected `$0.744000000000` and allowed it under the strict incremental ceiling;
the simultaneous global P0 gate reconciled all 31 earlier attempts and three
reservations and also allowed it under `$30`.

The monotonic clock began at `2026-08-26T20:29:20.3751477Z`. The Owner extended
the same clock from 60 to 120 minutes without resetting its start. Accepted
five-by-four collection completed at `2026-08-26T20:44:42.0904613Z`,
`921.701252` seconds after the original start. No retry was needed.

## One-item preflight

- Dataset run: `492c8cad-9cda-4dd9-ab1c-31b22a32cddf`
- Run suffix: `2026-08-26t20-29-20z`
- Usage: `2463` input, `9893` output, `9874` reasoning tokens
- Tier: Flex requested and observed; no fallback
- Observed paid cost: `$0.103856000000`
- Cap outcome: `9893 / 20000`, successful with output text and below cap

The exact one-item provisional row projected `$2.077120000000` for the pending
20 calls. Fresh dual gates recorded incremental all-in exposure
`$2.180976000000` and global all-in exposure `$6.988913270000`; both were
strictly below their ceilings before calibration began.

## Authoritative five-by-four row

Parallelism `5` completed the exact 20 linked items, so no p3 or p1 retry was
made. Immutable collection binds dataset run
`0205df36-af15-47ab-9f7e-4caf844932a3` to the 20-item manifest hash above.

- Observed tiers: Flex `19`, Standard fallback `1`
- Input / observed cached input: `48752` / `38979`
- Output / reasoning: `22312` / `21932`
- Maximum per-item output / reasoning: `6751` / `6732`
- Observed paid cost: `$0.265793800000`
- All-input-uncached Flex estimate: `$0.320624000000`
- Average planning cost per prediction: `$0.016031200000`

The planning row deliberately remains all-Flex and all-input-uncached even
though the accepted execution had one Standard fallback and observed cache
reads. Those execution facts remain explicit so a later operational estimate
can model fallback share separately.

Final machine ledgers contain both new observed attempts, totaling
`$0.369649800000`. The global observed total is `$5.077987070000`; its three
older unresolved-call reservations remain `$0.099600000000`, so actual global
observed-plus-existing-reserved exposure is `$5.177587070000`. Each final gate
also includes one explicitly unexecuted `N=1` authoritative-row guard because
`budget-gate` requires a candidate: incremental all-in is `$0.385681000000`,
and global all-in is `$5.193618270000`. The `$0.016031200000` sentinel is
conservative one-more-call headroom, not spent, in flight, or reserved; no call
was made for it.

## Estimator output

Exact command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-sol --reasoning-effort max
```

Exact estimates:

```text
N=20: $0.320624000000
N=100: $1.603120000000
N=150: $2.404680000000
N=200: $3.206240000000
N=306: $4.905547200000
N=493: $7.903381600000
```

The `493` figure reuses the repository's documented Bundesliga reprediction
baseline. It covers match predictions only, not bonus calls, and remains a
seven-document preseason proxy rather than live-season cost proof.
