# ADR-0020: Record immutable match-context manifests

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0014 makes roster and Club Elo publication sets coherent at their heads, but a later refresh can move either head. A prediction cannot be reconstructed by resolving a current head or a generic timestamp query without risking prompt drift. The older generic context path records document names only and previously allowed optional transfer reads.

## Decision

Every new `bundesliga-2026-27` match prediction persists an immutable `resolvedContextManifest` alongside its existing context-document names. It records the exact competition and community scope; the ordered eleven context entries `{ kind: Context, name, version }`; and the exact `rosterPublicationSnapshotId` and `clubEloPublicationSnapshotId`.

The manifest is a validated value object, not caller-owned JSON. Its competition is the canonical `bundesliga-2026-27` ID; scope and the ordered names must exactly match the persisted match, prediction community, and canonical catalog. Entries are unique `Context` entries with nonnegative exact versions, and both snapshot IDs are lowercase SHA-256 values. Stored JSON must retain the canonical field set and order; unknown, reordered, cross-scope, or otherwise noncanonical forms fail closed.

The seven ordinary documents (standings, community rules, recent home/away history, home history, away history, and head-to-head) retain generic/on-demand collection behavior. If an ordinary required document is generated on demand for a persisted prediction, the command must first save it, re-read the exact version, verify byte equality, and record that version. It fails clearly if this cannot be established.

The two `roster-{slug}` names and two `club-elo-{slug}.csv` names are reserved. Live reads load one semantically validated headed publication for each canonical set and select the two manifest teams from its exact payloads. They never use generic latest APIs. Reconstruction and experiments load the recorded publication snapshot IDs, validate the metadata and payload graph, and require the recorded reserved entry versions to match those snapshots. They never use current heads or `latest` for reserved context.

Legacy predictions without a manifest may reconstruct their historically supported non-reserved documents. A Bundesliga prediction whose stored context includes required roster or Club Elo documents but lacks a sufficient manifest fails clearly; it must not silently substitute current publication heads. Historical Firestore payloads are retained unchanged.

For Bundesliga, a prediction with a manifest is saved only through a provenance-capable persistence API. A normal or reprediction save failure blocks the corresponding Kicktipp submission. Outdated checks compare each ordinary exact version and the two semantically validated current publication heads with the manifest; a missing or corrupt manifest/head is outdated, never silently current.

## Alternatives considered

- **Use the snapshot heads at reconstruction time:** Rejected because a refresh changes the historical prompt.
- **Store only document names and a creation timestamp:** Rejected because it cannot prove a coherent roster/Elo set or recover an on-demand generic document precisely.
- **Keep transfers as optional historical fallback:** Rejected by ADR-0003; explicit callers may still supply immutable historical context outside the live Bundesliga contract.

## Consequences

- A Bundesliga match prompt has exactly eleven required documents: seven ordinary documents, two canonical roster documents, and two canonical Club Elo documents.
- Persisted predictions carry enough provenance to reproduce their resolved inputs after heads advance.
- Matchday, random-match, analyze-match, reconstruction, and experiment paths share the same catalog/resolver boundary.

## Affected tasks

- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P1-06](../tasks/p1-06-observability-datasets.md)

## Supersedes

None.
