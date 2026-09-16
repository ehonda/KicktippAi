# P1-04 issue-create fence - D0/F2 dependency

- Status: D0 documented; F2 not implemented
- Last reconciled: 2026-09-16
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

F2 starts only after D0 review plus E2 and W2-transport clearance, and releases
only with the ADR-0084 transaction/process-loss/legacy/genesis/listing/CAS
matrix, solution build, full Core/FirebaseAdapter emulator/Orchestrator suites,
deterministic Node and lock validation, fresh combined review, and exact-head
CI. V2 then wires the repository; live evidence and A2 remain later gates.

No runtime/source/live effect exists yet. P1-05/R1 remains deferred under
ADR-0082.
