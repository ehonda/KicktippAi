# ADR-0081: Close Club Elo HTML publication and selection seams

- Status: Accepted
- Implementation: Pending; this decision does not enable a source
- Date: 2026-09-09

## Context

ADR-0077 selected the official Club Elo HTML and froze the detailed acquisition,
inert-parser, table, lexer, fragment, response and mapping contract. C2 is
accepted at `c7cc0712b16834d4013948949f1502514ae46770`, but review found that
the remaining C3/E1 boundary did not completely specify receipt completion,
shared selection, or source-backed publication reconstruction. Those seams
must be closed before either implementation can resume. Historical data and
metadata, source flags, recovery topology, schedules, models, prompts,
credentials, posting and copy behavior remain unchanged.

## Decision

### Scope, ownership, and invariants

This ADR supersedes the C3/E1 ownership, shared-selection, receipt-completion,
and Club Elo publication portions of ADR-0077. Its detailed acquisition and
parser contract remains operative except where this ADR explicitly refines it.
ADR-0074's cycle, health, receipt, immutable-bundle and eight-independent-head
rules, ADR-0078's publication fence, ADR-0079's roster boundary, and ADR-0080's
transitional recovery matrix remain operative.

One immutable enabled-source observation is reused by eight independent
community heads. Document-byte snapshot identity and original historical
metadata, creation time and predecessor remain immutable. C3 owns canonical
HTML descriptor/diagnostics/payload, immutable bundle and handoff, receipt and
health family dispatch, the existing Club Elo publication builder/reconstructor,
and preparation-bound receipt completion. E1 owns acquisition, inert parsing,
evaluation, exact mapping materialization, the source provider and Club Elo
command consumption/guarded publication. W1 alone owns adapters, DI and
workflows. No CSV acquisition, schema/journal expansion, roster change, live
action or activation is authorized.

### HTML descriptor and evaluation

All ADR-0077 fixed identities, root order, field names, types, parser/table/
header/mapping identities, response predicates and first-failed gate order are
always present; they are contract identity rather than stage-relative observed
proof and are never null. No evidence field is added. Let `M = 2097152`.
A complete raw pair contains both the exact SHA-256 and length, never one.
`size passed` means a complete raw pair, length `1..M`, and declared length
null or equal to raw length. `response passed` additionally means status 200,
final URL exactly `https://clubelo.com/GER`, zero redirects, null location,
`text/html`, exactly one UTF-8 charset canonicalized as `utf-8`, and no content
encodings.

The required/null matrix is exact:

| Evaluation | Required proof | Required nulls |
| --- | --- | --- |
| TransportRejected | fixed identities only | response, raw/date pair, rows, payload |
| SizeRejected—advertised | response; declared length `> M` | raw/date/rows/payload |
| SizeRejected—streaming | response; declared null or `0..M`; E1 proves byte `M+1` was observed | raw/date/rows/payload |
| SizeRejected—empty | response; raw length 0 and SHA `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | date/rows/payload |
| SizeRejected—mismatch | response; declared `0..M`; complete raw pair with unequal length | date/rows/payload |
| ResponseRejected | size passed and a failed response-shape predicate | date/rows/payload |
| DomRejected | response passed | date/rows/payload |
| LexerRejected | response passed and E1-proved DOM | date/rows/payload |
| FragmentRejected | response passed and E1-proved DOM/lexer | date/rows/payload |
| DateRejected | response passed and all preceding parser gates | date pair/rows/payload |
| MappingRejected | response passed, valid date pair and E1 mapping-rejection proof | rows/payload |
| CoverageRejected | response passed, date pair and canonical proper-subset rows (empty permitted) | payload |
| StaleRejected | response passed, date pair, exactly 18 canonical rows, candidate older than seven calendar days | payload |
| NotNewer | response passed, date pair, exactly 18 rows, freshness and retained comparison | payload |
| Eligible | every gate passed; exactly 18 rows; selected HTML payload equals raw pair | none |

Both date fields are required together or absent together. The five-field HTML
`providerDateEvidence` is exact: `rawValue == ratedAt == displayedDate` after
a strict, untrimmed parse. Evaluation is dated at `observedAtUtc`; a future or
invalid source date is `DateRejected`, never substituted from another timestamp.
C3 validates represented evidence and earlier-gate consistency; E1 proves
actual DOM/lexer/fragment execution through fixtures because rejected
descriptors intentionally lack intermediate/raw-payload proof. Every size
branch checks its trailing nulls before returning. Source rank increases before
slug projection; descriptor rows are slug ordered and ranks need not increase
after projection.

Every projected row, including partial coverage, exactly matches ADR-0077's 18
`teamSlug`/`providerRoute`/`providerDisplayName` triples; regex/hash matching is
insufficient. Thus `fcb` is `/Bayern`/`Bayern München` and `fck` is
`/Koeln`/`Köln`. HTML ranks are unique positive Int32 values; HTML Elo is a
positive Int32 and may tie. Identity crossing, missing, duplicate, extra mapped
rows or an invalid manifest order rejects. Extra German clubs may be parsed but
are never projected. E1 materializes the exact mapping as UTF-8 NFC without BOM, CRLF,
no final terminator, 487 bytes and SHA-256
`8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7`; the C3
mapping test reproduces these bytes and hash through its existing
descriptor-contract surface. No new helper, model, dependency or fixture path
is inferred.

Only `Eligible` is `ArtifactCaptured` with selected payload and `[]`.
Every other evaluation is `Rejected`, no payload, and its exact ADR-0077
evaluation code. Only `TransportRejected` may additionally carry zero or one
terminal `CLUB_ELO_CONNECTION_FAILED`, `CLUB_ELO_TIMEOUT`, or
`CLUB_ELO_HTTP_REJECTED`. The array is unique and ordinal-sorted; a terminal
cause means the terminal attempt, never an array-position override. Multiple,
duplicate, unknown, unsorted, wrongly prefixed, or nontransport extra causes
fail. ADR-0080's separate roster diagnostic-precedence validator is unchanged.

### Shared selection, health, and receipts

Receipt dispatch is descriptor-family explicit. HTML `Eligible` that is newer
for a lane is `NetworkAccepted`/`NetworkCandidate` with
`ratedAt == displayedDate`; eligible but equal/newer retained data is
`NetworkCandidateNotNewer`/validated seed, LKG or original selected date;
`StaleRejected` is `NetworkCandidateStale`/validated retained-or-original
date; `NotNewer` is `NetworkCandidateNotNewer`/validated retained-or-original
date at least the candidate date; all other HTML rejections are
`NetworkCandidateRejected`/validated retained-or-original date. An eligible
HTML candidate never becomes generic reject or stale. Existing CSV
ArtifactCaptured accepted/stale/not-newer behavior is unchanged. In every
receipt `ratedAt` is selected data, never a rejected candidate; Elo roster
dates and revision remain null/carry zero.

Before shared evaluation, freeze verified freshest seed/LKG selections for all
expected consumers using exact publication evidence. Shared `NotNewer` is valid
only when the candidate is no newer than every retained selection (therefore no
newer than the minimum retained date); otherwise it remains `Eligible` with a
payload so lagging lanes can advance independently. A missing head uses a valid
seed. Corrupt, unverified or contradictory retained evidence is fatal—never a
producer-only head or an unverified health date. Consumption revalidates the
actual retained selection; contradiction is fatal and mutation-free. No field
is persisted and no provider signature changes. Rejected-observation health
counters, including stale/not-newer, remain ADR-0074 reductions once per
completed cycle; selected freshness has its separate current-staleness
reference, and replay/supersession is mutation-free.

C3 adds a preparation-bound operation in its existing
`ContextSourceCycleCoordinator`: it accepts the exact
`BundesligaContextSourceReceiptRequest`, validates current cycle, lane, source,
bundle and observation, routes through `RecordReceiptAsync`, and returns the
persisted canonical receipt. It supports the new rejected-with-head
`NotAttempted` receipt and exact replay of receipts atomically created by a
guarded `Published`, `Unchanged` or `Reactivated` operation. This is the
mandatory post-commit issue-projection seam. Dry-run writes nothing and
cross-identity fails. E1 calls it after every source-aware outcome. Existing
preparation only reconciles receipts loaded before the command; it is not a
substitute, and `Unchanged` must never be fabricated merely to record
`NotAttempted`.

### Publication and reconstruction

The existing nine-property v1 prefix and prompt/KPI CSV bytes remain unchanged.
The existing builder gains an explicit source-backed build API supplied with the
selection, cycle/observation and verified immutable payload inputs. It dispatches
only these exact schema/shape families:

- v1: `club-elo-publication-v1`, exactly the existing nine fields and semantics.
- Historical CSV-v2: `club-elo-publication-v2`, then exactly
  `cycle_id, attempt_id, source_observed_at, raw_sha256, raw_byte_length,
  provider_date_evidence, name_mapping_sha256, source_rows`. Its nested
  snake_case evidence/rows remain ADR-0074 shape; it receives no HTML nested
  provenance or mapping retrofit.
- HTML-v2: the same version literal, then exactly
  `cycle_id, attempt_id, source_observed_at, raw_sha256, raw_byte_length,
  displayed_date, provider_date_evidence, name_mapping_sha256, source_rows,
  sourceDescriptorSha256, sourceDescriptor, selectedPayload`. HTML nested
  evidence/rows copy camelCase unchanged. `selectedPayload` order is `path`,
  `rawSha256`, `rawByteLength`, deliberately distinct from bundle payload order.

Unknown, mixed or incomplete shapes fail without fallback. A shared
build/reconstruction truth validator requires exact prefix/extension/nested
shape, key order and types; `NetworkCandidate`/`NetworkAccepted`/`[]`; valid
cycle and deterministic Club Elo attempt; `collected_at == source_observed_at
== observation.observedAtUtc`; `rated_at == displayed_date == descriptor
displayedDate == evidence.ratedAt`; exact source URL; and 18 rows equal the
selected snapshot's slug/rank/Elo. It verifies the standalone canonical
descriptor SHA, flattened/nested equality, and `selectedPayload`
`club-elo/source.html` SHA/length equal to descriptor and immutable bundle.
The builder hashes actual immutable bytes before writing. Context-only
reconstruction checks embedded identities/hash but does not claim missing raw
bytes. Existing complete-18 per-team documents, aggregate, rank policy and
byte-only snapshot identity remain. Provenance-only change retains a head;
historical reactivation preserves metadata/time/predecessor. A rejected or
fallback case uses the applicable truthful legacy contract/diagnostics, retains
the exact HTML diagnostic in the observation, and never labels itself HTML-v2
or invents payload.

Numeric rules are family-first. Historical CSV-v2 evidence parsing and
serialization preserves each exact canonical, positive finite numeric Elo token,
its order and fractional values such as `1500.25`, without rounding,
truncation or constructing a snapshot. An exact integer token is represented as
an Int64 when possible; otherwise the token is represented as a finite double.
Its positive Int32 `global_rank` rule remains unchanged. This evidence contract
may not be tightened to integer Elo.

Full CSV-v2 publication/LKG binding is a separate operation: every source Elo
token must represent a positive Int32 and exactly equal the selected document's
Elo. Fractional or out-of-range evidence remains parseable and serializable,
but publication/LKG reconstruction fails with `InvalidDataException`; a corrupt
headed LKG remains fatal. Therefore a fractional CSV success proves evidence
parse/serialize only, while integral matching evidence is required for
publication reconstruction. HTML-v2 remains stricter: `globalRank` and `elo`
are positive Int32, status/redirect are Int32, lengths are Int64, and every
number has strict canonical integer spelling. HTML rejects a fractional, double,
string, exponent, overflow or coerced value. Native Firestore integers are
range checked before canonical output. Maps have exact keysets and fixed order,
arrays are not normalized, and reconstructed JSON re-hashes identically. C3
proves the fractional CSV evidence roundtrip paired with HTML rejection and the
separate integral CSV publication-reconstruction case.

### Literal paths, proof, and release gates

C3's exact 13 paths are:

1. `src/Core/BundesligaContextSourceBundle.cs`
2. `src/Core/BundesligaContextSourceHealth.cs`
3. `src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`
4. `src/Orchestrator/Commands/Operations/CollectContext/ContextSourceCycleCoordinator.cs`
5. `src/Orchestrator/Commands/Operations/CollectContext/ContextSourceBundleHandoff.cs`
6. `tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`
7. `tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`
8. `tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`
9. `tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`
10. `src/Core/BundesligaClubEloPublication.cs`
11. `tests/Core.Tests/BundesligaClubEloPublicationTests.cs`
12. `tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceBundleHandoffTests.cs`
13. `tests/FirebaseAdapter.Tests/FirebaseDocumentPublicationRepositoryTests.cs`

The last path is tests only; no `FirebaseDocumentPublicationRepository.cs`
implementation is authorized. E1's exact 17 paths are the existing 13
ADR-0077 source/mapping/fixture paths plus
`CollectContextClubEloCommand.cs`, its command and Firestore tests, and
`tests/Orchestrator.Tests/Orchestrator.Tests.csproj`. The project edit provides
only deterministic availability of the six named fixtures. E1 adds a direct
AngleSharp reference to `Orchestrator.csproj` using the existing central 1.7.2
pin; neither central package changes nor inferred helpers/models/dependencies/
fixtures are authorized. R1 and W1 fences are unchanged.

C3 proves every evaluation and size subcase, gate contradictions, body and
declared-length boundaries, mapping/diagnostic cross-products, receipt matrix
and replay, all JSON/Firestore type/shape/hash hostiles, v1/CSV-v2/HTML-v2
roundtrips, actual handoff path rejection, direct guarded Firebase cases,
receipt completion/issue projection, and disabled zero interaction. E1 adds
the exact URI/headers/redirect/encoding/bounded-read/retry proof, all six
fixtures and hostile grammar bounds, same-response date/gate order/exact mapping
bytes, divergent-head all-lane selection, prepared-observation command behavior,
and disabled legacy zero-service proof. Relevant Core/Firebase/Orchestrator
suites and required workflow checks precede exact-head CI; those validations
are later implementation work, not completion evidence for this decision.

The tracked 11-path specification must receive fresh final specification
acceptance and exact-head publication. Then a fresh C3 writer starts from the
reusable rejected `58e1e40860202c02375bc63a94fcfb28a38d5c61` candidate, receives
incremental review and a fresh final reviewer with no earlier milestone role,
then exact-head CI. E1 follows accepted C3 and its full 17-path acceptance; W1
follows C2 and one accepted source but never partially wires Club Elo. R1
remains independent after C2. This remains ADR-0074's seven-milestone upper
bound, not a per-writer push policy.

### Continuity and authority

Flags remain false; current heads/seed/LKG, recovery schedules/topology,
model/posting/credentials/copy remain untouched. There is no fresh live
acquisition, development persistence, unattended reuse, GitHub artifact/issue
mutation, R2 write or production activation. The owner retains live, rollback,
delegation, restoration and completion gates. Rollback is flag-off plus a
reviewed revert; the project owner is recovery owner. Corrupt, ambiguous,
contradictory or concurrent publication state is fatal and requires manual
recovery. Restoration requires separately authorized real accepted then later
distinct no-change/rejection evidence, all eight receipts, independent heads,
health/issues, copy compatibility and exact-head CI. Synthetic trusted-date
fixtures prove mechanics only; a real accepting-evidence recipe remains
unsupported. P1-05 remains `MetadataUnavailable` with no artifact and seed/LKG
until the exact ADR-0079 sidecar exists; this decision makes no provider-adoption
claim.

## Alternatives considered

- **Leave the extra seams in the orchestration preview:** rejected because a
  durable, implementation-changing contract must survive the run.
- **Use a producer head or health date for shared NotNewer:** rejected because
  it can suppress a lagging independent community and accepts unverified state.
- **Normalize numeric behavior across CSV and HTML:** rejected because it would
  silently break historical fractional CSV Elo compatibility.

## Consequences

- C3/E1 remain dormant implementation work; this ADR records no accepted code,
  live run, persistence, activation or completion evidence.
- Later writers and reviewers have exact seams, paths and proof obligations.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md)
- [P1-04/P1-05 execution packet](../p1-04-05-execution-packet.md)
- [Context-refresh design](../designs/p1-04-05-context-refresh.md)

## Supersedes

The specified C3/E1 seam and publication portions of
[ADR-0077](0077-refresh-club-elo-from-official-html.md); its detailed HTML
acquisition/parser contract remains operative.
