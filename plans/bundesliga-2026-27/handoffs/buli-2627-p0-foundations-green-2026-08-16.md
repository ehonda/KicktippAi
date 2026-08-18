# Bundesliga 2026/27 P0 orchestration handoff

- Handoff ID: `buli-2627-p0-foundations-green-2026-08-16`
- Created: 2026-08-16
- Intended resume: after the ChatGPT Plus weekly reset on the evening of 2026-08-20
- Repository: `ehonda/KicktippAi`
- Branch/remote: `main` / `origin/main`
- Exact head: `a0b05554db4a590b987316b590273ff4a398c382`
- CI: [Build and Test run 31972787674](https://github.com/ehonda/KicktippAi/actions/runs/31972787674), terminal success for the exact head

## Resume objective

Continue the Bundesliga 2026/27 P0 release train autonomously from the clean, green foundation. There is only one planned weekly reset before the season begins. Start with the same bounded parallelism that worked in the preceding session—one writer plus one read-heavy helper, serialized heavy commands—and reduce parallelism if allowance pressure returns. Do not spend the new window repeating completed audits.

## Read first

1. [`../AGENTS.md`](../AGENTS.md)
2. [`../README.md`](../README.md) for authoritative task statuses and dependencies
3. [`../execution-strategy.md`](../execution-strategy.md) for orchestration, Git, CI reconciliation, and late owner gates
4. The active task, its prerequisites, and linked accepted ADRs

The completed work, schemas, source evidence, and test results are already recorded in their tasks, ADRs, commits, and data READMEs. Reference those artifacts rather than reconstructing their history from this handoff.

## Durable state at pause

- P0-01, P0-02, P0-03, P0-04, P0-07, P0-08, and P0-10 are complete and pushed. The remaining status ledger is in [`../README.md`](../README.md).
- The final two commits are `28afb16 feat: add Bundesliga roster membership seed` and `a0b0555 docs: require Bundesliga history played dates`.
- The working tree was clean and synchronized with `origin/main` after the final push.
- P0-08's audited seed and quality evidence are in [`../tasks/p0-08-roster-membership-seed.md`](../tasks/p0-08-roster-membership-seed.md) and [`../../../data/bundesliga-2026-27/rosters/`](../../../data/bundesliga-2026-27/rosters/).
- P0-22 now makes exact played dates for recent/home/away history a launch gate; its source hierarchy, non-league handling, fail-closed rules, and head-to-head exclusion are in [`../tasks/p0-22-history-played-dates.md`](../tasks/p0-22-history-played-dates.md).
- The last workload estimate was that remaining P0 would consume roughly 2.5–3.5 times the preceding session's tokens at the same assurance level. One reset is unlikely to cover both all implementation and live activation, so protect the critical path and avoid duplicate investigation.

## Conversation findings not yet captured by accepted ADRs or implementation

These conclusions came from completed read-only audits. Revalidate against the current checkout, then turn durable choices into ADRs before code as required by `AGENTS.md`.

### Shared atomic publication for P0-09 and P0-11

- Add ADR-0014 for one reusable atomic context-plus-KPI snapshot repository; do not create separate roster and Club Elo publishers.
- Keep immutable payload versions in the existing `context-documents` and `kpi-documents` collections. Add shared immutable snapshot metadata and a single head per `(competition, communityContext, publicationSet)`; publication sets distinguish rosters from Club Elo.
- The snapshot's ordered `{kind,name,version,contentSha256}` entries are the latest-version pointers. Load last-known-good as head -> immutable snapshot -> exact version rows, validating scope, required set/order, hashes, and snapshot ID.
- Publish with `expectedPreviousSnapshotId` in a retry-safe Firestore transaction. Read everything before writes, allocate above existing versions, reuse unchanged versions, create changed versions plus immutable metadata, and switch the head atomically. Identical snapshots are no-ops.
- Live consumers that need snapshot consistency must read exact versions through the head. Independent generic “latest” queries can straddle a commit or observe unrelated uploads and must not be used for these reserved namespaces.

### Prompt route audit for P0-05

- Both accepted hosted routes are currently absent in Langfuse: `kicktippai/bundesliga-2026-27/predict-one-match` and `kicktippai/bundesliga-2026-27/predict-bonus`.
- Add canonical local mirrors under `prompts/bundesliga-2026-27/`. One schema-aware hosted match prompt should serve responses with and without justification; do not invent a third hosted route without changing ADR-0004.
- Remove the WM-era hosted-justification rejection, keep exact `{{context_documents}}` composition, and record requested/actual source, label, name, resolved version, fallback path, and normalized content hash in telemetry. Only real hosted versions receive the Langfuse prompt link.
- Candidate plumbing may proceed, but the exact `production` promotion remains an owner-controlled gate after the final context wording is stable.

### Match and bonus context audits

- P0-12's exact Bundesliga match set is the existing seven documents followed by `club-elo-{homeSlug}.csv`, `club-elo-{awaySlug}.csv`, `roster-{homeSlug}`, and `roster-{awaySlug}`. Roster names have no `.csv` suffix. All eleven are required; missing persistent Elo/roster context must block prediction after on-demand fallback.
- Historical handoff note: remove live optional-transfer selection and the upload-transfers command. Keep WM26's eight-document contract unchanged.
- Analyze/reconstruct/experiment paths must use the shared catalog with explicit competition rather than duplicate lists.
- P0-13/P0-16 should introduce a pure bonus selector over the full `BonusQuestion`, preserving a separate WM26 branch. Bundesliga's safe aggregate baseline is `club-elo-rankings` plus `team-squad-summary`; top-scorer/coach questions add only exact manifest-targeted `roster-{slug}` documents. Never fall back to all rosters.
- Bonus metadata, reprediction, and verification must retain document kind plus snapshot/version identity so context and KPI references are checked through the correct store.
- Record one shared ADR for the P0-13/P0-16 category/order/kind/failure contract. P0-16 owns numeric document/token budgets and measurement.
- P0-15 audit found no launch-required residual `team-data` or `manager-data` field: squad size/age/value are covered by the squad summary and manager/team by roster coach rows. Retire unsupported subjective preseason assessment and coach biography/tenure fields unless the owner explicitly expands product/source scope; record the retirement in an ADR.

## Suggested execution order

1. Verify clean `main`, exact remote, current CI, task statuses, and the next ADR number.
2. Record ADR-0014 and implement the shared atomic publication/read boundary, then complete P0-11 and P0-09 against it. Prefer the smaller seed-backed Club Elo path as the first end-to-end publisher proof.
3. In the read-heavy lane, audit and draft P0-22's played-date source/match-identity ADR. Evaluate official competition sources and the existing revision-pinned DuckDB data before adding a dependency; do not guess intervening cup/UEFA/friendly dates.
4. Implement P0-05 plumbing/candidate mirrors without promoting `production`, then P0-12 and the P0-22 collector.
5. Complete P0-13/P0-14/P0-15/P0-16, followed by P0-17 through P0-19.
6. Use P0-20 for real dev/arena writes and evidence. Finish P0-06's owner-controlled selection and P0-21 only after their explicit gates pass.
7. After every push, run the exact-SHA read-only CI reconciliation loop from the execution strategy; route trivial failures back to the sole writer and create durable work items for nontrivial failures.

## Safety and owner gates

- Do not enable production schedules or silently promote the Luna/none plumbing model.
- Autonomous prediction writes remain limited to the approved dev/arena Luna/none path with an explicit output cap.
- The owner still chooses the final production model/reasoning/output cap, prompt versions, cost ceiling, arena challengers, Club Elo unattended-network policy, production schedules/rollback, and confirms production community season setup. See [`../execution-strategy.md`](../execution-strategy.md#deliberately-late-owner-gates).
- Do not print credentials, overwrite the base `.env`, delete historical Firestore data, reintroduce transfer documents, restore null competition scope, or change prediction GUID identity.
- Run all `dotnet` commands outside the sandbox with `dotnet run`, use targeted TUnit filters during tasks, and serialize full suites/Testcontainers.

## Suggested skills

Call the Skill tool only when the corresponding work begins:

- `langfuse` — P0-05 hosted prompt inventory, creation, verification, telemetry contract, and eventual label promotion.
- `estimate-experiment-cost-skill` — bounded model/configuration cost preflights for P0-06.
- `whole-season-estimates` — reproducible Bundesliga season-scale cost baseline.
- `langfuse-experiments` — only when preparing/running the owner-approved comparison evidence, not for ordinary prompt plumbing.
- `github:gh-fix-ci` — inspect and fix a failing GitHub Actions check; retain the execution strategy's read-only reconciliation step after every push.

## First resume checkpoint

Before editing, report: exact `HEAD`, clean/dirty state, current task/ADR choice, sole-writer ownership, helper's bounded read-only assignment, intended targeted tests, and whether any owner gate is actually blocking. The recommended first implementation checkpoint is an accepted shared-publication ADR plus a compiling Core repository contract, not another broad audit.
