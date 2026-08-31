# P1-10 — Convert schadensfresse to a subcompetition-typed primary community

- Status: In progress; recovery runtime is frozen, target-primary completion remains an atomic future PR
- Priority: P1 — deadline-critical
- Depends on: [P0-21](p0-21-production-activation.md),
  [P1-13](p1-13-global-bundesliga-prediction-authority.md)
- Absorbs: [P1-08](p1-08-schadensfresse-mixed-competition-routing.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md), [ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md), [ADR-0059](../decisions/0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md), [ADR-0060](../decisions/0060-separate-generation-manifest-from-current-rules-attestation.md), [ADR-0061](../decisions/0061-preview-and-milestone-orchestration.md), [ADR-0062](../decisions/0062-temporarily-restore-schadensfresse-copy.md), [ADR-0063](../decisions/0063-construct-p1-10-full-branch-after-recovery.md), [ADR-0064](../decisions/0064-permit-portable-rules-fixture-test-in-p1-10-seed.md), [ADR-0065](../decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md)

- Orchestration readiness: The completed checkboxes are historical integrated
  evidence. The resumed recovery's frozen artifacts are the
  [P1 execution packet](../p1-execution-packet.md) and
  [production recovery design](../designs/p1-10-production-recovery-and-atomic-delivery.md).
  They govern recovery only; they do not mark the target-primary route done.
  ADR-0063 records that the full branch was constructed from D by the dedicated
  C-then-B inverses. Its A-equivalence is preservation, not merge readiness:
  this draft branch remains live-broken until ordinary fixture typing and every
  remaining P1-10 and Owner gate passes.
  ADR-0064 admits only E's portable fixture-test normalization; it does not
  change ADR-0062 recovery runtime, ADR-0063 construction, or any Owner gate.
  ADR-0065 and P1-13 now own the season-wide typed prediction-authority,
  per-community seed/copy-binding, exact-ID API, isolated-storage, and atomic-
  cutover foundation. This task consumes that foundation while retaining all
  Schadensfresse-specific rules, context/prompt composition, replacement, and
  activation ownership.

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

The earlier September 9 deadline, four-point bonus score, `2/3/4` match score,
and ordinary Bundesliga copy premise are historical for the final target route.
ADR-0062 temporarily restores source-compatible copy on recovery `main` while
the full target-primary implementation is preserved for an atomic PR. This
temporary route expires at `2026-09-08T12:00:00Z`; it creates no manual-copy
contingency. The separate September 8 CL bonus deadline remains a final-route
gate, not recovery authority.

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
serial chain, manual-only leaves, and no scheduled bonus. It expires at
`2026-09-08T12:00:00Z`: the atomic P1-10 PR must merge and replace/terminate
it, or Schadensfresse is re-quarantined while seven pairs remain. Project
Owner/on-call inherits ADR-0053's 30-minute acknowledgement and 60-minute
whole-cron-disable trigger. No manual dispatch, force, reprediction, prompt or
model change/call, prediction mutation, external write, credential change, or
other activation is authorized. Natural runs caused by the restored declarative
schedule may perform only ADR-0053/0054/0055's already-authorized operations;
observing them is read-only reconciliation, not additional authority.

## Implementation slices

### Global typed-authority prerequisite

- [ ] Complete and independently accept P1-13 R1-R5a before treating any
      P1-10 current read/save/reprediction/copy/verify/post path as merge-ready.
      P1-10 must use the shared Posting Community, Prediction-source
      Community, Community Context, Stable Local Item Key, Snapshot Hash,
      Copy Binding, Generation Provenance, and Authority Epoch contracts; it
      must not implement a target-only compatibility firewall.
      **Prediction-source Community**: The community under which the
      candidate prediction was generated and stored. It equals the Posting
      Community for self-contained generation; for an accepted copy it may
      differ and is identified by the Copy Binding.
- [ ] Keep P1-10's real Schadensfresse seeds/bindings, typed staging, and
      atomic activation in P1-13 R5b's all-community evidence/cutover gate.
      P1-10 still owns the exact target replacement set, prompt promotion,
      calls/cost, force/reprediction, UTC cutoff, and primary activation.
- [ ] Accept only a scheduled instant derived from exact ID-bearing fixture
      evidence and the same-ID structured detail `Termin`. Any cancelled,
      empty, inherited, sentinel, duplicate, unparsable, or conflicting item
      fails the complete selected operation before current read or downstream
      call; a same-ID reschedule keeps the Stable Local Item Key but requires a
      new additive seed generation and Snapshot Hash.

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
      CL bonus route IDs/contracts and fail-closed context dispatch under
      ADR-0058's names; never use the Bundesliga routes as a temporary
      fallback.
