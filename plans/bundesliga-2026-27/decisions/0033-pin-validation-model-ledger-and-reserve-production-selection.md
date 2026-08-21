# ADR-0033: Pin the validation model ledger and reserve production selection

- Status: Accepted
- Date: 2026-08-21

## Context

Bundesliga 2026/27 needs a cheap, reproducible configuration for plumbing validation before the owner selects a production model. The validation path must prove prompt resolution, context, persistence, traces, verification, and posting without becoming an accidental production default. The owner must later choose the launch model, reasoning effort, output cap, exact prompts, arena challengers, fallback policy, and cost ceiling from experiment and whole-season evidence.

P0-05 promoted the accepted hosted prompt mirrors. The exact production prompt identities are match version `2`, normalized SHA-256 `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`, and bonus version `1`, normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.

The authoritative cost-estimate store has no `gpt-5.6-luna` / `none` base row. Official model documentation records a 2026-02-16 knowledge cutoff, so the estimator's two-day sampling cutoff is `2026-02-18T00:00:00 Europe/Berlin (+01)`. Producing the missing row still requires separately authorized paid predictions.

## Decision

The development and arena plumbing identity is pinned in the tracked Bundesliga model ledger as:

- competition `bundesliga-2026-27`;
- model `gpt-5.6-luna`;
- reasoning effort `none`;
- maximum output tokens `10000`;
- hosted match prompt `kicktippai/bundesliga-2026-27/predict-one-match`, version `2`;
- hosted bonus prompt `kicktippai/bundesliga-2026-27/predict-bonus`, version `1`.

This is a validation identity only. It is not a command default, a production selection, or authority to activate a workflow or schedule. Every validation invocation and future P0-19 workflow must pass the exact model, effort, cap, competition, prompt name, and numbered prompt version. The production-label default may resolve the accepted numbered versions, while explicit `staging` or `latest` labels remain label-resolved unless a numeric version is also supplied. A label-only candidate run does not claim an exact stored prompt identity.

Runtime observation metadata and stored prediction identity include competition, model, reasoning effort, output cap, and exact prompt name/version when known. Legacy model-only and reasoning-only rows cannot satisfy a pinned cap/prompt lookup.

Production onboarding remains paused. The owner must select and record the final production row and cost ceiling before P0-19/P0-21 can activate production workflows. The missing Luna/none whole-season dollar row remains a separate two-stage spend gate: first authorize one exact match-v2 observation, then use its cost to state the expected 20-item spend and obtain a second confirmation for the prescribed 5-by-4 base estimate. Only after those observations are persisted may the 306/493 estimator run produce actionable dollar evidence.

Official capability and pricing evidence is recorded directly from [the `gpt-5.6-luna` model page](https://developers.openai.com/api/docs/models/gpt-5.6-luna) and [OpenAI API pricing](https://developers.openai.com/api/docs/pricing).

## Consequences

- Plumbing runs are reproducible and cannot silently reuse a prediction from another cap or prompt version.
- Candidate labels remain usable without being silently redirected to the production version.
- Exact season dollars are intentionally unavailable until the owner authorizes the one-item preflight and then the paid 20-item base evidence.
- No production model, arena challenger matrix, fallback behavior, cost ceiling, workflow, or schedule is selected by this ADR.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-19](../tasks/p0-19-community-workflow-triad.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None.
