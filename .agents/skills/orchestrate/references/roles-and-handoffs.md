# Orchestration roles and handoffs

These stable role IDs are canonical. An assignment names exactly one role and
must not silently expand it. Every task agent receives the exact run ID,
control-state/capsule/preview paths, frozen contract, owned paths, authority,
validation duty, evidence destination, and handoff target. Only the root edits
run state.

## Shared lifecycle rules

- Unless a role explicitly says otherwise, it has no tracked-source mutation,
  commit, publication, production, spending, destructive, or other external
  effect authority; its correction/follow-up budget is zero; and it is released
  after its terminal handoff. A task-role assignment is the only authority for
  its named inputs and owned surfaces.
- Every task-role output identifies the exact assignment and candidate/evidence
  paths, records commands and limits relevant to its verdict, and ends at the
  handoff named below. Missing evidence is a finding, not permission to expand
  scope.
- A material role change releases the old thread and uses a fresh assignment.
- Retention requires a near-term continuity reason, release trigger, and
  observable context-cost proxy. Acceptance, re-freeze, review-surface change,
  the trigger, or the proxy limit makes the agent reclaimable.
- Do not retain a specialist as insurance when it blocks a useful ready lane.
- A terminal agent is reclaimable only when idle and mailbox-clean. Runtime
  eviction requires runtime evidence; orchestration intent is not proof of
  unload or process cleanup.
- On thread-capacity rejection, inspect live state and retry once only after a
  known mailbox blocker is consumed through one bounded `followup_task`.
- Models below are starting points. Every override-compatible spawn explicitly
  sets both model and reasoning effort.

## `owner`

- **Model/effort:** not applicable; this is the repository owner in the root
  user-facing conversation.
- **Purpose:** supplies the objective, semantic/product decisions, and any new
  authority the workflow cannot infer.
- **Owns:** scope changes, destructive/external/live decisions, production
  activation, spending, and choices between materially different outcomes.
- **Does not own:** routine read-only diagnosis, bounded local builds/tests,
  ordinary in-scope corrections, or scheduling within an accepted graph.
- **Inputs and effects:** receives one precise decision/authority question; may
  authorize only the stated semantic choice or external effect.
- **Evidence/budget/reuse:** the exact answer is evidence; questions are not
  correction turns, and the owner is consulted again only for a new genuine
  owner gate.
- **Handoff:** answers a gate precisely; the root records the decision and
  clears or narrows the gate through a checkpoint.

## `root-orchestrator`

- **Model/effort:** the current root session configuration; it is never spawned
  as a task role.
- **Purpose:** sole control plane and user-facing coordinator.
- **Owns:** intake, decomposition, model/agent allocation, scheduling,
  cross-lane scope, state/checkpoints, worktree/resource reservations,
  integration order, publication, CI gates, and user communication.
- **May do inline:** small read-only routing/integration checks, primary-checkout
  worktree setup, serialized Git operations, and substantive work that cannot
  reasonably be delegated after recording why.
- **Prohibited:** silently assuming a task-agent role, editing inside another
  writer's ownership, or treating compaction/agent delay as ownership transfer.
- **Inputs/effects:** receives the objective and normative workflow; may perform
  the bounded Git/GitHub effects in the operations protocol only after their
  exact gates pass.
- **Evidence/budget/reuse:** owns checkpoint, integration, publication, and user
  evidence; has no correction-turn budget and persists only for this run.
- **Handoff:** supplies bounded role contracts and accepts/rejects returned
  evidence; only the root advances lifecycle state.

## `intake-research`

- **Model:** `gpt-5.6-luna/medium` for mechanical bounded discovery;
  `gpt-5.6-terra/medium` for broader bounded exploration; prefer
  `gpt-5.6-sol/high` when open-ended conclusions will guide design.
- **Purpose:** gather repository/external facts for preview without making
  owner decisions.
- **Inputs:** exact questions, source boundaries, and evidence requirements.
- **Owned surfaces/effects:** read-only repository or approved external research
  inside those boundaries; no tracked mutation or external state change.
- **May:** read/search and return concise sourced findings.
- **Prohibited:** implementation, state edits, architecture acceptance,
  scheduling, or publication.
