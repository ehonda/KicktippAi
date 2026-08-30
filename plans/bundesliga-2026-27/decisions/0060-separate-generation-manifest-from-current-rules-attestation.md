# ADR-0060: Separate generation provenance from current rules attestation

- Status: Accepted
- Date: 2026-08-30
- Decision authority: Project Owner authorized evidence-backed necessary and
  sanctioned decisions on 2026-08-30

## Context

ADR-0058 introduced the `resolvedTypedContextManifest` for schadensfresse's
rules-only DFB-Pokal and Champions-League routes. ADR-0059 then replaced its
legacy rules digest with a typed schema/hash pair and required
`rulesObservedAt` to be no more than 24 hours old. The generation manifest is
prediction provenance: it records what was resolved when a prediction was
created. Rewriting that timestamp merely to prove that unchanged live rules
were checked again would make historical provenance mutable.

The opposite interpretation also fails. If reuse considers only the old
generation timestamp, an otherwise identical immutable prediction becomes
unusable after 24 hours and forces another production generation solely to
refresh evidence. That would consume cost and reprediction capacity without a
semantic, document, prompt, model, fixture, or question change. A durable
lifecycle contract must preserve the original generation observation while
allowing a new authenticated observation to attest the same exact immutable
publication.

ADR-0058 and ADR-0059 do not fix the canonical timestamp text, the logical
identity of a refreshable publication attestation, or the exact separation
between generation provenance and current reuse evidence. This decision
closes only those gaps. The exact subcompetition field remains
`bundesligaSeasonSubcompetition`; there is no shortened or generic replacement.

## Decision

### Canonical timestamp contract

Every new `rulesObservedAt` value in a resolved typed context manifest or
publication binding is a required JSON string in exact invariant Gregorian UTC
format `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`. It is exactly 28 ASCII characters:
four-digit year, two-digit month/day/hour/minute/second, exactly seven
fractional-second digits, and an uppercase literal `Z`. The seven digits retain
.NET tick precision, including trailing zeroes.

Before serialization, a programmatic `DateTimeOffset` observation is converted
with `ToUniversalTime()` and formatted with that exact pattern and invariant
culture. Parsing an existing JSON value is stricter: accept only the exact UTC
textual form. Reject leading or trailing whitespace, a space instead of `T`,
lowercase `z`, any numeric offset including `+00:00`, a missing or variable-
length fraction, more than seven fraction digits, non-ASCII digits, an invalid
calendar date or time, a leap-second value, and any value whose parse followed
by exact reformatting is not byte-for-byte equal to the input. No permissive
RFC3339 or platform-default parser may precede this check.

Let `E` be the UTC evaluation instant and `O` the parsed UTC observation,
compared at 100-nanosecond tick precision. Freshness is valid exactly when
`O <= E` and `E - O <= 24 hours`. Therefore an observation equal to `E` and
one exactly 24 hours old are valid; one tick in the future or one tick older
than 24 hours is invalid. `E` is captured once for the gate so separate checks
cannot cross the boundary inconsistently.

At generation/publication time, the observation written into both new records
must satisfy that gate. On later prediction reuse, the binding observation is
the current freshness evidence and must satisfy it; the immutable generation
observation may then be older than 24 hours because it is historical
provenance. Neither observation may be reinterpreted or normalized while
parsing.

### Immutable generation manifest

The prediction's generation-time `resolvedTypedContextManifest` remains
immutable. ADR-0059's successor root schema is retained with this exact
property order and JSON types; all properties are required and additional
properties are rejected:

| Order | Property | JSON type | Constraint |
| ---: | --- | --- | --- |
| 0 | `seasonPartition` | string | Exact configured partition; P1-10 requires `bundesliga-2026-27` |
| 1 | `communityContext` | string | Exact configured community; P1-10 requires `schadensfresse` |
| 2 | `bundesligaSeasonSubcompetition` | string | One exact ADR-0058 serialized value and valid only in the Bundesliga season partition |
| 3 | `profileId` | string | One exact ADR-0058 rules-only profile ID |
| 4 | `routingSeedSha256` | string | Exactly 64 lowercase hexadecimal characters |
| 5 | `rulesObservedAt` | string | Exact canonical timestamp above; this is the generation observation |
| 6 | `rulesSchemaVersion` | string | `schadensfresse-live-rules-v1` |
| 7 | `canonicalRulesSha256` | string | `1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90` |
| 8 | `documents` | array | Exactly one `Document` object |

