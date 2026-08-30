# ADR-0059: Bind schadensfresse rules to a structured semantic record

- Status: Accepted
- Date: 2026-08-30
- Decision authority: Project Owner authorized evidence-backed necessary and
  sanctioned decisions on 2026-08-30

## Context

ADR-0058 requires a current authenticated rules preflight before
`schadensfresse` DFB-Pokal or Champions-League generation. It made SHA-256
`b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
the live semantic publication and freshness gate. Evidence gathered on
2026-08-30 reproduced that value exactly, but also proved that it is the hash
of a keyword-filtered array of 16 normalized prose strings, not a structured
rules record.

The historical extractor omits the complete numeric `Sieg` and
`Unentschieden` scoring rows because they contain none of its keywords. It also
deduplicates equal strings, silently drops strings longer than 1000 characters,
and can admit unrelated elements containing a keyword. The resulting hash can
therefore remain unchanged while the effective numeric scoring contract
changes. It remains valid historical observation evidence, but cannot prove
the rule values that prompted the target-owned-primary conversion.

The same authenticated read-only page produced an exact typed record whose
canonical JSON is 822 UTF-8 bytes and has SHA-256
`1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90`.
The exact scoring-table extraction independently has SHA-256
`4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e`.
This decision fixes the missing semantic contract before the P1-10 rules
validator and publication slice is implemented. It makes no source, live,
prompt, or schedule mutation and does not claim that the Owner reviewed a
draft of this ADR.

## Decision

### Authenticated source gate

The rules validator performs one authenticated `GET` starting at
`https://www.kicktipp.de/schadensfresse/spielregeln`. It fails closed unless
all of these conditions hold:

- the final response is HTTP `200`;
- the final request URI is present, uses `https`, has host
  `www.kicktipp.de` under ordinal-ignore-case comparison, has no query or
  fragment, and has ordinal exact path `/schadensfresse/spielregeln` after
  removing at most one trailing `/`;
- redirect handling may complete successfully only when the final
  `RequestUri` satisfies that exact target contract. The redirect itself is
  not a failure, but any final login page, non-HTTPS scheme, other host/path,
  query, or fragment fails regardless of the starting URI;
- the parsed document contains no `form#loginFormular`; and
- the normalized document title does not contain `Login` under
  ordinal-ignore-case comparison.

Another success status, a missing final URI, HTTP, a lookalike host, a
community-relative fallback, or a partially rendered/login response is not
accepted.

### Text and value normalization

For every accepted source string, use AngleSharp-decoded `TextContent`, apply
Unicode NFC, call `.Trim()`, and replace each match of the .NET regular
expression `\s+` with one ASCII space (`U+0020`), in that order. Compare the
result with the exact German labels below using ordinal, case-sensitive
comparison. Do not use locale-aware comparison, case folding, keyword or
prefix matching, fuzzy punctuation, or Unicode compatibility normalization.

A numeric source value must match `^(0|[1-9][0-9]*)$` and parse as an Int32
using invariant culture. Signs, leading zeroes, separators, decimal values,
and surrounding text fail. The exact ASCII hyphen `-` is the only nonnumeric
sentinel. It is valid only for the draw goal-difference cell and maps to an
explicit JSON `null`; an empty string, dash variant, `0`, or sentinel in any
other cell fails. Booleans and enum values are derived only from the exact
accepted labels and sentences, never from defaults.

### Exact semantic DOM contract

Select exactly one `div.pagecontent` as the semantic root. Select its direct
headings with `:scope > h2`; there must be exactly six in this order:

1. `Sichtbarkeit der Tipps`
2. `Tippmodus`
3. `Punktegleichstand`
4. `Tippabgaberegel: 0 Minuten Vorlaufzeit`
5. `Punkteregel: 2 - 5 Punkte`
6. `Punkteregel: 9 Punkte`

Each section consists of the direct siblings after its heading and before the
next direct `h2`. The validator consumes this exact shape:

1. `Sichtbarkeit der Tipps` contains one direct `p`, with exact text
   `Die Tipps sind erst sichtbar, wenn die Tippzeit abgelaufen ist.`
