# Bundesliga 2026/27 P0 closeout-ready handoff

- Handoff ID: `buli-2627-p0-closeout-ready-2026-08-25`
- Created: 2026-08-25
- Last advanced: 2026-08-28
- Status: **Active P0 closeout handoff; all manual rows are green, the eight-row schedule topology is prepared, and natural observation remains in P0-21**
- Repository: `ehonda/KicktippAi`
- Historical branch/remote baseline at creation: `main` / `origin/main`
- Historical exact clean baseline at creation: `78ee2c0aa1b4e1b0093b7ef442936cf042ad2681`
- Historical exact green Actions run at creation: [32898097769](https://github.com/ehonda/KicktippAi/actions/runs/32898097769), all 12 jobs successful

## Resume objective

Close P0 through [P0-21](../tasks/p0-21-production-activation.md). Do not
fabricate first-schedule runtime evidence. P0-06 and every schedule-free P0-19
repository row are complete; ADR-0055 prepares the validated `schadensfresse`
ordinary Bundesliga row for the strict outer lane.

## Schadensfresse activation addendum — 2026-08-28

This subsection supersedes older statements in this handoff that
`schadensfresse` is pending setup, unrun, or excluded from every prepared
schedule. Exact pushed head
`3dd93d51a98d29f4927c59642d084f12897c7285` completed context run
[`33121916551`](https://github.com/ehonda/KicktippAi/actions/runs/33121916551),
9/9 zero-generation match-copy run
[`33122627130`](https://github.com/ehonda/KicktippAi/actions/runs/33122627130),
and five-of-eight cutoff-bounded bonus-copy run
[`33123422316`](https://github.com/ehonda/KicktippAi/actions/runs/33123422316).
The 86 present context documents are the complete current nine-fixture scope,
not a strict pass of the 401-document full-season inventory.

ADR-0055 keeps cron `7 2,9 * * *` UTC and the shared non-cancelling lane, then
inserts `schadensfresse` context and ordinary match copy immediately after
`pes-squad`. The resulting topology is 16 jobs/eight context→match pairs. It
contains no bonus job, and the retained outer `workflow_dispatch` trigger must
not be used for validation. Natural scheduled observation remains open; no
statement in this handoff claims it has occurred.

## Schadensfresse copy-onboarding addendum — 2026-08-27

The administrator setup gate is now resolved. Read-only preflight found the
same nine Bundesliga fixtures as `pes-squad`, five Bundesliga bonus questions
due `2026-08-28T18:30:00Z`, and three CL questions due
`2026-09-09T10:00:00Z`. ADR-0054 supersedes the older independent-primary
statements in this handoff: ordinary Bundesliga match and opening bonus work
copies `pes-squad`, using exact five-question aliases and an inclusive initial
bonus ceiling. The P0 rules document is deliberately identical to `pes-squad`;
the live DFB/CL after-penalties exceptions and CL-specific September routing
are explicit P1-08 work.

After exact-head review/CI, run context workflow `181809317` first with the
pinned overlay, then match `343525557`, then bonus `343525555` with the pinned
ceiling. Require zero model calls/fallbacks on copy paths, final 9/9 and 5/5
verification, exact target writes, alias telemetry, and no CL-answer mutation.
Only then add target context plus copied match after `pes-squad` to the outer
lane with overlay false. Bonus stays unscheduled.

## Owner-selection and workflow closeout addendum — 2026-08-27

- [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
  selects production `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first /
  Standard-fallback, and the non-enforced USD 35 planning orientation.
- `pes-squad` is the independent primary; ADR-0054 later changed ordinary
  `schadensfresse` Bundesliga work to copy it. `relaxdays-tippt` and the arena
  Sol/`xhigh` participant also copy the exact
  `pes-squad` production identity. Self-contained arena challengers are
  Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`, all cap `10000`.
- Match prompt v3 is hosted and checked in at normalized SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`.
  `production`, `staging`, and automatic `latest` each resolve version 3;
  bonus remains version 1. Historical P0-23 runs remain immutable on v2.
- All primary/copy/challenger workflow triads are prepared as independent
  `workflow_dispatch` entrypoints with no schedules. The repository-preparation
  lane itself dispatched nothing; P0-21 has since exercised every currently
  ready ordered live row recorded below.
- The reusable context workflow now has a false-by-default pinned launch-roster
  input. `pes-squad`, `relaxdays-tippt`, and the pending `schadensfresse`
  caller opt in: their context job downloads the exact audited public artifact,
  runs the SHA/revision/date-gated paired P0-25 overlay before ordinary profile
  collection, and stops on any failure. Arena callers omit it and preserve the
  already verified shared enriched head
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
- The Owner confirmed every exact canonical Kicktipp Actions username/password
  pair in the community ledger provisioned on 2026-08-27. This is not API
  enumeration, authentication, current-season readiness, or POST evidence.
- Resume only with P0-21: wait for external `schadensfresse` setup, then repeat
  its readiness/deadline audit and complete its ordered context/prediction
  ladder. ADR-0053 now settles the Owner cadence, operating ownership, rollback,
  and activation decision; the first actual scheduled observation remains
  open. The Luna/`none` recovery is complete and its one-run authorization is
  consumed; do not dispatch a duplicate recovery.
- After this repository change is independently reviewed, integrated, pushed,
  and green, the Owner authorizes manual context-then-prediction dispatch and
  initial writes for `pes-squad`, `relaxdays-tippt`, and every selected arena
  participant. Run primaries before dependent secondaries and stop on failure.
  Keep `schadensfresse` unrun/manual-only. The successful evidence later led to
  Owner acceptance of ADR-0053's ready-row schedule.
- The schedule-free production-live precursor is integrated at exact commit
  `992af5a63c788c0cc066dce92dd1319a91e5083d`. The outer matchday caller
  serializes context immediately before each ready matchday row, contains no
  bonus or `schadensfresse` job, and shares a non-cancelling lane with the
  leaves. Independent exact-SHA review approved it with no findings;
  the workflow contract and actionlint passed with only pre-existing actionlint
  warnings, Release build completed with zero errors, and Orchestrator passed
  `1142/1142`. It was pushed/integrated to `main`; exact-head run
  [`33058783532`](https://github.com/ehonda/KicktippAi/actions/runs/33058783532)
  succeeded including Pages, and both integrated writer/reviewer worktrees were
  cleaned. This proves preparation/CI, not a dispatch or schedule outcome.
- The accepted activation contract permits no manual outer-lane pre-dispatch:
  leaf live evidence plus static/review/CI coverage suffices, while another run
  could consume index `1` or `2` repredictions. ADR-0053 selects
  `7 2,9 * * *` (02:07/09:07 UTC; 04:07/11:07 CEST;
  03:07/10:07 CET), offset from the top of the hour because GitHub documents
  possible high-load schedule delay. The observed serialized leaf ladder took
  51m04s; use 90 minutes as monitoring/escalation—not a timeout—and preserve a
  three-hour later-pass completion margin. Non-cancelling concurrency protects
  the running run but retains only one pending run, so no manual operation may
  overlap. On 2026-08-27 the Owner accepted the Project Owner as activation
  owner, first-cycle monitor, operational on-call contact, and rollback owner,
  with 30-minute acknowledgement and 60-minute schedule-disable targets. Exact
  activation commit `56238e5fd3615e11d0be2c462516e819dfded1db` is on
  `main`, and exact-head GitHub run
  [`33100581641`](https://github.com/ehonda/KicktippAi/actions/runs/33100581641)
  succeeded. The first actual scheduled observation remains open.
- GitHub workflow `343638152` is active. At the pre-observation readiness
  snapshot it had no outer run yet; the next natural occurrence was
  `2026-08-28T02:07:00Z` / 04:07 CEST, monitoring was due from 02:02 UTC, all
  nine matches were still open, and the first cutoff was 18:30 UTC. These
  facts prepare observation and do not claim runtime success.
- An authenticated GET-only `schadensfresse` audit at 2026-08-27 11:41 CEST
  (09:41 UTC) returned HTTP 200 but found no 2026/27 marker, zero open matches,
  only closed 30 May PSG–Arsenal, zero open bonus questions, eight closed
  2025/26 rows, and a still-2025/26 zero-minute rules page. It is NOT READY and
  remains absent from every schedule. After administrator setup, require the
  current marker, exactly nine open matches, current open bonus definitions,
  and current rules/deadlines before context overlay/profile, independent
  match, independent bonus, and payload-safe inspection in that order.
- The first authorized `relaxdays-tippt` context attempt
  ([Actions run `33047564359`](https://github.com/ehonda/KicktippAi/actions/runs/33047564359))
  proved authentication, current-season/match access, and successful pinned
  roster-overlay publication, then failed closed in normal profile collection
  because `community-rules/relaxdays-tippt.md` was not tracked. The bounded
  repository repair adds the exact target document with content identical to
  `pes-squad.md` plus deterministic coverage for every current Bundesliga
  production community. Exact repair commit
  `eedf33052591beb5bbdc51c9e0ebe9869d5ab64d` passed exact-head Build and Test
  run `33049482431`; the authorized context retry and both copy-prediction
  callers then succeeded. The original run remains failure evidence and no
  schedule authority is inferred.

### Ordered manual-live checkpoint — 2026-08-27

Initial prediction runs below used `force_prediction=false` /
`max_repredictions=0`; the single Owner-approved Luna/`none` bonus recovery used
`force_prediction=true` / `max_repredictions=0`. Every completed row ended with
a successful final verification:

| Row | Context | Matchday | Bonus |
| --- | --- | --- | --- |
| `pes-production-reference` | [`33046582867`](https://github.com/ehonda/KicktippAi/actions/runs/33046582867) | [`33046770442`](https://github.com/ehonda/KicktippAi/actions/runs/33046770442) | [`33047217909`](https://github.com/ehonda/KicktippAi/actions/runs/33047217909) |
| `relaxdays-production-copy` | [`33049949393`](https://github.com/ehonda/KicktippAi/actions/runs/33049949393) | [`33050188533`](https://github.com/ehonda/KicktippAi/actions/runs/33050188533) | [`33050549422`](https://github.com/ehonda/KicktippAi/actions/runs/33050549422) |
| `arena-production-copy` | [`33050848544`](https://github.com/ehonda/KicktippAi/actions/runs/33050848544) | [`33051066657`](https://github.com/ehonda/KicktippAi/actions/runs/33051066657) | [`33051557046`](https://github.com/ehonda/KicktippAi/actions/runs/33051557046) |
| `arena-challenger-sol-high` | [`33051863137`](https://github.com/ehonda/KicktippAi/actions/runs/33051863137) | [`33052087407`](https://github.com/ehonda/KicktippAi/actions/runs/33052087407) | [`33052537217`](https://github.com/ehonda/KicktippAi/actions/runs/33052537217) |
| `arena-challenger-luna-medium` | [`33052882246`](https://github.com/ehonda/KicktippAi/actions/runs/33052882246) | [`33053095243`](https://github.com/ehonda/KicktippAi/actions/runs/33053095243) | [`33053423396`](https://github.com/ehonda/KicktippAi/actions/runs/33053423396) |
| `arena-challenger-terra-xhigh` | [`33053664914`](https://github.com/ehonda/KicktippAi/actions/runs/33053664914) | [`33053888656`](https://github.com/ehonda/KicktippAi/actions/runs/33053888656) | [`33054314209`](https://github.com/ehonda/KicktippAi/actions/runs/33054314209) |
| `arena-challenger-luna-none` | [`33054637395`](https://github.com/ehonda/KicktippAi/actions/runs/33054637395) | [`33054826152`](https://github.com/ehonda/KicktippAi/actions/runs/33054826152) | Initial fail-closed [`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574); forced recovery [`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055) |

The Luna/`none` row initially stopped partial: context
[`33054637395`](https://github.com/ehonda/KicktippAi/actions/runs/33054637395)
is green on exact `eedf330`, matchday
[`33054826152`](https://github.com/ehonda/KicktippAi/actions/runs/33054826152)
is also green with final verification, and bonus
[`33055144574`](https://github.com/ehonda/KicktippAi/actions/runs/33055144574)
failed. The bonus caller authenticated and found five open questions, then
stopped on the first question because its stored Bundesliga bonus prediction
lacked current immutable provenance and could not be reused with
`force_prediction=false` / `max_repredictions=0`. No model call is evidenced;
final verification was skipped. This remains fail-closed evidence.

Across four real generated configurations through Terra/`xhigh`, the
completed-row payload audit found eight match/bonus trace families containing
exactly 56 generations (`36` match / `20` bonus), all index `0`, none at index
`1+`, no errors, and `$0.5683818` actual cost. Luna/`none` match trace
`45fc73cb82fc28c0366a6476a8127e4f` adds nine clean v3/index-0 generations at
`$0.0039741`, so the pre-recovery P0-21 total was 65 generations and
`$0.5723559`. Every generated observation has its exact selected
model/reasoning/cap-`10000` identity and exact hosted prompt. The audited
document sets retain roster snapshot
`591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
and Club Elo snapshot
`1f63ba33cb4f46bf37d21000743ca1e86b035a7ffe5792e64dddfea2336a653e`,
with no WM26, Bundesliga 2025/26, or transfer-document names. Terra/`xhigh`
used two successful Standard fallbacks among its 14 calls; every other call
used Flex.

Both compatible copy rows generated zero calls and copied 9/9 matches plus 5/5
bonus answers without independent fallback. Failed Luna bonus trace
`0cf1515e96813b42b4625f61d5350d73` has one root span and zero generations,
usage, or cost. Its pre/post prediction inventory is byte-identical at SHA-256
`02ce5533a1fbaec39555f7b4f55fe399d541ee6b17fa9612383a4b26ac86f4d0`;
the five old P0-25 v1/index-0 bonus records were unchanged and no index `1+`
existed.

The Owner approved the exact forced recovery. Bonus run
[`33089097055`](https://github.com/ehonda/KicktippAi/actions/runs/33089097055)
completed successfully on exact head
`89b875125fdae207b6f6f72cff8f968a718b112f`,
`force_prediction=true`, `max_repredictions=0`, exact `ehonda-ai-arena`
posting/context, competition `bundesliga-2026-27`, Luna/`none`, cap `10000`,
hosted `production` bonus prompt version `1`, and accepted 20-document /
32000-token budgets. Initial verification expectedly found 0/5
current-provenance rows. The run saved five, posted all five Kicktipp selections
together, and passed final verification 5/5. Inventory SHA-256 changed from
`0ab5df24cc2ac909e7b0f230427de28245334a40dab90a08402550c1a5ac2be2` to
`9f824612b8d4e98c2fb314708ef886597904b607cc704c4a3d940c4521601c94`.
The same five document IDs remain, all at index `0`, with none at index `1+`;
created/updated timestamps refreshed into
`2026-08-27T15:42:47Z`–`15:43:33Z`. Resolved manifests stayed 5/5;
compatibility manifests advanced 0/5 to 5/5. Three selection hashes stayed
unchanged and two changed.

Trace `0510f8a12d3d95c5923c89abff118ded` started at
`2026-08-27T15:42:33.502Z`, took `60.904s`, and contains one root plus five
clean `predict-bonus` generations with zero errors, warnings, or status
messages. It records independent behavior, repredict mode false, indices `|0|`,
no repredictions, exact Luna/`none` / cap `10000` / bonus-v1 hash
`332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`,
and Flex-to-Flex without fallback. Usage is `43,249` input + `98` output =
`43,347` total, zero cache-read/reasoning, at `$0.0043837` total
(`$0.0043249` input + `$0.0000588` output). Context contains only Club Elo,
team-squad summary, and 18 rosters with the accepted roster/Club Elo snapshots;
no WM26, Bundesliga 2025/26, experiment, or transfer name occurs. Successful
P0-21 evidence is now 70 generations / `$0.5767396`. No production-live
operation overlapped the recovery.

An authenticated GET-only audit at 2026-08-27 10:19:52 CEST found identical
zero-minute lead-time rules in `pes-squad`, `relaxdays-tippt`, and
`ehonda-ai-arena`; all nine match controls and all five bonus questions / seven
selection controls were open. Their first common cutoff is 2026-08-28 20:30
CEST / 18:30 UTC. The arena result is community-scoped and was read through the
Luna/`none` profile. Recheck after any fixture rescheduling or administrator
rule change. `schadensfresse` is not covered until its season setup completes.

This closes the manual workflow/authentication/posting and payload-safe
inspection rungs for every currently ready row, including proof of zero extra
generations on the compatible copy paths and the exact Luna/`none` recovery.
`schadensfresse` remains unrun, NOT READY,
and manual-only pending its administrator's season setup and full ladder.
ADR-0053 settles cadence, operator/monitor/rollback ownership, and the accepted
ready-row schedule. The first scheduled observation remains unproven.

This addendum supersedes later stale statements in this dated handoff that call
the Owner selection, production callers, challenger rows, prompt v3, or secret
provisioning unresolved.

### Schedule-free repository validation

- Release solution build completed with zero errors; existing dependency,
  nullability, and obsolete-API warnings remain unchanged.
- Full `OpenAiIntegration.Tests` passed `233/233` after the v3 mirror/hash and
  unresolved-placeholder assertions were added.
- Focused context-workflow contracts passed `12/12`, and the final full
  `Orchestrator.Tests` suite passed `1142/1142` after the launch-overlay path
  and copy-aware final-verifier remediation.
- The deterministic workflow contract passed with `2` prediction bases, `14`
  callable WM26 callers, `12` explicitly retired Bundesliga callers, and `16`
  current Bundesliga callers. Docker actionlint passed all `23` changed/new
  workflow files.
- Activation review found and repaired a final `verify-bonus` asymmetry for
  dependent communities. The verifier now maps a compatible `pes-squad`
  candidate to target-local option IDs, or exact-reads the target-context
  fallback produced by an ordinary incompatibility. It fails closed on stale
  or invalid provenance and performs no model call. The focused verifier family
  passed 67/67, including six new copy-aware regressions.
- The two checked-in match mirrors are byte-identical and reproduce normalized
  v3 SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`.
  Changed/new Markdown relative links, added-content secret patterns, and
  `git diff --check` passed.
- No workflow dispatch, model call, Kicktipp post, prediction write, schedule,
  bonus-prompt mutation, or roster publication was performed by this lane.
  Its independent exact-SHA review, integration as `c9dd22e` plus verifier
  repair `e095276`, explicit push, and exact-head CI run `33046305604` are now
  complete. Later P0-21 live work is recorded separately above.

## P0-23 completion addendum — 2026-08-26

- [P0-23](../tasks/p0-23-gpt-5-6-production-candidate-evidence.md) is complete.
  Its [quality results](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-quality-results.md)
  and [cost evidence](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-cost-results.md)
  are the current decision inputs for P0-06. No production model or arena
  participant was selected by the experiment lane.
- Eight originally planned configurations completed. Luna/max did not: its p5
  and p3 attempts ended in transient capacity failures, and the Owner explicitly
  stopped the planned p1 retry. It has no quality score, rank, confidence
  interval, or imputed quality result.
- After all eight accepted original scores were visible, the Owner added
  Sol/xhigh. Its cost row and quality run completed, but this was a post-hoc,
  data-dependent addition. Every nine-run-family inference that includes it is
  exploratory rather than preregistered confirmatory evidence.
- Final experiment accounting is USD `4.708337270000` observed plus USD
  `0.099600000000` reserved, USD `4.807937270000` all-in, and USD
  `25.192062730000` remaining under the cumulative USD 30 ceiling.
- This addendum supersedes the stale execution-state and resume instructions in
  the dated historical sections below. The original ADR/preregistration and its
  audit trail remain point-in-time records; do not rewrite them as though the
  Owner stop or Sol/xhigh addition had been planned originally. Do not perform
  another P0-23 dataset sync, model call, or experiment mutation without a new
  Owner-authorized task and fresh budget gate.

## Post-closeout Sol/`max` evidence addendum — 2026-08-27

- Under a separate Owner-authorized post-hoc extension, Sol/`max` completed the
  exact existing `10 × 20` paired sample: `200/200`, `552` total points,
  `27.6` average, and `$3.449498200000` observed run cost.
- Sol/`xhigh` remained first at `27.8`; xhigh-minus-max was `+0.2` with 95%
  bootstrap CI `[-1.2, 1.6]` and Holm-adjusted `p = 0.8918`. The extension is
  exploratory arena evidence only because it followed both prior scores and the
  production decision; it corroborates rather than reopens the Owner's
  Sol/`xhigh` selection.
- The authoritative Sol/`max` 493-call season estimate is
  `$7.903381600000`. Cumulative experiment spend after the extension is
  `$8.527485270000` observed and `$8.643116470000` bounded under the unchanged
  USD 30 ceiling.
- Exact lane commit `f7dd2aee6c35fec26a5f09df0f1a68d82495f01b` and the
  [quality-results](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-quality-results.md)
  plus [cost-results](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-cost-results.md)
  reports retain the immutable provenance. Do not rerun this extension without
  a new explicit authorization and fresh gate.

## Historical durable state at handoff creation

The dated state in this section is preserved for audit history. Where it says
P0-23 was pending or blocked, the completion addendum above is authoritative.

- `main` and `origin/main` were clean and equal at the exact baseline above.
  The exact-head Actions run was terminal green with all 12 jobs successful.
- Baseline cleanup had removed every temporary worktree. This handoff is being
  authored in one new helper-created worktree; remove it after integration and
  re-confirm that no temporary worktrees remain.
- Implementation through P0-20 and P0-24 is complete and integrated. P0-23 is
  the active closeout evidence gate; the final P0-06 selection and all P0-21
  production evidence remain open.
- P0-25 is a completed launch-data remediation under
  [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md)
  and its launch-boundary correction
  [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md).
  It adds v2 roster documents with one derived known-value subtotal per team,
  retains strict historical v1 reconstruction, and gates explicit launch
  publication on the audited artifact SHA and 464/464/450 coverage floors. Its
  explicit republish from exact-green main
  `f1cfddeb6e2f7ba376856c0843a196af104b9a5c` passed 18-team/18-derived-row and
  464/464/450 final reconstruction with unchanged headed snapshot
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
  Exactly one authorized Luna/none index-0 replacement round completed in
  [run 32917812259](https://github.com/ehonda/KicktippAi/actions/runs/32917812259)
  and passed pre/post identity, inventory, roster, and payload-safe trace checks.
  P0-25 records the full evidence; its arena authorization is consumed.
- [ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
  supersedes this handoff's provisional P0-23 owner-input template. The exact
  nine-row GPT-5.6 matrix, one cumulative USD 30 ceiling, evidence-derived cap
  mechanics, adaptive topology, and preliminary-return gate are now fixed. The
  no-spend checkpoint is independently approved, and the machine-readable
  Decimal cumulative-budget gate is integrated at exact main commit
  `0b86b11564b9cc7500b7bfaf94301e4e83263f73`; its 24 focused tests and
  exact-commit [Build and Test run 32910669112](https://github.com/ehonda/KicktippAi/actions/runs/32910669112)
  are green. The exact `1 × 1` and `5 × 4` artifacts are prepared locally and
  reproduce the frozen pool, selection, and manifest identities. Their first
  Langfuse sync remains blocked pending explicit authorization to upload the
  public cutoff-safe historical match dataset records described below. No HTTP
  or payload egress, model call, or P0-23 spend occurred; its observed
  cumulative ledger remains exactly USD 0.
- The `1 × 1` raw dataset/manifest hashes are
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717` /
  `b396ffd599c8c79569db656d66e68ebe9169caf9a7e274d1aa0e7a0c8f8017c1`;
  its canonical historical-artifact hash is
  `a03c31c174e0e0be1723b5214453a3992c2b5d023d125eb75fa658a7503c2946`.
  The `5 × 4` raw dataset/manifest hashes are
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18` /
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`;
  its canonical historical-artifact hash is
  `22dfcab23f063e2fbb7a7fa96df4f2fb5dca384bb1329adc0c33157f5419a105`.
  The exact eligible pool is `109` fixtures/hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`;
  selected-set hashes are
  `4a293d4bac8f6406cb88770332a5b85f9084f01d2f2e0227f7d52d63e93c4e16`
  and
  `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`.
- The exact pending upload contains only public historical match records:
  fixture/team names, kickoff, competition/season/community slug, matchday and
  label, Kicktipp match ID, fixture/repetition indices, and completed score.
  `slice-dataset.json` contains no historical context bodies, references, or
  hashes, prompt text, prediction output, credentials, or secrets. The local
  manifest alone retains seven context-document reference/version/timestamp/
  content-hash tuples and is not the dataset sync payload.
- `pes-squad-context-collection.yml` and
  `schadensfresse-context-collection.yml` are integrated manual-only
  `workflow_dispatch` context callers. They have no dispatch inputs,
  `workflow_call`, or schedule and had never been dispatched at handoff
  creation. ADR-0052 later added the reusable caller's exact internal launch-
  overlay opt-in.
- The four superseded `pes-squad` / `schadensfresse` Bundesliga 2025/26 match
  and bonus callers remain inert `workflow_call`-only paths with
  `retired_configuration: true`; the reusable prediction workflows reject them
  before checkout or prediction work.
- Matchday and bonus telemetry tests now explicitly prove that
  `schadensfresse` is classified as a Langfuse `production` environment, while
  retaining deterministic activity correlation. This is code evidence, not a
  live trace or posting claim.

## Historical remaining sequence from 2026-08-25 (superseded)

This sequence records the original handoff state. Resume from the current
objective and completion addendum above, not from its pending P0-23 steps.

1. **Complete:** P0-25 was independently reviewed, integrated, and green before
   the paired explicit overlay republish. The unchanged enriched snapshot passed
   the final 18/18/464/464/450 gate; the preflight inventory proved exactly nine
   Luna/none/cap-10000 records at index 0 and none at index 1+; and exactly one
   forced replacement workflow passed final verification. Exact trace
   `3c2814f7b2b6200f3cf4e4bab94d772e` had one root plus nine ordered Flex
   generations, snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
   no fallback/errors, and payload-safe usage/cost evidence. No prompt or
   prediction payload was retained. This is arena plumbing validation only.
2. **Complete:** The corrected no-spend checkpoint was independently approved,
   and the machine-readable Decimal cumulative-budget gate plus its exact
   aggregate command were integrated and validated at the exact green commit
   and Actions run recorded above. ADR-0049 authorizes its exact evidence
   program, but the first live dataset sync remains blocked on separate explicit
   authorization to upload the public cutoff-safe historical match records.
3. Collect the ADR-0049 cutoff-safe cost rows and adaptive quality evidence with
   immutable provenance. Keep cost and quality evidence separate, reuse the
   completed Luna row without another Luna model run, and return to the owner
   after the one preliminary quality-first block before any additional block.
4. P0-06 pauses for the owner to select the exact final production model,
   reasoning effort, output cap, numbered prompt versions, service-tier/fallback
   policy, cost ceiling, and challenger matrix. Record the selection, estimator
   evidence, and comparative evidence or waiver in the model ledger and a **new
   Accepted ADR**; do not edit an existing Accepted ADR to make the selection.
5. Build and review the model-bound manual matchday and bonus callers for
   `pes-squad` and `schadensfresse` using the exact selected identity. Their
   model-independent context callers are already present.
6. Build the arena production-copy callers only after the owner also supplies
   the arena participant, local profile, and exact credential names and the
   matching `pes-squad` callers are reviewed and green. Preserve P0-24 bonus
   compatibility and independent target-context fallback plus fail-closed match
   copy behavior.
7. P0-21 owns the remaining administrator and live gates: pinned enriched v2
   roster publication through the prepared ADR-0051 paired-overlay context
   path for each non-arena production community before its first prediction,
   preservation inspection of the exact already enriched arena head,
   external
   the revised `schadensfresse` ladder, names-only repository secret presence,
   authentication/current-season readiness, POST permission, exact match and
   bonus deadlines, the Club Elo operating decision, named
   operator/monitor/rollback ownership, manual context and prediction evidence,
   the new activation ADR, deliberate schedules, and first scheduled
   observation.

## Historical preregistered P0-23 owner input

[ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
is the authoritative P0-23 experiment contract. It records:

- Sol `high` / `medium` / `none`;
- Terra `xhigh` / `medium` / `none`;
- Luna `max` / `medium` / `none`;
- one cumulative USD 30 ceiling for new P0-23 attempts;
- exact preflight-derived candidate caps;
- a 20-paired-repetition full-matrix target and one quality-first preliminary
  fallback block, with explicitly weaker 15/10-repetition exploratory fallbacks
  only after the Decimal gate proves stronger options unaffordable; and
- a mandatory return to the owner after that preliminary report.

The owner still reserves final production and arena selection. Each candidate's
current official cutoff and price, the hosted prompt binding, and the exact
historical pool/manifest provenance remain execution-date fail-closed gates.

## Hard boundaries

- Preserve ADR-0049 and the preregistration as the frozen historical design. Do
  not describe the Owner-stopped Luna/max p1 attempt or post-hoc Sol/xhigh
  addition as preregistered. Any inference across all nine completed run
  families is exploratory and data-dependent.
- P0-23 is complete. Do not sync or rerun its dataset, mutate its Langfuse
  experiment state, call another model, or incur more experiment spend under
  the consumed authorization. A new experiment requires a new Owner-authorized
  task and fresh cumulative gate.
- Do not rerun the completed Luna cost row; it is reusable evidence and is not
  production selection.
- Do not manually dispatch or write while ADR-0053's production-live lane is
  running or pending. Preserve ADR-0053/ADR-0055's exact 16-job/eight-pair
  topology and rollback contract; `schadensfresse` bonus and P1-08
  mixed-competition work remain excluded. The Luna/`none` recovery authorization
  is consumed and independently grants no schedule authority.
- P0-25's authorization for exactly one arena-only Luna/none replacement round
  is consumed. Do not repeat that publish/override ladder or infer authority for
  a production-community prediction, bonus round, schedule, or P0-23 quality
  claim.
- The `schadensfresse` setup and ordered manual ladder are complete. Agents still
  do not administer that community or infer authority for its unscheduled bonus
  and P1-08 mixed-competition work from ordinary match schedule inclusion.
- Do not invent a production selection, participant, local profile, credential
  name or value, challenger, topology, budget, permission, deadline, cadence,
  rollback rule, or schedule.

## Resume protocol

1. Read [`../AGENTS.md`](../AGENTS.md), [`../README.md`](../README.md), and
   [`../execution-strategy.md`](../execution-strategy.md), then the active task
   and every linked Accepted ADR.
2. Keep the primary checkout integration-only. Create bounded writer lanes with
   `New-AgentWorktree.ps1`; never use raw `git worktree add`. Verify the
   helper-created `.codex-local/original-repository-path` locator before work.
3. Give every lane one writer and disjoint path ownership. Use at most the
   execution strategy's bounded two-writer limit and serialize Git integration,
   pushes, and live external work.
4. Freeze a scoped commit and obtain an independent review of its exact SHA.
   Integrate accepted commits sequentially against the current main head.
5. Before each push, record the exact branch, remote, status, and commit; push
   the explicit remote/branch. Reconcile every required Actions run and job to
   the exact pushed SHA before advancing.
6. After integration, remove the helper-created worktree, prune stale metadata,
   and verify that no temporary worktrees remain.

## Current resume checkpoint

Report exact main/origin SHA and CI state, worktree inventory, the ordered
manual-run table and payload-safe audit above, the completed Luna/`none`
recovery, the integrated schedule-free outer-lane precursor commit/run, the
completed `schadensfresse` ladder and ADR-0055 topology, Accepted ADR-0053's
`7 2,9 * * *` contract, and the still-unobserved first scheduled execution.
Do not assign a new P0-23 or P0-25 live lane and do not perform another P0-23
external or model action. Resume P0-21 at default-branch exact-head CI for the
ADR-0055 successor and the first natural scheduled observation; do not
repeat already green rows without new Owner authorization.
