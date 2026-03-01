---
name: submodules-display-tree
description: Display the external/ submodule directory tree in multiple formats (tree, indented, flat). Use this to visualize the current submodule structure.
---

# Submodules — Display Tree

Render the `external/` submodule directory tree in one of three formats.

## How to Use

Run the PowerShell script from the repository root:

```powershell
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 [Directory] [options]
```

Arguments:

- `Directory` — Directory to display (default: `external`)

Options:

- `-Format <tree|indented|flat>` — Output format (default: `flat`)
- `-Depth N` — Maximum traversal depth (default: `0` = unlimited)
- `-DetailView name1,name2` — Show only the specified submodule roots with their full internal directory structure

## Formats

### Flat (default)

Brace-expansion notation, compact single-line per top-level group:

```text
external/{dotnet/dotnet-api-docs,spectreconsole/{spectre.console,website}}
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

```powershell
# Flat format (default)
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1

# Indented format
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 -Format indented

# Tree format, depth-limited
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 -Format tree -Depth 2

# Custom directory
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 some/other/dir -Format indented

# Detail view — show full structure of specific submodules
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 -DetailView dotnet-api-docs,openai-dotnet -Format tree

# Detail view with depth limit
.\.github\copilot\skills\submodules-display-tree\scripts\Display-Tree.ps1 -DetailView openai-dotnet -Format tree -Depth 3
```
