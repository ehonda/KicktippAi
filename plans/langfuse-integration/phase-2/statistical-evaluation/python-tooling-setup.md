# Python Tooling Setup

This directory now has a minimal repo-local Python baseline for the Phase 2 statistical evaluation work.

## Current Setup

- Global shell default: `python` resolves to the `uv`-managed Python `3.14.x` installed in the user bin directory
- Repo pin: [`.python-version`](../../../../.python-version) pins the repository to Python `3.14`
- Repo project: [`pyproject.toml`](../../../../pyproject.toml) defines a minimal `uv` project for future tooling work
- Repo lockfile: [`uv.lock`](../../../../uv.lock) exists and should be updated as dependencies are added
- Local environment: `.venv/` exists at the repo root and is ignored by Git

## What Was Initialized

The repo was bootstrapped with `uv` so future sessions can add tooling incrementally instead of starting from scratch.

- `uv init --bare --python 3.14 --no-pin-python --no-readme --no-workspace .`
- `uv sync`

This gives us:

- a committed Python version pin
- a minimal `pyproject.toml`
- a committed `uv.lock`
- a local `.venv/` ready for `uv run`, `uv add`, and `uv sync`

## Recommended Workflow

From the repo root:

```powershell
uv sync
uv run python -V
```

When the analysis tooling session starts, add the first dependencies with `uv` instead of editing files manually. Expected early packages:

```powershell
uv add --dev langfuse pandas scipy statsmodels
```

If the first tooling pass needs notebooks or plotting, add those only when they are actually needed.

## Notes For The Next Session

- Prefer `uv run ...` for repo-local commands so the pinned interpreter and `.venv` are used consistently
- Keep new Python tooling repo-local; do not rely on a manually activated global environment
- Treat this as tooling infrastructure, not a distributable Python package
- Read [../tasks/further-improvement/02-statistical-evaluation-and-analysis-tooling.md](../tasks/further-improvement/02-statistical-evaluation-and-analysis-tooling.md) before adding the first analysis commands or scripts
