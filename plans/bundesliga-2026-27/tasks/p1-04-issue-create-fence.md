# P1-04 issue-create fence - D0/F2 dependency

- Status: Complete; D0 contract documented and F2 durable issue fence accepted in the flags-off runtime
- Last reconciled: 2026-09-30
- Depends on: [ADR-0084](../decisions/0084-fence-context-source-issue-creation.md), E2 clearance, and W2 transport clearance

## Outcome

Close the cross-process duplicate-create interval for the one production Club
Elo health-issue marker. D0 records the accepted durable contract only. F2
implements the marker-wide Ready -> CreateUncertain -> Bound /
LegacyUncertain seam; it never treats a changed body, watermark, run, process,
or run_attempt as a new create identity.

## Required F2 behavior

Arm in a Firestore CAS before one POST; return one grant only to the committing
caller; never retry an armed dispatch. Bind a validated create response or one
unique exact-marker discovery. Re-read current Pending desired health at POST
and PATCH admission, retain uncertainty through crash/response loss, and let
the existing expected-health CAS prevent stale synchronization. Genesis creates
Ready atomically with the first production Club Elo health only when neither
health nor fence exists. Existing no-fence health is LegacyUncertain; zero
matches, deletion, ambiguity, or removed marker are Pending with owner-directed
manual recovery, never reset/create/delete/foreign edit.

## Release and validation

[ADR-0085](../decisions/0085-isolate-optional-club-elo-refresh.md) supplies the current finite release graph: reviewed successor documents, concrete transport/security clearance and F2 before runtime isolation. ADR-0084's real-transaction process-loss, crash/replay, legacy/genesis, listing and stale-CAS correctness matrix remains required. Focused regressions, one coherent solution build, required exact-head project CI and fresh review replace duplicate local full-suite and capability gates. Unavailable local emulator capability transfers execution to the exact runner; it is never a pass. Known product/security defects remain blockers.

F2's real-transaction correctness matrix, focused regressions, and exact-head CI were accepted with the flags-off runtime in [PR #116](https://github.com/ehonda/KicktippAi/pull/116). The later source-only, controlled optional-failure, all-eight activation, and first scheduled observation gates are closed in the [P1-04 task](p1-04-club-elo-refresh.md) and [operational evidence](../evidence/p1-04-operational-activation.md). Historical diagnostic budgets stay spent. P1-05/R1 remains deferred.
