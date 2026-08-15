# ADR-0004: Use hosted prompts with local fallback

- Status: Accepted
- Date: 2026-08-16

## Context

WM26 successfully used Langfuse-hosted prompts as the primary runtime route and checked-in prompt files only when Langfuse was unavailable or the first fetch failed. Repository-level guidance still described hosted prompts as an opt-in proof of concept, which no longer matches operating practice.

## Decision

Bundesliga 2026/27 uses these Langfuse-hosted prompts as the primary route:

- `kicktippai/bundesliga-2026-27/predict-one-match`
- `kicktippai/bundesliga-2026-27/predict-bonus`

Candidate validation uses `latest` or a dedicated staging label. Scheduled production uses the deliberately promoted `production` label. Checked-in local mirrors contain the promoted production content and are used only as an outage or first-fetch fallback.

Agents may create candidate versions, assign development labels, and synchronize local mirrors. The project owner approves the exact production model/prompt configuration; an agent then promotes and records the exact prompt versions. Traces must expose prompt source, name, label, version, and fallback status.

## Alternatives considered

- **Keep local files primary:** Rejected because it would regress from the proven WM26 hosted-prompt operation.
- **Use `latest` in production:** Rejected because creating an unvalidated prompt version would change production immediately.
- **Remove local prompts:** Rejected because the checked-in mirror is the availability fallback.

## Consequences

- Prompt changes can be validated and promoted without an application deployment.
- Local mirrors require a synchronization check whenever `production` moves.
- Repository agent guidance and current onboarding documentation must stop calling hosted prompts merely a POC.

## Affected tasks

- [P0-05](../tasks/p0-05-prompt-route.md)
- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-17](../tasks/p0-17-community-scope.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

The repository guidance that limits hosted prompts to opt-in POC and experiment use.
