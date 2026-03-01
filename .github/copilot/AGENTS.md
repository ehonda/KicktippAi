# Copilot Agent Configuration

## Skill Script Conventions

This project maintains two versions of its `submodules-*` skills:

| Location | Language | Auto-discovered | Purpose |
|----------|----------|-----------------|---------|
| `.github/copilot/skills/` | **PowerShell** (`.ps1`) | Yes | Primary — optimized for local Windows development |
| `.github/copilot/skills-bash/` | **Bash** (`.sh`) | No | Mirror — portable scripts for CI / Linux / codespaces |

The `langfuse-api` skill exists only in PowerShell (no bash mirror).

### Creating or updating skills

- When **updating** a skill under `skills/`, check if a corresponding bash version exists in `skills-bash/`.
- If it does, **keep both versions in sync** — apply the same functional changes to both.
- When **creating** a new skill, decide whether a bash mirror is needed based on portability requirements.

## References

- [Agent Skills Standard](https://agentskills.io/llms.txt)
