# P1-04-05 orchestration redesign specification

**Accepted:** 2026-09-10 Europe/Berlin

**Status:** implementation contract
**Delivery:** one branch and pull request, direct cutover, no opt-in

This specification translates the accepted redesign into observable repository
changes and acceptance criteria. It is supplemental implementation material,
not a runtime dependency of `$orchestrate`.

## 1. Authority and document topology

1. Remove the complete orchestration workflow section from root `AGENTS.md`.
   Root instructions retain only repository-wide behavior unrelated to whether
   `$orchestrate` is active.
2. Keep `.agents/skills/orchestrate/agents/openai.yaml` explicit-only.
3. Reduce `.agents/skills/orchestrate/SKILL.md` to activation, scope, authority
   boundaries, the dependency on `$grill-me`, and the normative import:

   ```text
   @references/operations-protocol.md
   ```

4. Add the following normative references:

   ```text
   references/operations-protocol.md
   references/roles-and-handoffs.md
   references/validation-and-integration.md
   ```

5. `operations-protocol.md` must transitively import the other two references.
   The effective instruction packet for an invoked run contains all three.
6. Each normative rule has one owning document. Cross-document mentions use
   stable role IDs, transition names, and links instead of restating the rule.
7. `docs/codex/orchestration-workflow.md` and these design documents remain
   supplemental. The skill and normative references must not import or require
   them. Supplemental documents may link one-way to normative sources.
8. Scripts and JSON policy files remain authoritative for deterministic
   mechanics and numerical admission values; prose explains their meaning but
   does not create a second value source.

## 2. Direct cutover

The new state and protocol replace the current version directly.

- Do not add a legacy-state migrator, compatibility reader, version bridge,
  legacy resume branch, or legacy recovery test.
- Existing historical run directories remain untouched as evidence but are not
  executable state under the new protocol.
- A new `$orchestrate` invocation creates only the new schema.
- Attempts to use state that fails the current schema take the ordinary invalid
  or missing-state path; implementation must not special-case old versions.

## 3. Canonical run state and checkpoints

Add `.agents/skills/orchestrate/resources/control-state-template.json` and a
typed checkpoint helper, provisionally named
`.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1`.

`control-state.json` is the only independently authored run-state document. It
contains only current, non-derivable control-plane facts:

- schema version, run/session identity, monotonically increasing revision,
  objective, lifecycle, and wave;
- the current lane graph, dependencies, ownership, worktree reservations, and
  next actions;
- durable decisions and fixed-category gates;
- role assignments, correction counters, retention/release contracts, and
  evidence references;
- repository/publication allowlists and any reviewed unpublished candidate;
- current resource admission, active heavy reservations, warning state, and
  circuit-breaker mode; and
- the current Git integration/publication topology.

It excludes transcript chronology, event history, raw command output, repeated
resource samples, agent counts, quota estimates, and completed-work narratives
that can be reconstructed elsewhere.

The helper must:

1. acquire an exclusive lock scoped to the exact run directory;
2. require the caller's expected revision;
3. validate the requested typed transition and all affected invariants;
4. make a semantically unchanged request a successful no-op;
5. render temporary preview, capsule, and packet manifests from the prospective
   state;
6. validate size ceilings, paths, digests, session identity, and schema before
   replacing live files;
7. replace projections in a deterministic order and write
   `control-state.json` last as the commit record;
8. write the capsule checksum and activation marker only for the committed
   revision; and
9. clean temporary files without retaining an event journal.

Every generated projection carries the committed revision. Compaction hooks
retry briefly when the run lock is held and otherwise accept a hot capsule only
when the control state, preview, capsule, manifests, and checksum agree on that
revision. A crash or mismatch selects cold reconstruction.

All durability barriers, gate changes, reservations, corrections, integrations,
publications, stop, and completion flow through the checkpoint helper. Routine
agent messages, polls, no-change resource samples, and ordinary command output
do not.

