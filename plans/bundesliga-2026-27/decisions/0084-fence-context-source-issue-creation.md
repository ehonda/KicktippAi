# ADR-0084: Fence context-source issue creation

- Status: Accepted
- Date: 2026-09-16
- Partial successor to: [ADR-0083](0083-activate-official-club-elo-context-refresh.md)

> **Partial successor — ADR-0085.** [ADR-0085](0085-isolate-optional-club-elo-refresh.md) replaces only the optional-refresh/source-only/head-retention and release/validation boundaries it names. All remaining provisions of this Accepted ADR remain operative.

## Context

The reviewed durable-fence reconciliation packet (SHA-256
35d9e6495cedce1cace56cc65ea8f68bd3ffb1bc00cf40922c85a7b961f0af1c) and its
accepted independent specification review (SHA-256
ae4b9c65420e4412c088a24b441e341ec41b49c4c5ad523600a2b0136c5f1407) show
that a process-local uncertain-create marker and a later health CAS cannot
prevent a duplicate GitHub issue after POST commits but its response is lost.
This is a material durable-effect boundary and release-graph change.

## Decision

### Durable marker-wide fence

Add the auxiliary context-source-issue-fences collection with contract
context-source-issue-fence/v1. Its document ID uses the existing
length-prefixed HashFields scheme with domain context-source-issue-fence-storage/v1,
fixed repository ehonda/KicktippAi, production scope, competition, source, and
the exact canonical marker. The stable identity is marker-wide: body hash,
desired state, watermark, cycle, process, and run_attempt are attempt data and
never create a new fence. This operational path is Club Elo only; roster and
development create no fence records.

A strict record has its contract and identity, monotonic revision, state,
optional attempt token/body hash/watermark/desired state/started time, optional
positive issue number, update time, and fixed safe diagnostic code. It stores
no credentials, issue body, provider data, HTTP text, or raw exception.

| State | Meaning | Automatic POST |
| --- | --- | --- |
| Ready | Proven fresh marker lineage | One fresh CAS winner may arm |
| CreateUncertain | An armed request might have reached GitHub | Never |
| Bound | Exact-marker issue number is durably known | Never |
| LegacyUncertain | Existing health has no proved fence history | Never |

Ready is atomically changed to CreateUncertain before POST. Only the caller
whose transaction commits receives a one-use in-memory grant; transaction retry,
lost transaction reply, equal token, later watermark, or another process never
receives a new grant. CreateUncertain has no TTL or automatic reset. Bound
retains the issue identity permanently; resolving uncertainty means binding,
never returning to Ready.

### Atomic seam, ordering, and recovery

Keep IBundesligaContextSourceIssueProjector.ProjectAsync(health,
cancellationToken) and the coordinator's post-committed-health invocation.
Inject IBundesligaContextSourceIssueFenceRepository into the concrete projector.
It provides strict read/initialize, TryArmCreate, and BindObservedIssue typed
outcomes. TryArmCreate transactionally compares current health's watermark,
completed-cycle identity, exact desired projection, Pending status, fence
revision, and Ready state before persisting CreateUncertain. No GitHub call
occurs in a Firestore transaction.

List bounded pages including closed issues, excluding pull requests and matching
the marker as an exact body line. Partial, malformed, failed, or over-budget
listing is Indeterminate, never absence. A unique match is bound before
title/body/state reconciliation; duplicate matches remain Pending. A bound
number missing from a listing is resolved only by one bounded targeted GET,
which must reprove the exact marker before PATCH. Immediately before POST and
PATCH, re-read current health transactionally and require the exact current
Pending desired projection. A late admitted request may finish after a newer
watermark, but it cannot synchronize newer health; the later projection
reconciles the single bound issue.

On arm-before-send loss, POST-response loss, process death, rejection after
dispatch, failed bind, delayed visibility, a newer body, or a newer watermark,
retain CreateUncertain and probe only. Never retry POST automatically, including
through resilience handlers. A valid create response or later unique
exact-marker discovery binds the number. Bound-but-unsynchronized health is
reconciled on a later preparation with the existing expected-health CAS.

At first production Club Elo health creation, the same Firebase transaction
creates Ready only when both health and fence are absent and no old creating
projector can run. It preserves any existing fence and covers receipt completion
and aborted/superseded genesis paths. Existing health without a fence becomes
LegacyUncertain; a unique exact-marker discovery may bind it, but zero matches,
a deleted issue, a removed marker, or duplicate markers remain Pending with a
fixed recovery-needed diagnostic. No migration, reset, deletion, backoff, age,
null attempt, or rollout script grants POST permission. Owner-directed recovery
under the existing ambiguous-data rule is required.

Use a linked two-minute operation budget, positive finite request timeouts no
greater than 30 seconds with explicit InfiniteTimeSpan rejection or clamp, no
more than 100 pages of 100 entries, and 8 MiB as the finite bound for every
issue response body. The bound applies to each list page and to every targeted
GET, POST, and PATCH response body. Permit one POST, one targeted GET, one
PATCH, and at most one recovery list if budget remains. Cancellation does no
cleanup HTTP work. Normal flag-off rollback preserves fences and must
not restore an older creating projector.

