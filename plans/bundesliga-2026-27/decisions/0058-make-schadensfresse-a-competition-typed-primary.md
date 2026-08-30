# ADR-0058: Make schadensfresse a Bundesliga-subcompetition-typed primary

- Status: Accepted
- Date: 2026-08-30
- Decision authority: Project Owner authorized evidence-backed necessary and
  sanctioned decisions on 2026-08-30

## Context

`schadensfresse` was launched as a `pes-squad` copy under ADR-0054 and its
ordinary match copy was placed in the production-live lane by ADR-0055. An
authenticated, read-only live retrieval completed at
`2026-08-30T07:35:21.9308276Z` shows that premise is no longer true. The
current target rules award `2/3/5` points for a winning tendency/goal
difference/exact score and `3/-/5` for a draw, while each correct bonus answer
awards `9` points. Tips remain hidden, use exact-score mode, and close with zero
minutes of lead. Bundesliga results are evaluated after 90 minutes; DFB-Pokal
and Champions-League results are evaluated after a penalty shootout. The
retrieved rules HTML has SHA-256
`f788efe448ce538d530baf74ce66f5ef03a61faab5a527d965dcd8d314d2e9c0`;
the whitelisted normalized rule record has SHA-256
`b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`.
The checked-in `schadensfresse` rules are still byte-identical to `pes-squad`
at SHA-256
`e52945f0d63e9a332ee225d4a9fd60677b761771dac0ac6cc8d7957143252292`
and are therefore wrong for the target.

The same retrieval found two remaining open fixtures and joined Kicktipp IDs
`1662323362` and `1662323366` from the matchday-outcome surface. Their teams,
numeric `Matchday = 1`, and live rules context support an inference that they
are Bundesliga fixtures, but the safe DTO captured no exact round label,
competition field, or result-basis marker. That inference is evidence for the
gap, not canonical classification. The current parser extracts a displayed
round on the open-prediction page but retains it only in WM26-specific data,
and it interprets penalty markers only in WM26 paths. `BonusQuestion`
similarly has no typed Bundesliga-season subcompetition and exposes its form
field rather than a required stable Kicktipp question identity. Treating the
existing repository-wide `competition = bundesliga-2026-27` storage partition
as if it meant Bundesliga league play would make DFB-Pokal and Champions-
League predictions ambiguous.

Three Champions-League questions are open with the corrected common deadline
`2026-09-08T16:45:00Z` (not the stale September 9 deadline):

| Kicktipp question ID | Exact text | Maximum selections | Option count | Exact option ID/text array SHA-256 |
| --- | --- | ---: | ---: | --- |
| `1662326752` | `CL: Welche Mannschaft stellt den Spieler mit den meisten Toren?` | 1 | 37 | `e29a9636d4d2e4fd7ac48a371dfe650c242e041b006cc3d3fc31986a539f1c55` |
| `1662326753` | `CL: Wer erreicht das Halbfinale?` | 4 | 37 | `d1e7ed3827d6d07daf2416edc8862466885f0f80d115886203840850ec1b5920` |
| `1662326754` | `CL: Wer gewinnt die Champions League?` | 1 | 37 | `e5c1f2949d8cb7d8675f901c97fc09e8e24c6df892376f698caf4e6d28c1be9d` |

The payload-safe array containing all three complete definitions has SHA-256
`80def7b217a382ed95450c2a8f8db227ba13a2f55ca72513a8897f86fa511ef9`.
The implementation must check in the deterministic routing seed/config with
the exact option identities represented by these hashes; the 111 option rows
are intentionally not duplicated in this ADR.

## Decision

### Domain and storage contract

`schadensfresse` is a target-owned independent production primary for every
match and every bonus question. Its prediction route never reads, copies, or
inherits prediction payload or immutable provenance from `pes-squad`.

Keep `competition = bundesliga-2026-27` as the explicit top-level season and
community storage partition. Within that partition only, add a typed
`BundesligaSeasonSubcompetition` with exactly `Bundesliga`, `DfbPokal`, and
`ChampionsLeague`. Serialize it in Firestore, canonical JSON, trace metadata,
and routing seeds as field `bundesligaSeasonSubcompetition` with exact values
`bundesliga`, `dfb-pokal`, and `uefa-champions-league`. It is not a global
competition enum and is invalid outside the `bundesliga-2026-27` partition.

