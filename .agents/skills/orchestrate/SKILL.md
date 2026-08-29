---
name: orchestrate
description: Activate KicktippAi's full root-orchestrator control-plane workflow. This skill is explicit-only; invoke it as $orchestrate and never use it for ordinary subagent or parallel work.
---

# Orchestrate

Activate the repository's explicit orchestration workflow for the objective in
the user's invocation. The invocation is the user's opt-in to the full
control-plane, delegation, model-allocation, durable-ledger, and recovery
protocol in the repository-root `AGENTS.md`.

## Activate The Workflow

- Activate the workflow only in the root user-facing thread. A task agent must
  follow its bounded assignment and must not activate or initialize the
  workflow itself.
- Read and follow the complete **Explicit Orchestration Workflow And
  Compaction Recovery** section in the repository-root `AGENTS.md` before the
  first spawn.
- Use the objective supplied with `$orchestrate`. If the invocation does not
  contain a concrete objective, use the current user request when it is
  unambiguous; otherwise ask the user what should be orchestrated.
- Keep the workflow active for that objective until it is complete or the user
  explicitly stops the workflow.

## Establish Run Identity

Before the first spawn, resolve a root-run ID from `CODEX_THREAD_ID`, falling
back to `CODEX_SESSION_ID`. If neither is available, generate a UUID, state it
in commentary, and preserve it in the ledger and subsequent compacted state.

Use only this run-scoped ledger:

`.tmp/orchestration/<run-id>/state.md`

Initialize it with the run ID, objective, creation time, and `active` status.
Pass the run ID and ledger path in every task-agent assignment, while making
clear that only the root may edit the ledger. Mark the ledger `complete` when
the objective is finished.

Never use `.tmp/orchestration-state.md`, a shared `current` pointer, or another
run's ledger.

## Preserve Scope

Invoking this skill changes how the authorized work is coordinated. It does
not authorize unrelated work, destructive actions, external writes, spending,
or other scope expansion.
