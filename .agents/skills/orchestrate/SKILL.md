---
name: orchestrate
description: Activate KicktippAi's full root-orchestrator control-plane workflow. This skill is explicit-only; invoke it as $orchestrate and never use it for ordinary subagent or parallel work.
---

# Orchestrate

Activate the repository's explicit orchestration workflow for the objective in
the user's invocation. The invocation opts into phase-scale intake, grilling,
delegation, model allocation, recovery state, and the bounded Git publication
contract in the repository-root `AGENTS.md`.

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

## Establish Run Identity And Preview State

Resolve a root-run ID from `CODEX_THREAD_ID`, falling back to
`CODEX_SESSION_ID`. If neither exists, generate a UUID, state it in commentary,
and preserve it in compacted state.

Use only these run-scoped files:

- `.tmp/orchestration/<run-id>/state.md` for compact recovery state;
- `.tmp/orchestration/<run-id>/preview.md` for the design tree, readiness
  findings, settled decisions, deferred nodes, and frozen runnable graph.

Initialize the ledger with `preview` status. Pass the run ID and ledger path
in every task-agent assignment and state that only the root edits them. Never
use a shared ledger, a `current` pointer, or another run's files.

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
  work. Keep the lead recallable for scope discoveries and re-freezes.
- Writers may start only from a frozen contract. A new cross-cutting
  invariant, missing ADR, dependency seam, invalidated architecture, or
  material scope expansion pauses the affected lane for redesign and review.

For Bundesliga 2026/27, follow the accepted execution strategy and create the
tracked phase execution packet and detailed design artifacts only after their
grilling and review. Do not treat an earlier task file as a frozen packet.

## Admit Resources And Publish Deliberately

Before creating a worktree or launching a heavy local gate, run
`scripts/Get-OrchestrationResourceSnapshot.ps1` with the applicable admission
mode. Respect its fail-closed verdict and the resource lease recorded in the
ledger. Resource pressure queues work; it never authorizes killing unrelated
processes or deleting caches or user files.

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
