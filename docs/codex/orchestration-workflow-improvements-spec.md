# Orchestration workflow improvements — frozen specification

**Frozen:** 2026-09-07 Europe/Berlin  
**Status:** accepted for implementation  
**Implementation order:** PR 1 workflow → PR 2 Bundesliga archive → PR 3 session-analysis tooling

This document is the durable implementation contract produced by the owner
grill that followed the P1 context-refresh orchestration investigation. It is
the source for the three PRs below. Do not reconstruct the grill from the
conversation after compaction.

## Compaction and continuation entry point

After compaction or a new continuation of this implementation session:

1. Read this document.
2. Inspect the current branch, `git status --short --branch`, recent commits,
   worktrees, and open PRs/checks. Git and GitHub state determine which PR is
   active; do not rely on the status snapshot below alone.
3. Preserve the retained P1 worktrees. Never clean, reset, remove, or overwrite
   them as part of these three PRs.
4. Continue the first incomplete PR in the sequence. Each PR receives a fresh
   `gpt-5.6-sol/xhigh` review/fix cycle, is merged only after review and required
   checks pass, and is followed by updating local `main` before the next PR.
5. Do not repeat settled design questions unless implementation exposes a new
   semantic conflict or a requested scope change.

The explicit `$orchestrate` workflow is not active for this implementation
session. The owner's instructions in this document authorize the three scoped
PRs, their dedicated review/fix cycles, autonomous non-force publication and
merge after green gates, and updating local `main` between them. Repository and
platform safety requirements still apply.

## Live baseline at freeze

- Canonical remote: `origin` at `https://github.com/ehonda/KicktippAi.git`.
- `main` and local `origin/main`: `f3324f3bfe37349a36231768c7369002110f4811`
  (`docs: checkpoint P1-04 and P1-05 handoffs`).
- PR 1 branch: `codex/orchestration-workflow-improvements`.
- Retained common worktree: clean at `852d1798e77d78e4dee4350ddc1d59cba54f60a5`,
  with unpublished/diverged common-runtime work.
- Retained P1-10 worktree: dirty at
  `634b65316422b545ecd1956996a74525fafdd80d`; it contains modified, deleted,
  and untracked runtime/test work and must be preserved.
- Current free disk observed during the grill: 40.25 GiB on C:.

## Cross-PR invariants

- Keep fail-closed exact hook trust, session identity, capsule checksum,
  recovery, Git-target verification, ownership, and heavy-lease safeguards.
- A recovery hash verifies byte-level equality without loading the hashed files
  into model context. It is drift detection, not proof of correctness or
  authorship.
- Architecture describes semantic dependencies and release seams independently
  of transient local machine capacity. The root maps the accepted graph onto
  current agents, worktrees, disk, memory, and leases afterward.
- Do not create a generic orchestration journal, event stream, or append-only
  recovery narrative. Record purpose-specific evidence only when a declared
  experiment, calibration, recovery, or audit contract requires information
  that cannot be reconstructed from native transcripts, Git, reviews, or CI.
- Do not hard-code the investigation's example common-runtime milestones.
  Require each architecture lead to derive independently acceptable milestones
  and an explicit downstream-release matrix from the current objective.
- Historical/archive material is never an unconditional startup or recovery
  input merely because it shares a directory with active plans.

## PR 1 — orchestration workflow and recovery

### Context tiers and root responsibility

Define three explicit tiers:

1. **Root-read:** concise control-plane policy, current objective/index,
   capsule, compact frozen preview, accepted cross-cutting conclusions, and
   live reconciliation needed to schedule work.
2. **Hash-only:** governing instructions, hook scripts, frozen lane contracts,
   operative ADRs, exact reviewed artifacts, and owned-path manifests whose
   equality matters but whose full text the root does not need.
3. **Specialist-read:** full task, design, ADR, implementation, test, or review
   material relevant to that agent's bounded assignment.

