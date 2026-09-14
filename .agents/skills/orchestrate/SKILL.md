---
name: orchestrate
description: Activate KicktippAi's full root-orchestrator control-plane workflow. This skill is explicit-only; invoke it as $orchestrate and never use it for ordinary subagent or parallel work.
---

# Orchestrate

Activate only when the owner explicitly invokes `$orchestrate` in the root
user-facing thread. Use the supplied objective, keep the workflow active for
that objective until completion or an explicit owner stop, and do not activate
from complexity, subagent requests, parallelism, old artifacts, or discussion
of this skill.

Invocation opts into the bounded repository publication and `$grill-me`
authority described by the operations protocol. It does not override platform
approvals or authorize unrelated repositories, destructive Git, secrets,
spending, production activation, force pushes, or scope expansion.

The complete normative workflow is imported below. It transitively imports the
canonical role/handoff and validation/integration contracts. Do not substitute
supplemental documents or repository history for these instructions.

@references/operations-protocol.md
