# ADR-0038: Bound bonus context by question policy

- Status: Accepted
- Date: 2026-08-21

## Context

ADR-0024 establishes the safe Bundesliga bonus baseline and exact roster targeting, while ADR-0037 binds the selected bytes to immutable prediction provenance. The remaining pre-launch risk is an implicit or unbounded selection policy: broad wording matches can misclassify a question, an all-roster fallback can enlarge prompts based on storage contents, and a partial budget truncation can give only some answer options detailed roster evidence.

P0-16 must make category, selection, exclusions, and estimated context size deterministic and observable before the one-time preseason bonus run. The estimate is a guardrail rather than a model tokenizer and therefore needs a stable implementation-independent formula.

## Decision

### Category policy

Every Bundesliga bonus question is classified into exactly one of these categories from its question text:

| Category | Accepted signals | Context policy |
|---|---|---|
| `Champion` | whole German or English champion/league-winner phrases | headed Club Elo aggregate, then headed squad summary |
| `Relegation` | whole German or English relegation/relegated/bottom-place phrases | headed Club Elo aggregate, then headed squad summary |
| `TopScorer` | whole German or English top-scorer/most-goals phrases | aggregate baseline plus only exact team or player roster targets |
| `Coach` | whole German or English coach/dismissal/coach-change phrases | aggregate baseline plus only exact team or coach roster targets |
| `Unknown` | no supported signal | aggregate baseline only |

Matching is ordinal case-insensitive and Unicode phrase-boundary aware. A substring inside a longer letter-or-digit token is not a signal. Options never determine the category. If more than one supported category matches, classification fails actionably rather than choosing a priority that could select the wrong member role.

For `TopScorer` and `Coach`, an option targets a team or relevant member only when its trimmed value equals that canonical identity. Question text may contain the exact identity as a whole phrase. Targets are unique and ordered by manifest slug. A roster-relevant question with no exact target fails closed. `Champion`, `Relegation`, and `Unknown` never select a roster document.

Every result explicitly soft-excludes `team-rosters` with reason `ProhibitedAggregate`. Every canonical `roster-{slug}` not selected is reported in manifest-slug order with either `CategoryDoesNotUseRoster` or `NoExactIdentity`. These policy exclusions are diagnostic and do not make a useful aggregate-only category fail. Stored document presence never adds a candidate.

### Whole-selection budgets

A caller supplies an immutable maximum document count and maximum estimated context-token count. The accepted launch defaults are 20 documents and 32,000 estimated context tokens. The fixed representative P0 set below proves that every existing P0 selection fits and has an unchanged document footprint. The CLI rejects a document budget below the two required aggregate documents and rejects an estimated-token budget below 256 before any provider access.

The estimate covers the exact UTF-8 bytes of the context section rendered for the prompt: for each selected document, `---\n{name}\n\n{content}\n`, followed once by closing `---`. Estimated tokens are `ceiling(totalUtf8Bytes / 4)`. Prompt-template, question JSON, and output tokens are outside this context-only estimate and are not represented as exact tokenizer counts.

The complete selected set is required. If its document count or estimated tokens exceed the configured budget, resolution fails before the model call. It never drops a subset of exact roster targets, because partial truncation could bias a multi-option question. Missing headed publications, semantic validation failures, ambiguous categories, unmapped roster-relevant questions, and over-budget selections retain the P0-15 nonzero/no-placement boundary.

### Result and telemetry

The resolved Core result carries category, ordered selected document names, ordered excluded documents with reasons, estimated UTF-8 bytes, estimated tokens, and both effective budgets alongside the ADR-0037 manifest. The result must describe the exact manifest documents.

Bonus prediction telemetry records those fields on each prediction observation. Metadata is constructed anew per question so a prior category or exclusion list cannot leak into the next prediction. The immutable manifest and persistence schema remain unchanged. WM26 keeps its offline-tested legacy selector and receives none of the Bundesliga budget/category behavior.

P0-18 must surface the accepted budget inputs in reusable Bundesliga workflow composition. P0-16 does not edit workflow files.

### Fixed representative measurement

The deterministic headed publications used by the Firebase provider regression produce this exact comparison. `Documents` is the same P0 selection defined by ADR-0024; bytes and estimated tokens apply the formula above.

| Representative category | Documents | UTF-8 bytes | Estimated tokens | P0 document footprint |
|---|---:|---:|---:|---|
| `Champion` | 2 | 2,250 | 563 | unchanged aggregate baseline |
| `Relegation` | 2 | 2,250 | 563 | unchanged aggregate baseline |
| `TopScorer` | 3 | 4,441 | 1,111 | unchanged baseline plus one exact roster |
| `Coach` | 3 | 4,441 | 1,111 | unchanged baseline plus one exact roster |
| `Unknown` | 2 | 2,250 | 563 | unchanged aggregate baseline |

The maximum observed selection is 3 documents, 4,441 UTF-8 bytes, and 1,111 estimated tokens. It is below both accepted defaults without truncation or a quality exception.

## Alternatives considered

- **Choose a fixed category precedence when multiple categories match:** Rejected because deterministic precedence can still silently select the wrong team-member role; ambiguity fails closed.
- **Truncate roster targets until the budget fits:** Rejected because ordering-based partial evidence can bias answer options.
- **Use a model-specific tokenizer:** Rejected because it adds a changing dependency to a guardrail; the explicit UTF-8 formula is stable and auditable.
- **Load `team-rosters` for unknown or unmapped questions:** Rejected because it defeats the bounded question-aware contract and ADR-0024.
- **Treat every nonselected roster as an error:** Rejected because category and exact-identity exclusions are intentional, useful soft exclusions.

## Consequences

- Every Bundesliga bonus prompt has trace-visible, storage-independent routing and a deterministic context-size estimate.
- Budget overrides cannot silently change the relative evidence available to selected answer options.
- Unknown questions remain useful with the exact two-document aggregate baseline.
- P0-18 must carry the accepted CLI budget inputs into reusable workflows.

## Affected tasks

- [P0-16](../tasks/p0-16-question-aware-bonus-context.md)
- [P0-18](../tasks/p0-18-base-workflow-support.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)

## Supersedes

None. This makes ADR-0024's P0-16 refinement seam concrete without changing its headed baseline, roster targeting, or competition split.
