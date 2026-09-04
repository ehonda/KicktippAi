# MultiAgent V2 Lifecycle Reassessment

**Status:** validated in a fresh diagnostic session on 2026-09-04
**Purpose:** record the reassessment and resolution of the former `close_agent` merge prerequisite for PR #98

## Summary

The former `close_agent` prerequisite was based on an incorrect interpretation of Codex's multi-agent runtime.

The investigation that produced the latest prerequisite updates was performed with `gpt-5.3-codex-spark`. Its observations remain useful evidence, but its diagnosis should not be treated as authoritative. A subsequent source-level review of Codex `0.153.0` indicates that the collaboration surface observed in our sessions is likely **MultiAgent V2**, where the absence of `close_agent` is expected rather than evidence of a broken or incompletely negotiated V1 tool surface.

The observed tools were:

- `spawn_agent`
- `send_message`
- `followup_task`
- `interrupt_agent`
- `list_agents`
- `wait_agent`

Current Codex source distinguishes this V2 surface from the older V1 surface, which includes tools such as:

- `send_input`
- `resume_agent`
- `wait_agent`
- `close_agent`

This changes the lifecycle question substantially.

In MultiAgent V2, terminal, idle, mailbox-clean residents can be managed
through **automatic residency eviction and later reload**, rather than explicit
`close_agent` calls. The fresh-session experiment confirmed this behavior in
the actual runtime used for this repository, so explicit closure is no longer
a merge prerequisite for PR #98.

The complete environment, experiment matrix, upstream refresh, selected
policy, and residual resource limitation are recorded in
[`agent-closure-prerequisite.md`](agent-closure-prerequisite.md).

## Validated Working Model

The six-tool collaboration surface observed in our sessions is the intended MultiAgent V2 interface.

Therefore:

1. `close_agent` being absent does not by itself indicate a broken installation, stale CLI, repository configuration problem, or partially exposed V1 tool surface.
2. V2 maintains a bounded number of resident subagent threads.
3. When capacity is required, completed/idle V2 agents can be automatically unloaded to make room for new agents.
4. Unloaded completed agents can later be reloaded when addressed through V2 operations such as `followup_task`.
5. The orchestration workflow therefore should reason about:
   - active agents,
   - retained agents,
   - terminal/reclaimable agents,
   - and actual spawn admission,
   
   rather than requiring an explicit `close_agent -> released` transition.

This model was validated against the actual Codex Desktop/runtime surface on 2026-09-04 before the PR workflow was amended.

## Supporting Codex V2 Behavior

Source-level review of Codex `0.153.0` shows a V2 residency mechanism that attempts to unload an existing resident when the configured residency limit has been reached and another slot is needed.

An agent is eligible for unloading when it is terminal and idle, including states such as:

- completed,
- errored,
- interrupted,

provided that it has:

- no active turn, and
- no pending mailbox items.

There is also a Codex test explicitly covering the case where reserving another V2 residency slot unloads the oldest completed idle V2 agent.

This suggests the configured limit is primarily a bound on **concurrently resident subagent runtimes**, not a lifetime limit on the total number of agents a root thread can ever create.

That would also explain how long-running Codex sessions can create significantly more agents over their lifetime than the configured simultaneous thread count.

## Important V2 Caveat: Pending `send_message` Mail

There is an important known V2 failure mode.

`send_message` and `followup_task` have different semantics:

- `send_message` is queue-only.
- `followup_task` triggers/wakes the target for another turn.

A completed V2 agent with an unread queued message may not be eligible for residency eviction. Upstream reports describe this causing apparently surprising `agent thread limit reached` failures: only a small number of agents may be actively running, while completed agents remain resident because queued mailbox items prevent eviction.

The orchestration policy therefore distinguishes carefully between the two messaging operations.

Validated policy input:

- Use `send_message` only for steering an agent that is known to still be actively running.
- Do not send speculative or late `send_message` calls to agents near or after completion.
- Use `followup_task` when intentionally asking an idle/completed retained agent to perform additional work.
- Avoid unnecessary control-plane chatter with agents that are already release-due.

The queue-only mailbox failure and its `followup_task` recovery were reproduced directly.

## Proposed Lifecycle Model

The current workflow should not assume:

```text
running
  -> completed
  -> close_agent
  -> released
```

For V2, a more accurate conceptual model may be:

```text
running
  -> terminal
  -> reclaimable
  -> [automatically unloaded when capacity is needed]
```

A reclaimable agent would mean:

- its current assignment is finished or no longer needed;
- it has no active turn;
- no further immediate reuse is planned;
- there should be no pending queue-only mailbox work preventing eviction.

The orchestration ledger should not claim that a reclaimable agent has already been physically unloaded unless the client/runtime provides evidence of that fact.

