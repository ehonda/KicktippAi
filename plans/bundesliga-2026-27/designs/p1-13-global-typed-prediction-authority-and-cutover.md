# P1-13 global typed prediction authority and isolated cutover design

- Status: Frozen R0 specification; implementation starts only after independent
  acceptance of the exact R0 commit
- Authority: [ADR-0065](../decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md)
- Depends on: [ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md),
  [ADR-0059](../decisions/0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md),
  [ADR-0060](../decisions/0060-separate-generation-manifest-from-current-rules-attestation.md),
  and [ADR-0062](../decisions/0062-temporarily-restore-schadensfresse-copy.md)

## Outcome and non-goals

P1-13 makes complete typed item identity and immutable prediction provenance
the only current authority for every Bundesliga 2026/27 prediction operation.
It supplies the shared foundation that P1-10 consumes for Schadensfresse's
target-owned Bundesliga, DFB-Pokal, and Champions-League routes.

P1-13 does not choose or promote a prompt, change a model or context policy,
collect real authenticated seeds in R0, call a model, replace a prediction,
POST to Kicktipp, mutate production Firestore, change cadence, activate a
schedule, alter WM26, delete/backfill legacy rows, or extend ADR-0062's sunset.
P1-10 retains Schadensfresse-specific composition and activation.

## Seam map and invariants

```text
authenticated posting-community inventory
  -> pinned identity-seed generation
  -> complete typed snapshots
  -> command-scoped inventory gate
  -> route/context/prompt/model resolution
  -> typed current read/save/repredict or exact copy binding
  -> immutable generation provenance in one authority epoch
  -> exact-ID Kicktipp POST
  -> exact-ID Kicktipp readback
```

- Posting Community, Prediction-source Community, and Community Context are
  three independent identities. Credential selection follows Posting
  Community and never Community Context or Prediction-source Community.
- **Prediction-source Community**: The community under which the candidate
  prediction was generated and stored. It equals the Posting Community for
  self-contained generation; for an accepted copy it may differ and is
  identified by the Copy Binding.
- A Stable Local Item Key does not change when an item is rescheduled or its
  semantic snapshot changes. Snapshot drift changes the Snapshot Hash and
  invalidates current reuse.
- A match scheduled instant is authoritative only when exact ID-bearing
  fixture evidence and the same-ID structured detail `Termin` agree. It is
  never inherited from another row or represented by a missing-value sentinel.
- IDs are local to a Posting Community. All `ehonda-ai-arena` participants
  share one posting-community seed, irrespective of model or credentials.
- Every current operation stays inside one Authority Epoch and one physical
  namespace. No current query can inspect legacy and typed rows together.
- A Legacy Row is historical, context, audit, or cost evidence only; it cannot
  be promoted, repaired, or selected as a Typed Current Prediction.
- Every item in the command's selected inventory is supported and bound before
  current selection or any side effect. There is no partial success for a
  mixed or unknown inventory.
- Copy correspondence, prediction compatibility, and authority to post are
  separate gates. Passing one cannot stand in for another.
- A typed target copy is a new target record with source provenance; no legacy
  or source record is repaired, cloned, or mutated.

## Call-surface classification

The classification is exhaustive for calls that can reach Bundesliga
prediction data. New calls must enter one row before implementation.