- [ ] In R4b add only DFB/CL route IDs/contracts, fail-closed dispatch, and
      synthetic tests. Do not add prompt bodies or mirrors, assert an
      unverified hash, or imply fallback. A later slice may add a checked-in
      mirror and equality test only after evidence records the exact hosted
      name, numbered immutable version, normalized readback hash, and required
      `production` membership; then prove normalized mirror/readback equality.
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
- [ ] Add hostile scheduled-instant tests for a cancelled first row, a
      cancelled row after a valid row, fixture/detail conflict, and same-ID
      reschedule. Each invalid selected inventory must prove atomic
      no-current-read/no-prompt-or-service-or-model-call/no-mutation/no-POST.
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
- [ ] Complete and verify the DFB/CL hosted prompt publication before the CL
      bonus deadline `2026-09-08T16:45:00Z`.
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

## Contract-slice evidence — 2026-08-30

- ADR-0058 is accepted from evidence-backed decisions the Owner authorized on
  2026-08-30; it does not claim that the Owner reviewed a draft.
- Pre-`b0fd6b6` code inspection confirmed `Match` lacked a stable fixture ID
  and generic round/result-basis fields, `BonusQuestion` lacked a typed
  Bundesliga-season subcompetition, and the parser discarded round/penalty
  meaning outside WM26. The later typed implementation is preserved on the
  archival/full PR, not temporary recovered runtime after B.
- This planning slice makes no external write, prompt promotion, production
  model call, forced prediction, POST, or schedule mutation. ADR-0058 separately
  authorizes the immediate fail-safe repository workflow removal only.

## Typed-foundation evidence — 2026-08-30

- P1-10 commit `b0fd6b6` updates the checked-in
  `data/bundesliga-2026-27/schadensfresse-routing-seed.json` with exact
  `1662323362` and `1662323366` entries (`bundesliga` / `1. Spieltag` /
  `regularTime90Minutes`), the three evidenced CL question identities, all 111
  ordered option ID/text bindings, and canonical hash
  `81b1c6ab0a6ad3159fcafebcbf1e3525df2cdf8e1279369f2515f001176008e5`.
  This is truthful preserved archival/full-PR implementation evidence, not
  temporary recovered-runtime state after B.
- Core loader/classifier validation and captured parser tests reject missing,
  duplicate, unknown, and drifted identities. Parsing retains a source round,
  penalty result basis, and stable bonus-question ID when exposed, without
  deriving a fixture ID or subcompetition from text, teams, or partition.
- Focused Core and KicktippIntegration TUnit gates passed locally; the full
  affected project gates also passed. Existing unrelated nullable warnings
  remain in the test projects.

## Rules-contract evidence — 2026-08-30

