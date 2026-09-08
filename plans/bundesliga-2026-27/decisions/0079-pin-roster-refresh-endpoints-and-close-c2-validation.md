# ADR-0079: Pin roster-refresh endpoints and close C2 validation

- Status: Accepted
- Date: 2026-09-08

## Context

ADR-0078 defines how C2 evaluates roster metadata, but no provider-published
metadata authority currently exists. dcaribou has made no commitment to publish
the proposed sidecar and remains only the intended provider; the absent sidecar
is not an authority or provider-adoption claim and does not authorize an
artifact request. This ADR fixes the future consumer acceptance contract only.

## Decision

P1-05 remains dormant and rejection-only until dcaribou publishes a conforming
sidecar. There is no alternate provider or provider-adoption claim. The only
ordinal URLs are:

- Artifact: `https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb`
- Sidecar: `https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb.metadata.json`

Fetch the sidecar once before every possible artifact request, with redirects
disabled and its effective URL byte-for-byte equal to the ordinal sidecar URL.
Any absence or `404` is `MetadataUnavailable`: do not request the artifact,
retain seed/LKG, and do not mutate accepted or pending revision state.

### Future sidecar envelope

A future sidecar is one UTF-8, no-BOM JSON object with exactly these members,
in exactly this ordinal order, once each, and no whitespace, extension, null,
duplicate, reordering, coercion, or unknown member:

```json
{"contract":"transfermarkt-duckdb-sidecar/v1","artifactUrl":"https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb","revision":"<lowercase-40-hex>","rawSha256":"<lowercase-64-hex>","rawByteLength":<integer>,"publishedAt":"<UTC-instant>","artifactCaptureDate":"<UTC-date>","membershipEffectiveDate":"<UTC-date>","enrichmentCaptureDate":"<UTC-date>"}
```

`contract`, `artifactUrl`, `revision`, `rawSha256`, `publishedAt`, and all three
date members are JSON strings; `contract` and `artifactUrl` equal the displayed
literals. `revision` is `[0-9a-f]{40}` and `rawSha256` is `[0-9a-f]{64}`. A
missing or invalid `revision` is `MetadataRevisionRejected`; every other
invalid envelope member is `MetadataMalformed`. `rawByteLength` is a JSON integer (not a string,
decimal, exponent, sign, or leading-zero spelling) in
`1..9223372036854775807`; only a length strictly greater than `314572800` is
`SizeRejected`. `publishedAt` is a canonical UTC RFC-3339 instant in
`YYYY-MM-DDTHH:mm:ssZ`; the three dates are canonical valid Gregorian UTC
calendar dates in `YYYY-MM-DD`. They are authoritative source facts, not
transport timestamps or inferred substitutes. Reject every malformed,
noncanonical, null, duplicate, extra, reordered, or semantically invalid
non-revision envelope as `MetadataMalformed`, including look-alike/escaped,
percent-encoded, case-variant, user-info, port, query, fragment, dot-segment,
or redirect URL spellings.

Only after a valid sidecar may R1 request the exact ordinal artifact URL, also
with zero redirects/effective-URL equality. It must verify the advertised raw
length, SHA-256, and revision before selection. If dcaribou ever makes a
conforming publication, its required protocol is artifact first and conforming
sidecar last; this conditional protocol makes no present provider-adoption
claim. A mixed generation fails closed: raw SHA-256, length, and revision must
all agree with the sidecar; otherwise ADR-0078's transport/drift/hash/revision
outcomes apply.

### C2 closure and non-activation

C2 implements ADR-0078's state-aware reason precedence. A
`RemoteIdentityUnavailable` row permits only reasons already provable without
identity and excludes impossible reasons. `RemoteDriftRejected` requires both
raw SHA-256 and raw length. `ClubElo` serializes as `club-elo` and `Rosters` as
`rosters`. `MetadataUnchanged` validates the prior completed receipt and health
before any write. The transitional C1 head-before-receipt inference is exact,
mutation-limited, and ambiguity-fatal.

The frozen C2/R1 checklist covers hostile envelopes and URLs, null/type/range/
canonical/date defects, replay and supersession, every ADR-0078 matrix row,
legacy null guards, guarded CAS precedence, crash recovery, and disabled-source
zero resolution/writes/API calls. S1 acceptance makes C2 writable; it does not
make R1 live or the current source acceptable. Source flags remain false. Live
or development persistence, GitHub/R2 mutation, activation, rollback,
restoration, and completion-evidence gates are unchanged.

## Alternatives considered

- **Use the current DuckDB or a missing sidecar as authority:** Rejected; the
  current source remains rejection-only evidence and has no authoritative
  provider metadata.
- **Adopt an alternate provider or tolerate URL redirects:** Rejected; either
  weakens the pinned authority and artifact identity boundary.

## Consequences

P1-05 has a precise future acceptance interface but no newly authorized network
or persistence action. C2 can persist the safe rejection semantics; R1 stays
dormant until a valid dcaribou sidecar exists and separate owner gates permit
the work.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 design](../designs/p1-04-05-context-refresh.md)

## Supersedes

None.
