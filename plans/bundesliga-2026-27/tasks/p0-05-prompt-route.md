# P0-05 — Establish the 2026/27 prompt route

- Status: Not started
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md)

## Outcome

Match and bonus predictions resolve to prompts that explicitly describe Bundesliga 2026/27 and its new context documents.

## Work items

- [ ] Create the hosted prompt routes `kicktippai/bundesliga-2026-27/predict-one-match` and `kicktippai/bundesliga-2026-27/predict-bonus`.
- [ ] Use `latest` or a dedicated staging label for candidate validation and the deliberately promoted `production` label for scheduled production.
- [ ] Keep synchronized checked-in mirrors of the promoted content as outage/first-fetch fallback and expose fallback use in traces.
- [ ] Create or update the match, justification, and bonus prompt content for 2026/27; preserving a runnable 2025/26 prompt route is not required.
- [ ] Remove instructions that expect transfer documents and describe Club Elo, current rosters, and squad summaries instead.
- [ ] Make `CompetitionResolver.ResolveRuntimeMetadata` return an unambiguous 2026/27 prompt identity.
- [ ] Update prompt-provider, prompt-path, reconstruction, and runtime-metadata tests.
- [ ] Record a stable prompt version or content hash that P0-06 can put in the model ledger.

## Validation

- Run prompt provider/composer/service tests in `tests/OpenAiIntegration.Tests` and `CompetitionResolverTests` in `tests/Orchestrator.Tests`.
- Reconstruct one match and one bonus prompt and inspect season text and document labels.

## Complete when

- No live Bundesliga prompt says 2025/26 or requests transfer documents.
- Traces expose the selected prompt source and stable prompt version.
- The `production` label and local mirror resolve to the same approved content.
