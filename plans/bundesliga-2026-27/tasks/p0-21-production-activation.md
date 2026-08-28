# P0-21 — Validate production and activate schedules

- Status: Complete — the complete manual ladder and the first natural 16-job scheduled sequence are green
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), [P0-25](p0-25-roster-enrichment-and-team-total.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), and [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md)

## Outcome

Each selected production community succeeded manually before recurring
activation. The outer matchday schedule is active, and ADR-0055 extends its
strict chain with the validated ordinary Bundesliga `schadensfresse` copy.
The first natural scheduled execution succeeded on 2026-08-28 and closes the
final P0 runtime evidence gate.

## P0 closeout observation — 2026-08-28

The first natural outer-lane run
[`33143114280`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280)
was a GitHub Actions `schedule` event on `main` at exact head
`50f3ed148891977b5909659f9986c9c9958d7875`. GitHub created and started it at
`2026-08-28T04:53:22Z`; it completed successfully at
`2026-08-28T05:32:08Z` in 38m46s. Delivery of the nominal 02:07 UTC occurrence
was 2h46m22s late, beyond the 90-minute monitoring envelope, but the run
finished before the second 09:07 UTC occurrence. There was no manual dispatch,
retry, state mutation, relevant queued run, or overlap: the lane was empty
before the event, uniquely active while it ran, and empty afterward.

All 16 jobs succeeded in strict predecessor-complete-before-successor-start
order:

| Order | Job | Job ID | UTC interval |
| ---: | --- | ---: | --- |
| 1 | `pes-squad` context | [`98758150372`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98758150372) | 04:53:26–04:55:13 |
| 2 | `pes-squad` Sol/`xhigh` match | [`98758432301`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98758432301) | 04:55:17–04:57:52 |
| 3 | `schadensfresse` context | [`98758831083`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98758831083) | 04:57:55–05:00:02 |
| 4 | `schadensfresse` copy match | [`98759163336`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98759163336) | 05:00:05–05:03:07 |
| 5 | `relaxdays-tippt` context | [`98759673897`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98759673897) | 05:03:11–05:05:15 |
| 6 | `relaxdays-tippt` copy match | [`98759998698`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98759998698) | 05:05:18–05:07:36 |
| 7 | arena Sol/`xhigh` context | [`98760375292`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98760375292) | 05:07:39–05:09:29 |
| 8 | arena Sol/`xhigh` copy match | [`98760666402`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98760666402) | 05:09:33–05:12:21 |
| 9 | arena Sol/`high` context | [`98761110474`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98761110474) | 05:12:24–05:14:37 |
| 10 | arena Sol/`high` match | [`98761465833`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98761465833) | 05:14:41–05:17:43 |
| 11 | arena Luna/`medium` context | [`98761943996`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98761943996) | 05:17:46–05:19:52 |
| 12 | arena Luna/`medium` match | [`98762267578`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98762267578) | 05:19:56–05:22:06 |
| 13 | arena Terra/`xhigh` context | [`98762613058`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98762613058) | 05:22:09–05:23:59 |
| 14 | arena Terra/`xhigh` match | [`98762897952`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98762897952) | 05:24:02–05:27:02 |
| 15 | arena Luna/`none` context | [`98763357562`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98763357562) | 05:27:05–05:28:54 |
| 16 | arena Luna/`none` match | [`98763631759`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280/job/98763631759) | 05:28:57–05:32:07 |

