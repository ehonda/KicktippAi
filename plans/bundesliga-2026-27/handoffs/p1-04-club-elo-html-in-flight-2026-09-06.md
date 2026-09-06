# P1-04 official Club Elo HTML in-flight handoff

- Status: Source selected; specification reviewed but not yet accepted
- Date: 2026-09-06
- Orchestration run: `01a07449-de77-7ae0-ac4a-8f5330c43121`
- Task: [P1-04](../tasks/p1-04-club-elo-refresh.md)
- Shared authority: [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md)
- Current integration head: `204cfd8db2c4163ca320a7c8a1829ebbc2ee1ed2` on clean, pushed `main`

## Resume boundary

The Owner selected the official `https://clubelo.com/GER` HTML page as the
successor to the unavailable direct CSV. No ADR-0075 or runtime implementation
has been created. The independent specification reviewer returned `BLOCK` on
two remaining corrections after accepting the rest of the evidence and
contract direction.

In the next explicit orchestration session:

1. Reconstruct the exact run from
   `.tmp/orchestration/01a07449-de77-7ae0-ac4a-8f5330c43121/` if it is still
   present, then re-verify live Git, worktree, resource, and source state.
2. Correct the two specification blockers below with a fresh architecture
   owner under the improved orchestration protocol.
3. Obtain a different independent specification review.
4. Only after approval, create Accepted ADR-0075, update the shared design,
   packet, task, plan index, execution strategy, and Bundesliga onboarding
   profile, and materially re-freeze the P1-04 graph.
5. Admit a dormant implementation writer only from that tracked freeze. Do not
   enable a live GET, development persistence, or production refresh merely
   because the source was selected.

P1-05 does not depend on this work and must remain on its direct ADR-0074
branch.

## Exact retained evidence

- Two official HTML captures were byte-identical eleven minutes apart.
- Body length: `562,238` bytes.
- Body SHA-256:
  `a342b6f83dadbb49599c0fe6364ea3288f0953381d5b35e923eb87a03296aa59`.
- Response: `text/html; charset=utf-8`, no content coding and no BOM.
- Provider-displayed rating date: `2026-09-04`, bound in the same response by
  the exact heading link `/2026-09-04/GER` with text `Germany`.
- The page contains the expected 18 manifest clubs with their exact current
  ranks and Elo values.
- Proposed mapping bytes: 475-byte UTF-8/NFC CSV, SHA-256
  `ee14dfc556c03157ca215eb3c3161d2119ea7b59d8666084915376dde5213772`.
  Re-verify the exact bytes at implementation time.
- Ignored evidence, when retained locally:
  `.tmp/p1-04-evidence-01a07449/REPORT.md` and
  `.tmp/p1-04-evidence-01a07449/clubelo-GER.html`.

The unsuccessful direct-CSV investigation remains useful negative evidence:
current and historical HTTP requests returned empty 502 responses, HTTPS
timed out, and public archives/caches yielded no date-bound raw CSV. Do not
repeat that broad search unless the provider surface changes.

## Reviewed successor direction

This direction passed the last review except for the exact blockers below; it
is not yet an accepted ADR.

- Request exactly `https://clubelo.com/GER` with redirects disabled. Accept
  only status 200, exact final URL, no `Location`, `text/html`, exactly one
  UTF-8 charset, no content encoding, and a complete body of 1 through
  2,097,152 bytes. Hash raw entity bytes before UTF-8 or HTML parsing.
- The descriptor successor is
  `club-elo-official-html-descriptor/v1`. Its root order is `contract`,
  `sourceUrl`, `response`, `rawSha256`, `rawByteLength`, `parserContract`,
  `displayedDate`, `providerDateEvidence`, `tableContract`, `tableHeader`,
  `nameMappingContract`, `nameMappingSha256`, `sourceRows`, `evaluation`.
  The nested response order is `statusCode`, `finalUrl`, `redirectCount`,
  `redirectLocation`, `mediaType`, `charset`, `contentEncodings`,
  `declaredContentLength`. Nullable values are explicit null; native integer
  types and the raw hash/length and displayed-date/evidence pairing are strict.
