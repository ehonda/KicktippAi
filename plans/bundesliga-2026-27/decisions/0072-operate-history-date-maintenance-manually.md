# ADR-0072: Operate history-date maintenance manually

- Status: Accepted
- Date: 2026-09-05

## Context

ADR-0066 freezes reviewed source evidence while ADR-0067 permits a clearly labelled collection-date proxy when an otherwise valid completed selected-history occurrence has no exact external date. Operators need prompt, typed visibility of proxy continuity without adding a provider call or a second production lane.

## Decision

After the existing atomic history publication succeeds (or after a dry-run validates without saving), the profile-owned Markdown summary reports typed coverage counts and any proxy tuple groups. A nonzero proxy count is a successful, warning-level maintenance signal, never an exact played date. A scheduled metadata-only workflow reminds the one marker-bound GitHub issue weekly; it cannot invoke collectors, providers, predictions, context writes, or dispatch.

The checked-in manual procedure remains the sole route to refresh source evidence. It retains ADR-0066's raw-byte provenance, ODbL notice, no-runtime-fetch, exact identity, fail-closed, accumulated-map, and atomic-publication boundaries. This decision does not supersede ADR-0067's proxy resolution contract and narrowly qualifies ADR-0053 only to allow the non-production issue reminder.

## Alternatives considered

- **Silently accept proxies:** Reject because continuity would be indistinguishable from exact source evidence.
- **Fetch and repair sources during collection:** Reject because runtime provider access makes atomic context publication dependent on mutable external data.
- **Open a new issue every week:** Reject because duplicate work items obscure one accountable maintenance history.

## Consequences

- A summary-output failure happens after the one correct publication and is reported without replaying the save.
- Maintainers review a single issue and must record the weekly result with a distinct ISO-week marker.
- The schedule is limited to GitHub issue metadata and does not change production workflow topology.

## Affected tasks

- [P1-14](../tasks/p1-14-history-source-continuity.md)
- [P1-16](../tasks/p1-16-automatic-history-date-updates.md)

## Supersedes

None. This narrowly succeeds ADR-0066's one-checkpoint wording and narrowly qualifies ADR-0053's production-lane exclusivity; it retains their other boundaries.
