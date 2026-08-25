# P0-19 — Add the `schadensfresse` independent-production workflow triad

- Status: In progress — the model-independent manual context caller is prepared; final matchday and bonus callers wait for the P0-06 owner-selected `production-primary` identity
- Priority: P0
- Matrix row: `schadensfresse-production-independent`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), and [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), and [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)
- Readiness evidence: [production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) and [production activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md)

## Outcome

The `schadensfresse-production-independent` row has a reviewed, schedule-free
context, matchday, and bonus workflow triad for competition
`bundesliga-2026-27`. Posting target and community context are both
`schadensfresse`; every match and bonus prediction is generated independently
with the exact owner-approved `production-primary` identity.

The model-independent context entrypoint is prepared from the
accepted target/context topology and credential names. Final matchday and bonus
caller construction waits only for the exact P0-06 `production-primary`
identity. Secret presence, authentication/readiness, POST permission, Kicktipp
deadlines, live writes, and activation remain open P0-21 pre-dispatch gates;
they do not block schedule-free repository preparation. This task authorizes no
community request, prediction, workflow dispatch, schedule, or production POST.
P0-21 exclusively owns manual production evidence and activation.

## Repository-preparation boundary

- [ ] P0-06 records an owner-approved `production-primary` model, reasoning
      effort, positive output-token cap, hosted numbered match and bonus prompt
      versions, service-tier/fallback policy, whole-season cost ceiling, and
      estimator evidence after P0-23 evidence or an explicit accepted waiver.

That unchecked P0-06 item blocks only the model-bound matchday and bonus
callers. It does not block the model-independent context caller. Terra and Sol
were provisional P0-23 examples and are not the selected experiment surface.
Later owner-specified cost evidence informs the model, cost-ceiling, and
quality-budget decisions, but this clarification authorizes no exact paid
matrix, preflight, dataset mutation, or model call.

The `schadensfresse` setup request is external and pending with its community
administrator. The agent is neither authorized nor expected to administer the
community. Its completion, current-season readiness, and POST permission are
P0-21 pre-dispatch gates, not repository-preparation prerequisites.

Do not infer any unresolved value from Luna/none validation, a historical
Bundesliga or WM26 caller, a model default, a local environment file, or an old
schedule. Do not invent a replacement secret name, bypass the exact-nine input
gate, or treat successful authentication as season readiness.

## Work items

- [x] Copy the current P0-19 template into an explicit `schadensfresse` context
      entrypoint pinned to posting context `schadensfresse`, competition
      `bundesliga-2026-27`, and the accepted Bundesliga context profile. This
      model-independent repository work was completed before P0-06 selection and
      before the external community administrator completes setup.
      Evidence: [`schadensfresse-context-collection.yml`](../../../.github/workflows/schadensfresse-context-collection.yml)
      exposes `workflow_dispatch` only and passes literal
      `community_context: "schadensfresse"`,
      `competition: "bundesliga-2026-27"`, and `trigger_type: "manual"`. Its
      exact four symbolic mappings are
      `SCHADENSFRESSE_KICKTIPP_USERNAME`,
      `SCHADENSFRESSE_KICKTIPP_PASSWORD`, `FIREBASE_PROJECT_ID`, and
      `FIREBASE_SERVICE_ACCOUNT_JSON`; the local workflow contract and
      targeted actionlint and repository-wide actionlint with only unchanged
      shellcheck baseline codes excluded all passed without a dispatch or live
      operation.
- [ ] Create the `schadensfresse` matchday entrypoint with posting target
      `schadensfresse`, `community_context: "schadensfresse"`, the exact approved
      `production-primary` model/reasoning/cap/service policy, and the accepted
      hosted numbered match prompt with required `production` membership. Do
      not construct this caller until P0-06 records that exact identity.
- [ ] Create the `schadensfresse` bonus entrypoint with the same exact posting,
      context, model, cap, and service identity plus the accepted hosted numbered
      bonus prompt and explicit immutable budgets of 20 documents / 32000 tokens.
