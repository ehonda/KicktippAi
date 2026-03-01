---
name: submodules-update-tree-document
description: Update the agent-files/submodule-tree.txt file with the current flat tree view of external/ submodules. Use this after adding or removing submodules to keep the tree document in sync.
---

# Submodules — Update Tree Document

Update `agent-files/submodule-tree.txt` with the current flat tree view of the `external/` submodule directory.

The output file contains **only** the raw output from the display-tree script — no headers, no markdown, no timestamps.

## How to Use

Run the bash script from the repository root:

```bash
./.github/copilot/skills-bash/submodules-update-tree-document/scripts/update-tree-document.sh
```

## What It Does

1. Creates `agent-files/` directory if it doesn't exist
2. Runs the `submodules-display-tree` script with `--format flat` on `external/`
3. Writes the raw output to `agent-files/submodule-tree.txt`

The file is overwritten on each run (idempotent).

## When to Use

Run this script after any of the following:

- Adding a new submodule via `submodules-manage`
- Removing a submodule via `submodules-manage`
- Any change to the `external/` directory structure
