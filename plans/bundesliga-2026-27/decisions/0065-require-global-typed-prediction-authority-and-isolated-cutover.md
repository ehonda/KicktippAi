# ADR-0065: Require global typed prediction authority and isolated cutover

- Status: Accepted
- Date: 2026-08-31
- Decision authority: Project Owner selected literal season-wide typing on
  2026-08-31 after reviewing the target-scoped compatibility alternative

## Context

ADR-0058 requires every new `bundesliga-2026-27` prediction row to carry
complete typed identity and rejects legacy rows from current use. The restored
P1-10 implementation enforced that storage invariant before ordinary
communities could supply complete fixture identity, causing the live
`pes-squad` route to fail before model or POST work. A target-only exception
would restore that route, but would leave every other community dependent on
team, time, text, or top-level partition assumptions and would fail again if
another community later mixes Bundesliga, DFB-Pokal, or Champions-League
items.

The Owner therefore selected literal global typing rather than a
Schadensfresse-only compatibility firewall. That decision expands the shared
identity, persistence, command, and cutover foundation for the complete
Bundesliga 2026/27 season partition. It does not expand P1-10's ownership of
Schadensfresse prompt/context composition or production activation, and it
does not change ADR-0062's temporary recovery runtime, sunset, fallback, or
rollback authority.

## Decision

### Canonical communities and item identity

Use these terms consistently:

- **Posting Community** is the Kicktipp community whose live form is read and
  written.
- **Prediction-source Community**: The community under which the candidate
  prediction was generated and stored. It equals the Posting Community for
  self-contained generation; for an accepted copy it may differ and is
  identified by the Copy Binding.
- **Community Context** is the explicit rules and context owner used for
  generation. It is not a posting or prediction-source identity.
- **Posting Item Identity** and **Source Item Identity** are the exact local
  fixture or question identities in their respective communities.

Kicktipp IDs are local to a posting community. Multiple model participants in
`ehonda-ai-arena` share one posting-community identity namespace; model or
credential profiles do not create additional item namespaces.

The **Stable Local Item Key** is exactly the tuple `(season partition, posting
community, item kind, Kicktipp item ID)`. It survives a reschedule or other
semantic correction. A separate versioned **Snapshot Hash** binds the mutable
semantic state for that key:

- a match snapshot binds subcompetition, exact round, result basis, teams,
  matchday, and scheduled instant; and
- a bonus snapshot binds subcompetition, exact question text, deadline,
  maximum selections, and the complete ordered option ID/text array.

Semantic drift changes the snapshot hash and invalidates reuse. It never
fabricates a new stable item key or permits lookup by teams, time, text, form
name, prefix, or partition alone.

The canonical match scheduled instant comes only from exact ID-bearing
fixture evidence joined to the same fixture ID's structured detail `Termin`.
Cancelled or empty evidence, an inherited prior-row value, `Instant.MinValue`,
another missing-value sentinel, duplicate evidence, an unparsable `Termin`, or
conflicting fixture/detail evidence is not a scheduled instant. Any such item
fails the whole selected operation before current read, prompt fetch, service
construction, model activity, mutation, or POST. A same-ID reschedule preserves
the Stable Local Item Key, rotates the Snapshot Hash in a new additive Identity
Seed Generation, and makes the prior snapshot non-current.

### Prediction-authoritative boundary

Every Bundesliga 2026/27 `Matchday`, `RandomMatch`, `VerifyMatchday`, `Bonus`,
and `VerifyBonus` operation, including development wrappers and copy paths
that reach those operations, must use one shared typed authority boundary.
That boundary requires:

1. a complete typed snapshot from the posting community;
2. the pinned immutable identity-seed generation for that posting community;
3. exact typed current-read, save, reprediction, and copy operations addressed
   by authority epoch plus stable item key and snapshot hash;
4. complete immutable prompt, service, context, source, seed, and copy
   provenance; and
