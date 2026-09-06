# ADR-0074: Freeze context-source cycles, handoff, health, and successor provenance

- Status: Accepted
- Implementation: Not started
- Date: 2026-09-06

## Context

ADR-0073 accepts one immutable source observation per existing context cycle,
but leaves the durable cycle, handoff, receipt, health, and successor-document
seam open. P1-05 is independent of P1-04 source acceptance once that common
seam exists. The current real Club Elo candidate remains transport-rejected/
`UNKNOWN_SOURCE_DATE`; the current DuckDB artifact is revision
`e44f186d6f06dd8452aaf54c7921ba66c961f637`, SHA-256
`ba1eff7337b8ca78cb533df0b6eba0d6fc58218e0767460f6770e0b37f5a2113`,
and rejects for no eligible 2026 membership and unknown authoritative source
date. Those facts are safe rejection evidence, not task completion or source
activation evidence.

## Decision

### Canonical cycle, bundle, and descriptors

Production cycle IDs are `gha:<repository-id>:<github.run_id>`; local IDs are
`local:<uuid-v7>`. Scope is `production-live` or `development`. Every raw
UTF-8 field is independently LP32-wrapped by `HashFields`:

- `cycleStorageId = HashFields(context-cycle-storage/v1, competition, scope, cycleId)`
- `sourceCycleStorageId = HashFields(context-source-cycle-storage/v1, competition, scope, cycleId, source)`
- `receiptStorageId = HashFields(context-source-receipt-storage/v1, competition, scope, cycleId, source, consumerLaneId)`
- `healthStorageId = HashFields(context-source-health-storage/v1, competition, scope, source)`
- `attemptId = HashFields(context-source-attempt/v1, competition, scope, cycleId, source)`

The bundle contains only regular files named `manifest.json`, `bundle.sha256`,
and optional `club-elo/source.csv` and `rosters/source.duckdb`; links,
traversal, alternate casing, duplicates, and extra files fail. Canonical bundle
root order is `contract, competition, scope, cycleId, cycleStorageId,
cycleSequence, startedAtUtc, stalenessReferenceAtUtc, producerLaneId,
expectedConsumers, observations`; its contract is
`bundesliga-context-source-bundle/v1`. Timestamps are the same producer-
captured UTC value. Enabled observations are `club-elo`, then `rosters`, each
ordered `source, attemptId, observedAtUtc, disposition, descriptorSha256,
descriptor, payload, diagnostics`. Disposition is `ArtifactCaptured`,
`MetadataUnchanged`, or `Rejected`; metadata-unchanged is roster-only and has
no payload. `bundle.sha256` is lowercase hex plus LF over length-prefixed
path/content pairs for manifest then captured payloads in observation order.

Club Elo uses `club-elo-direct-csv-descriptor/v1`, ordered `contract,
sourceUrl, rawSha256, rawByteLength, csvHeader, providerRatedAt,
providerDateEvidence, nameMappingContract, nameMappingSha256, sourceRows,
evaluation`. Its evaluation is `Eligible`, `TransportRejected`,
`PayloadRejected`, `HeaderRejected`, `DateRejected`, `MappingRejected`, or
`CoverageRejected`. Eligible requires actual bytes, frozen header and mapping,
one proven provider date, and 18 manifest-slug-ordered rows; it alone captures
an artifact. No current real capture is Eligible until a separate P1-04
source-specific accepted contract closes the date-evidence gap.

Roster uses `transfermarkt-duckdb-observation-descriptor/v1`, ordered
`contract, metadataUrl, artifactUrl, advertisedRevision, metadataSha256,
metadataByteLength, remoteIdentityBefore, acquisitionReason,
remoteIdentityAfter, embeddedRevision, rawSha256, expectedRawSha256,
rawByteLength, artifactCaptureDate, membershipEffectiveDate,
enrichmentCaptureDate, policySha256, retainedDescriptorSha256,
retainedEvaluation, retainedDiagnostics, evaluation`. Revisions are lowercase
40-hex Git SHAs; remote identity is ordered `etag, byteLength`, with at least
one value. The fixed policy is the exact UTF-8/no-BOM/no-final-newline bytes:

