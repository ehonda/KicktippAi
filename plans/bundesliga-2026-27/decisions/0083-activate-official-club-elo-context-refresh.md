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

The optional exact prefix precedes only `const eloData = <array>;`; the exact
comment precedes only `JSSortableEloTable(eloData);`; only ASCII whitespace may
remain. V2 uses the frozen legacy/current first-cell grammar and inert DOM
validation. It executes and ignores no arbitrary JavaScript; unknown envelopes,
version pairs, parser repair and hostile fragments fail closed. ADR-0077's
transport, response, size, date, mapping, freshness and evaluation rules and
ADR-0081's required/null/type, selection, receipt, health, publication and
recovery rules remain operative except for this stated refinement.

### W2 transport and issue projection

W2 provides a checked-in Node 24 JavaScript action using pinned
`@actions/artifact` 6.2.1 for immutable same-run upload. .NET lists all
artifact pages for the exact run, downloads the one exact artifact by ID, keeps
GitHub API authentication separate from the HTTPS archive location, and
performs bounded in-memory ZIP inspection before domain verification. It
accepts only `manifest.json`, `bundle.sha256` and optional
`club-elo/source.html`, with the frozen size, path, duplicate, compression and
metadata bounds. Runtime credentials never appear in command arguments, logs or
diagnostics.

Issues are projected only after the final durable receipt/health commit, only
for `ehonda/KicktippAi`, with an exact body-line marker, all-pages closed-issue
search, PR exclusion and idempotent reconciliation. Uncertain failure remains
Pending and cannot undo committed contexts. Enabled jobs use only `contents:
read`, `actions: read`, and `issues: write`.

### Source-only route and fixed targets

`collect-context profile --enable-club-elo-source --club-elo-only` and the
matching development command are valid only for Bundesliga 2026/27 with
Club Elo enabled and rosters disabled. They reuse normal preparation,
selection, publication and receipt logic but resolve no Kicktipp, history,
roster, model, OpenAI or Langfuse service. Invalid scope, lane, community,
cycle, full-season/matchday or roster combinations fail before external service
resolution. A dry run makes no durable write, artifact, handoff or issue.

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

S0 owns only the twelve documentation paths named in its reviewed assignment,
including this ADR, current plan/task/design/packet/status/strategy/profile and
decision-index records, plus one successor-link block each in ADR-0077 and
ADR-0081. Its old-ADR ownership ends with S0.

E2 owns only `src/Core/BundesligaClubEloRefresh.cs`,
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

W2 owns only `src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceArtifactStore.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceIssueProjector.cs`,
their two corresponding Orchestrator test files, and
`.github/scripts/context-source-artifact/{package.json,package-lock.json,context-source-artifact.mjs,context-source-artifact.test.mjs}`.

V2 owns only Firebase and Orchestrator service registration; the profile and
development collection command/settings/runner; their three command/runner test
files; `ContextCollectionWorkflowContractTests.cs`;
`.github/scripts/Test-PredictionWorkflowContracts.ps1`; the local action and
runner files under `.github/scripts/context-source-artifact/`; and
`base-context-collection.yml`, `buli2627-production-live-matchday.yml`, and
`buli2627-club-elo-validation.yml`. A2 reuses only the production workflow,
workflow-contract test and PowerShell workflow-contract script after V2
releases them. C2 receives released S0 records except ADR-0077/0081, released
E2 source documentation, root attribution, the two named review briefs and
operational evidence; it cannot reopen accepted decision bodies.
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
