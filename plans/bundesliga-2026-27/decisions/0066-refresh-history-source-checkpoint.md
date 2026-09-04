# ADR-0066: Refresh the rolling Bundesliga history source checkpoint

- Status: Accepted
- Date: 2026-09-05

## Context

The original DFB 2026 OpenLigaDB capture predated completed fixtures `81836` and `81852`. The original preseason 1+2 source anchors are no longer a usable rolling occurrence checkpoint because closed Kicktipp rows no longer expose betting inputs.

## Decision

Capture `dfb/2026` at SHA-256 `728d31be6f928fa83cff7bf56925d0456642686a35bf02fb43e428ebd3ce81eb` and add only the four authenticated BVB/FCB away/recent occurrences for the two completed fixtures. Use ordered matchdays 3, 4, then 2 as the read-only rolling 54-document occurrence checkpoint. The 434-row accumulated map is retained even when the rolling checkpoint has fewer rows. The collection gate requires the exact selected-name set, structurally valid incomplete exclusions, and exact resolution of every current completed row; it does not require a fixed total or fixed incomplete count.

The broken historical full-season collection path is deferred; this decision does not change its selector or parser.

## Consequences

- Production context can retain exact dates for the newly completed DFB occurrences without claiming that rolling windows equal retained evidence.
- P1-14 owns later full-season capture and proxy continuity work.

## Affected tasks

- [P1-14](../tasks/p1-14-history-source-continuity.md)

## Supersedes

This ADR supersedes [ADR-0041](0041-freeze-completed-dfb-first-round-history-transition.md) only for its obsolete `dfb/2026` byte revision, completed-ID set, 16-identity/32-occurrence scope, and `81836`/`81852` incomplete-status claims. It retains ADR-0041's exact-source provenance, ODbL license, no-runtime-fetch, fail-closed, and atomic-publication boundaries.

It supersedes [ADR-0044](0044-select-canonical-preseason-history-sources.md) only for its 1+2 rolling source-checkpoint assumption. Its atomic publication and other selection contracts remain accepted.
