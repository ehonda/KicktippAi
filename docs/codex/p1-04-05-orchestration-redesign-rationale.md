# P1-04-05 orchestration redesign rationale

**Decision date:** 2026-09-10 Europe/Berlin  
**Status:** accepted  
**Scope:** the repository's explicit-only `$orchestrate` workflow

This document records why the orchestration workflow is being revised after
the P1-04-05 session. It is supplemental design history for maintainers, not a
runtime dependency of `$orchestrate`. The executable and normative workflow
belongs entirely to the skill and its references.

## Evidence

The source analysis is the
[P1-04-05 orchestration session report](../../session-analysis/p1-04-05-orchestration/index.html),
with reproducible inputs under
[`docs/codex/p1-04-05-orchestration-session-analysis`](p1-04-05-orchestration-session-analysis/README.md).

The session ran from 7 through 9 September 2026 and stopped at draft PR #111.
Its observed outcome and costs were:

| Measure | Observation |
| --- | ---: |
| Completed target tasks | 0 of 2 |
| Wall-clock span | 49.4 hours |
| Observed root engagement | about 33.8 hours |
| Aggregate subagent time | about 29.0 hours |
| Subagent threads / turns | 146 / 263 |
| Follow-up turns | 117 |
| Approximate logged tokens | 155.1 million |
| Review/specification time | 12.07 hours |
| Writer time | 6.76 hours |
| Average active concurrency | 1.27 |
| Time with at least two active agents | 5.36 hours |
| Root waits | 1,104 |

The analysis found several concrete causal problems rather than a single
capacity shortage:

- A metadata-permission question delayed useful work for 8 hours 17 minutes,
  although the actual read-only investigation took about 41 minutes and the
  genuine owner decision took about 16 minutes.
- Static-only writer slices deferred compilation and test feedback. Seven
  compile errors surfaced late, after correction waves had already accumulated.
- Review and specification consumed substantially more time than implementation,
  while long-lived writers and architecture agents accumulated repeated turns.
- Sixty-second polling produced high root overhead. Moving to five- and
  ten-minute waits reduced polling density by roughly 74 percent in the
  analyzed portion without losing completion signals.
- The workflow admitted only one heavy-operation family even when the host had
  roughly 3 GiB of available physical memory. A measured project gate used
  about 0.82-0.86 GiB at peak, so the fixed count left usable capacity idle.
- Twenty-nine linked worktrees accumulated. Retiring 27 obsolete worktrees
  recovered 11.11 GiB.
- Recovery survived 13 compactions and a reboot, but the root performed 241
  capsule-hook operations, 246 manifest-builder operations, 128 resource
  snapshots, and hundreds of state patches. The safety mechanism worked, but
  its manual multi-file update procedure imposed avoidable coordination cost.

The P0 closeout delivered much more work and used broader parallel validation,
but it used a materially different orchestration design and task mix. It is
directional evidence that broader build/test activity can be productive, not a
controlled throughput baseline and not evidence for uncapped process fanout.

## Design conclusions

### The root repository context is the wrong runtime authority

The orchestration protocol originated in root `AGENTS.md` before the
explicit-only skill and exact-session recovery hooks existed. That placement
helped early compaction recovery, but now loads a large inactive protocol into
ordinary repository sessions and creates two procedural sources of truth.

The revised design removes orchestration-specific instructions from root
`AGENTS.md`. `$orchestrate` remains explicit-only and loads its complete
normative instruction graph only after invocation. This restores the intended
activation boundary and eliminates duplicated or conflicting procedure.

The skill entry point stays concise. It imports one canonical operations
protocol, which imports the canonical role/handoff and
validation/integration references. All three are needed by every orchestration
run: roles shape intake and scheduling, and the validation route must be frozen
before writers are admitted.

### Safety gates should preserve progress

"Fail closed" should mean that an unsafe transition is denied, not that an
unattended run stops. Every gate therefore names the affected lane and
transition, automatic remediation, work that may continue, retry budget, and
the precise escalation condition. The run waits for the owner only when a
genuine owner decision blocks the entire remaining frontier.

Read-only diagnosis and bounded local build/test operations inside assigned
scope do not need owner permission. Platform approvals and meaningful external,
destructive, production, spending, or scope-changing decisions remain intact.

