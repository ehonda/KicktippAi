# ADR-0023: Use orchestrator-created CLI worktrees for parallel writers

- Status: Superseded by [ADR-0061](0061-preview-and-milestone-orchestration.md)
- Date: 2026-08-21

## Context

ADR-0009 permits two simultaneous writers only in isolated worktrees, but the earlier operating default rarely created the second worktree. That left independent P0 work queued and made the measured task-agent concurrency effectively one.

Bundesliga work also needs credentials stored in the sibling `KicktippAi.Secrets` checkout. A command-line worktree below the repository's ignored `.tmp/worktrees` directory does not have that sibling topology. Codex desktop-managed worktrees can copy selected ignored local files through `.worktreeinclude`, but command-line `git worktree` creation does not process that file. Copying credentials into worktrees is unacceptable.

The feasibility experiment proved that two agents can edit, build, test, commit, and push disjoint branches from repo-local command-line worktrees. It also exposed Windows Defender prompts from worktree-specific test apphosts whose WireMock tests open listeners. Disabling apphosts in listener test projects gives those worktrees the stable `C:\Program Files\dotnet\dotnet.exe` host identity.

## Decision

For dependency-safe P0 and P1 slices, the autonomous default is two isolated writer lanes:

- the orchestrator creates a branch and command-line worktree for each lane below `.tmp/worktrees`;
- the primary checkout is integration-only while lane writers are active;
- each lane has explicit, disjoint path ownership, one writer, and authority to commit and push only its own branch;
- the orchestrator writes the ignored `.codex-local/original-repository-path` file into each worktree and validates that it identifies the original checkout; code uses that locator to resolve the sibling secrets checkout, but neither credentials nor secret contents are copied or printed;
- `.worktreeinclude` remains optional support for desktop-managed worktrees and is not relied upon for command-line worktrees;
- lane-local builds and tests run concurrently by default, including full gates when required, and concurrency is reduced only when measured resource or test interference warrants it;
- Git integration and `main` mutation, live external collection or writes, and final integrated validation are serialized by the orchestrator;
- the orchestrator integrates reviewed lane commits sequentially, pushes the combined explicit `main` target, and reconciles exact-SHA CI before removing recoverable worktrees; and
- after each lane branch is pushed and integrated or otherwise recoverable, the orchestrator verifies the worktree is clean, removes it, prunes stale worktree metadata, and confirms no temporary worktrees remain; remote lane branches may remain for recoverability unless separately removed.

On Windows, every test project that uses WireMock, or a future equivalent local listener that triggers Defender, must set `<UseAppHost>false</UseAppHost>` for unattended worktree orchestration. Today that applies to `KicktippIntegration.Tests` and `Orchestrator.Tests`; adding WireMock or such a listener to another project requires adding and validating the setting there. Their listener tests run through the stable installed `dotnet.exe` host. A machine may require one private-network firewall approval for that stable host; it must not require a new rule or click for every worktree. Keep this setting narrowly for P0/P1, then remove and re-evaluate it, including whether command-line `-p:UseAppHost=false`, a worktree setup wrapper, or another solution is preferable. Production projects and the other test projects remain unchanged.

When no dependency-safe second write slice exists, use one writer plus one read-heavy helper. This decision does not raise the two-task-agent or two-writable-worktree limit.

## Alternatives considered

- **Codex desktop-managed worktrees as the required path:** Not chosen because the root orchestrator cannot autonomously rely on UI-managed worktree creation, and its `.worktreeinclude` copying behavior does not apply to command-line worktrees.
- **Two writers in the primary checkout:** Rejected because ownership boundaries would not isolate Git state, builds, or accidental edits.
- **Copy `.env` or Firebase credentials into each worktree:** Rejected because it multiplies secret material and creates disclosure and cleanup risk.
- **Serialize every build and test across lanes:** Rejected because isolation makes lane-local validation independent; unconditional serialization removes much of the parallelism. Concurrency should be reduced only from measured pressure or interference.
- **Keep one writer as the standing default:** Retained only as the fallback when the dependency graph or file ownership cannot supply a safe second writer slice.

## Consequences

- The orchestrator must create locators explicitly for command-line worktrees and validate them without exposing secret values.
- Every lane assignment must state branch, worktree, owned paths, validation, and push authority.
- Parallel validation consumes more local resources, so the orchestrator must observe real contention and throttle when evidence warrants it.
- Git integration, external mutations, and the final combined gate remain deterministic and sequential.
- Temporary worktree cleanup is a required workflow completion gate because each worktree consumes substantial disk.
- Windows listener tests use a stable host identity, avoiding per-worktree firewall prompts after the one-time machine approval. The temporary P0/P1 setting must be revisited after P1.

## Affected tasks

- [Bundesliga 2026/27 execution strategy](../execution-strategy.md).
- [Codex subagent orchestration investigation](../subagent-orchestration-investigation.md).

This changes orchestration around dependency-safe P0/P1 tasks; it does not change any individual task contract or status.

## Supersedes

ADR-0009's requirement to serialize full builds and full test suites. ADR-0009's two-agent, two-worktree ceiling and hybrid Git policy remain in force.
