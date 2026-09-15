# ADR-0083: Activate official Club Elo context refresh

- Status: Accepted
- Date: 2026-09-16

## Context

PR #111 merged dormant C3/E1 implementation into the baseline. That is prior
implementation evidence, not operational completion: source flags are false,
the current provider HTML differs from the historical parser envelope, no
production GitHub handoff adapter or source-only route exists, and no current
official observation has been accepted into all production contexts.

The owner requires refreshed official Club Elo in context. The scope is one
Club Elo observation, one immutable handoff, eight ordered receipts and four
physical community heads. P1-05/R1 remains the accepted ADR-0082 deferral.

## Decision

### Bounded authority and exclusions

The owner authorizes bounded real Club Elo reads; development and production
Club Elo-context writes; GitHub artifact and health-issue effects; use of the
existing schedule's source inputs after evidence; Pages publication; and a
ready reviewed PR merge. The project owner is recovery owner. This authority
does not include model calls, predictions, posting, a new schedule, changed
credential routing, roster refresh, raw-source redistribution beyond the
verified source artifact/evidence, unrelated P1 work, or ambiguous data repair.

Keep all existing source-cycle identifiers, watermarks, guarded atomic
head/receipt commits, provenance, fallback, health reduction, complete-18
mapping, seven-day freshness, publication reconstruction, production topology,
models, prompts, credentials, posting and copy behavior. Disabled sources must
resolve and call none of the source/provider/coordinator/handoff/GitHub
services.

### Parser v2 and source evidence

New acquisition uses only the paired identities
`club-elo-official-html-parser/v2` and
`club-elo-official-html-table/v2`. Descriptor validation accepts only paired
`(v1,v1)` or `(v2,v2)` identities; v1 remains byte-preserving historical
truth. V2 accepts either the historical bare declaration/call envelope or the
captured opaque helper prefix and exact before-call comment. The prefix is
canonical UTF-8, 1,475 bytes, SHA-256
`045a3ee82a23ed59f88d18feb2047567e81d6945feac8e03c2cc3e2b2c0e5f7c`; the
comment is 39 bytes, SHA-256
`1fa3dee76343dc95f00b82adbd0695fb1d1c7980134b43a81c1da18bd5a126a5`.

The durable v2 helper representation is the following exact canonical UTF-8
text, without BOM or terminal newline. It is recognized as opaque text, never
executed or interpreted:

```javascript
function JSSortableEloTable(data) {
                 const tableBody = document.getElementById('eloTable').getElementsByTagName('tbody')[0];
                 tableBody.innerHTML = ''; // Clear existing rows

                 data.forEach(row => {
                     const newRow = tableBody.insertRow();
                     row.forEach((cell, index) => {
                         const newCell = newRow.insertCell();
                             newCell.className = (index === 0 || index === 4) ? 'l' : 'r';
                         newCell.innerHTML = cell;
                     });
                 });
             }

             function sortTable(columnIndex) {
                 const table = document.getElementById("eloTable");
                 const rows = Array.from(table.rows).slice(1);
                 const isAscending = table.rows[0].cells[columnIndex].classList.toggle('asc');

                 rows.sort((rowA, rowB) => {
                     const cellA = rowA.cells[columnIndex].textContent;
                     const cellB = rowB.cells[columnIndex].textContent;

                     if (!isNaN(cellA) && !isNaN(cellB)) {
                         return isAscending ? cellA - cellB : cellB - cellA;
                     }

                     return isAscending ? cellA.localeCompare(cellB) : cellB.localeCompare(cellA);
                 });

                 rows.forEach(row => table.appendChild(row));
             }

             // Example data
```

The durable exact interstitial text is:

```javascript
// Populate the table with example data
```

For both segments, replace only CRLF with LF and trim only leading/trailing
ASCII space, tab, CR and LF. Do not normalize internal whitespace, isolated CR,
Unicode whitespace, entities or other text. For the captured reproducibility
boundary the selected raw script body has 7,302 UTF-16 characters; `const eloData =`
begins at offset 1,503, the interstitial comment at 7,207, and the final call at
7,260. Those offsets identify this representation only; production accepts by
complete envelope and hashes, never provider-page offsets.

The optional exact prefix precedes only `const eloData = <array>;`; the exact
comment precedes only `JSSortableEloTable(eloData);`; only ASCII whitespace may
remain. The literal parser must return the precise end offset. It permits at
most 262,144 script characters, 512 rows, exactly four single-quoted strings
per row and 8,192 decoded characters per string. It keeps v1's escape set,
scalar/NFC validation and array punctuation rules; trailing commas and
double-quoted JavaScript strings are not accepted. No prefix/suffix search may
choose among candidates. Additional declarations, calls or identifiers, extra
statements, changed helper bodies, executable/unterminated comments, template
or regex literals, expression-valued cells and encoded identifier tricks are
`LexerRejected`. Text resembling identifiers inside one of the four quoted
strings is data only and cannot delimit the script. An envelope change fails
closed and requires a reviewed grammar change.

