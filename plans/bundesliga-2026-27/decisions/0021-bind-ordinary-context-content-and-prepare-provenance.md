# ADR-0021: Bind ordinary context content and prepare provenance

- Status: Accepted
- Date: 2026-08-20

## Context

ADR-0020 records an exact ordinary-document version for each Bundesliga match prompt, but its original contract did not prevent two writers from allocating and overwriting the same version. It also did not bind an ordinary entry to its exact bytes, so an out-of-band mutation could make a recorded version reconstruct with different prompt content. The generic context mutation API made that risk explicit.

P0-12 also introduced immutable 2026/27 reconstruction for prepared experiment artifacts. The current outcomes-only prepare commands do not load a stored prediction and therefore cannot supply a manifest and prediction timestamp. They must not emit unsafe timestamp-only 2026/27 items, while a manually populated, file-backed artifact with complete provenance must be usable now.

## Decision

Every ordinary context version is append-only. `SaveContextDocumentAsync` allocates a changed-content version inside one Firestore transaction and creates, never overwrites, its payload. The mutable version-update API is removed rather than retained as an escape hatch. Historical rows are not migrated or deleted.

Exact ordinary reads validate the full stored envelope before returning content: deterministic row identity, requested competition, community, document name, empty publication-set scope, and requested version where applicable. A malformed envelope fails closed.

Every `ResolvedMatchContextDocument` records its lowercase SHA-256 content hash as well as kind, name, and version. Live resolution computes the hash from the exact resolved document. Recorded reconstruction requires both exact identity and hash equality for ordinary entries, and exact version/hash equality against the validated publication payload for roster and Club Elo entries. Thus a direct post-prediction payload mutation cannot silently alter a reconstructed prompt.

Until P1-06, the current outcomes-only `prepare-*` command families remain fail-closed for `bundesliga-2026-27`: they require explicitly supplied prediction provenance and do not synthesize it from timestamp lookup. A populated file-backed artifact containing the canonical manifest, matching scope, and prediction creation time is an executable P0-12 input and must round-trip through its serialization contract. P1-06 owns automatic provenance sourcing from stored predictions for prepare commands.

## Alternatives considered

- **Keep mutable historical rows and store versions only:** Rejected because it cannot prove that reconstruction uses the original ordinary bytes.
- **Restrict append-only behavior only to the Bundesliga adapter instance:** Rejected because no production caller needs in-place context updates, while one global append-only repository contract avoids a legacy mutation escape hatch.
- **Let outcomes-only prepare commands infer provenance from current heads or timestamps:** Rejected because that bypasses immutable reconstruction and can drift after a refresh.
- **Defer all file-backed 2026/27 artifacts until P1-06:** Rejected because a complete explicit artifact is already reconstructable and must remain a viable controlled workflow.

## Consequences

- New ordinary context content produces a new version; consumers needing a correction publish replacement content rather than editing history.
- Corrupt, missing, scope-mismatched, or hash-mismatched context fails reconstruction and blocks unsafe reuse.
- Existing historical Firestore payloads remain untouched; legacy paths retain their explicit historical contracts.
- P0-12 must prove a populated file-backed artifact round-trip, while P1-06 later adds automatic provenance acquisition.

## Affected tasks

- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P1-06](../tasks/p1-06-observability-datasets.md)

## Supersedes

Only the ordinary-document version and content-identity portions of [ADR-0020](0020-record-immutable-match-context-manifests.md). ADR-0020's roster/Club Elo publication-snapshot contract and all other decisions remain accepted.
