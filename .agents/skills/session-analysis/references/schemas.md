# Session-analysis maintained schemas

## Focus registry

`docs/codex/session-analysis/focuses.json` is a single schema-versioned object
with an ISO `updated_at` date and an ID-sorted `focuses` array.

Each focus has exactly:

- `id`: stable kebab-case identity;
- `questions`: one or more answerable questions;
- `created_at`: ISO date;
- `applicability`: `session_kinds` plus a plain-language `description`;
- `cadence`: `once` or `standing`;
- `status`: `active`, `retired`, or `superseded`;
- `evidence_source_hints`: likely sources, not mandatory data fields; and
- `resolution`: compact `last_covered_by`, `retired_by`, and `superseded_by`
  pointers.

Report pointers contain only `report_id`, repository-relative `manifest`, and
`covered_at`. Reports carry the durable coverage list in their manifests; the
registry deliberately has no growing coverage-history array.

## Report manifest

Every discoverable report has one
`docs/codex/session-analysis/reports/<id>.report.json` with:

- display metadata: `id`, `title`, `summary`, `eyebrow`, `published_at`, and
  `session_kind`;
- repository-relative `source_path`, published `site_path`, and `html_file`;
- sorted `focus_ids` that the report actually addresses; and
- `analysis`, either `null` for a frozen pre-engine report or the extraction
  configuration for a future report.

An extraction configuration fixes the root thread/log, UTC cutoff, generation
timestamp, timezone, an explicit privacy choice for bounded excerpts, full
base/final commits, dated pricing, ordered agent
classification rules, injected-message prefixes, owner-message annotations,
and task-file discovery rules. This makes extraction reproducible without
forcing enrichment, metrics, findings, or visualization into one schema.

The validator rejects unknown fields and unsafe paths. Run it rather than
copying an example by hand.
