---
name: session-analysis
description: Produce a reproducible, privacy-bounded analysis of a Codex session with exact transcript and Git boundaries, flexible question-driven evidence, a self-contained report, focus-registry coverage, and manifest-based publication. Invoke only when the owner explicitly requests `$session-analysis`; do not use during ordinary orchestration or for routine status reporting.
---

# Session Analysis

Analyze the requested session as a separate, explicit workflow. Never load this
skill, the focus registry, private transcripts, or prior report data into an
active `$orchestrate` run merely because later analysis is planned.

## Bound the snapshot

Before extraction, resolve and record:

- the exact root thread ID and one matching root JSONL filename;
- an explicit UTC event cutoff, even for an ended session;
- full base and final Git commit SHAs; and
- a fixed analysis-generation timestamp, local reporting timezone, and
  date-stamped model-pricing evidence.

Stop rather than selecting a transcript by recency or silently widening a Git
range. Transcript files stay local. Do not publish complete prompts, messages,
reasoning, tool output, secrets, user-home paths, or prediction payloads. Keep
bounded excerpts disabled in the manifest unless the questions genuinely
require them; when enabled, review the sanitized excerpts before publication.
The shared extractor always keeps hashes, timestamps, usage, and normalized
operational facts.

## Select questions without fixing the report shape

Run `scripts/report_manifest.py select-focuses` for the report's session kind.
Apply active focuses whose applicability matches the actual run, plus explicit
ad hoc owner questions. An active focus is a question, not a required finding:
leave it uncovered when the evidence cannot answer it, and never manufacture a
conclusion merely to retire it.

Choose metrics, enrichment tables, findings, and visualizations that answer the
selected questions. Reuse a prior report's shape only when it fits; there is no
universal dashboard. Keep causal claims separate from descriptive counts, and
state denominator, cutoff, overlap, and pricing limitations near the claim.

## Create the report

1. Read [the maintained schemas](references/schemas.md). Add one
   `docs/codex/session-analysis/reports/<id>.report.json` manifest and a new
   purpose-named source directory under `docs/codex/`. New manifests use
   `legacy: false`, a non-null extraction contract, an `analysis_file`, and a
   `snapshot_lock`. Never rewrite a frozen historical report to adopt this
   engine or mark another report as legacy.
2. Run `scripts/run_analysis.py` from the repository root with the manifest,
   local sessions directory, repository, and `--create-snapshot-lock` on the
   first extraction. Review and commit the generated lock; omit the creation
   flag on reruns so any family, filename, included-byte, or hash drift fails.
   An explicit output directory must contain the manifest's `analysis_file`.
3. Add only report-specific enrichment and presentation needed by the
   questions. Preserve normalized data and a concise reproduction contract in
   the report source directory.
4. For a new destination, run `scripts/create_report_shell.py --manifest
   <manifest> --repo .`, then replace its placeholder with whatever
   question-driven structure the report needs. The helper refuses to overwrite
   an existing report. Keep its offline Content-Security-Policy. Runtime CSS,
   JavaScript, fonts, and data must be embedded; ordinary source hyperlinks may
   remain external.
5. Put in `focus_ids` only focuses actually addressed by the reviewed report.
   If this changes the manifest after extraction, rerun the locked extraction
   so the normalized artifact carries the final manifest digest and focus IDs.
   Run `scripts/report_manifest.py validate --verify-html
   --allow-unconsumed-focuses` before consuming them.
6. In the same commit as the accepted report and manifest, run
   `scripts/focus_registry.py cover --manifest <path>`. This retires covered
   one-offs and advances only `last_covered_by` for standing focuses. Validate
   the complete manifest set again afterward.

The Pages builder discovers reports only through valid manifests. Publication,
commit, push, or merge follows the repository's normal authorization and exact-
target checks; invoking this skill alone does not expand external authority.

## Deterministic commands

Use repository-local `uv` execution and disable bytecode when the sandbox makes
`.agents/` read-only:

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/report_manifest.py select-focuses --registry docs/codex/session-analysis/focuses.json --session-kind orchestration
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/run_analysis.py --manifest <manifest> --sessions-dir <sessions-dir> --repo . --output-dir <report-data-dir> --quiet --create-snapshot-lock
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/create_report_shell.py --manifest <manifest> --repo .
uv --cache-dir .uv-cache run python -B .agents/skills/session-analysis/scripts/report_manifest.py validate --manifest-dir docs/codex/session-analysis/reports --registry docs/codex/session-analysis/focuses.json --repo . --verify-html
```

When changing the shared extractor or normalization contract, also run
`scripts/verify_parity.py` against the checked-in P0 snapshot. This requires the
owner's local transcript family; ordinary report validation does not.