## 4. Fail-forward gates

Use a closed set of gate categories covering owner, authority,
production-continuity, dependency, validation, resource, integration,
publication, and recovery concerns. Every active gate records:

- stable ID and category;
- affected lane or run scope;
- evidence and condition;
- specifically prohibited transitions;
- automatic remediation owner/action and retry budget;
- lanes/actions that may continue; and
- the exact escalation or clearance condition.

A gate denies only its named transition. Remediation runs automatically within
existing authority. A lane becomes blocked or `needs-diagnosis` without
stopping independent lanes. Run-level `awaiting-owner` is valid only when a
genuine owner decision blocks every remaining ready path.

Missing disk, inventory, locator, or memory evidence blocks the related
worktree/heavy transition and invokes bounded evidence repair. It does not stop
read-only work, validation that does not need the missing resource, or another
independent admitted lane.

## 5. Roles and handoffs

`roles-and-handoffs.md` defines stable IDs and complete contracts for at least:

- owner;
- root orchestrator;
- intake/research specialist;
- architecture lead;
- specification reviewer;
- milestone writer;
- implementation reviewer;
- cumulative validator and bounded triage;
- deep diagnosis;
- architectural reconciliation;
- final acceptance reviewer;
- CI/status monitor.

Each role definition states model/effort guidance, purpose, required inputs,
owned surfaces, allowed mutations and external effects, prohibited work,
required outputs/evidence, retention and reuse rules, correction/follow-up
budget, and terminal handoff.

There is no separate design-lead role. Cross-cutting design remains the
architecture lead's rare assignment, followed by a different independent
specification reviewer.

Every task-agent assignment includes the run ID, canonical state/capsule and
preview paths, stable role ID, bounded contract, owned paths, validation
responsibility, authority boundary, correction reservation where applicable,
and handoff target. Task agents never edit run state.

## 6. Correction and reuse enforcement

The control state records per-milestone counters and issued assignment IDs.

- The initial writer assignment does not consume a correction turn.
- At most three substantive writer correction turns are allowed.
- The first implementation reviewer may receive at most two incremental
  follow-ups on its own findings and cannot grant final acceptance.
- A correction is reserved through a checkpoint before dispatch. A dispatch
  failure before an agent turn starts releases the reservation. Once
  substantive work begins, success, failure, timeout, or abandonment consumes
  it.
- The helper rejects a reservation above the role limit and transitions the
  affected lane to `needs-diagnosis` with the existing specialist released.
- Diagnosis chooses a narrower defect slice, specification clarification,
  architecture/re-freeze, or ownership split. Other ready lanes continue.
- A material role change always requires a fresh agent. Retention requires a
  concrete near-term reason, release trigger, and observable context-cost
  proxy.

The protocol requires correction reservations before `spawn_agent` or
`followup_task`. Because the local runtime cannot intercept those APIs, later
analysis must flag any corrective assignment without a matching reservation.

## 7. Validation and integration

`validation-and-integration.md` defines this normative chain using the role IDs
from `roles-and-handoffs.md`:

1. Freeze a milestone contract, owned paths, affected projects/test projects,
   regression/cross-project gates, and integration route.
2. The writer edits and uses focused tests during iteration. Before handoff it
   builds every changed project and runs complete directly affected test-project
   suites plus specified regression and cross-project contract tests. A
   solution build is mandatory for central/shared or build-graph changes.
   Documentation-only work substitutes applicable lint/link/schema checks.
3. A fresh implementation reviewer performs read-only correctness/regression
   review of the diff and evidence. Findings return to the same writer within
   its correction budget.
4. The root serially integrates accepted lane commits into the run integration
   branch. Integration is not final acceptance.
5. After each coherent frozen wave, one cumulative validation-and-bounded-triage
   agent validates the exact combined tip with a diagnostic solution build,
   the union of affected suites, and cross-project gates. It stores full output
   outside recovery context and attributes compiler, test, infrastructure,
   cross-lane, unattributed, or architectural failures without changing tracked
   source.
