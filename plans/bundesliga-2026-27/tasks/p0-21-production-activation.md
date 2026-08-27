# P0-21 — Validate production and activate schedules

- Status: In progress — manual cycles and their payload-safe audits are green through `pes-squad`, `relaxdays-tippt`, arena production Sol/`xhigh`, Sol/`high`, Luna/`medium`, and Terra/`xhigh`; Luna/`none` stopped on a bonus provenance failure, and its recovery, the activation decision, schedules, and `schadensfresse` remain
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), [P0-25](p0-25-roster-enrichment-and-team-total.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Owner dispatch authorization — 2026-08-27

After this repository preparation is independently reviewed, integrated,
pushed, and green, P0-21 may manually dispatch context and then predictions for
`pes-squad`, `relaxdays-tippt`, and every selected `ehonda-ai-arena`
participant. Run independent primaries before dependent secondary copies and
stop the affected chain on failure. This explicitly authorizes the resulting
initial prediction writes for those ready rows, subject to the runtime gates
below. `schadensfresse` remains unrun and manual-only pending administrator
setup.

If all manual evidence passes, the Owner also authorizes a later lane to record
the activation ADR and add schedules for only those ready rows. A manual-only
outer matchday caller is now integrated, but it contains no schedule and grants
no activation authority.

## Manual-only production live lane — 2026-08-27

Exact commit `992af5a63c788c0cc066dce92dd1319a91e5083d` prepares the
manual-only production live lane. Its outer caller has a strict default-success
context-before-matchday `needs` chain for `pes-squad`, `relaxdays-tippt`, arena
Sol/`xhigh`, Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`. It
contains no bonus job, no `schadensfresse` job, and no cron; the outer and leaf
callers share one non-cancelling production-live concurrency group.

Independent exact-SHA review approved the commit with no findings. The
prediction-workflow contract and actionlint passed; actionlint reported only
pre-existing warnings. Release build completed with zero errors, and
Orchestrator passed `1142/1142`. The commit was pushed and integrated to
`main`; exact-head GitHub run
[`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
succeeded, including Pages. The integrated writer and reviewer worktrees were
cleaned. This is repository and CI evidence only: it records no outer-lane
dispatch, schedule activation, or production outcome.

## Read-only activation recommendation — Owner decision pending

Do not manually dispatch the outer lane merely to validate orchestration. The
completed leaf live ladder plus exact workflow contracts, independent review,
local validation, and exact-head CI already cover it; another outer dispatch
could consume match reprediction index `1` or `2` without adding proportional
evidence. No manual operation may overlap a running or pending production-live
lane.

The activation audit recommends `7 2,9 * * *`, not a top-of-hour cron, because
[GitHub documents that scheduled workflows can be delayed during high loads,
especially at the start of an hour](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule).
The fixed UTC schedule would run at 02:07 and 09:07 UTC: 04:07 and 11:07 CEST,
or 03:07 and 10:07 CET. The prior serialized leaf-validation sequence took
51m04s. Use 90 minutes as a monitoring/escalation envelope, not a workflow
timeout, and retain a three-hour later-pass completion margin.

The shared concurrency group with `cancel-in-progress: false` preserves a
running job and permits only one pending job; it is not an unbounded FIFO queue,
and another queued run can replace the pending run. The operator must therefore
avoid overlapping manual operations and inspect running/pending state before
dispatch or activation. Implementing the recommendation remains an Owner gate:
one minimal reviewed patch must add a new Accepted ADR, the chosen schedule to
the outer workflow, its exact contract test, and matching documentation. The
Owner still must accept cadence, operators/monitor/rollback, the Luna/`none`
forced-recovery and schedule treatment, and first-observation ownership.

## Work items

- [x] Confirm the ready-community match and bonus submission rules and first
      cutoff. `pes-squad`, `relaxdays-tippt`, and `ehonda-ai-arena` all expose
      zero minutes lead time; the first common cutoff is Bayern–Stuttgart on
      2026-08-28 at 20:30 CEST / 18:30 UTC.
- [ ] Confirm desired refresh cadence, context-before-prediction operation,
      owners, and rollback procedure. The read-only audit recommends
      `7 2,9 * * *`; recommendation is not Owner acceptance.
- [x] Obtain and record Owner approval for the exact production model, reasoning, output cap, accepted hosted prompt versions, planning ceiling, and arena challenger matrix. ADR-0052 records Sol/`xhigh` production, all challenger caps, and proves Luna/`none` was not inherited.
- [x] Prepare the exact primary/copy/challenger callers as manual-only,
      schedule-free `workflow_dispatch` entrypoints, pin live match v3 / bonus
      v1, and record the Owner-confirmed canonical Kicktipp secret pairs.
- [x] Prepare, independently approve, integrate, and validate the manual-only
      production outer matchday lane with strict serialized ordering, shared
      non-cancelling concurrency, and no bonus, `schadensfresse`, or cron.
- [x] Prepare the reusable context workflow's false-by-default, fail-closed
      launch-roster input. `pes-squad`, `relaxdays-tippt`, and the pending
      `schadensfresse` caller opt in to download the exact public artifact and
      run the SHA/revision/date-gated paired overlay before normal profile
      collection. Arena callers omit it because their shared context already
      has the verified exact enriched head.
- [x] Record the late Club Elo decision: use the accepted dated launch seed with network fetching disabled unless a separately authorized successor decision changes it. The completed context runs reported `LaunchSeed` / `NetworkDisabled`.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Before any initial prediction in each non-arena production community,
      dispatch its prepared context caller and record the pinned overlay's
      `NotEvaluated` DuckDB membership gates plus
      `LAUNCH_ENRICHMENT_OVERLAY`, v2 headed snapshot/disposition/document
      versions, reconstructed-final totals at or above 464 ages / 464
      positions / 450 values, and exactly 18 final `Team Accumulated` rows.
      For arena, re-verify that normal profile collection preserves exact
      enriched snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
      with no regression. Roster publication alone is not prediction-posting
      or schedule authority.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
- [ ] Verify `pes-squad` and `schadensfresse` generated independently and the
      accepted `pes-squad` prediction was copy-posted to both
      `relaxdays-tippt` and the arena Sol/`xhigh` participant without an extra
      model call.
- [ ] Validate self-contained arena Sol/`high`, Luna/`medium`, Terra/`xhigh`,
      and Luna/`none` in context-before-prediction order.
      Sol/`high`, Luna/`medium`, and Terra/`xhigh` are complete; Luna/`none`
      context and matchday passed, but bonus failed closed and requires a
      deliberate remediation decision before any retry.
- [ ] For bonus copy-posting, enforce P0-24's exact normalized question and complete-option-set compatibility; every ordinary source/provenance/question/option mismatch generates and persists exactly one independent prediction under the posting target's community context in the same invocation, never the requested `pes-squad` copy-source context, while invalid target selection or immutable-context safety violations fail closed. Confirm final `verify-bonus` maps a compatible source selection to target-local option IDs, or exact-reads the independently persisted target-context fallback for an ordinary incompatibility, without creating or calling a prediction service.
- [x] Inspect the current successful production traces through the Luna/`none`
      match for competition, prompt/model identity, context documents, tokens,
      costs, service tier, and errors. Repeat this gate for the Luna/`none`
      recovery and `schadensfresse` if and when they run.
- [x] Confirm no 2025/26 identity, WM26 collector/document, or transfer
      document appears in the current successful trace families. Repeat this
      gate for later recovery/onboarding runs.
- [ ] Enable schedules only for communities whose manual evidence passed; keep failed/unverified communities manual-only.
- [ ] Observe the first scheduled context and prediction sequence and record run links/results.

## Validation evidence

Production activation validation is incomplete, but the ordered live ladder is
partially green. Every terminal run below used an exact pushed main commit.
Each prediction run used `force_prediction=false` and
`max_repredictions=0`; its final verification step succeeded:

| Row | Context | Matchday | Bonus | Current conclusion |
| --- | --- | --- | --- | --- |
| `pes-production-reference` | [`33046582867`](https://github.com/ehonda/KicktippAi/actions/runs/33046582867) | [`33046770442`](https://github.com/ehonda/KicktippAi/actions/runs/33046770442) | [`33047217909`](https://github.com/ehonda/KicktippAi/actions/runs/33047217909) | Exact Sol/`xhigh` independent-generation triad green; context published the pinned roster overlay before normal profile collection. |
| `relaxdays-production-copy` | [`33049949393`](https://github.com/ehonda/KicktippAi/actions/runs/33049949393) | [`33050188533`](https://github.com/ehonda/KicktippAi/actions/runs/33050188533) | [`33050549422`](https://github.com/ehonda/KicktippAi/actions/runs/33050549422) | Retry and both Sol/`xhigh` copy callers green after the repository rules-source repair. |
| `arena-production-copy` | [`33050848544`](https://github.com/ehonda/KicktippAi/actions/runs/33050848544) | [`33051066657`](https://github.com/ehonda/KicktippAi/actions/runs/33051066657) | [`33051557046`](https://github.com/ehonda/KicktippAi/actions/runs/33051557046) | Shared arena context preservation and both Sol/`xhigh` copy callers green. |
| `arena-challenger-sol-high` | [`33051863137`](https://github.com/ehonda/KicktippAi/actions/runs/33051863137) | [`33052087407`](https://github.com/ehonda/KicktippAi/actions/runs/33052087407) | [`33052537217`](https://github.com/ehonda/KicktippAi/actions/runs/33052537217) | Self-contained Sol/`high` triad green. |
| `arena-challenger-luna-medium` | [`33052882246`](https://github.com/ehonda/KicktippAi/actions/runs/33052882246) | [`33053095243`](https://github.com/ehonda/KicktippAi/actions/runs/33053095243) | [`33053423396`](https://github.com/ehonda/KicktippAi/actions/runs/33053423396) | Self-contained Luna/`medium` triad green. |
| `arena-challenger-terra-xhigh` | [`33053664914`](https://github.com/ehonda/KicktippAi/actions/runs/33053664914) | [`33053888656`](https://github.com/ehonda/KicktippAi/actions/runs/33053888656) | [`33054314209`](https://github.com/ehonda/KicktippAi/actions/runs/33054314209) | Self-contained Terra/`xhigh` triad green. |

The final Luna/`none` row stopped partial. Context run
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
evidences no model call, and final verification was skipped. The ordered ladder
therefore stopped. Do not retry or treat the triad as green before a deliberate
remediation decision and a new authorized validation.

The payload-safe post-run audit closes inspection for every completed row and
for the Luna/`none` failure boundary. The four real generated configurations
through Terra/`xhigh` comprise eight match/bonus trace families and exactly 56
successful Langfuse generations: 36 match and 20 bonus, all immutable index
`0`, none at index `1+`, no errors, and
`$0.5683818` actual cost. Luna/`none` match trace
`45fc73cb82fc28c0366a6476a8127e4f` adds exactly nine v3 index-0 generations at
`$0.0039741`, bringing the current successful P0-21 total to 65 generations and
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
the five old P0-25 bonus v1/index-0 records are unchanged and no index `1+`
exists. A recovery audit identifies forced index-0 replacement as the exact
safe path, but a fresh Owner approval is pending and no retry is claimed here.

The `pes-squad` runs used exact head
`e09527616aff9522d533d5e846d4543f08f9b7d8`; the later successful rows used
exact head `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d`, which passed exact-head
Build and Test run
[`33049482431`](https://github.com/ehonda/KicktippAi/actions/runs/33049482431).
These terminal workflow, final-verifier, and payload-safe audit results
establish working credentials, readiness, posting behavior, exact generation
identity, and compatible-copy behavior for those exact completed rows. They do
not authorize schedules or satisfy the still-unrun Luna/`none` recovery and
`schadensfresse` gates.

An authenticated GET-only deadline audit at 2026-08-27 10:19:52 CEST
(08:19:52 UTC) used each matching sibling profile and found identical
`Tippabgaberegel: 0 Minuten Vorlaufzeit` in `pes-squad`, `relaxdays-tippt`, and
`ehonda-ai-arena`. All nine match controls were open. Each community exposed
five bonus questions / seven selection controls, all open, with the same first
deadline: 2026-08-28 20:30 CEST / 18:30 UTC. The arena check used the
Luna/`none` profile because the rule and deadlines are community-scoped. This
is a point-in-time launch audit; fixture rescheduling or an administrator rule
change requires a fresh read. `schadensfresse` remains outside this evidence
until its new season exists.

A separate authenticated GET-only `schadensfresse` audit at 2026-08-27 11:41
CEST (09:41 UTC) returned HTTP 200 but found no Bundesliga 2026/27 marker, zero
open match controls, and only the closed 30 May PSG–Arsenal match. It found zero
open bonus questions and eight closed 2025/26 rows; `/spielregeln` still showed
the 2025/26 competition with a zero-minute rule. This is definitive NOT READY
evidence, not a current deadline result, and `schadensfresse` must remain absent
from any schedule.

After the administrator completes setup, repeat the authenticated GET-only
audit and require a Bundesliga 2026/27 marker, exactly nine open current match
controls, the current open bonus questions/options, and current rules/deadlines.
Only then dispatch its pinned-overlay context caller and inspect the exact
publication; if green, dispatch and verify independent match then bonus callers
and complete the payload-safe Firestore/Langfuse audit. Only a fully green row
may return to a later Accepted schedule decision.

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
for presence only. External remediation, runtime authentication/permission,
roster publication, manual writes, and activation remain open.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger.
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.