Add generic `Match` identity fields that can coexist with, but do not replace,
the current `CompetitionSpecificMatchData`: nonempty `KicktippFixtureId`,
nonempty exact `KicktippRoundName`, and typed `ResultBasis`. Serialize them as
`kicktippFixtureId`, `kicktippRoundName`, and `resultBasis`; the exact result-
basis values are `regularTime90Minutes` and
`finalScoreIncludingExtraTimeAndPenaltyShootout`. Bundesliga requires the
former and DFB-Pokal/Champions League require the latter. WM26 retains
`FifaWorldCup2026MatchData`, its stage, and its existing competition-specific
meaning. It may share generic fixture/round/result-basis fields when its parser
can populate them, but it never receives a `BundesligaSeasonSubcompetition`
and this decision does not invalidate or replace its specific record.

Every live Bundesliga-season `BonusQuestion` carries a nonempty
`KicktippQuestionId` plus `BundesligaSeasonSubcompetition`, serialized as
`kicktippQuestionId` and `bundesligaSeasonSubcompetition`. A canonical bonus
identity binds the question ID, exact question text, the complete ordered
option ID/text array, exact `MaxSelections`, exact deadline instant, and
subcompetition. The three current CL rows above are the accepted identity
snapshot.

New `bundesliga-2026-27` match and bonus writes must contain the exact canonical
fields and values above. Existing rows stay readable for historical display,
audit, and dry-run inventory, but a row missing or contradicting any required
typed identity is legacy and cannot satisfy current reuse, freshness,
verification, copy, reprediction, or posting. No migration or deletion is
implied. Rows in WM26 or another partition continue under their existing
contracts; absence of `bundesligaSeasonSubcompetition` there is valid. Unknown
serialized values, a subcompetition outside the Bundesliga partition, or
unknown/duplicate/missing identity fields fail closed.

Classification is fail closed. A checked-in deterministic routing seed must
bind every canonical fixture ID to an exact tuple of
`{ bundesligaSeasonSubcompetition, kicktippRoundName, resultBasis }` and every
question ID to its exact canonical bonus identity. The two current inferred
fixture IDs are not canonical seed entries until the exact round and structured
subcompetition evidence are retrieved and recorded; team names and numeric
matchday cannot fill the missing values. A question-text prefix, round-name
prefix, top-level storage partition, team names, or untyped default is never
sufficient. Unknown IDs, incomplete seed values, missing page metadata,
conflicting signals, or live/seed drift stops context selection, generation,
persistence, and posting before a model service is created. No fallback may
copy a prediction or select a generic Bundesliga prompt/context route.

### Prediction routes

Each supported Bundesliga-season subcompetition has an explicit target-owned
prompt and context route. The Bundesliga match route may retain ADR-0052's immutable
production match prompt v3 and the Bundesliga bonus route may retain bonus
prompt v1, but both resolve `community_context: schadensfresse` and the
corrected target rules. DFB-Pokal and Champions League must use distinct
competition-correct hosted names under the existing season namespace:

- `kicktippai/bundesliga-2026-27/dfb-pokal/predict-one-match`;
- `kicktippai/bundesliga-2026-27/champions-league/predict-one-match`; and
- `kicktippai/bundesliga-2026-27/champions-league/predict-bonus`.

Their checked-in mirrors, immutable versions, normalized hashes, and required
`production` label membership must be reviewed and recorded before promotion.
Until that promotion, those routes fail closed; neither existing Bundesliga
prompt is a temporary mixed-competition fallback. Match validation and result
interpretation use the stored `ResultBasis`.

### DFB-Pokal and Champions-League context profile

The accepted safe launch context is deliberately rules-only because the
repository has no accepted complete, current DFB-Pokal or Champions-League
strength, roster, or history publication. Implement these exact profiles:

| Profile ID | Subcompetition | Prompt input outside context documents |
| --- | --- | --- |
| `schadensfresse-dfb-pokal-rules-only-v1` | `dfb-pokal` | canonical fixture ID, teams, exact round, start/deadline, and after-penalties result basis |
| `schadensfresse-champions-league-match-rules-only-v1` | `uefa-champions-league` | canonical fixture ID, teams, exact round, start/deadline, and after-penalties result basis |
| `schadensfresse-champions-league-bonus-rules-only-v1` | `uefa-champions-league` | canonical question ID, exact text, complete ordered option ID/text array, selection limit, and deadline |

Each profile has exactly one allowed context document: kind `Context`, name
`community-rules-schadensfresse.md`. Its repository source is exactly
`community-rules/schadensfresse.md`; context collection publishes those bytes
under the allowed document name. Before publication, a semantic validator must
reconstruct the exact live rule record and match normalized SHA-256
`b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03`.
The routing seed pins the checked-in file's lowercase content SHA-256, and the
published immutable version/content hash must match that pin.

