# ADR-0078: Refine context-source pre-artifact and publication fence

- Status: Accepted — its transitional C1 separate-receipt inference paragraph is narrowly superseded by [ADR-0080](0080-bound-transitional-context-publication-recovery.md); all other decisions remain operative.
- Implementation: Not started
- Date: 2026-09-07

## Context

ADR-0074 freezes the common cycle seam, but leaves two correctness boundaries
too broad: the roster outcome before an artifact can be identified, and the
authorization of a source-backed context publication. This ADR narrowly refines
ADR-0074; it does not change its cycle topology, source-disabled bypass,
historical publications, or source activation gates.

## Decision

### Roster pre-artifact matrix

Evaluation is exactly `MetadataUnavailable`, `MetadataMalformed`,
`MetadataRevisionRejected`, `MetadataUnchanged`, `RemoteIdentityUnavailable`,
`ArtifactTransportRejected`, `SizeRejected`, `RemoteDriftRejected`,
`HashRejected`, `RevisionRejected`, `SchemaRejected`, `SourceDateRejected`,
`SeasonRejected`, `IdentityRejected`, or `Eligible`, in that precedence order.
`RemoteIdentityChanged` is never an evaluation or diagnostic: it is an
acquisition reason only. Acquisition reasons are exactly `NewRevision`,
`PendingRevision`, `PolicyChanged`, `RemoteIdentityChanged`, and
`AcceptedRevisionUnchanged`.

Select a reason deterministically. For a pending tuple, the same revision and
policy select `PendingRevision`, the same revision with a different policy
selects `PolicyChanged`, and a different revision selects `NewRevision`. With
no pending tuple, no accepted tuple or a different revision selects
`NewRevision`; the same revision with a different policy selects
`PolicyChanged`; the same revision, policy, and an observed-before identity
different from accepted selects `RemoteIdentityChanged`; and the same
revision, policy, and identity selects `AcceptedRevisionUnchanged`. The same
revision and policy with unavailable identity selects null. A
`RemoteIdentityUnavailable` row may carry `NewRevision`, `PendingRevision`, or
`PolicyChanged` only when that reason is already provable without an identity;
otherwise it carries null. Code never invents a revision or a reason.

Metadata parsing is exact: no complete metadata bytes is `MetadataUnavailable`;
a complete envelope invalid for any reason other than its revision is
`MetadataMalformed`; otherwise a valid envelope with a missing or non-lowercase
40-hex revision is `MetadataRevisionRejected`. Diagnostic/evaluation precedence
is exactly `ROSTER_METADATA_UNAVAILABLE`, `ROSTER_METADATA_MALFORMED`,
`ROSTER_ADVERTISED_REVISION_REJECTED`,
`ROSTER_REMOTE_IDENTITY_UNAVAILABLE`, `ROSTER_ARTIFACT_TRANSPORT_REJECTED`,
`ROSTER_SIZE_REJECTED`, `ROSTER_REMOTE_DRIFT_REJECTED`,
`ROSTER_HASH_REJECTED`, `ROSTER_REVISION_REJECTED`,
`ROSTER_DUCKDB_SCHEMA_REJECTED`, `UNKNOWN_SOURCE_DATE`,
`NO_ELIGIBLE_2026_MEMBERSHIP`, `ROSTER_IDENTITY_REJECTED`, then `Eligible`.
The thirteen rejected evaluations map to `Rejected` and their corresponding
primary code; `MetadataUnchanged` maps to `MetadataUnchanged` with `[]`, and
`Eligible` maps to `ArtifactCaptured` with `[]`. A rejected observation stores
its mandatory primary code plus independently proven lower-precedence
diagnostics, serialized uniquely in ordinal precedence order. Membership and
enrichment rejection diagnostics remain content diagnostics, not evaluations.
Acquisition failures have acquisition health; source-date, season, and identity
outcomes have membership health; metadata unchanged resets acquisition and
repeats retained membership/enrichment; an eligible receipt decides the result.

