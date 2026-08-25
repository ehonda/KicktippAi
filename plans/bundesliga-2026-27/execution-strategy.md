# Bundesliga 2026/27 execution strategy

- Status: Accepted starting point
- Last updated: 2026-08-25
- Implementation gate: Ready for an explicit implementation-orchestration request; this planning change does not start implementation

This document describes how to deliver the accepted P0 scope quickly while preserving the project owner's control over the few deliberately late production choices. Task files and accepted ADRs are the implementation contracts.

## Operating principles

- Run P0 as one gated release train rather than unrelated planning exercises.
- Keep one strongest orchestration agent as the control plane for dependency order, ADR gates, integration, validation evidence, machine load, launch gates, and cross-task judgment. It is the coordinator, not the default writer or reviewer.
- Delegate bounded work only, with explicit owned paths, inputs, outputs, tests, and completion criteria.
- Start each task with a short plan audit, then implement in the same thread unless a missing durable decision requires owner direction and an ADR.
- Treat P0-15 context hygiene and P0-16 bonus-context budgeting as launch work. Other P1 tasks do not delay go-live.
- Do not enable final production schedules before P0-21.

## Execution waves

| Wave | Work | Gate before advancing |
|---|---|---|
| Foundations | P0-01 through P0-11 in dependency-safe lanes, leaving P0-06's final production choice open | Identity, storage, prompt, model-test, roster, and Club Elo contracts are fixed; targeted tests pass |
| Context integration | P0-12 through P0-16 plus P0-22 | Match/bonus allowlists, exact history played dates, and budgets pass; no WM26, old-season, stale, duplicate, or transfer context leaks |
| Community workflows | P0-17 through P0-19 for the fixed Luna/none path and production templates | Community matrix is complete; test entrypoints are explicit; final schedules remain disabled |
| Development and arena validation | P0-20 | Dev and arena ladder evidence passes, including fail-closed cases |
| Production evidence and copy safety | P0-23 and P0-24 in parallel | Owner-authorized GPT-5.6 cost/quality evidence or an explicit owner waiver exists; compatible bonus copy is zero-model and ordinary incompatibility produces exactly one independent target prediction |
| Production selection and activation | Finish P0-06, production P0-19 copies, then P0-21 | Owner approves the final model/cost/challenger configuration against the accepted prompt versions and records the Club Elo mode; all production communities pass manual and first scheduled validation |

The implementation path through P0-20 is complete. The closeout path is parallel P0-23 production-candidate evidence and P0-24 bonus copy safety, then the final P0-06 owner decision, production P0-19 copies, and P0-21. Prompt work is already fixed at the accepted numbered versions; Club Elo network reuse remains independently gated and the dated-seed path remains launch-safe.

## Agent roles and task loop

The orchestration agent owns the cross-task plan, delegation, integration checkpoints, and final judgment. A task agent owns only its named task or slice. The default expectation is that bounded implementation work is assigned to a task agent whose capability tier matches the task's risk and ambiguity, while the orchestration agent stays free for coordination and hard decisions.

For each task:

1. Read this directory's `AGENTS.md`, the plan index, the assigned task, its prerequisites, and linked ADRs.
2. Inspect code and tests, then state a concise implementation plan, affected paths, validation, and any genuine unresolved decision.
3. Pause only the affected branch if an owner decision is required; continue independent work.
4. Implement the smallest complete change, run targeted validation, and review the diff against every completion criterion.
5. Update task status and durable validation evidence.
6. Verify the exact Git target, create a scoped commit, and push under the hybrid Git policy.

Fact-finding agents may establish evidence and recommend; they may not silently decide final production model/configuration, Club Elo network reuse, final schedules, or a new product/data policy. Read-only research, status, and CI reconciliation should prefer the fastest reliable agent tier that can accurately gather the evidence.

## Bounded parallelism and worktrees