Instead, it can record that the specialist is no longer intentionally retained and that the runtime may reclaim its residency slot when necessary.

## Retained Agent Reuse

V2 also appears deliberately designed to support reuse of logical agent identities.

A completed agent that has been unloaded can potentially be reloaded when it receives another appropriate V2 interaction. This makes selective specialist retention useful.

Possible examples:

- architecture lead retained across closely related design decisions;
- implementation specialist retained across a cohesive task sequence;
- reviewer retained while a specific review surface is still changing.

However, agent reuse should not become the default merely to conserve slots.

Reuse carries context-history costs and can reduce independence. In particular, independent reviews should generally use fresh agents rather than repeatedly recycling the same reviewer.

Selected rule:

> Reuse agents where continuity is valuable. Spawn fresh agents where independence or a clean context is valuable. Let V2 residency management handle the resulting inactive agents.

## Optional One-Shot Alternative

For genuinely isolated work, `codex exec --ephemeral` may provide a useful second execution path.

An ephemeral `codex exec` invocation is a separate non-interactive Codex run whose session rollout is not retained after completion. It does not participate as a native V2 child thread of the root orchestrator and therefore does not require `close_agent`.

Potential use cases:

- isolated read-only repository analysis;
- one-shot independent review;
- bounded specification critique;
- deterministic verification tasks that do not require interactive steering.

This should not automatically replace native subagents.

Native V2 agents retain important orchestration capabilities such as:

- `wait_agent`;
- `list_agents`;
- `send_message`;
- `followup_task`;
- visible parent/child lifecycle integration.

If an ephemeral execution backend is considered for `$orchestrate`, its resource usage, concurrency, output capture, permissions, and failure behavior should be separately designed and validated.

## Capacity vs Resource Reclamation

Two separate questions must not be conflated:

### Thread capacity

The fresh-session experiment confirmed that V2 can reclaim residency capacity
by unloading a terminal, idle, mailbox-clean agent when another agent needs
admission. This resolves the ordinary orchestration thread-capacity concern.

### Memory/process/resource reclamation

A completed agent may remain resident until eviction is actually needed.

Upstream reports suggest completed V2 agents can retain MCP/runtime processes and meaningful memory while resident.

Therefore the absence of an explicit release operation may still matter for proactive resource cleanup even if it does not prevent future spawning.

The PR already contains machine-resource admission policy. The fresh-session
experiment distinguished the observable lifecycle states but did not prove
that eviction proactively reclaims process memory:

- "completed but still resident";
- "automatically evicted";
- "reloadable";
- and any observable memory/process effect.

A resource-cleanup limitation may deserve workflow policy or monitoring without necessarily being a merge blocker.

## Fresh-Session Validation Protocol

This protocol was executed on 2026-09-04. The results are recorded in
[`agent-closure-prerequisite.md`](agent-closure-prerequisite.md); the steps
remain here so the evidence can be reproduced after future runtime changes.

### 1. Identify the active multi-agent mode

Record:

- Codex Desktop/app version;
- CLI version;
- effective executable path;
- effective relevant config;
- collaboration tools exposed to the root session.

If the session exposes:

```text
spawn_agent
send_message
followup_task
interrupt_agent
list_agents
wait_agent
```

record this as evidence consistent with MultiAgent V2.

Do not treat missing `close_agent` alone as a defect until the active multi-agent version is established.

### 2. Validate normal capacity recycling

Using a deliberately small configured thread count if safe and convenient:

1. Spawn agents until the usable resident capacity is occupied.
2. Allow at least one agent to finish naturally.
3. Do not explicitly interrupt, message, or otherwise manipulate the completed agent.
4. Attempt to spawn another agent.
5. Record whether the spawn succeeds.
6. Inspect `list_agents` and any available lifecycle evidence.
7. Repeat sufficiently to establish that more total agents can be created over the session lifetime than can be simultaneously resident.

Expected V2 behavior:

> a completed idle agent is automatically unloaded when its residency slot is needed.

If this succeeds, explicit `close_agent` is not required to recycle ordinary completed-agent capacity.

### 3. Validate reload/reuse

1. Retain the identity of a completed agent.
2. Cause enough subsequent activity that it may be unloaded.
3. Send that agent a `followup_task`.
4. Confirm whether the same logical agent can perform another turn.
5. Record whether its previous context/history is preserved as expected.

This establishes whether "retain logically, unload physically, reload on demand" is practical for `$orchestrate`.

### 4. Validate the mailbox-pinning failure mode

Perform a bounded diagnostic reproduction of the known V2 edge case:

1. Let agent A complete.
2. Send A a queue-only `send_message`.
3. Fill remaining resident capacity with active agents.
4. Attempt another spawn.
5. Record whether A's queued mailbox item prevents its eviction and causes an agent-limit failure.
6. Use an appropriate `followup_task` or other supported operation to consume/resolve the queued work.
7. Retry the spawn.

