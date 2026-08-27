# P0-19 — Add the `schadensfresse` production-copy workflow triad

- Status: Complete — the manual-only copy triad is validated; P0-21 owns its ADR-0055 schedule observation
- Priority: P0
- Matrix row: `schadensfresse-production-copy`
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-17](p0-17-community-scope.md), [P0-18](p0-18-base-workflow-support.md), and [P0-24](p0-24-bonus-copy-post-compatibility.md)
- Decisions: [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0048](../decisions/0048-verify-bonus-compatibility-before-reference-copy.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0053](../decisions/0053-schedule-the-production-live-matchday-lane.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), and [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md)

## Outcome

The current `schadensfresse` matchday and opening Bundesliga bonus workflows
copy the exact Owner-selected Sol/`xhigh` prediction identity from `pes-squad`.
They post with the target community's own credentials and remain manual-only;
ADR-0055 places only their target context and ordinary match copy in P0-21's
recurring outer workflow after the live ladder completed.

The target's live 2026/27 rules now match `pes-squad` for Bundesliga matches
and the five opening Bundesliga bonus questions. The checked-in P0 prompt rules
are therefore intentionally content-identical. The target also contains later
Champions-League bonus questions and DFB-Pokal/Champions-League matches whose
result basis differs. [P1-08](p1-08-schadensfresse-mixed-competition-routing.md)
owns competition-specific primary routing before those predictions are due.

## Read-only readiness evidence — 2026-08-27

- Authentication succeeded and the current season exposes exactly the same
  nine open Bundesliga matchday-1 fixtures as `pes-squad`.
- The pre-context Firestore hygiene baseline is expected 401, present 0,
  missing 401, unexpected 0, conflicts 0. Roster and Club Elo heads are absent,
  so the pinned launch overlay context workflow must succeed and be inspected
  before either prediction workflow runs.
- Eight bonus questions are open. Five Bundesliga questions are due
  `2026-08-28T18:30:00Z`; three Champions-League questions are due
  `2026-09-09T10:00:00Z`.
- The five Bundesliga option IDs, option text, and selection limits match
  `pes-squad`. Their target text differs only through five explicitly audited
  full-text aliases using the `1.BL: ` label. No generic prefix normalization
  was accepted.
- The live rules are: hidden tips until the deadline; exact score; Bundesliga
  after 90 minutes; DFB-Pokal and Champions League after penalty shootout;
  matchday-win tie break; zero-minute lead; 2/3/4 points for a win and 2/-/4
  for a draw; and four points per correct bonus answer.

This readiness evidence authorizes no live operation by itself. P0-21 owns the
already Owner-authorized dispatch/inspection ladder and any later schedule
change.

## Repository implementation

- [x] Keep `schadensfresse-context-collection.yml` target-owned, pinned to
      `bundesliga-2026-27`, and opted into the exact audited launch roster
      overlay. Context must complete before predictions.
- [x] Change the matchday caller to posting target `schadensfresse` with
      `community_context: pes-squad`, while retaining the approved
      Sol/`xhigh`/cap `10000`, match-v3, Flex-first/Standard-fallback identity.
- [x] Change the bonus caller to the same copy topology and pin its initial
      manual cutoff to `2026-08-28T18:30:00Z`.
- [x] Add an optional strict-UTC, inclusive bonus deadline ceiling to the
      reusable generate and verify path. The default is empty and preserves
      every existing caller's behavior. An explicit ceiling that selects zero
      open questions fails visibly in both generation and verification.
- [x] Resolve only the exact audited tuple
      `bundesliga-2026-27` / `schadensfresse` / `pes-squad` and five full target
      question texts to source-lookup projections. The raw target question is
      retained for target context, fallback, persistence, POST, and display.
- [x] Use the same projection for source lookup, compatibility mapping, and
      source freshness in both generation and verification. Keep the raw target
      compatibility hash and record alias IDs, source/target text hashes, and
      projected compatibility hashes in trace metadata.
- [x] Preserve every current non-target bonus select value when the complete
      Kicktipp form is posted, so a cutoff-scoped write cannot erase later CL
      answers.
- [x] Keep `community-rules/schadensfresse.md` content-identical to
      `community-rules/pes-squad.md` for the supported P0 Bundesliga identity.
      Record the complete mixed live contract in ADR-0054 and P1-08.
- [x] Keep all three leaves `workflow_dispatch`-only. Do not add
      `schadensfresse` to `buli2627-production-live-matchday.yml` in this task.
- [x] Retain the exact `SCHADENSFRESSE_KICKTIPP_USERNAME` and
      `SCHADENSFRESSE_KICKTIPP_PASSWORD` target credential pair. Copy topology
      never selects credentials from the source context.
- [x] Add focused runner, verifier, form-preservation, rule-identity, workflow-
      contract, and alias telemetry tests.

## P0-21 activation boundary

Run the following only from a reviewed, pushed, green exact head:

1. Dispatch context workflow `181809317` with the pinned launch overlay and
   inspect publication heads/hygiene.
2. Dispatch match workflow `343525557`; require nine compatible copies, zero
   model generations, final 9/9 verification, and exact target POST.
3. Dispatch bonus workflow `343525555` with ceiling
   `2026-08-28T18:30:00Z`; require five compatible copies, zero fallbacks/model
   calls, final 5/5 verification, alias telemetry, and no mutation of the three
   later CL questions.
4. Inspect Kicktipp, Firestore, Langfuse, prompt/model/context identity, usage,
   and errors without recording private prediction payloads.
5. Only after all evidence is green, add target context then copied matchday to
   the recurring outer lane after `pes-squad`, with overlay false. Bonus stays
   out of the recurring lane; P1-08 owns later mixed bonus operation.

## Local validation

- Prediction workflow contract passed: 2 bases, 14 callable WM26 callers, 12
  explicitly retired Bundesliga callers, and 16 current Bundesliga callers.
- Docker actionlint passed the three changed workflows with the unchanged
  SC2129 style baseline excluded.
- Release solution build passed with zero errors; the existing SSH.NET NU1903
  advisory remained the only build-warning family.
- Full affected TUnit suites passed: Orchestrator 1161/1161,
  KicktippIntegration 196/196, and ContextProviders.Kicktipp 53/53.
- `git diff --check` passed, and the published schadensfresse/pes Bundesliga
  rules content is identical.

## Complete when

The repository contract and tests above are green and handed to P0-21. The
manual ladder and ADR-0055 topology are recorded in P0-21; natural scheduled
observation remains P0-21 work, not evidence claimed by this task.