| Surface | Commands and API families | Permitted data | Forbidden use |
|---|---|---|---|
| Current-authoritative match | `matchday`, `random-match`, `verify`; their development wrappers; shared match copy route; typed match snapshot/current/save/reprediction/copy APIs; exact-ID match POST/readback | One pinned Authority Epoch; complete posting/source item keys and Snapshot Hashes; complete Generation Provenance | Any legacy current read, team/time or team-only selection, latest-row selection, cross-epoch query, or text/default classification |
| Current-authoritative bonus | `bonus`, `verify-bonus`; its development wrapper; shared bonus copy route; typed bonus snapshot/current/save/reprediction/copy APIs; exact-ID bonus POST/readback | Same typed authority, plus exact question identity and ordered option-ID projection | Question-text/form-name lookup, partial option mapping, latest-row selection, cross-epoch query, or legacy fallback |
| Historical and context | `collect-context*`, match-outcome/history collection, context/KPI resolution, historical experiment reconstruction, and explicit stored-item inventory APIs | Raw authenticated/history/context items and legacy records when clearly labelled | Selecting a current prediction, computing a current reprediction index, supplying a copy candidate, or posting |
| Audit and cost | `cost`, context/history audits, prediction inventories, experiment export/prepare/analysis, available-model/matchday/community discovery, and separate explicitly authority-labelled audit/cost reads | Non-current audit/cost DTOs retrieved independently from one configured authority, retaining authority labels and per-authority subtotals through later combination | Any cross-authority repository query/enumeration, current lookup/fallback/copy/reprediction, unlabeled union, or return to a production command |
| WM26 and other partitions | Existing competition-specific APIs | Their accepted competition contracts | Receiving `BundesligaSeasonSubcompetition`, P1-13 Authority Epoch, or Bundesliga seed/binding semantics |

`reconstruct-prompt` and experiment commands may reconstruct historical
generation evidence, but their result is never current-authoritative and
cannot be saved or posted through a production path. The current five command
families reject any repository implementation that lacks the typed capability;
they do not downcast to `IPredictionRepository` legacy methods.

## Exact typed boundary

R1 freezes these shared contracts before provider or persistence work begins.
Method names below are the canonical capability names; overloads may add only
`CancellationToken` or the same immutable request record.

### Authority and snapshot requests

`BundesligaPredictionAuthority` contains exactly:

1. `SeasonPartition` (`bundesliga-2026-27`);
2. `PostingCommunity`;
3. `PredictionSourceCommunity`;
4. `CommunityContext`;
5. `AuthorityEpoch` (`bundesliga-2026-27-typed-v1`);
6. posting and source Identity Seed Generation IDs and hashes; and
7. optional Copy Binding generation and hash, required only for copy.

`TypedMatchSnapshot` contains its Stable Local Item Key, Snapshot Hash,
subcompetition, exact round, result basis, home and away team identities,
matchday, and canonical scheduled instant. `TypedBonusSnapshot` contains its
Stable Local Item Key, Snapshot Hash, subcompetition, exact text, canonical
deadline, maximum selections, and complete ordered option ID/text array.
Snapshot hashes use separately versioned canonical records; equality of the
hash and byte-identical canonical record is required.

### Scheduled-instant evidence

The match scheduled instant is materialized only by joining exact ID-bearing
fixture evidence with the structured detail `Termin` for that same fixture ID.
The join requires exactly one parseable, non-sentinel value and agreement
between all same-ID evidence. The inventory gate rejects:

- a cancelled or empty fixture, including a cancelled first row;
- a cancelled row after a valid row, even if a prior timestamp is available;
- inherited prior-row state, `Instant.MinValue`, or any other missing-value
  sentinel;
- missing, duplicate, or unparsable detail `Termin`; and
- any fixture/detail ID or scheduled-instant conflict.

One rejected item rejects the complete selected command scope before typed
current read, prompt fetch, service construction, model call, mutation, or
POST. A reschedule for the same exact Kicktipp ID keeps the Stable Local Item
Key and produces a new Snapshot Hash in a new additive Identity Seed
Generation. The prior generation remains immutable and its old snapshot is no
longer current.

### Kicktipp capability

The posting-community client exposes only these typed authority operations to
the five current command families:

- `GetTypedOpenMatchSnapshotsAsync(authority, scope)`;
- `GetTypedPlacedMatchPredictionsAsync(authority, scope)`;
- `PlaceTypedMatchPredictionsAsync(authority, predictions, overrideExisting)`;
- `GetTypedOpenBonusSnapshotsAsync(authority, scope)`;
- `GetTypedPlacedBonusPredictionsAsync(authority, scope)`; and
- `PlaceTypedBonusPredictionsAsync(authority, predictions, overrideExisting)`.

