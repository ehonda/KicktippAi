# ADR-0052: Select the production model, community matrix, and match prompt v3

- Status: Accepted
- Date: 2026-08-27

## Context

P0-06 reserved the Bundesliga 2026/27 production configuration and arena
participants for an explicit Owner decision after P0-23 produced cutoff-safe
cost and prediction-quality evidence. That evidence found Sol/`xhigh` highest
descriptively at `27.8` average Kicktipp points over the shared 200-item run
family. Its `+1.4` point difference from Sol/`high` was not statistically
significant after Holm correction (`p = 0.192`), and Sol/`xhigh` was a post-hoc,
data-dependent addition, so the result is exploratory rather than confirmatory.
The Owner nevertheless selected it because the higher-reasoning result followed
the broader observed performance trend and its season estimate remained modest.

A later post-hoc Sol/`max` run corroborates, but does not retroactively confirm
or preregister, that selection: Sol/`max` averaged `27.6` versus the selected
Sol/`xhigh` `27.8`; paired xhigh-minus-max was `+0.2` with 95% bootstrap CI
`[-1.2, 1.6]` and Holm-adjusted `p = 0.8918`. Its 493-call estimate is
`$7.903381600000`. The source evidence is frozen at exact lane commit
`f7dd2aee6c35fec26a5f09df0f1a68d82495f01b` and will integrate at
`docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-quality-results.md`.

The production match prompt version 2 also contains two unconditional sentences
about the optional `justification` response object. Production calls do not
request that object. Langfuse supports simple `{{variable}}` substitution, so
the optional instruction can be supplied by code only when the structured-output
schema includes justification, without splitting the hosted route.

The Owner also added `relaxdays-tippt` as a production community. It uses the
same default Kicktipp rules as `pes-squad`, so it can use the established
reference-copy topology. `schadensfresse` remains an independent primary and
still awaits external new-season setup. All final schedules remain a separate
P0-21 Owner gate.

## Decision

### Production and arena model ledger

The exact production identity is:

- competition `bundesliga-2026-27`;
- model `gpt-5.6-sol`;
- reasoning effort `xhigh`;
- maximum output tokens `10000`;
- hosted match prompt `kicktippai/bundesliga-2026-27/predict-one-match`,
  immutable version `3`, required label membership `production`, normalized
  SHA-256 `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`;
- hosted bonus prompt `kicktippai/bundesliga-2026-27/predict-bonus`, immutable
  version `1`, required label membership `production`, normalized SHA-256
  `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`;
- the existing prediction-service Flex-first request with one Standard fallback
  for retryable Flex resource unavailability; and
- USD `35` as a non-enforced whole-season planning orientation, not a runtime
  spend gate or hard budget.

The arena production participant uses the exact same Sol/`xhigh` identity and
copy-posts the stored `pes-squad` prediction. The admitted self-contained arena
challengers are Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`.
Every challenger pins maximum output tokens `10000`, match prompt v3, bonus
prompt v1, required `production` membership, and the same Flex-first/Standard-
fallback policy. The shared cap is the exact cap used by their accepted
cost/quality evidence and is not inherited from a command default. Luna/`none`
retains its plumbing role and is additionally admitted as a cheap arena
challenger; it is not the production model.

Historical P0-23 datasets, manifests, and compatibility constants remain bound
to match prompt v2. The prompt successor never rewrites their immutable
experiment identity.

### Prompt successor

Match prompt v3 replaces the old two-sentence conditional justification clause
with exactly one `{{justification_explainer}}` variable. The checked-in
`match.md` and `match.justification.md` mirrors are byte-identical to the hosted
v3 content. Runtime code expands the variable to this leading-space clause only
when justification is requested:

```text
 Populate the `justification` object concisely with neutral paraphrases of the evidence, important uncertainties, and the context documents used.
