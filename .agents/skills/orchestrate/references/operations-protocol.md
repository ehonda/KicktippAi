# Orchestration operations protocol

This is the canonical executable procedure for an explicitly invoked
`$orchestrate` run. The two imports below are equally normative for every run.

@roles-and-handoffs.md
@validation-and-integration.md

## Activation and responsibility

- The original user-facing root thread is the `root-orchestrator`. Task agents
  participate only when their assignment names the run ID, `control-state.json`,
  capsule and preview paths, and stable role ID.
- Only the root owns decomposition, scheduling, cross-lane scope, model
  allocation, state transitions, integration, publication, and user
  communication. It remains a control plane and delegates substantive
  implementation, open-ended research, independent review, and difficult
  diagnosis when they can be bounded.
- Task agents own only their assignment, do not edit run state, and do not
  adopt root duties or delegate unless explicitly authorized.
- `$orchestrate` authorizes use of installed `$grill-me` for genuine owner
  decisions exposed during intake. Stop clearly if the dependency is missing.
- A gate denies only the unsafe transition it names. Automatically remediate
  within existing authority and continue independent work. Stop the run only
  when no safe frontier remains and an owner or external decision is genuinely
  required.

## Initialize one exact run

Resolve the run ID from `CODEX_THREAD_ID`, falling back to `CODEX_SESSION_ID`.
If neither exists, generate a UUID, report it, and record that automatic hook
lookup cannot be assumed.

Initialize through `scripts/Set-OrchestrationCheckpoint.ps1`; do not manually
create or patch projections. The run directory is
`.tmp/orchestration/<run-id>/` and contains:

- `control-state.json`, the only independently authored current state;
- generated `preview.md`, `capsule.json`, `capsule.sha256`, instruction, hook,
  and active-contract manifests; and
- the exact-session `active` marker while the run is nonterminal.

The initialization checkpoint records the objective, stop condition, canonical
repository target, initial local/remote SHA, integration branch, allowed run
branch prefix, instruction inputs, active contracts, and first root action.
For this repository the only canonical remote is `origin` at
`https://github.com/ehonda/KicktippAi.git`; a mismatch gates publication and
requires owner direction.

Every later durability change uses the same checkpoint helper with the current
expected revision. Preview, capsule, checksum, and manifests are projections;
never edit them independently. A duplicate transition is a successful no-op.
Do not keep a shared `current` pointer, select another run by recency, or store
an orchestration event journal.

## Preview the entire objective

Begin in `preview`. Audit the complete supplied objective rather than only its
next apparent task. A phase/priority label is valid, but expand it into current
tasks and dependencies before admitting writers. Freeze:

- current task status, semantic dependencies, seams, independently acceptable
  milestones, downstream-release conditions, and the runnable frontier;
- unresolved owner decisions, evidence prerequisites, deadlines, external
  actions, and authority boundaries;
- current and proposed behavior, fallback or its absence, rollback, recovery
  owner, and restoration gate for production-continuity risk;
- writer ownership, review/validation routes, integration order, CI gates, and
  predicted publication milestones;
- verified repository/remote/default branch, allowed run branches, initial
  local/remote SHA, authenticated permission, and publication topology; and
- worktree inventory/reservations, resource snapshot, proposed heavy bundles,
  and external-side-effect leases.

Use an `architecture-lead` only for genuinely phase-wide or cross-cutting
architecture, followed by a different `specification-reviewer`. Architecture
defines semantic seams independently of transient machine capacity; the root
schedules the accepted graph against current resources afterward.

When owner decisions remain, finish the common foundation and use `$grill-me`
on one whole task or cohesive milestone at a time. Freeze interview-complete
work, mark the rest `needs-interview`, and execute independent ready work
without guessing deferred decisions.

Transition `preview -> ready -> active` automatically when no owner blocker
prevents the ready frontier. Use run-level `awaiting-owner` only when a genuine
owner decision blocks every remaining ready path. Immediately before the first
writer, emit one concise marker:

```text
EXECUTION START — <wave>; ready: <lanes>; deferred/blocked: <summary>
```

Emit another only after an independently reviewed material re-freeze starts a
new wave. A new cross-cutting invariant, missing ADR, dependency seam,
invalidated architecture, or material scope expansion pauses only the affected
lane for architecture/specification review and re-freeze.

## Current state and gates

Keep only current, non-derivable control facts in `control-state.json`: the
objective/lifecycle/wave, lane graph, durable decisions, gates, ownership and
worktree reservations, correction counters, evidence references, publication
allowlist/candidate, resource verdicts and active reservations, retention
contracts, and exact next actions.

Exclude raw resource samples, logs, transcript chronology, live agent counts,
quota estimates, ordinary completion history, and reconstructable Git/CI
observations. Store full validation and calibrated heavy-operation evidence
outside the recovery packet only when the operation contract requires it.

Every gate uses a fixed category and records its scope, evidence, prohibited
transitions, automatic remediation and retry budget, work that may continue,
and escalation/clearance condition. Resource uncertainty gates only the
affected worktree or heavy operation while evidence repair and unrelated work
continue. Read-only diagnosis and bounded local build/test work inside an
assigned authority envelope never need owner permission; platform approvals
still apply.

Checkpoint only at a durability barrier: initial freeze; owner/authority gate
change; independently reviewed material re-freeze; a blocker change that alters
safe work; ownership/worktree/heavy reservation change; correction reservation
or release; reviewed unpublished candidate; integration/publication; or
stop/completion. Coalesce barriers. Agent messages, polls, ordinary test output,
and unchanged resource samples are checkpoint-silent.

## Schedule and communicate

Treat spawned threads, writable worktrees, heavy resources, and external
effects as separate budgets. Admit useful independent ready lanes rather than
targeting occupancy. Use the model/effort mapping in `roles-and-handoffs.md` and
set both fields on every override-compatible spawn. Use `fork_turns: "none"`
or the smallest useful positive history when a different model/effort is
needed; full history is only for an intentional identical configuration.

Reserve a correction through the checkpoint helper before dispatch. Once
substantive work begins it consumes the turn even if it fails or is abandoned;
a failed dispatch that starts no turn releases the reservation. The helper
rejects over-budget corrections and moves only that lane to `needs-diagnosis`.

Use `send_message` only for necessary mid-turn steering of an agent clearly
still working. Use `followup_task` across idle/completion boundaries. Do not
send speculative, status-only, or late queue-only messages that pin terminal
agents. Reuse requires the same role, concrete near-term continuity value, and
an open recorded context-cost proxy. A material role change always uses a fresh
thread.

Use native mailbox completion signals. When otherwise idle, wait five minutes
by default and ten minutes for known long builds/tests/CI. A timeout alone is
not a reason to poll, message, or checkpoint. Refresh live state only when a
scheduling or gate decision depends on it.

Ignore subscription quota. It is neither observable enough nor an admission
signal.

## Admit worktrees and heavy operations

Before creating/reactivating a writable worktree or launching a heavy bundle,
run `scripts/Get-OrchestrationResourceSnapshot.ps1` with reconciled current
reservations. Admission failure gates that transition and starts bounded
evidence remediation; it does not stop other work.

Classify every linked worktree:

- `active-build-capable`: editable/buildable, with the configured growth
  reservation;
- `parked-recovery-only`: preserved but not editable/buildable and no new
  growth reservation;
- `removal-ready`: all retirement preconditions proven; or
- `uncertain`: preserved and conservatively reserved.

Count is inventory, not a gate. Existing bytes are already represented by free
disk and are not charged twice. Reuse a clean admitted worktree only after its
previous commit is integrated or safely published. Never use the primary
checkout to evade admission.

