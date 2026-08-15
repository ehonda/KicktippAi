# ADR-0006: Stage validation with a cheap test model

- Status: Accepted
- Date: 2026-08-16

## Context

Development and workflow validation need to prove context, persistence, prompt, posting, trace, and scheduling behavior. Prediction quality is irrelevant to those checks, and repeatedly using the eventual production model would waste money. Final production-model selection also needs direct owner involvement, experiment evidence, and whole-season cost estimates.

## Decision

Autonomous development and arena plumbing validation use [`gpt-5.6-luna`](https://developers.openai.com/api/docs/models/gpt-5.6-luna) with `none` reasoning and an explicitly pinned safe output cap. `gpt-5.6-luna` is the current durable cost-sensitive tier; older cheaper models already listed for retirement in the [OpenAI deprecations](https://developers.openai.com/api/docs/deprecations) are not selected for this path. Agents must never silently promote the test configuration to production.

Agents may write autonomously to `ehonda-dev-buli-2627`. The owner has confirmed `ehonda-ai-arena`, its Luna participant, local sibling `.env`, and model-specific Actions secrets ready, so one authorization covers this arena-only ladder:

1. local CLI validation;
2. `workflow_dispatch` validation;
3. scheduled validation;
4. Kicktipp, Firestore, Langfuse, prompt, and workflow-order inspection;
5. in-scope disable, fix, and retry work when a gate fails.

The final production model, reasoning effort, output cap, prompts, and arena challenger matrix remain unresolved until the owner-led experiment and cost-estimate decision. Final workflows are manually dispatched once and inspected before their schedules are enabled.

## Alternatives considered

- **Use the final production model for all validation:** Rejected as unnecessary spend.
- **Use deprecated `gpt-5-nano`:** Rejected because it is scheduled to shut down during the season.
- **Enable final schedules before a manual production run:** Rejected because manual evidence provides a safer last gate and creates the actual opening predictions rather than a throwaway run.

## Consequences

- Agent guidance must prominently distinguish plumbing validation from prediction-quality evaluation.
- Dev runs do not require repeated approval.
- Arena test credentials use the owner-confirmed community-specific sibling `.env` and model-specific GitHub Actions secrets; P0-20 verifies behavior without exposing values.
- Final model-dependent tasks pause for the owner decision without blocking independent upstream P0 work.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-17](../tasks/p0-17-community-scope.md)
- [P0-19](../tasks/p0-19-community-workflow-triad.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None.