The sole `Document` object has required properties in exact order
`kind`, `name`, `version`, `contentSha256`. `kind`, `name`, and
`contentSha256` are JSON strings; `version` is a JSON number that parses
strictly as a nonnegative Int32. `kind` is `Context`, `name` is
`community-rules-schadensfresse.md`, `version` is the exact immutable
publication version, and `contentSha256` is exactly 64 lowercase hexadecimal
characters binding the published Markdown bytes.
Null, missing, duplicate, reordered, wrong-typed, unknown, or extra properties;
an empty string; an extra document; an uppercase or malformed hash; and a
byte-for-byte canonical reserialization mismatch all fail current validation.
The canonical System.Text.Json settings and UTF-8/no-BOM/no-whitespace rules
from ADR-0059 apply.

Saving, reusing, verifying, or re-attesting a prediction never replaces this
manifest or its `rulesObservedAt`. The generation manifest remains the answer
to “what did this prediction use when it was generated?”

### Dedicated current publication binding

Current reuse evidence is a separate
`resolvedTypedContextPublicationBinding`. Its exact logical key, in tuple
order, is
`(seasonPartition, communityContext, profileId, routingSeedSha256)`. Persistence
must address that complete tuple directly. A floating `latest`, collection
enumeration, prefix query, partial key, newest-timestamp selection, or fallback
to another community/profile/seed is forbidden.

The physical storage key must preserve that ordered tuple injectively. Encode
the four values as a canonical minified JSON string array in the order above,
using ADR-0059's System.Text.Json UTF-8/no-BOM/no-whitespace settings, then use
the unpadded base64url encoding of those complete bytes as the document ID.
Base64url replaces `+` with `-` and `/` with `_` and removes only terminal `=`
padding; do not hash, truncate, reorder, case-fold, or otherwise normalize the
array or encoded ID. Decoding must reproduce the same canonical array and all
four stored key fields. Unframed concatenation with a separator is forbidden
because component values could alias a different tuple.

The binding has this exact ordered root schema. Every property is required,
no additional property is allowed, and the strict string/hash/timestamp and
canonical-byte rules above apply:

| Order | Property | JSON type | Constraint |
| ---: | --- | --- | --- |
| 0 | `seasonPartition` | string | First logical-key component |
| 1 | `communityContext` | string | Second logical-key component |
| 2 | `profileId` | string | Third logical-key component |
| 3 | `routingSeedSha256` | string | Fourth logical-key component; lowercase SHA-256 |
| 4 | `bundesligaSeasonSubcompetition` | string | Exact ADR-0058 value implied by the profile and seed |
| 5 | `rulesObservedAt` | string | Current authenticated rules observation |
| 6 | `rulesSchemaVersion` | string | `schadensfresse-live-rules-v1` |
| 7 | `canonicalRulesSha256` | string | ADR-0059 canonical v1 hash |
| 8 | `document` | object | Exactly one immutable `Document` object using the manifest schema above |

The key fields in the stored value must equal the addressed logical key. The
subcompetition must equal the seed/profile mapping. The singular document
must equal the seed/file/read-back publication by all four fields, including
exact immutable version and content hash. The rules schema and hash must equal
the authenticated live record, routing seed, and Markdown projection.
`contentSha256` remains the Markdown byte hash and is never populated with the
canonical rules JSON hash.

A successful authenticated re-attestation supplies one canonical candidate
binding. Its transaction has these deterministic result semantics:

1. If the exact key is absent, create the candidate and return it as the
   effective binding.
2. If the stored binding is identical in every field except
   `rulesObservedAt`, and the candidate observation is strictly newer, update
   only `rulesObservedAt` and return the updated effective binding.
3. If those identity fields are identical and the candidate observation is
   equal or older, perform no write and return the current stored effective
   binding as success.