- Payload-safe authenticated evidence at SHA-256
  `4503636dba2e6d14cd276733dffd12a3d3acd344c85368417d0f9d50e7869e95`
  exactly reproduced ADR-0058's legacy normalized hash
  `b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
  and proved that its keyword filter omits both numeric scoring rows.
- Accepted ADR-0059 now fixes the exact authenticated source/DOM failure gates,
  every v1 field/type/value, canonical System.Text.Json bytes and 822-byte
  contract, structured SHA-256
  `1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90`,
  and diagnostic scoring-table SHA-256
  `4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e`.
- The rules validator/publication slice is unblocked only by that Accepted
  contract. The historical digest remains regression evidence and no source,
  live publication, prompt, production, or schedule mutation occurred in this
  decision slice.
- Local validator/publication evidence (no authenticated production collection
  was invoked) now covers the complete ADR-0059 negative matrix through
  systematic source, DOM, semantic-value, numeric, canonical-JSON, Markdown,
  publication-identity, freshness, and numeric-drift mutations. Focused gates
  passed for the provider matrix (21/21, 1.169s), Core rules contract (5/5,
  0.864s), ordinary publication including unchanged and interleaved
  different-to-original concurrency (3/3, 2.495s), full atomic publication
  (1/1, 2.701s), future-generation preflight (1/1, 0.790s), and Firebase atomic
  result/concurrency behavior (8/8, 36.391s).
- Full affected suites passed via `dotnet run --project tests/Core.Tests`
  (316/316, 0 failed, 0 skipped, 3.233s), `dotnet run --project
  tests/ContextProviders.Kicktipp.Tests` (74/74, 0 failed, 0 skipped, 1.899s),
  `dotnet run --project tests/Orchestrator.Tests` (1195/1195, 0 failed,
  0 skipped, 1m 54.711s), and `dotnet run --project
  tests/FirebaseAdapter.Tests` (302/302, 0 failed, 0 skipped, 54.935s). Each
  report is under its project `bin/Debug/net10.0/TestResults/` directory; no
  Integration project source was affected by this slice.
- The command path authenticates/extracts before target markdown collection,
  validates the checked-in semantic/content identities before publication, and
  publishes the target through an atomic transaction in both ordinary and full
  modes. That transaction returns the independently selected effective immutable
  version even for unchanged content while retaining created-version/null
  compatibility, and the command reads back only that exact version by expected
  name and content hash; it never resolves the target through floating latest.
  The checked-in Markdown is pinned to repository LF bytes by an exact-path
  attribute and verifies as 763 bytes with SHA-256
  `f943f4b8f19d69dd1fc378d5684a2fdf7f59596accab4aa25866f81889b3e709`.
  The blocking successor above deliberately keeps this non-Firebase slice from
  claiming resolved-manifest persistence or DFB/CL generation readiness.

## Persistence-hardening evidence — 2026-08-30

- Typed match and bonus rows now bind exact canonical season identity. Bonus
  identity includes question text, deadline, maximum selections, and every
  ordered option ID/text pair; typed current reads require complete immutable
  provenance, while legacy APIs retain their old behavior and exclude typed
  rows.
- Typed DFB-Pokal and Champions League persistence remains fail closed until
  `resolvedTypedContextManifest` exists. Typed cancelled-match lookup is
  available explicitly, without changing the legacy cancelled-match contract.
- Typed initial match saves plus typed initial and repredicted bonus saves use
  transactional deterministic allocation. Concurrent writers cannot create a
  duplicate semantic index, approved reprediction limits remain enforceable,
  and subcompetition, stable identity, and model-configuration scopes remain
  isolated. Any pre-existing duplicate full-provenance exact-config typed index
  fails closed across current, cancelled, copy, and reprediction paths. Legacy
  and WM26 persistence paths are unchanged.
- Focused Firebase identity/concurrency and duplicate-corruption coverage
  passed `9/9`. The exact full Firebase gate passed with
  `dotnet run --project tests/FirebaseAdapter.Tests`
  (`301/301`, `0` failed, `0` skipped, `1m 12s 712ms`) and
  `dotnet run --project tests/Core.Tests` (`311/311`, `0` failed, `0` skipped,
  `3s 477ms`).

## Manifest-lifecycle decision evidence — 2026-08-30

- Accepted ADR-0060 preserves the exact field
  `bundesligaSeasonSubcompetition`, fixes `rulesObservedAt` to canonical
  100-nanosecond UTC text, and defines freshness as the inclusive interval
  from the evaluation instant through exactly 24 hours old.
- The prediction's generation-time resolved manifest is immutable. A separate
  publication binding is directly addressed by the exact season/community/
  profile/routing-seed tuple and binds one exact immutable document plus the
  structured rules schema, hash, and current authenticated observation.
- Re-attestation refreshes only binding-scoped rules/profile/seed/document
  identity. Per-prediction reuse separately compares the current typed
  invocation, exact pinned prompt route, and model/service configuration with
  immutable prediction provenance before allowing zero model calls and zero
  prediction mutation. Trace and verification distinguish generation from
  current observation; drift and legacy state fail closed, and ADR-0058's
  Owner replacement/cost/force/cutoff gate remains.
- This accepted planning decision makes no fixture seed or prompt decision,
  performs no production or external write, and does not activate a schedule.

## Complete when

- Every current `schadensfresse` match and bonus operation is classified by a
  stable exact identity into Bundesliga, DFB-Pokal, or Champions League, uses
  the required result basis and competition-correct target prompt/context, and
  fails closed on unknown or drifted state.
- No `schadensfresse` prediction can read, copy, or inherit payload/provenance
  from `pes-squad`; no untyped legacy row is accepted as current.
- The three CL questions pass exact-identity validation and are handled through
  the promoted CL route before `2026-09-08T16:45:00Z`.
- Local and applicable dev/arena evidence is green; the approved production
  replacement stays within its exact budget/cutoff and passes payload-safe
  Kicktipp/Firestore/Langfuse inspection.
- Until the final P1-10 merge, ADR-0062's temporary source-compatible pair is
  the active recovery route and its eight-pair operating contract is preserved.
  The final merge replaces or terminates it atomically; any missed sunset
  re-quarantines Schadensfresse while preserving seven unaffected pairs.
- A separately reviewed primary-activation commit reintroduces target context
  plus the primary match job, preserves the outer operating contract, and its
  first natural execution is green.