Before every manual DFB/CL production generation, a read-only authenticated
rules preflight must be no older than 24 hours, finish on exact HTTPS host
`www.kicktipp.de` and path `/schadensfresse/spielregeln`, and reproduce the
same normalized rules hash. The exact live fixture/question definition must
also match the seed. Missing evidence, age greater than 24 hours, redirects to
a login/different path, wrong scope, changed rules, changed options/deadline,
missing publication, or any hash/version mismatch fails before prompt fetch or
model-service construction. A future recurring primary schedule must automate
an equivalent fail-closed freshness check; this manual evidence window is not
an unattended-network authorization.

No DFB/CL profile may load `bundesliga-standings.csv`, Club Elo, squad summary,
rosters, Bundesliga recent/home/away/head-to-head history, team/manager data,
transfers, WM26 rankings/lineups, generic KPI rows, or any stored document not
named above. There is no implicit latest read, enumeration, on-demand fetch,
cross-community source, prefix-derived expansion, or truncation. The exact
budget is `MaximumDocuments = 1` and `MaximumEstimatedTokens = 2048`, measured
with the existing UTF-8-byte/4 ceiling estimator over the rendered context
section. Exceeding either limit fails closed.

Persist a canonical `resolvedTypedContextManifest` with this exact ordered root
schema: `seasonPartition`, `communityContext`,
`bundesligaSeasonSubcompetition`, `profileId`, `routingSeedSha256`,
`rulesObservedAt`, `normalizedRulesSha256`, `documents`. The sole ordered
document contains exactly `{ kind, name, version, contentSha256 }`.
`seasonPartition` is `bundesliga-2026-27`, `communityContext` is
`schadensfresse`, profile/subcompetition must be one exact row above,
`rulesObservedAt` is the authenticated UTC instant, and every hash is lowercase
SHA-256. Existing prediction metadata continues to bind the exact prompt,
model, reasoning, cap, and service policy. Freshness re-resolves all seed,
rules, document, prompt, and model identities; a legacy or noncanonical
manifest is not reusable.

This profile is safe but intentionally weak for prediction quality: the model
receives target rules and exact fixture/question inputs, but no independently
verified cross-competition strength, roster, or history evidence. Adding any
such enrichment requires an Accepted successor with exact source, freshness,
publication, allowlist, provenance, and budget contracts; it is not inferred
from Bundesliga data.

All target-owned production generations retain ADR-0052's `gpt-5.6-sol` /
`xhigh` / maximum-output-tokens `10000` identity and Flex-first with one
Standard fallback unless an accepted successor changes it. This ADR selects no
new model and grants no model-call authority.

### Rollout and activation gates

P1-10 fully absorbs and supersedes P1-08. Implement one primary route; do not
add a Bundesliga-copy path with DFB/CL exception switching.

The scoring mismatch makes continued unattended copy execution unsafe. As the
first repository change after this decision, quarantine `schadensfresse` from
the active outer lane before the next nominal `2026-08-30T09:07:00Z`
occurrence:

1. delete jobs `schadensfresse-context` and `schadensfresse-matchday` from
   `.github/workflows/buli2627-production-live-matchday.yml`; and
2. change only `relaxdays-tippt-context.needs` from
   `schadensfresse-matchday` to `pes-squad-matchday`.

The resulting lane has the prior seven context/match pairs and 14 jobs. Keep
the exact `7 2,9 * * *` cron, `bundesliga-2026-27-production-live-lane`
non-cancelling concurrency, remaining serial order, default-success failure
propagation, leaf-manual-only boundary, no-bonus boundary, monitoring/on-call,
and rollback contracts. The schadensfresse pair remains absent until a later
separately reviewed primary-activation commit passes every gate below. This
Accepted decision authorizes only that fail-safe repository schedule removal;
it authorizes no workflow dispatch/cancellation, model call, force,
reprediction, prediction deletion/replacement, Kicktipp POST, Firestore write,
Langfuse mutation, prompt promotion, or credential change.

Repository implementation may now add the typed domain/persistence contract,
parser and deterministic seed, fail-closed classifier, corrected target rules,
competition-specific context/prompt route selection, primary-only commands,
and local fixture/contract tests. The planning-artifact slice itself performs
no schedule or live mutation; the quarantine is a separately implemented
repository change and precedes every generation/activation slice.

Activation is staged and ordered:

1. Static workflow tests prove the exact seven-pair quarantine topology and
   preserved operating contract. Local captured-fixture tests then prove all
   three subcompetition classifications, both result bases, exact stable
   identities, the three CL question definitions, the exact rules-only context
   profiles, and every unknown/drift/copy/leakage rejection case. Persistence,
   provenance, command, and workflow-contract tests prove the typed primary
   route.
