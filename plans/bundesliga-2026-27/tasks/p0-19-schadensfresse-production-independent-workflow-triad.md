# P0-19 — Add the `schadensfresse` independent-production workflow triad

- Status: Blocked — not started; waiting for community-admin remediation and the P0-06 owner-selected `production-primary` identity
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

This task constructs schedule-free entrypoints only after the model, community,
and credential prerequisites in `Current blocked state` pass. The separate
P0-21 activation gates remain open after construction. This task authorizes no
community request, prediction, workflow dispatch, schedule, or production write.
P0-21 exclusively owns manual production evidence and activation.

## Current blocked state

- [ ] A `schadensfresse` community administrator fixes and verifies Bundesliga
      2026/27 setup. Authentication passed in the read-only audit, but the
      community exposed nine completed and zero pending results plus zero current
      prediction-input rows; the current-season profile therefore failed its
      exact-nine-fixture readiness gate.
- [ ] `schadensfresse` Bundesliga 2026/27 POST permission is unknown. The
      community administrator must confirm it; the audit made no POST request.
- [ ] P0-06 records an owner-approved `production-primary` model, reasoning
      effort, positive output-token cap, hosted numbered match and bonus prompt
      versions, service-tier/fallback policy, whole-season cost ceiling, and
      estimator evidence after P0-23 evidence or an explicit accepted waiver.
- [ ] A repository administrator confirms names-only Actions presence for
      `SCHADENSFRESSE_KICKTIPP_USERNAME` and
      `SCHADENSFRESSE_KICKTIPP_PASSWORD` without displaying values. Actions
      presence is unknown; accepted names are not evidence that repository
      secrets exist.

Do not infer any unresolved value from Luna/none validation, a historical
Bundesliga or WM26 caller, a model default, a local environment file, or an old
schedule. Do not invent a replacement secret name, bypass the exact-nine input
gate, or treat successful authentication as season readiness.

## Work items after the gates pass

- [ ] Re-run the supported current-season read-only profile after administrator
      remediation and require exactly nine current Bundesliga 2026/27 prediction
      inputs before workflow implementation proceeds.
- [ ] Copy the current P0-19 template into an explicit `schadensfresse` context
      entrypoint pinned to posting context `schadensfresse`, competition
      `bundesliga-2026-27`, and the accepted Bundesliga context profile.
- [ ] Create the `schadensfresse` matchday entrypoint with posting target
      `schadensfresse`, `community_context: "schadensfresse"`, the exact approved
      `production-primary` model/reasoning/cap/service policy, and the accepted
      hosted numbered match prompt with required `production` membership.
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
- [ ] Remove or clearly retire the superseded Bundesliga 2025/26
      `schadensfresse` matchday and bonus callers so no similarly named old-season
      entrypoint can remain live or appear to be the current production path.
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

- Validate YAML syntax, workflow-call contracts, explicit secret mapping, and
  deterministic caller inventory.
- Run focused workflow-contract and telemetry tests plus every affected full
  suite.
- Confirm no active trigger, 2025/26 identity, WM26 route, transfer document,
  Luna/none production inference, unresolved model slot, placeholder credential,
  or external write is introduced.

## Complete when

- Community-admin remediation and every other gate and work item have evidence;
  the triad is manually callable and schedule-free; every production field
  equals the P0-06 owner decision.
- The superseded 2025/26 callers cannot be mistaken for a live path.
- P0-21—not this task—owns the first production dispatch, opening writes,
  schedule activation, first scheduled observation, and rollback decision.
