---
name: submodules-manage
description: Manage external git submodules under the external/ directory. Use this skill to add, remove, list, or adjust sparse checkout for git submodules that provide local access to external repositories (documentation, source code) referenced by this project.
---

# Submodules — Manage

Manage git submodules under `external/`. Submodules are placed at `external/<owner>/<repo>`, mirroring their GitHub
path.

## How to Use

Run the bash script from the repository root:

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh <command> [options]
```

## Commands

### Add a Submodule

Adding a submodule is a **multi-step workflow**. Never run `add` without `--info-only` first — always preview the
repository and confirm scope with the user before committing to a checkout.

#### Step 1 — Preview the repository

Run `add` with `--info-only` to fetch repository metrics without actually adding the submodule:

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add <owner/repo> --info-only
```

This outputs JSON with:

- **Repository size** (on-disk size, file count)
- **Top-level contents** (list of top-level files and directories with sizes)

#### Step 2 — Present findings and confirm scope with the user

Use an interview or feedback tool to present the `--info-only` output and confirm checkout scope. Provide:

- The repo size and file count
- The list of top-level contents
- A **recommendation** on which top-level paths are relevant, based on the user's task or the instruction files that
  reference this repository

Ask the user whether to:

- Check out the **full repository** (if it's small or everything is needed)
- Check out **specific paths only** via sparse checkout (recommended for large repos)

#### Step 3 — Add the submodule

Based on the user's confirmation, run `add` with the appropriate options:

```bash
# Full checkout (user confirmed full repo is needed)
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add <owner/repo>

# Sparse checkout (user selected specific paths)
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add <owner/repo> --sparse-paths "<comma-separated-paths>"
```

#### Add options reference

- `--info-only` — Preview repo size and top-level contents (Step 1, always run first)
- `--depth N` — Clone depth (default: 1, shallow)
- `--full` — Clone with full history (overrides `--depth`)
- `--sparse-paths <paths>` — Comma-separated list of top-level paths for sparse checkout

#### Example: full add workflow

```bash
# Step 1: Preview
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add thomhurst/TUnit --info-only
# → JSON output shows repo is 150MB, top-level dirs include docs/, src/, tests/, ...

# Step 2: Present to user, recommend --sparse-paths "docs/docs" based on tunit.instructions.md

# Step 3: Add with confirmed scope
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add thomhurst/TUnit --sparse-paths "docs/docs"
```

### Remove a Submodule

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh remove <owner/repo> --confirm
```

The `--confirm` flag is required to prevent accidental removal.

### List Submodules

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh list
```

Outputs a JSON array of `{"path": "...", "url": "..."}` objects.

### Sparse Checkout Management

Incrementally add or remove sparse checkout paths for an existing submodule. This is useful for progressively building
the checked-out structure — e.g. adding namespaces one at a time to `dotnet/dotnet-api-docs`.

If sparse checkout is not yet active on the submodule, `sparse add` will automatically initialize it.

#### Sparse add

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse add <owner/repo> --paths "path1,path2"
```

#### Sparse remove

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse remove <owner/repo> --paths "path1,path2"
```

#### Sparse list

```bash
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse list <owner/repo>
```

#### Example: progressive sparse checkout

```bash
# Add a submodule with initial sparse paths
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh add dotnet/dotnet-api-docs --sparse-paths "xml/System"

# Later, add more namespaces incrementally
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse add dotnet/dotnet-api-docs --paths "xml/System.Net,xml/System.IO"

# Check what's currently checked out
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse list dotnet/dotnet-api-docs

# Remove a namespace you no longer need
./.github/copilot/skills-bash/submodules-manage/scripts/manage-submodules.sh sparse remove dotnet/dotnet-api-docs --paths "xml/System.IO"
```
