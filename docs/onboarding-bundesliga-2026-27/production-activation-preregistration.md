# Bundesliga 2026/27 production activation preregistration

**Status:** DRAFT — schedules remain disabled; this document authorizes no
production operation.

**Prepared:** 2026-08-25

This preregistration makes the remaining P0-21 facts, owner gates, sequence,
and rollback expectations reviewable before production activation. It does not
replace the required Accepted activation ADR and does not satisfy any manual or
scheduled evidence item in [P0-21](../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md).

## Owner-selection and repository-preparation addendum — 2026-08-27

[ADR-0052](../../plans/bundesliga-2026-27/decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
settles the previously open configuration and topology gates:

- production is `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first with one
  Standard fallback;
- match prompt v3 is promoted at normalized SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`;
  bonus remains v1;
- `pes-squad` and `schadensfresse` are independent primaries;
  `relaxdays-tippt` and arena Sol/`xhigh` copy the `pes-squad` reference; and
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
and inspection. `schadensfresse` still awaits external new-season setup. No
schedule may be enabled until the final Owner activation gate.

### Owner dispatch authorization — 2026-08-27

Once repository preparation is independently reviewed, integrated, pushed, and
green, the Owner authorizes P0-21 to dispatch context and then predictions for
`pes-squad`, `relaxdays-tippt`, and every selected arena participant, with
primaries before dependent secondaries and an immediate stop of an affected
chain on failure. The authorization includes the resulting initial prediction
writes for rows that pass their runtime checks. `schadensfresse` remains
unrun/manual-only until its administrator finishes new-season setup.

Successful inspected manual evidence permits a later activation lane to author
the schedule ADR and schedules for ready rows. The current callers remain
manual-only and schedule-free; no schedule is preregistered as already active.

No authenticated community lookup, workflow dispatch, model or Langfuse call,
schedule change, prediction write, or other external mutation was performed to
prepare this draft.

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

These are fixture kickoffs, not Kicktipp submission cutoffs. The exact
per-community match submission cutoff and the one-time bonus deadline have not
been verified from each target community. Kicktipp may close entry before the
official kickoff. No schedule or opening write may rely on kickoff as a proxy.

## Accepted activation boundaries

The draft inherits, but does not modify, these accepted repository contracts:

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
Club Elo unattended-network policy, exact schedules, and rollback authority
remain Owner-controlled P0-21 inputs.

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
  inspection, monitoring, rollback, and the later schedule ADR remain live
  gates.

The `schadensfresse` setup request is external and pending with its community
administrator. The agent is not authorized or expected to administer that
community; P0-21 must verify the external result before dispatch.

P0-23's earlier Terra/`medium`, Sol/`medium`, cap-`10000`, and `15 × 20`
surface is a superseded provisional example, not the selected experiment
surface. [ADR-0049](../../plans/bundesliga-2026-27/decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
supplies the owner-authorized exact matrix: Sol `high` / `medium` / `none`,
Terra `xhigh` / `medium` / `none`, and Luna `max` / `medium` / `none`, under
one cumulative USD 30 ceiling for new P0-23 attempts. The preregistered cost
and cutoff-safe quality evidence remains pending and is not a production-model
or arena-participant selection. The completed Luna cost row remains reusable
without another model run.

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

## Proposed opening-cycle windows — OWNER GATE

The following is a conservative first-cycle proposal, not an active cron. It
uses one outer workflow with a non-cancelling concurrency group and explicit
success dependencies. Each row is an operational observation window within the
one run, never an independent scheduled trigger.

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

The exact activation date and durable cadence are **OWNER GATE**. A candidate
first scheduled observation could use this 04:00 CEST / 02:00 UTC outer start
only after all manual production evidence passes and the deadlines are verified.
The required activation ADR must decide whether the bonus stage is removed or
left as a verification/no-op after the one-time bonus cutoff. It must also define
DST-safe UTC cron changes rather than assuming one UTC time remains 04:00 local
throughout the season.

## Required gates before any manual production write

- [x] **OWNER GATE — configuration:** record the exact `production-primary`
      model, reasoning, cap, prompt versions, service/fallback policy, cost
      ceiling, and arena challenger matrix after P0-23 evidence or an explicit
      accepted waiver. Luna/none must not be inherited.
- [ ] **OWNER GATE — Club Elo:** accept unattended network-source/reuse terms or
      select dated-seed operation with network fetching disabled.
- [ ] **OWNER GATE — deadlines:** read and record the exact match-submission and
      bonus deadlines shown by each target community, including timezone and
      retrieval time. Reconcile any difference per community; do not infer.
- [ ] **OWNER GATE — operator:** name the activation owner, schedule merger,
      first-cycle monitor, on-call responder, community administrators, and
      rollback operator. Record one reachable escalation path and response SLO.
- [ ] **OWNER GATE — rollback:** accept exact stop triggers, repository rollback
      change, manual-only fallback, and authority to disable before activation.
- [x] Production P0-19 entrypoints exist on the exact selected identity, are
      reviewed and green, expose manual dispatch only, and map only accepted
      credentials.
- [x] The three non-arena context callers are wired to the exact pinned
      launch-overlay step before normal profile collection; arena callers omit
      the download and retain their accepted enriched LKG path.
- [ ] All per-community readiness gates below pass.
- [ ] Manual context collection succeeds and its exact run ID/completion is
      recorded before each matching prediction dispatch. For non-arena rows,
      the same run must record the successful overlay and preserved enriched
      head; for arena rows it must record preservation of the verified shared
      enriched head.
- [ ] Manual match and required bonus runs post the expected Kicktipp values,
      persist exact Firestore identities, use exact hosted prompts without local
      fallback, and pass payload-safe Langfuse/cost/error inspection.
- [ ] No 2025/26 identity, WM26 collector/document, transfer document, wrong
      community context, extra model call, unexpected/non-zero reprediction
      index, or unintended reprediction is observed.

## Per-community readiness — all currently blocked for live dispatch

The current facts below incorporate the separate read-only
[production prerequisite audit](production-prerequisite-audit-2026-08-25.md).
That audit established authentication/read evidence only; it did not prove any
posting right or authorize production. These are P0-21 live gates, not blockers
to the schedule-free repository preparation described above.

| Matrix row | Current unresolved fact | Evidence required before manual dispatch | Schedule state |
|---|---|---|---|
| `pes-production-reference` | Authentication and current 9-fixture/18-team Bundesliga 2026/27 read readiness passed; exact secrets and pinned overlay caller are prepared, but POST permission is unknown | P0-21 obtains posting permission and exact deadlines, inspects the overlay/profile context run, then inspects one independent-generation cycle | No active schedule; dispatch forbidden until gates pass |
| `schadensfresse-production-independent` | Authentication passed and the pinned overlay caller is prepared, but the community exposed 9 completed / 0 pending results and 0 current prediction inputs; Bundesliga 2026/27 is not ready and POST permission is unknown; external setup request pending | External community administrator fixes the 2026/27 competition; P0-21 then verifies readiness/permission/deadlines, inspects the overlay/profile context run, and inspects one independent-generation cycle | No active schedule; do not dispatch pending setup |
| `relaxdays-production-copy` | Exact secrets and pinned overlay caller are prepared; runtime readiness, default-rule compatibility, and POST permission are unknown | Authenticate, verify rules/readiness/deadlines, inspect the overlay/profile context run, and inspect one reference-copy cycle | No active schedule; dispatch forbidden until gates pass |
| `arena-production-copy` | Sol/`xhigh`, its exact profile/secrets, and callers are prepared; runtime readiness and POST permission are unknown | Authenticate, verify fixture and P0-24 bonus compatibility, and inspect copy without an extra call or exact target-context fallback when incompatible | No active schedule; dispatch forbidden until gates pass |
| Arena Sol/`high`, Luna/`medium`, Terra/`xhigh`, Luna/`none` | Exact rows and callers are admitted; secrets are Owner-confirmed present | Authenticate each participant, verify readiness/deadlines/POST permission, and inspect one self-contained cycle | No active schedule; dispatch forbidden until each row passes |

One community passing does not activate another. P0-21 may enable only a row
whose own manual evidence passes; failed or unverified rows remain manual-only
or nondeployable.

## First scheduled observation and monitoring — OWNER GATE

The named monitor must watch the first outer run from creation through terminal
completion and record the exact event, ref, commit SHA, run/job IDs, dependency
order, timestamps, context publication dispositions, Kicktipp/Firestore final
verification, hosted prompt/model identity, usage/cost, fallback behavior, and
errors. The monitor must confirm no concurrent manual/live operation uses the
same external lane.

Required success behavior:

1. Context failure skips every dependent prediction for that context.
2. Reference prediction failure skips arena copy and all later dependent work.
3. Match failure skips bonus; no `always()` or automatic retry bypasses the
   dependency chain.
4. Copy compatibility is machine-verified. An ordinary mismatch takes exactly
   the P0-24 independent target-context path; invalid target/context safety fails.
5. Final verification agrees across Kicktipp and Firestore before the next stage.

## Rollback proposal — OWNER GATE

Any context-quality failure, authentication/permission failure, deadline-risking
delay, prompt identity drift or local fallback, unexpected model/configuration,
output-cap hit, uncontrolled service fallback, cost anomaly, Kicktipp/Firestore
mismatch, wrong competition/context, extra model call, reprediction, or ordering
violation triggers rollback.

The proposed response is: stop later jobs through dependencies; preserve run and
trace evidence without payloads or secrets; disable/remove the production outer
schedule in a visible reviewed commit; keep all affected entrypoints manual-only;
and do not force, automatically retry, substitute Luna, or change a deadline.
Resume only after the named owner accepts the diagnosis, repair, new manual
evidence, green exact-head CI, and a revised activation decision. The Accepted
activation ADR must name the rollback operator and exact repository change.

## Remaining factual gaps

- Exact Kicktipp match-submission cutoff for each of `pes-squad`,
  `schadensfresse`, and the selected arena production participant.
- Exact Kicktipp bonus deadline, question set, and complete option set for each
  target community at activation time.
- `pes-squad` POST permission and final season setup.
- `schadensfresse` Bundesliga 2026/27 readiness.
- External completion of the `schadensfresse` setup request; the agent neither
  administers nor is expected to administer the community.
- Arena production participant, credential profile, and any challengers.
- Owner-selected production configuration, cost ceiling, Club Elo operating
  mode, exact schedule/cadence, named monitor/on-call/rollback owners, and
  rollback trigger acceptance.

Until every applicable gate is closed through manual evidence and an Accepted
activation ADR, all final production schedules remain disabled.
