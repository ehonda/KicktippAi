# Validation and integration protocol

This protocol uses the stable role IDs from `roles-and-handoffs.md`. It is the
canonical validation path; role definitions are not repeated here.

## Freeze validation ownership

Before admitting a `milestone-writer`, the frozen milestone records:

- owned paths and expected semantic behavior;
- changed projects/components and directly affected test projects;
- regression tests and cross-project/contract gates;
- whether shared/central/build-graph impact requires a solution build;
- integration branch/order and downstream release dependencies; and
- where full command output and concise handoff evidence are stored.

A vague "static checks only" instruction is not a valid validation contract for
code. Documentation-only work may substitute applicable lint, link, schema, or
generation checks.

## Writer loop

The `milestone-writer` owns edit/build/test/fix inside its isolated worktree.
Use focused tests during iteration. Before handoff it must:

1. build every changed project;
2. run the complete directly affected test-project suites rather than only
   selected passing tests;
3. run frozen regression and cross-project contract tests; and
4. run a solution build when the change touches shared/central code, dependency
   wiring, build configuration, or the project graph.

Record exact commands, exit codes, relevant configuration, and concise results.
A failing required gate remains writer work within its correction budget unless
the evidence shows an external/infrastructure or cross-lane cause.

## Implementation review

A fresh `implementation-reviewer` receives the exact writer tip/diff, frozen
contract and validation evidence. It remains read-only for tracked source.
Findings return to the same writer through a mechanically reserved correction
turn. The reviewer may check its own findings at most twice but cannot provide
final acceptance.

The root accepts a lane for integration only when required writer evidence is
present, implementation-review findings are closed, the worktree is clean, and
the exact tip is recorded.

## Serialized integration

The root integrates accepted lane commits into the run integration branch in
semantic dependency order. Integration is a control-plane Git operation, not a
review or validation verdict. Preserve exact input and resulting SHAs and do
not publish an incoherent or unbuildable intermediate tip.

## Cumulative validation and bounded triage

After a coherent frozen wave/batch, a `cumulative-validator-triage` validates
the exact combined tip with:

- a diagnostic bounded solution build;
- the union of affected full test-project suites;
- cross-project/contract/regression gates; and
- any repository/package/lint checks required by the frozen graph.

The same role performs bounded triage so it already has the command context and
full output. Save full logs outside capsule and automatic recovery context.
Classify a failure as infrastructure, compiler/build, test/regression,
cross-lane integration, unattributed non-architectural, or architectural.

The role may inspect diffs and identify a straightforward owner but does not
edit tracked source or commit. Return an attributed defect to its writer within
budget. Use a fresh `deep-diagnosis` only for difficult/unattributed failures,
passing the preserved evidence so it need not repeat the full gate. Use
`architecture-reconciliation` only for a genuinely new cross-cutting seam,
invariant, or continuity problem.

Cumulative validation must pass before:

- a dependent downstream wave starts;
- a draft PR is created or updated with the candidate; or
- final acceptance begins.

Independent non-dependent lanes may continue while an affected lane is
diagnosed or corrected.

## Final acceptance, publication, and CI

A fresh `final-acceptance-reviewer` receives only the exact combined tip/full
diff, frozen contract/invariants, cumulative evidence, and closed-findings
checklist. It must be independent of all earlier roles in the milestone and
must not be primed with an intended result.

After acceptance, the root publishes that exact candidate. Create the first
draft PR after the first accepted, buildable, coherent milestone and before its
dependent next wave. CI must evaluate the exact published head. One eligible
failed workflow may be rerun; a repeat failure goes to diagnosis rather than
another retry.

A later source change invalidates final acceptance and any validation evidence
whose tested surface it changes. Re-run only the gates made stale by the new
diff, then obtain fresh final acceptance of the new exact tip.
