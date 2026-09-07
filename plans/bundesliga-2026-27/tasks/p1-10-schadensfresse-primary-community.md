# P1-10 — Convert schadensfresse to a subcompetition-typed primary community

- Status: In progress; recovery runtime is frozen, target-primary completion remains an atomic future PR
- Priority: P1 — final typed-primary replacement lane
- Depends on: [P0-21](../archive/p0/tasks/p0-21-production-activation.md)
- Absorbs: [P1-08](../archive/p1/tasks/p1-08-schadensfresse-mixed-competition-routing.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md), [ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md), [ADR-0059](../decisions/0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md), [ADR-0060](../decisions/0060-separate-generation-manifest-from-current-rules-attestation.md), [ADR-0061](../decisions/0061-preview-and-milestone-orchestration.md), [ADR-0062](../decisions/0062-temporarily-restore-schadensfresse-copy.md), [ADR-0068](../decisions/0068-replace-copy-sunset-with-reviewed-replacement-condition.md), [ADR-0069](../decisions/0069-deliver-frozen-schadensfresse-champions-league-bonus.md)

- Orchestration readiness: The completed checkboxes are historical integrated
  evidence, summarized in the [foundation evidence archive](../archive/p1/evidence/p1-10-foundation-evidence-2026-08-30.md).
  The resumed recovery's frozen artifacts are the
  [P1 execution packet](../p1-execution-packet.md) and
  [production recovery design](../designs/p1-10-production-recovery-and-atomic-delivery.md).
  They govern recovery only; they do not mark the target-primary route done.

## Trigger and live evidence

An authenticated read-only retrieval completed at
`2026-08-30T07:35:21.9308276Z` established the current `schadensfresse`
contract without recording prediction contents or selected answers:

- match scoring is `2/3/5` for a winning tendency/goal difference/exact score
  and `3/-/5` for a draw; each correct bonus answer scores `9`;
- tips are hidden, exact-score mode is enabled, the lead time is zero, bonus
  order is irrelevant, and ordinary ties use matchday wins unless agreed
  otherwise;
- Bundesliga uses the 90-minute result, while DFB-Pokal and Champions League
  use the result after a penalty shootout;
- the rules HTML SHA-256 is
  `f788efe448ce538d530baf74ce66f5ef03a61faab5a527d965dcd8d314d2e9c0`;
  the checked-in `schadensfresse` and `pes-squad` rules are currently
  byte-identical and the target copy is wrong;
- P1-10 commit `b0fd6b6` later added fixture IDs `1662323362` and `1662323366`
  to the canonical routing seed as `bundesliga` / `1. Spieltag` /
  `regularTime90Minutes`. That typed implementation/evidence is preserved on
  the archival/full P1-10 PR route and is intentionally absent from temporary
  recovered runtime after aggregate revert B; and
- open CL questions `1662326752`, `1662326753`, and `1662326754` each have 37
  options, maxima `1/4/1`, and the exact common deadline
  `2026-09-08T16:45:00Z`. Their full texts and per-question option hashes are
  frozen in ADR-0058; the complete safe array SHA-256 is
  `80def7b217a382ed95450c2a8f8db227ba13a2f55ca72513a8897f86fa511ef9`.
  P1-15 later completed those exact deadline-bound answers through its narrow
  target-owned exception; that completed operation is not authority for the
  future general P1-10 route.

The earlier September 9 deadline, four-point bonus score, `2/3/4` match score,
and ordinary Bundesliga copy premise are historical for the final target route.
ADR-0062 temporarily restores source-compatible copy on recovery `main` while
the full target-primary implementation is preserved for an atomic PR. Under
ADR-0068, that route remains until a reviewed successor explicitly replaces
or terminates it; calendar time alone does not trigger quarantine. It creates
no manual-copy contingency, and completed P1-15 is not recovery authority.

## Outcome

The completed P1-10 PR will make `schadensfresse` an independent target-owned
primary for every match and bonus question. The explicit `bundesliga-2026-27` storage partition remains,
while `BundesligaSeasonSubcompetition` distinguishes Bundesliga, DFB-Pokal,
and Champions League only inside it. Stable Kicktipp identities, exact
rounds/questions, correct result bases, competition-specific prompt/context
routes, and fail-closed storage/provenance are mandatory. WM26 retains its
existing competition-specific model. No path copies or falls back to
`pes-squad`.