V2 accepts exactly one whole first-cell fragment alternative, with a raw lexical
proof before parsing in a fresh inert `tr` context and an exact resulting-node
proof after parsing. The legacy alternative is the v1 `td / a / small / a`
shape with rank in the first anchor. The current alternative is:

```html
<td class="l"><a href="/GER"><img src="/static/flags/deu.png" alt="GER" style="width:20px; opacity:0.8;"></a> <small> 1 </small><a href="/Bayern">Bayern München<span class="min481"></span></a></td>
```

Only rank, provider route and display name vary. The `img` attributes occur in
exact `src`, `alt`, `style` order; matching single or double quotes and ASCII
inter-attribute whitespace are allowed; `img` is void (`>` or `/>`) with no
children. `td` has only `class=l`; the federation anchor has only `href=/GER`
and exactly that image/no text; `small` has no attributes; the final anchor has
only one `href`, one nonempty name text node, then an empty
`span class=min481`. Whitespace is allowed only at fragment ends, between the
three direct children and around `small` rank text. The fixed image style is
inert text, never evaluated or loaded.

Reject parser repair, omitted close tags, extra CSS/attributes/elements,
`srcset`, handlers, namespaces, comments, processing instructions, duplicate
attributes, unknown URLs, arbitrary images, nonempty spans, nested anchors and
entity/Unicode-lookalike/percent-escaped current-form attributes. Reject raw
`&`, invalid scalar, NUL and non-NFC name/rank text. Trim ASCII whitespace only
around `small` rank, then require canonical positive decimal Int32 spelling:
no sign, exponent, leading zero or internal space. Elo is the untrimmed second
string and the same positive Int32 spelling. The route is
`^/[A-Za-z0-9][A-Za-z0-9-]{0,127}$`, excludes `/GER`, and is never decoded or
normalized. The display name is exact NFC text from the final anchor, excluding
the span. Ranks increase; ranks/routes are unique; exact route/name pairs map
to the unchanged 18 rows; unmatched extra German clubs may be ignored, while
crossed, duplicate or missing required identities reject. The two unused data
strings remain bounded inert text. Date evidence is the one Germany heading,
never helper text, image URL, another table or an HTTP timestamp.

Unknown versions, mixed pairs, missing identities, hostile envelopes and
fragments all fail closed. ADR-0077's transport, response, size, date, mapping,
freshness and evaluation rules and ADR-0081's required/null/type, selection,
receipt, health, publication and recovery rules remain operative except for
this stated refinement.

### W2 transport and issue projection

W2 provides a checked-in Node 24 JavaScript action using pinned
`@actions/artifact` 6.2.1 and a committed lockfile for immutable same-run
upload. Its wrapper invokes the profile CLI with an argument array and inherited
action runtime environment, never a shell. It verifies fixed repository/run
identity against the `gha` cycle, known operation/paths and required runtime
environment values without printing them. No runtime token is passed in an
argument, stdout/stderr, diagnostics, request header or signed URL. The bridge
kills and awaits the child process tree on cancellation or its two-minute
operation deadline; the enclosing context job remains bounded at 45 minutes.
Production enabled-source jobs use setup-node then `npm ci --ignore-scripts`;
development and disabled routes do not require the Node bridge.

Artifact name is the existing handoff derivation from the bound cycle storage
identity: no attempt/random suffix, overwrite, delete or fallback artifact.
Node uploads only producer-owned regular files in one unique checked contained
scratch directory, rejects symlinks/unknown files, passes an explicit file list,
`compressionLevel=0` and `retentionDays=7`, and uses immutable create. Finalize
then probe/download/domain-verify before `HandoffReady`; an upload return value
is never bundle proof.

.NET lists every artifact page for the exact repository and run and matches one
exact ordinal name. Zero after a complete successful list is `Absent`; multiple,
malformed identity, expired matching artifact or wrong run is `Conflict`; any
failed, partial, authentication or rate-limited listing is `Indeterminate`.
It downloads one match by ID. The GitHub bearer credential goes only to the
configured API origin. At most one HTTPS archive-location redirect is followed
with a separate unauthenticated client; non-HTTPS, userinfo or unbounded
redirects reject, and the signed URL is never logged or sent a bearer token.

