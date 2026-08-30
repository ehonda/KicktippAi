# Bundesliga 2026/27 execution strategy

- Status: Accepted execution strategy; P0 complete, P1 next
- Last updated: 2026-08-30
- Implementation state: P0-01 through P0-25 are complete; P1-10 is in progress and its immediate first change quarantines the unsafe schadensfresse scheduled pair

This document describes how to deliver the accepted P0 scope quickly while preserving the project owner's control over the few deliberately late production choices. Task files and accepted ADRs are the implementation contracts.

## P0 closeout — 2026-08-28

Natural scheduled run
[`33143114280`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280)
succeeded on exact `main` head
`50f3ed148891977b5909659f9986c9c9958d7875`. All 16 jobs followed the accepted
eight-pair serial topology. Context remained on the approved dated Club Elo and
enriched roster snapshots; all match jobs verified 9/9 current predictions and
skipped generation/posting, so the run produced no write, reprediction, usage,
or cost. GitHub delivered the nominal 02:07 UTC event 2h46m22s late, outside
the 90-minute monitoring envelope, but the 38m46s execution succeeded before
the 09:07 UTC occurrence with no overlap. [P0-21](tasks/p0-21-production-activation.md)
holds the complete job-level evidence.

This section supersedes later execution-state text that directs agents to
continue P0-21. The central P0 train is closed and P1 is next. P1-08 is
superseded and fully absorbed by P1-10; Club Elo network refresh and exploratory
model follow-ups also remain outside P0.

## Operating principles

- Run P0 as one gated release train rather than unrelated planning exercises.
- Keep one strongest orchestration agent as the control plane for dependency order, ADR gates, integration, validation evidence, machine load, launch gates, and cross-task judgment. It is the coordinator, not the default writer or reviewer.
- Delegate bounded work only, with explicit owned paths, inputs, outputs, tests, and completion criteria.
- Start each task with a short plan audit, then implement in the same thread unless a missing durable decision requires owner direction and an ADR.
- Treat P0-15 context hygiene and P0-16 bonus-context budgeting as launch work. Other P1 tasks do not delay go-live.
- Apply ADR-0058's immediate quarantine before other P1-10 implementation:
  remove both scheduled schadensfresse jobs and reconnect relaxdays directly
  after `pes-squad-matchday`. Preserve the resulting seven-pair serial
  topology, exact ADR-0053 cadence/concurrency/failure/no-bonus/rollback
  contract, and manual-only leaves until reviewed primary activation.

## Execution waves

| Wave | Work | Gate before advancing |
|---|---|---|
| Foundations | P0-01 through P0-11 in dependency-safe lanes, leaving P0-06's final production choice open | Identity, storage, prompt, model-test, roster, and Club Elo contracts are fixed; targeted tests pass |
| Context integration | P0-12 through P0-16 plus P0-22 | Match/bonus allowlists, exact history played dates, and budgets pass; no WM26, old-season, stale, duplicate, or transfer context leaks |
| Community workflows | P0-17 through P0-19 for the fixed Luna/none path and production templates | Community matrix is complete; leaf entrypoints are explicit and manual-only |
| Development and arena validation | P0-20 | Dev and arena ladder evidence passes, including fail-closed cases |
| Production evidence and copy safety | P0-23 and P0-24 complete | Owner-authorized GPT-5.6 cost/quality evidence is published with Luna/`max` explicitly incomplete and post-hoc Sol/`xhigh` exploratory; P0-24 proves compatible bonus copy is zero-model and ordinary incompatibility produces exactly one independent target prediction |
| Launch roster remediation | P0-25 complete | ADR-0051's explicit overlay republish passed the final reconstructed 18-team / 18-derived-row / 464-age / 464-position / 450-value gate from exact-green main; the headed snapshot remained unchanged, and exactly one authorized Luna/none index-0 replacement round passed payload-safe pre/post and trace validation |
| Production selection and activation | Complete through P0-21 | Ordered manual validation and the first natural ADR-0053/0055 scheduled sequence are green |

The implementation path through P0-25 is complete, including P0-06 and every
schedule-free P0-19 row. P0-21 preserved P0-25's enriched-publication
precondition and the exact context-first, primary-before-secondary order.
Bundesliga P0 prompt work is fixed at match v3 / bonus v1. ADR-0058's distinct
DFB/CL prompt routes remain fail closed until immutable promotion. Club Elo
network reuse remains independently Owner-gated P1 work and the dated-seed path
remains launch-safe.

## P1-10 current safety boundary — 2026-08-30

Authenticated read-only evidence invalidated schadensfresse's copy premise:
match scoring is now `2/3/5` for wins and `3/-/5` for draws, bonus answers score
nine points, and three open CL questions are due
`2026-09-08T16:45:00Z`. [ADR-0058](decisions/0058-make-schadensfresse-a-competition-typed-primary.md)
makes P1-10 the sole primary-routing owner and supersedes P1-08.

Before the next nominal `2026-08-30T09:07:00Z` outer occurrence, the repository
schedule must delete `schadensfresse-context` and `schadensfresse-matchday` and
set `relaxdays-tippt-context.needs: pes-squad-matchday`. Every other outer-lane
contract remains unchanged. This fail-safe authorization removes execution
only; it does not dispatch, cancel a run, call a model, replace/delete a
prediction, POST, mutate Firestore/Langfuse, promote a prompt, or change a
credential.