When a worktree becomes terminal, run
`scripts/Remove-OrchestrationWorktree.ps1` with its exact recorded branch/tip.
The helper may remove only a clean, unowned, process/lease-free linked checkout
whose tip remains reachable. It never deletes the branch or commit. A failed
precondition parks the worktree for remediation.

Heavy admission has no operation-count cap. Each operation declares a profile,
memory reservation, recoverability, external/live-effect classification, and
controllable worker cap. Admit a critical-path-aware bundle only when summed
reservations fit available memory above the applicable policy floor and summed
worker caps do not exceed logical processors.

Prefer the configured 1.0 GiB memory floor. The 0.5 GiB experimental floor is
allowed automatically only for mandatory, bounded, recoverable local
validation with healthy paging/commit evidence and no external/live effects.
An unmeasured or unbounded-fanout operation runs without another heavy
operation for its first sample but may use the full controlled pool internally.
After an operation completes, backfill only after a fresh snapshot and
conservative accounting for all active reservations.

On memory-related OOM, severe paging failure, or abnormal termination, run the
circuit-breaker helper. Disable the experimental band, restore the preferred
floor, make the offending profile exclusive with reduced fanout, and allow one
necessary recoverable retry before diagnosis. Other work continues. Only
owner-reviewed analysis may clear aggressive-mode degradation.

Never kill unrelated processes or delete caches, build trees, worktrees, or
user files merely to regain capacity.

## Integrate, review, and publish

Follow `validation-and-integration.md`. The root serially integrates reviewed
lane commits into the run integration branch. A coherent combined candidate
must pass cumulative validation/triage and fresh final acceptance before
publication or a dependent wave.

Create a draft PR after the first reviewed, buildable, coherent milestone and
before its dependent next wave. Keep ordinary lane commits local; publish
cohesive milestones and recovery-critical long-lived candidates. Direct `main`
is allowed only for independently safe milestones; otherwise use the run
integration branch/draft PR.

Invocation authorizes, for this objective and run only:

- staging owned in-scope paths and creating scoped commits;
- non-force pushing an exact reviewed/frozen commit to `origin` on allowlisted
  `main` or `codex/<run-or-objective>-*` refs using an explicit refspec;
- creating/updating draft PRs, and marking ready or merging only when the
  frozen state names the exact base/head and green checks; and
- one rerun of an eligible failed milestone workflow and cancellation of a
  superseded run for an allowlisted exact SHA.

Before every push record branch, remotes, short status, latest commit, exact
payload paths, secret review, and fast-forward target. A rejection is a gate;
do not reshape an operation to evade it. Authorization expires on completion,
stop, repository/remote/branch/scope/topology change, or an unplanned external
operation. Force pushes, history rewrites, tags/releases, unrelated changes,
remote deletion, secrets, production activation, spending, and destructive Git
require new authority.

## Recover and finish

The exact-session hooks validate the committed state revision, capsule checksum,
preview ceiling, packet manifests, and active material before compaction and on
compact session start. They remain silent when no exact-session active marker
exists.

For hot recovery:

1. use the injected validated capsule rather than broadly rereading unchanged
   instructions/contracts;
2. run `scripts/Get-OrchestrationRecoverySnapshot.ps1` once;
3. inspect native live agents once and reconcile lane ownership, roles, paths,
   worktrees, resources, lease, and next action;
4. read an unchanged active contract only when the preview is insufficient for
   the immediate decision; and
5. query CI only when the next action depends on it.

Switch to cold reconstruction for invalid/missing state, revision or packet
drift, interrupted checkpoints, a material re-freeze, unresolved run-wide
owner/authority gate, or uncertain ownership. Use only the exact run directory.
Re-read these normative instructions and the active contracts needed to rebuild
current truth; reconcile live agents, Git/worktrees/resources/lease and relevant
CI; recreate `control-state.json`; independently review a material re-freeze;
and checkpoint before resuming. Legacy state is unsupported and must not be
migrated or special-cased.

Mark the final checkpoint `complete` or `stopped`. This removes the active
marker and expires run authorization.

