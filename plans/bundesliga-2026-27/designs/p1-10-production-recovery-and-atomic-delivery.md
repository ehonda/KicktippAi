# P1-10 production recovery and atomic delivery design

- Status: Frozen recovery design, not completion evidence
- Authority: [ADR-0062](../decisions/0062-temporarily-restore-schadensfresse-copy.md)
- Baseline: `3a2ba35529b262327a3ec08e6bde47b186c8e5b2`

## Seam map

```text
pes-squad context -> pes-squad match
                              -> schadensfresse context -> schadensfresse copy match
                                                         -> relaxdays context -> relaxdays match
                                                                            -> arena pairs
```

The outer workflow remains one strict default-success serial lane with eight
context/match pairs (16 jobs). The Schadensfresse context belongs to its target
community. The match job reads `pes-squad` source context but posts with the
target `schadensfresse` credentials. A compatible copy makes zero model calls;
missing/incompatible source identity fails closed. No bonus job or separately
scheduled leaf is added.

## Invariants

- Cron is exactly `7 2,9 * * *`; concurrency is non-cancelling.
- Schadensfresse follows `pes-squad`; `relaxdays-tippt-context` follows
  Schadensfresse match; no `always()` bypass changes the serial chain.
- Recovery restores the P1-10 runtime/workflow path slice to the selected
  baseline, while keeping P1-09/P1-12 and unrelated work.
- The known rules mismatch is explicit temporary quality/provenance debt:
  source-compatible copy rules operate despite target `2/3/5`, `3/-/5`, and
  nine-point-bonus scoring.
- WM26 collectors, prompts, workflows, and contracts remain untouched.
- The only safe fallback is reviewed re-quarantine of the pair and reconnecting
  seven; lane-wide defects use ADR-0053's whole-cron disablement.

## Verification design

Before push, inspect the exact runtime/workflow diff from `3a2ba355` and
review every exception. Capture a regression for ordinary fixtures whose P1-10
typed fields are blank; it must pass before model/post code. Contract tests
must establish 8/16 ordering, source/target credential separation, zero copy
generation, fail-closed source compatibility, no scheduled bonus, and the
unchanged cron/concurrency/manual-only/default-success behavior.

Run `dotnet run` TUnit gates for Core, KicktippIntegration,
ContextProviders.Kicktipp, FirebaseAdapter, Orchestrator, and Integration;
then the Release build, workflow-contract PowerShell script, and `actionlint`.
Require P1-09, P1-12, WM26-isolation, no-active-or-pending-production-live-run,
and independent exact-SHA review evidence. Exact-head Build-and-Test CI must
pass after push. The first natural recovered schedule is complete only after
all 16 jobs, errors/usage, final writes, and zero Schadensfresse generation
have been inspected.

## Atomic completion path

Commit A adds the recovery metadata. Commit B is the explicit aggregate revert
set named in ADR-0062. After A+B is reviewed and pushed to `main`, branch
`codex/01a054ee-p1-10-full` from recovered main and revert B on that branch so
the full P1-10 implementation appears as a branch-unique, reviewable diff.
The PR must replace or terminate temporary copy mode atomically by
`2026-09-08T12:00:00Z`; otherwise re-quarantine Schadensfresse and keep seven
unaffected pairs. A later regression reverts the P1-10 merge to the recovery
baseline.