The compressed response is limited to 4 MiB; declared and actual expanded total
to 3 MiB; manifest to 512 KiB; `bundle.sha256` to 65 bytes; and source HTML to
2 MiB. Only `manifest.json`, `bundle.sha256` and optional
`club-elo/source.html` are accepted; a rejected observation has only the first
two. Before any extraction, reject unsupported compression/encryption,
duplicate/case-colliding names, traversal, rooted/drive paths, backslashes,
links/reparse/device entries, extra directories/files and unsafe ZIP metadata.
Read bounded regular entries only into memory. Independent domain verification
proves manifest, digest, selected payload hash/length, observation, cycle and
reservation; ZIP integrity alone cannot yield `HandoffReady`.

On replay, indeterminate upload is probed; authoritative absence follows the
coordinator failure path; a present verified reservation resumes without
reacquisition/reupload; conflict fails closed. Cross-job and same-run replay
find the original artifact regardless of `run_attempt`. Expired/deleted
artifacts fail and are never rebuilt from current network data. W2 tests cover
pagination/auth, ambiguity/expiration, redirect credential separation, hostile
ZIP metadata/path/bomb cases, timeout/cancellation cleanup, upload uncertainty,
replay and same-run identity. Cross-job visibility and runtime credentials stay
V2 live gates.

Issues are projected only after the durable final receipt/health commit, only
for `ehonda/KicktippAi`, through the existing canonical body/hash/marker seam.
List every page including closed issues, exclude pull requests and match the
exact marker as a body line. With desired Closed, zero matches creates nothing;
one match reconciles title/body/state; multiple matches remain `Pending` and no
issue is edited or deleted. Do not create labels, assignments or comments. API
origin and requests are bounded, cancellation propagates and no secret or raw
provider text enters an issue. HTTP list/create/update/close failure leaves
committed data untouched and health `Pending`; a successful response must prove
expected identity/state/body or be re-read before `Synchronized`. Later
preparation retries only a pending current-watermark projection. Late or
superseded cycles, synchronized replay, disabled sources and development create
no issue effect; indeterminate create must not duplicate an issue. Enabled jobs
use only `contents: read`, `actions: read`, and `issues: write`.

### Source-only route and fixed targets

`collect-context profile --enable-club-elo-source --club-elo-only` and the
matching development command are valid only for Bundesliga 2026/27 with
Club Elo enabled and rosters disabled. They reuse normal preparation,
selection, publication and receipt logic but resolve no Kicktipp, history,
roster, model, OpenAI or Langfuse service. Invalid scope, lane, community,
cycle, full-season/matchday or roster combinations fail before external service
resolution. A dry run makes no durable write, artifact, handoff or issue.

The GitHub outer production workflow adds a `workflow_dispatch` selector
`club_elo_validation` with the exact values `off`, `development` and
`production`, default `off`. Schedule and ordinary manual `off` retain the
existing 16-job graph. The first normal context job has the mutually exclusive
validation guard; normal dependent/match/model jobs remain unreachable when a
validation value is selected. Only an explicit development/production selection
starts the separate reusable `buli2627-club-elo-validation.yml`, which has no
schedule or dispatch trigger. It runs one fixed development source-only job or
eight production source-only jobs in receipt order via the same base-context
workflow.

Reusable inputs are typed Club Elo enable, source-only and exact cycle/lane;
combinations validate before Node setup or external writes. Normal production
callers bind cycle `gha:${{ github.repository_id }}:${{ github.run_id }}`,
producer `pes-squad-context`, their fixed lane and consumer string. Branch
validation preserves the dispatched ref through nested workflows and shares the
outer non-cancelling concurrency group. The validation path passes Firebase and
bounded GitHub permissions only: no Kicktipp, OpenAI, Langfuse or model secret,
credential or call. Kicktipp may be optional at the reusable declaration only
when normal full-profile preflight still requires it before collection. GitHub
acceptance of this new branch-ref input schema is a live bootstrap gate: if
rejected, stop and respec rather than merge enabled code merely to try it.

The fixed receipt order is:

```text
pes-squad-context,schadensfresse-context,relaxdays-tippt-context,arena-sol-xhigh-context,arena-sol-high-context,arena-luna-medium-context,arena-terra-xhigh-context,arena-luna-none-context
```

These are eight receipts across four physical community heads: `pes-squad`,
`schadensfresse`, `relaxdays-tippt`, and the shared `ehonda-ai-arena` head for
the five arena receipts. Every lane is selected and evidenced independently.

### Staged activation, rollback and restoration

