# Bundesliga P1-13 global typed authority execution packet

- Status: Frozen R0 packet; R1+ blocked until independent acceptance of the
  exact R0 commit
- Objective: establish season-wide typed prediction authority and an isolated,
  all-community cutover foundation without changing recovery production
- Authority: [ADR-0065](decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md)
- Design: [P1-13 global authority design](designs/p1-13-global-typed-prediction-authority-and-cutover.md)
- Task: [P1-13](tasks/p1-13-global-bundesliga-prediction-authority.md)

## Frozen boundary

ADR-0062 recovery `main`, its eight-pair/16-job topology, absolute
`2026-09-08T12:00:00Z` sunset, pair-local re-quarantine, whole-cron rollback,
and external authority remain unchanged. The P1-10 draft branch is not merge
ready. P1-13 is a new global dependency: it owns shared typed authority and
cutover; P1-10 retains Schadensfresse rules, contexts, routes, prompt
promotion, replacement/cost/cutoff decisions, and primary activation.

R0 is documentation only. It makes no runtime, workflow, data-seed, prompt,
model, prediction, Firestore, Kicktipp, Langfuse, credential, or schedule
change. R1-R5a use synthetic evidence and local repository validation. Real
authenticated generations, production staging, or cutover begin only at R5b
after the existing Owner gates.

The canonical current authority binds Posting Community and
Prediction-source Community; Community Context; Stable Local Item Key and
Snapshot Hash; Identity Seed Generation; Copy Binding; Generation Provenance;
and one Authority Epoch.
A Legacy Row is never a Typed Current Prediction.
**Prediction-source Community**: The community under which the candidate
prediction was generated and stored. It equals the Posting Community for
self-contained generation; for an accepted copy it may differ and is
identified by the Copy Binding.

## Frozen call-surface decision

| Class | In scope | Authority rule |
|---|---|---|
| Current match | Matchday, RandomMatch, VerifyMatchday, dev/copy wrappers, typed match repository and exact-ID Kicktipp operations | Complete posting-community inventory, pinned seed, one typed epoch, exact current/save/repredict/copy, exact-ID POST/readback only |
| Current bonus | Bonus, VerifyBonus, dev/copy wrappers, typed bonus repository and exact-ID Kicktipp operations | Same, plus complete question snapshot and one-to-one option-ID projection |
| Historical/context | Collection, outcomes/history/context, explicit historical reconstruction/inventory | May read labelled legacy/typed evidence; cannot select current, copy, repredict, mutate, or post |
| Audit/cost/experiments | Cost, inventory, export/prepare/analysis, available-value discovery | Separate configured reads each materialize explicitly authority-labelled non-current DTOs from one physical namespace; only a later shared combiner may sort/combine/total after retrieval while preserving labels and per-authority subtotals |
| Other competitions | Existing WM26/other APIs | Remain isolated; never accept P1-13 subcompetition, epoch, seed, or binding |

Team/time, team-only, question-text, form-name, prefix, substring, newest,
partition-only, and default lookup are never current-authoritative. Any
unsupported or unbound item fails the complete selected command scope before
current database selection or any prompt/model/mutation/POST work.

The canonical scheduled instant for a match comes only from exact ID-bearing
fixture evidence plus the same-ID structured detail `Termin`. Cancelled or
empty evidence, inherited prior-row state, `Instant.MinValue` or another
sentinel, missing/duplicate/unparsable detail, and fixture/detail conflict fail
the whole selected operation before current read, prompt/service/model call,
mutation, or POST. A same-ID reschedule preserves the Stable Local Item Key but
creates a new additive seed generation and Snapshot Hash; the prior snapshot
is not current.

No repository method, query, enumeration, current lookup, fallback, copy, or
reprediction spans authorities. Combined audit/cost output remains non-current
and cannot be converted back into a current row.

## Dependency graph and admission

```text
R0 -> R1 -> (R2a || R2b) -> R3a -> R3b -> (R4a || R4b) -> R5a
                                                                  -> Owner/evidence gates
                                                                  -> R5b -> review/CI
                                                                  -> Owner atomic cutover
                                                                  -> natural-run evidence
```

- R0 exact commit receives independent Sol/high review before R1.
- R2a/R2b start only after R1's canonical contracts are frozen and may run in
  parallel with disjoint paths.
- R3a integrates inventory, registered route/copy, fixed factories, and audit
  combination. R3b then adds observed prompt/service results, bound context,
  and provenance assembly. Both slices pass review before R4a/R4b start.
