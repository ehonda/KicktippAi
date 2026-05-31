# Codex Session Analysis: `Update prediction identity`

## Purpose

This note captures the analysis of the Codex session that updated prediction identity in `KicktippAi`, so we can rerun the same analysis after workflow/tooling changes and compare the results.

## Session Analyzed

- Thread name: `Update prediction identity`
- Thread id: `019e7b5a-495f-76f1-b466-41797f7fbd10`
- Main session log:
  `C:\Users\dennis\.codex\sessions\2026\05\31\rollout-2026-05-31T02-06-20-019e7b5a-495f-76f1-b466-41797f7fbd10.jsonl`
- Approval-review fork:
  `C:\Users\dennis\.codex\sessions\2026\05\31\rollout-2026-05-31T09-22-52-019e7ce9-f452-7bd3-9d73-19c35012ef75.jsonl`
- Supporting token/telemetry store:
  `C:\Users\dennis\.codex\logs_2.sqlite`

## Executive Summary

The session really was heavy. The biggest drivers were not a giant repeated tool-schema dump in the saved main transcript, but:

1. A very large number of tool round-trips in one long turn.
2. Broad searches and large file reads that repeatedly pushed near the tool output ceiling.
3. Approval-review overhead caused by forcing `git` and `dotnet` outside the sandbox.
4. Search noise from external/vendor trees and the local NuGet cache.

There was also a high `apply_patch` count, but that is confounded here by the workload itself touching many files. It is better treated as a possible multiplier, not as evidence of bad behavior by itself.

## Main Measurements

### Main session

- File size: about `4.12 MB`
- Line count: `1325`
- `response_item` entries: `946`
- Shell tool calls: `262`
- Shell tool outputs: `262`
- `apply_patch` calls: `92`
- Assistant messages: `36`
- User messages: `4`
- Total shell output volume: about `2.16 MB`

### Main-turn token usage

From `logs_2.sqlite`:

- `input_tokens`: `33,713,050`
- `cached_input_tokens`: `33,031,424`
- `non_cached_input_tokens`: `681,626`
- `output_tokens`: `92,305`
- `reasoning_output_tokens`: `21,851`
- `total_tokens`: `33,805,355`

Interpretation:

- About `97.98%` of input tokens were cached.
- About `2.02%` were non-cached.

This means the system kept replaying a very large and growing conversation context across many tool turns. Even though most of that was cached, the absolute size was still large enough to make the window feel expensive.

### Approval-review fork

- File size: about `1.06 MB`
- Line count: `273`
- User messages: `31`
- Assistant messages: `30`
- User-message text volume: about `429 KB`

Observed approval-review turns in the relevant time window: `17`

Lower-bound total token volume across those review turns:

- About `1,736,584` total tokens
- Average about `102,152` tokens per review turn

This is important because the repo instructions forced many `git` and `dotnet` commands outside the sandbox, which in turn created repeated approval-review traffic.

## Findings

### 1. Approval-review overhead was a real multiplier

The session issued `42` escalated shell calls:

- `30` `git` calls
- `11` `dotnet` calls
- `1` additional PowerShell rewrite command

Examples included repeated:

- `git status --short --branch`
- `git diff --check`
- `dotnet build ... --no-restore`

Because these commands had to run outside the sandbox, they triggered approval-review activity. That created a second conversation stream carrying approval prompts, transcript slices, and review instructions.

This was likely one of the biggest hidden reasons the window felt smaller than expected.

### 2. Search scope was often broader than necessary

The main transcript contained many large search outputs:

- `16` shell outputs above `30 KB`
- `10` shell outputs at about `40 KB`, which looks like the tool output ceiling

Representative examples:

- `rg -n "...many search terms..." .`
- `rg --files | rg "(workflow|workflows|predict|prediction|model|reasoning|verify|bonus|Firestore|database|commands|cli)"`
- `Get-ChildItem -Recurse -Filter *.cs src,tests | Select-String ...`

These commands pulled back large candidate sets before narrowing.

### 3. External trees and non-repo sources added noise

This was not just a theoretical concern. The saved outputs show clear noise from sources outside the core repo code:

- `external/openai/openai-dotnet/...`
- `external/spectreconsole/...`
- local NuGet cache hits under `C:\Users\dennis\.nuget\packages\spectre.console\...`
- research-heavy docs under `docs/research/...`

So yes: broad searches did pick up external/vendor/submodule-style content and inflated output volume.

### 4. Large file rereads also contributed

Repeated file reads were another meaningful source of transcript growth.

Examples:

- `src/Orchestrator/Commands/Observability/Cost/CostCommand.cs`: read `10` times
- `src/FirebaseAdapter/FirebasePredictionRepository.cs`: read `8` times
- `src/Core/IPredictionRepository.cs`: read `3` times
- `tests/Orchestrator.Tests/Infrastructure/OrchestratorTestFactories.cs`: read `3` times