Every returned and submitted entry is keyed by Stable Local Item Key and
Snapshot Hash. POST implementations address the exact Kicktipp item IDs from
those keys, never teams or form names. A successful POST is incomplete until
the corresponding placed-read method returns the exact same IDs, snapshots,
and accepted values. Missing, extra, duplicate, or changed readback fails and
enters reconciliation; it is not retried through a looser API.

### Repository capability

`IBundesligaTypedPredictionAuthorityRepository` is the only repository
capability available to current commands. It exposes these operation families
for both match and bonus snapshots:

- `GetCurrentTyped*PredictionAsync` and
  `GetCurrentTyped*PredictionMetadataAsync`;
- `HasCurrentTyped*PredictionAsync` and
  `GetCurrentTyped*RepredictionIndexAsync`;
- `SaveCurrentTyped*PredictionAsync`;
- `SaveCurrentTyped*RepredictionAsync`, with expected current index and hard
  maximum; and
- `GetTyped*CopyCandidateAsync` plus `SaveCurrentTyped*CopyAsync`, requiring
  the complete source/target authority and exact Copy Binding.

Every call requires `BundesligaPredictionAuthority`, the complete typed
snapshot, exact `PredictionModelConfig`, and, for saves, complete
`PredictionGenerationProvenanceV2`. Current reads match authority epoch,
posting community, source community, context, stable key, Snapshot Hash,
model configuration, route, and immutable provenance. Absence is not a signal
to query a legacy API. Reprediction allocation is transactional and scoped by
the same complete identity. Copy selection returns exactly one typed source
record or fails; target saving is transactional and records its own target
identity plus immutable source provenance.

The existing string, team/time, team-only cancelled, question-text,
all-predictions, stored-match, and latest methods remain outside this
interface. They are explicitly historical/context/audit/cost APIs even if
their current names do not yet say `Legacy`.

### Non-current audit and cost reads

Physical isolation applies to reads as well as current writes. R2b supplies
separate configured read capabilities for legacy and each typed Authority
Epoch. Each capability addresses exactly one physical namespace and emits
explicitly authority-labelled, non-current audit/cost DTOs. It cannot expose a
current prediction capability, accept multiple authorities, enumerate another
namespace, or participate in current lookup, fallback, copy, or reprediction.

The later shared slice owns a pure combiner. It invokes the isolated reads
independently and only after retrieval may concatenate, sort, or total their
DTOs. Combined output retains every authority label and a subtotal for each
authority; an overall total may be derived only from those labelled subtotals.
No repository method, database query, or collection enumeration spans
authorities, and no combined DTO can be converted back into a current record.

## Identity seeds and copy bindings

### Immutable files

Deterministic tooling writes, but never rewrites, these tracked artifact
families:

```text
data/bundesliga-2026-27/prediction-authority/
  identity-seeds/<posting-community>/generation-<NNNN>.json
  copy-bindings/<posting-community>--from--<source-community>/generation-<NNNN>.json
```

Community slugs use the existing lowercase path-safe validation. Generation is
a zero-padded positive integer. Each canonical JSON file declares its schema,
season partition, posting/source communities as applicable, generation, source
evidence identity, ordered entries, and its predecessor hash when generation
is greater than one. Runtime configuration pins both generation and lowercase
SHA-256; directory enumeration or a floating latest generation is forbidden.

An identity entry binds one exact Kicktipp ID, item kind, Stable Local Item
Key, complete canonical snapshot and Snapshot Hash, subcompetition, and route
classification. A copy entry binds one exact posting entry and one exact
source entry by their seed generations, stable keys, and Snapshot Hashes. A
bonus entry additionally contains the complete ordered one-to-one source-to-
posting option-ID mapping. Complete inventories reject duplicate IDs, duplicate
stable keys, duplicate copy endpoints, missing options, option reuse, or an
entry whose recomputed canonical bytes/hash differs.

