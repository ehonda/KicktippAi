# External Submodules — Open Tasks

Open tasks remaining from the initial implementation session. See
[external-submodules.md](external-submodules.md) for the full system overview.

## 1. Test and fix all three skill scripts

The bash scripts have not been executed yet. They need to be run, debugged, and corrected:

- `.github/copilot/skills/submodules-manage/scripts/manage-submodules.sh`
  - Test `--info-only`, `add`, `remove --confirm`, `list`
  - Test `--sparse-paths` and `--full` flags
  - Verify JSON output is valid
- `.github/copilot/skills/submodules-display-tree/scripts/display-tree.sh`
  - Test all three formats: `--format flat`, `--format indented`, `--format tree`
  - Test `--depth` limiting
  - Verify output matches the examples in the SKILL.md
- `.github/copilot/skills/submodules-update-tree-document/scripts/update-tree-document.sh`
  - Test that it invokes display-tree correctly and writes to `agent-files/submodule-tree.txt`

## 2. Add the 7 initial submodules

None of the initial submodules have been added yet. Use the `submodules-manage` skill (following its 3-step
info-only → confirm → add workflow) for each:

| Repository | Suggested Sparse Paths | Source Instruction |
|------------|----------------------|---------------------|
| `dotnet/dotnet-api-docs` | TBD (large repo, needs `--info-only` review) | `dotnet-api-usage.instructions.md` |
| `openai/openai-dotnet` | TBD | `openai-dotnet-usage.instructions.md` |
| `spectreconsole/spectre.console` | TBD | `spectre-console.instructions.md` |
| `spectreconsole/website` | `Spectre.Docs/Content` | `spectre-console.instructions.md` |
| `thomhurst/TUnit` | `docs/docs` | `tunit.instructions.md` |
| `wiremock/wiremock.org` | `src/content/docs/dotnet` | `wiremock.instructions.md` |
| `wiremock/WireMock.Net` | TBD | `wiremock.instructions.md` |

## 3. Generate the tree document

After all submodules are added, run the `submodules-update-tree-document` skill to populate
`agent-files/submodule-tree.txt` with the actual flat tree content. Currently it only contains a placeholder.

## 4. Verify `.gitattributes` LF enforcement

Confirm that `*.sh text eol=lf` in `.gitattributes` is working correctly for all three scripts. Run:

```bash
git check-attr eol -- .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh
```

## Context

### Files created in this session

- `.gitattributes`
- `.github/copilot/skills/submodules-manage/SKILL.md`
- `.github/copilot/skills/submodules-manage/scripts/manage-submodules.sh`
- `.github/copilot/skills/submodules-display-tree/SKILL.md`
- `.github/copilot/skills/submodules-display-tree/scripts/display-tree.sh`
- `.github/copilot/skills/submodules-update-tree-document/SKILL.md`
- `.github/copilot/skills/submodules-update-tree-document/scripts/update-tree-document.sh`
- `agent-files/submodule-tree.txt` (placeholder)
- `plans/agents-md/external-submodules.md` (system overview / AGENTS.md handover)

### Key design decisions already made

- Submodule root: `external/<owner>/<repo>`
- Skill naming: `submodules-manage`, `submodules-display-tree`, `submodules-update-tree-document`
- Scripts use bash (not PowerShell) for cross-platform/agent interoperability
- Add workflow: mandatory 3-step process (info-only → user confirmation → add)
- Tree document: `agent-files/submodule-tree.txt`, raw flat tree output only (no markdown wrapping)
- Shallow clones by default (`--depth 1`)
- Safe removal requires `--confirm`
