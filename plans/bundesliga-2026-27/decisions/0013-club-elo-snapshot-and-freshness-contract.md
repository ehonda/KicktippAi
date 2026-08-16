# ADR-0013: Fix the Club Elo snapshot and freshness contract

- Status: Accepted
- Date: 2026-08-16

## Context

ADR-0008 permits launch from a complete dated Club Elo seed and keeps unattended network access behind an owner gate. P0-10 must make that direction implementable by fixing the snapshot schema, the exact 18-club join, provenance semantics, and the point at which a later network candidate is too old to displace a complete seed or last-known-good snapshot.

The accepted 2026-08-14 Germany ranking is safe as a launch seed even if it ages while terms remain unresolved. A network response is different: accepting an old, partial, or same-version response as a refresh would create false freshness and could overwrite a better complete snapshot.

## Decision

The checked-in launch seed is `data/bundesliga-2026-27/club-elo-launch-seed.csv` with exact columns:

`Team_Slug,Club_Elo_Name,Global_Rank,ELO,Rated_At,Collected_At,Source_Url`

It contains exactly the 18 ADR-0010 manifest identities, ordered by `Team_Slug` using ordinal comparison. `Global_Rank` and `ELO` are positive integers; ranks are unique. Every row shares one `Rated_At` source date, one later UTC `Collected_At` timestamp, and one HTTPS source URL. The file is UTF-8 without a byte-order mark, uses CRLF only, starts with the header, and ends with a final CRLF.

Core owns the strict snapshot parser, immutable snapshot model, source-result interface boundary, and selection policy. Malformed, missing, duplicate, mixed-date, mixed-collection-time, mixed-source, alias-mismatched, or non-deterministically ordered data cannot become a complete snapshot.

Unattended network use remains disabled unless P0-21 records owner acceptance of reuse terms or an accepted alternative. When it is enabled, a network candidate may replace the freshest complete launch-seed or last-known-good snapshot only when all of these gates pass:

- it is a complete strict snapshot for all 18 manifest aliases;
- its `Rated_At` is no more than seven calendar days before its `Collected_At`;
- its `Rated_At` is strictly newer than the retained complete snapshot.

Invalid, partial, unavailable, older, same-date, or more-than-seven-day-old network candidates retain the freshest complete seed or last-known-good snapshot and surface a stable diagnostic. The seven-day gate applies only to a proposed network refresh. A launch seed or last-known-good snapshot may be older, remains usable, and must expose its age to later publication and traces rather than disappearing.

## Alternatives considered

- **Use only “newer than retained” as freshness:** Rejected because an upstream response can be newer than an old launch seed while still being too stale at collection time.
- **Expire the launch seed or last-known-good snapshot after seven days:** Rejected because ADR-0008 deliberately makes the complete dated snapshot the fail-safe while network reuse is unresolved.
- **Allow equal source dates with a later collection timestamp to replace retained data:** Rejected because a later fetch time is not a newer rating version.
- **Implement the live HTTP or Firestore collector in P0-10:** Rejected because P0-11 owns collection and atomic publication after this contract exists.

## Consequences

- P0-11 can implement parsing, cache selection, diagnostics, and publication without inventing source or freshness rules.
- Launch remains independent of network availability and reuse approval.
- P1-04 may choose refresh cadence and alert policy, but cannot weaken completeness or the seven-day candidate gate without superseding this ADR.

## Affected tasks

- [P0-10](../tasks/p0-10-club-elo-source.md)
- [P0-11](../tasks/p0-11-club-elo-collector.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)
- [P1-04](../tasks/p1-04-club-elo-refresh.md)

## Supersedes

None. This decision makes ADR-0008's provider and last-known-good direction concrete.