| Evaluation | disposition | primary diagnostic |
| --- | --- | --- |
| `MetadataUnavailable` | `Rejected` | `ROSTER_METADATA_UNAVAILABLE` |
| `MetadataMalformed` | `Rejected` | `ROSTER_METADATA_MALFORMED` |
| `MetadataRevisionRejected` | `Rejected` | `ROSTER_ADVERTISED_REVISION_REJECTED` |
| `MetadataUnchanged` | `MetadataUnchanged` | `[]` |
| `RemoteIdentityUnavailable` | `Rejected` | `ROSTER_REMOTE_IDENTITY_UNAVAILABLE` |
| `ArtifactTransportRejected` | `Rejected` | `ROSTER_ARTIFACT_TRANSPORT_REJECTED` |
| `SizeRejected` | `Rejected` | `ROSTER_SIZE_REJECTED` |
| `RemoteDriftRejected` | `Rejected` | `ROSTER_REMOTE_DRIFT_REJECTED` |
| `HashRejected` | `Rejected` | `ROSTER_HASH_REJECTED` |
| `RevisionRejected` | `Rejected` | `ROSTER_REVISION_REJECTED` |
| `SchemaRejected` | `Rejected` | `ROSTER_DUCKDB_SCHEMA_REJECTED` |
| `SourceDateRejected` | `Rejected` | `UNKNOWN_SOURCE_DATE` |
| `SeasonRejected` | `Rejected` | `NO_ELIGIBLE_2026_MEMBERSHIP` |
| `IdentityRejected` | `Rejected` | `ROSTER_IDENTITY_REJECTED` |
| `Eligible` | `ArtifactCaptured` | `[]` |

`R` means a required canonical observed value, `N` an explicit null, and
`C(fact)` a value required exactly when the named fact is proved and otherwise
null. Every descriptor has the exact
`transfermarkt-duckdb-observation-descriptor/v1` contract, the fixed HTTPS
metadata and artifact URLs, and `policySha256`
`56ce2f0543b91a59b63fbec7889f1bf547681e90f58da7c419028fd749285d9b`
(the exact ADR-0074 policy SHA). Every descriptor field is present in its
ADR-0074 order. A remote identity is ordered `etag, byteLength` and has at
least one non-null member. When both members were observed, both are retained
canonically; no selection between simultaneously observed members occurs, so
descriptor hashing, acquisition reason, revision-state comparison, and replay
remain deterministic. Every Matrix A `R` identity cell may therefore contain
one or both observed members, while an `N` identity cell has both members null;
no matrix row deletes a simultaneously observed member. In every
non-`MetadataUnchanged` row,
`retainedDescriptorSha256` and `retainedEvaluation` are null and
`retainedDiagnostics` is `[]`; only `MetadataUnchanged` retains all three.
Unspecified facts below are explicit null and no source, transport, or
observation timestamp substitutes for a missing source fact.

Matrix A fixes metadata, acquisition, and remote-identity fields.

| Evaluation | metadata SHA + length | advertised revision | acquisition reason | before identity | after identity |
| --- | --- | --- | --- | --- | --- |
| `MetadataUnavailable` | N | N | N | N | N |
| `MetadataMalformed` | R | N | N | N | N |
| `MetadataRevisionRejected` | R | N | N | N | N |
| `MetadataUnchanged` | R | R, exactly accepted | `AcceptedRevisionUnchanged` | R, exactly accepted | N |
| `RemoteIdentityUnavailable` | R | R | C(reason provable without identity) else N | N | N |
| `ArtifactTransportRejected` | R | R | R non-accepted reason | R | N |
| `SizeRejected` | R | R | R non-accepted reason | R | N |
| `RemoteDriftRejected` | R | R | R non-accepted reason | R | R, unequal |
| `HashRejected` | R | R | R non-accepted reason | R | R, equal |
| `RevisionRejected` | R | R | R non-accepted reason | R | R, equal |
| `SchemaRejected` | R | R | R non-accepted reason | R | R, equal |
| `SourceDateRejected` | R | R | R non-accepted reason | R | R, equal |
| `SeasonRejected` | R | R | R non-accepted reason | R | R, equal |
| `IdentityRejected` | R | R | R non-accepted reason | R | R, equal |
| `Eligible` | R | R | R non-accepted reason | R | R, equal |

Here, a non-accepted reason is exactly `NewRevision`, `PendingRevision`,
`PolicyChanged`, or `RemoteIdentityChanged`. `RemoteIdentityChanged` is only
that reason, never an evaluation or diagnostic.

Matrix B fixes identified-artifact and selection-input fields. “Dates” means,
in order, `artifactCaptureDate`, `membershipEffectiveDate`, and
`enrichmentCaptureDate`. Season and identity facts are evaluation inputs, not
new descriptor properties.

