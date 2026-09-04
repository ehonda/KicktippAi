# P1-13 — Establish global Bundesliga prediction authority

- Status: R0 specification authored; implementation blocked pending independent
  exact-commit acceptance
- Priority: P1 — blocking foundation for P1-10 completion
- Depends on: [P0-21](p0-21-production-activation.md),
  [P1-09](p1-09-current-open-matchday-context.md), and
  [P1-12](p1-12-standings-reprediction-exemption.md)
- Blocks: [P1-10](p1-10-schadensfresse-primary-community.md)
- Decision: [ADR-0065](../decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md)
- Design: [global typed prediction authority and cutover](../designs/p1-13-global-typed-prediction-authority-and-cutover.md)
- Execution packet: [P1-13 execution packet](../p1-13-execution-packet.md)

## Outcome

Every prediction-authoritative Bundesliga 2026/27 match and bonus operation
uses exact community-local item identities, versioned semantic snapshots,
immutable seed/copy bindings, complete generation provenance, isolated typed
storage, and exact-ID Kicktipp POST/readback. Legacy rows remain available for
explicit history, context, audit, and cost use but cannot feed current
selection, reuse, reprediction, copy, verification, or posting.

P1-13 owns the global foundation. P1-10 remains the owner of Schadensfresse's
target rules, contexts, DFB/CL prompts/routes, replacement plan, and primary
activation.

The canonical authority is the relationship among Posting Community,
Prediction-source Community, Community Context, Stable Local Item Key,
Snapshot Hash, Identity Seed Generation, Copy Binding, Generation Provenance,
and Authority Epoch. A Legacy Row can serve only explicit non-current uses.
**Prediction-source Community**: The community under which the candidate
prediction was generated and stored. It equals the Posting Community for
self-contained generation; for an accepted copy it may differ and is
identified by the Copy Binding.

## Current production boundary

Recovery `main` remains on ADR-0062's eight-pair source-copy authority through
`2026-09-08T12:00:00Z`. P1-13 work stays on the draft route and uses only
synthetic fixtures or a physically isolated typed epoch until the later Owner
gates pass. It does not modify recovery data, workflows, cadence, prompts,
predictions, credentials, or external state. Missing the sunset re-quarantines
Schadensfresse and preserves seven unaffected pairs; it does not permit a
partial typed cutover.

## Milestones

### R0 — Tracked specification freeze

- [x] Record the Owner's season-wide typing decision and rejected target-only
      compatibility firewall in Accepted ADR-0065.
- [x] Define the canonical season glossary without implementation details.
- [x] Freeze the call-surface matrix, exact typed API boundary, stable key and
      Snapshot Hash distinction, immutable seed/copy/provenance contracts,
      hostile scenarios, isolated epoch, atomic cutover, rollback, ownership,
      gates, and non-goals in the design and execution packet.
- [x] Link the global foundation from P1-10, the P1 recovery artifacts, plan,
      execution strategy, onboarding profile/guide, data contract, and
      community-configuration design without rewriting recovery evidence.
- [ ] Obtain independent `gpt-5.6-sol` / `high` acceptance of the exact R0
      commit before admitting R1.

### R1 — Core authority contracts

- [ ] Add canonical authority, stable-key, typed match/bonus snapshot,
      Snapshot Hash, identity-seed generation, Copy Binding,
      `PredictionGenerationProvenanceV2`, and compatibility contracts.
- [ ] Make the canonical match scheduled instant require exact ID-bearing
      fixture evidence plus the same-ID structured detail `Termin`. Represent
      no cancelled/empty/unparsable/conflicting/inherited/sentinel state as a
      valid scheduled instant.
- [ ] Add strict canonical serializers/loaders and complete-inventory
      validation using synthetic fixtures only. Reject duplicate/local-ID
      ambiguity, snapshot drift, partial option maps, unknown routes, and all
      cross-community/global-ID assumptions.
- [ ] Freeze `IBundesligaTypedPredictionAuthorityRepository` request/result
      contracts before provider or persistence work begins.

### R2a — Kicktipp exact identity boundary

- [ ] Produce complete typed posting-community match and bonus snapshots from
      exact source identities. Retain every field needed to recompute the
      canonical snapshot.
- [ ] Reject cancelled first rows, cancelled rows after valid rows, missing or
      duplicate detail rows, inherited prior-row timestamps,
      `Instant.MinValue`/sentinels, unparsable `Termin`, and fixture/detail
      conflicts for the whole selected operation before any current read or
      prompt/service/model/mutation/POST work.
- [ ] Add exact-ID POST and exact-ID placed-prediction readback. Missing,
      duplicate, changed, or extra IDs fail closed; no team/text/form fallback
      exists.
