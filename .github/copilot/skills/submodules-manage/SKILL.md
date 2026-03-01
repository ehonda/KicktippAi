---
name: submodules-manage
description: Manage external git submodules under the external/ directory. Use this skill to add, remove, or list git submodules that provide local access to external repositories (documentation, source code) referenced by this project.
---

# Submodules — Manage

Manage git submodules under `external/`. Submodules are placed at `external/<owner>/<repo>`, mirroring their GitHub
path.

## How to Use

Run the bash script from the repository root:

```bash
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh <command> [options]
```

## Commands

### Add a Submodule

```bash
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh add <owner/repo> [options]
```

Options:

- `--depth N` — Clone depth (default: 1, shallow)
- `--full` — Clone with full history (overrides `--depth`)
- `--sparse-paths <paths>` — Comma-separated list of top-level paths to check out via sparse checkout
- `--info-only` — Print repo size and top-level contents without actually adding the submodule (see [Large Repository Guidance](#large-repository-guidance))

Examples:

```bash
# Add a repo with default shallow clone
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh add spectreconsole/spectre.console

# Add with sparse checkout (only specific directories)
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh add thomhurst/TUnit --sparse-paths "docs/docs"

# Preview repo info before adding
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh add spectreconsole/spectre.console --info-only
```

### Remove a Submodule

```bash
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh remove <owner/repo> --confirm
```

The `--confirm` flag is required to prevent accidental removal.

### List Submodules

```bash
bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh list
```

Outputs a JSON array of `{"path": "...", "url": "..."}` objects.

## Large Repository Guidance

**When adding a repository, you MUST follow this process for any repository that is not already known to be small:**

1. **Run with `--info-only` first** to get repository metrics:
   ```bash
   bash .github/copilot/skills/submodules-manage/scripts/manage-submodules.sh add <owner/repo> --info-only
   ```
   This outputs:
   - **Repository size** (on-disk size, file count)
   - **Top-level contents** (list of top-level files and directories with sizes)

2. **Present findings to the user** using an interview or feedback tool. Provide:
   - The repo size and file count from the `--info-only` output
   - The list of top-level contents
   - A **recommendation** on which top-level paths are relevant based on the user's task or the instruction files that reference this repository

3. **Confirm checkout scope** with the user before proceeding:
   - If the user agrees the full repo is needed, run `add` without `--sparse-paths`
   - If the user selects specific paths, run `add` with `--sparse-paths "<comma-separated-paths>"`

This process prevents unnecessarily large checkouts that waste disk space and clone time.
