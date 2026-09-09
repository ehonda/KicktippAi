# ADR-0077: Refresh Club Elo from official HTML

- Status: Accepted
- Implementation: Not started
- Date: 2026-09-07

> ADR-0081 refines this ADR's C3/E1 ownership, shared-selection,
> receipt-completion and publication seams. This ADR remains Accepted; its
> detailed HTML acquisition and parser contract remains operative unless
> ADR-0081 explicitly refines it.

## Context

The CSV route accepted by ADR-0073 and described by the Club Elo portion of
ADR-0074 remains useful negative evidence only: it did not yield a date-bound,
accepting capture. The Owner selected `https://clubelo.com/GER` after the exact
official HTML capture recorded in the P1-04 handoff. Selection does not permit
live acquisition, unattended reuse, development persistence, production
publication, GitHub artifact/issue work, or source activation.

## Decision

This ADR narrowly supersedes only the **Club Elo** CSV descriptor, source,
payload, health-date, reconstruction, and successor-publication portions of
ADR-0073 and ADR-0074. Their cycle, head, receipt, health, disabled-source,
roster, and historical-document rules remain operative. Existing CSV-era
evidence and v1/v2 bytes remain history and are never rewritten.

### Descriptor, bundle, and provenance

Dispatch is descriptor-selected and exclusive:

- `club-elo-direct-csv-descriptor/v1` selects only `club-elo/source.csv`.
- `club-elo-official-html-descriptor/v1` selects only `club-elo/source.html`.

An eligible observation contains exactly one selected payload, never both paths,
no cross-path fallback, and no other Club Elo file; rejected observations have
none. Bundle allowlisting otherwise remains `manifest.json`, `bundle.sha256`,
optional selected Club Elo payload, and optional roster payload; links,
traversal, casing variants, duplicates and extras fail. CSV bytes are not a
future implementation requirement.

The HTML descriptor root order is `contract, sourceUrl, response, rawSha256,
rawByteLength, parserContract, displayedDate, providerDateEvidence,
tableContract, tableHeader, nameMappingContract, nameMappingSha256, sourceRows,
evaluation`. `response` order is `statusCode, finalUrl, redirectCount,
redirectLocation, mediaType, charset, contentEncodings, declaredContentLength`.
Rows are manifest-slug ordered `teamSlug, providerRoute, providerDisplayName,
globalRank, elo`. The publication-v2 provenance extension is exactly
`cycle_id, attempt_id, source_observed_at, raw_sha256, raw_byte_length,
displayed_date, provider_date_evidence, name_mapping_sha256, source_rows,
sourceDescriptorSha256, sourceDescriptor, selectedPayload` after the immutable
v1 fields. `sourceDescriptor` is the complete canonical HTML descriptor in its
exact root order; `sourceDescriptorSha256` is the SHA-256 of its standalone
canonical JSON. `selectedPayload` is ordered `path, rawSha256,
rawByteLength`. For an eligible HTML publication it identifies only
`club-elo/source.html`, and its SHA and length equal both the embedded
descriptor's raw facts and the immutable bundle payload's SHA and length. The
flattened descriptor-derived provenance fields remain useful, but each must
equal its corresponding `sourceDescriptor` value.

Before writing an HTML v2 context document, publication reconstructs and
validates the complete descriptor, its SHA, and the selected-payload identity
against the immutable bundle. The raw payload bytes remain only in that bundle:
the context document does not embed them and cannot recreate them without the
bundle. A context-document verifier can recompute the descriptor SHA and bind
the payload identity, but does not claim that the flattened fields alone
reconstruct either the descriptor or payload bytes. Rejected and fallback
publications retain their applicable historical provenance contract and never
fabricate an HTML payload. Health/reception source date for HTML is
`displayedDate`; it maps to the already ordered receipt `ratedAt` field and no
observation, transport, upload, or publication time substitutes for it.

All integer values are JSON integers in the `Int32`/`Int64` ranges required by
their fields: `statusCode`, `redirectCount`, declared and raw byte lengths,
`globalRank`, and `elo`. Firestore reconstruction rejects doubles, strings,
fractions, overflow, coercion, absent/extra/reordered fields, and noncanonical
hashes. When present, `declaredContentLength` is non-negative and, for
`Eligible`, equals raw length.

