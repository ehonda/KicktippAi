# Bundesliga 2026/27 Club Elo refresh

The dormant E1 source implements [ADR-0077](../../plans/bundesliga-2026-27/decisions/0077-refresh-club-elo-from-official-html.md)
and [ADR-0081](../../plans/bundesliga-2026-27/decisions/0081-close-club-elo-html-publication-and-selection-seams.md).
Club Elo supplies the ratings. Its only acquisition URL is `https://clubelo.com/GER`.
The implementation makes a GET with no discretionary request headers. Its handler
disables redirects, automatic decompression, and cookies. W1 owns registration
and integration; this change leaves both source flags false.

## Acquisition and evaluation

The source freezes a verified retained selection for every expected consumer
before acquisition. Each production lane reads its own publication evidence,
including all five arena lanes. A missing head uses the valid launch seed;
a headed set must reconstruct as an exact, complete LKG. Corrupt or crossed
evidence fails before acquisition. A shared `NotNewer` result is possible only
when every retained selection is at least as recent as the candidate.

The first failed gate determines the immutable observation:

1. Transport completes, with at most two attempts in a monotonic 25-second
   budget, at most ten seconds per attempt, and one injected five-second delay.
   Only connection failures, timeouts, HTTP 408/429/5xx retry. Caller
   cancellation is rethrown; only the terminal attempt supplies diagnostics.
2. The body limit is 2,097,152 bytes. Advertised oversize stops before a read;
   observing byte 2,097,153 stops streaming without claiming a complete hash.
   Empty and declared-length-mismatch bodies retain their complete raw hash
   and length. Transport failures retain neither.
3. The response must be HTTP 200 at the exact source URL, with zero redirects,
   no Location, `text/html`, exactly one UTF-8 charset, and no content encoding.
4. An inert AngleSharp 1.7.2 parser finds the single sheet, direct Germany
   heading, exact table and adjacent inline script. It never executes scripts,
   navigates, loads resources, or evaluates CSS.
5. A bounded literal lexer accepts only the frozen declaration/call and array
   grammar: 262,144 script characters, 512 rows, four strings per row,
   and 8,192 decoded characters per string. Only the accepted minimal escapes
   decode; malformed scalar values and non-NFC text fail.
6. Each club fragment is checked against the exact leaf-only `td/a/small/a`
   grammar and parsed in a fresh inert `tr` context. Structural repair,
   duplicate/event/unknown attributes, active descendants and unsafe routes
   fail. Source ranks strictly increase and are unique positive Int32 values;
   Elo is positive Int32 and may tie. Historical CSV fractional Elo evidence
   remains a separate unchanged contract.
7. The same-response heading href `/yyyy-MM-dd/GER` supplies the sole displayed
   date. A missing, invalid, noncanonical or future date fails. No response
   timestamp, observation timestamp or other link substitutes for it.
8. Exact route/display-name pairs map to the checked-in manifest slugs.
   Crossed identities and duplicate mapped clubs fail; extra German clubs are
   ignored. Projection is ordinal slug ordered and must cover all 18 clubs.
9. The candidate must be no more than seven calendar days old, then strictly
   newer than at least one retained consumer selection.

Only an eligible observation contains `club-elo/source.html`. Every rejection
has no payload and preserves only the evidence permitted at its failed gate.
The mapping is [club-elo-name-map.csv](../../data/bundesliga-2026-27/club-elo-name-map.csv):
487 UTF-8 NFC bytes without BOM, CRLF between records, no final terminator,
SHA-256 `8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7`.

## Consumption and persistence

The command consumes the prepared immutable bundle and revalidates the actual
lane's headed publication. Eligible candidates advance lagging heads
independently; equal/newer heads remain LKG. A contradictory shared `NotNewer`
fails before mutation. New HTML payloads use C3's verified source-backed v2
builder and guarded atomic publication. Seed fallbacks retain v1 provenance;
existing LKG metadata, creation time and predecessor are not rewritten.

Every source-aware committed outcome completes through
`CompletePreparedReceiptAsync`. Retaining a verified head records
`NotAttempted`. Guarded `Published`, `Unchanged` and `Reactivated` receipts are
completed through exact replay after their atomic write. Persisted receipts
are replayed before any selection/read/write; dry runs never write receipts.
Without a prepared Club Elo observation, the legacy command resolves no source
cycle, HTTP, artifact or receipt service.

## Evidence and remaining gates

The six checked-in fixtures are synthetic mechanics evidence: eligible,
unknown source date, partial coverage, hostile DOM, hostile lexer and hostile
fragment. The automated tests also mutate these fixtures to prove bounds,
response predicates, gate order and independent selection. Local Firestore
tests use the Docker emulator.

This implementation performs no live acquisition and establishes no accepting
real-world capture recipe or reuse permission. Owner gates for live reads,
development persistence, unattended reuse, GitHub mutation, production writes,
activation and restoration remain separate. Workflow, schedule, model, prompt,
credential and production recovery routing are unchanged.
