# ADR-0073: Refresh strength and rosters during context collection

- Status: Accepted
- Implementation: Not started
- Date: 2026-09-05

## Context

P1-04 and P1-05 need current Club Elo and roster evidence without adding a
second schedule, laundering source freshness through collection time, or making
one community's selected head another community's publication. The prior ADRs
freeze strict snapshot, reconstruction, complete-set, and atomic-publication
boundaries, but do not decide how a recurring context cycle should observe a
mutable external artifact or handle an enrichment-only defect.

## Decision

Refresh is due only within an existing Bundesliga `collect-context profile`
cycle, currently twice daily. One immutable per-source observation (exact bytes
and descriptor/hash, or one stable rejection) is reused by every job in that
cycle; each community independently selects against and atomically publishes to
its own seed/last-known-good head. There is no Elo-only mode, standalone source
schedule, hidden refresh command, matrix/`always()` fanout, provider write, or
production activation in this decision.

Club Elo uses the approved direct public Club Elo CSV and captures its raw bytes
and SHA-256 before parsing. A candidate needs
the frozen header, one proven coherent provider date, explicit daily-name to
manifest mapping, all 18 clubs, positive ranks/Elo values, and deterministic
ordering. It may retry only connection, timeout, 408, 429, and 5xx failures:
at most two ten-second attempts, at most five seconds' delay, and no more than
25 seconds total. Unknown or contradictory date semantics reject the candidate
as `UNKNOWN_SOURCE_DATE`; collection, URL, `From`/`To`, upload, and first-seen
times are not substitutes. A strictly newer proven rating date is required for
publication; equal or older data remains not-newer, while identical ratings at
a newer proven date are a valid revalidation.

Roster metadata is checked once per cycle against the accepted dcaribou
derivative; no alternate provider is in scope. A changed or still-pending exact
revision is streamed to a temporary file, limited to five minutes and 300 MiB,
hashed, rechecked for remote-identity drift, opened read-only, and verified
against its embedded revision before querying. One transient retry fits that
same budget. Membership needs explicit 2026/27 club and player rows plus the
existing quality gates and a revision-bound authoritative capture/effective
date. A recent build, upload, `Last-Modified`, `last_season=2026`, or mutable
object identity never proves that date.

Membership and enrichment are selected independently by stable player ID. A
rejected membership candidate falls back only for each affected club, then the
unchanged complete-18, global-identity, and atomic-publication gates apply;
there is no force bypass. A valid membership change may publish with rejected
enrichment: an existing same-ID supplemental field is carried with its original
provenance and age; a new player without a value is `N/A` and emits a warning.
Candidate-only cross-club conflicts reject the candidate, while a contradiction
in the final selected complete set is fatal. Genuine consistently sourced
valuation decreases are valid. Membership, enrichment, and field-effective
dates remain separate; observation or publication does not refresh any of them.
Recurring refresh never applies P0 launch floors such as `464/464/450`;
departures leave the denominator and complete-set safety plus accepted
per-club percentage diagnostics remain.

External candidate failure, remote drift, hash/revision mismatch, unknown
source date, or candidate-only identity conflict retains valid seed/LKG with a
warning. Corrupt seed/head metadata, invalid final 18-club set, final-set
identity conflict, concurrency error, and atomic-publication failure are fatal
and block existing serial descendants; published heads are never rolled back.
Dry-run may read, parse, select, hash, and report, but writes no health, issue,
context, or publication state. A supplied cycle bundle makes it reproducible
offline.

Every affected job reports source attempt/disposition, immutable
identity/hash, proven source dates or `unknown`, selected origin/date/age, and
carried-field counts/ages, and emits a warning annotation. One reusable issue
per source is found by an exact marker. It opens after two failed due cycles or
immediately when Club Elo is older than seven days, roster membership is older
than 14 days, or enrichment is older than 14 days; roster severities increase
past 30 days. Counts are per due cycle, not repeated community job. A valid
unchanged observation may clear a transport streak but cannot clear stale data;
an issue closes only when every active condition recovers.

Attribution and endpoint/reuse notes belong in a source document linked from
the repository-root README, never in prompt CSV content. This planning ADR does
not add that attribution document or implement any runtime path.

## Alternatives considered

- **A standalone Club Elo/roster schedule:** Rejected because it duplicates the
  context cycle and breaks one-observation-per-cycle reuse.
- **Treating rebuilt dcaribou artifacts as fresh:** Rejected because artifact
  observation, build, and upload dates do not prove upstream data capture.
- **Retaining an entire headed roster on enrichment rejection:** Rejected when
  valid membership and safe same-ID carry/`N/A` enrichment can still satisfy
  the complete-set and atomic gates.

## Consequences

- First enablement is development-first with exact valid, unchanged, rejected,
  and outage fixtures. The first production activation is separately reviewed;
  later valid publications may be automatic only after that gate.
- The implementation must first freeze Club Elo daily-name/date semantics and
  a dcaribou revision-to-authoritative-capture/effective-date recipe. While the
  upstream membership source is paused, `UNKNOWN_SOURCE_DATE` remains the safe
  real-candidate outcome; future descriptor-path tests can still be built.
- The smallest durable cycle-health/deduplication record and artifact-handoff
  recovery behavior need independent implementation-design review before
  workflow edits.

## Supersedes

This ADR narrowly supersedes ADR-0013 only where it left recurring Club Elo
observation, retry, cadence, source identity, and staleness handling open; its
strict 18-club, freshness, last-known-good, and publication gates remain.
It narrowly supersedes ADR-0017 and ADR-0019 only where rejected enrichment
required retaining an entire headed roster: valid membership may now publish
with same-ID carried fields or `N/A`. Their immutable reconstruction,
membership, complete-set, identity, and atomic-publication contracts remain.
All prior ADR files remain immutable.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 context-refresh design](../designs/p1-04-05-context-refresh.md)