4. If any non-observation field differs, fail closed without a write.

The datastore transaction retries the complete read/compare/write function on
a transaction conflict. The operation returns the binding selected by the
committed attempt, or directly rereads and validates that committed effective
binding by the exact key. It never returns an uncommitted candidate or a
candidate that lost a retry. Thus two equal concurrent creators converge on
one identical binding; concurrent older/newer candidates converge on the newer
observation regardless of scheduling; and a drifted candidate never replaces
the committed identity.

For a fixed initial binding and completed set of identity-equal candidates,
the final exact-key value has the greatest observation across the initial
committed binding, when present, and every accepted candidate, independent of
interleaving. Each operation returns the effective value at its own committed
attempt or immediate exact-key readback; a later operation may legitimately
advance the binding after an earlier caller has returned.

Publishing changed bytes or changing a routing seed creates a new immutable
publication and/or a different exact key; it cannot be disguised as an
attestation refresh. Publication re-attestation validates only the binding-
scoped season, community, profile, routing seed, subcompetition, rules, and
immutable-document identities. It does not inspect or attest a prediction's
typed input, prompt, or model configuration; those belong to the separate
per-prediction reuse decision below. Prompt and model fields remain outside
the binding and are rejected there as additional properties.

### Re-attestation and reuse

A rules-only typed prediction may be reused after a fresh authenticated
re-attestation with zero model call and zero prediction mutation only when all
of the following are true at one evaluation instant:

1. the prediction has a canonical generation manifest and complete ADR-0058
   typed fixture or question identity;
2. direct lookup by the exact four-part key returns exactly one canonical
   publication binding whose observation satisfies the inclusive freshness
   rule;
3. the current live schema/hash, routing-seed schema/hash, Markdown semantic
   projection, binding schema/hash, and generation-manifest schema/hash are
   identical;
4. the seed/file/publication/binding/generation document kind, name, immutable
   version, and content hash are identical, and exact-version readback returns
   those bytes;
5. the current typed invocation equals the prediction's persisted generation
   identity for season partition, community context,
   `bundesligaSeasonSubcompetition`, profile, routing seed, stable fixture or
   question identity and every bound field, and round/result basis when
   applicable;
6. the current invocation's exact pinned prompt route equals the prediction's
   immutable persisted prompt provenance: hosted prompt name, immutable
   version, normalized hash checked against the read-back prompt, and required
   promotion-label membership where the accepted route requires it; no
   floating label or version lookup satisfies this comparison;
7. the current invocation configuration equals the prediction's immutable
   persisted model provenance for exact model ID, reasoning effort, output
   token cap, and Flex-first with one Standard-fallback service policy; and
8. ordinary prediction completeness, cutoff, posting, verification, and
   reprediction rules independently permit reuse.

The binding refresh and per-prediction reuse check are separate operations.
Refreshing the binding proves only current rules/profile/seed/document
identity. Reuse then compares the current typed invocation, resolved pinned
prompt route, and exact model/service configuration with the immutable
prediction provenance. It selects no new prompt version, hash, label,
promotion, model, or model configuration; it only verifies identities required
by ADR-0058/0059 and the applicable accepted prompt/model decisions.

Trace and verification output expose the two observations distinctly as
`generationRulesObservedAt` from the immutable prediction manifest and
`currentRulesObservedAt` from the exact publication binding. They must never
collapse either value into a single ambiguous `rulesObservedAt`. Payload-safe
output may include the exact binding key, schema/hash, document identity, and
the reuse result, but not prediction contents, option selections, prompt
bodies, context bodies, or secrets.

Stage the reuse gate according to what can be known. Typed prediction/input
identity, routing and configured model/service identity, exact publication
readback, binding freshness, and rules/profile/seed/document drift checks all
fail closed before any prompt fetch. Only after those checks pass, fetch the
exact pinned prompt version and verify its hosted name, immutable version,
normalized read-back hash, and required promotion-label membership. Any prompt
mismatch fails before model-service construction. Prompt retrieval is not a
model call; no model service is constructed and no model call occurs anywhere
on either failure path or on successful reuse.

