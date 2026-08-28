# P0-19 — Add the `pes-squad` production-reference workflow triad

- Status: Complete — the exact leaf triad remains manual-only and schedule-free while ADR-0053's outer lane owns the active recurring schedule
- Priority: P0
- Matrix row: `pes-production-reference`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), and [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), and [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md)
- Readiness evidence: [production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) and [production activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md)

## Outcome

The `pes-production-reference` row has a reviewed, schedule-free context,
matchday, and bonus workflow triad for competition `bundesliga-2026-27`.
Posting target and community context are both `pes-squad`; match and bonus
predictions are generated independently using the exact owner-approved
`production-primary` identity and stored as the reference for compatible arena
copy-posting.

The model-independent context entrypoint was prepared from the accepted
target/context topology and credential names. The dated construction boundary
below preceded the exact P0-06 `production-primary` decision and is preserved
as historical task evidence; ADR-0052 supersedes it for the current model and
community matrix. P0-21 has since closed the ready-row secret, authentication,
readiness, POST, deadline, and manual-write gates and activated only the strict
outer matchday schedule. This leaf triad itself remains manual-only.

## Repository closeout — 2026-08-27

ADR-0052 resolved the blocked identity to `gpt-5.6-sol` / `xhigh` / cap
`10000`, match v3 and bonus v1, with Flex-first / Standard-fallback. The new
matchday and bonus callers use that exact identity and the existing context
caller, expose only `workflow_dispatch`, and contain no schedule. The Owner
confirmed the exact `PES_SQUAD_KICKTIPP_USERNAME` /
`PES_SQUAD_KICKTIPP_PASSWORD` pair is
provisioned; this is not authentication or POST evidence.

The context caller also opts into the false-by-default reusable launch-roster
path fixed by ADR-0052. Its same job downloads the exact audited artifact,
publishes the SHA/revision/date-gated P0-25 enrichment overlay, and only then
runs normal profile collection. Any overlay failure blocks the profile and all
later prediction dispatch; the live publication evidence remains P0-21.

## Repository-preparation boundary

- [x] P0-06 records an owner-approved `production-primary` model, reasoning
      effort, positive output-token cap, hosted numbered match and bonus prompt
      versions, service-tier/fallback policy, whole-season cost ceiling, and
      estimator evidence after P0-23 evidence or an explicit accepted waiver.

Historically, that then-open P0-06 item blocked only the model-bound matchday
and bonus callers, not the model-independent context caller. The following
pre-selection wording is superseded by ADR-0052: Terra and Sol were provisional
P0-23 examples rather than a selected experiment surface. It is retained only
to explain the earlier preparation boundary and does not describe current
configuration.

Do not infer any unresolved value from Luna/none validation, a historical
Bundesliga or WM26 caller, a model default, a local environment file, or an old
schedule. Do not invent a replacement secret name.

## Work items

- [x] Copy the current P0-19 template into an explicit `pes-squad` context
      entrypoint pinned to posting context `pes-squad`, competition
      `bundesliga-2026-27`, and the accepted Bundesliga context profile. This
      model-independent repository work was completed before P0-06 selection.
      Evidence: [`pes-squad-context-collection.yml`](../../../.github/workflows/pes-squad-context-collection.yml)
      exposes `workflow_dispatch` only and passes literal
      `community_context: "pes-squad"`, `competition: "bundesliga-2026-27"`,
      `trigger_type: "manual"`, and `publish_launch_roster_overlay: true`. Its
      exact four symbolic secret mappings are
      `PES_SQUAD_KICKTIPP_USERNAME`, `PES_SQUAD_KICKTIPP_PASSWORD`,
      `FIREBASE_PROJECT_ID`, and `FIREBASE_SERVICE_ACCOUNT_JSON`; the local
      workflow contract, targeted actionlint, and repository-wide actionlint
      with only unchanged shellcheck baseline codes excluded all passed without
      a dispatch or live operation.
- [x] Create the `pes-squad` matchday entrypoint with posting target
      `pes-squad`, `community_context: "pes-squad"`, the exact approved
      `production-primary` model/reasoning/cap/service policy, and the accepted
      hosted numbered match prompt with required `production` membership. Do
      not construct this caller until P0-06 records that exact identity.
- [x] Create the `pes-squad` bonus entrypoint with the same exact posting,
      context, model, cap, and service identity plus the accepted hosted numbered
      bonus prompt and explicit immutable budgets of 20 documents / 32000 tokens.
- [x] Wire only `PES_SQUAD_KICKTIPP_USERNAME` and
      `PES_SQUAD_KICKTIPP_PASSWORD` as the posting credential pair, plus the
      already accepted shared Firebase/OpenAI/Langfuse inputs required by each
      reusable workflow. Never select credentials from another context or row.
- [x] Expose `workflow_dispatch` only. Leave every final production `schedule`
      absent or commented out until P0-21 has successful manual evidence and an
      Accepted activation decision.