If reproducible, explicitly encode a workflow rule against late queue-only messages to terminal/release-due agents.

### 5. Validate interruption semantics

Interrupt an agent that is no longer needed.

Then establish:

- whether the interrupted agent becomes residency-evictable;
- whether its slot can subsequently be recycled;
- whether an interrupted-and-evicted agent remains reusable or is intentionally lost.

Do not assume interrupted and naturally completed agents have identical reload semantics.

### 6. Observe resource behavior

Where practical, record lightweight evidence around:

- number of resident child processes;
- memory before spawning;
- memory with several agents resident;
- memory after terminal agents exist;
- memory after capacity pressure causes eviction.

This does not need to become a full performance study. The purpose is to establish whether lack of proactive release creates a material problem for the resource policy already defined in PR #98.

## Resolved `close_agent` Prerequisite

The fresh-session tests confirmed V2 automatic eviction and continued
spawning, so the former prerequisite was rewritten.

The amended PR no longer states or implies that:

> capacity cannot be reclaimed unless the root can explicitly call `close_agent`.

It documents the actual observed V2 semantics instead.

The adopted workflow direction is:

- mark specialists release-due when the orchestration policy no longer intends to retain them;
- stop sending them unnecessary messages;
- treat clean terminal agents as reclaimable;
- allow V2 to unload them when residency pressure requires it;
- use `followup_task` only when deliberate reuse is valuable;
- use fresh agents where independence is more important than continuity;
- fail safely if actual spawn admission nevertheless reports capacity exhaustion;
- record any known V2 edge cases rather than pretending explicit closure exists.

Proactive resource cleanup remains unproven and is recorded as a separate
limitation rather than being conflated with ordinary thread-capacity reuse.

## Aside: Investigate `CODEX_CLI_PATH`

A separate configuration question remains unresolved.

During the earlier investigation, user-level Codex configuration contained an `app_server` / `CODEX_CLI_PATH` setting pinned to an older Codex executable. It was changed to point at the current launcher/executable, and Codex appeared to continue functioning normally.

We do not currently know:

- when that setting was introduced;
- whether Codex Desktop created it automatically;
- whether it came from an older workaround or installation state;
- what component consumes it;
- whether it is still necessary;
- whether pointing it directly at the current launcher is the intended configuration;
- whether it should instead be removed and allowed to resolve automatically;
- whether an updater is expected to maintain it;
- or whether leaving an explicit version/path there risks future version skew.

This is no longer the leading explanation for missing `close_agent` if the runtime is confirmed to be V2, but the configuration should still be understood rather than left as an unexplained manual mutation.

### `CODEX_CLI_PATH` follow-up

In the fresh diagnostic session:

1. Inspect official current Codex documentation and source for `CODEX_CLI_PATH`.
2. Search the `openai/codex` repository, issues, discussions, and release notes for:
   - `CODEX_CLI_PATH`;
   - app-server CLI path selection;
   - desktop-bundled CLI discovery;
   - stale pinned CLI paths;
   - updater behavior involving this setting.
3. Determine which process reads the setting and what its documented purpose is.
4. Determine whether Codex Desktop normally writes or updates it itself.
5. Establish the supported/default behavior when it is absent.
6. Compare:
   - explicit current launcher path;
   - no explicit `CODEX_CLI_PATH`;
   - any documented recommended form.
7. Prefer restoring/defaulting to supported automatic discovery if an explicit path is not required.
8. Record the conclusion in repository documentation only if it affects reproducibility or project-specific Codex setup. Avoid retaining a user-machine-specific path in repository configuration.

Do not make further configuration changes merely to explain the old value. Preserve enough evidence to understand the previous state first.

## Resolution

The fresh-session evidence answered the lifecycle questions that controlled
the merge gate:

- the six-tool surface and runtime behavior are consistent with MultiAgent V2;
- clean completed agents were evicted and their capacity reused;
- completed and interrupted identities were reloaded with `followup_task` and
  retained prior context;
- queue-only mail pinned a completed resident and caused the predicted generic
  thread-limit failure;
- consuming that mail through `followup_task` restored admission;
- interruption produced an evictable terminal resident; and
- successful eviction did not establish proactive process or memory cleanup.

The configured `CODEX_CLI_PATH` resolved to the same current `0.153.0`
executable used by the session, excluding stale version skew as the cause of
the observed surface. Whether an explicit path should be removed in favor of
automatic discovery remains a user-machine setup question, not a PR #98 merge
gate; no additional user configuration was changed.

PR #98 now adopts the validated V2 lifecycle policy and treats proactive
resource cleanup as a separately monitored limitation.
