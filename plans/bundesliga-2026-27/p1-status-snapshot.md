# Bundesliga 2026/27 P1 status snapshot

- Snapshot date: 2026-09-08
- Verified integration baseline: `f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`
- C1 review/integration: satisfied; historical review range `c99e1635428bcfea48271e4169767b38f014148c..852d1798e77d78e4dee4350ddc1d59cba54f60a5`
- Scope: P1-04/P1-05 only

This is a reconciliation aid, not activation, source-reuse, or production
authority. Recheck Git/worktree/CI before relying on it.

| Item | Current state | Next gate |
| --- | --- | --- |
| P1-04 | ADR-0077 accepts the official-HTML contract; no implementation or live authority | S3 tracked-contract acceptance/publication, then C2, C3, E1 and reviews |
| P1-05 | dcaribou sidecar is absent; this is `MetadataUnavailable`, with no artifact request and seed/LKG retained | S3 tracked-contract acceptance/publication, then C2; R1 remains dormant until a conforming sidecar and owner gates |

The current DuckDB artifact remains rejection-only (`NO_ELIGIBLE_2026_MEMBERSHIP`,
`UNKNOWN_SOURCE_DATE`); retained seed/LKG dates and enrichment facts must stay
truthful. [ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md)
pins the future sidecar/artifact URLs and rejects all other
provider or URL-adoption claims. A synthetic trusted-date artifact proves
mechanics only. The retained official HTML capture is historical specification
evidence, not an accepting live run. Source flags are false and no other P1
task is released. [ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md)
must be accepted before C2: a present receipt replays exactly; an absent receipt
can reduce only a proved `expected == current == target` case to no-rewrite
`Unchanged`; every expected/current mismatch is fatal and mutation-free. C2
then corrects the five local fence/recovery areas and direct guarded Firebase
matrix before either source lane can proceed.