## Frozen recovery route — 2026-08-31

The current failing head is `71637cc154cfdcbe2436069470b5e04b0d4f753d`.
Build-and-Test run `33340578338` is green, while production-live runs
`33350964121` and `33377913801` fail before model/post in ordinary blank
typed-fixture validation. ADR-0062 selects
`3a2ba35529b262327a3ec08e6bde47b186c8e5b2` as the recovery runtime baseline,
retaining P1-09/P1-12, and its packet/design require exact path comparison,
the blank-fixture regression, 8 pairs/16 jobs, source/target credential
separation, zero-copy-generation/fail-closed evidence, WM26 isolation, full
affected TUnit/Release/workflow-contract/actionlint gates, independent
exact-SHA review, exact-head CI, and first natural-run observation.

Recovery `main` temporarily runs target-owned Schadensfresse context followed
by `pes-squad`-source copy matching after `pes-squad`; relaxdays follows it.
The route preserves the cron, non-cancelling concurrency, default-success
serial chain, manual-only leaves, and no scheduled bonus. ADR-0068 keeps it in
place until an independently reviewed P1-10 successor names the replacement
topology, rollback/recovery owner, exact integrated revision, and required
green validation. Project Owner/on-call inherits ADR-0053's 30-minute acknowledgement and 60-minute
whole-cron-disable trigger. No manual dispatch, force, reprediction, prompt or
model change/call, prediction mutation, external write, credential change, or
other activation is authorized. Natural runs caused by the restored declarative
schedule may perform only ADR-0053/0054/0055's already-authorized operations;
observing them is read-only reconciliation, not additional authority.

## Implementation slices

### 0. Historical schedule quarantine (temporarily superseded on recovery main)

- [x] Before the next nominal `2026-08-30T09:07:00Z` occurrence, remove
      `schadensfresse-context` and `schadensfresse-matchday` from
      `.github/workflows/buli2627-production-live-matchday.yml`, and reconnect
      `relaxdays-tippt-context.needs` directly to `pes-squad-matchday`.
- [x] Prove the outer lane has exactly seven remaining context/match pairs and
      14 jobs while retaining cron `7 2,9 * * *`, non-cancelling concurrency,
      serial/default-success ordering, leaf-manual-only operation, no bonus,
      monitoring/on-call ownership, and rollback behavior.
- [x] The historical quarantine kept both Schadensfresse jobs absent. ADR-0062
      now temporarily restores the reviewed copy pair on recovery `main`; it
      still authorizes no dispatch, model call, force, prediction mutation,
      POST, Firestore/Langfuse write, prompt promotion, or credential change.

### 1. Typed identity and classifier

- [x] Add Bundesliga-partition-only `BundesligaSeasonSubcompetition` with the
      exact ADR-0058 values/serialization. Do not place WM26 in that enum or
      replace `CompetitionSpecificMatchData`/`FifaWorldCup2026MatchData`.
- [x] Add generic `KicktippFixtureId`, exact `KicktippRoundName`, and typed
      `ResultBasis` to `Match`, allowing coexistence with WM26-specific data.
- [x] Add `KicktippQuestionId` and `BundesligaSeasonSubcompetition` to
      Bundesliga-season `BonusQuestion` identity, bound to exact text, ordered
      option ID/text array, `MaxSelections`, and deadline.
- [x] P1-10 commit `b0fd6b6` checks in a deterministic routing seed with
      fixture IDs `1662323362` and `1662323366` mapped exactly to
      `bundesliga` / `1. Spieltag` / `regularTime90Minutes`, plus three
      question definitions and 111 option ID/text bindings. Its canonical seed
      hash is `81b1c6ab0a6ad3159fcafebcbf1e3525df2cdf8e1279369f2515f001176008e5`.
      This belongs to archival/full-PR P1-10 evidence and is intentionally
      absent from temporary recovery `main` after B.
- [ ] Parse and retain the stable IDs and structured round/competition signals.
      Join fixture IDs from the outcome surface until the open-prediction DTO
      exposes them directly. Fail before model creation on missing, unknown,
      duplicate, conflicting, or drifted identity; forbid text-prefix,
      round-prefix, storage-partition, team-name, and untyped fallbacks.

### 2. Target-owned routes and persistence

