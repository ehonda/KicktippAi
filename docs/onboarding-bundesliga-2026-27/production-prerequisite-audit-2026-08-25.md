# Bundesliga 2026/27 production prerequisite audit — 2026-08-25

This read-only audit records which production prerequisites can be verified before the owner selects the final model and arena participants. It is prerequisite evidence for [P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md), not production validation or authorization to post predictions.

## Final closeout addendum — 2026-08-28

P0-21 is complete. The later authenticated manual ladders superseded this
audit's unresolved readiness and posting-right rows, and natural GitHub Actions
`schedule` run
[`33143114280`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280)
then succeeded on exact `main` head
`50f3ed148891977b5909659f9986c9c9958d7875`. All 16 jobs ran in strict
context→match order. Context retained the accepted snapshots; each match row
verified 9/9 current predictions, then skipped generation/posting and final
verification. The run made no write, generation, reprediction, token use, or
cost and had no runtime WM26, Bundesliga 2025/26, transfer-context, error, or
overlap evidence.

GitHub delivered the nominal 02:07 UTC event 2h46m22s late, beyond the
90-minute monitoring envelope, but the 38m46s run completed before the next
daily occurrence. [P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md)
contains the authoritative per-job and payload-safe evidence. The dated
read-only rows below remain historical, not current blockers. P1 is next;
P1-08 remains open for `schadensfresse` mixed-competition routing, and Club Elo
network refresh plus exploratory model follow-ups remain non-P0.

## Schadensfresse current-readiness addendum — 2026-08-27

The earlier NOT READY rows below remain historical evidence. After the
administrator completed setup, authenticated read-only preflight found the
2026/27 marker and exactly the same nine opening fixtures as `pes-squad`.
Firestore hygiene before context is expected 401 / present 0 / missing 401 /
unexpected 0 / conflicts 0; roster and Club Elo heads are absent.

Eight bonus questions are open: five audited Bundesliga questions due
`2026-08-28T18:30:00Z`, and three CL questions due
`2026-09-09T10:00:00Z`. The Bundesliga option IDs/text/limits match
`pes-squad`; five exact `1.BL: ` target texts require ADR-0054's scoped alias
projection. Current live rules are hidden/exact/zero-minute, Bundesliga after
90 minutes, DFB/CL after penalties, matchday-win tie break, 2/3/4 (win) and
2/-/4 (draw), and four points per correct bonus answer.

This replaces the old independent/external-pending next step: run target-owned
context first, then copied match, then copied bonus with the inclusive
2026-08-28 ceiling. P1-08 owns the excluded CL questions and later DFB/CL
primary routing.

## Owner-selection and provisioning addendum — 2026-08-27

