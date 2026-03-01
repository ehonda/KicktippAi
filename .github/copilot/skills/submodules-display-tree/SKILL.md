---
name: submodules-display-tree
description: Display the external/ submodule directory tree in multiple formats (tree, indented, flat). Use this to visualize the current submodule structure.
---

# Submodules — Display Tree

Render the `external/` submodule directory tree in one of three formats.

## How to Use

Run the bash script from the repository root:

```bash
bash .github/copilot/skills/submodules-display-tree/scripts/display-tree.sh [directory] [options]
```

Arguments:

- `directory` — Directory to display (default: `external/`)

Options:

- `--format <tree|indented|flat>` — Output format (default: `flat`)
- `--depth N` — Maximum traversal depth (default: unlimited)

## Formats

### Flat (default)

Brace-expansion notation, compact single-line per top-level group:

```text
external/[dotnet/dotnet-api-docs,spectreconsole/{spectre.console,website}]
```

### Indented

Single-space indented list with trailing `/` on directories:

```text
external/
 dotnet/
  dotnet-api-docs/
 spectreconsole/
  spectre.console/
  website/
```

### Tree

Classic tree-style with box-drawing characters:

```text
external/
├── dotnet/
│   └── dotnet-api-docs/
└── spectreconsole/
    ├── spectre.console/
    └── website/
```

## Examples

```bash
# Flat format (default)
bash .github/copilot/skills/submodules-display-tree/scripts/display-tree.sh

# Indented format
bash .github/copilot/skills/submodules-display-tree/scripts/display-tree.sh --format indented

# Tree format, depth-limited
bash .github/copilot/skills/submodules-display-tree/scripts/display-tree.sh --format tree --depth 2

# Custom directory
bash .github/copilot/skills/submodules-display-tree/scripts/display-tree.sh some/other/dir --format indented
```
