# External Submodules — Overview

## Purpose

External GitHub repositories (documentation, source code) referenced by this project's instruction files are now
available as **git submodules** under `external/`. This gives agents local, offline-capable access to these resources
instead of relying on GitHub MCP fetches at runtime.

## Structure

Submodules are placed at `external/<owner>/<repo>`, mirroring their GitHub path. For example,
`https://github.com/spectreconsole/spectre.console` lives at `external/spectreconsole/spectre.console`.

The current submodule tree is maintained in `agent-files/submodule-tree.txt` (flat brace-expansion format).

## Skills

Three bash-based Agent Skills manage the submodule lifecycle:

| Skill | Description | Script |
|-------|-------------|--------|
| `submodules-manage` | Add, remove, list submodules | `.github/copilot/skills/submodules-manage/scripts/manage-submodules.sh` |
| `submodules-display-tree` | Render `external/` tree in flat, indented, or tree format | `.github/copilot/skills/submodules-display-tree/scripts/display-tree.sh` |
| `submodules-update-tree-document` | Update `agent-files/submodule-tree.txt` with current flat tree | `.github/copilot/skills/submodules-update-tree-document/scripts/update-tree-document.sh` |

Skills follow the [Agent Skill Standard](https://agentskills.io/specification.md) and use bash scripts (not
PowerShell) for OS and agent harness interoperability.

### Key Behaviors

- **Shallow clones by default** (`--depth 1`) — override with `--full` or `--depth N`
- **Sparse checkout** via `--sparse-paths` for repos where only specific subdirectories are needed
- **Large repo interview** — the manage skill instructs agents to use `--info-only` and confirm checkout scope with
  the user before adding large repositories
- **Safe removal** — `remove` requires `--confirm`

## Initial Submodules

These repositories were identified from the instruction files listed below:

| Repository | Submodule Path | Sparse Paths | Source |
|------------|---------------|--------------|--------|
| `dotnet/dotnet-api-docs` | `external/dotnet/dotnet-api-docs` | — | `dotnet-api-usage.instructions.md` |
| `openai/openai-dotnet` | `external/openai/openai-dotnet` | — | `openai-dotnet-usage.instructions.md` |
| `spectreconsole/spectre.console` | `external/spectreconsole/spectre.console` | — | `spectre-console.instructions.md` |
| `spectreconsole/website` | `external/spectreconsole/website` | `Spectre.Docs/Content` | `spectre-console.instructions.md` |
| `thomhurst/TUnit` | `external/thomhurst/TUnit` | `docs/docs` | `tunit.instructions.md` |
| `wiremock/wiremock.org` | `external/wiremock/wiremock.org` | `src/content/docs/dotnet` | `wiremock.instructions.md` |
| `wiremock/WireMock.Net` | `external/wiremock/WireMock.Net` | — | `wiremock.instructions.md` |

## Related Instruction Files

These instruction files currently direct agents to use GitHub MCP for accessing external repositories. In the
`AGENTS.md` migration, they can be updated to reference local submodule paths instead:

| Instruction File | External Repos Referenced |
|-----------------|--------------------------|
| [`.github/instructions/dotnet-api-usage.instructions.md`](.github/instructions/dotnet-api-usage.instructions.md) | `dotnet/dotnet-api-docs` |
| [`.github/instructions/openai-dotnet-usage.instructions.md`](.github/instructions/openai-dotnet-usage.instructions.md) | `openai/openai-dotnet` |
| [`.github/instructions/spectre-console.instructions.md`](.github/instructions/spectre-console.instructions.md) | `spectreconsole/spectre.console`, `spectreconsole/website` |
| [`.github/instructions/tunit.instructions.md`](.github/instructions/tunit.instructions.md) | `thomhurst/TUnit` |
| [`.github/instructions/wiremock.instructions.md`](.github/instructions/wiremock.instructions.md) | `wiremock/wiremock.org`, `wiremock/WireMock.Net` |
| [`.github/instructions/github-repos.instructions.md`](.github/instructions/github-repos.instructions.md) | General GitHub repo access guidelines |

## Migration Notes for AGENTS.md

- The instruction files above currently tell agents to use **GitHub MCP** (`#github/github-mcp-server/*`,
  `#githubRepo`) to fetch external repo contents at runtime
- With submodules in place, `AGENTS.md` can reference the **local paths** under `external/` instead — making access
  faster, offline-capable, and independent of GitHub API rate limits
- The `agent-files/submodule-tree.txt` file provides a compact directory listing that can be included in `AGENTS.md`
  to give agents an overview of available local resources
- Instruction files that only contain "use GitHub MCP to get docs from X" can likely be consolidated into sections
  of `AGENTS.md` that point to the local submodule paths