The required evidence sequence is: offline/emulator matrices; local
development source-only dry run with a real official GET; development
persistence; a distinct later development cycle; branch Actions development
dispatch; branch Actions production dispatch with an eligible observation,
artifact, eight receipts and four heads; a distinct later production
retention/rejection cycle; same-run replay; then the all-eight normal-source
flag change. A source-only success does not certify a failing whole-profile
schedule. The workflow validation selection excludes every normal context,
match and model job and has no schedule trigger.

Rollback is a reviewed all-eight normal-flag-off commit and, if implicated,
disables source-only dispatch for the investigation. It does not delete source
state, reset heads, change the schedule/topology or revert compatible v2
reconstruction. Verified prior heads are fallback. Corrupt, ambiguous or
concurrent state is owner-directed manual recovery. Restoration needs a
corrected reviewed/CI-green tip, real accepted then later distinct retention or
no-change evidence, eight receipts, correct heads/health/issues, copy
compatibility and a fresh activation gate.

### Literal downstream ownership

S0 owns exactly:

- `plans/bundesliga-2026-27/decisions/0083-activate-official-club-elo-context-refresh.md`
- `plans/bundesliga-2026-27/decisions/0077-refresh-club-elo-from-official-html.md` (one successor-link block only)
- `plans/bundesliga-2026-27/decisions/0081-close-club-elo-html-publication-and-selection-seams.md` (one successor-link block only)
- `plans/bundesliga-2026-27/decisions/README.md`
- `plans/bundesliga-2026-27/README.md`
- `plans/bundesliga-2026-27/tasks/p1-04-club-elo-refresh.md`
- `plans/bundesliga-2026-27/tasks/p1-05-roster-refresh.md`
- `plans/bundesliga-2026-27/designs/p1-04-05-context-refresh.md`
- `plans/bundesliga-2026-27/p1-04-05-execution-packet.md`
- `plans/bundesliga-2026-27/p1-status-snapshot.md`
- `plans/bundesliga-2026-27/execution-strategy.md`
- `.agents/skills/bundesliga-2026-27-onboarding/references/competition-profile.md`

E2 owns exactly `src/Core/BundesligaClubEloRefresh.cs`,
`src/Core/BundesligaContextSourceBundle.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaClubEloRefreshSource.cs`,
`tests/Core.Tests/BundesligaClubEloRefreshTests.cs`,
`tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`,
`tests/Core.Tests/BundesligaClubEloPublicationTests.cs`,
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaClubEloRefreshSourceTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/current-official-2026-09-15.html`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/official-helper-prefix.txt`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/official-before-call.txt`,
`tests/Orchestrator.Tests/Orchestrator.Tests.csproj`, and
`docs/sources/bundesliga-2026-27-club-elo.md`.

W2 owns exactly `src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceArtifactStore.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceIssueProjector.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceArtifactStoreTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceIssueProjectorTests.cs`,
`.github/scripts/context-source-artifact/package.json`,
`.github/scripts/context-source-artifact/package-lock.json`,
`.github/scripts/context-source-artifact/context-source-artifact.mjs`, and
`.github/scripts/context-source-artifact/context-source-artifact.test.mjs`.

V2 owns exactly `src/FirebaseAdapter/ServiceCollectionExtensions.cs`,
`src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileCommand.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileSettings.cs`,
`src/Orchestrator/Commands/Operations/Dev/CollectContextDevCommand.cs`,
`src/Orchestrator/Commands/Operations/Dev/CollectContextDevSettings.cs`,
`src/Orchestrator/Commands/Operations/Dev/CompetitionProfileCollectionRunner.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextProfileCommandTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/Dev/CollectContextDevCommandTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/Dev/CompetitionProfileCollectionRunnerTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextCollectionWorkflowContractTests.cs`,
`.github/scripts/Test-PredictionWorkflowContracts.ps1`,
`.github/scripts/context-source-artifact/action.yml`,
`.github/scripts/context-source-artifact/run-context-profile.mjs`,
`.github/scripts/context-source-artifact/run-context-profile.test.mjs`,
`.github/workflows/base-context-collection.yml`,
`.github/workflows/buli2627-production-live-matchday.yml`, and
`.github/workflows/buli2627-club-elo-validation.yml`.

A2 owns exactly `.github/workflows/buli2627-production-live-matchday.yml`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextCollectionWorkflowContractTests.cs`, and
`.github/scripts/Test-PredictionWorkflowContracts.ps1`, after V2 releases
those three paths. C2 receives released S0 documents except ADR-0077/0081,
released E2 source documentation, plus root `README.md` attribution, the two
named review briefs and `plans/bundesliga-2026-27/evidence/p1-04-operational-activation.md`.
S0's old-ADR ownership ends permanently; C2 cannot reopen accepted decision
bodies. E2 and W2 are disjoint; V2 and A2 reuse only the stated serial paths.
### Six-milestone ownership and release matrix

