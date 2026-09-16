# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04 D0 durable-fence documentation accepted with implementation and live gates pending; P1-05/R1 dormant and deferred
- Last reconciled: 2026-09-16
- Current policy: [execution strategy](execution-strategy.md)

Production continuity is unchanged while the activation gates run: the existing
cron, non-cancelling serial/default-success topology, models, prompts,
credentials, posting and copy behavior stay fixed. P1-10 and other P1 work are
excluded.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | D0 durable-fence contract accepted; no runtime effects | E2 and W2 transport clearance, then F2 |
| E2 and W2 transport | Not implemented | Their independent parser/transport validation; W2 issue paths transfer to F2 |
| F2 | Not implemented | Combined Core/Firebase/Orchestrator/Node validation, fresh acceptance and exact-head CI |
| V2 | Not implemented | Accepted F2 candidate; wire the fence repository while normal flags stay false |
| Live validation, A2 and closeout | Not implemented | V2, real source-only evidence, all-eight final flag diff and final review |
| [P1-05](tasks/p1-05-roster-refresh.md) | Deferred under ADR-0082 | Exact ADR-0079 dcaribou sidecar and separate future owner gates |
Merged PR #111 is prior dormant C3/E1 implementation evidence. It does not
complete operational activation and does not prove parser-v2, W2, V2,
source-only live validation, scheduled enablement or all-eight receipts.

The operative source decisions are [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md),
[ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md),
[ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md),
[ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md),
[ADR-0081](decisions/0081-close-club-elo-html-publication-and-selection-seams.md),
[ADR-0082](decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md),
[ADR-0083](decisions/0083-activate-official-club-elo-context-refresh.md), and
[ADR-0084](decisions/0084-fence-context-source-issue-creation.md).
ADR-0083 and ADR-0084 are jointly operative for activation: ADR-0084 makes D0
documentation complete in this candidate, keeps W2 transport-only, and makes F2
the required unimplemented issue-fence milestone. Their remaining accepted
provisions and the ADR-0077/0081 refinements stay operative.
## Authority, targets and gates

The owner-authorized scope is bounded Club Elo context work: current official
reads, development and production context writes, GitHub handoff/issue effects,
existing-schedule source activation after evidence, Pages publication and a
ready PR merge. It excludes model calls, prediction/posting changes, roster
work, new schedules, credential-route changes and unrelated P1 work.

Eight receipt lanes target four physical community heads: `pes-squad`,
`schadensfresse`, `relaxdays-tippt`, and the shared `ehonda-ai-arena` context
for its five distinct arena receipts. The fixed lane order and evidence recipe
are in [P1-04](tasks/p1-04-club-elo-refresh.md). Source flags are false until
A2; D0 is documentation complete in this candidate, while F2 remains required and unimplemented. D0 claims no downstream runtime or live result.

P1-05 is `MetadataUnavailable` pending its exact sidecar. It performs no
sidecar/artifact probe and has no provider resolution, observation, receipt,
health, publication, issue or API activity. Seed/LKG remains truthful fallback;
this plan makes no provider-adoption claim.

No disk restriction applies at or above 20 GiB effective free space. The former
percentage warning is not a restriction.

## Current artifacts

- [P1-04 task](tasks/p1-04-club-elo-refresh.md)
- [P1-05 task](tasks/p1-05-roster-refresh.md)
- [context-refresh design](designs/p1-04-05-context-refresh.md)
- [execution packet](p1-04-05-execution-packet.md)
- [P1 status snapshot](p1-status-snapshot.md)
- [decision index](decisions/README.md)


## D0 durable issue-create fence update — 2026-09-16

[ADR-0084](decisions/0084-fence-context-source-issue-creation.md) accepts the
marker-wide durable issue-create fence. The exact canonical marker, rather than
body hash, desired state, watermark, cycle, process, or run_attempt, is the
stable identity. A transaction arms Ready to CreateUncertain before POST and
only its committed caller receives one grant. Valid response or one unique
exact-marker discovery binds the issue number. Legacy no-fence health and
uncertain, zero-match, deleted, removed-marker, or duplicate-marker evidence
remain Pending for owner-directed manual recovery; they never retry, expire,
reset, delete, foreign-edit, or replacement-create.

The refrozen graph is D0 -> E2 and W2 transport -> F2 -> V2 -> existing live
evidence -> A2 -> closeout. F2 owns the durable Core/Firebase seam and the
transferred issue projector/tests; E2 retains its 13 parser/descriptor paths,
W2 retains only artifact/package/Node paths, and V2 wires the repository after
F2 acceptance. F2 requires real transaction/process-loss/genesis/legacy/list/
stale-CAS matrices, solution build, full Core/FirebaseAdapter emulator/
Orchestrator suites, deterministic Node/lock validation, fresh review, and
exact-head CI. D0 implements no runtime/source/Firestore/GitHub/artifact/issue
or live effect. P1-05/R1 remains ADR-0082 deferred.
