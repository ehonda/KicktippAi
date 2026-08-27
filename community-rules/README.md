# Community Scoring Rules

This directory contains community-specific scoring rules used by the KicktippAi system.

## How it Works

The `KicktippContextProvider` loads community-specific scoring rules based on the `community-context` parameter. Normal matches use `{community-context}.md`; knockout-stage matches use `{community-context}-knockout.md` instead. A missing selected rules file is an error.

## Adding New Community Rules

To add rules for a new community:

1. Create a new markdown file named `{community-name}.md`
2. For a competition with knockout-stage result accumulation, also create `{community-name}-knockout.md`
3. Follow the format shown in the existing files
4. Include at least:
   - A scoring system table
   - Explanation of tendency, goal difference, and exact result
   - Examples

## Current Communities

- `ehonda-dev-buli-2627.md` - Bundesliga 2026/27 development-community rules verified from Kicktipp
- `ehonda-test-buli.md` - Test community rules
- `ehonda-ai-arena.md` - AI arena community rules
- `pes-squad.md` - PES squad production-community rules
- `schadensfresse.md` - Schadensfresse Bundesliga 2026/27 prompt rules, intentionally identical to `pes-squad.md`; the mixed DFB-Pokal/Champions-League contract is recorded in ADR-0054 and P1-08
- `relaxdays-tippt.md` - Relaxdays production-copy rules, intentionally identical to `pes-squad.md`
- `rabetrabauken2026.md` - WM26 reference community rules

## Example Usage

```bash
# Uses ehonda-test-buli.md rules
dotnet run --project src/Orchestrator -- matchday o4-mini --community ehonda-test-buli

# Uses custom-community.md rules; a missing selected rules file fails closed
dotnet run --project src/Orchestrator -- matchday o4-mini --community some-community --community-context custom-community
```