- R4a/R4b may run in parallel with disjoint match/bonus command ownership.
- R5a freezes deterministic tooling and future workflow shape without real
  evidence or production mutation.
- R5b starts only after all repository gates and later Owner/evidence gates.
- At most two writable worktrees and one global heavy-operation lease are
  admitted. Root owns integration, publication, and cross-lane decisions.

## Path ownership

| Milestone | Owned paths |
|---|---|
| R0 | ADR-0065, season `CONTEXT.md`, P1-13 design/task/packet, and the exact linkage/current-contract documents listed in the orchestration re-freeze |
| R1 | `src/Core`, `tests/Core.Tests`, and synthetic schema fixtures; no real `data/` generation |
| R2a | `src/KicktippIntegration`, `tests/KicktippIntegration.Tests`, encrypted/synthetic fixtures only |
| R2b | `src/FirebaseAdapter`, `tests/FirebaseAdapter.Tests`, isolated configured audit/cost reads, and authority-labelled non-current DTOs |
| R3a | Core inventory proof; Orchestrator registered route/copy/audit services; fixed Firebase/Kicktipp factory methods; registration; corresponding Core/Orchestrator tests |
| R3b | Core context observation; opt-in OpenAiIntegration prompt/service evidence and tests; observed Langfuse prompt implementation; OpenAI factory method; Orchestrator provenance assembler and serialized registration/tests |
| R4a | Matchday, RandomMatch, VerifyMatchday command paths and their Orchestrator tests |
| R4b | Bonus, VerifyBonus, P1-10 DFB/CL route IDs/contracts, fail-closed dispatch, and synthetic Orchestrator/ContextProviders tests; no prompt bodies/mirrors/hash claims/fallbacks |
| R5a | deterministic seed/binding tooling, synthetic fixtures, configuration/workflow shape, workflow-contract tests |
| R5b | reviewed real files below `data/bundesliga-2026-27/prediction-authority`, isolated staging evidence, and approved cutover artifacts |

One writer owns a path at a time. R3a and R3b deliberately use one writer and
one worktree in sequence because their Orchestrator registration and tests
overlap. A new cross-cutting invariant, missing ADR, or scope expansion pauses
the affected lane for architecture recall, independent review, and re-freeze.

## Corrected R3 implementation boundary

The exact graph is `R3a -> R3b -> (R4a || R4b)`. R3a first creates immutable
validated match/bonus inventories only through the inventory gate. The gate
takes exact authority, posting seed, expected keys from the same R2a scope,
observed snapshots, and registered route catalog; rejects duplicate expected
or observed keys before comparison; requires exact community/seed, one-to-one
scope, canonical snapshot bytes/hash, kind, subcompetition, and route; orders
by Kicktipp ID; and accepts only exactly empty/empty. Raw snapshots cannot
enter current or copy APIs.

Registered route selections bind stable selection ID, accepted route contract,
Community Context, profile, generation-input contract, pinned model, and
optional copy contract. Selection and current preparation accept no default or
caller-created route/policy. Copy planning reads the exact typed source current
row before compatibility and binds that actual row's prompt, model, route,
context/profile, generation-input, and rules identity/hash to the registered
source policy. It binds target context/profile to the prepared target
authority, preserves R1 rejection before candidate read, verifies an accepted
candidate against the source payload/provenance/decision fingerprint, and
leaves R2b save as the last drift guard. Bonus mapping preserves selected
source-option order through the exact accepted projection.

R3a exposes only fixed factory methods for the dedicated typed Kicktipp client,
typed repository, and four separate legacy/typed match/bonus audit readers.
There is no generic authority, epoch, community, collection, list, cast, or
fallback seam. Registration is opt-in, idempotent, default-free, and unwired.
The audit reader materializes all four reads before combining and returns no
partial report on failure/cancellation. Counts, known tokens, unknown counts,
and decimal costs use checked arithmetic. Empty authority subtotals are all
zero. A token total is null exactly when any contributing value is unknown.
Overall values are checked sums of subtotals, and overall cost is never
recomputed from rows. Label disagreement, current claims, overflow, and
duplicate `(authority, collection, documentId)` fail atomically; output is
immutable, labelled, deterministically sorted, subtotalled, and non-current.

R3b adds opt-in OpenAiIntegration prompt requirements, resolved templates, and
observed prompt provider APIs. Each observed resolution returns exact
template/path and immutable prompt provenance atomically. Hosted evidence must
match exact name, numbered immutable version, label, and normalized readback
hash; fallback must match the exact pinned file/hash. It never uses mutable
last-prompt state, and legacy interfaces remain unchanged.

