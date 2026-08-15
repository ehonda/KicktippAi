# P1-03 — Extract generic competition onboarding tooling

- Status: Not started
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)

## Outcome

A generic `competition-onboarding` skill drives shared onboarding stages while Bundesliga and WM26 remain thin, explicit profiles.

## Work items

- [ ] Compare the completed Bundesliga evidence with `.agents/skills/wm26-onboarding/` and identify only proven shared stages.
- [ ] Use the global `skill-creator` instructions to design and validate the generic skill.
- [ ] Define a profile contract for competition identity, teams, collectors, required context, prompts/models, costs, communities, validation, and activation.
- [ ] Move shared sequencing and evidence requirements into the generic skill.
- [ ] Keep WM26-specific squad timing, FIFA rankings, date maps, knockout behavior, and hosted prompt checks in its profile/entry point.
- [ ] Add a Bundesliga profile that links to this task plan, ADRs, data paths, and validation commands.
- [ ] Validate both entry points and document how future domestic seasons create a new profile.

## Validation

- Run the global skill validator with the repository-prescribed `uv --with PyYAML` command.
- Dry-walk both profiles and confirm neither silently calls the other's collectors.

## Complete when

- Shared steps have one source of truth and competition-specific requirements remain isolated.
- Both skill entry points pass validation and include ADR checkpoints.
