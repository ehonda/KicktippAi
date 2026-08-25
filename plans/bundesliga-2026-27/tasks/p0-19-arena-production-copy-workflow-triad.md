# P0-19 — Add the arena production-copy workflow triad

- Status: Blocked — not started; the production-primary identity, arena production participant, and exact credential names remain owner-gated
- Priority: P0
- Matrix row: `arena-production-copy`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), and the separately instantiated [`pes-production-reference` P0-19 task](p0-19-pes-squad-production-reference-workflow-triad.md) with reviewed, green callers
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0048](../decisions/0048-verify-bonus-compatibility-before-reference-copy.md)

## Outcome

The exact ADR-0039 `arena-production-copy` row has one explicit, manual-only,
schedule-free Bundesliga 2026/27 workflow triad. Its prediction entrypoints post
to `ehonda-ai-arena`, read the reference prediction with
`community_context: "pes-squad"`, and use the exact same owner-approved
`production-primary` identity as `pes-production-reference`.

This task record does not resolve any gated value. Schedule-free repository
preparation becomes available only after the preparation prerequisites below;
it authorizes no dispatch, model call, prediction write, credential use,
production POST, or schedule change. Current prerequisite facts and activation
gates are recorded in the
[production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md)
and [production activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md).

## Repository-preparation prerequisites

