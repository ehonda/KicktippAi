---
name: orchestrate
description: Activate KicktippAi's full root-orchestrator control-plane workflow. This skill is explicit-only; invoke it as $orchestrate and never use it for ordinary subagent or parallel work.
---

# Orchestrate

Activate the repository's explicit orchestration workflow for the objective in
the user's invocation. The invocation opts into phase-scale intake, grilling,
delegation, model allocation, recovery capsule, and the bounded Git publication
contract in the repository-root `AGENTS.md`.

## Validated MultiAgent V2 Lifecycle

The fresh-session experiment and selected lifecycle policy are recorded in
[`docs/codex/agent-closure-prerequisite.md`](../../../docs/codex/agent-closure-prerequisite.md).
The six-tool MultiAgent V2 surface automatically evicts terminal, idle,
mailbox-clean residents under capacity pressure and can reload their logical
identities with `followup_task`. Treat reclaimability as orchestration intent,
not proof of physical unload or proactive process and memory cleanup.

## Activate The Workflow

- Activate only in the root user-facing thread. A task agent follows its
  bounded assignment and must not initialize orchestration itself.
- Read the complete **Explicit Orchestration Workflow And Compaction
  Recovery** section in the repository-root `AGENTS.md` before the first
  preview agent or writer.
- Require the installed explicit-only `$grill-me` skill. Invocation of
  `$orchestrate` explicitly opts into its use for readiness defects. Stop with
  a clear dependency error if it is unavailable.
- Use the supplied objective. If it is absent, use the current request only
  when unambiguous; otherwise ask what should be orchestrated.
- Keep the workflow active until the objective is complete or the user stops
  it. A phase or priority objective is valid; expand it during intake before
  admitting writers.
- Treat operational hook readiness as an execution prerequisite. Confirm that
  `codex features list` reports `hooks` enabled and that the current exact
  `.codex/hooks.json` definition is trusted in `/hooks`. The root cannot grant
  that client trust itself. Without current evidence, remain in
  `awaiting-owner`, ask the owner to review/confirm the hook, and do not start
  writers or claim automatic compaction recovery.

## Establish Run Identity And Preview State

Resolve a root-run ID from `CODEX_THREAD_ID`, falling back to
`CODEX_SESSION_ID`. This exact identity binds the run to Codex's hook
`session_id`. If neither exists, generate a UUID, state it in commentary and
the capsule, and record that automatic hook lookup cannot be assumed until the
client exposes a matching session identity.

Use only these run-scoped files:

- `.tmp/orchestration/<run-id>/capsule.json` plus `capsule.sha256` for the
  sealed, size-capped recovery snapshot;
- `.tmp/orchestration/<run-id>/preview.md` for the design tree, readiness
  findings, settled decisions, deferred nodes, and frozen runnable graph.

Create the capsule from `resources/capsule-template.json`, set `preview`
status, and seal it with
`scripts/Invoke-OrchestrationCapsuleHook.ps1 -Mode Seal -RunId <run-id>` before
the first preview lane. Sealing writes the checksum and exact-session active
marker atomically. Pass the run ID, capsule path, and preview path in every
task-agent assignment and state that only the root edits them. Never use a
shared state file, a `current` pointer, or another run's files.

Overwrite and reseal the capsule only at the durability barriers defined in
`AGENTS.md`. It is a recovery snapshot, not an event log. Store only facts that
cannot be safely reconstructed; keep it at or below 8 KiB. Routine activity
and unchanged resource samples are capsule-silent. Set status to `complete` or
`stopped` and reseal at the end so the active marker is removed.

Follow the object shapes in the template. Blockers use `{category, detail}`.
Ownership reservations use `{agent_path, role, model, reasoning_effort,
owned_paths, next_action}`. Resource state stores only admission verdicts,
warning bands, and any bounded owner override; a non-null heavy lease uses
`{owner, operation}`. Retained agents use `{agent_path, role, reason,
release_trigger, context_cost_limit}`. Do not add live counters or history to
these records.

## Complete Intake Before Writers

Audit the entire objective before admitting implementation writers. Apply the
Initial Intake Preflight in `AGENTS.md`, including task/dependency expansion,
architecture risk, owner and external gates, production continuity, Git
targets, resource admission, and proposed milestones.

- If the objective is coherent and within existing authority, freeze the
  runnable graph and transition `preview -> ready -> active` automatically.
- If owner decisions are open, invoke `$grill-me`. Finish the phase-wide
  foundation first, then grill one complete task or cohesive milestone at a
  time. The owner may stop only between those units and start the already
  frozen independent graph; mark the remainder `needs-interview`.
- Require a `gpt-5.6-sol` / `xhigh` architecture lead and a different
  `gpt-5.6-sol` / `xhigh` specification reviewer for cross-cutting or high-risk
  work. Keep the lead recallable only while a near-term reuse reason and
  release trigger are recorded and retention does not block useful ready work.
