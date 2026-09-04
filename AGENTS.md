# KicktippAi Agent Context

This document contains context relevant when working on tasks in this repository.

@AUTO-REVIEW.md

## Running Parallel Powershell Work

When a workflow says to run independent commands in parallel, do not place the commands on one line separated by `;`. Semicolon-chained Powershell commands run sequentially. Use `Start-Job` or separate terminal tasks to launch all commands first, then wait for all of them with `Wait-Job`, collect output with `Receive-Job`, and fail the workflow if any job failed.

For Langfuse experiment run families, create one shared `$runStamp` before launching jobs, pass that stamp into every job's run name, and start all jobs before waiting for any one of them.

## Explicit Orchestration Workflow And Compaction Recovery

The rationale, terminology, pilot assumptions, and first-P1 evidence for this
workflow are recorded in [docs/codex/orchestration-workflow.md](docs/codex/orchestration-workflow.md).

### Activation Boundary

This workflow is inactive by default. It becomes active only when the repository's explicit-only `$orchestrate` skill is invoked in the current root user-facing thread. Requesting subagents, asking for parallel work, task complexity, available agent capacity, a prior orchestrated session, an existing ledger, or the presence of these instructions does not activate it. Mentioning, discussing, reviewing, or editing `$orchestrate` or this workflow also does not activate it.

While this workflow is inactive, the root acts as a normal Codex working agent. It may use subagents under the ordinary applicable rules, but the control-plane role restrictions, orchestration ledger, recovery preflight, and subagent model-allocation protocol below do not apply.

Once `$orchestrate` is explicitly invoked, the workflow remains active for that objective in the current root thread until the objective is complete or the user explicitly stops the workflow. The root orchestrator is the original user-facing thread, identified by the canonical agent path `/root` when agent paths are available. A task agent is any spawned child such as `/root/<task>`.

