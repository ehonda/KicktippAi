# KicktippAi Agent Context

This document contains context relevant when working on tasks in this repository.

## Gathering Information

We use different external dependencies, some of which are partially or fully available locally via git submodules. When gathering information like

- Code
- Documentation
- Usage examples

search it in the following places, in that order:

1. Local git submodules (See [Submodule Tree](#submodule-tree))
2. GitHub via MCP
3. Web search

## Git Submodules

### Submodule Tree

@agent-files/submodule-tree.txt

### Updating the Submodules

When you encounter a dependency that is not available locally, and which has a chance of being consulted multiple times, use the `submodules-manage` skill to add it or part of it as a git submodule. This will make it available locally for future reference and easy agentic access.
