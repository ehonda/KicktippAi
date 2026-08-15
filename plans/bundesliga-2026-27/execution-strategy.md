# Bundesliga 2026/27 execution strategy

- Status: Draft starting point
- Last updated: 2026-08-16
- Implementation gate: Closed until the decision-grilling session is complete and the user confirms shared understanding

This document describes how to execute the tasks in this directory quickly without giving implementation agents authority over unresolved product, data-source, cost, community, or production decisions. The task files and accepted ADRs remain the source of truth for implementation scope and completion criteria.

## Operating principles

- Run P0 as a gated release train, not as one unconstrained request and not as 19 unrelated planning exercises.
- Keep one strong orchestration agent responsible for dependencies, ADR checkpoints, integration, validation evidence, resource scheduling, and launch gates.
- Delegate only bounded work with explicit inputs, outputs, owned paths, tests, and completion criteria.
- Use the existing task record as the implementation contract. Begin a task with a short plan audit, then implement in the same thread unless a missing durable decision requires an ADR and user direction.
- Keep P1 out of the launch path. In particular, do not spend pre-launch time extracting the generic onboarding skill from P1-03.
- Do not activate a production schedule anywhere except P0-19 after its manual evidence and activation decision are complete.

## Execution waves

| Wave | Work | Gate before advancing |
|---|---|---|
| Decision readiness | Resolve the decision register below and complete required manual prerequisites | User confirms shared understanding; required ADRs are accepted |
| Foundations | P0-01 through P0-11 in dependency-safe lanes | Targeted tests pass; identity, data, prompt, model, and community contracts are fixed |
| Context integration | P0-12 first, then P0-13 and P0-14 where file ownership permits | Match and bonus context tests pass; no WM26 or transfer-document leakage |
| Workflows | P0-16, then one copied P0-17 task per selected community | Every workflow is explicit and manual-only; schedules remain disabled |
| Development validation | P0-18 | Positive and fail-closed evidence is recorded from the development environment |
| Production activation | P0-19 | Each activated community passes manual production validation and the activation ADR |

The roster path is the likely critical path: P0-04 -> P0-07 -> P0-08 -> P0-09 -> P0-12 -> P0-14 -> P0-16 -> P0-17 -> P0-18 -> P0-19. Club Elo source acceptance, exact Kicktipp team names, communities, credentials, and model/cost choices are the other likely non-code blockers and should be resolved early.

## Agent roles and task loop

The orchestration agent owns the cross-task plan and final judgment. A task agent owns only its assigned task or explicitly named slice.

For each task:

1. Read this directory's `AGENTS.md`, the plan index, the assigned task, its prerequisites, and linked ADRs.
2. Inspect the actual code and tests, then return a concise implementation plan, affected paths, test commands, and any unresolved decision.
3. Stop that branch of work if an ADR or user decision is missing. Continue independent work that is not downstream of the unresolved choice.
4. Implement the smallest complete change, run targeted validation, and review the diff against every completion criterion.
5. Update the task status and validation evidence.
6. Verify the exact Git target, create a scoped commit, and push according to the selected Git integration policy.

Use separate fact-finding agents only when the fact cannot be established cheaply in the orchestration thread. Agents may gather evidence and propose a recommendation; they may not silently make the user's decisions.

## Conservative parallelism and worktrees

Worktrees are the isolation mechanism for simultaneous writers. The primary checkout remains the integration and coordination checkout; agents must not switch its branch while other work is active.

Provisional starting policy, subject to the decision-grilling session:

- Allow at most two task subagents concurrently in addition to the orchestrator.
- Allow at most two writable worktrees, each on a dedicated branch with disjoint ownership.
- Prefer one writer plus one read-heavy researcher or validator when tasks touch shared composition, catalog, workflow, or documentation files.
- Serialize full solution builds, full test runs, Docker/Testcontainers suites, live external collection, and other resource-heavy commands.
- Let the orchestrator reduce concurrency immediately when the machine, Docker, network, CI, or agent allowance shows pressure.
- Do not permit recursive delegation unless the orchestrator explicitly decides that the expected saving exceeds the additional usage and coordination cost.

Parallelize research, fixture analysis, and clearly separated providers more readily than shared code edits. For the 18-club roster seed, research can be divided into small club batches, but one owner assembles the canonical seed and a separate validation pass checks its provenance and coverage.

## ChatGPT Pro usage policy