2. The safe Luna/`none` path with a pinned cap validates applicable Bundesliga
   primary plumbing in `ehonda-dev-buli-2627`. `ehonda-ai-arena` is used only
   if it presents the same typed fixture/question contract; otherwise the task
   records an evidence-backed not-applicable result rather than fabricating an
   alias. Neither environment substitutes for target classification evidence.
3. Read-only production preflight rechecks the no-older-than-24-hours rules
   evidence, fixtures, questions, deadlines, exact seed/document/prompt hashes,
   rules-only context readiness, existing copied prediction inventory, and the
   earliest affected cutoff. Drift closes the gate.
4. Before replacing any existing copied row, the Owner approves an exact
   replacement set, maximum additional production calls/cost, force and
   reprediction limits, and a UTC cutoff. Empty or exceeded limits fail closed;
   there is no default replacement budget. The three CL bonus routes and their
   promoted prompt identities must be ready before
   `2026-09-08T16:45:00Z`, which is the current critical path.
5. Run target context collection, then the minimum approved manual primary
   prediction operations. Inspect Kicktipp, Firestore, and Langfuse in that
   order using payload-safe identities, counts, route/provenance hashes,
   result basis, model configuration, tokens, and cost—never prediction
   contents, option selections, prompts, context bodies, or secrets.
6. Only after green manual evidence, make and separately review a primary-
   activation commit that reintroduces target context followed by the target-
   owned primary match job. It may not restore a copy job or add bonus
   scheduling. Observe the first natural execution against its exact pushed
   commit before closing P1-10.

## Alternatives considered

- **Keep the copy and add DFB/CL exceptions:** Rejected because the live
  Bundesliga scoring contract already differs; exception routing would retain
  an unsafe false default and split one community's ownership across sources.
- **Treat `bundesliga-2026-27` as the match competition:** Rejected because it
  is the required season/storage partition and cannot distinguish Bundesliga,
  DFB-Pokal, and Champions League.
- **Classify from `CL:`/round prefixes:** Rejected because mutable display text
  is not a stable identity and makes unknown or drifted state look valid.
- **Use Bundesliga prompts until DFB/CL promotion:** Rejected because a model
  call with the wrong scoring, context, or result basis is not safe temporary
  behavior.
- **Reuse Bundesliga Elo/rosters/history for DFB/CL:** Rejected because those
  publications describe a league-specific team set and evidence contract;
  leakage would look richer while being incomplete or wrong.
- **Keep the scheduled copy until primary activation:** Rejected because the
  known scoring mismatch makes another unattended copy execution unsafe.
- **Disable the complete production lane:** Rejected because the defect is
  isolated to the schadensfresse pair and reconnecting the exact serial edge
  preserves safe rows without broad operational disruption.
- **Replace copied predictions during quarantine:** Rejected because schedule
  removal is a fail-safe repository change, while replacement consumes
  production calls/state and remains behind the explicit budget/cutoff gate.

## Consequences

- `schadensfresse` regains the independent-primary topology originally selected
  in ADR-0052, now backed by current rules and subcompetition-typed identities.
- Domain code and Firestore gain an explicit distinction between storage
  season and Bundesliga-season subcompetition. Legacy untyped rows remain
  preserved but cannot silently become live reusable rows.
- The corrected September 8 CL bonus deadline is the immediate delivery gate;
  DFB/CL generation remains unavailable until its exact prompt promotion and
  typed rules-only route are implemented and verified.
- The rules-only DFB/CL profile is reproducible and leak-free but deliberately
  sacrifices evidence breadth and expected prediction quality.
- Independent target predictions add bounded production cost. That cost and
  any copied-row replacement remain explicit Owner-controlled rollout inputs.
- The active recurring lane immediately returns to the seven-pair/14-job
  topology; `schadensfresse` has no recurring execution until reviewed primary
  activation.

## Affected tasks

- [P1-08](../tasks/p1-08-schadensfresse-mixed-competition-routing.md)
- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)

## Supersedes

- [ADR-0054](0054-copy-schadensfresse-bundesliga-from-pes-squad.md), its entire
  `schadensfresse` copy/alias/provenance topology and its stale `2/3/4`,
  four-point bonus, and September 9 premises. Historical P0 evidence remains
  historical and is not rewritten.
- [ADR-0055](0055-add-schadensfresse-to-production-live-lane.md), only its
  `schadensfresse` context/copy pair and resulting 16-job/eight-pair topology.
  Its inherited outer cadence, concurrency, remaining serial ordering, failure,
  monitoring, rollback, leaf-manual-only, and no-bonus contracts remain in
  force until the separately reviewed primary activation change.
