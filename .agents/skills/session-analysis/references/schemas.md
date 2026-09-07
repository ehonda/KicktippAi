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
- `legacy`, `analysis_file`, and `analysis`. Only the fixed five pre-engine
  report IDs may use `legacy: true` with both analysis fields null. Every future
  report uses `legacy: false`, a repository-relative normalized artifact under
  `source_path`, and a non-null extraction configuration.

An extraction configuration fixes the root thread/log, UTC cutoff, generation
timestamp, timezone, an explicit privacy choice for bounded excerpts, a
repository-relative transcript snapshot lock, full base/final commits, dated
pricing, ordered agent
classification rules, injected-message prefixes, owner-message annotations,
and task-file discovery rules. This makes extraction reproducible without
forcing enrichment, metrics, findings, or visualization into one schema.

The snapshot lock lists every included thread ID and sessions-root-relative
filename plus the byte count, record count, and SHA-256 of records at or before
the cutoff. A future normalized `analysis_file` binds the manifest path and
digest, focus IDs, root/cutoff, Git range, privacy mode, and snapshot-lock path
and digest. Discovery fails if any binding drifts.

Future report HTML carries the offline CSP emitted by the report-shell helper.
The CSP must appear in `head` before script, style, body, or resource-capable
elements. Manifest paths reject `.` and `..` components and are resolved under
their repository, source, report, and publication roots before use. Pages copies
only the manifest-declared self-contained HTML file and privacy-checks the
manifest, complete report directory, future source directory, and generated
session-analysis publication surface.
The validator rejects unknown fields, unsafe paths, network-capable HTML assets,
and high-confidence private-path or credential leakage. Run it rather than
copying an example by hand.