2. `Tippmodus` contains direct children `p`, `p`, `ul` in that order. The
   paragraphs are `Es wird das genaue Ergebnis getippt.` and
   `Es wird das jeweils folgende Ergebnis gewertet:`. Select list entries with
   `:scope > li`; there must be exactly three in observed source order:
   `DFB-Pokal 2026/27: nach Elfmeterschießen`,
   `Champions League 2026/27: nach Elfmeterschießen`, and
   `1. Bundesliga 2026/27: 90 Minuten`.
3. `Punktegleichstand` contains one direct `p`, with exact text
   `Soweit nicht etwas anderes vereinbart wurde, entscheidet bei Gleichstand in der Gesamtpunktzahl die Anzahl der Spieltagssiege ("Siege") über die Platzierung der Tipper.`
4. `Tippabgaberegel: 0 Minuten Vorlaufzeit` contains one direct `p`, with exact
   text `Die Tippzeit endet 0 Minuten vor dem Termin des jeweiligen Ereignisses.`
5. `Punkteregel: 2 - 5 Punkte` contains one direct unclassified `div`, which
   contains exactly one `table.ktable`. Across the entire root,
   `div.pagecontent table.ktable` must select that table and no other. The
   table contains exactly one direct `thead` with one direct `tr` and one
   direct `tbody` with two direct `tr` rows. Each row contains exactly four
   direct `th`/`td` cells and no nested or additional cells. Normalized cell
   text must equal this 3-by-4 matrix:

   | Row | Cell 0 | Cell 1 | Cell 2 | Cell 3 |
   | ---: | --- | --- | --- | --- |
   | Header | empty | `Tendenz` | `Tordifferenz` | `Ergebnis` |
   | Win | `Sieg` | `2` | `3` | `5` |
   | Draw | `Unentschieden` | `3` | `-` | `5` |

6. `Punkteregel: 9 Punkte` contains one direct unclassified `div`, which
   contains exactly two direct `p` elements in order:
   `Punkte pro richtiger Antwort: 9` and
   `Punkte gibt es für jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung.`

An unclassified wrapper has no nonempty `class` value. Attributes unrelated to
the selectors above may vary, but selector identity must remain unambiguous.
Missing, duplicate, reordered, nested, wrong-tag, or extra roots, headings,
accepted paragraphs, wrappers, lists, list items, tables, table groups, rows,
cells, result-basis labels, scoring labels, or bonus paragraphs fail. Changed
case, punctuation, season, competition label, number, or result phrase fails.
Never deduplicate an ambiguity.

Help navigation, archived-season navigation, and initialization scripts outside
the accepted sections are nonsemantic chrome and may be ignored. Any otherwise
unconsumed rule-like `h2`, `p`, `ul`, `li`, or `table` below the semantic root
fails. An additional element within an accepted section fails unless it is one
of the exact structural wrappers above.

### Canonical structured record

The required schema identifier is `schadensfresse-live-rules-v1`. No field is
optional and no additional property is allowed. Object properties and arrays
use the order declared here.

The root fields are:

| Order | Field | JSON type | Exact constraint |
| ---: | --- | --- | --- |
| 0 | `schemaVersion` | string | `schadensfresse-live-rules-v1` |
| 1 | `tipsVisibleBeforeDeadline` | boolean | `false` |
| 2 | `predictionMode` | string | `exact-score` |
| 3 | `resultBases` | array | Exactly three `ResultBasis` objects in the canonical order below |
| 4 | `tieBreak` | string | `matchday-wins-unless-otherwise-agreed` |
| 5 | `leadTimeMinutes` | number | Int32 `0` |
| 6 | `matchScoring` | object | Exact `MatchScoring` schema below |
| 7 | `bonusScoring` | object | Exact `BonusScoring` schema below |

Each `ResultBasis` contains `subcompetition`, `sourceLabel`, and `resultBasis`,
all strings in that property order. The canonical array order deliberately
differs from the observed list order and is:

1. `bundesliga`, `1. Bundesliga 2026/27`, `regularTime90Minutes`;
2. `dfb-pokal`, `DFB-Pokal 2026/27`,
   `finalScoreIncludingExtraTimeAndPenaltyShootout`; and