```json
{"contract":"bundesliga-roster-refresh-policy/v1","competition":"bundesliga-2026-27","manifestSha256":"d77462d4ed18ed746ef1674b957714805a22c1f16130c10f880be3b9b1d680f5","domesticCompetitionId":"L1","lastSeason":2026,"minimumPlayerCount":20,"maximumPlayerCount":40,"minimumReferenceRatioBasisPoints":7500,"maximumReferenceRatioBasisPoints":12500,"minimumIdentityOverlapBasisPoints":5000,"agePositionWarningBasisPoints":8000,"marketValueWarningBasisPoints":5000,"candidateFreshnessDays":14,"membershipStaleDays":14,"rosterCriticalDays":30,"maximumArtifactBytes":314572800,"acquisitionBudgetSeconds":300,"maximumAttempts":2,"queryProjection":"clubs-players-player-valuations/v1","captureDateRecipe":"revision-bound-authoritative/v1","effectiveDateRecipe":"revision-bound-authoritative/v1"}
```

`policySha256` is plain SHA-256 of those bytes. After metadata-only equality,
the single reported roster evaluation precedence is `TransportRejected`,
`SizeRejected`, `RemoteDriftRejected`, `HashRejected`, `RevisionRejected`,
`SchemaRejected`, `SourceDateRejected`, `SeasonRejected`, `IdentityRejected`,
then `Eligible`; diagnostics retain every proven defect. For
`MetadataUnchanged`, metadata digest/length and before identity are required;
after/embedded/raw/date/payload fields are null; retainedDescriptorSha256,
retainedEvaluation, and retainedDiagnostics are all non-null and identify the
completed accepted artifact state. **For every non-metadata rejected or eligible
row, retainedDescriptorSha256 and retainedEvaluation are null while
retainedDiagnostics is `[]`.** The complete matrix below is normative. The
current artifact reports `SourceDateRejected` with
`NO_ELIGIBLE_2026_MEMBERSHIP` and `UNKNOWN_SOURCE_DATE`.

### Transaction, handoff, and receipt contract

Cycle status is `Claiming`, `ObservationsFinalized`, `BundleVerified`,
`UploadReserved`, `HandoffReady`, `Complete`, or `Aborted`. Source-cycle status
is `Claimed`, `Finalized`, `Complete`, or `Aborted`; it records an exact
expected-consumer list and `receivedConsumers`, which must be its prefix.
Claims are transactional and exclusive: `NewClaim`, `OwnedClaim`,
`ExistingFinalized`, `Busy`, or `ExistingAborted`. An expired claim CASes to
`Aborted/ACQUISITION_INTERRUPTED`; it is never taken over or reacquired.

After build and independent verification, the producer CASes to
`BundleVerified`, transactionally binds `bundleSha256` and a fixed artifact name
while CASing to `UploadReserved`, then probes that name. A present artifact is
downloaded and validated against the reservation before CAS to `HandoffReady`.
An absent artifact is uploaded with overwrite false, downloaded and verified,
then CASed. Invalid, different, or multiple artifacts are
`HANDOFF_ARTIFACT_CONFLICT`; indeterminate upload is always probed on replay;
`HANDOFF_UPLOAD_FAILED` is recorded only after authoritative absence. A
post-upload CAS failure resumes from the bound reservation: replay neither
reacquires nor reuploads. Missing finalized local bytes before reservation is
`FINALIZED_PAYLOAD_UNAVAILABLE`.

Only `HandoffReady` consumes/publishes. A receipt request has no recorded time;
the creation transaction assigns one persisted UTC-second timestamp and a
semantic replay returns it. The final-receipt transaction atomically creates
the receipt, appends the lane, reduces health and accepted/pending revision
state, CASes source `Finalized` to `Complete`, reads all enabled source cycles,
and CASes outer `HandoffReady` to `Complete` with one completion timestamp
when all are complete. Issue API work occurs only after that commit.

Development is exactly the producer and sole consumer `development-profile`,
community `ehonda-dev-buli-2627`, scope `development`, and a validated local
UUIDv7 cycle. It creates no GitHub artifact (`artifactName` is null); it builds
and verifies the same bundle below
`Path.GetTempPath()/kicktippai-context-source/<64-hex-cycleStorageId>/`, moves
directly from `BundleVerified` to `HandoffReady`, consumes in process, persists
the one receipt, and removes the directory in `finally`. Resume after finalized
observation with no verified bundle aborts `LOCAL_HANDOFF_MISSING` without
reacquisition. Dry-run creates no durable cycle or temporary handoff and keeps
only its in-memory result.

