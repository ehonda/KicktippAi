# Session-analysis registry and report discovery

This directory is the durable control surface for future Codex session
analyses. It does not contain transcripts, raw prompts, reasoning, tool output,
or a universal report schema.

- `focuses.json` records active, retired, and superseded analysis questions.
  Standing questions remain active after coverage; one-off questions retire
  when an accepted report addresses them.
- `reports/*.report.json` is the manifest set used for focus coverage and GitHub
  Pages discovery. The fixed five historical reports use `legacy: true` with
  null analysis fields and remain frozen. New reports must bind a normalized
  artifact and transcript snapshot lock before discovery.
- Report-specific metrics, enrichment, findings, data shapes, and
  visualizations live with each purpose-named report under `docs/codex/`.

Use the explicit-only `$record-session-analysis-focus` skill to change the
registry and `$session-analysis` to produce a new bounded analysis. The active
orchestration workflow never reads or writes this directory.

From the repository root, validate the registry and complete discovery set
with:

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/focus_registry.py --registry docs/codex/session-analysis/focuses.json validate
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/report_manifest.py validate --manifest-dir docs/codex/session-analysis/reports --registry docs/codex/session-analysis/focuses.json --repo . --verify-html
```