5. exact-ID Kicktipp POST followed by exact-ID readback from the posting
   community.

The five operations may not call legacy current APIs. Team/start, team-only,
question-text, form-name, substring, prefix, newest-row, cancelled-match
team-only, top-level partition, or default classification cannot select a
current prediction or Posting Item Identity. Repository methods that retain
those shapes are historical, context, audit, or cost surfaces only. They cannot
feed a current operation, a copy candidate, a reprediction index, or a POST.

Each command first materializes and classifies its complete selected
inventory. `RandomMatch` classifies the complete candidate set before random
selection. Matchday and bonus operations classify every item in their command
scope. An explicit supported subcompetition filter may exclude items before
processing; otherwise any unsupported, unknown, duplicate, conflicting, or
unbound item fails the whole operation before current database selection,
prompt fetch, service construction, model activity, mutation, or POST.

### Immutable seed generations and copy bindings

Each posting community has immutable, versioned identity-seed generations
created from exact authenticated fixture and question evidence. A generation
binds every supported item to its stable local key, current snapshot hash,
subcompetition, and route classification. Generations are additive and never
rewritten; runtime configuration pins one exact generation and content hash.
An ID missing from the pinned generation is not current-authoritative.

The **Copy Binding** is a separate immutable, versioned, one-to-one mapping
between one exact posting item identity and one exact source item identity. It binds
both stable item keys, both snapshot hashes, both pinned seed generations,
the prediction route, and, for bonus questions, a total one-to-one projection
between exact source and posting option IDs. Duplicate targets, duplicate
sources, partial option maps, or many-to-one mappings fail validation. The
binding proves correspondence only; copy compatibility separately requires
the accepted scoring, result-basis, question, prompt/model, and copy-policy
contract.

A compatible copy creates a new typed target record in the posting
community's authority namespace. It maps the accepted prediction through the
binding, records the exact source prediction identity and immutable source
provenance, and records zero target model activity. It never clones a stored
payload, changes a source record, repairs a legacy row, or invents missing
metadata.

### Complete generation provenance

Every Typed Current Prediction records immutable **Generation Provenance** for:

- exact model ID, reasoning effort, and output-token cap;
- prompt source, name, immutable version, normalized content hash, required
  label readback, and the actual checked-in fallback identity when used;
- requested service tier, final service tier, and fallback outcome;
- posting community, prediction-source community, and community context;
- posting and source stable item keys and snapshot hashes;
- pinned posting/source seed generations and hashes, plus the exact copy
  binding and source prediction identity when applicable;
- immutable context and rules manifests; and
- authority epoch and physical storage namespace.

This global provenance extends rather than weakens ADR-0059/0060. The
generation-time manifest remains immutable; any separately refreshable current
rules attestation retains ADR-0060's exact boundary.

### Isolated staging, cutover, and rollback

Typed preparation uses a required immutable **Authority Epoch** and physically
separate match, bonus, and item-snapshot collections. The first epoch is
`bundesliga-2026-27-typed-v1`; its collections are exactly
`match-predictions-bundesliga-2026-27-typed-v1`,
`bonus-predictions-bundesliga-2026-27-typed-v1`, and
`matches-bundesliga-2026-27-typed-v1`. Every stored row repeats the epoch and
is rejected if it does not match the repository's configured namespace.

Recovery `main` continues to read only the existing legacy collections. The
draft route reads and writes only the typed epoch. No query, fallback,
enumeration, copy, or reprediction operation spans the two authorities. Legacy
Rows remain available through separate configured, explicitly
authority-labelled historical/audit/cost reads that materialize non-current
DTOs. Each authority is retrieved independently; only a later shared combiner
may combine, sort, or total the results, and it must retain every row's
authority label plus per-authority subtotals. No cross-authority repository
method, query, enumeration, current lookup, fallback, copy, or reprediction is
permitted. Legacy Rows are never mutated, deleted, backfilled, or promoted into
the typed epoch.