| Evaluation | embedded revision | raw SHA | expected SHA | raw length | dates | season fact | identity fact | observation payload |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `MetadataUnavailable` | N | N | N | N | N | N | N | N |
| `MetadataMalformed` | N | N | N | N | N | N | N | N |
| `MetadataRevisionRejected` | N | N | N | N | N | N | N | N |
| `MetadataUnchanged` | N | N | N | N | N | retained-only truth | retained-only truth | N; no current artifact fact |
| `RemoteIdentityUnavailable` | N | N | N | N | N | N | N | N |
| `ArtifactTransportRejected` | N | N | N | N | N | N | N | N |
| `SizeRejected` | N | N | C(valid advertised digest) | R, over maximum | N | N | N | N |
| `RemoteDriftRejected` | C(extracted) | R | C(valid advertised digest) | R | each C(authoritatively extracted) | C(proved) | C(proved) | N |
| `HashRejected` | C(extracted) | R | R, unequal raw SHA | R | each C(authoritatively extracted) | C(proved) | C(proved) | N |
| `RevisionRejected` | N or R, unequal advertised | R | N or R, equal raw SHA | R | each C(authoritatively extracted) | C(proved) | C(proved) | N |
| `SchemaRejected` | R, equal advertised | R | N or R, equal raw SHA | R | each C(authoritatively extracted) | N | N | N |
| `SourceDateRejected` | R, equal advertised | R | N or R, equal raw SHA | R | each C(authoritatively proved), at least one N | C(proved) | C(proved) | N |
| `SeasonRejected` | R, equal advertised | R | N or R, equal raw SHA | R | all R | R, no complete eligible `last_season=2026` membership | C(proved) | N |
| `IdentityRejected` | R, equal advertised | R | N or R, equal raw SHA | R | all R | R, eligible membership | R, rejects manifest/stable-ID gate | N |
| `Eligible` | R, equal advertised | R | N or R, equal raw SHA | R | all R | R, eligible | R, passes gate | R, `rosters/source.duckdb` length/SHA equals descriptor |

Matrix C fixes the receipt, selection, publication, and revision-state result
for successfully consumed observations. Every receipt has `ratedAt` null and
`rosterRevision` equal to `descriptor.advertisedRevision`, including null. Its
selected seed/LKG membership, enrichment, capture/effective/carry/snapshot, and
provenance values retain their original truthful values. With no valid retained
or fallback data, consumption is fatal and creates no receipt. Metadata
unavailable, malformed, and revision-rejected observations have no candidate
membership.

| Evaluation | selection / origin | publication and state reduction |
| --- | --- | --- |
| `MetadataUnavailable` | `CandidateRejected` / validated seed or LKG | Existing head: `NotAttempted`; otherwise exact fallback v2 publish/reactivate. Accepted and pending unchanged. |
| `MetadataMalformed` | `CandidateRejected` / validated seed or LKG | Existing head: `NotAttempted`; otherwise exact fallback v2 publish/reactivate. Accepted and pending unchanged. |
| `MetadataRevisionRejected` | `CandidateRejected` / validated seed or LKG | Existing head: `NotAttempted`; otherwise exact fallback v2 publish/reactivate. Accepted and pending unchanged. |
| `MetadataUnchanged` | `MetadataUnchanged` / exact prior lane snapshot and origin | Exact prior bytes, provenance, source dates, carry, and conditions; `Unchanged`, no new roster payload. Retained descriptor fields R and match accepted; accepted unchanged; pending is required null and remains null. |
| `RemoteIdentityUnavailable` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted and pending unchanged because no identity tuple exists. |
| `ArtifactTransportRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted unchanged; create/update pending with advertised revision, before identity, policy, original first-seen cycle, and exact evaluation. |
| `SizeRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted unchanged; create/update the same pending tuple. |
| `RemoteDriftRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted unchanged; pending uses before identity and retains unequal after identity as drift evidence. |
| `HashRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted unchanged; create/update the same pending tuple. |
| `RevisionRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Accepted unchanged; create/update the same pending tuple. |
| `SchemaRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Store the fully identified artifact observation as accepted with revision, equal before identity, policy, and descriptor SHA; clear pending. This accepts an artifact observation, not membership selection. |
| `SourceDateRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Store the fully identified artifact observation as accepted and clear pending; it is not membership selection. |
| `SeasonRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Store the fully identified artifact observation as accepted and clear pending; it is not membership selection. |
| `IdentityRejected` | `CandidateRejected` / validated fallback or LKG | Existing head: `NotAttempted`; otherwise fallback v2 publish/reactivate. Store the fully identified artifact observation as accepted and clear pending; it is not membership selection. |
| `Eligible` | Per club: `DuckDbAccepted` / `DuckDb`, `MixedPerClubSelection` / `Mixed`, or `CandidateRejected` / (`FallbackSeed` or `LastKnownGood`); membership and enrichment are independent. | A changed observed selection is v3 `Published` or `Reactivated`; byte equality is `Unchanged`; all-rejected follows the fallback rule. Store the current accepted tuple and clear pending. |