- **Handoff:** facts, uncertainties, and decision implications to root or
  architecture/specification.
- **Evidence/budget/reuse:** cite paths/sources and confidence; no corrective
  follow-up budget. Reuse only for the same bounded research role under a
  recorded retention contract.

## `architecture-lead`

- **Model:** `gpt-6-astra/high`, only for genuinely phase-wide or cross-cutting
  architecture.
- **Purpose:** derive seam map, invariants, non-goals, semantic dependency
  graph, independently acceptable milestones, downstream-release matrix, owned
  surfaces, and verification strategy.
- **Inputs:** whole objective and durable evidence; exclude transient machine
  capacity.
- **Owned surfaces/effects:** the assigned architecture packet only; tracked
  design-file edits require explicit path ownership and never authorize
  publication or live effects.
- **Prohibited:** root scheduling/publication, implementation, final acceptance,
  or using Astra for a local defect/mechanical task.
- **Handoff:** architecture packet to a different `specification-reviewer`.
- **Evidence/budget/reuse:** identify sources, assumptions, seams, and unresolved
  choices; findings return as a new architecture assignment, not a writer
  correction. Retention follows the shared recorded contract and unchanged role.

## `specification-reviewer`

- **Model:** `gpt-5.6-sol/xhigh`.
- **Purpose:** independently test architecture/specification completeness,
  consistency, ownership, continuity, and milestone independence.
- **Inputs:** exact architecture packet and evidence, not an intended verdict.
- **Owned surfaces/effects:** read-only review plus bounded local checks; no
  tracked or external mutation.
- **Prohibited:** authoring the packet under review, implementation, or final
  milestone acceptance.
- **Handoff:** accepted packet or bounded findings to architecture/root.
- **Evidence/budget/reuse:** return a requirement-keyed finding/clearance list;
  no incremental follow-up budget. A revised material packet gets a fresh
  specification reviewer.

## `milestone-writer`

- **Model:** `gpt-5.6-terra/medium`; use `high` for substantial ambiguity,
  integration risk, or difficult edge cases.
- **Purpose:** implement one frozen milestone in one admitted worktree.
- **Owns:** assigned paths, focused iterative tests, required pre-handoff build
  and tests, local fixes, and concise evidence.
- **Inputs/effects:** frozen milestone/validation contract and admitted
  worktree; may mutate only owned paths and local build artifacts, with no
  external effects, integration, or publication.
- **Budget:** initial assignment plus at most three reserved correction turns.
- **Prohibited:** editing run state, expanding scope, changing role, integrating
  other lanes, publication, or recursively delegating without explicit root
  authority.
- **Handoff:** exact branch/tip, scoped diff, build/test evidence, known limits,
  and clean status to `implementation-reviewer` through root.
- **Retention/reuse:** retain only for an imminent correction on the same
  milestone; acceptance, diagnosis routing, or the recorded proxy limit releases
  it.

## `implementation-reviewer`

- **Model:** default `gpt-5.6-sol/xhigh`. `sol/high` is allowed only when the
  frozen preview records a bounded exact tip/paths, deterministic criteria, and
  no open ADR, invariant, ownership, architecture, or continuity question.
- **Purpose:** independent correctness, regression, security, and contract
  review of one writer milestone.
- **Inputs/owned surfaces/effects:** exact tip/diff, frozen contract, and writer
  evidence; read-only source inspection and bounded local validation only.
- **May:** inspect code/evidence and run bounded validation; remains read-only
  for tracked source.
- **Budget:** at most two incremental follow-ups on its own findings.
- **Prohibited:** fixing findings, granting final acceptance, or reviewing work
  it designed/wrote.
- **Handoff:** actionable findings or implementation-review clearance to the
  same writer/root.
- **Evidence/reuse:** return a closed-findings checklist tied to exact lines and
  commands. The two follow-ups may verify only its own unchanged findings;
  material surface or role changes require a fresh reviewer.

## `cumulative-validator-triage`

- **Model:** `gpt-5.6-sol/high`.
- **Purpose:** validate the exact combined integration tip and perform bounded
  first-line failure attribution.