The opt-in observed prediction service returns an immutable defensive match or
bonus prediction together with that same invocation's exact model and prompt,
requested/final service tier, fallback fact/reason, response usage, and
calculated cost. It derives only from that invocation's resolved prompt pair,
response, telemetry, and cost service—not tracker/provider last-call state.
Missing prompt evidence, usage, final tier, or cost yields no partial result;
cancellation propagates. `PredictionService` and the OpenAI factory may expose
the new capability, but R3 wires no command to it.

Core's immutable context observation binds exact Community Context and profile
to context provenance. Orchestrator owns provenance assembly. Direct assembly
validates one complete observed result against the registered selection and
the context observation against prepared current authority. Copy assembly
derives prompt/model/service/source identity from the accepted actual source
row, binds target context and new target identity/time/index, forces target
usage/cost to zero, and delegates to R1 validators. Neither accepts raw caller
prompt, service, usage, or provenance. Full Core and Orchestrator focused gates
close R3a; full Core, OpenAiIntegration, and Orchestrator gates close R3b. Both
reviewed slices and the combined milestone gate are prerequisites to R4.

All R3 APIs are opt-in and unwired. R3 changes no command, actual DFB/CL route,
prompt body or mirror, real seed/binding or other data, workflow, WM26 behavior,
production/live state, staging, cutover, or rollback. It does not modify R1/R2
provider implementations or create a new Owner/ADR decision.

R4b cannot create a DFB/CL checked-in prompt mirror. A later evidence slice may
do so only after recording the exact hosted name, numbered immutable version,
normalized readback hash, and required `production` membership; its test must
then prove normalized mirror/readback equality. Until then the route has no
fallback and dispatch remains fail closed.

## Production-continuity and cutover gate

The typed epoch is `bundesliga-2026-27-typed-v1` in the exact three collections
named by ADR-0065. Recovery runtime cannot read it; draft typed runtime cannot
read legacy collections. Legacy records are preserved unchanged.

Before cutover, require complete authenticated identity/binding coverage,
pinned prompt and immutable context/rules readback, approved replacement set,
calls/cost/force/cutoff, complete no-POST typed staging, no active/pending
affected workflow, exact-SHA review/CI, payload-safe Kicktipp/storage baseline,
and named rollback owner. Cutover deploys runtime/workflow and storage
authority as one all-community unit. Git merge alone is not cutover.

Before a typed POST, the complete operation may return to ADR-0062 recovery.
After a typed POST, disable the affected lane and reconcile exact Kicktipp and
typed storage before legacy posting can resume. Schadensfresse re-quarantine
preserves seven unaffected pairs; lane-wide defects invoke the inherited
whole-cron disablement. Partial or mixed authority is prohibited.

## Gates

R0:

- exact owned-path scope and valid relative links;
- ADR-0058/0059/0060/0062 byte-identical;
- glossary contains only canonical domain language, with no APIs, paths,
  schemas, hostile examples, or rollout mechanics;
- ADR/design/task/packet and linkage terminology agree;
- `git diff --check` and sensitive-token review pass;
- one scoped local commit and independent Sol/high exact-commit verdict.

R1-R5a focused gates follow the task/design ownership table. R3a runs full
Core and Orchestrator focused gates; R3b runs full Core, OpenAiIntegration, and
Orchestrator focused gates. Both receive independent review, then pass their
combined milestone gate before R4. The cohesive repository gate is Release
build; Core, KicktippIntegration, FirebaseAdapter, OpenAiIntegration,
ContextProviders.Kicktipp, Orchestrator, and Integration TUnit projects via
`dotnet run`; workflow contracts; `actionlint`; exact-SHA scope/security/
authority/rollback review; and exact-head Build-and-Test CI.

R5b/cutover adds authenticated payload-safe inventories, real immutable
generation/binding coverage, production staging and exact-ID readback,
Owner-approved external authority, and first-natural-run evidence.

## Publication topology

R0 is a local scoped commit followed by independent review; root decides its
integration into the existing draft branch. R1+ implementation remains on the
draft PR topology while it is not production-safe. Keep ordinary lane commits
local and publish cohesive reviewed milestones under the existing explicit
remote/branch allowlist. No force push, history rewrite, tag, release, merge,
or production activation is authorized by this packet.
