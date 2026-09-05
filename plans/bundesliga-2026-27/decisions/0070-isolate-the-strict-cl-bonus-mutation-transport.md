# ADR-0070: Isolate the strict CL bonus mutation transport

- Status: Accepted
- Date: 2026-09-05

## Context

ADR-0069 requires one complete POST for the three frozen Schadensfresse Champions-League bonus answers. The ordinary authenticated Kicktipp handler may reauthenticate and replay a request after an authorization failure, while its primary handler automatically follows redirects. Those useful generic behaviors make the number and method of strict mutation attempts ambiguous.

## Decision

Give the strict CL route a second, dedicated `HttpClient` whose primary handler shares the ordinary authenticated client's `CookieContainer` but disables automatic redirects and has no authentication or resilience handler. The ordinary client performs the immediately preceding authenticated form GET; the strict client then makes exactly one POST attempt to the frozen action. It accepts a direct `200`, or follows one exact `302`/`303` redirect to the frozen bonus page with a bodyless GET. Every other redirect, authorization response, login surface, URI drift, transport failure, and cancellation fails without another POST. Response validation and the independent final readback also use the no-retry strict transport. Generic Kicktipp operations retain their existing authentication replay and redirect behavior.

## Alternatives considered

- **Keep the shared authenticated client and suppress retries in the caller:** Rejected because handler-level reauthentication and automatic `307`/`308` redirects can replay the POST below the caller.
- **Disable redirects and authentication replay globally:** Rejected because it would regress established generic Kicktipp operations.
- **Reject every redirect:** Rejected because Kicktipp may use conventional `302`/`303` post/redirect/get after a successful form submission; one exact bodyless GET preserves that response path without replaying the mutation.

## Consequences

- Production construction owns two handler chains and disposes them together; their shared cookies preserve authentication without sharing mutation policy.
- A transport exception after dispatch has an unknown mutation outcome. Recovery begins with strict read-only state inspection and never automatically resubmits.
- The manual-only CL leaf has no fallback to the ordinary client. Reverting this decision stops strict placement rather than restoring replay-prone posting.
- Handler-level tests use the same factory builder with a loopback origin and assert the real server request journal, including the shared cookie, exact payload, and request counts.

## Affected tasks

- [P1-15](../tasks/p1-15-schadensfresse-champions-league-bonus.md)

## Supersedes

None. This refines ADR-0069's transport mechanism without changing its accepted identity, prompt, model, storage, or execution boundaries.
