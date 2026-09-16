# Bundesliga 2026/27 Competition Profile

## Current activation state — 2026-09-16

The current plan is [P1-04 operational activation](../../../../plans/bundesliga-2026-27/tasks/p1-04-club-elo-refresh.md)
under [ADR-0083](../../../../plans/bundesliga-2026-27/decisions/0083-activate-official-club-elo-context-refresh.md).
Merged PR #111 is prior dormant C3/E1 implementation evidence, not operational
completion. S0 is accepted; E2 parser-v2, W2 transport, V2 wiring, source-only
live validation and A2 scheduled enablement are not implemented or evidenced.

Use only the explicit Club Elo source-only route for validation. It reuses
normal preparation/selection/publication/receipt behavior while resolving no
Kicktipp, history, roster, model, OpenAI or Langfuse service. The production
target is eight ordered receipts across four physical heads: pes-squad,
schadensfresse, relaxdays-tippt and a shared arena context for five arena
receipts. Normal source flags stay false until A2 after real evidence. The
owner-authorized scope covers bounded Club Elo reads/context writes, GitHub
handoff/issues, existing-schedule source activation after proof, Pages and a
ready PR merge. It excludes models, prediction/posting, roster work, new
schedules, credential changes and unrelated P1 work.

P1-05/R1 remains ADR-0082 dormant/deferred and `MetadataUnavailable`. Do not
probe a sidecar/artifact, resolve or adopt a provider, or create roster source
observations, receipts, health, publications, issues or API effects. Its exact
ADR-0079 sidecar and separate future owner gates remain required. No disk
restriction applies at or above 20 GiB effective free space.

## Historical dormant profile record

The remainder of this profile retains prior dormant C3/E1 and P1-05 scope
details for reconstruction. It is not the current activation status; ADR-0083,
the current plan index and active task control where they differ.

# Historical Bundesliga 2026/27 Competition Profile

Use this profile only for `bundesliga-2026-27`. Follow the [current plan index](../../../../plans/bundesliga-2026-27/README.md), then read only the active task/design and operative ADRs it names for the assigned change. Read completed P0 tasks, historical activation evidence, and the broader [execution strategy](../../../../plans/bundesliga-2026-27/execution-strategy.md) only when the current contract or a cold reconstruction explicitly requires them. Runtime source: `CompetitionCollectionProfileResolver`.

## Dormant refresh status — 2026-09-15

P1-04 dormant implementation is complete. P1-05 dormant closeout remains
complete under ADR-0082, with R1 deferred pending the exact ADR-0079 dcaribou
sidecar and owner gates. Rejected R1 and old closeout work receive no credit.