OpenAI documents that usage varies with model, task complexity, context, reasoning, tools, retrieval, and caching, and that additional weekly limits may apply. The execution strategy therefore budgets agent work qualitatively rather than pretending that prompt count alone predicts consumption. See [OpenAI Codex pricing and usage limits](https://learn.chatgpt.com/docs/pricing#what-are-the-usage-limits-for-my-plan).

Provisional resource policy:

- Reserve the strongest model and high reasoning for orchestration, ambiguous cross-cutting implementation, launch gates, and difficult failure analysis.
- Prefer the everyday model at balanced reasoning for normal implementation.
- Use the lightweight model only for narrow, deterministic extraction, inventory, or formatting work whose output is checked mechanically.
- Do one task-agent self-review. Add an independent agent review only for high-risk boundaries or a wave-level integration diff, not automatically for every task.
- Run targeted tests per task and broader affected suites at wave gates. Run the complete suite when integration risk justifies it, not once per subagent.
- Keep raw logs and exploration noise out of the orchestration thread. Persist durable findings in task evidence and ADRs so a later wave can start with a compact context.
- Avoid speculative agents, duplicate investigations, and repeated review loops without new evidence.

## Git integration policy

The repository currently builds and tests both pull requests to `main` and pushes to `main`. As inspected on 2026-08-16:

- the default branch is `main`;
- merge commits, squash merges, and rebase merges are allowed;
- automatic branch deletion is disabled;
- native GitHub auto-merge is disabled;
- no repository rulesets were returned;
- classic branch protection could not be inspected with the current authenticated token.

The decision-grilling session must select one of these policies:

### Direct-main integration

- Use worktrees and temporary task branches for isolation when concurrency is needed.
- Integrate completed, validated task commits into the primary `main` checkout in dependency order.
- Push `origin main` explicitly after verifying branch, remote, status, and commit.
- Preserve small, task-scoped commits so code paths and rollback points remain traceable without PR overhead.

### Autonomous PR integration

- Use one branch/worktree per independent lane or coherent wave, not necessarily one PR per tiny task.
- Push each completed task commit to its branch so the repository task instructions remain satisfied.
- Open a PR with task/ADR links and validation evidence.
- Do not require the user to click through routine merges. Either:
  - enable native repository auto-merge and require the Build and Test checks through branch protection/rulesets; or
  - have the orchestration agent wait for the PR checks and merge the green PR through GitHub.
- Stop for user input only on a failed gate, a genuinely new decision, or an unsafe merge—not for routine approval ceremony.

The PR path requires confirming branch protection, enabling native auto-merge if selected, and ensuring the connected GitHub identity can create branches and PRs, observe checks, update branches, merge, and delete task branches if desired.

## Review and validation policy

- Review against task completion criteria and observable behavior, not general style preferences.
- Use one independent integration review at the end of a wave for changes that cross storage identity, context selection, workflow inputs, or production activation.
- Avoid an automatic author-reviewer-fixer loop per task. Escalate to a second pass only when the first review produces a concrete finding.
- Do not run two heavy local test processes concurrently.
- Treat CI as confirmation, not the first place basic correctness is tested.
- Keep production/manual evidence in P0-18 and P0-19 rather than spreading live writes across earlier implementation tasks.

## Decision and manual-step register

The entries below are intentionally unresolved until the user answers them. Agents should establish facts and prepare recommendations, but the user owns each decision.

| Decision or manual step | Why it matters | Required by | Current recommended starting point |
|---|---|---|---|
| Direct-main or autonomous-PR integration | Determines worktree/branch lifecycle, CI timing, and GitHub prerequisites | Before orchestration | Prefer direct main for the deadline unless PR visibility is worth the setup; if PRs are selected, make merging fully autonomous |
| Maximum concurrent agents, writers, and heavy commands | Protects the local machine and weekly agent allowance | Before orchestration | Two task agents, two writable worktrees, and one heavy command at a time |
| Selected development and production communities | Multiplies workflow triads, credentials, validation, and operational risk | Before P0-15 | One safe development community and one canary production community initially |
| Exact Kicktipp team names | Defines the authoritative join manifest | Before P0-04 completes | Collect from the selected development community and validate all 18 one-to-one |
| Authoritative roster membership source and review policy | DuckDB cannot safely determine 2026/27 membership | Before P0-07/P0-08 | Use official Bundesliga/DFB squad pages for a checked-in, source-dated, manually reviewed seed |
| DuckDB enrichment-gap policy | Promoted clubs and some players are absent or stale in the audited artifact | Before P0-07 | Membership remains authoritative; unresolved age, position, valuation, or ID enrichment becomes `N/A` with a coverage report rather than dropping a player |
| Club Elo source/reuse acceptance and fallback | The strength provider must be operationally and legally acceptable | Before P0-10 | Validate Club Elo; fall back to locally computed cross-division Elo only if it is unsuitable |
| Local or hosted prompt route and fallback | Controls reproducibility, trace identity, and operational failure behavior | Before P0-05 | Keep production file-based unless there is a concrete reason to make the hosted POC the launch dependency |
| Launch model, reasoning, output cap, and cost ceiling | Controls prediction quality, trace identity, and spend | Before P0-06 | Pin an explicit configuration after a reproducible pre-launch estimate |
| Credential, Kicktipp membership, Firebase, and Langfuse readiness | Missing access can block validation after code is complete | Before P0-17/P0-18 | Audit names and access early; provision manually before workflow creation finishes |
| Manual development validation ownership and timing | P0-18 needs real writes, traces, negative tests, and a safe overwrite target | Before P0-18 | Reserve a validation window before code freeze and record evidence in the task |
| Production schedules, cutoff spacing, owner, rollback, and activation authority | Controls live posting and recovery | Before P0-19 | Keep schedules disabled until a manual canary succeeds and the activation ADR is accepted |

## Orchestration start condition

Implementation orchestration may begin only after:

- the decision-grilling frontier is empty;
- the user explicitly confirms shared understanding;
- decisions that affect durable implementation are recorded in accepted ADRs;
- immediate manual prerequisites have owners and dates;
- the selected Git and resource policies are executable with the available permissions and machine capacity.