| Milestone | Owns | Admission and release |
| --- | --- | --- |
| S0 | This ADR; the current P1-04/P1-05 plan, design, packet, status, strategy, profile and decision-index records; one successor-link block in ADR-0077 and ADR-0081 | Independent specification acceptance; reviewed/cumulative/final documents create the first draft-PR checkpoint and release E2/W2. |
| E2 | Parser-v2, descriptor/bundle and source-command implementation, fixtures, tests and Club Elo source documentation | S0 accepted; mechanics and compatibility validation release its implementation to integration, never live activation. |
| W2 | GitHub artifact/issue adapters, their tests, and the Node artifact bridge package | S0 accepted; Orchestrator and deterministic Node validation release adapters to integration, never runtime wiring. |
| V2 | DI, profile/dev source-only command/settings, reusable/base/outer/validation workflow wiring and workflow tests | Cumulative E2+W2 acceptance and exact-head CI; keeps normal scheduled source inputs false and releases live evidence steps. |
| A2 | Only normal production workflow source flags and their contract tests | V2 plus all real operational evidence; atomically enables all eight normal callers and releases final PR acceptance. |
| C2 | After release, truthful review brief, attribution, source/evidence records and current status | A2 candidate and actual evidence; final acceptance/exact-head CI, merge, then scoped post-merge evidence only. ADR-0077/0081 never transfer from S0. |

Each milestone has one isolated writer and a scoped local commit. E2 and W2 are
independent; all subsequent path reuse is serial. Root integrates S0 → E2/W2 →
V2 → A2 → C2, creates the first draft PR after S0, and publishes only coherent
reviewed milestones. Before every push root verifies branch, remote, status and
tip, then uses an explicit remote/branch. Merge requires a fresh independent
final reviewer, exact reviewed head, required exact-head CI and no unresolved
material finding. Post-merge verifies main, Pages, one source-only production
dispatch and the next existing scheduled run; an unrelated whole-schedule
failure is reported as blocked rather than green.

No disk restriction applies at or above 20 GiB effective free space. The prior
percentage warning is not a restriction.

## Alternatives considered

- **Treat PR #111 as operational completion:** rejected because it has no
  current accepting observation, W2 transport, source-only validation or
  all-eight receipt evidence.
- **Enable all normal callers before live validation:** rejected because it
  would expose scheduled context collection before source, handoff and receipt
  gates are proved.
- **Revive P1-05 with P1-04:** rejected because ADR-0082's sidecar-gated
  deferral and source-off boundary remain operative.

## Consequences

- P1-04 is operational work in progress; S0 itself implements no parser-v2,
  W2, V2, live-validation or A2 behavior.
- P1-05/R1 stays source-off, `MetadataUnavailable`, without probe, provider,
  artifact, observation, receipt, health, publication, issue or API action.
- A live provider result, workflow dispatch schema, GitHub runtime/artifact
  behavior and history failures remain observable gates, not completion claims.

## Affected tasks

- [P1-04 Club Elo refresh](../tasks/p1-04-club-elo-refresh.md)
- [P1-05 roster refresh](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 context-refresh design](../designs/p1-04-05-context-refresh.md)
- [P1-04/P1-05 execution packet](../p1-04-05-execution-packet.md)

## Supersedes

Specified portions only. ADR-0077 remains Accepted; ADR-0083 supersedes only
its fixed v1-only parser/table identity requirement, selected-script envelope
and first-cell fragment grammar for new acquisition, historical E1
literal-path/release boundary, and prior no-live/no-activation boundary for the
bounded owner-authorized Club Elo effects. Existing v1 interpretation remains
historical truth; its acquisition/response/size/date/mapping/freshness/
evaluation rules, canonical descriptor/publication shapes, provenance and
fallback remain operative subject to ADR-0081 and this ADR.

ADR-0081 remains Accepted; ADR-0083 admits the exact paired `(v1,v1)` and
`(v2,v2)` identities while retaining its required/null/type/evaluation rules;
replaces its C3/E1 literal-path/release boundaries with this six-milestone
contract; and replaces its dormant-only Club Elo authority with the bounded
live/handoff/persistence/activation/flag-off authority and gates above. Shared
selection, health/receipt completion, publication reconstruction,
guard/fence/order/recovery invariants and all non-Club-Elo exclusions remain
operative. This does not change schema/journal, R1/roster deferral, production
topology/models/prompts/credential/posting/copy contracts or future incident
recovery authority.
