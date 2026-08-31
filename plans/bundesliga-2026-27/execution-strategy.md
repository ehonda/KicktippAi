# Bundesliga 2026/27 execution strategy

- Status: Accepted execution strategy; P0 complete, P1 restarts under ADR-0061
- Last updated: 2026-08-31
- Implementation state: P0-01 through P0-25 are complete. The failing P1-10
  runtime is preserved for an atomic future PR; recovery `main` temporarily
  restores the eight-pair source-copy lane under ADR-0062 through
  `2026-09-08T12:00:00Z`.

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
- Under [ADR-0061](decisions/0061-preview-and-milestone-orchestration.md), audit the whole requested phase before the first writer. Freeze the dependency graph, seams, milestones, owner/external gates, production-continuity declaration, Git targets, resource budget, and review/CI cadence. Create the substantive P1 execution packet only from that preview.
- Use `$grill-me` automatically for readiness defects. Finish the phase foundation first, then fully grill one task or cohesive milestone at a time; a timeboxed owner session may release the completed independent subgraph and defer the rest as `needs-interview`.
- Give cross-cutting or high-risk architecture and independent specification review to different `gpt-5.6-sol` / `xhigh` agents. Recall the architecture lead when a semantic scope-growth trigger invalidates a frozen seam.
- Treat P0-15 context hygiene and P0-16 bonus-context budgeting as launch work. Other P1 tasks do not delay go-live.
- Apply [ADR-0062](decisions/0062-temporarily-restore-schadensfresse-copy.md)'s
  temporary recovery: restore target-owned Schadensfresse context and its
  `pes-squad` source-compatible copy match after `pes-squad-matchday`, then
  make relaxdays depend on it. Preserve the eight-pair serial topology, exact
  ADR-0053 cadence/concurrency/failure/no-bonus/rollback contract, and
  manual-only leaves. The route expires at `2026-09-08T12:00:00Z`; no manual
  copy contingency or primary activation follows from it.

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

## P1-10 recovery safety boundary — 2026-08-31

Authenticated read-only evidence invalidated schadensfresse's copy premise:
match scoring is now `2/3/5` for wins and `3/-/5` for draws, bonus answers score
nine points, and three open CL questions are due
`2026-09-08T16:45:00Z`. [ADR-0058](decisions/0058-make-schadensfresse-a-competition-typed-primary.md)
makes P1-10 the sole primary-routing owner and supersedes P1-08.

The failing head `71637cc154cfdcbe2436069470b5e04b0d4f753d` has green
Build-and-Test run `33340578338`, but production-live runs `33350964121` and
`33377913801` fail on the `pes-squad` ordinary blank typed-fixture validation
before model/post work. ADR-0062 restores the eight-pair/16-job copy lane from
the `3a2ba35529b262327a3ec08e6bde47b186c8e5b2` runtime baseline, retaining
P1-09/P1-12. It uses target context, `pes-squad` source context, target
credentials, zero expected copy model calls, and fail-closed compatibility.
The resulting checked-in recovery grants no dispatch, cancellation, model call,
replacement/delete, POST, Firestore/Langfuse mutation, prompt promotion, or
credential change.

DFB/CL implementation uses ADR-0058's Accepted rules-only profiles: exact typed
fixture/question inputs plus only the hash-bound target rules document, with a
one-document/2048-estimated-token budget and no Bundesliga team, Club Elo,
roster, history, generic-latest, or cross-community leakage. ADR-0059 makes the
typed `schadensfresse-live-rules-v1` record/hash—not ADR-0058's legacy
keyword-array hash—the no-older-than-24-hours semantic publication, freshness,
and canonical provenance gate. This is deliberately safe but evidence-poor;
richer cross-competition context needs an Accepted successor.
ADR-0060 keeps each prediction's generation manifest immutable and moves
current freshness to a separately stored publication binding addressed only
by the exact season/community/profile/routing-seed key. An identical fresh
authenticated re-attestation may validate reuse with no model call and no
prediction mutation; generation and current observations remain distinct.
The binding refresh proves only rules/profile/seed/document identity; reuse
separately compares the current typed invocation and exact pinned prompt/model
configuration with the prediction's immutable provenance.

## Agent roles and task loop

The orchestration agent owns the cross-task plan, delegation, integration checkpoints, and final judgment. A task agent owns only its named task or slice. The default expectation is that bounded implementation work is assigned to a task agent whose capability tier matches the task's risk and ambiguity, while the orchestration agent stays free for coordination and hard decisions.

