# Competition Onboarding Profile Contract

An onboarding profile is an explicit, checked-in boundary for one competition. Its `SKILL.md` stays thin: it names this generic skill and routes to this profile file. The profile file supplies the following fields and links to their source of truth. A concise profile may satisfy a field through an explicit required-read link to one authoritative operational record; the link must state what it supplies and must be read before the dependent action.

| Contract area | Required profile content |
| --- | --- |
| Competition identity | Canonical competition ID, display name, season bounds, supported development community or communities, and linked accepted decisions. |
| Teams | Expected team and match counts, matchday cardinality where fixed, team manifest/source, and any source freshness or provenance gate. |
| Collectors | A unique ordered list of direct and embedded phases, exact commands or code-profile route, dependencies, and an explicit list of collectors not selected. Embedded phases name the direct phase that owns their atomic publication. |
| Required context | Match-document templates, aggregate/KPI documents, schemas or data paths, feature flags (history, knockout, transfers), and the acceptance check before predictions. |
| Prompts and models | Hosted/local route, match and bonus names, immutable version or accepted label rule, fallback route, development identity, production/challenger identity or owner gate, cap, and service policy. |
| Costs | Exact whole-competition count assumptions, estimate document/command, and a policy for a missing estimate or base row. |
| Communities | Posting targets, community contexts, credential-selection rule, reuse/copy compatibility, telemetry environment, and authoritative community ledger. |
| Validation | Safe dry-run inputs and commands; context, prediction, final-verification, trace, and relevant test evidence; plus the condition that demonstrates collector isolation. |
| Activation | Manual membership/secrets gates, workflow/schedule state, owner authority, rollback or follow-up boundary, and authoritative activation evidence. |

## Isolation rules

- A profile must name only its own collectors and data paths. It must explicitly say that it does not invoke the other profile's collectors; absence is not a default.
- Generic sequencing does not execute a collector. Resolve the exact profile before invoking `collect-context profile`, an individual collector, prediction command, or workflow.
- A future profile may reuse a generic stage, but it must declare its own identity, source evidence, prompt route, and activation decision. It cannot inherit WM26 or Bundesliga values by convention.
- Treat a profile change that alters any contract area as a durable decision: link an accepted ADR or stop for the required decision. Preserve existing accepted activation and production choices.

## Dry-walk a profile

Use realistic, non-secret raw inputs in ignored scratch state: competition ID, development/posting target, community context, selected collectors, required document names, prompt identities, and activation state. Confirm all of the following before a live operation:

1. The identity resolves to the expected profile and each collector is in its declared order.
2. The selected list contains none of the other profile's collectors, data paths, prompts, or activation policy.
3. Every required context item, model/cost/community record, validation command, and ADR checkpoint is present.
4. The dry-run mode reaches every direct collector without a write and reports embedded phases as included, not separately executed.
5. The profile's test or command evidence proves the same collector-isolation boundary.

Record the raw input, expected collector list, observed result, and any intentionally unexecuted external gate in the owning task or onboarding ledger.