The root normally needs only ADR ID, status, applicability, concise operative
consequence, and digest. It reads a full ADR only to resolve an open or
conflicting cross-task decision, production-continuity/authority issue, or
material re-freeze. Architecture leads, writers, and reviewers read their full
applicable contracts.

### Versioned recovery packets

Use separate canonical ordered manifests with normalized identifiers and
raw-byte SHA-256 values:

- **Instruction packet:** root `AGENTS.md` and transitive includes, applicable
  nested `AGENTS.md`, `$orchestrate`, and other skill instructions/references
  explicitly governing the frozen graph.
- **Hook packet:** `.codex/hooks.json` plus the exact kickoff/recovery scripts
  whose execution supplies trust or recovery evidence.
- **Active-contract packet:** compact `preview.md` plus only the active task,
  execution packet, design documents, ADRs, and handoff evidence explicitly
  named by the frozen graph.

The capsule stores each manifest path and digest rather than a large path list.
Do not use one repository commit SHA as a substitute: it is both too broad for
unrelated commits and insufficient for dirty contract drift.

### Hot and cold recovery

Hot recovery is allowed only when session identity, capsule checksum/schema,
packet manifests/digests, exact hook evidence, and ownership reconcile.

Cold reconstruction is required for:

- missing, corrupt, session-mismatched, or schema-invalid state;
- missing/unreadable packet material or a packet/manifest digest mismatch;
- an explicit material re-freeze or unresolved owner/authority gate; or
- ownership that cannot be reconciled between the capsule and live agents or
  worktrees.

Ordinary live changes—agent completion, Git/worktree status, CI, memory, and
lease availability—are refreshed and do not by themselves force cold recovery.

The hot path is bounded to:

1. the validated injected capsule;
2. one deterministic local helper for packet validation and compact
   Git/worktree/resource/lease facts;
3. one native live-agent inspection;
4. only an unchanged active contract whose validated preview summary is
   insufficient for the immediate control-plane decision; and
5. remote CI only when the immediate next action depends on it.

Do not parse transcripts or broadly reread unchanged policies, phase history,
task history, ADR chains, or unrelated evidence during hot recovery. Preserve
the existing full reconstruction as the cold path.

### Positive hook trust

Add a synchronous `UserPromptSubmit` hook. Because Codex ignores matchers for
this event, its script must remain silent for ordinary prompts and detect an
explicit `$orchestrate` invocation itself. Emitting evidence must never activate
orchestration.

For an explicit invocation, emit a versioned positive marker binding:

- canonical repository identity;
- current session ID;
- raw `.codex/hooks.json` digest;
- relevant kickoff/recovery script digest; and
- marker schema version.

Successful execution is positive evidence that hooks were enabled and the
exact current definition was trusted. Absence is ambiguous and remains a
fail-closed owner gate. A hook or script digest change invalidates the marker.

### Compact preview contract

`preview.md` is a replace-in-place current graph with these fixed concerns:

- objective, lifecycle state, and current wave;
- active, ready, deferred, and blocked lanes;
- dependencies and accepted cross-lane seams;
- concise durable decisions plus instruction- and hook-packet digests;
- owner, authority, and production-continuity gates;
- ownership, worktree, and heavy-lease reservations;
- integration/publication topology; and
- exact root and delegated next actions.

Exclude raw interview dialogue, completed-lane chronology, review transcripts,
repeated resource samples, CI logs, and historical task evidence. Enforce an
8 KiB warning/target and a 12 KiB hard ceiling for every sealed preview.
Because `preview.md` belongs to the active-contract packet, it must not embed
that packet's own manifest digest. The capsule is the authoritative location
for the active-contract manifest path and digest.

### Milestones and architecture

When a shared foundation gates multiple lanes, the architecture lead defines
semantic, independently acceptable milestones and a downstream-release matrix.
The specification reviewer independently accepts that map. Local machine
capacity is not an architecture input; it affects scheduling after acceptance.

