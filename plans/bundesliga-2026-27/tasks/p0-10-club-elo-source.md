# P0-10 — Accept an operational Club Elo source

- Status: Complete
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md)

## Outcome

The project has a complete, testable, source-dated Club Elo launch snapshot covering all 18 clubs and an explicit gate for later unattended network use.

## Work items

- [x] Verify Club Elo response behavior, availability, source-date semantics, and all 18 manifest aliases using a captured snapshot.
- [x] Test all 18 manifest aliases against a captured response, including the three promoted clubs.
- [x] Define the minimum valid payload and behavior for timeouts, malformed rows, duplicate clubs, missing clubs, and stale dates.
- [x] Store a complete source-dated launch seed plus the smallest permitted fixture needed for deterministic provider tests.
- [x] Keep unattended network fetching disabled until the owner records acceptable reuse terms or selects a permitted Bundesliga plus 2. Bundesliga results source for locally computed cross-division Elo; do not substitute UEFA coefficient or squad value.

## Validation

- Produce an 18-row mapping report with no fallback aliases.
- Confirm the source exposes a rating date distinct from collection time.

## Validation evidence

- `data/bundesliga-2026-27/club-elo-launch-seed.csv` captures the Germany ranking observed on 2026-08-16 with `Rated_At=2026-08-14`, a distinct UTC `Collected_At`, all 18 exact ADR-0010 aliases, positive unique global ranks, and the source URL `https://clubelo.com/GER`.
- The source page exposed all 18 values used by the seed, including promoted `Elversberg` (205/1632), `Schalke` (233/1615), and `Paderborn` (190/1639). The dated CSV endpoint timed out in two bounded read-only attempts during P0-04, so launch has no runtime endpoint dependency and timeout/partial results are represented as rejected source results that retain seed/last-known-good data.
- `BundesligaClubEloSeedTests` verifies the complete 18-row mapping, exact rating pairs, manifest alias joins, one rating date versus one collection timestamp, deterministic bytes/order, and rejection of malformed, duplicate, missing, mixed-provenance, and invalid rows.
- `BundesligaClubEloPolicyTests` verifies disabled-network, unavailable/partial candidate, seven-day freshness boundary, stale candidate, last-known-good, and strictly-newer source-date behavior. Network code and Firestore publication remain owned by P0-11.
- `dotnet run --project tests/Core.Tests` passed on 2026-08-16: 92 total, 92 succeeded, 0 failed, 0 skipped. The pre-existing `SSH.NET` NU1903 advisory warning remains unchanged.

## Complete when

- P0-11 has an accepted seed/provider contract and fixture, not just an undocumented URL.
- Launch does not depend on resolving unattended refresh terms; network activation remains an explicit late owner gate.
