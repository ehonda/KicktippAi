# Bundesliga 2026/27 production activation preregistration

**Status:** Activated and observed; P0-21 is complete.

**Prepared:** 2026-08-25

This preregistration preserves the P0-21 facts, sequence, and evidence that led
to [Accepted ADR-0053](../../plans/bundesliga-2026-27/decisions/0053-schedule-the-production-live-matchday-lane.md).
Its dated sections preserve the evidence available before activation. The
closeout addendum below records the later runtime result.

## Activation closeout addendum — 2026-08-28

Natural GitHub Actions `schedule` run
[`33143114280`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280)
succeeded on `main` at exact head
`50f3ed148891977b5909659f9986c9c9958d7875`, from
`2026-08-28T04:53:22Z` to `05:32:08Z`. All 16 jobs succeeded in the exact
ADR-0053/0055 serial order, with every context predecessor complete before its
match job and every match job complete before the next community context.
Context preserved the accepted snapshots with overlay false; every match row
verified 9/9 current predictions and skipped generation/posting and final
verification. There was no write, generation, reprediction, usage, cost,
runtime identity contamination, retry, queued relevant run, or overlap.

GitHub delivered the nominal 02:07 UTC occurrence 2h46m22s late, beyond the
90-minute observation envelope, but its 38m46s execution completed before the
second daily occurrence. The exact job IDs and payload-safe evidence are in
[P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md).
This addendum supersedes later pre-observation statements and closes the
preregistered P0 runtime gate. Bonus remains outside the outer lane. P1-08 is
still open and non-P0 for `schadensfresse` mixed-competition routing.

## Schadensfresse readiness and topology addendum — 2026-08-27

The administrator has now completed setup. Read-only preflight found the same
nine opening Bundesliga fixtures as `pes-squad`; pre-context Firestore hygiene
is expected 401 / present 0 / missing 401 / unexpected 0 / conflicts 0, with
roster and Club Elo heads absent. Context workflow `181809317` must therefore
run first with the pinned launch overlay and be inspected before predictions.

[ADR-0054](../../plans/bundesliga-2026-27/decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md)
supersedes every older statement here that calls `schadensfresse` an
independent primary or externally pending. Ordinary Bundesliga match workflow
`343525557` copies `pes-squad`. Bonus workflow `343525555` defaults an inclusive
ceiling of `2026-08-28T18:30:00Z`, selecting the five audited Bundesliga
questions through exact scoped aliases and excluding three CL questions due
`2026-09-09T10:00:00Z`. A complete-form POST preserves non-target selections.

The preregistered order was manual context, match, and bounded bonus, followed
by schedule inclusion only if all evidence was green. That ladder subsequently
succeeded on exact pushed head `3dd93d5`, and ADR-0055 prepares target context
plus copied matchday immediately after `pes-squad` with launch overlay false.
Bonus remains unscheduled. P1-08 owns CL bonus routing before September 9 and
later DFB/CL primary match routing.

## Owner-selection and repository-preparation addendum — 2026-08-27

