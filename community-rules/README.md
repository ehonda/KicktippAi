# Community Scoring Rules

This directory contains community-specific scoring rules used by the KicktippAi system.

## How it Works

The `KicktippContextProvider` loads community-specific scoring rules based on the `community-context` parameter:

1. First, it tries to load a file named `{community-context}.md`
2. If that doesn't exist, it falls back to `default.md`
3. If that doesn't exist, it uses built-in fallback content

## Adding New Community Rules

To add rules for a new community:

1. Create a new markdown file named `{community-name}.md`
2. Follow the format shown in the existing files
3. Include at least:
   - A scoring system table
   - Explanation of tendency, goal difference, and exact result
   - Examples

## Current Communities

- `default.md` - Standard Kicktipp scoring rules
- `ehonda-test-buli.md` - Test community rules
- `ehonda-ai-arena.md` - AI arena community rules  
- `pes-squad.md` - PES squad community with modified scoring

## Example Usage

```bash
# Uses ehonda-test-buli.md rules
dotnet run --project src/Orchestrator -- matchday o4-mini --community ehonda-test-buli

# Uses custom-community.md rules if it exists, otherwise falls back to default.md
dotnet run --project src/Orchestrator -- matchday o4-mini --community some-community --community-context custom-community
```
