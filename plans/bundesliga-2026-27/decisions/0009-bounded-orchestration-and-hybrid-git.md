# ADR-0009: Use bounded orchestration and hybrid Git integration

- Status: Accepted
- Date: 2026-08-16

## Context

The system is slow, the user has a limited ChatGPT Pro weekly allowance, and one-by-one implementation would leave too little integration time. Unbounded agent parallelism, repeated review loops, or a PR for every small task would consume machine and agent capacity without proportional confidence.

## Decision

One strong orchestration agent owns dependency order, ADR gates, integration, and launch evidence. At most two task agents and two writable worktrees are active at once, normally one writer plus one read-heavy helper. Full builds, full test suites, containers, and live external collection are serialized.

Use direct `main` integration for isolated low-risk changes. Use a coherent branch and PR for cross-cutting or high-risk work where CI/review visibility materially helps. Native auto-merge is not a launch prerequisite: the orchestrator waits for checks, fixes in-scope failures, and autonomously rebase-merges green PRs. The user is not required to click routine merges.

Each task gets one self-review. Add an independent review only for high-risk artifacts or wave integration, and repeat only when a concrete finding warrants it.

## Alternatives considered

- **Implement strictly one task at a time:** Rejected because bounded independent lanes can shorten the critical path.
- **Maximize subagent parallelism:** Rejected because local and weekly capacity are hard constraints.
- **Require a PR for every task:** Rejected because the ceremony would dominate small changes.
- **Work only directly on `main`:** Rejected because selected cross-cutting changes benefit from CI visibility and isolated review.

## Consequences

- The orchestrator must actively schedule machine-heavy work and reduce concurrency under pressure.
- Worktree ownership and integration order must be explicit.
- Review effort is concentrated on storage identity, context selection, workflow inputs, roster provenance, and activation.

## Affected tasks

- All P0 implementation tasks and the execution strategy.

## Supersedes

The unresolved Git and parallelism alternatives in the draft execution strategy.