R5a may implement schemas, generators, validators, and hostile fixtures using
synthetic data. R5b is the first milestone allowed to add real authenticated
seed/binding generations, and only after the evidence and Owner gates below.

### Compatibility decision

`PredictionCopyCompatibilityV2` consumes the exact binding, both canonical
snapshots, both communities' rules/result-basis contract, and the requested
model/prompt route. It returns either a complete typed projection or one
explicit failure reason; no degraded result exists. Match compatibility
requires equivalent posting semantics for the predicted result. Bonus
compatibility requires equal selection meaning and a total option projection.
The source record must be current under its own authority before compatibility
is evaluated.

## Generation provenance

`PredictionGenerationProvenanceV2` is canonical and immutable. It includes:

- authority epoch and physical namespace;
- posting, prediction-source, and community-context identities;
- posting/source stable keys and Snapshot Hashes;
- posting/source seed generations and hashes;
- route/profile identity and Copy Binding generation/hash/source-prediction ID
  for a copy;
- exact prompt source, hosted name, immutable version, normalized readback
  hash, required label membership, and actual checked-in fallback file/hash;
- exact model ID, reasoning effort, and output-token cap;
- requested and final service tier, whether fallback occurred, and its reason;
- immutable resolved context/rules manifests and document identities; and
- generation timestamp and prediction/reprediction identity.

For a direct generation, Prediction-source Community and source item equal the
posting values and copy fields are absent. For a copy, model token usage and
target generation cost are explicitly zero, source prediction provenance is
preserved as an immutable identity, and mapped target values are recorded.
Prompt fallback provenance records what actually ran; merely configuring a
fallback is not equivalent to using it.

### DFB/CL prompt admission

R4b defines DFB-Pokal match, Champions-League match, and Champions-League
bonus route IDs/contracts, fail-closed dispatch, and synthetic routing tests
only. No DFB/CL prompt body, checked-in mirror, mirror hash assertion, or
fallback availability claim belongs to R4b.

A later prompt-evidence slice may add a checked-in mirror and equality test
only after payload-safe evidence records the exact hosted prompt name, exact
numbered immutable version, normalized hosted readback hash, and membership of
the required `production` label for that same version. The test then proves the
normalized checked-in mirror equals the authenticated hosted readback and its
recorded hash. Until that gate passes, dispatch remains fail closed and no
fallback exists for the DFB/CL route.

ADR-0060's generation rules observation remains immutable while its current
publication binding may refresh independently. A refresh can validate current
rules evidence but cannot change any field above.

## Staging, cutover, and recovery

### Physical and query isolation

The first typed epoch uses exactly these collections:

| Kind | Collection |
|---|---|
| Match item snapshots | `matches-bundesliga-2026-27-typed-v1` |
| Match predictions | `match-predictions-bundesliga-2026-27-typed-v1` |
| Bonus predictions | `bonus-predictions-bundesliga-2026-27-typed-v1` |

The repository is constructed for exactly one authority mode. Recovery mode
uses existing legacy collections and refuses a typed epoch. Typed mode requires
the exact epoch and collection set above and refuses legacy collection names.
Every typed document repeats the epoch and complete addressed identity. Query
builders take an authority object and cannot accept a collection override,
prefix, fallback, or multi-epoch list.

Typed staging is non-authoritative until cutover. It cannot post, satisfy a
recovery read, or change a legacy row. Legacy inventories can be compared
read-only to plan regeneration, but no field is copied into typed provenance
unless regenerated or mapped from accepted exact evidence.

### Atomic cutover protocol

1. Complete R0-R5a and exact-SHA review/CI with no production mutation.
2. Collect and review the authenticated seed/binding, prompt, context,
   existing-row, cutoff, and cost evidence; obtain the existing Owner
   approvals.
3. Generate every required typed prediction into the isolated epoch without a
   Kicktipp POST and prove complete one-to-one coverage/readback there.
4. Confirm no active or pending affected workflow, hold new posting, and take a
   payload-safe exact-ID snapshot of Kicktipp plus both storage authorities.
