# P0-19 — Add the arena production-copy workflow triad

- Status: Complete — the manual copy triad and audit are green; the leaf stays schedule-free while ADR-0053's outer lane owns the active recurring schedule
- Priority: P0
- Matrix row: `arena-production-copy`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), and the separately instantiated [`pes-production-reference` P0-19 task](p0-19-pes-squad-production-reference-workflow-triad.md) with reviewed, green callers
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0048](../decisions/0048-verify-bonus-compatibility-before-reference-copy.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md)

## Outcome

The exact ADR-0039 `arena-production-copy` row has one explicit, manual-only,
schedule-free Bundesliga 2026/27 workflow triad. Its prediction entrypoints post
to `ehonda-ai-arena`, read the reference prediction with
`community_context: "pes-squad"`, and use the exact same owner-approved
`production-primary` identity as `pes-production-reference`.

This task record's preparation boundary is historical. Schedule-free repository
preparation became available only after the prerequisites below and authorized
no dispatch, model call, prediction write, credential use, production POST, or
schedule change. Current runtime and activation facts are recorded in the
[production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md)
and [production activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md).

## Repository-preparation prerequisites

- [x] P0-06 records the owner-approved `production-primary` model, reasoning effort, maximum output-token cap, numbered match and bonus prompt versions, service-tier/fallback policy, whole-season cost ceiling, and estimator evidence; Luna/none is not inherited.
- [x] The separately instantiated `pes-production-reference` P0-19 task and its callers are reviewed and green, and those callers pin the exact `production-primary` configuration that the arena callers must mirror byte for byte.
- [x] The owner selects the arena production-copy Kicktipp participant and records the exact model-specific local credential-profile name and exact model-specific Actions username/password names.
- [x] Any local profile selector needed to distinguish the selected production participant from other `ehonda-ai-arena` participants is accepted and implemented before local use.
- [x] The existing `.env.ehonda-ai-arena` profile and `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP_PASSWORD` names remain reserved for the Luna plumbing participant and are not reused.
- [x] P0-24's integrated exact bonus-compatibility, independent target-context fallback, immutable provenance, and fail-closed safety contracts are present on the implementation base.

No placeholder participant, model value, local profile, or Actions credential
name may be invented to close a prerequisite.

At that pre-selection checkpoint, Terra and Sol were provisional P0-23 examples
rather than a selected experiment surface. ADR-0052 supersedes that historical
wording with the current exact production and challenger matrix; the old text
does not reopen model selection.

Repository secret presence, participant authentication and Bundesliga 2026/27
readiness, POST permission, exact Kicktipp match-submission and bonus deadlines,
live writes, and schedule activation were P0-21 live-dispatch gates rather than
construction prerequisites. P0-21 has since closed them for this ready row and
activated only the Accepted outer lane; this leaf remains manual-only.

## Repository closeout — 2026-08-27

ADR-0052 selects the arena Sol/`xhigh` production participant, cap `10000`,
match v3 / bonus v1, Flex-first / Standard-fallback, and local profile
`.env.ehonda-ai-arena.gpt-5-6-sol-xhigh`. The dedicated context caller and
prediction callers are prepared; match and compatible bonus paths copy from
`pes-squad`. All three callers expose only `workflow_dispatch` and no schedule.
The Owner confirmed the exact
`EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_USERNAME` /
`EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP_PASSWORD` pair is provisioned,
without establishing runtime readiness or POST permission.

## Work items

- [x] Create a dedicated context entrypoint for the selected arena production participant with explicit `competition: bundesliga-2026-27` and `ehonda-ai-arena` target context required by the independent bonus-fallback safety path.
- [x] Create the matchday entrypoint with `community: "ehonda-ai-arena"`, `community_context: "pes-squad"`, and the exact same explicit `production-primary` model, reasoning, cap, prompt, and service-tier identity as the reviewed `pes-production-reference` caller.
- [x] Require the matchday caller to exact-read and validate the stored `pes-squad` source at runtime. Copy-post only when its model configuration, fixture, and immutable context are compatible; never assume another manual workflow completed. A missing, incompatible, or ambiguous source/configuration/fixture/context fails closed with zero model-service construction/call, zero prediction persistence, and zero Kicktipp post. No independent arena-context match fallback may be added without a separate Accepted contract.
- [x] Create the bonus entrypoint with the same posting target, source context, explicit `production-primary` identity, and exact question-aware context budgets of `20` documents and `32000` estimated tokens.
- [x] Require exact normalized question, `MaxSelections`, and complete normalized option-set compatibility before copy-posting a stored `pes-squad` bonus prediction; a compatible path maps target option IDs and performs no model-service construction or call.
- [x] For every ordinary missing/incompatible source bonus candidate or legacy/partial/malformed provenance, generate exactly one independent prediction in the same invocation with effective `community_context: "ehonda-ai-arena"`, persist it under that target context, and post only the independently generated target selection.
- [x] Fail closed without a Kicktipp post for invalid or ambiguous target definitions/selections, failed target mapping, or immutable source/target context safety violations; never run an incompatibility fallback with effective context `pes-squad`.
- [x] Wire only the owner-recorded arena production-copy credential names and the shared Firebase, OpenAI, and Langfuse contracts; load credentials from the posting participant, never from `community_context`.
- [x] Expose `workflow_dispatch` only and keep the triad schedule-free. Do not add `workflow_call`, `schedule`, automatic retry, or `always()`. The three independent manual workflows cannot use `needs` to enforce cross-workflow order and must not imply that they can.
- [x] Record the arena prediction dispatch precondition: P0-21 must hold exact successful `pes-production-reference` evidence for the same runtime identity and target item before either arena prediction workflow is dispatched; the arena caller still revalidates source identity and compatibility at runtime.
- [x] Keep every Bundesliga 2025/26 and historical arena caller retired. Add new explicitly named Bundesliga 2026/27 callers rather than repurposing an old entrypoint.
- [x] Leave deadline verification, live dispatch, posting verification, trace inspection, first production copy evidence, and deliberate schedule activation exclusively to [P0-21](p0-21-production-activation.md). ADR-0053 now records that Owner-approved activation and gives only the outer workflow machine-enforced reference-before-copy `needs` dependencies.

