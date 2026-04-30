# Langfuse Agent Tooling

Install Langfuse agent integrations globally or at user scope. They are useful across repositories and should not be vendored into this repo.

## Recommended Global Setup

1. Install the official Langfuse agent skill and keep it current:

```powershell
npx skills add langfuse/skills --skill "langfuse"
```

For agent-specific installs, add `--agent "<agent-id>"`. Cursor users can alternatively install the Langfuse Cursor plugin, which includes the skill.

2. Use the globally installed Langfuse CLI for generic API access. The npm package is `langfuse-cli`, but the installed command is `langfuse`. Prefer this installed command for agent workflows instead of `npx`, especially when loading the external secrets file.

```powershell
npm i -g langfuse-cli
langfuse api __schema
```

For this repo, prefer the external secrets file instead of copying keys into the checkout:

```powershell
langfuse --env ..\KicktippAi.Secrets\src\Orchestrator\.env api traces list --limit 10 --json
```

If `langfuse` is missing or stale, install or update it with `npm i -g langfuse-cli` after the user approves the global install. Avoid `npx langfuse-cli ...` for routine work because it fetches executable package code at runtime and can create unnecessary credential-exposure risk when paired with `--env`.

For agents that cannot load installed skills directly, `langfuse get-skill` can print the current official Langfuse skill for temporary context injection.

3. Add the public Langfuse Docs MCP server globally to MCP-capable coding agents.

For Codex:

```powershell
codex mcp add langfuse-docs --url https://langfuse.com/api/mcp
```

4. Add the authenticated Langfuse MCP server when working with Langfuse prompt management. Use the same host as `LANGFUSE_HOST` and a Basic auth token made from `LANGFUSE_PUBLIC_KEY:LANGFUSE_SECRET_KEY`.

This repository already configures the authenticated server in `.vscode/mcp.json` through a password prompt input:

```json
{
  "servers": {
    "langfuse": {
      "url": "https://cloud.langfuse.com/api/public/mcp",
      "type": "http",
      "headers": {
        "Authorization": "Basic ${input:langfuse-basic-auth}"
      }
    }
  }
}
```

The authenticated MCP server currently focuses on prompt management. It exposes read tools (`getPrompt`, `listPrompts`) and write tools (`createTextPrompt`, `createChatPrompt`, `updatePromptLabels`), so configure client approvals or allowlists when you want read-only behavior.

## Usage In This Repo

- Use the official `$langfuse` skill for current Langfuse docs, SDK/API guidance, prompt migration, and generic Langfuse API work.
- Use the installed `langfuse` command for direct traces, observations, datasets, scores, sessions, and prompt API calls. Start with `langfuse api __schema` and `langfuse api <resource> --help` because resource names can change with the bundled API spec.
- Use the public Docs MCP server for current documentation lookup when the agent supports MCP.
- Use the authenticated MCP server for hosted prompt discovery, creation, and label promotion. Use the CLI for Langfuse resources that the MCP server does not expose yet.
- Keep secrets outside the repository. Do not commit MCP config files that contain authorization headers.
- Continue reading [docs/langfuse.md](../docs/langfuse.md) for repository-specific Langfuse tracing and filtering behavior that was verified against this project.
- Use `.agents/skills/langfuse-experiments/` for KicktippAi-specific experiment orchestration, statistical report generation, Pages verification, and commit/push workflow.