- [x] Accept ADR-0059's implementation-ready authenticated DOM, typed
      `schadensfresse-live-rules-v1`, canonical JSON, markdown binding,
      immutable publication, 24-hour freshness, and legacy-rejection contract.
      This decision unblocks the validator/publication implementation only; it
      completes no source, publication, production, prompt, or schedule work.
- [x] Accept ADR-0060's exact UTC timestamp, immutable generation-manifest,
      directly keyed current-publication-binding, and zero-call/zero-mutation
      re-attestation contract. This decision unblocks persistence and reuse
      tests only; it grants no production, prompt, model, seed, or activation
      authority.
- [ ] Replace the wrong checked-in rules with the verified target scoring,
      visibility, deadline, bonus-order/tie-break, and result-basis contract.
      Validate its semantic projection against ADR-0059's structured hash and
      bind its separate exact content hash through seed, immutable publication,
      readback, and resolved-manifest provenance.
- [ ] Remove or reject every `schadensfresse` → `pes-squad` match/bonus alias,
      copy lookup, source context, and immutable copy-provenance path.
- [ ] Persist Bundesliga-season subcompetition, Kicktipp fixture/question
      identity, match round, and result basis inside the existing season
      partition and bind them to lookup, freshness, provenance, trace, and verification.
      Apply ADR-0058's exact serialization and preserve legacy/WM26 rows while
      rejecting untyped legacy Bundesliga rows as current.
- [ ] Route Bundesliga through target-owned context and ADR-0052's production
      match v3/bonus v1 identities. Add explicit DFB-Pokal match, CL match, and
      CL bonus prompt/context routes under ADR-0058's names; never use the
      Bundesliga routes as a temporary fallback.
- [ ] Prepare checked-in DFB/CL prompt mirrors and tests now, but keep live
      routes fail closed until their immutable hosted versions, hashes, and
      `production` promotion receive review and are recorded.
- [ ] Implement the exact three ADR-0058 rules-only profiles: sole allowlisted
      `community-rules-schadensfresse.md`, current validated repo publication,
      24-hour authenticated rules freshness, one-document/2048-token budget,
      ADR-0059's canonical structured rules identity and successor
      `resolvedTypedContextManifest` fields, and explicit rejection of all
      Bundesliga/Club-Elo/roster/history/latest/generic leakage. Record the
      rules-only quality limitation in trace/validation evidence. The legacy
      `b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
      hash and diagnostic
      `4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e`
      table hash cannot substitute for the canonical
      `1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90`
      semantic gate.
- [ ] Retain ADR-0052's `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first with
      one Standard fallback identity unless an accepted successor replaces it.

### 3. Automated validation

- [ ] Add captured-fixture/parser and classifier tests for Bundesliga,
      DFB-Pokal, Champions League, 90-minute versus after-penalties bases, the
      two current fixture inferences/noncanonical state, and the three exact CL
      question definitions.
- [ ] Add negative tests for missing/unknown IDs, ambiguous competition,
      mutated text/options/max/deadline, seed drift, prefix-only classification,
      untyped legacy rows, cross-partition subcompetition, context allowlist or
      budget violations, stale rules evidence, and any attempted `pes-squad`
      copy/fallback.
- [ ] Add persistence, freshness, provenance, command, workflow-contract, and
      schedule tests proving target-owned primary generation while preserving
      the outer lane's cadence/concurrency/failure/monitoring/no-bonus contract.
- [ ] Run targeted TUnit projects with `dotnet run`, then the affected full
      solution/test gate and `actionlint`; record exact commands and results.

### Blocking successor — runtime provenance and DFB/CL invocation

The local rules-publication slice now authenticates and semantically validates
the source before accepting the target-owned markdown candidate, and reads the
exact saved immutable rules version back by name/version/content hash. It is
not runtime-ready: do not mark the rules-publication/profile item complete
until the persistence/call-site owner completes all of the following.

- [ ] Persist and read back the `resolvedTypedContextManifest` successor schema
      with ordered `rulesSchemaVersion` and `canonicalRulesSha256` fields, and
      keep its generation-time `rulesObservedAt` immutable under ADR-0060.
- [ ] Persist and address `resolvedTypedContextPublicationBinding` only by the
      exact `(seasonPartition, communityContext, profileId,
      routingSeedSha256)` key using the canonical injective physical encoding.
      Prove deterministic create/newer-update/equal-or-older-no-op/drift-fail
      transaction results and schedule-independent effective bindings under
      equal-creator and both older/newer concurrency interleavings, plus drift
      conflicts and cross-key separation.
