# `gpt-5.6-luna none` Base Estimate Evidence

Date: `2026-08-25`

This is cost and plumbing evidence only. It makes no prediction-quality claim.
The sample uses the explicit Bundesliga 2025/26 seven-document historical
compatibility route as a preseason proxy for Bundesliga 2026/27 match prompt
v2. It can understate the live eleven-document 2026/27 input cost.

## Configuration and sample provenance

- Model: `gpt-5.6-luna`
- Reasoning effort: `none`
- Prompt route: `Langfuse Bundesliga match v2; Bundesliga 2025/26 7-document legacy-id-hash-v1 context`
- Hosted prompt: `kicktippai/bundesliga-2026-27/predict-one-match`, exact version `2`, required label `production`
- Model knowledge cutoff: `2026-02-16`
- Sampling cutoff: fixtures start strictly after `2026-02-18T00:00:00 Europe/Berlin (+01)`
- Evaluation policy: `startsAt -12:00:00`
- Maximum output tokens: `10000`
- Base-estimate service tier: `flex`
- Compatibility mode: `bundesliga-2025-26-legacy-id-hash-v1`
- Eligibility policy: `bundesliga-2025-26-completed-after-sampling-cutoff-all-7-context-documents-at-or-before-starts-at-minus-12h-v1`
- Eligible fixture count: `109`
- Eligible source-ID hash: `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`
- Selected source-ID hash: `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`
- Prepared historical artifact SHA-256: `22dfcab23f063e2fbb7a7fa96df4f2fb5dca384bb1329adc0c33157f5419a105`
- Prepared manifest SHA-256: `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`

Seed `20260821` selected these five source fixtures, each repeated four times:

- `1423757259`: Hamburger SV vs RB Leipzig, `2026-03-01T19:30:00 UTC+01 (+01)`
- `1423757286`: VfL Wolfsburg vs Eintracht Frankfurt, `2026-04-11T16:30:00 UTC+02 (+02)`
- `1423757328`: 1. FC Köln vs Bayer 04 Leverkusen, `2026-04-25T16:30:00 UTC+02 (+02)`
- `1423757333`: Hamburger SV vs 1899 Hoffenheim, `2026-04-25T19:30:00 UTC+02 (+02)`
- `1423757341`: FC St. Pauli vs 1. FC Köln, `2026-04-17T21:30:00 UTC+02 (+02)`

Every prepared item binds a completed outcome and exactly seven historical
context document identities, versions, and content hashes. No prompt, context,
or prediction payload is retained in this evidence.

## One-item gate

The authorized one-item preflight selected source fixture `1423757341`:

- dataset ID: `cmt86fx6o0aeuad0dg99ivamv`
- dataset-run ID: `80e17c90-631d-4c89-8640-21fe36fef541`
- run name: `repeated-match-slice__pes-squad__gpt-5.6-luna__match-v2__reasoning-none__random-1x1-seed-20260821__cost-preflight__startsat-12h__2026-08-25t04-40-57z`
- trace ID: `59e783632fedfb512a90717b60ad1a6a`
- usage: `2463` uncached input, `17` output, `0` reasoning tokens
- requested/final tier: `flex` / `flex`; fallback: `false`
- observed Langfuse cost: `$0.0002565`
- cap outcome: `17 / 10000` output tokens (`0.17%`), no cap pressure

## Five-by-four execution and immutable recovery

The dataset ID is `cmt86m8gn0awvad0eyx7mn5f6`. The first parallelism-5
attempt completed all 20 items, but one flex request received HTTP 429 and used
the standard-tier fallback. That attempt is retained only as retry context and
is not the authoritative base sample.

The prescribed parallelism-3 replacement reused the exact manifest, settings,
and shared run name. It completed 20/20 with no warning, non-flex request, or
fallback. Its accepted identities are:

- dataset-run ID: `6a3c4e70-ebb4-4c07-9a1a-7af19c32d995`
- run name: `repeated-match-slice__pes-squad__gpt-5.6-luna__match-v2__reasoning-none__random-5x4-seed-20260821__startsat-12h__2026-08-25t04-45-49z`
- prepared-manifest SHA-256/sample-size tuple: `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7` / `20`

After `--replace-run`, run-name-only collection correctly failed closed at
`40/20` because traces from both paid attempts remained. No traces were
truncated or selected by time. Once ADR-0046 was integrated, exact recovery
used the immutable dataset-item-to-trace links of only the accepted dataset run:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env $LANGFUSE_ENV --group "repeated-match-slice-measured=repeated-match-slice__pes-squad__gpt-5.6-luna__match-v2__reasoning-none__random-5x4-seed-20260821__startsat-12h__2026-08-25t04-45-49z" --dataset-id "repeated-match-slice-measured=cmt86m8gn0awvad0eyx7mn5f6" --dataset-run-id "repeated-match-slice-measured=6a3c4e70-ebb4-4c07-9a1a-7af19c32d995" --manifest "repeated-match-slice-measured=artifacts/langfuse-experiments/repeated-match-slices/pes-squad/all-matchdays-after-20260217t230000z/random-5x4-seed-20260821-gpt-5-6-luna-none-cost-estimate/slice-manifest.json" --expect repeated-match-slice-measured=20 --output .tmp/p0-06-luna-none-accepted-p3-usage.json
```

The collector admitted exactly 20 distinct dataset item/trace links and bound
the exact dataset, dataset run, run name, manifest hash, and manifest sample
size into every compact record.

## Authoritative base row

`upsert-row` produced:

- observations: `20`
- observed service tiers: `{'flex': 20}`
- non-flex requests/retries and fallback-used requests: `0` / `0` / `0`
- total input tokens: `48752`
- observed cached-input tokens: `48692`
- total output tokens: `340`
- total reasoning tokens: `0`
- maximum per-item output: `17` of `10000`
- all-input-uncached flex estimate: `$0.005079200000`
- average cost per match prediction: `$0.000253960000`
- observed Langfuse cost total with cache reads: `$0.000696920000`

Per the estimator contract, the authoritative estimate prices every input
token as uncached even though Langfuse observed cache reads.

The required estimator command was run verbatim:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 306,493 --model gpt-5.6-luna --reasoning-effort none
```

Exact estimator output:

```text
N=306: $0.077711760000
N=493: $0.125202280000
```
