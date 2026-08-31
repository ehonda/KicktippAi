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

## Frozen call-surface decision

| Class | In scope | Authority rule |
|---|---|---|
| Current match | Matchday, RandomMatch, VerifyMatchday, dev/copy wrappers, typed match repository and exact-ID Kicktipp operations | Complete posting-community inventory, pinned seed, one typed epoch, exact current/save/repredict/copy, exact-ID POST/readback only |
| Current bonus | Bonus, VerifyBonus, dev/copy wrappers, typed bonus repository and exact-ID Kicktipp operations | Same, plus complete question snapshot and one-to-one option-ID projection |
| Historical/context | Collection, outcomes/history/context, explicit historical reconstruction/inventory | May read labelled legacy/typed evidence; cannot select current, copy, repredict, mutate, or post |
| Audit/cost/experiments | Cost, inventory, export/prepare/analysis, available-value discovery | May aggregate all authority classes; cannot return a current row to a production command |
| Other competitions | Existing WM26/other APIs | Remain isolated; never accept P1-13 subcompetition, epoch, seed, or binding |

Team/time, team-only, question-text, form-name, prefix, substring, newest,
partition-only, and default lookup are never current-authoritative. Any
unsupported or unbound item fails the complete selected command scope before
current database selection or any prompt/model/mutation/POST work.

## Dependency graph and admission

```text
R0 -> R1 -> (R2a || R2b) -> R3 -> (R4a || R4b) -> R5a
                                                        -> Owner/evidence gates
                                                        -> R5b -> review/CI
                                                        -> Owner atomic cutover
                                                        -> natural-run evidence
```

- R0 exact commit receives independent Sol/high review before R1.
- R2a/R2b start only after R1's canonical contracts are frozen and may run in
  parallel with disjoint paths.
- R3 integrates the provider/persistence seams before R4a/R4b start.
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
| R2b | `src/FirebaseAdapter`, `tests/FirebaseAdapter.Tests` |
| R3 | shared route/provenance/copy-policy kernel in Core/Orchestrator and narrowly owned focused tests |
| R4a | Matchday, RandomMatch, VerifyMatchday command paths and their Orchestrator tests |
| R4b | Bonus, VerifyBonus, P1-10 DFB/CL composition and their Orchestrator/ContextProviders tests |
| R5a | deterministic seed/binding tooling, synthetic fixtures, configuration/workflow shape, workflow-contract tests |
| R5b | reviewed real files below `data/bundesliga-2026-27/prediction-authority`, isolated staging evidence, and approved cutover artifacts |

One writer owns a path at a time. A new cross-cutting invariant, missing ADR,
or scope expansion pauses the affected lane for architecture recall,
independent review, and re-freeze.

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

R1-R5a focused gates follow the task/design ownership table. The cohesive
repository gate is Release build; Core, KicktippIntegration, FirebaseAdapter,
OpenAiIntegration, ContextProviders.Kicktipp, Orchestrator, and Integration
TUnit projects via `dotnet run`; workflow contracts; `actionlint`; exact-SHA
scope/security/authority/rollback review; and exact-head Build-and-Test CI.

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