5. Deploy one reviewed runtime/workflow release that selects the typed epoch
   for every Bundesliga current command and removes recovery authority from
   those deployed paths. Git merge alone does not perform this switch.
6. Verify exact-ID current reads and then perform only the separately approved
   minimum POST set. Read back every exact ID and reconcile storage, Kicktipp,
   and trace provenance before admitting the normal lane.
7. Observe the first natural run on the exact deployed SHA before closing the
   cutover.

No community, command family, match/bonus path, or read/write side may cut over
alone. Before the first typed POST, the complete release may return to the
ADR-0062 recovery authority. After a typed POST, disable the affected lane and
reconcile exact external state first. Schadensfresse re-quarantine with seven
unaffected pairs is the only preaccepted pair-local fallback; broader risk uses
whole-cron disablement.

## Hostile scenarios and required behavior

| Scenario | Required result |
|---|---|
| `RandomMatch` has eight bound candidates and one unknown fixture that random selection would not choose | Fail before random selection or current database access |
| A match keeps its Kicktipp ID but moves by one day | Stable Local Item Key stays fixed; Snapshot Hash changes; old prediction is not current |
| The first fixture row is cancelled and has no `Termin` | Fail the whole selected operation; never use `Instant.MinValue`, a sentinel, or another row's time |
| A valid fixture row is followed by a cancelled row | Fail on the cancelled row; never inherit the valid row's scheduled instant |
| Exact fixture evidence and same-ID detail `Termin` disagree | Fail the whole selected operation before any current read or downstream call |
| A same-ID fixture is rescheduled | Preserve the Stable Local Item Key, add a new immutable seed generation with a new Snapshot Hash, and reject the old snapshot as current |
| One selected item has invalid scheduled-instant evidence | Atomically perform no current read, prompt/service/model call, mutation, or POST for the complete operation |
| Two communities expose the same numeric Kicktipp ID | Produce distinct keys and seed entries; never cross-read |
| Two arena model participants use the same community | Use the same posting-community item seed and different model provenance |
| Bonus source/posting texts match but one option ID is missing from the binding | Reject the complete binding and the whole copy batch |
| One source option maps to two posting options | Reject as non-one-to-one; do not map by text at runtime |
| A current typed query returns one typed and one legacy candidate | Treat this as an authority/query defect and fail; never choose newest |
| A copy source row is legacy but payload-compatible | Reject it as a copy candidate |
| Prompt label resolves to a newer version than the pinned version | Fail immutable prompt verification before service construction |
| Configured Flex falls back to Standard | Record requested/final tiers and fallback result in immutable provenance |
| Remote POST reports success but exact-ID readback is missing one item | Stop the lane and reconcile; do not retry through team/text lookup |
| Cutover deployment selects typed runtime while a command still points at legacy storage | Fail startup/readiness; no command executes |
| Cutover fails after one typed POST | Disable the affected lane and reconcile Kicktipp/typed storage before any legacy resume |
| A future non-Schadensfresse community exposes a DFB/CL item without an accepted route | Complete command scope fails before current selection/model/POST |

Tests keep these as concrete scenarios; they do not belong in the glossary.

## Milestones, ownership, and dependencies

```text
R0 tracked specification freeze
  -> R1 Core identity/seed/copy/provenance/authority contracts
       -> R2a Kicktipp typed snapshots + exact-ID POST/readback
       -> R2b Firebase isolated staging + global current enforcement
            -> R3 shared route/provenance/copy-policy kernel
                 -> R4a Matchday/RandomMatch/VerifyMatchday integration
                 -> R4b Bonus/VerifyBonus + Schadensfresse CL composition
                      -> R5a deterministic seed/binding tooling + workflow shape
                           -> existing Owner/evidence gates
                                -> R5b real seeds/bindings + isolated typed staging
                                     -> exact-SHA review/CI
                                          -> Owner atomic cutover
                                               -> natural-run evidence
```