3. `uefa-champions-league`, `Champions League 2026/27`,
   `finalScoreIncludingExtraTimeAndPenaltyShootout`.

`MatchScoring` contains `win` then `draw`, both required `Score` objects. Each
`Score` contains `tendencyPoints` as Int32, `goalDifferencePoints` as nullable
Int32, and `exactResultPoints` as Int32, in that order. The exact win values are
`2`, `3`, `5`; the exact draw values are `3`, `null`, `5`.

`BonusScoring` contains `pointsPerCorrectAnswer` as Int32 `9` and
`answerOrderMatters` as boolean `false`, in that order.

### Canonical bytes and hashes

Use .NET 10 `System.Text.Json`. Every property has an explicit
`JsonPropertyName` and `JsonPropertyOrder`. Configure `WriteIndented = false`,
`Encoder = JavaScriptEncoder.Default`, `PropertyNamingPolicy = null`,
`DictionaryKeyPolicy = null`, `DefaultIgnoreCondition =
JsonIgnoreCondition.Never`, and `NumberHandling = JsonNumberHandling.Strict`.
Serialize with `JsonSerializer.SerializeToUtf8Bytes`.

The result is UTF-8 without a BOM, leading or trailing whitespace, or terminal
CR/LF. Array and property order are significant. SHA-256 covers exactly those
bytes and is formatted as lowercase hexadecimal. Canonical validation rejects
unmapped properties, missing fields, wrong JSON types, null required values,
noncanonical enum values, wrong property or array order, and any byte-for-byte
reserialization mismatch.

The exact canonical JSON is this single line:

```json
{"schemaVersion":"schadensfresse-live-rules-v1","tipsVisibleBeforeDeadline":false,"predictionMode":"exact-score","resultBases":[{"subcompetition":"bundesliga","sourceLabel":"1. Bundesliga 2026/27","resultBasis":"regularTime90Minutes"},{"subcompetition":"dfb-pokal","sourceLabel":"DFB-Pokal 2026/27","resultBasis":"finalScoreIncludingExtraTimeAndPenaltyShootout"},{"subcompetition":"uefa-champions-league","sourceLabel":"Champions League 2026/27","resultBasis":"finalScoreIncludingExtraTimeAndPenaltyShootout"}],"tieBreak":"matchday-wins-unless-otherwise-agreed","leadTimeMinutes":0,"matchScoring":{"win":{"tendencyPoints":2,"goalDifferencePoints":3,"exactResultPoints":5},"draw":{"tendencyPoints":3,"goalDifferencePoints":null,"exactResultPoints":5}},"bonusScoring":{"pointsPerCorrectAnswer":9,"answerOrderMatters":false}}
```

Its exact length is 822 bytes and its SHA-256 is
`1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90`.
That schema identifier plus hash is the sole semantic rules identity for new
P1-10 publication, freshness, provenance, reuse, and activation checks.

For extraction diagnostics, serialize the normalized scoring matrix as the
ordered `string[3][4]` below with the same minified default
`System.Text.Json`, UTF-8, no-BOM, and no-newline rules:

```json
[["","Tendenz","Tordifferenz","Ergebnis"],["Sieg","2","3","5"],["Unentschieden","3","-","5"]]
```

Its SHA-256 is
`4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e`.
Record it in captured-fixture tests and payload-safe preflight diagnostics. It
does not replace the structured record, does not independently prove the other
rules, and is not a semantic freshness or publication identity.

### Markdown publication and immutable provenance

`community-rules/schadensfresse.md` remains the sole repository source for
published document `community-rules-schadensfresse.md`. Before publication, a
rules-specific validator must extract every semantic claim from that markdown
into `schadensfresse-live-rules-v1` and require byte-identical canonical JSON
and hash equality with the authenticated live record. The markdown projection
must bind all eight root fields, all three ordered result bases and source
labels, both complete score rows including the explicit draw null, and both
bonus values. Examples or explanatory prose must not contradict or provide an
alternative score, result basis, lead time, visibility, tie-break, or bonus
meaning. Missing, duplicate, extra, ambiguous, or unparsable semantic claims
fail publication.