[ADR-0052](../../plans/bundesliga-2026-27/decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
supersedes this audit's unresolved model/participant statements. The Owner
selected Sol/`xhigh` production, four exact arena challengers, and the added
`relaxdays-tippt` production copy. All corresponding repository callers are
prepared as manual-only entrypoints; no schedule or dispatch was added.
Accepted ADR-0053 later schedules only the strict outer ready-row matchday lane;
the leaf callers remain manual-only.

The Owner also confirmed every canonical Actions Kicktipp username/password
pair provisioned:

- `PES_SQUAD_KICKTIPP_USERNAME` / `PES_SQUAD_KICKTIPP_PASSWORD`;
- `SCHADENSFRESSE_KICKTIPP_USERNAME` / `SCHADENSFRESSE_KICKTIPP_PASSWORD`;
- `RELAXDAYS_TIPPT_KICKTIPP_USERNAME` / `RELAXDAYS_TIPPT_KICKTIPP_PASSWORD`;
- `EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_PASSWORD`;
- `EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH_KICKTIPP_PASSWORD`;
- `EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP_PASSWORD`;
- `EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP_PASSWORD`; and
- the existing `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` pair.

The [community ledger](community-onboarding.md) records the same exact names.
This Owner confirmation replaced the generic secret-presence uncertainty. The
later exact workflow evidence below establishes successful use for the rows
already exercised; it still does not enumerate secret metadata or expose a
value.

The later production-live lane is independently approved, pushed,
and integrated at exact commit
`992af5a63c788c0cc066dce92dd1319a91e5083d`. Its workflow contract and
actionlint passed with only pre-existing actionlint warnings; Release build had
zero errors; Orchestrator passed `1142/1142`; and exact-head GitHub run
[`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
succeeded including Pages. The integrated writer/reviewer worktrees were
cleaned. The outer caller has no bonus or `schadensfresse` path. Exact
activation commit `56238e5` is on `main`, exact-head CI run `33100581641` is
green, and ADR-0053's sole cron is active; first scheduled runtime observation
was still open at that checkpoint.

Pre-closeout runtime conclusions at that checkpoint were:

| Posting target/participant | Then-current evidence | Then-remaining P0-21 gate |
| --- | --- | --- |
| `pes-squad` | Context `33046582867`, Sol/`xhigh` match `33046770442`, and bonus `33047217909` succeeded with final verification and payload-safe audit | First scheduled observation under ADR-0053 |
| `schadensfresse` | Exact secrets Owner-confirmed present; authenticated HTTP-200 GET audit at 2026-08-27 11:41 CEST found no 2026/27 marker, 0 open matches, 0 open bonus questions, and only closed historical rows | NOT READY: external season setup, current marker/exact-nine/open-bonus/current-deadline gates, POST permission, context, independent match/bonus, and payload-safe evidence |
| `relaxdays-tippt` | Context retry `33049949393`, Sol/`xhigh` match copy `33050188533`, and bonus copy `33050549422` succeeded after exact rules-source repair `eedf330`; payload audit proves zero generation/fallback | First scheduled observation under ADR-0053 |
| Arena Sol/`xhigh` copy | Context `33050848544`, match copy `33051066657`, and bonus copy `33051557046` succeeded; payload audit proves zero generation/fallback and shared-roster preservation | First scheduled observation under ADR-0053 |
| Arena Sol/`high` | Context `33051863137`, match `33052087407`, and bonus `33052537217` succeeded with payload-safe audit | First scheduled observation under ADR-0053 |
| Arena Luna/`medium` | Context `33052882246`, match `33053095243`, and bonus `33053423396` succeeded with payload-safe audit | First scheduled observation under ADR-0053 |
| Arena Terra/`xhigh` | Context `33053664914`, match `33053888656`, and bonus `33054314209` succeeded with payload-safe audit | First scheduled observation under ADR-0053 |
| Arena Luna/`none` | Context `33054637395` and match `33054826152` succeeded; bonus `33055144574` failed closed with zero side effects, then Owner-approved forced index-0 recovery `33089097055` regenerated/saved the same five IDs, posted all five selections, passed final 5/5 verification, and passed payload-safe audit | First scheduled observation under ADR-0053; do not repeat recovery |

The completed-row audit found four real generated configurations through
Terra/`xhigh`, comprising eight match/bonus trace families and 56 successful
generations (`36` match / `20` bonus) at `$0.5683818`. Nine Luna/`none` match
generations at `$0.0039741` bring the pre-recovery total to 65 / `$0.5723559`.
All are exact
index `0` with no index `1+` or errors, exact selected
model/reasoning/cap-`10000`, hosted match v3 or bonus v1, roster snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`;
no WM26, Bundesliga 2025/26, or transfer-document names appear. Terra used two
successful Standard fallbacks among 14 calls; all others used Flex. Each
compatible copy path copied 9/9 matches and 5/5 bonus answers with zero
generation or independent fallback. Failed Luna bonus trace
`0cf1515e96813b42b4625f61d5350d73` has one root span and zero
generation/usage/cost; its pre/post inventory is byte-identical at SHA-256
`02ce5533a1fbaec39555f7b4f55fe399d541ee6b17fa9612383a4b26ac86f4d0`.

The Owner-approved recovery run
[`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055)
used exact head `89b875125fdae207b6f6f72cff8f968a718b112f`,
`force_prediction=true`, `max_repredictions=0`, and the exact Luna/`none` /
cap-`10000` / hosted bonus-v1 identity. Initial verification expectedly found
0/5 current-provenance rows. The run saved five predictions, posted all five
Kicktipp selections together, and passed final 5/5 verification. Inventory
SHA-256 changed from
`0ab5df24cc2ac909e7b0f230427de28245334a40dab90a08402550c1a5ac2be2` to
`9f824612b8d4e98c2fb314708ef886597904b607cc704c4a3d940c4521601c94`:
the same five document IDs remain at index `0`, none exists at index `1+`, and
timestamps refreshed into `2026-08-27T15:42:47Z`–`15:43:33Z`. Resolved
manifests remained 5/5 while compatibility manifests advanced from 0/5 to 5/5;
three selection hashes stayed unchanged and two changed.

Recovery trace `0510f8a12d3d95c5923c89abff118ded` started at
`2026-08-27T15:42:33.502Z`, took `60.904s`, and contains one root plus five
clean `predict-bonus` generations, with no error/warning/status message. All
five are independent, repredict mode false, index `0`, exact Luna/`none` /
cap-`10000` / production bonus-v1 hash
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`,
and Flex-to-Flex without fallback. They consumed `43,249` input and `98` output
tokens (`43,347` total), zero cache-read/reasoning tokens, for `$0.0043837`:
`$0.0043249` input plus `$0.0000588` output. Context contains only Club Elo,
team-squad summary, and 18 roster documents with the exact accepted snapshots;
no WM26, Bundesliga 2025/26, experiment, or transfer name appears. P0-21's
successful total is therefore 70 generations / `$0.5767396`, and the
Luna/`none` manual triad and recovery gate are complete.

The ready-community deadline uncertainty is also closed for this launch
checkpoint. An authenticated GET-only audit at 2026-08-27 10:19:52 CEST
(08:19:52 UTC) found identical zero-minute lead-time rules in `pes-squad`,
`relaxdays-tippt`, and `ehonda-ai-arena`; all nine match controls and all five
bonus questions / seven selection controls were open. Their first common cutoff
is 2026-08-28 20:30 CEST / 18:30 UTC. The arena result is community-scoped and
was read with the Luna/`none` profile. Recheck after any fixture rescheduling or
administrator rule change. `schadensfresse` still needs its own audit after
season setup.

The latest `schadensfresse` audit occurred at 2026-08-27 11:41 CEST
(09:41 UTC). Authentication succeeded and its GET returned HTTP 200, but the
page contained no Bundesliga 2026/27 marker and zero open match controls; the
only match was closed 30 May PSG–Arsenal. Bonus exposed zero open questions and
eight closed 2025/26 rows. `/spielregeln` still identified 2025/26 and the
historical zero-minute lead rule. Therefore the community is NOT READY. The
zero-minute value is not a current-season deadline, and the row remains absent
from every schedule.

After external setup, repeat the same authenticated GET-only audit. Require the
Bundesliga 2026/27 marker, exactly nine open match controls, current open bonus
questions/options, and current rules/deadlines. Then and only then run the
pinned-overlay context workflow; inspect its publication; run independent match
then bonus validation; and inspect Kicktipp, Firestore, hosted prompt, model,
context, usage/cost, service-tier, and error evidence. Schedule eligibility is
a later Accepted decision after that entire ladder is green.

The dated sections below remain the read-only evidence as observed on
2026-08-25/26. Where they call the model, participants, names, callers, or later
runtime state unresolved, this addendum is authoritative.

## Result

| Posting target | Credential authentication | Bundesliga 2026/27 read readiness | Posting rights | Repository/live boundary |
|---|---|---|---|---|
| `pes-squad` | Passed with the sibling `.env.pes-squad` profile | Passed: the current matchday exposed 9 upcoming fixtures, the current 18-team standings, and the expected 47 Kicktipp context documents | Unknown; the audit used read-only requests only | The model-independent `pes-squad-context-collection.yml` caller is prepared as a manual-only `workflow_dispatch` entrypoint; final match/bonus callers wait only for P0-06's exact `production-primary`. Secret presence, reauthentication, POST permission, deadlines, dispatch, and live evidence remain P0-21 pre-dispatch gates. |
| `schadensfresse` | Passed with the sibling `.env.schadensfresse` profile | Failed: the results view reported 9 completed and 0 pending matches, while the prediction-input view exposed 0 current input rows; the Bundesliga profile rejected 0 instead of exactly 9 current matches | Unknown; the audit used read-only requests only | The model-independent `schadensfresse-context-collection.yml` caller is prepared as a manual-only `workflow_dispatch` entrypoint; final match/bonus callers wait only for P0-06's exact `production-primary`. The setup request is external/pending with the community administrator; remediation, secret presence, reauthentication, POST permission, deadlines, dispatch, and live evidence remain P0-21 pre-dispatch gates. |
| `ehonda-ai-arena` production copy | Not testable: no accepted production participant or credential profile exists | Not tested | Unknown | Repository preparation waits for P0-06's shared `production-primary` plus owner selection of the arena participant/profile/exact credential names. Secret presence, authentication/readiness, POST permission, deadlines, and live evidence remain P0-21 gates. |
| `ehonda-ai-arena` challengers | Not testable: zero challengers are admitted | Not tested | Unknown | Owner selection of each challenger, participant, and exact model-specific credential names |

## Read-only readiness refresh — 2026-08-26

Both production-community profile checks were repeated with
`OTEL_SDK_DISABLED=true` set before process startup. The refresh was dry-run
only: it made no Firestore or Kicktipp write, constructed no Langfuse telemetry
path, exposed no submission deadline, and did not test or prove POST permission.

| Posting target | Exit and authentication | Exact read-only profile evidence | Current conclusion |
|---|---|---|---|
| `pes-squad` | Exit `0`; sibling-profile authentication and GET access passed | Matchday 1 exposed exactly 9 current prediction inputs, 0 completed and 9 pending matches; standings contained 18 teams; Kicktipp collection selected 47 current context documents and 288 history rows; Club Elo selected 18 `LaunchSeed` rows with `NetworkDisabled`; rosters selected the 18-club fallback path | Read readiness is reconfirmed only. Actions secret presence, POST permission, exact deadlines, production roster publication, prediction dispatch, and activation remain open. |
| `schadensfresse` | Exit `1`; sibling-profile authentication and GET access passed | Matchday 1 exposed 9 completed and 0 pending results but 0 current prediction-input rows. The profile stopped with the exact gate failure `The bundesliga-2026-27 profile expected exactly 9 matches for current matchday, but found 0.`; all later profile stages were skipped. | Current-season readiness still fails and the external community-administrator remediation remains required. Authentication does not establish readiness or posting rights. |

The refresh therefore confirms the same boundary as the original audit:
`pes-squad` can proceed to later P0-21 permission and live gates after its
model-bound workflows exist, while `schadensfresse` cannot be dispatched until
the external setup is corrected and the exact-nine read gate passes. It records
no model selection, participant admission, production authorization, or
schedule decision.

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

The names-only check was repeated on 2026-08-26 at exact main
`94224b23ad35e16f5fd6c4b68a70815d3b9b3e3f`. GitHub CLI authenticated as
`ehonda`, but both commands again exited `1` with HTTP 403,
`Resource not accessible by personal access token`. This reconfirms only that
the current token cannot enumerate the metadata; it neither verifies nor
disproves presence of the `PES_SQUAD_*` or `SCHADENSFRESSE_*` names.

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

The 2026-08-26 refresh followed that rule for both communities: the process
received `OTEL_SDK_DISABLED=true` before startup, so neither refresh used a
Langfuse path. This is telemetry-boundary evidence only and does not weaken any
production trace-inspection requirement.

## Historical workflow-readiness snapshot at audit time

The statements in this section preserve the 2026-08-25 audit-time state. The
2026-08-27 addendum above supersedes them for current workflow and activation
status.

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
