# P0-05 — Establish the 2026/27 prompt route

- Status: Implementation complete — staging publication approval and production promotion pending
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md)

## Outcome

Match and bonus predictions resolve to prompts that explicitly describe Bundesliga 2026/27 and its new context documents.

## Work items

- [ ] Create the hosted prompt routes `kicktippai/bundesliga-2026-27/predict-one-match` and `kicktippai/bundesliga-2026-27/predict-bonus`.
- [ ] Use `latest` or a dedicated staging label for candidate validation and the deliberately promoted `production` label for scheduled production.
- [ ] Keep synchronized checked-in mirrors of the promoted content as outage/first-fetch fallback and expose fallback use in traces.
- [x] Create or update the match, justification, and bonus prompt content for 2026/27; preserving a runnable 2025/26 prompt route is not required.
- [x] Remove instructions that expect transfer documents and describe Club Elo, current rosters, and squad summaries instead.
- [x] Make `CompetitionResolver.ResolveRuntimeMetadata` return an unambiguous 2026/27 prompt identity.
- [x] Update prompt-provider, prompt-path, reconstruction, and runtime-metadata tests.
- [x] Record a stable prompt version or content hash that P0-06 can put in the model ledger.

## Validation

- Run prompt provider/composer/service tests in `tests/OpenAiIntegration.Tests` and `CompetitionResolverTests` in `tests/Orchestrator.Tests`.
- Reconstruct one match and one bonus prompt and inspect season text and document labels.

## Candidate implementation evidence

- 2026-08-21: Bundesliga runtime metadata now defaults both prediction kinds to hosted Langfuse with the accepted route name, the non-floating `production` label, and the fixed `bundesliga-2026-27` local fallback model. Candidate validation can override the label to `staging`; code does not assign or move `production`.
- 2026-08-21: Checked-in candidate mirrors exist at `prompts/bundesliga-2026-27/match.md`, `match.justification.md`, and `bonus.md`. The two match files intentionally contain identical schema-aware content so the one accepted hosted match route serves responses with and without justification. Their normalized SHA-256 is `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`; the bonus mirror hash is `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`. Hashing normalizes line endings to LF, trims trailing whitespace, restores one final LF, and hashes UTF-8 bytes.
- 2026-08-21: Hosted and local providers expose requested source, actual source, prompt name, requested label, resolved hosted version when present, fallback status/path, template path, and normalized content hash on generation telemetry. Only a resolved hosted prompt sets the Langfuse prompt-link name/version fields.
- 2026-08-21: Replaced the over-broad WM-era hosted-justification guards with an exact-route boundary: the schema-aware Bundesliga hosted match route supports responses with and without justification, while the existing WM26 hosted match route remains fail closed in the provider, ordinary commands, experiment settings, and prepared executor. Focused validation passed before that final WM regression remediation: `CompetitionResolverTests` 15/15, `LangfuseAndServiceRegistrationTests` 26/26, matchday settings 23/23, matchday error handling 7/7, bonus settings 20/20, and experiment settings/executor 19/19. Full `OpenAiIntegration.Tests` passed 217/217, including mirror content, exact-placeholder reconstruction for one match and one bonus prompt, hash normalization, and trace telemetry. The final exact-route regression counts are recorded after rerun below.
- 2026-08-21: Final exact-route remediation validation passed: `LangfuseAndServiceRegistrationTests` 27/27, matchday settings 24/24, RandomMatch additional coverage 9/9, experiment settings/executor 20/20, and the exact trace-route telemetry regression 1/1. An explicit `Orchestrator.Tests` project build succeeded with zero errors before the final command reruns.
- 2026-08-21: The installed Langfuse CLI 0.0.11 can read prompts but its bundled `CreatePromptRequest` remains an unflattened `oneOf`, so prompt creation exposes no body flags; refetching the spec also fails its YAML alias guard. The staging-only authenticated API fallback was stopped by external approval review before execution. No hosted version or label changed. Once explicit upload approval is available, create and read back both `staging` candidates, record their versions, and verify these hashes before marking the hosted-route work items complete.

## Complete when

- No live Bundesliga prompt says 2025/26 or requests transfer documents.
- Traces expose the selected prompt source and stable prompt version.
- The `production` label and local mirror resolve to the same approved content.