Every context job ran the Bundesliga 2026/27
Kicktipp→played-dates→Club-Elo→rosters profile with
`publish_launch_roster_overlay: false`. Each observed 18 teams, 306 fixtures,
nine fixtures per matchday, the nine matchday-1 fixtures, the 288-row
played-date gate, and 47 unique ordinary context documents. All 47 ordinary
documents were unchanged (`Saved 0` / `Skipped 47`). Club Elo remained
`LaunchSeed` / `NetworkDisabled` at snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`,
and rosters remained at enriched snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
The effective approved current scope was 86 documents: 47 ordinary, 18 Elo,
18 rosters, and three aggregate documents. This is not a strict pass of the
401-document full-season inventory.

Every match job passed configuration and initial verification with 9 total,
9 Kicktipp, 9 Firestore, and 9 matching predictions. Both generation/posting
and final-verification steps then skipped because the current predictions were
already complete. Thus `force_prediction=false` and `max_repredictions=2`
were unused: the scheduled observation made no write, model generation,
reprediction, token use, or cost. The exact cap-`10000`, production-labelled
hosted match-v3 identities matched ADR-0052: the three copies used
`pes-squad` context and Sol/`xhigh`; the production reference used
`pes-squad` Sol/`xhigh`; and the challengers used self-contained Sol/`high`,
Luna/`medium`, Terra/`xhigh`, and Luna/`none` contexts.

The payload-safe Langfuse production-environment query for
`2026-08-28T04:53:00Z` through `05:33:00Z`, limited to core and metrics fields,
returned no observations (`data: []`, `totalItems: 0`), consistent with zero
generation, usage, and cost; service tier and fallback are therefore not
applicable, and no trace error exists. Runtime log scans after the actual
execution markers found zero WM26, Bundesliga 2025/26, or transfer-context
identities and zero actual error outcomes. The benign configuration sentence
`Init matchday mode enabled - will return error if no predictions exist.`
contains the only runtime `error` literal and is not an error-level failure.
One WM26 literal existed only in the pre-runtime generic shell validation case
table and is not runtime contamination. This evidence closes
P0-21 and the central P0 release train. P1 is next; in particular, P1-08 still
owns `schadensfresse` CL bonus and DFB/CL primary routing, while unattended
Club Elo network refresh and exploratory model follow-ups remain non-P0.

## Owner dispatch authorization — 2026-08-27

After this repository preparation is independently reviewed, integrated,
pushed, and green, P0-21 may manually dispatch context and then predictions for
`pes-squad`, `relaxdays-tippt`, and every selected `ehonda-ai-arena`
participant. Run independent primaries before dependent secondary copies and
stop the affected chain on failure. This explicitly authorizes the resulting
initial prediction writes for those ready rows, subject to the runtime gates
below. At that 2026-08-27 gate, `schadensfresse` remained unrun and manual-only
pending administrator setup. After setup became available, the Owner authorized its complete ordered
manual ladder and schedule inclusion if green. ADR-0054 and P0-19 define the
bounded copy implementation; do not use the earlier independent path.

All originally ready-row manual evidence passed. On 2026-08-27 the Owner explicitly
accepted ADR-0053's cadence, operating ownership, rollback contract, and
schedule treatment. This activation change adds the single ready-row cron to
the outer matchday caller. After the administrator completed setup, the Owner
authorized the full `schadensfresse` ladder and, when green, schedule
inclusion. ADR-0055 records the resulting topology evolution without changing
the cron or operational contract.

## Production-live lane preparation — 2026-08-27

Exact commit `992af5a63c788c0cc066dce92dd1319a91e5083d` prepares the
production live lane. Its outer caller has a strict default-success
context-before-matchday `needs` chain for `pes-squad`, `relaxdays-tippt`, arena
Sol/`xhigh`, Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`. It
contains no bonus job or `schadensfresse` job; the outer and leaf callers share
one non-cancelling production-live concurrency group. ADR-0053's later
activation change adds the only cron without changing this 14-job topology.

