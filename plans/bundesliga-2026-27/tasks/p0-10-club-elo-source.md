# P0-10 — Accept an operational Club Elo source

- Status: Not started
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)
- Decision: [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md)

## Outcome

The project has a complete, testable, source-dated Club Elo launch snapshot covering all 18 clubs and an explicit gate for later unattended network use.

## Work items

- [ ] Verify Club Elo response behavior, availability, source-date semantics, and all 18 manifest aliases using a captured snapshot.
- [ ] Test all 18 manifest aliases against a captured response, including the three promoted clubs.
- [ ] Define the minimum valid payload and behavior for timeouts, malformed rows, duplicate clubs, missing clubs, and stale dates.
- [ ] Store a complete source-dated launch seed plus the smallest permitted fixture needed for deterministic provider tests.
- [ ] Keep unattended network fetching disabled until the owner records acceptable reuse terms or selects a permitted Bundesliga plus 2. Bundesliga results source for locally computed cross-division Elo; do not substitute UEFA coefficient or squad value.

## Validation

- Produce an 18-row mapping report with no fallback aliases.
- Confirm the source exposes a rating date distinct from collection time.

## Complete when

- P0-11 has an accepted seed/provider contract and fixture, not just an undocumented URL.
- Launch does not depend on resolving unattended refresh terms; network activation remains an explicit late owner gate.
