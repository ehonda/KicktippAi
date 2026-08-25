# P0-19 — Add the `pes-squad` production-reference workflow triad

- Status: Blocked — not started; waiting for the P0-06 owner-selected `production-primary` identity and the readiness gates below
- Priority: P0
- Matrix row: `pes-production-reference`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), and [P0-18](p0-18-base-workflow-support.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), and [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)
- Readiness evidence: [production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) and [production activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md)

## Outcome

The `pes-production-reference` row has a reviewed, schedule-free context,
matchday, and bonus workflow triad for competition `bundesliga-2026-27`.
Posting target and community context are both `pes-squad`; match and bonus
predictions are generated independently using the exact owner-approved
`production-primary` identity and stored as the reference for compatible arena
copy-posting.

This task constructs schedule-free entrypoints only after the model, community,
and credential prerequisites in `Current blocked state` pass. The separate
P0-21 activation gates remain open after construction. This task authorizes no
community request, prediction, workflow dispatch, schedule, or production write.
P0-21 exclusively owns manual production evidence and activation.

## Current blocked state

- [ ] P0-06 records an owner-approved `production-primary` model, reasoning
      effort, positive output-token cap, hosted numbered match and bonus prompt
      versions, service-tier/fallback policy, whole-season cost ceiling, and
      estimator evidence after P0-23 evidence or an explicit accepted waiver.
- [ ] `pes-squad` Bundesliga 2026/27 POST permission is unknown. The community
      administrator must confirm it. The read-only audit established
      authentication, nine current fixtures, the current 18-team standings, and
      expected context readability, but made no POST request.
- [ ] A repository administrator confirms names-only Actions presence for
      `PES_SQUAD_KICKTIPP_USERNAME` and
      `PES_SQUAD_KICKTIPP_PASSWORD` without displaying values. Actions presence
      is unknown; accepted names are not evidence that repository secrets exist.

Do not infer any unresolved value from Luna/none validation, a historical
Bundesliga or WM26 caller, a model default, a local environment file, or an old
schedule. Do not invent a replacement secret name.

## Work items after the gates pass

- [ ] Copy the current P0-19 template into an explicit `pes-squad` context
      entrypoint pinned to posting context `pes-squad`, competition
      `bundesliga-2026-27`, and the accepted Bundesliga context profile.
- [ ] Create the `pes-squad` matchday entrypoint with posting target
      `pes-squad`, `community_context: "pes-squad"`, the exact approved
      `production-primary` model/reasoning/cap/service policy, and the accepted
      hosted numbered match prompt with required `production` membership.
- [ ] Create the `pes-squad` bonus entrypoint with the same exact posting,
      context, model, cap, and service identity plus the accepted hosted numbered
      bonus prompt and explicit immutable budgets of 20 documents / 32000 tokens.
- [ ] Wire only `PES_SQUAD_KICKTIPP_USERNAME` and
      `PES_SQUAD_KICKTIPP_PASSWORD` as the posting credential pair, plus the
      already accepted shared Firebase/OpenAI/Langfuse inputs required by each
      reusable workflow. Never select credentials from another context or row.
- [ ] Expose `workflow_dispatch` only. Leave every final production `schedule`
      absent or commented out until P0-21 has successful manual evidence and an
      Accepted activation decision.
- [ ] Update `MatchdayCommand.ProductionCommunities` and
      `BonusCommand.ProductionCommunities` only as required for the exact
      accepted `pes-squad` production entrypoints.
- [ ] Remove or clearly retire the superseded Bundesliga 2025/26 `pes-squad`
      matchday and bonus callers so no similarly named old-season entrypoint can
      remain live or appear to be the current production path.
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
- [ ] P0-21 manually collects and inspects `pes-squad` context, then manually
      records the exact successful context workflow run ID and completion before
      dispatching and verifying match and required bonus predictions. A later
      Accepted outer workflow must use machine-enforced `needs` ordering before
      any schedule is enabled.
- [ ] P0-21 verifies exact Kicktipp, Firestore, hosted-prompt, model, context,
      telemetry, usage/cost, and error evidence and confirms the stored reference
      is eligible for the separately gated arena copy path.

## Validation

- Validate YAML syntax, workflow-call contracts, explicit secret mapping, and
  deterministic caller inventory.
- Run focused workflow-contract and telemetry tests plus every affected full
  suite.
- Confirm no active trigger, 2025/26 identity, WM26 route, transfer document,
  Luna/none production inference, unresolved model slot, placeholder credential,
  or external write is introduced.

## Complete when

- Every gate and work item above has evidence, the triad is manually callable
  and schedule-free, and every production field equals the P0-06 owner decision.
- The superseded 2025/26 callers cannot be mistaken for a live path.
- P0-21—not this task—owns the first production dispatch, opening writes,
  schedule activation, first scheduled observation, and rollback decision.