- [ ] Prove complete-scope classification and hostile parser/readback behavior
      with encrypted/synthetic fixtures only; make no live request.

### R2b — Isolated typed persistence

- [ ] Implement exact epoch `bundesliga-2026-27-typed-v1` in the three frozen
      collections with repository construction bound to one authority only.
- [ ] Implement typed current/read/save/reprediction/copy operations and
      transactional duplicate/concurrency gates using the complete authority,
      item, snapshot, route, model, and provenance identity.
- [ ] Keep legacy APIs explicit and audit-only for current commands. Prove no
      cross-epoch query, fallback, migration, deletion, mutation, or backfill.
- [ ] Add separate configured reads for legacy and each typed Authority Epoch
      that each address one physical namespace and materialize explicitly
      authority-labelled non-current audit/cost DTOs. Forbid a cross-authority
      repository method, query, enumeration, current lookup, fallback, copy,
      or reprediction.

### R3a — Inventory, route, copy, factory, and audit kernel

- [ ] Add private-constructor validated match/bonus inventories created only
      by the inventory gate from exact authority, posting seed, same-scope
      expected keys, observed R2a snapshots, and the registered route catalog.
      Reject duplicate/missing/extra/cross-community/drifted items before any
      current, prompt, service, candidate, or mutation activity; allow only
      exactly empty/empty and order accepted items by Kicktipp ID.
- [ ] Register opt-in stable route selections containing the accepted Core
      route and optional copy contracts, Community Context, profile,
      generation-input contract, and pinned model. Accept no caller-created or
      default route/copy policy. Prepare typed current requests only from a
      validated item and exact registered selection ID.
- [ ] Read the actual typed source current row before compatibility and bind
      its prompt, model, route, context/profile, generation-input, and rules
      provenance to the registered source policy. Require the registered
      target selection and prepared authority, preserve exact R1 rejection
      before candidate read, and retain R2b save as the final drift guard.
      Map bonus selections in source-candidate order through the exact
      one-to-one option projection.
- [ ] Add only fixed factory seams for the dedicated typed Kicktipp client,
      typed repository, and four isolated audit readers. Registration is
      opt-in, idempotent, default-free, and unwired from commands.
- [ ] Fully materialize all four audit reads before pure combination and
      return no partial result on failure/cancellation. Use checked `long` and
      `decimal` arithmetic; zero all empty-subtotal values; expose token totals
      as null exactly when any contributing usage is unknown; derive overall
      values only from subtotals; reject duplicate identities, overflow,
      current claims, or label disagreement atomically; preserve immutable
      deterministic rows, labels, and per-authority subtotals.
- [ ] Run full Core and Orchestrator focused gates and obtain independent
      exact-commit acceptance before R3b.

### R3b — Observed call, context, and provenance seams

- [ ] Add opt-in OpenAiIntegration prompt requirements, resolved template, and
      observed provider APIs. Return exact template/path and immutable hosted
      or pinned-fallback evidence atomically; verify hosted name, numbered
      version, required label, and normalized readback hash; never consult or
      mutate last-prompt metadata. Preserve legacy interfaces.
- [ ] Add opt-in observed match/bonus prediction results containing a
      defensive prediction plus the same invocation's exact model, prompt,
      requested/final tier, fallback fact/reason, usage, and calculated cost.
      Construct only from that invocation's prompt/template pair, response
      usage, execution telemetry, and cost service. Missing evidence, usage,
      final tier, or cost fails without a partial result; cancellation
      propagates. Keep the observed service unwired from commands.
- [ ] Add the immutable Core Community Context/profile observation and the
      Orchestrator provenance assembler. Direct assembly accepts only prepared
      current identity, one complete observed result, bound context
      observation, time, and prediction identity/index. Copy assembly derives
      source prompt/model/service/identity from the accepted actual source
      row, binds target context, forces target usage/cost zero, and delegates
      to R1 validators. Accept no raw caller provenance fields.
- [ ] Run full Core, OpenAiIntegration, and Orchestrator focused gates, obtain
      independent exact-commit acceptance, and pass the combined R3 milestone
      gate before R4.

The exact dependency is `R3a -> R3b -> (R4a || R4b)`. Both R3 slices use one
writer/worktree because registration and tests overlap. All R3 APIs remain
opt-in, default-free, and unwired until R4.

### R4a — Match commands

- [ ] Move Matchday, RandomMatch, and VerifyMatchday, including applicable dev
      and copy wrappers, entirely to typed capabilities.
- [ ] Make RandomMatch classify its complete candidate scope before selection;
      make matchday/verify fail the complete selected scope on one unsupported
      item.