Use `gpt-6-astra/high` only for a genuinely necessary phase-wide or
cross-cutting architecture lead. It does not replace the root or independent
reviewer. A different `gpt-5.6-sol/xhigh` specification reviewer remains
mandatory. Reconciliation after a correction circuit breaker may use a fresh
Astra/high agent only when the work is again genuinely architectural; local
defects or mechanical work do not qualify.

### Writer and review continuity

- One writer owns one frozen milestone and may receive at most three correction
  turns. Acceptance, material contract/role change, or a third unsuccessful
  correction releases it.
- The first independent implementation reviewer may perform at most two
  incremental follow-ups on its own findings. It cannot grant final milestone
  acceptance.
- Final acceptance uses a fresh agent that had no architecture, specification,
  writing, reconciliation, or incremental-review role in that milestone. It
  receives the exact frozen tip, full milestone diff, frozen acceptance
  contract, applicable ADRs/invariants, and a concise closed-findings checklist,
  but not the earlier conversational transcript or intended conclusion.
- Final review defaults to Sol/xhigh, retaining the already accepted bounded,
  deterministic Sol/high exception.
- Reaching the correction limit pauses only the affected lane for a fresh
  diagnosis. Local defects cause a narrower correction contract or re-slice;
  ambiguous criteria return to specification; new seams/invariants/continuity
  issues return to architecture and independent re-freeze; ownership/surface
  problems cause a split. Independent ready lanes continue.

### Disk and worktree admission

Remove the universal maximum linked-worktree count. Count remains inventory,
not an admission gate. Retain one writer per isolated worktree and serialize
root integration.

Classify each linked worktree:

- **Active/build-capable:** owns a provisional 1.25 GiB future-growth
  reservation.
- **Parked/recovery-only:** retains its current bytes but cannot receive edits
  or builds until reactivated and admitted; no growth reservation.
- **Removal-ready:** exact evidence proves all work is integrated, published,
  or intentionally abandoned; removal remains deliberate.

Existing bytes are already reflected in volume free space and are not charged
again. Uncertain classification conservatively holds 1.25 GiB. Admission uses:

`effective post-admission free = measured free - outstanding reservations - proposed reservation`

Require at least **14 GiB** effective post-admission free. Calculate the 15%
warning after reservations. Fail closed when disk measurement, worktree
inventory, or reservation reconciliation is unavailable. The 1.25 GiB value is
provisional: it originated as a rounded buffer over one historical roughly
1.1 GiB mature-worktree observation, not a measured distribution.

Remove the universal concurrent-writer limit. A frozen execution wave admits
useful writers only for ready, semantically independent, path-disjoint lanes
with one writer per worktree, disk admission, required external-side-effect
leases, and a defined review/integration route. Writer count is an operational
throttle chosen by the root for the current wave, not an architecture property
or occupancy target. The sole-heavy-operation-family lease remains independent.

At intake, classify every existing worktree as in-scope active, recovery
required, intentionally preserved out of scope, or removal-ready. Record owner,
branch/tip, dirty summary, retention reason, and release condition. Resume or
reconcile in-scope predecessors first when capacity requires it. Never treat
missing ownership as cleanup permission and never shift implementation into the
primary checkout merely to evade admission.

### Memory experiment

- Lower the heavy-operation hard floor from 1.10 GiB to **1.00 GiB**.
- Preserve the 1.50 GiB warning and one-heavy-family lease.
- Treat 1.00–1.10 GiB as an experimental band restricted to recoverable local
  operations without external/live side effects.
- Capture purpose-specific heavy-operation outcome evidence: operation family,
  start/min/post available memory, commit/paging signals sufficient to
  assess pressure, duration/outcome, paging or OOM symptoms, and causal queue
  delay. Do not put this stream in the capsule or automatic recovery context.
- A memory-related OOM, abnormal termination, or severe paging failure trips a
  repository-local, machine-scoped circuit breaker at
  `.tmp/orchestration/resource-policy-state.json`. The resource helper must
  consult that state for every heavy admission and atomically restore the
  effective floor to 1.10 GiB for resumed and later runs in this checkout.
  Record the triggering run, operation, timestamp, and concise reason without
  putting the evidence stream in recovery context. The breaker remains active
  until an owner-reviewed analysis explicitly clears it; never clear it from a
  successful sample or a new session. Do not impose a sample-count rule now.

