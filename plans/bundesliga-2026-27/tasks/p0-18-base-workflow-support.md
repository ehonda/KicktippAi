# P0-18 — Add Bundesliga support to reusable workflows

- Status: Complete
- Priority: P0
- Depends on: [P0-14](p0-14-profile-driven-collection.md), [P0-17](p0-17-community-scope.md)
- Decisions: [ADR-0034](../decisions/0034-drive-context-collection-from-competition-profiles.md), [ADR-0037](../decisions/0037-record-immutable-bonus-context-manifests.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

Reusable context, matchday, and bonus workflows can run the 2026/27 profile with explicit competition, prompt, and model inputs.

## Work items

- [x] Replace WM26-specific collector booleans in `base-context-collection.yml` with the profile/command contract from P0-14, while keeping WM26 callable through its own profile.
- [x] Require an explicit competition in all three reusable workflows.
- [x] Surface the pinned model, reasoning effort, output cap, and prompt identity needed by P0-06.
- [x] Make context summary output list the resolved profile and actual collector results.
- [x] Provide a sequencing mechanism or documented dependency so prediction jobs cannot consume context before collection succeeds.
- [x] Add workflow validation/tests or a deterministic local lint path for required inputs and generated command lines.

## Validation

- Validate all workflow YAML and inspect rendered command lines for Bundesliga and WM26 callers.
- Confirm a Bundesliga context invocation contains the Bundesliga history played-date step and no FIFA, lineup, WM26 date-map, or transfer step.

## Evidence — 2026-08-22

- `base-context-collection.yml` now requires an explicit competition and invokes the general profile command. Its stable job summary records the resolved profile and every collector disposition, including skipped and failed phases. The Bundesliga order is Kicktipp, included-in-Kicktipp played-date reconstruction, Club Elo, and rosters; WM26 remains an inert callable regression path with its own profile.
- The matchday and bonus bases require explicit competition, model, reasoning effort, output cap, prompt source, and exact prompt identity. Hosted prompt preflight resolves the requested name, label, and numeric version before checkout; the exact same identity reaches initial verification, prediction, and final verification. Bonus final verification includes the ADR-0037 `--check-outdated` gate.
- The [workflow sequencing contract](../../../.github/workflows/README.md#bundesliga-202627-sequencing-contract) makes a successful context run for the exact competition and `community_context` a prerequisite to prediction dispatch. P0-19 entrypoints remain manual-only, P0-20 records the context run before dispatching predictions, and P0-21 owns production schedule ordering and first-sequence observation.
- Fourteen prefixed WM26 callers remain `workflow_call`-only and keep their accepted historical identity. Twelve superseded Bundesliga 2025/26 callers are explicitly retired and fail before checkout instead of silently inheriting a fabricated prompt version. No schedule, live trigger, external write, or production choice was added.
- The prediction lane's first independent review identified shell rendering of dispatch inputs inside summary steps. Commit `e99a6e865ba1909726f163651da90523113df98a` routed those values through quoted environment variables; hostile quotes, newlines, command substitutions, `$HOME`, and backticks remain literal. The second review approved the full lane with no findings.
- The context lane was independently approved through `d9a15a5331b0f688e93f9f0c44b31b2002247953`. The prediction lane was independently approved through `e99a6e865ba1909726f163651da90523113df98a` and integrated as `e23d553` plus `220c93c`.
- Integrated main at `220c93c0bb73a4ad9dba77401efa0382ad0692c8` passed the Release solution build with `0` errors, the full Release Orchestrator suite `1033/1033`, the prediction workflow contract (`2` bases, `14` callable WM26 entrypoints, `12` explicitly retired Bundesliga entrypoints, `0` current Bundesliga callers), and parsing of all `36` workflow YAML files.
- Exact-head CI run [32533821269](https://github.com/ehonda/KicktippAi/actions/runs/32533821269) for closure commit `481fac53bb1a6bf0d8b3c651d2179c11efad0bc9` passed Build, all eight test projects, merged coverage, and GitHub Pages (`12/12` jobs successful).
- The temporary worktrees were created with `New-AgentWorktree.ps1`; both contained the validated `.codex-local/original-repository-path` locator. Neither lane encountered a worktree restriction or auto-review denial.

## Complete when

- A community entrypoint only needs to supply its community matrix values and trigger policy.
- Missing competition or model identity fails before a prediction command starts.