```

Otherwise it expands to the empty string. Rendered production prompts without
justification therefore contain neither the placeholder nor any justification
instruction. Duplicate supported placeholders and any unknown/unresolved
template placeholder fail closed before context content is inserted. All
prediction and historical reconstruction paths pass their explicit
justification mode to the same composer.

The v3 hosted version receives `production` and `staging`; Langfuse maintains
`latest` automatically. Runtime and every live Bundesliga 2026/27 workflow pin
version 3 and verify `production` membership. Version 2 remains immutable and
unlabeled for current production after promotion.

Authenticated publication and readback completed on 2026-08-27 only after the
parallel Sol/`max` comparison finished on immutable v2. Readback verified exact
name, text type, version 3, all three labels resolving version 3, and the
normalized hash above. The bonus route was not mutated and no model call was
made during promotion.

### Community and credential topology

`pes-squad` and `schadensfresse` are independent production primaries.
`relaxdays-tippt` and the Sol/`xhigh` participant in `ehonda-ai-arena` are
secondary targets that copy the exact `pes-squad` production identity.
Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none` in the arena are
self-contained.

Match copies retain the existing exact fixture/configuration/context checks and
fail closed when a compatible source is unavailable. Bonus copies retain
ADR-0048: exact normalized question, selection limit, complete option-set, and
immutable-context compatibility produce a zero-model copy; ordinary
incompatibility generates exactly one independent target-context prediction;
invalid or ambiguous target/context state fails closed.

The reusable context workflow exposes one boolean
`publish_launch_roster_overlay` input that is optional and false by default.
Only the `pes-squad`, `relaxdays-tippt`, and prepared `schadensfresse` context
callers set it to true. Before their ordinary competition profile, the workflow
downloads the public CC0 DuckDB artifact from the existing repository-audited
R2 URL into the ephemeral runner directory and invokes `collect-context
rosters` with exact revision
`154367dfa6d6eb0b86332e332f9df0a080c7ddce`, snapshot date `2026-08-13`,
SHA-256 `808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c`,
and the paired `--require-launch-coverage --launch-enrichment-overlay` flags.
Download, pin, reconstruction, coverage, or publication failure stops the job
before ordinary profile collection. The following no-DuckDB profile roster
step preserves the enriched same-date last-known-good publication, as covered
by the accepted P0-25 source contract and tests.