| Milestone | Owner and paths | Completion gate |
|---|---|---|
| R0 | Specification writer: ADR-0065, glossary, this design, P1-13 task/packet, and listed linkage docs only | Link/scope/terminology, glossary purity, accepted-ADR immutability, diff check, sensitive-token scan, exact commit, independent Sol/high acceptance |
| R1 | Core/data-contract writer: `src/Core`, `tests/Core.Tests`, synthetic schema fixtures only | Canonical key/snapshot/seed/binding/provenance/authority tests, including all hostile serialization and inventory cases |
| R2a | Kicktipp writer: `src/KicktippIntegration`, `tests/KicktippIntegration.Tests` | Complete typed snapshot parsing, exact-ID POST/readback, duplicate/drift/mixed inventory rejection; no live call |
| R2b | Firebase writer: `src/FirebaseAdapter`, `tests/FirebaseAdapter.Tests` | Physical/query isolation, exact typed current/read/save/repredict/copy concurrency, separate authority-labelled non-current audit/cost reads and DTOs, cross-epoch rejection |
| R3 | Shared route writer: shared Core/Orchestrator registration, provenance, copy-policy kernel, audit/cost combiner, and focused tests | One registration path, complete pre-model inventory gate, exact prompt/service/context/source provenance, no command-specific fallback; combine only retrieved labelled DTOs and preserve per-authority subtotals |
| R4a | Match command writer: Matchday, RandomMatch, VerifyMatchday and tests | All three commands use only typed capabilities; complete-scope and exact-ID post/readback cases pass |
| R4b | Bonus/P1-10 writer: Bonus, VerifyBonus, Schadensfresse DFB/CL route contracts and synthetic tests | Typed bonus/copy boundary, exact option projection, fail-closed DFB/CL dispatch, ADR-0058/0059/0060 profiles and preflight; no prompt body/mirror/hash assertion, implied fallback, or activation |
| R5a | Tooling/workflow writer: deterministic generators/validators, synthetic fixtures, workflow shape and contract tests | No floating generations, all rows wired to the typed epoch as one future cutover unit, recovery remains active |
| R5b | Evidence/data/staging owner after gates: real immutable seed/binding files and isolated typed staging | Complete authenticated coverage, Owner-approved calls/cost/replacement/cutoff, payload-safe staging audit; no POST before cutover gate |

R2a and R2b may proceed concurrently only after R1 is accepted. R4a and R4b
may proceed concurrently only after R3 is accepted. Writers own disjoint paths;
the root serializes integration. One heavy-operation lease covers the whole
host, including parallel PowerShell children.

## Verification strategy

Focused gates follow the table above. Every integrated implementation
milestone runs its affected TUnit project with `dotnet run`. The cohesive
implementation gate runs:

- Core, KicktippIntegration, FirebaseAdapter, OpenAiIntegration,
  ContextProviders.Kicktipp, Orchestrator, and Integration TUnit projects;
- a Release build;
- prediction workflow-contract validation and `actionlint`;
- exact-SHA scope, accepted-contract, secrets, authority-isolation, and
  rollback review; and
- exact-head Build-and-Test CI.

No unchanged-head rerun substitutes for diagnosis. No live collection or write
is needed for R0-R5a. R5b and cutover additionally require payload-safe exact
inventories for every posting community, source/posting copy correspondence,
all option mappings, pinned prompt readback and promotion, immutable context
publications, current legacy-row/replacement inventory, earliest cutoff, no
active/pending run, typed staging completeness, and rollback ownership.

## Later Owner gates

ADR-0065 resolves repository architecture only. Existing Owner gates remain
for authenticated real seeds/bindings, prompt promotion, production model
calls and cost ceiling, force/reprediction, exact copied-row replacement set,
UTC cutoff, production Firestore staging, Kicktipp POST, schedule/activation,
and rollback. P1-10 owns the Schadensfresse-specific values. Missing the
ADR-0062 sunset re-quarantines Schadensfresse; it never authorizes a partial
P1-13 cutover.