### Durable repository instructions

Root `AGENTS.md` owns generic orchestration invariants. `$orchestrate` owns the
executable procedure. `plans/bundesliga-2026-27/AGENTS.md` owns Bundesliga
source-of-truth precedence, current-index maintenance, handoff reconciliation,
and archival lifecycle. Remove unconditional historical plan/ADR/P0 reads,
including the competition profile's unconditional P0-06/P0-21 routing, and fix
stale root schedule guidance.

PR 1 changes routing and policy but does not move historical files or perform
the wholesale README/execution-strategy rewrite.

### PR 1 validation and review

- Validate capsule/manifest/hot-cold paths, trust-marker silence/emission and
  mismatch behavior, preview size rules, resource admission/reservations, and
  the 1.00 GiB memory boundary with deterministic tests.
- Validate the modified `$orchestrate` skill with the repository-prescribed
  skill validator and forward-test behavior where useful.
- Run applicable PowerShell tests and repository build/test gates outside the
  sandbox as required by repository instructions.
- Use dedicated fresh Sol/xhigh review agents for implementation review and
  corrections, then final exact-tip review.
- Push only after recording exact branch, remotes, status, and tip. Create a
  focused ready-for-review PR, resolve checks/review, merge autonomously, and
  update local `main` before PR 2.

PR 1 description must include this owner copy/paste block:

```markdown
One-off directives for the next Bundesliga P1 orchestration run:

- Before resuming implementation, use a fresh `gpt-6-astra/high` architecture lead to reconcile the shared common-runtime graph and contracts in response to the repeated review/fix churn. Keep this as an architecture role, and use a different `gpt-5.6-sol/xhigh` specification reviewer.
- For this first run after removing the universal writer cap, allow at most four concurrent writer subagents. This is a run-local entry throttle for later evaluation, not a new workflow-wide limit or occupancy target.
```

It must also tell the owner to review and trust the changed `.codex/hooks.json`
definition through `/hooks` before the next `$orchestrate` writers may start.

## PR 2 — full Bundesliga archive migration

PR 2 is independent and reviewable, but its description states that PR 1 must
merge first.

- Slim `plans/bundesliga-2026-27/README.md` into the current program-state and
  navigation index rather than a chronology or procedure manual.
- Slim `execution-strategy.md` into current dependencies, milestones,
  production-continuity strategy, and execution policy.
- Move every completed P0 task and P0 handoff into a clear `archive/p0/`
  hierarchy; extract P0 chronology and completed launch evidence from the live
  entrypoints into an indexed archive.
- Update all repository links atomically and run deterministic Markdown-link
  validation. Do not leave redirect stubs solely to preserve discovery clutter.
- Keep accepted ADRs in `decisions/` while operative. Age or P0 numbering is not
  an archival criterion.
- Reconcile live state before archiving. Archive `Complete` or `Superseded`
  items only after live recovery/rollback dependencies are cleared.
- For mixed P1-10 artifacts, first separate completed recovery evidence from
  the still-live atomic-delivery contract. Archive other eligible completed or
  superseded P1 records only when that test is satisfied.
- Make archive directories opt-in reference material, never an unconditional
  root or task-agent read.
- Use dedicated fresh Sol/xhigh review/fix and final-review agents; pass link and
  repository gates; push a focused ready PR; merge autonomously; update local
  `main` before PR 3.

## PR 3 — session-analysis tooling and focus registry

### Skills and flexible analysis shape

Add two explicit-only repository skills:

- `$session-analysis`: reusable extraction, privacy bounding, snapshot
  verification, self-contained report shell, publication, and focus
  consumption.
- `$record-session-analysis-focus`: narrow owner-invoked management of focus
  definitions; it does not run analyses or load transcripts.

