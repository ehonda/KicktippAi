# ADR-0045: Verify versioned prompt promotion before validation

- Status: Accepted
- Date: 2026-08-25

## Context

ADR-0004 makes Langfuse-hosted prompts primary and retains checked-in mirrors
as an outage or first-fetch fallback. ADR-0033 separately pins the Bundesliga
plumbing identity to the immutable match prompt version 2 and bonus prompt
version 1. Both immutable versions must also carry the deliberately assigned
`production` label.

The first P0-20 matchday validation attempt supplied both selectors to the
Langfuse v2 prompt endpoint. Langfuse rejected the request with HTTP 400 because
the endpoint accepts a version or a label, not both. The runtime then followed
the ordinary availability contract and used the local mirror. Because the dev
shortcut had not distinguished required hosted validation from ordinary
fallback operation, all nine fallback-based predictions were stored and posted
before the rung stopped. This is failed-rung plumbing evidence, not hosted
prompt validation.

An immutable version and its promotion label express two different assertions:
the version selects bytes; the label proves that the owner deliberately
promoted those exact bytes. Retrieving by a floating label alone cannot prove
the configured numbered identity, while sending both selectors is invalid.

## Decision

When a Langfuse prompt lookup has an immutable version, send only `version` to
the public API. Send `label` only for a label-resolved lookup with no immutable
version. After every successful fetch, validate the returned prompt before it
can supply a template:

- returned name exactly equals the requested name;
- returned version exactly equals the requested version when one is configured;
- returned labels contain the exact requested label when one is configured.

Name, version, or label drift is an identity/provenance failure and never
activates the local fallback. A missing prompt or fetch failure may still use
the checked-in mirror on ordinary runtime routes under ADR-0004.

The Bundesliga `matchday-dev` and `bonus-dev` shortcuts are stricter. They mark
their accepted Luna/none/cap-10000 prompt binding as hosted-required. The
provider must retrieve and validate match v2/`production` or bonus
v1/`production` before the prediction-service factory is called. Missing hosted
content, fetch failure, or binding drift prevents model construction and all
prediction/storage/posting work. A local mirror remains available to ordinary
runtime routes, but its use cannot pass the P0-20 hosted validation rung.

Prompt paths and telemetry continue to record the requested label and the exact
resolved version. This decision does not create, move, or promote a Langfuse
label and does not authorize a model, prediction, workflow, or schedule call.

## Alternatives considered

- **Fetch by `production` only:** Rejected because a later label move could
  satisfy the request without matching the configured immutable version.
- **Fetch by version and trust the configured label:** Rejected because it
  would not prove that the returned version is currently promoted.
- **Treat the local mirror as a successful validation result:** Rejected because
  P0-20 must prove the hosted route and exact promotion binding.
- **Remove the ordinary local fallback:** Rejected because ADR-0004 deliberately
  retains that availability behavior outside the hosted-required validation
  rung.

## Consequences

- The public API never receives the invalid simultaneous selector shape.
- Exact version and promotion are both proven without using a floating version.
- A hosted validation failure occurs before prediction-service/model
  construction and cannot write another fallback-based result.
- Existing ordinary hosted routes retain their visible availability fallback;
  identity drift remains fail closed everywhere.

## Affected tasks

- [P0-05](../tasks/p0-05-prompt-route.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

None. This decision refines the simultaneous ADR-0004 availability and ADR-0033
immutable validation contracts.