6. Straightforward failures return to their owning lane. Difficult,
   unattributed, or architectural failures receive a fresh deep-diagnosis or
   architecture role using the preserved output; it does not rerun the full
   gate unless the evidence is insufficient.
7. A fresh final reviewer receives the exact candidate tip/full diff, frozen
   contract and invariants, validation evidence, and closed-findings checklist,
   without prior conversation or an intended verdict.
8. The root publishes the accepted candidate and CI validates that exact head.

Cumulative validation must pass before a dependent wave, draft-PR update, or
final acceptance. Independent non-dependent lanes may continue during failure
triage.

## 8. Waiting and autonomous diagnosis

- Use native mailbox/event completion as the primary signal.
- Default an otherwise idle root wait to five minutes. Extend to ten minutes
  for known long builds, tests, or CI.
- Do not send status-only messages, poll simply because a timeout expired, or
  mutate recovery state for a wait.
- Refresh live state after a timeout only when a scheduling or gate decision
  depends on it; otherwise wait again.
- Read/search/status/log inspection and bounded local builds/tests within an
  assigned worktree and authority envelope do not require owner permission.
- Platform approvals still apply. Owner input remains required for semantic
  product choices, destructive actions, unplanned external/live effects,
  production changes, spending, or scope/authority expansion.

## 9. Worktree lifecycle

Preserve measured disk admission and one writer per writable worktree, but make
worktree count inventory rather than a cap.

Add a deterministic retirement helper that verifies the exact absolute target
inside the repository's linked-worktree inventory before any removal. It may
retire a worktree only when:

- its agent is terminal/reclaimable with no pending assignment;
- no active process or heavy/external-effect lease owns it;
- `git status --porcelain` is empty;
- its branch and exact tip are recorded in control state; and
- the tip is integrated/published or remains reachable through the recorded
  retained branch.

The helper removes only the worktree checkout, never the retained branch or
commit. Dirty, unpublished-unreachable, process-owned, lease-owned, mismatched,
or uncertain worktrees become `parked-recovery-only` with remediation and do
not block unrelated retirement or execution.

## 10. Dynamic heavy-operation admission

Replace `heavyOperation.concurrentLimit` with an evidence-based bundle policy.
The checked-in policy defines:

- preferred available-memory floor: `1.0 GiB`;
- absolute experimental floor: `0.5 GiB`;
- warning threshold: `1.5 GiB`;
- provisional bounded project-gate reservation: `0.85 GiB`;
- operation-profile schema and evidence requirements; and
- degraded circuit-breaker policy.

Each requested heavy operation declares a profile/fingerprint, memory
reservation, recoverability, external/live-effect classification, and a
controllable maximum worker fanout. Admission forms the largest useful
critical-path-aware ready bundle for which:

```text
sum(memory reservations) <= available physical memory - applicable floor
sum(worker caps) <= logical processor count
```

The 1.0 GiB floor is preferred. The 0.5 GiB floor is available automatically
only for mandatory, bounded, recoverable, local-only validation with healthy
commit/paging signals. Below 0.5 GiB, new heavy work queues.

CPU workers are distributed across admitted operations without exceeding the
logical processor count. A command that cannot enforce its assigned fanout is
unmeasured/exclusive until evidence and controls exist. Exclusive means no
other heavy operation runs concurrently; it does not forbid a full build/test
or internal parallelism within the assigned cap.

An unmeasured fingerprint receives the full eligible pool for its first
sample. Runtime evidence may immediately raise an underestimated reservation
or degrade the profile, but may not lower a checked-in reservation. Durable
profile reductions require owner-reviewed post-run analysis and a repository
change.

After an operation completes, the scheduler may backfill a ready operation only
after a fresh snapshot. The candidate plus conservative remaining active
reservations must fit. Reduce an active reservation only when reliable
process-level evidence supports the consumed/remaining calculation; otherwise
charge it fully.

