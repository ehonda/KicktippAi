# P0-18 — Add Bundesliga support to reusable workflows

- Status: Not started
- Priority: P0
- Depends on: [P0-14](p0-14-profile-driven-collection.md), [P0-17](p0-17-community-scope.md)
- Decisions: [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

Reusable context, matchday, and bonus workflows can run the 2026/27 profile with explicit competition, prompt, and model inputs.

## Work items

- [ ] Replace WM26-specific collector booleans in `base-context-collection.yml` with the profile/command contract from P0-14, while keeping WM26 callable through its own profile.
- [ ] Require an explicit competition in all three reusable workflows.
- [ ] Surface the pinned model, reasoning effort, output cap, and prompt identity needed by P0-06.
- [ ] Make context summary output list the resolved profile and actual collector results.
- [ ] Provide a sequencing mechanism or documented dependency so prediction jobs cannot consume context before collection succeeds.
- [ ] Add workflow validation/tests or a deterministic local lint path for required inputs and generated command lines.

## Validation

- Validate all workflow YAML and inspect rendered command lines for Bundesliga and WM26 callers.
- Confirm a Bundesliga context invocation contains the Bundesliga history played-date step and no FIFA, lineup, WM26 date-map, or transfer step.

## Complete when

- A community entrypoint only needs to supply its community matrix values and trigger policy.
- Missing competition or model identity fails before a prediction command starts.