- [ ] Require exact-ID POST/readback and deterministic reconciliation on any
      partial or changed remote result.

### R4b — Bonus commands and P1-10 composition

- [ ] Move Bonus and VerifyBonus, including applicable dev and copy wrappers,
      entirely to typed capabilities and exact option-ID mappings.
- [ ] Compose P1-10's Schadensfresse rules-only DFB/CL routes on the shared
      kernel while preserving ADR-0058/0059/0060 preflight, provenance, and
      activation gates.
- [ ] In R4b add DFB/CL route IDs/contracts, fail-closed dispatch, and
      synthetic tests only. Add no prompt body or mirror, unverified hash
      assertion, or implied fallback. A later slice may add a mirror/test only
      after evidence records exact hosted name, numbered immutable version,
      normalized readback hash, and required `production` membership, then
      proves normalized mirror/readback equality.
- [ ] Reject partial option maps, text-only matches, unsupported mixed batches,
      legacy copy candidates, and every context/prompt fallback outside an
      accepted route.

### R5a — Deterministic tooling and cutover shape

- [ ] Add deterministic immutable generation tooling, validators, synthetic
      hostile fixtures, and pinning configuration for the exact data paths in
      the design. Do not add real evidence rows yet.
- [ ] Prove a same-ID reschedule preserves its Stable Local Item Key, creates a
      new additive seed generation and Snapshot Hash, and leaves the old
      snapshot non-current without rewriting the prior generation.
- [ ] Update workflow shape so every future Bundesliga current row selects the
      same typed epoch as one cutover unit. Keep recovery runtime active and
      unchanged.
- [ ] Pass focused and cohesive repository gates plus independent exact-SHA
      review and exact-head CI before requesting real evidence authority.

### R5b — Real evidence, staging, and cutover

- [ ] Collect payload-safe authenticated complete inventories for every
      Posting Community and exact source/posting Copy Binding, including every
      bonus option mapping. Check in reviewed immutable generations only.
- [ ] Reconcile pinned prompts, contexts/rules, existing legacy rows, exact
      replacement set, earliest cutoff, maximum calls/cost, force limits, and
      rollback ownership under the existing Owner gates.
- [ ] Create and verify the complete typed prediction set in the isolated epoch
      without POST. No mixed/missing item may pass staging.
- [ ] Obtain Owner approval and execute the all-community atomic runtime/storage
      cutover protocol. After any typed POST, disable and reconcile before any
      rollback; never mix authorities.
- [ ] Observe the first natural run on the exact deployed SHA. Keep P1-10's
      separate target-primary activation evidence and final ADR-0062
      replacement/termination decision.

## Required validation

R0 requires link and scope checks, accepted-ADR immutability, glossary purity,
terminology consistency, `git diff --check`, sensitive-token review, a scoped
exact commit, and independent Sol/high review. It requires no build, test,
external fetch, or live operation.

Focused implementation gates are Core for R1, KicktippIntegration for R2a,
FirebaseAdapter for R2b, full Core/Orchestrator for R3a, full Core/
OpenAiIntegration/Orchestrator for R3b, Orchestrator match tests for R4a,
Orchestrator bonus plus ContextProviders tests for R4b, and workflow-contract/
`actionlint` gates for R5a. Both R3 focused gates and the combined R3 review
must pass before either R4 slice starts.

The cohesive gate runs TUnit with `dotnet run` for Core,
KicktippIntegration, FirebaseAdapter, OpenAiIntegration,
ContextProviders.Kicktipp, Orchestrator, and Integration; a Release build;
workflow-contract validation; `actionlint`; independent exact-SHA scope,
security, authority-isolation, and rollback review; and exact-head
Build-and-Test CI.

## Complete when

- Every current-authoritative call surface uses the exact P1-13 typed boundary
  and every historical/context/audit/cost surface is unable to feed it.
- Audit/cost reads remain physically isolated by configured authority and
  expose only labelled non-current DTOs; shared totals preserve authority
  labels and per-authority subtotals after independent retrieval.
- Every Posting Community has a complete pinned immutable identity generation;
  every copy row has a complete one-to-one binding and compatibility decision.
- Typed match, bonus, copy, and reprediction rows have complete immutable
  provenance and reside only in one physically/query-isolated epoch.
- The all-community cutover and exact-ID readback pass under Owner authority,
  with rollback and natural-run evidence recorded.
- P1-10 can complete target-owned Schadensfresse composition and activation
  without owning or bypassing the global foundation.
- Scoped commits are reviewed, the final exact head is green, and changes are
  pushed only through the explicit approved remote/branch topology.
