# ADR-0071: Bind the strict CL POST to a stable advertised action

- Status: Accepted
- Date: 2026-09-05

## Context

Authenticated production observations exposed two distinct exact form actions
for the same frozen Schadensfresse Champions-League bonus page:
`/schadensfresse/tippabgabe` and
`/schadensfresse/tippabgabeForm`. Both singleton implementations therefore
failed closed in production at different times. The evidence does not explain
why Kicktipp varies the action, so neither member is a preferred fallback.

## Decision

The strict route owns one exact GET page and an immutable, unordered two-member
approved POST-action set. Each action retains exact scheme, authority,
effective port, empty user-info, case-sensitive path, empty query, and empty
fragment. Parsing preserves the approved member advertised by the form. The
initial and every immediate pre-POST form read must advertise the same member;
switching is concurrency drift and stops before transport.

After those checks, the client maps that selected canonical member by stable
member index to the strict transport's injected origin and passes the resulting
URI explicitly. The transport validates membership before sending exactly one
POST. The direct response, an allowed `302`/`303` followed page, and the
independent final page must all advertise the selected member. The client never
switches to or retries the other approved action. Post-dispatch drift remains
an unknown outcome requiring read-only recovery.

## Alternatives considered

- **Prefer either singleton:** Rejected because authenticated production
  evidence has contradicted both singleton choices.
- **Trust any advertised or same-origin action:** Rejected because it turns a
  finite reviewed mutation boundary into server-controlled routing.
- **Retry the other approved member after failure:** Rejected because that can
  duplicate a mutation whose first outcome is unknown.

## Consequences

- The approved set is finite code-owned identity, not generic configuration or
  a runtime fallback.
- Loopback tests retain member identity while mapping to their injected origin.
- The shared cookies, dedicated handler, single POST, redirect limits,
  no-authentication-replay behavior, and generic-client separation from
  ADR-0070 remain unchanged.
- Safe rollback stops or reverts the manual route; reverting to either
  singleton is not a production restoration strategy.

## Affected tasks

- [P1-15](../tasks/p1-15-schadensfresse-champions-league-bonus.md)

## Supersedes

This supersedes ADR-0070 only where “the frozen action” denotes one global
singleton. All other ADR-0070 transport and recovery decisions remain accepted.
