# ADR-0011: Fix roster snapshots and atomic publication

- Status: Accepted
- Date: 2026-08-16

## Context

ADR-0003 selects quality-gated DuckDB membership per club, with a complete checked-in fallback and last-known-good behavior. It deliberately did not define the repository seed schema, generated document schemas, numeric quality thresholds, freshness semantics, or the storage boundary needed for atomic publication. Without one contract, P0-08 and P0-09 would have to make incompatible data and failure choices independently.

The historical WM26 lineup implementation remains useful for parsing, safe enrichment, CSV formatting, and freshness concepts. Its header-only team behavior and sequential per-document writes are not the Bundesliga contract.

## Decision

### Fallback membership seed

The fallback lives at `data/bundesliga-2026-27/rosters/roster-membership-seed.csv` with this exact header:

```csv
Team_Slug,Role,Name,Transfermarkt_Club_Id,Transfermarkt_Player_Id,Membership_Source_Url,Membership_As_Of
```

`Team_Slug` joins exactly to ADR-0010. `Role` is `Coach` or `Player`. Names are non-empty Unicode text normalized to Form KC with leading, trailing, and repeated whitespace removed for validation and ordering. Club and player IDs are positive integers when known; unknown IDs are empty rather than guessed. A supplied club ID must match the manifest, and a coach has no player ID. Every source is an authoritative absolute HTTPS membership URL. Dates use `yyyy-MM-dd`, and every row for a club has the same date.

The seed contains exactly the manifest clubs, exactly one primary coach and 20 through 40 players per club. A name is unique within a club, player IDs are unique across the complete seed, and a cross-club name collision without IDs produces an explicit review diagnostic rather than an automatic identity join. Rows sort by manifest slug using ordinal comparison, coach before players, normalized name using ordinal comparison, then player ID.

### Prompt documents

Each `roster-{slug}` context document and the `team-rosters` context aggregate use:

```csv
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
```

`Team` is the exact manifest Kicktipp name. `Data_Collected_At` is the selected membership snapshot date, not the command execution time. The coach row has `N/A,Coach,N/A` for age, position, and market value. Player positions normalize to `Goalkeeper`, `Defender`, `Midfield`, or `Attack`. Age is the integer age on the membership date. Market value is the latest positive value at or before the selected snapshot date and uses dot thousands separators. Missing supplemental data is `N/A`; `0` never means unknown. Each club sorts coach first and then players by normalized name and stable ID. The aggregate concatenates club bodies in manifest-slug order under the same single header.

The `team-squad-summary` KPI aggregate uses:

```csv
Team_Slug,Team,Data_Collected_At,Membership_Source,Coach,Squad_Size,Known_Age_Count,Average_Age,Valued_Player_Count,Total_Market_Value_EUR,Median_Market_Value_EUR
```

It has one row per club in manifest-slug order. Source is `DuckDB`, `FallbackSeed`, or `LastKnownGood`. Counts may legitimately be zero. Average age uses known player ages and one invariant decimal. Value aggregates use positive known player values; an even median is the mean of the middle values rounded to whole EUR away from zero. Aggregates with no known input are `N/A`.

The reusable checked-in and runtime quality report uses:

```csv
Team_Slug,Team,Selected_Source,Membership_As_Of,Source_References,Source_Revision,Last_Known_Good_Snapshot_Id,DuckDB_Snapshot_As_Of,Player_Count,Coach_Count,Stable_Player_Id_Count,Known_Age_Count,Known_Position_Count,Valued_Player_Count,DuckDB_Gate_Result,Selection_Reason,Diagnostics
```

It has one slug-ordered row per club. References are unique ordinal-sorted URLs joined by ` | `. Unknown report values are `N/A`. Gate result is `PASS`, `REJECTED`, `NOT_AVAILABLE`, or `NOT_EVALUATED`. Diagnostics are stable ordinal-sorted machine codes joined with `;`, or `NONE`. This is an audit artifact and snapshot metadata, not a third prompt document.

Every CSV is UTF-8 without a byte-order mark, starts with its header, uses CRLF, contains one record per line, and ends with CRLF.

### DuckDB takeover gates

DuckDB replaces a club's trusted reference only when every gate passes:

1. The command supplies a non-empty dataset revision and snapshot date; filesystem timestamps are not provenance.
2. The snapshot is not future, is at most 14 calendar days old, and is not older than the trusted reference membership date.
3. Exactly one club record matches the manifest Transfermarkt ID, with domestic competition `L1` and `last_season=2026`.
4. Every player row explicitly has `last_season=2026` and `current_club_id` equal to the manifest ID. Games and transfers do not infer membership.
5. There are 20 through 40 distinct players, and the club's positive declared squad size equals that count.
6. Every player has a positive unique Transfermarkt ID and non-empty unique normalized name.
7. There is exactly one non-empty primary head coach; assistants are excluded. A missing coach rejects DuckDB rather than mixing membership sources.
8. Relative to the newest trusted fallback or last-known-good reference, player count stays between `max(20, ceil(reference * 0.75))` and `min(40, floor(reference * 1.25))`, inclusive, and at least 50 percent of reference player identities overlap. Identity uses player ID first and normalized name only when the reference ID is absent.
9. The final selected set contains exactly the 18 manifest clubs, and every supplied non-empty player ID is globally unique.

Age and position coverage below 80 percent and market-value coverage below 50 percent produce diagnostics, not membership rejection. Missing enrichment never removes a valid member.

### Selection, freshness, and last known good

The seed and the previous complete published snapshot validate independently. The newer valid one is the trusted reference; a date tie prefers last known good. Valid DuckDB wins automatically. Rejected or unavailable DuckDB retains the trusted reference without routine human approval. A newer reviewed seed may supersede older last known good.

An enrichment query or schema failure retains available last known good rather than publishing a degraded replacement. A successful query with individual missing supplemental cells remains publishable with `N/A` and diagnostics. Initial publication may use a complete seed with unavailable enrichment.

Fallback and last-known-good membership do not hard-expire. Freshness diagnostics use the strongest mutually exclusive bucket: age 15 through 30 days emits `STALE_MEMBERSHIP_GT_14_DAYS`, and age over 30 days emits `STALE_MEMBERSHIP_GT_30_DAYS`. Production activation requires every selected membership date to be at most 30 days old. Publication time is separate metadata and never masquerades as source freshness.

### Atomic publication

P0-09 builds and validates all 20 prompt documents in memory: 18 `roster-*` context documents, `team-rosters`, and the `team-squad-summary` KPI. A snapshot ID is lowercase SHA-256 over the required ordered document kind, name, and exact bytes, with each field encoded as UTF-8 and prefixed by a four-byte big-endian length.

A dedicated repository publishes changed versions, immutable snapshot metadata, all visible latest-version pointers, and one `(community, bundesliga-2026-27)` head in one Firestore transaction. Metadata records the previous snapshot, the exact 20-document version/hash map, publication time, and 18 provenance rows. Last known good is reconstructed only from that complete head, never from independently read latest documents. Unchanged content reuses its previous version; a fully unchanged set is a no-op. Validation, transaction, or concurrency failure cannot advance the head or make a partial set visible. Dry-run performs selection, validation, reporting, and hashing with no writes.

## Alternatives considered

- **Copy the WM26 contract unchanged:** Rejected because it admits header-only teams and publishes documents sequentially.
- **Use enrichment coverage as a membership gate:** Rejected because missing supplemental data does not invalidate authoritative membership and ADR-0003 requires `N/A` preservation.
- **Use transfer events or names to infer current membership:** Rejected because neither is a stable current-season identity boundary.
- **Hard-expire fallback and last known good:** Rejected because retaining a visibly stale complete snapshot is safer than publishing a partial or suspicious replacement.
- **Approve every roster refresh manually:** Rejected by ADR-0003; conservative numeric gates provide the automatic path.

## Consequences

- P0-08 can author and audit the seed without choosing fields or ordering.
- P0-09 has deterministic schemas, source selection, diagnostics, hashing, and failure behavior.
- Firestore publication needs a dedicated transactional repository rather than the historical sequential context/KPI interfaces.
- Conservative gates may retain fallback during a legitimate unusually large squad change; a reviewed newer seed remains the recovery path.

## Affected tasks

- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-08](../tasks/p0-08-roster-membership-seed.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

None. This makes ADR-0003's quality and publication policy concrete.