The preceding equality applies only to `Eligible`. The per-evaluation matrix
below is the sole normative source for every evaluation-varying null/required
cell. The retained CSV descriptor continues to select only `club-elo/source.csv`,
but no future CSV source implementation is authorized.

Literal descriptor values are: `parserContract` is
`club-elo-official-html-parser/v1`; `tableContract` is
`club-elo-official-html-table/v1`; `nameMappingContract` is
`bundesliga-2026-27-club-elo-name-map/v1`; `providerDateEvidence` is ordered
`kind, recipeId, field, rawValue, ratedAt` and exactly
`kind=OfficialHtmlHeadingLink`,
`recipeId=club-elo-official-html-displayed-date/v1`, and
`field=h1>a[href]`. `rawValue` is the exact untrimmed captured `yyyy-MM-dd`
substring between the final `/` and `/GER` in that same link; `ratedAt` is its
strict calendar-date parse and `displayedDate` equals `ratedAt`. `tableHeader` is exactly the
four-element JSON array `Club`, `Elo`, `+/-`, `Golo` in that order. The mapping
file is exactly 487 UTF-8/NFC/no-BOM bytes with CRLF between lines and no final
line terminator, SHA-256
`8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7`, header
`teamSlug,providerRoute,providerDisplayName`, and exactly 18 ordinal-slug rows
of those three non-empty fields. Its descriptor hash is the plain SHA-256 of
those exact bytes.

`response.statusCode` is a positive `Int32`; `response.redirectCount` is a
non-negative `Int32` (and exactly `0` for an accepting response); each row's
`globalRank` and `elo` is a positive `Int32`; `declaredContentLength` and
`rawByteLength`, when non-null, are non-negative `Int64` values. `sourceUrl`, `parserContract`,
`tableContract`, `tableHeader`, `nameMappingContract`, and
`nameMappingSha256` are fixed canonical contract identity, not stage-relative
observed evidence, and are required in every matrix row. Observed response,
raw, parser/table, date, mapping, and row evidence are distinct from those
fixed identities. The matrix below is the sole normative null/required source
for that observed evidence and payload; no field is synthesized from a
transport, observation, upload, or publication timestamp.

`evaluation` is exactly `Eligible`, `TransportRejected`, `SizeRejected`,
`ResponseRejected`, `DomRejected`, `LexerRejected`, `FragmentRejected`,
`DateRejected`, `MappingRejected`, `CoverageRejected`, `StaleRejected`, or
`NotNewer`.
`Eligible` maps to `ArtifactCaptured` and `[]`; every other evaluation maps to
`Rejected` and respectively `CLUB_ELO_TRANSPORT_REJECTED`,
`CLUB_ELO_SIZE_REJECTED`, `CLUB_ELO_RESPONSE_REJECTED`,
`CLUB_ELO_DOM_REJECTED`, `CLUB_ELO_LEXER_REJECTED`,
`CLUB_ELO_FRAGMENT_REJECTED`, `CLUB_ELO_DISPLAYED_DATE_REJECTED`,
`CLUB_ELO_MAPPING_REJECTED`, `CLUB_ELO_COVERAGE_REJECTED`,
`CLUB_ELO_STALE_GT_7_DAYS`, or `CLUB_ELO_NOT_NEWER`. A terminal
connection/timeout/HTTP cause is retained as the additional final code
`CLUB_ELO_CONNECTION_FAILED`, `CLUB_ELO_TIMEOUT`, or
`CLUB_ELO_HTTP_REJECTED`, unique and ordinal-sorted with the evaluation code.

Publication-v2 maps descriptor fields without transformation: `cycle_id` =
cycle ID, `attempt_id` = attempt ID, `source_observed_at` = observed UTC,
`raw_sha256` = raw SHA, `raw_byte_length` = raw length,
`displayed_date` = displayed date, `provider_date_evidence` = the five-field
object above, `name_mapping_sha256` = mapping SHA, and `source_rows` = the
ordered descriptor rows. All flattened descriptor-derived fields are required
for an HTML v2 publication and equal the embedded `sourceDescriptor`; the
complete descriptor, its SHA, and `selectedPayload` are required as described
above. Historical v1/v2 bytes are never retrofitted.