- Use at most two task agents and two writable worktrees at once. The default for dependency-safe, non-overlapping P0/P1 slices is two isolated writer lanes; fall back to one writer plus one read-heavy helper when a safe second write slice is unavailable.
- Create each writer branch from the primary checkout with `./New-AgentWorktree.ps1 -Name <lane> -Branch <branch> -StartPoint <sha>`. The helper creates the command-line worktree below the ignored repo-local `.tmp/worktrees` directory and installs the required original-checkout locator. Give each lane exact, disjoint path ownership and keep one active writer per worktree.
- Keep the primary checkout as an integration-only checkout while lanes are active. The orchestrator creates worktrees, reviews frozen lane commits, and integrates them sequentially; lane agents never mutate `main` or another lane's worktree.
- `New-AgentWorktree.ps1` writes and validates the ignored `.codex-local/original-repository-path` locator in every command-line worktree. The locator contains only the canonical primary-checkout path and allows repository code to resolve the sibling `KicktippAi.Secrets` checkout without copying or printing credentials. Do not replace the helper with a raw `git worktree add`. `.worktreeinclude` remains optional support for Codex desktop-managed worktrees; command-line worktree creation does not process it.
- Let isolated lanes run their lane-local builds and tests concurrently by default, including full solution/test gates when each lane requires them. Reduce command or lane concurrency only in response to measured CPU, memory, disk, network, test-port, or allowance pressure; do not serialize merely because commands are heavy.
- During P0/P1, every test project using WireMock or an equivalent local listener that triggers Defender must set `<UseAppHost>false</UseAppHost>` so unattended worktrees use the stable installed `dotnet.exe` host. Adding such a listener to another project requires adding and validating the setting there. Re-evaluate and remove this temporary convention after P1; production projects and unrelated test projects remain unchanged.
- Serialize Git integration and primary-checkout mutation, live external collection or writes, and final integrated validation against the exact combined head.
- Each lane verifies its exact Git target, commits only owned paths, and pushes its explicit experiment/task branch. The orchestrator then integrates the branch commits in dependency order.
- Worktree cleanup is a completion gate, not optional housekeeping: after each lane commit is pushed and integrated or otherwise recoverable, verify the worktree is clean, remove it, prune stale worktree metadata, and confirm no temporary worktrees remain. Remote lane branches may remain for recoverability unless separately removed.
- Do not recursively delegate unless the orchestrator explicitly determines that the bounded saving justifies the coordination cost.
- For the 18-club fallback seed, small research batches are acceptable, but one owner assembles the canonical seed and one targeted independent audit checks provenance and coverage.

## Codex usage policy