After semantic equality passes, hash the exact checked-in markdown bytes with
SHA-256. The deterministic routing seed/config pins that lowercase content
hash. Atomic context publication preserves those exact bytes, produces an
immutable document version, and records the same content hash. Readback must
match document name, immutable version, and content hash; neither a floating
latest document nor prose reinterpretation can satisfy the gate. The current
checked-in schadensfresse file with `2/3/4` win and `2/-/4` draw scoring is
known invalid and cannot be published under this contract.

For new `resolvedTypedContextManifest` values, replace ADR-0058's
`normalizedRulesSha256` field with adjacent ordered fields
`rulesSchemaVersion` and `canonicalRulesSha256`. They must equal
`schadensfresse-live-rules-v1` and
`1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90`.
The sole document's existing `contentSha256` continues to bind the immutable
markdown bytes; it is a different identity and must not be populated with the
canonical JSON hash. The remaining ADR-0058 manifest fields and their order
remain unchanged.

Before each rules-only publication and every manual DFB/CL production
generation, reconstruct the live structured record. At the instant the gate is
evaluated, `rulesObservedAt` must not be in the future and its age must be at
most 24 hours. Require equality among the live schema/hash, the seed's expected
schema/hash, the markdown projection, and the published manifest's schema/hash,
then require the seed/file/publication immutable markdown content hashes and
version to agree. Any missing value, drift, stale observation, publication
failure, or hash/version mismatch fails before prompt fetch or model-service
construction. A future recurring primary schedule must automate the same
checks; this decision grants no unattended-network or activation authority.

### Legacy treatment