- Writers may start only from a frozen contract. A new cross-cutting
  invariant, missing ADR, dependency seam, invalidated architecture, or
  material scope expansion pauses the affected lane for redesign and review.

Immediately before the first writer, emit one concise `EXECUTION START` line
that names the wave, ready lanes, and deferred/blocking summary. Emit it again
only when an independently reviewed material re-freeze starts a new execution
wave, never for routine continuation, recovery, polling, or scheduling.

For Bundesliga 2026/27, follow the accepted execution strategy and create the
tracked phase execution packet and detailed design artifacts only after their
grilling and review. Do not treat an earlier task file as a frozen packet.

## Schedule Useful Work And Release Specialists

Treat the repository's eight spawned-agent threads as capacity, not a target.
Admit independent ready work when dependencies, authority, ownership, and
machine budgets permit it; never manufacture work simply to fill slots.

- Keep at most two concurrent writers with disjoint ownership and at most two
  admitted writable task worktrees.
- Keep at most one active heavy-operation family. Read, review, research, or bounded
  editing may overlap it.
- Give every retained specialist a retention reason and release trigger. At
  acceptance, freeze, review-surface change, or the recorded trigger, stop
  intentional retention and mark a terminal, idle, mailbox-clean specialist
  reclaimable. Never retain a specialist merely as insurance when it blocks
  useful ready work.
- A material role change always releases a retained thread. Record an
  observable context-cost limit before retention as well; when either the role
  changes or the limit is reached, mark the old thread reclaimable and assign
  a fresh bounded thread for the next role. If live token use is unavailable,
  use an enforceable proxy such as completed follow-up turns or one milestone
  and label it as a proxy.
- Use `send_message` only for necessary mid-turn steering when the target is
  clearly active and not near completion. Use `followup_task` when work must
  be consumed across an idle or completion boundary. Never send speculative,
  status-only, or late queue-only messages to terminal, release-due, or
  possibly completing agents; unread mail pins a terminal V2 resident.
- Record eviction only when the runtime exposes evidence such as removal from
  `list_agents` or successful admission under known capacity pressure. On
  `agent thread limit reached`, inspect live state and retry once only after a
  known mailbox blocker has been consumed with one bounded `followup_task`.
  Otherwise record the capacity blocker and queue the lane.
- Reuse a logical agent with `followup_task` only while continuity is valuable,
  the role is unchanged, and the recorded context-cost limit remains open.
  Use a fresh agent when independence or clean context is more important.
- Use only the fixed blocker categories in `AGENTS.md`. Update the capsule only
  at a durability barrier, never on every message, poll, test, or unchanged
  resource sample.
- Ignore subscription quota when scheduling. It is neither an admission gate
  nor a reason to throttle useful ready work.

Keep Sol/xhigh as the independent-review default during the pilot. Sol/high is
allowed only with a frozen-preview justification that the contract, exact tip,
bounded paths, and deterministic acceptance criteria are frozen and no ADR,
invariant, ownership, architecture, or production-continuity question remains.

## Admit Resources And Publish Deliberately

Before creating a worktree or launching a heavy local gate, run
`scripts/Get-OrchestrationResourceSnapshot.ps1` with the applicable admission
mode. Respect its fail-closed verdict and the resource lease recorded in the
capsule. Resource pressure queues work; it never authorizes killing unrelated
processes or deleting caches or user files.

The sole heavy lease has a `1.10 GiB` available-memory hard floor and a
`1.50 GiB` warning threshold. A warning does not deny work by itself. Preserve
detailed sample/calibration evidence outside the recovery capsule. Change the
capsule only when admission, warning band, override, reservation, or lease
ownership changes; no-change samples remain silent.

Automatic residency eviction is not evidence of proactive memory cleanup. If
resource pressure persists and no supported release operation is exposed,
stop admitting affected work, record the blocker, and request an
owner-controlled end/restart of the current session. Recover from the run
capsule and re-sample resources before new admission; do not open a concurrent
replacement or claim that agent resources were released.

Choose cohesive integration milestones from the frozen graph. Keep ordinary
lane branches local; publish milestone commits and only recovery-critical lane
commits. Direct `main` is permitted only for independently production-safe
milestones. Any intermediate change that disables or regresses live behavior
uses an integration branch or draft PR until the safe release unit is ready,
except for an explicitly owner-approved safety quarantine.

## Preserve Scope And Authorization

Invocation authorizes, for this objective and run only, staging owned paths,
creating scoped local commits, and non-force pushing reviewed/frozen commits to
the startup-verified canonical repository and allowlisted branch family. It
also authorizes the bounded draft-PR and CI operations defined in `AGENTS.md`
and `AUTO-REVIEW.md`.

It does not authorize another repository or remote, force pushes, history
rewrites, tags or releases, remote deletion outside agreed cleanup, unrelated
changes, secrets, spending, production activation, or other scope expansion.
The frozen preview packet may request a more specific external authority
envelope. Platform approval boundaries still apply and must never be bypassed.