C3 was accepted at `d882f75b5dcd86ec2886b1373a260af3f7ea3d54` and
passed exact-head CI on [draft PR #111](https://github.com/ehonda/KicktippAi/pull/111).
E1 was accepted and pushed to the same draft PR at
`1d43ac397eaed4f82db016114630acdd874042c5`;
[exact-head CI run 34931605592](https://github.com/ehonda/KicktippAi/actions/runs/34931605592)
was green: 10 build/test/coverage checks passed, with the conditional Pages
check skipped. Local cumulative E1 evidence was Core 390, Firebase 448, and
Orchestrator 1,398: 2,236 passed, no failures or skips. C3's prior cumulative
evidence was 2,120 passed.

All source flags remain false. Live acquisition, unattended HTML reuse,
development or production Firestore writes, and source activation remain
separate owner gates. This closeout authorizes or completes no W1/A1 or other
P1 work, and changes no schedule, topology, model, prompt, credential, posting,
or copy behavior. Operational/live validation and activation require separate
owner authorization and evidence.

## Contract

- **Identity and teams:** Development community `ehonda-dev-buli-2627`; 18 teams; 306 matches; exactly nine fixtures/matchday; season `2026-08-28` through `2027-05-22`. Use `data/bundesliga-2026-27/team-manifest.csv`, `data/bundesliga-2026-27/history/history-played-dates.csv`, `data/bundesliga-2026-27/rosters/roster-membership-seed.csv`, and `data/bundesliga-2026-27/club-elo-launch-seed.csv`. Preserve ADR-0003 fallback/LKG gates.
- **Collectors:** `Kicktipp` → embedded `BundesligaHistoryPlayedDates` → `ClubElo` → `Rosters` through the profile only. History is atomic with Kicktipp. Never invoke WM26 history, FIFA rankings or national lineups.
- **Context:** standings, community rules, recent/home/away history, head-to-head, paired roster/Club Elo docs, aggregate `team-rosters`, and KPIs `club-elo-rankings`/`team-squad-summary`; home/away/head-to-head enabled, knockout/transfers prohibited. Apply ADR-0074/0077/0078 and [ADR-0079](../../../../plans/bundesliga-2026-27/decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md) source-provenance contracts.
- **Refresh sources:** ADR-0074/0077/0078, [ADR-0079](../../../../plans/bundesliga-2026-27/decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0081](../../../../plans/bundesliga-2026-27/decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](../../../../plans/bundesliga-2026-27/decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md) govern dormant P1-04/P1-05. Club Elo's implemented dormant candidate is descriptor-selected official HTML (`club-elo/source.html`), with historical CSV evidence only; ADR-0081 closes shared selection, receipt completion and source-backed publication while preserving CSV fractional Elo compatibility. P1-05 is HTML-independent and `Deferred — authoritative dcaribou sidecar and owner gates outstanding; dormant closeout complete under ADR-0082.` Dcaribou remains the sole metadata authority: until the exact ADR-0079 sidecar exists, it is `MetadataUnavailable`, has no probe, resolves no provider, requests no artifact, and produces no observation, receipt, artifact, health, publication, issue, or API action. Seed/LKG retains truthful original dates and provenance; automatic freshness is unavailable. The current DuckDB and synthetic trusted-date fixtures are rejection/mechanics evidence only. C2 is reusable common evidence only. R1 acquisition/provider/consumer, selection/diff/carry, canonical v3/reconstruction, source tests, enrichment automation, and valid takeover remain deferred. Existing v1/v2 remain unchanged; no v3 relabel, migration, or backfill. Source flags remain false.
- **Prompts/models:** hosted `kicktippai/bundesliga-2026-27/predict-one-match` v3 and `kicktippai/bundesliga-2026-27/predict-bonus` v1 require `production`; checked-in fallback is `bundesliga-2026-27`. Validation only `gpt-5.6-luna`/`none`/`10000`; ADR-0052 controls production/challenger identity. Flex-first and one Standard fallback remain fixed.
- **Costs/communities:** The authoritative [community matrix](../../../../docs/onboarding-bundesliga-2026-27/community-onboarding.md) supplies posting-target credentials, copy compatibility, Langfuse environment, secrets/variables, membership and model rows. Estimate 306/493 calls with `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 306,493 --model <model> --reasoning-effort <effort>`; on a missing base row stop for the `estimate-experiment-cost-skill` approval/base-row/`upsert-row` procedure. P0-06's USD 35 orientation is not a runtime gate.
- **Temporary versus final Schadensfresse route:** ADR-0062 restores the eight-pair recovery lane until ADR-0068's reviewed replacement condition: target-owned `schadensfresse` context then `pes-squad` source-compatible ordinary-match copy with `SCHADENSFRESSE_KICKTIPP_*` credentials. Compatible copy expects zero model calls and fails closed on source mismatch. It deliberately preserves the known target scoring debt; ADR-0058/0059/0060's target-primary route is a future atomic P1-10 contract, and P1-08 is superseded. Do not confuse recovery copy with final activation.
- **Archived P1-10 typed evidence:** Commit `b0fd6b6` records fixtures `1662323362` and `1662323366` as `bundesliga` / `1. Spieltag` / `regularTime90Minutes` in `schadensfresse-routing-seed.json`, canonical hash `81b1c6ab0a6ad3159fcafebcbf1e3525df2cdf8e1279369f2515f001176008e5`. This archival/full-PR evidence is absent from temporary recovery main and does not describe recovery runtime.
- **Validation:** dry-run full profile, then audit history; prove exact-nine fixtures and every required document before any authorized Luna/none/cap-10000 development plumbing write. Inspect final Firestore/Kicktipp and Langfuse competition, prompt/model/cap, document, usage, cost, tier and errors. Existing P0 evidence is historical and must not be replayed as this lane's validation.
- **Activation:** Existing recovery schedule/topology is unchanged: ADR-0053 cron, non-cancelling serial/default-success topology, manual-only leaves and no scheduled bonus; ADR-0062/0068 recovery remains until reviewed replacement. Owner separately controls live source acquisition, development persistence, unattended HTML reuse, GitHub artifact/issues, production writes/activation, rollback, restoration and completion evidence. Synthetic trusted-date fixtures prove mechanics only.

## Safe dry-run

Run these exact commands before collector topology or development-onboarding
work:

```powershell
dotnet run --project src/Orchestrator -- collect-context-dev --community ehonda-dev-buli-2627 --competition bundesliga-2026-27 --full-season --dry-run --verbose
dotnet run --project src/Orchestrator -- bundesliga-history audit --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27
```

They prove collector isolation only; they grant no source activation or
persistence authority. P0-14 proves collector order and WM26 isolation; P0-21
is historical live evidence and must not be replayed for this documentation.
This profile never calls WM26 collectors, `data/wm26/`, WM26 prompts,
communities or schedules; a future domestic season requires its own profile.
