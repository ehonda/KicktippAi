# P0-05 — Establish the 2026/27 prompt route

- Status: Complete
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

Match and bonus predictions resolve to prompts that explicitly describe Bundesliga 2026/27 and its new context documents.

## Work items

- [x] Create the hosted prompt routes `kicktippai/bundesliga-2026-27/predict-one-match` and `kicktippai/bundesliga-2026-27/predict-bonus`.
- [x] Use `latest` or a dedicated staging label for candidate validation and the deliberately promoted `production` label for scheduled production.
- [x] Keep synchronized checked-in mirrors of the promoted content as outage/first-fetch fallback and expose fallback use in traces.
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
- 2026-08-21: After explicit owner approval, authenticated publication and readback completed for both accepted routes. `kicktippai/bundesliga-2026-27/predict-one-match` labels `staging`, `production`, and automatic `latest` resolve immutable version 2 with normalized SHA-256 `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`. The initial match version 1 had an encoding mismatch, was superseded by corrected immutable version 2, and is unlabeled. `kicktippai/bundesliga-2026-27/predict-bonus` labels `staging`, `production`, and automatic `latest` resolve version 1 with normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`. Both readback hashes equal the checked-in mirrors; production promotion is complete.
- 2026-08-25: P0-20 exposed that the Langfuse v2 prompt endpoint rejects a request containing both `version` and `label`. [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md) now retrieves immutable configurations by version only, then verifies the returned name, exact version, and required label membership. Binding drift cannot fall back; ordinary fetch failures retain the visible ADR-0004 mirror fallback, while Bundesliga dev validation requires the hosted binding before prediction-service construction.
- 2026-08-27: After the Owner selected ADR-0052 and the parallel Sol/`max`
  experiment finished against byte-identical v2/`production`, authenticated
  publication created exact text prompt version 3. Readback verified name
  `kicktippai/bundesliga-2026-27/predict-one-match`, type `text`, immutable
  version `3`, labels `production`, `staging`, and automatic `latest`, and
  normalized SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`.
  Each of the three labels resolved version 3. The hosted content hash equals
  both byte-identical checked-in match mirrors. The bonus route was not
  mutated. No prompt experiment or model call was made during promotion.

## Complete when

- No live Bundesliga prompt says 2025/26 or requests transfer documents.
- Traces expose the selected prompt source and stable prompt version.
- The `production` label and local mirror resolve to the same approved content.
