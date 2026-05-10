---
name: whole-season-estimates
description: Estimate KicktippAi whole-season or whole-competition prediction costs. Use when Codex needs to document full-season model costs, research official match counts, reuse or collect reprediction-rate evidence, run the experiment cost estimator for projected match-prediction counts, or create docs under docs/experiments for season-scale cost planning.
---

# Whole-Season Estimates

Use this skill for season-scale prediction cost estimates. It extends
`estimate-experiment-cost-skill`; use that skill for all dollar estimates and
its hard rules.

## Workflow

1. Read `AGENTS.md`, this skill, `docs/langfuse.md`, and the
   `estimate-experiment-cost-skill` instructions.
2. Research the competition match count from an official or primary source.
   Record the source URL in the estimate document.
3. Search existing docs and skill references for matching reprediction evidence
   before querying Firebase. Prefer reusable evidence that matches the
   competition, community context, and production model.
4. Only run `dotnet run --project src/Orchestrator -- cost` when existing
   evidence is missing, stale for the intended estimate, or the user explicitly
   requests a refresh.
5. If `cost` is necessary, use the tightest filters possible. For example, use
   `--community-contexts pes-squad --models o3` for a Bundesliga production
   estimate instead of broad configs or `--all`. Add `--output-json` and keep
   the JSON under `C:\tmp` unless the user asks to persist it.
6. Convert the match count into two prediction counts:
   - No repredictions: official match count.
   - With repredictions: official match count plus the projected extra
     reprediction calls.
7. Run `experiment_cost_estimator.py estimate --counts ...` for every intended
   model and reasoning-effort pair. Rerun these commands immediately before
   writing final dollar values.
8. Document the exact estimator commands, base estimate rows used, prompt route,
   service tier, max output tokens, model knowledge cutoff, match-count source,
   reprediction evidence source, and assumptions under `docs/experiments`.

## Firestore Read Discipline

Treat Firebase reads as scarce because the project uses the free plan.

- Do not rerun `cost` just to refresh an estimate that already has suitable
  documented reprediction evidence.
- Do not run broad cost-analysis workflow configs for a single estimate.
- Always specify exact `--models` and `--community-contexts` when the estimate
  only needs one model/community pair.
- Use `--matchdays` only when a narrower subset is needed; otherwise leave
  matchdays unfiltered so the command does not enumerate every matchday first.
- Preserve the command and observed counts in the estimate doc so future agents
  can reuse the evidence without additional reads.

## Current Reusable Evidence

For Bundesliga 2025/26, reuse this evidence unless the user asks for a refresh:

- Source command family: `dotnet run --project src/Orchestrator -- cost`
  against `pes-squad` and `o3`, split once to avoid Firestore's 30-value
  `IN` limit before the command was made read-safer.
- Observed counts: index `0` = `313`, index `1` = `123`, index `2+` = `68`.
- Extra repredictions: `191`.
- Production-model multiplier basis: `(313 + 191) / 313`.
- For a `306` match Bundesliga season, projected calls are
  `306 + round(306 * 191 / 313) = 493`.

For competitions without meaningful between-matchday club context changes, use
`0` repredictions unless there is stronger evidence.

## Closeout

Validate any new or changed skill with the `skill-creator` validator. Inspect
the diff, commit the intended docs/code/skill changes, and push the branch with
an explicit remote and branch.