[ADR-0052](../../plans/bundesliga-2026-27/decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
settles the previously open configuration and topology gates:

- production is `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first with one
  Standard fallback;
- match prompt v3 is promoted at normalized SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`;
  bonus remains v1;
- `pes-squad` is the independent primary; ADR-0054 later changed ordinary
  `schadensfresse` Bundesliga work, `relaxdays-tippt`, and arena Sol/`xhigh` to
  copy the `pes-squad` reference; and
- self-contained arena challengers are Sol/`high`, Luna/`medium`,
  Terra/`xhigh`, and Luna/`none`, all cap `10000`.

All exact workflow triads are now prepared as `workflow_dispatch`-only callers
with no schedules. The Owner confirmed every canonical Actions Kicktipp pair in
the [community ledger](community-onboarding.md) provisioned on 2026-08-27.
That confirmation does not claim API enumeration, authentication, readiness,
POST permission, deadlines, or a successful workflow run.

The context workflow now has one false-by-default
`publish_launch_roster_overlay` input. The manual `pes-squad`,
`relaxdays-tippt`, and prepared `schadensfresse` callers opt in. Their job first
downloads the public CC0 DuckDB artifact from the existing audited R2 URL into
ephemeral runner storage and runs `collect-context rosters` with exact SHA
`808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c`,
revision `154367dfa6d6eb0b86332e332f9df0a080c7ddce`, snapshot date
`2026-08-13`, and both launch flags. Only a successful fail-closed overlay may
proceed to ordinary profile collection, whose no-DuckDB roster step preserves
the enriched same-date last-known-good head. Arena callers omit the opt-in and
preserve their already verified shared enriched snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.

This addendum supersedes dated statements below that call the production model,
participants, prompt v3, callers, or secret presence unresolved. It does not
close any live P0-21 gate. Before a caller is dispatched, P0-21 still requires
participant authentication and current-season readiness, POST permission,
exact deadlines, enriched roster publication, context-before-prediction order,
and inspection. No schedule was enabled before the final Owner activation
gate. That gate passed on 2026-08-27 for the originally ready rows. The
administrator later completed `schadensfresse` setup; its Owner-authorized
manual ladder succeeded, and ADR-0055 records its subsequent schedule
inclusion without changing the cron or operations contract.

### Owner dispatch authorization — 2026-08-27

Once repository preparation is independently reviewed, integrated, pushed, and
green, the Owner authorizes P0-21 to dispatch context and then predictions for
`pes-squad`, `relaxdays-tippt`, and every selected arena participant, with
primaries before dependent secondaries and an immediate stop of an affected
chain on failure. The authorization includes the resulting initial prediction
writes for rows that pass their runtime checks. At that 2026-08-27 gate,
`schadensfresse` remained unrun/manual-only until its administrator finished
new-season setup; the separate authorization and completed evidence below
supersede that historical restriction.

### schadensfresse dispatch and schedule authorization — 2026-08-28

After administrator setup, the Owner authorized the complete ordered
`schadensfresse` context, match-copy, and cutoff-bounded bonus ladder, including
the resulting target writes and the later schedule inclusion if green. The
three runs succeeded on exact pushed head `3dd93d5`. Context's 86 present
documents are complete for the current nine fixtures but are not a strict
401-document full-season inventory pass. ADR-0055 adds only context then
ordinary match copy to the outer lane; no bonus or P1-08 mixed-competition work
is scheduled.

Successful inspected manual evidence permitted the Owner to accept ADR-0053.
This activation change schedules only the outer ready-row matchday lane; every
leaf caller remains manual-only and schedule-free.

### Historical production-live outer-lane precursor — 2026-08-27

Exact commit `992af5a63c788c0cc066dce92dd1319a91e5083d` prepares one
production-live outer matchday caller. Its strict default-success
`needs` chain runs context immediately before `pes-squad`, `relaxdays-tippt`,
arena Sol/`xhigh`, Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`
matchday rows. It has no bonus or `schadensfresse` job. The outer caller and
production leaves share a non-cancelling concurrency group. ADR-0053's later
activation change adds the sole production cron without changing that topology.

Independent exact-SHA review approved the commit with no findings. The
prediction-workflow contract passed; actionlint passed with only pre-existing
warnings; Release build completed with zero errors; and Orchestrator passed
`1142/1142`. The commit was pushed and integrated to `main`, and exact-head
GitHub run
[`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
succeeded including Pages. Integrated writer/reviewer worktrees were cleaned.
This evidence did not dispatch the lane. At that precursor checkpoint, cadence,
ownership, and rollback were settled by ADR-0053 while first scheduled
observation and `schadensfresse` onboarding remained open. The later completed
ladder and ADR-0055 supersede only the latter state.

For the original preregistration-writing pass, no authenticated community
lookup, workflow dispatch, model or Langfuse call, schedule change, prediction
write, or other external mutation was performed merely to prepare the draft.

### Ordered manual-live checkpoint — 2026-08-27

The separate Owner authorization has now been exercised through every ready
row. All terminal runs used an exact pushed main commit. Initial prediction
runs used `force_prediction=false` / `max_repredictions=0`; the single
Owner-approved Luna/`none` bonus recovery used `force_prediction=true` /
`max_repredictions=0`. Every completed row passed final verification:

| Row | Context | Matchday | Bonus |
| --- | --- | --- | --- |
| `pes-production-reference` | [`33046582867`](https://github.com/ehonda/KicktippAi/actions/runs/33046582867) | [`33046770442`](https://github.com/ehonda/KicktippAi/actions/runs/33046770442) | [`33047217909`](https://github.com/ehonda/KicktippAi/actions/runs/33047217909) |
| `relaxdays-production-copy` | [`33049949393`](https://github.com/ehonda/KicktippAi/actions/runs/33049949393) | [`33050188533`](https://github.com/ehonda/KicktippAi/actions/runs/33050188533) | [`33050549422`](https://github.com/ehonda/KicktippAi/actions/runs/33050549422) |
| `arena-production-copy` | [`33050848544`](https://github.com/ehonda/KicktippAi/actions/runs/33050848544) | [`33051066657`](https://github.com/ehonda/KicktippAi/actions/runs/33051066657) | [`33051557046`](https://github.com/ehonda/KicktippAi/actions/runs/33051557046) |
| `arena-challenger-sol-high` | [`33051863137`](https://github.com/ehonda/KicktippAi/actions/runs/33051863137) | [`33052087407`](https://github.com/ehonda/KicktippAi/actions/runs/33052087407) | [`33052537217`](https://github.com/ehonda/KicktippAi/actions/runs/33052537217) |
| `arena-challenger-luna-medium` | [`33052882246`](https://github.com/ehonda/KicktippAi/actions/runs/33052882246) | [`33053095243`](https://github.com/ehonda/KicktippAi/actions/runs/33053095243) | [`33053423396`](https://github.com/ehonda/KicktippAi/actions/runs/33053423396) |
| `arena-challenger-terra-xhigh` | [`33053664914`](https://github.com/ehonda/KicktippAi/actions/runs/33053664914) | [`33053888656`](https://github.com/ehonda/KicktippAi/actions/runs/33053888656) | [`33054314209`](https://github.com/ehonda/KicktippAi/actions/runs/33054314209) |
| `arena-challenger-luna-none` | [`33054637395`](https://github.com/ehonda/KicktippAi/actions/runs/33054637395) | [`33054826152`](https://github.com/ehonda/KicktippAi/actions/runs/33054826152) | Initial fail-closed [`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574); forced recovery [`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055) |

The first Luna/`none` bonus attempt
[`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574)
failed closed on the first question because its stored prediction lacked
current immutable provenance and could not be reused with
`force_prediction=false` / `max_repredictions=0`. No model call is evidenced,
and final verification was skipped. This remains fail-closed evidence.

The payload-safe audit closes inspection for all completed rows and the failed
Luna bonus boundary. Four real generated configurations through Terra/`xhigh`
comprise eight match/bonus trace families and exactly 56 successful generations
(`36` match / `20` bonus), all index `0`, none at index `1+`, no errors, and
`$0.5683818` actual cost. Luna/`none` match
trace `45fc73cb82fc28c0366a6476a8127e4f` adds nine clean v3/index-0 generations
at `$0.0039741`, bringing the pre-recovery successful P0-21 total to 65
generations and `$0.5723559`. All generated observations use the exact selected
model/reasoning/cap-`10000` identity, hosted match-v3 or bonus-v1 hash, roster
snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`.
No WM26, Bundesliga 2025/26, or transfer-document name appears. Flex-first
succeeded except for two successful Standard fallbacks among Terra/`xhigh`'s
14 calls.

Both compatible copy paths generated zero calls and copied 9/9 matches plus
5/5 bonus answers without independent fallback. Failed Luna bonus trace
`0cf1515e96813b42b4625f61d5350d73` contains one root span and zero
generations, usage, or cost. Its pre/post prediction inventory is byte-identical
at SHA-256
`02ce5533a1fbaec39555f7b4f55fe399d541ee6b17fa9612383a4b26ac86f4d0`;
the five old P0-25 v1/index-0 bonus records were unchanged and no index `1+`
existed.

The Owner-approved forced recovery
[`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055)
completed successfully on exact head
`89b875125fdae207b6f6f72cff8f968a718b112f` with
`force_prediction=true`, `max_repredictions=0`, `ehonda-ai-arena` as both
posting target and context, competition `bundesliga-2026-27`, Luna/`none`, cap
`10000`, hosted `production` bonus prompt version `1`, and context budgets
20 documents / 32000 tokens. Initial verification expectedly found 0/5
current-provenance rows; the run saved five, posted all five selections together,
and passed final 5/5 verification. Inventory SHA-256 changed from
`0ab5df24cc2ac909e7b0f230427de28245334a40dab90a08402550c1a5ac2be2` to
`9f824612b8d4e98c2fb314708ef886597904b607cc704c4a3d940c4521601c94`.
The same five document IDs remain, all five are index `0`, none exists at index
`1+`, and created/updated timestamps refreshed into
`2026-08-27T15:42:47Z`–`15:43:33Z`. Resolved manifests remained 5/5;
compatibility manifests advanced 0/5 to 5/5. Three selection hashes were
unchanged and two changed.

Recovery trace `0510f8a12d3d95c5923c89abff118ded` started at
`2026-08-27T15:42:33.502Z`, took `60.904s`, and has one root plus five clean
`predict-bonus` generations with zero errors, warnings, or status messages. All
five are independent, repredict mode false, reprediction index `0`, exact
Luna/`none` / cap `10000` / production bonus v1, and Flex-to-Flex without
fallback. Exact bonus hash is
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.
Usage is `43,249` input + `98` output = `43,347` total, zero cache-read and zero
reasoning tokens; `$0.0043249` input + `$0.0000588` output = `$0.0043837`.
Context contains only Club Elo, team-squad summary, and 18 rosters, using roster
snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`;
zero WM26, Bundesliga 2025/26, experiment, or transfer names occur. Successful
P0-21 evidence now totals 70 generations / `$0.5767396`. The Luna/`none` manual
triad and recovery gate are complete. No production-live operation overlapped
the recovery.

The first `relaxdays-tippt` context attempt
[`33047564359`](https://github.com/ehonda/KicktippAi/actions/runs/33047564359)
failed after a successful pinned overlay because its target-owned community
rules source was absent. Exact repair commit
`eedf33052591beb5bbdc51c9e0ebe9869d5ab64d` added the deterministic source,
passed exact-head CI run
[`33049482431`](https://github.com/ehonda/KicktippAi/actions/runs/33049482431),
and preceded the successful retry above.

The green workflow/final-verifier and payload-safe evidence establishes working
credentials, current-season readiness, posting behavior, exact generated
identity, and zero extra model generations on compatible copy paths for every
ready row. The Luna/`none` forced-recovery gate is complete and must not be
repeated. At that ready-row checkpoint, `schadensfresse` and the first scheduled
observation remained open. The former later completed its manual ladder and was
included by ADR-0055; natural run `33143114280` later closed the observation.

## Official opening matchday facts

The official Bundesliga fixture sources were checked on 2026-08-25. Matchday 1
runs from Friday 28 August through Sunday 30 August 2026. Europe/Berlin is on
CEST (UTC+02:00) on those dates.

| Date | Official kickoff Europe/Berlin | UTC | Fixture |
|---|---:|---:|---|
| Fri 2026-08-28 | 20:30 CEST | 18:30 UTC | FC Bayern München–VfB Stuttgart |
| Sat 2026-08-29 | 15:30 CEST | 13:30 UTC | RB Leipzig–Borussia Mönchengladbach |
| Sat 2026-08-29 | 15:30 CEST | 13:30 UTC | 1. FSV Mainz 05–SC Paderborn 07 |
| Sat 2026-08-29 | 15:30 CEST | 13:30 UTC | 1. FC Union Berlin–Eintracht Frankfurt |
| Sat 2026-08-29 | 15:30 CEST | 13:30 UTC | 1. FC Köln–TSG 1899 Hoffenheim |
| Sat 2026-08-29 | 15:30 CEST | 13:30 UTC | SV Elversberg–Bayer 04 Leverkusen |
| Sat 2026-08-29 | 18:30 CEST | 16:30 UTC | Borussia Dortmund–Hamburger SV |
| Sun 2026-08-30 | 15:30 CEST | 13:30 UTC | Sport-Club Freiburg–SV Werder Bremen |
| Sun 2026-08-30 | 17:30 CEST | 15:30 UTC | FC Augsburg–FC Schalke 04 |

Primary sources:

- [Official Bundesliga Matchday 1 fixture page](https://www.bundesliga.com/en/bundesliga/matchday/2026-2027/1)
- [Official Bundesliga Media fixture table with local and UTC kickoffs](https://products.bundesliga.com/fixtures)
- [Official opening-four-matchdays timing announcement, 15 July 2026](https://www.bundesliga.com/en/bundesliga/news/confirmed-kick-off-times-dates-2026-27-fixtures-23955)
- [Official 2026/27 fixture-list announcement, 2 July 2026](https://www.bundesliga.com/en/bundesliga/news/2026-27-fixture-lists-now-available-38068)
- [Official 2026/27 season calendar](https://www.bundesliga.com/en/bundesliga/news/calendar-for-2026-27-season-world-cup-34676)

An authenticated GET-only audit at 2026-08-27 10:19:52 CEST (08:19:52 UTC)
checked the matching sibling profile for `pes-squad`, `relaxdays-tippt`, and
`ehonda-ai-arena`. All three `/spielregeln` pages reported
`Tippabgaberegel: 0 Minuten Vorlaufzeit`, and all nine match controls were open.
Each community exposed five bonus questions / seven selection controls, all
open, with the same first/common submission deadline as the Bayern–Stuttgart
kickoff: 2026-08-28 20:30 CEST / 18:30 UTC. The arena audit used Luna/`none`
credentials because rules and deadlines are community-scoped. This is
point-in-time evidence only: rescheduling or an administrator rule change
requires a fresh read. That three-community audit did not cover
`schadensfresse`; its separate 2026-08-27 11:41 CEST audit below recorded the
historical NOT READY state later superseded by administrator setup and the
completed ladder. The old zero-minute rule was not launch-deadline evidence.

## Accepted activation boundaries

The activation contract inherits, but does not modify, these accepted
repository contracts:

- [ADR-0005](../../plans/bundesliga-2026-27/decisions/0005-launch-community-and-prediction-topology.md)
  is superseded by ADR-0052's exact four-community/challenger matrix.
- [ADR-0006](../../plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md)
  forbids silently promoting the Luna/none plumbing identity and requires one
  inspected manual production run before final schedules are enabled.
- [ADR-0007](../../plans/bundesliga-2026-27/decisions/0007-require-context-hygiene-before-launch.md)
  makes current-season context hygiene and question-aware bonus selection launch
  requirements.
- [ADR-0039](../../plans/bundesliga-2026-27/decisions/0039-record-bundesliga-community-and-credential-topology.md)
  retains posting-target credential ownership, as refined by ADR-0052's exact
  participant profiles and resolved slots.
- [ADR-0045](../../plans/bundesliga-2026-27/decisions/0045-verify-versioned-prompt-promotion-before-validation.md)
  requires exact hosted prompt name/version/`production` membership to pass
  before model construction on the strict validation path.
- [ADR-0047](../../plans/bundesliga-2026-27/decisions/0047-observe-one-temporary-arena-luna-scheduled-cycle.md)
  proved that one outer workflow with `needs` dependencies is the safe ordering
  primitive. Independent offset crons do not establish completion ordering.
- [P0-24](../../plans/bundesliga-2026-27/tasks/p0-24-bonus-copy-post-compatibility.md)
  governs arena bonus reuse. Compatible questions/options may copy; an ordinary
  mismatch creates exactly one independent target prediction using
  `community_context: "ehonda-ai-arena"`; invalid target selection or immutable
  context-safety failure remains fail closed.

P0-06 has recorded the final production model, reasoning effort, output cap,
service-tier/fallback policy, planning orientation, arena participants, and
match v3 / bonus v1 prompt versions. P0-21 consumes those exact identities.
The launch Club Elo mode is the accepted dated seed with network fetching
disabled unless a later Accepted decision authorizes another source path.
ADR-0053 records the original ready-row schedule and Project Owner rollback
authority; ADR-0055 records the validated `schadensfresse` extension without
changing that operations contract.

## Repository preparation versus live authorization

Schedule-free repository preparation does not prove or consume a live community
permission:

- P0-19 has prepared every exact production, copy, and challenger caller with
  manual `workflow_dispatch` only and no schedule.
- The Owner confirmed all canonical Kicktipp Actions pairs provisioned, without
  API enumeration or runtime proof.
- After reviewed/integrated/pushed/green preparation, the separate Owner
  authorization above permits P0-21's ordered initial dispatch/writes for ready
  rows. Authentication/current-season readiness, failures, deadlines,
  row-specific recovery/onboarding inspection, monitoring, and rollback remain
  live duties under ADR-0053. The first scheduled observation is not yet
  evidence-complete.

The `schadensfresse` administrator setup and ordered manual ladder are complete.
Context run `33121916551`, 9/9 zero-generation match-copy run `33122627130`,
and five-of-eight cutoff-bounded bonus-copy run `33123422316` succeeded on exact
pushed head `3dd93d5`. Agents remain unauthorized to administer the community;
P1-08 owns its later CL bonus and DFB/CL match routing.

P0-23 is complete. Its originally preregistered matrix was Sol `high` /
`medium` / `none`, Terra `xhigh` / `medium` / `none`, and Luna `max` /
`medium` / `none`; Luna/`max` remained incomplete after the Owner stopped its
last retry. Sol/`xhigh` and the later Sol/`max` extension are explicitly
post-hoc exploratory evidence. ADR-0052 records the resulting Owner selection
of Sol/`xhigh` for production and the four challenger rows. Do not rerun any of
those experiment families under the consumed P0-23 authorization.

## Evidence-backed duration envelope

[P0-20's observed scheduled arena cycle](../../plans/bundesliga-2026-27/tasks/p0-20-seed-and-development-validation.md)
ran in one strict dependency chain:

| Stage | Observed UTC interval | Observed duration |
|---|---|---:|
| Context | 09:01:37–09:03:56 | 2m19s |
| Matchday, nine fixtures | 09:04:00–09:11:20 | 7m20s |
| Bonus, five questions | 09:11:23–09:16:29 | 5m06s |
| Whole outer cycle | 09:01:32–09:16:30 | 14m58s |

That cycle used Luna/none and one arena community; it is timing evidence, not
a production-model latency guarantee. Production activation therefore assigns
approximately four-to-eight times the observed stage durations and serializes
all external work. A stage finishing early does not weaken its downstream
dependency. A stage exceeding its window does not start the next stage by the
clock: the chain waits, alerts the on-call owner, and fails before a verified
Kicktipp cutoff would be endangered.

## Historical opening-cycle proposal — superseded, do not activate

The following table preserves the 2026-08-25 preregistration draft for audit
history. It predates the final community/participant matrix and observed manual
runtimes and is not an active cron or current activation proposal. In
particular, recurring automation must not include the one-time bonus calls and
must not include `schadensfresse` until its external setup and complete manual
cycle pass. Do not implement these windows.

| Order | Proposed Europe/Berlin window | UTC window | Required successful predecessors |
|---:|---|---|---|
| 1 | 04:00–04:20 CEST | 02:00–02:20 UTC | none; collect `pes-squad` context |
| 2 | 04:20–04:40 CEST | 02:20–02:40 UTC | 1; collect `schadensfresse` context |
| 3 | 04:40–05:00 CEST | 02:40–03:00 UTC | 2; collect `ehonda-ai-arena` context for independent fallback safety |
| 4 | 05:00–05:30 CEST | 03:00–03:30 UTC | 3; generate and verify `pes-squad` match predictions |
| 5 | 05:30–06:00 CEST | 03:30–04:00 UTC | 4; generate and verify `schadensfresse` match predictions |
| 6 | 06:00–06:30 CEST | 04:00–04:30 UTC | 5 and accepted `pes-squad` record; copy/generate and verify arena match predictions |
| 7 | 06:30–06:50 CEST | 04:30–04:50 UTC | 6; generate and verify `pes-squad` bonus predictions |
| 8 | 06:50–07:10 CEST | 04:50–05:10 UTC | 7; generate and verify `schadensfresse` bonus predictions |
| 9 | 07:10–07:30 CEST | 05:10–05:30 UTC | 8 and accepted `pes-squad` record; copy/generate and verify arena bonus predictions |

The proposed outer envelope is 04:00–07:30 CEST (02:00–05:30 UTC). This 3h30
envelope provides substantial latency and inspection margin relative to the
observed 14m58s single-community Luna cycle and ends 13 hours before the
official opening kickoff. It is not safe merely because it precedes kickoff.
Activation must first prove that every applicable Kicktipp match cutoff and the
bonus deadline occur after the corresponding successful final verification.

The historical draft never selected an activation cadence. Accepted ADR-0053
later selects the final ready-row topology, excludes recurring bonus work,
records the deliberate UTC/DST policy, and derives timing from the completed
manual ladder. Nothing in the historical table selects `02:00 UTC`.

## Accepted activation contract — 2026-08-27

Do not manually dispatch the integrated outer lane merely to prove its
orchestration. The completed leaf live ladder plus workflow contracts,
independent review, local validation, and exact-head CI are sufficient; another
outer run could consume match reprediction index `1` or `2` without
proportional evidence.

ADR-0053 selects cron `7 2,9 * * *`. Both values are fixed UTC and
avoid the top of the hour because
[GitHub documents possible high-load delay, especially at that time](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule):

| UTC trigger | Europe/Berlin in CEST | Europe/Berlin in CET |
| ---: | ---: | ---: |
| 02:07 | 04:07 | 03:07 |
| 09:07 | 11:07 | 10:07 |

The observed serialized leaf-validation sequence took 51m04s. A 90-minute
envelope is for monitoring and escalation, not a workflow timeout; preserve a
three-hour later-pass completion margin. The shared production-live concurrency
group uses `cancel-in-progress: false`: a running run is not cancelled, only one
pending run is retained, and a newer queued run may replace the pending one.
This is not FIFO serialization. Do not overlap any manual operation with a
running or pending production-live run.

The Project Owner accepted the exact cadence, operator/monitor/on-call and
rollback ownership, Luna/`none` schedule treatment, and first-observation
procedure on 2026-08-27. Exact activation commit `56238e5` is on `main`, and
exact-head CI run `33100581641` is green. The forced recovery is already
complete and must not be repeated. The first actual `schedule` event remains
unobserved and therefore open.

## Activation gates and current live status

The Owner's separate authorizations permitted the ordered manual writes
recorded above after each runtime check passed. The original repository
activation is complete and ADR-0055's extension is prepared; the only unchecked
item below is natural scheduled observation. This does not retroactively
invalidate successful final-verifier evidence.

- [x] **OWNER GATE — configuration:** record the exact `production-primary`
      model, reasoning, cap, prompt versions, service/fallback policy, cost
      ceiling, and arena challenger matrix after P0-23 evidence or an explicit
      accepted waiver. Luna/none must not be inherited.
- [x] **OWNER GATE — Club Elo:** use the accepted dated launch seed with network
      fetching disabled unless a separately authorized successor changes it.
- [x] **OWNER GATE — ready-community deadlines:** the timestamped GET-only audit
      above records identical match/bonus closure behavior and first deadlines
      for `pes-squad`, `relaxdays-tippt`, and `ehonda-ai-arena`. Recheck after
      any fixture rescheduling or administrator rule change.
- [x] **OWNER GATE — schadensfresse deadlines:** after the historical NOT READY
      audit, administrator setup exposed the 2026/27 season, exactly nine
      matching Bundesliga fixtures, five Bundesliga bonus questions due
      `2026-08-28T18:30:00Z`, and three CL questions due
      `2026-09-09T10:00:00Z`.
- [x] **OWNER GATE — operator:** ADR-0053 names the Project Owner as activation
      owner, first-cycle monitor, on-call responder, and rollback operator,
      with 30-minute acknowledgement and 60-minute schedule-disable targets.
- [x] **OWNER GATE — rollback:** ADR-0053 accepts exact stop triggers, the
      visible cron-removal change, manual-only fallback, and authority to
      disable after activation.
- [x] Production P0-19 entrypoints exist on the exact selected identity, are
      reviewed and green, expose manual dispatch only, and map only accepted
      credentials.
- [x] The three non-arena context callers are wired to the exact pinned
      launch-overlay step before normal profile collection; arena callers omit
      the download and retain their accepted enriched LKG path.
- [x] All per-community readiness gates below pass. Every scheduled row's
      manual ladder is green; first natural outer-lane observation remains a
      separate runtime gate.
- [x] Manual context collection succeeded and its exact run ID/completion was
      recorded before each ready-row prediction dispatch, including the pinned
      non-arena overlay and preserved arena enriched head.
- [x] Ready-row manual match and required bonus runs posted the expected
      Kicktipp values, persisted exact Firestore identities, used exact hosted
      prompts without local fallback, and passed payload-safe inspection.
- [x] No 2025/26 identity, WM26 collector/document, transfer document, wrong
      community context, extra copy-row model call, unexpected/non-zero
      reprediction index, or unintended reprediction was observed in ready-row
      evidence.

## Per-community readiness — ready-row ladder complete

The current facts below incorporate the separate read-only
[production prerequisite audit](production-prerequisite-audit-2026-08-25.md).
That audit established authentication/read evidence only; it did not prove any
posting right or authorize production. The later evidence below closes those
gates for every row, including the separately authorized `schadensfresse`
ladder.

| Matrix row | Current evidence | P0 conclusion | Schedule state |
|---|---|---|---|
| `pes-production-reference` | Context `33046582867`, match `33046770442`, and bonus `33047217909` succeeded with final verification; pinned overlay and payload-safe audit passed | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| `schadensfresse-production-copy` | After the historical NOT READY audit, administrator setup passed current-season/deadline preflight. Context `33121916551` published the accepted enriched snapshot and the complete 86-document current nine-fixture scope; this is not a strict 401-document full-season pass. Match `33122627130` copied 9/9 with zero generation/usage/cost and exact target verification. Bonus `33123422316` selected and copied the five Bundesliga questions at zero generation/usage/cost, passed final 5/5 verification, and left the three later CL questions untouched. | Complete for P0; natural run `33143114280` verified 9/9 current. P1-08 separately owns CL bonus and DFB/CL routing | Included by ADR-0055 as target context plus ordinary match copy immediately after `pes-squad`; leaf callers remain manual-only and bonus remains unscheduled |
| `relaxdays-production-copy` | Initial context run `33047564359` found the missing rules source; exact repair `eedf330` passed CI, then context `33049949393`, match `33050188533`, and bonus `33050549422` succeeded; payload audit proves 9/9 match plus 5/5 bonus copies and zero generation/fallback | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| `arena-production-copy` | Context `33050848544`, match `33051066657`, and bonus `33051557046` succeeded; payload audit proves 9/9 match plus 5/5 bonus copies, zero generation/fallback, and shared-roster preservation | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| Arena Sol/`high` | Context `33051863137`, match `33052087407`, and bonus `33052537217` succeeded with final verification and payload-safe audit | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| Arena Luna/`medium` | Context `33052882246`, match `33053095243`, and bonus `33053423396` succeeded with final verification and payload-safe audit | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| Arena Terra/`xhigh` | Context `33053664914`, match `33053888656`, and bonus `33054314209` succeeded with final verification and payload-safe audit | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule |
| Arena Luna/`none` | Context `33054637395` and match `33054826152` succeeded; bonus `33055144574` failed closed with zero side effects, then Owner-approved forced recovery `33089097055` replaced the same five index-0 records, posted all five selections, passed final 5/5 verification, and passed payload-safe audit | Complete; natural run `33143114280` verified 9/9 current | Included in ADR-0053 outer schedule; do not repeat recovery |

One community passing did not activate another. P0-21 enabled only rows whose
own manual evidence passed; all eight scheduled rows now also have the natural
observation above.

## First scheduled observation and monitoring — runtime gate open

The named monitor must watch the first outer run from creation through terminal
completion and record the exact event, ref, commit SHA, run/job IDs, dependency
order, timestamps, context publication dispositions, Kicktipp/Firestore final
verification, hosted prompt/model identity, usage/cost, fallback behavior, and
errors. The monitor must confirm no concurrent manual/live operation uses the
same external lane.

Required success behavior:

1. The one strict serial chain contains exactly these eight context→matchday
   pairs in order: `pes-squad`, `schadensfresse`, `relaxdays-tippt`, arena
   Sol/`xhigh`, arena Sol/`high`, arena Luna/`medium`, arena Terra/`xhigh`, and
   arena Luna/`none`. Each context job immediately precedes its matching
   matchday job.
2. Any job result other than success blocks every descendant. No later pair
   starts after an upstream context, matchday, copy, or verification failure.
3. Compatible `schadensfresse`, `relaxdays-tippt`, and arena Sol/`xhigh` match
   copies make zero model calls. Any unexpected incompatibility, fallback, or
   extra model call is surfaced by the matchday contract and triggers
   ADR-0053/ADR-0055 rollback;
   P0-24's bonus-copy fallback semantics do not apply to this match-only lane.
4. The four self-contained arena challengers generate only when the matchday
   contract requires a new prediction for final verification, using their exact
   accepted model, reasoning, prompt, and cap identities.
5. The outer workflow contains no bonus job, automatic workflow retry loop,
   `always()` continuation, or matrix. Its accepted
   `max_repredictions: 2` persistence bound is not an automatic workflow retry.
6. Each matchday job's final Kicktipp and Firestore verification succeeds before
   its downstream context job may start.

## Accepted rollback contract

Any context-quality failure, authentication/permission failure, deadline-risking
delay, prompt identity drift or local fallback, unexpected model/configuration,
output-cap hit, uncontrolled service fallback, cost anomaly, Kicktipp/Firestore
mismatch, wrong competition/context, extra model call, unexpected or unintended
reprediction, reprediction-index violation, or ordering violation triggers
rollback.

The accepted response is: stop later jobs through dependencies; preserve run and
trace evidence without payloads or secrets; disable/remove the production outer
schedule in a visible reviewed commit; keep all affected entrypoints manual-only;
and do not force, automatically retry, substitute Luna, or change a deadline.
Resume only after the named owner accepts the diagnosis, repair, new manual
evidence, green exact-head CI, and a revised activation decision. ADR-0053 names
the Project Owner as rollback operator and removal of the outer cron as the
exact repository change.

## Post-P0 factual follow-up

- Recheck the ready-community deadlines if a fixture is rescheduled or an
  administrator changes the zero-minute rule.
- Monitor GitHub schedule delivery latency operationally; the first occurrence
  exceeded the 90-minute observation envelope without causing overlap or
  missing the later daily occurrence.
- Complete P1-08 before the relevant CL bonus and DFB/CL match deadlines.

ADR-0053 closes the original ready-row schedule decision and ADR-0055 adds the
validated `schadensfresse` ordinary Bundesliga pair. Natural run `33143114280`
closed the P0-21 completion gate.