DFB/CL implementation uses ADR-0058's Accepted rules-only profiles: exact typed
fixture/question inputs plus only the hash-bound target rules document, with a
one-document/2048-estimated-token budget, no-older-than-24-hours authenticated
rules evidence, canonical provenance, and no Bundesliga team, Club Elo, roster,
history, generic-latest, or cross-community leakage. This is deliberately safe
but evidence-poor; richer cross-competition context needs an Accepted successor.

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
| Communities | Dev: `ehonda-dev-buli-2627`; production: `pes-squad`, `schadensfresse`, `relaxdays-tippt`, `ehonda-ai-arena` |
| Prediction topology | Independent primary `pes-squad`; relaxdays and arena Sol/xhigh copy `pes-squad`; four arena challengers are independent; schadensfresse is quarantined from recurring execution while P1-10 implements one target-owned primary for Bundesliga/DFB/CL match and bonus work |
| Rosters | DuckDB primary per valid 2026/27 club; complete one-time fallback seed; last-known-good on invalid data; `N/A` enrichment gaps |
| Launch roster publication | ADR-0050 v2 adds one final known-value subtotal row per team; ADR-0051's paired explicit overlay preserves authoritative seed/LKG membership, adds supplemental fields only by exact stable ID, and gates the strictly reconstructed final payload at 18 teams / 18 derived rows / 464 ages / 464 positions / 450 values before write. ADR-0052 prepares a false-by-default workflow input enabled for pes/relaxdays/schadens before normal profile collection; arena preserves its verified enriched head; recurring automation stays P1-05 |
| Prompts | Accepted hosted match v3 and bonus v1, with required `production` membership for live routes; historical P0-23 remains on v2; checked-in local mirrors remain the ordinary outage fallback |
| Plumbing model | `gpt-5.6-luna`, `none` reasoning, pinned output cap; never promote silently to production |
| P0-23 candidate evidence | Complete under ADR-0049 and the Owner's execution-time amendments: eight original paired runs completed, Luna/`max` is incomplete after two transient capacity failures and an explicit p1 stop, and post-hoc Sol/`xhigh` is exploratory; this is evidence, not production selection |
| Context | Bundesliga retains explicit live allowlists; schadensfresse DFB/CL uses ADR-0058's Accepted target-rules-only profiles with exact typed inputs and no Bundesliga context leakage |
| Club Elo | Implement provider/cache/gates now; a complete dated seed is launch-safe; network use remains an owner gate |
| Production identity | `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first with Standard fallback; USD 35 is planning orientation only |
| Arena challengers | Sol/high, Luna/medium, Terra/xhigh, Luna/none; cap `10000`, match v3 / bonus v1 |
| Activation | Every leaf caller remains manual-only; ADR-0058 quarantines the scheduled schadensfresse context/copy pair, leaving seven pairs/14 jobs at `7 2,9 * * *` until separately reviewed primary activation; historical natural observation `33143114280` closed P0-21 |

## Prerequisite state

Confirmed by the project owner on 2026-08-16:

- `ehonda-dev-buli-2627` and `ehonda-ai-arena` are configured, with a `gpt-5.6-luna`/`none` participant registered in each.
- The arena sibling `.env` and its model-specific GitHub Actions Kicktipp secrets are updated.
- Existing local and GitHub Actions Firebase, OpenAI, Langfuse, and other shared credentials remain valid from prior WM26 runs.
- The base local `.env` remains the development credential source.

P0-17 recorded posting-target credential resolution and the implementation now loads a present `.env.<posting-community>` for ordinary local arena validation without replacing the shared base environment. Agents inspect names/presence without printing secret values.

The connected GitHub token could not enumerate Actions secret names. On
2026-08-27 the Owner confirmed every canonical ADR-0052 Kicktipp pair
provisioned; that confirmation remains the planning source of truth and does
not itself replace authentication, readiness, or POST evidence. P0-21 later
recorded that runtime evidence.

## Post-P0 deliberately late gates

These are not ambiguities agents may decide on their own:

| Decision | Timing | Work that may proceed first |
|---|---|---|
| Final production model, reasoning, output cap, service/fallback policy, arena challengers, and planning ceiling | Resolved by ADR-0052 on 2026-08-27 | Configuration is live and its first natural scheduled verification is green |
| Whether Club Elo terms permit unattended network refresh, or which permitted alternative to use | P1-04 after launch | Continue the accepted dated seed and last-known-good behavior until resolved |
| Exact production schedules, spacing, rollback trigger, and activation | ADR-0053/0055 remain historical launch decisions; ADR-0058 immediately quarantines the schadensfresse pair while preserving every unaffected schedule contract | Run the seven safe pairs only; schadensfresse primary reactivation needs separate review and manual evidence |
| `schadensfresse` primary routing | P1-10 before the corrected `2026-09-08T16:45:00Z` CL bonus deadline and later cup finals; P1-08 is superseded | Implement typed identities plus Accepted rules-only DFB/CL profiles while schadensfresse remains absent from recurring execution |

ADR-0052 settled final model selection from the completed experiments and
whole-season estimates. New-season outcomes did not exist at selection time,
and older-season evaluation may be training-contaminated; later exploratory
evidence does not silently alter the accepted production default.

## P1 start condition

P0 is complete, including P0-21's natural scheduled observation. Begin bounded
P1 work from the task ledger. Apply ADR-0058's schadensfresse quarantine while
preserving ADR-0053/0055's unaffected outer-lane and manual-only contracts; do
not infer bonus or mixed-competition schedule authority. P1-10 owns every
`schadensfresse` Bundesliga/DFB/CL match and bonus route and fully absorbs
P1-08. P1-04 owns any unattended Club Elo network refresh decision.