Preserve
`b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
and its exact historical reconstruction below as observation evidence and a
regression fixture:

1. Select `tr, li, dt, dd, p, h1, h2, h3, h4` across the authenticated
   document in DOM order.
2. For each `TextContent`, return empty for null/blank; otherwise call `.Trim()`
   and replace each .NET regex `\s+` match with one ASCII space. Do not apply
   Unicode normalization in this historical path.
3. Retain strings of length `1..1000` containing at least one keyword under
   ordinal-ignore-case comparison. The exact keyword-list order is `sichtbar`,
   `tippabgabe`, `tippzeit`, `vorlauf`, `tendenz`, `tordifferenz`, `exakt`,
   `ergebnis`, `bonus`, `bundesliga`, `dfb`, `champions`, `elfmeter`,
   `90 minuten`, `spieltagssieg`, `punkte`.
4. Apply `Distinct(StringComparer.Ordinal)`, preserving first-occurrence DOM
   order. Serialize the resulting `string[]` with the .NET 10
   `JsonSerializer.Serialize(value)` default overload as minified JSON using
   `JavaScriptEncoder.Default`, then hash its UTF-8 bytes without BOM,
   surrounding whitespace, or terminal line ending.

The exact historical JSON is:

```json
["Sichtbarkeit der Tipps","Die Tipps sind erst sichtbar, wenn die Tippzeit abgelaufen ist.","Es wird das genaue Ergebnis getippt.","Es wird das jeweils folgende Ergebnis gewertet:","DFB-Pokal 2026/27: nach Elfmeterschie\u00DFen","Champions League 2026/27: nach Elfmeterschie\u00DFen","1. Bundesliga 2026/27: 90 Minuten","Punktegleichstand","Soweit nicht etwas anderes vereinbart wurde, entscheidet bei Gleichstand in der Gesamtpunktzahl die Anzahl der Spieltagssiege (\u0022Siege\u0022) \u00FCber die Platzierung der Tipper.","Tippabgaberegel: 0 Minuten Vorlaufzeit","Die Tippzeit endet 0 Minuten vor dem Termin des jeweiligen Ereignisses.","Punkteregel: 2 - 5 Punkte","TendenzTordifferenzErgebnis","Punkteregel: 9 Punkte","Punkte pro richtiger Antwort: 9","Punkte gibt es f\u00FCr jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung."]
```

That historical value never satisfies a new semantic, publication, freshness,
provenance, reuse, or activation gate. Do not rewrite it to mean the structured
record.

The authenticated raw-HTML hash
`f788efe448ce538d530baf74ce66f5ef03a61faab5a527d965dcd8d314d2e9c0`
also remains observation evidence, not a semantic identity. A legacy manifest
containing only `normalizedRulesSha256`, any record without the v1 schema, or a
record with the historical/table hash in `canonicalRulesSha256` is preserved
for audit but cannot be reused as current.

### Required implementation and tests

P1-10 must implement this contract in the narrowest applicable surfaces:

- an AngleSharp authenticated live-rules extractor and typed v1 record in
  `src/ContextProviders.Kicktipp` or the shared domain surface it requires;
- a deterministic canonical serializer, markdown semantic validator, and
  rules publication/preflight orchestration in `src/Core` and
  `src/Orchestrator`;
- the corrected `community-rules/schadensfresse.md`, its pinned routing-seed
  content hash, and the successor resolved-manifest fields;
- persistence/readback support where the resolved typed context manifest is
  stored; and
- captured-fixture, canonical-byte, provider, publication, manifest,
  freshness, command, and workflow-contract tests in the corresponding Core,
  ContextProviders.Kicktipp, Orchestrator, and FirebaseAdapter test projects.

Tests must cover the exact captured page producing the legacy, table, and
structured hashes; the 822-byte canonical payload, explicit null, encoding,
field/array order, BOM/newline absence, and round-trip rejection; every
missing, duplicate, extra, reordered, nested, wrong-tag, wrong-label,
wrong-case, punctuation, season, numeric-format, score, result-basis,
visibility, tie-break, lead-time, and bonus drift case; Unicode NFC and
whitespace equivalence; non-`200` status and login-form/title rejection;
missing final `RequestUri`; a final URI with a non-HTTPS scheme; nonempty final
query; nonempty final fragment; wrong final host or path; a redirect chain that
successfully finishes at the exact allowed target; and redirect chains whose
final target violates any scheme/login/host/path/query/fragment gate;
numeric-row drift changing the table and structured hashes even when the
legacy digest does not; markdown semantic mismatch; immutable
file/publication/version hash mismatch; stale/future observations; and
legacy-manifest rejection.

This ADR unblocks only that validator/publication implementation and its local
tests. ADR-0058's owner-controlled production replacement, prompt promotion,
manual run, and separately reviewed schedule-activation gates remain closed.

## Alternatives considered

- **Keep the legacy normalized hash as the gate:** Rejected because it omits
  the numeric score rows and can silently hide duplicates or long values.
- **Add only the table hash to the legacy hash:** Rejected because two
  untyped digests still require prose reinterpretation and do not bind all
  visibility, mode, result-basis, tie-break, lead-time, scoring, and bonus
  values into one versioned contract.
- **Gate on raw HTML bytes:** Rejected because nonsemantic markup or chrome
  changes would invalidate publication while still providing no typed consumer
  contract.
- **Trust the checked-in markdown content hash alone:** Rejected because
  immutability proves which bytes were published, not that those bytes match
  the authenticated live rules.
- **Accept missing fields from defaults:** Rejected because a default can make
  absent or changed live state appear valid.

## Consequences

- Numeric scoring, the exact source shape, and every other live rule become
  part of one versioned, typed, byte-reproducible identity.
- Cosmetic Unicode/whitespace differences normalize safely, while structural
  ambiguity and semantic drift stop before model construction.
- The markdown document has both a semantic binding to the live record and a
  separate immutable byte/content binding in publication provenance.
- Existing legacy evidence remains reproducible without being mistaken for
  current validation.
- P1-10 gains explicit parser, publication, persistence, and negative-test
  work; no production, prompt, or scheduling authority expands.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)

## Supersedes

- [ADR-0058](0058-make-schadensfresse-a-competition-typed-primary.md), only
  its use of normalized legacy hash
  `b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
  as the semantic publication/freshness identity and its
  `normalizedRulesSha256` successor-manifest field. The hash and raw retrieval
  remain historical evidence; ADR-0058's domain, routing, prompt, context
  allowlist/budget, model, rollout, owner, and activation contracts remain in
  force.