Independent exact-SHA review approved the commit with no findings. The
prediction-workflow contract and actionlint passed; actionlint reported only
pre-existing warnings. Release build completed with zero errors, and
Orchestrator passed `1142/1142`. The commit was pushed and integrated to
`main`; exact-head GitHub run
[`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
succeeded, including Pages. The integrated writer and reviewer worktrees were
cleaned. This is repository and CI evidence only: it records no outer-lane
dispatch or production outcome. ADR-0055 later extends this historical
seven-pair precursor to eight pairs by inserting `schadensfresse` immediately
after `pes-squad`.

## Accepted activation contract — 2026-08-27

Do not manually dispatch the outer lane merely to validate orchestration. The
completed leaf live ladder plus exact workflow contracts, independent review,
local validation, and exact-head CI already cover it; another outer dispatch
could consume match reprediction index `1` or `2` without adding proportional
evidence. No manual operation may overlap a running or pending production-live
lane.

ADR-0053 selects `7 2,9 * * *`, not a top-of-hour cron, because
[GitHub documents that scheduled workflows can be delayed during high loads,
especially at the start of an hour](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule).
The fixed UTC schedule runs at 02:07 and 09:07 UTC: 04:07 and 11:07 CEST,
or 03:07 and 10:07 CET. The prior serialized leaf-validation sequence took
51m04s. Use 90 minutes as a monitoring/escalation envelope, not a workflow
timeout, and retain a three-hour later-pass completion margin.

The shared concurrency group with `cancel-in-progress: false` preserves a
running job and permits only one pending job; it is not an unbounded FIFO queue,
and another queued run can replace the pending run. The operator must therefore
avoid overlapping manual operations and inspect running/pending state before
dispatch or activation. The Project Owner is the activation owner, first-cycle
monitor, operational on-call contact, and rollback owner. The accepted targets
are acknowledgement within 30 minutes and schedule removal within 60 minutes
when a rollback trigger fires. Exact commit
`56238e5fd3615e11d0be2c462516e819dfded1db` activated the accepted schedule
on `main`; exact-head GitHub run
[`33100581641`](https://github.com/ehonda/KicktippAi/actions/runs/33100581641)
succeeded. At that activation checkpoint, only an actual `schedule` event could
satisfy the then-open first-observation gate. Natural run `33143114280` later
satisfied it. The separately approved Luna/`none` forced recovery is complete
and must not be repeated.

## Work items

- [x] Confirm the ready-community match and bonus submission rules and first
      cutoff. `pes-squad`, `relaxdays-tippt`, and `ehonda-ai-arena` all expose
      zero minutes lead time; the first common cutoff is Bayern–Stuttgart on
      2026-08-28 at 20:30 CEST / 18:30 UTC.
- [x] Confirm desired refresh cadence, context-before-prediction operation,
      owners, and rollback procedure. ADR-0053 records the Owner-accepted
      `7 2,9 * * *` contract and Project Owner responsibilities.
- [x] Obtain and record Owner approval for the exact production model, reasoning, output cap, accepted hosted prompt versions, planning ceiling, and arena challenger matrix. ADR-0052 records Sol/`xhigh` production, all challenger caps, and proves Luna/`none` was not inherited.
- [x] Prepare the exact primary/copy/challenger callers as manual-only,
      schedule-free `workflow_dispatch` entrypoints, pin live match v3 / bonus
      v1, and record the Owner-confirmed canonical Kicktipp secret pairs.
- [x] Prepare, independently approve, integrate, and validate the schedule-free
      precursor to the production outer matchday lane with strict serialized
      ordering, shared non-cancelling concurrency, and no bonus,
      `schadensfresse`, or cron.
- [x] Prepare the reusable context workflow's false-by-default, fail-closed
      launch-roster input. `pes-squad`, `relaxdays-tippt`, and `schadensfresse`
      callers opt in to download the exact public artifact and run the
      SHA/revision/date-gated paired overlay before normal profile collection;
      `schadensfresse` was still pending at preparation time. Arena callers omit
      it because their shared context already has the verified exact enriched
      head.
- [x] Record the late Club Elo decision: use the accepted dated launch seed with network fetching disabled unless a separately authorized successor decision changes it. The completed context runs reported `LaunchSeed` / `NetworkDisabled`.
- [x] Record the schedule and activation gate in Accepted ADR-0053.
- [x] Before initial prediction for the ready non-arena production rows,
      dispatch the prepared `pes-squad` and `relaxdays-tippt` context callers
      and record the pinned overlay's
      `NotEvaluated` DuckDB membership gates plus
      `LAUNCH_ENRICHMENT_OVERLAY`, v2 headed snapshot/disposition/document
      versions, reconstructed-final totals at or above 464 ages / 464
      positions / 450 values, and exactly 18 final `Team Accumulated` rows.
      For arena, re-verify that normal profile collection preserves exact
      enriched snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
      with no regression. Roster publication alone is not prediction-posting
      or schedule authority.
- [x] Confirm `schadensfresse` exposes the current season, exactly nine matching
      Bundesliga fixtures, five Bundesliga bonus questions due
      `2026-08-28T18:30:00Z`, and three later CL questions due
      `2026-09-09T10:00:00Z`.
- [x] Dispatch its prepared context caller and prove the same
      pinned overlay, v2 snapshot, minimum enrichment counts, and exact 18-row
      team-total contract before any prediction dispatch. Run `33121916551`
      succeeded with roster snapshot
      `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
      464/464/450 age/position/value coverage, and 18 team totals. Its 86
      current-scope documents are not a strict pass of the 401-document
      full-season inventory; the 315 absent documents are future/opposite-role
      documents outside the exact nine-fixture launch scope.
- [x] Manually dispatch production context collection and inspect all publication dispositions for the originally ready rows. `schadensfresse` now has its separate current ladder below.
- [x] Manually dispatch one production matchday run and required bonus run for every currently ready row; confirm the expected Kicktipp writes, including the approved Luna/`none` recovery.
- [x] Verify `pes-squad` generated independently and the accepted `pes-squad`
      prediction was copy-posted to both
      `relaxdays-tippt` and the arena Sol/`xhigh` participant without an extra
      model call.
- [x] Verify `schadensfresse` copies all nine match predictions from
      `pes-squad` with zero model generations and exact target writes. Run
      `33122627130` copied 9/9 at zero usage/cost, and an authenticated explicit
      matchday read confirmed all nine submitted pairs.
- [x] Run its initial bonus workflow with the inclusive ceiling
      `2026-08-28T18:30:00Z`; verify five exact alias-projected copies, zero
      fallback/model calls, and no mutation of the three later CL questions.
      Run `33123422316` selected five of eight, copied all five at zero
      usage/cost, and passed final 5/5 verification; the later CL scope remains
      excluded.
- [x] Validate self-contained arena Sol/`high`, Luna/`medium`, Terra/`xhigh`,
      and Luna/`none` in context-before-prediction order.
      Luna/`none` initially failed closed at bonus provenance, then completed
      through the one Owner-approved forced index-0 recovery.
- [x] For bonus copy-posting, enforce P0-24's exact normalized question and complete-option-set compatibility; every ordinary source/provenance/question/option mismatch generates and persists exactly one independent prediction under the posting target's community context in the same invocation, never the requested `pes-squad` copy-source context, while invalid target selection or immutable-context safety violations fail closed. Final `verify-bonus` maps a compatible source selection to target-local option IDs, or exact-reads the independently persisted target-context fallback for an ordinary incompatibility, without creating or calling a prediction service.
- [x] Inspect the current successful production traces through the Luna/`none`
      recovery for competition, prompt/model identity, context documents,
      tokens, costs, service tier, and errors. The later `schadensfresse`
      payload-safe audit repeated and passed this gate.
- [x] Confirm no 2025/26 identity, WM26 collector/document, or transfer
      document appears in the current successful trace families, including the
      completed `schadensfresse` onboarding evidence.
- [x] Activate the single outer schedule only for rows whose manual evidence
      passed. Exact `main` commit `56238e5` and exact-head CI run `33100581641`
      are green for the original rows; ADR-0055 adds the now-green
      `schadensfresse` context/copy pair while every leaf caller remains
      manual-only.
- [x] Observe the first scheduled context and prediction sequence and record
      run links/results. Natural run `33143114280` succeeded on exact head
      `50f3ed148891977b5909659f9986c9c9958d7875`; the complete 16-job evidence
      is recorded above.
- [x] After the schadensfresse manual ladder and exact-head CI are green, add
      its context then matchday copy after `pes-squad` in the recurring outer
      lane with launch overlay false. Keep bonus out of the schedule; P1-08
      owns the CL-specific September operation. ADR-0055 and the exact workflow
      contract prepared the 16-job/eight-pair topology. Default-branch CI later
      passed, and natural run `33143114280` proved the complete topology.

## Validation evidence

Production activation validation is complete. The ordered live ladder is green
for every ready row, repository activation is complete, and natural scheduled
run `33143114280` closes the final runtime observation as recorded above. The
following manual-run table remains the prerequisite evidence that preceded
schedule activation. Every terminal run below used an exact pushed main commit.
Initial prediction runs used `force_prediction=false` and
`max_repredictions=0`; the single approved Luna/`none` bonus recovery used
`force_prediction=true` / `max_repredictions=0`. Every completed row passed its
final verification step:

| Row | Context | Matchday | Bonus | Current conclusion |
| --- | --- | --- | --- | --- |
| `pes-production-reference` | [`33046582867`](https://github.com/ehonda/KicktippAi/actions/runs/33046582867) | [`33046770442`](https://github.com/ehonda/KicktippAi/actions/runs/33046770442) | [`33047217909`](https://github.com/ehonda/KicktippAi/actions/runs/33047217909) | Exact Sol/`xhigh` independent-generation triad green; context published the pinned roster overlay before normal profile collection. |
| `schadensfresse-production-copy` | [`33121916551`](https://github.com/ehonda/KicktippAi/actions/runs/33121916551) | [`33122627130`](https://github.com/ehonda/KicktippAi/actions/runs/33122627130) | [`33123422316`](https://github.com/ehonda/KicktippAi/actions/runs/33123422316) | Target-owned context and 9/9 ordinary match copies are green; the opening deadline-bounded bonus run selected and copied only the five Bundesliga questions. |
| `relaxdays-production-copy` | [`33049949393`](https://github.com/ehonda/KicktippAi/actions/runs/33049949393) | [`33050188533`](https://github.com/ehonda/KicktippAi/actions/runs/33050188533) | [`33050549422`](https://github.com/ehonda/KicktippAi/actions/runs/33050549422) | Retry and both Sol/`xhigh` copy callers green after the repository rules-source repair. |
| `arena-production-copy` | [`33050848544`](https://github.com/ehonda/KicktippAi/actions/runs/33050848544) | [`33051066657`](https://github.com/ehonda/KicktippAi/actions/runs/33051066657) | [`33051557046`](https://github.com/ehonda/KicktippAi/actions/runs/33051557046) | Shared arena context preservation and both Sol/`xhigh` copy callers green. |
| `arena-challenger-sol-high` | [`33051863137`](https://github.com/ehonda/KicktippAi/actions/runs/33051863137) | [`33052087407`](https://github.com/ehonda/KicktippAi/actions/runs/33052087407) | [`33052537217`](https://github.com/ehonda/KicktippAi/actions/runs/33052537217) | Self-contained Sol/`high` triad green. |
| `arena-challenger-luna-medium` | [`33052882246`](https://github.com/ehonda/KicktippAi/actions/runs/33052882246) | [`33053095243`](https://github.com/ehonda/KicktippAi/actions/runs/33053095243) | [`33053423396`](https://github.com/ehonda/KicktippAi/actions/runs/33053423396) | Self-contained Luna/`medium` triad green. |
| `arena-challenger-terra-xhigh` | [`33053664914`](https://github.com/ehonda/KicktippAi/actions/runs/33053664914) | [`33053888656`](https://github.com/ehonda/KicktippAi/actions/runs/33053888656) | [`33054314209`](https://github.com/ehonda/KicktippAi/actions/runs/33054314209) | Self-contained Terra/`xhigh` triad green. |
| `arena-challenger-luna-none` | [`33054637395`](https://github.com/ehonda/KicktippAi/actions/runs/33054637395) | [`33054826152`](https://github.com/ehonda/KicktippAi/actions/runs/33054826152) | Initial fail-closed [`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574); forced recovery [`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055) | Self-contained Luna/`none` triad green after exact Owner-approved recovery. |

The Luna/`none` row initially stopped partial. Context run
[`33054637395`](https://github.com/ehonda/KicktippAi/actions/runs/33054637395)
succeeded on exact head `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`;
matchday run
[`33054826152`](https://github.com/ehonda/KicktippAi/actions/runs/33054826152)
also succeeded with final verification. Bonus run
[`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574)
failed after authenticating and finding five open questions. On the first
question, pre-verification found that the stored Bundesliga bonus prediction
lacked current immutable provenance; with `force_prediction=false` and
`max_repredictions=0`, generation failed closed rather than reuse it. The log
evidences no model call, and final verification was skipped. That original run
remains fail-closed evidence.

The payload-safe post-run audit closes inspection for every completed row and
for the Luna/`none` failure boundary. The four real generated configurations
through Terra/`xhigh` comprise eight match/bonus trace families and exactly 56
successful Langfuse generations: 36 match and 20 bonus, all immutable index
`0`, none at index `1+`, no errors, and
`$0.5683818` actual cost. Luna/`none` match trace
`45fc73cb82fc28c0366a6476a8127e4f` adds exactly nine v3 index-0 generations at
`$0.0039741`, bringing the pre-recovery P0-21 total to 65 generations and
`$0.5723559`. All generated observations have their exact selected
model/reasoning/cap-`10000` identity and Langfuse actual usage/cost. Every
successful match observation uses v3 hash
`7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`;
every successful bonus observation uses v1 hash
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.
The audited document sets use roster snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`,
with zero WM26, Bundesliga 2025/26, or transfer-document names. Flex-first
worked: Terra/`xhigh` used two Standard fallbacks among its 14 successful
generations; every other generation used Flex.

The compatible `relaxdays-tippt` and arena Sol/`xhigh` paths generated zero
model calls. Each copied 9/9 matches and 5/5 bonus answers, with no independent
fallback. Failed Luna/`none` bonus trace
`0cf1515e96813b42b4625f61d5350d73` contains one root span and zero generations,
usage, or cost. Its pre/post prediction inventory is byte-identical at SHA-256
`02ce5533a1fbaec39555f7b4f55fe399d541ee6b17fa9612383a4b26ac86f4d0`;
the five old P0-25 bonus v1/index-0 records were unchanged and no index `1+`
existed. A recovery audit identifies forced index-0 replacement as the exact
safe path.

The Owner approved that exact recovery. Bonus run
[`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055)
completed successfully on exact head
`89b875125fdae207b6f6f72cff8f968a718b112f` with
`force_prediction=true`, `max_repredictions=0`, posting target and context
`ehonda-ai-arena`, competition `bundesliga-2026-27`, Luna/`none`, cap `10000`,
Langfuse prompt `kicktippai/bundesliga-2026-27/predict-bonus` with label
`production` and version `1`, trigger type `manual`, and the accepted
20-document / 32000-token context budgets. Initial verification expectedly
found 0/5 current-provenance rows. The run saved five predictions, posted all
five selections to Kicktipp together, and passed final verification 5/5.

The prediction-inventory SHA-256 changed from
`0ab5df24cc2ac909e7b0f230427de28245334a40dab90a08402550c1a5ac2be2` to
`9f824612b8d4e98c2fb314708ef886597904b607cc704c4a3d940c4521601c94`.
The same five document IDs remain; all five are index `0`, none exists at index
`1+`, and created/updated timestamps refreshed into
`2026-08-27T15:42:47Z`–`15:43:33Z`. Resolved manifests remained present 5/5;
compatibility manifests advanced from 0/5 to 5/5. Three selection hashes
remained unchanged and two changed.

Payload-safe trace `0510f8a12d3d95c5923c89abff118ded` started at
`2026-08-27T15:42:33.502Z`, took `60.904s`, and contains one root plus five
clean `predict-bonus` generations with zero errors, warnings, or status
messages. The root and generations report independent behavior, repredict mode
false, reprediction indices `|0|`, and no repredictions. All five generations
used exact Luna/`none`, cap `10000`, bonus v1 hash
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`,
and Flex-to-Flex without fallback. Usage is `43,249` input + `98` output =
`43,347` total, zero cache-read and reasoning tokens; `$0.0043249` input +
`$0.0000588` output = `$0.0043837`. Context contains only Club Elo,
team-squad summary, and 18 roster documents with roster snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`;
no WM26, Bundesliga 2025/26, experiment, or transfer name appears. Successful
P0-21 evidence now totals 70 generations / `$0.5767396`. The Luna/`none` manual
triad and its recovery gate are complete. No production-live operation
overlapped the recovery.

The `pes-squad` runs used exact head
`e09527616aff9522d533d5e846d4543f08f9b7d8`; the later successful rows used
exact head `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`, which passed exact-head
Build and Test run
[`33049482431`](https://github.com/ehonda/KicktippAi/actions/runs/33049482431).
The recovery used exact head `89b875125fdae207b6f6f72cff8f968a718b112f`.
These terminal workflow, final-verifier, and payload-safe audit results
established working credentials, readiness, posting behavior, exact generation
identity, and compatible-copy behavior for every originally ready row. At that
checkpoint they did not substitute for ADR-0053's separate schedule decision
or satisfy the then-unrun `schadensfresse` gate; the subsequent evidence below
does.

An authenticated GET-only deadline audit at 2026-08-27 10:19:52 CEST
(08:19:52 UTC) used each matching sibling profile and found identical
`Tippabgaberegel: 0 Minuten Vorlaufzeit` in `pes-squad`, `relaxdays-tippt`, and
`ehonda-ai-arena`. All nine match controls were open. Each community exposed
five bonus questions / seven selection controls, all open, with the same first
deadline: 2026-08-28 20:30 CEST / 18:30 UTC. The arena check used the
Luna/`none` profile because the rule and deadlines are community-scoped. This
is a point-in-time launch audit; fixture rescheduling or an administrator rule
change requires a fresh read. `schadensfresse` was outside this particular
evidence set until the subsequent setup and dedicated audit below.

A separate authenticated GET-only `schadensfresse` audit at 2026-08-27 11:41
CEST (09:41 UTC) returned HTTP 200 but found no Bundesliga 2026/27 marker, zero
open match controls, and only the closed 30 May PSG–Arsenal match. It found zero
open bonus questions and eight closed 2025/26 rows; `/spielregeln` still showed
the 2025/26 competition with a zero-minute rule. This was definitive NOT READY
evidence at that time, not a current deadline result, and correctly required
`schadensfresse` to remain absent from any schedule until the subsequent setup
and green ladder below.

The administrator subsequently completed setup. Current preflight now has the
2026/27 marker, exactly nine `pes-squad`-matching fixtures, five audited
Bundesliga bonus questions due `2026-08-28T18:30:00Z`, and three CL questions
due `2026-09-09T10:00:00Z`. ADR-0054 replaces the independent path: first
inspect pinned-overlay context publication; if green, verify copied match and
deadline-bounded copied bonus with no model calls, then complete the payload-
safe audit. The ordered live ladder then completed on exact pushed head
`3dd93d51a98d29f4927c59642d084f12897c7285`: context run `33121916551`,
match run `33122627130`, and bonus run `33123422316` all succeeded. Context
published the accepted enriched snapshot and complete current nine-fixture
scope. Its 86 present documents are not a strict pass of the 401-document
full-season inventory; the 315 absent documents are future head-to-head and
opposite-role history outside this launch scope. Match copied all nine source
predictions with zero model usage, and an explicit authenticated matchday read
confirmed all nine submitted score pairs. Bonus selected five of eight open
questions under the inclusive cutoff, copied all five at zero usage/cost, and
left the later Champions-League scope excluded. ADR-0055 consequently adds
only target context then ordinary match copy to the recurring lane. It does not
schedule bonus. This manual-onboarding evidence alone did not claim a natural
scheduled observation; the later closeout run above supplied it.

The first authorized `relaxdays-tippt` context dispatch, Actions run
[`33047564359`](https://github.com/ehonda/KicktippAi/actions/runs/33047564359),
proved authentication, current-season/match access, and successful pinned
roster-overlay publication. Its normal profile phase then failed closed before
completion because the repository lacked the exact required
`community-rules/relaxdays-tippt.md` source. The independently reviewed repair
added that target-owned document with content identical to `pes-squad.md` and a
deterministic all-production-community existence contract. It was integrated as
exact commit `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`, passed exact-head CI
run `33049482431`, and the authorized retry plus matchday and bonus copy runs
then succeeded as recorded above. The original failed run remains failure
evidence; it is not rewritten as a success.

A telemetry-disabled read-only refresh on 2026-08-26 reconfirmed this state
without a write or Langfuse path. `pes-squad` exited successfully with exactly
9 current inputs (0 completed / 9 pending), 18 standings teams, 47 selected
Kicktipp context documents, 288 history rows, 18 Club Elo `LaunchSeed` rows
under `NetworkDisabled`, and the 18-club fallback roster path.
`schadensfresse` authenticated and completed its GET requests, but still exposed
9 completed / 0 pending results and 0 current prediction-input rows; it exited
at the exact-nine gate and skipped later profile stages. Neither refresh exposed
deadlines or proved POST permission. A repeated names-only GitHub check remained
blocked by HTTP 403; the later Owner provisioning confirmation is authoritative
for presence only. This paragraph is historical 2026-08-26 evidence: the later
ready-row manual ladder and exact activation evidence above supersede its
then-open ready-row authentication, permission, roster, write, and activation
gates. At that boundary, the remaining items applied only to `schadensfresse`:
current-season readiness and administrator POST permission, manual context then
match/bonus evidence, payload-safe inspection, and a later row-specific
topology, activation, and observation decision. The later manual ladder and
ADR-0055 closed those items. The ready-row scheduled observation was then open;
natural run `33143114280` later closed it as recorded above.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger. **Complete in ADR-0053.**
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.