Cutover is one reviewed operational unit: complete typed staging and
read-only evidence first; confirm no active or pending affected run; freeze
posting; deploy the runtime/workflow that selects the typed epoch; switch the
storage authority used by every affected current operation; then perform
exact-ID verification before admitting normal execution. Merging Git alone is
not cutover and no community may switch independently.

Before any typed POST, a failed cutover may restore the complete ADR-0062
recovery authority. After any typed POST, disable the affected lane and
reconcile every exact Kicktipp item plus typed storage before legacy posting
can resume. Schadensfresse re-quarantines and the seven unaffected recovery
pairs remain the safe fallback. Lane-wide risk uses ADR-0053's inherited
whole-cron disablement. Partial or mixed authority is never a rollback mode.

### Ownership and authority

P1-13 owns this season-wide identity, seed, copy-binding, provenance, typed
repository, exact-ID Kicktipp, command-kernel, staging, and atomic-cutover
foundation. P1-10 depends on P1-13 and retains all Schadensfresse-specific
rules, target-owned context, prompt routes, DFB/CL composition, replacement
set, cost/call limits, UTC cutoff, and primary activation.

R4b may add only DFB/CL route IDs and contracts, fail-closed dispatch, and
synthetic tests. It may not add a DFB/CL prompt body or mirror, assert an
unverified prompt hash, or imply fallback availability. A checked-in mirror
and equality test are admitted only after evidence records the exact hosted
prompt name, numbered immutable version, normalized readback hash, and required
`production` membership; the later test must prove mirror/readback equality.

Repository-only implementation after this Accepted decision needs no further
Owner choice. Real authenticated seed/binding evidence, immutable prompt
promotion, production calls or writes, force/reprediction, copied-row
replacement, schedule change, typed staging against production, and atomic
cutover remain behind the existing Owner and evidence gates. If ADR-0062's
fixed sunset is missed, Schadensfresse is re-quarantined and seven unaffected
pairs continue; P1-13 does not extend the sunset.

## Alternatives considered

- **Target-scoped compatibility firewall:** Rejected because it would let
  ordinary communities remain prediction-authoritative without stable IDs and
  would recreate the same ambiguity when another community mixes
  subcompetitions.
- **Backfill or repair legacy rows in place:** Rejected because reconstructed
  identity cannot prove what the original prediction used and would erase the
  authority boundary needed for rollback.
- **Use team/time or question text as current identity:** Rejected because
  reschedules, repeated pairings, text edits, and community-local IDs make
  those fields ambiguous.
- **Share the legacy collections and filter by a typed flag:** Rejected because
  an omitted predicate, prefix query, or fallback could mix authorities during
  staging or rollback.
- **Merge code first and migrate communities incrementally:** Rejected because
  deployed runtime and storage could disagree and allow partial posting from
  incompatible authorities.

## Consequences

- Every Bundesliga production and validation row needs complete exact item
  evidence before it can use the typed current path.
- Shared Core, Kicktipp, Firebase, command, data, workflow, and test surfaces
  expand before P1-10 can complete its target-primary route.
- Legacy data remains inspectable but cannot silently satisfy current reuse,
  reprediction, copy, verification, or posting.
- Pre-staging can proceed without affecting recovery production; final cutover
  is more operationally constrained because it is deliberately all-or-nothing.
- P1-10 remains a bounded consumer/composer of the global foundation rather
  than becoming its owner.

## Affected tasks

- [P1-13](../tasks/p1-13-global-bundesliga-prediction-authority.md)
- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)

## Supersedes

None. This decision generalizes ADR-0058's typed new-write and current-use
requirement across every Bundesliga 2026/27 community and supplies the shared
authority/cutover prerequisite. ADR-0058/0059/0060 remain the exact
Schadensfresse domain, rules, and attestation contracts; ADR-0062 remains the
temporary recovery runtime.