For each frozen task or milestone:

1. Read this directory's `AGENTS.md`, the plan index, frozen phase packet, assigned task/design, prerequisites, and linked ADRs.
2. Confirm the owned paths, inputs, outputs, focused validation, production-impact declaration, and exact handoff boundary. A writer does not redesign a frozen seam inline.
3. Pause only the affected branch for a semantic scope-growth trigger or owner decision; recall the architecture lead and continue proven-independent work.
4. Implement the smallest complete frozen change, run focused validation, and self-review against every completion criterion.
5. Commit the owned local scope. Independently review exceptional high-risk lanes and the exact cohesive milestone SHA; update compact task status and durable evidence links.
6. Publish the reviewed milestone or recovery-critical long lane under the frozen Git topology, run full CI for the published milestone, and reconcile the exact SHA.

Fact-finding agents may establish evidence and recommend; they may not silently decide final production model/configuration, Club Elo network reuse, final schedules, or a new product/data policy. Read-only research, status, and CI reconciliation should prefer the fastest reliable agent tier that can accurately gather the evidence.

## Bounded parallelism and worktrees

- Use at most two simultaneous writers and two writable task worktrees, but admit actual concurrency from the frozen dependency graph and `.agents/skills/orchestrate/resources/resource-policy.json`. Read-only/review agent capacity remains separate. Default to at most two writers touching related architecture; additional available agent slots do not prove machine or seam capacity.
- Before each worktree or heavy local gate, run `.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1` in the applicable admission mode and record lease transitions. On this host the default heavy-operation budget is one, so a second writer may edit/research/review while its full validation waits.
- Create each admitted writer branch from the primary checkout with `./New-AgentWorktree.ps1 -Name <lane> -Branch <branch> -StartPoint <sha>`. The helper fails closed on worktree resource admission, creates the command-line worktree below ignored `.tmp/worktrees`, and installs the required original-checkout locator. Give each lane exact, disjoint path ownership and keep one active writer per worktree.
- Keep the primary checkout as an integration-only checkout while lanes are active. The orchestrator creates worktrees, reviews frozen lane commits, and integrates them sequentially; lane agents never mutate `main` or another lane's worktree.
- `New-AgentWorktree.ps1` writes and validates the ignored `.codex-local/original-repository-path` locator in every command-line worktree. The locator contains only the canonical primary-checkout path and allows repository code to resolve the sibling `KicktippAi.Secrets` checkout without copying or printing credentials. Do not replace the helper with a raw `git worktree add`. `.worktreeinclude` remains optional support for Codex desktop-managed worktrees; command-line worktree creation does not process it.
- Treat focused builds/tests as lane checks and full solution/test or multi-job families as heavy operations sharing the root-owned lease. `Start-Job` children cannot bypass that budget. Queue excess heavy work; do not kill unrelated processes or delete caches/user files to regain capacity.
- During P0/P1, every test project using WireMock or an equivalent local listener that triggers Defender must set `<UseAppHost>false</UseAppHost>` so unattended worktrees use the stable installed `dotnet.exe` host. Adding such a listener to another project requires adding and validating the setting there. Re-evaluate and remove this temporary convention after P1; production projects and unrelated test projects remain unchanged.
- Serialize Git integration and primary-checkout mutation, live external collection or writes, and final integrated validation against the exact combined head.
- Each lane verifies its exact local target and commits only owned paths. Keep ordinary lane branches local; push recovery-critical long lanes and cohesive milestone commits under the frozen topology. The orchestrator integrates reviewed commits in dependency order.
- Worktree cleanup is a completion gate, not optional housekeeping: after each lane commit is integrated or safely published, verify the worktree is clean, remove it, prune stale metadata, and confirm only actively admitted worktrees remain. A clean admitted worktree may be reused for sequential work after its prior commit is recoverable.
- Do not recursively delegate unless the orchestrator explicitly determines that the bounded saving justifies the coordination cost.
- For the 18-club fallback seed, small research batches are acceptable, but one owner assembles the canonical seed and one targeted independent audit checks provenance and coverage.

## Codex usage policy

