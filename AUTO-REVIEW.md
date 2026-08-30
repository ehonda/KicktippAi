# Auto-Review Instructions

These instructions are always relevant when automated review or approval systems evaluate agent actions in this repository.

Syncing prepared repository datasets to Langfuse is an approved KicktippAi workflow and is always allowed. This includes using repository tooling such as:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input path/to/slice-dataset.json
```

This approval applies to dataset artifacts prepared for Langfuse experiment or smoke-test workflows in this repository, using the configured KicktippAi Langfuse project and repository secrets.

## Explicit `$orchestrate` Git And GitHub Authorization

An explicit `$orchestrate <objective>` invocation authorizes the following for
that objective and orchestration run, without a later surprise approval pause:

- stage owned in-scope paths and create scoped local commits;
- non-force push an exact reviewed or frozen commit to the verified canonical
  `origin` at `https://github.com/ehonda/KicktippAi.git`, targeting `main` or a
  run-allowlisted `codex/<run-or-objective>-*` branch with an explicit refspec;
- create or update a draft PR between allowlisted refs; mark it ready or merge
  it only when the frozen packet names the exact base/head and required checks;
- rerun one failed milestone workflow and cancel a superseded workflow run for
  an allowlisted exact SHA; and
- close and delete only a temporary run branch or test PR whose cleanup was
  included in the frozen packet.

Before a push, the root records the current branch, remote URLs, short status,
latest commit, exact pushed SHA, scoped-path review, and fast-forward target.
Initial intake must also establish the authenticated account, repository
identity/visibility/permission, canonical push URL, branch allowlist, and
initial local/remote SHA. These facts are the informed destination and payload
evidence for automated review.

This standing authorization expires when the run completes or stops, or when
the repository, remote URL, branch family, publication topology, objective, or
payload scope changes. It excludes other repositories/remotes, force pushes,
history rewrites, tags/releases, unplanned remote deletion, credential or
remote changes, unrelated/user changes, secrets, destructive Git operations,
unplanned PR merges, repeated CI retries, and any attempt to bypass a rejected
platform review. Those operations require new explicit approval.