Receipt fields are ordered `contract, competition, scope, cycleId, source,
consumerLaneId, communityContext, recordedAtUtc, observationDigest,
bundleDigest, selectionDisposition, selectedSnapshotId, selectedOrigin,
publicationDisposition, sourceDates, rosterRevision, carriedFields,
activeConditions`. Successful consumers always have a snapshot ID;
`NotAttempted` requires a validated retained head. Elo uses a non-null `ratedAt`
and null roster dates; roster uses null `ratedAt`, non-null membership effective
date, provenance-constrained capture/enrichment dates, and advertised revision.

### Health, issue, and monotonicity contract

Health is `bundesliga-context-source-health/v1`, ordered `contract,
competition, scope, source, watermark, lastCompletedCycleId,
consecutiveFailures, lastSuccessfulSourceDates, rosterRevisionState,
communitySelections, activeConditions, desiredIssueProjection`. Watermark is
`sequence, cycleId` and advances only to a greater tuple. A newer cycle aborts
an incomplete prior cycle as `SUPERSEDED_INCOMPLETE_CYCLE`; a lower cycle is
`LATE_CYCLE` and changes no acquisition, publication, receipt, head, counter,
revision, or issue. Disabled sources create no due cycle.

All staleness uses `stalenessReferenceAtUtc`: Elo is strictly over seven days;
membership and enrichment are over 14 days and critical over 30. Active
conditions are exactly `ACQUISITION_FAILED`, `HANDOFF_INCOMPLETE`,
`CYCLE_ABORTED`, `CLUB_ELO_SOURCE_REJECTED`, `CLUB_ELO_STALE_GT_7_DAYS`,
`ROSTER_MEMBERSHIP_REJECTED`, `ROSTER_ENRICHMENT_REJECTED`,
`ROSTER_MEMBERSHIP_DATE_UNKNOWN`, `ROSTER_MEMBERSHIP_STALE_GT_14_DAYS`,
`ROSTER_MEMBERSHIP_STALE_GT_30_DAYS`, `ROSTER_ENRICHMENT_DATE_UNKNOWN`,
`ROSTER_ENRICHMENT_STALE_GT_14_DAYS`, and `ROSTER_ENRICHMENT_STALE_GT_30_DAYS`.
Metadata-only resets acquisition but repeats retained membership/enrichment
outcomes; late cycles change nothing. Issue opens at counter two or any
stale/unknown condition, otherwise closes. The exact markers are:

Elo receipt selection is `NetworkAccepted`, `NetworkCandidateRejected`,
`NetworkCandidateStale`, or `NetworkCandidateNotNewer`; roster selection is
`DuckDbAccepted`, `MixedPerClubSelection`, `CandidateRejected`, or
`MetadataUnchanged`. Origins are Elo `NetworkCandidate`, `LaunchSeed`, or
`LastKnownGood`, and roster `DuckDb`, `Mixed`, `FallbackSeed`, or
`LastKnownGood`. Publication is `Published`, `Unchanged`, `Reactivated`, or
`NotAttempted`. Cycle/source errors are exactly `ACQUISITION_INTERRUPTED`,
`FINALIZED_PAYLOAD_UNAVAILABLE`, `HANDOFF_UPLOAD_FAILED`,
`HANDOFF_ARTIFACT_MISSING`, `HANDOFF_ARTIFACT_CONFLICT`,
`LOCAL_HANDOFF_MISSING`, `SUPERSEDED_INCOMPLETE_CYCLE`, `LATE_CYCLE`, and
`STATE_CONFLICT`; issue errors are `GITHUB_ISSUE_LIST_FAILED`,
`GITHUB_ISSUE_CREATE_FAILED`, `GITHUB_ISSUE_UPDATE_FAILED`, and
`GITHUB_ISSUE_CLOSE_FAILED`.

```text
<!-- kicktippai:context-source-health:bundesliga-2026-27:club-elo -->
<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->
```

Development desired issue is null and invokes no GitHub API. Production issue
bodies are LF-only, final-LF UTF-8, with the marker, competition,
`production-live`, source, watermark, and ordinal condition lines (or `NONE`).

### Successor documents and disabled bypass

Existing snapshot identity remains document-byte-only: provenance-only changes
retain the existing head and are recorded only in health/receipts. Club Elo v2
extends the current nine properties with `cycle_id, attempt_id,
source_observed_at, raw_sha256, raw_byte_length, provider_date_evidence,
name_mapping_sha256, source_rows`; v1 remains strict.