Agent usage varies with model, task complexity, context, reasoning, tools, retrieval, and caching. Budget qualitatively rather than treating prompt count as a reliable allowance measure. See [OpenAI Codex pricing and usage limits](https://learn.chatgpt.com/docs/pricing#what-are-the-usage-limits-for-my-plan).

- Reserve the strongest capability tier and highest reasoning for orchestration, ambiguous cross-cutting implementation, launch gates, and difficult failure analysis. Do not treat the top tier as the routine default for every writer, reviewer, or status check.
- Prefer a balanced everyday capability tier for normal implementation and a lightweight tier for narrow deterministic work, read-only research, status gathering, and mechanical verification.
- Use one task-agent self-review during implementation. Add an independent dedicated review agent only for high-risk artifacts or wave integration, size that reviewer to the review risk, and repeat only after a concrete finding.
- Run targeted tests per task and broader affected suites at wave gates. Avoid redundant full-suite runs, but let both lanes run required full gates concurrently when branch independence or the task contract calls for them.
- Persist decisions and evidence in tasks/ADRs so later waves do not repeatedly rediscover them.
- Avoid speculative agents, duplicate investigations, and routine author-reviewer-fixer loops.

## Git integration policy

Use the accepted hybrid policy from [ADR-0009](decisions/0009-bounded-orchestration-and-hybrid-git.md):

- Integrate isolated, low-risk changes directly to `main` as small scoped commits.
- Use a coherent branch/worktree and PR for cross-cutting or high-risk changes when CI and review visibility materially help.
- Do not require user clicks for routine PR merges. The orchestrator waits for checks, resolves in-scope failures, updates the branch if required, and rebase-merges a green PR.
- Native GitHub auto-merge and new branch-protection setup are not launch prerequisites.
- Before every push, record branch, remotes, status, and latest commit, then push an explicit remote and branch.

The repository currently builds/tests PRs and pushes to `main`; native auto-merge is disabled. The orchestrator must verify actual permissions and applicable checks when it first selects the PR route.

## Validation policy

- Review against task completion criteria and observable behavior.
- Concentrate independent review on storage identity, roster provenance/source switching, context selection, workflow inputs, prompt promotion, and activation.
- Treat CI as confirmation, not the first correctness check.
- Keep real-write evidence in P0-20 and P0-21 rather than scattering it across provider implementation tasks.
- Preserve historical partitions. Any proposed remote deletion requires an explicit dry-run inventory and separate authorization.

### CI reconciliation loop

- After every push and at every wave gate, assign one dedicated read-only CI reconciliation agent. It may inspect GitHub state and logs but must not push, rerun, cancel, approve, or otherwise mutate CI state.
- Record the exact local and remote head SHA, workflow run ID and status/conclusion, and every relevant job ID, name, status/conclusion, and URL in the active task or wave evidence. Reconcile the run's head SHA with the pushed commit before treating a result as current.
- Route a trivial in-scope failure, such as formatting or a deterministic test correction that does not change an accepted contract, immediately back to the writer that owns that change. After the fix is pushed, repeat the read-only reconciliation loop against the new head.
- For a nontrivial, cross-task, flaky, infrastructure, or policy-sensitive failure, the reconciliation agent reports the evidence and the orchestrator creates or links a durable work item with the failing head/run/job evidence, owner, scope, and dependencies. Keep independent work moving when its gates do not depend on that failure; do not silently broaden the active task.
- A wave gate remains closed until every required check for its exact head succeeds or a linked accepted decision explicitly changes the gate.

## Resolved decisions

| Area | Accepted direction |
|---|---|
| Git and isolation | Hybrid direct-main/PR integration; worktrees for simultaneous writers; routine merges autonomous |
| Capacity | At most two task agents and two writable worktrees; lane-local builds/tests may overlap, with concurrency reduced only on measured pressure |
| Communities | Dev: `ehonda-dev-buli-2627`; production: `pes-squad`, `schadensfresse`, `ehonda-ai-arena` |
| Prediction topology | `pes-squad` reference; `schadensfresse` independent; matching production arena entry copy-posted from `pes-squad`; challengers independent |
| Rosters | DuckDB primary per valid 2026/27 club; complete one-time fallback seed; last-known-good on invalid data; `N/A` enrichment gaps |
| Prompts | Accepted hosted match v2 and bonus v1, with required `production` membership for scheduled/hosted-required routes; checked-in local mirrors remain the ordinary outage fallback |
| Plumbing model | `gpt-5.6-luna`, `none` reasoning, pinned output cap; never promote silently to production |
| Context | Explicit live allowlists; stale/duplicate team/manager cleanup and question-aware bonus budgeting before production |
| Club Elo | Implement provider/cache/gates now; a complete dated seed is launch-safe; network use remains an owner gate |
| Activation | Dev and arena ladder first; final production manual dispatch and inspection; then enable and observe schedules |

## Prerequisite state

Confirmed by the project owner on 2026-08-16:

- `ehonda-dev-buli-2627` and `ehonda-ai-arena` are configured, with a `gpt-5.6-luna`/`none` participant registered in each.
- The arena sibling `.env` and its model-specific GitHub Actions Kicktipp secrets are updated.
- Existing local and GitHub Actions Firebase, OpenAI, Langfuse, and other shared credentials remain valid from prior WM26 runs.
- The base local `.env` remains the development credential source.

P0-17 recorded posting-target credential resolution and the implementation now loads a present `.env.<posting-community>` for ordinary local arena validation without replacing the shared base environment. Agents inspect names/presence without printing secret values.

The connected GitHub token could not enumerate Actions secret names, so the owner's provisioning confirmation remains the planning source of truth. P0-20 subsequently verified the Luna arena workflow connectivity and fail-closed behavior without exposing values; production-participant credentials remain a P0-21 runtime gate.

## Deliberately late owner gates

These are not ambiguities agents may decide on their own:

| Decision | Timing | Work that may proceed first |
|---|---|---|
| Final production model, reasoning, output cap, service/fallback policy, arena challengers, and cost ceiling; carry accepted match v2/bonus v1 unless a successor ADR changes them | Late in P0, after P0-23 comparative evidence or an explicit owner waiver and before production onboarding/dispatch | Completed Luna/none plumbing and cost row; owner-authorized candidate evidence preparation |
| Whether Club Elo terms permit unattended network refresh, or which permitted alternative to use | Late before go-live | Provider boundary, validation, cache, dated seed, and last-known-good behavior |
| Exact production schedules, spacing, rollback trigger, and activation | P0-21 after manual evidence | Manual-only workflows and the arena validation schedule |
| `pes-squad` and `schadensfresse` season setup | Later, before their production validation | All foundations, dev/arena validation, and workflow templates |

Final model selection will mix experiments and whole-season cost estimates. New-season outcomes do not exist, older-season evaluation may be training-contaminated, and the retired o3 configuration is not a season-long option; a GPT-5.6 variant is the tentative direction, not an implementation default.

## Orchestration start condition

The ambiguity-grilling session is complete and the durable decisions are recorded. Implementation may begin when the user explicitly requests orchestration. The orchestrator then keeps late gates visible, schedules manual prerequisites near their dependent tasks, and never substitutes an agent preference for an owner decision.
