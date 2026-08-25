# Bundesliga 2026/27 production prerequisite audit — 2026-08-25

This read-only audit records which production prerequisites can be verified before the owner selects the final model and arena participants. It is prerequisite evidence for [P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md), not production validation or authorization to post predictions.

## Result

| Posting target | Credential authentication | Bundesliga 2026/27 read readiness | Posting rights | Repository/live boundary |
|---|---|---|---|---|
| `pes-squad` | Passed with the sibling `.env.pes-squad` profile | Passed: the current matchday exposed 9 upcoming fixtures, the current 18-team standings, and the expected 47 Kicktipp context documents | Unknown; the audit used read-only requests only | The model-independent `pes-squad-context-collection.yml` caller is prepared as a manual-only `workflow_dispatch` entrypoint; final match/bonus callers wait only for P0-06's exact `production-primary`. Secret presence, reauthentication, POST permission, deadlines, dispatch, and live evidence remain P0-21 pre-dispatch gates. |
| `schadensfresse` | Passed with the sibling `.env.schadensfresse` profile | Failed: the results view reported 9 completed and 0 pending matches, while the prediction-input view exposed 0 current input rows; the Bundesliga profile rejected 0 instead of exactly 9 current matches | Unknown; the audit used read-only requests only | The model-independent `schadensfresse-context-collection.yml` caller is prepared as a manual-only `workflow_dispatch` entrypoint; final match/bonus callers wait only for P0-06's exact `production-primary`. The setup request is external/pending with the community administrator; remediation, secret presence, reauthentication, POST permission, deadlines, dispatch, and live evidence remain P0-21 pre-dispatch gates. |
| `ehonda-ai-arena` production copy | Not testable: no accepted production participant or credential profile exists | Not tested | Unknown | Repository preparation waits for P0-06's shared `production-primary` plus owner selection of the arena participant/profile/exact credential names. Secret presence, authentication/readiness, POST permission, deadlines, and live evidence remain P0-21 gates. |
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

For `pes-squad` and `schadensfresse`, this names-only Actions check is a P0-21
pre-dispatch gate and did not block preparing their schedule-free manual context
entrypoints with the already accepted credential names. For arena production
copy, the owner must first select the participant/profile/exact names before
repository preparation; actual presence and live use still remain P0-21 gates.

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

The current Bundesliga 2026/27 entrypoints are the manual arena Luna validation
triad plus `pes-squad-context-collection.yml` and
`schadensfresse-context-collection.yml`. Both production context callers expose
`workflow_dispatch` only, with no inputs or schedule; they call the reusable
context workflow with their literal community context, competition
`bundesliga-2026-27`, trigger type `manual`, and the accepted four symbolic
Kicktipp/Firebase secret mappings. Their matchday and bonus callers remain
explicitly retired Bundesliga 2025/26 configurations. Historical arena callers
are likewise not production defaults.

Repository preparation and live authorization have separate gates:

1. The model-independent `pes-squad` and `schadensfresse` context callers are
   prepared and locally contract/actionlint validated from the accepted topology
   and credential names; neither has been dispatched.
2. Their final matchday and bonus callers wait only for [P0-06](../../plans/bundesliga-2026-27/tasks/p0-06-model-ledger-and-cost-baseline.md)
   to record the exact owner-selected `production-primary` configuration.
3. Arena production-copy repository preparation additionally waits for the owner
   to select its participant/profile/exact credential names and for the reviewed
   `pes-production-reference` callers it must mirror.
4. Secret presence, authentication/current-community readiness, POST permission,
   exact Kicktipp deadlines, live writes, and schedule activation remain open
   P0-21 pre-dispatch gates. They do not block schedule-free P0-19 construction.

The `schadensfresse` setup request is external and pending with the community
administrator. The agent is not authorized or expected to administer that
community; P0-21 consumes the administrator's result before live dispatch.

[P0-23](../../plans/bundesliga-2026-27/tasks/p0-23-gpt-5-6-production-candidate-evidence.md)
retains the reusable Luna cost row, but its earlier Terra/`medium`, Sol/`medium`,
cap-`10000`, and `15 × 20` surface is a superseded provisional example and was
not selected. The owner will supply a detailed experiment surface and phase
budget after autonomous preparation. No exact paid matrix or spend is authorized
by this boundary clarification.

After schedule-free repository construction, P0-21 performs the first production
writes, validates reference-copy behavior, and alone decides whether to enable
final schedules.

No production workflow placeholder, participant identity, credential name, posting-right claim, or schedule activation is established by this audit.