- [ ] P0-06 records the owner-approved `production-primary` model, reasoning effort, maximum output-token cap, numbered match and bonus prompt versions, service-tier/fallback policy, whole-season cost ceiling, and estimator evidence; Luna/none is not inherited.
- [ ] The separately instantiated `pes-production-reference` P0-19 task and its callers are reviewed and green, and those callers pin the exact `production-primary` configuration that the arena callers must mirror byte for byte.
- [ ] The owner selects the arena production-copy Kicktipp participant and records the exact model-specific local credential-profile name and exact model-specific Actions username/password names.
- [ ] Any local profile selector needed to distinguish the selected production participant from other `ehonda-ai-arena` participants is accepted and implemented before local use.
- [ ] The existing `.env.ehonda-ai-arena` profile and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` names remain reserved for the Luna plumbing participant and are not reused.
- [ ] P0-24's integrated exact bonus-compatibility, independent target-context fallback, immutable provenance, and fail-closed safety contracts are present on the implementation base.

No placeholder participant, model value, local profile, or Actions credential
name may be invented to close a prerequisite.

Terra and Sol were provisional P0-23 examples and are not the selected
experiment surface. Later owner-specified cost evidence informs the model,
cost-ceiling, and quality-budget decisions, but this clarification authorizes no
exact paid matrix, preflight, dataset mutation, or model call.

Repository secret presence, participant authentication and Bundesliga 2026/27
readiness, POST permission, exact Kicktipp match-submission and bonus deadlines,
live writes, and schedule activation are P0-21 live-dispatch gates. They are not
prerequisites to constructing this manual-only triad after the owner has selected
the participant/profile/exact credential names and P0-06 identity.

## Work items

- [ ] Create a dedicated context entrypoint for the selected arena production participant with explicit `competition: bundesliga-2026-27` and `ehonda-ai-arena` target context required by the independent bonus-fallback safety path.
- [ ] Create the matchday entrypoint with `community: "ehonda-ai-arena"`, `community_context: "pes-squad"`, and the exact same explicit `production-primary` model, reasoning, cap, prompt, and service-tier identity as the reviewed `pes-production-reference` caller.
- [ ] Require the matchday caller to exact-read and validate the stored `pes-squad` source at runtime. Copy-post only when its model configuration, fixture, and immutable context are compatible; never assume another manual workflow completed. A missing, incompatible, or ambiguous source/configuration/fixture/context fails closed with zero model-service construction/call, zero prediction persistence, and zero Kicktipp post. No independent arena-context match fallback may be added without a separate Accepted contract.
- [ ] Create the bonus entrypoint with the same posting target, source context, explicit `production-primary` identity, and exact question-aware context budgets of `20` documents and `32000` estimated tokens.
- [ ] Require exact normalized question, `MaxSelections`, and complete normalized option-set compatibility before copy-posting a stored `pes-squad` bonus prediction; a compatible path maps target option IDs and performs no model-service construction or call.
- [ ] For every ordinary missing/incompatible source bonus candidate or legacy/partial/malformed provenance, generate exactly one independent prediction in the same invocation with effective `community_context: "ehonda-ai-arena"`, persist it under that target context, and post only the independently generated target selection.
- [ ] Fail closed without a Kicktipp post for invalid or ambiguous target definitions/selections, failed target mapping, or immutable source/target context safety violations; never run an incompatibility fallback with effective context `pes-squad`.
- [ ] Wire only the owner-recorded arena production-copy credential names and the shared Firebase, OpenAI, and Langfuse contracts; load credentials from the posting participant, never from `community_context`.
- [ ] Expose `workflow_dispatch` only and keep the triad schedule-free. Do not add `workflow_call`, `schedule`, automatic retry, or `always()`. The three independent manual workflows cannot use `needs` to enforce cross-workflow order and must not imply that they can.
- [ ] Record the arena prediction dispatch precondition: P0-21 must hold exact successful `pes-production-reference` evidence for the same runtime identity and target item before either arena prediction workflow is dispatched; the arena caller still revalidates source identity and compatibility at runtime.
- [ ] Keep every Bundesliga 2025/26 and historical arena caller retired. Add new explicitly named Bundesliga 2026/27 callers rather than repurposing an old entrypoint.
- [ ] Leave deadline verification, live dispatch, posting verification, trace inspection, first production copy evidence, and any deliberate schedule activation exclusively to [P0-21](p0-21-production-activation.md). Machine-enforced reference-before-copy ordering belongs only to P0-21's later owner-approved Accepted activation ADR and outer workflow with explicit `needs` dependencies.

## Activation boundary

- [ ] P0-21 confirms names-only Actions presence for the exact owner-selected
      credential names without exposing values, authenticates that participant,
      and verifies Bundesliga 2026/27 community readiness.
- [ ] P0-21 confirms POST permission and records exact Kicktipp match and bonus
      deadlines before the first manual dispatch. This task authorizes no POST.
- [ ] P0-21 obtains and inspects the first production reference/copy evidence,
      retains all live-write gates, and alone decides schedule activation and
      rollback.

## Validation

- [ ] Parse and run `actionlint` against all three callers, and compare every `with` and `secrets` key against the corresponding reusable-workflow declaration.
- [ ] Add exact-shape workflow contracts proving manual-only triggers, no schedule or `workflow_call`, explicit `bundesliga-2026-27`, arena posting target, `pes-squad` source context, exact shared `production-primary` identity, bonus budgets `20` / `32000`, exact owner-recorded credentials, and no hidden or retired inputs.
- [ ] Prove the arena and `pes-production-reference` callers have byte-for-byte equivalent model, reasoning, cap, numbered prompt, and service-tier/fallback values wherever the reusable contracts require the shared runtime identity.
- [ ] Retain command coverage proving fixture-compatible match reuse and that every missing, incompatible, or ambiguous match source/configuration/fixture/context terminates with zero model-service construction/call, zero persistence, zero Kicktipp post, and no independent arena-context fallback.
- [ ] Retain P0-24 command, persistence, and topology coverage proving compatible bonus copy uses zero model-service constructions/calls; each ordinary incompatibility uses exactly one independent arena-context prediction; and invalid target/context safety failures persist and post nothing.
- [ ] Add telemetry coverage for posting-target environment `production`, payload-safe copy source and stored prediction identity, compatible no-model behavior, and independent fallback effective context `ehonda-ai-arena`.
- [ ] Verify the new callers contain no Bundesliga 2025/26 identity, WM26 identity, transfer document, Luna participant credential, unresolved placeholder, secret value, or schedule, and verify historical callers remain retired.
- [ ] Run the repository prediction-workflow contract, focused copy/telemetry tests, affected full suites, link checks, secret scans, and `git diff --check`; obtain independent review before integration.

## Current state

The [production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md)
found no accepted arena production participant or credential profile, and the
[activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md)
keeps this row nondeployable with no workflow or schedule. Repository preparation
is blocked on the P0-06 identity, reviewed `pes-production-reference` callers,
and owner-selected participant/profile/exact credential names—not on P0-21 live
permission evidence. The command-level P0-24 topology is implemented and tested,
but no live production copy-post or independent fallback has run. P0-21 owns
that evidence after the repository triad is ready and every pre-dispatch gate
passes.

## Complete when

- [ ] The owner-selected participant and exact credential names are recorded, the `production-primary` slot is resolved, the matching `pes-production-reference` task and callers are reviewed and green, and the arena callers mirror their exact `production-primary` configuration byte for byte.
- [ ] The triad is fully explicit, manually callable, schedule-free, and protected by workflow, copy-compatibility, persistence, telemetry, and retired-caller contracts.
- [ ] No unresolved model, participant, profile, credential, compatibility, or activation gate is deployed or marked complete by this task record.
- [ ] Open P0-21 secret-presence, authentication/readiness, POST, deadline,
      live-write, and activation gates remain recorded for handoff and do not
      block repository closeout.