- Only `Eligible` retains `club-elo/source.html`. Partial bodies and rejected
  attempts never enter a bundle. A complete empty or declared/actual-length
  mismatch retains its raw pair only for `SizeRejected`; failed/incomplete
  reads discard response and raw facts as `TransportRejected`.
- Parse with inert AngleSharp 1.7.2: no scripting, browsing context, navigation,
  resource loading, CSS evaluation, or subresource fetch. Require one
  `div.blatt`; its direct, exact Germany `h1` link; one `table#eloTable` inside
  a direct-child wrapper; exact `thead`/empty `tbody` structure and headers
  `Club`, `Elo`, `+/-`, `Golo`; and the adjacent inline script.
- Use a bounded JS-literal lexer, not a JS engine: one
  `const eloData = <array>;`, one `JSSortableEloTable(eloData);`, no other
  `eloData` identifier, at most 262,144 characters, 512 rows, four strings per
  row, and 8,192 characters per string. Accept only brackets, commas, ASCII
  whitespace, single-quoted strings, and the frozen minimal escape set.
- Parse each first-cell fragment in a fresh inert document with a synthetic
  HTML `tr` context. Require exactly one `td` root, exact element children
  `a`, `small`, `a`, exactly two anchors and one small in the subtree, raw
  decoded `href` attributes, first href `/GER`, positive rank, a safe provider
  route, and exact NFC display text. Reject event attributes, duplicate
  attributes, active/unknown descendants, and structural repair outside the
  frozen grammar.
- Provider rank order is strictly increasing; ranks and routes are globally
  unique; Elo values are positive integers and may tie. Map the exact route
  plus display-name pair to exactly 18 manifest slugs and emit rows in ordinal
  slug order. Unknown extra German clubs are allowed; ambiguous or crossed
  route/name identities reject.
- Retry only connection failure, internal timeout, 408, 429, and 5xx. One
  observation has at most two HTTP attempts inside a monotonic 25-second
  budget, each at most ten seconds, with one exact five-second injected delay,
  no jitter and no `Retry-After`. Caller cancellation is rethrown. The final
  descriptor describes only the terminal attempt.
- The accepted authorization ladder is: dormant fixtures/mock tests first;
  separately authorized bounded read-only live evidence; separately authorized
  development network plus persistence; then separately reviewed production
  activation. Source flags remain false and the disabled path performs zero
  source/cycle/persistence work.

## Remaining specification blockers

### 1. Expand the common amendment truthfully

The prior proposal claimed only four common bundle/handoff paths would change.
Current ADR-0074 code still embeds the retired CSV descriptor:

- `src/Core/BundesligaContextSourceHealth.cs` reads `providerRatedAt`; the HTML
  descriptor exposes `displayedDate`.
- `src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs` freezes the old
  descriptor and row shapes and cannot persist/reconstruct nested `response`
  or the `providerRoute`/`providerDisplayName` rows.

The corrected P1-04 common amendment and cumulative review surface must include
at least these paths in addition to the already listed bundle/handoff paths:

```text
src/Core/BundesligaContextSourceHealth.cs
src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs
tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs
tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs
tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs
```

The Firebase amendment must preserve exact nested field order and reject
Firestore doubles for integer response status, redirect count, declared/raw
length, rank, and Elo. Receipt/health schemas and P1-05 semantics may remain
unchanged, but the eventual exact amended tip needs a fresh cumulative review
against both ADR-0074 and ADR-0075 plus P1-05 regression coverage.

### 2. Remove diagnostic-name ambiguity

The proposed table already listed final codes such as
`CLUB_ELO_CONNECTION_FAILED`, but a trailing instruction also said to prefix
every suffix with `CLUB_ELO_`. Delete that instruction or list only unprefixed
suffixes. ADR-0075 must carry one unambiguous list of final exact codes.

## Activation and completion gates

Owner selection of HTML is not unattended network/reuse approval. Before any
live step, preserve the staged authority in the reviewed direction. Production
activation additionally requires scheduled acquisition authority, production
cycle/health/context writes, GitHub bundle and issue permissions, rollback
ownership, and restoration criteria. P1-04 is not complete until an authorized
real accepted refresh and a later distinct no-change cycle are observed with
all eight receipts, independent heads, reconciled health/issue state, copy
compatibility, and no model/post work.
