---
name: record-session-analysis-focus
description: Add, update, list, retire, or supersede durable Codex session-analysis questions in the canonical focus registry. Invoke only when the owner explicitly requests `$record-session-analysis-focus`; do not run an analysis, load transcripts, or use this skill from `$orchestrate`.
---

# Record Session Analysis Focus

Manage only `docs/codex/session-analysis/focuses.json` through
`scripts/manage_focuses.py`. Do not inspect transcripts or infer an answer to
the question.

Start by listing active focuses. Infer a concise stable ID and the narrowest
useful applicability from the owner's wording. Distinguish:

- `standing`: revisit on every matching analysis; coverage advances
  `last_covered_by` and leaves it active;
- `once`: answer once; report consumption retires it.

Ask only when cadence or applicability is materially ambiguous. Keep related
questions in one focus when they share evidence and lifecycle. The helper
rejects likely duplicate or overlapping active focuses; use `--allow-overlap`
only after confirming that the new focus is intentionally distinct.

Supported operations are `list`, `validate`, `add`, `update`, `supersede`, and
`retire`. `retire` requires the exact covering report pointer. Prefer
`supersede` when a better standing question replaces an older one. Never hand-
edit report-resolution pointers or append a coverage-history array.

Run from the repository root with repository-local Python tooling:

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
uv --cache-dir .uv-cache run python -B .agents/skills/record-session-analysis-focus/scripts/manage_focuses.py --registry docs/codex/session-analysis/focuses.json list --status active
```

This skill changes a repository file only. Commit or publication still follows
the repository's normal authorization; explicit invocation does not grant
unrelated external actions.
