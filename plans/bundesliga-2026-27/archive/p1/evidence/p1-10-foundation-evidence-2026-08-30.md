# P1-10 foundation evidence — 2026-08-30

- Status: Historical integrated and preserved-branch evidence
- Live contract: [P1-10](../../../tasks/p1-10-schadensfresse-primary-community.md)
- Governing decisions: [ADR-0058](../../../decisions/0058-make-schadensfresse-a-competition-typed-primary.md), [ADR-0059](../../../decisions/0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md), and [ADR-0060](../../../decisions/0060-separate-generation-manifest-from-current-rules-attestation.md)

This record preserves completed planning, typed-foundation, rules-contract,
persistence-hardening, and manifest-lifecycle evidence that was formerly mixed
into the live P1-10 execution contract. It is historical evidence, not proof
that the target-primary route is complete or active.

## Contract-slice evidence

- ADR-0058 is accepted from evidence-backed decisions the Owner authorized on
  2026-08-30; it does not claim that the Owner reviewed a draft.
- Pre-`b0fd6b6` code inspection confirmed `Match` lacked a stable fixture ID
  and generic round/result-basis fields, `BonusQuestion` lacked a typed
  Bundesliga-season subcompetition, and the parser discarded round/penalty
  meaning outside WM26. The later typed implementation is preserved on the
  archival/full PR, not temporary recovered runtime after B.
- This planning slice made no external write, prompt promotion, production
  model call, forced prediction, POST, or schedule mutation. ADR-0058 separately
  authorized the immediate fail-safe repository workflow removal only.

## Typed-foundation evidence

- P1-10 commit `b0fd6b6` updated the checked-in
  `data/bundesliga-2026-27/schadensfresse-routing-seed.json` with exact
  `1662323362` and `1662323366` entries (`bundesliga` / `1. Spieltag` /
  `regularTime90Minutes`), the three evidenced CL question identities, all 111
  ordered option ID/text bindings, and canonical hash
  `81b1c6ab0a6ad3159fcafebcbf1e3525df2cdf8e1279369f2515f001176008e5`.
  This is truthful preserved archival/full-PR implementation evidence, not
  temporary recovered-runtime state after B.
- Core loader/classifier validation and captured parser tests rejected missing,
  duplicate, unknown, and drifted identities. Parsing retained a source round,
  penalty result basis, and stable bonus-question ID when exposed, without
  deriving a fixture ID or subcompetition from text, teams, or partition.
- Focused Core and KicktippIntegration TUnit gates passed locally; the full
  affected project gates also passed. Existing unrelated nullable warnings
  remained in the test projects.

## Rules-contract evidence

- Payload-safe authenticated evidence at SHA-256
  `4503636dba2e6d14cd276733dffd12a3d3acd344c85368417d0f9d50e7869e95`
  exactly reproduced ADR-0058's legacy normalized hash
  `b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`
  and proved that its keyword filter omitted both numeric scoring rows.
- Accepted ADR-0059 fixed the exact authenticated source/DOM failure gates,
  every v1 field/type/value, canonical System.Text.Json bytes and 822-byte
  contract, structured SHA-256
  `1fac1a26a539a8c20b5f71be6e6dccb622528fc8aa40cdea22e6b21d994d90`,
  and diagnostic scoring-table SHA-256
  `4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e`.
- The rules validator/publication slice was unblocked only by that Accepted
  contract. The historical digest remained regression evidence and no source,
  live publication, prompt, production, or schedule mutation occurred in this
  decision slice.
- Local validator/publication evidence (no authenticated production collection
  was invoked) covered the complete ADR-0059 negative matrix through
  systematic source, DOM, semantic-value, numeric, canonical-JSON, Markdown,
  publication-identity, freshness, and numeric-drift mutations. Focused gates
  passed for the provider matrix (21/21, 1.169s), Core rules contract (5/5,
  0.864s), ordinary publication including unchanged and interleaved
  different-to-original concurrency (3/3, 2.495s), full atomic publication
  (1/1, 2.701s), future-generation preflight (1/1, 0.790s), and Firebase atomic
  result/concurrency behavior (8/8, 36.391s).