This is understandable in a multi-file refactor, but it still adds up when combined with many tool turns.

### 5. No single repeated tool-description dump dominated the main saved session

I did not find evidence that the main saved session was dominated by the same full tool descriptions being injected over and over into the transcript itself.

There was some repeated instruction/context overhead:

- `session_meta` carried base instructions of about `21 KB`
- `turn_context` entries added some repeated environment/sandbox context
- compaction entries added about `206 KB` total

But these were not the primary bloat source compared with shell output volume and approval-review overhead.

### 6. The approval-review fork did repeat large instruction-style payloads

While the main session did not show a dominant repeated tool-schema dump, the approval-review fork did repeatedly carry large review payloads:

- approval policy/rubric instructions
- transcript slices
- approval request envelopes

So the repeated-overhead concern was real, just more in the review path than in the main coding path.

### 7. `apply_patch` count is a weak signal here

The session made `92` `apply_patch` calls.

That can increase cost because every extra edit round-trip replays the accumulated context again. However, this particular workload touched many files and involved a real cross-cutting refactor, so the raw `apply_patch` count is confounded.

Conclusion:

- It is fair to treat `apply_patch` fragmentation as a possible multiplier.
- It is not fair to conclude from this session alone that patching behavior was intrinsically inefficient.
- For future comparisons, the better question is whether similar many-file work can be completed with fewer edit round-trips without fighting the model.

## Most Likely Root Causes

If we rank the likely contributors to window pressure for this session:

1. Forced escalated `git` and `dotnet` commands, and the resulting approval-review fork traffic.
2. Broad searches that hit external/docs/vendor trees and returned near-maximal outputs.
3. Repeated large file reads during multi-file investigation.
4. Many total tool turns in one long turn.
5. Fragmented `apply_patch` usage, with the caveat above.

## Recommended Improvements

### High value

- Preapprove routine `git` prefixes used for status/diff/log/add/commit/push review flows in `KicktippAi`.
- Preapprove routine `dotnet build` and test prefixes used in this repo.
- Narrow default search scope to `src`, `tests`, `.github`, and only add `docs` or `.agents` when they are explicitly relevant.
- Exclude noisy trees by default when searching:
  - `external/**`
  - `docs/research/**`
  - generated artifacts
  - other vendor-like trees

### Medium value

- Prefer `rg -l` or a small first-pass search before full `rg -n` result dumps.
- Open targeted files after the first pass instead of keeping wide searches in the transcript.
- Avoid rereading the same large files unless new context really requires it.

### Low-to-medium value

- Encourage batching of edits when practical.
- Do not over-optimize around `apply_patch` counts alone for many-file work.

## Repeatable Comparison Checklist

When rerunning this analysis after fixes, compare at least these metrics:

1. Main session file size and line count.
2. Count of shell calls and shell outputs.
3. Count of outputs above `30 KB` and near `40 KB`.
4. Number of escalated `git` and `dotnet` calls.
5. Presence and size of approval-review fork sessions.
6. Total token usage for the main turn.
7. Lower-bound token usage across approval-review turns.
8. Whether broad searches still hit:
   - `external/`
   - vendor/submodule trees
   - `.nuget`
   - research-only docs
9. Whether large files are reread repeatedly.
10. `apply_patch` count, interpreted carefully against workload size.

## Suggested Comparison Questions

When we analyze the next session, the key questions should be:

- Did approval-review overhead drop after preapprovals or workflow changes?
- Did broad search output shrink after tightening search scope?
- Did external/vendor noise disappear from first-pass searches?
- Did the total number of tool round-trips fall?
- For similarly broad code changes, did the session become meaningfully smaller even if `apply_patch` remained fairly high?

## Bottom Line

The session felt expensive for good reasons. The strongest evidence points to review-path overhead plus broad/noisy search behavior, not just to the fact that the task touched many files.

If we fix the approval pattern and narrow search scope first, that should give the cleanest before/after comparison on a future session.

## 2026-06-01 Follow-Up

We tried a repo-local Codex permission-profile change to see whether the remaining Git escalation pain could be removed as well.

Observed result after a full Codex restart:

- `git add .codex/config.toml` still failed in the sandbox with `Unable to create '.git/index.lock': Permission denied`.
- `git ls-remote origin` started working in the sandbox.
- `git push --dry-run origin main` still did not complete reliably enough to adopt.

Practical conclusion:

- The earlier `safe.directory` change still helps with routine read-only `git` commands.
- The attempted `.git` write and push-network sandbox changes were not good enough to replace the existing outside-sandbox workflow for `git add`, `git commit`, and `git push`.
