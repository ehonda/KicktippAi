# P0-10 — Accept an operational Club Elo source

- Status: Not started
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)

## Outcome

The project has a documented, permitted, testable source for source-dated strength ratings covering all 18 clubs.

## Work items

- [ ] Verify Club Elo endpoint behavior, availability, reuse terms, required attribution, and snapshot-date semantics.
- [ ] Test all 18 manifest aliases against a captured response, including the three promoted clubs.
- [ ] Define the minimum valid payload and behavior for timeouts, malformed rows, duplicate clubs, missing clubs, and stale dates.
- [ ] Record the accepted source and terms in an ADR.
- [ ] If Club Elo is not acceptable, select and record a licensed Bundesliga plus 2. Bundesliga results source for locally computed cross-division Elo; do not substitute UEFA coefficient or squad value.
- [ ] Store a small lawful fixture for deterministic provider tests.

## Validation

- Produce an 18-row mapping report with no fallback aliases.
- Confirm the source exposes a rating date distinct from collection time.

## Complete when

- P0-11 has an accepted source contract and fixture, not just an undocumented URL.
- The fallback decision is resolved before unattended collection is implemented.