The complete tracked canonical mapping is the following exact 18-row semantic
mapping, in ordinal `teamSlug` order. At E1 admission it is materialized as the
487-byte UTF-8/NFC/no-BOM file with CRLF between lines and no final terminator, header
`teamSlug,providerRoute,providerDisplayName` and SHA-256
`8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7`.
This table is the Accepted materialization, not ignored evidence; no route/name
is inferred. The former 475-byte/`ee14…3772` pair is a superseded,
non-normative handoff proposal.

| teamSlug | providerRoute | providerDisplayName |
| --- | --- | --- |
| b04 | `/Leverkusen` | Leverkusen |
| bmg | `/Gladbach` | Gladbach |
| bvb | `/Dortmund` | Dortmund |
| fca | `/Augsburg` | Augsburg |
| fcb | `/Bayern` | Bayern München |
| fck | `/Koeln` | Köln |
| fcu | `/UnionBerlin` | Union Berlin |
| hsv | `/Hamburg` | Hamburg |
| m05 | `/Mainz` | Mainz |
| rbl | `/RBLeipzig` | RB Leipzig |
| s04 | `/Schalke` | Schalke |
| scf | `/Freiburg` | Freiburg |
| scp | `/Paderborn` | Paderborn |
| sge | `/Frankfurt` | Frankfurt |
| sve | `/Elversberg` | Elversberg |
| svw | `/Werder` | Werder |
| tsg | `/Hoffenheim` | Hoffenheim |
| vfb | `/Stuttgart` | Stuttgart |

### Exact acquisition and parser contract

Request only `https://clubelo.com/GER`, with redirects disabled. The size gate
has four ordered evidence states. First, a valid declared `Content-Length`
greater than `2097152` before an entity read is advertised-oversize
`SizeRejected`, with raw fields null. Otherwise read to the hard observed limit
of `2097153`: observing byte `2097153` is stream-limit `SizeRejected`, with raw
fields null; that deliberate stop is not a transport failure. EOF at zero is
complete-empty `SizeRejected`, with `rawByteLength` `0` and `rawSha256`
`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. EOF at
`1..2097152` with a present declared length unequal to the actual length is
complete-length-mismatch `SizeRejected`, with the exact raw pair. A failure
before EOF or other size proof is `TransportRejected`. Only complete bodies
have raw length/hash fields, and complete raw bytes are hashed before
decoding/parsing.

After the size gate, an accepting response has status `200`, final URL exactly
that URL, redirect count `0`, null location, media type `text/html`, exactly
one UTF-8 charset, no content encoding, and complete raw bytes in
`[1, 2097152]`.

Use inert AngleSharp `1.7.2` only: no scripting, browsing context, navigation,
resource loading, CSS evaluation, or subresource fetch. Require exactly one
`div.blatt`, its direct exact-Germany `h1` link, one `table#eloTable` inside a
direct-child wrapper, exact `thead` and empty `tbody`, headers `Club`, `Elo`,
`+/-`, `Golo`, and the adjacent inline script. Bind the displayed date to the
same response through exactly `/yyyy-MM-dd/GER` and text `Germany`; it is a
valid calendar date and is the sole rating-date evidence.

The inline script is parsed by a bounded literal lexer, never a JavaScript
engine: exactly one `const eloData = <array>;`, exactly one
`JSSortableEloTable(eloData);`, no other `eloData` identifier, at most 262144
characters, 512 rows, four strings/row, and 8192 characters/string. Its grammar
allows only brackets, commas, ASCII whitespace, single-quoted strings and the
frozen minimal escape set. Each first-cell fragment is parsed in a fresh inert
`tr` context and has exactly one `td` root and exact element children `a`,
`small`, `a`; it has exactly two anchors and one small in its subtree, raw
decoded hrefs, first href `/GER`, a positive rank, a safe provider route, and
exact NFC display text. Event attributes, duplicate attributes, active/unknown
descendants, and structural repair outside this grammar fail.

