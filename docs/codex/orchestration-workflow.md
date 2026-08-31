# Phase-scale orchestration workflow

This document records why KicktippAi changed its explicit `$orchestrate`
workflow after the first P1 run and which parts remain pilot hypotheses. The
procedural source of truth is the repository-root `AGENTS.md` and
`.agents/skills/orchestrate/SKILL.md`; Bundesliga-specific adoption is recorded
in ADR-0061 and the execution strategy.

## Evidence that motivated the change

The interim P1 investigation covers thread
`01a04fd8-ffcf-7263-944f-98d1bc53c645` through repository boundary
`c4669aa..04a6d85`. Model allocation was disciplined: all 78 spawns explicitly
selected model, effort, and `fork_turns: none`. The main problems were elsewhere:

- P1-10 expanded from a 65-line task record to 326 lines at the cutoff while
  cross-cutting architecture was discovered between implementation slices.
- The P1-10-only boundary changed 83 files by +8,803/-724 lines and contained
  14 commits, so the work was genuinely large rather than merely slow.
- Fourteen local and fourteen remote P1-10 branches, thirteen P1-10 main CI
  runs, and repeated late exact-SHA reviews turned narrow slices into serial
  micro-release gates.
- Nineteen review turns found substantive defects; 11 of the 13 review lanes
  that eventually approved required more than one turn. Review was valuable,
  but architecture arrived too late.
- The ledger took only about 11.5 direct minutes, yet 375 patches added root
  attention and compaction pressure.
- A first push was rejected because `$orchestrate` expressly did not authorize
  external writes and the remote identity had not been established. The run
  then had a 5h42m54s no-agent interval before the owner explicitly authorized
  publication.
- The laptop has four logical processors, about eight GiB RAM, and limited
  free disk. One mature linked worktree occupies about 1.1 GiB, so agent count
  alone is not a safe capacity model.

The investigation is interim and dominated by one pathological cross-cutting
task. Architecture-first intake, scope reclassification, production-safe
publication, bounded Git authorization, compact recovery state, and measured
resource admission are requirements. A universal milestone count or numeric
productivity target is not. The orchestrator retains judgment and records its
chosen parameters during preview.

## First follow-up correction — 2026-09-01

The first successor analysis initially described lower token throughput as
improved efficiency and over-weighted machine admission as a cause of reduced
parallelism. The preserved snapshot supports a narrower conclusion:

- average active concurrency fell from 1.521 to 1.121 and two-plus occupancy
  from 52.1% to 11.8%;
- only P1-10 had completed grilling, while P1-04/05/06/07/11 remained
  `needs-interview`, so the narrower runnable graph is the dominant cause;
- logged tokens per adjusted hour fell 42.2%, but worker activity per adjusted
  wall-second fell 41.3%; and
- uncached input plus output per worker-hour fell only about 5.6%, from 771k to
  728k, which does not establish accepted-work-per-token efficiency.

The safer headline is **less parallel, less wasteful, efficiency unproven**.
Milestone publication, production continuity, and ledger coalescing reduced
observable waste. Output productivity remains unmeasured. Subscription quota
is intentionally not an orchestration signal: rapid quota use is acceptable
when it buys useful accepted work, and reset timing must not govern dispatch.

Post-cutoff evidence identified secondary constraints. The first
concurrent-ready R2 siblings ran sequentially because three reusable threads
occupied the spawned-agent limit, and a heavy gate was denied at 1.48 GiB
against the 1.50 GiB floor. The normalized correction and separated addendum
are in
[`p1-orchestration-follow-up-investigation/data/corrections.json`](p1-orchestration-follow-up-investigation/data/corrections.json).

## Lifecycle and canonical terms

An **orchestration run** owns one explicit objective and may span a whole
phase. A **phase foundation** is the complete set of cross-cutting decisions
needed before any task can be frozen. A **frozen runnable graph** is the
interview-complete, independently reviewed subgraph that writers may execute.
A **milestone** is a cohesive, independently integrable layer of that graph,
not a synonym for an agent lane or commit.

The run lifecycle is:

`preview -> awaiting-owner | ready -> active -> complete`

Every run performs a read-only whole-objective intake. A clean packet moves
from `ready` to `active` automatically. Readiness defects invoke `$grill-me`:
complete the phase foundation, then fully interview one task or cohesive
milestone at a time. The owner may stop between those units and release the
already frozen independent graph; deferred nodes are `needs-interview`.

Raw interview state remains in
`.tmp/orchestration/<run-id>/preview.md`. The compact recovery snapshot remains
in sibling `state.md`. Stable phase ordering, authority, and review results are
promoted into the competition's tracked execution packet only after review.
High-risk architecture belongs in a dedicated design artifact rather than an
ever-growing task checklist. The restarted P1 run—not this policy change—will
create `plans/bundesliga-2026-27/p1-execution-plan.md` and any required
`plans/bundesliga-2026-27/designs/*.md` briefs from then-current facts.

## Architecture and scope control

Phase-wide or cross-cutting architecture always uses a
`gpt-5.6-sol` / `xhigh` lead and a different `gpt-5.6-sol` / `xhigh` reviewer
during this pilot. Getting the seam map, invariants, non-goals, dependency
graph, owned paths, and verification strategy right is cheaper than correcting
downstream drift. The accepted lead remains recallable only with a concrete
near-term retention reason and release trigger, and never when retention blocks
useful ready work merely to preserve optional continuity.