### Validation belongs with the writer and at integration seams

Writers need direct feedback from their changes. They therefore own builds and
tests appropriate to the changed surface before handoff. A fresh implementation
reviewer evaluates that evidence and the diff. The root serially integrates
accepted lane commits, after which one cumulative validation-and-triage role
validates the combined tip and attributes straightforward failures.

Combining cumulative validation with bounded triage avoids handing a second
agent an opaque failure that it must reproduce. A fresh deep-diagnosis role is
used only when attribution is difficult, cross-cutting, or architectural. A
fresh final reviewer evaluates the exact candidate before publication.

Correction budgets remain hard limits, but exhausting one pauses and re-routes
only the affected lane. The budget counts substantive correction turns,
including failed or abandoned turns after work begins.

### Capacity should be measured, not counted

Spawned-agent capacity, writable worktrees, memory, CPU fanout, disk, and
external-effect leases are independent constraints. A universal heavy-operation
count cannot express those constraints and prevented safe use of available
memory.

The new scheduler admits bundles whose declared memory reservations and bounded
worker fanout fit the measured pools. The normal available-memory floor is
1.0 GiB. Mandatory, recoverable, local-only validation may use an experimental
0.5 GiB absolute floor when paging and commit health are acceptable. At the
P1 observation of 3.06 GiB available, three provisional 0.85 GiB project gates
fit just above that absolute floor if CPU capacity also permits them.

An unmeasured operation initially runs without another heavy operation, but it
may use the full controlled CPU and memory pool internally. This is not a
static-check restriction. A full solution build or test suite is allowed; its
fanout must simply be bounded. Evidence may raise a reservation immediately,
while durable reductions require post-run analysis.

Memory failures degrade the heavy scheduler rather than stopping orchestration:
the experimental band is disabled, the preferred floor is restored, and the
offending profile becomes exclusive with lower fanout. Independent work
continues.

### Recovery state should be a transaction, not a manual choreography

The current procedure independently edits the preview and capsule, rebuilds
several manifests, recomputes digests, and seals the result. The repeated work
is easy to duplicate and exposes temporary disagreement between files.

The revised workflow has one independently authored `control-state.json` and
one typed checkpoint operation. Preview, capsule, and manifests are derived
projections. The checkpoint uses an expected revision and an exclusive
run-scoped lock, stages and validates all projections, replaces them in a
deterministic order, and commits control state last. Hooks accept only a
matching committed revision and retry briefly while a checkpoint lock is held.
An interrupted transaction therefore causes explicit cold reconstruction,
never silent partial recovery. A semantically duplicate checkpoint is a no-op.

The state is current control-plane truth, not a journal. Coordination analysis
continues to reconstruct evidence from native transcripts, Git, worktrees,
validation artifacts, PR/CI surfaces, and current state. New instrumentation
requires a demonstrated evidence gap.

### Cleanup and publication should follow semantic milestones

A clean terminal worktree is automatically retired after its branch and tip
are preserved and no agent, process, lease, or unpublished unreferenced work
depends on it. Dirty, uncertain, or unreachable worktrees are parked for
remediation instead of being deleted or blocking unrelated lanes.

A draft PR is created after the first reviewed, buildable, coherent milestone
and before the next dependent execution wave. This exposes CI and the
integration surface early without publishing half-safe lane commits.

### Coordination must become a standing analysis concern

Every future orchestration-session analysis should synthesize how architecture,
specification, implementation, review, validation, diagnosis, and final
acceptance interacted. It should examine the role/handoff graph, correction
cycles, first-pass results, transition latency and token use, failure ownership,
stale evidence, role reuse, model fit, and whether design produced genuinely
independent lanes.

This is additive to narrower existing focuses. It does not authorize
`$orchestrate` to read the focus registry or collect an event journal during an
active run.

## Non-goals

- Do not activate orchestration implicitly for complex or parallel work.
- Do not add a distinct "design lead" role; the existing architecture lead
  remains the rare cross-cutting design role.
- Do not introduce a universal writer, worktree, or heavy-operation count.
- Do not infer subscription quota or use it as an admission signal.
- Do not make supplemental documents runtime inputs.
- Do not support migration or resumption of legacy orchestration run state.
- Do not weaken repository identity, publication, destructive-action,
  production, or platform-approval boundaries.

