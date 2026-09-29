# Bundesliga 2026/27 P1 status snapshot

- Status: In progress; flags-off runtime merged, production source-only evidence accepted, ordinary-continuation proof and activation pending
- Last reconciled: 2026-09-29
- Current contract: [ADR-0086](decisions/0086-bound-live-club-elo-replay-gate.md) and [ADR-0085](decisions/0085-isolate-optional-club-elo-refresh.md), retaining ADR-0083/0084 outside their partial successor boundaries

## Current route

Completed flags-off runtime, focused regressions and exact-head CI → accepted development/production source-only and distinct production retention/rejection evidence → controlled refresh failure with successful ordinary collection and verified retained Elo → reviewed all-eight activation → closeout and existing-schedule monitoring. ADR-0086 records the failed GitHub rerun and removes live replay success from the activation gate.

PR #116 merged the v2-compatible reader with all eight normal source flags false; exact main CI passed. Run `36478629294` accepted official Club Elo across the eight source-only lanes/four heads. A later source timeout in run `36481153403` retained them, while its attempt-2 replay failed closed after the original artifact disappeared. The controlled normal-job continuation probe and A2 remain pending.

The new boundary requires early authorized source-only validation with no Kicktipp/history/roster/model resolution. Optional Node setup is bounded to two minutes and refresh to five, both continue-on-error; enabled jobs allow 52 minutes and disabled jobs 45. Required ordinary collection follows with source flags false and no cycle arguments under normal success conditions. Ordinary failures and workflow cancellation remain failures. Required source-only validation dispatch cannot false-pass. A nonblocking summary reports actual optional outcomes and stale/absent health truthfully.

Ordinary source-off collection validates and retains existing heads without republication or source-cycle access, preserving full provenance, dates, receipt and watermark; no-head seed publication remains, corrupt heads fail. Eight ordered receipts/four heads and sixteen jobs/eight context-match pairs remain fixed, including cron, secrets, model/prompt/cap routes, posting and copy. Rollback is reviewed all-eight flags-off with retained context and zero source-service interaction. P1-05/R1 remains deferred and source-disabled.

Historical 54 reds (P4/H12/Z31/Docker7) remain failed evidence. Concrete product/security defects block release and require focused passing regressions. Local Docker capability, repeated full-suite P confirmation and historical timeout attribution are no longer independent activation gates; required exact-runner CI carries unavailable local checks. CT correction 1/1 and diagnostic execution 1/1 remain spent. Runtime/concurrency diagnosis is parked; no .NET concurrency side program or budget alias is authorized. Timeout cause remains uncertain; production monitoring follows deployment.

## Historical dormant snapshot

The retained snapshot below records the old dormant closeout. It is historical
only and does not override the current table above.

# Historical Bundesliga 2026/27 P1 status snapshot

- Snapshot date: 2026-09-15
- Accepted/pushed dormant implementation tip: `1d43ac397eaed4f82db016114630acdd874042c5` on draft PR #111
- Exact-head CI: run `34931605592` green; 10 build/test/coverage checks passed, conditional Pages skipped
- C1 review/integration: satisfied; historical review range `c99e1635428bcfea48271e4169767b38f014148c..852d1798e77d78e4dee4350ddc1d59cba54f60a5`
- Scope: P1-04/P1-05 only

This is a reconciliation aid, not activation, source-reuse, or production
authority. Recheck Git/worktree/CI before relying on it.

| Item | Current state | Next gate |
| --- | --- | --- |
| P1-04 | Dormant implementation complete: C3 and E1 accepted, pushed and exact-head CI green | Separate owner gates for operational/live validation and activation |
| P1-05 | Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082. Dcaribou is `MetadataUnavailable`; there is no probe or provider, observation, receipt, artifact, health, publication, or API action; seed/LKG is retained. | Future R1 remains deferred until a conforming ADR-0079 sidecar and owner gates |

The current DuckDB artifact remains rejection-only (`NO_ELIGIBLE_2026_MEMBERSHIP`,
`UNKNOWN_SOURCE_DATE`); retained seed/LKG dates and enrichment facts must stay
truthful. [ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md)
pins the future sidecar/artifact URLs and rejects all other
provider or URL-adoption claims. A synthetic trusted-date artifact proves
mechanics only. The retained official HTML capture is historical specification
evidence, not an accepting live run. Source flags are false and no other P1
task is released. [ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md)
is operative: a present receipt replays exactly; an absent receipt can reduce
only a proved `expected == current == target` case to no-rewrite `Unchanged`.
C2 is reusable common evidence. ADR-0081 adds the required receipt-completion seam and
family-specific publication rules implemented by accepted C3/E1.
The rejected R1 `b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected
closeout `80b7c6c` remain unintegrated evidence only and confer no
implementation credit. Accepted E1 satisfies W1's implementation prerequisite; W1/A1 are outside this
objective and not released. A1 later owns E1 attribution only, while roster
attribution waits for future accepted R1. Existing v1/v2 remain unchanged; no
v3 relabel, migration, or backfill occurs.

## Accepted dormant evidence

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.
