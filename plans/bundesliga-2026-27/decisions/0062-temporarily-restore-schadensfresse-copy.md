# ADR-0062: Temporarily restore schadensfresse copy while completing P1-10 atomically

- Status: Accepted
- Date: 2026-08-31
- Decision authority: Project Owner through the resumed orchestration

## Context

The current exact failing head is `71637cc154cfdcbe2436069470b5e04b0d4f753d`.
Build-and-Test run [`33340578338`](https://github.com/ehonda/KicktippAi/actions/runs/33340578338)
is green, but production-live runs
[`33350964121`](https://github.com/ehonda/KicktippAi/actions/runs/33350964121)
and [`33377913801`](https://github.com/ehonda/KicktippAi/actions/runs/33377913801)
fail in the `pes-squad` ordinary blank typed-fixture validation before any
model or post operation. Green build CI is therefore not proof of the live
fixture shape.

The last green seven-pair run was
[`33303764698`](https://github.com/ehonda/KicktippAi/actions/runs/33303764698)
at `18ba8413e9a9df54932b574eedc7642e8e8990df`. The last green full
eight-pair/16-job run was
[`33143114280`](https://github.com/ehonda/KicktippAi/actions/runs/33143114280)
at `50f3ed148891977b5909659f9986c9c9958d7875`. The recovery baseline is
`3a2ba35529b262327a3ec08e6bde47b186c8e5b2`: it retains P1-09 and P1-12
while predating the P1-10 runtime/workflow slice.

ADR-0058 correctly identified that live `schadensfresse` scoring differs from
the historical copy premise: target scoring is `2/3/5` for wins and `3/-/5`
for draws, with nine-point bonuses. Its target-primary implementation remains
the intended final architecture, and ADR-0059/0060 remain its accepted
rules/provenance contracts. The implementation at the failing head is not
currently safe to operate. Restoring ordinary service requires a narrow,
time-bounded recovery path that does not claim the primary conversion is
complete.

## Decision

### Temporary recovery runtime

On recovery `main`, temporarily reactivate ADR-0054/0055's eight-pair/16-job
match topology. Place `schadensfresse-context` and then
`schadensfresse-matchday` immediately after `pes-squad-matchday`, with
`relaxdays-tippt-context` depending on `schadensfresse-matchday`.

The Schadensfresse context is target-owned (`community_context:
schadensfresse`). Its ordinary match copy uses `pes-squad` as the source
context, posts to `schadensfresse`, and resolves only the
`SCHADENSFRESSE_KICKTIPP_*` target credentials. Compatible copies are expected
to make zero Schadensfresse model calls; missing or incompatible source
identity fails closed. Checked-in target rules are deliberately restored to
the source-compatible copy contract for this temporary runtime. This accepts
the known target scoring/provenance-quality debt rather than silently
presenting copied predictions as target-primary results.

Keep the exact cron `7 2,9 * * *`, non-cancelling concurrency, serial
default-success chain, leaf-manual-only boundary, and no scheduled bonus work.
No `always()` continuation, independent Schadensfresse schedule, or manual-copy
contingency is introduced.

This decision narrowly and temporarily supersedes ADR-0058's quarantine and
target-primary operational clauses and the affected operational consequences
of ADR-0059/0060. ADR-0058/0059/0060 stay accepted and define the atomic P1-10
target; their documents are not rewritten.

### Authority boundary

This authorizes only restoration of the declarative schedule and its checked-in
recovery behavior. It does not authorize manual dispatch, workflow
cancellation, force or reprediction, prompt promotion, a model/configuration
change or model call, prediction deletion/replacement, Kicktipp POST,
Firestore write, Langfuse mutation, credential change, or another activation.
Natural runs caused by the restored declarative schedule may perform only the
already-authorized ADR-0053/0054/0055 operations under their existing
contract. Observing those runs is read-only reconciliation, not new operational
authority. The no-bonus and manual-only leaf boundaries remain unchanged.

### Recovery, fallback, and sunset

The recovery owner is the Project Owner/on-call and inherits ADR-0053's
30-minute acknowledgement and 60-minute whole-cron-disable trigger. If only
the Schadensfresse pair fails, a reviewed successor may re-quarantine that pair
and reconnect the seven unaffected pairs. A defect threatening the lane invokes
the inherited whole-cron disablement; do not leave a partial or silently
degraded chain running.

The temporary mode expires absolutely at `2026-09-08T12:00:00Z`. By then the
atomic P1-10 PR must merge and replace it; otherwise re-quarantine
Schadensfresse and preserve the seven unaffected pairs. P1-10 completion still
requires a final successor/termination decision at merge. If the completed PR
later regresses, revert its merge to this ADR-0062 recovery-copy baseline.

### Non-rewriting Git topology

The archival ref at exact `71637cc154cfdcbe2436069470b5e04b0d4f753d` is
already pushed. Create documentation commit A, then create aggregate recovery
commit B with newest-first `git revert --no-commit` over exactly:

`1a4355f`, `552dd07`, `04a6d85`, `05d38e9`, `25fbb56`, `d515726`,
`b0fd6b6`, `86cb5a5`, `2b91958`, `1fb6957`, `a084263`, `18ba841`, and
`ae8fc46`.

Because `ae8fc46`, `18ba841`, `a084263`, and `1fb6957` touch
`plans/bundesliga-2026-27/tasks/p1-10-schadensfresse-primary-community.md`,
B must preserve/restore commit A's exact current-state and historical-evidence
version of that file after conflict resolution and the final inverse. B reverts
runtime/workflow/test effects only; it may not delete or uncheck current-state
or historical evidence. Any runtime/workflow/test conflict is unexpected and
pauses review rather than being resolved by inference.

Validate and independently review A+B, then push them to `main`. Create
`codex/01a054ee-p1-10-full` from recovered `main`, revert B on that branch,
push it, and open a draft PR. Finish P1-10 atomically in that PR. Do not reset,
force push, rebase, or rewrite history.

## Alternatives considered

- **Leave `71637cc` on main:** Rejected because ordinary production fixtures
  fail before model/post work.
- **Revert only the observed validator:** Rejected because dependent P1-10
  layers would remain while neither the requested topology nor a known
  path-baseline is restored.
- **Roll back the full tree to the last green eight-pair run:** Rejected
  because it would discard P1-09/P1-12 and unrelated safe changes.
- **Independently generate Schadensfresse predictions:** Rejected because the
  temporary recovery must make no additional model call or activation change.

## Consequences

- Recovery restores the previous serial all-community route but temporarily
  accepts source-copy scoring/provenance debt for Schadensfresse.
- The runtime/workflow P1-10 path must match the selected baseline except
  reviewed recovery metadata and regressions.
- The first natural recovered run is required evidence; it must show all 16
  jobs, usage/errors, and zero Schadensfresse copy generation.
- The full primary conversion remains preserved for a reviewable atomic PR,
  with later owner-controlled prompt, replacement, cost, force, cutoff, and
  activation gates unchanged.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)
- [Bundesliga execution strategy](../execution-strategy.md)
- [P1 recovery execution packet](../p1-execution-packet.md)
- [P1-10 production recovery design](../designs/p1-10-production-recovery-and-atomic-delivery.md)

## Supersedes

- [ADR-0058](0058-make-schadensfresse-a-competition-typed-primary.md), only
  its immediate scheduled Schadensfresse quarantine and target-primary runtime
  operation on recovery `main`, until this ADR's sunset or a final successor.
- The operational applicability of [ADR-0059](0059-bind-schadensfresse-rules-to-a-structured-semantic-record.md)
  and [ADR-0060](0060-separate-generation-manifest-from-current-rules-attestation.md)
  while the temporary copy runtime is active. Their accepted final P1-10
  contracts remain unchanged.
