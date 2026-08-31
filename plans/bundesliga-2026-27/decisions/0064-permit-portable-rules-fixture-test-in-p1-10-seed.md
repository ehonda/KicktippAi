# ADR-0064: Permit portable rules-fixture test in the P1-10 seed

- Status: Accepted
- Date: 2026-08-31
- Decision authority: root-orchestrator re-freeze under ADR-0061 after distinct architecture/spec review

## Context

ADR-0063's construction assertions allowed only D outside the restored P1-10
implementation. Commit E, `798fb890843ccbf9cdb4a84cf1357c2540a2375e`, has
parent `300ae2d` and changes one test path only:
`tests/ContextProviders.Kicktipp.Tests/SchadensfresseLiveRulesExtractorTests.cs`.
It normalizes the checked-in fixture text with `.ReplaceLineEndings("\n")` so
the test harness is portable across line-ending conventions.

This is a test-harness portability exception, not a fixture, runtime, hash,
rules, workflow, model, prompt, storage, activation, external-write, or
authority change. ADR-0063 remains Accepted and byte-identical. This is not
the final ADR-0062 termination decision.

## Decision

Supersede only ADR-0063's two D-only equivalence assertions. The frozen branch
comparison bases are now:

- `.github/`, `community-rules/`, `data/`, and `src/` equal A/original
  `71637cc` implementation exactly.
- `tests/` equals A/original `71637cc` implementation except D's exact
  standings-reuse telemetry test and E's exact portable-rules-fixture test.
- Planning compared with A (`c0aa524`) allows exactly ADR-0063, this ADR-0064,
  and the README, P1-10 task, P1 execution packet, and recovery design.

Authority remains distinct: ADR-0062 owns the recovery runtime; ADR-0063 owns
non-rewriting branch construction; this ADR-0064 owns only E's test exception.
All ADR-0063 construction, draft-only, non-rewriting, sunset, fallback, and
rollback boundaries remain unchanged.

## Alternatives considered

- **Treat E as an implementation exception:** Rejected because it changes only
  test input normalization and cannot alter the fixture bytes or production
  behavior.
- **Rewrite ADR-0063:** Rejected because accepted ADRs remain immutable and
  its construction decision is unchanged.

## Consequences

Before publication, the final branch gate requires the six allowed planning
paths; D and E as the only test exceptions; required ancestry; absence of the
recovery blank-fixture test and recovery copy argument; valid links, clean
diff and secrets review, and a clean tree; plus independent whole-tip review.
Only then may the reviewed exact head receive one non-force push, a draft PR,
and its exact-head CI gate.

This creates no new Owner decision and grants no merge, dispatch, activation,
prompt/model/cost change or call, prediction operation, or external-write
authority.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)
- [P1 recovery execution packet](../p1-execution-packet.md)
- [P1-10 production recovery design](../designs/p1-10-production-recovery-and-atomic-delivery.md)

## Supersedes

- [ADR-0063](0063-construct-p1-10-full-branch-after-recovery.md), only its
  two D-only equivalence assertions.
