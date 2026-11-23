# ADR-0088: Move production predictions to GPT-6.2 Sol

- Status: Proposed
- Date: 2026-11-24

## Context

The project owner selected GPT-6.2 Sol as the production successor to GPT-5.6 Sol. OpenAI's GPT-5.6 Sol promotion runs through November 30, 2026. The production predictor currently requests Flex and retries with Standard when Flex capacity is unavailable. GPT-6.2 Sol supports synchronous Standard processing. [OpenAI pricing](https://developers.openai.com/api/docs/pricing) · [OpenAI Flex guide](https://developers.openai.com/api/docs/guides/flex-processing)

## Decision

Run `gpt-6.2-sol` with reasoning effort `xhigh`, a 10,000-token output cap, and explicit Standard processing (`service_tier: default`). Retain the pinned Langfuse match v3 and Bundesliga bonus v1 routes and the existing fallback prompts. Move the `pes-squad` source and its three scheduled matchday copy targets together. The corresponding manual matchday workflows and the Bundesliga bonus workflows for `pes-squad`, `relaxdays-tippt`, and the arena production participant use the new model as well.

The manual Schadensfresse Champions League bonus retains its separately pinned GPT-5.6 Sol profile. Arena challengers retain their selected models and reasoning efforts. Preserve the scheduled matchday triggers, job order, concurrency group, posting accounts, and workflow filenames. The arena production account retains its existing credential names.

## Alternatives considered

- Keep GPT-5.6 Sol on Flex. This preserves existing prediction keys and incurs the older model's regular rates after the promotion.
- Use discounted background processing. This would require asynchronous completion handling and would introduce completion windows measured in hours into the matchday workflow.

## Consequences

GPT-6.2 Sol short-context Standard rates are $2.00 per million uncached input tokens, $0.10 per million cached input tokens, and $10.00 per million output tokens. Actual prediction spend depends on model usage. [GPT-6.2 Sol pricing](https://developers.openai.com/api/docs/models/gpt-6.2-sol)

The model is part of the prediction storage key. At cutover, eligible open fixtures with GPT-5.6 Sol picks can receive a fresh GPT-6.2 Sol prediction at index 0 and a replacement Kicktipp post. Each copy target requires a compatible GPT-6.2 Sol source record. Existing picks retain their original model identity in storage.

The review branch can be pushed while a production run is active. That run uses its triggering commit. The first scheduled run triggered after the reviewed change lands on `main` uses the new model.

## Affected tasks

- [Production and arena community matrix](../../../docs/onboarding-bundesliga-2026-27/community-onboarding.md)
- [Production-live matchday schedule](0053-schedule-the-production-live-matchday-lane.md)

## Supersedes

The production model and service tier portions of [ADR-0052](0052-select-production-model-community-matrix-and-match-prompt-v3.md).