- Full affected suites passed via `dotnet run --project tests/Core.Tests`
  (316/316, 0 failed, 0 skipped, 3.233s), `dotnet run --project
  tests/ContextProviders.Kicktipp.Tests` (74/74, 0 failed, 0 skipped, 1.899s),
  `dotnet run --project tests/Orchestrator.Tests` (1195/1195, 0 failed,
  0 skipped, 1m 54.711s), and `dotnet run --project
  tests/FirebaseAdapter.Tests` (302/302, 0 failed, 0 skipped, 54.935s). Each
  report was under its project `bin/Debug/net10.0/TestResults/` directory; no
  Integration project source was affected by this slice.
- The command path authenticated/extracted before target markdown collection,
  validated the checked-in semantic/content identities before publication, and
  published the target through an atomic transaction in both ordinary and full
  modes. That transaction returned the independently selected effective
  immutable version even for unchanged content while retaining
  created-version/null compatibility, and the command read back only that exact
  version by expected name and content hash; it never resolved the target
  through floating latest. The checked-in Markdown was pinned to repository LF
  bytes by an exact-path attribute and verified as 763 bytes with SHA-256
  `f943f4b8f19d69dd1fc378d5684a2fdf7f59596accab4aa25866f81889b3e709`.
  The live blocking-successor contract deliberately prevents this non-Firebase
  slice from claiming resolved-manifest persistence or DFB/CL generation readiness.

## Persistence-hardening evidence

- Typed match and bonus rows bound exact canonical season identity. Bonus
  identity included question text, deadline, maximum selections, and every
  ordered option ID/text pair; typed current reads required complete immutable
  provenance, while legacy APIs retained their old behavior and excluded typed rows.
- Typed DFB-Pokal and Champions League persistence remained fail closed until
  `resolvedTypedContextManifest` exists. Typed cancelled-match lookup was
  available explicitly, without changing the legacy cancelled-match contract.
- Typed initial match saves plus typed initial and repredicted bonus saves used
  transactional deterministic allocation. Concurrent writers could not create
  a duplicate semantic index, approved reprediction limits remained
  enforceable, and subcompetition, stable identity, and model-configuration
  scopes remained isolated. Any pre-existing duplicate full-provenance
  exact-config typed index failed closed across current, cancelled, copy, and
  reprediction paths. Legacy and WM26 persistence paths were unchanged.
- Focused Firebase identity/concurrency and duplicate-corruption coverage
  passed 9/9. The exact full Firebase gate passed with `dotnet run --project
  tests/FirebaseAdapter.Tests` (301/301, 0 failed, 0 skipped, 1m 12s 712ms) and
  `dotnet run --project tests/Core.Tests` (311/311, 0 failed, 0 skipped, 3s 477ms).

## Manifest-lifecycle decision evidence

- Accepted ADR-0060 preserved the exact field
  `bundesligaSeasonSubcompetition`, fixed `rulesObservedAt` to canonical
  100-nanosecond UTC text, and defined freshness as the inclusive interval from
  the evaluation instant through exactly 24 hours old.
- The prediction's generation-time resolved manifest is immutable. A separate
  publication binding is directly addressed by the exact season/community/
  profile/routing-seed tuple and binds one exact immutable document plus the
  structured rules schema, hash, and current authenticated observation.
- Re-attestation refreshes only binding-scoped rules/profile/seed/document
  identity. Per-prediction reuse separately compares the current typed
  invocation, exact pinned prompt route, and model/service configuration with
  immutable prediction provenance before allowing zero model calls and zero
  prediction mutation. Trace and verification distinguish generation from
  current observation; drift and legacy state fail closed, and ADR-0058's Owner
  replacement/cost/force/cutoff gate remains.
- This accepted planning decision made no fixture seed or prompt decision,
  performed no production or external write, and did not activate a schedule.
