# P1-04 / P1-05 context-refresh design

- Status: Frozen implementation design; no runtime implementation or activation
- Authority: [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md)
- Tasks: [P1-04](../tasks/p1-04-club-elo-refresh.md) then [P1-05](../tasks/p1-05-roster-refresh.md)

## Purpose and seam

P1-04 and P1-05 observe mutable strength and roster sources only as part of an
existing Bundesliga context cycle. The implementation must preserve the serial
context-to-prediction topology, independent community heads, atomic context
publication, and all existing seed/LKG safety boundaries.

```text
outer context cycle -> one due source observation/bundle -> repeated context jobs
                    -> exact bytes/rejection reused       -> community-specific LKG selection
                                                        -> independent atomic heads
```

The bundle contains exact bytes plus descriptor/hash, or one stable rejection.
Manual profile collection acquires once for that invocation; repeated arena jobs
do not reacquire. Individual seed/local-file commands remain deterministic
consumers. A later missed community can use retained exact bundle evidence only
through separately authorized retry or the next cycle; it grants no refetch.

## Provenance vocabulary

- **Cycle observation** is when this application checked a source; it is
  diagnostic-only.
- **Artifact identity** is SHA-256 of exact bytes plus immutable embedded or
  upstream revision; weak HEAD fields are change hints only.
- **Source capture date** is the proven upstream capture date. Build, upload,
  HTTP metadata, download, and publication dates never substitute for it.
- **Record effective date** is when one fact applies. It does not make a stale
  source capture fresh.
- **Membership as of** and **enrichment as of** remain distinct. Carried fields
  retain their original provenance and age.

Existing Club Elo v1 and roster v1/v2 metadata stays immutable and
reconstructable. Implementation adds a Club Elo v2 successor and roster v3
successor; neither rewrites historical metadata or labels carried data newly
sourced.

## Club Elo candidate

The approved public CSV is an additional candidate provider, not a replacement
for the dated seed interface. Hash raw bytes before parsing and require the
frozen header, one coherent proven date, explicit daily-name-to-manifest
mapping, all 18 clubs, positive Elo/ranks, deterministic ordering, and a
strictly newer proven rating date. Identical ratings with a newer real date are
a valid revalidation; equal/older dates are not-newer.

Only connection, timeout, 408, 429, and 5xx failures retry: two attempts of at
most ten seconds, no more than five seconds delay, and 25 seconds total.
Semantic, CSV, coverage, or name/date rejection is immediate. Unknown or
contradictory date evidence produces `UNKNOWN_SOURCE_DATE`, a warning, and LKG.
Exact daily-name/date semantics must be frozen before accepting real bytes.

## Roster candidate

One metadata check occurs per due cycle. Only a changed or still-pending exact
revision is downloaded; stream it to a temporary file within five minutes and
300 MiB, hash it, recheck remote identity, open read-only, then verify its
embedded revision. At most one transient retry shares that budget.

Membership requires explicit 2026/27 club/player rows, all existing
ADR-0011 gates, and a revision-bound authoritative capture/effective date. The
current paused upstream therefore rejects a real candidate until its date
binding is proven. A future descriptor path may be implemented and tested
without claiming current membership is fresh.

Resolve membership first. For enrichment, carry a missing supplemental value
only from the prior same stable player ID and preserve its provenance/age; give
a new unknown player `N/A` plus a warning. Accept genuine consistently sourced
valuation decreases. Reject candidate-only conflicts; fail on contradictions in
the final selected complete set. A rejected candidate falls back only for the
affected club and still passes global 18-club identity and atomic publication.

## Failure, reporting, and recovery

Transport/semantic candidate defects retain seed/LKG with warning. Corrupt
seed/head metadata, final-set failure, concurrency error, or atomic publication
failure is fatal and blocks serial descendants; it never rolls back an already
published head. Source-copy compatibility continues to fail closed.

Dry-run has permitted public reads, parsing, selection, hashing, and reporting
only; it writes no health, issue, context, or publication state. Markdown
summaries and warning annotations report attempt ID, disposition, exact
identity/hash, source dates or `unknown`, selected origin/date/age, and carried
field counts/ages.

One exact-marker reusable issue exists per source. Failed cycles and staleness
are separate: open after two due-cycle failures, or immediately at Club Elo
seven days / membership fourteen days / enrichment fourteen days; roster
severity rises after 30 days. An unchanged valid check cannot advance source
dates or clear stale data. Only actual recovery closes the issue.

## Implementation gates and verification

Before workflow edits, independently accept the durable per-cycle health and
artifact-handoff/recovery seam. Keep collector order and production prediction
topology unchanged; add neither standalone schedules nor prompt attribution.
README-linked source attribution is a future implementation artifact.

Development-first tests cover exact bytes/date/name mapping, unchanged/partial
Elo, retry limits, source-date rejection, paused-versus-fresh roster capture,
revision/hash drift, time/size limits, affected-club fallback, departures,
candidate-only/final-set conflicts, carried/`N/A` enrichment, valuation
decreases, immutable v1/v2 reconstruction and v2/v3 provenance, same-cycle
reuse, serial failure, issue threshold/recovery, dry-run, manual profile, and
copy-context fail-closed behavior. A separately reviewed first production
activation then verifies heads, warnings, issue behavior, and copy compatibility
without paid model or posting work.

Rollback disables network acquisition and preserves seed/LKG and immutable
heads; the owner controls cron disable/resume. P1-04 completes only after a
real accepted CSV refresh and no-change cycle. P1-05 completes when trustworthy
enrichment automation and a proven future valid membership takeover are active;
paused upstream may leave membership on LKG and its source issue open.
