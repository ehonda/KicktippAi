# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04 in progress under accepted ADR-0085 continuity contract; runtime/live gates pending; P1-05/R1 dormant and deferred
- Last reconciled: 2026-09-27
- Current policy: [execution strategy](execution-strategy.md)

Production continuity is unchanged while the activation gates run: the existing
cron, non-cancelling serial/default-success topology, models, prompts,
credentials, posting and copy behavior stay fixed. P1-10 and other P1 work are
excluded.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | In progress; ADR-0085 accepted intent, isolation not implemented | Review successor documents |
| Transport/security and [F2](tasks/p1-04-issue-create-fence.md) | Acceptance pending | Concrete defect regressions and real-transaction fence matrix |
| Source-only/workflow/head preservation | Not implemented | Accepted transport/security/F2 candidate |
| Focused validation and exact-head CI | Pending | One coherent build, focused seam matrices and required runner project checks |
| Live validation, A2 and closeout | Not implemented | Bounded accepting/retention/rejection/replay plus controlled refresh failure and ordinary continuation, then all-eight enable and reviewed closeout/merge/monitoring |
| [P1-05](tasks/p1-05-roster-refresh.md) | Deferred under ADR-0082 | Exact ADR-0079 sidecar and separate future owner gates |

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
[ADR-0085](decisions/0085-isolate-optional-club-elo-refresh.md) is the current partial successor for source-only execution, optional production refresh, verified head retention and finite release gates. ADR-0083/0084 retain their other invariants, including F2's durable issue fence and real-transaction correctness matrix. Historical timeout causation is uncertain and the runtime audit is parked. Historical 54 reds remain failed evidence; concrete product/security defects still block release. CT correction 1/1 and diagnostic execution 1/1 remain spent.

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
A2. Optional Node setup (two minutes) and refresh (five minutes) must continue on error; enabled normal jobs allow 52 minutes, disabled jobs 45, then required ordinary source-off collection runs under normal success semantics. Source-only validation is required and cannot false-pass. Verified heads retain dates/provenance/receipt/watermark without republication; corrupt heads fail. No current runtime isolation or live result is credited.

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