Agent usage varies with model, task complexity, context, reasoning, tools, retrieval, and caching. Budget qualitatively rather than treating prompt count as a reliable allowance measure. See [OpenAI Codex pricing and usage limits](https://learn.chatgpt.com/docs/pricing#what-are-the-usage-limits-for-my-plan).

- Reserve the strongest capability tier and highest reasoning for orchestration, launch gates, and difficult failure analysis. During the ADR-0061 pilot, cross-cutting/high-risk architecture and its independent specification review are the explicit exception: both always use different `gpt-5.6-sol` / `xhigh` agents.
- Prefer a balanced everyday capability tier for normal implementation and a lightweight tier for narrow deterministic work, read-only research, status gathering, and mechanical verification.
- Use one task-agent self-review during implementation. Independently review frozen milestone SHAs and exceptional high-risk lanes; repeat only after a concrete finding.
- Run focused tests per lane and broader affected suites at published milestone gates. Avoid redundant full-suite runs and honor the global heavy-operation lease even when branches are independent.
- Persist decisions and evidence in tasks/ADRs so later waves do not repeatedly rediscover them.
- Avoid speculative agents, duplicate investigations, and routine author-reviewer-fixer loops.

## Git integration policy

Use the preview-and-milestone policy from [ADR-0061](decisions/0061-preview-and-milestone-orchestration.md):

- Integrate independently production-safe changes directly to `main` as small,
  cohesive milestone commits when that route remains useful.
- Use an integration branch and draft PR whenever an intermediate milestone
  temporarily disables or regresses active production behavior. Cross-cutting
  work that remains production-safe may still use either route by judgment.
- A temporary production-safety quarantine requires explicit Owner approval
  of the exact impact, fallback, rollback, recovery owner, and recovery
  deadline. It is not an implicit consequence of implementation convenience.
- Publish and gate cohesive milestones rather than every lane checkpoint. Keep
  shorter lane commits local; publish a recovery-critical long lane when loss
  of the local worktree would be material.
- Do not require user clicks for routine draft-PR maintenance or a ready/merge
  transition whose frozen exact base/head and required checks have already
  passed. The orchestrator updates the branch if required and rebase-merges a
  green PR.
- Native GitHub auto-merge and new branch-protection setup are not launch prerequisites.
- Before every push, record branch, remotes, status, and latest commit, then
  push an explicit remote and branch. The active `$orchestrate` invocation is
  the bounded authorization for non-force pushes and draft-PR lifecycle
  operations against the startup-verified canonical repository; repository
  policy and platform approval still apply.

The repository currently builds/tests PRs and pushes to `main`; native auto-merge is disabled. The orchestrator must verify actual permissions and applicable checks when it first selects the PR route.

## Validation policy

- Review against task completion criteria and observable behavior.
- Concentrate independent review on storage identity, roster provenance/source switching, context selection, workflow inputs, prompt promotion, and activation.
- Treat CI as confirmation, not the first correctness check.
- Keep real-write evidence in P0-20 and P0-21 rather than scattering it across provider implementation tasks.
- Preserve historical partitions. Any proposed remote deletion requires an explicit dry-run inventory and separate authorization.

### Milestone CI reconciliation loop

- After each published milestone and at every wave gate, use one CI
  reconciliation thread while its context remains valid. It may inspect
  GitHub state and logs, rerun one failed milestone run, and cancel a run whose
  head has been superseded by a newer frozen milestone. It must not repeatedly
  rerun failures, approve deployments, change settings, or widen release
  authority.
- Record the exact local and remote head SHA, workflow run ID and status/conclusion, and every relevant job ID, name, status/conclusion, and URL in the active task or wave evidence. Reconcile the run's head SHA with the pushed commit before treating a result as current.
- Route a trivial in-scope failure, such as formatting or a deterministic test correction that does not change an accepted contract, immediately back to the writer that owns that change. After the fix is pushed, repeat the read-only reconciliation loop against the new head.
- For a nontrivial, cross-task, flaky, infrastructure, or policy-sensitive failure, the reconciliation agent reports the evidence and the orchestrator creates or links a durable work item with the failing head/run/job evidence, owner, scope, and dependencies. Keep independent work moving when its gates do not depend on that failure; do not silently broaden the active task.
- A wave gate remains closed until every required check for its exact head succeeds or a linked accepted decision explicitly changes the gate.

## Resolved decisions

| Area | Accepted direction |
|---|---|
| Git and isolation | ADR-0061 production-safe direct-main/PR integration; mandatory integration branch for temporary production regression; reusable worktrees for simultaneous writers; bounded routine draft-PR lifecycle autonomous |
| Capacity | Admit at most two linked task worktrees and use a separate heavy-operation lease; the checked-in resource policy and live snapshot may reduce concurrency before any new worktree or heavy operation |
| Communities | Dev: `ehonda-dev-buli-2627`; production: `pes-squad`, `schadensfresse`, `relaxdays-tippt`, `ehonda-ai-arena` |
| Prediction topology | Independent primary `pes-squad`; relaxdays and arena Sol/xhigh copy `pes-squad`; four arena challengers are independent; recovery `main` temporarily uses target-context Schadensfresse copy from `pes-squad` in the eight-pair lane, while P1-10's target-primary route remains PR-only |
| Rosters | DuckDB primary per valid 2026/27 club; complete one-time fallback seed; last-known-good on invalid data; `N/A` enrichment gaps |
| Launch roster publication | ADR-0050 v2 adds one final known-value subtotal row per team; ADR-0051's paired explicit overlay preserves authoritative seed/LKG membership, adds supplemental fields only by exact stable ID, and gates the strictly reconstructed final payload at 18 teams / 18 derived rows / 464 ages / 464 positions / 450 values before write. ADR-0052 prepares a false-by-default workflow input enabled for pes/relaxdays/schadens before normal profile collection; arena preserves its verified enriched head; recurring automation stays P1-05 |
| Prompts | Accepted hosted match v3 and bonus v1, with required `production` membership for live routes; historical P0-23 remains on v2; checked-in local mirrors remain the ordinary outage fallback |
| Plumbing model | `gpt-5.6-luna`, `none` reasoning, pinned output cap; never promote silently to production |
| P0-23 candidate evidence | Complete under ADR-0049 and the Owner's execution-time amendments: eight original paired runs completed, Luna/`max` is incomplete after two transient capacity failures and an explicit p1 stop, and post-hoc Sol/`xhigh` is exploratory; this is evidence, not production selection |
| Context | Bundesliga retains explicit live allowlists; schadensfresse DFB/CL uses ADR-0058's Accepted target-rules-only profiles with exact typed inputs and no Bundesliga context leakage, ADR-0059 binds publication to the structured v1 rules record, and ADR-0060 separates immutable generation provenance from the exact-key current rules attestation used for reuse |
| Club Elo | Implement provider/cache/gates now; a complete dated seed is launch-safe; network use remains an owner gate |
| Production identity | `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first with Standard fallback; USD 35 is planning orientation only |
| Arena challengers | Sol/high, Luna/medium, Terra/xhigh, Luna/none; cap `10000`, match v3 / bonus v1 |
| Activation | Every leaf caller remains manual-only; ADR-0062 temporarily restores the scheduled Schadensfresse context/copy pair, giving eight pairs/16 jobs at `7 2,9 * * *` through `2026-09-08T12:00:00Z`; historical natural observation `33143114280` closed P0-21 |

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
| Exact production schedules, spacing, rollback trigger, and activation | ADR-0062 temporarily restores ADR-0054/0055's eight-pair copy lane while preserving ADR-0053's operating contract | Observe the recovered natural lane; final P1-10 primary activation remains separately reviewed and owner-controlled |
| `schadensfresse` primary routing | P1-10 atomic PR must replace/terminate temporary copy mode by `2026-09-08T12:00:00Z`; P1-08 is superseded | Implement typed identities and Accepted rules-only DFB/CL profiles on the future PR; do not treat recovery copy as primary activation |

ADR-0052 settled final model selection from the completed experiments and
whole-season estimates. New-season outcomes did not exist at selection time,
and older-season evaluation may be training-contaminated; later exploratory
evidence does not silently alter the accepted production default.

## P1 start condition

P0 is complete, including P0-21's natural scheduled observation. Restart P1
under ADR-0061 with a read-only whole-phase preview and `$grill-me` readiness
pass before any new writer starts. Freeze only fully grilled tasks or cohesive
milestones in the resulting tracked P1 execution plan; leave the remaining
frontier explicitly `needs-interview` and allow the runnable subgraph to proceed
overnight.

Apply ADR-0062's temporary recovery while preserving ADR-0053's outer-lane and
manual-only contracts; do not infer bonus or mixed-competition schedule
authority. The frozen [P1 recovery execution packet](p1-execution-packet.md)
and [P1-10 production recovery design](designs/p1-10-production-recovery-and-atomic-delivery.md)
require an atomic P1-10 PR to replace or terminate copy mode by
`2026-09-08T12:00:00Z`. P1-10 continues to own every `schadensfresse`
Bundesliga/DFB/CL match and bonus route, fully absorbs P1-08, and retains later
owner-controlled prompt/replacement/cost/force/cutoff/activation gates. P1-04
owns any unattended Club Elo network refresh decision.
