# Bundesliga 2026/27 P1 status snapshot

- Snapshot date: 2026-09-09
- Verified integration baseline: `f37c9e549e952004c5443f25aab363fda6e2811c`
- ADR-0081 publication: workflow `34319614680`, all 12 jobs green
- C1 review/integration: satisfied; historical review range `c99e1635428bcfea48271e4169767b38f014148c..852d1798e77d78e4dee4350ddc1d59cba54f60a5`
- Scope: P1-04/P1-05 only

This is a reconciliation aid, not activation, source-reuse, or production
authority. Recheck Git/worktree/CI before relying on it.

| Item | Current state | Next gate |
| --- | --- | --- |
| P1-04 | ADR-0081 is published/green at the verified integration baseline; C3 is correction-limited at local unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8` and no live authority exists | Fresh bounded C3 diagnosis/reslicing, correction and review; E1 remains blocked until corrected C3 is accepted, published, and exact-head green |
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
family-specific publication rules before either Club Elo implementation lane.
The rejected R1 `b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected
closeout `80b7c6c` remain unintegrated evidence only and confer no
implementation credit. Only accepted E1 may release W1; W1/A1 are outside this
objective and not released. A1 later owns E1 attribution only, while roster
attribution waits for future accepted R1. Existing v1/v2 remain unchanged; no
v3 relabel, migration, or backfill occurs.