## Activation boundary

- [x] P0-21 confirms names-only Actions presence for the exact owner-selected
      credential names without exposing values, authenticates that participant,
      and verifies Bundesliga 2026/27 community readiness.
- [x] Successful match/bonus final verification establishes posting behavior;
      the timestamped community audit records zero minutes lead time and first
      match/bonus cutoff 2026-08-28 20:30 CEST / 18:30 UTC, subject to a fresh
      read after rescheduling or an administrator rule change.
- [x] P0-21 obtained and inspected the first production reference/copy evidence
      and recorded the active outer schedule plus rollback contract in
      ADR-0053. This leaf remains schedule-free.

## Validation

- [x] Parse and run `actionlint` against all three callers, and compare every `with` and `secrets` key against the corresponding reusable-workflow declaration.
- [x] Add exact-shape workflow contracts proving manual-only triggers, no schedule or `workflow_call`, explicit `bundesliga-2026-27`, arena posting target, `pes-squad` source context, exact shared `production-primary` identity, bonus budgets `20` / `32000`, exact owner-recorded credentials, and no hidden or retired inputs.
- [x] Prove the arena and `pes-production-reference` callers have byte-for-byte equivalent model, reasoning, cap, numbered prompt, and service-tier/fallback values wherever the reusable contracts require the shared runtime identity.
- [x] Retain command coverage proving fixture-compatible match reuse and that every missing, incompatible, or ambiguous match source/configuration/fixture/context terminates with zero model-service construction/call, zero persistence, zero Kicktipp post, and no independent arena-context fallback.
- [x] Retain P0-24 command, persistence, and topology coverage proving compatible bonus copy uses zero model-service constructions/calls; each ordinary incompatibility uses exactly one independent arena-context prediction; and invalid target/context safety failures persist and post nothing.
- [x] Add telemetry coverage for posting-target environment `production`, payload-safe copy source and stored prediction identity, compatible no-model behavior, and independent fallback effective context `ehonda-ai-arena`.
- [x] Verify the new callers contain no Bundesliga 2025/26 identity, WM26 identity, transfer document, Luna participant credential, unresolved placeholder, secret value, or schedule, and verify historical callers remain retired.
- [x] Run the repository prediction-workflow contract, focused copy/telemetry tests, affected full suites, link checks, secret scans, and `git diff --check`.
- [x] Obtain independent exact-SHA review before integration.

## Current state

ADR-0052 resolved the historical blockers recorded by the dated
[production prerequisite audit](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md)
and [activation preregistration](../../../docs/onboarding-bundesliga-2026-27/production-activation-preregistration.md).
The exact participant, credential names, identity, and three manual-only callers
are prepared and covered. P0-21 context run
[`33050848544`](https://github.com/ehonda/KicktippAi/actions/runs/33050848544),
match copy run
[`33051066657`](https://github.com/ehonda/KicktippAi/actions/runs/33051066657),
and bonus copy run
[`33051557046`](https://github.com/ehonda/KicktippAi/actions/runs/33051557046)
all completed successfully with final verification. Payload-safe evidence
proves the compatible path copied 9/9 matches and 5/5 bonus answers, generated
zero calls, used no independent fallback, and preserved enriched roster head
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
This leaf remains schedule-free; ADR-0053's outer ready-row schedule is active,
with its first natural observation still open.

## Complete when

- [x] The owner-selected participant and exact credential names are recorded, the `production-primary` slot is resolved, the matching `pes-production-reference` task and callers are prepared, and the arena callers mirror their exact `production-primary` configuration byte for byte.
- [x] The triad is fully explicit, manually callable, schedule-free, and protected by workflow, copy-compatibility, persistence, telemetry, and retired-caller contracts.
- [x] No unresolved model, participant, profile, credential, compatibility, or activation gate is deployed or marked complete by this task record.
- [x] P0-21 secret-presence, authentication/readiness, POST, deadline,
      live-write, copy-audit, and outer-activation evidence is recorded. The
      leaf remains schedule-free and the first outer observation remains open.