- [ ] Keep publication refresh scoped to rules/profile/seed/document identity.
      Separately validate prediction reuse by comparing the current typed
      invocation, exact hosted prompt name/immutable version/read-back
      normalized hash/required label membership, and exact model/reasoning/
      output-cap/Flex-first-with-one-Standard-fallback identity against the
      immutable prediction provenance. Prove zero model call and zero
      prediction mutation only after both checks pass.
- [ ] Add Firebase persistence/readback coverage that rejects legacy
      `normalizedRulesSha256`-only manifests and legacy/table hashes as current
      semantic identities.
- [ ] Invoke the shared fail-closed rules generation preflight from the actual
      DFB-Pokal and Champions-League command paths before prompt fetch. After
      every prompt-independent gate passes, fetch and verify the exact pinned
      prompt name/version/read-back normalized hash/required label before
      model-service construction. Those commands/routes do not exist yet;
      this is blocked work, not runtime readiness.
- [ ] Re-run the full persistence/call-site validation matrix and record the
      reviewed immutable-manifest, current-binding, and distinct generation/
      current observation evidence before any manual generation.

### 4. Ordered manual transition

- [ ] Validate applicable Bundesliga primary plumbing in
      `ehonda-dev-buli-2627` with `gpt-5.6-luna`, `none` reasoning, and a pinned
      output cap. Use `ehonda-ai-arena` only when it exposes the same typed
      fixture/question contract; otherwise record why it is not applicable.
- [ ] Perform a read-only target preflight for exact rules/seed hashes,
      fixtures, questions, deadline, context readiness, existing copied rows,
      earliest cutoff, and prompt promotion. Rules evidence must be no older
      than 24 hours and the sole published rules document must match its exact
      seed hash; any drift closes the gate.
- [ ] Obtain Owner approval for the exact copied-row replacement set, maximum
      added calls/cost, force/reprediction limits, and UTC cutoff. No default
      budget exists; this task makes no production force/model call before it.
- [ ] Complete and verify each immutable DFB/CL hosted prompt before its
      corresponding target-primary operation. P1-15's completed one-off CL
      bonus prompt and route do not substitute for this general contract.
- [ ] Run target context collection, then only the approved minimum manual
      primary operations. Inspect Kicktipp, Firestore, and Langfuse in order
      using payload-safe IDs/counts/hashes/configuration/usage/cost, without
      exposing predictions, selections, prompts, context bodies, or secrets.
- [ ] After green manual evidence, make and separately review the exact
      primary-activation commit reintroducing target context followed by the
      primary match job. Do not restore the copy, add bonus scheduling, or
      alter the accepted cron, concurrency, remaining ordering, failure,
      monitoring, or rollback contracts.
- [ ] Observe and record the first natural execution on the exact pushed
      schedule commit.
- [ ] Verify branch, remotes, status, and latest commit; commit scoped changes
      intentionally and push the explicit remote/branch.

## Historical foundation evidence

The completed 2026-08-30 planning, typed-foundation, rules-contract,
persistence-hardening, and manifest-lifecycle evidence is preserved in the
[P1-10 foundation evidence archive](../archive/p1/evidence/p1-10-foundation-evidence-2026-08-30.md).
It is evidence for the retained full-PR work, not runtime completion.

## Complete when

- Every current `schadensfresse` match and bonus operation is classified by a
  stable exact identity into Bundesliga, DFB-Pokal, or Champions League, uses
  the required result basis and competition-correct target prompt/context, and
  fails closed on unknown or drifted state.
- No `schadensfresse` prediction can read, copy, or inherit payload/provenance
  from `pes-squad`; no untyped legacy row is accepted as current.
- Future typed CL operations pass exact-identity validation through the
  reviewed general CL route; completed P1-15 remains a separate historical exception.
- Local and applicable dev/arena evidence is green; the approved production
  replacement stays within its exact budget/cutoff and passes payload-safe
  Kicktipp/Firestore/Langfuse inspection.
- Until the final P1-10 merge, ADR-0062's temporary source-compatible pair is
  the active recovery route and its eight-pair operating contract is preserved.
  A reviewed successor replaces or terminates it atomically under ADR-0068;
  the former calendar sunset no longer changes runtime by itself.
- A separately reviewed primary-activation commit reintroduces target context
  plus the primary match job, preserves the outer operating contract, and its
  first natural execution is green.
