# ADR-0085: Isolate optional Club Elo refresh

- Status: Accepted
- Date: 2026-09-27

## Context

The owner accepts uncertainty about the historical native timeout provided
refresh errors cannot stop production workflows. The independently accepted
optional-refresh contract (`9aa1fc5989493c3e3d1013556666ceb118118352a402d451ed5959f0101e31ed`)
and specification review (`76d7eac6d23e14240fcf64d7ad2ca156a4a18ba48d7f1374b78cb7bf7c10d8fa`)
define this successor. Slow localhost may contribute, but causation is unproved;
runtime/concurrency diagnosis is parked. Main remains source-disabled. This
decision records intent, not implemented isolation or completed activation.

## Decision

### Source-only boundary

Add `--context-source-only` to existing profile/development entry points and one
request boolean. Before credentials, service resolution or writes require
Bundesliga 2026/27, Club Elo enabled and rosters disabled; reject matchday,
full-season and Kicktipp credential-profile combinations. Reuse current scope
and cycle validation: development retains its fixed lane and allocated UUIDv7;
production binds `gha:<repository_id>:<run_id>` before bridge initialization,
producer `pes-squad-context`, authorized current community/lane and ADR-0083's
entire ordered consumer list.

Execute preparation, exactly Club Elo, persisted-receipt reconciliation and
disposal. Resolve no Kicktipp credentials, history, roster or model collectors;
make no prediction calls. Preparation, integrity, persistence and cleanup errors
remain nonzero, without invented observations, receipts or completed health.

### Production continuation

Each existing reusable production context job has optional Node 24 setup
(two-minute timeout), then optional refresh (five-minute timeout including
`npm ci --ignore-scripts` and source-only invocation). Both use
`continue-on-error: true`; refresh requires successful setup. No optional
prerequisite remains required. Enabled normal jobs allow 52 minutes, preserving
the ordinary 45-minute allowance; disabled jobs retain 45. Inner operation and
cleanup bounds remain operative.

Then run the required ordinary profile with both source flags absent/false and
no cycle arguments under normal success conditions. Ordinary context/history
failures still stop dependents. Do not use blanket `always()` for collection or
prediction; whole-workflow cancellation remains cancellation. Stable step IDs
and an always-run, nonblocking summary report setup/refresh **outcome**, lane,
cycle and ordinary outcome without secrets. Failed/skipped refresh is unavailable;
health may be absent/stale and Actions is the fallback when persistence fails.
Validation dispatch instead requires setup/refresh and runs no ordinary profile,
so refresh failure fails validation.

### Preserve verified heads

With no prepared observation and an existing head, ordinary disabled collection
strictly validates documents and reconstructs/validates full provenance, then
returns retained-head success without publication or cycle-repository access.
Preserve snapshot ID, documents, metadata/source descriptor, original dates,
receipt and watermark; never relabel origin to NetworkDisabled or refresh
collected-at. No-head complete-seed publication remains; corrupt heads still
fail ordinary collection. Repeated arena lanes retain the same physical head
and distinct receipt identities. A valid atomic head/receipt commit survives
later optional failure; pending issue synchronization is not successful refresh.

Keep shared cycles, immutable artifacts, reservations, consumer authority,
watermarks, eight individually necessary receipts and four heads. Missing,
aborted or indeterminate handoff fails the optional consumer without producer
reacquisition or fabricated receipts; ordinary collection continues.

### Finite release graph

Reviewed successor documents → concrete finite transport/security corrections
and F2 durable fence → source-only/workflow/head-preservation implementation →
focused regressions, one coherent solution build and exact-head required CI →
bounded development/branch-production accepting, retention/rejection and replay
evidence plus controlled refresh failure with ordinary continuation → all-eight
activation → reviewed closeout/merge → production observation.

F2 precedes runtime and retains ADR-0084's real-transaction crash/process-loss,
genesis, legacy, listing and stale-CAS correctness matrix. Focused gates cover
artifact path identity, owned-process termination, output/deadlines, complete
authenticated pagination, redirect credential separation, hostile ZIP and
pinned-upload interoperability; profile/dev/runner/workflow contracts; and Club
Elo publication/fallback/disabled matrices. Include preparation timeout, artifact
conflict, persistence/projection failure and missing handoff proving executable
ordinary continuation, and repeated arena head/provenance/watermark retention.
Required CI runs configured project checks on the reviewed exact tip; unavailable
local capability transfers execution there without a false pass.

Historical P4 attribution, repeated full local P confirmation, duplicate local
full-suite gates and local Docker capability cease to be independent activation
requirements. The historical 54 reds (P4/H12/Z31/Docker7) remain failed evidence,
never passed, xfailed or waived as flaky. Concrete H/Z product/security defects
remain blockers requiring correction and focused passing regressions. CT
correction 1/1 and diagnostic execution 1/1 remain spent; no .NET concurrency
side program or budget aliases reopen them. New bounded milestones use one
initial assignment and at most three root-reserved corrections.

Keep sixteen jobs/eight ordered context-match pairs, cron, concurrency, secrets,
credentials, models/prompts/caps, posting and copy unchanged. P1-05/R1 remains
deferred/source-disabled. Existing live authority and useful ADR-0083 evidence
remain; controlled refresh failure with verified old Elo and ordinary continuation
is additionally required before activation and authorizes no predictions.
After deployment inspect the next existing schedule's optional outcomes, actual
heads/dates, receipts/health/issues and ordinary chain. Report degraded refresh
separately. Historical timeout causation is residual risk, not a new live gate.

Rollback is one reviewed all-eight flags-off change preserving heads and fences;
verify usable retained context and zero source/provider/coordinator/handoff/GitHub
interaction. No deletion or automatic ambiguous repair.

## Alternatives considered

- **Required refresh before ordinary collection:** optional failure would stop
  production and consume its ordinary time allowance.
- **Continue unlimited local timeout diagnosis:** does not establish production
  isolation; focused regressions, exact-runner CI and monitoring bound the gate.
- **Republish disabled-source LKG:** would change durable provenance/watermarks.

## Consequences

Refresh degradation remains visible while ordinary collection can continue.
Integrity and security defects remain release blockers. All implementation,
live evidence, activation and closeout milestones remain unimplemented/pending
until their exact candidates and evidence pass review.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md)
- [P1-04 issue-create fence](../tasks/p1-04-issue-create-fence.md)

## Supersedes

Partially supersedes [ADR-0083](0083-activate-official-club-elo-context-refresh.md)
for source-only execution, normal workflow refresh failure/time boundaries,
disabled-head preservation and release gates, and
[ADR-0084](0084-fence-context-source-issue-creation.md) for release/validation
sequencing only. All other accepted provisions remain operative, including the
durable fence and its transaction correctness matrix.

## Subsequent decision

[ADR-0086](0086-bound-live-club-elo-replay-gate.md) removes live replay success
from the release gate while preserving this decision's controlled optional
refresh failure and ordinary-continuation requirement.