The exact lexer escapes are `\\`, `\'`, `\n`, `\r`, `\t`, `\xHH`, and
`\uHHHH`, where hexadecimal digits are ASCII and decoded scalar values must be
valid NFC text; every other backslash sequence fails. A safe provider route is
an ASCII absolute path matching `^/[A-Za-z0-9][A-Za-z0-9-]{0,127}$`, is not
`/GER`, contains no query/fragment/percent escape, and is unique. The fragment
grammar permits only whitespace text surrounding three direct children:
`a[href="/GER"]`, `small`, `a[href=providerRoute]`; each is a leaf with one
non-empty NFC text node, the first anchor's text is the positive decimal rank,
and the final anchor's text is the provider display name. No other node,
attribute, entity repair, or descendant is accepted.

Ranks strictly increase; routes and ranks are globally unique; Elo is a
positive integer and may tie. Map the exact `(providerRoute, providerDisplayName)`
pair to exactly 18 manifest slugs, producing ordinal slug order. Extra German
clubs are permitted; crossed or ambiguous identities fail. Required mapping
bytes are UTF-8/no-BOM/CRLF and their plain SHA-256 is descriptor-bound.

Only connection failure, timeout, `408`, `429`, and `5xx` retry: at most two
attempts in a monotonic 25-second budget, each at most ten seconds, one exact
five-second injected delay, no jitter or `Retry-After`. Caller cancellation is
rethrown. The terminal attempt alone forms the descriptor. Exact diagnostics
are `CLUB_ELO_CONNECTION_FAILED`, `CLUB_ELO_TIMEOUT`,
`CLUB_ELO_HTTP_REJECTED`, `CLUB_ELO_TRANSPORT_REJECTED`,
`CLUB_ELO_SIZE_REJECTED`, `CLUB_ELO_RESPONSE_REJECTED`,
`CLUB_ELO_DOM_REJECTED`, `CLUB_ELO_LEXER_REJECTED`,
`CLUB_ELO_FRAGMENT_REJECTED`, `CLUB_ELO_DISPLAYED_DATE_REJECTED`,
`CLUB_ELO_MAPPING_REJECTED`, `CLUB_ELO_COVERAGE_REJECTED`,
`CLUB_ELO_STALE_GT_7_DAYS`, and `CLUB_ELO_NOT_NEWER`; codes are final
literals, never prefixed again.

Evaluation stops at the first failed gate, in this exact order: transport
completion; size; response shape; DOM; lexer; fragment; displayed-date validity;
name mapping; exact 18-club coverage; seven-calendar-day freshness; and
strictly-newer-than-retained. Those gates yield, respectively,
`TransportRejected`, `SizeRejected`, `ResponseRejected`, `DomRejected`,
`LexerRejected`, `FragmentRejected`, `DateRejected`, `MappingRejected`,
`CoverageRejected`, `StaleRejected`, and `NotNewer`; `Eligible` occurs only
after all gates pass.

Invalid or missing displayed-date evidence yields `DateRejected`. A valid stale
candidate with a mapping failure yields `MappingRejected`, and one with a
coverage failure yields `CoverageRejected`; both stop at the earlier failed
gate. `StaleRejected` requires proven response/raw/parser/table/mapping/date
evidence and exactly 18 ordered rows, has null payload, and preserves those
facts for health/date semantics. `NotNewer` occurs only after freshness passes.
Partial or structural candidates stop at their first earlier failed gate.

The exact HTML evaluation matrix below has required non-null (`R`) and
explicit-null (`N`) values. `C` is an observed non-negative `Int64` when a valid
`Content-Length` exists and otherwise explicit null. `R` in `response` means a
canonical response object is present; `declaredContentLength` follows its own
column. `sourceUrl` and fixed contract identity fields are R in every row;
`retained seed/LKG` is never descriptor data and remains the unchanged selected
fallback for every rejected row. This matrix is the sole normative source for
observed-evidence and payload nullability.