An unavailable identity has both identity fields null. Artifact transport has a
successful pre-download before identity and a null after identity. Drift has
both identities unequal. Hash, revision, schema, content, and eligible rows
have equal identities. Metadata unchanged has before identity equal to
accepted, with after identity and all current download/raw/date/payload facts
null. A row with null advertised revision has a null receipt revision and never
mutates either revision state; incomplete or late cycles also mutate neither.

### Optional normative source-publication commit

`DocumentPublicationRequest` carries a nullable transportable
`ContextSourcePublicationCommitRequest`: a typed `ContextSourcePublicationGuard`
plus receipt template. Null preserves the legacy path and reads no source-cycle
state. Guard fields are exactly `competition, scope, cycleId, source,
consumerLaneId, communityContext, publicationSet, bundleDigest,
observationDigest, watermarkSequence, watermarkCycleId`; all are strongly typed
or validated canonical values, never Firestore paths. The template contains
every receipt semantic field except repository-computed `selectedSnapshotId`
and `publicationDisposition`; those computed original values and one transaction
timestamp are persisted in the receipt.

The first transaction branch reads the exact lane receipt. If present it
semantic-compares guard/template identities and every receipt field, then returns
the persisted original snapshot/disposition with zero head/health/revision/issue
mutation only after recomputing the snapshot ID from current ordered request
documents and requiring equality with persisted `selectedSnapshotId`; mismatch
is fatal. Receipt absence is required only for the new branch.
That branch reads outer cycle, source cycle, health and head; validates exact
typed identity, enabled source, `HandoffReady`/bundle digest,
`Finalized`/observation digest/strict prefix and matching watermark before head
CAS or unchanged decision. It computes snapshot ID and
`Published`/`Unchanged`/`Reactivated` once, then atomically writes/reuses
documents/head, exact receipt, strict next consumer prefix, and (if final)
health plus accepted/pending-revision reduction. Thus newer ineligible B fails
guard before an unchanged head can succeed.

Guarded head and receipt are all-or-nothing: a pre-commit failure leaves neither
new head nor receipt; a post-commit replay returns the persisted original result.
Sources are disabled until C2, so a guarded orphan/malformed head is fatal and
manual-recovery-only; no code manufactures receipt provenance. This replaces
C1's separate receipt transaction for guarded calls. Guard failure is
fatal/mutation-free, including no issue/API action. Guard reads/checks precede
head CAS even if target equals current.

For the transitional C1 separate-receipt shape only, an absent receipt with a
current head equal to recomputed target first validates immutable target bytes,
entries, metadata and exact guard/bundle/observation identities. It infers
`Unchanged` only when expected head equals target; `Published` only when a newly
created target has `previousSnapshotId` equal to expected previous; and
`Reactivated` only when the exact bundle/selection evidence identifies an
already-existing historical target. It then creates the exact receipt and final
reduction transactionally without head/document rewrite. Any ambiguity is fatal/
manual recovery. New C2 guarded commits remain all-or-nothing and never use this
transitional branch; C2 does not enable sources, which remain disabled through
all dormant milestones until separate owner activation.

### Verification and authority

C2 must prove round-trip and strict null reconstruction, all matrix rows,
supersession precedence, guard/head-CAS ordering, crash/retry replay, legacy
null-guard compatibility, and source-disabled zero resolution/writes/API calls.
R1 proves real-artifact rejection and synthetic trusted-date takeover; a
synthetic fixture proves mechanics only and is not real accepting evidence.
Use incremental review then a fresh final reviewer, one serialized heavy family,
and exact-head CI. Flags remain false; schedules, models, posting, credentials,
copy behavior, production writes, activation, rollback delegation,
restoration, and completion evidence each retain their separate owner gate.

## Consequences

The common amendment is required after the reviewed C1 integration. P1-05 can
remain independent of HTML and must treat the current artifact as rejection-only
evidence; P1-04 cannot bypass this common fence.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 design](../designs/p1-04-05-context-refresh.md)

## Supersedes

Only ADR-0074's roster pre-artifact and source-backed publication authorization
details. All other ADR-0074 contracts remain accepted.