A semantic discovery—not a line-count threshold—reopens design: a new
cross-cutting invariant, missing ADR, new dependency seam, invalidated
architecture, or material objective expansion pauses the affected lane. The
lead refreshes the brief, the independent reviewer approves it, and the root
re-freezes the affected graph. The root may do so autonomously only when the
accepted outcome, durable decisions, and external authority remain unchanged.

## Execution, review, and publication

Writer concurrency follows dependency independence and machine admission, not
available agent slots. Ordinary lane branches remain local. Focused lane checks
happen before handoff; independent review and full CI attach to frozen
milestones and exceptional high-risk lanes. A reconciliation thread may be
reused while its context remains applicable.

The project config allows eight spawned-agent threads, excluding the primary.
This does not create a target occupancy or replace the separate limits of two
writers/worktrees and one heavy operation. Useful independent ready work should
be admitted; speculative work should not be invented to fill capacity.

Sol/xhigh remains the independent-review default during this pilot. Sol/high
may review a frozen exact artifact only when the root records bounded paths and
deterministic acceptance criteria and confirms there is no open ADR, invariant,
ownership, architecture, or production-continuity question. Retained reviewers
need the same reason/release-trigger discipline as architecture specialists.

Direct `main` integration remains useful for independently production-safe
milestones. A change that temporarily disables or regresses active workflow
topology, production entrypoints, routing, feature gates, prompts, or
configuration must remain on an integration branch or draft PR until the safe
release unit is ready. An emergency safety quarantine may land separately only
with explicit owner approval, exact impact, fallback or its absence, rollback,
recovery owner, and restoration deadline.

P1-10 is the case study: CI-green commits removed scheduled Schadensfresse
context/match execution and fail-closed its prediction and verification
entrypoints before the replacement was ready. There was no automated copy
fallback. The new workflow requires the restarted P1 preview to treat
September 4, 2026 as the target for restoring the typed scheduled context and
matchday path, while the CL bonus retains its September 8 16:45 UTC deadline.
Manual copying is an owner-operated last resort, not orchestrator authority.

## Bounded authorization and unattended recovery

Explicit `$orchestrate` invocation grants run-scoped authority to commit owned
paths and non-force push reviewed/frozen commits to the startup-verified
canonical repository and allowlisted refs. Draft PR creation/update is allowed;
ready/merge requires exact base/head and green-check conditions in the frozen
packet. One failed milestone CI rerun and cancellation of a superseded run are
allowed. Broader Git, GitHub, production, spending, or destructive actions are
not implied.

This contract supplies automated review with the previously missing informed
destination and payload evidence. It cannot override platform policy. If a
push is rejected, preserve the exact SHA, continue safe independent local work
only within recovery and resource budgets, and never route around the review.

The host capability check on 2026-08-31 confirmed that `gh auth setup-git` is
not needed here: Git push authentication was already working independently,
while that command would only configure Git to use GitHub CLI as its credential
helper. The updated fine-grained PAT identifies `ehonda` with administrator
permission on the public `ehonda/KicktippAi` repository. A temporary commit and
`codex/gh-capability-check-20260831` branch proved code push plus PR create,
edit, ready, inspect, close, and branch cleanup through PR #96; Actions write
permission cancelled test run `33339073809`. The merge operation was exposed
and the PR was mergeable, but the capability probe intentionally closed it
instead of merging the empty test commit. No branch protection or ruleset was
present on `main` at the check. These facts establish capability, not broader
run authority; each orchestration preview still verifies the live target and
permissions.

## Resource budget

The checked-in resource helper separates three limits:

- task-agent capacity;
- linked writable-worktree capacity; and
- heavy local operations such as full builds, tests, and multi-job families.

The default profile admits at most two linked task worktrees and reserves 1.25
GiB for each new one. It requires at least 10 GiB free after reservation and
warns below 15% disk free. Heavy work has one lease on every host, requires at
least 1.10 GiB available memory, and warns below 1.50 GiB. The warning is
calibration evidence rather than a denial. At admission/release transitions,
the ledger records operation type, start/post memory, duration, and outcome.
Missing required measurements still fail closed; an explicit run-scoped owner
override remains available for a measured exceptional shortfall.

## Specialist release prerequisite

The workflow now defines when a specialist should stop being retained, but the
current client surface did not expose the documented explicit thread-closure
operation. Merging and activating this revision is blocked until the separate
fresh-session experiment in
[`agent-closure-prerequisite.md`](agent-closure-prerequisite.md) establishes the
actual mechanism and the owner chooses a policy. No missing-tool fallback is
assumed in this draft.

The immediate preview-to-execution UX remains intentionally small: one
`EXECUTION START` commentary marker before the first writer. A durable,
discoverable status surface that does not require scrolling the root thread is
a future improvement, not a reason to build ad-hoc high-frequency telemetry in
the ledger now.

## Evaluation

Do not add a telemetry service or turn the ledger into an event stream. The
current P1 run may finish under its existing workflow; after the merge gates
pass, a fresh run will use this revision and trigger another analysis at a
useful checkpoint. That review
will reconstruct normalized evidence from native Codex transcripts, Git,
branches, PRs, and CI. It should examine accepted milestones/review closures,
ready-lane utilization, root work per accepted milestone, correction turns and
tokens, blocker time by the fixed categories, explicit releases and
agent-slot-blocked time, resource start/post/outcome calibration, re-freezes,
publication and CI count, owner waits, and escaped defects. Generic event-wait
frequency and raw token burn are contextual utilization measures, not
standalone efficiency KPIs. Early results guide parameter changes; they are not
hard pass/fail targets.