Roster v3 is `bundesliga-roster-publication/v3`, ordered `contract,
qualityReportCsv, sourceObservation, clubs`. Its self-contained source
observation is ordered `cycleId, attemptId, observedAtUtc, disposition,
observationDigest, bundleDigest, descriptorSha256, descriptor, payload`.
Reconstruction recomputes descriptor digest, requires `ArtifactCaptured` plus
`Eligible`, validates payload identity, and reads no mutable health/cycle/head
state. Only eligible changed content creates v3; rejected-with-head is
`CandidateRejected/NotAttempted`, no-head uses v2 fallback, metadata-only and
eligible unchanged retain the head. Re-activating historical v1/v2 bytes keeps
their original metadata, creation time, and predecessor; v2/v3 metadata is
never manufactured.

Selected source/provenance pairs are only `DuckDb/Artifact`,
`FallbackSeed/FallbackSeed`, `LastKnownGood/Artifact`, and
`LastKnownGood/LegacyPublication`. Enrichment pairs are `Observed/Artifact`,
`Carried/Artifact`, `Carried/LegacyPublication`, and
`Unavailable/Unavailable`, with their fixed null/provenance constraints.

With source flags off, the legacy path is a complete zero-interaction bypass:
no claims, bundles, health or issue writes, uploads, or new failure paths.

### Ownership, continuity, and activation

The common writer owns the common source/cycle/bundle/health, Firebase
repository/model, coordinator/handoff, profile, and common-test reservation.
P1-04 owns its Club Elo source/module/tests/fixtures/mapping reservation;
P1-05 owns its roster source/module/tests/fixtures reservation.
`IBundesligaContextSourceObservationProvider` is the source-module extension
boundary. Only the serialized integration owner edits central DI, workflow
files/tests, ADR/packet, README, strategy, design, tasks, and snapshot. No path
is inferred from this role description.

Source flags default false. Immutable heads, the 16-job serial/default-success
topology, cron, non-cancelling concurrency, manual-only leaves, and no-bonus
boundary remain unchanged. P1-05 activation requires owner authorization for
scheduled acquisition, production cycle/health/context writes, issue permission,
rollback owner, and restoration. P1-04 additionally requires its accepted
source recipe and unattended-network/reuse approval. Rollback disables the
affected flag; restoration needs exact-head green CI, valid/no-change/rejection
evidence, all eight receipts, independent heads, issue reconciliation, and
copy compatibility without model/post work.

### Normative canonical appendix

All canonical JSON is compact UTF-8 without BOM or trailing newline. Every
stated property is present, in order; nullable values are explicit `null`.
Unknown, duplicate, missing, reordered, case-variant, or type-coerced
properties fail. Arrays are non-null and already ordered. SHA-256 is lowercase
64-hex; dates are `yyyy-MM-dd`; UTC times are second-precision
`yyyy-MM-ddTHH:mm:ssZ`; JSON byte lengths are non-negative Int64 and counts
non-negative Int32. Diagnostics are unique ordinal-sorted codes. `LP32` is an
unsigned four-byte big-endian length followed by bytes; `LP64` is unsigned
eight-byte big-endian length followed by bytes. `HashFields(domain, fields...)`
is SHA-256 of `LP32(UTF8(domain))` followed by LP32 of each raw field, with no
delimiter. Descriptor and observation digests hash standalone canonical JSON.

Cycle IDs are exactly `^gha:[1-9][0-9]{0,18}:[1-9][0-9]{0,18}$` for production
and `^local:[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$`
for development. Production is `gha:<github.repository_id>:<github.run_id>`;
development is a validated lowercase UUIDv7. Sequence is production run ID or
the UUIDv7 48-bit Unix-millisecond component. Watermark ordering is
`(sequence, cycleId ordinal)`. Bundle digest is SHA-256 of
`LP32("bundesliga-context-source-bundle-digest/v1")`, then each file's LP32
relative path and LP64 exact bytes, in manifest/payload order.

The production consumer list, in execution order, is exactly
`pes-squad-context`, `schadensfresse-context`, `relaxdays-tippt-context`,
`arena-sol-xhigh-context`, `arena-sol-high-context`,
`arena-luna-medium-context`, `arena-terra-xhigh-context`, and
`arena-luna-none-context`. Only `pes-squad-context` creates the cycle. Every
reusable call receives this literal list, current lane, scope, flags, and cycle
ID; a producer/list/order mismatch is fatal. Production artifact identity is
`bundesliga-context-source-bundle-<cycleStorageId>`, seven-day retention,
compression level zero, overwrite false.