An OOM, severe paging failure, or memory-related abnormal termination
atomically:

- disables the 0.5 GiB band;
- restores the 1.0 GiB floor;
- marks the offending profile exclusive with reduced fanout; and
- permits one necessary recoverable retry before routing another failure to
  diagnosis.

The breaker blocks aggressive concurrency, not all validation. Independent
work continues. Only owner-reviewed analysis may clear the degraded state.
Heavy-operation evidence remains purpose-specific and outside capsule and
automatic recovery context.

## 11. Publication

- Establish the canonical remote, integration branch, run branch prefix, and
  publication topology during intake.
- Use an integration branch when the objective has more than one milestone or
  any intermediate state is not independently safe for `main`.
- Create a draft PR immediately after the first implementation-reviewed,
  cumulatively validated, final-reviewed, coherent milestone and before its
  dependent next wave.
- Keep lane commits local. Push cohesive reviewed milestones and
  recovery-critical long-lived candidates.
- Before every push, verify exact branch, remotes, status, tip, scoped paths,
  secret absence, allowlisted ref, and non-force fast-forward relation.
- CI and any rerun operate on the exact published head. One workflow rerun is
  allowed for an eligible failed milestone; repeated failure routes to
  diagnosis.

## 12. Standing coordination analysis

Add an active standing focus named `subagent-coordination-effectiveness` to
`docs/codex/session-analysis/focuses.json`. It applies to orchestration sessions
and requires each analysis to synthesize:

- the milestone role/handoff graph;
- writer-review-correction cycles;
- first-pass build, test, implementation-review, cumulative-validation, and
  final-acceptance outcomes;
- time and logged tokens between lifecycle transitions;
- failure attribution, reruns, and duplicated validation;
- handoff completeness and stale evidence;
- agent reuse, material role changes, correction-budget compliance, and model
  fit; and
- how architecture/specification affected seam quality, lane independence,
  downstream release, and implementation/review efficiency.

Use privacy-bounded aggregate or hashed references rather than full messages.
The focus is additive to existing narrower focuses and remains active after
coverage. `$orchestrate` does not read the registry or collect an event journal.

## 13. Acceptance tests

The implementation is complete only when deterministic checks cover:

1. skill frontmatter, explicit-only policy, the transitive normative import
   graph, stable role references, and the absence of a dependency on
   supplemental documents;
2. absence of orchestration procedure from root `AGENTS.md` and absence of a
   second normative copy in the skill entry point;
3. control-state initialization, expected-revision conflict, valid typed
   transitions, semantic no-op, fail-forward gates, correction reservations,
   and rejected over-budget corrections;
4. transactional checkpoint rendering, size ceilings, manifest coverage,
   revision agreement, lock behavior, interrupted-write cold recovery, and
   exact-session activation/termination;
5. writer validation evidence and the cumulative validation/triage handoff;
6. five-/ten-minute wait policy and absence of status-only polling guidance;
7. safe worktree retirement and parking for every failed precondition;
8. dynamic memory/CPU bundle admission, the `3.06 GiB`/three-project-gate
   example, unknown exclusive behavior, safe backfill, absolute-floor denial,
   and circuit-breaker degradation;
9. draft-PR eligibility only after the first coherent reviewed and validated
   milestone;
10. the standing coordination focus and focus-registry validation;
11. hot/cold recovery on the new schema without a legacy compatibility path;
    and
12. ordinary repository prompts not activating or loading orchestration.

Run the skill validator, focused PowerShell tests, session-analysis registry
tests, repository build, and repository tests applicable to the changed
surface. A fresh `gpt-5.6-sol/xhigh` subagent performs an independent
review-and-fix cycle. Merge only after its findings are resolved, local gates
pass, and required PR checks are green; then update local `main` from `origin`.
