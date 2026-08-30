# P1-03 — Extract generic competition onboarding tooling

- Status: Complete
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)

## Outcome

A generic `competition-onboarding` skill drives shared onboarding stages while Bundesliga and WM26 remain thin, explicit profiles.

## Work items

- [x] Compare the completed Bundesliga evidence with `.agents/skills/wm26-onboarding/` and identify only proven shared stages.
- [x] Use the global `skill-creator` instructions to design the generic skill.
- [x] Define a profile contract for competition identity, teams, collectors, required context, prompts/models, costs, communities, validation, and activation.
- [x] Move shared sequencing and evidence requirements into the generic skill.
- [x] Keep WM26-specific squad timing, FIFA rankings, date maps, knockout behavior, and hosted prompt checks in its profile/entry point.
- [x] Add a Bundesliga profile that links to this task plan, ADRs, data paths, and validation commands.
- [x] Validate both entry points and document how future domestic seasons create a new profile.

## Validation

- [x] The prescribed `uv --cache-dir .uv-cache run --with PyYAML python C:\Users\dennis\.codex\skills\.system\skill-creator\scripts\quick_validate.py <skill-folder>` passed for `competition-onboarding`, `wm26-onboarding`, and `bundesliga-2026-27-onboarding`.
- [x] An isolated raw-input dry-walk resolved WM26 to `Kicktipp -> Wm26HistoryPlayedDates -> FifaRankings -> NationalLineups` and Bundesliga to `Kicktipp -> BundesligaHistoryPlayedDates -> ClubElo -> Rosters`; each profile included its own ADR checkpoint and explicit exclusion of the other collector set. The dry-walk made no collector or external calls.
- [x] Focused TUnit runs for `CollectContextProfileCommandTests` and `CollectContextDevCommandTests` exited successfully. They are the existing regression coverage for exact profile resolution, collector order, dry-run behavior, and WM26/Bundesliga isolation.

### Review follow-up forward dry-walks — 2026-08-30

The reproducible raw requests and inputs were created in ignored
`.tmp/p1-03-forward-tests/requests.json` and deleted after the run:

```text
Use $wm26-onboarding to prepare a safe no-write context preflight for ehonda-dev-wm26 after a date-map change. Do not perform a write.
Input: fifa-world-cup-2026 / ehonda-dev-wm26
Expected: Kicktipp -> Wm26HistoryPlayedDates -> FifaRankings -> NationalLineups

Use $bundesliga-2026-27-onboarding to prepare the safe development-profile dry-run before a collector-topology change. Do not write context or predictions.
Input: bundesliga-2026-27 / ehonda-dev-buli-2627
Expected: Kicktipp -> BundesligaHistoryPlayedDates -> ClubElo -> Rosters
```

- [x] The observed terminal artifact confirmed both exact orders, the explicit
      exclusion of the other profile's collector set, every generic contract
      area, the WM26-only safe `--dry-run` date-map command, required WM26
      safety terms, and every local profile link.
- [x] The forward dry-walks intentionally did **not** perform provider
      authentication, Firestore writes, community-membership checks, prediction
      posting, workflow activation, or schedule rollback approval. They are
      profile-routing evidence only, not live operational evidence.
- [x] Review restored WM26 safety through the required
      `references/operational-safety.md` read while retaining the concise entry
      point. It routes exact existing source/format, strict/guarded date-map,
      probe, snapshot, secrets, costs, workflow, first-run, owner, and rollback
      rules rather than duplicating the historical onboarding record.
- [x] Final review validation reran all three prescribed skill validators;
      `CollectContextProfileCommandTests` passed `13/13` and
      `CollectContextDevCommandTests` passed `4/4`; final link and diff checks
      passed. Existing compiler warnings remained non-failing and unrelated.
- [x] Rereview corrected the WM26 full-profile preflight to
      `collect-context profile --community-context <community-context>
      --competition fifa-world-cup-2026 --dry-run --verbose`; the
      `collect-context-dev` shortcut remains explicitly development-only.
      A raw `rabetrabauken2026` forward input resolved only that non-dev target
      and its WM26 collector sequence, not `ehonda-dev-wm26`.
- [x] Rereview verified the WM26 runtime policy from
      `PredictionServiceCommandSupport` and `PredictionService`: Flex first,
      with exactly one `default`-tier retry only for HTTP `408`; a `429` classified as
      Flex resource-unavailable or retryable non-quota rate limit;
      `TimeoutRejectedException`; `TimeoutException`; or non-caller-cancelled
      `TaskCanceledException`. Quota `429`s, auth/validation failures, and
      caller cancellation do not fall back.
      It also verified all four fully qualified Bundesliga data paths exist.
- [x] A raw future `bundesliga-2027-28` request found no profile and stopped
      before inheriting any current collector, prompt, community, cost, or
      activation value. The ignored forward-test input was removed after this
      no-write check.

## Complete when

- Shared steps have one source of truth and competition-specific requirements remain isolated.
- Both skill entry points pass validation and include ADR checkpoints.
