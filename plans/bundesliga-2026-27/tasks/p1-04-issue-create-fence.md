# P1-04 issue-create fence - D0/F2 dependency

- Status: In progress; D0 documented; F2 acceptance pending
- Last reconciled: 2026-09-27
- Depends on: [ADR-0084](../decisions/0084-fence-context-source-issue-creation.md), E2 clearance, and W2 transport clearance

## Outcome

Close the cross-process duplicate-create interval for the one production Club
Elo health-issue marker. D0 records the accepted durable contract only. F2
later implements the marker-wide Ready -> CreateUncertain -> Bound /
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

Source-only/workflow/preservation implementation, bounded live evidence including controlled refresh failure with ordinary continuation, all-eight activation and closeout remain later unimplemented gates. Historical diagnostic budgets stay spent. P1-05/R1 remains deferred; no runtime or live result is credited by documentation.
