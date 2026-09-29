# ADR-0086: Bound live replay evidence for Club Elo activation

- Status: Accepted
- Date: 2026-09-29

## Context

P1-04's accepted activation sequence required same-run replay before enabling normal source flags. The accepted production cycle in run `36478629294` advanced four community Elo heads, published one verified artifact, completed eight receipts, and synchronized health. A later distinct cycle in run `36481153403` rejected a timed-out source and retained those heads with eight receipts. Both were source-only, with no prediction jobs.

The bounded same-run rerun of the later cycle failed during its first source preparation. Its original artifact `10996642926` was listed before the rerun and returned 404 afterward. The inspected Firestore cycle, source observation, receipt, heads, payloads, and health values were unchanged; later jobs were skipped. This is a **failed replay gate**, not idempotence proof. GitHub's `actions/upload-artifact` project records a similar report of earlier-attempt artifacts becoming inaccessible on rerun (issue #585); the precise mechanism for this run is unproven. Do not rerun the accepted production run: its artifact `10994562013` still exists and is referenced by the current head.

The owner explicitly accepts bounded uncertainty and production monitoring, provided an optional refresh error cannot take down ordinary production workflows. The flags-off v2-compatible reader is merged to main and exact-head CI is green. A controlled normal-job failure-continuation proof is still pending.

## Decision

Remove live GitHub rerun success as a prerequisite to A2 activation. Record run `36481153403` attempt 2 as failed and retain its before/after evidence; it receives no success credit. Retain the attempt-independent cycle identity and artifact lookup rules, all replay/idempotence tests, and fail-closed behavior for absent, conflicting, expired, or deleted artifacts. On replay of a cycle whose required artifact is unavailable, never reacquire, reupload, rebuild from a new network observation, fabricate a receipt, or rewrite a verified head. An incomplete old cycle with lost artifact requires explicit manual owner recovery; the optional normal refresh step reports failure while ordinary collection continues.

Before any of the eight normal production source flags become true, require a controlled failure of the actual optional refresh step in a normal context job, followed by a successful required ordinary profile using verified retained Elo. The probe must have no prediction job and must show the optional failure and ordinary success in one job's step outcomes, with unchanged retained-head provenance/payload and no fabricated source receipt. Existing accepting/retention production evidence, focused regression and workflow contracts, fresh final review, and exact-head CI remain required. Activation changes all eight normal flags together and keeps schedule, dependency graph, model/prompt/cap, credential, posting, and copy routes unchanged.

After deployment, inspect the next existing scheduled run for each optional refresh outcome and ordinary context continuation, plus current Elo heads, receipts, health, and desired issue. Report degraded refresh and any unrelated ordinary failure separately. Rollback remains the reviewed all-eight flags-off change, preserving heads and fences.

## Scope of successor

This partially supersedes ADR-0083's staged rerun-before-activation gate and ADR-0085's live replay-evidence release clause. ADR-0083's `run_id` rather than `run_attempt` identity remains operative; it is not a promise that GitHub preserves earlier-attempt artifacts. ADR-0083's expired/deleted-artifact failure rule and ADR-0085's controlled refresh-failure/ordinary-continuation gate remain operative. No replay success is claimed or implied.

## Consequences

Cross-attempt artifact loss may prevent automatic recovery of an old incomplete cycle. The refresh fails closed; ordinary production continuity relies on the required isolated optional step and retained-head validation. Monitoring provides operational evidence after activation but does not retroactively turn the failed replay into a pass.

## Evidence

- Runs: `36478629294` accepting; `36481153403` rejecting and failed attempt-2 replay.
- Local ignored evidence: `.tmp/orchestration/01a0a211-c4bc-7941-86f9-37dfba1da3fa/evidence/L5-production-audit.json`, `L6-production-audit.json`, `L7-pre-replay-pes-squad.json`, `L7-post-replay-pes-squad.json`, `L7-replay-first-job.log`, and `L7-replay-platform-note.md`.
- GitHub artifact behavior report: https://github.com/actions/upload-artifact/issues/585.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md)
