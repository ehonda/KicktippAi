# ADR-0063: Construct the P1-10 full branch after recovery

- Status: Accepted
- Date: 2026-08-31
- Decision authority: Project Owner through the resumed orchestration

## Context

ADR-0062 restored the temporary recovery-copy runtime on `main` through
aggregate revert B. Its future-branch instruction to revert B alone is stale:
that inverse would remove the independently retained standings-reuse telemetry
patch D and can create a modify/delete conflict around the recovery-only
blank-fixture coverage.

The recovered-main and branch base is D,
`d47c1b2b8f47b2755d9c382c46b830876efccbaf`, with green Build-and-Test run
[`33393738486`](https://github.com/ehonda/KicktippAi/actions/runs/33393738486).
D must remain an ancestor of the branch tip.

## Decision

Construct `codex/01a054ee-p1-10-full` non-rewriting from D in this exact
history order:

1. Revert recovery-only C, original `22a0c6d`, as dedicated branch commit
   `0e4f3a9`.
2. Revert aggregate recovery B, original `68af9e1`, as dedicated branch commit
   `dc29899`.

C precedes B to avoid the known modify/delete collision. C is recovery-only
and incompatible with ADR-0058's final target-primary contract. B restores the
archival P1-10 implementation; D overlaps that area and is retained exactly.
Do not reset, rebase, squash, force-push, or otherwise rewrite history.

The construction seed is acceptable only when all of the following are true:

- Across `.github/`, `community-rules/`, `data/`, `src/`, and `tests/`, the
  branch equals A (`c0aa524`) / the original `71637cc` implementation except
  for D's exact standings-reuse telemetry patch.
- The recovery blank-fixture test and recovery copy argument are absent.
- D is an ancestor, and the whole-repository delta from A is only D plus this
  ADR and the four linked planning documents.
- Review inspects history and the two-dot tree/diff, not a pull-request diff
  alone. It also checks the accepted-baseline/A-to-tip diff because the
  restored baseline contains byte-identical historical whitespace.
- Scope and secrets review pass; the listed Core, KicktippIntegration,
  ContextProviders.Kicktipp, FirebaseAdapter, Orchestrator, and Integration
  TUnit projects, Release build, workflow-contract script, and `actionlint`
  pass; and Build-and-Test is green for the exact PR head.

A-equivalence proves preservation only, not merge readiness. The branch stays
draft and live-broken until ordinary fixture typing and every remaining P1-10
and Owner gate pass.

## Alternatives considered

- **Revert B alone:** Rejected because it loses D and risks the C-related
  modify/delete conflict.
- **Reset, rebase, or squash the branch:** Rejected because the recovery and
  retained-patch provenance must stay independently reviewable.

## Consequences

This supersedes only ADR-0062's stale future-branch instruction to revert B
alone. ADR-0062's recovery runtime, sunset, fallback, rollback, and recovery
owner remain unchanged; ADR-0058/0059/0060 final contracts also remain
unchanged. This is not the final merge-time termination ADR.

It grants no merge, activation, dispatch, prompt promotion, model or cost
change/call, prediction operation, or external write authority. It creates no
new Owner decision.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)
- [P1 recovery execution packet](../p1-execution-packet.md)
- [P1-10 production recovery design](../designs/p1-10-production-recovery-and-atomic-delivery.md)

## Supersedes

- [ADR-0062](0062-temporarily-restore-schadensfresse-copy.md), only its stale
  future-branch instruction to revert B alone.
