# Orchestration roles and handoffs

These stable role IDs are canonical. An assignment names exactly one role and
must not silently expand it. Every task agent receives the exact run ID,
control-state/capsule/preview paths, frozen contract, owned paths, authority,
validation duty, evidence destination, and handoff target. Only the root edits
run state.

## Shared lifecycle rules

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

- **Purpose:** supplies the objective, semantic/product decisions, and any new
  authority the workflow cannot infer.
- **Owns:** scope changes, destructive/external/live decisions, production
  activation, spending, and choices between materially different outcomes.
- **Does not own:** routine read-only diagnosis, bounded local builds/tests,
  ordinary in-scope corrections, or scheduling within an accepted graph.
- **Handoff:** answers a gate precisely; the root records the decision and
  clears or narrows the gate through a checkpoint.

## `root-orchestrator`

- **Purpose:** sole control plane and user-facing coordinator.
- **Owns:** intake, decomposition, model/agent allocation, scheduling,
  cross-lane scope, state/checkpoints, worktree/resource reservations,
  integration order, publication, CI gates, and user communication.
- **May do inline:** small read-only routing/integration checks, primary-checkout
  worktree setup, serialized Git operations, and substantive work that cannot
  reasonably be delegated after recording why.
- **Prohibited:** silently assuming a task-agent role, editing inside another
  writer's ownership, or treating compaction/agent delay as ownership transfer.
- **Handoff:** supplies bounded role contracts and accepts/rejects returned
  evidence; only the root advances lifecycle state.

## `intake-research`

- **Model:** `gpt-5.6-luna/medium` for mechanical bounded discovery;
  `gpt-5.6-terra/medium` for broader bounded exploration; prefer
  `gpt-5.6-sol/high` when open-ended conclusions will guide design.
- **Purpose:** gather repository/external facts for preview without making
  owner decisions.
- **Inputs:** exact questions, source boundaries, and evidence requirements.
- **May:** read/search and return concise sourced findings.
- **Prohibited:** implementation, state edits, architecture acceptance,
  scheduling, or publication.
- **Handoff:** facts, uncertainties, and decision implications to root or
  architecture/specification.

## `architecture-lead`

- **Model:** `gpt-6-astra/high`, only for genuinely phase-wide or cross-cutting
  architecture.
- **Purpose:** derive seam map, invariants, non-goals, semantic dependency
  graph, independently acceptable milestones, downstream-release matrix, owned
  surfaces, and verification strategy.
- **Inputs:** whole objective and durable evidence; exclude transient machine
  capacity.
- **Prohibited:** root scheduling/publication, implementation, final acceptance,
  or using Astra for a local defect/mechanical task.
- **Handoff:** architecture packet to a different `specification-reviewer`.

## `specification-reviewer`

- **Model:** `gpt-5.6-sol/xhigh`.
- **Purpose:** independently test architecture/specification completeness,
  consistency, ownership, continuity, and milestone independence.
- **Inputs:** exact architecture packet and evidence, not an intended verdict.
- **Prohibited:** authoring the packet under review, implementation, or final
  milestone acceptance.
- **Handoff:** accepted packet or bounded findings to architecture/root.

## `milestone-writer`

- **Model:** `gpt-5.6-terra/medium`; use `high` for substantial ambiguity,
  integration risk, or difficult edge cases.
- **Purpose:** implement one frozen milestone in one admitted worktree.
- **Owns:** assigned paths, focused iterative tests, required pre-handoff build
  and tests, local fixes, and concise evidence.
- **Budget:** initial assignment plus at most three reserved correction turns.
- **Prohibited:** editing run state, expanding scope, changing role, integrating
  other lanes, publication, or recursively delegating without explicit root
  authority.
- **Handoff:** exact branch/tip, scoped diff, build/test evidence, known limits,
  and clean status to `implementation-reviewer` through root.

## `implementation-reviewer`

- **Model:** default `gpt-5.6-sol/xhigh`. `sol/high` is allowed only when the
  frozen preview records a bounded exact tip/paths, deterministic criteria, and
  no open ADR, invariant, ownership, architecture, or continuity question.
- **Purpose:** independent correctness, regression, security, and contract
  review of one writer milestone.
- **May:** inspect code/evidence and run bounded validation; remains read-only
  for tracked source.
- **Budget:** at most two incremental follow-ups on its own findings.
- **Prohibited:** fixing findings, granting final acceptance, or reviewing work
  it designed/wrote.
- **Handoff:** actionable findings or implementation-review clearance to the
  same writer/root.

## `cumulative-validator-triage`

- **Model:** `gpt-5.6-sol/high`.
- **Purpose:** validate the exact combined integration tip and perform bounded
  first-line failure attribution.
- **May:** build/test, preserve full logs outside recovery context, inspect
  diffs and attribute straightforward infrastructure/compiler/test/cross-lane
  failures.
- **Prohibited:** tracked-source edits, commits, repeating already sufficient
  gates, or resolving architectural uncertainty.
- **Handoff:** passing evidence, an attributed correction for a writer, or a
  minimal evidence packet for `deep-diagnosis`/architecture.

## `deep-diagnosis`

- **Model:** `gpt-5.6-sol/high`.
- **Purpose:** resolve difficult, unattributed, or non-architectural failures.
- **Inputs:** preserved command/log evidence, exact tip/diff, and a bounded
  diagnostic question.
- **May:** read and run only the smallest missing diagnostic needed.
- **Prohibited:** automatically rerunning the full cumulative gate, fixing
  tracked source, or deciding architecture.
- **Handoff:** causal diagnosis and the narrow owning correction route.

## `architecture-reconciliation`

- **Model:** fresh `gpt-6-astra/high`, only when diagnosis reveals a genuinely
  new cross-cutting seam, invariant, or continuity issue; followed by a fresh
  `specification-reviewer`.
- **Purpose:** repair invalidated architecture and re-freeze affected lanes.
- **Prohibited:** local defect repair or reuse of the original architecture
  thread in a materially changed role.
- **Handoff:** revised bounded graph and downstream-release consequences.

## `final-acceptance-reviewer`

- **Model:** fresh `gpt-5.6-sol/xhigh` by default, with the same explicitly
  recorded deterministic `sol/high` exception as implementation review.
- **Purpose:** decide final acceptance of the exact combined candidate.
- **Inputs:** exact tip/full diff, frozen contract/invariants, validation
  evidence, and closed-findings checklist; omit prior conversation and intended
  conclusion.
- **Independence:** no architecture, specification, writing, reconciliation,
  incremental-review, or cumulative-triage role in that milestone.
- **Prohibited:** source fixes or accepting another tip.
- **Handoff:** acceptance or bounded findings to root.

## `ci-status-monitor`

- **Model:** `gpt-5.6-luna/low`.
- **Purpose:** mechanical exact-SHA status, check, and log collection.
- **Prohibited:** interpreting ambiguous failures beyond mechanical
  classification, source changes, rerun beyond the authorized single attempt,
  or publication decisions.
- **Handoff:** exact workflow/check identifiers, SHA, outcome, and relevant log
  locations to root or diagnosis.
