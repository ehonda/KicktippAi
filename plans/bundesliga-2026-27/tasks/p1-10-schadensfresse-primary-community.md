# P1-10 — Convert schadensfresse to a subcompetition-typed primary community

- Status: In progress
- Priority: P1 — deadline-critical
- Depends on: [P0-21](p0-21-production-activation.md)
- Absorbs: [P1-08](p1-08-schadensfresse-mixed-competition-routing.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md), [ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md), [ADR-0059](../decisions/0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md)

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
- open fixture IDs `1662323362` and `1662323366` were joined from the outcome
  surface. Their teams, numeric matchday, and rules context support a
  Bundesliga inference, but both current `Match` objects have null competition-
  specific data and the safe evidence captured no exact round or result basis;
  they are not yet canonical route entries; and
- open CL questions `1662326752`, `1662326753`, and `1662326754` each have 37
  options, maxima `1/4/1`, and the exact common deadline
  `2026-09-08T16:45:00Z`. Their full texts and per-question option hashes are
  frozen in ADR-0058; the complete safe array SHA-256 is
  `80def7b217a382ed95450c2a8f8db227ba13a2f55ca72513a8897f86fa511ef9`.

The earlier September 9 deadline, four-point bonus score, `2/3/4` match score,
and ordinary Bundesliga copy premise are historical and superseded. The
September 8 CL deadline is the critical path.

## Outcome

`schadensfresse` is an independent target-owned primary for every match and
bonus question. The explicit `bundesliga-2026-27` storage partition remains,
while `BundesligaSeasonSubcompetition` distinguishes Bundesliga, DFB-Pokal,
and Champions League only inside it. Stable Kicktipp identities, exact
rounds/questions, correct result bases, competition-specific prompt/context
routes, and fail-closed storage/provenance are mandatory. WM26 retains its
existing competition-specific model. No path copies or falls back to
`pes-squad`.

## Implementation slices

### 0. Immediate schedule quarantine

- [x] Before the next nominal `2026-08-30T09:07:00Z` occurrence, remove
      `schadensfresse-context` and `schadensfresse-matchday` from
      `.github/workflows/buli2627-production-live-matchday.yml`, and reconnect
      `relaxdays-tippt-context.needs` directly to `pes-squad-matchday`.
- [x] Prove the outer lane has exactly seven remaining context/match pairs and
      14 jobs while retaining cron `7 2,9 * * *`, non-cancelling concurrency,
      serial/default-success ordering, leaf-manual-only operation, no bonus,
      monitoring/on-call ownership, and rollback behavior.
- [x] Keep both schadensfresse jobs absent until the separately reviewed
      primary activation. The quarantine authorizes no dispatch, model call,
      force, prediction mutation, POST, Firestore/Langfuse write, prompt
      promotion, or credential change.

### 1. Typed identity and classifier

- [x] Add Bundesliga-partition-only `BundesligaSeasonSubcompetition` with the
      exact ADR-0058 values/serialization. Do not place WM26 in that enum or
      replace `CompetitionSpecificMatchData`/`FifaWorldCup2026MatchData`.
- [x] Add generic `KicktippFixtureId`, exact `KicktippRoundName`, and typed
      `ResultBasis` to `Match`, allowing coexistence with WM26-specific data.
- [x] Add `KicktippQuestionId` and `BundesligaSeasonSubcompetition` to
      Bundesliga-season `BonusQuestion` identity, bound to exact text, ordered
      option ID/text array, `MaxSelections`, and deadline.
- [x] Check in a deterministic schadensfresse routing seed/config containing
      every exact fixture→subcompetition→round→result-basis mapping plus all
      three question definitions and 111 option ID/text bindings represented
      by ADR-0058's hashes. Do not promote the two inferred current fixture IDs
      into the seed until exact round/subcompetition evidence is recorded.
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
      with ordered `rulesSchemaVersion` and `canonicalRulesSha256` fields.
- [ ] Add Firebase persistence/readback coverage that rejects legacy
      `normalizedRulesSha256`-only manifests and legacy/table hashes as current
      semantic identities.
- [ ] Invoke the shared fail-closed rules generation preflight from the actual
      DFB-Pokal and Champions-League command paths before prompt/model-service
      construction. Those commands/routes do not exist yet; this is blocked
      work, not runtime readiness.
- [ ] Re-run the full persistence/call-site validation matrix and record the
      reviewed immutable-manifest evidence before any manual generation.

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
- Current code inspection confirms `Match` lacks a stable fixture ID and
  generic round/result-basis fields, `BonusQuestion` lacks a typed Bundesliga-
  season subcompetition, and the parser discards round/penalty meaning outside
  WM26.
- This planning slice makes no external write, prompt promotion, production
  model call, forced prediction, POST, or schedule mutation. ADR-0058 separately
  authorizes the immediate fail-safe repository workflow removal only.

## Typed-foundation evidence — 2026-08-30

- The checked-in `data/bundesliga-2026-27/schadensfresse-routing-seed.json`
  records the exact three evidenced CL question identities, all 111 ordered
  option ID/text bindings, source option-set hashes, and canonical seed hash
  `52ce7ba4430d07ed71528a7ce48fee499e25b9dd303bd7bce22eed17a1921660`.
  It intentionally contains no fixture entries: `1662323362` and `1662323366`
  remain unseeded until exact structured round/subcompetition evidence exists.
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
- The unsafe schadensfresse context/copy pair is absent from the active outer
  lane before any primary activation, with all unaffected schedule contracts
  preserved.
- A separately reviewed primary-activation commit reintroduces target context
  plus the primary match job, preserves the outer operating contract, and its
  first natural execution is green.