Use a declarative per-report manifest and shared extraction/validation mechanics
for future reports. Keep report-specific enrichment, metrics, findings, data
shapes, and visualizations flexible because they depend on the questions asked.
Do not build a rigid universal dashboard.

Leave every historical investigation and its frozen outputs unchanged. Build a
new stable engine for future analyses, verify parity against one checked-in
historical snapshot, and do not migrate old reports absent a later bug. Replace
the hard-coded Pages report cards with manifest-driven report discovery.

### Canonical focus registry

Create `docs/codex/session-analysis/focuses.json`. Each focus has a stable ID,
question(s), creation date, applicability, cadence (`once` or `standing`), status
(`active`, `retired`, or `superseded`), evidence-source hints, and a compact
latest/report resolution pointer.

- A one-off focus retires when a report covers it.
- A standing focus remains active and updates only `last_covered_by`.
- Reports list the focus IDs they addressed, preserving discovery through report
  manifests rather than a growing history array in the registry.
- Unanswered focuses remain active; the tool never manufactures a conclusion.
- Focuses constrain questions, not report data structures or visual form.
- `$orchestrate` never reads or writes the focus registry. The owner manages it
  in a dedicated session through `$record-session-analysis-focus`.

The companion skill adds, updates, supersedes, retires, lists, and validates
focuses; infers stable IDs and applicability; asks only when cadence or scope is
materially ambiguous; and detects likely duplicates/overlap. A deterministic
helper shared with `$session-analysis` owns schema-safe updates.

Seed these standing focuses:

- useful parallelism, chosen writer-wave throttle, blocked ready work, and the
  actual graph/ownership/review/integration/heavy-lease constraints;
- review/correction churn and acceptance bottlenecks;
- owner waiting versus avoidable agent-created blocking;
- compaction recovery work and context occupancy;
- whether the memory policy is too restrictive, including outcomes in the
  1.00–1.10 GiB experimental band; and
- how often correction limits trigger reconciliation, what was reconciled,
  every model/effort used, whether classification was correct, independent
  review outcome, and downstream stability.

Seed these one-off focuses:

- whether Astra/high was used only for a necessary architecture lead in the
  next P1 run and how its result held up under independent review and downstream
  implementation; and
- whether hot recovery and the compact-preview contract reduced recovery work
  without weakening safety.

`$session-analysis` selects matching active focuses plus owner-supplied ad hoc
questions. The published report manifest records coverage. In the same commit,
covered one-off focuses retire and point to the report; covered standing focuses
remain active and update `last_covered_by`.

### PR 3 validation and review

- Validate both new skills with the skill validator and realistic forward tests.
- Validate focus add/update/retire/supersede/duplicate behavior and report
  consumption.
- Prove extractor/normalization parity against one frozen historical snapshot.
- Validate self-contained report output and manifest-driven Pages discovery.
- Use dedicated fresh Sol/xhigh review/fix and final-review agents; pass relevant
  build/site checks; push a focused ready PR; merge autonomously; and update
  local `main`.

## Settled non-goals

- No technical replanning of the current P1-04/P1-05 common runtime in this
  implementation session.
- No Astra involvement in mechanical worktree inspection or operational
  scheduling.
- No machine-capacity data supplied to architecture merely to shape the graph.
- No universal linked-worktree or concurrent-writer count.
- No generic orchestration history directory or pilot ledger.
- No predefined Astra success score, correction cap, or permanent-default
  decision before the next session analysis.
- No automatic focus-registry activity from `$orchestrate`.
- No rigid analysis schema that dictates which metrics or visualizations a
  report must use.
- No historical report rewrites in PR 3.

## Completion condition

The objective is complete only when all three scoped PRs have passed their
dedicated review/fix and final-review cycles, required checks are green, each PR
has been merged, local `main` is synchronized after the final merge, retained P1
worktrees are preserved, and the final handoff reports exact PRs/commits plus the
required `/hooks` trust step for the next orchestration run.
