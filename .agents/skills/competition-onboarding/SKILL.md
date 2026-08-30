---
name: competition-onboarding
description: Onboard a KicktippAi football competition or create its explicit onboarding profile. Use for shared onboarding sequencing, evidence gates, profile design, community/model/cost readiness, validation, and activation handoff; use a competition-specific profile for collectors and data contracts.
---

# Competition Onboarding

Use an explicit competition profile. Read its `references/competition-profile.md` before taking an onboarding action; that profile owns the competition identity, collectors, documents, and accepted decisions. Known profiles are:

- [WM26](../wm26-onboarding/SKILL.md)
- [Bundesliga 2026/27](../bundesliga-2026-27-onboarding/SKILL.md)

Read [the profile contract](references/profile-contract.md) when creating, changing, reviewing, or dry-walking a profile. Do not infer a profile from a community name, season, or a previous competition.

## Shared workflow

1. Resolve the exact profile, competition, posting target, and community context. Read the profile's linked ADRs before changing a durable contract or crossing an owner gate.
2. Inspect the profile's team source, ordered collectors, required context, prompt/model record, cost evidence, community matrix, validation commands, and activation state. Stop before an undefined or conflicting item rather than borrowing it from another profile.
3. Collect context in the declared order with the exact competition and community context. Preserve profile-defined atomic or embedded phases, pass dry-run to every direct collector, and stop later phases after a failure. Do not construct or invoke collectors absent from the profile.
4. Verify required context documents and profile acceptance counts before prediction validation. Treat a dry run as collector and write-safety evidence, not prediction, credential, or posting evidence.
5. Record each effective prediction configuration: model, reasoning effort, output cap, prompt source/name/version-or-label, fallback/service policy, and full-competition cost estimate. Do not promote a development configuration or use a floating prompt label when the profile's accepted decision requires a promoted version.
6. Validate a community in context-before-prediction order. Select credentials from the posting target, inspect final verification and Langfuse evidence, and preserve copy/reuse compatibility checks where the profile declares them.
7. Activate only through the profile's accepted owner-controlled gate. Keep schedules inactive when that gate is not complete; a successful manual run is not schedule authority.
8. Update the profile's authoritative ledger or task with command, exact input/identity, outcome, links, and remaining manual follow-ups. Commit only the intended tracked changes after the profile's validation passes.

## Create a domestic-season profile

Create a new discoverable `*-onboarding` skill containing a thin `SKILL.md`, UI metadata, and one `references/competition-profile.md`. Fill every required field in the contract from that season's plan, ADRs, code profile, and tracked data. Link the generic skill for shared sequencing; never copy it. Keep automatic discovery enabled unless the owner explicitly asks for an explicit-only profile.

Do not create a new profile merely to revive a historical competition. Add a plan and accepted decisions first when the requested competition changes identity, collectors, prompt route, community scope, cost policy, or activation behavior.
