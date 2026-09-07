# ADR-0075: Refine orchestration recovery and resource admission

- Status: Accepted
- Date: 2026-09-07
- Refined by: [ADR-0076](0076-pause-orchestration-hook-readiness-gate.md)

## Context

Analysis of the first P1 context-refresh orchestration found that compaction
recovery itself was reliable, but the root repeatedly reloaded unchanged
instructions, completed phase history, tasks, and ADRs. The two-worktree and
two-writer limits also reflected an earlier disk constraint rather than the
current machine or a measured failure boundary. Repeated common-runtime
review/fix churn showed a need for a clearer architecture circuit breaker and
fresh final acceptance.

The existing 1.10 GiB heavy-operation memory floor was conservative but not
calibrated. Three observed denied samples fall between 1.00 and 1.10 GiB, while
the captured data cannot establish that running at those levels is safe.

## Decision

Orchestration uses root-read, hash-only, and specialist-read context tiers.
Versioned instruction, hook, and active-contract manifests bind normalized,
ordered paths to raw-byte SHA-256 values. A valid exact-session capsule,
checksum, packet set, hook-trust marker, compact preview, and reconciled
ownership permit bounded hot recovery. Drift, corruption, a material re-freeze,
an owner/authority gate, or uncertain ownership requires cold reconstruction.

A synchronous `UserPromptSubmit` hook stays silent unless the prompt explicitly
contains `$orchestrate`. Its positive marker binds the canonical repository,
session, hook-definition digest, and recovery-script digest without activating
the workflow. Absence or mismatch remains an owner trust gate.

`preview.md` is a replace-in-place current graph with an 8 KiB target and 12
KiB hard ceiling. It excludes transcripts, chronology, logs, repeated samples,
and completed evidence. It belongs to the active-contract packet and therefore
does not embed that packet's own digest; the capsule stores it.

Genuinely phase-wide or cross-cutting architecture uses `gpt-6-astra` / `high`
only as architecture lead, followed by an independent `gpt-5.6-sol` / `xhigh`
specification reviewer. The lead defines semantic milestones and downstream
release seams without transient machine-capacity inputs. Writer and incremental
review continuity are bounded, and final acceptance comes from a fresh
reviewer with no earlier milestone role.

There is no universal linked-worktree or concurrent-writer count. Worktrees are
classified as active/build-capable, parked/recovery-only, removal-ready, or
uncertain. Active and uncertain worktrees reserve 1.25 GiB of future growth;
existing bytes are already represented by free-space measurement. Admission
requires measured free disk minus outstanding and proposed reservations to
remain at least 14 GiB, with the 15% warning calculated after reservations.
The root chooses a run-local writer-wave throttle for independent path-disjoint
work. One writer per worktree, serialized integration, and the sole heavy-job
family remain.

The heavy-operation floor becomes 1.00 GiB, with 1.00–1.10 GiB restricted to
recoverable local work without external side effects. A memory-related OOM,
abnormal termination, or severe paging failure trips a repository-local,
machine-scoped state file shared through the primary-checkout locator that
restores 1.10 GiB across linked worktrees until an owner-reviewed analysis
clears the preserved trigger. The locator must match the linked worktree's Git
common-directory identity or admission fails closed. Purpose-specific outcome evidence stays out
of the recovery capsule.

Historical material is opt-in. The live Bundesliga index and execution
strategy route only current work, while completed execution evidence is moved
to an indexed archive after live dependencies and recovery state are cleared.

## Alternatives considered

- **Keep full rereads after every compaction:** rejected because packet hashes
  can prove unchanged bytes without spending model context on them.
- **Use one repository commit SHA:** rejected because it is both too broad for
  unrelated commits and unable to detect uncommitted contract drift.
- **Retain the count of two:** rejected because count is not the constrained
  resource; free disk after reconciled growth reservations is.
- **Make Astra/high the general orchestrator or reviewer:** rejected because
  its role is the necessary semantic architecture lead, not operations or
  final independent acceptance.
- **Treat 1.00 GiB as proven safe:** rejected; the new band is an experiment
  with a durable local circuit breaker.

## Consequences

- Normal compaction recovery should consume materially less root context while
  preserving a deterministic cold path.
- Hook changes require owner trust again and invalidate existing markers.
- More than two isolated writers or worktrees may be admitted when the graph,
  ownership, reviews, disk reservations, and leases support them.
- Low-memory local gates may start sooner, but external/live operations remain
  excluded from the experimental band and a qualifying failure restores the
  earlier floor.
- A separate archive migration is required to remove accumulated completed P0
  material from live plan entrypoints.

## Affected tasks

- All remaining Bundesliga P1 orchestration.
- Repository `$orchestrate`, hook recovery, and resource-admission tooling.
- Bundesliga plan routing and archival policy.

## Supersedes

- [ADR-0061](0061-preview-and-milestone-orchestration.md) for model allocation,
  recovery context, worktree/writer limits, and resource admission. ADR-0061's
  whole-phase preview, production-continuity, Git, and publication decisions
  otherwise remain operative through this refinement.