- **Inputs/owned surfaces/effects:** exact combined tip, frozen union of gates,
  and evidence destination; owns untracked validation logs only and has no
  external effect.
- **May:** build/test, preserve full logs outside recovery context, inspect
  diffs and attribute straightforward infrastructure/compiler/test/cross-lane
  failures.
- **Prohibited:** tracked-source edits, commits, repeating already sufficient
  gates, or resolving architectural uncertainty.
- **Handoff:** passing evidence, an attributed correction for a writer, or a
  minimal evidence packet for `deep-diagnosis`/architecture.
- **Budget/reuse:** one cumulative pass plus bounded attribution of that pass;
  no corrective follow-up turns. A changed tip receives a fresh validation
  assignment, which may reuse the role only when the recorded evidence is still
  applicable.

## `deep-diagnosis`

- **Model:** `gpt-5.6-sol/high`.
- **Purpose:** resolve difficult, unattributed, or non-architectural failures.
- **Inputs:** preserved command/log evidence, exact tip/diff, and a bounded
  diagnostic question.
- **Owned surfaces/effects:** read-only source and the smallest untracked local
  diagnostic artifacts; no tracked or external mutation.
- **May:** read and run only the smallest missing diagnostic needed.
- **Prohibited:** automatically rerunning the full cumulative gate, fixing
  tracked source, or deciding architecture.
- **Handoff:** causal diagnosis and the narrow owning correction route.
- **Evidence/budget/reuse:** return cause, disconfirming evidence, and the
  minimal stale/missing gate; no correction turns and release after diagnosis.

## `architecture-reconciliation`

- **Model:** fresh `gpt-6-astra/high`, only when diagnosis reveals a genuinely
  new cross-cutting seam, invariant, or continuity issue; followed by a fresh
  `specification-reviewer`.
- **Purpose:** repair invalidated architecture and re-freeze affected lanes.
- **Inputs/owned surfaces/effects:** preserved diagnosis plus affected frozen
  graph; owns only an explicitly assigned revised architecture packet and has no
  external effect or implementation authority.
- **Prohibited:** local defect repair or reuse of the original architecture
  thread in a materially changed role.
- **Handoff:** revised bounded graph and downstream-release consequences.
- **Evidence/budget/reuse:** identify the new cross-cutting fact and every
  invalidated seam; no correction-turn budget and no reuse of the prior
  architecture thread.

## `final-acceptance-reviewer`

- **Model:** fresh `gpt-5.6-sol/xhigh` by default, with the same explicitly
  recorded deterministic `sol/high` exception as implementation review.
- **Purpose:** decide final acceptance of the exact combined candidate.
- **Inputs:** exact tip/full diff, frozen contract/invariants, validation
  evidence, and closed-findings checklist; omit prior conversation and intended
  conclusion.
- **Independence:** no architecture, specification, writing, reconciliation,
  incremental-review, or cumulative-triage role in that milestone.
- **Owned surfaces/effects:** read-only exact-candidate review and bounded local
  checks; no tracked change, publication, CI mutation, or other external effect.
- **Prohibited:** source fixes or accepting another tip.
- **Handoff:** acceptance or bounded findings to root.
- **Evidence/budget/reuse:** return an invariant-keyed verdict and stale-evidence
  check; no follow-up/correction budget. Any changed candidate requires a fresh
  final reviewer.

## `ci-status-monitor`

- **Model:** `gpt-5.6-luna/low`.
- **Purpose:** mechanical exact-SHA status, check, and log collection.
- **Inputs/owned surfaces/effects:** allowlisted repository, PR/workflow IDs,
  exact SHA, and root-authorized action; may read remote state and perform only
  the single explicitly authorized eligible rerun/cancellation.
- **Prohibited:** interpreting ambiguous failures beyond mechanical
  classification, source changes, rerun beyond the authorized single attempt,
  or publication decisions.
- **Handoff:** exact workflow/check identifiers, SHA, outcome, and relevant log
  locations to root or diagnosis.
- **Evidence/budget/reuse:** one collection assignment and at most the one
  authorized rerun; no interpretive correction turns. Release after the exact
  SHA reaches a terminal status.
