# Bundesliga P1 recovery execution packet

- Status: Frozen recovery packet for the resumed 2026-08-31 orchestration
- Scope: Recover the production-live lane to the P1-10 pre-runtime baseline,
  then preserve the completed P1-10 implementation for an atomic draft PR.
- Authorities: [ADR-0062](decisions/0062-temporarily-restore-schadensfresse-copy.md)
  owns recovery runtime; [ADR-0063](decisions/0063-construct-p1-10-full-branch-after-recovery.md)
  owns branch construction; [ADR-0064](decisions/0064-permit-portable-rules-fixture-test-in-p1-10-seed.md)
  permits E's test-harness exception only.

## Current state and boundary

`71637cc154cfdcbe2436069470b5e04b0d4f753d` has green Build-and-Test run
[`33340578338`](https://github.com/ehonda/KicktippAi/actions/runs/33340578338),
but production-live runs [`33350964121`](https://github.com/ehonda/KicktippAi/actions/runs/33350964121)
and [`33377913801`](https://github.com/ehonda/KicktippAi/actions/runs/33377913801)
fail in `pes-squad` ordinary blank typed-fixture validation before model/post
work. The recovery runtime baseline is
`3a2ba35529b262327a3ec08e6bde47b186c8e5b2`; it preserves P1-09 and P1-12.

This packet is not a P1-10 completion claim. P1-08 remains superseded, WM26 is
isolated, and the full target-primary implementation stays on archival/future
PR history. The recovery's temporary copy route sunsets at
`2026-09-08T12:00:00Z`.

## Frozen delivery topology

1. Preserve the archival exact `71637cc` ref already pushed.
2. Commit this recovery metadata as commit A on `main`.
3. Build aggregate recovery commit B with newest-first `git revert --no-commit`
   over exactly `1a4355f`, `552dd07`, `04a6d85`, `05d38e9`, `25fbb56`,
   `d515726`, `b0fd6b6`, `86cb5a5`, `2b91958`, `1fb6957`, `a084263`,
   `18ba841`, and `ae8fc46`.
4. Validate and independently exact-SHA review A+B, then push them to `main`.
5. ADR-0063 supersedes this stale step: construct
   `codex/01a054ee-p1-10-full` from D
   (`d47c1b2b8f47b2755d9c382c46b830876efccbaf`, green CI `33393738486`) by
   reverting C (`22a0c6d`) first as `0e4f3a9`, then B (`68af9e1`) as
   `dc29899`; preserve D. Push and open only a draft PR after the ADR-0063
   preservation and exact-head gates pass.

ADR-0064 adds E (`798fb89`, parent `300ae2d`) only: its sole extractor-test
path uses `.ReplaceLineEndings("\n")` for harness portability. The final
branch comparison is `.github/`/`community-rules/`/`data/`/`src/` exactly A or
`71637cc`; `tests/` exactly A or `71637cc` except D+E; and A-to-tip planning
only ADR-0063, ADR-0064, README, task, packet, and design.

This topology is non-rewriting: no reset, force push, rebase, squash, or
history rewrite. A later regression in the completed PR is recovered by
reverting its merge to the ADR-0062 baseline.

`ae8fc46`, `18ba841`, `a084263`, and `1fb6957` touch
`plans/bundesliga-2026-27/tasks/p1-10-schadensfresse-primary-community.md`.
After conflicts and the final inverse, B must preserve/restore that file's
exact commit-A current-state/historical-evidence content. B reverts only
runtime/workflow/test effects; it must not delete or uncheck evidence. A
runtime/workflow/test conflict is unexpected and pauses review.

## Recovery acceptance gates

- Compare the P1-10 runtime/workflow path set exactly against
  `3a2ba35529b262327a3ec08e6bde47b186c8e5b2`; only reviewed recovery metadata
  and regression evidence may differ.
- Add and pass the ordinary blank-typed-fixture regression that failed in
  production before model/post work.
- Prove eight pairs/16 jobs: Schadensfresse context then match after
  `pes-squad`; `relaxdays-tippt-context` follows the match; source context is
  `pes-squad`; posting target and credentials are `schadensfresse`; compatible
  copy performs zero model calls and fails closed; no scheduled bonus exists.
- Preserve cron `7 2,9 * * *`, non-cancelling concurrency, the exact serial
  default-success chain, leaf-manual-only boundary, and the no-bonus boundary.
- Retain P1-09 current-open-fixture and P1-12 standings-reuse behavior, and
  prove WM26 workflow/contract isolation.
- Run TUnit with `dotnet run` for Core, KicktippIntegration,
  ContextProviders.Kicktipp, FirebaseAdapter, Orchestrator, and Integration;
  run the Release build, `.github/scripts/Test-PredictionWorkflowContracts.ps1`,
  and `actionlint`.
- Before publication, ensure there is no active or pending production-live run
  and obtain independent exact-SHA scope/revert/secrets review.
- After push, the exact recovered head's Build-and-Test CI must be green.
- Observe the first natural recovered eight-pair run: all 16 jobs, expected
  source-copy usage, zero Schadensfresse generation, errors, and final
  Kicktipp/Firestore/Langfuse evidence.

## Operational and owner gates

The recovery owner is Project Owner/on-call under ADR-0053's 30-minute
acknowledgement and 60-minute whole-cron-disable trigger. A reviewed successor
may re-quarantine only the Schadensfresse pair and reconnect seven pairs if
that pair alone fails; any lane-threatening defect uses the inherited whole
cron disablement.

No manual dispatch, cancellation, force/reprediction, prompt promotion, model
or configuration change/call, prediction deletion/replacement, Kicktipp POST,
Firestore write, Langfuse mutation, credential change, or other activation is
within this packet. Later P1-10 prompt/replacement/cost/force/cutoff/activation
decisions remain owner-controlled.

Natural runs caused by the restored declarative schedule can perform only the
already-authorized ADR-0053/0054/0055 operations. Observation is read-only
reconciliation; it grants no direct/manual or expanded operational authority.

## Successor implementation boundary

This packet remains the unchanged recovery and branch-preservation contract.
[ADR-0065](decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md)
and the [P1-13 execution packet](p1-13-execution-packet.md) now own the
season-wide typed identity, exact-ID current API, isolated staging, and atomic
cutover prerequisite for P1-10. P1-10 retains Schadensfresse-specific
rules/context/prompt composition and activation. The successor creates no
recovery authority, sunset extension, partial cutover, or evidence rewrite.