Cycle document root order is `contract, competition, scope, cycleId,
cycleSequence, startedAtUtc, stalenessReferenceAtUtc, producerLaneId,
expectedConsumers, enabledSources, status, bundleSha256, artifactName,
abortCode, completedAtUtc`; source-cycle root order is `contract, competition,
scope, cycleId, source, attemptId, status, claimToken, claimedAtUtc,
leaseExpiresAtUtc, finalizedAtUtc, observationDigest, observation, abortCode,
receivedConsumers, completedAtUtc`. Claim tokens are lowercase UUIDv4 and the
lease is exactly ten minutes. The cycle root includes `UploadReserved`; source
status never does. Claim outcomes are `NewClaim`, `OwnedClaim`,
`ExistingFinalized`, `Busy`, and `ExistingAborted`; an absent document creates
NewClaim, same unexpired token is OwnedClaim, a different unexpired token is
fatal Busy without provider call, and an expired claim CASes source and cycle to
`Aborted/ACQUISITION_INTERRUPTED` without takeover. Finalize requires matching
token/Claimed; exact retry is a no-op and conflicts are fatal.

Producer order is validate/create cycle; claim enabled sources in source order;
acquire/finalize once; build/verify bundle; CAS to BundleVerified; bind digest
and fixed artifact name/CAS to UploadReserved; probe/upload/reverify/CAS to
HandoffReady; consume/publish producer head; persist receipt; project issue
after the durable transaction. Consumers download and fully verify before
source commands, then consume/publish and receipt. Upload or handoff failure
is fatal while any expected receipt is absent; only post-commit issue API
failure is nonfatal/Pending. `receivedConsumers` is always a prefix of the
literal list. A completed rejection is not an abort and is counted once only
after all receipts.

Receipt source-date order is `ratedAt, membershipCapturedAt,
membershipEffectiveAt, enrichmentCapturedAt`; carry order is `ageCount,
positionCount, marketValueCount, oldestFieldEffectiveAt`. A receipt's snapshot
is lowercase SHA. Elo selection is `NetworkAccepted`,
`NetworkCandidateRejected`, `NetworkCandidateStale`, or
`NetworkCandidateNotNewer`; roster selection is `DuckDbAccepted`,
`MixedPerClubSelection`, `CandidateRejected`, or `MetadataUnchanged`. Origins
are Elo `NetworkCandidate|LaunchSeed|LastKnownGood` and roster
`DuckDb|Mixed|FallbackSeed|LastKnownGood`. Elo has only non-null `ratedAt`;
roster has null ratedAt, non-null membership effective date, advertised
revision, and an enrichment capture date only where its unknown-date condition
does not require null. Zero carries have null oldest date; a legacy unknown
carry has null oldest date plus `ROSTER_ENRICHMENT_DATE_UNKNOWN`.

Health nested orders are: watermark `sequence, cycleId`; failures
`acquisition, membership, enrichment, handoff`; successful dates `ratedAt,
membershipEffectiveAt, enrichmentCapturedAt`; roster accepted `revision,
remoteIdentity, policySha256, descriptorSha256`; roster pending `revision,
remoteIdentity, policySha256, firstSeenCycleId, lastFailureCode`; community
selection `consumerLaneId, communityContext, selectedSnapshotId, selectedOrigin,
ratedAt, membershipCapturedAt, membershipEffectiveAt, enrichmentCapturedAt,
conditions`; issue projection `marker, title, bodySha256, desiredState,
appliedBodySha256, synchronizationStatus, lastAttemptedAtUtc, lastErrorCode`.
Elo roster state is null; roster ratedAt is null and roster revision state is
non-null. Accepted/pending may initially be null and cannot contain the same
revision/identity/policy tuple. `Synchronized` requires equal applied/desired
hash, timestamp, and null error; pre-attempt Pending has all application fields
null; failed Pending has timestamp/error and optional prior applied hash.

Acquisition increments for Elo rejection and roster Transport/Size/RemoteDrift/
Hash/Revision/Schema rejection; membership increments for SourceDate/Season/
Identity rejection or receipt membership rejection; enrichment increments for
roster receipt enrichment rejection; the latter two are always zero for Elo.
Other outcomes reset their respective counter; handoff increments once for a
current-watermark abort/supersession and resets on completion. Metadata-only
resets acquisition but repeats retained membership/enrichment; late cycles
change nothing. Issue opens at two or any stale/unknown condition, else closes.
Issue body bytes are LF-only/final LF: marker, `Competition:
\`bundesliga-2026-27\``, `Scope: \`production-live\``, source, watermark, and
ordinal condition bullets (or `- \`NONE\``); body SHA hashes those exact UTF-8
bytes. Its exact title is `[KicktippAi] Bundesliga 2026/27 <source>
context-source health`.