- [x] Update `MatchdayCommand.ProductionCommunities` and
      `BonusCommand.ProductionCommunities` only as required for the exact
      accepted `pes-squad` production entrypoints.
- [x] Remove or clearly retire the superseded Bundesliga 2025/26 `pes-squad`
      matchday and bonus callers so no similarly named old-season entrypoint can
      remain live or appear to be the current production path. At exact base
      `177bc0bf9c9fbf4e888991eebecd7bc82243d069`, both
      `pes-squad-matchday.yml` and `pes-squad-bonus.yml` expose only
      `workflow_call`, retain explicit competition `bundesliga-2025-26`, and
      pass `retired_configuration: true`. Both reusable prediction bases test
      that flag and exit with the explicit historical-retirement error before
      checkout or prediction work. The read-only deterministic workflow
      contract passed and counted these files within exactly 12 explicitly
      retired Bundesliga callers.
- [x] Add/update workflow-contract and telemetry-environment tests proving the
      exact 2026/27 competition, posting/context identity, numbered hosted
      prompts, approved model configuration, credential names, schedule absence,
      and rejection of historical/Luna inference.
- [x] Validate the three YAML files and every reusable-workflow input. For the
      separate manual callers, require the P0-21 operator to record the exact
      successful context workflow run ID and completion before manually
      dispatching either prediction workflow. Machine-enforced `needs` ordering
      now belongs only to ADR-0053's Accepted outer workflow.

## Activation boundary

- [x] P0-21 confirms Owner-provisioned Actions presence for
      `PES_SQUAD_KICKTIPP_USERNAME` and
      `PES_SQUAD_KICKTIPP_PASSWORD`, then authenticates and rechecks current
      community readiness without displaying secret values.
- [x] P0-21's Owner-authorized match and bonus runs post and pass final
      verification, establishing working posting behavior for this exact row.
- [x] P0-21 records the exact Kicktipp match and bonus deadline rule and first
      cutoff through the timestamped GET-only ready-community audit.
- [x] ADR-0053 records the Project Owner as operator, first-cycle monitor,
      on-call responder, and rollback owner, with the accepted schedule and
      response targets.
- [x] Hand the reviewed, green, manual-only triad to P0-21 without dispatching it.
- [x] P0-21 manually collects `pes-squad` context, then manually
      records the exact successful context workflow run ID and completion before
      dispatching and verifying match and required bonus predictions. Accepted
      ADR-0053's active outer workflow now supplies machine-enforced `needs`
      ordering while this leaf caller remains schedule-free.
- [x] P0-21 verifies exact Kicktipp, Firestore, hosted-prompt, model, context,
      telemetry, usage/cost, and error evidence and confirms the stored reference
      is eligible for the separately gated arena copy path.

## Validation

- Validate YAML syntax, reusable-workflow contracts, explicit secret mapping, and
  deterministic caller inventory.
- Run focused workflow-contract and telemetry tests plus every affected full
  suite.
- Confirm no schedule or non-manual trigger, 2025/26 identity, WM26 route, transfer document,
  Luna/none production inference, unresolved model slot, placeholder credential,
  or external write is introduced.

## Manual P0-21 evidence — 2026-08-27

- Context run [`33046582867`](https://github.com/ehonda/KicktippAi/actions/runs/33046582867)
  completed the pinned launch-roster overlay and normal profile collection.
- Matchday run [`33046770442`](https://github.com/ehonda/KicktippAi/actions/runs/33046770442)
  and bonus run [`33047217909`](https://github.com/ehonda/KicktippAi/actions/runs/33047217909)
  completed successfully with final verification on exact pushed head
  `e09527616aff9522d533d5e846d4543f08f9b7d8`.
- Payload-safe roster, Firestore, prompt/context/model, usage/cost, fallback, and
  error inspection passed in the aggregate P0-21 audit. This row contributes
  exactly 9 match and 5 bonus index-0 Sol/`xhigh` generations, with no index
  `1+` or errors, and its stored references were subsequently copied by both
  compatible targets without a model call. The GET-only 2026-08-27 deadline
  audit found zero minutes lead time and first cutoff 2026-08-28 20:30 CEST /
  18:30 UTC; recheck after rescheduling or a rule change. ADR-0053's outer
  ready-row schedule is active on exact `main` commit `56238e5`; this leaf
  remains schedule-free. P0-21's natural run `33143114280` later completed the
  first scheduled observation.

## Complete when

- Every repository work item has evidence, the triad is manually callable and
  schedule-free, and every model-bound production field equals the P0-06 owner
  decision. P0-21's completed first-observation evidence and continuing rollback
  boundary do not alter this leaf's repository closeout.
- The superseded 2025/26 callers cannot be mistaken for a live path.
- P0-21—not this task—owns the active outer schedule, the completed first
  scheduled-observation evidence, and any rollback decision.