Missing, duplicate, malformed, legacy, stale, future, or noncanonical data;
lookup ambiguity; any identity or byte mismatch; a changed document, rules
record, route, prompt, model, fixture, or question; or inability to read the
exact immutable publication fails at its applicable stage above.
Re-attestation never authorizes generating or repredicting. Any new or
repredicted production row remains behind ADR-0058's Owner-approved exact
replacement set, maximum added calls/cost, force/reprediction limits, and UTC
cutoff.

### Legacy treatment and tests

Preserve legacy predictions, manifests, and publication records for display,
audit, and dry-run inventory. They cannot serve as current generation
provenance or a current publication binding. This includes a manifest with
only `normalizedRulesSha256`, a missing or noncanonical timestamp, a timestamp
with an offset or variable fraction, the historical or table hash in
`canonicalRulesSha256`, a multi-document or floating-latest record, and any
record lacking the exact four-part binding key. No migration, deletion, or
in-place repair is implied.

Core and Firebase tests must cover canonical field order/types and exact
round-trip bytes for both schemas; timestamp normalization on serialization;
every rejected timestamp shape and invalid calendar value; the exact-now and
exactly-24-hours-old accepted boundaries; future-by-one-tick and stale-by-one-
tick rejection; direct exact-key reads; absent, duplicate, prefix, latest, and
cross-key rejection; injective canonical physical-key round trips and
separation of tuples that would collide under unframed concatenation;
immutable single-document/version/hash equality; generation-manifest
immutability; distinct trace/verification observations; and publication
refresh that never inspects prediction/prompt/model fields.

Transaction tests must cover absent create, strictly newer update, equal and
older no-op success, identity drift failure, and returned committed effective
values. Concurrency tests must cover two equal creators, older/newer candidates
in both commit interleavings with the same schedule-independent final binding,
each call's committed effective result, and no return of a candidate that lost
a retry. They must assert that the final observation is the greatest across a
present initial binding and every accepted candidate. Drift conflicts must
never merge or overwrite identities.
Per-prediction tests must separately cover equality and
drift for every typed-input field, hosted prompt name/version/read-back
normalized hash/required label membership, model ID, reasoning effort, output
cap, and Flex/Standard policy; zero-call/zero-mutation reuse; and legacy audit
visibility with current-use rejection.

This decision authorizes only the manifest/binding implementation and local
tests. It does not authorize a prompt version/hash/promotion, fixture seed,
production run or write, copied-row replacement, model call, schedule
activation, or new model/configuration.

## Alternatives considered

- **Rewrite prediction provenance in place:** Rejected because changing the
  generation observation would erase which evidence existed when the model
  produced the prediction and make an immutable provenance record mutable.
- **Always regenerate after 24 hours:** Rejected because unchanged rules,
  documents, prompts, model configuration, and typed input can be safely
  re-attested without spending a model call or consuming reprediction capacity.
- **Accept variable RFC3339 timestamps:** Rejected because offsets, optional
  fractions, and permissive parser normalization produce multiple byte forms
  for the same instant and weaken canonical persistence and tests.
- **Resolve the newest or floating-latest publication:** Rejected because
  enumeration and recency cannot prove the prediction's exact profile, seed,
  immutable version, or bytes and can silently cross an identity boundary.

## Consequences

- Historical generation provenance stays immutable while current rules
  freshness can advance independently.
- An unchanged prediction can remain reusable beyond its original 24-hour
  observation with zero model cost and no prediction write.
- Persistence gains one directly addressed, refreshable attestation record;
  only its current observation may change under the same exact identity.
- Canonical timestamps and inclusive boundary semantics remove platform and
  parser ambiguity.
- Any semantic, document, route, prompt, model, fixture, or question drift
  continues to fail closed and cannot be converted into a refresh.
- Legacy records remain auditable but are not treated as current.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)

## Supersedes

None. This decision clarifies the lifecycle of ADR-0058's generation manifest
and ADR-0059's structured rules freshness contract without reopening their
accepted field names, identities, hashes, routing, publication, model, rollout,
or activation decisions.