The explicit invocation also authorizes the bounded repository publication work needed to finish that objective: staging owned in-scope paths, creating scoped commits, and non-force pushing reviewed or frozen commits to the startup-verified canonical repository and allowlisted branch family. It authorizes the draft-PR and CI operations in [Bounded Git And GitHub Authorization](#bounded-git-and-github-authorization). It does not authorize unrelated repositories or remotes, force pushes, history rewrites, tags or releases, secrets, destructive Git, spending, production activation, or other external scope expansion. Platform approval boundaries still apply and must not be bypassed.

A task agent participates in an active orchestration workflow only when its root assignment explicitly supplies the orchestration run ID and ledger path. That assignment activates only the task-agent responsibilities below; it does not authorize the task agent to assume root duties, edit the ledger, or delegate further unless the root explicitly says otherwise.

Task agents also receive applicable repository instructions, so role boundaries are explicit:

- Only the root orchestrator owns decomposition, agent and model allocation, scheduling, cross-task scope decisions, integration order, and user communication.
- Task agents own only their bounded assignment. They must not adopt root control-plane duties, update the shared orchestration ledger, reassign work, or recursively delegate unless the root explicitly authorizes that in their assignment.
- Task agents should return concise checkpoints and final evidence to the root. The root decides follow-up ownership, acceptance, integration, and release gates.
- The root-only rules in this section and in [Subagent Model Allocation](#subagent-model-allocation) do not instruct task agents to spawn or manage other agents.

The root orchestrator should remain a control plane rather than becoming an implementation worker. Delegate substantive implementation, open-ended or complex research, independent review, and CI or log analysis whenever that work can be expressed as a bounded assignment.

The root may perform only:

- small read-only checks needed to define, route, verify, or integrate delegated work;
- cross-agent coordination and resolution of ownership, dependency, or scope conflicts;
- primary-checkout worktree setup and serialized Git integration operations;
- substantive task work that cannot reasonably be delegated, after recording why delegation is unavailable or inappropriate.

Compaction, automatic continuation, agent delay, an idle agent, or the convenience of already having context does not transfer task-agent work back to the root.

### Initial Intake Preflight

Every orchestration run begins in `preview`. Before the first implementation writer, the root must audit the entire supplied objective rather than only the next task. A phase or priority label is a valid objective, but the preview must expand it into its current tasks and dependencies before work begins.

The root may delegate bounded read-only preview audits. It must then freeze a reviewed execution packet that records:

- current task status, dependencies, seams, milestones, and the runnable subgraph;
- unresolved ADRs, owner decisions, evidence prerequisites, deadlines, external actions, and authority envelopes;
- architecture and production-continuity risk, including current behavior, proposed behavior, fallback or its absence, rollback, recovery owner, and restoration gate for any live-impacting change;
- proposed writer ownership, review and CI gates, publication topology, and predicted milestone/push counts;
- the verified canonical repository, remote URL, integration branch, allowed run-branch prefix, initial local/remote SHA, and whether direct-main, draft-PR, or milestone-branch publication is permitted; and
- the resource snapshot, worktree reservation, heavy-operation budget, and throttle rules.

Cross-cutting or high-risk work requires one `gpt-5.6-sol` / `xhigh` architecture lead to produce the seam map, invariants, non-goals, dependency graph, owned paths, and verification strategy, followed by a different `gpt-5.6-sol` / `xhigh` specification reviewer. After acceptance, keep a specialist recallable only when reuse is expected before the next milestone and retention does not block useful ready work or a lightweight monitor. Record the retention reason and release trigger; architecture acceptance alone is not a reason to retain a thread indefinitely.

When preview exposes genuine owner decisions, invocation of `$orchestrate` is explicit consent to invoke the installed explicit-only `$grill-me` skill. Complete the phase-wide foundation first, then interview one whole task or cohesive milestone at a time. The owner may timebox the session and stop between those complete units. Freeze the interview-complete independent graph, mark the rest `needs-interview`, and execute the ready work without guessing the deferred decisions. Preserve the design tree and frontier in `.tmp/orchestration/<run-id>/preview.md`; stop clearly if `$grill-me` is unavailable.

Transition `preview -> ready -> active` automatically when the frozen packet contains no owner blocker and stays within existing authority. Use `awaiting-owner` when it does not. Immediately before the first implementation writer, emit one concise commentary marker in the form `EXECUTION START — <wave>; ready: <lanes>; deferred/blocked: <summary>`. Do not turn this marker into a high-frequency status stream. A new cross-cutting invariant, missing ADR, dependency seam, invalidated architecture, or material scope expansion pauses only the affected lane, recalls the architecture lead, requires independent review, and re-freezes the affected graph. The root may approve the re-freeze when it preserves the accepted outcome, durable decisions, and authority envelope; otherwise return to `awaiting-owner` while independent ready work continues.

### Durable Orchestration Ledger

When `$orchestrate` activates the workflow, the root must resolve a run ID from `CODEX_THREAD_ID`, falling back to `CODEX_SESSION_ID`. If neither is available, generate a UUID and preserve it explicitly in commentary, the ledger, and subsequent compacted state.

The root must maintain `.tmp/orchestration/<run-id>/state.md` as the compact source of current session state and `.tmp/orchestration/<run-id>/preview.md` as the design-tree and frozen-graph source. Initialize them before the first preview lane. Update the ledger only at recovery-relevant transitions: preview verdict, owner or authority gate, ownership or scope change, blocker, frozen milestone SHA, resource admission/release or threshold crossing, integration/publication result, release gate, and completion. Coalesce near-simultaneous events. Routine spawns, messages, polls, test output, and ordinary agent completions do not require their own patch when the current recovery state is unchanged. Never use the former shared `.tmp/orchestration-state.md` path, a shared `current` pointer, or another run's ledger. Existing ledgers from other runs do not activate this workflow and must not be selected by recency.

Keep the ledger concise and include:

- the current objective and work wave;
- each active or recently completed lane's task, agent path, role, model and reasoning effort, worktree or owned paths, status, and next action;
- counts and identifiers for ready and running lanes, plus blockers using only `none`, `dependency`, `interview`, `owner`, `resource`, `agent-slot`, `review`, or `external`;
- root-only decisions, pending owner gates, blockers, and available agent capacity;
- each retained agent's retention reason, release trigger, and whether release is due; never claim capacity was reclaimed until the active client mechanism confirms it;
- preview status (`preview`, `awaiting-owner`, `ready`, `active`, or `complete`), frozen-artifact paths and exact SHAs, deferred `needs-interview` nodes, and the Git allowlist;
- latest resource sample time, free disk/memory, logical processors, linked-worktree count and reservation, active heavy-operation lease owner, and throttle reason;
- the latest integrated and pushed commit; and
- the next root action and the next actions that must remain delegated.

The root exclusively owns its run-scoped files. Include the run ID and ledger path in every task-agent assignment. Task agents report evidence but do not edit orchestration state. Mark the ledger `complete` only when the objective is complete; recovery must preserve any non-complete preview or execution status.

Do not add new blocker categories or higher-frequency fields merely because they
would be convenient. Expand this minimal schema only after a later evaluation
shows that native transcripts plus these transition records cannot answer a
specific operational question.

### Scheduling And Specialist Lifecycle

The repository config admits up to eight spawned-agent threads, excluding the
primary thread. This is capacity, not a target and not a second resource model.

- Admit useful independent ready work when ownership and machine budgets allow
  it. Do not invent, prematurely grill, or speculatively start work merely to
  fill slots.
- Keep at most two concurrent writers, each with disjoint owned paths and an
  admitted writable worktree when one is required. Keep at most one heavy
  operation family regardless of how many read, review, or edit lanes run.
- Do not impose another universal active-agent cap. Writer/worktree, heavy,
  and spawned-thread capacity remain separate.
- At acceptance, freeze, review-surface change, or a recorded release trigger,
  stop intentional retention and mark a terminal, idle, mailbox-clean
  specialist reclaimable. Retain it only when near-term continuity is
  concretely valuable and does not block a useful ready lane. Never retain a
  specialist merely as insurance in the last available slot.
- On the MultiAgent V2 surface, use `send_message` only for necessary mid-turn
  steering when the target is clearly active and not near completion. Use
  `followup_task` when work must be consumed across an idle or completion
  boundary. Never send speculative, status-only, or late queue-only messages
  to terminal, release-due, or possibly completing agents; pending mail pins a
  terminal resident and can block automatic eviction.
- `reclaimable` records orchestration intent, not proof of physical unload.
  Record eviction only from runtime evidence such as removal from `list_agents`
  or successful admission under known capacity pressure. If spawning reports
  `agent thread limit reached`, inspect live state and retry once only after a
  known mailbox blocker has been consumed with a bounded `followup_task`.
  Otherwise record the blocker and queue the lane.
- Subscription quota is not an admission signal. Do not introspect, estimate,
  conserve, or accelerate quota burn when scheduling. Dispatch useful ready
  work according to the graph, authority, quality gates, and machine budgets.

The validated lifecycle evidence and selected policy are recorded in
[`docs/codex/agent-closure-prerequisite.md`](docs/codex/agent-closure-prerequisite.md).
MultiAgent V2 may automatically evict and later reload clean terminal agents;
it does not provide proactive resource-cleanup evidence. Keep machine-resource
admission separate. If pressure persists without a supported release operation,
stop affected admission and request an owner-controlled end/restart of the
current session. Recover from the ledger and re-sample resources before
admitting work; do not open a concurrent replacement or claim cleanup.

### Resource Admission

Agent capacity, writable-worktree capacity, and heavy-operation capacity are separate budgets. Before creating a worktree or launching a heavy local gate, run `.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1` with the applicable admission mode and the checked-in `.agents/skills/orchestrate/resources/resource-policy.json` profile. Record admission and lease transitions in the ledger.

- Keep at most two writable task worktrees. Reuse a clean admitted worktree for sequential work only after its prior commit is integrated or safely published; remove idle recoverable worktrees promptly.
- Admit only one full build/test or other heavy job family at once. Other agents may edit, research, or review while that lease is occupied.
- The helper's disk and memory gates fail closed. A run-scoped owner override must state the measured shortfall and reserved capacity; a task agent cannot override admission itself.
- Heavy admission uses a `1.10 GiB` available-memory hard floor and a `1.50 GiB` warning threshold. A warning does not deny the sole lease. At admission and release transitions, record the operation type, starting and post-operation available memory, duration, and outcome so the next analysis can calibrate the floor.
- PowerShell `Start-Job` children share the same global heavy-operation lease. Admit the whole job family before launching it.
- Under pressure, queue new heavy work and allow the current bounded command to finish. Do not kill unrelated host processes or delete caches, build trees, worktrees, or user files merely to regain capacity.

### Bounded Git And GitHub Authorization

At initial intake, verify and record the canonical repository identity, remote name and push URL, authenticated account and repository permission, default integration branch, run-scoped branch prefix, and initial local/remote SHA. For this repository the canonical target is `origin` at `https://github.com/ehonda/KicktippAi.git`; fail closed if the observed target differs.

The `$orchestrate` invocation authorizes, for its objective and run only:

- staging owned in-scope paths and creating scoped local commits;
- non-force pushes of exact reviewed or frozen commits to allowlisted `main` or `codex/<run-or-objective>-*` refs using an explicit remote and refspec;
- creating and updating draft PRs for those refs; marking ready or merging only when the frozen packet names the exact base/head and required green checks; and
- one rerun of a failed milestone workflow and cancellation of a superseded run for an allowlisted exact SHA. Repeated failure requires diagnosis, not another retry.

Before every push, record `git branch --show-current`, `git remote -v`, `git status --short --branch`, and `git log -1 --oneline`; verify the exact SHA contains only scoped paths, no secrets, and is a non-force fast-forward to the allowlisted ref. Keep ordinary lane commits local. Publish cohesive milestone commits and recovery-critical long lanes. If publication is rejected, record the exact SHA and reason, continue independent local work only within resource and recovery budgets, and stop admitting new writers before unpublished state becomes unsafe. Never evade or reshape a rejected operation to bypass review.

Authorization expires when the objective completes or stops, or when repository identity, remote URL, branch family, publication topology, or scope changes. Another remote or repository, force or lease-force push, tag/release publication, remote deletion outside agreed temporary-branch cleanup, history rewrite, credential/remote change, unrelated user change, destructive reset/clean/checkout, secrets, and unplanned PR merge require new approval.

### Recovery Preflight

Only while the `$orchestrate` workflow is active, after any compaction or automatic continuation, or whenever current ownership is uncertain, the root must complete this recovery preflight before substantive task work:

1. Re-read `.agents/skills/orchestrate/SKILL.md`, the repository-root `AGENTS.md`, and every applicable nested `AGENTS.md`, task record, execution strategy, and active decision document. During the current Bundesliga 2026/27 work, this includes `plans/bundesliga-2026-27/AGENTS.md`, `plans/bundesliga-2026-27/execution-strategy.md`, the active task file, and its linked ADRs.
2. Resolve the active run ID and read only `.tmp/orchestration/<run-id>/state.md` plus its sibling `preview.md`. If the active run ID cannot be recovered, do not choose a ledger by recency; request user direction. If the exact state is missing or stale, reconstruct and update it before continuing.
3. Inspect live agent state and Git/worktree state rather than relying on the compacted summary alone.
4. Reconcile every active lane's owner, status, model allocation, owned paths, and next action; re-sample resource admission and reconcile the active heavy-operation lease.
5. State in a concise commentary update which next actions belong to the root and which remain delegated.
6. Delegate worker work before doing it inline. If an allowed exception applies, record the reason before starting that work.

Recovery reads, agent-status inspection, Git/worktree inspection, and ledger repair are control-plane work. Do not edit source or planning artifacts, run task validation, or perform substantive research until the preflight is complete.

### Subagent Model Allocation

This section applies only while the `$orchestrate` workflow is active. It explicitly authorizes the root to select model and reasoning-effort overrides for orchestrated task-agent spawns. It applies to a task agent only when the root explicitly authorizes that agent to delegate.

Before the first spawn in a workflow or work wave, classify each planned role and record its model, reasoning effort, fork strategy, and a concise justification. A role mapping may be recorded once and reused for equivalent tasks in the same wave.

Use these starting points:

- Mechanical CI/status/exact-SHA checks and deterministic lookups: `gpt-5.6-luna` / `low`.
- Bounded, well-defined read-only exploration: `gpt-5.6-luna` / `medium` or `gpt-5.6-terra` / `medium`, depending on breadth and ambiguity.
- Normal bounded implementation and deterministic fixes: `gpt-5.6-terra` / `medium`; raise to `high` when the implementation has substantial ambiguity, integration risk, or difficult edge cases.
- Independent correctness, security, or regression review: default to `gpt-5.6-sol` / `xhigh` during this pilot. `gpt-5.6-sol` / `high` is allowed only when the root records that the contract is frozen, the exact commit or tip and owned paths are bounded, acceptance criteria are deterministic, and no ADR, invariant, ownership, architecture, or production-continuity question is open.
- Open-ended or complex research whose conclusions will guide later design or implementation: prefer `gpt-5.6-sol` / `high`. Use a lighter model only when the question is bounded, evidence gathering is mechanical, and the result will receive stronger independent synthesis or review.
- Ambiguous cross-cutting work, launch gates, architecture decisions, or difficult failure analysis: `gpt-5.6-sol` / `high`.

Phase-wide or cross-cutting architecture and its independent specification review always use different `gpt-5.6-sol` / `xhigh` agents in the current pilot because architecture drift multiplies downstream rework. Post-freeze review remains xhigh by default, but the bounded Sol/high downgrade above does not require pretending an exact artifact reopened architecture.

Every override-compatible spawn must explicitly set both `model` and `reasoning_effort`. Omitting either field is a protocol violation.

Use `fork_turns: "none"` or the smallest bounded positive history when the child should differ from the parent. Do not choose a full-history fork merely for convenience. A full-history fork is allowed only when the child intentionally needs the parent's exact model and reasoning effort; record that reason explicitly.

Before repeating an allocation pattern, verify that the first realized child used the intended model and reasoning effort. If it unexpectedly inherited the orchestrator configuration, stop that pattern and correct the spawn strategy.

When a task changes role materially—for example, from mechanical evidence collection to open-ended analysis—reclassify it before assigning a follow-up turn or reuse a differently configured agent.

## Gathering Information

We use different external dependencies, some of which are partially or fully available locally via git submodules.

For routine repository searches, start with repo-owned paths such as `src`, `tests`, `.github`, `.agents`, and only add `docs` when the task needs them.

The repo-root `.ignore` intentionally excludes `external/` from broad `rg` and `rg --files` searches so dependency mirrors do not pollute first-pass results.

Search `external/` only when the task is clearly dependency-specific or when repo-local code points to a dependency. When a submodule is relevant, search the narrowest submodule path directly, for example `rg -n "ChatClient" external/openai/openai-dotnet`. Use `--no-ignore` when you need file discovery inside an ignored tree, for example `rg --no-ignore --files external/openai/openai-dotnet`.

When gathering dependency information like

- Code
- Documentation
- Usage examples

search it in the following places, in that order:

1. The relevant local git submodule (See [Submodule Tree](#submodule-tree))
2. GitHub via MCP
3. Web search

## CSV Context Documents

When creating or updating CSV context documents that may appear in prompts or Langfuse trace views, match the rendering style used by the FIFA ranking docs:

- The first byte of the content should be the first header character; do not add leading blank lines.
- The header row and first data row must be separated by exactly one line terminator.
- Use one record per line and keep rows in deterministic order.
- End every CSV content string with a final trailing line terminator.
- Prefer CRLF line endings for generated CSV context stored in Firestore, matching the currently cleanly rendered ranking documents.
- Use empty fields for genuinely blank values. Use an explicit sentinel such as `N/A` for unavailable supplemental values where an empty field would be ambiguous; do not use `0` to mean unknown.
- For large integer money-like values, use readable thousands separators that do not conflict with the CSV delimiter, for example `15.000.000` for EUR values.

## Git Submodules

### Submodule Tree

@agent-files/submodule-tree.txt

### Updating the Submodules

When you encounter a dependency that is not available locally, and which has a chance of being consulted multiple times, use the `submodules-manage` skill to add it or part of it as a git submodule. This will make it available locally for future reference and easy agentic access.

## Langfuse Agent Tooling

@agent-files/langfuse-agent-tooling.md

Use the installed `langfuse` CLI entrypoint for Langfuse API work. Do not use `npx langfuse-cli` for routine agent workflows with repository secrets; install or update the global `langfuse-cli` package only when the `langfuse` command is missing or stale.

## Langfuse Experiments

The initial Langfuse integration is complete. Treat the active repository docs as the source of truth, and treat the old phase trackers as historical design context.

- For current Langfuse docs, generic API access, prompt management, SDK guidance, and prompt migration, use the official global `$langfuse` skill and the global Langfuse tooling described above.
- For verified repository-specific Langfuse tracing and filtering behavior, read [docs/langfuse.md](docs/langfuse.md).
- For active experiment preparation, execution, analysis, and publishing workflows, read [docs/langfuse/experiments](docs/langfuse/experiments).
- Use `.agents/skills/langfuse-experiments/` for KicktippAi-specific experiment orchestration, statistical report generation, Pages verification, and commit/push workflow.
- Read `plans/langfuse-integration/phase-2/AGENTS.md` and linked trackers only when researching historical implementation decisions or changing experiment behavior.

Hosted Langfuse prompts are an established runtime path, not merely a POC. WM26 used hosted prompts as primary with checked-in files as the outage/first-fetch fallback, and Bundesliga 2026/27 follows the same pattern. Competition configuration and its accepted ADR determine the prompt names and labels; scheduled production must use an explicitly promoted version rather than a floating `latest` label.

## Prediction Validation Safety

- Agents may autonomously write test predictions to `ehonda-dev-buli-2627` only with `gpt-5.6-luna`, reasoning effort `none`, and an explicitly pinned output cap. Prediction quality is irrelevant in this community; use it only to validate plumbing.
- The same Luna/none participant in `ehonda-ai-arena` is authorized for the Bundesliga validation ladder: local CLI, `workflow_dispatch`, then an arena-only schedule with result, Firestore, Langfuse, and ordering inspection.
- Never silently promote the Luna/none validation configuration to production. The project owner selects and approves the final production model, reasoning effort, output cap, prompt versions, cost ceiling, and arena challenger matrix.
- For local community writes, load the matching sibling `.env.<community>` credentials where available. Do not swap or overwrite the base development `.env`, and never print secret values while inspecting configuration.
- Final production schedules remain disabled until the Bundesliga activation task's manual evidence and owner-controlled decisions pass. See [the Bundesliga execution strategy](plans/bundesliga-2026-27/execution-strategy.md), [ADR-0005](plans/bundesliga-2026-27/decisions/0005-launch-community-and-prediction-topology.md), and [ADR-0006](plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md).

## Python Tooling

Use `uv` to manage everything Python-related in this repository, including interpreter selection, virtual environments, dependencies, and repo-local command execution.

When running `uv` from Codex, prefer the repo-local cache form:

```powershell
uv --cache-dir .uv-cache run ...
```

The default Windows uv cache under `%LOCALAPPDATA%` can be blocked by sandbox permissions. The `.uv-cache/` directory is ignored by git. If a `uv` command still fails due to permissions, needs network access, or needs unrestricted external secrets access, rerun that same command outside the sandbox with approval.

When validating Codex skills with the global `skill-creator` validator, use `uv --with PyYAML` because the ambient Python environment often does not have the `yaml` module installed:

```powershell
uv --cache-dir .uv-cache run --with PyYAML python C:\Users\dennis\.codex\skills\.system\skill-creator\scripts\quick_validate.py path\to\skill-folder
```

If this command fails because `PyYAML` needs to be fetched and sandbox networking blocks PyPI, rerun the same command outside the sandbox with approval.

## Codex Sandbox State

This repo does not currently configure `dotnet` or NuGet path overrides through [`.codex/config.toml`](.codex/config.toml).

- `.tmp/` is ignored by git and is safe for ad-hoc repo-local scratch state when a task needs it.
- Run all `dotnet` commands outside the sandbox in this repo for now.
- Routine read-only `git` commands such as `status`, `diff`, and `log` can run in the sandbox, but `git add`, `git commit`, and `git push` should still be run outside the sandbox in this repo for now.
- Fresh-clone setup and one-time trust steps are documented in [docs/codex/efficient-usage.md](docs/codex/efficient-usage.md).

## Running and Filtering Tests

This project uses TUnit for testing, which has some differences compared to more common frameworks like xUnit or NUnit. They are documented in the following sections.

### Running Tests

Always use `dotnet run` instead of `dotnet test` to run TUnit tests:

```powershell
dotnet run --project tests/MyProject.Tests
```

To see available command-line options:

```powershell
dotnet run --project tests/MyProject.Tests -- --help
```

### Filtering Tests

Use `--treenode-filter` to run specific tests. The filter syntax is:

```text
/<Assembly>/<Namespace>/<Class>/<Test>
```

Use `*` as a wildcard and `**` for multi-level matching.

**Common Filter Patterns:**

| Goal | Command |
|------|---------|
| Run all tests in a class | `dotnet run -- --treenode-filter "/*/*/MyTestClass/*"` |
| Run a specific test | `dotnet run -- --treenode-filter "/*/*/*/My_test_name"` |
| Run tests matching a prefix | `dotnet run -- --treenode-filter "/*/*/*/Adding_*"` |
| Run all tests in matching classes | `dotnet run -- --treenode-filter "/*/*/MyService*/**"` |

**Combining Filters:**

Use `&` (AND) and `|` (OR) operators. OR requires parentheses at the name level:

```powershell
# Tests starting with "Valid" OR "Invalid"
dotnet run -- --treenode-filter "/*/*/*/(Valid*)|(Invalid*)"
```

**Filtering by Properties:**

Filter tests by custom properties using `[PropertyName=Value]`:

```powershell
dotnet run -- --treenode-filter "/*/*/*/*[Category=Unit]"
```

### Copilot Auto-Approval Workaround for TUnit Filters

When running filtered TUnit commands through Copilot's terminal tool in PowerShell, inline `--treenode-filter "/*/*/.../*"` arguments may fail terminal auto-approval because VS Code sometimes parses the command as having no sub-commands.

This is a Copilot/VS Code parser workaround, not a TUnit requirement. In a normal terminal, the inline form is still fine.

If you want reliable terminal auto-approval in Copilot, put the filter into a variable first:

```powershell
$filter = '/*/*/MyTestClass/*'
dotnet run --project tests/Orchestrator.Tests -- --treenode-filter $filter
```

This variable-based form has been observed to auto-approve reliably, while the inline quoted filter often does not.

### Listing Available Tests

To see all available tests without running them:

```powershell
dotnet run -- --list-tests
```
