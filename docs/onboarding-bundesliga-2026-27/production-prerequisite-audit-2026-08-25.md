# Bundesliga 2026/27 production prerequisite audit — 2026-08-25

This read-only audit records which production prerequisites can be verified before the owner selects the final model and arena participants. It is prerequisite evidence for [P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md), not production validation or authorization to post predictions.

## Result

| Posting target | Credential authentication | Bundesliga 2026/27 read readiness | Posting rights | Remaining gate |
|---|---|---|---|---|
| `pes-squad` | Passed with the sibling `.env.pes-squad` profile | Passed: the current matchday exposed 9 upcoming fixtures, the current 18-team standings, and the expected 47 Kicktipp context documents | Unknown; the audit used read-only requests only | Confirm posting permission, repository secret presence, and the owner-approved `production-primary` configuration |
| `schadensfresse` | Passed with the sibling `.env.schadensfresse` profile | Failed: the results view reported 9 completed and 0 pending matches, while the prediction-input view exposed 0 current input rows; the Bundesliga profile rejected 0 instead of exactly 9 current matches | Unknown; the audit used read-only requests only | A community administrator must configure and verify Bundesliga 2026/27 membership and posting permission before workflow implementation or production validation |
| `ehonda-ai-arena` production copy | Not testable: no accepted production participant or credential profile exists | Not tested | Unknown | Owner selection of the shared `production-primary` identity, the arena participant, and exact model-specific credential names |
| `ehonda-ai-arena` challengers | Not testable: zero challengers are admitted | Not tested | Unknown | Owner selection of each challenger, participant, and exact model-specific credential names |

The existing `.env.ehonda-ai-arena` profile and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` Actions names remain exclusive to the Luna plumbing participant. They do not authorize a production-copy participant or challenger. This preserves the credential boundary in [ADR-0039](../../plans/bundesliga-2026-27/decisions/0039-record-bundesliga-community-and-credential-topology.md) and the community matrix in [community onboarding](community-onboarding.md).

## Credential-name inventory

Names-only inspection confirmed that the sibling secrets checkout contains `.env.pes-squad` and `.env.schadensfresse`, each with the required `KICKTIPP_USERNAME` and `KICKTIPP_PASSWORD` keys. No credential value was displayed or recorded.

The accepted production Actions names remain:

- `PES_SQUAD_KICKTIPP_USERNAME` / `PES_SQUAD_KICKTIPP_PASSWORD`
- `SCHADENSFRESSE_KICKTIPP_USERNAME` / `SCHADENSFRESSE_KICKTIPP_PASSWORD`

Actual GitHub repository secret and variable presence remains unknown. Both authenticated, names-only metadata commands returned HTTP 403, `Resource not accessible by personal access token`:

```text
gh secret list --app actions --repo ehonda/KicktippAi --json name,updatedAt
gh variable list --repo ehonda/KicktippAi --json name,updatedAt
```

The 403 is a repository-metadata permission gap, not evidence that a named secret is absent. A repository administrator must perform the names-only inventory without viewing values. Arena production-copy and challenger names must not be invented during that inventory because their participants remain owner-gated.

## Read-only community checks

The supported local entrypoint is the competition-profile command because it loads `.env.<community-context>` before delegating to the Kicktipp collector:

```text
dotnet run --project src/Orchestrator --configuration Release -- collect-context profile --community-context <community> --competition bundesliga-2026-27 --dry-run
```

Direct `collect-context kicktipp` does not load a community-specific sibling credential profile. The supported behavior is implemented by [CollectContextProfileCommand](../../src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileCommand.cs), while the profile's exact 9-match rejection is implemented by [CollectContextKicktippCommand](../../src/Orchestrator/Commands/Operations/CollectContext/CollectContextKicktippCommand.cs).

The dry-run path read Kicktipp and existing Firestore state but did not persist either service. Match-outcome upserts are guarded by `!dryRun` in [MatchOutcomeCollectionService](../../src/Orchestrator/Services/MatchOutcomeCollectionService.cs), and context publication returns or skips before every save in `CollectContextKicktippCommand`.

Authentication and successful GET requests establish credential validity and read reachability, and—only for `pes-squad`—the current competition identity. They do not establish community membership beyond that observed access or permission to submit predictions. P0-21 must retain its explicit manual posting and verification gate.

## Telemetry boundary observed during the audit

The `pes-squad` profile dry run ran at approximately 2026-08-25 14:53 CEST. Although document writes were disabled, the normal process telemetry configuration sent one OTLP batch to Langfuse. That batch may contain up to two independent root traces, named `collect-context-club-elo` and `collect-context-rosters`. No trace identifiers or payloads were printed, queried, changed, or recorded.

Before the `schadensfresse` run at approximately 2026-08-25 14:57 CEST, the process-only `OTEL_SDK_DISABLED=true` guard was applied. OpenTelemetry .NET 1.17 reads this flag from environment configuration and returns a no-op tracer provider before constructing the configured tracer provider and exporter. No OTLP exporter request occurred in that run. The repository's exporter setup is in [ServiceRegistrationExtensions](../../src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs); the pinned dependency version is in [Directory.Packages.props](../../Directory.Packages.props), and the upstream guard is visible in [`TracerProviderBuilderBase`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.17.0/src/OpenTelemetry/Trace/Builder/TracerProviderBuilderBase.cs).

This telemetry observation does not change any Langfuse prompt, trace, or production gate. Future read-only profile audits should hard-disable the telemetry SDK before process startup when trace ingestion is outside their authorized scope.

## Workflow readiness and next gates

The only current Bundesliga 2026/27 workflow triad is the manual arena Luna validation triad. The existing `pes-squad` and `schadensfresse` context callers are reusable `workflow_call` entrypoints, while their matchday and bonus callers remain explicitly retired Bundesliga 2025/26 configurations. Historical arena callers are likewise not production defaults.

Production workflow copies remain blocked by [P0-06](../../plans/bundesliga-2026-27/tasks/p0-06-model-ledger-and-cost-baseline.md) and the owner decision after [P0-23](../../plans/bundesliga-2026-27/tasks/p0-23-gpt-5-6-production-candidate-evidence.md), plus these community-specific gates:

1. A repository administrator confirms the required Actions secret and variable names without exposing values.
2. `pes-squad` posting permission is confirmed.
3. A `schadensfresse` administrator fixes and verifies Bundesliga 2026/27 competition readiness and posting permission.
4. The owner selects the exact `production-primary` model configuration and arena production participant, and admits zero or more exact challengers.
5. Each deployable row receives its explicit, schedule-free [P0-19](../../plans/bundesliga-2026-27/tasks/p0-19-community-workflow-triad.md) workflow triad.
6. P0-21 performs the first production writes, validates reference-copy behavior, and alone decides whether to enable final schedules.

No production workflow placeholder, participant identity, credential name, posting-right claim, or schedule activation is established by this audit.