| Evaluation | response | declaredContentLength | rawByteLength | rawSha256 | other observed evidence | rows | payload | disposition | diagnostics |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Eligible | R | C (equal raw if present) | R | R | R | R | R | ArtifactCaptured | `[]` |
| TransportRejected | N | N | N | N | N | N | N | Rejected | `CLUB_ELO_TRANSPORT_REJECTED` plus terminal connection/timeout/HTTP code when applicable |
| SizeRejected — advertised oversize | R | R > `2097152` | N | N | N | N | N | Rejected | `CLUB_ELO_SIZE_REJECTED` |
| SizeRejected — streaming limit crossed | R | C (null or <= `2097152`) | N | N | N | N | N | Rejected | `CLUB_ELO_SIZE_REJECTED` |
| SizeRejected — complete empty | R | C (null or `0..2097152`) | R exactly `0` | R empty hash | N | N | N | Rejected | `CLUB_ELO_SIZE_REJECTED` |
| SizeRejected — complete nonempty mismatch | R | R `0..2097152`, unequal raw | R `1..2097152` | R | N | N | N | Rejected | `CLUB_ELO_SIZE_REJECTED` |
| ResponseRejected | R | C | R | R | N | N | N | Rejected | `CLUB_ELO_RESPONSE_REJECTED` |
| DomRejected | R | C | R | R | parser R; table/mapping/date N | N | N | Rejected | `CLUB_ELO_DOM_REJECTED` |
| LexerRejected | R | C | R | R | parser/table R; mapping/date N | N | N | Rejected | `CLUB_ELO_LEXER_REJECTED` |
| FragmentRejected | R | C | R | R | parser/table R; mapping/date N | N | N | Rejected | `CLUB_ELO_FRAGMENT_REJECTED` |
| DateRejected | R | C | R | R | parser/table R; mapping/date N | N | N | Rejected | `CLUB_ELO_DISPLAYED_DATE_REJECTED` |
| MappingRejected | R | C | R | R | parser/table/date and mapping-rejection evidence R | N | N | Rejected | `CLUB_ELO_MAPPING_REJECTED` |
| CoverageRejected | R | C | R | R | parser/table/mapping/date R | R | N | Rejected | `CLUB_ELO_COVERAGE_REJECTED` |
| StaleRejected | R | C | R | R | R | R | N | Rejected | `CLUB_ELO_STALE_GT_7_DAYS` |
| NotNewer | R | C | R | R | R | R | N | Rejected | `CLUB_ELO_NOT_NEWER` |

ADR-0013 remains operative and is not superseded: the displayed-date/ratedAt
network candidate may replace the retained complete seed/LKG only if it is no
more than seven calendar days before this observation's collection timestamp
and strictly newer than retained `Rated_At`. In the first-failed order above,
invalid or missing date is `DateRejected`; a valid candidate that first fails
mapping or coverage is `MappingRejected` or `CoverageRejected`, even if stale;
only an otherwise complete valid candidate that fails freshness is
`StaleRejected`; and only one that passes freshness but is equal or older is
`NotNewer`. Every rejected outcome retains seed/LKG; the age gate never expires
a retained seed/LKG.

### Literal implementation surface and gates

C3 owns exactly the shared HTML amendment paths:
`src/Core/BundesligaContextSourceBundle.cs`,
`src/Core/BundesligaContextSourceHealth.cs`,
`src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceCycleCoordinator.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceBundleHandoff.cs`,
`tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`,
`tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`,
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`.
E1 owns the literal Club Elo source paths, `src/Orchestrator/Orchestrator.csproj`
for the direct AngleSharp dependency, and fixtures `eligible.html`,
`unknown-source-date.html`, `partial.html`, `hostile-dom.html`,
`hostile-lexer.html`, and `hostile-fragment.html` beneath
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/`.
No unlisted dependency or fixture is implied.

Tests cover transport/headers, redirects, DOM, lexer, fragment grammar, date,
retry budget, route/name mapping, all null/required matrices, hostile fixtures,
strict integer Firestore reconstruction, descriptor-selected payload isolation,
publication-v2 reconstruction, displayed-date health, and disabled-source zero
resolution/writes/API calls. C3/E1 remain dormant until C2 and their fresh
reviews; source flags stay false. Synthetic fixtures prove mechanics only. The
exact real accepting-evidence recipe remains unsupported until separately
specified and approved.

## Consequences

P1-04 is now a dormant HTML lane after C3, not a direct-CSV lane. The owner
retains separate gates for live acquisition, development persistence,
unattended HTML reuse, GitHub artifacts/issues, production writes/activation,
rollback ownership, restoration, and completion evidence.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-04/P1-05 design](../designs/p1-04-05-context-refresh.md)
- [P1-04/P1-05 packet](../p1-04-05-execution-packet.md)

## Supersedes

Only the Club Elo portions described above of ADR-0073 and ADR-0074. It does
not amend roster acquisition, historical CSV evidence, or any accepted
historical publication bytes.
