# ADR-0051: Require an explicit launch roster enrichment overlay

- Status: Accepted
- Date: 2026-08-26

## Context

ADR-0050 introduced a pinned, coverage-gated launch publication, but its first
live arena use exposed an ambiguous source-selection contract. The ordinary
DuckDB path treats the database as a candidate source of roster membership. For
the audited 2025/26 artifact, all 18 membership candidates correctly failed the
current-season quality gates, so every quality row reported `LastKnownGood` and
`DUCKDB_REJECTED_USE_LAST_KNOWN_GOOD`. Supplemental data was nevertheless
overlaid by exact stable ID after membership selection.

The live command on exact main
`517db42ce66cb9554848230e176e104ddc87bb64` published headed v2 snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2` after
printing 464 known ages, 464 positions, and 450 valued players. A payload-safe,
read-only in-process reconstruction using the pinned database and headed
snapshot `0773e9baa4f73ced6e0f86e6eb4b513ef82669e0a80ed5180749a08ebc52a7fa`
reproduced the published hash and the same 464/464/450 coverage. The publication
was therefore enriched; `LastKnownGood` described membership provenance, not
loss of enrichment. Firestore was mutated by that publication and one
`collect-context` OTLP trace was emitted. No prediction workflow, model call,
Kicktipp prediction write, or schedule action occurred.

Even though the published bytes were correct, the launch contract was unsafe
and misleading: it evaluated a historical DuckDB membership candidate that the
operator did not intend to adopt, and the launch floor was checked on source
snapshots rather than after strict reconstruction of the exact serialized v2
publication.

## Decision

The one-time launch path is an explicit opt-in overlay:
`--launch-enrichment-overlay`. It is valid only when paired with
`--require-launch-coverage` and the exact pinned DuckDB path, SHA-256, upstream
revision, and snapshot date. Either launch flag without the other fails closed.

In overlay mode:

1. Membership is selected only from the authoritative headed last-known-good
   snapshot and the checked-in seed. The newest membership date wins, with
   last-known-good winning a same-date tie.
2. DuckDB membership is neither read, evaluated, nor adopted. Its membership
   gate is `NotEvaluated`, and every team records the stable
   `LAUNCH_ENRICHMENT_OVERLAY` diagnostic so logs state the actual operation.
3. Age, canonical position, and positive market value are overlaid only where a
   DuckDB player has the exact stable ID already present in the selected
   authoritative membership. The overlay cannot add, remove, reorder, rename,
   or otherwise replace members or their stable IDs.
4. The command constructs the exact v2 publication, strictly reconstructs that
   in-memory headed graph under the publication contract, and then validates
   the reconstructed final set before creating any Firestore write request. It
   must contain all 18 teams, exactly one valid final `Team Accumulated` row per
   team, and at least 464 known ages, 464 known positions, and 450 valued
   players. Any reconstruction or coverage failure writes nothing.
5. A launch collection result that asks to retain last-known-good is an error,
   not a successful launch publication.

The ordinary optional-DuckDB collector remains unchanged: when it is used for a
real membership candidate it continues applying ADR-0017's season, competition,
quality, and per-team fallback gates. Historical v1 reconstruction remains
strict, and normal v2 reconstruction retains ADR-0050's derived-row contract.

The local artifact remains an explicit operator input. Generic CI and reusable
context workflows do not acquire it or embed its machine-local path. P1-05
continues to own recurring acquisition, refresh, diff review, and automated
current-season roster adoption.

## Alternatives considered

- **Keep the implicit post-selection overlay and only rename log output:**
  Rejected because the launch intent would still unnecessarily evaluate a
  historical membership candidate and the safe boundary would remain unclear.
- **Treat the audited 2025/26 membership as current:** Rejected because it would
  replace authoritative launch membership with a wrong-season source.
- **Validate only source-model counts:** Rejected because the exact bytes sent
  to Firestore, including the 18 derived rows, are the publication boundary.
- **Make the overlay the default DuckDB behavior:** Rejected because ordinary
  current-season DuckDB collection still needs its existing membership quality
  and adoption semantics.

## Consequences

- Launch enrichment is explicit, payload-verified, and fails before Firestore
  mutation on any pin, reconstruction, derived-row, or coverage regression.
- Logs distinguish authoritative membership selection from supplemental
  enrichment and no longer imply that rejected DuckDB membership supplied the
  launch roster.
- The already-published arena snapshot remains valid enriched-v2 evidence, but
  P0-25's live ladder must republish through the corrected explicit mode before
  its one authorized Luna/none replacement round.
- P0-21 must use both launch flags for every initial production roster
  publication; that publication alone grants no prediction or schedule
  authority.

## Affected tasks

- [P0-25](../tasks/p0-25-roster-enrichment-and-team-total.md)
- [P0-21](../tasks/p0-21-production-activation.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Corrects

This decision corrects ADR-0050's launch-source selection and coverage-check
boundary. ADR-0050's v2 CSV, derived-subtotal, historical reconstruction, pin,
and P1-05 ownership decisions remain in force.