Roster descriptor null matrix is exact: MetadataUnchanged requires metadata
digest/length, before identity, and non-null retained descriptor/evaluation/
diagnostics; after/embedded/raw/date/payload are null. TransportRejected has
metadata/before both null before metadata, otherwise both required; all later
facts, retained fields, and payload null/empty. SizeRejected requires metadata,
before, observed length, null actual raw SHA, optional expected SHA; later facts
null. RemoteDriftRejected requires unequal before/after; HashRejected requires
equal identities and unequal actual/expected SHA; RevisionRejected requires
equal identities, raw length/SHA and null-or-unequal embedded revision;
SchemaRejected requires equal identities/revision/raw; SourceDateRejected adds
at least one null date; SeasonRejected/IdentityRejected require all dates;
Eligible requires all identity/revision/raw/date facts, expected SHA null or
equal, null retained scalars, empty retained diagnostics, and payload. Except
MetadataUnchanged, retainedDescriptorSha256 and retainedEvaluation are null and
retainedDiagnostics is `[]`.

Club Elo v2 adds, after v1's exact nine properties, `cycle_id, attempt_id,
source_observed_at, raw_sha256, raw_byte_length, provider_date_evidence,
name_mapping_sha256, source_rows`. Evidence order is `kind, recipe_id, field,
raw_value, rated_at`; rows are `team_slug, provider_name, global_rank, elo`.
It requires NetworkCandidate/NetworkAccepted, no diagnostics, descriptor/payload
equality, 18 unique canonical rows, exact mapping hash, evidence/rated-at
equality, and collected-at equal source-observed-at. V1 stays strict.

Roster v3 root is `contract, qualityReportCsv, sourceObservation, clubs`; its
source observation is `cycleId, attemptId, observedAtUtc, disposition,
observationDigest, bundleDigest, descriptorSha256, descriptor, payload`.
It embeds the full descriptor, validates its digest, requires ArtifactCaptured
and Eligible, validates payload identity, and reads no mutable state. Club order
is `teamSlug, selectedSource, membershipProvenanceKind, membershipCapturedAt,
membershipEffectiveAt, membershipAsOf, sourceReferences, selectedSourceRevision,
lastKnownGoodSnapshotId, attemptedDuckDbRevision, attemptedDuckDbEffectiveAt,
duckDbGateResult, selectionReason, diagnostics, members`; member order is
`role, name, transfermarktPlayerId, age, position, marketValueEur`; each
enrichment is `value, selection, provenanceKind, sourceUrl, sourceRevision,
sourceCapturedAt, fieldEffectiveAt, firstPublishedSnapshotId,
carriedFromSnapshotId`. Only `DuckDb/Artifact`, `FallbackSeed/FallbackSeed`,
`LastKnownGood/Artifact`, and `LastKnownGood/LegacyPublication` are valid
source/provenance pairs. Enrichment pairs are only `Observed/Artifact`,
`Carried/Artifact`, `Carried/LegacyPublication`, `Unavailable/Unavailable`.
Observed artifact fields require full source/capture/effective/first-published
facts and null carry; carried artifact retains all original facts and immediate
carry; legacy carry retains value/immediate carry with unavailable old facts
null; unavailable has every value/provenance field null. Historical v1/v2 bytes
are reactivated with their metadata/creation/predecessor unchanged; v3 metadata
is never attached to them.

## Consequences

- Common seam and P1-05 may proceed; P1-04 accepting-source work remains
  deferred pending its isolated source-contract decision.
- The predicted upper bound is seven cohesive reviewed milestones/pushes;
  source activation stays on separate draft branches.
- Validation covers canonical order/null/type/time/hash, hostile handoff paths,
  claim/CAS/replay, all receipts and health idempotency, disabled bypass,
  rejection plus synthetic roster takeover, v1/v2/v3 reconstruction, and
  unchanged workflow topology under one heavy-operation lease.

## Affected tasks

- [P1-04](../tasks/p1-04-club-elo-refresh.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)
- [P1-04/P1-05 context refresh](../designs/p1-04-05-context-refresh.md)
- [P1-04/P1-05 execution packet](../p1-04-05-execution-packet.md)

## Supersedes

None. This concretizes ADR-0073 without changing historical v1/v2 contracts.