### Frozen ownership and release graph

D0 owns exactly:

- plans/bundesliga-2026-27/decisions/0084-fence-context-source-issue-creation.md
- plans/bundesliga-2026-27/decisions/0083-activate-official-club-elo-context-refresh.md (successor link only)
- plans/bundesliga-2026-27/decisions/README.md
- plans/bundesliga-2026-27/README.md
- plans/bundesliga-2026-27/tasks/p1-04-club-elo-refresh.md
- plans/bundesliga-2026-27/tasks/p1-04-issue-create-fence.md
- plans/bundesliga-2026-27/designs/p1-04-05-context-refresh.md
- plans/bundesliga-2026-27/p1-04-05-execution-packet.md
- plans/bundesliga-2026-27/p1-status-snapshot.md
- plans/bundesliga-2026-27/execution-strategy.md
- .agents/skills/bundesliga-2026-27-onboarding/references/competition-profile.md
F2 owns exactly:

- src/Core/BundesligaContextSourceIssueFence.cs
- src/FirebaseAdapter/FirebaseContextSourceIssueFenceRepository.cs
- src/FirebaseAdapter/Models/ContextSourceIssueFenceFirestoreModels.cs
- src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs (atomic genesis only)
- src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceIssueProjector.cs
- tests/Core.Tests/BundesligaContextSourceIssueFenceContractTests.cs
- tests/FirebaseAdapter.Tests/FirebaseContextSourceIssueFenceRepositoryTests.cs
- tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceIssueProjectorTests.cs
- tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs

W2 retains exactly:

- src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceArtifactStore.cs
- tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceArtifactStoreTests.cs
- .github/scripts/context-source-artifact/package.json
- .github/scripts/context-source-artifact/package-lock.json
- .github/scripts/context-source-artifact/context-source-artifact.mjs
- .github/scripts/context-source-artifact/context-source-artifact.test.mjs
Its issue paths transfer
serially to F2 and its current issue code is quarantined starting input. E2
retains its 13 original parser/descriptor/source paths, including the existing
Firebase cycle-repository test and Orchestrator test project. V2 retains its
registration paths and wires the new repository only after F2 acceptance.

The release graph is exact specification acceptance -> reviewed D0 -> E2 and
W2-transport clearance -> combined dormant baseline -> F2 -> combined
Core/Firebase/Orchestrator/Node validation, fresh acceptance and exact-head CI
-> V2 -> the original root-leased live evidence -> A2 -> closeout. W2 transport
alone never releases runtime wiring or production effects. P1-05/R1 remains
ADR-0082 deferred.

F2 acceptance requires the following real-transaction and controllable-HTTP
matrix across distinct projector/repository instances with empty process-local
state:

- Concurrent Ready arms yield exactly one grant and POST; transaction callback
  retry, a lost transaction reply, or replay of the same attempt token yields
  no duplicate grant.
- Simulate process loss before arm, after durable arm before send, after
  committed POST before response, after response before BindObservedIssue, and
  after binding before health persistence. Delayed visibility is retried only
  as bounded probe across fresh processes and newer body/watermark data; it
  never produces a second POST.
- Verify one exact marker discovery, duplicate markers, pull-request exclusion,
  wrong repository/number/state/body, malformed or incomplete bounded listings,
  closed uncertainty, deleted issue, and removed marker behavior.
- Verify stale callers are denied before admission; after an older admitted
  effect, current health reconciles the one permanent bound identity and stale
  results cannot synchronize newer health.
- Verify atomic new health/fence genesis, concurrent genesis, receipt completion
  and aborted/superseded genesis, historical health round trip, synchronized
  zero-fence/effect handling, legacy no-fence fail-closed state, invalid durable
  maps, and no automatic expiry/reset.
- Verify disabled, development, roster, and already synchronized cases make
  zero effects and no fence; receipt/head health commits remain durable if the
  projector fails; cancellation propagates without cleanup HTTP calls.
- Verify canonical marker, body, and watermark equality; permanent bound
  identity/number persistence; bound response/read identity checks; bounded GET,
  POST, PATCH, and listing response bodies; and secret-safe diagnostics.

It also requires solution build, full Core, FirebaseAdapter emulator coverage,
Orchestrator, deterministic Node and lock validation, then fresh combined review
and exact-head CI.

## Consequences

No runtime, source, Firestore, GitHub, artifact, issue, workflow, schedule,
credential, model, prediction, posting, or live effect is implemented or
authorized by D0. P1-04 remains dormant pending the stated gates. Existing
health, publication, descriptor, receipt, and source-journal contracts retain
their shapes; this is a strict auxiliary fence contract.

## Supersedes

ADR-0083 remains Accepted. This ADR supersedes only ADR-0083's issue
persistence/recovery implementation boundary, its no-auxiliary-schema statement
as necessary, literal W2 issue ownership, and its six-milestone release graph.
Parser, artifact transport, receipt, health-reduction, authority, topology, and
P1-05 deferral provisions remain operative.
