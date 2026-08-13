# P1-04 — Schedule Club Elo refreshes

- Status: Not started
- Priority: P1
- Depends on: [P0-19](p0-19-production-activation.md)

## Outcome

Club strength snapshots refresh unattended at an accepted cadence without publishing stale or partial data.

## Work items

- [ ] Use launch evidence to choose cadence, maximum source age, retry behavior, and alert ownership; record them in an ADR.
- [ ] Add an Elo-only refresh entry point or profile mode so roster/Kicktipp work is not repeated unnecessarily.
- [ ] Gate publication on 18 mapped clubs, one coherent source date, and a strictly useful version change.
- [ ] Preserve last-known-good context and surface stale-age warnings before prediction cutoff.
- [ ] Add scheduled workflow concurrency and timeout controls.
- [ ] Record version/source-date metadata in workflow summaries and traces.
- [ ] Test no-change, partial, stale, recovery, and concurrent-run scenarios.

## Validation

- Observe at least two scheduled runs, including one no-change run.
- Confirm predictions continue with last-known-good data during a simulated provider failure and expose its age.

## Complete when

- The accepted cadence is active and observable.
- Partial upstream data cannot create a new published version.
