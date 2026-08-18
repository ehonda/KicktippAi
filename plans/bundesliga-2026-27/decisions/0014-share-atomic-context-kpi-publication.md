# ADR-0014: Share atomic context and KPI publication snapshots

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0011 requires the Bundesliga roster collector to publish 19 context documents and one KPI document as one complete snapshot. P0-11 needs the same guarantee for 18 per-team Club Elo context documents and one aggregate KPI document. The existing `IContextRepository` and `IKpiRepository` allocate and write versions independently, expose generic latest-version queries, and permit an existing context version to be mutated. Composing those repositories cannot make a mixed context/KPI set atomic or reconstruct a trustworthy last-known-good snapshot.

ADR-0011 described one head per community and competition plus separate visible latest-version pointers. That identity is insufficient once rosters and Club Elo are independently refreshed publication sets. This ADR replaces only ADR-0011's **Atomic publication** subsection; its roster schemas, ordering, content hash, quality, provenance, and source-selection rules remain accepted.

## Decision

### Shared boundary and reserved namespaces

Core defines one reusable context-plus-KPI publication repository. A repository instance is bound to one non-empty competition. Every operation also requires a non-empty community context and an immutable publication definition. The definition fixes the publication-set name and complete canonical document-key set; it is the API boundary for both publication and last-known-good reads, rather than a caller-supplied self-certified list.

Core owns the canonical Bundesliga definitions derived from the manifest and accepted contracts: `rosters` has the 18 `roster-{slug}` context documents plus `team-rosters` and `team-squad-summary`; `club-elo` has the 18 `club-elo-{slug}.csv` context documents plus `club-elo-rankings`. Callers must use those exact Core definition instances for those reserved names and cannot redefine them. A definition under any alternate publication-set name that contains even one reserved roster or Club Elo key is rejected; each reserved key belongs only to its owning canonical definition. Non-reserved publication definitions remain extensible. The Core scope type derives deterministic, scope-qualified IDs for the head and immutable snapshot metadata; metadata identity includes the content snapshot ID.

The following `bundesliga-2026-27` names are reserved for this boundary:

- `rosters`: context documents `roster-{slug}` and `team-rosters`; KPI document `team-squad-summary`;
- `club-elo`: context documents `club-elo-{slug}.csv`; KPI document `club-elo-rankings`.

The generic context/KPI repositories must reject writes or in-place updates to those reserved names. Generic exact-version reads remain available for diagnostics and reconstruction, but live consumers that require a coherent roster or Club Elo set must resolve it through the publication head. Generic latest-version queries are not a valid read path for reserved live context.

### Content and snapshot identity

A publication request contains its community context, expected previous snapshot ID, metadata JSON, and a document payload for each and only each key in its definition. Each document contains kind (`Context` or `Kpi`), name, exact valid UTF-8 content, and a non-empty description for KPI documents. Names are unique by kind and sort canonically by kind followed by ordinal name. Core copies input collections into immutable collections and keeps content as immutable text, so callers cannot mutate a request, transaction retry, or returned result through a retained byte array or list. UTF-8 is encoded strictly and deterministically at the hashing/persistence boundary, preserving exact valid UTF-8 byte identity.

Each entry stores `{kind,name,version,contentSha256}`. `contentSha256` is lowercase SHA-256 over the exact document bytes. The snapshot ID is lowercase SHA-256 over the canonical document sequence; kind, name, and exact content are each UTF-8 or raw bytes prefixed by a four-byte big-endian length. This preserves ADR-0011's roster snapshot identity.

Metadata JSON, publication time, descriptions, and previous snapshot ID do not affect content identity. An identical content set is therefore a no-op even if only metadata or descriptions changed.

### Firestore layout and transaction

Immutable payload versions remain in `context-documents` and `kpi-documents`. Shared immutable metadata lives in `document-publication-snapshots`, and one head per `(competition, communityContext, publicationSet)` lives in `document-publication-heads`. Head and metadata document IDs are deterministic scope hashes so values cannot create ambiguous paths. Snapshot metadata stores the full scope, snapshot ID, previous snapshot ID, creation time, metadata JSON, and ordered entries.

Publication uses a retry-safe Firestore transaction and requires `expectedPreviousSnapshotId` to equal the current head, including `null` for the first publication. The adapter first applies Core's pure CAS transition decision: compare expected and current heads; only then return `Unchanged` when target equals current, `Reactivated` when a valid target metadata row already exists, or `Published` otherwise. The transaction reads the head, its immutable snapshot and exact payload versions, any existing target snapshot, and the maximum existing version for every requested name before performing writes. It validates scope, the definition's ordered key set, entry hashes, snapshot ID, and payload identity.

Unchanged documents reuse the exact version referenced by the current valid head. Changed documents allocate above every existing version for that scoped kind and name, including versions not referenced by a publication head. The transaction creates changed payload rows and new immutable metadata, then switches the single head. A transaction, validation, or concurrency failure cannot advance the head or expose a partial set through the snapshot read boundary.

If the requested content is the current snapshot, publication is a no-op. If the same immutable content snapshot already exists in the same scope but is not current, the repository may reactivate it after fully validating its metadata and exact payloads; immutable creation metadata is not rewritten, so its `previousSnapshotId` remains the predecessor from its first creation rather than an event log of later head movements.

### Last-known-good reads

A last-known-good read takes the publication definition and runs through one consistent Firestore transaction: head -> immutable snapshot -> every exact context/KPI version named by its ordered entries. Each returned payload row retains its competition, community, and publication-set scope. One Core validation entry point takes the exact expected competition, community, and definition, then fails closed on a missing/extra row, wrong scope or set, wrong kind/name/order/version, hash mismatch, or recomputed snapshot-ID mismatch.

Collectors supply the Core definition to the shared repository. Roster validation remains owned by the ADR-0011 contract. P0-11 owns the Club Elo document schemas while Core owns its canonical required 19-document definition. Dry-run performs construction, publication-contract validation, hashing, and reporting without calling the repository.

## Alternatives considered

- **Keep separate roster and Club Elo publishers:** Rejected because both need the same cross-collection transaction, concurrency, version reuse, and verified last-known-good behavior.
- **Add independent latest pointers per document:** Rejected because readers could observe a partially advanced set and would have no single compare-and-swap boundary.
- **Treat generic maximum-version queries as last known good:** Rejected because unrelated or partial uploads can straddle a publication and do not prove set completeness.
- **Copy immutable payloads into snapshot metadata:** Rejected because it duplicates large prompt content and abandons the existing version collections and exact-version tooling.
- **Allow metadata-only snapshot revisions:** Rejected because roster snapshot identity is already defined by exact prompt bytes and identical publications must be retry-safe no-ops.

## Consequences

- P0-09 and P0-11 share one implementation and can refresh independently through separate heads.
- Existing WM26 collection and generic non-reserved document workflows remain unchanged.
- Bundesliga match, bonus, verification, and reconstruction paths must use snapshot exact-version reads for reserved roster/Elo context when P0-12 and P0-13 integrate those documents.
- Hash validation detects direct payload mutation or corrupt metadata instead of silently treating it as last known good.
- Snapshot metadata is content history, not a complete publication-event ledger; reactivation does not rewrite immutable ancestry or timestamps.
- The adapter must preserve immutable snapshot ancestry during reactivation: it only moves the head and never replaces the existing metadata's original `previousSnapshotId` or creation time.

## Affected tasks

- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-16](../tasks/p0-16-question-aware-bonus-context.md)

## Supersedes

The **Atomic publication** subsection of [ADR-0011](0011-roster-snapshot-and-publication-contract.md). All other ADR-0011 decisions remain accepted.