- [ ] Wire only `SCHADENSFRESSE_KICKTIPP_USERNAME` and
      `SCHADENSFRESSE_KICKTIPP_PASSWORD` as the posting credential pair, plus the
      already accepted shared Firebase/OpenAI/Langfuse inputs required by each
      reusable workflow. Never select credentials from another context or row.
- [ ] Expose `workflow_dispatch` only. Leave every final production `schedule`
      absent or commented out until P0-21 has successful manual evidence and an
      Accepted activation decision.
- [ ] Update `MatchdayCommand.ProductionCommunities` and
      `BonusCommand.ProductionCommunities` only as required for the exact
      accepted `schadensfresse` production entrypoints.
- [x] Remove or clearly retire the superseded Bundesliga 2025/26
      `schadensfresse` matchday and bonus callers so no similarly named old-season
      entrypoint can remain live or appear to be the current production path. At
      exact base `177bc0bf9c9fbf4e888991eebecd7bc82243d069`, both
      `schadensfresse-matchday.yml` and `schadensfresse-bonus.yml` expose only
      `workflow_call`, retain explicit competition `bundesliga-2025-26`, and
      pass `retired_configuration: true`. Both reusable prediction bases test
      that flag and exit with the explicit historical-retirement error before
      checkout or prediction work. The read-only deterministic workflow
      contract passed and counted these files within exactly 12 explicitly
      retired Bundesliga callers.
- [ ] Add/update workflow-contract and telemetry-environment tests proving the
      exact 2026/27 competition, posting/context identity, numbered hosted
      prompts, approved model configuration, credential names, schedule absence,
      and rejection of historical/Luna inference.
- [ ] Validate the three YAML files and every reusable-workflow input. For the
      separate manual callers, require the P0-21 operator to record the exact
      successful context workflow run ID and completion before manually
      dispatching either prediction workflow. Machine-enforced `needs` ordering
      belongs only to a later Accepted outer workflow.

## Activation boundary

- [ ] The external `schadensfresse` community administrator completes the
      pending Bundesliga 2026/27 setup request. P0-21 then re-runs the supported
      read-only profile and requires exactly nine current prediction inputs;
      the agent does not administer the community.
- [ ] P0-21 confirms names-only Actions presence for
      `SCHADENSFRESSE_KICKTIPP_USERNAME` and
      `SCHADENSFRESSE_KICKTIPP_PASSWORD`, then authenticates without displaying
      secret values.
- [ ] P0-21 obtains community-administrator confirmation of Bundesliga 2026/27
      POST permission. The audit made no POST request, and this task authorizes
      none.
- [ ] P0-21 records the exact Kicktipp match and bonus deadlines, operator,
      monitor/on-call, schedule proposal, and rollback authority.
- [ ] Hand the reviewed, green, manual-only triad to P0-21 without dispatching it.
- [ ] P0-21 manually collects and inspects `schadensfresse` context, then
      records the exact successful context workflow run ID and completion before
      dispatching and verifying match and required bonus predictions. A later
      Accepted outer workflow must use machine-enforced `needs` ordering before
      any schedule is enabled.
- [ ] P0-21 verifies exact Kicktipp, Firestore, hosted-prompt, model, context,
      telemetry, usage/cost, and error evidence for the independent-generation
      path.

## Validation

- Validate YAML syntax, reusable-workflow contracts, explicit secret mapping, and
  deterministic caller inventory.
- Run focused workflow-contract and telemetry tests plus every affected full
  suite.
- Confirm no schedule or non-manual trigger, 2025/26 identity, WM26 route, transfer document,
  Luna/none production inference, unresolved model slot, placeholder credential,
  or external write is introduced.

## Complete when

- Every repository work item has evidence, the triad is manually callable and
  schedule-free, and every model-bound production field equals the P0-06 owner
  decision. External community remediation and the other open P0-21
  pre-dispatch gates do not block repository closeout.
- The superseded 2025/26 callers cannot be mistaken for a live path.
- P0-21—not this task—owns the first production dispatch, opening writes,
  schedule activation, first scheduled observation, and rollback decision.
