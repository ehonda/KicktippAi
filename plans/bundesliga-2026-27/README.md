# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1-04 flags-off runtime merged and live source-only acceptance/retention observed, ordinary-continuation proof and activation pending; P1-05/R1 dormant and deferred
- Last reconciled: 2026-09-29
- Current policy: [execution strategy](execution-strategy.md)

Production continuity is unchanged while the activation gates run: the existing
cron, non-cancelling serial/default-success topology, models, prompts,
credentials, posting and copy behavior stay fixed. P1-10 and other P1 work are
excluded.

## Current graph

| Lane | State | Next gate |
| --- | --- | --- |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | In progress; flags-off v2 runtime merged in PR #116 | Controlled refresh-failure/ordinary-continuation proof |
| Transport/security and [F2](tasks/p1-04-issue-create-fence.md) | Accepted in the flags-off runtime | Monitor actual operation |
| Source-only/workflow/head preservation | Implemented with eight normal source flags false | Normal continuation probe |
| Focused validation and exact-head CI | Passed for flags-off runtime and main merge | Fresh exact-head CI for later probe and activation commits |
| Live validation, A2 and closeout | Production source-only acceptance and distinct retention/rejection observed; L7 GitHub replay failed closed | ADR-0086 removes live rerun success as an A2 prerequisite; prove ordinary continuation, then review/CI and enable all eight flags |
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
[ADR-0085](decisions/0085-isolate-optional-club-elo-refresh.md) governs source-only execution, optional production refresh and verified head retention. [ADR-0086](decisions/0086-bound-live-club-elo-replay-gate.md) narrows the live replay gate after the observed artifact loss while retaining fail-closed behavior and the required ordinary-continuation proof. ADR-0083/0084 retain their other invariants, including F2's durable issue fence. Historical timeout causation is uncertain and its runtime audit is parked.

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
A2. Optional Node setup (two minutes) and refresh (five minutes) must continue on error; enabled normal jobs allow 52 minutes, disabled jobs 45, then required ordinary source-off collection runs under normal success semantics. Source-only validation is required and cannot false-pass. Verified heads retain dates/provenance/receipt/watermark without republication; corrupt heads fail. Runtime isolation is implemented, but the controlled live normal-job continuation proof remains pending.

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