Arena context callers deliberately leave the input absent because the shared
`ehonda-ai-arena` context already has exact enriched headed snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`;
their ordinary profile collection preserves it without another download.
`schadensfresse` is wired now but remains unrun pending administrator setup.
This bounded initial-publication path does not automate refresh or current-
season DuckDB membership adoption, which remain P1-05.

The canonical Kicktipp credential names are:

| Participant or community | Username/password secret stem | Local profile |
| --- | --- | --- |
| `pes-squad` | `PES_SQUAD_KICKTIPP` | `.env.pes-squad` |
| `schadensfresse` | `SCHADENSFRESSE_KICKTIPP` | `.env.schadensfresse` |
| `relaxdays-tippt` | `RELAXDAYS_TIPPT_KICKTIPP` | `.env.relaxdays-tippt` |
| Arena Sol/`xhigh` | `EHONDA_AI_ARENA_GPT_5_6_SOL_XHIGH_KICKTIPP` | `.env.ehonda-ai-arena.gpt-5-6-sol-xhigh` |
| Arena Sol/`high` | `EHONDA_AI_ARENA_GPT_5_6_SOL_HIGH_KICKTIPP` | `.env.ehonda-ai-arena.gpt-5-6-sol-high` |
| Arena Luna/`medium` | `EHONDA_AI_ARENA_GPT_5_6_LUNA_MEDIUM_KICKTIPP` | `.env.ehonda-ai-arena.gpt-5-6-luna-medium` |
| Arena Terra/`xhigh` | `EHONDA_AI_ARENA_GPT_5_6_TERRA_XHIGH_KICKTIPP` | `.env.ehonda-ai-arena.gpt-5-6-terra-xhigh` |
| Arena Luna/`none` | `EHONDA_AI_ARENA_GPT_5_6_LUNA_NONE_KICKTIPP` | `.env.ehonda-ai-arena` |

Each stem expands to the exact `_USERNAME` and `_PASSWORD` pair. The Owner
confirmed all listed Actions secrets present on 2026-08-27. This is provisioning
evidence only; it is not API enumeration, authentication, current-season
readiness, or POST-permission evidence. Local profile selection is explicit and
continues to derive credentials from the posting participant, never from a
copy-source `community_context`.

Every P0-19 caller is `workflow_dispatch`-only. No schedule, `workflow_call`,
dispatch, model call, production POST, or live-write authority follows from
this decision. P0-21 alone owns manual runtime evidence, the later activation
ADR, deliberate schedules, and the first scheduled observation.

After accepting this decision, the Owner separately authorized P0-21—only once
the implementation is reviewed, integrated, pushed, and green—to dispatch
context then initial predictions for `pes-squad`, `relaxdays-tippt`, and every
selected arena participant. Context failure stops that row; `pes-squad` must
precede its dependent secondary copies; each self-contained challenger requires
its own successful context run. Successful inspected evidence may be followed
by a later activation ADR/schedule lane for ready rows. `schadensfresse`
remains manual-only and unscheduled pending administrator setup. This follow-up
authorization does not add a schedule or dispatch from this decision lane.

## Alternatives considered

- **Select Sol/`high`:** Rejected by the Owner because Sol/`xhigh` performed
  better descriptively and its additional estimated season cost was acceptable,
  while retaining the exploratory/non-significant caveat.
- **Wait for Sol/`max` quality evidence:** Rejected as a P0 blocker. Its cheap
  post-hoc quality run later corroborated the settled choice descriptively but
  is not retroactive preregistered selection evidence.
- **Keep match prompt v2:** Rejected because it sends production-only calls
  instructions for an absent response field.
- **Create separate hosted justification/no-justification routes:** Rejected
  because one simple precomputed Langfuse variable keeps one immutable prompt
  identity while preserving both schemas.
- **Generate separately in every production community:** Rejected for
  `relaxdays-tippt` and the production arena participant because their accepted
  default rules allow guarded reference reuse and avoid duplicate model spend.
- **Enable schedules with repository preparation:** Rejected because manual
  evidence, exact deadlines, operating ownership, and rollback remain P0-21
  activation gates.

## Consequences

- P0-06's production and challenger gate is resolved with complete, reproducible
  identities and evidence caveats.
- Two independent Sol/`xhigh` primary streams are planned; compatible production
  copies add no match-model call, while bonus incompatibility can add a bounded
  target-context call.
- The exact match-only 493-call planning total for both primaries plus all four
  self-contained challengers is USD `14.094805910000`; bonus calls, richer live
  context, Standard fallback premiums, and retry behavior remain planning
  caveats under the non-enforced USD 35 orientation.
- All workflows can be manually tested once their community/runtime gates pass,
  while schedule activation remains visibly absent.
- Initial non-arena context dispatches can satisfy the P0-25 enriched-roster
  prerequisite in the same fail-closed job before normal profile collection;
  no out-of-band local credentials or Firebase operation is required.
- Existing Luna/`none` validation evidence stays truthful even though future
  live calls use the promoted match prompt successor.

## Affected tasks

- [P0-05](../tasks/p0-05-prompt-route.md)
- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-17](../tasks/p0-17-community-scope.md)
- [P0-19 template and deployable rows](../tasks/p0-19-community-workflow-triad.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

- [ADR-0005](0005-launch-community-and-prediction-topology.md), whose three-
  production-community topology is replaced by the exact four-community matrix
  above.
- [ADR-0033](0033-pin-validation-model-ledger-and-reserve-production-selection.md),
  only its unresolved production gate and live match-prompt-v2 pin. Its
  historical validation and cost evidence remain immutable.
- [ADR-0039](0039-record-bundesliga-community-and-credential-topology.md), only
  its six-row/unresolved-slot matrix and its match-prompt-v2 live identity.
  Posting-target credential selection, environment classification, and the
  ADR-0048 compatibility refinement remain in force.
- [ADR-0050](0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md)
  and [ADR-0051](0051-require-explicit-launch-roster-enrichment-overlay.md),
  only their statement that the reusable context workflow never acquires the
  pinned launch artifact. Their exact pins, overlay semantics, fail-closed
  publication contract, P0-21 evidence gate, and P1-05 recurring ownership
  remain in force.
